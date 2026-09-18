# Contract: Layout, Commands, CI and Release for the Python Port

**Feature**: [../spec.md](../spec.md) | **Research**: [../research.md](../research.md) R4, R8–R14, R19, R20 | **Extends**: [003 package-and-release contract](../../003-javascript-typescript-port/contracts/package-and-release.md)

## Layout

New or changed paths only.

```text
/
├── VERSION                                  # 1.3.0 → 1.4.0
├── conformance/cases/robustness.json        # + characters outside the Basic Multilingual Plane (research R2)
├── dotnet/src/PersianTextGuard/PersianTextGuard.csproj   # PackageValidationBaselineVersion 1.2.0 → 1.3.0
├── python/
│   ├── pyproject.toml                       # [project] name, dynamic version, requires-python >=3.11; tool config
│   ├── hatch_build.py                       # metadata hook (version) + build hook (word lists)
│   ├── uv.lock                              # dev tools pinned
│   ├── README.md                            # shown on PyPI; English + Persian
│   ├── src/persian_text_guard/
│   │   ├── __init__.py                      # public names, __all__, __version__
│   │   ├── py.typed
│   │   ├── _types.py  _unicode.py  _utf16.py  _normalizer.py  _source_map.py
│   │   ├── _filter.py  _scan.py  _regions.py  _fold.py  _word_list.py
│   │   └── _wordlists.py  _version.py       # generated, git-ignored
│   ├── tests/
│   │   ├── corpus/                          # loader, input builder, evaluation, comparison
│   │   ├── test_corpus.py  test_corpus_guards.py
│   │   ├── test_api.py  test_unicode.py  test_internals.py  test_threads.py
│   │   ├── test_word_list_load.py  test_readme.py
│   ├── bench/bench_filter.py                # pyperf
│   ├── consumers/smoke.py  typed_usage.py  type_errors.py
│   └── scripts/check_package.py  check_consumers.py  check_api.py  bench_table.py  bench_gate.py
├── README.md                                # PyPI package listed; Development + Releasing gain python/
├── .gitignore                               # python build outputs
└── .github/workflows/ci.yml                 # Python jobs, Publish to PyPI; needs updated
```

**Git-ignored**: `python/.venv/`, `python/dist/`, `python/build/`, the generated
`src/persian_text_guard/_wordlists.py` and `_version.py`, `.pytest_cache/`, `.mypy_cache/`,
`.ruff_cache/`, `__pycache__/` and `python/.bench/`.

## Commands

Run from `python/`. `uv` manages the environment. `uv sync --locked` installs the pinned tools and the
package in editable mode.

| Purpose | Command | Result |
| --- | --- | --- |
| Set up | `uv sync --locked` | `.venv` with the dev tools and an editable package; generates `_wordlists.py` and `_version.py` |
| Lint and format | `uv run ruff check && uv run ruff format --check` | Includes the `D` rules: a docstring on every public member |
| Type check | `uv run mypy --strict src tests` | |
| Unit tests | `uv run pytest -m "not corpus"` | Everything except the corpus |
| Corpus | `uv run pytest -m corpus` | Every case in `conformance/`; one test per case id |
| All tests | `uv run pytest` | Unit tests, corpus, README examples and threads |
| Another Python | `uv run --python 3.11 pytest` | Also 3.12, 3.13, 3.14 and 3.14t (research R20) |
| Build | `uv build` | `dist/*.whl` and `dist/*.tar.gz` |
| Package checks | `uv run python scripts/check_package.py` | `twine check --strict`, `check-wheel-contents`, the file allowlist, no dependencies, `Requires-Python`, size under 1 MB |
| Consumer checks | `uv run python scripts/check_consumers.py` | Clean virtual environments from the wheel and the sdist; smoke test; `pyright` and `mypy` strict on the consumer files |
| API check | `uv run python scripts/check_api.py` | griffe against the newest release tag that has `python/`; baseline when there is none |
| Benchmarks | `uv run python bench/bench_filter.py -o .bench/results.json` | pyperf; `scripts/bench_table.py` prints the README table |
| Benchmark gate | `uv run python scripts/bench_gate.py` | Fails if the short-message mean is over 250 µs (research R3) |

## Package contents

`scripts/check_package.py` compares the built files with these lists exactly:

```text
wheel:  persian_text_guard/__init__.py, _types.py, _unicode.py, _utf16.py, _normalizer.py,
        _source_map.py, _filter.py, _scan.py, _regions.py, _fold.py, _word_list.py,
        _wordlists.py, _version.py, py.typed
        persian_text_guard-<v>.dist-info/{METADATA, WHEEL, RECORD, licenses/LICENSE,
        licenses/THIRD-PARTY-NOTICES.md}
sdist:  the same package files under src/, plus pyproject.toml, hatch_build.py, README.md,
        LICENSE, THIRD-PARTY-NOTICES.md, PKG-INFO
```

The metadata has:
- `Name: persian-text-guard`, `Version: <PEP 440 VERSION>`, `License-Expression: MIT`, and both licence
  files under `License-File`;
- `Requires-Python: >=3.11`, with no `Requires-Dist`;
- classifiers for Python 3.11–3.14, `Typing :: Typed` and free threading;
- project URLs for the repository (directory `python`), the issues and the README;
- keywords matching the NuGet tags;
- `Description-Content-Type: text/markdown`.

## CI

The existing job names MUST NOT change. The Python rows are added.

| Job | Runs on | Steps | Required on `main` |
| --- | --- | --- | --- |
| `Build, test, pack` | ubuntu | as today | yes |
| `Test on .NET Framework 4.8 (netstandard2.0 build)` | windows | as today | yes |
| `JavaScript (Node 22)`, `JavaScript (Node 24)` | ubuntu | as today | yes |
| `Python (3.11)`, `Python (3.12)`, `Python (3.13)` | ubuntu | `uv sync --locked`, ruff, mypy, unit tests, corpus, README examples | yes (added after merge) |
| `Python (3.14)` | ubuntu | as above, plus: `uv build`, package checks, consumer checks, API check (full git history), benchmark gate, upload the `python-package` artifact | yes (added after merge) |
| `Python (3.14t)` | ubuntu | free-threaded 3.14 with `PYTHON_GIL=0`: unit tests, corpus, thread tests, `pytest-run-parallel` | yes (added after merge) |
| `Publish to NuGet`, `Publish to npm` | ubuntu | as today; `needs` gains the five Python jobs | — |
| `Publish to PyPI` | ubuntu | on `v*` tags; `needs` all nine build and test jobs; `environment: pypi`; `id-token: write`; tag check; download `python-package`; check the file versions; `pypa/gh-action-pypi-publish@release/v1` with `skip-existing: true` | — |

**Guarantees**:
- On a `v*` tag, no publish job starts unless every build and test job of every port succeeds.
- Every publish job refuses a tag that differs from `v` + `VERSION`.
- Re-running any publish job after a partial release is safe.
- The PyPI upload carries attestations that name this repository and workflow.

## Release 1.4.0

Steps marked 👤 are account actions only the maintainer can perform.

1. 👤 On pypi.org: **Your projects → Publishing → Add a new pending publisher** (GitHub):
   - PyPI project name `persian-text-guard`;
   - owner `AmirehsanK`, repository `PersianTextGuard`;
   - workflow `ci.yml`, environment `pypi`.
2. 👤 On GitHub: **Settings → Environments → New environment** `pypi`, with deployment branches and tags
   limited to tag `v*`, as for `npm`. No secrets.
3. Before merge, a real CI dry run on `v1.4.0-dev.*` tags from a scratch branch proves the gates, with
   every publish step stubbed (research R19). The tags and branch are deleted afterwards.
4. The pull request merges with the maintainer's go-ahead, `VERSION` = `1.4.0`, and CI green.
5. Add the five Python job names to `main`'s required status checks.
6. Push tag `v1.4.0` on the merge commit after checking: `main` is green, `VERSION` is `1.4.0`, and no
   registry has 1.4.0. This is **irreversible**.
7. CI publishes `PersianTextGuard 1.4.0` to NuGet, and `persian-text-guard 1.4.0` to npm and to PyPI.
8. Verify from the public registries in fresh projects (SC-011, quickstart §7).
9. Create the GitHub release `v1.4.0` with English notes and a Persian summary (research R15).

Unlike npm, no placeholder has to be retired: the pending publisher creates the PyPI project on its
first upload.
