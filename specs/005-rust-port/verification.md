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
