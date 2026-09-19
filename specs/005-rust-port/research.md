# Research: Rust Port Published to crates.io

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Date**: 2026-09-19

Decisions made in Phase 0, each with the measurement or reasoning behind it. Spec 004's research is cited
as "004 R*n*", spec 003's as "003 R*n*".

---

## R1. Unicode data: generated tables for categories and case, `unicode-normalization` for NFKC

**Method.** A Rust program in the scratchpad dumped, for every UTF-16 unit and every code point, what the
Rust port would get from the standard library (`char::to_lowercase` under the one-unit rule,
`char::is_whitespace`) and from `unicode-normalization` 0.1.25 (NFKC, NFD). The output used the format of
`specs/004-python-port/tools/dump.py` and was compared with the .NET 10.0.11 dump from 004's tools
(quickstart §8 of 004).

**Findings.**

| Primitive | Rust 1.98.1 std / `unicode-normalization` 0.1.25 (both Unicode 17.0) against .NET 10 (Unicode 16.0) |
| --- | --- |
| Whitespace: `char::is_whitespace` and the explicit .NET set | 0 differences on all 65,536 units |
| Lower-casing one unit, kept when the result is not one BMP character | 8: U+1C89, U+A7CB, U+A7CC, U+A7CE, U+A7D2, U+A7D4, U+A7DA, U+A7DC (case pairs new in Unicode 16 and 17) |
| NFKC | 37: U+A7F1 (unassigned in .NET 10) and U+1CCD6–U+1CCF9 (the same 36 as Python 3.14, 004 R1) |
| NFD | 20: the same 20 Unicode 16 decompositions as Python 3.14 |
| General category | not in the standard library at all |

Rust's standard library has no general-category API, and its case mapping follows the compiler's Unicode
version, so the same crate would lower-case differently on Rust 1.85 (Unicode 16) and on current stable
(Unicode 17).

**Decision** (spec clarification, FR-004).
- **Categories** come from a table generated from .NET 10 itself: `rust/tools/gen_tables.cs`, a .NET
  file-based program, writes `rust/src/tables.rs` with the category of every code point as sorted
  ranges. Measured on the .NET 10 dump: 4,099 runs of equal category over U+0000–U+10FFFF, about
  20 KB as `(start, category)` pairs. CI regenerates the file with .NET 10
  and fails on any difference. The tables therefore agree with .NET on every code point by construction,
  which neither JavaScript nor Python achieves (they document the Unicode 15+ drift as a limitation).
- **Lower-casing** comes from the same generator: the 1,172 units whose `char.ToLowerInvariant`
  differs from themselves, as a sorted table. It removes the 8 differences, and makes the result independent of the
  Rust version that compiles the crate.
- **Whitespace** is the explicit .NET set, as in every port.
- **NFKC and NFD** come from `unicode-normalization`, the one Rust crate the constitution allows. Its 37
  NFKC and 20 NFD differences all concern characters added in Unicode 16 or 17; none is in the corpus.
  They are documented as a limitation, like 004's, and research re-runs the comparison whenever the crate
  or .NET moves to a new Unicode version.

**Alternatives considered.**
- *The standard library for case mapping*: 8 differences today, and the result would change with the
  compiler version, which a library should not do.
- *A second dependency for categories* (option B of the clarification): rejected by the maintainer; it
  also follows its own Unicode version.
- *Tables built by `build.rs` from a shipped UnicodeData.txt* (option C): about 2 MB in the crate, and
  a build step every user pays for.

---

## R2. Strings: the matcher runs on UTF-16 units, positions are converted to bytes

**Problem.** As in 004 R2, .NET decides tokens by testing single UTF-16 units, so a supplementary
character next to a word behaves as two non-letter units. The corpus pins this (the ten cases added in
004). A Rust `&str` is UTF-8 and indexes bytes.

**Decision.** Port the JavaScript source line by line onto a UTF-16 view, exactly as 004 did for Python:
- each public entry point encodes the message as `Vec<u16>` (`str::encode_utf16`); an ASCII message takes a
  fast path that works on the bytes as units, since every ASCII byte is one unit;
- the matcher, normalizer and tokenizer work on `&[u16]`, with `text[i]` meaning unit *i*, as
  `charCodeAt(i)` does in TypeScript;
- match regions are converted from units to byte offsets of the caller's string once, with a prefix
  table built only when there is a match and the message is not ASCII. Regions never split a surrogate
  pair (004 R2), so every converted offset is a character boundary;
- `censor` splices the caller's own string, so text outside a match is returned byte for byte;
- `normalize` and `tokenize` convert back to UTF-8. `tokenize` returns slices of the caller's string,
  because every token is a contiguous run of the message.

**Alternatives considered.** *Working on `char`s*: the per-unit tests and indexes of the shared algorithm
would all need rewriting, where the corpus is thinnest (004 R2). *Working on bytes*: the same problem, and
more of it.

---

## R3. The byte versions (FR-008a)

**Decision.** `contains_profanity_bytes`, `find_match_bytes`, `find_matches_bytes` and `censor_bytes`
(and `censor_bytes_with` for a chosen mask) take `&[u8]`:
1. The bytes are decoded with `<[u8]>::utf8_chunks`, the iterator behind `String::from_utf8_lossy`, so each
   maximal invalid sequence becomes exactly one U+FFFD, as Rust's lossy decoding does. Valid input is
   borrowed, not copied.
2. A table maps each byte of the decoded string back to the caller's bytes: valid bytes one to one, each
   U+FFFD's three bytes to the invalid sequence it replaced.
3. The string versions run on the decoded text, and regions are mapped back. A region that covers a
   U+FFFD covers the whole invalid sequence, and never ends inside a valid character.
4. `censor_bytes` splices the caller's bytes: each region becomes four copies of the mask's UTF-8 encoding,
   and every other byte, invalid or not, is copied unchanged.

Randomized tests check, for every byte sequence, that the decision and matches equal the string versions
on `String::from_utf8_lossy` of the same bytes (SC-007).

**Alternatives considered.** *Replacing invalid bytes in the output*: callers who pass bytes usually store
bytes; returning them unchanged outside masked words is what the string version does for text.

---

## R4. The corpus's lone surrogates and `null` inputs

**Measurement.** A .NET file-based program referencing `dotnet/src/PersianTextGuard` ran the four corpus
inputs that contain a lone surrogate twice: as recorded, and with U+FFFD in place of each lone surrogate.

| Case | Recorded (lone surrogate) | With U+FFFD |
| --- | --- | --- |
| `robustness-find-matches-tests-001` | `kir` at 5, length 3; censored `hi <D83D> ****` | the same; censored `hi <FFFD> ****` |
| `robustness-consistency-tests-001` | no match; `hi <D83D>` | no match; `hi <FFFD>` |
| `robustness-consistency-tests-002` | no match; `<DE00> hi` | no match; `<FFFD> hi` |
| `robustness-consistency-tests-003` | `kir` at 0 and `kos` at 6; `**** <D83D> ****` | the same; `**** <FFFD> ****` |

The results are identical apart from the replaced character, as expected: .NET replaces a lone surrogate
with U+FFFD before normalizing (004 R1), and neither is a word character.

Four other cases have a `null` input (`normalization-persian-normalizer-tests-010`,
`robustness-censor-tests-001`, `robustness-profanity-filter-tests-001`,
`tokenization-persian-normalizer-tests-002`). Each records exactly the empty string's result: output `""`,
no match, censored `""`, no tokens.

**Decision** (spec clarification, FR-017).
- The Rust runner builds every Input as UTF-16 units and decodes them with `String::from_utf16_lossy`,
  which turns each lone surrogate into U+FFFD and keeps valid pairs; the same is applied to text in
  `expected`. Positions still count the replacement as one code point.
- `null` is read as `""`.
- `mask-lone-high-surrogate` (expected `accepted: false`): a Rust mask is a `char`, which cannot hold a
  surrogate, so the runner records `accepted: false` for a mask that does not build into one `char`. The
  case then passes like any other, and a test asserts that `char::from_u32(0xD83D)` is `None`.
- The corpus format contract (002) gains these two readings, for ports whose strings cannot hold lone
  surrogates or have no missing string value, and only these; see
  [contracts/corpus-runner-amendment.md](contracts/corpus-runner-amendment.md). No case, and no other port,
  changes.

---

## R5. Word lists and version at build time

**Decision.**
- **`build.rs`** reads the three lists in .NET's order (`persian.txt`, `finglish.txt`, `english.txt`),
  first from `../wordlists/` (building in the repository), otherwise from the crate's own `wordlists/`
  (building from crates.io), and writes them into `OUT_DIR` for `include_str!`. It emits
  `cargo:rerun-if-changed` for each file.
- **Packaging**: `rust/scripts/prepare-package.sh` (bash, which every CI runner and Git for Windows has)
  copies the three lists into `rust/wordlists/` (git-ignored), with the root `LICENSE` and
  `THIRD-PARTY-NOTICES.md`. `Cargo.toml`'s `include` list names them, which overrides `.gitignore` for
  `cargo package`. There is no hand-edited copy in the repository (FR-005).
- **Version**: a manifest's `version` must be a literal, so `rust/Cargo.toml` states it, and `build.rs`
  fails the build when `../VERSION` exists and differs from `CARGO_PKG_VERSION`. A stale version cannot
  reach a build, a test run or a release. `rust/scripts/set-version.sh` rewrites the manifest from
  `VERSION`. The prerelease form `1.5.0-dev.2` is valid SemVer as it is, so no conversion is needed (004
  R9 needed PEP 440).

---

## R6. Public API in Rust

**Decision.** Full signatures are in [contracts/public-api.md](contracts/public-api.md).
- **Enums** `WordMatchMode`, `WordCategory`, `EvasionKind`, `NormalizationStep`, each `Copy`, with
  `FromStr` and `Display` using the corpus names (`"wholeWord"`, `"slur"`), so configuration files can
  name them. Unknown names are `ParseNameError` (FR-013).
- **`BannedWord`** `{ text: String, mode, category }`, with `new(text)` plus `with_mode` and
  `with_category` for the defaults. Invalid modes and categories cannot be expressed (FR-013).
- **`ProfanityFilterOptions`**: `#[non_exhaustive]`, `Default` (every option on), and setters, so adding an
  option later is not a breaking change.
- **`ProfanityFilter::new(words, options)`** takes any `IntoIterator` whose items `Borrow<BannedWord>`, so
  `WordList::persian_default()`, a `Vec<BannedWord>` and a slice all work. Construction cannot fail.
- **`ProfanityMatch<'f>`** `{ word: &'f BannedWord, evasion: EvasionSet, start: usize, len: usize }`, with
  `range()`. The entry is borrowed from the filter, so a match costs no allocation. `EvasionSet` is a
  `Copy` set that iterates in declaration order.
- **Censoring**: `censor(&str) -> String` with the default `*`, which cannot fail; `censor_with(&str, char)
  -> Result<String, InvalidMask>`.
- **Normalization**: `normalize(&str, Normalization) -> String`, where `Normalization` is a `Copy` set of
  steps with the constants `COMPARISON`, `STANDARD` and `NONE`, built from steps with `|`. `tokenize(&str)
  -> Vec<&str>`; `to_persian_digits` and `to_ascii_digits` return `String`.
- **Word lists**: `WordList::all()` and `persian_default()` return `&'static [BannedWord]`, parsed once;
  `bundled(&[WordCategory]) -> Vec<&'static BannedWord>`; `parse(&str) -> Result<Vec<BannedWord>,
  WordListError>`; `load(path)` and `load_reader(reader)`.
- **Errors**: `InvalidMask`, `ParseNameError` and `WordListError` (unknown category with its line and name,
  I/O, invalid UTF-8), each `std::error::Error + Send + Sync + 'static`.

**Alternatives considered.** *Returning owned entries in matches*: an allocation per match on the hot path.
*`Option<&str>` parameters for a missing message*: unidiomatic; the caller's `Option` handles it.

---

## R7. Sharing and never panicking

**Decision.**
- The filter owns immutable data only (`Box<[..]>`, `HashMap`), so it is `Send + Sync` automatically; a
  compile-time test asserts it for every public type. The bundled lists use `std::sync::LazyLock`
  (stable since 1.80), so they are parsed once even under contention.
- `#![forbid(unsafe_code)]`.
- Indexing is kept inside loops whose bounds are checked, as in the TypeScript. A `proptest` test
  (1.11, minimum Rust 1.85) runs 100,000 arbitrary strings and 100,000 arbitrary byte sequences through
  every entry point (SC-007), and `cargo test` runs the whole corpus, whose robustness cases include the
  132,000-character message.

---

## R8. Tests

**Decision.**
- **Unit tests** beside the code, ported from `js/test/internals.test.ts` and `unicode.test.ts` (as 004
  T019).
- **`tests/corpus.rs`** with `harness = false` and `libtest-mimic` 0.8 (minimum Rust 1.65): one named trial
  per corpus case, so `cargo test` lists and filters cases by id and continues past failures (FR-016).
  Guards are trials too. Parsing uses `serde_json` (test-only).
- **`tests/api.rs`**, **`tests/bytes.rs`**, **`tests/word_list_load.rs`**, **`tests/threads.rs`** (8 threads,
  every corpus input twice, SC-008), **`tests/no_panic.rs`** (proptest).
- **Doc tests**: `lib.rs` includes the README with `#![doc = include_str!("../README.md")]`, so every
  `rust` block in it is compiled and run (FR-024), and each public item's examples are doc tests too.
- Test-only dependencies (`serde_json`, `libtest-mimic`, `proptest`) all build on Rust 1.85, so the
  minimum-version jobs run the full suite.

---

## R9. Benchmarks with Criterion

**Measurement.** criterion 0.8.2 declares Rust 1.86, one release above the crate's minimum.

**Decision.** The benchmarks are a separate, unpublished package, `rust/bench/` (`persian-text-guard-bench`,
`publish = false`), with Criterion and a path dependency on the crate. The crate's own dev-dependencies
stay within Rust 1.85. It runs the same ten operations and messages as .NET, JavaScript and Python (003
R11, 004 R11). A small binary in the same package turns Criterion's `estimates.json` files into the
README's table. CI runs a regression gate on stable: `CleanShortMessage` must stay under 50 µs on shared
runners, ten times SC-005's 5 µs, like 004's 2× gate but with room for runner noise at microsecond scale.

**Estimate.** The Rust port is a native, ahead-of-time compiled version of the algorithm .NET runs with a
JIT. .NET 10 measures 600 µs to build and 2.5 µs for a short clean message (README, BenchmarkDotNet,
Release); a Debug run of the same code here measured 1.8 ms, 7.9 µs and 18.6 ms for the 132,000-character
message. SC-005's targets (5 ms, 5 µs, 50 ms) are 2× to 8× above those, so they hold even if the UTF-16
conversion per message and a less tuned first version cost Rust some of .NET's speed. They are checked on
the named machine before release (quickstart §6).

---

## R10. API compatibility with `cargo-semver-checks`

**Decision.** `cargo-semver-checks` 0.50 (which needs Rust 1.93 to build, so it runs on stable) through
`obi1kenobi/cargo-semver-checks-action` v2, pinned by commit. It compares the crate with the newest
version on crates.io.
- **Before 1.5.0**, crates.io has no `persian-text-guard`. A preceding step asks the crates.io API and, when
  the crate does not exist, prints "baseline: no previous release" and skips the check (FR-022's
  first-release rule, as 004 R14).
- **Proof** before release: on a scratch commit, rename a public function's parameter type or remove a
  method, and run `cargo semver-checks --baseline-rev HEAD~1`; it must fail and name the change.

---

## R11. CI jobs

**Decision.**

| Job | Runs on | Steps |
| --- | --- | --- |
| `Rust (stable, ubuntu-latest)`, `Rust (stable, windows-latest)`, `Rust (stable, macos-latest)`, `Rust (1.85, ubuntu-latest)`, `Rust (1.85, windows-latest)`, `Rust (1.85, macos-latest)` | matrix | `cargo test --locked` (unit, integration, corpus, threads, proptest, doc tests) |
| `Rust checks` | ubuntu, stable | `cargo fmt --check`; `cargo clippy --all-targets -- -D warnings`; `cargo doc` with `RUSTDOCFLAGS=-D warnings` and `missing_docs` denied; table regeneration with .NET 10 and `git diff --exit-code`; `prepare-package.sh`, `cargo package --locked` and the file-list check; build of the packaged crate on its own; the semver check; the benchmark gate; upload of the `.crate` as the `crate-package` artifact |
| `Publish to crates.io` | ubuntu | on `v*` tags; `needs` every build and test job of every port; `environment: crates-io`; tag and version checks; skip if the version exists; `cargo publish` |

- Toolchains come from `dtolnay/rust-toolchain`, pinned by commit, with the `toolchain` input; Windows runs
  the MSVC target, the default there.
- `Publish to NuGet`, `Publish to npm` and `Publish to PyPI` gain `rust` and `rust-checks` in `needs`.
- Existing job names are unchanged (FR-021). After merge, the seven Rust job names join `main`'s
  required checks (sixteen in all).

---

## R12. Publishing to crates.io (spec clarification, FR-020)

**Decision.**
- **Environment** `crates-io` on GitHub, deployments limited to tag `v*`, like `pypi` (004).
- **1.5.0**: the maintainer creates a crates.io token with the `publish-new` scope only, restricted to
  `persian-text-guard` and set to expire within days, and stores it as the environment secret
  `CARGO_REGISTRY_TOKEN`. The job publishes with it. After release the maintainer deletes the secret,
  revokes the token, and adds this repository and `ci.yml` as a trusted publisher on the crate's
  crates.io settings.
- **Every later release**: when the secret is absent, the job authenticates with
  `rust-lang/crates-io-auth-action` v1 (pinned by commit, `id-token: write`) and publishes with the
  short-lived token it returns. No token is stored.
- **Re-runs**: the job asks the crates.io API whether the version exists and skips if it does, as NuGet's
  `--skip-duplicate`, npm's check and PyPI's `skip-existing` do.
- **Dry run** (R14): `cargo publish --dry-run` replaces the upload.

---

## R13. Documentation

**Decision.**
- `rust/README.md` is the crates.io page and the crate's front page on docs.rs. It has installation
  (`cargo add persian-text-guard`) and a quick start in English and Persian, the four-language name table,
  the byte versions, the performance table, the minimum Rust version, the limitations (R1) and links.
- Persian paragraphs follow 004 R16: each in its own `<div dir="rtl">`, starting and ending with a Persian
  word, with code in separate blocks, since crates.io's renderer strips `dir` like PyPI's.
- `#![warn(missing_docs)]`, denied in CI, gives every public item documentation (FR-023).
- `[package.metadata.docs.rs]` sets `all-features = true` and the default target.

---

## R14. Release gates dry run

**Decision.** Repeat 004 R19 with the crates.io job added: versions `1.5.0-dev.1` to `.3`, the upload
replaced by `cargo publish --dry-run`, the safety grep also requiring that no `cargo publish` without
`--dry-run` and no `crates-io-auth-action` step remains. The three runs are a failing Rust job, everything
green, and a mismatched tag. The dry run needs the `crates-io` environment but not the token.

---

## R15. Local toolchain

**Decision.** The maintainer's machine has no MSVC C++ build tools, which Rust's default Windows target
links with. Rust was installed (with the maintainer's approval) with the self-contained GNU host,
`x86_64-pc-windows-gnu`: stable 1.98.1 and 1.85, through the official `rustup-init.exe` from
`static.rust-lang.org`. CI tests the MSVC target on `windows-latest`. The crate has no target-specific
code, so the two agree.

---

## R16. Version 1.5.0 and the other ports

**Decision.** `VERSION` 1.4.0 → **1.5.0** (MINOR: a newly supported language). NuGet, npm and PyPI 1.5.0
are identical to 1.4.0 apart from the version. .NET's `PackageValidationBaselineVersion` moves to 1.4.0;
`npm run api:compat` compares with `v1.4.0`; and `python/scripts/check_api.py` now finds `v1.4.0`, the
first tag with `python/`, so griffe runs for real for the first time. The Python classifiers gain nothing
(CPython 3.15 is still a release candidate).
