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

## US2: the corpus, every Python, threads (T033–T041)

- `tests/corpus/` (`load.py`, `values.py`, `evaluate.py`), `tests/test_corpus.py` and
  `tests/test_corpus_guards.py`, ported from `js/test/`. pytest's `pythonpath = ["tests"]` makes the
  runner importable as `corpus`. `build_input` joins `utf16` parts as UTF-16 does: a high surrogate
  followed by a low one is one code point, any other surrogate stays a lone one.
- **T037**: `uv run pytest -m corpus` on CPython 3.14.6 (Unicode 16.0.0): **532 passed** (523 cases
  plus 9 guards) in 5.0 s, on the first run, with no fix to the port needed. `test_api.py` gained the
  P4 check over all 470+ matching corpus inputs.
- **T038**, the whole suite on every supported Python (each in its own environment through
  `UV_PROJECT_ENVIRONMENT`):

  | Interpreter | `sys.version` | `unidata_version` | GIL | Result |
  | --- | --- | --- | --- | --- |
  | `--python 3.11` | 3.11.16 | 14.0.0 | on | 708 passed |
  | `--python 3.12` | 3.12.14 | 15.0.0 | on | 708 passed |
  | `--python 3.13` | 3.13.15 | 15.1.0 | on | 708 passed |
  | `--python 3.14+gil` | 3.14.6 | 16.0.0 | on | 708 passed |
  | `PYTHON_GIL=0 --python 3.14t` | 3.14.7 free-threaded | 16.0.0 | off | 708 passed |

  On this machine `uv run --python 3.14` picks the free-threaded 3.14.7, because it is newer than
  3.14.6; `3.14+gil` asks for the standard build. No Unicode-data difference showed up on any version.
- **T039**, `tests/test_threads.py` (8 threads behind a `threading.Barrier`, every matching corpus input
  twice each, compared with a single-threaded run; and 8 threads calling `WordList.all()` and
  `persian_default()` first in a fresh interpreter):
  - 3.14.6: 2 passed, 1 skipped (the GIL assertion, which only applies to 3.14t with `PYTHON_GIL=0`);
  - 3.14.7t with `PYTHON_GIL=0`: 3 passed, and `sys._is_gil_enabled()` is `False`;
  - `PYTHON_GIL=0 uv run --python 3.14t pytest -p pytest_run_parallel --parallel-threads=8 -m "not corpus and not threads"`:
    176 passed, each test run on 8 threads at once; no test needed a `thread_unsafe` mark.
- **T040**, failure reporting on scratch edits, reverted with `git checkout -- conformance`:
  1. `fa-emoji-before-word`'s `censored` set to `"😀 ####"`, and
     `matching-persian-ordinary-messages-pass-001`'s `containsProfanity` set to `true`: exactly those 2
     failed in one run (2 failed, 530 passed):

     ```text
     Case 'fa-emoji-before-word' in matching-persian.json: 1 field(s) differ
       input "😀 کیر"
       expected.censored: expected "😀 ####" actual "😀 ****"
     Case 'matching-persian-ordinary-messages-pass-001' in matching-persian.json: breaks its kind rule
       input "هر کس پلات بالاست پیام بده"
       ordinary requires containsProfanity to be false
     ```
  2. `conformance/` renamed to `conformance.off`: collection failed with
     `CorpusError: Conformance corpus not found: D:\Git\PersianTextGuard\conformance`. Renamed back;
     `git status` clean for `conformance/`.
- **T041**, bundled selections against the built JavaScript package (quickstart §3): both one-liners print
  `{"all": 1250, "default": 1025, "by": {"uncategorized": 0, "profanity": 93, "sexual": 353,
  "insult": 400, "slur": 146, "harassment": 33, "mild": 225}}` (identical). Every entry dumped as
  `text\tmode\tcategory` to `artifacts/compare/python.txt` and `js.txt`: 1,250 lines each, and
  `git diff --no-index` shows **0 differences**.

## Benchmarks and API check (T048, T052)

- `bench/bench_filter.py` (pyperf, the ten JavaScript benchmarks with the same names and messages;
  `--only NAME` runs one), `scripts/bench_table.py` and `scripts/bench_gate.py`. pyperf ships no type
  information, so mypy ignores its missing imports.
- `uv run python scripts/bench_gate.py` on this machine: `ok CleanShortMessage: mean 91.8 µs, limit 500 µs`.
- `scripts/check_api.py` runs griffe from the repository root with `--search python/src`: griffe
  checks the old reference out in a worktree of the repository and resolves the search path there too,
  so `--search src` from `python/` finds nothing in the old tree.
  - `uv run python scripts/check_api.py`: "baseline: no previous release has python/, so there is
    nothing to compare with" (exit 0).
  - **Proof** (quickstart §5): a throwaway commit renamed `censor`'s `mask` parameter to `character`,
    then `check_api.py --against HEAD~1` exited 1:

    ```text
    python\src\persian_text_guard\_filter.py:283: ProfanityFilter.censor(mask):
    Parameter was removed

    griffe check persian_text_guard against HEAD~1
    FAIL: griffe reported breaking changes against HEAD~1, or could not run,
    and VERSION has the same major version
    ```

    The throwaway commit was then removed with `git reset HEAD~1` and
    `git checkout -- python/src/persian_text_guard/_filter.py` rather than `git reset --hard HEAD~1`,
    which would also have discarded the uncommitted work in the tree.

## US3: CI and publishing (T042–T046)

- **`.github/workflows/ci.yml`**: job `python`, `Python (${{ matrix.python }})` for 3.11, 3.12, 3.13,
  3.14 and 3.14t, with `astral-sh/setup-uv@v10` (the current major, v10.1.0), and job
  `publish-pypi`, `Publish to PyPI`. Existing job names are unchanged. Deviations from T042/T043:
  - the 3.14 entry asks uv for `3.14+gil`, so it can never resolve to the free-threaded build, as it did
    on this machine;
  - `PYTHON_GIL: "0"` is set on the two 3.14t test steps only, not job-wide, so the standard builds
    never see the variable;
  - the 3.14 job syncs `--group package` (see "Scaffold");
  - "Check file versions" gets `packaging` through `uv run --no-project --with packaging`, because the
    runner's system pip refuses to install into the system Python (PEP 668).
- **YAML**: parsed with the `yaml` package (strict, unique keys) in the scratchpad's
  `yamlcheck/check.mjs`. The first parse caught `": "` inside a plain `run:` scalar (003's lesson), fixed
  with a block scalar.
- **T045 gates**, read back from the parsed workflow:

  | Job | Name | needs | if |
  | --- | --- | --- | --- |
  | `publish` | Publish to NuGet | build, netfx, javascript, python | `startsWith(github.ref, 'refs/tags/v')` |
  | `publish-npm` | Publish to npm | build, netfx, javascript, python | `startsWith(github.ref, 'refs/tags/v')` |
  | `publish-pypi` | Publish to PyPI | build, netfx, javascript, python | `startsWith(github.ref, 'refs/tags/v')` |

  - the tag check, extracted from `ci.yml` and run in Git Bash: `v1.3.0` (then `VERSION`) passes;
    `v9.9.9` fails with "Tag v9.9.9 does not match VERSION 1.3.0";
  - "Check file versions", extracted and run against the local build: passes for `VERSION` 1.3.0, and
    fails with the expected names when `VERSION` says 9.9.9;
  - `Version('1.4.0-dev.2')` prints `1.4.0.dev2`.
- **T044**: `PackageValidationBaselineVersion` 1.2.0 → 1.3.0; `dotnet pack` succeeds, so package
  validation passes against 1.3.0. `npm run api:compat`: "API compatible with v1.3.0: 45 declarations
  kept, 0 added".
- **T046**: `VERSION` 1.4.0. `uv build` gives `persian_text_guard-1.4.0-py3-none-any.whl` and
  `persian_text_guard-1.4.0.tar.gz` (`__version__` 1.4.0); `npm run pack` gives
  `persian-text-guard-1.4.0.tgz`; `dotnet pack` gives `PersianTextGuard.1.4.0.nupkg` with package
  validation passing against 1.3.0.
