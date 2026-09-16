---

description: "Task list for Monorepo with Shared Word Lists and a Conformance Corpus"
---

# Tasks: Monorepo with Shared Word Lists and a Conformance Corpus

**Input**: Design documents from `/specs/002-monorepo-conformance-corpus/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/corpus-format.md](contracts/corpus-format.md), [contracts/repository-and-tooling.md](contracts/repository-and-tooling.md), [quickstart.md](quickstart.md)

**Tests**: Included. The corpus runner is itself a test project, and the spec requires it (FR-011 to FR-013). The constitution requires every README example and bug fix to be pinned by a test.

**Organization**: Moving the files blocks every story, so the moves are the Foundational phase. After that:
- US1 builds the corpus and its runner.
- US2 proves the shared word lists.
- US3 finishes the monorepo with the version source, "nothing changed" proof, CI and docs.

US2 and US3 are independent of each other; both only need Foundational.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- All paths are relative to the repository root, `D:\Git\PersianTextGuard`

## Path Conventions

| Path | Contents |
| --- | --- |
| `dotnet/` | the .NET port: `src/`, `tests/`, `benchmarks/`, `tools/`, solution, build props |
| `wordlists/`, `conformance/`, `VERSION` | shared data at the repository root |
| `specs/002-monorepo-conformance-corpus/verification.md` | results of the one-time checks; created in T001 |

**Files that MUST NOT be modified** (Clarification 3, FR-023): every `*.cs` file under `dotnet/tests/PersianTextGuard.Tests/`. Moving them is allowed; editing their content is not.

**Commit discipline** (research R11): T004 is one commit that contains **only** `git mv` renames. No task edits file contents in that commit.

---

## Phase 1: Setup

**Purpose**: Confirm the starting point and fetch the published package to compare against.

- [X] T001 Confirm the branch is `002-monorepo-conformance-corpus` with a clean working tree (`git status --short` prints nothing). Run `dotnet test tests/PersianTextGuard.Tests`: all 1,029 tests pass on `net10.0`, `net8.0` and `net48`. Create `specs/002-monorepo-conformance-corpus/verification.md` with a heading "Baseline (before restructure)" and record the commit hash and the three test counts.
- [X] T002 [P] Download the published package: run `dotnet add package PersianTextGuard --version 1.2.0` in a throwaway console project at `artifacts/compare/published/`, which is under the git-ignored `artifacts/`. Copy the restored `persiantextguard.1.2.0.nupkg` from the NuGet cache (`~/.nuget/packages/persiantextguard/1.2.0/`) to `artifacts/compare/persiantextguard.1.2.0.nupkg`. Nothing under `artifacts/` is committed.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Move every file to the constitution's layout and make the build pass again. Every user story works on files at their new paths.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete and all 1,029 existing tests pass from `dotnet/`.

- [X] T003 Add a root `.gitattributes` containing exactly these two lines: `wordlists/*.txt text eol=lf` and `conformance/**/*.json text eol=lf`. Commit it on its own as "Pin LF line endings for shared data".
- [X] T004 Move `src/`, `tests/`, `benchmarks/`, `PersianTextGuard.slnx` and `Directory.Build.props` under `dotnet/`, and the word lists to `wordlists/`, with history — renames only, in one commit.
  1. `git mv src dotnet/src`
  2. `git mv tests dotnet/tests`
  3. `git mv benchmarks dotnet/benchmarks`
  4. `git mv PersianTextGuard.slnx dotnet/PersianTextGuard.slnx`
  5. `git mv Directory.Build.props dotnet/Directory.Build.props`
  6. `mkdir wordlists`, then `git mv` each of `dotnet/src/PersianTextGuard/WordLists/persian.txt`, `finglish.txt` and `english.txt` into `wordlists/`
  7. Remove the now-empty `dotnet/src/PersianTextGuard/WordLists/` directory

  Commit as "Move .NET port under dotnet/ and word lists to wordlists/ (renames only)". Verify with `git show --stat -M HEAD`: every entry is a rename with no content change (`{… => …}` with 0 insertions and 0 deletions). Record the output summary in `verification.md`.
- [X] T005 Fix the paths the build needs in `dotnet/src/PersianTextGuard/PersianTextGuard.csproj`:
  - change `<EmbeddedResource Include="WordLists\*.txt" LogicalName="PersianTextGuard.WordLists.%(Filename)%(Extension)" />` to `<EmbeddedResource Include="..\..\..\wordlists\*.txt" LogicalName="PersianTextGuard.WordLists.%(Filename)%(Extension)" />`, keeping the `LogicalName` identical so resource names stay `PersianTextGuard.WordLists.{name}.txt` (research R14);
  - change `..\..\README.md`, `..\..\icon.png` and `..\..\THIRD-PARTY-NOTICES.md` to `..\..\..\README.md`, `..\..\..\icon.png` and `..\..\..\THIRD-PARTY-NOTICES.md`.

  The `ProjectReference` paths in `dotnet/tests/PersianTextGuard.Tests/PersianTextGuard.Tests.csproj` and `dotnet/benchmarks/PersianTextGuard.Benchmarks/PersianTextGuard.Benchmarks.csproj`, and the project paths in `dotnet/PersianTextGuard.slnx`, stay unchanged: they are relative and moved together.
- [X] T006 Update `.github/workflows/ci.yml` so CI builds from the new location, keeping every job name unchanged:
  - `dotnet restore dotnet/PersianTextGuard.slnx`
  - `dotnet build dotnet/PersianTextGuard.slnx -c Release --no-restore`
  - `dotnet test dotnet/tests/PersianTextGuard.Tests -c Release --no-build`
  - the pack step packs `dotnet/src/PersianTextGuard`, still using `$VERSION_ARG`, which T033 replaces
  - the net48 job runs `dotnet test dotnet/tests/PersianTextGuard.Tests -c Release -f net48`
- [X] T007 Verify the build: `dotnet build dotnet/PersianTextGuard.slnx` succeeds with no warnings, and `dotnet test dotnet/tests/PersianTextGuard.Tests` passes 1,029 tests on all three targets. Commit T005 and T006 together as "Fix paths after moving the .NET port". Record the counts in `verification.md`.

**Checkpoint**: The layout matches the constitution, the build and all existing tests pass from `dotnet/`, and history is intact.

---

## Phase 3: User Story 1 - Behaviour defined once, in a form every language can check (Priority: P1) 🎯 MVP

**Goal**: A JSON conformance corpus in `conformance/` with at least 300 cases across the eight kinds, recorded from 1.2.0, that the .NET port passes on every target. A fill-in tool adds expectations for new cases and never overwrites existing ones.

**Independent Test**: `dotnet test dotnet/tests/PersianTextGuard.Conformance` passes every case on `net10.0`, `net8.0` and `net48`. The guard tests confirm the corpus is present, holds at least 300 unique cases with none pending and no unescaped invisible characters, and covers every existing `[InlineData]` input.

### Corpus scaffolding

- [X] T008 [P] [US1] Create `conformance/corpus.json` containing exactly `{ "formatVersion": 1, "recordedFrom": "1.2.0", "positionUnit": "codePoint" }`, pretty-printed with two-space indentation, LF, and a trailing newline.
- [X] T009 [P] [US1] Create `conformance/configurations.json` as an array of configuration objects, per data-model "Configuration".

  **Bundled configurations**:

  | Name | `wordLists` | `options` |
  | --- | --- | --- |
  | `default` | `{"bundled":"default"}` | — |
  | `all` | `{"bundled":"all"}` | — |
  | `slurs-only` | `{"bundled":["slur"]}` | — |
  | `slurs-and-harassment` | `{"bundled":["slur","harassment"]}` | — |
  | `no-folding` | default | `{"foldLookalikeCharacters":false}` |
  | `no-squeezing` | default | `{"squeezeRepeatedLetters":false}` |
  | `no-joining` | default | `{"joinSpacedLetters":false}` |

  **Custom `{"entries":[…]}` configurations**, each entry with `"category":"uncategorized"`:

  | Name | Entries | Options |
  | --- | --- | --- |
  | `custom-kesafat` | `كثافت` wholeWord | — |
  | `custom-damn` | `damn` wholeWord | — |
  | `custom-k0s-whole` | `k0s` wholeWord | — |
  | `custom-k0s-anywhere` | `k0s` anywhere | — |
  | `custom-shit` | `shit` wholeWord | — |
  | `custom-fuck-anywhere` | `fuck` anywhere | — |
  | `custom-ass` | `ass` wholeWord | — |
  | `custom-fuck-whole` | `fuck` wholeWord | — |
  | `custom-fuck-whole-no-squeezing` | `fuck` wholeWord | `{"squeezeRepeatedLetters":false}` |
  | `custom-fuck-whole-no-folding` | `fuck` wholeWord | `{"foldLookalikeCharacters":false}` |
  | `custom-fuck-whole-no-joining` | `fuck` wholeWord | `{"joinSpacedLetters":false}` |
  | `custom-x` | `x` wholeWord | — |
  | `custom-empty` | `[]` | — |
  | `custom-readme` | `اسپم` wholeWord; `casino` anywhere | — |

  Data-model rule: a configuration's `name` is "Unique. `default` is required."
- [X] T010 [P] [US1] Write `conformance/README.md` in English, covering:
  - what the corpus is: the specification of behaviour, per constitution Principle V;
  - the file layout (research R2);
  - one example of each case kind, copied from `specs/002-monorepo-conformance-corpus/contracts/corpus-format.md`;
  - the writing rules, verbatim from that contract: invisible-character escaping, no lone-surrogate escapes, lowerCamelCase names, evasion order `repeatedLetters`, `lookalikeCharacters`, `splitWord`, and pending cases;
  - how to add a case, and how to run `dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill` (default, `--check`, `--seed`);
  - the rule that the fill-in tool never overwrites an existing `expected`.

### Runner project and shared evaluation code

- [X] T011 [US1] Create `dotnet/tests/PersianTextGuard.Conformance/PersianTextGuard.Conformance.csproj`, mirroring `dotnet/tests/PersianTextGuard.Tests/PersianTextGuard.Tests.csproj`:
  - **Targets**: `<TargetFrameworks>net8.0;net10.0</TargetFrameworks>`, plus `net48` when `'$(OS)' == 'Windows_NT'`.
  - **Packages**: the same xUnit, test SDK and `Microsoft.NETFramework.ReferenceAssemblies` references and versions, `<IsPackable>false</IsPackable>`, `<Using Include="Xunit" />`, and `System.Text.Json` version `9.0.0` for `net48` only (in-box on the others).
  - **Project references**: `..\..\src\PersianTextGuard\PersianTextGuard.csproj`, and `..\PersianTextGuard.Tests\PersianTextGuard.Tests.csproj`, needed for the `[InlineData]` coverage guard.
  - **Corpus path attribute**: an `AssemblyAttribute` item of type `System.Reflection.AssemblyMetadataAttribute`, with `_Parameter1` = `ConformanceDirectory` and `_Parameter2` = `$([System.IO.Path]::GetFullPath('$(MSBuildThisFileDirectory)..\..\..\conformance'))`.

  Add the project to `dotnet/PersianTextGuard.slnx` under the `/tests/` folder.
- [X] T012 [US1] Create `dotnet/tests/PersianTextGuard.Conformance/Corpus.cs` with no xUnit dependency; T017 compiles the same file into the fill-in tool.
  - **Loading**: `Corpus.Load(string directory)` reads `corpus.json`, `configurations.json` and every `cases/*.json` with `System.Text.Json`. Every loading error — a missing directory, a file that does not parse, or an unknown case `kind` — MUST throw an exception that names the file.
  - **Case model**: represents every case kind and field from data-model.md. `CorpusCase` keeps `Id`, `Kind`, `File`, the raw `JsonObject`, and whether `expected` is absent (pending).
  - **Input building**: `BuildInput(JsonNode? input)` returns `string?`. JSON `null` becomes `null`; a string is itself; for `{ "build": [...] }` it concatenates parts: `{ "text" }` literal; `{ "repeat", "times" }` repeated (`times ≥ 1`); `{ "utf16": "XXXX" }` the single `char` parsed from four hex digits, lone surrogates included.
  - **Positions**: `CodePointsToUtf16(string text, int start, int length)` returns `(index, length)`. A valid surrogate pair counts as one code point and two units; a lone surrogate or any other unit counts as one of each (research R5). `Utf16ToCodePoints` is the inverse.
  - **Filters**: `BuildFilter(configuration)` maps `bundled` `"default"` to `WordList.PersianDefault`, `"all"` to `WordList.All`, and arrays to `WordList.Bundled(...)`. It maps `entries` to `BannedWord` values with `Category` set, and `options` to `ProfanityFilterOptions`, each defaulting to `true`. Filters are cached per configuration name.
  - **Evaluation**: `Evaluate(CorpusCase)` returns a `JsonObject` in exactly the `expected` shape for the case's kind. It runs the public PersianTextGuard API and converts every position to code points. `evasion` lists names in the order `repeatedLetters`, `lookalikeCharacters`, `splitWord`. The shape for each kind:
    - **Matching**: `censored` and `censoredWith` from `Censor(input)` and `Censor(input, mask)` for each `masks` entry; a missing input censors to `""`. Any output that contains a lone surrogate is written as a `build` object.
    - **Normalization**: steps `"comparison"`, `"standard"` or `"none"` map to the `PersianNormalization` presets; an array ORs the named flags; `"toPersianDigits"` and `"toAsciiDigits"` call those helpers.
    - **Word-list parsing**: `WordList.Parse`. A `FormatException` whose message starts `Line N:` becomes `{"error":{"kind":"unknown-category","line":N}}`.
    - **Category selection**: `count`, the first 5 and last 5 entries, and `rules`. `rules` is `["categoriesInSelection","bundledOrder"]`, plus `"noMild"` when the selection is `"default"`.
    - **Mask validation**: `Censor("kir", mask[0])` throwing `ArgumentException` gives `accepted: false`.
  - **Kind rules** (quoted from data-model.md): `ordinary` ⇒ `containsProfanity` is `false`, `firstMatch` is `null`, `matches` is empty, and `censored` and every value of `censoredWith` equal the built input (the missing input censors to `""`); `must-match` ⇒ `containsProfanity` is `true`; `robustness` ⇒ either outcome is allowed. `CheckKindRules` returns violations as strings.
  - **Comparison**: `Compare(JsonNode expected, JsonNode actual)` returns a list of `(path, expected, actual)` differences, comparing every field exactly.
  - **Display**: `ShowInvisible(string?)` escapes as `\uXXXX` every character in categories Cf, Cc, Zl and Zp, every whitespace other than U+0020, and lone surrogates.
- [X] T013 [US1] Create `dotnet/tests/PersianTextGuard.Conformance/CorpusGuardTests.cs`, with one `[Fact]` each (FR-013, research R12):
  - `The_corpus_directory_exists_and_loads`: reads the path from `AssemblyMetadataAttribute("ConformanceDirectory")` and calls `Corpus.Load`.
  - `The_format_version_is_understood`: `formatVersion == 1`.
  - `There_are_at_least_300_cases`.
  - `Case_ids_are_unique`: ids also match `^[a-z0-9]+(-[a-z0-9]+)*$`.
  - `No_case_is_pending`: the failure message lists pending ids and says "run dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill".
  - `No_file_contains_an_unescaped_invisible_character`: scans each raw file for characters in Cf, Cc other than LF, Zl, Zp, whitespace other than U+0020 and LF, and lone surrogates, and reports file, line and code point.
  - `Every_configuration_a_case_names_exists`.
  - `Every_existing_test_input_is_in_the_corpus` (FR-005, research R9): reflects over every public method of every type in the `PersianTextGuard.Tests` assembly that carries `Xunit.InlineDataAttribute`. For each row from `attribute.GetData(method)`, the **first** `string` argument, when there is one, must equal the built `input` of at least one case, or the `text` of a word-list-parsing case. It lists every missing input.
- [X] T014 [US1] Create `dotnet/tests/PersianTextGuard.Conformance/MatchingCaseTests.cs`: a `[Theory]` with `[MemberData(nameof(Ids), DisableDiscoveryEnumeration = false)]`, where `Ids` yields the ids (plain ASCII strings) of cases of kind `ordinary`, `must-match` and `robustness`. For each id:
  1. Load the case.
  2. Assert no kind-rule violations; the failure message says "kind rule".
  3. Compute `Corpus.Evaluate`.
  4. Assert `Corpus.Compare(expected, actual)` is empty.

  On failure, the message MUST contain the case id, the file name, `ShowInvisible(built input)`, and one line per difference: `path: expected … actual …` (FR-012). Add the same theory shape for the other kinds in T015 and T016.
- [X] T015 [P] [US1] Create `dotnet/tests/PersianTextGuard.Conformance/TextCaseTests.cs` with the same theory pattern and failure message as T014, for kinds `normalization` and `tokenization`.
- [X] T016 [P] [US1] Create `dotnet/tests/PersianTextGuard.Conformance/ListCaseTests.cs` with the same theory pattern and failure message as T014, for kinds `word-list-parsing`, `category-selection` and `mask-validation`.

### Fill-in tool

- [X] T017 [US1] Create `dotnet/tools/PersianTextGuard.CorpusFill/PersianTextGuard.CorpusFill.csproj`:
  - `<OutputType>Exe</OutputType>`, `<TargetFramework>net10.0</TargetFramework>`, `<IsPackable>false</IsPackable>`;
  - `ProjectReference` to `..\..\src\PersianTextGuard\PersianTextGuard.csproj` and `..\..\tests\PersianTextGuard.Tests\PersianTextGuard.Tests.csproj`, the latter for seeding by reflection;
  - `<Compile Include="..\..\tests\PersianTextGuard.Conformance\Corpus.cs" Link="Corpus.cs" />`.

  Add it to `dotnet/PersianTextGuard.slnx` under a new `/tools/` folder.
- [X] T018 [US1] Create `dotnet/tools/PersianTextGuard.CorpusFill/CaseWriter.cs`: writes a JSON value in the canonical form of research R8.
  - **Formatting**: two-space indentation; LF; trailing newline.
  - **Key order**: `id`, `kind`, `configuration`, `note`, `selection`, `mask`, `masks`, `steps`, `text`, `input`, `expected`, then any remaining keys in their existing order. Inside `expected`: `containsProfanity`, `firstMatch`, `matches`, `censored`, `censoredWith`, `output`, `tokens`, `entries`, `error`, `count`, `first`, `last`, `rules`, `accepted`. Inside a match: `entry`, `evasion`, `start`, `length`. Inside an entry: `text`, `mode`, `category`.
  - **Strings**: written by its own escaper, **not** System.Text.Json's encoder. `"` and `\` are escaped. Characters in Cf, Cc, Zl and Zp, and every whitespace other than U+0020, become `\uXXXX` with uppercase hex. Every other character, Persian included, is written literally.
  - **Lone surrogates**: must never reach the writer; `Evaluate` already turns them into `build` objects. The writer throws if it sees one.
  - **Tested by** a `[Fact]` in `dotnet/tests/PersianTextGuard.Conformance/CorpusGuardTests.cs`, `Canonical_writer_escapes_invisible_characters_and_keeps_persian_literal`, which compiles `CaseWriter.cs` via `<Compile Include="..\..\tools\PersianTextGuard.CorpusFill\CaseWriter.cs" Link="CaseWriter.cs" />` in the conformance project. It asserts that the string `"کی\u200Cر"` is written as `"کی\u200Cر"`, with the zero-width non-joiner as a six-character escape and the Persian letters literal.
- [X] T019 [US1] Create `dotnet/tools/PersianTextGuard.CorpusFill/Program.cs` implementing the contract in `specs/002-monorepo-conformance-corpus/contracts/repository-and-tooling.md` → "Fill-in tool".
  - **Finding the corpus**: walk up from `AppContext.BaseDirectory` until a directory containing both `VERSION` and `conformance/` is found. Otherwise print "Could not find the repository root (a directory with VERSION and conformance/)" and exit 2. Until T020 creates `VERSION`, also accept a directory containing `conformance/` and `dotnet/`.
  - **Default mode**:
    - Fill each pending case with `Evaluate`.
    - For each case with `expected`, print every `Compare` difference as `DISAGREES <id> <path>: expected <…> actual <…>`, never writing that case.
    - Rewrite only files that had pending cases, via `CaseWriter`.
    - Print "Filled N case(s); M disagreement(s)" and exit `1` if M > 0, else `0`.
  - **`--check`**: the same comparison, writes nothing.
  - **`--seed`**: runs the seeding defined in T021 (`Seeds.cs`), then default mode.
  - **Invariant**: an existing `expected` is never modified in any mode (Clarification 2, FR-010a).

### Seeding and recording the corpus

- [X] T020 [US1] Create the root `VERSION` file containing `1.2.0` and a trailing newline. Only the file is created here; the build reads it in US3 (T030). The fill-in tool uses it to locate the repository root.
- [X] T021 [US1] Create `dotnet/tools/PersianTextGuard.CorpusFill/Seeds.cs` for `--seed` (research R9). It appends pending cases to the files named below. The rules below apply to every source.
  - **Missing files**: if a named case file does not exist yet, create it containing `[]`; T008–T010 do not create `conformance/cases/`.
  - **No duplicates**, whatever the id (spec, Edge Cases, "Duplicate messages"). Before adding a case, skip it if the corpus already holds a case with the same **content key**:
    - matching kinds (`ordinary`, `must-match`, `robustness`): configuration, the built input (the JSON value, compared structurally), and `masks` sorted;
    - `normalization`: steps and input;
    - `tokenization`: input;
    - `word-list-parsing`: text;
    - `category-selection`: selection;
    - `mask-validation`: mask.

    An input used by several tests, or in both an `[InlineData]` and a `[Fact]` literal, becomes one case.
  - **Ids** match T013's pattern `^[a-z0-9]+(-[a-z0-9]+)*$` and never depend on the input text, which may be Persian, emoji or empty. The form is `<file-prefix>-<source>-<nnn>`:
    - `<file-prefix>` is the case file name without `.json` (e.g. `matching-persian`);
    - `<source>` is:
      - for `[InlineData]` seeds, the test method name in lowercase kebab-case (`Persian_suffixes_are_matched` → `persian-suffixes-are-matched`);
      - for explicit literals, the source test class name in kebab-case (`FindMatchesTests` → `find-matches-tests`);
      - for supplementary suite cases, `suite-` plus the suite category in kebab-case (`FA evasion` → `suite-fa-evasion`);
    - `<nnn>` is a three-digit counter per `<file-prefix>-<source>`, starting at the next number not already used (`001`, `002`, …).
  - **From `[InlineData]` by reflection** over the `PersianTextGuard.Tests` assembly, one case per distinct input string per configuration:
    - `DefaultListTests`:
      - `Ordinary_messages_pass` → `ordinary`/`default`, in `matching-persian.json` when the input contains an Arabic-script letter (U+0600–U+06FF), otherwise in `matching-english.json`;
      - `Plain_entries_match`, `Evasions_are_caught`, `Evasions_found_by_testing_1_0_1_are_caught`, `Persian_suffixes_are_matched`, `Spellings_missing_from_1_0_1_are_covered`, `Entries_from_the_persian_swear_words_dataset_match` and `Anywhere_entries_match_inside_longer_words` → `must-match`/`default`. The file is chosen by the **matched entry**, not by the input's script, because Finglish and English are both Latin letters and cannot be told apart by script. Run `FindMatch` under `default` and read `wordlists/persian.txt`, `wordlists/finglish.txt` and `wordlists/english.txt` with `WordList.Load`: the case goes to `matching-persian.json`, `matching-finglish.json` or `matching-english.json` for whichever list contains an entry with the same `Text` and `Mode`, checking Persian, then Finglish, then English;
      - `Additional_persian_entries_match` and `Web_sourced_finglish_entries_match` → `must-match`/`all`, in `matching-options.json`.
    - `FindMatchesTests`:
      - `Regions_cover_whole_words` and `Results_are_ordered_non_overlapping_and_inside_the_message` → `must-match`/`default`;
      - `Missing_or_blank_text_returns_an_empty_list` → `robustness`/`default`, in `robustness.json`.
    - `CensorTests`:
      - `Banned_words_are_replaced_by_the_mask`: the first argument → `must-match`/`default`;
      - `Clean_text_comes_back_as_the_same_instance`: `"كتاب‌هاي خوب"` → `ordinary`; `""` and `"   "` → `robustness`;
      - `Letters_digits_whitespace_controls_and_surrogates_are_refused_as_masks`: each char → `mask-validation`, in `mask-validation.json`, as `{"mask":"<char>"}` — for `'\uD83D'`, the JSON string `"\uD83D"` is **not** allowed, so skip it here; T022 adds it as `{"mask":{"build":[{"utf16":"D83D"}]}}`.
    - `ProfanityFilterTests`:
      - `The_match_says_which_evasion_was_undone`: the string argument → `must-match`/`custom-damn`, in `matching-options.json`;
      - `An_entry_saved_in_its_look_alike_spelling_matches_the_plain_one` → cases `"ye kos inja"` and `"ye k0s inja"` under both `custom-k0s-whole` and `custom-k0s-anywhere`.
    - `PersianNormalizerTests`:
      - `Look_alike_letters_from_other_scripts_fold_to_persian`, `Invisible_and_decorative_characters_are_removed_for_comparison` and `All_three_digit_ranges_fold_to_ascii`: the first argument → `normalization` with `steps: "comparison"`, in `normalization.json`.
    - `ReadmeExampleTests`:
      - `Ordinary_text_named_in_the_readme_passes` → `ordinary`/`default`.
  - **Explicit literals**, from `[Fact]` bodies:
    - **FindMatchesTests**, `must-match`/`default` unless noted:
      - `"you bitch, kos kesh"`, `"kir kir kir"`, `"sh1t and f u c k"`, `"ﻛﻴﺮ"`, `"motherfucker"`, `"جنده‌ها رو ببین"`, `"پدر سگ پدر"`, `"fuckfuck"`, `"this is kir"`
      - `"سلام، سفارشم کی میرسه؟"` → `ordinary`
      - `"sh1t"` under `no-folding` → `ordinary`
      - `{"build":[{"text":"hi "},{"utf16":"D83D"},{"text":" kir"}]}` → `robustness`
    - **CensorTests**:
      - `"k kos i kos r"` → `must-match`
      - `"kir"` with `masks: ["#"]` → `must-match`
      - `null` → `robustness`
    - **ProfanityFilterTests**:
      - `custom-kesafat`: `"چه کثافتی"` → `ordinary`; `"این کثافت"` → `must-match`
      - `custom-shit`: `"push it"` → `ordinary`; `"s h i t"` → `must-match`
      - `custom-fuck-anywhere`: `"clusterfuck"` → `must-match`
      - `custom-ass`: `"classic"` → `ordinary`
      - `"fuuuuck"` under `custom-fuck-whole` → `must-match`, and under `custom-fuck-whole-no-squeezing` → `ordinary`
      - `"f*ck"` under `custom-fuck-whole-no-folding` → `ordinary`
      - `"f u c k"` under `custom-fuck-whole-no-joining` → `ordinary`
      - `null` and `"   "` under `custom-x` → `robustness`
      - `"anything"` under `custom-empty` → `ordinary`
      - word-list parsing: `"# a comment\r\n\r\nwhole\n~anywhere\n  ~  spaced  \n"` and `"کیر\n~fuck\n"`
    - **ConsistencyTests** robustness inputs, `robustness`/`default`:
      - `{"build":[{"text":"hi "},{"utf16":"D83D"}]}`
      - `{"build":[{"utf16":"DE00"},{"text":" hi"}]}`
      - `{"build":[{"text":"kir "},{"utf16":"D83D"},{"text":" kos"}]}`
      - `{"build":[{"repeat":"سلام این یک متن معمولی است و هیچ مشکلی ندارد. hello this is fine. ","times":2000}]}`
      - the same with a final `{"text":" کیر"}` part
    - **DefaultListTests**:
      - `"این فیلم آشغال بود"` → `ordinary`/`default` and `must-match`/`all`
      - `"faggot"` → `must-match`/`slurs-only`; `"fuck"` → `ordinary`/`slurs-only`
      - `"nigger"`, `"کیر"`, `"kys"` → `must-match`/`default`
    - **ReadmeExampleTests**:
      - `"sh1iiit"` → `must-match`/`default`
      - `"kys"` → `must-match`/`slurs-and-harassment`; `"کیر"` → `ordinary`/`slurs-and-harassment`
      - `"این پیام اسپم است"` and `"onlinecasino.example"` → `must-match`/`custom-readme`
      - `"kir and motherfucker"` with `masks: ["#"]`
    - **Normalization**, in `normalization.json`:
      - `"كتاب‌هاي  ۱۲ ABC"` with steps `comparison` and `standard`
      - `"كتاب‌هاي"` with steps `["unifyLetters"]` and `"none"`
      - `"بازي"`, `"بازی"`, `"كمك"` and `"کمک"` with `comparison` — both the Arabic-keyboard and the Persian spellings, since `PersianNormalizerTests` compares each pair
      - `"سسسسلام"` and `"BOOOOOK"` with `comparison`
      - `"  سلام \t\n  دنیا  "` with `comparison`
      - `null` and `"   "` with `comparison`
      - `"2 ساعت پیش، KR1"` and `"2 ساعت پیش"` with `toPersianDigits`
      - `"۲ ساعت پیش، KR١"` and `"۱۴۰۴/۰۵/۱۴"` with `toAsciiDigits`
    - **Tokenization**, in `tokenization.json`: `"سلام، دنیا! خوبی؟"`, `null`, `"hello,fuck"`, `"کیر😂"`
    - **Category selection**, in `category-selection.json`: `"default"`, `"all"`, and each of `["profanity"]`, `["sexual"]`, `["insult"]`, `["slur"]`, `["harassment"]`, `["mild"]`, `["slur","harassment"]`
    - **Word-list parsing**: `"[nonsense]\nword\n"`, expecting an unknown-category error on line 1, and `"[insult]\n~x\ny\n"`
    - **Mask validation**: `"*"`, `"#"` → accepted
  - **Out of scope, on purpose**: `ProfanityFilterTests.Duplicate_spellings_become_one_entry` and `DefaultListTests.The_bundled_list_loads` check the filter's **entry count** and object identity. The constitution's list of capabilities (Principle V) does not include those, so they have no corpus case kind and stay .NET-only tests.
  - **Supplementary suite** (research R9), to reach the 300-case minimum (SC-002). The inline messages and literals above yield about 270–290 distinct cases, since the existing test files hold only 196 distinct `[InlineData]` inputs.
    - **Source**: copy the 305 `(Cat, Text, Bad, Note)` tuples from `D:\Git\PtgTest\Program.cs`, lines 12–332, verbatim into a `SupplementarySuite` array in `Seeds.cs`, so the repository no longer depends on that folder. They are valid C#, including the `\u200C`-style escapes.
    - **Kind and inclusion**: for each tuple, run `ContainsProfanity(Text)` under `default`.
      - Include it as `must-match` when `Bad` is `true` and the result is `true`.
      - Include it as `ordinary` when `Bad` is `false` and the result is `false`.
      - **Skip** it when 1.2.0 disagrees with `Bad` — known gaps such as `"جن ده"`, and judgment calls such as `"Kir Royale cocktail"` — so the corpus stays a record of 1.2.0 behaviour (FR-009) and never pins a known gap as expected.
    - **Other rules**: `Note`, when not empty, becomes the case `note`. Category `FA *` goes to `matching-persian.json`, `Finglish*` to `matching-finglish.json`, and `EN *` to `matching-english.json`. The content-key rule above removes tuples already covered by the existing tests.
    - **Record** the numbers included, skipped as disagreements, and skipped as duplicates in `verification.md` (T023).
- [X] T022 [US1] Add hand-written cases that seeding cannot produce, as pending cases (no `expected`), to `conformance/cases/matching-persian.json` and `conformance/cases/mask-validation.json`:
  - **Emoji before a match** (`matching-persian.json`, Story 1 scenario 2): `"😀 کیر"` → `must-match`/`default`. Its code-point `start` (2) differs from its UTF-16 index (3), which proves the conversion.
  - **Invisible characters inside words** (`matching-persian.json`): `"ک\u200Cی\u200Cر"`, `"ک\u200Bیر"`, `"کی\u00ADر"`, `"ج\u200Fنده"` → `must-match`, written with escapes.
  - **Lone-surrogate mask** (`mask-validation.json`): `{"mask":{"build":[{"utf16":"D83D"}]}}`.
- [X] T023 [US1] Seed and record the corpus in `conformance/cases/`.
  1. Run `dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill -- --seed`. It must exit `0` and print "Filled N case(s); 0 disagreement(s)".
     - **If the corpus holds fewer than 300 cases** after seeding, **stop and report** the count to the user. Do not invent cases to reach the number. The only permitted sources are the existing tests, the explicit literals and the supplementary suite in T021, and a shortfall means one of them was not fully seeded.
     - Otherwise continue.
  2. Then run `dotnet test dotnet/tests/PersianTextGuard.Conformance`: every test passes on `net10.0`, `net8.0` and `net48`, including all guard tests.
  3. Record in `verification.md`:
     - the case count per kind;
     - the supplementary suite's included, disagreement-skipped and duplicate-skipped counts (T021);
     - the three test counts.
  4. Review the generated files for readability: Persian literal, invisible characters escaped.
  5. Commit `conformance/`, `VERSION` and the new projects as "Add the conformance corpus, recorded from 1.2.0, with .NET runner and fill-in tool".
- [X] T024 [US1] Update `.github/workflows/ci.yml` so both jobs run the corpus:
  - the Linux job adds `dotnet test dotnet/tests/PersianTextGuard.Conformance -c Release --no-build` after the existing test step;
  - the Windows job adds `dotnet test dotnet/tests/PersianTextGuard.Conformance -c Release -f net48`.

  Keep job names unchanged.
- [X] T025 [US1] Check failure reporting against `conformance/cases/`, per quickstart §5 and §6, on scratch changes that are reverted afterwards:
  1. Add a new `ordinary` case with `expected` written by hand, and run the corpus: it passes. Then corrupt one `must-match` case's `expected.censored`, and set one `ordinary` case's `containsProfanity` to `true`. Run the corpus on `net10.0`: both fail in the same run; the messages show id, file, escaped input and field differences; the ordinary one says "kind rule".
  2. Rename `conformance/` temporarily: the guard test fails with "not found", not zero cases.
  3. Add a pending `"this is kir"` case: the corpus fails with the fill-in hint; running the tool fills only that case and file.
  4. Hand-edit an existing `start` to a wrong value, then run the tool: it prints `DISAGREES`, exits `1`, and leaves the case unchanged (`git diff` shows only the hand edit).

  Time steps 1 and 3 from opening the file to the corpus passing: each must be under 5 minutes (SC-005).

  Revert everything and record the outcomes and both elapsed times in `verification.md`.

**Checkpoint**: The corpus exists, .NET passes it on every target, CI runs it, and adding a case needs no code.

---

## Phase 4: User Story 2 - One copy of the word lists, shared by every package (Priority: P2)

**Goal**: The lists live once in `wordlists/`, the package embeds them, and the bundled entries are identical to 1.2.0.

**Independent Test**: An entry-by-entry comparison of every selection between the published 1.2.0 package and the new build shows 0 differences. Exactly one copy of each list exists, and a list edit is picked up after a rebuild.

- [X] T026 [P] [US2] Compare bundled entries using two throwaway console projects under `artifacts/compare/` (git-ignored). **Reuse** T002's `artifacts/compare/published/` project, which already references `PersianTextGuard 1.2.0`. Create only `artifacts/compare/current/`, with a `ProjectReference` to `dotnet/src/PersianTextGuard/PersianTextGuard.csproj`. Give both the same `Program.cs`, which writes to stdout:
  - for each of `WordList.All`, `WordList.PersianDefault`, and `WordList.Bundled(c)` for every `WordCategory` value, a header line `## <selection> <count>`, then one line per entry: `text<TAB>mode<TAB>category`, in order;
  - `typeof(WordList).Assembly.GetManifestResourceNames()`, sorted, under `## resources`.

  Run both with output redirected to `artifacts/compare/published.txt` and `artifacts/compare/current.txt`, then `git diff --no-index artifacts/compare/published.txt artifacts/compare/current.txt`. **Expected: no differences** (FR-016, SC-003). Record the command and "0 differences" in `verification.md`.
- [X] T027 [P] [US2] Verify there is exactly one copy of each list: `git ls-files | grep -E '(^|/)(persian|finglish|english)\.txt$'` prints exactly `wordlists/english.txt`, `wordlists/finglish.txt` and `wordlists/persian.txt` (FR-014, SC-004). Record in `verification.md`.
- [X] T028 [US2] Verify an edit to `wordlists/english.txt` is picked up (Story 2, scenario 2), then revert.
  1. Append `zzqqtestword` under `[insult]` in `wordlists/english.txt`.
  2. Rebuild, and run `artifacts/compare/current`. The `## all` section contains `zzqqtestword`.
  3. Run the corpus: at least the `category-selection` cases for `all` and `insult` fail (Story 2, scenario 4).

  Revert the edit, re-run the corpus to confirm it passes, and record in `verification.md`.
- [X] T029 [P] [US2] Update word-list path references in `README.md` and `THIRD-PARTY-NOTICES.md`:
  - in `README.md`, the three links `src/PersianTextGuard/WordLists/persian.txt`, `…/finglish.txt` and `…/english.txt` become `wordlists/persian.txt`, `wordlists/finglish.txt` and `wordlists/english.txt`;
  - in `THIRD-PARTY-NOTICES.md`, `src/PersianTextGuard/WordLists/` becomes `wordlists/`.

**Checkpoint**: Word lists are shared, identical to 1.2.0, and every reference points at `wordlists/`.

---

## Phase 5: User Story 3 - A monorepo that can hold every port, with nothing changing for .NET users (Priority: P3)

**Goal**: A single `VERSION` source, validation against 1.2.0, a CI tag check, package contents identical to 1.2.0, documentation of the new commands, and history verified.

**Independent Test**:
- Pack produces `PersianTextGuard.1.2.0.nupkg` from `VERSION` and passes validation against 1.2.0.
- Package contents match 1.2.0.
- `git log --follow` shows history from before the move for moved files.
- No reference to an old path remains.

- [X] T030 [US3] Make `VERSION` the only version source (FR-018, research R13):
  - add `<Version>$([System.IO.File]::ReadAllText('$(MSBuildThisFileDirectory)../VERSION').Trim())</Version>` inside the `PropertyGroup` of `dotnet/Directory.Build.props`;
  - delete `<Version>1.2.0</Version>` from `dotnet/src/PersianTextGuard/PersianTextGuard.csproj`;
  - in the same file, change `<PackageValidationBaselineVersion>1.1.0</PackageValidationBaselineVersion>` to `1.2.0` (FR-022, research R14).

  Verify `git grep -n "<Version>" -- '*.csproj' '*.props'` prints only the `Directory.Build.props` line.
- [X] T031 [US3] Verify the version source and compatibility by packing `dotnet/src/PersianTextGuard`:
  1. `dotnet pack dotnet/src/PersianTextGuard -c Release -o artifacts` produces `artifacts/PersianTextGuard.1.2.0.nupkg` with package validation against 1.2.0 and no errors.
  2. Temporarily set `VERSION` to `1.2.1` and pack: the file is `PersianTextGuard.1.2.1.nupkg` (Story 3, scenario 2). Revert `VERSION` to `1.2.0`.

  Record both in `verification.md`.
- [X] T032 [US3] Compare the contents of `artifacts/PersianTextGuard.1.2.0.nupkg` with the published 1.2.0 (research R14):
  1. List the entries of `artifacts/compare/persiantextguard.1.2.0.nupkg` and of `artifacts/PersianTextGuard.1.2.0.nupkg` with `unzip -l`, or `[System.IO.Compression.ZipFile]::OpenRead(...).Entries` in PowerShell.
  2. Diff the sorted entry names, ignoring `.nuspec`, `_rels/`, `package/services/metadata/` and `[Content_Types].xml`. **Expected: no differences.**

  Resource names were already compared in T026. Record in `verification.md`.
- [X] T033 [US3] Replace the CI version override with a check, in `.github/workflows/ci.yml`:
  - in the pack step, remove the `VERSION_ARG` lines and pack with `dotnet pack dotnet/src/PersianTextGuard -c Release --no-build -o artifacts`;
  - in the `Publish to NuGet` job, add a first step `actions/checkout@v5`;
  - then a step "Check tag matches VERSION" running `test "${GITHUB_REF_NAME#v}" = "$(tr -d '[:space:]' < VERSION)" || { echo "Tag $GITHUB_REF_NAME does not match VERSION $(cat VERSION)"; exit 1; }` before the push step.

  **Verify the check locally**, because pull request CI never runs the tag path. From the repository root, in Git Bash, run the step's exact command twice:
  - with `GITHUB_REF_NAME=v1.2.0`: it exits `0`;
  - with `GITHUB_REF_NAME=v9.9.9`: it exits `1` and prints `Tag v9.9.9 does not match VERSION 1.2.0`.

  For example: `GITHUB_REF_NAME=v9.9.9 bash -c 'test "${GITHUB_REF_NAME#v}" = "$(tr -d "[:space:]" < VERSION)" || { echo "Tag $GITHUB_REF_NAME does not match VERSION $(cat VERSION)"; exit 1; }'; echo "exit $?"`. Record both exit codes in `verification.md`.
- [X] T034 [P] [US3] Update `README.md` for the new layout (FR-020, FR-021), in English per the constitution's adoption clause:
  - in "Performance", change `dotnet run -c Release --project benchmarks/PersianTextGuard.Benchmarks` to `dotnet run -c Release --project dotnet/benchmarks/PersianTextGuard.Benchmarks -f net10.0`;
  - add a section "Development" before "Limitations" that shows the repository layout tree from `specs/002-monorepo-conformance-corpus/contracts/repository-and-tooling.md` → "Layout", and a table of the commands from that contract's "Commands" (build, existing tests, corpus, benchmarks, pack, fill-in tool);
  - that section states that `conformance/` is the specification every port must pass, and links to `conformance/README.md`.

  Every new command in the section must be one verified in T007, T023 or T031.
- [X] T035 [US3] Verify history and references, recording results in `specs/002-monorepo-conformance-corpus/verification.md`:
  1. `git log --follow --oneline -- dotnet/src/PersianTextGuard/ProfanityFilter.cs | tail -1` shows `889a667`, and `git log --follow --oneline -- wordlists/persian.txt | tail -1` shows `889a667` (FR-019, SC-007).
  2. `git grep -nE '(^|[^[:alnum:]_/])(src|tests|benchmarks)/PersianTextGuard' -- ':!specs/001-censor-find-matches' ':!specs/002-monorepo-conformance-corpus'` prints nothing (FR-020). The pattern only matches an old path that is **not** preceded by a letter, digit, `_` or `/`. The new `dotnet/src/PersianTextGuard`, which appears throughout the README, CI and these specs, therefore never matches, while an old link such as `(src/PersianTextGuard/WordLists/…)` or `` `src/PersianTextGuard` `` does. Spec 001 is excluded as a historical record, and spec 002 because it describes the move from the old paths.

  Record both in `verification.md`.

**Checkpoint**: One version source, validation against 1.2.0, package contents unchanged, CI checks tags against `VERSION`, and docs match the layout.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T036 Run the full matrix once more from a clean build: `dotnet build dotnet/PersianTextGuard.slnx`; `dotnet test dotnet/tests/PersianTextGuard.Tests` (1,029 on each of three targets); `dotnet test dotnet/tests/PersianTextGuard.Conformance` (all cases on three targets). Time the `net10.0` conformance run: under 30 seconds (SC-008). Record the counts and the time in `verification.md`.
- [X] T037 Walk through `specs/002-monorepo-conformance-corpus/quickstart.md` sections 1–8 and tick each expected outcome in `verification.md`, referencing the task that produced each result. Mark any outcome that cannot be checked locally, such as the tag check needing a pushed tag, as "verified in CI" with the job name.
- [ ] T038 Commit the remaining changes (T024, T029, T030–T034, `verification.md`) in logical commits. Push `002-monorepo-conformance-corpus`, open a pull request to `main` whose description summarizes the restructure and links `specs/002-monorepo-conformance-corpus/verification.md`, and confirm both CI jobs are green. The logs must show the conformance tests on `net8.0`, `net10.0` and `net48`.

  Then check how `main` is protected (FR-024): read `GET https://api.github.com/repos/AmirehsanK/PersianTextGuard/branches/main/protection` with the git credential used for pushing. Record in `verification.md` whether both `Build, test, pack` and `Test on .NET Framework 4.8 (netstandard2.0 build)` are **required** status checks.
  - If they are not, or if `main` has no protection (HTTP 404), **report it to the user** with the settings change needed (GitHub → Settings → Branches → add a rule for `main` requiring both checks).
  - Do **not** change repository settings yourself; that is an account-settings change the user must approve.

  Do not merge without the user's go-ahead.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: none. T002 can run alongside T001.
- **Foundational (Phase 2)**: depends on Setup; T003 → T004 → T005 → T006 → T007, in strict order. T004 must be a renames-only commit.
- **US1 (Phase 3)**: depends on Foundational.
- **US2 (Phase 4)**: depends on Foundational. T028's corpus check needs US1 done; T026, T027 and T029 do not.
- **US3 (Phase 5)**: depends on Foundational. T034's commands reference the corpus runner and fill-in tool, so run it after US1. T030 must precede T031 and T032.
- **Polish (Phase 6)**: depends on all three stories.

### Within User Story 1

- T008, T009 and T010 can run in parallel.
- T011 → T012 → T013 → T014, then T015 and T016 in parallel.
- T017 → T018 → T019. T018 also edits `CorpusGuardTests.cs`, so it runs after T013.
- T020 before T019 is used, since the tool needs `VERSION` to find the root.
- T021 → T022 → T023 → T024 → T025.

### Parallel Opportunities

- Setup: T001 ∥ T002.
- US1: T008 ∥ T009 ∥ T010; T015 ∥ T016.
- US2: T026 ∥ T027 ∥ T029.
- Across stories after US1: US2's T026, T027 and T029 can run while US3's T030–T033 run; different files, apart from `README.md`, where T029 and T034 touch different lines and should be committed separately.

---

## Parallel Example: User Story 1

```bash
# Corpus scaffolding together:
Task: "Create conformance/corpus.json (T008)"
Task: "Create conformance/configurations.json (T009)"
Task: "Write conformance/README.md (T010)"

# After T014, the remaining case-kind runners together:
Task: "TextCaseTests.cs for normalization and tokenization (T015)"
Task: "ListCaseTests.cs for word-list parsing, category selection and masks (T016)"
```

---

## Implementation Strategy

### MVP First (Foundational + User Story 1)

1. Setup, then Foundational: files moved, build green, 1,029 tests pass.
2. US1: corpus recorded from 1.2.0 and passing on every .NET target; CI runs it.
3. **Stop and validate** with quickstart §4–§6. The corpus is now usable by the JavaScript/TypeScript port feature.

### Incremental Delivery

1. Foundational: monorepo layout.
2. \+ US1: the corpus, which unblocks every port.
3. \+ US2: proof that the shared lists equal 1.2.0.
4. \+ US3: single version source, 1.2.0 validation, CI tag check, docs.
5. Polish: full verification and the PR.

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks.
- Never edit `.cs` files under `dotnet/tests/PersianTextGuard.Tests/` (Clarification 3).
- The fill-in tool never modifies an existing `expected` (Clarification 2); a disagreement is fixed by a person, on purpose.
- Every recorded field is compared exactly (Clarification 1).
- Scratch verifications (T025, T028, T031's `1.2.1`) are always reverted before committing.
