# Implementation Plan: Monorepo with Shared Word Lists and a Conformance Corpus

**Branch**: `002-monorepo-conformance-corpus` | **Date**: 2026-09-16 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/002-monorepo-conformance-corpus/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Turn the repository into the monorepo the constitution (v2.0.0) describes, with no change for .NET
users. The work, per [research.md](research.md):

- **Move.** The .NET source, tests and benchmarks go under `dotnet/`, and the bundled word lists to
  `wordlists/`. Moves go in one commit with no content changes, so history follows every file (R11).
- **One version.** A root `VERSION` file becomes the only version source; the .NET build reads it, and
  release tags are checked against it (R13).
- **Write down today's behaviour** as a language-neutral JSON corpus in `conformance/`:
  - about 300+ cases of eight kinds, recorded from 1.2.0 (R1–R6, R9, R10);
  - invisible characters written as visible escapes;
  - text JSON cannot carry, such as lone surrogates, described structurally rather than as literal
    escapes. This was checked on this machine: System.Text.Json throws on a lone `\uD83D`, and
    Node.js keeps it.
- **Check it on .NET.** A new xUnit project runs every case exactly on `net10.0`, `net8.0` and `net48`
  (R12).
- **Fill in cases.** A fill-in tool computes expectations for new cases from .NET, never overwrites
  existing ones, and reports disagreements (R7, R8).
- **Prove nothing changed.** Package validation moves to the 1.2.0 baseline, plus a one-time
  entry-by-entry and package-content comparison against the published 1.2.0 (R14).
- **CI** runs the corpus on every target (R15).

Planning found one conflict in the spec. FR-001 required a format every language reads "with its
standard library", and no structured format meets that, because Java and Rust have no JSON in their
standard library. FR-001 is amended to "readable in its tests", which the constitution's test-only
dependency rule covers (R1).

## Technical Context

**Language/Version**: C# (`LangVersion` latest), .NET SDK 10. Corpus data in JSON; MSBuild; GitHub
Actions YAML.

**Primary Dependencies**:
- **Library**: none added; the package still has no runtime dependencies.
- **Test and tool only**:
  - `System.Text.Json`, in-box on `net8.0`/`net10.0`, a package reference on `net48` for the runner;
  - xUnit 2.9, as in the existing tests.

**Storage**: Files in the repository: `wordlists/*.txt`, `conformance/**/*.json`, `VERSION`.

**Testing**:
- The existing xUnit project, unchanged (1,029 tests).
- The new conformance xUnit project, on `net10.0`, `net8.0` and `net48`.
- Package validation against 1.2.0.
- A one-time comparison of entries and package contents against the published 1.2.0.

**Target Platform**: Unchanged for the package: `netstandard2.0`, `net8.0`, `net10.0`. The fill-in tool
targets `net10.0` only; it is repository tooling, not shipped.

**Project Type**: Library monorepo. After this feature it contains one port, .NET, plus the shared data
every later port will read.

**Performance Goals**: The corpus run on `net10.0` in under 30 seconds (SC-008). No change to the
package's per-message path, so no benchmark changes are required (Principle IV applies only to
per-message changes).

**Constraints**:
- Zero changes to the package's public API, bundled entries, behaviour or embedded resource names
  (FR-016, FR-022).
- File history preserved (FR-019).
- The existing test files are not modified (Clarification 3).
- Every recorded corpus field is compared exactly (Clarification 1).
- The fill-in tool never overwrites an expectation (Clarification 2).

**Scale/Scope**: About 300–400 corpus cases in 10 files and 6+ named configurations. Moves: 27
tracked files — everything under `src/`, `tests/` and `benchmarks/`, including the 3 word lists, plus
the solution and build props. New code: the conformance test project, about 600 lines, and the fill-in tool, about 700 lines.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution v2.0.0](../../.specify/memory/constitution.md).

| Principle / section | Gate | Before research | After design |
| --- | --- | --- | --- |
| **I. Ordinary Messages Must Pass** | Ordinary messages protected in the corpus | ✅ FR-006 marks ordinary cases. | ✅ The `ordinary` kind has enforced rules: no match and censored output equal to the input. Every ordinary message from the existing tests is seeded (R9). |
| **II. User Input Never Throws** | Robustness inputs covered for every port | ✅ FR-008 covers missing, invalid and very long input. | ✅ The `robustness` kind plus `build` inputs with `utf16` and `repeat` parts; the runner reports not-applicable cases by id (R4). |
| **III. Native and Dependency-Free** | No runtime dependency added; test-only dependencies allowed | ✅ | ✅ The package gains nothing. `System.Text.Json` is test-only on `net48`, and the fill-in tool is not shipped. The FR-001 amendment relies on this rule for future Java and Rust runners (R1). |
| **IV. Build Once, Match Fast, Share Safely** | No per-message change without benchmarks | ✅ No matcher change. | ✅ `dotnet/src` code is moved, not edited, apart from the embedded-resource path in the project file. |
| **V. One Behaviour, Verified in Every Language** | A language-neutral corpus the .NET port passes; code-point positions; every capability covered | ✅ The purpose of this feature. | ✅ Eight case kinds cover every capability Principle V lists: checking, first match, every match, censoring with masks, normalizing, tokenizing, category selection, plus word-list reading. Positions are in code points with a defined lone-surrogate rule (R5). Exact comparison (R6). |
| **VI. Documented in Persian and English** | Documentation updated; bilingual rule | ⚠️ The README is English-only. | ⚠️ **Allowed by the Adoption clause.** The bilingual requirement binds only once the bilingual README feature lands. This feature updates README paths and commands in English, adds `conformance/README.md` in English, and moves the project no further from a bilingual README. Recorded in Complexity Tracking. |
| **VII. Curated, Categorised, Credited Word Lists** | Lists live once in `wordlists/` and are embedded at build time | ✅ FR-014, FR-015. | ✅ The `EmbeddedResource` source path changes; resource names are unchanged (R14). No copies remain. `THIRD-PARTY-NOTICES.md` paths are updated. |
| **Public API & Versioning** | Single version source; API check against the previous release; tag equals version | ✅ FR-018. | ✅ `VERSION` is read by `Directory.Build.props`; the CI tag check replaces the `-p:Version` override; the baseline moves to 1.2.0 (R13, R14). |
| **Development Workflow & Quality Gates** | CI green for every port, including the corpus; behaviour PRs update the corpus | ✅ | ✅ Both CI jobs run the corpus on all three targets (R15). Every future behaviour change fails the corpus until it is updated (R7). |
| **Governance: Adoption** | No PR moves further from unmet requirements | ✅ | ✅ This feature satisfies LAYOUT, CORPUS and the version part of CI, and leaves PORTS and README_FA unchanged. |

**Gate result**: **PASS.** The one ⚠️ is covered by the constitution's adoption clause and is recorded
below, not a violation.

## Project Structure

### Documentation (this feature)

```text
specs/002-monorepo-conformance-corpus/
├── plan.md                         # This file
├── research.md                     # Phase 0: R1–R15
├── data-model.md                   # Phase 1: corpus, configuration, input, case kinds, word lists, version
├── quickstart.md                   # Phase 1: validation guide
├── contracts/
│   ├── corpus-format.md            # Phase 1: the corpus format every port's runner implements
│   └── repository-and-tooling.md   # Phase 1: layout, version source, commands, fill-in tool, CI
├── checklists/
│   └── requirements.md             # Spec quality checklist
└── tasks.md                        # Phase 2 (/speckit-tasks — not created by /speckit-plan)
```

### Source Code (repository root)

```text
VERSION                                   # NEW: "1.2.0"
.gitattributes                            # NEW: LF for wordlists/*.txt and conformance/**/*.json

wordlists/                                # MOVED from src/PersianTextGuard/WordLists/
├── persian.txt
├── finglish.txt
└── english.txt

conformance/                              # NEW
├── README.md
├── corpus.json
├── configurations.json
└── cases/
    ├── matching-persian.json
    ├── matching-finglish.json
    ├── matching-english.json
    ├── matching-options.json
    ├── robustness.json
    ├── normalization.json
    ├── tokenization.json
    ├── word-list-parsing.json
    ├── category-selection.json
    └── mask-validation.json

dotnet/
├── PersianTextGuard.slnx                 # MOVED from the root; project paths updated; 2 projects added
├── Directory.Build.props                 # MOVED from the root; reads ../VERSION
├── src/PersianTextGuard/                 # MOVED from src/PersianTextGuard/
│   └── PersianTextGuard.csproj           # <Version> removed; EmbeddedResource → ../../../wordlists/*.txt;
│                                         # README/icon/notices → ../../../; baseline → 1.2.0
├── tests/PersianTextGuard.Tests/         # MOVED from tests/; content unchanged
├── tests/PersianTextGuard.Conformance/   # NEW: corpus runner (R12)
│   ├── PersianTextGuard.Conformance.csproj
│   ├── Corpus.cs                         # loading, input building, code-point conversion
│   ├── CorpusGuardTests.cs               # presence, count, unique ids, pending, escaping, InlineData coverage
│   ├── MatchingCaseTests.cs              # ordinary / must-match / robustness
│   ├── TextCaseTests.cs                  # normalization, tokenization
│   └── ListCaseTests.cs                  # word-list parsing, category selection, mask validation
├── benchmarks/PersianTextGuard.Benchmarks/  # MOVED from benchmarks/; reference path unchanged (same depth)
└── tools/PersianTextGuard.CorpusFill/    # NEW: fill-in and seed tool (R7–R9)
    ├── PersianTextGuard.CorpusFill.csproj
    ├── Program.cs                        # modes: default, --check, --seed
    ├── CaseWriter.cs                     # canonical JSON with the R3 escaping rules
    └── Seeds.cs                          # [Fact] literals and non-matching case kinds from the existing tests

README.md                                 # paths and commands updated (English; see Complexity Tracking)
THIRD-PARTY-NOTICES.md                    # word-list path updated
.github/workflows/ci.yml                  # R15
```

The project reference paths inside the moved projects (`..\..\src\PersianTextGuard\…`) keep working,
because `src/`, `tests/` and `benchmarks/` move together and keep the same relative depth. Only paths
pointing out to the repository root gain one `..\`: README, icon, notices and word lists.

**Structure Decision**: The constitution's monorepo layout, with the .NET port's existing internal
structure (`src/`, `tests/`, `benchmarks/`) kept intact under `dotnet/` and two additions: the
conformance runner under `tests/` and the fill-in tool under a new `tools/`. Shared data (`wordlists/`,
`conformance/`, `VERSION`) sits at the root, where every future port can reach it. Project-wide files
(README, LICENSE, notices, icon, CI, Spec Kit) stay at the root.

## Complexity Tracking

No constitution violation requires justification. Recorded for reviewers:

| Decision | Why needed | Simpler alternative rejected because |
| --- | --- | --- |
| README stays English-only while its paths and commands are updated | This feature must fix references to moved paths (FR-020, FR-021); the bilingual README is its own feature | Writing the Persian README here would merge two independent features; the constitution's adoption clause covers the gap |
| Amend spec FR-001 from "standard library" to "readable in its tests" | Java and Rust standard libraries read no structured data format (R1) | A custom line-based format every standard library can read needs a hand-written parser in every port — a new source of divergence between ports |
| Structured `build` inputs instead of JSON string escapes for invalid text | Parsers disagree on lone-surrogate escapes; verified on this machine that System.Text.Json throws (R4) | Encoding every input as bytes or base64 would make the Persian cases unreadable (FR-007) |
| Separate conformance test project instead of adding to the existing tests | Keeps the 1,029-test project untouched as the restructure's safety net (Clarification 3) | Adding to the existing project would modify it and mix port-specific tests with the cross-port corpus runner |
| Category selections recorded as count + first/last 5 + rules, not full entry lists | Every word-list edit would otherwise rewrite hundreds of corpus lines (R10) | Full lists duplicate `wordlists/` inside the corpus and make list diffs unreviewable; parsing cases already pin every entry |
