# Contract: Repository Layout, Commands, CI and Release for the JavaScript Port

**Feature**: [../spec.md](../spec.md) | **Research**: [../research.md](../research.md) R4, R6, R8–R19 | **Extends**: [002 repository-and-tooling contract](../../002-monorepo-conformance-corpus/contracts/repository-and-tooling.md)

## Layout

New or changed paths only. Everything else is as in the 002 contract.

```text
/
├── VERSION                                 # 1.3.0 after this feature
├── conformance/
│   ├── README.md                           # writing rule 1 gains noncharacters; headings are names
│   └── cases/robustness.json, normalization.json, word-list-parsing.json   # + noncharacter and heading cases
├── dotnet/
│   ├── src/PersianTextGuard/PersianNormalizer.cs   # noncharacter fix (research R2)
│   ├── src/PersianTextGuard/WordList.cs            # headings are category names only (research R17)
│   ├── tests/PersianTextGuard.Conformance/Corpus.cs, CorpusGuardTests.cs  # noncharacters in the escaping rule
│   └── tools/PersianTextGuard.CorpusFill/CaseWriter.cs                    # escapes noncharacters
├── js/
│   ├── package.json                        # name persian-text-guard, version 0.0.0-development
│   ├── package-lock.json
│   ├── README.md                           # shown on npm; English + Persian
│   ├── tsconfig.json
│   ├── tsup.config.ts
│   ├── api-extractor.json
│   ├── eslint.config.js
│   ├── vitest.config.ts
│   ├── etc/persian-text-guard.api.md       # API Extractor report (committed baseline)
│   ├── scripts/
│   │   ├── generate-wordlists.mjs          # wordlists/*.txt → src/generated/wordlists.ts (git-ignored)
│   │   └── pack.mjs                        # staging dir, version from VERSION, npm pack
│   ├── src/
│   │   ├── index.ts                        # public exports only
│   │   ├── types.ts
│   │   ├── unicode.ts
│   │   ├── normalizer.ts
│   │   ├── source-map.ts
│   │   ├── filter.ts  scan.ts  regions.ts
│   │   ├── word-list.ts
│   │   ├── fold.ts                         # Fold, Squeeze and character classes (← ProfanityFilter.Scan.cs)
│   │   └── generated/                      # git-ignored
│   ├── test/
│   │   ├── corpus/                         # corpus runner: load, build inputs, evaluate, compare
│   │   ├── corpus.test.ts
│   │   ├── api.test.ts                     # JavaScript-specific behaviour (FR-019)
│   │   ├── internals.test.ts               # source maps and normalizer internals (ported SourceMapTests)
│   │   ├── unicode.test.ts                 # research R1 cases
│   │   └── readme.test.ts                  # every ts block in js/README.md
│   ├── scripts/check-package.mjs  check-consumers.mjs  check-api-compat.mjs   # publint + attw; consumers; API vs previous release
│   ├── bench/filter.bench.ts
│   └── consumers/esm/ cjs/ typescript/ browser/
├── README.md                               # npm package listed; Development + Releasing sections
└── .github/workflows/ci.yml                # JavaScript jobs, Publish to npm
```

**Git-ignored**: `js/node_modules/`, `js/dist/`, `js/src/generated/`, `js/.pack/`, `js/artifacts/`, and
`js/temp/` (API Extractor).

## Commands

Every command runs from `js/` unless it starts with `dotnet`.

| Purpose | Command | Result |
| --- | --- | --- |
| Install dev tools | `npm ci` | Exact versions from `package-lock.json` |
| Build | `npm run build` | Generates word lists, then `dist/index.{mjs,cjs,d.mts,d.cts}` |
| Lint and type check | `npm run lint` | ESLint (including TSDoc presence on exports), then `tsc --noEmit` for `tsconfig.json` (src, no Node.js types) and `tsconfig.node.json` (tests, benchmarks, scripts) |
| Unit tests | `npm test` | Every test except the corpus |
| Corpus | `npm run corpus` | Every case in `conformance/`; exits non-zero on any failing case |
| All tests | `npm run test:all` | Unit tests, corpus and README examples |
| API report | `npm run api` | API Extractor; fails if the report differs (CI); `npm run api -- --local` updates it |
| API compatibility | `npm run api:compat` | Fails if a declaration from the previous release's report is removed or changed without a MAJOR `VERSION` (research R14) |
| Pack | `npm run pack` | `artifacts/persian-text-guard-<VERSION>.tgz` |
| Package checks | `npm run check:package` | publint and attw on the tarball |
| Consumer checks | `npm run check:consumers` | Installs the tarball into `consumers/*` and runs each |
| Benchmarks | `npm run bench` | tinybench table on the current Node.js |
| .NET corpus (unchanged) | `dotnet test dotnet/tests/PersianTextGuard.Conformance` | Also passes the noncharacter cases |

## Package contents

`npm pack --dry-run` on the staging directory lists exactly:

```text
package.json
README.md
LICENSE
THIRD-PARTY-NOTICES.md
dist/index.mjs
dist/index.cjs
dist/index.d.mts
dist/index.d.cts
```

Source maps for `dist` are not published. `package.json`:
- `"name": "persian-text-guard"`, `"version": "<VERSION>"`, `"license": "MIT"`;
- `"type": "module"`, `"sideEffects": false`, `"engines": { "node": ">=22" }`;
- `repository`, `homepage` and `bugs` pointing at `AmirehsanK/PersianTextGuard`, `"directory": "js"`;
- `keywords` matching the NuGet tags;
- no `dependencies`;
- `exports`:

```json
{
  ".": {
    "import": { "types": "./dist/index.d.mts", "default": "./dist/index.mjs" },
    "require": { "types": "./dist/index.d.cts", "default": "./dist/index.cjs" }
  },
  "./package.json": "./package.json"
}
```

plus `main`, `module` and `types` for tools that ignore `exports`.

## CI

`.github/workflows/ci.yml`. The job names in the first three rows below already exist and MUST NOT
change.

| Job | Runs on | Steps | Required on `main` |
| --- | --- | --- | --- |
| `Build, test, pack` | ubuntu | as today (.NET build, tests, corpus, pack) | yes (existing) |
| `Test on .NET Framework 4.8 (netstandard2.0 build)` | windows | as today | yes (existing) |
| `Publish to NuGet` | ubuntu | on `v*` tags: tag check, push. `needs` gains both JavaScript jobs | — |
| `JavaScript (Node 22)` | ubuntu | `npm ci`, lint, build, unit tests, corpus, README examples | yes (added after merge, delegated) |
| `JavaScript (Node 24)` | ubuntu | as Node 22, plus: pack, `check:package`, `api`, `api:compat` (full history), `check:consumers`, upload artifact `npm-package` | yes (added after merge, delegated) |
| `Publish to npm` | ubuntu | on `v*` tags; `needs` all four build/test jobs; `environment: npm`; `id-token: write`; Node.js 24 with npm ≥ 11.5.1; tag check; skip if the version already exists; `npm publish <tgz> --access public` | — |

**Guarantees**:
- On a `v*` tag, neither publish job starts unless all four build/test jobs succeed.
- Both publish jobs refuse a tag that differs from `v` + `VERSION`.
- Re-running a publish job after a partial release is safe: NuGet uses `--skip-duplicate`, and npm skips a
  version that already exists.
- The npm package carries a provenance attestation for this repository and workflow (automatic with
  trusted publishing).

## Release 1.3.0

Order of operations. Steps marked 👤 are account or registry actions only the maintainer can perform.

1. ✅ 👤 Claimed `persian-text-guard` (0.0.1 placeholder) and configured the npm trusted publisher:
   `AmirehsanK/PersianTextGuard`, `ci.yml`, environment `npm`, direct publish allowed (2026-09-16).
2. ✅ 👤 Created the GitHub environment `npm` with deployment rule tag `v*`, no reviewers and no secrets
   (2026-09-17).
3. Before merge, the release gates are proven by a real CI dry run on `v0.0.0-dryrun.*` tags from a scratch
   branch whose publish steps cannot publish (research R18); the tags and branch are deleted afterwards.
4. The feature pull request merges, with the maintainer's go-ahead, with `VERSION` = `1.3.0` and all CI
   green, including the noncharacter corpus cases on .NET.
5. Add `JavaScript (Node 22)` and `JavaScript (Node 24)` to `main`'s required status checks. The
   maintainer delegated this on 2026-09-17 (tasks T073).
6. Push tag `v1.3.0` on the merge commit, after checking the preconditions in T074. Delegated in
   advance; **irreversible**.
7. CI publishes `PersianTextGuard 1.3.0` to NuGet and `persian-text-guard 1.3.0` to npm.
8. Verify from the public registries in fresh projects (SC-010, quickstart §7).
9. Create the GitHub release `v1.3.0` with English notes and a Persian summary (research R15).
10. `npm deprecate persian-text-guard@0.0.1 "Placeholder; use 1.3.0 or later" --auth-type=web`.
   Delegated; the owner approves the 2FA prompt in the browser (T077).
