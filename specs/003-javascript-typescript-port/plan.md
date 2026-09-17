# Implementation Plan: JavaScript/TypeScript Port Published to npm

**Branch**: `003-javascript-typescript-port` | **Date**: 2026-09-16 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/003-javascript-typescript-port/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Add a native JavaScript/TypeScript port of PersianTextGuard in `js/`, published to npm as
`persian-text-guard`, and release it together with NuGet as `1.3.0`. Per [research.md](research.md):

- **Port, don't redesign.** A file-by-file TypeScript port of the .NET matcher (about 2,200 lines of C#)
  keeps algorithms, tie-breaking and evasion reporting identical, so all 497 corpus cases can pass
  (R7).
- **Unicode, measured.** Every code point was compared between .NET 10 and Node.js. The port uses the
  engine's Unicode data behind one internal module that reproduces .NET semantics where they differ by
  design: .NET's whitespace set, per-unit lower-casing that leaves `İ` alone, and NFKC with lone
  surrogates replaced. Differences affect only characters assigned in Unicode 16 or later, which .NET
  itself treats differently across its targets; they are documented as a limitation (R1).
- **A 1.2.0 bug found and fixed.** The same measurement showed the published .NET package throws on
  Unicode noncharacters: U+FFFE on .NET 8/10, all 66 on .NET Framework 4.8. By the user's decision, this
  feature adds corpus cases and fixes .NET by normalizing around noncharacters. Every input that works
  today gives the same result (R2, R3).
- **Word-list headings: names only.** .NET 1.2.0 also accepts category numbers as section headings
  (`[3]` = insult). By the user's decision, both ports now accept only names; corpus cases pin it (R17).
- **One package, both module styles.** tsup emits ESM and CommonJS with separate `.d.mts`/`.d.cts`
  declarations, and publint and attw check them. The API uses the corpus's own lowerCamelCase names and
  ordered evasion arrays (R4, R5).
- **Word lists embedded at build time** from `wordlists/`, generated into a git-ignored module and
  parsed lazily (R6).
- **Tests.** A Vitest corpus runner follows the corpus-format contract; unit tests cover
  JavaScript-specific behaviour; README examples are executed; and four consumer checks (ESM, CJS,
  TypeScript, browser bundle in a bare `vm`) run on the packed tarball (R8, R12).
- **Release in lockstep.** The version comes from `VERSION` via a staging pack (R9). Publishing uses npm
  trusted publishing from `ci.yml` in environment `npm`, with provenance. Both publish jobs require every
  port's jobs to be green (R10, R13). The gating is proven by a real CI dry run on throwaway tags before
  `v1.3.0` (R18).
- **Kept compatible and documented.**
  - API Extractor keeps an API report, and a script checks it against the previous release's report
    (R14).
  - tinybench measures the JavaScript port, and the .NET suite is re-run before and after the .NET change
    with its README table refreshed (R11).
  - The npm README is in English and Persian (R16).
  - The corpus is also run locally on Node.js 22 and 24 (R19).

## Technical Context

**Language/Version**: TypeScript (strict), compiled to ES2020. Runtime: Node.js 22 and 24 (active and
maintenance LTS on 2026-09-16) and current browsers. C# for the .NET fix (unchanged toolchain).

**Primary Dependencies**:
- **Runtime**: none (Principle III).
- **Development only**: TypeScript, tsup, Vitest, ESLint (with a TSDoc/JSDoc presence rule), API
  Extractor, publint, `@arethetypeswrong/cli`, tinybench and esbuild (browser consumer check). Exact
  versions are pinned in `js/package-lock.json` at implementation time.

**Storage**: Files in the repository: `wordlists/*.txt` (read at build time), `conformance/**/*.json`
(read by tests), `VERSION` (read at pack time).

**Testing**:
- Vitest unit tests and the corpus runner, on Node.js 22 and 24;
- README-example tests;
- consumer checks on the packed tarball;
- the existing .NET tests (1,029) and .NET corpus runner (three targets), which gain the noncharacter
  cases.

**Target Platform**:
- **npm package**: Node.js 22+ and browsers via bundlers, ESM and CommonJS.
- **.NET package**: unchanged targets (`netstandard2.0`, `net8.0`, `net10.0`).

**Project Type**: Library monorepo. This feature adds the second port.

**Performance Goals**: On the README's machine with Node.js 24, a filter from the bundled default list
builds in under 50 ms, and a short clean message checks in under 50 µs mean (SC-005). The .NET
per-message path is unchanged except that `Normalize` now scans for noncharacters. That is an O(n) pass
over text it already scans; .NET benchmarks run before and after (Principle IV).

**Constraints**:
- 0 runtime dependencies, and an unpacked size under 1 MB (SC-004).
- No Node.js-only APIs in the library (FR-003).
- The corpus is authoritative: 100% pass, 0 not applicable (FR-017).
- .NET behaviour unchanged except the noncharacter fix and the names-only heading rule (FR-026, FR-028, FR-029).
- Existing CI job names unchanged (FR-021).
- A release is irreversible; tag only with the maintainer's go-ahead.

**Scale/Scope**:
- **JavaScript source**: about 2,500 lines of TypeScript, plus about 800 lines of tests and corpus
  runner, 4 consumer projects and 1 benchmark file.
- **Corpus**: about 20 new cases.
- **.NET**: a fix of about 40 lines in `PersianNormalizer.cs`, about 5 lines in `WordList.cs`, and about 10 lines in the corpus writer and guard.
- **Docs**: CI adds 3 jobs; 2 READMEs change.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution v2.0.0](../../.specify/memory/constitution.md).

| Principle / section | Gate | Before research | After design |
| --- | --- | --- | --- |
| **I. Ordinary Messages Must Pass** | Ordinary corpus cases pass in every port | ✅ FR-017 | ✅ The JavaScript runner enforces the `ordinary` kind rules (R8). The .NET fix leaves every ordinary result unchanged (R2). |
| **II. User Input Never Throws** | Every text function returns for any text the language can represent | ⚠️ Non-string → `TypeError` (user's choice) | ⚠️ **Justified** (Complexity Tracking). `null`, `undefined` and every string, including lone surrogates, noncharacters and 132,000-character input, never throw (G1). A non-string is a programmer error at the call site, like an invalid mask. **The design also found and fixes a real violation in 1.2.0** (noncharacters), with corpus cases as the principle requires (R2). |
| **III. Native and Dependency-Free** | Native TypeScript; no runtime dependencies; ESM + CJS + bundled types; Node LTS; runs in browsers | ✅ | ✅ Pure TypeScript, 0 dependencies; tsup dual output with `.d.mts`/`.d.cts`; CI on Node.js 22 and 24; a browser bundle is executed in a bare `vm` (R4, R12). Dev tools only, as allowed. |
| **IV. Build Once, Match Fast, Share Safely** | Immutable, shareable filter; construction-time work; token lookup; benchmarks with tinybench | ✅ | ✅ Frozen snapshots and results (G7); a `Map`/`Set` token lookup ported from .NET (R7); tinybench suite and README table (R11). The .NET `Normalize` change is benchmarked before and after with BenchmarkDotNet on the machine the README names (confirmed the same i7-9700K), the .NET README table is refreshed, and both tables go in the PR (R11). |
| **V. One Behaviour, Verified in Every Language** | Passes every corpus case; UTF-16 positions in JS; same capabilities | ✅ | ✅ Corpus runner per the contract (R8). Every capability is present. `WordList.Load(Stream)` becomes read-then-`WordList.parse`, which is the same capability with no cross-platform stream type (R5; recorded below). Noncharacter cases are added to the corpus and both ports pass them. |
| **VI. Documented in Persian and English, Pinned by Tests** | Package README bilingual; every README example tested; API docs on every export | ✅ | ✅ `js/README.md` in English and Persian (RTL); README blocks executed by `readme.test.ts`; lint enforces docs on exports; release notes with a Persian summary (R16, R15). The root README stays English-only under the adoption clause, as in 002. |
| **VII. Curated, Categorised, Credited Word Lists** | Lists live once in `wordlists/`, embedded at build | ✅ | ✅ Generated at build into a git-ignored file; no copy in `js/` (R6). `THIRD-PARTY-NOTICES.md` ships in the package. |
| **Public API & Versioning** | Lockstep from one version source; tag = version; API check vs previous release; release blocked if any port fails; MINOR for a new language | ✅ | ✅ `VERSION` → staging `package.json` (R9); both publish jobs need all four build/test jobs and check the tag (R10, R13); API Extractor report plus an automated compatibility check against the previous release tag's report (R14); gating proven by a real CI dry run (R18); `1.3.0` MINOR, with both .NET behaviour changes (noncharacters, numeric headings) called out in bilingual notes (R15, R17). ⚠️ Two registries cannot publish atomically; mitigated with idempotent re-runs (Complexity Tracking). |
| **Development Workflow & Quality Gates** | Branch + PR; CI green for every port; behaviour PRs update the corpus, every port and the README | ✅ | ✅ Branch `003-javascript-typescript-port` created. The noncharacter behaviour change updates the corpus, both ports and the READMEs in the same PR. New JavaScript jobs become required checks, which is a maintainer step after merge (contract, Release step 4). |
| **Governance: Adoption** | No PR moves further from unmet requirements | ✅ | ✅ Adds the `js/` port and npm to lockstep releases; moves nothing further away. |

**Gate result**: **PASS.** Two ⚠️ items are justified below.

## Project Structure

### Documentation (this feature)

```text
specs/003-javascript-typescript-port/
├── plan.md                          # This file
├── research.md                      # Phase 0: R1–R16
├── data-model.md                    # Phase 1: entries, options, filter, match, normalization, word lists, errors, package
├── quickstart.md                    # Phase 1: validation guide, including the release
├── contracts/
│   ├── public-api.md                # Phase 1: the TypeScript declarations, guarantees, .NET name table
│   └── package-and-release.md       # Phase 1: layout, commands, package contents, CI, release order
├── checklists/
│   └── requirements.md              # Spec quality checklist
└── tasks.md                         # Phase 2 (/speckit-tasks — not created by /speckit-plan)
```

### Source Code (repository root)

```text
VERSION                                         # 1.2.0 → 1.3.0

conformance/
├── README.md                                   # writing rule 1: noncharacters escaped
└── cases/
    ├── robustness.json                         # + noncharacter messages (alone, between words, inside a word)
    ├── normalization.json                      # + noncharacter normalization
    └── word-list-parsing.json                  # + numeric and spaced headings (R17)

dotnet/
├── src/PersianTextGuard/PersianNormalizer.cs   # NFKC between noncharacters (both paths)
├── src/PersianTextGuard/WordList.cs            # section headings: category names only (R17)
├── tests/PersianTextGuard.Conformance/
│   ├── Corpus.cs                               # IsInvisible includes noncharacters
│   └── CorpusGuardTests.cs                     # writer test covers a noncharacter
└── tools/PersianTextGuard.CorpusFill/CaseWriter.cs  # escapes noncharacters

js/                                             # NEW: the npm package persian-text-guard
├── package.json  package-lock.json  README.md
├── tsconfig.json  tsup.config.ts  vitest.config.ts  eslint.config.js  api-extractor.json
├── etc/persian-text-guard.api.md               # API report baseline
├── scripts/generate-wordlists.mjs  pack.mjs  check-package.mjs  check-consumers.mjs  check-api-compat.mjs
├── src/
│   ├── index.ts                                # public exports
│   ├── types.ts                                # BannedWord, options, match, unions, errors
│   ├── unicode.ts                              # .NET-equivalent whitespace, categories, lower-casing, NFKC/NFD
│   ├── normalizer.ts                           # normalize, tokenize, digits (← PersianNormalizer.cs)
│   ├── source-map.ts                           # ← SourceMap.cs
│   ├── filter.ts  scan.ts  regions.ts          # ← ProfanityFilter*.cs
│   ├── word-list.ts                            # ← WordList.cs
│   ├── fold.ts                                 # ← Fold/Squeeze and character classes from ProfanityFilter.Scan.cs
│   └── generated/wordlists.ts                  # build output, git-ignored
├── test/
│   ├── corpus/  corpus.test.ts                 # corpus runner
│   ├── api.test.ts  unicode.test.ts  readme.test.ts
├── bench/filter.bench.ts
└── consumers/esm/  cjs/  typescript/  browser/

README.md                                       # npm package listed; Development gains js/; Releasing section
.gitignore                                      # js build outputs
.github/workflows/ci.yml                        # JavaScript (Node 22/24), Publish to npm; publish needs updated
```

**Structure Decision**: The constitution's monorepo layout, with the JavaScript port in its own
top-level `js/` directory next to `dotnet/`. It reads the shared `wordlists/`, `conformance/` and
`VERSION` from the root, and has its own tests, benchmarks and README, as the Development Workflow
section requires. Inside `js/`, source files map one-to-one to the .NET files they port, so behaviour
changes can be made in both ports side by side. The .NET changes are confined to the noncharacter fix
and the corpus tooling's escaping rule.

## Complexity Tracking

| Decision | Why needed | Simpler alternative rejected because |
| --- | --- | --- |
| Non-string message → `TypeError` (Principle II allows failures only for programmer errors) | The user chose it (spec Clarifications). A number or object passed as a message is a caller bug, and failing loudly avoids an unchecked value passing as clean | *Treat as missing*: `{}` would be reported clean while the caller stores it. *Coerce with `String()`*: `"[object Object]"` results surprise callers. Neither was chosen. |
| Fixing a .NET bug inside a JavaScript-port feature | Both ports must pass one corpus. A JavaScript port that never throws cannot match a .NET package that does, and Principle II requires crashes to become corpus cases. The user chose to fix it here | *A separate 1.2.1 release first* delays the port and adds a release. *Leaving it out of the corpus* knowingly ships two ports that differ. |
| Two registries published by two jobs, not atomically | GitHub Actions cannot publish to npm and NuGet in one transaction | *A single job publishing both* is still not atomic and mixes two OIDC environments. Mitigation: both jobs gate on every port's jobs, and re-runs are idempotent (R10). |
| `WordList.Load(Stream)` has no JavaScript equivalent | Node.js and browsers share no stream type; the capability is building a filter from list text | *Accepting a Node.js `Readable`* would break browser use (FR-003). *A web `ReadableStream` overload* adds async API for a one-line `await file.text()`. |
| Engine Unicode data instead of pinned tables | Keeps the package small and data-free; the differences affect only Unicode 16+ characters, which .NET itself handles differently across targets | *Generated tables pinned to one Unicode version* add about 50–100 KB and yearly regeneration, and still cannot make .NET Framework 4.8 agree. |
| Staging directory for `npm pack` | Keeps `VERSION` the only version source while npm requires `package.json` to carry one | *Committing the version and checking it in CI* makes two places to edit per release. |
