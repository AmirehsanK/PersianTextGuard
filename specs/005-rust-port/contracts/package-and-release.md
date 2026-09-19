# Contract: Layout, Commands, CI and Release for the Rust Port

**Feature**: [../spec.md](../spec.md) | **Research**: [../research.md](../research.md) R5, R8–R14 | **Extends**: [004 package-and-release contract](../../004-python-port/contracts/package-and-release.md)

## Layout

New or changed paths only.

```text
/
├── VERSION                                  # 1.4.0 → 1.5.0
├── conformance/README.md                    # runner rules: the two readings for Rust (corpus-runner-amendment.md)
├── specs/002-monorepo-conformance-corpus/contracts/corpus-format.md   # the same amendment
├── dotnet/src/PersianTextGuard/PersianTextGuard.csproj   # PackageValidationBaselineVersion 1.3.0 → 1.4.0
├── rust/
│   ├── Cargo.toml                           # persian-text-guard; version = VERSION; rust-version 1.85; edition 2024
│   ├── Cargo.lock                           # committed; resolved for Rust 1.85
│   ├── build.rs                             # word lists into OUT_DIR; fails when VERSION ≠ CARGO_PKG_VERSION
│   ├── README.md                            # crates.io and docs.rs front page; English + Persian; doc-tested
│   ├── src/
│   │   ├── lib.rs                           # public items; #![doc = include_str!("../README.md")]
│   │   ├── types.rs  errors.rs              # enums, sets, BannedWord, options, match, errors
│   │   ├── unicode.rs  tables.rs            # .NET-equivalent primitives; generated tables (committed)
│   │   ├── utf16.rs  bytes.rs               # UTF-16 view and positions; the byte versions (FR-008a)
│   │   ├── normalizer.rs  source_map.rs  fold.rs
│   │   ├── filter.rs  scan.rs  regions.rs
│   │   └── word_list.rs
│   ├── tests/
│   │   ├── corpus.rs  corpus/               # libtest-mimic runner (harness = false): load, values, evaluate
│   │   ├── api.rs  bytes.rs  word_list_load.rs  threads.rs  no_panic.rs
│   ├── bench/                               # persian-text-guard-bench, publish = false, Criterion
│   │   ├── Cargo.toml  Cargo.lock  benches/filter.rs  src/bin/bench_table.rs
│   ├── consumer/                            # a tiny binary crate using the packaged crate (quickstart §4)
│   ├── tools/gen_tables.cs                  # .NET 10 generator of src/tables.rs
│   ├── scripts/prepare-package.sh  set-version.sh  check-package.sh
│   └── wordlists/  LICENSE  THIRD-PARTY-NOTICES.md   # copied by prepare-package.sh; git-ignored
├── README.md                                # crates.io next to NuGet, npm and PyPI; Development and Releasing gain rust/
├── .gitignore                               # rust/target/, rust/bench/target/, rust/wordlists/, rust/LICENSE, …
└── .github/workflows/ci.yml                 # Rust jobs, Publish to crates.io; needs updated
```

## Commands

Run from `rust/`.

| Purpose | Command | Result |
| --- | --- | --- |
| Test everything | `cargo test --locked` | unit, integration, corpus (one trial per case), threads, proptest, doc tests (README included) |
| Corpus only | `cargo test --locked --test corpus` | one line per case id; filter with `-- <id>` |
| Minimum Rust | `cargo +1.85 test --locked` | the same suite on 1.85 |
| Lint and format | `cargo fmt --check` and `cargo clippy --locked --all-targets -- -D warnings` | clean |
| Documentation | `RUSTDOCFLAGS="-D warnings" cargo doc --locked --no-deps` | every public item documented |
| Regenerate tables | `dotnet run tools/gen_tables.cs -- src/tables.rs` | `git diff --exit-code src/tables.rs` is clean |
| Package | `scripts/prepare-package.sh && cargo package --locked` then `scripts/check-package.sh` | `target/package/persian-text-guard-<v>.crate`, file list equal to the allowlist, builds on its own, under 1 MB |
| API check | `cargo semver-checks` | against the newest crates.io version; "baseline: no previous release" before 1.5.0 |
| Benchmarks | `cd bench && cargo bench`, then `cargo run --release --bin bench_table` | Criterion results; the README table |
| Set the version | `scripts/set-version.sh` | `Cargo.toml` version = `VERSION` |

## Package contents

`scripts/check-package.sh` compares `cargo package --list` with this list exactly:

```text
Cargo.toml  Cargo.toml.orig  Cargo.lock  .cargo_vcs_info.json  build.rs  README.md  LICENSE
THIRD-PARTY-NOTICES.md  wordlists/persian.txt  wordlists/finglish.txt  wordlists/english.txt
src/lib.rs  src/types.rs  src/errors.rs  src/unicode.rs  src/tables.rs  src/utf16.rs  src/bytes.rs
src/normalizer.rs  src/source_map.rs  src/fold.rs  src/filter.rs  src/scan.rs  src/regions.rs
src/word_list.rs
```

(`.cargo_vcs_info.json` appears only when packaging from a clean Git tree, as CI does.) The manifest has:
`name = "persian-text-guard"`, `version` = `VERSION`, `edition = "2024"`, `rust-version = "1.85"`,
`license = "MIT"`, `description`, `repository`, `homepage` (the `rust/` README), `documentation` (docs.rs),
`readme = "README.md"`, `keywords` (five: persian, farsi, profanity, moderation, normalization),
`categories = ["text-processing"]`, and one `[dependencies]` entry, `unicode-normalization`.

## CI

The existing job names MUST NOT change. The Rust rows are added.

| Job | Runs on | Steps | Required on `main` |
| --- | --- | --- | --- |
| existing nine | as today | as today | yes |
| `Rust (stable, ubuntu-latest)`, `Rust (stable, windows-latest)`, `Rust (stable, macos-latest)` | matrix | `cargo test --locked` | yes (added after merge) |
| `Rust (1.85, ubuntu-latest)`, `Rust (1.85, windows-latest)`, `Rust (1.85, macos-latest)` | matrix | `cargo test --locked` on 1.85 | yes (added after merge) |
| `Rust checks` | ubuntu, stable | fmt, clippy, docs, table regeneration with .NET 10, package and `check-package.sh`, build of the `.crate` on its own, semver check, benchmark gate (`CleanShortMessage` under 50 µs), upload `crate-package` | yes (added after merge) |
| `Publish to NuGet`, `Publish to npm`, `Publish to PyPI` | ubuntu | as today; `needs` gains `rust` and `rust-checks` | — |
| `Publish to crates.io` | ubuntu | on `v*` tags; `needs` every build and test job (`build`, `netfx`, `javascript`, `python`, `rust`, `rust-checks`); `environment: crates-io`; `id-token: write`; tag check; `Cargo.toml` version = `VERSION`; skip when crates.io has the version; `cargo publish --locked` with `CARGO_REGISTRY_TOKEN` from the environment when present, otherwise through `rust-lang/crates-io-auth-action` | — |

**Guarantees**: the 004 guarantees, for four registries: no publish job starts unless every build and test
job of every port succeeds; every publish job refuses a tag that differs from `v` + `VERSION`; re-running
a publish job after a partial release is safe.

## Release 1.5.0

Steps marked 👤 are account actions only the maintainer can perform.

1. 👤 On GitHub: **Settings → Environments → New environment** `crates-io`, deployments limited to tag `v*`,
   before any push that runs the new workflow (or Claude creates it with `gh api`, as for `pypi`).
2. Before merge, a real CI dry run on `v1.5.0-dev.*` tags from a scratch branch proves the gates, with every
   publish step stubbed (research R14). The tags and branch are deleted afterwards.
3. The pull request merges with the maintainer's go-ahead, `VERSION` = `1.5.0`, and CI green.
4. Add the seven Rust job names to `main`'s required status checks.
5. 👤 On crates.io (signed in with GitHub): **Account Settings → API Tokens → New Token**: name
   `persian-text-guard-first-release`, expiry 7 days, scope **publish-new** only, crate pattern
   `persian-text-guard`. 👤 Store it as the `crates-io` environment secret `CARGO_REGISTRY_TOKEN`.
6. Push tag `v1.5.0` on the merge commit after checking: `main` is green, `VERSION` is `1.5.0`, and no
   registry has 1.5.0. This is **irreversible**.
7. CI publishes `PersianTextGuard 1.5.0` to NuGet, and `persian-text-guard 1.5.0` to npm, PyPI and crates.io.
8. 👤 Revoke the token on crates.io; delete the environment secret (or Claude deletes it with `gh`). 👤 On the
   crate's crates.io settings, **Trusted Publishing → Add**: repository `AmirehsanK/PersianTextGuard`,
   workflow `ci.yml`, environment `crates-io`.
9. Verify from the public registries in fresh projects, and docs.rs (SC-011, quickstart §7).
10. Create the GitHub release `v1.5.0` with English notes and a Persian summary.
