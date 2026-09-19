# Implementation Plan: Rust Port Published to crates.io

**Branch**: `005-rust-port` | **Date**: 2026-09-19 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/005-rust-port/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Add a native Rust port of PersianTextGuard in `rust/`, published to crates.io as `persian-text-guard`
(used as `persian_text_guard`), and release it with NuGet, npm and PyPI as `1.5.0`. Per
[research.md](research.md):

- **Unicode, pinned to .NET.** Rust's standard library has no general categories and case-maps with the
  compiler's Unicode version (17 today, 16 on Rust 1.85). The crate embeds category and lower-case tables
  generated from .NET 10 itself (4,099 category runs, 1,172 case pairs, about 20 KB), checked in CI, so it
  agrees with .NET on every code point. NFKC and NFD come from `unicode-normalization` (the one allowed
  crate), which differs from .NET only on 37 and 20 characters added in Unicode 16 and 17 (R1).
- **A UTF-16 view inside the crate**, as in 004: the JavaScript source is ported line by line onto `&[u16]`,
  and positions are converted to byte offsets of the caller's string at the boundary; `censor` splices the
  caller's string (R2).
- **Byte versions** of checking, finding matches and censoring for input that may not be UTF-8: decoded
  as Rust's lossy decoding decodes, positions and output in the caller's bytes (R3).
- **All 523 corpus cases run.** Measured on .NET: the four lone-surrogate inputs give the same results
  with U+FFFD in their place, and the four `null` inputs record the empty string's results. The corpus
  runner rules are amended for these two readings (R4, [contracts/corpus-runner-amendment.md](contracts/corpus-runner-amendment.md)).
- **An idiomatic API** with the same capabilities: `#[non_exhaustive]` enums parsing the corpus names,
  borrowed matches (`ProfanityMatch<'f>`), `Result` errors, `Send + Sync`, `LazyLock` bundled lists (R6, R7).
- **Build from the shared files**: `build.rs` embeds `wordlists/` and refuses a manifest version that
  differs from `VERSION`; packaging copies the lists into the crate (R5).
- **Tests**: a `libtest-mimic` corpus runner with one trial per case, API, byte, word-list, thread and
  proptest no-panic tests, and the README as doc tests; on Rust 1.85 and stable, on Linux, Windows and
  macOS (R8, R11). Criterion benchmarks in an unpublished package (R9); `cargo-semver-checks` (R10).
- **Released in lockstep**: `Publish to crates.io` needs every port's jobs. The first version is published
  with a short-lived, publish-new-only token in a protected `crates-io` environment; later versions use
  trusted publishing. A real CI dry run proves the gates first (R12, R14).

## Technical Context

**Language/Version**: Rust, edition 2024, minimum supported Rust 1.85 (spec clarification), tested on 1.85
and current stable (1.98.1 on 2026-09-19).

**Primary Dependencies**:
- **Runtime**: `unicode-normalization` 0.1.25 only (constitution allowlist).
- **Test-only**: `serde_json` 1, `libtest-mimic` 0.8, `proptest` 1.11, all building on Rust 1.85.
- **Benchmark-only**, in the separate `rust/bench` package: `criterion` 0.8 (needs Rust 1.86, so stable only).
- **Tools**: `cargo-semver-checks` 0.50; .NET 10 for `tools/gen_tables.cs`.

**Storage**: Repository files: `wordlists/*.txt` (embedded at build), `VERSION` (checked at build),
`conformance/**/*.json` (read by tests).

**Testing**: `cargo test` (unit, integration, the corpus runner, threads, proptest, doc tests); consumer
check on the packaged crate; the .NET, JavaScript and Python suites, unchanged.

**Target Platform**: Any target with `std`; CI tests x86_64 Linux, Windows (MSVC) and macOS (ARM). The
maintainer's machine builds with the GNU host (R15). `no_std` and WebAssembly are not goals of this feature.

**Project Type**: Library monorepo. This feature adds the fourth port.

**Performance Goals**: SC-005 on the i7-9700K: build under 5 ms, short clean message under 5 µs mean,
132,000-character message under 50 ms. .NET (Release) measures 600 µs and 2.5 µs (R9).

**Constraints**:
- No runtime dependencies beyond `unicode-normalization`; `#![forbid(unsafe_code)]`; no panics for any input.
- Results equal the corpus with 0 cases not applicable.
- .NET, JavaScript and Python behaviour unchanged; existing CI job names unchanged.
- Published crate under 1 MB; builds without the repository.
- Releases are irreversible: tag only with the maintainer's go-ahead.

**Scale/Scope**:
- **Rust source**: about 3,000 lines, plus the generated tables (about 1,000 lines).
- **Tests**: about 1,200 lines.
- **Other files**: a table generator, a benchmark package, a consumer crate, 5 scripts.
- **Elsewhere**: the corpus runner amendment (002 contract, `conformance/README.md`), a one-line `.csproj`
  change, CI adding 7 jobs and a publish job, two READMEs.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution v2.0.0](../../.specify/memory/constitution.md).

| Principle / section | Gate | Before research | After design |
| --- | --- | --- | --- |
| **I. Ordinary Messages Must Pass** | Ordinary corpus cases pass in every port | ✅ FR-017 | ✅ All ordinary cases run (none needs a reading from the amendment). |
| **II. User Input Never Throws** | Every text function returns for any text the language can represent; no panics | ✅ FR-014 | ✅ `&str` and `&[u8]` entry points never fail; `Result` only for masks and word-list input; `forbid(unsafe_code)`; proptest over 200,000 inputs (R7). |
| **III. Native and Dependency-Free** | Native Rust; allowlisted dependencies only; MSRV declared and tested | ✅ | ✅ `unicode-normalization` only; categories and case from embedded generated tables, no amendment (R1); MSRV 1.85 tested on three platforms. Test and benchmark dependencies are allowed. |
| **IV. Build Once, Match Fast, Share Safely** | Immutable, `Send + Sync`; construction-time work; token lookup; Criterion | ✅ | ✅ Immutable filter without interior mutability; `LazyLock`; `HashMap` token lookup ported from .NET; Criterion in `rust/bench`, README table, CI gate (R6, R7, R9). |
| **V. One Behaviour, Verified in Every Language** | Every corpus case; byte positions in Rust; same capabilities | ⚠️ The runner rules would leave 9 cases not applicable | ✅ **Resolved by amendment**: 0 not applicable, with two readings measured to equal .NET's (R4). Positions are bytes. Every capability is present. The byte versions are an input form, not a new capability: Principle II itself names invalid UTF-8 in Go's byte slices as input a port must accept, so taking raw bytes is how a language accepts its raw text, as Python's `WordList.load` is how it reads files. |
| **VI. Documented in Persian and English, Pinned by Tests** | Bilingual README; examples tested; rustdoc on every public item | ✅ | ✅ `rust/README.md` bilingual (R13), included as doc tests; `missing_docs` denied in CI. |
| **VII. Curated, Categorised, Credited Word Lists** | Lists live once in `wordlists/`, embedded at build | ✅ | ✅ `build.rs` embeds them; the packaged copy is generated and git-ignored; `THIRD-PARTY-NOTICES.md` ships in the crate. |
| **Public API & Versioning** | Lockstep from `VERSION`; tag = version; `cargo-semver-checks`; blocked if any port fails; MINOR | ✅ | ✅ `build.rs` enforces `VERSION`; every publish job needs every port's jobs and checks the tag; semver check with a first-release baseline; `#[non_exhaustive]` enums and structs keep new categories, fields and options MINOR (additive API); `1.5.0`. ⚠️ Four registries cannot publish atomically; mitigated by idempotent re-runs, as before. |
| **Development Workflow & Quality Gates** | Branch and PR; CI green for every port; behaviour PRs update corpus and ports together | ✅ | ✅ Branch `005-rust-port`; no behaviour change; the Rust jobs become required checks after merge. |
| **Governance: Adoption** | No PR moves further from unmet requirements | ✅ | ✅ Adds `rust/` and crates.io to lockstep releases. |

**Gate result**: **PASS.** The ⚠️ items are justified in Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/005-rust-port/
├── plan.md                         # This file
├── research.md                     # Phase 0: R1–R16
├── data-model.md                   # Phase 1
├── quickstart.md                   # Phase 1: validation guide, including the release
├── contracts/
│   ├── public-api.md               # Rust declarations, guarantees G1–G11, four-language name table
│   ├── package-and-release.md      # layout, commands, package contents, CI, release order
│   └── corpus-runner-amendment.md  # the two readings for ports like Rust
├── tools/                          # R1's Rust dump (rust-dump/) and R4's .NET check (fffd.cs)
├── checklists/requirements.md      # Spec quality checklist
└── tasks.md                        # Phase 2 (/speckit-tasks — not created by /speckit-plan)
```

### Source Code (repository root)

```text
VERSION                                          # 1.4.0 → 1.5.0
conformance/README.md                            # runner rules amended (corpus-runner-amendment.md)
specs/002-monorepo-conformance-corpus/contracts/corpus-format.md   # the same amendment
dotnet/src/PersianTextGuard/PersianTextGuard.csproj  # PackageValidationBaselineVersion 1.3.0 → 1.4.0

rust/                                            # NEW: the crates.io crate persian-text-guard
├── Cargo.toml  Cargo.lock  build.rs  README.md
├── src/
│   ├── lib.rs                                   # public items, crate docs from the README
│   ├── types.rs  errors.rs                      # ← types.ts
│   ├── unicode.rs  tables.rs                    # ← unicode.ts; generated tables (R1)
│   ├── utf16.rs  bytes.rs                       # view and positions (R2); byte versions (R3)
│   ├── normalizer.rs  source_map.rs  fold.rs    # ← normalizer.ts, source-map.ts, fold.ts
│   ├── filter.rs  scan.rs  regions.rs           # ← filter.ts, scan.ts, regions.ts
│   └── word_list.rs                             # ← word-list.ts, plus load
├── tests/  corpus.rs corpus/  api.rs  bytes.rs  word_list_load.rs  threads.rs  no_panic.rs
├── bench/                                       # unpublished Criterion package
├── consumer/                                    # uses the packaged crate
├── tools/gen_tables.cs
└── scripts/prepare-package.sh  set-version.sh  check-package.sh

README.md                                        # crates.io listed; Development and Releasing gain rust/
.gitignore                                       # Rust build outputs and packaging copies
.github/workflows/ci.yml                         # Rust (…) ×6, Rust checks, Publish to crates.io; needs updated
```

**Structure Decision**: The constitution's monorepo layout, with the Rust port in its own top-level
`rust/` directory next to `dotnet/`, `js/` and `python/`. It reads the shared `wordlists/`, `conformance/`
and `VERSION` from the root, and has its own tests, benchmarks and README. Modules map one to one to the
JavaScript files they port, plus `utf16.rs` and `bytes.rs`, which exist because Rust strings are UTF-8,
and `tables.rs`, because Rust's standard library has no general categories.

## Complexity Tracking

| Decision | Why needed | Simpler alternative rejected because |
| --- | --- | --- |
| Amending the corpus runner rules (U+FFFD for lone surrogates, `""` for `null`) | A Rust string cannot hold a lone surrogate, and Rust has no missing string value; the spec requires 0 not applicable. Both readings are measured to give .NET's own results (R4) | *Nine not-applicable cases*: rejected by the maintainer. *A UTF-16 input API* (clarification option C): a permanent public API few Rust users need. |
| Generated Unicode tables in the crate (about 20 KB) | The standard library has no general categories, and its case mapping changes with the compiler; the constitution allows only `unicode-normalization` | *A second dependency*: needs a constitution amendment, and follows its own Unicode version. *Standard-library case mapping*: 8 differences today, and different on 1.85 and stable (R1). |
| A UTF-16 view inside the matcher | .NET's per-unit semantics decide tokens next to supplementary characters (004 R2) | *A char- or byte-native port*: rewrites the shared algorithm where the corpus is thinnest. |
| One release with a stored token (1.5.0) | crates.io's trusted publishing is configured on an existing crate; the maintainer chose CI publishing over a manual upload (clarification) | *Manual `cargo publish`*: outside the gated CI run. *A placeholder release*: a version to yank, and still a token. The token is publish-new only, crate-scoped, expires within days, lives only in the protected environment, and is revoked after release. |
| Benchmarks in a separate package | Criterion 0.8 needs Rust 1.86, above the crate's 1.85 minimum | *Criterion as a dev-dependency*: the 1.85 jobs could not build the tests. |
| GNU toolchain on the maintainer's machine | No MSVC C++ build tools are installed | *Installing Visual Studio Build Tools*: several GB, and CI covers the MSVC target anyway. |
| Four registries published by four jobs, not atomically | GitHub Actions cannot publish to four registries in one transaction | *One job*: still not atomic. Mitigation: every job gates on every port's jobs, and re-runs are idempotent. |
