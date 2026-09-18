# Research: JavaScript/TypeScript Port Published to npm

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Date**: 2026-09-16

The shape of the port is largely fixed by the constitution (v2.0.0, Principles III–V):
- one package, authored in TypeScript, shipping ES modules, CommonJS and type declarations;
- no runtime dependencies;
- Node.js active and maintenance LTS, plus browsers;
- positions in UTF-16 code units;
- tinybench for benchmarks and API Extractor for API compatibility;
- lockstep releases from `VERSION`.

This research settles what the constitution leaves open. Where a decision depends on how a runtime
actually behaves, it was measured on this machine first, as in feature 002.

---

## R1. Unicode behaviour: .NET versus JavaScript, measured

**Method.** Every one of the 1,112,064 Unicode scalar values, and every one of the 65,536 UTF-16 code
units, was run through the five primitives the .NET matcher relies on:
- the general category (`CharUnicodeInfo.GetUnicodeCategory`, per unit and per code point);
- `char.IsWhiteSpace`;
- `char.ToLowerInvariant`;
- NFKC normalization;
- NFD normalization.

The same was done in Node.js with the candidate JavaScript equivalents, and the outputs were diffed.
Runtimes: .NET 10.0.11 on Windows 11, and Node.js v26.4.0 (Unicode 17.0, ICU 78.3). Node.js 22 and 24,
the supported releases, are not installed here; CI covers them (R13).

**Findings.**

| Primitive | JavaScript candidate | Differences | Nature |
| --- | --- | --- | --- |
| Whitespace | `/\s/` | 2 | `\s` omits U+0085 (NEL), which .NET counts, and includes U+FEFF (BOM), which .NET does not. |
| Whitespace | explicit set: `\p{Zs}`, `\p{Zl}`, `\p{Zp}`, `\t\n\v\f\r`, U+0085, U+00A0 | **0** | Identical on all 65,536 units. |
| Lower-casing | `c.toLowerCase()` on one unit | 9 | U+0130 `İ` becomes two units (`i` + U+0307) in JavaScript; .NET leaves it **unchanged**. The other 8 are case pairs new in Unicode 17. |
| Lower-casing | `s.toLowerCase()` on a whole string | more | Also applies the context-sensitive final-sigma rule (`ΑΣ` → `ας`); .NET maps `Σ` to `σ` everywhere. |
| Category per UTF-16 unit | `/^\p{gc=Xx}$/u` on `String.fromCharCode(u)` | 63 | 62 are unassigned in .NET 10 and assigned in Unicode 17; U+0295 was re-categorised from Ll to Lo in Unicode 17. A lone surrogate tests as `Cs`, as in .NET. |
| Category per code point | same, on `String.fromCodePoint(cp)` | 4,804 | All but U+0295 are code points that .NET 10's tables leave unassigned and Unicode 17 assigns. |
| NFKC | `s.normalize('NFKC')` | 38 | 36 are U+1CCD6–U+1CCF9 (Unicode 16 outlined letters and digits), which .NET 10 categorises but does not fold: on this machine its normalization data is older than its category tables. |
| NFD | `s.normalize('NFD')` | 21 | Decompositions added in Unicode 16/17 (Garay, Tulu-Tigalari, Kirat Rai scripts). |
| Lone surrogates | `normalize` | — | JavaScript keeps them; .NET's `string.Normalize` throws, so the .NET normalizer replaces them with U+FFFD first. |
| Noncharacters | `normalize` | — | JavaScript keeps them. **.NET throws** (R2). |

**Decision.** The port uses the JavaScript engine's own Unicode data (`\p{…}` property escapes and
`String.prototype.normalize`), wrapped in one internal module (`unicode.ts`) that reproduces .NET's
semantics where they differ by design:
- **Whitespace**: the explicit set above, never `\s`.
- **Lower-casing**: one UTF-16 unit at a time. A result longer than one unit, which in practice means
  U+0130, leaves the unit unchanged, exactly as .NET does. Never lower-case a whole string.
- **Categories**: per UTF-16 unit where .NET tests a `char`, per code point where .NET uses
  `GetUnicodeCategory(string, index)`.
- **NFKC**: lone surrogates are replaced with U+FFFD first, as .NET does. Normalization runs between
  noncharacters, which pass through unchanged (R2).

**Differences in newly assigned characters are accepted.** They affect only characters assigned in
Unicode 16 or later. .NET itself already disagrees across its own targets here, because .NET Framework
4.8 uses Windows' older tables. Pinning one Unicode version would mean shipping generated tables, adding
size and a second copy of data the engine already has. The corpus is the arbiter:
- No existing corpus case uses such a character, which the runner's success on both sides confirms.
- The port README's Limitations section documents the gap.
- A difference that ever matters in practice becomes a corpus case first (spec, Edge Cases).

**Alternatives considered.**
- *Generated Unicode tables pinned to one version*: deterministic across engines, but .NET would still
  differ by target. It also costs roughly 50–100 KB, and the tables must be regenerated with each
  Unicode release.
- *`\s` and whole-string `toLowerCase`*: simpler, but measurably different from .NET on U+0085, U+FEFF,
  U+0130 and final sigma, all of which can appear in real messages.

---

## R2. PersianTextGuard 1.2.0 throws on Unicode noncharacters (fixed in this feature)

**Finding.** R1's NFKC dump showed .NET 10 throwing `ArgumentException` for U+FFFE. Probing the package
through its public API, with the message `"hi " + noncharacter + " kir"`, gave:

| Target | Noncharacters that throw (of 66) | Methods that throw |
| --- | --- | --- |
| `net8.0` | 1: U+FFFE | `PersianNormalizer.Normalize`, `ContainsProfanity`, `FindMatch`, `FindMatches`, `Censor` |
| `net10.0` | 1: U+FFFE | same |
| `net48` (the `netstandard2.0` build) | 66: all of them | same |

Unassigned code points, private-use characters and emoji from Unicode 11 to 16 did **not** throw on any
target. This violates Principle II: "User Input Never Throws". The user chose to fix it in this feature
(spec Clarifications, FR-028).

**Decision.** `PersianNormalizer` applies NFKC to the text **between** noncharacters and copies each
noncharacter through unchanged. The same applies to the segment-by-segment path used for source maps.
- **Why this changes nothing that works today.** A noncharacter has canonical combining class 0, has no
  decomposition and composes with nothing. It is a starter that blocks composition on both sides, so
  NFKC of `a + X + b` equals NFKC(`a`) + `X` + NFKC(`b`). Every input that did not throw in 1.2.0 on
  .NET 8/10 gives an identical result.
- **Why this is the right fix.** It is the output .NET 8/10 already produce for the 65 noncharacters
  they accept, so the fix defines U+FFFE, and every noncharacter on .NET Framework, as behaving like the
  rest.
- **Regression tests.** Corpus cases, recorded after the fix, cover each of these, in matching and
  normalization cases:
  - U+FFFE, U+FFFF, U+FDD0, U+1FFFE and U+10FFFF, alone;
  - between words, as `hi X kir`;
  - inside a banned word, as `کX یر` and `kXir`.

  They fail on 1.2.0 on every target (U+FFFE on all three, the rest on `net48`).
- **Recording.** The inputs are written as `build` objects with `utf16` parts for supplementary
  noncharacters, or as escapes (R3).
- **JavaScript** needs no workaround, since `normalize` never throws. The shared `unicode.ts` still splits
  at noncharacters, so both ports follow the same algorithm.

**Alternatives considered.**
- *Replace noncharacters with U+FFFD, as for lone surrogates*: this changes results .NET 8/10 give today.
  U+FFFD is an "other symbol", which the matcher treats as filler inside words, while a noncharacter is
  not. It would also contradict FR-028's "every input that did not throw gives the same result".
- *Catch the exception and skip normalization*: messages would then be checked without compatibility
  folding, so `ﻛﻴﺮ` plus a noncharacter would slip through.

---

## R3. Corpus writing rule for noncharacters

**Finding.** The fill-in tool's escaper (`CaseWriter`) escapes categories Cf, Cc, Zl and Zp and non-space
whitespace. Noncharacters are category Cn, so a censored output such as `"hi ￾ ****"` would be
written literally. That puts an invisible character, one many editors mishandle, into a corpus file.
`JSON.stringify` also leaves U+FFFE literal (checked in Node).

**Decision.** Writing rule 1 of the corpus format gains noncharacters. `CaseWriter` escapes them as
`\uXXXX`, with a supplementary noncharacter as its two surrogate escapes (valid JSON, since the pair is
well-formed), and the .NET guard test rejects them unescaped. This is an additive tightening of the
writing rules: every existing file still complies, and `formatVersion` stays `1` (corpus-format
contract, "Compatibility of the format").

---

## R4. Build: one TypeScript source, two module formats, two declaration flavours

**Decision.**
- **Compiler**: TypeScript in strict mode. Output targets ES2020, which is supported by every browser
  version in scope and by Node.js 22.
- **Bundling**: **tsup** (esbuild-based) builds `dist/index.mjs` (ESM) and `dist/index.cjs` (CommonJS)
  from `src/index.ts`, with `dist/index.d.mts` and `dist/index.d.cts` declarations.
- **Exports map**: `package.json` `exports` gives `import` and `require` their own `types`, with
  `"sideEffects": false`.
- **Checks in CI** on the packed tarball:
  - **publint**, for package-structure errors;
  - **`@arethetypeswrong/cli`** (attw), for declaration resolution under `node16` ESM, `node16` CJS and
    `bundler`.

**Rationale.**
- Separate `.d.mts` and `.d.cts` files are what TypeScript needs to type `import` and `require` correctly
  under `moduleResolution: node16/nodenext`. A single `.d.ts` shared by both formats is the most common
  dual-package mistake, and attw exists to catch it.
- Both formats are built from one bundle each, so there is no build-order coupling.
- The package has no module-level mutable state beyond lazily parsed bundled lists (R6). The
  "dual-package hazard" (the ESM and CJS copies both loaded in one app) is harmless: entries and matches
  are plain frozen data that work across copies (spec edge case "Both module styles").

**Alternatives considered.**
- *Plain `tsc` twice*: needs extension rewriting and two `tsconfig`s, and still does not emit `.d.cts`
  cleanly.
- *ESM only*: the constitution requires CommonJS too.
- *Rollup with plugins*: more configuration for the same output.

---

## R5. Public API shape in TypeScript

The spec requires the same capabilities as .NET under JavaScript naming (FR-007–FR-013). Full contract:
[contracts/public-api.md](contracts/public-api.md).

**Decisions.**

| Topic | .NET | JavaScript/TypeScript | Why |
| --- | --- | --- | --- |
| Filter | `new ProfanityFilter(words, options)` | `new ProfanityFilter(words, options?)` | Same shape. `words` is any iterable of entries. |
| Mask | `Censor(text)`, `Censor(text, char)` | `censor(text, mask = '*')` | JavaScript has no `char` or overloads; the mask is a one-unit string. |
| Enums | `WordMatchMode`, `WordCategory` | string literal unions `'wholeWord' \| 'anywhere'`, `'profanity' \| …` | The same lowerCamelCase names the corpus uses. Type-safe in TypeScript (spec scenario 7), readable in logs and JSON, and needing no import in JavaScript. A frozen `WORD_CATEGORIES` array lists the categories for iteration. |
| Evasion | `[Flags] EvasionKind` | `readonly EvasionKind[]` in the order `repeatedLetters`, `lookalikeCharacters`, `splitWord`; empty means none | Bit flags are awkward in JavaScript, and the ordered array is exactly the corpus shape. |
| Normalization steps | `[Flags] PersianNormalization` | `'comparison' \| 'standard' \| 'none' \| readonly NormalizationStep[]` | Presets and step names as the corpus uses them. |
| Normalizer | static class `PersianNormalizer` | named functions `normalize`, `tokenize`, `toPersianDigits`, `toAsciiDigits` | Idiomatic, and lets a bundler drop what an app does not use. |
| Word lists | static class `WordList` | object `WordList` with `all`, `persianDefault`, `bundled(...categories)`, `parse(text)` | Same members. `all` and `persianDefault` are lazily parsed frozen arrays that return the same instance every time, as in .NET. |
| `WordList.Load(Stream)` | yes | **omitted** | JavaScript has no stream type common to Node.js and browsers. Callers read the file themselves and call `WordList.parse(text)`. The capability "build a filter from word lists" is fully covered; the name table documents it. |
| Match | `ProfanityMatch(Word, Evasion)` + `Index`, `Length` | frozen `{ word, evasion, index, length }` | Same fields. `word` is the filter's frozen copy of the entry, as given with defaults filled in, so mutating the caller's object later changes nothing (spec edge case). |
| Count | `Count` | `count` (read-only) | |

**Errors** (spec FR-013; everything else never throws):

| Situation | .NET | JavaScript |
| --- | --- | --- |
| A message or text argument is not a string, `null` or `undefined` | — (cannot happen) | `TypeError` |
| An invalid mask (not one UTF-16 unit, or a letter, digit, whitespace, control character or surrogate) | `ArgumentException` | `RangeError` for a bad one-unit string, `TypeError` for a non-string, both thrown before the text is looked at |
| `words` missing or not iterable | `ArgumentNullException` | `TypeError` |
| An unknown category in word-list text | `FormatException("Line N: …")` | `WordListFormatError` (extends `Error`), with a numeric `line` property |
| `WordList.parse` given something other than a string | `ArgumentNullException` | `TypeError` |

**Constitution note (Principle II).** Throwing `TypeError` for a non-string message was the user's
choice (spec Clarifications). A non-string is a programmer error at the call site, in the same class as
an invalid mask, which .NET already throws for at call time. Principle II's guarantee covers "any text
the language can represent", and a number or object is not text. Recorded in the plan's Constitution
Check.

---

## R6. Bundled word lists: embedded from `wordlists/` at build time

**Decision.** A build script (`js/scripts/generate-wordlists.mjs`) runs before tsup. It reads
`wordlists/persian.txt`, `finglish.txt` and `english.txt` and writes
`js/src/generated/wordlists.ts`, which exports the three texts as string constants in that order. The
generated file is git-ignored, so the repository still holds one copy of each list (FR-005). At run
time, `WordList.all` parses the three texts with the same `parse` function users call, once, on first
access. That mirrors .NET, which embeds the files as resources and parses them lazily.

**Rationale.**
- **Parsing at run time** keeps one parser for bundled and user lists, which is what the
  word-list-parsing corpus cases check.
- **Cost**: parsing 23 KB of text is well under a millisecond.
- **Size**: 23 KB in each module format, about 47 KB of the 1 MB budget (SC-004).
- **Tree-shaking**: since the texts are only referenced from `WordList`, an app that imports only
  `normalize` does not bundle them (verified by the browser consumer check, R12).

**Alternatives considered.**
- *Pre-parsed JSON*: a second representation of the lists that can drift from the parser.
- *Reading files at run time*: impossible in browsers, and it breaks bundling.
- *Committing the generated file*: a second copy, which FR-005 forbids.

---

## R7. Porting strategy for the matcher

**Decision.** A file-by-file port of `dotnet/src/PersianTextGuard`, about 2,200 lines of C#, keeping the
same algorithms and structure so the two can be reviewed side by side:

| .NET | TypeScript |
| --- | --- |
| `PersianNormalization.cs`, `PersianNormalizer.cs` | `src/normalizer.ts` |
| `SourceMap.cs` | `src/source-map.ts` |
| `ProfanityFilter.cs`, `.Scan.cs`, `.Regions.cs` | `src/filter.ts`, `src/scan.ts`, `src/regions.ts` |
| `BannedWord.cs`, `ProfanityMatch.cs`, `ProfanityFilterOptions.cs` | `src/types.ts` |
| `WordList.cs` | `src/word-list.ts` |
| — | `src/unicode.ts` (R1, R2) |

Code units are handled as numbers (`charCodeAt`) in hot loops. Strings are built with arrays and
`join`, and lookups use `Map` and `Set` keyed by string, which gives the same "whole-word entries looked
up by token" structure as .NET (Principle IV).

**Rationale.** The corpus pins observable behaviour, but not every internal decision (for example, list
order breaking ties between overlapping entries). A faithful port is the shortest path to 497 of 497,
and it makes future behaviour changes (constitution: "every port in the same pull request") mechanical.

**Alternatives considered.** *A fresh JavaScript-idiomatic design* (for example, one big RegExp):
different tie-breaking and evasion reporting would surface as corpus failures case by case, with no
reference to reason from.

**Category tests per code unit.** A property-escape regex test per character is too slow for the
per-message path. `unicode.ts` answers ASCII from constant tables, and caches every other unit's category
in a lazily filled `Uint8Array` of 65,536 entries, so each unit is classified by regex at most once per
process. The benchmarks (R11) confirm the per-message path meets SC-005.

---

## R8. Tests and the corpus runner

**Decision.** **Vitest**.
- **`js/test/corpus.test.ts`** is the corpus runner. It implements every runner obligation in the corpus
  format contract:
  - format version check, presence, at least 300 cases, unique ids, no pending cases;
  - kind rules before comparison;
  - exact comparison of every field, converting code points to UTF-16 (the same rule as the .NET
    `Corpus.cs`);
  - `build` inputs.

  It registers one test per case, named by id. A failure message shows the id, the file, the input with
  `\uXXXX` escapes, and `path: expected … actual …` per differing field. The corpus path is resolved from
  the repository root, found by walking up to the directory that has `VERSION` and `conformance/`, never
  from the working directory.
- **Unit tests** cover JavaScript-specific behaviour (FR-019):
  - `TypeError` for non-strings, and `undefined` handled like `null`;
  - mask and word-list errors;
  - immutability after construction, and frozen results;
  - `WordList` returning the same instances;
  - the U+0130, U+0085/U+FEFF and final-sigma cases from R1.
- **README examples** are pinned by a test that extracts every fenced `ts` block from `js/README.md` and
  runs it against the built package (Principle VI).
- **Consumer checks** run against the packed tarball (R12).

**Rationale.**
- Vitest runs TypeScript directly on both supported Node.js releases and reports a failing case with
  good diffs.
- `test.each`-style registration gives one reported result per case id, like xUnit theories in .NET.

**Alternatives considered.** *`node:test`*: no dependency, but it has no TypeScript support on Node.js 22
without an extra loader, and its diffs are poorer.

---

## R9. Package version from `VERSION`

**Decision.**
- The committed `js/package.json` holds `"version": "0.0.0-development"` and is `"private": false`.
- The only supported way to produce the tarball is `npm run pack`. It runs `js/scripts/pack.mjs`, which:
  - builds;
  - assembles a staging directory `js/.pack/` with `package.json`, its `version` set from `../VERSION`;
  - adds `dist/`, `README.md`, and copies of the root `LICENSE` and `THIRD-PARTY-NOTICES.md`;
  - runs `npm pack` there, producing `js/artifacts/persian-text-guard-<VERSION>.tgz`.
- CI and publishing use only that tarball.
- A test asserts the committed placeholder, so nobody "fixes" it into a second version source.

**Rationale.** npm reads the version only from `package.json`, so it must be written somewhere. Writing
it into a staging copy keeps `VERSION` the single source (Public API & Versioning) without a committed
duplicate that can drift. The files list is then explicit (FR-006).

**Alternatives considered.**
- *Commit the version and check it matches `VERSION` in CI*: two places to edit per release, even if
  guarded.
- *`npm version` in CI*: it mutates the checked-out file, and local packs would get the placeholder.

---

## R10. Publishing to npm with trusted publishing

**Facts** (npm documentation, "Trusted publishing with OIDC", read 2026-09-16):
- Requires npm CLI **11.5.1 or later** and Node.js 22 or later.
- The job needs the `id-token: write` permission.
- The npm CLI detects the GitHub Actions OIDC environment automatically.
- Provenance attestations are generated automatically; no `--provenance` flag is needed.

**Maintainer configuration** (done 2026-09-16):
- `persian-text-guard@0.0.1` was published as a name placeholder, owned by `darkerys`.
- Trusted publisher: GitHub Actions, `AmirehsanK/PersianTextGuard`, workflow `ci.yml`, environment
  `npm`, "Allow npm publish" checked.

**Decision.** A new job, "Publish to npm", in `.github/workflows/ci.yml`:
- **When**: on `v*` tags only, with `needs` on every build/test job for both ports (R13).
- **Settings**: `environment: npm`, `permissions: { contents: read, id-token: write }`.
- **Setup**: Node.js 24, then `npm install -g npm@^11.5.1` to guarantee the minimum.
- **Steps**:
  1. Check that the tag matches `VERSION`, reusing the NuGet job's check.
  2. Download the `npm-package` artifact built by the JavaScript job.
  3. `npm publish persian-text-guard-<VERSION>.tgz --access public`.
- **Maintainer steps before the first tag**:
  - create the GitHub environment `npm` (Settings → Environments), optionally with a required reviewer;
  - add the new required status checks to `main`'s protection (R13).

**Lockstep and partial failure.**
- **Both publish jobs depend on the same complete set of checks.** A failure anywhere before publishing
  publishes nothing (FR-020, SC-008).
- **The two registries cannot be published atomically.** If NuGet succeeds and npm fails at the push
  step itself, re-running the failed job is safe:
  - NuGet already uses `--skip-duplicate`;
  - the npm job first checks `npm view persian-text-guard@<VERSION> version` and skips publishing when
    that version already exists.

  This is documented in the release section of the root README.

---

## R11. Benchmarks

**Decision.** **tinybench** (named by the constitution), in `js/bench/filter.bench.ts`, mirroring the .NET
BenchmarkDotNet suite:
- a short clean message, a long clean message, and a message with evasions;
- normalizing a long message;
- building a filter from the bundled list;
- `findMatches` clean and dirty, and `censor` short and long;
- **plus one task with no .NET counterpart**: `containsProfanity` on the 132,000-character corpus
  message, the spec's "very long messages" edge case, which must stay under 100 ms.

It is run with `npm run bench` on Node.js 24. The port README's performance table names the machine and
Node.js version: an Intel Core i7-9700K, the same machine the .NET table names, confirmed on 2026-09-17.
SC-005 limits: under 50 ms to build, under 50 µs for a short clean message.

**.NET table (constitution Principle IV).** The noncharacter fix (R2) and the heading rule (R17) change
.NET code. Only R2 touches the per-message path (`Normalize`). The full .NET BenchmarkDotNet suite
therefore runs before and after on the same machine; the root README's .NET performance table is
refreshed from the "after" run, and the pull request shows both tables.

---

## R12. Consumer checks on the packed tarball

**Decision.** `js/consumers/` holds four minimal projects. CI installs the tarball into each (SC-007).
Each asserts the same four facts:
- `"ک.ی.ر"` is flagged;
- `"سلام، سفارشم کی میرسه؟"` is not;
- censoring `"kir and motherfucker"` gives `"**** and ****"`;
- the index of the match in `"😀 کیر"` is 3.

| Consumer | What it proves |
| --- | --- |
| `esm/` | `import { ProfanityFilter, WordList } from 'persian-text-guard'` works on Node.js. |
| `cjs/` | `require('persian-text-guard')` works and gives identical results. |
| `typescript/` | A TypeScript project with `moduleResolution: node16` and `strict` compiles with no `@types` package. It also has an `@ts-expect-error` line for an unknown option and an unknown category (spec scenario 7). |
| `browser/` | esbuild bundles an entry with `platform: 'browser'`, which fails on any Node.js built-in import. The bundle is then executed in a Node.js `vm` context that has no `require`, `process` or `Buffer`. A second entry that imports only `normalize` must produce a bundle without the word-list texts (R6). |

**Rationale.** Running the bundle in a bare `vm` context proves "no Node.js APIs" without a real browser
in CI. The engine is the same V8 as Chrome, and the only engine-specific surface is Unicode data (R1).

**Alternative considered.** *Playwright with real Chromium, Firefox and WebKit*: stronger evidence, but a
multi-hundred-megabyte CI dependency for a library with no DOM use. It is left as a possible future
addition.

**Consequence for Firefox and Safari.** Their engines (SpiderMonkey, JavaScriptCore) run the same
ES2020 code but carry their own Unicode data, so R1's version drift applies to them too. The spec's
Assumptions and the port README's Limitations say so: supported, not CI-tested, with the corpus as the
reference if someone reports a difference.

---

## R13. CI jobs

**Decision.** Existing jobs keep their names (FR-021). Added to `.github/workflows/ci.yml`:

| Job name | Runs on | Steps |
| --- | --- | --- |
| `JavaScript (Node 22)`, `JavaScript (Node 24)` (matrix) | ubuntu | `npm ci` in `js/`; lint and type check; build; unit tests; corpus; README examples; on Node.js 24 only: `npm run pack`, publint, attw, API Extractor report check, API compatibility against the previous release (R14), consumer checks, upload the `npm-package` artifact |
| `Publish to npm` | ubuntu | R10 |

- `Publish to NuGet` gains the JavaScript jobs in its `needs`, and `Publish to npm` needs both .NET
  jobs, so neither registry publishes unless every port is green (Public API & Versioning).
- Node.js versions follow the release schedule. Node.js 26 is added to the matrix when it enters LTS
  (October 2026), and 22 is removed at its end of life (spec Assumptions).
- **After merge**: `JavaScript (Node 22)` and `JavaScript (Node 24)` are added to `main`'s required
  status checks. The maintainer delegated this to the implementer on 2026-09-17.

---

## R14. API compatibility: API Extractor

**Decision.** Two checks, because the constitution requires comparison "against the previous release",
not only against whatever a pull request last committed.

1. **Report up to date.** API Extractor runs on the built declarations and writes the API report
   `js/etc/persian-text-guard.api.md`, which is committed. In CI it runs in non-local mode, so a
   difference between the built API and the committed report fails the build. A pull request that
   changes the public API must update the report, which makes the change visible in review.
2. **Compatible with the previous release.** `js/scripts/check-api-compat.mjs` (`npm run api:compat`):
   - Finds the previous release tag: the newest `v*` tag that is an ancestor of `HEAD` (`git describe
     --tags --abbrev=0 --match "v[0-9]*"`), excluding a tag that points at `HEAD` itself, so a release
     build compares with the release before it.
   - Reads that tag's report with `git show <tag>:js/etc/persian-text-guard.api.md`.
   - If the tag has no report, prints "No previous npm release report (first release); baseline only"
     and exits `0`. This is the case for `1.3.0`, since `v1.2.0` predates the port.
   - Otherwise it compares the report's declaration lines (from the fenced `ts` block). Every declaration
     line of the previous report must still appear unchanged in the new one. A removed or changed line is
     a breaking change, and fails unless `VERSION`'s major number is greater than the tag's. Added lines
     are allowed (MINOR).
   - CI checks out full history (`fetch-depth: 0`) for this step.

- **Baseline**: the report committed with `1.3.0` is the baseline for every later release (FR-022).
- **TSDoc**: API Extractor also validates TSDoc syntax on exported members, supporting FR-023.
- **Documentation coverage** (SC-006): enforced by the ESLint rule `jsdoc/require-jsdoc` on exported
  declarations. ESLint and the plugin are dev-only.

**Note.** Line-level comparison is deliberately conservative: reformatting a declaration counts as a
change and needs the report regenerated, and a real breaking change needs a MAJOR `VERSION`. That is the
same effect .NET package validation has against its baseline.

---

## R15. Version and release notes for 1.3.0

**Decision.**
- **In the pull request**: `VERSION` changes from `1.2.0` to `1.3.0`, a MINOR release (new supported
  language, plus two .NET fixes: R2 and R17).
- **.NET baseline**: .NET `PackageValidationBaselineVersion` stays `1.2.0`, since compatibility is always
  checked against the previous release. It moves to `1.3.0` after the release, in the next change that
  touches .NET.
- **Release notes**: a GitHub release for `v1.3.0` with English notes and a Persian summary
  (constitution, Development Workflow). They cover:
  - the new npm package, with install and quick-start links;
  - the noncharacter fix for .NET users, as a matching-behaviour change on inputs that used to throw;
  - the word-list heading rule (R17): numeric headings such as `[3]` are now unknown categories;
  - no other .NET behaviour change.
- **After the release**:
  - verify both registries (SC-010);
  - deprecate the placeholder with `npm deprecate persian-text-guard@0.0.1 "Placeholder; use 1.3.0 or later" --auth-type=web`.
    The implementer runs it, as delegated on 2026-09-17, and the owner approves the 2FA prompt in the
    browser.

---

## R16. Documentation

**Decision.**
- **`js/README.md`** is shown on npm. It contains, in this order:
  1. an English section: installation, quick start, the non-string `TypeError` note, the .NET-to-JavaScript
     name table, the performance table, Limitations (R1's Unicode-version note), and a link to the root
     README;
  2. a Persian section, in `<div dir="rtl">`, with installation and quick start (Principle VI; spec
     FR-024).

  Code blocks are shared, with bilingual comments.
- **TSDoc comments** in English on every export (FR-023).
- **Root `README.md`**:
  - the npm package is listed beside NuGet;
  - the Development section's layout and command table gain `js/` (FR-025);
  - a short "Releasing" section covers the lockstep tag and the re-run rule from R10;
  - the .NET performance table is refreshed (R11);
  - a note on the two 1.3.0 behaviour changes for .NET users (R2, R17), linking the release notes. A
    behaviour change must update the README (constitution, Development Workflow). The Persian half waits
    for the bilingual README feature, under the adoption clause.
- **`conformance/README.md`**: the writing rules mention noncharacters (R3), and the word-list format
  note says headings are category names (R17).
- **`js/README.md` Limitations**: R1's Unicode-version note, and R12's note that Firefox and Safari are
  supported but not CI-tested.

---

## R17. Word-list section headings: names only (decided 2026-09-17)

**Finding** (checked on .NET 10 and .NET Framework 4.8). `WordList.Parse` reads a heading with
`Enum.TryParse(name, ignoreCase: true, …)` and then `Enum.IsDefined`. Because `Enum.TryParse` also
accepts integers:
- `[3]` reads as `insult`, `[+4]` as `slur`, and `[ 03 ]` as `insult`;
- `[7]`, `[-1]`, `[0x3]` and `[Insult, Slur]` are rejected;
- names work in any case and with surrounding spaces (`[INSULT]`, `[ insult ]`).

Nothing documents numeric headings, the bundled lists do not use them, and no corpus case covered them.
So a port written from the documented format would have silently disagreed with .NET.

**Decision** (user, 2026-09-17). Headings are category names only, in every port.
- **.NET**: the parser accepts a heading only when the trimmed name consists of letters and matches a
  category name, ignoring case. Every other heading throws `FormatException("Line N: unknown word category '…'.")`,
  as unknown names do today.
- **Corpus** (`word-list-parsing.json`), recorded after the change:

  | Text | Expected |
  | --- | --- |
  | `"[3]\nword\n"` | error on line 1 |
  | `"[+4]\nword\n"` | error on line 1 |
  | `"[ 03 ]\nword\n"` | error on line 1 |
  | `"# list\n[ Insult ]\nword\n[SLUR]\n~other\n"` | entries `word` (insult, whole word), `other` (slur, anywhere) |

- **JavaScript**: `WordList.parse` compares the trimmed name, ignoring case, with `WORD_CATEGORIES` only.

**Rationale.** The file format is shared by every port and edited by hand. Category numbers are a .NET
implementation detail, and would have to be reproduced in every port. No known user writes them, and the
bundled lists never did. The change affects only word-list files with numeric headings, and is called out
in the release notes (R15).

---

## R18. Proving the release gates with a real CI dry run (SC-008)

**Decision.** After the publish jobs exist (tasks US3), run the real workflow on GitHub Actions against
throwaway tags, on a scratch branch whose publish **steps** cannot publish.
- **Scratch branch**: `dryrun/release-gates`, from the feature branch. Only two things change there:
  - in `Publish to NuGet`, the NuGet login step is removed and the push step becomes
    `ls -l artifacts/*.nupkg && echo "DRY RUN: would push"`;
  - in `Publish to npm`, the publish step's final command becomes `npm publish … --dry-run`, which
    neither authenticates nor uploads.

  Before any tag is pushed, a check confirms the branch's workflow contains no `dotnet nuget push`, no
  `NuGet/login` and no `npm publish` without `--dry-run`. The job graph (`needs`, `if`, environments, tag
  check) is unchanged, since that is what is under test.
- **Run 1: a failing port blocks both.** Add a temporary step `run: exit 1` named "DRY RUN: simulated
  failure" to the JavaScript job, and set `VERSION` to `0.0.0-dryrun.1`. Push tag `v0.0.0-dryrun.1`.
  - **Expected**: both JavaScript jobs fail; `Publish to NuGet` and `Publish to npm` are skipped. The
    .NET jobs may pass.
- **Run 2: all green, both publish.** Remove the failing step and set `VERSION` to `0.0.0-dryrun.2`. Push
  tag `v0.0.0-dryrun.2`.
  - **Expected**: every job succeeds.
  - **NuGet job**: lists `PersianTextGuard.0.0.0-dryrun.2.nupkg`.
  - **npm job**: its tag check passes; `npm view` finds no such version; `npm publish --dry-run` reports
    `persian-text-guard@0.0.0-dryrun.2` with the eight files.
- **Run 3: tag must match VERSION.** Push tag `v0.0.0-dryrun.3` on the same commit (`VERSION` still
  `…dryrun.2`).
  - **Expected**: both publish jobs fail at "Check tag matches VERSION".
- **Cleanup**: delete the three tags locally and on GitHub, and delete the branch. Confirm that
  `npm view persian-text-guard versions` lists only `0.0.1`, and that NuGet has no `0.0.0-dryrun`
  version (`https://api.nuget.org/v3-flatcontainer/persiantextguard/index.json`).

**Safety.** Tags matching `v*` are allowed by the `npm` environment's deployment rule, so the run
exercises it. No publish command in the scratch workflow can upload, and the checked-out commit's
workflow file is what GitHub runs for a tag. The run leaves no releases and no packages; only workflow
run history and environment deployment records remain.

**Alternative considered.** *Inspecting the workflow file only*: that was the original plan, and it does
not prove that GitHub evaluates `needs` and skips as expected, which is what SC-008 claims.

---

## R19. Running the corpus locally on the supported Node.js releases

**Finding.** This machine has Node.js 26 only. R1's measurements and local test runs would therefore
prove nothing about Node.js 22 and 24 until CI runs at the pull request.

**Decision.** Install **fnm** (`winget install Schniz.fnm`), then `fnm install 22` and `fnm install 24`
(official Node.js builds from nodejs.org). After the JavaScript corpus passes on the default Node.js,
run `fnm exec --using=22 npm run corpus` and `fnm exec --using=24 npm run corpus`. A failure there is a
Unicode-data difference (R1) and follows the same stop-and-report rule.
