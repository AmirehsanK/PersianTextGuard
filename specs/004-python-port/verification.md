# Verification: Python Port Published to PyPI

**Feature**: [spec.md](spec.md) | **Tasks**: [tasks.md](tasks.md) | **Quickstart**: [quickstart.md](quickstart.md)

Results recorded while implementing the tasks, on the maintainer's machine (Windows 11, i7-9700K)
unless noted.

## Baseline (T001)

- **Branch**: `004-python-port`; `git status --short` shows only the untracked `graphify-out/`.
- **Commit**: `a33a8ab31f52069d0d6062fb9fc8a42fdec7dc39`.
- `dotnet test dotnet/tests/PersianTextGuard.Tests`: 1,029 passed on each of `net10.0`, `net8.0` and
  `net48`.
- `dotnet test dotnet/tests/PersianTextGuard.Conformance`: 522 passed on each of `net10.0`, `net8.0`
  and `net48`.
- `js/`: `npm ci`, then `npm run test:all`: 665 passed (5 files).
- `uv --version`: `uv 0.12.11 (4b53f66b7 2026-09-08 x86_64-pc-windows-msvc)`.
- `uv python list --only-installed`: CPython 3.11.16, 3.12.14, 3.13.15, 3.14.6 and 3.14.7
  free-threaded.
- **Environment note**: on this machine uv's default cache fails with "cannot move the file to a
  different disk drive" (os error 17) even with `TMP` and `TEMP` on `C:`. Setting `UV_CACHE_DIR` to a
  fresh folder (`C:\Users\amire\AppData\Local\Temp\claude\uvcache`) avoids it; every uv command below
  ran with it.

## Scaffold (T002–T008)

- `python/pyproject.toml` and `python/hatch_build.py` as in T003 and T004. Two deviations, both
  found while locking:
  - **`package` dependency group.** `readme-renderer[md]` needs `comrak`, which has wheels for every
    CI interpreter on Linux but, on Windows, only for standard CPython 3.14. The package-check tools
    (`build`, `twine`, `check-wheel-contents`, `readme-renderer[md]`) are therefore a separate
    `package` group, synced with `uv sync --locked --group package` where the package checks run (the
    3.14 job). `dev` holds everything else, so `uv run --python 3.11 pytest` works on every platform.
  - **`python/.python-version`** is `3.14+gil`. Without it uv picked the free-threaded 3.14.7 as the
    default interpreter, because it is newer than 3.14.6. CI's `python-version` input overrides it.
- The word lists are `wordlists/persian.txt`, `finglish.txt` and `english.txt` (T004 calls them
  `fa`, `finglish` and `en`); the hook embeds them in that order, the .NET embedding order.
- `uv lock` and `uv sync --locked`: `.venv` on CPython 3.14.6; `_wordlists.py` and `_version.py`
  generated and git-ignored (`git status --short --ignored python` lists them under `!!`);
  `uv run python -c "import persian_text_guard as p; print(p.__version__)"` prints `1.3.0`.
- `scripts/check_unicode_usage.py` self-check: a throwaway `_x.py` with `"A".lower()`, `" a ".strip()`
  and `re.compile(r"\s")` gave `_x.py:3: .lower()`, `_x.py:4: .strip() with no separator` and
  `_x.py:5: re.compile with a Unicode class`; the file was then deleted.

**Lint command** (run in `python/`):

```bash
uv run ruff check && uv run ruff format --check && uv run python scripts/check_unicode_usage.py && uv run mypy
```

On the skeleton: ruff "All checks passed", 7 files formatted, the Unicode check clean, mypy "no
issues found in 7 source files".
