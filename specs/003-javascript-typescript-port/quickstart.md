# Quickstart: Validating the JavaScript/TypeScript Port and the 1.3.0 Release

**Feature**: [spec.md](spec.md) | **Contracts**: [public API](contracts/public-api.md), [package and release](contracts/package-and-release.md)

How to prove the feature is done. Each section names the requirements it checks. Commands run from
the repository root unless a step says `cd js`.

## Prerequisites

- Node.js 22 and 24, with npm 11.5.1 or later for publishing.
- .NET SDK 10, the .NET 8 runtime, and Windows for `net48`.
- The branch `003-javascript-typescript-port` checked out; network access to npm and NuGet.

## 1. The two .NET fixes, and nothing else changed (FR-026, FR-028, FR-029; research R2, R3, R17)

```bash
dotnet test dotnet/tests/PersianTextGuard.Tests
dotnet test dotnet/tests/PersianTextGuard.Conformance
dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill -- --check
```

**Expected**:
- 1,029 existing tests pass on `net8.0`, `net10.0` and `net48`.
- The corpus passes on all three targets, including the new noncharacter and word-list heading cases.
- The heading rule: in the corpus, `"[3]\nword\n"`, `"[+4]\nword\n"` and `"[ 03 ]\nword\n"` are
  unknown-category errors on line 1, and `"[ Insult ]"` and `"[SLUR]"` are accepted.
- **.NET benchmarks**: the BenchmarkDotNet suite ran before and after on the i7-9700K the README names,
  and the README's .NET table shows the "after" numbers.
- `--check` reports 0 disagreements.
- `git grep -nP '[\x{FDD0}-\x{FDEF}\x{FFFE}\x{FFFF}]' -- conformance` prints nothing, because every
  noncharacter is escaped.

**Regression proof**: check out `dotnet/src` from `main` (1.2.0 behaviour) and rerun the corpus. The
noncharacter cases fail on `net48` (all of them) and on `net8.0`/`net10.0` (the U+FFFE cases), and the numeric
heading cases fail on every target, because 1.2.0 accepts `[3]`. Then
restore the branch.

## 2. Build and unit tests on both supported Node.js releases (FR-002–FR-015, FR-019)

```bash
cd js
npm ci
npm run lint
npm run build
npm test
```

**Expected**: lint and type check are clean, and every unit test passes, on Node.js 22 and on Node.js 24.
The tests include:
- `TypeError` for `42`, `{}` and `['kir']` passed as a message;
- `undefined` handled like `null`;
- `RangeError` for masks `'x'`, `'5'`, `' '`, `'\n'`, `'\uD83D'` and `'##'`;
- `WordListFormatError` with `line === 1` for `"[nonsense]\nword\n"`;
- frozen results, and a filter unaffected by mutating its inputs;
- `İ`, U+0085/U+FEFF and final-sigma handling (research R1).

## 3. The corpus passes (FR-016, FR-017, SC-001)

```bash
cd js
npm run corpus
```

Then, locally, on both supported releases (research R19):

```bash
fnm exec --using=22 npm run corpus
fnm exec --using=24 npm run corpus
```

**Expected**: every case passes on Node.js 22 and 24 (and on this machine's default Node.js), with 0 not
applicable. The count equals the .NET runner's case count: 497 plus the noncharacter and heading cases.

**Failure reporting**, on a scratch edit that is reverted afterwards:
1. Change one must-match case's `expected.censored` in `conformance/cases/matching-persian.json`, and set
   one ordinary case's `containsProfanity` to `true`.
2. Run `npm run corpus`.

**Expected**: both fail in one run, each with id, file, escaped input and `path: expected … actual …`;
the ordinary case is reported as breaking its kind rule. Renaming `conformance/` makes the run fail with
"not found".

## 4. Bundled lists equal .NET's (FR-018, SC-002)

```bash
cd js
npm run build
node --input-type=module -e "import { WordList, WORD_CATEGORIES } from './dist/index.mjs'; for (const [n, l] of [['all', WordList.all], ['default', WordList.persianDefault], ...WORD_CATEGORIES.map(c => [c, WordList.bundled(c)])]) { console.log('##', n, l.length); for (const w of l) console.log([w.text, w.mode, w.category].join('\t')); }" > ../artifacts/compare/js.txt
```

Compare `artifacts/compare/js.txt` with the same dump from .NET (feature 002's
`artifacts/compare/current` project), after mapping .NET's PascalCase mode and category names to
lowerCamelCase.

**Expected**: 0 differences. `all` has 1,250 entries and `default` 1,025.

## 5. The package as users install it (FR-001, FR-004, FR-006, FR-020–FR-022, SC-004, SC-007)

```bash
cd js
npm run pack
npm run check:package
npm run api
npm run api:compat
npm run check:consumers
```

**Expected**:
- **Tarball**: `artifacts/persian-text-guard-<VERSION>.tgz` holds exactly the eight files listed in the
  package contract. Its unpacked size is under 1 MB, and it has no `dependencies`.
- **publint**: no errors.
- **attw**: every resolution mode (`node16`-CJS, `node16`-ESM, `bundler`) is green.
- **API Extractor**: the report matches `etc/persian-text-guard.api.md`.
- **API compatibility**: `api:compat` prints "No previous npm release report (first release); baseline
  only". On a scratch branch where the report's `censor` line is changed and a tag `v1.3.0-compat-test`
  is created locally on the parent commit (then deleted), it fails and names the changed declaration.
- **Consumer checks**: all four pass (ESM, CJS, TypeScript with `@ts-expect-error` lines, browser bundle
  in a bare `vm`). The normalize-only bundle does not contain the word-list texts.
- **Version**: temporarily set `VERSION` to `9.9.9` and run `npm run pack`; the tarball is named
  `persian-text-guard-9.9.9.tgz`. Revert.

## 6. Documentation and performance (FR-023–FR-025, SC-005, SC-006)

```bash
cd js
npm run test:all
npm run bench
```

**Expected**:
- **README examples**: every `ts` example in `js/README.md` passes.
- **Lint**: reports no undocumented export.
- **Benchmarks**: on Node.js 24 on the README's machine, building from `WordList.persianDefault` takes
  under 50 ms, a short clean message takes under 50 µs on average, and the 132,000-character message
  under 100 ms. The README table shows these numbers.
- **Onboarding (SC-003)**: following only `js/README.md`'s quick start, a fresh JavaScript project and a
  fresh TypeScript project each install the packed tarball and flag `"ک.ی.ر"`, each in under 5
  minutes. The elapsed times are recorded.
- **`js/README.md`**: has English and Persian (right to left) installation and quick start, the name
  table, and Limitations.
- **Root `README.md`**: lists the npm package, shows `js/` in Development, and has a Releasing section.

## 7. CI, release and registries (FR-020, FR-027, SC-008, SC-010)

**On the pull request**:
- Every job is green, and the logs show the JavaScript corpus on Node.js 22 and 24.
- Both .NET jobs still show 1,029 tests and the corpus on three targets.

**Release gates, real dry run** (SC-008, research R18), before merging. On the scratch branch
`dryrun/release-gates`, whose publish steps cannot publish:

| Tag | Scratch change | Expected |
| --- | --- | --- |
| `v0.0.0-dryrun.1` | JavaScript job has a failing step; `VERSION` = `0.0.0-dryrun.1` | JavaScript jobs fail; `Publish to NuGet` and `Publish to npm` are **skipped** |
| `v0.0.0-dryrun.2` | failing step removed; `VERSION` = `0.0.0-dryrun.2` | all jobs succeed; the publish jobs' dry-run steps list `PersianTextGuard.0.0.0-dryrun.2.nupkg` and `persian-text-guard@0.0.0-dryrun.2` with eight files |
| `v0.0.0-dryrun.3` | none (same commit as `.2`) | both publish jobs **fail** at "Check tag matches VERSION" |

**Afterwards**: the three tags and the branch are deleted. `npm view persian-text-guard versions` lists
only `0.0.1`, and NuGet has no `0.0.0-dryrun` version.

**After the maintainer's go-ahead and tag `v1.3.0`**:

```bash
npm view persian-text-guard@1.3.0 version dist.attestations
mkdir -p /tmp/ptg-npm && cd /tmp/ptg-npm && npm init -y && npm install persian-text-guard@1.3.0
node -e "const { ProfanityFilter, WordList } = require('persian-text-guard'); console.log(new ProfanityFilter(WordList.persianDefault).containsProfanity('ک.ی.ر'))"
```

**Expected**:
- **npm**: `1.3.0` has a provenance attestation, and the script prints `true`.
- **NuGet**: `dotnet add package PersianTextGuard --version 1.3.0` in a fresh console app succeeds, and
  the same check prints `True`.
- **GitHub release**: `v1.3.0` exists with English notes and a Persian summary.
- **Placeholder**: after the maintainer deprecates `0.0.1`, `npm view persian-text-guard@0.0.1 deprecated`
  shows the message.
