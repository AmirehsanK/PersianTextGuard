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

## US4: documentation, benchmarks and API compatibility (T049–T056)

- **T049**, `python/README.md`: English sections (installation with pip and uv, quick start, matches and
  evasions, positions in code points, censoring and masks, categories, your own words, word-list
  files with `load` and `parse`, options, normalization and tokenizing, thread safety, validating input
  with the error table, the .NET/JavaScript/Python name table, performance, limitations with the Unicode
  note and Python 3.11's Cyrillic modifier letters, links), and a Persian part (introduction,
  installation, quick start, censoring, input). Each Persian paragraph is its own `<div dir="rtl">`
  block, starts and ends with a Persian word, and keeps code in separate blocks with comments in both
  languages (research R16). `twine check --strict` passes on it. Every `python` block is complete and
  asserts its results.
- **T050**, benchmarks on the named machine: `Get-CimInstance Win32_Processor` reports
  "Intel(R) Core(TM) i7-9700K CPU @ 3.60GHz". `uv run --python 3.14+gil python bench/bench_filter.py -o .bench/results.json --rigorous`,
  then `scripts/bench_table.py` (CPython 3.14.6):

  | Operation | Mean | Operations/s |
  | --- | ---: | ---: |
  | Short clean message (5 words) | 91.4 µs | 10,937 |
  | Long clean message (60 words) | 909 µs | 1,100 |
  | Message with evasions | 50.4 µs | 19,850 |
  | Normalize a long message | 35.1 µs | 28,454 |
  | Build a filter from the bundled list | 13.3 ms | 75 |
  | `find_matches`, clean short message | 90.4 µs | 11,059 |
  | `find_matches`, message with three banned words | 240 µs | 4,169 |
  | `censor`, short message with one banned word | 118 µs | 8,461 |
  | `censor`, 60-word message with three banned words | 3.2 ms | 311 |
  | A 132,000-character message | 210 ms | 5 |

  **SC-005**: build 13.3 ms (target under 500 ms), `CleanShortMessage` 91.4 µs (under 250 µs), and
  `VeryLongMessage` 210 ms (under 3 s): all met, without profiling or optimising.
  `scripts/bench_gate.py --limit-us 250`: "ok CleanShortMessage: mean 90.3 µs, limit 250 µs". pyperf
  records no CPU model on Windows, so `bench_filter.py` now adds it from the registry (the rigorous run
  above predates that change; the CPU was confirmed with `Get-CimInstance`).
- **T051**, `tests/test_readme.py`: 13 blocks from `python/README.md` and 1 from the root `README.md`,
  one test each (ids `<file>-block-<n>-line-<line>`), plus the minimum-count guards (at least 8 and 1):
  17 passed after T054.
- **T053**: ruff's `D` rules pass; `inspect.getdoc` finds a docstring for all 19 names in `__all__`. The
  four `Literal` name aliases carry theirs at run time (a string after an assignment is not attached to
  the object), on 3.11 as well. `help(ProfanityFilter.censor)` and `help(WordList.load)` explain the
  mask rules, `None`, the file and BOM handling and every error, not only the signatures.
- **T054**, root `README.md`: PyPI next to NuGet and npm, with `pip install persian-text-guard` and a
  tested Python example; "Changes in 1.4.0"; `python/` and its commands under Development; PyPI trusted
  publishing and the `pypi` environment under Releasing; the Python Unicode note under Limitations.
- **T055**: `release-notes-1.4.0.md`, English with a Persian summary.
- **T056**, in `python/` with `uv sync --locked --group package`: lint command clean (36 files formatted,
  mypy "no issues found in 34 source files"); `uv run pytest`: **727 passed**, none skipped (the GIL
  assertion is now part of the thread test, so standard CPython has nothing to skip); `uv build` gives
  the 1.4.0 wheel and sdist; `check_package.py` passes (unpacked wheel 153,642 bytes);
  `check_consumers.py` 4 of 4; `check_api.py` "baseline: no previous release".

## Full matrix (T057)

From a clean state: `git clean -xdn python js/dist` listed only build outputs (`js/dist/`, and in
`python/` the caches, `.venv/`, `.bench/`, `dist/`, `consumers/.envs/`, the copied licence files and
the two generated modules); then `git clean -xdf python js/dist`. `graphify-out/` was not touched.

| Suite | Result |
| --- | --- |
| `dotnet test dotnet/tests/PersianTextGuard.Tests` | 1,029 passed on each of `net8.0`, `net10.0` and `net48` |
| `dotnet test dotnet/tests/PersianTextGuard.Conformance` | 532 passed on each of `net10.0`, `net8.0` and `net48` |
| `js/`: `npm ci && npm run test:all` | 675 passed, 5 files (665 at baseline plus the 10 new corpus cases) |
| `python/`: `uv sync --locked --group package` | generates `_wordlists.py` and `_version.py` |
| CPython 3.11.16 (Unicode 14.0.0) | 727 passed |
| CPython 3.12.14 (Unicode 15.0.0) | 727 passed |
| CPython 3.13.15 (Unicode 15.1.0) | 727 passed |
| CPython 3.14.6 (`3.14+gil`, Unicode 16.0.0) | 727 passed |
| CPython 3.14.7t, `PYTHON_GIL=0` (GIL off) | 727 passed |
| `uv build`; `check_package.py`; `check_consumers.py` | 1.4.0 wheel and sdist; all package checks passed (153,642 bytes unpacked); 4 of 4 consumer checks |

**Found and fixed here.** The first clean run failed collection on every interpreter ("5 errors"): after
`git clean` removed the generated modules, `uv sync` reused uv's cached editable build and did not
rerun the build hook, so `_wordlists.py` was missing until the next `uv build`. Deleting the two files
and running `uv sync` reproduced it. `pyproject.toml` now sets `[tool.uv] cache-keys` to the hook's
inputs (`pyproject.toml`, `hatch_build.py`, `../VERSION`, `../wordlists/*.txt`) and its two outputs,
so a changed `VERSION` or word list, or a missing generated module, rebuilds the editable install. The
same deletion then regenerated the files, and the table above is the run after the fix, from a clean
state again.

## Quickstart walkthrough (T058)

- **§1, set up and run everything once**: `uv sync --locked`, ruff, the Unicode-usage check, mypy and
  `uv run pytest` are clean, with 727 passed and nothing skipped: unit tests, 523 corpus cases plus
  9 guards, README examples and thread tests (T056, T057). ✓
- **§2, every supported Python, free-threaded included**: 727 passed on 3.11, 3.12, 3.13, 3.14 and 3.14t
  with `PYTHON_GIL=0`; `sys._is_gil_enabled()` is `False` there (T038, T039, T057). ✓
- **§3, same entries as the other ports**: identical JSON lines and 0 differences over 1,250 entries
  (T041). ✓
- **§4, the package as users install it**: both files in `dist/`, `check_package.py` and
  `check_consumers.py` pass (T032, T056, T057). **SC-003 by hand**: in an empty scratch folder,
  `py -3.14 -m venv t`, `t/Scripts/pip install python/dist/persian_text_guard-1.4.0-py3-none-any.whl`
  and the quick start printed `False True True`, 8 seconds from the empty folder. ✓
- **§5, API compatibility detects a break**: "baseline: no previous release", and the throwaway rename
  of `censor(mask)` failed with "Parameter was removed" (T052). ✓
- **§6, performance**: 13.3 ms, 91.4 µs and 210 ms against 500 ms, 250 µs and 3 s; the gate at 250 µs
  passes (T050). ✓
- **§7, release checks**: not done here; T059–T066 need the `pypi` environment and the PyPI pending
  publisher (T047), and the maintainer's go-ahead.
- **§8, re-running the Unicode measurement**: `dotnet run specs/004-python-port/tools/dump.cs` (.NET
  10.0.11), `dump.py` on 3.11.16, 3.12.14, 3.13.15, 3.14.6 and 3.14.7t, then `compare.py`: every count
  equals research R1 (category per unit 24, 22, 17, 0 and 0; per code point 10,302, 5,813, 5,186, 0
  and 0; .NET whitespace 0 everywhere; lower-casing 0, 0, 0, 5 and 5; NFKC 62 on 3.11 (U+1E030–U+1E06D)
  and 36 on 3.14 (U+1CCD6–U+1CCF9); NFD 20 on 3.14). `str.isspace` differs from .NET on
  U+001C–U+001F on every version. ✓

## First CI runs on the branch

Before any dry-run tag, CI was run on `004-python-port` with `workflow_dispatch` (publish jobs only run
on tags). It found two problems in the Python job, both fixed on the branch:

1. [Run 35433277522](https://github.com/AmirehsanK/PersianTextGuard/actions/runs/35433277522): every
   Python job failed with "Unable to resolve action `astral-sh/setup-uv@v10`". Since v8, setup-uv
   publishes exact version tags only, with no moving major tag. The workflow now pins the v10.1.0
   commit, `bec219d24cd3e171d82865faccec33120bb574f4`, with the version in a comment.
2. [Run 35433397141](https://github.com/AmirehsanK/PersianTextGuard/actions/runs/35433397141): `Python (3.14)`
   failed with "No interpreter found for Python 3.14+gil": uv on the runner resolves `+gil` only against
   installed interpreters, not downloads. The job now asks for plain `3.14`, which downloads the standard
   build on a fresh runner, and a step asserts `sys._is_gil_enabled()` so it can never test the
   free-threaded build by mistake.
3. [Run 35433517074](https://github.com/AmirehsanK/PersianTextGuard/actions/runs/35433517074): every job
   green. `Python (3.14)`: CPython 3.14.7 (Unicode 16.0.0, GIL on), 195 unit tests and 532 corpus tests,
   package and consumer checks passed, "baseline: no previous release", benchmark gate
   "CleanShortMessage: mean 76.7 µs, limit 500 µs". `Python (3.14t)`: 3.14.7 free-threaded with
   `PYTHON_GIL=0`, 727 passed, then 193 passed on 8 threads each.

## SC-009: release gates dry run (T059–T062)

- **Precondition**: the GitHub environment `pypi` was created with `gh api`, as `npm` is set up:
  custom deployment policies, exactly one rule, `v*` of type `tag`, no secrets.
  `gh api repos/AmirehsanK/PersianTextGuard/environments/pypi/deployment-branch-policies` lists
  `v* tag` (total 1).
- `004-python-port` pushed. Scratch branch `dryrun/release-gates`: NuGet login and push replaced by
  `ls -l artifacts/*.nupkg` and an echo, `--dry-run` added to `npm publish`, the PyPI upload replaced by
  `ls -l dist/`, and a `"DRY RUN: simulated failure"` step first in the Python job. The safety greps
  printed nothing, and the YAML parsed, before each push.

| Run | Tag | Result |
| --- | --- | --- |
| [1](https://github.com/AmirehsanK/PersianTextGuard/actions/runs/35433682039) | `v1.4.0-dev.1` | All five `Python (…)` jobs `failure`; `Build, test, pack`, .NET Framework and both JavaScript jobs `success`; `Publish to NuGet`, `Publish to npm` and `Publish to PyPI` all `skipped`. |
| [2](https://github.com/AmirehsanK/PersianTextGuard/actions/runs/35433837300) | `v1.4.0-dev.2` | Every job `success`. NuGet stub: `artifacts/PersianTextGuard.1.4.0-dev.2.nupkg`, "DRY RUN - would push to NuGet". npm: `+ persian-text-guard@1.4.0-dev.2`, tag `next`, 8 files, `(dry-run)`. PyPI: "Check file versions" printed `['persian_text_guard-1.4.0.dev2-py3-none-any.whl', 'persian_text_guard-1.4.0.dev2.tar.gz']` and passed; the stub listed both files. Deployments to `pypi`, `nuget` and `npm` for `v1.4.0-dev.2`: the `v*` rule admits release tags. |
| [3](https://github.com/AmirehsanK/PersianTextGuard/actions/runs/35433983874) | `v1.4.0-dev.3` on the run-2 commit | Every build and test job `success`; all three publish jobs `failure` at "Check tag matches VERSION": "Tag v1.4.0-dev.3 does not match VERSION 1.4.0-dev.2". |

**Cleanup (T062)**: the three tags deleted on `origin` and locally; `dryrun/release-gates` deleted on
`origin` and locally; `git ls-remote origin | grep -i -E "dev|dryrun"` prints nothing; npm versions
`["0.0.1","1.3.0"]`; NuGet index `1.0.0, 1.0.1, 1.1.0, 1.2.0, 1.3.0`; PyPI JSON `404`; on
`004-python-port`, `VERSION` is `1.4.0` and `ci.yml` has no `DRY RUN` or `--dry-run`; the `pypi`
environment still has exactly the `v*` tag rule.

## Pull request, merge and required checks (T063–T065)

- [PersianTextGuard#5](https://github.com/AmirehsanK/PersianTextGuard/pull/5), "Python port on PyPI,
  corpus cases for supplementary characters, version 1.4.0": all nine build and test jobs passed
  (.NET ×2, JavaScript ×2, Python ×5); the three publish jobs were skipped.
- Merged with the maintainer's go-ahead (`gh pr merge 5 --merge`), merge commit
  `ae92cb9d2966f481e21408f38f15f206827a1d43`; `main`'s CI on it
  ([run 35434275824](https://github.com/AmirehsanK/PersianTextGuard/actions/runs/35434275824)) green.
- `main`'s required status checks (`strict: false`, `app_id` 15368) are now the four existing checks
  plus `Python (3.11)`, `Python (3.12)`, `Python (3.13)`, `Python (3.14)` and `Python (3.14t)`: nine.

## Release 1.4.0 (T066–T068)

- **Before tagging**: `origin/main` at `ae92cb9` with green CI; `VERSION` 1.4.0; the `pypi` environment
  with its `v*` tag rule, and the PyPI pending publisher confirmed by the maintainer; npm
  `["0.0.1","1.3.0"]`, NuGet up to 1.3.0, PyPI 404; no `v1.4.0` tag on `origin`. **CPython 3.15**:
  `uv python list 3.15` shows only `3.15.0rc2`, not a final release, so the matrix stays at 3.11–3.14
  (constitution Principle III).
- `v1.4.0` pushed on `ae92cb9`.
  [Run 35434765981](https://github.com/AmirehsanK/PersianTextGuard/actions/runs/35434765981): every job
  succeeded, including `Publish to NuGet` ("Your package was pushed"), `Publish to npm`
  (`+ persian-text-guard@1.4.0`, tag `latest`, provenance signed) and `Publish to PyPI`.
- **Registries (SC-011)**, from fresh environments:
  - PyPI: `pip install persian-text-guard==1.4.0` in a new 3.14 venv; `__version__` 1.4.0, `ک.ی.ر`
    flagged, `censor("kir and motherfucker")` gives `**** and ****`, and `consumers/smoke.py` prints
    `ok`. The JSON API lists `persian_text_guard-1.4.0-py3-none-any.whl` and `persian_text_guard-1.4.0.tar.gz`;
    the description is the README (`text/markdown`, Persian section included); the wheel's provenance
    has a GitHub attestation for `AmirehsanK/PersianTextGuard`, workflow `ci.yml`, environment `pypi`.
  - npm: dist-tag `latest` is `1.4.0`; a fresh `npm install persian-text-guard@1.4.0` flags `ک.ی.ر`.
    It became visible a few minutes after the publish.
  - NuGet: the flat-container index lists 1.4.0 (about two minutes after the push); a new console app
    with `PersianTextGuard` 1.4.0 prints `nuget 1.4.0.0 True` for `ک.ی.ر`.
- GitHub release [`v1.4.0`](https://github.com/AmirehsanK/PersianTextGuard/releases/tag/v1.4.0),
  "PersianTextGuard 1.4.0", from `release-notes-1.4.0.md` (`--verify-tag`), not a draft or prerelease.
