# Research: Python Port Published to PyPI

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Date**: 2026-09-18

Decisions made in Phase 0, each with the measurement or reasoning behind it. Spec 003's research, which
this builds on, is cited as "003 R*n*".

---

## R1. Unicode behaviour: .NET versus Python, measured

**Method.** As in 003 R1, every one of the 65,536 UTF-16 code units and every one of the 1,112,064
Unicode scalar values was run through the primitives the matcher relies on, on .NET 10.0.11 and on
CPython 3.11.16, 3.12.14, 3.13.15, 3.14.6 and 3.14.7 free-threaded (Windows 11). The primitives were:
- the general category, per unit and per code point;
- whitespace;
- lower-casing one unit;
- NFKC and NFD normalization.

The Python candidates were `unicodedata.category`, the explicit .NET whitespace set from 003 R1,
`str.lower` on one character (kept unchanged when the result is not one character), and
`unicodedata.normalize`. The dump and compare scripts are in `tools/` next to this file and are
reproducible (quickstart §8).

**Findings.**

| Primitive | 3.11 (Unicode 14.0) | 3.12 (15.0) | 3.13 (15.1) | 3.14 and 3.14t (16.0) |
| --- | --- | --- | --- | --- |
| Category per unit | 24 | 22 | 17 | **0** |
| Category per code point | 10,302 | 5,813 | 5,186 | **0** |
| Whitespace (.NET set) | 0 | 0 | 0 | 0 |
| Lower-casing per unit | 0 | 0 | 0 | 5 |
| NFKC | 62 | 0 | 0 | 36 |
| NFD | 0 | 0 | 0 | 20 |

- **Categories.** Every difference is a character that .NET 10 assigns and the older Python does not
  (reported as `Cn`), plus one re-categorisation, U+1171E AHOM SIGN (Mc in .NET, Mn in Python ≤ 3.13).
  Python 3.14 agrees with .NET 10 on all 1,112,064 code points.
- **Whitespace.** `str.isspace` is **not** usable: it also counts U+001C–U+001F, which .NET does not.
  The explicit set from 003 R1 has 0 differences on every version.
- **Lower-casing.** The 5 differences on 3.14 are case pairs that are new in Unicode 16 (U+1C89, U+A7CB,
  U+A7CC, U+A7DA, U+A7DC), which .NET 10 leaves unchanged. U+0130 `İ` lower-cases to two characters in
  Python. The one-unit rule keeps it unchanged, as .NET does; this is the same rule as in the JavaScript
  port.
- **NFKC.**
  - 3.11 does not fold U+1E030–U+1E06D (Cyrillic modifier letters, Unicode 15), which .NET does.
  - 3.14 folds U+1CCD6–U+1CCF9 (Unicode 16 outlined letters and digits), which .NET 10 does not. This is
    the same 36 as JavaScript (003 R1).
- **NFD.** 20 Unicode 16 decompositions that .NET 10 lacks.
- **Noncharacters.** `unicodedata.normalize` keeps them, and so do lone surrogates, so Python needs
  neither .NET's U+FFFD replacement nor its noncharacter workaround for normalization to succeed. The
  port still reproduces .NET's replacement of lone surrogates, because that replacement is visible in
  the results the corpus records, as the JavaScript port's `normalizer.ts` does.

**Decision.** Use CPython's own `unicodedata`, behind one internal module, `_unicode.py`, that
reproduces .NET semantics exactly as the JavaScript port's `unicode.ts` does:
- the explicit whitespace set, never `str.isspace`;
- lower-casing one unit at a time, leaving a unit unchanged when Python would expand it;
- categories per UTF-16 unit or per code point, wherever .NET tests one or the other;
- NFKC between noncharacters, and U+FFFD for lone surrogates.

All differences affect only characters assigned in Unicode 15 or later. None appears in the corpus, as
the Node.js 22 run in 003 already showed for 15.1 data. Like JavaScript, the differences are documented
as a limitation in the port README; on Python 3.11 that includes the 62 Cyrillic modifier letters.

**Alternatives considered.**
- *Bundled Unicode tables pinned to .NET's version*: identical results on every Python, but about 100 KB
  of generated data, a generator to maintain, and still a mismatch with .NET Framework 4.8 (003 R1).
  Rejected for the same reasons as in JavaScript.
- *Requiring Python 3.14*: it would remove every category difference, but the user chose 3.11 as the
  minimum (spec Clarifications).

---

## R2. Strings: running the matcher on UTF-16 units inside Python

**Problem.** The .NET matcher, and the JavaScript port that copies it, reads text as UTF-16 code units.
Python strings are sequences of code points. The difference matters wherever .NET looks at a single
unit, for example `char.IsLetter` on half of a surrogate pair, which is false. Probing the JavaScript
port, whose results equal .NET's on the whole corpus, shows the effect:

| Input | Matches (entry, UTF-16 index, length) | `tokenize` | `censor` |
| --- | --- | --- | --- |
| `kir𠀀` | `kir`, 0, 5 | `["kir𠀀"]` | `****` |
| `𠀀kir` | `kir`, 0, 5 | `["𠀀kir"]` | `****` |
| `kir 𠀀` | `kir`, 0, 3 | `["kir","𠀀"]` | `**** 𠀀` |
| `k𠀀ir` | `kir`, 0, 5 | `["k𠀀ir"]` | `****` |
| `کیر😀` | `کیر`, 0, 3 | `["کیر"]` | `****😀` |
| `𝐤𝐢𝐫` | `kir`, 0, 6 | `["𝐤𝐢𝐫"]` | `****` |
| `fu𐐀ck` | `fuck`, 0, 6 | `["fu𐐀ck"]` | `****` |

A port that tests Python's code points directly would treat `𠀀` (U+20000, a CJK letter) as one letter,
where .NET sees two surrogate units. That changes tokens, and so regions and censored output. The corpus
has only 9 cases with characters outside the Basic Multilingual Plane, and none of these.

**Decision.**
1. **The matcher runs on a UTF-16 view of the message.** At the public boundary, a message that contains
   a code point above U+FFFF is rewritten into a `str` in which each such code point becomes its two
   surrogate code points. A Python `str` can hold surrogates. Inside, the port is then a line-by-line
   port of the JavaScript code, with `text[i]` meaning "UTF-16 unit *i*" exactly as `charCodeAt` does.
   - **Fast path.** Most messages have no supplementary character. That is checked with one C-level
     scan: `text.isascii()`, or a pre-compiled regex search for `[\U00010000-\U0010FFFF]`. The view is
     then the caller's string itself, and no mapping is needed.
   - **Pairs recombined where .NET reads code points.** NFKC and `GetUnicodeCategory(string, index)`
     recombine a valid surrogate pair in the view before calling `unicodedata`, as .NET does.
2. **Positions are converted once, at the boundary.** Match `index` and `length` are converted from
   view units to code points of the caller's string with a prefix table built only when the view
   differs. The caller's own surrogates, which Python allows, map one to one. This is the same
   conversion the corpus runners do (constitution Principle V).
3. **`censor` splices the caller's string.** It masks the matched regions, converted to code points, in
   the caller's original string, so unmasked text is returned exactly as given, including any lone or
   split surrogates. Each region becomes four mask characters, whatever its length, as in .NET
   (`MaskLength = 4` in `ProfanityFilter.Regions.cs`), so the unit-to-code-point difference never
   changes the mask.
4. **Pin it in the corpus.** The seven probes above, plus a supplementary letter inside a Persian word,
   become corpus cases (`robustness.json`, group "characters outside the Basic Multilingual Plane"),
   recorded from .NET by the fill tool. Every port must pass them (spec FR-026, Edge Cases).

**Alternatives considered.**
- *A code-point-native port adapted by hand*: every per-unit test, and every index, would need its own
  surrogate handling. That rewrites the algorithm the other ports share and invites divergence exactly
  where the corpus is thinnest.
- *Converting to a list of integers*: loses Python's fast `str` operations (slicing, `in`, `dict` keys
  of `str`) and is slower.

---

## R3. Performance: what pure Python can do, measured by proxy

The spec asks that SC-005's targets be confirmed by a measurement during planning. A pure-Python port
does not exist yet, so the measurement used a proxy with a calibration step.

1. **The JavaScript port with V8's JIT compiler disabled** (`node --jitless`, Node.js 24.21.0) runs the
   same algorithm in an interpreter.
2. **A calibration kernel**, written identically in JavaScript and Python, measures the speed of
   CPython's interpreter against V8's. It does a per-unit scan with a fold table, a cached category test,
   lower-casing, token building and set lookups on a 160-character message.

| Measurement (i7-9700K) | Node.js 24, JIT | Node.js 24, `--jitless` | CPython 3.11 | 3.12 | 3.13 | 3.14 | 3.14t |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Calibration kernel | 24.3 µs | 46.6 µs | 58.1 µs | 53.0 µs | 48.5 µs | 51.0 µs | 53.7 µs |
| Build filter from the default list | 2.5 ms | 19.4 ms | | | | | |
| Short clean message | 11.6 µs | 114.5 µs | | | | | |
| Long clean message (≈ 280 characters) | 123 µs | 934 µs | | | | | |
| `find_matches`, mixed dirty message | 33.0 µs | 234 µs | | | | | |
| 132,000-character message | 34.9 ms | 284 ms | | | | | |

CPython runs the kernel at 1.0–1.25× the time of V8's interpreter. The estimates for the Python port
below use a factor of 1.25, rounded up:

| Operation | Estimate | SC-005 target | Margin |
| --- | ---: | ---: | ---: |
| Build from the default list | ≈ 25 ms | < 500 ms | 20× |
| Short clean message | ≈ 145 µs | < 250 µs | 1.7× |
| 132,000-character message | ≈ 0.36 s | < 3 s | 8× |

**Decision.** Keep SC-005's targets. The short-message target has the least margin, so:
- the port keeps .NET's construction-time work, such as normalized entry tables and first-character
  indexes, and avoids per-message allocation where Python allows;
- it uses C-implemented `str` methods for whole-string passes, such as `str.translate` for digit and
  letter folding, and `isascii` fast paths;
- it caches the category of each unit in a `dict`, which R17 shows is safe without the GIL;
- the pyperf suite runs in CI on 3.14 and fails if the short-message mean exceeds 250 µs on the CI
  runner, while the README table comes from the named machine. A miss is fixed before release, not
  documented away (constitution Principle IV).

**Alternatives considered.** *A C extension or Rust core*: the constitution requires the port to be
pure Python (Principle III). *Measuring only after implementation*: the spec required a check during
planning, and the proxy is enough to show that the targets are reachable.

---

## R4. Packaging: hatchling with a build hook, one universal wheel and a self-contained sdist

**Decision.**
- **Build backend**: `hatchling` 1.32 (a build-only dependency; the constitution allows those).
  `python/hatch_build.py` holds two small hooks:
  - **metadata hook**: sets the version from `../VERSION` when building from the repository, or from
    the generated `_version.py` when building a wheel from the sdist. The version is converted to
    PEP 440 with `packaging.version.Version`, which `hatchling` already depends on (R9);
  - **build hook**: generates `src/persian_text_guard/_wordlists.py` from `../wordlists/*.txt` (R6)
    and `_version.py`, both git-ignored. When `../wordlists` is absent, as inside an unpacked sdist, it
    keeps the generated files already present and fails if they are missing.
- **Outputs**: `persian_text_guard-<v>-py3-none-any.whl`, and `persian_text_guard-<v>.tar.gz`, which
  contains the generated files, so installing from the sdist needs nothing from the repository (spec
  Edge Cases).
- **Contents** are fixed by an allowlist in `pyproject.toml` (`[tool.hatch.build.targets.*]`) and
  checked by `scripts/check_package.py` (R12). The wheel holds the package, `py.typed`, `LICENSE`,
  `THIRD-PARTY-NOTICES.md` and the metadata with the README; the sdist adds `pyproject.toml` and
  `hatch_build.py`.
- **Layout**: `src/` layout, so tests import the installed package, not the source tree (FR-019).
- **Tool management**: `uv` pins the development tools in `python/uv.lock` through a PEP 735
  `[dependency-groups] dev` table. The package's own `[project] dependencies` stays empty (FR-004).

**Alternatives considered.**
- *setuptools with `setup.py` commands*: works, but uses more machinery and legacy hooks for the same
  two steps.
- *uv_build*: fast, but has no build hooks, so generating the word lists would need a separate
  pre-build step and a staging directory, as npm needed (003 R9).
- *Reading the lists from package data files through `importlib.resources`*: this is equivalent, but
  a generated module avoids file I/O at import, and it is how the JavaScript port does it (003 R6).

---

## R5. Public API in Python

**Decision.** Mirror the other ports' capabilities with Python conventions (FR-012). The full signatures
are in [contracts/public-api.md](contracts/public-api.md).
- **Enumerations** are `enum.StrEnum` (Python 3.11+): `WordMatchMode`, `WordCategory`, `EvasionKind` and
  `NormalizationStep`. Members are upper-case (`WordCategory.SLUR`), and each value is the corpus name
  (`"slur"`, `"wholeWord"`, `"lookalikeCharacters"`), so a value compares equal to the name the corpus
  and the JavaScript port use. Every parameter that takes one also accepts its name as a `Literal`
  string. A type checker rejects an unknown name, and at run time it raises `ValueError` (spec US1
  scenario 7, FR-013).
- **Value objects** are frozen, slotted dataclasses: `BannedWord(text, mode=WHOLE_WORD,
  category=UNCATEGORIZED)`, `ProfanityFilterOptions(squeeze_repeated_letters=True,
  fold_lookalike_characters=True, join_spaced_letters=True)` and `ProfanityMatch(word, evasion, index,
  length)`, with `evasion` a tuple in corpus order. Frozen dataclasses give immutability, equality,
  hashing and a readable `repr`. An unknown option name is a `TypeError` from the generated `__init__`,
  and a type-checker error.
- **`ProfanityFilter(words, options=None)`** has `count`, `contains_profanity`, `find_match` (which
  returns `None` when there is no match), `find_matches` (which returns a tuple), and `censor(text,
  mask="*")`.
- **`WordList`** is a class of static methods: `all()`, `persian_default()`, `bundled(*categories)`,
  `parse(text)` and `load(source)`. Python has no class-level property since 3.13 removed chaining
  `classmethod` and `property`, and methods keep the cached-tuple semantics explicit. `load` takes a
  `str` or `os.PathLike` path, or an open binary or text file (spec Clarifications, FR-011).
- **Functions**: `normalize(text, steps="comparison")`, `tokenize(text)`, `to_persian_digits(text)` and
  `to_ascii_digits(text)`.
- **Errors**:
  - a non-`str` message, word-list text or mask raises `TypeError`;
  - an invalid mask, an unknown category, mode, step or preset name, or a bad entry raises `ValueError`;
  - `WordListFormatError(ValueError)` has a 1-based `line`;
  - file problems in `load` raise Python's own `OSError` and `UnicodeDecodeError`, which name the file.
- **The exported set** is `__all__`, and `py.typed` marks the package as typed. Everything else lives in
  underscore modules.

**Alternatives considered.**
- *Plain `Enum`*: callers could not pass `"slur"` and would lose equality with the corpus names.
- *`TypedDict` entries, as in JavaScript*: no validation at construction and no hashing. Dataclasses are
  the idiomatic value type.
- *A `WordList` module with functions*: `WordList.parse(...)` keeps the name table one-to-one with .NET
  and JavaScript (FR-012).

---

## R6. Bundled word lists: generated at build time

**Decision.** The build hook reads `wordlists/fa.txt`, `finglish.txt` and `en.txt` in the order .NET
embeds them (spec 002). It writes them into `_wordlists.py` as a tuple of string literals, which is
generated and git-ignored. `WordList` parses them once, on first use, under a lock (R17), and caches the
resulting tuples. The test suite also compares every selection with the JavaScript package's, entry by
entry (FR-018): quickstart §3 runs this once, and the corpus's category-selection cases run it on every
build.

---

## R7. Porting strategy

**Decision.** Port the JavaScript source file by file. It is already a faithful, string-based port of
.NET that passes the corpus, and R2's UTF-16 view makes `str` behave like a JavaScript string inside the
matcher. .NET stays the reference whenever the two read differently.

| JavaScript | Python |
| --- | --- |
| `unicode.ts` | `_unicode.py` (R1), plus `_utf16.py` (the view and position mapping, R2) |
| `normalizer.ts`, `source-map.ts` | `_normalizer.py`, `_source_map.py` |
| `filter.ts`, `scan.ts`, `regions.ts`, `fold.ts` | `_filter.py`, `_scan.py`, `_regions.py`, `_fold.py` |
| `word-list.ts` | `_word_list.py` |
| `types.ts` | `_types.py` |
| `index.ts` | `__init__.py` |

This comes to about 2,700 lines of Python with type hints and docstrings. Tests assert the corpus, not
the JavaScript code.

---

## R8. Tests and the corpus runner

**Decision.** Use `pytest` 9.
- **Corpus runner**: `tests/corpus/` loads `conformance/` per the corpus-format contract of spec 002.
  - It refuses a newer format version, and fails on a missing or unreadable corpus, fewer than 300
    cases, duplicate ids or pending cases.
  - It checks the kind rules, then compares every field.
  - Each case is one parametrized test with the case id as its test id, so a failure names the id and
    the run continues. The failure message gives the file, the input with invisible characters escaped,
    and each differing field.
  - Guard tests cover the refusal conditions, as in .NET's `CorpusGuardTests`.
- **Port tests** (`test_api.py`, `test_unicode.py`, `test_internals.py`, `test_threads.py`,
  `test_word_list_load.py`) cover FR-019:
  - `None` handling, and `TypeError` for non-strings;
  - mask, entry and word-list errors;
  - `load` from a path, from `PathLike`, and from binary and text files, with a BOM, with bad UTF-8 and
    with a missing file;
  - immutability;
  - R1's Unicode cases, and R2's view and position mapping, including the caller's own surrogates.
- **README examples**: `test_readme.py` extracts every `python` block from `python/README.md` and runs
  it, with its asserts.
- **Against the installed package**: CI installs the built wheel into the test environment. Locally,
  `uv run` uses an editable install. `tests/` never imports from `src/` by path.

---

## R9. Version from `VERSION`, converted to PEP 440

**Decision.** The metadata hook reads `VERSION` and normalizes it with `packaging.version.Version`:
`1.4.0` stays `1.4.0`, and a prerelease such as `1.4.0-dev.2` becomes `1.4.0.dev2`. A `VERSION` that
PEP 440 cannot represent fails the build. The dry-run versions therefore use the `-dev.N` form (R19),
which is valid for NuGet, npm and PyPI alike. The CI tag check still compares the tag with `VERSION`
itself. The PyPI publish step checks that the wheel's version equals the normalized `VERSION`.

---

## R10. Publishing to PyPI with trusted publishing

**Decision.**
- **Job `Publish to PyPI`** in `ci.yml`: `environment: pypi`, `permissions: id-token: write,
  contents: read`, and `needs` every build and test job of every port.
- **Steps**:
  1. the tag check, as in the other publish jobs;
  2. download the `python-package` artifact;
  3. check that the file names carry the normalized version;
  4. `pypa/gh-action-pypi-publish@release/v1` with `skip-existing: true`, so a re-run after a partial
     release is safe.

  Attestations (PEP 740) are on by default, so PyPI shows where the package was built (spec US3
  scenario 4).
- **Maintainer steps**, before the first release:
  - 👤 on PyPI, add a **pending publisher** for project `persian-text-guard`: owner `AmirehsanK`,
    repository `PersianTextGuard`, workflow `ci.yml`, environment `pypi`;
  - 👤 on GitHub, create environment `pypi` with deployment rule tag `v*`, as for `npm`.

  No token or password is stored (FR-020).
- **No placeholder release.** Unlike npm, a pending publisher reserves nothing, but the name is free
  (checked 2026-09-18). If it were taken before release, the first publish would fail harmlessly and
  nothing else would be published after it. A placeholder upload would need a manual token, which FR-020
  rules out.

**Order in the release.** The three publish jobs run in parallel after every build and test job
passes, as in 1.3.0. Atomicity across registries remains impossible; `skip-existing`, NuGet's
`--skip-duplicate` and npm's existence check make re-runs safe (003 R10).

---

## R11. Benchmarks with pyperf

**Decision.**
- `python/bench/bench_filter.py` uses `pyperf` 2.10 to run the same ten operations and messages as the
  .NET and JavaScript suites, copied verbatim.
- `uv run python bench/bench_filter.py -o results.json` writes the results, and
  `scripts/bench_table.py` turns them into the README's performance table. The table is measured on the
  README's named machine (the i7-9700K), on CPython 3.14.
- CI runs `bench/bench_filter.py --fast` on 3.14 and fails if the short-message mean exceeds 250 µs
  (R3).

---

## R12. Package and consumer checks

**Decision.** These run on the 3.14 job, against the built files:
- `twine check --strict` validates the metadata and that the README renders on PyPI;
- `check-wheel-contents` checks the wheel's layout;
- `scripts/check_package.py` checks that the wheel and sdist file lists equal the allowlist, that there
  are no runtime dependencies, that `Requires-Python` is `>=3.11`, and that the installed size is under
  1 MB (SC-004);
- `scripts/check_consumers.py` makes two clean virtual environments, installs the wheel into one and the
  sdist into the other with `pip --no-deps --no-index`, and runs `consumers/smoke.py` in each, which is
  the README quick start with asserts. It then runs `pyright --strict` and `mypy --strict` on
  `consumers/typed_usage.py`, and checks that `consumers/type_errors.py` fails with exactly the expected
  errors: an unknown option name and an unknown category (spec US1 scenario 7, SC-007).

---

## R13. CI jobs

**Decision.**

| Job | What it does |
| --- | --- |
| `Python (3.11)`, `Python (3.12)`, `Python (3.13)` | `ruff check`, `ruff format --check`, `mypy --strict`, unit tests, corpus, README examples |
| `Python (3.14)` | The same, plus: build, package and consumer checks, API check, benchmark gate, and upload of the `python-package` artifact |
| `Python (3.14t)` | Unit tests, corpus and the thread tests on free-threaded 3.14, with the GIL disabled (`PYTHON_GIL=0`), asserting `sys._is_gil_enabled()` is false |

- Each Python job uses `astral-sh/setup-uv` and `uv sync --locked`. Every job name is fixed by the
  matrix.
- `Publish to NuGet` and `Publish to npm` gain the Python jobs in `needs`, and `Publish to PyPI` needs
  all nine build and test jobs.
- **Required checks** after merge add the five Python job names. Existing job names are unchanged
  (FR-021).

---

## R14. API compatibility with griffe

**Decision.** `griffe` 2.3 (command syntax checked against its `check --help`) compares the public API of `persian_text_guard` at the working tree with the
most recent release tag that contains `python/` (`griffe check persian_text_guard --search python/src
--against <tag>`), via `scripts/check_api.py`.
- **Before 1.4.0 exists**, no tag contains the package. The script prints "baseline: no previous
  release" and passes, which is FR-022's first-release rule.
- A breaking change fails unless `VERSION`'s major version is greater than the tag's.
- **Proof that it detects a break**, before release: on a scratch commit, change a public signature and
  run the script `--against HEAD~1`. Quickstart §5 records this, as T059 did for JavaScript.

**Alternatives considered.** *A committed API snapshot, generated from the stubs*: readable in review,
but griffe already knows Python's compatibility rules (parameters, defaults, keyword-only changes,
removed members).

---

## R15. Version, .NET and JavaScript at 1.4.0

**Decision.**
- `VERSION` changes from 1.3.0 to **1.4.0**: MINOR, because a language is newly supported.
- NuGet and npm 1.4.0 are identical to 1.3.0 apart from the version.
- .NET's `PackageValidationBaselineVersion` moves from 1.2.0 to **1.3.0**, so the .NET package is
  validated against the previous release (SC-010). The JavaScript `api:compat` already compares with
  the newest release tag, now `v1.3.0`.
- The release notes, in English with a Persian summary, announce the Python package, the new corpus
  cases (R2), and that .NET and JavaScript behaviour is unchanged.

---

## R16. Documentation

**Decision.**
- `python/README.md` is the PyPI long description. It has installation and a quick start in English and
  Persian, the .NET/JavaScript/Python name table, the evasion summary, the performance table, the
  limitations (R1) and links.
- **Persian on PyPI.** PyPI's renderer removes the `dir` attribute (verified with readme-renderer 46.0,
  which renders `<div dir="rtl">` as `<div>`), so `<div dir="rtl">` blocks render
  left to right there but right to left on GitHub. The Persian text is written so it reads correctly in
  both directions (spec FR-024): each Persian paragraph is its own block, with no Latin words at the
  start or end of a line, and code in separate code blocks. `twine check --strict` confirms that the
  README renders.
- The root README lists the PyPI package next to NuGet and npm, and gains `python/` in its Development
  and Releasing sections. This part stays English-only under the constitution's adoption clause, as in
  003.
- Docstrings follow Google style, and `ruff`'s `D` rules enforce one on every public member (FR-023).

---

## R17. Thread safety, including free-threaded CPython

**Decision.**
- **Filter state** is built in `__init__` and then only read: `dict`, `frozenset` and tuples, stored on
  a class with `__slots__`, with no setters. Result objects are frozen dataclasses.
- **Lazy caches** are the category cache and the parsed bundled lists.
  - The **category cache** is a `dict` written with `setdefault`. Every thread computes the same value,
    and CPython's per-object locking without the GIL keeps `dict` operations safe, so the worst case is
    computing a value twice.
  - The **bundled lists** are parsed under a `threading.Lock` with double-checked initialization, so
    every caller gets the same tuple object (as 003's G8 does).
- **No module-level mutable state** otherwise. `re` patterns are compiled at import.
- **Tests**:
  - `test_threads.py` runs 8 threads, each checking every corpus message against one shared filter, and
    compares every result with a single-threaded run (SC-008);
  - `pytest-run-parallel` 0.10 re-runs the unit tests concurrently in the 3.14t job.

---

## R18. Values that are not strings

**Decision.**
- Each public text parameter is checked with `isinstance(text, str)`. `None` is the missing value, and
  anything else raises `TypeError` naming the parameter and the type received.
- A `str` subclass is accepted, and converted with `str(text)` before any work, so an overridden method
  cannot change the results.
- The same rule applies to `mask`, `WordList.parse` and `normalize`'s `steps`: a `str` preset name or an
  iterable of steps.

This matches the JavaScript port and is justified in the plan's Complexity Tracking.

---

## R19. Proving the release gates with a real CI dry run

**Decision.** Repeat 003 R18 with three changes:
- versions `1.4.0-dev.1` to `.3` (R9), because 003's lesson was that 0.0.0 fails .NET package validation;
- `Publish to PyPI` becomes `ls dist/` plus a check of the versions in the file names, instead of the
  upload;
- the safety grep also requires that no `pypa/gh-action-pypi-publish` step remains.

The three runs are:
1. **a failing Python job**: every publish job is skipped;
2. **everything green**: every stubbed publish step reports what it would upload;
3. **a mismatched tag**: every publish job fails at the tag check.

The YAML is validated locally before each push (003's lesson), and every dry-run ref is deleted
afterwards.

---

## R20. Running every supported Python locally

**Decision.** Use `uv` for local runs: `uv run --python 3.11 pytest`, and likewise for 3.12, 3.13, 3.14
and 3.14t. The CPython 3.11, 3.12, 3.13, 3.14 and 3.14t interpreters are already installed on the
maintainer's machine (2026-09-18). Quickstart §2 runs the corpus on all five before the pull request is
opened, as 003 R19 did for Node.js.
