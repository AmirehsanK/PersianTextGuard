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
