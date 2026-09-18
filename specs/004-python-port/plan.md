# Implementation Plan: Python Port Published to PyPI

**Branch**: `004-python-port` | **Date**: 2026-09-18 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/004-python-port/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Add a pure-Python port of PersianTextGuard in `python/`, published to PyPI as `persian-text-guard`
(imported as `persian_text_guard`), and release it with NuGet and npm as `1.4.0`. Per
[research.md](research.md):

- **Unicode, measured on every code point** against .NET 10, for CPython 3.11–3.14 and 3.14t.
  - Python 3.14 agrees with .NET on every category.
  - Older versions differ only on characters that are new in Unicode 15 and 16.
  - `str.isspace` differs from .NET on U+001C–U+001F, so the port uses .NET's whitespace set, as
    JavaScript does.
  - The differences are documented as a limitation (R1).
- **A UTF-16 view inside Python.** .NET reads text in UTF-16 units; Python strings are code points. The
  matcher runs on a view in which each supplementary character is split into its two surrogates, so the
  JavaScript code ports line by line. Positions are converted to code points at the boundary, and
  `censor` splices the caller's own string. Eight new corpus cases pin the behaviour this exposed (for
  example `kir𠀀`) for every port (R2).
- **Speed checked before building.** A proxy, the JavaScript port without its JIT calibrated against
  CPython, estimates 145 µs for a short message against the 250 µs target. CI enforces that target
  (R3).
- **One universal wheel and a self-contained sdist.** hatchling hooks generate the word lists from
  `wordlists/` and read the version from `VERSION`, converted to PEP 440 (R4, R6, R9).
- **A Pythonic API with the same capabilities.** It uses `StrEnum` enumerations whose values are the
  corpus names, frozen dataclasses, `None` for a missing message, `TypeError` for non-strings, and
  `WordList.load` for files (R5, R18).
- **Tests.**
  - A pytest corpus runner with one test per case, and port tests for Python-specific behaviour.
  - README examples are executed.
  - Consumer checks run on the wheel and the sdist, with strict pyright and mypy.
  - 8-thread tests run on free-threaded 3.14t (R8, R12, R17).
- **Released in lockstep.** Trusted publishing to PyPI from `ci.yml`, with attestations. Every publish
  job needs every port's jobs. griffe checks API compatibility. A real CI dry run proves the gates
  before `v1.4.0` (R10, R13, R14, R19).

## Technical Context

**Language/Version**: Python 3.11–3.14, including free-threaded 3.14t; pure Python with full type
hints. C# is touched only in the `.csproj` validation baseline.

**Primary Dependencies**:
- **Runtime**: none (Principle III, FR-004).
- **Build**: hatchling 1.32, which brings `packaging`.
- **Development**, pinned in `python/uv.lock`: pytest 9, pytest-run-parallel 0.10, mypy 2.3, pyright
  1.1.414, ruff 0.16, pyperf 2.10, griffe 2.3, build 1.6, twine 7.0, check-wheel-contents 0.6 and
  readme-renderer 46.

**Storage**: Repository files: `wordlists/*.txt` (read at build time), `conformance/**/*.json` (read by
tests) and `VERSION` (read at build time).

**Testing**:
- pytest: unit tests, the corpus runner, README examples and thread tests, on 3.11–3.14 and 3.14t;
- consumer checks on the built wheel and sdist;
- the .NET and JavaScript corpus runners, which also run the new corpus cases.

**Target Platform**: CPython ≥ 3.11 on any operating system (a `py3-none-any` wheel). Other
interpreters, such as PyPy, may work but are not supported or tested.

**Project Type**: Library monorepo. This feature adds the third port.

**Performance Goals**: SC-005 on the i7-9700K with CPython 3.14: building under 500 ms, a short clean
message under 250 µs mean, and the 132,000-character message under 3 s. R3 estimates 25 ms, 145 µs and
0.36 s.

**Constraints**:
- 0 runtime dependencies and an installed size under 1 MB.
- Pure Python (Principle III).
- Results equal the corpus, with 0 cases not applicable.
- .NET and JavaScript behaviour unchanged (FR-026).
- Existing CI job names unchanged.
- Releases are irreversible: tag only with the maintainer's go-ahead.

**Scale/Scope**:
- **Python source**: about 2,700 lines.
- **Tests and runner**: about 1,000 lines.
- **Other files**: 3 consumer files, 1 benchmark, 5 scripts.
- **Elsewhere**: about 8 corpus cases, a one-line `.csproj` change, and CI adding 6 jobs.
- **Docs**: 2 READMEs.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Evaluated against [constitution v2.0.0](../../.specify/memory/constitution.md).

| Principle / section | Gate | Before research | After design |
| --- | --- | --- | --- |
| **I. Ordinary Messages Must Pass** | Ordinary corpus cases pass in every port | ✅ FR-017 | ✅ The runner enforces kind rules (R8). The new corpus cases include an ordinary message beside a supplementary letter (`kir 𠀀` is flagged only for `kir`). |
| **II. User Input Never Throws** | Every text function returns for any text the language can represent | ⚠️ Non-`str` → `TypeError` (spec Assumptions) | ⚠️ **Justified** below. `None` and every `str` never raise, including lone and caller-split surrogates, noncharacters and 132,000 characters (P1). The UTF-16 view keeps .NET's surrogate semantics (R2). |
| **III. Native and Dependency-Free** | Pure Python with type hints; no runtime dependencies; tested on every CPython in upstream support | ⚠️ 3.10 is in upstream support until 2026-10-31, and the user chose 3.11 as the minimum | ⚠️ **Justified** below. Pure Python, 0 dependencies, `py.typed`; CI on 3.11, 3.12, 3.13, 3.14 and 3.14t; build and development tools only (R4, R13). |
| **IV. Build Once, Match Fast, Share Safely** | Immutable, shareable across threads including free-threaded; construction-time work; token lookup; pyperf | ✅ | ✅ Slotted, frozen state; no mutable module state except an idempotent category cache and a locked lazy parse (R17); `dict` token lookup ported from .NET (R7); a pyperf suite, README table and CI gate (R3, R11); tests on 3.14t with the GIL off. |
| **V. One Behaviour, Verified in Every Language** | Passes every corpus case; code-point positions in Python; same capabilities | ✅ | ✅ The corpus runner follows the contract (R8). Positions are native code points (R2). Every capability is present (contracts/public-api.md). `WordList.load` is a file-reading convenience, not a new capability (spec Assumptions). New behaviour found by planning becomes corpus cases first, for every port (R2). |
| **VI. Documented in Persian and English, Pinned by Tests** | Package README bilingual; every example tested; docstrings on every public member | ✅ | ✅ `python/README.md` in English and Persian, readable on PyPI, which removes `dir` (R16); `test_readme.py`; ruff `D` rules; release notes with a Persian summary. The root README stays English-only under the adoption clause, as in 003. |
| **VII. Curated, Categorised, Credited Word Lists** | Lists live once in `wordlists/`, embedded at build | ✅ | ✅ Generated by the build hook into a git-ignored module; no copy in `python/` (R6). `THIRD-PARTY-NOTICES.md` ships in both artifacts. |
| **Public API & Versioning** | Lockstep from `VERSION`; tag = version; API check against the previous release; blocked if any port fails; MINOR | ✅ | ✅ The hook reads `VERSION` (PEP 440 form); every publish job needs every port's jobs and checks the tag (R10, R13); griffe with a first-release baseline (R14); `1.4.0` is MINOR; .NET's validation baseline moves to 1.3.0 (R15). ⚠️ Three registries cannot publish atomically; mitigated by idempotent re-runs, as in 003. |
| **Development Workflow & Quality Gates** | Branch and PR; CI green for every port; behaviour PRs update corpus and ports together | ✅ | ✅ Branch `004-python-port`. The new corpus cases run in .NET and JavaScript in the same PR, and change no existing behaviour. The Python jobs become required checks after merge. |
| **Governance: Adoption** | No PR moves further from unmet requirements | ✅ | ✅ Adds the `python/` port and PyPI to lockstep releases. |

**Gate result**: **PASS.** Three ⚠️ items are justified in Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/004-python-port/
├── plan.md                         # This file
├── research.md                     # Phase 0: R1–R20
├── data-model.md                   # Phase 1: enums, entries, options, filter, match, word lists, errors, package
├── quickstart.md                   # Phase 1: validation guide, including the release
├── contracts/
│   ├── public-api.md               # Phase 1: Python declarations, guarantees P1–P12, name table
│   └── package-and-release.md      # Phase 1: layout, commands, package contents, CI, release order
├── tools/                          # R1's Unicode dump and compare scripts
├── checklists/requirements.md      # Spec quality checklist
└── tasks.md                        # Phase 2 (/speckit-tasks — not created by /speckit-plan)
```

### Source Code (repository root)

```text
VERSION                                          # 1.3.0 → 1.4.0

conformance/cases/robustness.json                # + 8 cases: characters outside the BMP (R2)

dotnet/src/PersianTextGuard/PersianTextGuard.csproj  # PackageValidationBaselineVersion 1.2.0 → 1.3.0

python/                                          # NEW: the PyPI package persian-text-guard
├── pyproject.toml  hatch_build.py  uv.lock  README.md
├── src/persian_text_guard/
│   ├── __init__.py                              # public names, __all__, __version__
│   ├── py.typed
│   ├── _types.py                                # enums, BannedWord, options, match, errors (← types.ts)
│   ├── _unicode.py                              # .NET-equivalent Unicode primitives (← unicode.ts)
│   ├── _utf16.py                                # UTF-16 view and position mapping (R2)
│   ├── _normalizer.py  _source_map.py           # ← normalizer.ts, source-map.ts
│   ├── _filter.py  _scan.py  _regions.py  _fold.py  # ← filter.ts, scan.ts, regions.ts, fold.ts
│   ├── _word_list.py                            # ← word-list.ts, plus load()
│   └── _wordlists.py  _version.py               # generated, git-ignored
├── tests/
│   ├── corpus/                                  # loader, inputs, evaluation, comparison
│   ├── test_corpus.py  test_corpus_guards.py
│   ├── test_api.py  test_unicode.py  test_internals.py  test_threads.py
│   ├── test_word_list_load.py  test_readme.py
├── bench/bench_filter.py
├── consumers/smoke.py  typed_usage.py  type_errors.py
└── scripts/check_package.py  check_consumers.py  check_api.py  bench_table.py  bench_gate.py

README.md                                        # PyPI package listed; Development and Releasing gain python/
.gitignore                                       # python build outputs
.github/workflows/ci.yml                         # Python (3.11–3.14, 3.14t), Publish to PyPI; needs updated
```

**Structure Decision**: The constitution's monorepo layout, with the Python port in its own top-level
`python/` directory next to `dotnet/` and `js/`. It reads the shared `wordlists/`, `conformance/` and
`VERSION` from the root, and has its own tests, benchmarks and README. Inside, a `src/` layout keeps
tests on the installed package. Modules map one to one to the JavaScript files they port, plus
`_utf16.py`, which exists only because Python strings are not UTF-16.

## Complexity Tracking

| Decision | Why needed | Simpler alternative rejected because |
| --- | --- | --- |
| Non-`str` message → `TypeError` (Principle II allows failures only for programmer errors) | A caller passing `bytes` or a number has a bug. Failing loudly avoids an unchecked value passing as clean, and matches the JavaScript port and the user's earlier decision | *Treat as missing*: `b"..."` would be reported clean. *Coerce with `str()`*: `str(b"kir")` is `"b'kir'"`, and surprises callers. |
| Minimum CPython 3.11 while 3.10 is still in upstream support (Principle III: "every CPython version in upstream support") | The user chose it (spec Clarifications). 3.10 reaches end of life on 2026-10-31, weeks after this release, and 3.11 brings `StrEnum`, which the API uses | *Supporting 3.10* adds a version for about six weeks, needs a `StrEnum` fallback, and then a breaking-looking `Requires-Python` bump. The deviation ends by itself on 2026-10-31. |
| A UTF-16 view instead of native code points inside the matcher | .NET's per-unit semantics decide tokens next to supplementary characters; a code-point-native port would differ (R2) | *Adapting each per-unit test by hand* rewrites the shared algorithm where the corpus is thinnest. The view costs one C-level scan on the fast path. |
| Three registries published by three jobs, not atomically | GitHub Actions cannot publish to NuGet, npm and PyPI in one transaction | *One job for all three* is still not atomic and mixes three OIDC environments. Mitigation: every job gates on every port's jobs, and re-runs are idempotent (R10). |
| CPython's `unicodedata` instead of pinned tables | Small and data-free. The differences affect only Unicode 15+ characters, and vanish on 3.14 for categories (R1) | *Tables pinned to .NET's Unicode version*: about 100 KB, a generator, and still no agreement with .NET Framework 4.8. |
| A pending publisher instead of a placeholder release | FR-020 forbids stored tokens, and a placeholder upload would need one | *Uploading a 0.0.1 placeholder by hand*, as on npm, needs an API token and leaves a version to yank. |
