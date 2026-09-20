# Verification: Rust Port Published to crates.io

**Feature**: [spec.md](spec.md) | **Tasks**: [tasks.md](tasks.md) | **Quickstart**: [quickstart.md](quickstart.md)

Results recorded while implementing spec 005, on the maintainer's machine (Windows 11, i7-9700K) unless
a CI run is named.

## Baseline (T001)

- Branch `005-rust-port`, commit `7b4a9d5475bbef0946fbcc445efd8f2e6b56cd14`; `git status --short` showed only the untracked
  `graphify-out/`.
- `dotnet test dotnet/tests/PersianTextGuard.Tests`: 1,029 passed on each of `net8.0`, `net10.0` and `net48`.
- `dotnet test dotnet/tests/PersianTextGuard.Conformance`: 532 passed on each of the three targets.
- `js/`: `npm ci`, then `npm run test:all`: 675 passed (5 files).
- `python/`: `uv sync --locked --group package`, then `uv run pytest -q`: 727 passed.
- `rustc 1.98.1 (48a229cea 2026-09-01)`; `rustc 1.85.1 (4eb161250 2025-03-15)`; `cargo 1.98.1 (797e8a9bc 2026-08-05)`; active toolchain
  `stable-x86_64-pc-windows-gnu`.

### Local toolchain note

The GNU host's self-contained `dlltool.exe` needs an assembler (`as.exe`) that is not installed, so
test-only dependencies using `raw-dylib` (`windows-sys` 0.61 through `libtest-mimic` and `tempfile`;
`getrandom` through `proptest`) failed to build. Fixed locally, without changing the repository: the
rustup component `llvm-tools` was added to stable, its `llvm-ar.exe` copied as
`C:\Users\amire\.cargo\llvm-dlltool\llvm-dlltool.exe` (it acts as `llvm-dlltool` under that name), and
each shell sets
`CARGO_TARGET_X86_64_PC_WINDOWS_GNU_RUSTFLAGS="-C dlltool=C:/Users/amire/.cargo/llvm-dlltool/llvm-dlltool.exe"`.
CI builds the MSVC target and is unaffected.

## Lint command (T008)

Run in `rust/`:

```bash
cargo fmt --check && cargo clippy --locked --all-targets -- -D warnings && RUSTDOCFLAGS="-D warnings" cargo doc --locked --no-deps && cargo test --locked
cargo +1.85 test --locked
```

On the skeleton (T005–T007): clean on stable, and `cargo test --locked` passes on stable and 1.85 (the
placeholder README doc test). `scripts/set-version.sh` self-check: "no change" with `VERSION` unchanged; a
hand-edited `version = "0.0.1"` fails the build with "VERSION says 1.4.0 but Cargo.toml says 0.0.1; run
scripts/set-version.sh"; the script restores 1.4.0 and the `--locked` build passes.

## Corpus runner amendment (T009)

Applied to `specs/002-monorepo-conformance-corpus/contracts/corpus-format.md` (obligations 3 and 5, and a
line under the title) and to `conformance/README.md` with the same wording. No case file changed. The
existing runners after the change: .NET conformance 532 × 3 (`net10.0`, `net8.0`, `net48`); JavaScript
`test/corpus.test.ts` 530; Python `pytest -m corpus` 532.

## Foundational layers (T010–T020)

- `dotnet run tools/gen_tables.cs -- src/tables.rs` (from `rust/`, .NET 10.0.11): **4,099 category runs and
  1,172 lower-case pairs**, as research R1 measured, plus the 128-entry ASCII category table of the fast
  path. The header names the .NET major version only ("from .NET 10"), not `Environment.Version`, so a
  .NET patch release in CI does not change the file; running the generator again leaves
  `git diff --exit-code src/tables.rs` clean. `.gitattributes` keeps the file LF on Windows checkouts.
- Unit tests (`cargo test --locked --lib`): 40 passed on stable 1.98.1 and on 1.85.1: Unicode primitives
  (whitespace against the generated categories on all 65,536 units, the 8 Unicode 16/17 case pairs left
  unchanged, `Cs`/`Lo`/`Cn`/`Mc` categories, NFKC around all 66 noncharacters, noncharacter lengths),
  the ported `internals.test.ts` (source maps, normalizer, word-list headings, 1,250 bundled entries and
  1,025 in `persian_default()`), fold and squeeze, the UTF-16 view and byte map.
- `cargo fmt --check` and `cargo clippy --locked --all-targets -- -D warnings`: clean. `clippy.toml` allows
  `unwrap`/`expect` in tests. The internal modules carry a temporary `#[allow(dead_code)]` in `lib.rs`
  until the filter uses them (T024–T028). The documentation check and the doc tests run from T028, since
  several examples use `ProfanityFilter`.

## US1: the filter, byte versions and package (T021–T032)

- `cargo test --locked --test api --test word_list_load --test bytes`: 17 + 5 + 4 passed (spec US1
  scenarios 1–7 and the independent test, G3–G7, G9, G11, masks, options, any iterable of entries,
  file and reader loading with a BOM and `\r\n`, `NotFound`, `InvalidUtf8`, the byte versions with
  invalid sequences kept). Whole suite: 45 unit tests, 26 integration tests and 35 doc tests (README
  quick start and every public type's example) pass; clippy and `cargo doc` with warnings denied are clean.
- `scripts/prepare-package.sh && scripts/check-package.sh`:
  1. file list: 25 files, exactly the allowlist (`.cargo_vcs_info.json` is absent when packaging a dirty
     tree locally; the script accepts both, and CI packages a clean tree);
  2. `cargo package --locked` built the unpacked crate on its own, from its packaged `wordlists/`. Cargo
     warned that the test targets (`api`, `bytes`, `corpus`, `word_list_load`) are not in the package, as
     expected, and the packaged `Cargo.toml` has no `[[test]]` section;
  3. `persian-text-guard-1.4.0.crate`: 80,453 bytes (280.7 KiB unpacked), under 1 MB (SC-004);
  4. manifest: `persian-text-guard` 1.4.0 (= `VERSION`), `rust-version` 1.85, one normal dependency,
     `unicode-normalization`;
  5. `consumer/` built against `target/package/persian-text-guard-1.4.0` and printed `ok`.
- `scripts/set-version.sh` self-check repeated with `consumer/` present: "no change"; a hand-edited
  `0.0.1` fails the build; the script restores 1.4.0 in `Cargo.toml` and both lock files.

## US2: the conformance corpus (T033–T041)

- `cargo test --locked --test corpus`: **531 trials passed, 0 failed** — 523 cases and 8 guard trials
  (loads; `formatVersion` is 1; a copy with `formatVersion` 2 in a temporary directory is refused; at
  least 300 cases; unique ids matching `^[a-z0-9]+(-[a-z0-9]+)*$`; no pending case; every configuration
  exists and builds; **0 cases not applicable**). Every case passed on the first run, on stable and on
  1.85; no port change was needed, and no corpus case was touched. 1.10 s in debug, 0.12 s in release.
- Positions: the port reports bytes; each case's recorded code-point positions are converted to bytes of
  the built input before comparing (amended obligation 5). Lone surrogates are read as U+FFFD and `null`
  as `""` (amended obligation 3); `mask-lone-high-surrogate` is `accepted: false` because the units do not
  build into one `char`.
- `tests/api.rs` extends G3 and G4 to **every matching corpus input** (523 inputs × 3 filter
  configurations).
- `tests/no_panic.rs` (proptest): 2,000 cases per strategy by default (4 s). SC-007's full run,
  `PROPTEST_CASES=100000 cargo test --locked --release --test no_panic`: **100,000 strings and 100,000
  byte sequences, 17.4 s**, no panic; the byte versions agreed with the string versions on
  `String::from_utf8_lossy` throughout, and every region was inside its input.
- `tests/threads.rs` (SC-008): one filter per configuration, 8 threads behind a `Barrier`, each checking
  every matching input twice — identical answers to the single-threaded pass; a filter shared through an
  `Arc`; and, in a fresh process, 8 threads using the bundled lists for the first time at the same moment
  printed `FIRST-USE 8 1`: one slice for all of them.
- Whole suite (T039): **645 tests** on stable 1.98.1 and on 1.85.1 — 45 unit, 18 api, 4 bytes, 531 corpus,
  3 no-panic, 4 threads, 5 word-list-load, 35 doc tests.
- Failure reporting (T040), on scratch edits reverted afterwards with `git checkout -- conformance`:

  ```text
  ---- matching-persian-ordinary-messages-pass-001 ----
  Case 'matching-persian-ordinary-messages-pass-001' in matching-persian.json: breaks its kind rule
    input "هر کس پلات بالاست پیام بده"
    ordinary requires containsProfanity to be false

  ---- fa-emoji-before-word ----
  Case 'fa-emoji-before-word' in matching-persian.json: 1 field(s) differ
    input "😀 کیر"
    expected.censored: expected "😀 ####" actual "😀 ****"
  ```

  Exactly those 2 trials failed (529 passed). With `conformance/` renamed away, one trial failed with
  "Conformance corpus not found: D:\Git\PersianTextGuard\conformance". Both were restored.
- Bundled selections (T041, FR-018, SC-002): `cargo test --test api -- --ignored` wrote
  `artifacts/compare/rust.txt` (1,250 lines) and printed
  `{"all": 1250, "default": 1025, "categories": {"uncategorized": 0, "profanity": 93, "sexual": 353,
  "insult": 400, "slur": 146, "harassment": 33, "mild": 225}}`. `git diff --no-index` against
  `python.txt` and `js.txt`: **0 differences** with either.

## US3: CI and the release gates (T042–T047)

- `.github/workflows/ci.yml` gains two jobs, with every existing job name unchanged:
  - **`Rust (${{ matrix.toolchain }}, ${{ matrix.os }})`**: `stable` and `1.85` × `ubuntu-latest`,
    `windows-latest`, `macos-latest` (six jobs), `fail-fast: false`, `cargo test --locked`;
  - **`Rust checks`** (ubuntu, stable): `cargo fmt --check`; `cargo clippy --locked --all-targets -D
    warnings`; `cargo doc` with `RUSTDOCFLAGS=-D warnings`; the .NET 10 table regeneration and
    `git diff --exit-code src/tables.rs`; `PROPTEST_CASES=100000 cargo test --locked --release --test
    no_panic`; `scripts/prepare-package.sh` and `scripts/check-package.sh`; the API check (a step asks
    crates.io and, on 404, prints "baseline: no previous release" and skips
    `obi1kenobi/cargo-semver-checks-action`); `scripts/bench-gate.sh`; and the `crate-package` artifact.
  - Actions are pinned by commit: `dtolnay/rust-toolchain@02cb101` (master),
    `Swatinem/rust-cache@6323deb` (v2.9.2), `obi1kenobi/cargo-semver-checks-action@6b69fcf` (v2.9),
    `rust-lang/crates-io-auth-action@c6f97d4` (v1.0.5).
- **`Publish to crates.io`**: `needs: [build, netfx, javascript, python, rust, rust-checks]`, only on
  `refs/tags/v*`, `environment: crates-io`, `id-token: write`; it checks the tag against `VERSION` and
  `rust/Cargo.toml` against `VERSION`, skips a version already on crates.io, runs
  `prepare-package.sh`, authenticates through `crates-io-auth-action` only when the environment has no
  `CARGO_REGISTRY_TOKEN`, and publishes with `cargo publish --locked`.
- `Publish to NuGet`, `Publish to npm` and `Publish to PyPI` now also need `rust` and `rust-checks`.
- The YAML was validated with the scratchpad's strict checker (unique keys): 10 jobs, names and `needs`
  as above.
- **Release gates checked locally (T045)**: reading `ci.yml` back, all four publish jobs need the six
  build and test jobs and run only on `refs/tags/v`; the tag check passes for `v1.4.0` (the version at
  the time) and fails for `v9.9.9`; the `Cargo.toml` check passes for the file's version and fails for
  `9.9.9`; `https://crates.io/api/v1/crates/persian-text-guard/9.9.9` answers **404** today, and so does
  the crate itself, so the API check takes its "no previous release" path.
- **Baselines moved to 1.4.0 (T044)**: `PackageValidationBaselineVersion` 1.3.0 → 1.4.0 and
  `dotnet pack` succeeded; `npm run api:compat` reported "API compatible with v1.4.0: 45 declarations
  kept, 0 added"; `uv run python scripts/check_api.py` found `v1.4.0` and griffe ran for real:
  "ok: no breaking change against v1.4.0".
- **Version 1.5.0 (T046)**: `VERSION` 1.4.0 → 1.5.0, then `rust/scripts/set-version.sh` rewrote
  `rust/Cargo.toml` and both lock files. All four packages rebuilt:
  `persian-text-guard-1.5.0.crate` (85,563 bytes, all package checks pass, consumer prints `ok`),
  `persian_text_guard-1.5.0-py3-none-any.whl` and `.tar.gz`, `persian-text-guard-1.5.0.tgz`, and
  `PersianTextGuard.1.5.0.nupkg` with package validation against 1.4.0.
- **The `crates-io` environment (T047)** was created with `gh api`, exactly as `pypi`: custom
  deployment branch policy with one rule, `v*` of type `tag`, and no secrets
  (`deployment-branch-policies` → `{"name": "v*", "type": "tag"}`, `secrets.total_count` 0). The
  repository now has `crates-io`, `npm`, `nuget` and `pypi`.

## US4: documentation, benchmarks and API compatibility (T048–T056)

- **Benchmarks (T048, T050)**: `rust/bench/` is a separate, unpublished package with the same ten
  operations and message constants as the .NET, JavaScript and Python benchmarks (copied verbatim from
  `js/bench/filter.bench.ts`), plus `src/bin/bench_table.rs` and `scripts/bench-gate.sh`.
  Measured with `cargo bench` on the maintainer's machine (confirmed `Intel(R) Core(TM) i7-9700K CPU @
  3.60GHz` with `Get-CimInstance Win32_Processor`), rustc 1.98.1, release:

  | Operation | Mean | Operations/s |
  | --- | ---: | ---: |
  | Short clean message (5 words) | 3.8 µs | 262,929 |
  | Long clean message (60 words) | 41.4 µs | 24,145 |
  | Message with evasions | 3.6 µs | 280,985 |
  | Normalize a long message | 11.7 µs | 85,746 |
  | Build a filter from the bundled list | 1.4 ms | 734 |
  | `find_matches`, clean short message | 3.8 µs | 266,191 |
  | `find_matches`, message with three banned words | 9.3 µs | 107,110 |
  | `censor`, short message with one banned word | 5.5 µs | 182,664 |
  | `censor`, 60-word message with three banned words | 168 µs | 5,957 |
  | A 132,000-character message | 7.5 ms | 134 |

  **SC-005 is met with room to spare**: build 1.4 ms (target under 5 ms), short clean message 3.8 µs
  (under 5 µs), 132,000-character message 7.5 ms (under 50 ms). No optimisation pass was needed.
  `scripts/bench-gate.sh` passes at the CI limit (50 µs) and at SC-005's own limit
  (`scripts/bench-gate.sh 5`: "mean 3.9 µs (limit 5 µs) ok").

  **Deviation from research R9**: the package pins **criterion 0.7**, not 0.8. Criterion 0.8 depends on
  `alloca`, which compiles C, and this machine has no C toolchain (the GNU Rust host ships a linker
  driver only), so 0.8 cannot run the benchmarks here at all. 0.7 has no C dependency, runs the same ten
  benchmarks and writes the same `estimates.json`. The package stays separate from the crate either way,
  as the contract's layout says.
- **README (T049, T051)**: `rust/README.md` is the crates.io and docs.rs front page: installation, quick
  start, what matched and why, byte positions and slicing, censoring and masks, categories, your own
  words, word-list files, options, normalizing and tokenizing, byte versions, matching on
  `#[non_exhaustive]` enums, threads, the errors table, the four-language name table, the performance
  table, limitations, links, and a Persian section whose paragraphs are each in their own
  `<div dir="rtl">`. **14 `rust` blocks**, every one a doc test. `cargo test --locked --doc`: **48 passed**
  (the 14 README blocks and 34 public-item examples) on stable and on 1.85.
- **Documentation (T053)**: `RUSTDOCFLAGS="-D warnings -D missing_docs" cargo doc --locked --no-deps`
  passes. The generated pages for `ProfanityFilter::censor_with`, `find_matches_bytes` and
  `WordList::load_reader` explain the edge cases, not just the signatures: the mask rules and that the
  mask is "checked before the text"; that invalid sequences "are read as U+FFFD", that a region "covers
  the whole sequence" and "never ends inside a valid character"; and the byte-order mark, `NotFound`,
  `InvalidUtf8` and `valid_up_to`.
- **API check (T052)**: `rust/scripts/check-api.sh` asks crates.io and prints "baseline: no previous
  release (crates.io has no persian-text-guard yet)" today; CI does the same and only then runs
  `cargo-semver-checks`. **Proof**, on two throwaway commits reverted afterwards with `git reset HEAD~1`
  and `git checkout`:
  - changing `censor` to take `String` is **not** caught — cargo-semver-checks 0.50 has no lint for a
    changed parameter type (223 checks, 223 pass). Worth knowing: the check is not a complete guard.
  - removing `ProfanityFilter::censor_bytes_with` **is** caught, and named:
    "failure inherent_method_missing: pub method removed or renamed … Failed in:
    ProfanityFilter::censor_bytes_with", "Summary semver requires new major version: 1 major and 0 minor
    checks failed", exit code 100.

  `cargo-semver-checks` cannot be built on this machine (its `ring` dependency needs a C compiler), so
  the proof used the official prebuilt release binary, v0.50.0, the same artifact the CI action uses.
- **Root README (T054)**: crates.io is listed beside NuGet, npm and PyPI with `cargo add
  persian-text-guard` and a Rust example; the layout shows `rust/`; "Development" gains the Rust command
  table; "Releasing" covers crates.io, the `crates-io` environment and the first-release token;
  "Limitations" gains the Rust Unicode note; and a "Changes in 1.5.0" section was added. The root
  README's Python example is still tested: `uv run pytest tests/test_readme.py` — 17 passed.
- **Release notes (T055)**: `specs/005-rust-port/release-notes-1.5.0.md`, English with a Persian summary
  in `<div dir="rtl">` blocks.
- **T056**: `cargo fmt --check`, `cargo clippy --locked --all-targets -- -D warnings`, `cargo doc` with
  warnings denied, `cargo test --locked` (8 test binaries green) on stable and 1.85, the package checks,
  the API check, the benchmark gate, and `uv run pytest tests/test_readme.py` in `python/` — all pass.
