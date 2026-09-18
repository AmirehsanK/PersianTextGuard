---

description: "Task list for the JavaScript/TypeScript port published to npm (release 1.3.0)"
---

# Tasks: JavaScript/TypeScript Port Published to npm

**Input**: Design documents from `/specs/003-javascript-typescript-port/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/public-api.md](contracts/public-api.md), [contracts/package-and-release.md](contracts/package-and-release.md), [quickstart.md](quickstart.md)

**Tests**: Included. The spec requires them:
- a corpus runner (FR-016, FR-017);
- JavaScript-specific tests run against the packed package (FR-019);
- a test for every README example (FR-024);
- a regression corpus case for the noncharacter bug (FR-028, constitution Principle VI).

**Organization**: Phases follow the spec's user stories:
- **Foundational**: the two .NET fixes (noncharacters never throw; word-list headings are names only) change the corpus that every later phase checks, and the
  Unicode layer, normalizer, fold helpers, source maps and word lists are shared by every story.
- **US1**: the filter and the package users install.
- **US2**: the corpus runner and proof of identical answers.
- **US3**: CI and publishing.
- **US4**: documentation, benchmarks and API compatibility.
- **Release**: the release phase runs last, because it is irreversible, and starts with a real CI dry run of the release gates on throwaway tags.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4)
- All paths are relative to the repository root, `D:\Git\PersianTextGuard`. Commands under `js/` run with
  `js/` as the working directory.

## Porting conventions (apply to every task that ports a .NET file)

- **Faithful port.** Keep the .NET algorithm, control flow, constants, tables and comments, so the two
  files can be reviewed side by side (research R7). Do not "improve" behaviour: the corpus is the
  specification.
- **Code units.** In hot loops, read UTF-16 units with `text.charCodeAt(i)`. A .NET `char` is a `number`
  in the range 0–0xFFFF, and a .NET `string` is a JS `string`.
- **Primitives.** Every Unicode-dependent call goes through `js/src/unicode.ts`, never a direct
  regex, `toLowerCase` or `normalize`:

  | .NET | `unicode.ts` |
  | --- | --- |
  | `char.IsWhiteSpace(c)` | `isWhiteSpace(unit)` |
  | `char.IsLetter(c)` | `isLetter(unit)` |
  | `char.IsLetterOrDigit(c)` | `isLetterOrDigit(unit)` |
  | `char.IsControl(c)` | `isControl(unit)` |
  | `char.IsSurrogate` / `IsHighSurrogate` / `IsLowSurrogate` | `isSurrogate` / `isHighSurrogate` / `isLowSurrogate` |
  | `CharUnicodeInfo.GetUnicodeCategory(char)` | `categoryOfUnit(unit)` |
  | `CharUnicodeInfo.GetUnicodeCategory(string, index)` | `categoryAt(text, index)` |
  | `char.ToLowerInvariant(c)` | `toLowerInvariant(unit)` |
  | `s.Normalize(NormalizationForm.FormKC)` | `nfkc(s)` |
  | `s.Normalize(NormalizationForm.FormD)` | `nfd(s)` |

  Categories are returned as the two-letter codes `'Lu' | 'Ll' | … | 'Cn'` (type `GeneralCategory`).
- **Collections.** `StringBuilder` → an array of strings or code units joined once. `List<int>` → a
  `number[]`. `Dictionary<string, …>` with `StringComparer.Ordinal` → `Map<string, …>`.
  `HashSet<string>` → `Set<string>`. Ordinal `StartsWith`/`EndsWith`/`IndexOf` → the same JS string
  methods, which are ordinal.
- **Records.** Internal record structs become plain `interface`s or `readonly` tuples; public ones follow
  [contracts/public-api.md](contracts/public-api.md).
- **Internals.** No exports except those in the public API contract. Internal helpers are exported
  between modules but not from `js/src/index.ts`.

---

## Phase 1: Setup

**Purpose**: Scaffold `js/` with pinned dev tools, and record the starting point.

- [X] T001 Confirm the branch is `003-javascript-typescript-port` and that `git status --short` shows only `specs/003-javascript-typescript-port/` (and the unrelated, untracked `graphify-out/`, which must never be committed). Run `dotnet test dotnet/tests/PersianTextGuard.Tests` and `dotnet test dotnet/tests/PersianTextGuard.Conformance`: 1,029 and 506 passed on each of `net8.0`, `net10.0` and `net48`. Create `specs/003-javascript-typescript-port/verification.md` with a heading "Baseline", the commit hash and those six counts. Also record `node --version` and `npm --version`, and note that CI covers Node.js 22 and 24.
- [X] T002 Commit the spec, plan and design documents on their own: `git add specs/003-javascript-typescript-port .specify/feature.json`, then commit as "Add spec, plan and design for the JavaScript/TypeScript port (spec 003)". Do not add `graphify-out/`.
- [X] T003 Append the JavaScript build outputs to the root `.gitignore`: `node_modules/`, `js/dist/`, `js/src/generated/`, `js/.pack/`, `js/artifacts/`, `js/temp/`, `js/consumers/*/node_modules/`, `js/consumers/*/package-lock.json`, `js/consumers/browser/out/`.
- [X] T004 Create `js/package.json`:
  - **Identity**: `"name": "persian-text-guard"`, `"version": "0.0.0-development"` (placeholder; research R9), `"description"` (the NuGet description adapted for JavaScript/TypeScript), `"license": "MIT"`, `"author": "Amirehsan Kohannasab"`.
  - **Keywords**: `persian`, `farsi`, `finglish`, `profanity`, `profanity-filter`, `moderation`, `normalization`, `arabic`, `text`, `rtl`, `typescript`.
  - **Links**: `"repository": { "type": "git", "url": "git+https://github.com/AmirehsanK/PersianTextGuard.git", "directory": "js" }`, `"homepage": "https://github.com/AmirehsanK/PersianTextGuard/tree/main/js#readme"`, `"bugs": "https://github.com/AmirehsanK/PersianTextGuard/issues"`.
  - **Package shape**: `"type": "module"`, `"sideEffects": false`, `"engines": { "node": ">=22" }`, `"main": "./dist/index.cjs"`, `"module": "./dist/index.mjs"`, `"types": "./dist/index.d.cts"`, and `"exports"` exactly as in [contracts/package-and-release.md](contracts/package-and-release.md) → "Package contents". There is no `dependencies` field.
  - **Scripts**:

    | Script | Command |
    | --- | --- |
    | `generate` | `node scripts/generate-wordlists.mjs` |
    | `build` | `npm run generate && tsup` |
    | `lint` | `npm run generate && eslint . && tsc --noEmit -p tsconfig.json && tsc --noEmit -p tsconfig.node.json` |
    | `test` | `npm run generate && vitest run --exclude test/corpus.test.ts --exclude test/readme.test.ts` |
    | `corpus` | `npm run generate && vitest run test/corpus.test.ts` |
    | `test:readme` | `npm run build && vitest run test/readme.test.ts` |
    | `test:all` | `npm run build && vitest run` |
    | `api` | `npm run build && api-extractor run` |
    | `api:compat` | `node scripts/check-api-compat.mjs` |
    | `pack` | `node scripts/pack.mjs` |
    | `check:package` | `node scripts/check-package.mjs` |
    | `check:consumers` | `node scripts/check-consumers.mjs` |
    | `bench` | `npm run build && node --import tsx bench/filter.bench.ts` |

  - **devDependencies**: install with `npm install --save-dev --save-exact` using the current stable versions of `typescript`, `tsup`, `vitest`, `eslint`, `@eslint/js`, `typescript-eslint`, `eslint-plugin-jsdoc`, `@microsoft/api-extractor`, `publint`, `@arethetypeswrong/cli`, `tinybench`, `tsx`, `esbuild` and `@types/node`. Commit the resulting `js/package-lock.json`.
- [X] T005 [P] Create two TypeScript configs:
  - **`js/tsconfig.json`**, for the library only:
    - **Compiler options**: `"strict": true`, `"target": "ES2020"`, `"lib": ["ES2020"]` (no DOM), `"types": []` (no Node.js types, so a Node.js-only API in the library fails the type check, FR-003), `"module": "ESNext"`, `"moduleResolution": "Bundler"`, `"declaration": true`, `"noEmit": true`, `"isolatedModules": true`, `"noUncheckedIndexedAccess": true`, `"exactOptionalPropertyTypes": true`, `"verbatimModuleSyntax": true`.
    - **`include`**: `["src"]` only.
  - **`js/tsconfig.node.json`**, for everything that runs on Node.js:
    - `"extends": "./tsconfig.json"`;
    - `"compilerOptions"`: `"types": ["node"]`, `"lib": ["ES2022"]`, `"module": "NodeNext"`, `"moduleResolution": "NodeNext"`, `"allowImportingTsExtensions": true` (tests import `../src/index.ts`), `"noEmit": true`;
    - **`include`**: `["test", "bench", "scripts", "vitest.config.ts", "tsup.config.ts", "eslint.config.js"]`, plus `"allowJs": true` and `"checkJs": false`, so the `.mjs` scripts are part of the project without being type-checked strictly.

  **Verify**: once T010 has run `npm ci`, a file under `test/` that imports `node:fs` and `../src/index.ts` type-checks with `tsc --noEmit -p tsconfig.node.json`, and `process.env` in `src/` fails `tsc --noEmit -p tsconfig.json`. Check both with a throwaway file, then delete it.
- [X] T006 [P] Create `js/tsup.config.ts`: entry `src/index.ts`; `format: ['esm', 'cjs']`; `dts: true`, emitting `dist/index.d.mts` and `dist/index.d.cts`; `target: 'es2020'`; `platform: 'neutral'`; `sourcemap: false`; `clean: true`; `treeshake: true`; `outExtension` giving `.mjs` for ESM and `.cjs` for CJS.
- [X] T007 [P] Create `js/vitest.config.ts` with `test.include: ['test/**/*.test.ts']`, `testTimeout: 60000` (the 132,000-character corpus cases), `reporters: ['default']`, and `pool: 'forks'`.
- [X] T008 [P] Create `js/eslint.config.js` (flat config):
  - the `@eslint/js` recommended rules and `typescript-eslint` `strictTypeChecked` for `src/**`;
  - `eslint-plugin-jsdoc` with `jsdoc/require-jsdoc`, which requires a doc comment on every exported function, class, method, interface, type alias and variable in `src/**` (FR-023, SC-006), and `jsdoc/check-tag-names` with TSDoc tags allowed;
  - `no-restricted-globals` for `process`, `Buffer`, `require`, `__dirname` and `global` in `src/**` (FR-003);
  - `no-restricted-syntax` in `src/**`, except `src/unicode.ts` (research R1), with these selectors, each with a message pointing to `unicode.ts`:
    - `CallExpression[callee.property.name=/^(toLowerCase|toUpperCase|toLocaleLowerCase|toLocaleUpperCase|normalize|trim|trimStart|trimEnd)$/]`;
    - `Literal[regex.pattern=/\\s|\\S/]`, which catches `\s` or `\S` in a regex literal;
    - `NewExpression[callee.name='RegExp'] > Literal[value=/\\s|\\S/]`;
  - **Lint self-check**: after T010, a throwaway `src/x.ts` containing `'A'.toLowerCase()` and `/\s/` makes `npm run lint` report both, then delete it;
  - ignore `dist/`, `temp/`, `src/generated/`, `consumers/` and `.pack/`.
- [X] T009 Create `js/scripts/generate-wordlists.mjs`:
  - **Root**: find the repository root by walking up from the script's directory to the first directory containing both `VERSION` and `wordlists/`; if none is found, exit `1` with "Could not find the repository root (VERSION and wordlists/)".
  - **Input**: read `wordlists/persian.txt`, `wordlists/finglish.txt` and `wordlists/english.txt` as UTF-8.
  - **Output**: write `js/src/generated/wordlists.ts` with a header comment "Generated from wordlists/ by scripts/generate-wordlists.mjs. Do not edit." and `export const BUNDLED_WORD_LISTS: readonly string[] = [<persian>, <finglish>, <english>];`, each text as a `JSON.stringify` string literal, in that order (research R6).
  - **Stable output**: write the file only when the content changed, so watch mode does not loop.
- [X] T010 Run `npm ci` and `npm run generate` in `js/`. Confirm `src/generated/wordlists.ts` exists and that `git status` does not list it. Commit T003–T010 as "Scaffold the JavaScript port in js/".

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**:
- Fix the 1.2.0 noncharacter crash in .NET, and make word-list headings names only, each with corpus cases that every port must then pass (FR-028, FR-029).
- Build the shared JavaScript layers every story uses: types, Unicode primitives, normalizer, source maps and word lists.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete. At the end, the .NET corpus passes on three targets with the new cases, and the JavaScript normalizer, word-list and source-map unit tests pass.

### .NET: noncharacters never throw, headings are names (FR-028, FR-029; research R2, R3, R17)

- [X] T011 Extend the corpus escaping rule to noncharacters (research R3). A noncharacter is a code point in U+FDD0–U+FDEF, or one whose low 16 bits are `FFFE` or `FFFF`. In UTF-16 that means a BMP unit U+FDD0–U+FDEF, U+FFFE or U+FFFF, or a surrogate pair decoding to U+nFFFE or U+nFFFF.
  - **`dotnet/tools/PersianTextGuard.CorpusFill/CaseWriter.cs`**: `NeedsEscape` also returns `true` for BMP noncharacters. In `AppendString`, a valid surrogate pair whose code point is a noncharacter is written as two `\uXXXX` escapes (uppercase hex) instead of literally.
  - **`dotnet/tests/PersianTextGuard.Conformance/Corpus.cs`**:
    - add `public static bool IsNoncharacter(int codePoint)`;
    - `IsInvisible(char)` returns `true` for BMP noncharacters;
    - `ShowInvisible` escapes both BMP noncharacters and pairs that form noncharacters.
  - **`dotnet/tests/PersianTextGuard.Conformance/CorpusGuardTests.cs`**:
    - `No_file_contains_an_unescaped_invisible_character` also reports a surrogate pair forming a noncharacter;
    - `Canonical_writer_escapes_invisible_characters_and_keeps_persian_literal` additionally asserts that `CaseWriter.Write(JsonValue.Create("a\uFFFEb\uD83F\uDFFE"))` equals `"\"a\\uFFFEb\\uD83F\\uDFFE\""`.
  - **`conformance/README.md`** → "Writing rules": rule 1 adds "Unicode noncharacters (U+FDD0–U+FDEF and every code point ending in FFFE or FFFF)"; mirror the sentence in `specs/002-monorepo-conformance-corpus/contracts/corpus-format.md` rule 1.

  Run `dotnet test dotnet/tests/PersianTextGuard.Conformance`: all 506 still pass on three targets, since no existing file contains a noncharacter.
- [X] T012 Add the regression cases as **pending** cases (no `expected`). Write every noncharacter as a JSON string escape: supplementary ones as their surrogate-pair escapes, e.g. `\uD83F\uDFFE`, which is a valid pair. Ids and inputs are exactly:
  - **`conformance/cases/robustness.json`**, each `"kind": "robustness"`, `"configuration": "default"`, with `"note": "PersianTextGuard 1.2.0 threw on noncharacters (U+FFFE on .NET 8/10, all of them on .NET Framework 4.8)."` on the first case:

    | Id | Input |
    | --- | --- |
    | `robustness-noncharacter-fffe-between-words` | `"hi \uFFFE kir"` |
    | `robustness-noncharacter-ffff-between-words` | `"hi \uFFFF kir"` |
    | `robustness-noncharacter-fdd0-between-words` | `"hi \uFDD0 kir"` |
    | `robustness-noncharacter-1fffe-between-words` | `"hi \uD83F\uDFFE kir"` |
    | `robustness-noncharacter-10ffff-between-words` | `"hi \uDBFF\uDFFF kir"` |
    | `robustness-noncharacter-fffe-alone` | `"\uFFFE"` |
    | `robustness-noncharacter-fffe-inside-persian-word` | `"ک\uFFFEیر"` |
    | `robustness-noncharacter-ffff-inside-latin-word` | `"k\uFFFFir"` |
    | `robustness-noncharacter-fdd0-with-presentation-forms` | `"ﻛﻴ\uFDD0ﺮ"` |

  - **`conformance/cases/normalization.json`**, each `"kind": "normalization"`:

    | Id | Steps | Input |
    | --- | --- | --- |
    | `normalization-noncharacter-fffe-comparison` | `"comparison"` | `"ﻛﻴﺮ \uFFFE BOOOK"` |
    | `normalization-noncharacter-fdd0-between-presentation-forms` | `"comparison"` | `"ﻛﻴ\uFDD0ﺮ"` |
    | `normalization-noncharacter-10ffff-standard` | `"standard"` | `"كتاب\uDBFF\uDFFF  ۱۲"` |

  Then run `dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill` and record in `verification.md`, under "Before the fixes", what the unfixed tool does. The expected result is an unhandled `ArgumentException` ("String contains invalid Unicode code points"): `Program.cs` catches only `InvalidDataException`, `FormatException` and `InvalidOperationException`. That crash is the observed failure before the fix. The regression proof that the **recorded** cases fail on 1.2.0 comes later, in T066. Do not commit a file changed by the crashed run; the tool writes only after evaluating every case, so none should be.
- [X] T013 Add the word-list heading cases (research R17) as **pending** cases to `conformance/cases/word-list-parsing.json`, each `"kind": "word-list-parsing"`, with `"note": "Headings are category names only; PersianTextGuard 1.2.0 also accepted category numbers."` on the first:

  | Id | Text |
  | --- | --- |
  | `word-list-parsing-heading-number` | `"[3]\nword\n"` |
  | `word-list-parsing-heading-signed-number` | `"[+4]\nword\n"` |
  | `word-list-parsing-heading-spaced-number` | `"[ 03 ]\nword\n"` |
  | `word-list-parsing-heading-name-spaced-and-upper-case` | `"# list\n[ Insult ]\nword\n[SLUR]\n~other\n"` |

  Do not fill them yet: the fill-in tool would record 1.2.0's acceptance of numbers. Also, in `conformance/README.md`, add a "Word-list format" note next to the `word-list-parsing` example: a heading is a category name in any letter case, and numbers are not categories.
- [X] T014 Record the .NET "before" benchmarks: run the full suite with `dotnet run -c Release --project dotnet/benchmarks/PersianTextGuard.Benchmarks -f net10.0 -- --filter "*"` on this machine. It is the Intel Core i7-9700K the README's .NET table names; confirm with `Get-CimInstance Win32_Processor`, and stop and report if it differs. Copy the Mean and Allocated columns of all nine benchmarks into `verification.md` under ".NET benchmarks → before" (constitution Principle IV).
- [X] T015 Fix `dotnet/src/PersianTextGuard/PersianNormalizer.cs` (research R2). Add `private static bool IsNoncharacterAt(string text, int index, out int length)`, which recognises BMP noncharacters (length 1) and surrogate pairs forming noncharacters (length 2). Then:
  1. **Whole-string path** (line 68): replace `ReplaceLoneSurrogates(text!).Normalize(NormalizationForm.FormKC)` with `NormalizeKeepingNoncharacters(ReplaceLoneSurrogates(text!))`. The new method walks the text, applies `Normalize(NormalizationForm.FormKC)` to each maximal run that contains no noncharacter (skipping empty runs), and appends each noncharacter's units unchanged. When the text contains no noncharacter, it returns `text.Normalize(NormalizationForm.FormKC)`, so the common path makes one call as before.
  2. **Segment path** (`NormalizeCompatibilityBySegment`):
     - **Fast path**: guard the `text.IsNormalized(NormalizationForm.FormKC)` fast path so it is only used when the text contains no noncharacter.
     - **Noncharacter segments**: when a segment's starter is a noncharacter, append the noncharacter's units unchanged (each mapped to `start`), then append `Normalize(NormalizationForm.FormKC)` of the remaining combining marks, if any, also mapped to `start`.
  3. **Comments**: add a comment at both sites explaining that `string.Normalize` throws on noncharacters (U+FFFE on .NET 8/10, all of them on .NET Framework), and that a noncharacter is a starter that composes with nothing, so normalizing around it gives the same result as normalizing through it where that does not throw.

  No other file changes. Build with `TreatWarningsAsErrors`: 0 warnings.
- [X] T016 Change `dotnet/src/PersianTextGuard/WordList.cs` so section headings are names only (research R17). In `Parse`, the section branch accepts the heading only when the trimmed `name` is non-empty, **every** character is a letter (`name.All(char.IsLetter)`), and `Enum.TryParse(name, ignoreCase: true, out category) && Enum.IsDefined(typeof(WordCategory), category)` holds. Otherwise it throws the existing `FormatException($"Line {lineNumber}: unknown word category '{name}'.")`.
  - **Docs**: update the XML doc remark on `WordList` to say "`[insult]`: a category name, in any letter case" and add `<exception>` wording that numbers are not category names.
  - **Tests**: run `dotnet test dotnet/tests/PersianTextGuard.Tests`. All 1,029 still pass on three targets, since no existing test or bundled list uses a numeric heading.
- [X] T017 Fill and verify:
  1. `dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill` prints "Filled 16 case(s); 0 disagreement(s)" and exits `0` (12 noncharacter cases and 4 heading cases).
  2. **Review the diff**:
     - every noncharacter in the recorded `expected` values is escaped, and `git grep -nP '[\x{FDD0}-\x{FDEF}\x{FFFE}\x{FFFF}]' -- conformance` prints nothing;
     - the three numeric-heading cases recorded `"error": { "kind": "unknown-category", "line": 1 }`;
     - the spaced and upper-case case recorded `word` as `insult` (whole word) and `other` as `slur` (anywhere).
  3. Run `dotnet test dotnet/tests/PersianTextGuard.Tests` (1,029 × 3) and `dotnet test dotnet/tests/PersianTextGuard.Conformance` (522 × 3, which is 513 cases + 9 guards). Every test passes on `net8.0`, `net10.0` and `net48`.
  4. `dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill -- --check` reports 0 disagreements.

  Record the counts in `verification.md`. If the fill-in count or test totals differ from these numbers because an id was added or removed, record the real numbers and why.
- [X] T018 Record the .NET "after" benchmarks with the same full-suite command as T014. Add them next to the "before" numbers in `verification.md`, with a before/after table (Operation, Mean before, Mean after, Allocated before, Allocated after). A regression in Mean or Allocated beyond noise (more than 5%) must be explained there.
  - **README**: refresh the root `README.md` "Performance" table for .NET with the "after" Mean and Allocated values. Keep its rows, units and the machine line (Intel Core i7-9700K, BenchmarkDotNet, .NET 10), as constitution Principle IV requires.
  - **Commit** everything from the start of this .NET track as "Never throw on Unicode noncharacters; word-list headings are names only (corpus cases, benchmarks)".

### JavaScript: shared layers

- [X] T019 [P] Create `js/src/types.ts` with every public type from [contracts/public-api.md](contracts/public-api.md): `WordMatchMode`, `WordCategory`, `WORD_CATEGORIES` (frozen, in the order `'uncategorized', 'profanity', 'sexual', 'insult', 'slur', 'harassment', 'mild'`), `BannedWord`, `ProfanityFilterOptions`, `EvasionKind`, `ProfanityMatch`, `NormalizationStep`, `NormalizationSteps`, and `class WordListFormatError extends Error`. The class has a `readonly line: number`, sets `name = 'WordListFormatError'`, and takes the message `` `Line ${line}: unknown word category '${name}'.` ``, matching .NET's message. Also add the internal type `ResolvedWord = Readonly<Required<BannedWord>>`. Every export gets a TSDoc comment explaining behaviour and edge cases (FR-023); adapt the .NET XML docs in `dotnet/src/PersianTextGuard/BannedWord.cs`, `ProfanityMatch.cs` and `ProfanityFilterOptions.cs`.
- [X] T020 [P] Create `js/src/unicode.ts`, which implements research R1 exactly:
  - `isWhiteSpace(unit)`: `true` for categories Zs, Zl, Zp, and for U+0009–U+000D, U+0085 and U+00A0. **Not** `\s`: U+FEFF is not whitespace.
  - `categoryOfUnit(unit)`: the general category of one UTF-16 unit, from `/^\p{gc=XX}$/u` tests on `String.fromCharCode(unit)`. A lone surrogate is `'Cs'`; no match is `'Cn'`. ASCII (0–127) is answered from a constant table. Other results are cached in a lazily allocated `Uint8Array(65536)`, where 0 means not computed.
  - `categoryAt(text, index)`: the category of the code point at `index`, using the pair when `index` starts a valid surrogate pair, else `categoryOfUnit`.
  - `isLetter`: category L*. `isLetterOrDigit`: L* or Nd. `isControl`: Cc. `isSurrogate`, `isHighSurrogate`, `isLowSurrogate`: numeric ranges.
  - `toLowerInvariant(unit)`: `String.fromCharCode(unit).toLowerCase()`. If the result is not exactly one unit, return `unit` unchanged (U+0130 stays U+0130). Never lower-case whole strings (final sigma).
  - `isNoncharacterAt(text, index)`: returns 0, 1 or 2, the length of a noncharacter starting at `index` (same definition as T011).
  - `nfkc(text)`: returns `text.normalize('NFKC')` applied to each maximal run without a noncharacter, with noncharacters copied through. Lone surrogates must already be replaced by the caller, as in .NET.
  - `nfd(text)`: `text.normalize('NFD')`.

  TSDoc on each function says which .NET API it reproduces.
- [X] T021 [P] Create `js/test/unicode.test.ts` (research R1 findings as tests):
  - **Whitespace**: `isWhiteSpace(0x85) === true`, `isWhiteSpace(0xFEFF) === false`, `isWhiteSpace(0xA0) === true`, `isWhiteSpace(0x200B) === false`.
  - **Lower-casing**: `toLowerInvariant(0x130) === 0x130`, `toLowerInvariant(0x3A3) === 0x3C3`, `toLowerInvariant(0x41) === 0x61`.
  - **Categories**: `categoryOfUnit(0xD83D) === 'Cs'`, `categoryOfUnit(0x0643) === 'Lo'`, `categoryOfUnit(0x064B) === 'Mn'`, `categoryAt('😀', 0) === 'So'`.
  - **NFKC**: `nfkc('ﻛﻴﺮ') === 'كير'`, `nfkc('a\uFFFEﻛ') === 'a\uFFFEك'`, and `nfkc` does not throw for all 66 noncharacters.
  - **Noncharacters**: `isNoncharacterAt('\uD83F\uDFFE', 0) === 2`.

  Run `npx vitest run test/unicode.test.ts`: it fails before T020 and passes after.
- [X] T022 Port `dotnet/src/PersianTextGuard/PersianNormalizer.cs` and `PersianNormalization.cs` to `js/src/normalizer.ts`, including the T015 fix and following the porting conventions.
  - **Internal step flags**: a `const enum`-free numeric bit set with the same bit values as .NET's `PersianNormalization`.
  - **`resolveSteps(steps: NormalizationSteps): number`**:
    - `'comparison'`, `'standard'` and `'none'` map to the preset values;
    - an array ORs the named steps;
    - an unknown preset or step name throws `TypeError`;
    - the default is `'comparison'`.
  - **Internal ports**: `normalizeWithMap(text, stepBits, map: number[] | null)`, the segment-wise NFKC with map, `replaceLoneSurrogates` (appending U+FFFD), `isRemoved`, `unifyLetter`, `collapseRepeats`, `collapseWhitespace`, `tokenizeWithOffsets(text)` returning `{ text, start, end }[]`, and `isWordCharacter(text, index)`.
  - **Public exports**: `normalize(text, steps?)`, `tokenize(text)`, `toPersianDigits(text)`, `toAsciiDigits(text)`, each with full TSDoc.
  - **Argument checks**: `assertText(value, name)`, shared with `filter.ts` and exported internally, throws `TypeError(`${name} must be a string, null or undefined`)` when `value` is not `string`, `null` or `undefined`. `null` and `undefined` produce `''` or `[]` exactly as .NET does for `null`.
- [X] T023 Port `dotnet/src/PersianTextGuard/WordList.cs` to `js/src/word-list.ts`.
  - **`parse(text)`**:
    - a non-string throws `TypeError`;
    - split on `'\n'`, trim each line with a whitespace trim equivalent to .NET `string.Trim()` (use `isWhiteSpace` from `unicode.ts`, not `String.prototype.trim`, which also strips U+FEFF);
    - `#` starts a comment;
    - a section is a trimmed line longer than 2 characters that starts with `[` and ends with `]`, as in .NET. Its trimmed name is accepted only when it is non-empty, every character is a letter (`isLetter` from `unicode.ts`), and it equals one of `WORD_CATEGORIES` ignoring case: compare by lower-casing each unit with `toLowerInvariant`, never `toLowerCase` (research R17, FR-029);
    - any other name, including numbers such as `3`, `+4` and `03`, throws `WordListFormatError` with a 1-based `line`;
    - `~` means anywhere, with the trimmed remainder as the word;
    - empty words are skipped;
    - each entry is a frozen `{ text, mode, category }`, and the result is a frozen array.
  - **`WordList` object**:
    - `all` is a lazy getter that parses `BUNDLED_WORD_LISTS` from `src/generated/wordlists.ts`, in order, once, and freezes the result;
    - `persianDefault` is a lazy getter that filters `all` to categories other than `'mild'`, once;
    - `bundled(...categories)` validates each name against `WORD_CATEGORIES` (`TypeError` for an unknown one) and returns a new frozen array in list order;
    - `parse`.

  TSDoc adapted from the .NET XML docs.
- [X] T024 Port the character-level reading helpers from `dotnet/src/PersianTextGuard/ProfanityFilter.Scan.cs` to `js/src/fold.ts`. They are the ones `SourceMap.cs` and the scanner both use, so they belong to this foundational layer and are ported **only here**:
  - `LATIN_BASE_LETTERS`, built as in `BuildLatinBaseLetters` with `nfd` and `toLowerInvariant`, plus the stroke and hook overrides (`ø`, `Ø`, `đ`, `Đ`, `ł`, `Ł`, `ƒ`, `ħ`, `ı`, `ß`);
  - `fold(normalized, map)` and `foldCharacter`;
  - `isFiller`, `isRunBoundary` and `enclosedLetter`;
  - `squeeze(value, map)`.

  Follow the porting conventions; every category and character test goes through `unicode.ts`. Export them internally (not from `index.ts`).
- [X] T025 Port `dotnet/src/PersianTextGuard/SourceMap.cs` to `js/src/source-map.ts`: `MappedText` (`text`, `startMap`, `endMap`), `ReadingKind` (a numeric `const` object and type with the .NET enum's values, declared here and imported by `scan.ts`), `build(original, kind, cache)`, `chunkMap` and `wholeMessageMap`. The fold and squeeze steps are imported from `./fold.ts`. Keep the .NET fallback that compares the mapped normalization with whole-string normalization, and uses the chunk map and then the whole-message map when they differ.
- [X] T026 Port the internal normalizer and source-map tests to `js/test/internals.test.ts`:
  - `dotnet/tests/PersianTextGuard.Tests/SourceMapTests.cs`: the same 14 samples, including `"hi \uD83D"` and the zero-width and RLM samples written with `\u` escapes, and every theory and fact;
  - `PersianNormalizerTests.cs` facts that the corpus does not cover by construction: `normalize` of `null` gives `''`, and the digits helpers keep letters;
  - for each sample, `normalizeWithMap` equals `normalize`;
  - a test that `resolveSteps(['unifyLetters'])` equals the `UnifyLetters` bit;
  - a test that `resolveSteps('bogus')` throws `TypeError`;
  - `WordList.parse` heading tests (FR-029): `'[3]\nword\n'`, `'[+4]\nword\n'` and `'[ 03 ]\nword\n'` throw `WordListFormatError` with `line === 1`; `'[ Insult ]\nword\n[SLUR]\n~other\n'` yields `word` (insult, `wholeWord`) and `other` (slur, `anywhere`); `'[]\n'` is a word `'[]'`, as in .NET, because a section needs more than 2 characters.

  Run `npm test`: `unicode.test.ts` and `internals.test.ts` pass, including `SourceMapTests`' fold and squeeze theories, which use `./fold.ts`.
- [X] T027 Commit the JavaScript shared-layer tasks as "Port the Unicode layer, normalizer, word lists, fold helpers and source maps to TypeScript".

**Checkpoint**: The .NET corpus (513 cases) passes on three targets with both .NET fixes. The JavaScript shared layers are ported and their unit tests pass.

---

## Phase 3: User Story 1 - Check and censor messages from JavaScript or TypeScript (Priority: P1) 🎯 MVP

**Goal**: The `ProfanityFilter` class works from JavaScript and TypeScript, through `import` and `require`, from the packed tarball, and in a browser bundle.

**Independent Test**: `npm run pack && npm run check:consumers` passes all four consumers (ESM, CJS, TypeScript, browser). Each asserts:
- `"ک.ی.ر"` is flagged;
- `"سلام، سفارشم کی میرسه؟"` is not;
- censoring `"kir and motherfucker"` gives `"**** and ****"`;
- the match in `"😀 کیر"` has index 3.

The TypeScript consumer compiles with no `@types` package.

### Tests for User Story 1 (write first; they fail until T030–T034)

- [X] T028 [P] [US1] Create `js/test/api.test.ts`. It imports from `../src/index.ts` and covers spec acceptance scenarios 1–6 and the JavaScript-specific behaviour in FR-019 and the public API contract's G2, G4, G5, G6, G7 and G8:
  - **Scenario 1**: `containsProfanity('f u c k')` is `true`.
  - **Scenario 2**: `'هر کس پلات بالاست پیام بده'` has no match, and `censor` returns the same string.
  - **Scenario 3**: `findMatches('sh1t and f u c k')` returns `[{ word.text 'shit', category 'profanity', evasion ['lookalikeCharacters'], index 0, length 4 }, { word.text 'fuck', evasion ['splitWord'], index 9, length 7 }]`.
  - **Scenario 4**: `findMatch('😀 کیر')` has `index` 3 and `length` 3, and `'😀 کیر'.slice(3, 6) === 'کیر'`.
  - **Scenario 5**: a filter from `[{ text: 'اسپم' }, { text: 'casino', mode: 'anywhere' }]` flags `'onlinecasino.example'`.
  - **Scenario 6**: `censor('this is kir', '#') === 'this is ####'`.
  - **G2**: `TypeError` from `containsProfanity`, `findMatch`, `findMatches`, `censor`, `normalize` and `tokenize` for `42`, `{}`, `['kir']` and `true`. `undefined` behaves like `null`: `false`, `null`, `[]`, `''`, `''`, `[]`.
  - **G6**:
    - `RangeError` from `censor('kir', m)` for `m` in `'x'`, `'5'`, `' '`, `'\n'`, `'\uD83D'`, `'##'` and `''`, and also for `censor(null, 'x')`;
    - `TypeError` for `censor('kir', 5)`.
  - **Construction errors**: `new ProfanityFilter(undefined)` and `new ProfanityFilter(5)` throw `TypeError`, as does an entry `{ text: 5 }` and an entry with `mode: 'sometimes'`.
  - **G7**:
    - mutate an entry object and the options object after construction: results and `match.word` are unchanged;
    - `Object.isFrozen` holds for the `findMatches` result, each match, `match.word` and `match.evasion`.
  - **Count**: `count` for `[{ text: 'كص' }, { text: 'کص' }, { text: '  کص ' }, { text: '' }]` is `1`.
  - **G8**: `WordList.all === WordList.all`, `WordList.persianDefault === WordList.persianDefault`, and no `'mild'` in `persianDefault`.
  - **G4 and G5**: for every input in a small list (the README messages plus `null`, `''`, `'kir kir kir'`, `'hi \uD83D kir'`, `'hi \uFFFE kir'`):
    - `containsProfanity === (findMatch !== null) === (findMatches.length > 0)`;
    - matches are ordered and non-overlapping, and lie inside the text.
  - **`WordListFormatError`**: `WordList.parse('[nonsense]\nword\n')` throws it with `line === 1`, and the error is `instanceof Error`.

- [X] T029 [US1] Add a temporary `js/src/index.ts` that exports only what the foundational phase provides (`WordList`, `normalize`, `tokenize`, `toPersianDigits`, `toAsciiDigits`, and the types from `types.ts`), so `js/test/api.test.ts` compiles. Run `npx vitest run test/api.test.ts` and confirm it fails because `ProfanityFilter` is missing (red). Record the failing count in `verification.md` under "US1".

### Implementation for User Story 1

- [X] T030 [US1] Port the token-level reading machinery from `dotnet/src/PersianTextGuard/ProfanityFilter.Scan.cs` to `js/src/scan.ts`. Import `ReadingKind` from `./source-map.ts`, and `fold`, `squeeze`, `isFiller`, `isRunBoundary` and `LATIN_BASE_LETTERS` from `./fold.ts`, which the foundational phase already ported; do **not** port them again. Port:
  - `Token` (reused from `normalizer.ts`'s `tokenizeWithOffsets`) and `Hit`;
  - `PERSIAN_SUFFIXES`;
  - `tryJoinSingleLetters`, `isSingleLetter`, `hasSuffix`, `isPersianLetter`, `isLatinWord`;
  - `maskedPattern`, `fitsMaskAnywhere`, `fitsMask`, `phraseStartsAt`.

  Follow the porting conventions exactly. Every `char.Is…` and category call goes through `unicode.ts`.
- [X] T031 [US1] Complete `js/src/scan.ts` with the filter-dependent scan functions, taking the filter's internal state as an argument or as methods on an internal class used by `filter.ts`: `scan(state, text, all, firstRef)`, `report`, `matchTokens`, `matchAnywhere`, `matchSplitHalves`, `matchBrokenChunks` and `tryFindWord`. Keep `MinimumBrokenWordLength = 4` and the same order of readings as the .NET `Scan` method.
- [X] T032 [US1] Port `dotnet/src/PersianTextGuard/ProfanityFilter.Regions.cs` to `js/src/regions.ts`: `Candidate`, `toCandidate(original, hit, mapCache)`, `merge(candidates)`, `censorMatches(filter, text, matches, mask)` (including the re-mask loop that keeps the output clean), `applyMask` and `isSurrogatePairAt`.
- [X] T033 [US1] Port `dotnet/src/PersianTextGuard/ProfanityFilter.cs` to `js/src/filter.ts`: `export class ProfanityFilter`.
  - **Constructor**:
    - `words` must be a non-null, non-string iterable (`TypeError` otherwise);
    - each entry is validated (object with a string `text`; `mode` in `'wholeWord' | 'anywhere'`; `category` in `WORD_CATEGORIES`) and snapshotted into a frozen `ResolvedWord` with defaults filled;
    - build `words`, `phrases`, `anywhere` and `maskable` exactly as the .NET constructor and `Add` do, with the same order counter and duplicate rule `(normalized, mode)`;
    - copy options with defaults `true`;
    - freeze the instance's options copy.
  - **`count`**: a read-only getter.
  - **`containsProfanity`, `findMatch`, `findMatches`**: `assertText(text, 'text')` first. Matches are frozen objects `{ word, evasion, index, length }`; `evasion` is a frozen array built from the flag bits in the order `repeatedLetters`, `lookalikeCharacters`, `splitWord`. The `findMatches` result array is frozen.
  - **`censor(text, mask = '*')`**, in this order:
    1. validate `mask`: not a string → `TypeError('mask must be a one-character string')`; `mask.length !== 1`, or `isLetterOrDigit`, `isWhiteSpace`, `isControl` or `isSurrogate` of its unit → `RangeError('The mask must not be a letter, a digit, whitespace, a control character or half of a surrogate pair.')`;
    2. then `assertText`;
    3. `null` or `undefined` → `''`;
    4. no match → the same string.

  TSDoc on the class and every member, adapted from the .NET XML docs, including thread and async safety and immutability.
- [X] T034 [US1] Create `js/src/index.ts`, which exports exactly the declarations in [contracts/public-api.md](contracts/public-api.md) and nothing else: types and `WORD_CATEGORIES` and `WordListFormatError` from `types.ts`, `WordList` from `word-list.ts`, `ProfanityFilter` from `filter.ts`, and `normalize`, `tokenize`, `toPersianDigits` and `toAsciiDigits` from `normalizer.ts`. Run `npm test`: `api.test.ts` passes. Run `npm run lint`: clean.
- [X] T035 [US1] Create `js/scripts/pack.mjs` (research R9):
  1. Find the repository root (as T009), and read and trim `VERSION`.
  2. Run `npm run build`.
  3. Recreate `js/.pack/` and copy in `dist/index.mjs`, `dist/index.cjs`, `dist/index.d.mts`, `dist/index.d.cts`, `js/README.md`, and the root `LICENSE` and `THIRD-PARTY-NOTICES.md`.
  4. Write `js/.pack/package.json` from `js/package.json`: set `version` to the `VERSION` value; delete `scripts`, `devDependencies` and `private`; set `"files": ["dist"]`.
  5. Run `npm pack --pack-destination ../artifacts` in `.pack/`.
  6. Print the tarball path.
  7. Fail if the tarball's file list (from `npm pack --dry-run --json`) is not exactly the eight files in the package contract.
- [X] T036 [P] [US1] Create a placeholder `js/README.md` holding only the title and one English quick-start example. T056 writes the full README, but `pack.mjs` needs the file now.
- [X] T037 [P] [US1] Create `js/consumers/esm/`: `package.json` (`"type": "module"`, `"private": true`) and `index.js`. It imports `{ ProfanityFilter, WordList }` from `'persian-text-guard'`, builds a filter from `WordList.persianDefault`, and asserts, with `node:assert/strict`, the four facts from the Independent Test above. It prints `esm ok`.
- [X] T038 [P] [US1] Create `js/consumers/cjs/`: `package.json` (`"type": "commonjs"`) and `index.js`, the same as T037 but with `require('persian-text-guard')`. It prints `cjs ok`.
- [X] T039 [P] [US1] Create `js/consumers/typescript/`:
  - `package.json` with a pinned `typescript` dev dependency;
  - `tsconfig.json` with `"strict": true`, `"module": "node16"`, `"moduleResolution": "node16"` and `"noEmit": true`;
  - `index.mts` with the same four facts, typed variables (`const m: ProfanityMatch | null`, `const c: WordCategory = 'slur'`), and three `// @ts-expect-error` lines: an unknown option `{ squeezeLetters: false }`, a category `'rude'`, and `censor` called with a number mask;
  - `index.cts` doing the same through `require`.

  The check runs `tsc -p .`, which must exit `0` (spec scenario 7).
- [X] T040 [P] [US1] Create `js/consumers/browser/`:
  - **`entry-full.js`**: imports the package and runs the same four facts, throwing on failure.
  - **`entry-normalize.js`**: imports only `normalize` and calls it.
  - **`bundle.mjs`**:
    1. uses esbuild's JavaScript API to bundle each entry with `platform: 'browser'`, `format: 'iife'`, `bundle: true` and `minify: false` into `out/`;
    2. runs `out/entry-full.js` in `node:vm` with `vm.runInNewContext(code, {})`, a context with no `require`, `process` or `Buffer`;
    3. asserts that `out/entry-normalize.js` does not contain the string `'[profanity]'`, a marker present in the bundled word-list text (research R6, R12);
    4. prints `browser ok`.
- [X] T041 [US1] Create `js/scripts/check-consumers.mjs`:
  - **Build and pack**: run `npm run pack` unless `--no-pack` is passed, then find `js/artifacts/persian-text-guard-*.tgz`.
  - **Each consumer** (`esm`, `cjs`, `typescript`, `browser`): run `npm install --no-save <tarball>` (and `npm install` for its own dev dependencies), then its check: `node index.js`, `npx tsc -p .` or `node bundle.mjs`.
  - **Result**: exit non-zero on the first failure with the consumer's name.

  Run `npm run check:consumers`: 4 of 4 pass.
- [X] T042 [US1] Record in `verification.md` under "US1": the `api.test.ts` result, the tarball name and file list, and the four consumer results. Commit T028–T042 as "Port the profanity filter to TypeScript with ESM, CJS, types and browser checks".

**Checkpoint**: The package builds, packs and works as users install it, through all four consumers.

---

## Phase 4: User Story 2 - The same answer as every other port, proven by the corpus (Priority: P1)

**Goal**: The JavaScript corpus runner passes every case on Node.js 22 and 24, and the bundled selections equal .NET's.

**Independent Test**: `npm run corpus` passes every case with 0 not applicable. A corrupted case is reported with its id, file, escaped input and field differences. A missing corpus fails as "not found".

The runner modules (T043–T045) can be written in parallel with Phase 3; the full pass (T047) needs Phase 3.

- [X] T043 [P] [US2] Create `js/test/corpus/load.ts` (a port of the loading part of `dotnet/tests/PersianTextGuard.Conformance/Corpus.cs`):
  - **`findRepositoryRoot()`**: walks up from `import.meta.dirname` to a directory containing `VERSION` and `conformance/`.
  - **`loadCorpus(directory)`**: reads `corpus.json`, `configurations.json` and `cases/*.json` in ordinal file-name order, with `JSON.parse`. It throws an `Error` naming the file for a missing directory (message starting "Conformance corpus not found:"), a missing file, an unparsable file, a non-array case file, a case without a string `id` or `kind`, or an unknown `kind`.
  - **Return value**: `{ metadata, configurations: Map<name, object>, files, cases: { id, kind, file, json, pending }[] }`.
- [X] T044 [P] [US2] Create `js/test/corpus/values.ts`, porting the text and position helpers from `Corpus.cs`:
  - `buildInput(node)`: string, `null`, or `{ build: [...] }` with `text`, `repeat`/`times ≥ 1` and `utf16` parts. It throws on anything else.
  - `isText(node)`.
  - `codePointsToUtf16(text, start, length)` and `utf16ToCodePoints(text, index, length)`: a valid pair counts as one code point and two units; any other unit counts as one of each.
  - `showInvisible(text)`: escapes Cf, Cc, Zl, Zp, whitespace other than U+0020, lone surrogates and noncharacters as `\uXXXX`, using `unicode.ts` categories.
  - `display(node)`.
  - `compare(expected, actual)`, which returns `{ path, expected, actual }[]`: exact field-by-field comparison where two text values (string or build) compare by built text, and missing keys are reported as `(missing)`.
- [X] T045 [US2] Create `js/test/corpus/evaluate.ts`, the counterpart of `Corpus.Evaluate` and `CheckKindRules`, evaluating against `../../src/index.ts`.
  - **Filters**: `filterFor(corpus, name)` caches one filter per configuration. `bundled: "default"` → `WordList.persianDefault`; `"all"` → `WordList.all`; an array → `WordList.bundled(...)`; `entries` → the entries as given; `options` → the three booleans.
  - **Result shapes**: `evaluate(corpus, case)` returns the kind's `expected` shape with **code-point positions**, converting each match's `index`/`length` with `utf16ToCodePoints`.
    - **Matching kinds**: `containsProfanity`, `firstMatch`, `matches` (`entry` `{ text, mode, category }`, `evasion`, `start`, `length`), `censored` (`''` for a missing input), and `censoredWith` for each of `masks`.
    - **Normalization**: `steps` `"toPersianDigits"` or `"toAsciiDigits"` call those helpers; otherwise `normalize(input, steps)`.
    - **Tokenization**: `tokens`.
    - **Word-list parsing**: `entries`, or `error: { kind: 'unknown-category', line }` from `WordListFormatError`.
    - **Category selection**: `count`, `first` (first 5), `last` (last 5), and `rules`. `rules` holds `categoriesInSelection` and `bundledOrder` when they hold (in that order), plus `noMild` when the selection is `"default"` and it holds.
    - **Mask validation**: `accepted` is `false` when `censor('kir', mask)` throws `RangeError`.
  - **Comparing text**: output text that contains a lone surrogate is compared by built text, so no special serialization is needed.
  - **`checkKindRules(case)`**: `ordinary` requires `containsProfanity` `false`, `firstMatch` `null`, an empty `matches`, and `censored` plus every `censoredWith` value equal to the built input (`''` for a missing input); `must-match` requires `containsProfanity` `true`. The violation strings match .NET's wording.
- [X] T046 [US2] Create `js/test/corpus.test.ts`:
  - **Loading**: at module load, `const corpus = loadCorpus(join(findRepositoryRoot(), 'conformance'))`.
  - **Guard tests** (one `test` each):
    - the corpus loads;
    - `formatVersion === 1`, refusing newer (FR-016);
    - at least 300 cases;
    - unique ids matching `^[a-z0-9]+(-[a-z0-9]+)*$`;
    - no pending case (the message lists ids and says "run dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill");
    - every matching case's configuration exists;
    - 0 not-applicable cases, since every build part is representable in JavaScript.
  - **Case tests**: `describe.each` per kind with `test.each(ids)`, one test per case named by id. Each test checks pending, then the kind rules (message "breaks its kind rule"), then `compare`. On any problem it throws an `Error` whose message is `Case '<id>' in <file>: <problem>` on the first line, then `  input "<showInvisible(built input)>"`, then one line per difference, `  <path>: expected <expected> actual <actual>` (FR-016, spec US2 scenario 2).
- [X] T047 [US2] Run `npm run corpus`. Every case passes (513 cases + guards).
  - **Failures**: for each failing case, find the root cause by comparing `js/src` with the .NET source it ports, and fix the port. **Never** edit the corpus to match the port. If a failure is a genuine engine Unicode difference (research R1), stop and report it to the user with the case id and code points. Do not add a workaround without their decision.
  - **Record** in `verification.md`: the pass count, the Node.js version and the run time.
- [X] T048 [US2] Run the corpus locally on both supported Node.js releases (research R19), since this machine's default Node.js is 26:
  1. Install fnm with `winget install Schniz.fnm --accept-source-agreements --accept-package-agreements`, then `fnm install 22` and `fnm install 24`. Both come from the official nodejs.org builds.
  2. In `js/`, run `fnm exec --using=22 npm run corpus` and `fnm exec --using=24 npm run corpus`. Every case must pass on both.
  3. A failure on only one release is a Unicode-data difference (research R1): stop and report it to the user with the case id, the code points and both Node.js versions.

  Record `node --version`, `process.versions.unicode` and the pass counts for each release in `verification.md`.
- [X] T049 [US2] Check failure reporting on scratch edits, reverted afterwards with `git checkout -- conformance`:
  1. Change `fa-emoji-before-word`'s `expected.censored` to `"😀 ####"`, and set `matching-persian-ordinary-messages-pass-001`'s `containsProfanity` to `true`. `npm run corpus` fails exactly those 2 tests in one run, with the messages specified in T046; the second says "breaks its kind rule".
  2. Rename `conformance/` to `conformance.off`. `npm run corpus` fails with "Conformance corpus not found". Rename it back.

  Record both outputs in `verification.md`.
- [X] T050 [US2] Compare the bundled selections with .NET (FR-018, SC-002):
  1. Build, then run the `node --input-type=module -e …` dump from [quickstart.md](quickstart.md) §4 into `artifacts/compare/js.txt`.
  2. Rebuild the .NET dump with `dotnet run --project artifacts/compare/current > artifacts/compare/current.txt` (its project from feature 002 still exists locally; if not, recreate it as in 002's T029).
  3. Convert the .NET dump's mode and category names to lowerCamelCase, and its section headers to the JavaScript ones (`## Profanity` → `## profanity`, and so on; drop the `## resources` section) into `artifacts/compare/dotnet-normalized.txt`, with a small Node.js script run inline.
  4. `git diff --no-index artifacts/compare/dotnet-normalized.txt artifacts/compare/js.txt`: no differences.

  Record the counts (`all` 1,250, `default` 1,025, and the per-category counts) and "0 differences" in `verification.md`. Commit T043–T050 as "Run the conformance corpus against the JavaScript port".

**Checkpoint**: Both ports pass the same corpus. The JavaScript port is behaviourally complete.

---

## Phase 5: User Story 3 - Released to npm together with NuGet as 1.3.0 (Priority: P2)

**Goal**: CI builds, tests and packs the JavaScript port on every pull request. A `v*` tag publishes npm and NuGet together only when every job is green. `VERSION` is `1.3.0`.

**Independent Test**:
- the pull request shows `JavaScript (Node 22)` and `JavaScript (Node 24)` green, next to the existing jobs;
- locally, the tag check refuses `v9.9.9`;
- the packed tarball has version `1.3.0` and exactly eight files.

- [X] T051 [US3] Add the JavaScript job to `.github/workflows/ci.yml`. Leave the three existing jobs' names unchanged.
  - **Job**: `javascript`, `name: JavaScript (Node ${{ matrix.node }})`, `runs-on: ubuntu-latest`, `strategy: { fail-fast: false, matrix: { node: [22, 24] } }`, `defaults: { run: { working-directory: js } }`.
  - **Steps**:
    1. `actions/checkout@v5` with `fetch-depth: 0`, since the API compatibility check reads earlier release tags.
    2. `actions/setup-node@v5` with `node-version: ${{ matrix.node }}`, `cache: npm` and `cache-dependency-path: js/package-lock.json`.
    3. `npm ci`.
    4. `npm run lint`.
    5. `npm run build`.
    6. `npm test`.
    7. `npm run corpus`.
    8. `npm run test:readme`.
    9. With `if: matrix.node == 24`:
       - `npm run pack`;
       - `npm run check:package`;
       - `npm run api`;
       - `npm run api:compat`;
       - `npm run check:consumers -- --no-pack`;
       - `actions/upload-artifact@v4` with `name: npm-package`, `path: js/artifacts/*.tgz`.
- [X] T052 [US3] Create `js/scripts/check-package.mjs`: find `js/artifacts/persian-text-guard-*.tgz` (exactly one, else fail); run `npx publint run .pack --strict`, then `npx attw <tarball> --profile node16`. The `node16` profile checks `node16`-CJS, `node16`-ESM and `bundler` resolutions. It exits non-zero if either reports a problem. Run it locally: both pass.
- [X] T053 [US3] Add the npm publish job and gate NuGet on every port in `.github/workflows/ci.yml`:
  - **`publish`** (`Publish to NuGet`): change `needs: [build, netfx]` to `needs: [build, netfx, javascript]`. Nothing else changes.
  - **New job `publish-npm`**, `name: Publish to npm`:
    - `needs: [build, netfx, javascript]`, `if: startsWith(github.ref, 'refs/tags/v')`, `runs-on: ubuntu-latest`, `environment: npm`;
    - `permissions: { contents: read, id-token: write }`, with a comment that npm trusted publishing needs no token and generates provenance automatically.
  - **Steps**:
    1. `actions/checkout@v5`.
    2. The same "Check tag matches VERSION" step as the NuGet job, with the same command.
    3. `actions/setup-node@v5` with `node-version: 24` and `registry-url: https://registry.npmjs.org`.
    4. `npm install -g npm@^11.5.1`, with a comment that trusted publishing requires 11.5.1 or later.
    5. `actions/download-artifact@v4`, `name: npm-package`, `path: npm-package`.
    6. "Publish (skip if already published)":

       ```bash
       V="$(tr -d '[:space:]' < VERSION)"
       if npm view "persian-text-guard@$V" version >/dev/null 2>&1; then echo "persian-text-guard@$V already published"; exit 0; fi
       npm publish "npm-package/persian-text-guard-$V.tgz" --access public
       ```

       with a comment that a re-run after a partial release is safe (research R10).
- [X] T054 [US3] Check the release gates locally before the real dry run (research R18), and record in `verification.md`:
  1. In Git Bash, run the tag-check command with `GITHUB_REF_NAME=v1.3.0` after T055 (exit `0`) and with `GITHUB_REF_NAME=v9.9.9` (exit `1`, printing "Tag v9.9.9 does not match VERSION 1.3.0").
  2. Read `.github/workflows/ci.yml` back and confirm that both publish jobs list `build`, `netfx` and `javascript` in `needs`, and that both run only on `refs/tags/v`.

  This is only a precheck. SC-008 is demonstrated by the real CI dry run in Phase 7, which also catches workflow syntax errors.
- [X] T055 [US3] Set `VERSION` to `1.3.0` (`printf '1.3.0\n' > VERSION`). Run:
  - `npm run pack` in `js/`: the tarball is `persian-text-guard-1.3.0.tgz`;
  - `dotnet pack dotnet/src/PersianTextGuard -c Release -o artifacts`: this produces `PersianTextGuard.1.3.0.nupkg`, and package validation against **1.2.0** passes (`PackageValidationBaselineVersion` stays `1.2.0`, research R15).

  Record both in `verification.md`. Commit T051–T055 as "Build, test and publish the npm package in CI; version 1.3.0".

**Checkpoint**: CI is ready to publish both packages in lockstep from one tag.

---

## Phase 6: User Story 4 - Documented, measured and kept compatible (Priority: P3)

**Goal**: Every export is documented, the npm README is bilingual with a tested example for every code block, performance is measured, and the API report baseline exists.

**Independent Test**:
- `npm run test:readme` passes;
- `npm run api` passes with the committed report;
- `npm run lint` reports no undocumented export;
- `npm run bench` meets SC-005 and matches the README table.

- [X] T056 [US4] Write the full `js/README.md` (FR-024, research R16). The first line is `# persian-text-guard`, followed by a one-line English description, then:
  1. **English**:
     - **Installation**: `npm install persian-text-guard`.
     - **Quick start**: TypeScript examples, each in its own fenced `ts` block that imports from `'persian-text-guard'` and ends with `console.log` lines showing the result as comments:
       - building a filter from `WordList.persianDefault`;
       - `containsProfanity`, `findMatch` and `findMatches`, with index, length and the `slice` idiom;
       - `censor` with a default and a chosen mask;
       - categories via `WordList.bundled('slur', 'harassment')` and `WordList.all`;
       - custom words;
       - options;
       - `normalize`, `tokenize` and the digit helpers;
       - `WordList.parse` for a file the caller reads.
     - **Validating input**: explains that anything other than a string, `null` or `undefined` throws `TypeError`, with an example that checks `typeof body.message === 'string'`.
     - **.NET to JavaScript**: the full name table from [contracts/public-api.md](contracts/public-api.md).
     - **Performance**: filled by T060.
     - **Limitations**:
       - the Unicode-version note from research R1: characters assigned in Unicode 16 or later can be classified or normalized differently across JavaScript engines and .NET targets;
       - the browser note from research R12: Chrome, Edge and Node.js (V8) are tested in CI; Firefox and Safari are supported but not CI-tested, and the conformance corpus is the reference if their results differ;
       - a link to the main README's evasion table and limitations.
     - **Links**: to the project README, the conformance corpus and the licence.
  2. **Persian**, inside `<div dir="rtl">` … `</div>`, with a `## فارسی` heading: installation, and a quick start with the first two examples (build and check; censor) reusing the same code blocks, with a Persian comment line added to each. Also a Persian sentence for the input-validation note, and a link to the full project README.

  Every fenced block is `ts` and self-contained, so T057 can run it.
- [X] T057 [US4] Create `js/test/readme.test.ts`. It reads `js/README.md`, extracts every fenced block tagged `ts`, rewrites `from 'persian-text-guard'` to an absolute `file://` URL of `js/dist/index.mjs`, writes each block to a temporary `.mts` file in `js/temp/readme/`, and runs it with `node --import tsx <file>`. Each block must exit `0`. For each `console.log(x); // → value` line, the test asserts the printed value equals `value`. Name each test `README block <n>: <first comment or line>`. Run `npm run test:readme`: every block passes.
- [X] T058 [US4] Create `js/api-extractor.json`:
  - `mainEntryPointFilePath: "<projectFolder>/dist/index.d.mts"`;
  - `apiReport: { enabled: true, reportFolder: "<projectFolder>/etc/", reportTempFolder: "<projectFolder>/temp/" }`;
  - `docModel.enabled: false` and `dtsRollup.enabled: false`;
  - `tsdocMetadata.enabled: false`;
  - `messages` that report `ae-missing-release-tag` as none and treat `tsdoc-*` warnings as errors.

  If API Extractor does not accept `.d.mts` as the entry point, add a `build:api-dts` step that emits a plain `temp/api/index.d.ts` with `tsc -p tsconfig.api.json --emitDeclarationOnly`, point `mainEntryPointFilePath` at it, and record the reason in `api-extractor.json` as a comment.

  Run `npx api-extractor run --local` to create `js/etc/persian-text-guard.api.md`. Review that it lists exactly the declarations in the public API contract (FR-022). Then `npm run api` (non-local) passes.
- [X] T059 [US4] Create `js/scripts/check-api-compat.mjs` (research R14; constitution: compare "against the previous release"):
  1. **Previous release**: run `git describe --tags --abbrev=0 --match "v[0-9]*" <ref>`, where `<ref>` is `HEAD`, or `HEAD^` when `git tag --points-at HEAD` lists a `v*` tag, so a release build compares with the release before it. If no tag is found, print "No previous release tag; baseline only" and exit `0`.
  2. **Previous report**: `git show <tag>:js/etc/persian-text-guard.api.md`. If the file does not exist at that tag, print "No previous npm release report (first release); baseline only" and exit `0`.
  3. **Compare**: extract the lines inside the fenced `ts` block of both the previous report and the current `js/etc/persian-text-guard.api.md`, trimming trailing whitespace and ignoring blank lines and `//` comment lines. Every previous declaration line must appear in the current block.
  4. **Breaking changes**: for each missing line, print `BREAKING: <line>`. If there are any, exit `1`, unless the major number in `VERSION` is greater than the tag's major number, in which case print "Allowed by MAJOR version" and exit `0`.
  5. **Otherwise**: print "API compatible with <tag>: <n> declarations kept, <m> added" and exit `0`.

  **Verify**, then undo every scratch change:
  - `npm run api:compat` prints the first-release baseline message.
  - Create a local tag `v1.3.0-compat-test` on `HEAD`, commit a scratch change that edits the `censor` line in the report, and run the script: it prints `BREAKING:` for that line and exits `1`.
  - Revert the commit, delete the tag, and record both outputs in `verification.md`.
- [X] T060 [US4] Create `js/bench/filter.bench.ts` with tinybench, mirroring `dotnet/benchmarks/PersianTextGuard.Benchmarks/Program.cs`. Copy the same message constants (`CleanShort`, `CleanLong`, `Evasion`, `DirtyShort`, `DirtyMixed`, `DirtyLong`) verbatim, and add the same nine tasks: `CleanShortMessage`, `CleanLongMessage`, `EvasiveMessage`, `NormalizeLongMessage`, `BuildFilterFromDefaultList`, `FindMatchesClean`, `FindMatchesDirty`, `CensorShortDirty` and `CensorLongDirty`. It imports from `../dist/index.mjs`, runs with a warm-up, and prints a Markdown table: Operation | Mean | Throughput.
  - **Extra task**: add `VeryLongMessage`, which checks `containsProfanity` on the 132,000-character message (`"سلام این یک متن معمولی است و هیچ مشکلی ندارد. hello this is fine. "` repeated 2000 times, then `" کیر"`), for the spec's "very long messages" edge case.
  - **Run** `npm run bench` on Node.js 24 on this machine, using `fnm exec --using=24` if needed.
  - **Check the limits**: `BuildFilterFromDefaultList` under 50 ms and `CleanShortMessage` under 50 µs (SC-005), and `VeryLongMessage` under 100 ms (spec Edge Cases). If any fails, stop and report it to the user with the numbers before optimising.
  - **Record**: fill the README's "Performance" table with the numbers, the machine (read the CPU model from `wmic cpu get name` or `Get-CimInstance Win32_Processor`) and the Node.js version.
- [X] T061 [US4] Update the root `README.md` (FR-025):
  1. After the introduction, add an "Install" subsection listing both packages: `dotnet add package PersianTextGuard` and `npm install persian-text-guard`, linking `js/README.md`.
  2. In "Development": add `js/` to the layout tree, and to the commands table add `cd js && npm ci`, `npm run build`, `npm test`, `npm run corpus`, `npm run bench` and `npm run pack`.
  3. Add a "Releasing" section:
     - set `VERSION`;
     - merge;
     - tag `vX.Y.Z` on `main`;
     - CI publishes NuGet and npm only when every job is green;
     - if one publish job fails after the other succeeded, re-run the failed job, which is safe.
  4. In "Limitations", link the npm README's Unicode and browser notes.
  5. Add a short "Changes in 1.3.0 for .NET users" note, required by the constitution's rule that a behaviour-changing PR updates the README. It says:
     - messages containing Unicode noncharacters no longer throw (U+FFFE on .NET 8/10, all noncharacters on .NET Framework 4.8);
     - word-list section headings must be category names, and a number such as `[3]` is now an unknown category;
     - it links the GitHub release notes.

     The Persian half follows with the bilingual README feature, under the adoption clause.

  Every new command must be one verified in T010–T060.
- [X] T062 [US4] Draft the bilingual release notes in `specs/003-javascript-typescript-port/release-notes-1.3.0.md` (research R15). It has an English section and a Persian summary in `<div dir="rtl">`.
  - **New npm package**: `persian-text-guard`, with install and quick start, and a link to `js/README.md`.
  - **Fixed**: .NET no longer throws `ArgumentException` for messages containing Unicode noncharacters (U+FFFE on .NET 8/10, all noncharacters on .NET Framework 4.8). This is called out as a change in matching behaviour for inputs that used to throw.
  - **Changed**: word-list section headings must be category names (`[insult]`, any case). A category number such as `[3]` or `[+4]`, which 1.2.0 accepted, is now reported as an unknown category.
  - **Unchanged**: no other change to .NET behaviour, API or bundled entries.
- [X] T063 [US4] Run `npm run lint` (clean, no undocumented export), `npm run test:all` (unit tests, corpus and README all pass), `npm run api` (passes) and `npm run api:compat` (first-release baseline). Record the results in `verification.md`. Commit the US4 tasks as "Document, benchmark and record the API of the npm package".

**Checkpoint**: The feature is complete and documented, and ready for a pull request.

---

## Phase 7: Polish, Pull Request and Release 1.3.0

**Purpose**: Full verification, the pull request, and the irreversible release. The user delegated steps T073–T077 in advance (memory: `persiantextguard-npm-release-delegation`). Perform them without asking again, but only when every precondition in the task holds, and report each outcome.

- [X] T064 Run the full matrix from a clean state:
  - delete `js/node_modules`, `js/dist`, `js/.pack`, `js/artifacts`, and every `bin/` and `obj/` under `dotnet/`;
  - `dotnet build dotnet/PersianTextGuard.slnx` (0 warnings);
  - `dotnet test dotnet/tests/PersianTextGuard.Tests` (1,029 × 3);
  - `dotnet test dotnet/tests/PersianTextGuard.Conformance` (all pass × 3);
  - in `js/`: `npm ci`, `npm run lint`, `npm run test:all`, `npm run pack`, `npm run check:package`, `npm run api`, `npm run api:compat`, `npm run check:consumers -- --no-pack`.

  Record the counts, and the unpacked size from `npm pack --dry-run --json` (must be under 1 MB, SC-004), in `verification.md`.
- [X] T065 Walk through [quickstart.md](quickstart.md) §1–§6 and tick each expected outcome in `verification.md` with the task that produced it. §7 is completed by the dry-run and release tasks below. As part of §6, run the SC-003 onboarding check:
  1. Create two empty temporary directories outside the repository, one for plain JavaScript and one for TypeScript.
  2. Start a timer, and follow **only** the steps written in `js/README.md`'s quick start, installing `js/artifacts/persian-text-guard-1.3.0.tgz` in place of the registry name.
  3. Stop the timer when the example flags `"ک.ی.ر"`.

  Both runs must finish in under 5 minutes and need no step the README does not state; any missing step is a README fix. Record both times in `verification.md`.
- [X] T066 Run the quickstart §1 regression proof:
  1. Create a separate checkout of the pre-fix code with `git worktree add ../ptg-1.2.0 main`, leaving the feature branch untouched.
  2. Copy the current `conformance/` over the worktree's.
  3. Run `dotnet test ../ptg-1.2.0/dotnet/tests/PersianTextGuard.Conformance`. On 1.2.0 code, these recorded cases fail:
     - the noncharacter cases: the U+FFFE ones on all three targets, and all 12 on `net48`;
     - the three numeric-heading cases (`[3]`, `[+4]`, `[ 03 ]`) on all three targets, because 1.2.0 accepts them.

     The spaced and upper-case heading case passes on 1.2.0 too, as it should.
  4. Record the output, then `git worktree remove ../ptg-1.2.0 --force`.
- [X] T067 Prepare the real CI dry run (research R18, SC-008).
  1. Commit remaining changes, `verification.md` included, on `003-javascript-typescript-port`, and push the branch; no pull request yet.
  2. Create the scratch branch with `git switch -c dryrun/release-gates`.
  3. Edit its `.github/workflows/ci.yml` only:
     - in `Publish to NuGet`, remove the `NuGet/login` step and replace the push command with `ls -l artifacts/*.nupkg && echo "DRY RUN: would push to NuGet"`;
     - in `Publish to npm`, change the publish command's last line to `npm publish "npm-package/persian-text-guard-$V.tgz" --access public --dry-run`;
     - in the `javascript` job, add a first step after checkout, `- name: "DRY RUN: simulated failure"` with `run: exit 1`.
  4. Set `VERSION` to `0.0.0-dryrun.1`.
  5. **Safety check, before committing.** Run `grep -nE "dotnet nuget push|NuGet/login" .github/workflows/ci.yml` (must print nothing) and `grep -n "npm publish" .github/workflows/ci.yml | grep -v -- "--dry-run"` (must print nothing). Stop if either prints anything.
  6. Commit as "DRY RUN ONLY: release gate test (do not merge)" and push the branch.
- [X] T068 Dry run 1, a failing port blocks both registries:
  1. `git tag v0.0.0-dryrun.1` and `git push origin v0.0.0-dryrun.1`.
  2. Find the run with `gh run list --workflow ci.yml --branch v0.0.0-dryrun.1 --limit 1`, and wait with `gh run watch <id>`.
  3. With `gh run view <id> --json jobs --jq '.jobs[] | "\(.name): \(.conclusion)"'`, confirm that `JavaScript (Node 22)` and `JavaScript (Node 24)` are `failure`, and that `Publish to NuGet` and `Publish to npm` are `skipped`.

  Record the job list in `verification.md`. If either publish job ran, stop, delete the tag, and report to the user: the gating is wrong.
- [X] T069 Dry runs 2 and 3, green publishing and tag mismatch:
  1. On `dryrun/release-gates`, remove the "DRY RUN: simulated failure" step and set `VERSION` to `0.0.0-dryrun.2`. Repeat the T067 safety check, commit, push, then tag and push `v0.0.0-dryrun.2`.
  2. Watch the run. Every job must succeed:
     - the `Publish to NuGet` log lists `PersianTextGuard.0.0.0-dryrun.2.nupkg` and prints "DRY RUN";
     - the `Publish to npm` log shows the tag check passing, the "already published" check not triggering, and `npm publish --dry-run` reporting `persian-text-guard@0.0.0-dryrun.2` with 8 files;
     - the run shows a deployment to the `npm` environment, proving its `v*` tag rule allows release tags.
  3. Tag the **same** commit `v0.0.0-dryrun.3` and push it. Both publish jobs must fail at "Check tag matches VERSION", printing "Tag v0.0.0-dryrun.3 does not match VERSION 0.0.0-dryrun.2".

  Record the job conclusions and log excerpts for both runs in `verification.md`.
- [X] T070 Clean up the dry run and confirm nothing was published:
  1. `git push origin --delete v0.0.0-dryrun.1 v0.0.0-dryrun.2 v0.0.0-dryrun.3`, then `git tag -d` for the same three.
  2. `git switch 003-javascript-typescript-port`, `git push origin --delete dryrun/release-gates`, and `git branch -D dryrun/release-gates`.
  3. Confirm that `npm view persian-text-guard versions` prints only `0.0.1`, and that `curl -s https://api.nuget.org/v3-flatcontainer/persiantextguard/index.json` contains no `0.0.0-dryrun`.
  4. Confirm that `VERSION` on `003-javascript-typescript-port` is still `1.3.0`, and that its `ci.yml` has none of the dry-run edits (`git diff main -- .github/workflows/ci.yml` shows only the real JavaScript and npm jobs).

  Record the results in `verification.md` under "SC-008: release gates dry run".
- [X] T071 Commit `verification.md` on `003-javascript-typescript-port`, push, and open a pull request to `main` with `gh pr create`.
  - **Title**: "JavaScript/TypeScript port on npm, two .NET fixes, version 1.3.0".
  - **Body** summarises the parts and ends with the Claude Code attribution line:
    - the port;
    - the noncharacter fix;
    - the names-only heading rule;
    - CI and publishing, including the dry-run results;
    - the **.NET benchmark before/after table** from `verification.md`, as the constitution requires for a per-message path change;
    - links to `specs/003-javascript-typescript-port/verification.md` and the release-notes draft.

  Wait for CI with `gh pr checks --watch`. The four build and test jobs must pass, and the two publish jobs must be skipped (pull requests never publish):
  - must pass: `Build, test, pack`;
  - must pass: `Test on .NET Framework 4.8 (netstandard2.0 build)`;
  - must pass: `JavaScript (Node 22)`;
  - must pass: `JavaScript (Node 24)`;
  - skipped: `Publish to NuGet` and `Publish to npm`.

  Confirm in the logs that the JavaScript corpus ran on Node.js 22 and 24 and the .NET corpus on three targets. If a job fails, fix it on the branch and repeat. **Do not merge** without the user's go-ahead: ask once CI is green.
- [ ] T072 After the user approves, merge with a merge commit (`gh pr merge <n> --merge`, as for PR #3), fast-forward local `main`, and confirm `main`'s CI run is green on the merge commit (`gh run list --branch main --limit 1`, then `gh run watch`). Record the merge commit in `verification.md`.
- [ ] T073 (delegated) Add the new required checks to `main`, keeping the two existing ones, `strict: false`, `enforce_admins: false`, and the GitHub Actions `app_id` 15368:

  ```bash
  gh api -X PATCH repos/AmirehsanK/PersianTextGuard/branches/main/protection/required_status_checks --input -
  ```

  with a body whose `checks` lists `Build, test, pack`, `Test on .NET Framework 4.8 (netstandard2.0 build)`, `JavaScript (Node 22)` and `JavaScript (Node 24)`. Confirm with `gh api repos/AmirehsanK/PersianTextGuard/branches/main --jq .protection.required_status_checks`.
- [ ] T074 (delegated; **irreversible**) Push the release tag, only if all of these hold:
  - `main`'s latest CI run is green;
  - `VERSION` on `main` is `1.3.0`;
  - `npm view persian-text-guard versions` does not contain `1.3.0`;
  - NuGet does not already list `PersianTextGuard 1.3.0`.

  Then `git tag -a v1.3.0 -m "PersianTextGuard 1.3.0"` on the merge commit, and `git push origin v1.3.0`. Watch the tag's workflow run with `gh run watch`: both publish jobs must succeed. If one fails, read its log, fix the cause without deleting the tag, and re-run that job (research R10). If the cause needs a code change, stop and report to the user.
- [ ] T075 (delegated) Verify both registries (SC-010, quickstart §7):
  - **npm**: `npm view persian-text-guard@1.3.0 version dist.attestations` shows a provenance attestation. Install it into a fresh temporary project and run the `containsProfanity('ک.ی.ر')` check through `require`; it prints `true`.
  - **NuGet**: after it indexes (retry for up to 30 minutes), `dotnet add package PersianTextGuard --version 1.3.0` into a fresh console app succeeds, and the same check prints `True`.

  Record both in `verification.md`.
- [ ] T076 Create the GitHub release with `gh release create v1.3.0 --title "PersianTextGuard 1.3.0" --notes-file specs/003-javascript-typescript-port/release-notes-1.3.0.md`. Confirm with `gh release view v1.3.0` that it contains the Persian summary.
- [ ] T077 (delegated; the user approves 2FA in the browser) Run `npm deprecate persian-text-guard@0.0.1 "Placeholder; use 1.3.0 or later" --auth-type=web` as `darkerys`: tell the user a browser approval is waiting. Confirm with `npm view persian-text-guard@0.0.1 deprecated`. Also confirm `npm view persian-text-guard dist-tags.latest` is `1.3.0`. Record the results in `verification.md`, then commit and push it to `main` as "Record 1.3.0 release verification".

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: none. T001 → T002 → T003–T008 ([P] T005–T008) → T009 → T010.
- **Foundational (Phase 2)**: depends on Setup.
  - **.NET track**, strictly in order: T011 (escaping rule) → T012 (noncharacter cases, pending) → T013 (heading cases, pending) → T014 ("before" benchmarks) → T015 (normalizer fix) → T016 (heading fix) → T017 (fill and verify) → T018 ("after" benchmarks, README table, commit). The cases are added before the fixes, and the benchmarks run before and after.
  - **JavaScript track**: T019 ∥ T020 ∥ T021, then T022 (normalizer) → T023 (word lists) → T024 (fold helpers) → T025 (source maps) → T026 (internal tests) → T027 (commit).
  - The two tracks are independent, but T023's heading rule must match T016's.
- **US1 (Phase 3)**: depends on Foundational.
  - T028 (API tests) → T029 (red run).
  - T030 → T031 → T032 → T033 → T034: the scanner, regions, filter and exports, which share files, so they run in order.
  - T035 (pack script), then T036 ∥ T037 ∥ T038 ∥ T039 ∥ T040, then T041 (consumer checks), then T042 (record and commit).
- **US2 (Phase 4)**: T043 ∥ T044 need only Foundational. T045 needs T034's exports. Then T046 → T047 (needs Phase 3 complete) → T048 (Node.js 22 and 24) → T049 → T050.
- **US3 (Phase 5)**: depends on US1 (pack and consumers) and US2 (corpus script). T051 → T052 → T053 → T054 → T055; T054 step 1 needs T055's `VERSION`.
- **US4 (Phase 6)**: depends on US1.
  - T056 → T057 (README and its test).
  - T058 → T059 (API report, then compatibility script).
  - T060 (benchmarks) after T034.
  - T061 and T062 anytime after T055.
  - T063 last.
  - The CI job from T051 runs `test:readme` and `api:compat`, so T057 and T059 must exist before the first CI run (T067).
- **Phase 7**: depends on all phases.
  - T064 → T065 → T066.
  - T067 → T068 → T069 → T070: the dry run, which must pass before the pull request.
  - T071 → T072 (the user's go-ahead to merge) → T073 → T074 (irreversible) → T075 → T076 → T077.

### User Story Dependencies

- **US1 (P1)**: needs Foundational only. It is the MVP: a usable package.
- **US2 (P1)**: needs US1 for the full pass; its runner modules (T043, T044) can be built alongside US1.
- **US3 (P2)**: needs US1 and US2, since CI runs both and publishing requires them green.
- **US4 (P3)**: needs US1. Its README test and API compatibility script are needed by US3's CI job before that job first runs.

### Parallel Opportunities

- **Setup**: T005 ∥ T006 ∥ T007 ∥ T008.
- **Foundational**: the .NET track (T011–T018) ∥ the JavaScript track (T019–T027); within JavaScript, T019 ∥ T020 ∥ T021.
- **US1**: T036 ∥ T037 ∥ T038 ∥ T039 ∥ T040, after T035.
- **Across stories**: US2's T043 ∥ T044 alongside US1's T030–T033; US4's T058 and T060 alongside US2 once T034 is done.

---

## Parallel Example: User Story 1

```bash
# After T035 (pack script), the four consumers and the README placeholder together:
Task: "Create js/consumers/esm/ (T037)"
Task: "Create js/consumers/cjs/ (T038)"
Task: "Create js/consumers/typescript/ (T039)"
Task: "Create js/consumers/browser/ (T040)"
Task: "Create placeholder js/README.md (T036)"

# Meanwhile, US2's runner helpers that do not need the filter:
Task: "Create js/test/corpus/load.ts (T043)"
Task: "Create js/test/corpus/values.ts (T044)"
```

---

## Implementation Strategy

### MVP First (Foundational + User Story 1)

1. **Setup**: scaffold `js/`.
2. **Foundational**: the two .NET fixes with their corpus cases and benchmarks, plus the TypeScript Unicode layer, normalizer, word lists, fold helpers and source maps.
3. **US1**: the filter, packed and checked through ESM, CJS, TypeScript and a browser bundle.
4. **Stop and validate** with the US1 Independent Test. The package is usable, but it must not be released until US2 proves corpus parity.

### Incremental Delivery

1. **Foundational**: .NET no longer throws on noncharacters, and word-list headings are names only.
2. **+ US1**: a usable JavaScript/TypeScript package.
3. **+ US2**: proven identical to .NET across every corpus case.
4. **+ US3**: CI and lockstep publishing; `VERSION` 1.3.0.
5. **+ US4**: bilingual README, benchmarks, API baseline.
6. **Phase 7**: full re-run, regression proof, real CI dry run of the release gates, pull request, merge (with user approval), required checks, tag, verification on both registries, GitHub release, placeholder deprecation.

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks.
- Never edit the corpus to make a port pass. A disagreement is investigated against the .NET source, and any genuine engine difference goes to the user (T047).
- `graphify-out/` is unrelated and must never be committed.
- The release (T074) is irreversible. Every precondition in its description must hold first.
- The user approves npm 2FA in the browser for T077. Tell them when the prompt is waiting.
