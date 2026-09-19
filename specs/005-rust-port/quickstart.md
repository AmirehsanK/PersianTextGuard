# Quickstart: Validating the Rust Port

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Contracts**: [contracts/](contracts/)

Runnable checks that prove the feature end to end. Commands run from the repository root unless noted,
in Git Bash on the maintainer's Windows machine; they work the same on Linux and macOS.

## 0. Prerequisites

- Rust through rustup: stable and `1.85` (`rustup toolchain install stable 1.85`). On a Windows machine
  without the MSVC build tools, use the GNU host (`x86_64-pc-windows-gnu`), as research R15 does.
- `cargo install cargo-semver-checks --locked` (needs stable Rust 1.93 or later).
- .NET 10 SDK, for regenerating the Unicode tables and for the other ports; Node.js 24 and uv for §3.

## 1. Build and test everything (FR-016, FR-019, SC-001, SC-006)

```bash
cd rust
cargo fmt --check
cargo clippy --locked --all-targets -- -D warnings
RUSTDOCFLAGS="-D warnings" cargo doc --locked --no-deps
cargo test --locked
cargo +1.85 test --locked
```

**Expected**: all clean. `cargo test` runs the unit tests, the integration tests, one corpus trial per case
(523 cases plus the guards, 0 not applicable), the thread and proptest tests, and every doc test, README
examples included. The same on 1.85.

## 2. The Unicode tables are exactly .NET's (FR-004)

```bash
cd rust
dotnet run tools/gen_tables.cs -- src/tables.rs
git diff --exit-code src/tables.rs
```

**Expected**: no difference. Research R1's dump comparison (§8) can be re-run to list the remaining NFKC
and NFD differences, all on characters added in Unicode 16 or 17.

## 3. Same entries as the other ports (FR-018, SC-002)

Print, from each port, the total, the default count and the per-category counts, and dump every entry as
`text\tmode\tcategory`; the Rust dump comes from a small test binary in `rust/consumer` (or a
`--ignored` test). Diff the Rust dump with `artifacts/compare/python.txt` and `js.txt` from 004.

**Expected**: 1,250 in all, 1,025 in the default selection, identical per-category counts, and 0 differences.

## 4. The crate as users get it (FR-001, FR-006, SC-003, SC-004)

```bash
cd rust
scripts/prepare-package.sh
cargo package --locked
scripts/check-package.sh
cd consumer && cargo run --locked
```

**Expected**: `target/package/persian-text-guard-<v>.crate` exists, under 1 MB, with exactly the allowlisted
files, and builds from the unpacked crate alone; the consumer, which depends on the packaged crate (not the
source tree), prints the quick-start results. For SC-003, follow the README quick start by hand in a new
`cargo new` project with a path dependency on the unpacked crate: under 5 minutes from an empty folder to a
flagged message.

## 5. API compatibility detects a break (FR-022)

```bash
cd rust
cargo semver-checks
```

**Expected** before 1.5.0: the CI step reports "baseline: no previous release". Then, on a throwaway commit,
change a public signature (for example make `censor` take `String`), and run
`cargo semver-checks --baseline-rev HEAD~1`: it fails and names the change. Remove the commit with
`git reset HEAD~1` and `git checkout -- rust/src` (not `--hard`, which would discard uncommitted work).

## 6. Performance (SC-005)

```bash
cd rust/bench
cargo bench
cargo run --release --bin bench_table
```

**Expected**, on the i7-9700K: building under 5 ms, `CleanShortMessage` under 5 µs, `VeryLongMessage` under
50 ms. The table goes into `rust/README.md`. CI's gate is 50 µs for `CleanShortMessage` (research R9).

## 7. Release checks (SC-009, SC-011)

**Before merge**: the dry run (research R14), as in 004:

| Run | Tag | Rust jobs | Publish jobs |
| --- | --- | --- | --- |
| 1 | `v1.5.0-dev.1` | fail (forced) | all four skipped |
| 2 | `v1.5.0-dev.2` | pass | all four succeed with stubbed steps; `cargo publish --dry-run` packages `persian-text-guard 1.5.0-dev.2` |
| 3 | `v1.5.0-dev.3` on the run-2 commit | pass | all four fail at "Check tag matches VERSION" |

Then delete every dry-run ref. No registry may show 1.5.0 or a dev version.

**After the release**, in fresh folders:

```bash
cargo new t && cd t && cargo add persian-text-guard@1.5.0 && cargo run
pip install persian-text-guard==1.5.0
npm view persian-text-guard@1.5.0 version
curl -s https://api.nuget.org/v3-flatcontainer/persiantextguard/index.json
curl -s -o /dev/null -w '%{http_code}' https://docs.rs/persian-text-guard/1.5.0/persian_text_guard/
```

(`src/main.rs` in `t` is the README quick start.) **Expected**: each package flags `ک.ی.ر`; docs.rs returns
200 with the crate page.

## 8. Re-running the Unicode comparison (research R1)

The Rust dump (`std` lower-casing, `unicode-normalization` NFKC and NFD) uses the format of
`specs/004-python-port/tools/dump.py`; compare it with the .NET dump from 004's `dump.cs`.

**Expected**: the counts in research R1, or a new difference to add to the corpus first (spec Edge Cases).
