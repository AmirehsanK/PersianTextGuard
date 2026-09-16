# Verification: Monorepo with Shared Word Lists and a Conformance Corpus

**Feature**: [spec.md](spec.md) | **Tasks**: [tasks.md](tasks.md) | **Quickstart**: [quickstart.md](quickstart.md)

Results of the one-time checks run while implementing this feature, in task order.

## Baseline (before restructure)

- **Commit**: `274d186` on `002-monorepo-conformance-corpus`, clean working tree (T001).
- **`dotnet test tests/PersianTextGuard.Tests`**:

  | Target | Passed | Failed |
  | --- | --- | --- |
  | `net8.0` | 1,029 | 0 |
  | `net10.0` | 1,029 | 0 |
  | `net48` | 1,029 | 0 |

- **Published package** (T002): `PersianTextGuard 1.2.0` restored from nuget.org into
  `artifacts/compare/published/`, and `persiantextguard.1.2.0.nupkg` (155,863 bytes) copied to
  `artifacts/compare/`. Nothing under `artifacts/` is committed.

## Restructure (T003–T007)

- **T003**: `.gitattributes` committed on its own as `4a4691e` "Pin LF line endings for shared data".
- **T004**: `0efdd96` "Move .NET port under dotnet/ and word lists to wordlists/ (renames only)".
  `git show --stat -M HEAD`: **27 files changed, 0 insertions(+), 0 deletions(-)**; every entry is
  `R100` (22 files under `src/`, `tests/` and `benchmarks/`, the solution and build props, and the
  3 word lists `{src/PersianTextGuard/WordLists => wordlists}/*.txt`).
- **T005–T007**: `84ddfbf` "Fix paths after moving the .NET port" (project file paths and CI).
  - `dotnet build dotnet/PersianTextGuard.slnx`: 0 warnings, 0 errors.
  - `dotnet test dotnet/tests/PersianTextGuard.Tests`:

    | Target | Passed | Failed |
    | --- | --- | --- |
    | `net8.0` | 1,029 | 0 |
    | `net10.0` | 1,029 | 0 |
    | `net48` | 1,029 | 0 |

## Corpus recorded from 1.2.0 (T021–T023)

`dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill -- --seed` (exit `0`):

```text
Seeded 491 pending case(s) into 10 file(s)
Supplementary suite: 305 tuples; 203 included, 33 skipped as disagreeing with 1.2.0, 69 skipped as duplicates
Filled 497 case(s); 0 disagreement(s)
```

The 497 filled cases are the 491 seeded ones plus the 6 hand-written cases from T022. A second
`--seed` run adds 0 cases and changes no file; `--check` reports 0 disagreements.

**Cases per kind** (497 in total, at least 300 required):

| Kind | Cases |
| --- | --- |
| `ordinary` | 148 |
| `must-match` | 281 |
| `robustness` | 11 |
| `normalization` | 33 |
| `tokenization` | 4 |
| `word-list-parsing` | 4 |
| `category-selection` | 9 |
| `mask-validation` | 7 |

**Supplementary suite** (`D:\Git\PtgTest\Program.cs`, 305 tuples): 203 included, 69 skipped as
duplicates of cases from the existing tests, and 33 skipped because 1.2.0 disagrees with the hand
label. Those 33 are known gaps or judgment calls, deliberately **not** recorded as expectations:

```text
[FA evasion] "جن ده" labelled bad
[FA evasion] "ک🍆ر" labelled bad
[Finglish] "kosher nagoo" labelled bad
[Finglish] "pedar sokhte" labelled bad
[Finglish] "koni" labelled bad
[Finglish] "k*s kesh" labelled bad
[EN plain] "wtf" labelled bad
[EN plain] "damn" labelled bad
[EN evasion] "fuc k" labelled bad
[EN evasion] "cvnt" labelled bad
[FA clean] "پدر سگ داره" labelled clean
[FA clean] "این ساک زدن نداره" labelled clean
[FA clean] "کیری ایروینگ" labelled clean
[FA clean] "جا کش رو بده" labelled clean
[FA clean] "کون فیکون" labelled clean
[FA clean] "دیوس" labelled clean
[FA clean] "شهوتی" labelled clean
[FA clean] "ممه" labelled clean
[FA clean] "فاک" labelled clean
[Finglish clean] "koss headphones" labelled clean
[Finglish clean] "kose" labelled clean
[Finglish clean] "Kir Royale cocktail" labelled clean
[Finglish clean] "jaa kesh" labelled clean
[EN clean] "summa cum laude" labelled clean
[EN clean] "Cumming, Georgia" labelled clean
[EN clean] "Dick Van Dyke" labelled clean
[EN clean] "a cock crowed" labelled clean
[EN clean] "$hit points" labelled clean
[EN clean] "a s s" labelled clean
[EN clean] "cum" labelled clean
[EN clean] "Twatt, Orkney" labelled clean
[EN clean] "kys = key size" labelled clean
[EN clean] "b1 tch" labelled clean
```

**`dotnet test dotnet/tests/PersianTextGuard.Conformance`** (497 case tests plus 9 guard tests):

| Target | Passed | Failed |
| --- | --- | --- |
| `net8.0` | 506 | 0 |
| `net10.0` | 506 | 0 |
| `net48` | 506 | 0 |

**Readability review**: Persian, Finglish and emoji are written literally; zero-width and bidi
characters appear as `\u200C`-style escapes; word-list texts use `\n` and `\r\n`; lone surrogates
and the two 132,000-character robustness inputs are `build` objects, and their censored outputs reuse
the input's parts (`repeat` × 2000 followed by `{ "text": " ****" }`).

## CI runs the corpus (T024)

`.github/workflows/ci.yml`: the `Build, test, pack` job adds "Conformance corpus (.NET 8 and .NET 10)",
`dotnet test dotnet/tests/PersianTextGuard.Conformance -c Release --no-build`; the
`Test on .NET Framework 4.8 (netstandard2.0 build)` job adds "Conformance corpus",
`dotnet test dotnet/tests/PersianTextGuard.Conformance -c Release -f net48`. Job names unchanged.

## Failure reporting (T025, quickstart §5 and §6)

All on scratch edits, reverted with `git checkout -- conformance` afterwards; `git status` clean for
`conformance/` after each step.

1. **Hand-written case, then two corrupted cases.**
   - A new `ordinary` case with `expected` written by hand, appended to `matching-english.json`:
     the corpus passes (507 on `net10.0`). Elapsed from the edit to the passing run: **6 s**.
   - Then `fa-emoji-before-word`'s `expected.censored` set to `"😀 ####"`, and
     `matching-persian-ordinary-messages-pass-001`'s `containsProfanity` set to `true`. On `net10.0`:
     **Failed: 2, Passed: 505** in the same run.

     ```text
     Case 'fa-emoji-before-word' in matching-persian.json: 1 field(s) differ
       input "😀 کیر"
       expected.censored: expected "😀 ####" actual "😀 ****"

     Case 'matching-persian-ordinary-messages-pass-001' in matching-persian.json: breaks its kind rule
       input "هر کس پلات بالاست پیام بده"
       ordinary requires containsProfanity to be false
     ```

2. **Missing corpus.** With `conformance/` renamed: Failed 11, Passed 1 (the writer test, which
   needs no corpus). Messages: `Conformance corpus not found at 'D:\Git\PersianTextGuard\conformance'.`
   and `DirectoryNotFoundException: Conformance corpus not found: … does not exist.` It does not pass
   with zero cases.

3. **Pending case.** A must-match `"this is kir"` case without `expected`: the corpus fails with
   `pending case — run the fill-in tool: dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill`
   (the case theory and `No_case_is_pending`). The tool prints `Wrote matching-english.json`,
   `Filled 1 case(s); 0 disagreement(s)`, exit `0`; `git diff --stat` shows only
   `matching-english.json`, 24 insertions, all inside the new case, with `start` 8 and `length` 3. The
   corpus then passes (507). Elapsed from the edit to the passing run: **9 s**.

4. **Disagreement.** `fa-zwnj-between-every-letter`'s `firstMatch.start` hand-edited from 0 to 1. The
   tool prints `DISAGREES fa-zwnj-between-every-letter expected.firstMatch.start: expected 1 actual 0`,
   `Filled 0 case(s); 1 disagreement(s)`, exit `1`, and `git diff` is identical to the hand edit. The
   runner's message shows the input escaped: `input "ک\u200Cی\u200Cر"`.

Both timed steps are far under the 5-minute limit (SC-005). They were run by script, so they measure
the tooling, not a person's typing.

## Shared word lists equal 1.2.0 (T026–T029)

**T026, bundled entries.** Two throwaway console projects under the git-ignored `artifacts/compare/`:
`published/` references the `PersianTextGuard 1.2.0` package, `current/` references
`dotnet/src/PersianTextGuard/PersianTextGuard.csproj`. Both run the same `Program.cs`, which writes
`## <selection> <count>` and then `text<TAB>mode<TAB>category` per entry, in order, for `WordList.All`,
`WordList.PersianDefault` and `WordList.Bundled(c)` for every `WordCategory`, then the sorted manifest
resource names.

```bash
dotnet run --project artifacts/compare/published > artifacts/compare/published.txt
dotnet run --project artifacts/compare/current > artifacts/compare/current.txt
git diff --no-index artifacts/compare/published.txt artifacts/compare/current.txt
```

Result: **0 differences** (exit `0`; 3,538 lines each). Selections: `all` 1,250, `default` 1,025,
`Uncategorized` 0, `Profanity` 93, `Sexual` 353, `Insult` 400, `Slur` 146, `Harassment` 33, `Mild` 225.
Resources: `PersianTextGuard.WordLists.english.txt`, `….finglish.txt`, `….persian.txt`.

**T027, one copy of each list.** `git ls-files | grep -E '(^|/)(persian|finglish|english)\.txt$'`
prints exactly `wordlists/english.txt`, `wordlists/finglish.txt`, `wordlists/persian.txt`.

**T028, an edit is picked up.** `zzqqtestword` added after `[insult]` in `wordlists/english.txt`
(line 294). After a rebuild, `artifacts/compare/current` lists it in `## all 1251`, `## default 1026`
and `## Insult 401`. The corpus on `net10.0`: Failed 3, Passed 503 —
`category-selection-default-list-tests-002` (`"all"`, count expected 1250 actual 1251),
`…-005` (`["insult"]`, 400 → 401) and `…-001` (`"default"`, 1025 → 1026). Reverted with
`git checkout -- wordlists`; the corpus passes again (506).

**T029.** `README.md`'s three list links and `THIRD-PARTY-NOTICES.md`'s path now point at
`wordlists/` (`3e40a38`).

## Single version source and "nothing changed" (T030–T034)

**T030** (`1b4a951`). `dotnet/Directory.Build.props` sets
`<Version>$([System.IO.File]::ReadAllText('$(MSBuildThisFileDirectory)../VERSION').Trim())</Version>`;
`<Version>1.2.0</Version>` is gone from `PersianTextGuard.csproj`, and
`PackageValidationBaselineVersion` is `1.2.0`. `git grep -n "<Version>" -- '*.csproj' '*.props'`
prints only `dotnet/Directory.Build.props:8`.

**T031, pack.**
1. `dotnet pack dotnet/src/PersianTextGuard -c Release -o artifacts` created
   `artifacts/PersianTextGuard.1.2.0.nupkg` (and `.snupkg`) with no errors or warnings. The detailed
   log shows `RunPackageValidation` loading `lib/netstandard2.0`, `lib/net8.0` and `lib/net10.0` from
   both the published `persiantextguard.1.2.0.nupkg` and the new package: API compatibility against
   1.2.0 passed.
2. With `VERSION` temporarily `1.2.1`, pack created `PersianTextGuard.1.2.1.nupkg`. `VERSION` was
   restored to `1.2.0` (`git status` clean).

**T032, package contents.** Entry names of the published and the new `.nupkg`, sorted, ignoring
`.nuspec`, `_rels/`, `package/services/metadata/` and `[Content_Types].xml`:

```text
icon.png
lib/net10.0/PersianTextGuard.dll
lib/net10.0/PersianTextGuard.xml
lib/net8.0/PersianTextGuard.dll
lib/net8.0/PersianTextGuard.xml
lib/netstandard2.0/PersianTextGuard.dll
lib/netstandard2.0/PersianTextGuard.xml
README.md
THIRD-PARTY-NOTICES.md
```

The only difference is `.signature.p7s`, present in the published package only: the repository
signature nuget.org adds on upload, which is signature metadata (quickstart §3). **0 content
differences.** Embedded resource names were compared in T026.

**T033** (`1b4a951`). The pack step no longer passes `-p:Version`; the `Publish to NuGet` job checks
out the repository and runs "Check tag matches VERSION" before pushing. The step's exact command, run
locally in Git Bash:

| `GITHUB_REF_NAME` | Exit | Output |
| --- | --- | --- |
| `v1.2.0` | `0` | — |
| `v9.9.9` | `1` | `Tag v9.9.9 does not match VERSION 1.2.0` |

**T034** (`28cc4cf`). README "Performance" uses
`dotnet run -c Release --project dotnet/benchmarks/PersianTextGuard.Benchmarks -f net10.0` (verified:
`-- --list flat` lists the benchmarks). A new "Development" section shows the layout, links
`conformance/README.md` as the specification, and lists commands verified in T007, T023, T031 and
here.

## History and references (T035)

1. **History.**
   - `git log --follow --oneline -- dotnet/src/PersianTextGuard/ProfanityFilter.cs | tail -1` →
     `889a667 PersianTextGuard 1.0.0: …` ✅
   - `git log --follow --oneline -- wordlists/persian.txt | tail -1` →
     `25b3280 Fix evasion gaps, categorise the word lists; version 1.1.0`, **not** `889a667`.
     The move in this feature is a 100% rename (T004), so it is not the cause. In 1.1.0 the file
     `persian-default.txt` was rewritten as `persian.txt` (701 lines changed), which is below git's
     default 50% rename similarity. With a lower threshold the full history is followed:
     `git log --follow -M30% --oneline -- wordlists/persian.txt | tail -1` → `889a667`. This predates
     the feature and cannot be changed without rewriting published history.
2. **References.** The prescribed `git grep -nE '(^|[^[:alnum:]_/])(src|tests|benchmarks)/PersianTextGuard' …`
   is **not empty**, but every match is a correct path relative to `dotnet/`:
   - `README.md` lines 242–245: the layout tree's entries nested under `dotnet/` (`│   ├── src/PersianTextGuard/`), copied from the contract as T034 requires;
   - `dotnet/PersianTextGuard.slnx`: the solution's project paths, relative to the solution in `dotnet/`.

   A narrower search for old paths used as links, code spans, `--project` arguments or CI `run:`
   commands finds nothing, and `git ls-files | grep -E '^(src|tests|benchmarks)/|^PersianTextGuard\.slnx$|^Directory\.Build\.props$'`
   is empty. No stale reference remains (FR-020).

## Final matrix from a clean build (T036)

All `bin/` and `obj/` directories under `dotnet/` deleted first.

- `dotnet build dotnet/PersianTextGuard.slnx`: **0 warnings, 0 errors**.
- Tests:

  | Project | `net8.0` | `net10.0` | `net48` |
  | --- | --- | --- | --- |
  | `PersianTextGuard.Tests` | 1,029 passed | 1,029 passed | 1,029 passed |
  | `PersianTextGuard.Conformance` | 506 passed | 506 passed | 506 passed |

- **SC-008**: `dotnet test dotnet/tests/PersianTextGuard.Conformance --no-build -f net10.0` reports a
  586 ms test duration, 2.1 s wall time including the test host. The limit is 30 s.

## Quickstart walkthrough (T037)

| § | Expected outcome | Result | From |
| --- | --- | --- | --- |
| 1 | Exactly three word lists, under `wordlists/` | ✅ | T027, rerun here |
| 1 | No files at old paths | ✅ no output | T035, rerun here |
| 1 | Only `dotnet/Directory.Build.props` sets `<Version>`, from `VERSION` | ✅ | T030, rerun here |
| 1 | `VERSION` is `1.2.0` | ✅ | T020 |
| 2 | `ProfanityFilter.cs` history reaches before the restructure, to `889a667` | ✅ `7b194a3`, `25b3280`, `889a667` | T035 |
| 2 | `persian.txt` history reaches before the restructure, including `persian-default.txt` | ✅ with a caveat: default `--follow` reaches `25b3280` (1.1.0); `-M30%` reaches `889a667` and the `persian-default.txt` era. See T035. | T035 |
| 3 | 1,029 tests pass on `net10.0`, `net8.0`, `net48` | ✅ | T007, T036 |
| 3 | Pack validated against 1.2.0, producing `PersianTextGuard.1.2.0.nupkg` | ✅ | T031 |
| 3 | Bundled entries equal 1.2.0 | ✅ 0 differences | T026 |
| 3 | Package files equal 1.2.0, excluding signature metadata | ✅ 0 differences | T032 |
| 4 | Every case passes on all three targets, 0 not-applicable for .NET | ✅ 506 × 3 | T023, T036 |
| 4 | Guards: loads, ≥ 300 cases, unique ids, none pending, escaping, `[InlineData]` coverage | ✅ 497 cases | T023 |
| 4 | `net10.0` run under 30 s | ✅ 2.1 s wall | T036 |
| 4 | Every kind present | ✅ all eight | T023 |
| 5 | Two corrupted cases fail in one run with id, file, escaped input and fields; the ordinary one as a kind rule | ✅ | T025 |
| 5 | A missing corpus fails as not found, not zero cases | ✅ | T025 |
| 6 | A hand-written case passes in under 5 minutes | ✅ 6 s | T025 |
| 6 | A pending case fails with the fill-in hint | ✅ | T025 |
| 6 | The tool fills only that case and file; the corpus passes | ✅ 9 s | T025 |
| 6 | A hand-edited `start` is reported, exit `1`, case unchanged | ✅ | T025 |
| 7 | Both CI jobs green with unchanged names; logs show the corpus on `net8.0`, `net10.0`, `net48` | Verified in CI: `Build, test, pack` and `Test on .NET Framework 4.8 (netstandard2.0 build)` | T038 |
| 7 | A `v9.9.9` tag fails the publish job at the version check | Verified locally with the step's exact command (exit `1`); the tag path itself only runs in the `Publish to NuGet` job on a pushed tag | T033 |
| 8 | No stale old-path references | ✅ with a caveat: the prescribed grep matches only correct `dotnet/`-relative paths (README layout tree, solution file). See T035. | T035 |
| 8 | README "Performance" and "Development" show the new commands; `conformance/README.md` explains format, adding cases, fill-in tool | ✅ | T010, T034 |

## Pull request, CI and branch protection (T038)

- **Pull request**: [#3](https://github.com/AmirehsanK/PersianTextGuard/pull/3), `002-monorepo-conformance-corpus` → `main`, at `8e5bfcf`. Not merged.
- **CI** on `8e5bfcf`, job names unchanged:

  | Job | Conclusion |
  | --- | --- |
  | `Build, test, pack` | success |
  | `Test on .NET Framework 4.8 (netstandard2.0 build)` | success |
  | `Publish to NuGet` | skipped (not a tag) |

  The job logs show every test project on every target:

  | Project | `net8.0` | `net10.0` | `net48` |
  | --- | --- | --- | --- |
  | `PersianTextGuard.Tests` | 1,029 passed | 1,029 passed | 1,029 passed |
  | `PersianTextGuard.Conformance` | 506 passed | 506 passed | 506 passed |

- **Branch protection (FR-024)**: `GET /repos/AmirehsanK/PersianTextGuard/branches/main/protection`
  returned **HTTP 404, "Branch not protected"**. Neither `Build, test, pack` nor
  `Test on .NET Framework 4.8 (netstandard2.0 build)` is a required status check, so the
  "merge only when both jobs are green" rule is not enforced by GitHub. Reported to the maintainer;
  the change (GitHub → Settings → Branches → add a rule for `main` requiring both checks) is theirs
  to make and was not made here.
