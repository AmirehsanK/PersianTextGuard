# Quickstart: Validating Find Every Match and Censor

**Feature**: [spec.md](spec.md) | **Contract**: [contracts/public-api.md](contracts/public-api.md)

How to prove the feature works end to end once it is implemented. It describes scenarios and
expected outcomes; the tests themselves are written during implementation.

## Prerequisites

- .NET SDK 10 (builds all three targets) and .NET 8 runtime.
- Windows for the .NET Framework 4.8 run, which exercises the `netstandard2.0` build.
- The feature branch checked out: `001-censor-find-matches`.

## 1. Build and run the full test suite

```bash
dotnet test tests/PersianTextGuard.Tests
```

**Expected**: all tests pass on `net10.0`, `net8.0` and `net48`. The 225 tests that existed in 1.1.0
pass without having been edited (SC-008) — confirm with:

```bash
git diff main --stat -- tests/PersianTextGuard.Tests/DefaultListTests.cs tests/PersianTextGuard.Tests/ProfanityFilterTests.cs tests/PersianTextGuard.Tests/PersianNormalizerTests.cs
```

**Expected**: no output, meaning none of those three files was modified. `ReadmeExampleTests.cs`
may only have methods added.

## 2. Scenario checks

Each row is covered by a test in `FindMatchesTests`, `CensorTests` or `ConsistencyTests`. Run one
group with, for example, `dotnet test tests/PersianTextGuard.Tests --filter FullyQualifiedName~CensorTests`.
The filter under test is `new ProfanityFilter(WordList.PersianDefault)`.

### Every match (User Story 1)

| Input | Expected `FindMatches` | Covers |
| --- | --- | --- |
| `you bitch, kos kesh` | 2 matches, in order: `bitch` at (4, 5), then an `Insult` match at (11, 8) covering `kos kesh` | FR-001, FR-004, FR-007 |
| `kir kir kir` | 3 matches at indexes 0, 4, 8, each length 3 | FR-005 |
| `sh1t and f u c k` | 2 matches; the second has `SplitWord` in `Evasion` and spans `f u c k` (length 7) | FR-002, FR-003 |
| `سلام، سفارشم کی میرسه؟` | empty | FR-006 |
| `ﻛﻴﺮ` (presentation forms) | 1 match at index 0, length 3 — positions in the original, not the normalised text | FR-003 |
| `motherfucker` | 1 match: the whole-word entry, spanning 12 | FR-007 |

### Censoring (User Story 2)

| Input | Expected `Censor` | Covers |
| --- | --- | --- |
| `this is kir` | `this is ****` | FR-008 |
| `كتاب‌هاي خوب` (clean, Arabic yeh and ZWNJ) | the same instance, unchanged | FR-009, FR-010 |
| `جنده‌ها رو ببین` | `**** رو ببین` — the suffix is gone with the word | FR-011 |
| `kir and motherfucker` | `**** and ****` — identical masks regardless of length | FR-012 |
| `f.u.c.k off` | `**** off` | FR-013 |
| `ج.نده` | `****` | FR-013 |
| `کیر😂` | `****😂` | FR-011 |
| `k kos i kos r` | output for which `ContainsProfanity` is `false` | FR-015 (R6) |
| `kir`, mask `#` | `####` | FR-014 |
| `kir`, mask `x` | throws `ArgumentException` | FR-014 |

### Consistency and robustness (User Story 3)

| Check | Expected | Covers |
| --- | --- | --- |
| Over every message in the corpus (all `DefaultListTests` inline data): yes/no, first-match, every-match and censoring agree on clean vs dirty | 0 disagreements | FR-016 |
| Same corpus: the first-match region lies inside an every-match region | 0 violations | FR-016 |
| Same corpus: `ContainsProfanity(Censor(t))` | `false` for every message | FR-015, SC-003 |
| Same corpus: text outside every region is unchanged and every region reads `****` | 0 violations | FR-009, SC-004 |
| `null`, `""`, `"   "`, `"hi \uD83D"`, a 132,000-character message | results returned, no exception | FR-017, SC-007 |
| 20,000 parallel calls on one shared filter mixing all four capabilities | results equal to sequential calls | FR-020 |

## 3. Performance

```bash
dotnet run -c Release --project benchmarks/PersianTextGuard.Benchmarks -f net10.0 -- --filter '*'
```

**Expected**, on the machine the README names:

| Benchmark | Target | Covers |
| --- | --- | --- |
| `CleanShortMessage` (yes/no) | within 5% of 1.1.0's 2.4 µs | SC-005 |
| `FindMatchesClean` | ≤ 1.5 × `CleanShortMessage` | SC-005 |
| `CensorLongDirty` (60 words) | < 100 µs | SC-006 |

Record the before-and-after table in the pull request (constitution, Principle IV).

## 4. Package compatibility

```bash
dotnet pack src/PersianTextGuard -c Release -o artifacts
```

**Expected**: pack succeeds with package validation enabled against the 1.1.0 baseline — only
additions, no breaking changes ([contract](contracts/public-api.md#compatibility)).

## 5. Documentation

- The README has examples for `FindMatches` and `Censor`, each with a matching `ReadmeExampleTests`
  method.
- README Limitations no longer says `FindMatch` "reports the first match, not every match or its
  position".
- The release notes call out the `ProfanityMatch` equality change (R7).
