# Implementation Plan: Find Every Match and Censor Messages

**Branch**: `001-censor-find-matches` | **Date**: 2026-09-16 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-censor-find-matches/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Add `FindMatches` (every banned word in a message, each with its entry, category, evasion and
position) and `Censor` (the message with each banned word's whole word replaced by a fixed `****`)
to `ProfanityFilter` for 1.2.0, without changing what `ContainsProfanity` and `FindMatch` return.

The technical approach ([research.md](research.md)):

- **One engine.** Matching is refactored into one scan with a *first* mode and an *all* mode, so all
  four capabilities agree by construction (R2).
- **Positions on demand.** The lossy readings are searched exactly as in 1.1.0. Only a reading that
  produced a hit is rebuilt with a character-level source map, so hits can be translated back to
  the original message and clean messages pay nothing (R1).
- **Whole-word regions.** Hits are widened to whole words using the tokenizer's existing word rule
  (R3). Overlapping hits merge into one match: union region, longest entry, ties by list order (R4).
- **Clean output, guaranteed.** `Censor` checks its own output and masks again when needed, because
  `*` is a filler character the filter drops and masking can otherwise join letters into a new word.
  This was verified against 1.1.0: `k **** i **** r` is detected as `kir` (R6).
- **Positions on the existing type.** `ProfanityMatch` gains init-only `Index` and `Length`, which
  `FindMatch` fills too (R7).

Planning also found and fixed a contradiction in the spec: FR-016 as written could not hold alongside
FR-007 (R5). FR-016 is amended.

## Technical Context

**Language/Version**: C# (`LangVersion` latest, nullable enabled), .NET SDK 10

**Primary Dependencies**: None at runtime. PolySharp is build-only on `netstandard2.0`
(`PrivateAssets="all"`).

**Storage**: N/A — in-memory library; the bundled word lists are embedded resources, read once.

**Testing**: xUnit 2.9 on `net10.0`, `net8.0` and `net48`; BenchmarkDotNet for the per-message path;
package validation during `dotnet pack`.

**Target Platform**: `netstandard2.0` (.NET Framework 4.6.1+), `net8.0`, `net10.0`; AOT-compatible on
the modern targets.

**Project Type**: Library (NuGet package `PersianTextGuard`).

**Performance Goals**:
- Yes/no check within 5% of 1.1.0 (2.4 µs for a short clean message).
- `FindMatches` on a clean message ≤ 1.5× the yes/no check.
- `Censor` on a 60-word dirty message < 100 µs.

(SC-005, SC-006; i7-9700K reference machine.)

**Constraints**:
- No exceptions from message input.
- No new runtime dependencies.
- Existing 225 tests unmodified.
- `TreatWarningsAsErrors`.
- Public API additive only, with package validation passing.
- Immutable, thread-safe filter.

**Scale/Scope**: Messages of any length; tested to 132,000 characters. Filter of about 1,250 entries.
Three new public members, two new properties on `ProfanityMatch`, and roughly 500 lines of source
across the refactor and additions.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution v1.0.0](../../.specify/memory/constitution.md).

| Principle / section | Gate | Before research | After design |
| --- | --- | --- | --- |
| **I. Ordinary Messages Must Pass** | No new way of matching; ordinary-message suite stays green | ✅ The feature reports and hides what is already matched. | ✅ The engine refactor keeps the 1.1.0 examination order (R2). `ConsistencyTests` asserts 0 matches and an unchanged instance for every ordinary message (SC-002). |
| **II. User Input Never Throws** | Every message input returns a result | ✅ Required by FR-017. | ✅ Null → empty list / `string.Empty`. Invalid surrogates never split a region and stop word widening (R3). Only the mask character, a programmer input, can throw (R8). |
| **III. Zero Dependencies, Broad Compatibility** | No dependencies; `netstandard2.0` APIs only; warnings as errors | ✅ | ✅ Uses `string.Normalize`, `CharUnicodeInfo`, `IReadOnlyList<T>`, `Array.Empty<T>` — all on `netstandard2.0`. No polyfill types in public API; `System.Range` rejected for that reason (R7). |
| **IV. Build Once, Match Fast, Share Safely** | Immutable; per-message path benchmarked; no linear scan of whole-word entries | ✅ | ✅ Scan state is per call (data model: no state transitions). Clean messages build no maps (R1, R9). Four new benchmark cases, plus before-and-after numbers for the existing five, required in the PR (quickstart §3). Entry order is computed at construction. |
| **V. Behaviour Is Documented and Test-Pinned** | README examples tested; limitations current; XML docs on public members | ✅ | ✅ Quickstart §5 covers the new README examples with `ReadmeExampleTests` methods, removes the "first match only" limitation, and adds XML docs per the [contract](contracts/public-api.md). |
| **VI. Curated, Categorised, Credited Word Lists** | Lists unchanged or changed per the rules | ✅ No list changes. | ✅ No list changes. |
| **Public API & Versioning** | Additive, source-compatible; matching-behaviour changes called out | ✅ MINOR, 1.2.0. | ⚠️ **Passes, with a release note.** `ProfanityMatch` equality now includes `Index` and `Length` (R7). Source- and binary-compatible, and package validation passes, but a `FindMatch` result no longer equals a hand-built `ProfanityMatch(word, evasion)`. The versioning section requires calling out changes that alter matching behaviour; this alters comparison behaviour, so it is called out the same way. |
| **Development Workflow & Quality Gates** | Branch + PR; CI green on 3 frameworks; docs in same PR; benchmarks for hot-path changes | ⚠️ The branch `001-censor-find-matches` is recorded in `.specify/feature.json` but not created in git; the working tree is on `main`. | ⚠️ Same. It must be created before `/speckit-implement`. Everything else is planned in the quickstart. |

**Gate result**: **PASS.** No violation needs justifying. The two ⚠️ rows are obligations, not
exceptions: a release note, and creating the branch before implementation.

## Project Structure

### Documentation (this feature)

```text
specs/001-censor-find-matches/
├── plan.md                  # This file
├── research.md              # Phase 0: R1–R10 design decisions
├── data-model.md            # Phase 1: public and internal entities
├── quickstart.md            # Phase 1: validation scenarios, benchmarks, compatibility
├── contracts/
│   └── public-api.md        # Phase 1: the 1.2.0 public API contract
├── checklists/
│   └── requirements.md      # Spec quality checklist (from /speckit-specify)
└── tasks.md                 # Phase 2 (/speckit-tasks — not created by /speckit-plan)
```

### Source Code (repository root)

```text
src/PersianTextGuard/
├── ProfanityFilter.cs            # public surface: ContainsProfanity, FindMatch (now positioned),
│                                 #   FindMatches, Censor; constructor records entry order;
│                                 #   class becomes partial
├── ProfanityFilter.Scan.cs       # NEW: the first/all scan engine refactored from 1.1.0 matching;
│                                 #   produces hits per reading, in 1.1.0 order
├── ProfanityFilter.Regions.cs    # NEW: hit → mapped region → whole-word widening → merge (R3, R4);
│                                 #   Censor output building and the clean-output loop (R6)
├── SourceMap.cs                  # NEW (internal): mapped Comparison normalisation (segment-wise
│                                 #   NFKC with fallbacks), mapped Fold, mapped Squeeze (R1)
├── PersianNormalizer.cs          # IsWordCharacter reused for widening; public behaviour unchanged
├── ProfanityMatch.cs             # + Index, Length (init-only, non-positional)
├── BannedWord.cs                 # unchanged
├── ProfanityFilterOptions.cs     # unchanged
├── WordList.cs                   # unchanged
└── PersianTextGuard.csproj       # Version 1.2.0

tests/PersianTextGuard.Tests/
├── FindMatchesTests.cs           # NEW: User Story 1 scenarios and edge cases
├── CensorTests.cs                # NEW: User Story 2 scenarios, mask validation, FR-015 cases
├── ConsistencyTests.cs           # NEW: corpus-wide FR-016 / FR-015 / SC-002 / SC-004 checks, built
│                                 #   by reflection over DefaultListTests inline data, plus robustness
│                                 #   and concurrency
├── SourceMapTests.cs             # NEW: mapped readings equal unmapped readings; maps are
│                                 #   non-decreasing; fallbacks only ever widen
├── ReadmeExampleTests.cs         # + methods for the new README examples (existing methods untouched)
├── DefaultListTests.cs           # unchanged (SC-008)
├── ProfanityFilterTests.cs       # unchanged (SC-008)
└── PersianNormalizerTests.cs     # unchanged (SC-008)

benchmarks/PersianTextGuard.Benchmarks/
└── Program.cs                    # + FindMatchesClean, FindMatchesDirty, CensorShortDirty,
                                  #   CensorLongDirty

README.md                         # FindMatches and Censor examples; Limitations updated
```

**Structure Decision**: Keep the existing single-library layout: one package project, one xUnit
project, one benchmark project. `ProfanityFilter` becomes a partial class split across three files by
responsibility (public surface, scanning, regions and censoring). The 1.1.0 `ProfanityFilter.cs` is
already about 600 lines, and the scan engine and region logic are separable units with their own
tests. `SourceMap` is internal and reached from tests through the existing `InternalsVisibleTo`.

## Complexity Tracking

No constitution violations require justification. Recorded for reviewers, not as violations:

| Decision | Why needed | Simpler alternative rejected because |
| --- | --- | --- |
| Lazy, per-reading source maps (R1) | Positions without slowing clean messages | Mapping every reading up front allocates on every clean message and breaks SC-005. |
| Segment-wise NFKC with chunk and whole-message fallbacks (R1) | `string.Normalize` cannot report positions | Whole-string NFKC has no map. Chunk-only positions would hide innocent words next to punctuation (FR-011). |
| Refactor into one engine rather than add a second (R2) | FR-016 agreement by construction | A parallel `FindMatches` implementation duplicates ~400 lines that would drift. |
| Censor re-checks and re-masks its own output (R6) | `*` is an evasion filler; verified that masking can create a new match | Treating `*` as a boundary would stop catching `f*ck`. Padding masks with spaces would change characters outside hidden regions (FR-009). |
