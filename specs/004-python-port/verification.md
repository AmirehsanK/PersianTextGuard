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

## Corpus: characters outside the Basic Multilingual Plane (T009–T010)

Ten pending cases added: eight in `robustness.json`, and one `ordinary` case each in
`matching-persian.json` and `matching-english.json`.

- `dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill`: "Filled 10 case(s); 0 disagreement(s)".
- Recorded results (positions in code points), against research R2's UTF-16 table:

  | Id | Match (entry, start, length, evasion) | Censored | R2 (UTF-16) |
  | --- | --- | --- | --- |
  | `robustness-supplementary-letter-after-word` | `kir`, 0, 4, lookalikeCharacters | `****` | 0, 5 ✓ |
  | `robustness-supplementary-letter-before-word` | `kir`, 0, 4, lookalikeCharacters | `****` | 0, 5 ✓ |
  | `robustness-supplementary-letter-separate-word` | `kir`, 0, 3, none | `**** 𠀀` | 0, 3 ✓ |
  | `robustness-supplementary-letter-inside-word` | `kir`, 0, 4, lookalikeCharacters | `****` | 0, 5 ✓ |
  | `robustness-emoji-after-persian-word` | `کیر`, 0, 3, none | `****😀` | 0, 3 ✓ |
  | `robustness-mathematical-bold-letters` | `kir`, 0, 3, none | `****` | 0, 6 ✓ |
  | `robustness-deseret-letter-inside-word` | `fuck`, 0, 5, lookalikeCharacters | `****` | 0, 6 ✓ |
  | `robustness-supplementary-letter-inside-persian-word` (new) | `کیر`, 0, 4, lookalikeCharacters | `****` | — |
  | `fa-ordinary-with-supplementary-characters` | no match | unchanged | — |
  | `en-ordinary-with-supplementary-characters` | no match | unchanged | — |

  Every one of the first seven equals R2; both ordinary cases stay unflagged (Principle I).
- `dotnet test dotnet/tests/PersianTextGuard.Conformance`: 532 passed on each of `net10.0`, `net8.0`
  and `net48` (523 cases plus 9 guards).
- `js/`: `npm run corpus`: 530 passed (523 cases plus 7 guards). The JavaScript port passes every new
  case unchanged.
- `CorpusFill -- --check`: "Filled 0 case(s); 0 disagreement(s)".

## Shared layers (T011–T020)

- `_types.py`, `_unicode.py`, `_utf16.py`, `_normalizer.py`, `_word_list.py`, `_fold.py` and
  `_source_map.py`, ported from `js/src/`.
- `WHITE_SPACE` is written out (25 characters) rather than computed at import; the Zs, Zl and Zp sets
  are identical on CPython 3.11, 3.12, 3.13, 3.14 and 3.14t (checked on each), and
  `test_white_space_set_matches_the_categories_on_this_python` re-checks it on every run.
- Without a source map, the normalizer uses whole-string passes (research R3): the per-unit steps are
  one `str.translate` through a table filled in lazily per unit, and the collapsing steps are regular
  expressions. With a map it runs the ported per-unit loop. The source-map tests compare the two.
- `WordList.load` re-raises a `UnicodeDecodeError` with the file name added to its `reason`, so the
  error names the file (spec FR-013) and is still a `UnicodeDecodeError`.
- **ruff**: `PLR2004` (magic values) and `RUF001`–`RUF003` (ambiguous characters) are ignored for the
  whole project, with a comment in `pyproject.toml`: a line-by-line port compares against code points
  that are the specification, and its docstrings are Persian.
- `uv run pytest tests/test_unicode.py tests/test_internals.py`: 96 passed.
- Lint command: ruff clean, 16 files formatted, the Unicode check clean over 7 package files, mypy
  "no issues found in 16 source files".

## US1: the filter and the package (T021–T032)

- `_scan.py`, `_regions.py` and `_filter.py` ported from `js/src/`; `__init__.py` exports exactly the
  names in contracts/public-api.md, plus `__version__`.
- `uv run pytest tests/test_api.py tests/test_word_list_load.py`: 79 passed. The whole suite at this
  point: 175 passed.
- **Build** (`uv build`, `VERSION` 1.3.0): `persian_text_guard-1.3.0-py3-none-any.whl` and
  `persian_text_guard-1.3.0.tar.gz`.
  - **Wheel**: `persian_text_guard/` with `__init__.py`, `_filter.py`, `_fold.py`, `_normalizer.py`,
    `_regions.py`, `_scan.py`, `_source_map.py`, `_types.py`, `_unicode.py`, `_utf16.py`,
    `_version.py`, `_word_list.py`, `_wordlists.py` and `py.typed`; and
    `persian_text_guard-1.3.0.dist-info/` with `METADATA`, `WHEEL`, `RECORD`, `licenses/LICENSE` and
    `licenses/THIRD-PARTY-NOTICES.md`.
  - **Sdist**: the same package files under `src/persian_text_guard/`, plus `pyproject.toml`,
    `hatch_build.py`, `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md` and `PKG-INFO`.
  - hatchling adds the nearest `.gitignore` (here the repository's own) to every sdist, whatever
    `only-include` says. The build hook removes it from the sdist's `force_include`, so the sdist equals
    the allowlist.
- `scripts/check_package.py`: every check passed (`twine check --strict` on both files,
  `check-wheel-contents`, both allowlists exact, metadata, no `Requires-Dist`); unpacked wheel
  135,321 bytes (SC-004: under 1 MB).
- `scripts/check_consumers.py`: 4 of 4 checks passed:
  - wheel smoke: `ok`;
  - sdist smoke (a wheel built from the sdist alone with `uv build --wheel`): `ok`;
  - typed usage: 0 errors from `mypy --strict` and from `pyright` (strict via `# pyright: strict`,
    because the pyright CLI has no `--strict` flag);
  - type errors: both checkers report exactly lines 7 and 8, the two marked lines.
- ruff also ignores `PLR0913` and `PLR0917` (too many arguments), because the port keeps the other
  ports' function signatures.
