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
