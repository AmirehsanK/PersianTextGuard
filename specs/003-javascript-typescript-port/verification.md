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
