# Quickstart: Validating the Restructure and the Conformance Corpus

**Feature**: [spec.md](spec.md) | **Contracts**: [corpus format](contracts/corpus-format.md), [repository and tooling](contracts/repository-and-tooling.md)

How to prove the feature is done. Each section names the requirements it checks.

## Prerequisites

- .NET SDK 10 and the .NET 8 runtime; Windows for the `net48` run.
- The feature branch `002-monorepo-conformance-corpus` checked out.
- Network access to nuget.org, for the 1.2.0 comparison package.

## 1. Layout and single sources (FR-014, FR-017, FR-018, SC-004)

```bash
git ls-files | grep -E '(^|/)(persian|finglish|english)\.txt$'
git ls-files | grep -E '^(src|tests|benchmarks)/|^PersianTextGuard\.slnx$|^Directory\.Build\.props$'
git grep -n "<Version>" -- '*.csproj' '*.props'
cat VERSION
```

**Expected**:
- **Word lists**: exactly three files, all under `wordlists/`.
- **Old paths**: no output.
- **`<Version>`**: only the `dotnet/Directory.Build.props` line that reads `VERSION`.
- **`VERSION`**: `1.2.0`.

## 2. History survived the moves (FR-019, SC-007)

```bash
git log --follow --oneline -- dotnet/src/PersianTextGuard/ProfanityFilter.cs | tail -3
git log --follow --oneline -- wordlists/persian.txt | tail -3
```

**Expected**: both show commits from before the restructure, including 1.0.0's
`889a667 PersianTextGuard 1.0.0 …` for the filter. For `persian.txt`, the history includes its time as
`persian-default.txt` in 1.0.x.

## 3. Nothing changed for .NET users (FR-016, FR-022, FR-023, SC-003, SC-009)

```bash
dotnet test dotnet/tests/PersianTextGuard.Tests
dotnet pack dotnet/src/PersianTextGuard -c Release -o artifacts
```

**Expected**:
- **Tests**: 1,029 passed on each of `net10.0`, `net8.0` and `net48`.
- **Pack**: succeeds with package validation against **1.2.0** and no compatibility errors, producing
  `artifacts/PersianTextGuard.1.2.0.nupkg`.

Then run the one-time comparisons from research R14, recording their output in the pull request:

| Comparison | Expected |
| --- | --- |
| Entries of `WordList.All`, `PersianDefault` and each single-category selection: published 1.2.0 vs new build, entry by entry in order | 0 differences |
| Files in the published 1.2.0 `.nupkg` vs the new one, excluding `.nuspec` version and signature metadata | 0 differences, embedded resource names included |

## 4. The corpus exists and .NET passes it (FR-001–FR-013, SC-001, SC-002, SC-006, SC-008)

```bash
dotnet test dotnet/tests/PersianTextGuard.Conformance
```

**Expected**:
- **Results**: every case passes on each of `net10.0`, `net8.0` and `net48`, with 0 not-applicable
  cases for .NET.
- **Guard tests pass**:
  - the corpus loads and has at least 300 cases;
  - ids are unique;
  - no case is pending;
  - no unescaped invisible characters;
  - every `[InlineData]` message from the existing tests is in the corpus.
- **Time**: the `net10.0` run finishes in under 30 seconds (SC-008).

**Coverage check (SC-002)**: count cases per kind. Every kind is present: `ordinary`, `must-match`,
`robustness`, `normalization`, `tokenization`, `word-list-parsing`, `category-selection` and
`mask-validation`.

## 5. Failures are reported properly (FR-012, FR-013, SC-006)

Do this on a scratch commit and discard it afterwards.

1. In `conformance/cases/matching-persian.json`, change one `must-match` case's `expected.censored`, and
   one `ordinary` case's `expected.containsProfanity` to `true`.
2. Run `dotnet test dotnet/tests/PersianTextGuard.Conformance -f net10.0`.

**Expected**:
- Both cases fail in the same run.
- Each failure shows the case id, the file, the input with invisible characters as `\uXXXX`, and the
  expected and actual field.
- The ordinary case is reported as breaking its kind rule (data model).

Then point the runner at a missing corpus directory (for example by renaming `conformance/` locally).
The run fails with a message that the corpus was not found; it does not pass with zero cases.

## 6. Adding cases (FR-010, FR-010a, SC-005)

Do this on a scratch commit and discard it afterwards.

1. Add a new ordinary case with `expected` written by hand. Run the corpus: it passes, and it took under
   5 minutes.
2. Add a new must-match case **without** `expected`, for example `"input": "this is kir"`. Run
   `dotnet test …Conformance`: it fails with "pending case — run the fill-in tool".
3. Run `dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill`. The case gains `expected` with
   exact positions and evasion, only that file changes, and the corpus passes.
4. Hand-edit an **existing** case's `expected.matches[0].start` to a wrong value. Run the fill-in tool:
   it prints the disagreement, exits `1`, and `git diff` shows the case unchanged by the tool.

## 7. CI and release (FR-024, research R13, R15)

- Open the pull request. Both CI jobs are green with the same job names as before, and the logs show the
  conformance tests running on `net8.0`, `net10.0` and `net48`.
- **Tag check**: in a fork or a dry run, push a tag `v9.9.9` while `VERSION` is `1.2.0`. The publish job
  fails at the version check before pushing anything.

## 8. Documentation (FR-020, FR-021)

```bash
git grep -nE '(^|[^[:alnum:]_/])(src|tests|benchmarks)/PersianTextGuard' -- ':!specs/001-censor-find-matches' ':!specs/002-monorepo-conformance-corpus'
```

**Expected**: no output. Every reference points at the new paths. The pattern ignores the new
`dotnet/src/PersianTextGuard`-style paths, because an old path only counts when it is not preceded by a
letter, digit, `_` or `/`. Spec 001 is exempt as a historical record, and spec 002 because it
describes the move from the old paths.

The README's "Performance" and development sections show the new commands. `conformance/README.md`
explains the format, how to add a case and how to use the fill-in tool.
