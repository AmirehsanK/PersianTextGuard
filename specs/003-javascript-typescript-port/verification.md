# Verification: JavaScript/TypeScript Port Published to npm

**Feature**: [spec.md](spec.md) | **Tasks**: [tasks.md](tasks.md) | **Quickstart**: [quickstart.md](quickstart.md)

Results of the checks run while implementing this feature, in task order.

## Baseline

- **Commit**: `5b4c9ba` (merge of PR #3) on `003-javascript-typescript-port`. The working tree held only the untracked spec folder, plus the unrelated `graphify-out/`, which is never committed (T001).
- **Local tools**: Node.js v26.4.0, npm 12.0.2. CI covers Node.js 22 and 24; T048 runs the corpus locally on both.
- **.NET tests**:

  | Project | `net8.0` | `net10.0` | `net48` |
  | --- | --- | --- | --- |
  | `PersianTextGuard.Tests` | 1,029 passed | 1,029 passed | 1,029 passed |
  | `PersianTextGuard.Conformance` | 506 passed | 506 passed | 506 passed |

## Scaffold (T003–T010)

- **Dev tools** pinned with `--save-exact`, current stable versions except where noted:

  | Package | Version |
  | --- | --- |
  | `typescript` | 6.0.3 |
  | `tsup` | 8.5.1 |
  | `vitest` | 5.0.1 |
  | `eslint` | 10.10.0 |
  | `typescript-eslint` | 8.70.0 |
  | `eslint-plugin-jsdoc` | 64.5.2 |
  | `@microsoft/api-extractor` | 7.59.1 |
  | `publint` | 0.3.24 |
  | `@arethetypeswrong/cli` | 0.18.5 |
  | `tinybench` | 6.2.0 |
  | `tsx` | 4.23.13 |
  | `esbuild` | 0.28.2 |
  | `@types/node` | 22 (the minimum supported Node.js) |

  **Deviation**: TypeScript 7.0.2 is the newest release, but `typescript-eslint` 8.70.0 requires `<6.1.0`, and tsup's declaration build and API Extractor use TypeScript's JavaScript API. TypeScript is pinned to 6.0.3, the newest compatible release. `@types/node` is pinned to 22, so tests cannot use APIs newer than the oldest supported runtime.
- **`npm audit`**: 1 low-severity advisory, in a dev-only dependency; nothing ships in the package, which has no dependencies.
- **npm 12** blocks install scripts by default: esbuild's `postinstall` did not run, and `npx esbuild --version` still works (0.28.2).
- **Config self-checks**, using throwaway files that were then deleted:
  - `test/` importing `node:fs` and `../src/index.ts` type-checks under `tsconfig.node.json`;
  - `process.env` in `src/` fails `tsconfig.json` with TS2591;
  - ESLint reports `no-restricted-globals` for `process`, and `no-restricted-syntax` for `'A'.toLowerCase()` and for `/\s/`.
- **`npm run generate`** writes `src/generated/wordlists.ts`, and `git status` does not list it.

## .NET fixes: noncharacters and word-list headings (T011–T018)

**Escaping rule (T011).** `CaseWriter`, `Corpus.IsInvisible`/`ShowInvisible` and the unescaped-character guard treat Unicode noncharacters as invisible; supplementary ones are written as their two surrogate escapes. The writer test asserts `"a￾b🿾"`. The corpus still passed (506 × 3), and writing rule 1 was updated in `conformance/README.md` and the 002 corpus-format contract.

**Before the fixes (T012).** With the 12 noncharacter cases pending, `dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill` crashed:

```text
Unhandled exception. System.ArgumentException: String contains invalid Unicode code points. (Parameter 'strInput')
   at PersianTextGuard.PersianNormalizer.Normalize(String text, PersianNormalization steps, List`1 map) in …\PersianNormalizer.cs:line 68
```

No file was rewritten. The regression proof for the recorded cases is T066.

**Heading cases (T013).** Added as pending: `[3]`, `[+4]`, `[ 03 ]`, and `# list\n[ Insult ]\nword\n[SLUR]\n~other\n`.

**Fixes (T015, T016).**
- `PersianNormalizer` normalizes around noncharacters in both the whole-string and segment paths, and skips the `IsNormalized` fast path when a noncharacter is present.
- `WordList.Parse` accepts a heading only when every character of its name is a letter.
- Build: 0 warnings. `PersianTextGuard.Tests`: 1,029 × 3.

**Fill and verify (T017).**
- `Filled 16 case(s); 0 disagreement(s)`, exit `0`.
- `git diff --numstat`: normalization.json +21/−0, robustness.json +205/−0, word-list-parsing.json +36/−0. Only additions; no existing case changed.
- `git grep -nP '[\x{FDD0}-\x{FDEF}\x{FFFE}\x{FFFF}]' -- conformance` prints nothing.
- **Recorded**:
  - `"hi ￾ kir"` censors to `"hi ￾ ****"`;
  - `"ک￾یر"` censors to `"****"`;
  - normalization `"ﻛﻴﺮ ￾ BOOOK"` gives `"کیر ￾ book"`;
  - the three numeric headings give `{ "kind": "unknown-category", "line": 1 }`;
  - `[ Insult ]`/`[SLUR]` give `word` (insult, wholeWord) and `other` (slur, anywhere).
- `PersianTextGuard.Conformance`: **522 × 3** (513 cases + 9 guards) on `net8.0`, `net10.0`, `net48`. `--check`: 0 disagreements.

**.NET benchmarks (T014, T018)**: BenchmarkDotNet, full suite, `net10.0`, Intel Core i7-9700K (the machine the README names), .NET 10.0.11.

| Benchmark | Mean before | Mean after | Change | Allocated before | Allocated after |
| --- | ---: | ---: | ---: | ---: | ---: |
| CleanShortMessage | 2.432 µs | 2.521 µs | +3.7% | 2.66 KB | 2.66 KB |
| CleanLongMessage | 22.428 µs | 22.969 µs | +2.4% | 21.92 KB | 21.92 KB |
| EvasiveMessage | 2.046 µs | 1.933 µs | −5.5% | 3.09 KB | 3.09 KB |
| NormalizeLongMessage | 5.922 µs | 6.041 µs | +2.0% | 3.3 KB | 3.3 KB |
| BuildFilterFromDefaultList | 590.741 µs | 600.059 µs | +1.6% | 1061.36 KB | 1061.36 KB |
| FindMatchesClean | 2.690 µs | 2.897 µs | +7.7% | 2.7 KB | 2.7 KB |
| FindMatchesDirty | 6.892 µs | 7.768 µs | +12.7% | 9.7 KB | 9.7 KB |
| CensorShortDirty | 4.535 µs | 4.712 µs | +3.9% | 6.43 KB | 6.43 KB |
| CensorLongDirty | 95.948 µs | 99.471 µs | +3.7% | 97.33 KB | 97.33 KB |

- **Allocations** are identical in every benchmark.
- **The two means above 5% are machine drift, not the fix.** A rerun of `FindMatches*` and `CleanShortMessage` together gave CleanShortMessage 2.735 µs, FindMatchesClean 2.884 µs (ratio 1.05) and FindMatchesDirty 7.541 µs (ratio 2.76). The baseline benchmark itself drifted +12% between runs with no change on its path, while the ratios fell from 1.11 and 2.83 in the "before" run.
- **The only per-message addition** is one scan for noncharacters in text that is compatibility-normalized.
- **README**: the .NET "Performance" table was refreshed from the "after" run.

## JavaScript shared layers (T019–T027)

- **Files**: `src/types.ts`, `src/unicode.ts`, `src/normalizer.ts`, `src/word-list.ts`, `src/fold.ts` and `src/source-map.ts`.
- **`npx vitest run test/unicode.test.ts test/internals.test.ts`**: **85 passed** (26 + 59).
  - the R1 findings: U+0085 and U+FEFF whitespace, U+0130 and Σ lower-casing, `Cs` for lone surrogates, NFKC around all 66 noncharacters;
  - every `SourceMapTests` theory on the same 14 samples, and its facts;
  - the heading rule: `[3]`, `[+4]` and `[ 03 ]` throw `WordListFormatError` with `line` 1, and `[ Insult ]`/`[SLUR]` parse;
  - a normalization-with-map of a 320,000-character message;
  - the bundled lists: `all` 1,250 and `persianDefault` 1,025.
- **ESLint** on `src/` and **`tsc --noEmit`** for both configs: clean.
- **Deviation from T005**: `tsconfig.node.json` uses `"module": "ESNext"` and `"moduleResolution": "Bundler"`, not `NodeNext`. Tests import `src/` files, which use extensionless relative imports resolved by tsup and Vitest, and NodeNext would demand `.js` extensions there. Node.js types are still available only to tests, benchmarks and scripts.

## US1: the filter as users install it (T028–T042)

- **Red run (T029).** With a temporary `index.ts` that had no `ProfanityFilter`, `npx vitest run test/api.test.ts` failed at load with `TypeError: ProfanityFilter is not a constructor`.
- **Port.** `src/scan.ts`, `src/regions.ts`, `src/filter.ts` and `src/index.ts`, faithful to `ProfanityFilter.cs`, `.Scan.cs` and `.Regions.cs`. Two .NET details are kept: `MaskedPattern` trims with `char.IsLetterOrDigit(string, int)` (code points) but classifies with the `char` overload (units), and `Merge` compares entries by value.
- **Tests.** `test/api.test.ts`: **47 passed**, covering spec scenarios 1–6 and contract guarantees G2, G4–G8. The whole suite: **132 passed**. ESLint and both `tsc` configs are clean.
- **Build (tsup).** `dist/index.mjs`, `dist/index.cjs`, `dist/index.d.mts` and `dist/index.d.cts`. Three adjustments:
  - tsup's declaration build sets `baseUrl`, which TypeScript 6 rejects as deprecated, so the declaration build alone gets `ignoreDeprecations: '6.0'`;
  - in a `"type": "module"` package tsup names the ESM declarations `index.d.ts`, and ignores a `dts` extension from a config function, so `scripts/finish-build.mjs` renames the file to `index.d.mts` and checks all four files exist;
  - `ResolvedWord` (`Readonly<Required<BannedWord>>`) is exported, because public declarations reference it; the public API contract was updated.
- **Pack (T035).** `artifacts/persian-text-guard-1.2.0.tgz` has exactly the eight contract files, an unpacked size of **228,393 bytes** (under 1 MB) and version 1.2.0 read from `VERSION`. Two fixes came out of this:
  - the script's own contents check caught that `"files": ["dist"]` left out `THIRD-PARTY-NOTICES.md`, so it is now listed;
  - npm 12 prints `npm pack --json` as an object keyed by name rather than an array, and the script handles both.
- **Consumer checks (T037–T041).** `node scripts/check-consumers.mjs --no-pack`: **4 of 4 passed**.
  - `esm`: `esm ok`.
  - `cjs`: `cjs ok`.
  - `typescript`: strict, `node16`, no `@types`. `tsc -p .` succeeds, including three `@ts-expect-error` lines (an unknown option, an unknown category, a number mask) in `index.mts` and one in `index.cts`; TypeScript would fail on any of them that did not error.
  - `browser`: esbuild with `platform: 'browser'`, run in a bare `vm` context with no `require`, `process` or `Buffer`. `browser ok`: the full bundle is 94,223 characters; the normalize-only bundle is 39,563 characters and does not contain the word lists.

## US2: same answers as .NET, proven by the corpus (T043–T050)

- **Runner.** `test/corpus/load.ts`, `values.ts` and `evaluate.ts`, and `test/corpus.test.ts`, following the corpus-format runner obligations. Positions are converted to code points; text compares by built value; kind rules are checked before fields.
- **First full run (T047), local Node.js 26.4.0.** `npm run corpus`: **520 passed**, in 0.9 s.

  | Group | Passed |
  | --- | --- |
  | guards | 7 |
  | `ordinary` | 148 |
  | `must-match` | 281 |
  | `robustness` | 20 |
  | `normalization` | 36 |
  | `tokenization` | 4 |
  | `word-list-parsing` | 8 |
  | `category-selection` | 9 |
  | `mask-validation` | 7 |

  The cases add up to 513, equal to the .NET runner's count, and 0 are not applicable. No port change was needed.
- **Supported releases (T048).** fnm 1.39.0 could not install Node.js on this machine ("The system cannot move the file to a different disk drive", os error 17, even with `FNM_DIR`, `TEMP` and the working directory all on C:), and was uninstalled. Instead, the official `node-v22.23.2-win-x64.zip` and `node-v24.21.0-win-x64.zip` were downloaded from nodejs.org into the session scratchpad and checked against `SHASUMS256.txt`. Both report `process.versions.unicode` 17.0.

  | Node.js | `corpus.test.ts` | unit tests |
  | --- | --- | --- |
  | 22.23.2 | 520 passed | 132 passed |
  | 24.21.0 | 520 passed | — |

- **Failure reporting (T049)**, on scratch edits reverted with `git checkout -- conformance`:
  1. With `fa-emoji-before-word`'s `censored` set to `"😀 ####"` and `matching-persian-ordinary-messages-pass-001`'s `containsProfanity` set to `true`: **2 failed | 518 passed**.

     ```text
     Error: Case 'matching-persian-ordinary-messages-pass-001' in matching-persian.json: breaks its kind rule
       input "هر کس پلات بالاست پیام بده"
       ordinary requires containsProfanity to be false
     Error: Case 'fa-emoji-before-word' in matching-persian.json: 1 field(s) differ
       input "😀 کیر"
       expected.censored: expected "😀 ####" actual "😀 ****"
     ```

  2. With `conformance/` renamed, the first run failed with "Could not find the repository root (a directory with VERSION and conformance/)". The root lookup was then changed to look for `VERSION` and `wordlists/`, and the run fails with `Error: Conformance corpus not found: 'D:\Git\PersianTextGuard\conformance' does not exist.` (Test Files 1 failed, no tests). After restoring the corpus: 520 passed.
- **Bundled selections (T050).** The dump of `dist/index.mjs` versus feature 002's .NET dump (`artifacts/compare/current`, with names converted to lowerCamelCase and the resources section dropped): `git diff --no-index` exits 0, **0 differences**. `all` 1,250; `default` 1,025; `uncategorized` 0; `profanity` 93; `sexual` 353; `insult` 400; `slur` 146; `harassment` 33; `mild` 225.
- **`npm run lint`** (ESLint, and `tsc` for `src` and for tests, benchmarks and scripts): clean.

## US3: CI and lockstep publishing (T051–T055)

- **`.github/workflows/ci.yml` (T051, T053).** The three existing job names are unchanged. Added:
  - **`JavaScript (Node 22)` and `JavaScript (Node 24)`**: install, lint and type check, build, unit tests, corpus, README examples. On Node.js 24 also: pack, `check:package`, `api`, `api:compat` (checkout with `fetch-depth: 0`), consumer checks, and upload of the `npm-package` artifact.
  - **`Publish to npm`**: `v*` tags only, `environment: npm`, `id-token: write`, tag check, Node.js 24 with `npm@^11.5.1`, skip if the version is already published, then `npm publish <tgz> --access public`.
  - **Gating**: `Publish to NuGet` and `Publish to npm` both have `needs: [build, netfx, javascript]`.
- **`scripts/check-package.mjs` (T052).** publint `--strict` on `.pack/` reports "All good!". attw `--profile node16` on the tarball is 🟢 for node16 (from CJS), node16 (from ESM) and bundler, with "No problems found".
- **Release gate precheck (T054).** The "Check tag matches VERSION" command in Git Bash, with `VERSION` at 1.3.0:

  | `GITHUB_REF_NAME` | Exit | Output |
  | --- | --- | --- |
  | `v1.3.0` | 0 | — |
  | `v9.9.9` | 1 | `Tag v9.9.9 does not match VERSION 1.3.0` |

  Reading `ci.yml` back confirms both publish jobs need `build`, `netfx` and `javascript` and run only on `refs/tags/v`. SC-008 itself is demonstrated by the real CI dry run (T067–T070).
- **`VERSION` → 1.3.0 (T055).**
  - `npm run pack` produces `persian-text-guard-1.3.0.tgz` (8 files, 228,393 bytes unpacked).
  - `dotnet pack dotnet/src/PersianTextGuard -c Release -o artifacts` creates `PersianTextGuard.1.3.0.nupkg` and `.snupkg` with no errors or warnings. Package validation ran against the 1.2.0 baseline (`PackageValidationBaselineVersion` is still `1.2.0`), so the noncharacter and heading fixes changed no public API.

## US4: documented, measured and kept compatible (T056–T063)

- **`js/README.md` (T056).** English sections: installation, quick start, matches and positions, censoring, categories, your own words, options, normalization, validating input, the .NET-to-JavaScript table, performance, limitations and links. Then a Persian section in `<div dir="rtl">` with installation, quick start, censoring and the input-validation note.
- **README examples (T057).** `npm run test:readme`: **13 passed**, 12 `ts` blocks plus a check that there are at least 10, each run with `tsx` against `dist/index.mjs` and its printed lines compared with the block's `// → …` comments. Changing one expected value (`→ #### and ****`) made exactly 1 test fail; restored, 13 passed.
- **API report (T058).** `api-extractor.json` reads `dist/index.d.mts` directly; no plain `.d.ts` workaround was needed. `etc/persian-text-guard.api.md` lists exactly the contract's declarations, plus `ResolvedWord` and `WordListFormatError`'s constructor. `npm run api` (non-local): "API Extractor completed successfully", with no TSDoc errors. It analyses with its bundled TypeScript 5.9.3; a notice says the project uses 6.0.3, which does not affect reading declaration files.
- **API compatibility (T059).** `scripts/check-api-compat.mjs`:
  - On this branch it prints "No previous npm release report at v1.2.0 (first release); baseline only" and exits 0.
  - **Proof it catches a break.** The report was committed (`f7e103e`) and tagged locally `v1.3.0-compat-test`, followed by an empty scratch commit and an edit of the report's `censor` line to `censor(text: string, mask?: string): string;`. The script printed `BREAKING: censor(text: string | null | undefined, mask?: string): string;` and "1 declaration(s) from v1.3.0-compat-test were removed or changed; that needs a MAJOR version.", exit 1. The edit, the scratch commit and the local tag (never pushed) were then removed.
- **Benchmarks (T060).** `bench/filter.bench.ts` (tinybench 6.2.0, 2 s per task after 0.5 s warm-up) uses the .NET suite's six messages extracted verbatim from `Program.cs`, plus the 132,000-character message. Node.js v24.21.0, Intel Core i7-9700K:

  | Benchmark | Mean | Limit |
  | --- | ---: | ---: |
  | CleanShortMessage | 11.0 µs | < 50 µs (SC-005) ✅ |
  | CleanLongMessage | 92.8 µs | |
  | EvasiveMessage | 7.9 µs | |
  | NormalizeLongMessage | 32.5 µs | |
  | BuildFilterFromDefaultList | 2.4 ms | < 50 ms (SC-005) ✅ |
  | FindMatchesClean | 11.0 µs | |
  | FindMatchesDirty | 29.0 µs | |
  | CensorShortDirty | 13.2 µs | |
  | CensorLongDirty | 372 µs | |
  | VeryLongMessage | 30.7 ms | < 100 ms (spec edge case) ✅ |

  The README's performance table shows these numbers.
- **Root `README.md` (T061).** A diff of 50 insertions and 0 deletions: the npm install beside NuGet, a "Changes in 1.3.0 for .NET users" section (noncharacters and numeric headings), `js/` in the layout and a JavaScript command table, a "Releasing" section, and a Limitations bullet linking the npm README.
- **Release notes draft (T062).** `release-notes-1.3.0.md`: English notes and a Persian summary. They cover the new npm package, the noncharacter fix, the heading change, and no other .NET change.
- **Final checks (T063).**
  - `npm run lint`: clean.
  - `npm run test:all`: **665 passed** in 5 files (132 unit, 520 corpus, 13 README).
  - `npm run api`: passes.
  - `npm run api:compat`: first-release baseline.

## Full matrix from a clean state (T064)

`js/node_modules`, `js/dist`, `js/artifacts`, the contents of `js/.pack`, and every `bin/` and `obj/` under `dotnet/` were deleted first.

- **.NET**:
  - `dotnet build dotnet/PersianTextGuard.slnx`: 0 warnings, 0 errors.
  - `PersianTextGuard.Tests`: 1,029 × 3.
  - `PersianTextGuard.Conformance`: 522 × 3 (`net8.0`, `net10.0`, `net48`).
- **JavaScript** (`npm ci`: 291 packages):

  | Step | Result |
  | --- | --- |
  | `npm run test:all` | 665 passed |
  | `npm run pack` | `persian-text-guard-1.3.0.tgz`, 8 files, **242,197 bytes unpacked** (under 1 MB; the README grew) |
  | `check:package` | publint "All good!", attw "No problems found" |
  | `api` | completed successfully |
  | `api:compat` | first-release baseline |
  | `check:consumers -- --no-pack` | 4 of 4 |

- **Found and fixed**: `npm run lint` failed (exit 2) straight after `npm ci`, before `dist/` existed. `bench/filter.bench.ts` imports `../dist/index.mjs` and was type-checked by `tsconfig.node.json`. CI runs lint before build, so this would have failed there. `bench` was removed from that config's `include`, and `npm run lint` now passes with `dist/` deleted (exit 0).

## Quickstart walkthrough (T065)

| § | Expected outcome | Result | From |
| --- | --- | --- | --- |
| 1 | 1,029 .NET tests × 3 | ✅ | T017, T064 |
| 1 | Corpus passes × 3, including noncharacter and heading cases | ✅ 522 × 3 | T017, T064 |
| 1 | `[3]`, `[+4]`, `[ 03 ]` are errors; `[ Insult ]`, `[SLUR]` accepted | ✅ | T017 |
| 1 | `--check` 0 disagreements | ✅ | T017 |
| 1 | No raw noncharacters in `conformance/` | ✅ | T017 |
| 1 | .NET benchmarks before/after, README table refreshed | ✅ | T014, T018 |
| 1 | Regression proof on 1.2.0 code | ✅ | T066 |
| 2 | Lint and type check clean; unit tests pass on Node.js 22 and 24 | ✅ 132 on 22.23.2 and on 26.4.0; CI runs 22 and 24 | T048, T063, T064 |
| 2 | `TypeError`, `RangeError`, `WordListFormatError`, frozen results, R1 cases | ✅ | T028, T021, T026 |
| 3 | Corpus passes on Node.js 22 and 24, 0 not applicable | ✅ 520 on 22.23.2 and 24.21.0 | T047, T048 |
| 3 | Failure reporting; missing corpus is "not found" | ✅ | T049 |
| 4 | Bundled lists equal .NET, 0 differences | ✅ | T050 |
| 5 | Tarball has 8 files, < 1 MB, no dependencies | ✅ 242,197 bytes | T035, T064 |
| 5 | publint and attw clean | ✅ | T052 |
| 5 | API report matches; `api:compat` baseline, and fails on a changed declaration | ✅ | T058, T059 |
| 5 | 4 of 4 consumer checks; normalize-only bundle without word lists | ✅ | T041, T064 |
| 5 | Version from `VERSION` | ✅ 1.2.0 tarball before T055, 1.3.0 after | T035, T055 |
| 6 | README examples pass | ✅ 13 | T057 |
| 6 | Benchmarks under limits, README table filled | ✅ 2.4 ms, 11.0 µs, 30.7 ms | T060 |
| 6 | npm README bilingual; root README updated | ✅ | T056, T061 |
| 6 | Onboarding (SC-003) under 5 minutes | ✅ JS 4.0 s, TS 4.5 s (scripted) | below |
| 7 | Tag gate dry run: failing port blocks both, green run publishes (dry), mismatched tag blocks both; nothing published | ✅ 3 runs | T067–T070 |
| 7 | CI green on the pull request (4 jobs pass, both publish jobs skipped) | ✅ PR #4 | T071 |
| 7 | Merge, release and registries | ✅ 1.3.0 on npm and NuGet | T072–T077 |

**Onboarding (SC-003).** In two empty directories under the session scratchpad, a script followed only the npm README's installation and quick-start steps, installing `js/artifacts/persian-text-guard-1.3.0.tgz` in place of the registry name:
1. `npm init -y`;
2. `npm install <tarball>`;
3. save the quick-start block as `index.mjs` (JavaScript) or `index.ts` (TypeScript, run by Node.js 26's built-in type stripping);
4. run it.

Both printed `false true true`: the JavaScript project in **3,963 ms**, the TypeScript project in **4,475 ms**. No step outside the README was needed. The timings are scripted, so they measure the steps, not a person reading and typing; both are far under 5 minutes.

## Regression proof on 1.2.0 code (T066)

`git worktree add ../ptg-1.2.0 main` (the 1.2.0 code) was given the branch's `conformance/`, then `dotnet test ../ptg-1.2.0/dotnet/tests/PersianTextGuard.Conformance`:

| Target | Result | Failing cases |
| --- | --- | --- |
| `net8.0` | Failed 7, Passed 515 | U+FFFE: `robustness-noncharacter-fffe-between-words`, `-fffe-alone`, `-fffe-inside-persian-word`, `normalization-noncharacter-fffe-comparison`; headings: `word-list-parsing-heading-number`, `-signed-number`, `-spaced-number` |
| `net10.0` | Failed 7, Passed 515 | the same 7 |
| `net48` | Failed 15, Passed 507 | all 12 noncharacter cases and the 3 numeric headings |

`word-list-parsing-heading-name-spaced-and-upper-case` passes on 1.2.0 too, as it should. The worktree was removed.

## SC-008: release gates dry run (T067–T070)

The scratch branch `dryrun/release-gates` had `ci.yml` edited so that nothing could publish: the NuGet login and push steps were replaced by `ls -l artifacts/*.nupkg` plus an echo, and `npm publish` carried `--dry-run`. Before each commit, a grep confirmed that no `dotnet nuget push`, no `NuGet/login` and no `npm publish` without `--dry-run` remained.

**Deviations from tasks.md, and what they found.**

1. **Invalid YAML in the edit, not in the real workflow.** The first push (`v0.0.0-dryrun.1`) gave a run with no jobs: "You have an error in your yaml syntax on line 162". The replacement push line, `run: ls ... && echo "DRY RUN: would push to NuGet"`, is a plain scalar containing `": "`. It was rewritten as a `run: |` block. From then on, each version of the file was also checked with the `yaml` package, which reproduces GitHub's error on the bad line and passes the feature branch's `ci.yml`.
2. **Versions 1.3.0-dryrun.N instead of 0.0.0-dryrun.N.** With `v0.0.0-dryrun.1`, `Build, test, pack` failed at Pack: .NET package validation reported `CP0003: assembly version '0.0.0.0' should be equal to or higher than [Baseline] ... '1.2.0.0'`. That is a real gate working correctly, since a release can't go backwards, but it would hide the gate being tested. The runs below therefore use `1.3.0-dryrun.N`, a prerelease that sorts below 1.3.0; the stubbed steps still could not publish it.
3. **A real bug in the npm publish step, now fixed on this branch.** The first green-path run failed in `Publish to npm`: `npm publish "npm-package/persian-text-guard-….tgz"` was read as the GitHub repository `npm-package/persian-text-guard-….tgz` (`git ls-remote ssh://git@github.com/npm-package/...`, "Permission denied (publickey)"). The real 1.3.0 release would have failed the same way, after NuGet had published. The step now publishes `./npm-package/...`. It also passes `--tag latest`, or `--tag next` for a prerelease version, because npm refuses a prerelease without a dist-tag. The corrected command was also run locally with `--dry-run` on the 1.3.0 tarball: "Publishing to https://registry.npmjs.org/ with tag latest and public access (dry-run)", 8 files. The fix was cherry-picked onto the scratch branch, and run 2 was repeated.

**Run 1: a failing port blocks both registries.** Tag `v1.3.0-dryrun.1`, `VERSION` 1.3.0-dryrun.1, with a `run: exit 1` step in the `javascript` job (run 35371454545):

| Job | Conclusion |
| --- | --- |
| Build, test, pack | success |
| Test on .NET Framework 4.8 (netstandard2.0 build) | success |
| JavaScript (Node 22) | failure |
| JavaScript (Node 24) | failure |
| Publish to NuGet | skipped |
| Publish to npm | skipped |

**Run 2: every gate green.** The failure step was removed. Tag `v1.3.0-dryrun.2`, `VERSION` 1.3.0-dryrun.2 (run 35372104389): all six jobs succeeded, and both publish jobs ran in their environments, so the `npm` environment's `v*` rule admits release tags.
- Publish to NuGet: `artifacts/PersianTextGuard.1.3.0-dryrun.2.nupkg` (146,444 bytes), "DRY RUN - would push to NuGet".
- Publish to npm: the tag check passed and the "already published" check did not trigger. Output: `total files: 8`, "Publishing to https://registry.npmjs.org/ with tag next and public access (dry-run)", `+ persian-text-guard@1.3.0-dryrun.2`.

**Run 3: tag and VERSION disagree.** Tag `v1.3.0-dryrun.3` on the same commit (run 35372358918). The four build and test jobs succeeded. `Publish to NuGet` and `Publish to npm` both failed at "Check tag matches VERSION", each printing "Tag v1.3.0-dryrun.3 does not match VERSION 1.3.0-dryrun.2".

**Cleanup.**
- All dry-run tags (`v0.0.0-dryrun.1` and `v1.3.0-dryrun.1` to `.3`) and the `dryrun/release-gates` branch were deleted locally and on GitHub. `git ls-remote` shows no dry-run refs.
- `npm view persian-text-guard versions` prints `["0.0.1"]`. NuGet lists 1.0.0, 1.0.1, 1.1.0 and 1.2.0 only.
- `VERSION` on `003-javascript-typescript-port` is `1.3.0`, and its `ci.yml` contains no `DRY RUN` or `--dry-run`. Its only change from the reviewed workflow is the publish-path fix above.

## Release 1.3.0 (T072–T077)

- **T072.** PR #4 was merged (merge commit `d24bf06`). CI on main (run 35373299911): the four build and test jobs succeeded, and both publish jobs were skipped.
- **T073.** Main's required checks are now "Build, test, pack", "Test on .NET Framework 4.8 (netstandard2.0 build)", "JavaScript (Node 22)" and "JavaScript (Node 24)".
- **T074.** Before tagging, main was green, `VERSION` was `1.3.0`, npm listed only `0.0.1`, and NuGet listed up to `1.2.0`. The release run for tag `v1.3.0` (run 35373550302) succeeded on all six jobs:
  - NuGet: "Your package was pushed".
  - npm: "Publishing to https://registry.npmjs.org/ with tag latest and public access", "Signed provenance statement with source and build information from GitHub Actions", and `+ persian-text-guard@1.3.0`.
- **T075.** NuGet lists 1.0.0, 1.0.1, 1.1.0, 1.2.0 and 1.3.0. npm's `latest` is `1.3.0`, and its provenance predicate is `https://slsa.dev/provenance/v1`. In a fresh project:
  - `npm install persian-text-guard@1.3.0` with `require` printed `true **** and ****`;
  - `npm audit signatures` reported "1 package has a verified attestation".
- **T076.** GitHub release created: https://github.com/AmirehsanK/PersianTextGuard/releases/tag/v1.3.0
- **T077.** The CLI session token had expired, and `npm login --auth-type=web` can't finish without an interactive terminal. The user deprecated 0.0.1 themselves. `npm view persian-text-guard@0.0.1 deprecated` prints "Placeholder; use 1.3.0 or later", and `npm view persian-text-guard dist-tags.latest` prints `1.3.0`.
