# Quickstart: Validating the Python Port

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Contracts**: [contracts/](contracts/)

Runnable checks that prove the feature end to end. Commands run from the repository root unless noted.
They assume Windows with Git Bash, as on the maintainer's machine. The same commands work on Linux,
apart from the interpreter paths.

## 0. Prerequisites

- `uv` (0.12 or later), and the interpreters `uv python list --only-installed` shows: CPython 3.11,
  3.12, 3.13, 3.14 and 3.14t. `uv python install 3.11 3.12 3.13 3.14 3.14t` installs any that are
  missing. If uv fails with "cannot move the file to a different disk drive" (os error 17), set `TMP`
  and `TEMP` to a folder on the same drive as uv's cache.
- .NET 10 SDK and Node.js 24, for the parts that touch the other ports.

## 1. Set up and run everything once (FR-016, FR-017, FR-019)

```bash
cd python
uv sync --locked
uv run ruff check && uv run ruff format --check
uv run python scripts/check_unicode_usage.py
uv run mypy
uv run pytest
```

**Expected**: ruff, the Unicode-usage check and mypy (strict, over `src`, `tests`, `scripts`, `bench`
and the typed consumer, as `pyproject.toml` configures) are clean, and pytest passes every test:
- the unit tests;
- `test_corpus.py`, which has one test per corpus case, 513 plus the new cases from research R2;
- the corpus guards;
- the README examples;
- the thread tests.

No test is skipped or marked "not applicable" (SC-001).

## 2. Every supported Python, including free-threaded (FR-003, FR-017, SC-001, SC-008)

```bash
cd python
for v in 3.11 3.12 3.13 3.14; do uv run --python $v pytest -q; done
PYTHON_GIL=0 uv run --python 3.14t pytest -q
PYTHON_GIL=0 uv run --python 3.14t python -c "import sys; assert not sys._is_gil_enabled()"
```

**Expected**: every run passes, and the last command confirms that the GIL is off. The 3.14t run
includes `test_threads.py`: 8 threads times every corpus message, with results identical to a
single-threaded run.

## 3. Same entries as the other ports (FR-018, SC-002)

```bash
cd python
uv run python -c "import json, persian_text_guard as p; print(json.dumps({'all': len(p.WordList.all()), 'default': len(p.WordList.persian_default()), 'by': {c.value: len(p.WordList.bundled(c)) for c in p.WordCategory}}))"
node -e "const p=require('./../js/dist/index.cjs'); const by={}; for (const c of ['uncategorized','profanity','sexual','insult','slur','harassment','mild']) by[c]=p.WordList.bundled(c).length; console.log(JSON.stringify({all:p.WordList.all.length, default:p.WordList.persianDefault.length, by}))"
```

**Expected**: the two JSON lines are identical: about 1,250 in all and about 1,025 in the default
selection, with equal per-category counts. The corpus's category-selection cases already compare
entries and order; this is the one-off cross-check against a built JavaScript package.

## 4. The package as users install it (FR-001, FR-002, FR-004, FR-006, SC-003, SC-004, SC-007)

```bash
cd python
uv build
uv run python scripts/check_package.py
uv run python scripts/check_consumers.py
```

**Expected**:
- `dist/` holds `persian_text_guard-<v>-py3-none-any.whl` and `persian_text_guard-<v>.tar.gz`;
- `check_package.py` reports that the file lists match the allowlist, there are 0 dependencies,
  `Requires-Python` is `>=3.11`, the installed size is under 1 MB, and `twine check` passed;
- `check_consumers.py` reports that the wheel and sdist environments both print the quick-start results;
  `pyright --strict` and `mypy --strict` accept `typed_usage.py`; and `type_errors.py` fails with exactly
  the two expected errors.

For SC-003, follow the README's quick start by hand in a new folder:

```bash
py -3.14 -m venv t
t/Scripts/pip install python/dist/*.whl
```

Then paste the quick start into `t/Scripts/python`. The time from an empty folder to a flagged message
should be under 5 minutes.

## 5. API compatibility detects a break (FR-022)

```bash
cd python
uv run python scripts/check_api.py
```

**Expected**: "baseline: no previous release", which passes, because no release tag has `python/` yet.

Then prove that griffe catches a break. On a throwaway commit, rename the `mask` parameter of
`ProfanityFilter.censor` and run:

```bash
uv run python scripts/check_api.py --against HEAD~1
```

**Expected**: it fails and names `censor(mask)`. Afterwards, `git reset --hard HEAD~1` removes the
throwaway commit.

## 6. Performance (SC-005)

```bash
cd python
uv run python bench/bench_filter.py -o .bench/results.json
uv run python scripts/bench_table.py .bench/results.json
```

**Expected**, on the i7-9700K with CPython 3.14: building takes under 500 ms, `CleanShortMessage` under
250 µs, and `VeryLongMessage` under 3 s. The table goes into `python/README.md`. In CI, `scripts/bench_gate.py`
applies a 500 µs regression gate, because shared runners vary in speed (research R3). Run
`uv run python scripts/bench_gate.py --limit-us 250` here to check SC-005's own value.

## 7. Release checks (SC-009, SC-011)

**Before merge**: the dry run (research R19). On the scratch branch, the three runs must end as follows:

| Run | Tag | Python job | Publish jobs |
| --- | --- | --- | --- |
| 1 | `v1.4.0-dev.1` | fails (forced) | all three skipped |
| 2 | `v1.4.0-dev.2` | passes | all three succeed with stubbed steps; the PyPI stub lists `persian_text_guard-1.4.0.dev2-py3-none-any.whl` and `.tar.gz` |
| 3 | `v1.4.0-dev.3` on the run-2 commit | passes | all three fail at "Check tag matches VERSION" |

Then delete every dry-run ref. PyPI, npm and NuGet must show no 1.4.0 or dev versions.

**After the release**, in fresh folders:

```bash
py -3.14 -m venv v && v/Scripts/pip install persian-text-guard==1.4.0 && v/Scripts/python -c "from persian_text_guard import ProfanityFilter, WordList; print(ProfanityFilter(WordList.persian_default()).contains_profanity('ک.ی.ر'))"
npm view persian-text-guard@1.4.0 version
curl -s https://api.nuget.org/v3-flatcontainer/persiantextguard/index.json
```

**Expected**: `True`; `1.4.0`; and a list that includes `1.4.0`. The PyPI project page shows the
attestation for `AmirehsanK/PersianTextGuard`, and the README in English and Persian.

## 8. Re-running the Unicode measurement (research R1)

The dump scripts, `dump.cs` (.NET, run with `dotnet run dump.cs -- <dir>`), `dump.py` and `compare.py`,
are kept in `specs/004-python-port/tools/`, so the comparison can be
repeated whenever CPython or .NET moves to a new Unicode version:

```bash
dotnet run specs/004-python-port/tools/dump.cs -- <out>
py -3.14 specs/004-python-port/tools/dump.py <out> py3.14
py -3.14 specs/004-python-port/tools/compare.py <out>
```

**Expected**: the counts in research R1, or a new difference to add to the corpus first (spec Edge
Cases).
