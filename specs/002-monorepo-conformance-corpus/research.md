# Research: Monorepo with Shared Word Lists and a Conformance Corpus

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Date**: 2026-09-16

The stack is fixed: the existing .NET port, its test and benchmark projects, GitHub Actions, and the
constitution v2.0.0. The research below settles how the restructure and the corpus are built. Where
a decision rested on how a tool actually behaves, it was checked on this machine first.

---

## R1. Corpus data format

**Decision**: JSON, pretty-printed, UTF-8 without a byte-order mark, LF line endings.

**Finding that forces a spec amendment**: FR-001 said the format must be readable by "every supported
language … with its standard library". JSON is readable by the standard libraries of .NET 8/10,
JavaScript, Python and Go. It is **not** readable by Java's or Rust's standard libraries, and .NET
Framework 4.8 needs the System.Text.Json package. No format that can hold nested match lists and
arbitrary Persian text is readable by every standard library, short of inventing one.

**Resolution**: Amend FR-001 to require "a widely supported data format that every supported language
can read in its tests". This fits the constitution: Principle III allows test-only dependencies in
every port, and the corpus is only ever read by tests and the fill-in tool, never by a shipped
package.

**Rationale**:
- Every port's test tooling reads JSON.
- Diffs are line-based and reviewable.
- Persian text can be written literally.

**Alternatives considered**:
- *A custom line-based text format*: readable with every standard library, but every port would need
  a hand-written parser and escaping rules. That is a new source of divergence between ports, the
  very thing the corpus exists to prevent.
- *YAML*: friendlier to hand-edit, but no standard library reads it, parsers disagree on edge cases,
  and indentation-sensitive files are easy to break in a hand edit.
- *TOML*: no nested arrays of tables that stay readable at 300+ cases, and no standard-library
  parser either.

---

## R2. Corpus layout

**Decision**:

```text
conformance/
├── README.md               # the format, how to add a case, how to run the fill-in tool
├── corpus.json             # corpus metadata: format version, recorded-from release
├── configurations.json     # named filter configurations shared by cases
└── cases/
    ├── matching-persian.json
    ├── matching-finglish.json
    ├── matching-english.json
    ├── matching-options.json      # cases under non-default configurations
    ├── robustness.json            # missing value, invalid text, very long, blank
    ├── normalization.json
    ├── tokenization.json
    ├── word-list-parsing.json
    ├── category-selection.json
    └── mask-validation.json
```

**Rationale**:
- Files grouped by capability stay small enough to review, around 30–120 cases each.
- A contributor adding a Persian message edits one obvious file.
- Configurations are named once and referred to by name, so a case stays short (spec, Key Entities).

---

## R3. Writing invisible characters visibly (FR-007)

**Decision**: In corpus files, every character in the Unicode categories below MUST be written as a
`\uXXXX` JSON escape. All other characters are written literally, Persian included.

- Format (Cf): zero-width non-joiner and joiner, zero-width space, soft hyphen, direction marks and
  embeddings, word joiner, byte-order mark
- Control (Cc), apart from the escapes JSON already requires
- Line and paragraph separators (Zl, Zp)
- Every whitespace character other than the ASCII space

The .NET corpus runner has a test that fails if any corpus file contains one of these characters
unescaped.

**Rationale**:
- `‌` in a diff is unmistakable; an actual zero-width non-joiner is invisible in every editor and
  review tool.
- Escaping everything non-ASCII would make the Persian cases unreadable.
- The rule is mechanical, so the fill-in tool can write it and the runner can enforce it.

---

## R4. Inputs that cannot be written literally (FR-008)

**Finding**: JSON parsers disagree about a lone surrogate written as a `\uD83D` escape.

| Parser | Result | Source |
| --- | --- | --- |
| System.Text.Json | throws `InvalidOperationException`: "Cannot read incomplete UTF-16 JSON text as string with missing low surrogate" | tested on this machine, from a JSON file |
| Node.js `JSON.parse` | accepts it and keeps the lone surrogate (`d83d`) | tested on this machine |
| Go `encoding/json` | replaces it with U+FFFD | documented behaviour of `Unmarshal` |
| Rust `serde_json` | rejects it ("lone leading surrogate") | documented behaviour |

A corpus that wrote invalid text as JSON string escapes could not be read the same way by every port.

**Decision**: A case's `input` is one of:

- a JSON string, for ordinary text;
- `null`, for the language's missing value;
- an object `{ "build": [ …parts ] }` that tells the port how to assemble the text. A part is one of:
  - `{ "text": "…" }` — literal text;
  - `{ "repeat": "…", "times": N }` — text repeated N times (the 132,000-character message);
  - `{ "utf16": "D83D" }` — one UTF-16 code unit, which may be a lone surrogate.

Each port's runner decides whether it can build a given input. A port whose strings cannot hold a
part — Rust `&str` cannot hold a lone surrogate — reports that case as **not applicable** by name.
The runner prints every not-applicable case, so none is silently skipped. For .NET every case is
applicable.

**Alternatives considered**: *Base64 or hex-encoded bytes for every input*. It works everywhere, but
turns every Persian case into unreadable data, which defeats FR-007 and code review.

---

## R5. Positions in code points (FR-004)

**Decision**: A match's `start` and `length` count Unicode code points of the built input, where a
lone surrogate counts as one code point. The .NET runner converts before comparing: it walks the built
string, counting a valid surrogate pair as one code point and two UTF-16 units, and every other unit
as one of each.

**Rationale**: This is the constitution's rule (Principle V). Counting a lone surrogate as one code
point matches Python, whose strings hold lone surrogates as single code points, and keeps the
conversion total, so no input can have an undefined position.

---

## R6. Case shape and exactness (FR-002, Clarification 1)

**Decision**: Every field recorded in `expected` is compared exactly:

- `containsProfanity`
- `firstMatch`
- `matches`: every element's `entry`, `mode`, `category`, `evasion`, `start` and `length`
- `censored`
- every entry of `censoredWith`

`evasion` is a JSON array of names in a fixed canonical order: `repeatedLetters`,
`lookalikeCharacters`, `splitWord`. An empty array means none. Enum-like values use lowerCamelCase
names (`wholeWord`, `anywhere`, `insult`), so they are not tied to any one language's casing.

A case with no `expected` object is **pending**. The runner fails on pending cases, with a message
telling the maintainer to run the fill-in tool. An unfinished case can never pass as an empty success
(FR-013).

Full shapes: [contracts/corpus-format.md](contracts/corpus-format.md).

---

## R7. The fill-in tool (FR-010a, Clarification 2)

**Decision**: A console tool, `dotnet/tools/PersianTextGuard.CorpusFill`, run with
`dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill`, does the following:

1. Reads every corpus file.
2. For each **pending** case, computes `expected` from the .NET package in this repository and writes
   it.
3. For each case that already has `expected`, computes the .NET result and **reports** any
   difference: case id, field, expected and actual. It never writes to that case.
4. Rewrites only the files that contained pending cases, in the canonical form (R8).
5. Exits with 0 when nothing disagrees, and 1 when any case disagrees, so it can double as a local
   check.

**Rationale**: Clarification 2 as specified. Because existing expectations are never written, a
behaviour change — intended or not — always surfaces as a failing corpus test that has to be fixed on
purpose.

---

## R8. Canonical file formatting

**Decision**: The fill-in tool writes JSON in one stable form:

- two-space indentation;
- one case per array element;
- object keys in a fixed order (`id`, `kind`, `configuration`, `note`, `input`, `expected`, …);
- the escaping rules of R3;
- LF line endings and a trailing newline.

The initial corpus is generated in this form. Files that contain no pending case are never rewritten,
so hand-edited files keep their formatting and diffs stay minimal. Formatting is **not** enforced on
hand edits, which would add friction to FR-010. Only the invisible-character rule (R3) is enforced.

`System.Text.Json`'s default encoder escapes all non-ASCII characters (Persian would become `ک…`),
and its relaxed encoder still escapes some characters. The tool therefore writes string values with
its own small escaper implementing R3, not through the built-in encoder.

---

## R9. Seeding the initial corpus (FR-005, FR-009)

**Decision**: A one-off seeding step, which is a mode of the fill-in tool (`--seed`), builds the
pending cases:

- **Test data**: every `[InlineData]` string from the existing .NET test assembly, collected by
  reflection as `ConsistencyTests` already does. A message's kind comes from its test:
  - ordinary, for `Ordinary_messages_pass` and the clean-text theories;
  - must-match, for the evasion and entry theories;
  - robustness, for blank and invalid inputs.
- **Literals in test bodies**: messages written directly inside `[Fact]` methods, which reflection
  cannot see. These are listed explicitly in the seeding code, taken from `FindMatchesTests`,
  `CensorTests`, `ReadmeExampleTests`, `ProfanityFilterTests` and `ConsistencyTests`.
- **Other case kinds**: normalization, tokenization, word-list parsing, category selection and mask
  validation cases come from `PersianNormalizerTests`, `ProfanityFilterTests`, `DefaultListTests` and
  `CensorTests`, listed explicitly.

The seeded cases are then filled from the .NET package (R7). `corpus.json` records
`"recordedFrom": "1.2.0"` (FR-009).

A conformance test enforces FR-005 permanently: it reflects over the existing test assembly's
`[InlineData]` strings and fails if any is missing from the corpus inputs. `[Fact]` literals cannot be
checked this way; the seeding list in the tool is their record.

**Recorded from 1.2.0**: the seeding runs against the repository's .NET package after the moves but
before any behaviour change. Since the restructure changes no behaviour (FR-022), that equals 1.2.0's
behaviour. SC-009 and the API-compatibility check confirm it.

---

## R10. Category selection cases without copying the lists (FR-003)

**Finding**: Recording every entry of every selection would put about 1,250 entries into the corpus
several times over, and every word-list edit would rewrite hundreds of lines.

**Decision**: A category-selection case records, for a selection (`default`, `all`, or a set of
categories):

- the exact entry **count**;
- the exact **first five** and **last five** entries (text, mode, category), in order;
- **rules** that must hold for every entry: every entry's category is in the selection, `default`
  contains no `mild` entry, and entries keep their order from the bundled lists.

**Rationale**: Every port parses the same `wordlists/` files, and parsing is itself pinned by the
word-list-parsing cases. With identical parsed lists, count plus membership plus order determines a
selection exactly, which satisfies Clarification 1 without duplicating the lists. SC-003's
entry-by-entry comparison with 1.2.0 is a separate one-time .NET check (R14).

---

## R11. Moving files with their history (FR-019, SC-007)

**Decision**: The restructure is done in two commits:

1. **Moves only**: `git mv` every file to its new place, with no content changes. Every rename is
   100% similar, so `git log --follow` and GitHub's history view follow it.
2. **Fix-ups**: path references, project files, CI, README, notices, version source.

Word-list files are moved in commit 1 as well. `.gitattributes` gains `*.txt text eol=lf` for
`wordlists/` and `*.json text eol=lf` for `conformance/`, so the files read identically on every
checkout and in every port.

**Target layout**:

```text
VERSION                         # 1.2.0
wordlists/                      # persian.txt, finglish.txt, english.txt
conformance/                    # R2
dotnet/
├── PersianTextGuard.slnx
├── Directory.Build.props       # moved from the root; imports ../VERSION
├── src/PersianTextGuard/
├── tests/PersianTextGuard.Tests/          # unchanged tests (Clarification 3)
├── tests/PersianTextGuard.Conformance/    # new corpus runner (R12)
├── benchmarks/PersianTextGuard.Benchmarks/
└── tools/PersianTextGuard.CorpusFill/     # new fill-in tool (R7)
README.md, LICENSE, THIRD-PARTY-NOTICES.md, icon.png   # stay at the root
.github/workflows/ci.yml
```

`README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md` and `icon.png` stay at the root. They belong to the
whole project, and the package still packs them from there.

---

## R12. The .NET corpus runner (FR-011–FR-013)

**Decision**: A new xUnit project, `dotnet/tests/PersianTextGuard.Conformance`, targets the same three
frameworks as the existing tests, so every case runs on `net10.0`, `net8.0` and `net48`. It is a
separate project so the existing 1,029-test project stays untouched (Clarification 3, FR-023).

- **One theory per case kind**, with member data yielding case **ids** only. Ids are plain ASCII, so
  test discovery never serializes Persian text or invalid input, and each case appears as its own
  test result. xUnit reports every failing case in one run (FR-012).
- **Failure messages** include the case id, the file, the built input with invisible characters shown
  as `\uXXXX`, and a field-by-field expected-versus-actual listing.
- **Guards** (FR-013), each a test of its own:
  - the corpus directory resolves;
  - every file parses;
  - there are at least 300 cases;
  - ids are unique;
  - no case is pending;
  - no file contains an unescaped invisible character (R3);
  - every inline test message appears in the corpus (R9).
- **Finding the corpus**: the path is passed in at build time as an MSBuild property. The project
  writes `$(RepoRoot)conformance` into an assembly attribute, so the path never depends on the
  process's working directory.
- **JSON**: `System.Text.Json`, which is in-box on `net8.0` and `net10.0`, and a test-only package
  reference on `net48` (allowed by Principle III).

---

## R13. The single version source (FR-018)

**Decision**: A `VERSION` file at the repository root holds `1.2.0` and nothing else.
`dotnet/Directory.Build.props` reads it:

```xml
<Version>$([System.IO.File]::ReadAllText('$(MSBuildThisFileDirectory)../VERSION').Trim())</Version>
```

The `<Version>` element is removed from `PersianTextGuard.csproj`.

**CI on a tag** no longer overrides the version with `-p:Version=…`, which would be a second source.
Instead it **checks** that the tag equals `v` followed by `VERSION`, and fails the release if they
differ.

**Rationale**: One file every future ecosystem can read with a single line: npm, Python, Maven, Go
and Cargo release scripts all read a plain file. A mismatched tag fails loudly instead of publishing a
version nobody committed.

**Alternatives considered**:
- *Version in `Directory.Build.props`*: .NET-only; other ports would have to parse MSBuild XML.
- *Nerdbank.GitVersioning or MinVer*: derives versions from git history, which each future ecosystem
  would need its own integration for, and it adds a build dependency to every port.

---

## R14. Proving nothing changed for .NET users (FR-016, FR-022, SC-003, SC-009)

**Decision**: Three checks, each run once during implementation and recorded in
[quickstart.md](quickstart.md):

1. **API**: `PackageValidationBaselineVersion` moves from `1.1.0` to `1.2.0`. `dotnet pack` then fails
   on any public API difference from the published 1.2.0.
2. **Bundled entries**: a one-time comparison lists `WordList.All`, `WordList.PersianDefault` and every
   single-category selection from the published `PersianTextGuard 1.2.0` package and from the newly
   built one, and diffs them entry by entry in order. The expected difference is 0.
3. **Package contents**: the newly packed `.nupkg` is compared with the published 1.2.0 `.nupkg`, apart
   from version metadata. The same files are expected, including embedded resource names.

The embedded resource names stay `PersianTextGuard.WordLists.{name}.txt`: only the source path in
`EmbeddedResource Include` changes. `WordList.cs` is therefore not modified at all.

---

## R15. CI changes (FR-024)

**Decision**: Update `.github/workflows/ci.yml`:

- **Build**: restore, build and test from `dotnet/PersianTextGuard.slnx`. That now includes the
  conformance project, so the corpus runs on `net8.0` and `net10.0` in the Linux job, and on `net48`
  in the Windows job.
- **Pack**: `dotnet/src/PersianTextGuard`, with package validation against 1.2.0 (R14).
- **Tag version check** (R13) before publishing.
- **Path filters**: none; every change runs the whole pipeline, as today.
- **Publish job**: unchanged, apart from the artifact path.

The job names stay the same, so the branch protection rules and badges that reference them keep
working.
