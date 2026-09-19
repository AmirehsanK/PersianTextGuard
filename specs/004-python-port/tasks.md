---

description: "Task list for the Python port published to PyPI (release 1.4.0)"
---

# Tasks: Python Port Published to PyPI

**Input**: Design documents from `/specs/004-python-port/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/public-api.md](contracts/public-api.md), [contracts/package-and-release.md](contracts/package-and-release.md), [quickstart.md](quickstart.md)

**Tests**: Included. The spec requires them:
- a corpus runner (FR-016, FR-017);
- Python-specific tests run against the installed package (FR-019);
- a test for every README example (FR-024);
- thread tests on free-threaded CPython (FR-015, SC-008).

**Organization**: Phases follow the spec's user stories.
- **Foundational**: the new corpus cases for characters outside the Basic Multilingual Plane (research R2), which every port must pass, and the Python layers every story shares: types, Unicode, the UTF-16 view, the normalizer, the fold helpers, source maps and word lists.
- **US1**: the filter and the package users install.
- **US2**: the corpus runner, identical answers and thread safety.
- **US3**: CI and publishing.
- **US4**: documentation, benchmarks and API compatibility.
- **Release**: runs last, because it is irreversible, and starts with a real CI dry run.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4)
- Paths are relative to the repository root, `D:\Git\PersianTextGuard`. Commands under `python/` run with
  `python/` as the working directory.
- **Environment note** (research R20, quickstart §0): if `uv` fails with "cannot move the file to a
  different disk drive" (os error 17), export `TMP` and `TEMP` as `C:\Users\amire\AppData\Local\Temp`
  for that command. Interpreter shims in `~/.local/bin` resolve relative paths against the shell's
  starting directory, so pass them absolute paths.

## Porting conventions (apply to every task that ports a JavaScript file)

- **Source.** Port from `js/src/*.ts`, the faithful port of .NET that passes the corpus (research R7).
  When the TypeScript and the C# (`dotnet/src/PersianTextGuard/*.cs`) read differently, the C# wins.
  Keep the algorithm, control flow, constants, tables and comments, so the files can be reviewed side by
  side. Do not "improve" behaviour: the corpus is the specification.
- **Code units.** Inside the matcher, text is the UTF-16 **view** from `_utf16.py` (research R2): a
  `str` in which each supplementary code point is two surrogate code points. So `text[i]` is a unit,
  `len(text)` is the unit count, and `ord(text[i])` is `charCodeAt(i)`. The view never leaves the
  package. Every public entry point converts to the view on the way in and converts positions back on the
  way out.
- **Primitives.** Every Unicode-dependent operation goes through `_unicode.py`. That means never
  `str.lower`, `upper`, `casefold`, `isspace`, `isalpha`, `isalnum`, `isdigit`, `isdecimal`,
  `isnumeric`, `strip()`, `split()` or `splitlines()` without an explicit separator, and never
  `unicodedata` outside `_unicode.py`. Python's whitespace set differs from .NET's (research R1).
  `scripts/check_unicode_usage.py` enforces this (T007).

  | TypeScript (`unicode.ts`) | Python (`_unicode.py`) |
  | --- | --- |
  | `isWhiteSpace(unit)` | `is_white_space(c)` |
  | `isLetter(unit)` / `isLetterOrDigit(unit)` / `isControl(unit)` | `is_letter(c)` / `is_letter_or_digit(c)` / `is_control(c)` |
  | `isSurrogate` / `isHighSurrogate` / `isLowSurrogate` | `is_surrogate` / `is_high_surrogate` / `is_low_surrogate` |
  | `categoryOfUnit(unit)` / `categoryAt(text, index)` | `category_of_unit(c)` / `category_at(text, index)` |
  | `toLowerInvariant(unit)` | `to_lower_invariant(c)` |
  | `nfkc(s)` / `isNfkc(s)` / `nfd(s)` | `nfkc(s)` / `is_nfkc(s)` / `nfd(s)` |
  | `isNoncharacter(cp)` / `noncharacterLengthAt(text, i)` | `is_noncharacter(cp)` / `noncharacter_length_at(text, i)` |

  Here `c` is a one-character `str` (a unit of the view), and the functions take `str` rather than
  `int`, so hot loops index the string without calling `ord`.
- **Collections.** A TS array built by `push` and `join('')` becomes a `list[str]` and `''.join`. `Map`
  and `Set` become `dict` and `set` (`frozenset` once built). A `number[]` of positions becomes a
  `list[int]`, or an `array('i')` where it is large. `Object.freeze` becomes a tuple or a frozen
  dataclass.
- **Types.** Every function has full annotations. `mypy --strict` must pass. Internal records are
  `NamedTuple`s or slotted dataclasses; public ones follow [contracts/public-api.md](contracts/public-api.md).
- **Internals.** Modules are `_`-prefixed. `__init__.py` exports exactly the public API.

---

## Phase 1: Setup

**Purpose**: Scaffold `python/` with pinned tools, and record the starting point.

- [X] T001 Confirm the branch is `004-python-port` and that `git status --short` shows nothing except the untracked `graphify-out/`, which must never be committed. Record the baseline in a new `specs/004-python-port/verification.md` under the heading "Baseline":
  - the commit hash;
  - `dotnet test dotnet/tests/PersianTextGuard.Tests` (expect 1,029 on each of `net8.0`, `net10.0` and `net48`);
  - `dotnet test dotnet/tests/PersianTextGuard.Conformance` (expect 522 × 3);
  - in `js/`: `npm ci`, then `npm run test:all` (expect 665);
  - `uv --version`, and `uv python list --only-installed` (expect 3.11, 3.12, 3.13, 3.14 and 3.14t).
- [X] T002 Append the Python build outputs to the root `.gitignore`:
  - `python/.venv/`, `python/dist/`, `python/build/` and `python/.bench/`;
  - `python/src/persian_text_guard/_wordlists.py` and `python/src/persian_text_guard/_version.py`;
  - `__pycache__/`, `.pytest_cache/`, `.mypy_cache/` and `.ruff_cache/`;
  - `python/consumers/.envs/`.
- [X] T003 Create `python/pyproject.toml`:
  - **`[build-system]`**: `requires = ["hatchling>=1.32,<2"]`, `build-backend = "hatchling.build"`.
  - **`[project]`**:
    - `name = "persian-text-guard"`, `dynamic = ["version"]`, `requires-python = ">=3.11"`, `dependencies = []`;
    - `description`: the npm description, adapted;
    - `readme = "README.md"`, `license = "MIT"`, `license-files = ["LICENSE", "THIRD-PARTY-NOTICES.md"]` (both copied in by the build hook, T004);
    - `authors = [{ name = "Amirehsan Kohannasab" }]`;
    - `keywords`: the npm keywords, with `python` in place of `typescript`;
    - `classifiers`: `Development Status :: 5 - Production/Stable`, `Intended Audience :: Developers`, `Natural Language :: Persian`, `Natural Language :: English`, `Operating System :: OS Independent`, `Programming Language :: Python :: 3 :: Only`, `Programming Language :: Python :: 3.11` to `3.14`, `Programming Language :: Python :: Free Threading :: 3 - Stable`, `Topic :: Text Processing :: Linguistic` and `Typing :: Typed`;
    - `[project.urls]`: `Homepage = "https://github.com/AmirehsanK/PersianTextGuard/tree/main/python#readme"`, `Source = "https://github.com/AmirehsanK/PersianTextGuard"`, `Issues = "https://github.com/AmirehsanK/PersianTextGuard/issues"`.
  - **Hooks**: `[tool.hatch.metadata.hooks.custom]` and `[tool.hatch.build.hooks.custom]`, both with `path = "hatch_build.py"`. There is **no** `[tool.hatch.version]` table: hatchling 1.32 has no custom version source (only `code`, `env` and `regex`), so the metadata hook sets `version`, which `dynamic` lists (research R4).
  - **Targets**:
    - `[tool.hatch.build.targets.wheel]`: `packages = ["src/persian_text_guard"]`, and `artifacts` listing the two generated files, which are git-ignored but must be included;
    - `[tool.hatch.build.targets.sdist]`: `only-include = ["src/persian_text_guard", "pyproject.toml", "hatch_build.py", "README.md", "LICENSE", "THIRD-PARTY-NOTICES.md"]`, and the same `artifacts`.
  - **`[dependency-groups] dev`**: `pytest`, `pytest-run-parallel`, `mypy`, `pyright`, `ruff`, `pyperf`, `griffe`, `build`, `twine`, `check-wheel-contents`, `readme-renderer[md]`, each with a lower bound at the version in plan.md.
  - **`[tool.ruff]`**: `line-length = 110`, `target-version = "py311"`, `src = ["src", "tests"]`.
    - `lint.select = ["E", "F", "W", "I", "UP", "B", "SIM", "RUF", "D", "ANN", "PL", "PT", "TID"]` and `lint.pydocstyle.convention = "google"`;
    - `lint.flake8-tidy-imports.banned-api`: `"unicodedata".msg = "Use persian_text_guard._unicode (spec 004 research R1)"`;
    - `lint.per-file-ignores`: `"src/persian_text_guard/_unicode.py" = ["TID251"]`, `"tests/**" = ["D", "ANN", "PLR2004", "TID251"]`, and `"tools/**"`, `"scripts/**"` and `"bench/**"` = `["D"]`.
  - **`[tool.mypy]`**: `strict = true`, `python_version = "3.11"`, `files = ["src", "tests", "scripts", "bench", "consumers/typed_usage.py"]`.
  - **`[tool.pytest.ini_options]`**:
    - `testpaths = ["tests"]` and `addopts = "-ra --strict-markers --import-mode=importlib"`;
    - `markers = ["corpus: a conformance corpus case or guard", "threads: multi-threaded tests"]`;
    - `xfail_strict = true`.
- [X] T004 Create `python/hatch_build.py` (research R4, R6, R9), with a module docstring stating why it exists.
  - **`_repo_root()`**: `Path(__file__).resolve().parent.parent` if it has `VERSION` and `wordlists/`, else `None` (building from an unpacked sdist).
  - **`CustomMetadataHook(MetadataHookInterface)`**, `update(metadata)`:
    - with a repository root, read `VERSION`, strip it, and set `metadata["version"] = str(packaging.version.Version(raw))`, so `1.4.0-dev.2` becomes `1.4.0.dev2`. Raise `ValueError` naming the file if PEP 440 rejects it;
    - without one (an unpacked sdist), read `__version__` from `src/persian_text_guard/_version.py`, and fail with a clear message if the file is missing;
    - with a repository root, also copy the root `LICENSE` and `THIRD-PARTY-NOTICES.md` into `python/` (git-ignored). Hatchling resolves `license-files` after metadata hooks run (checked in hatchling 1.32's `metadata/core.py`), so the files exist in time.
  - **`CustomBuildHook(BuildHookInterface)`**, `initialize()`:
    - with a repository root, write `src/persian_text_guard/_wordlists.py` containing a module docstring saying "Generated by hatch_build.py from wordlists/*.txt; do not edit" and `BUNDLED_WORD_LISTS: tuple[str, ...] = (...)`, with the text of `wordlists/fa.txt`, `finglish.txt` and `en.txt` **in that order** (the .NET embedding order) as `repr()` literals, read as UTF-8 and with `\r\n` normalized to `\n`;
    - write `_version.py` with `__version__ = "<PEP 440 version>"`;
    - without a repository root, require the two generated files to exist, and fail naming the missing one otherwise;
    - each file is written only when its content changes, so editable installs don't churn.

  Add `python/LICENSE` and `python/THIRD-PARTY-NOTICES.md` to `.gitignore` too.
- [X] T005 [P] Create the package skeleton:
  - `python/src/persian_text_guard/py.typed`, which is empty;
  - a temporary `python/src/persian_text_guard/__init__.py` with only a module docstring and `from ._version import __version__`.
- [X] T006 Pin the tools. In `python/`, run `uv lock`, then `uv sync --locked`. Confirm:
  - `.venv` exists;
  - `_wordlists.py` and `_version.py` were generated, and `git status` does not list them;
  - `uv run python -c "import persian_text_guard as p; print(p.__version__)"` prints `1.3.0`, the current `VERSION`.

  Commit `python/uv.lock`.
- [X] T007 [P] Create `python/scripts/check_unicode_usage.py`, an `ast` walker over `src/persian_text_guard/*.py` except `_unicode.py` and the generated files. It fails listing `file:line` for:
  - any attribute call named `lower`, `upper`, `casefold`, `title`, `swapcase`, `isspace`, `isalpha`, `isalnum`, `isdigit`, `isdecimal`, `isnumeric`, `isprintable`, `isidentifier`, `islower`, `isupper` or `istitle`;
  - `strip`, `lstrip`, `rstrip`, `split`, `rsplit` or `splitlines` called with **no** arguments;
  - `import unicodedata` (also banned by ruff; checked twice on purpose);
  - `re` patterns containing `\s`, `\S`, `\w`, `\W`, `\d` or `\D` in a string literal passed to `re.compile`, `re.search`, `re.match`, `re.sub`, `re.split`, `re.findall` or `re.fullmatch`.

  **Self-check**: a throwaway `src/persian_text_guard/_x.py` containing `"A".lower()`, `" a ".strip()` and `re.compile(r"\s")` makes it report all three. Then delete the file.
- [X] T008 Add the lint command to `verification.md` and confirm it runs clean on the skeleton: `uv run ruff check && uv run ruff format --check && uv run python scripts/check_unicode_usage.py && uv run mypy`. Commit T002–T008 as "Scaffold the Python port (spec 004)".

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Pin the behaviour research R2 found for every port, and build the layers every story uses.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### Corpus: characters outside the Basic Multilingual Plane (research R2)

- [X] T009 Add pending cases (no `expected`) to `conformance/cases/robustness.json`, each `"kind": "robustness"` and `"configuration": "default"`. The first case gets `"note": "Characters outside the Basic Multilingual Plane are read as UTF-16 units, as .NET reads them (spec 004 research R2)."`. Write visible characters literally (they are not invisible under writing rule 1). Ids and inputs:

  | Id | Input |
  | --- | --- |
  | `robustness-supplementary-letter-after-word` | `"kir𠀀"` |
  | `robustness-supplementary-letter-before-word` | `"𠀀kir"` |
  | `robustness-supplementary-letter-separate-word` | `"kir 𠀀"` |
  | `robustness-supplementary-letter-inside-word` | `"k𠀀ir"` |
  | `robustness-emoji-after-persian-word` | `"کیر😀"` |
  | `robustness-mathematical-bold-letters` | `"𝐤𝐢𝐫"` |
  | `robustness-deseret-letter-inside-word` | `"fu𐐀ck"` |
  | `robustness-supplementary-letter-inside-persian-word` | `"ک𠀀یر"` |

  Also add two **ordinary** pending cases (constitution Principle I: pin the ordinary text next to the new behaviour), `"configuration": "default"`:

  | File | Id | Kind | Input |
  | --- | --- | --- | --- |
  | `conformance/cases/matching-persian.json` | `fa-ordinary-with-supplementary-characters` | `ordinary` | `"سلام 😀 دوست 𠀀 عزیز"` |
  | `conformance/cases/matching-english.json` | `en-ordinary-with-supplementary-characters` | `ordinary` | `"hello 𠀀 world 𝐡𝐞𝐥𝐥𝐨"` |

  If .NET flags either one when filling (T010), stop and report it: that would be a false positive for Principle I, and it needs the user's decision.

- [X] T010 Fill and verify:
  1. `dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill` prints "Filled 10 case(s); 0 disagreement(s)".
  2. **Review the diff.** The first seven must equal research R2's table, with positions in code points: for example, `kir𠀀` records `start` 0 and `length` 4, and censors to `****`. The eighth is new, so record what .NET does. The two ordinary cases record no match, and censoring leaves them unchanged. If any of the first seven differs from R2, stop and report it: it would mean the JavaScript port and .NET disagree outside the corpus.
  3. Run `dotnet test dotnet/tests/PersianTextGuard.Conformance` (532 × 3, which is 523 cases plus 9 guards), and in `js/` run `npm run corpus` (530, which is 523 cases plus 7 guards). If the JavaScript port fails a new case, stop and report the case id and difference to the user. Fixing JavaScript is in scope, but only after their decision.
  4. `dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill -- --check` reports 0 disagreements.

  Record the counts in `verification.md` and commit as "Pin characters outside the BMP in the corpus".

### Python: shared layers

- [X] T011 [P] Create `python/src/persian_text_guard/_types.py` with the enumerations, `BannedWord`, `ProfanityFilterOptions`, `ProfanityMatch`, `WordListFormatError` and the `Literal` name aliases, exactly as in [contracts/public-api.md](contracts/public-api.md) and [data-model.md](data-model.md):
  - `StrEnum` members and values as in data-model.md → "Enumerations".
  - `BannedWord`: `@dataclass(frozen=True, slots=True, init=False)`, with a hand-written `__init__(text, mode=WordMatchMode.WHOLE_WORD, category=WordCategory.UNCATEGORIZED)` that:
    - raises `TypeError` if `text` is not a `str`, and stores `str(text)`;
    - converts a mode or category given as a name with the coercion helpers below;
    - sets the fields with `object.__setattr__`.
  - `ProfanityFilterOptions`: `@dataclass(frozen=True, slots=True, kw_only=True)` with three `bool` fields defaulting to `True`. `__post_init__` raises `TypeError` for a value that is not a `bool` ("a truthy string would be a silent mistake").
  - `ProfanityMatch`: `@dataclass(frozen=True, slots=True)` with `word`, `evasion: tuple[EvasionKind, ...]`, `index`, `length`.
  - `WordListFormatError(ValueError)`, with `__init__(self, message: str, line: int)` and a `line` attribute.
  - Internal helpers: `to_mode(value)`, `to_category(value)` and `to_step(value)`. Each accepts the member or its value string, and raises `ValueError("unknown word category 'x'; expected one of: ...")` otherwise, or `TypeError` for a non-`str`. Also `require_text(value, name) -> str | None`, which returns `None` for `None`, `str(value)` for a `str`, and otherwise raises `TypeError(f"{name} must be a str or None, not {type(value).__name__}")` (research R18).
  - Google-style docstrings on every public class and member, explaining behaviour and edge cases (FR-023).
- [X] T012 [P] Create `python/src/persian_text_guard/_unicode.py`, implementing research R1 exactly, as a port of `js/src/unicode.ts` using the names in the porting table.
  - `category_of_unit(c)`: `unicodedata.category(c)` with a module-level `dict` cache written with `setdefault` (research R17). A surrogate unit is `"Cs"`.
  - `category_at(text, i)`: when `text[i]` is a high surrogate followed by a low surrogate, combine them (`chr(0x10000 + ((hi - 0xD800) << 10) + (lo - 0xDC00))`) and categorize that code point.
  - `is_white_space(c)`: exactly `c in "\t\n\v\f\r \x85\xa0"`, or a category in `("Zs", "Zl", "Zp")`. **Never** `str.isspace`, which also counts U+001C–U+001F.
  - `to_lower_invariant(c)`: ASCII fast path; otherwise `c.lower()` if that is exactly one character, else `c` (U+0130 stays U+0130).
  - `is_noncharacter(cp)` and `noncharacter_length_at(text, i)`, which treats a surrogate pair in the view as one code point.
  - `nfkc(s)`: the input is a view; recombine valid surrogate pairs into code points, normalize the runs between noncharacters with `unicodedata.normalize("NFKC", …)`, copy noncharacters unchanged, then split every result code point above U+FFFF back into surrogates. Lone surrogates are copied unchanged (the caller replaces them first, as in JavaScript). Also `is_nfkc(s)` and `nfd(s)`, which uses the same recombine and split.
  - `replace_lone_surrogates(s)` if `normalizer.ts` has it; otherwise follow wherever the TypeScript does the U+FFFD replacement.
- [X] T013 [P] Create `python/src/persian_text_guard/_utf16.py` (research R2):
  - `SUPPLEMENTARY = re.compile("[\U00010000-\U0010FFFF]")`;
  - `to_view(text) -> str`: return `text` unchanged when `text.isascii()` or `SUPPLEMENTARY.search(text) is None`; otherwise build the string with each code point above U+FFFF replaced by its two surrogates;
  - `class PositionMap`: built from the caller's text only when the view differs. `to_code_points(unit_index, unit_length) -> tuple[int, int]` uses a prefix table from each unit index to its code-point index; a unit that is the second half of a pair maps to its pair's start. It is the identity when the view equals the text;
  - `from_view(view) -> str`, which recombines valid surrogate pairs, for normalizer and tokenizer outputs. Document why the caller's own split surrogates stay split in `censor` (it splices the original) but become one code point in normalizer output, which is what .NET does when it round-trips through UTF-16;
  - docstrings with R2's `kir𠀀` example.
- [X] T014 [P] Create `python/tests/test_unicode.py` with the research R1 findings and the R2 view as tests:
  - U+0085 is whitespace and U+001C, U+FEFF and U+200B are not;
  - `to_lower_invariant("İ") == "İ"`, and `to_lower_invariant("Σ") == "σ"`;
  - `category_of_unit("\ud800") == "Cs"`, and `category_at("\ud840\udc00", 0) == "Lo"` for U+20000;
  - `nfkc` of a view containing U+1D424 (𝐤) is `"k"`, and `nfkc("a\ufffeb")` keeps U+FFFE;
  - `to_view("kir𠀀") == "kir\ud840\udc00"`, and `to_view("abc") is "abc"`;
  - `PositionMap` maps unit `(0, 5)` to code points `(0, 4)` for `kir𠀀`, and is the identity for a string with the caller's own split surrogates `"k\ud840\udc00"`, whose view has the same length.
- [X] T015 Port `js/src/normalizer.ts` to `python/src/persian_text_guard/_normalizer.py`, following the porting conventions.
  - **Internals**: the step implementations, presets, source-mapped segment path and `tokenize` core take a view.
  - **Public functions**: `normalize(text, steps="comparison")`, `tokenize(text)`, `to_persian_digits(text)` and `to_ascii_digits(text)`. Each calls `require_text` first; `None` gives `""` (or `[]` for `tokenize`); then it converts to the view, runs, and converts results back with `from_view`.
  - **Steps**: `"comparison"`, `"standard"` or `"none"`, or any iterable of steps as members or names. An unknown preset or step raises `ValueError`. A plain `str` that is not a preset is **not** iterated character by character: raise `ValueError`.
  - **Speed** (research R3): digit and letter unification that maps one unit to one unit uses a module-level `str.maketrans` table and `str.translate`, where the TypeScript loops unit by unit and the mapping is context-free.
- [X] T016 Port `js/src/word-list.ts` to `python/src/persian_text_guard/_word_list.py`: `class WordList` with static methods only, whose `__init__` raises `TypeError("WordList is not instantiable")`.
  - **`parse(text)`**: `require_text`; `None` raises `TypeError` (data-model.md: "there is no missing word list"). Keep the .NET trimming semantics via the TypeScript's `trimDotNet`, ported as `_trim_dot_net`, which uses `is_white_space`. Headings are category names only, in any case, with spaces allowed; `[3]` raises `WordListFormatError(f"Line {n}: unknown word category '{name}'.", n)`.
  - **Bundled lists**: `all()`, `persian_default()` and `bundled(*categories)` parse `_wordlists.BUNDLED_WORD_LISTS` once, under a module-level `threading.Lock` with double-checked initialization (research R17), and cache tuples. `persian_default()` excludes `MILD`. `bundled` returns a new tuple in list order, and raises `ValueError` for an unknown category.
  - **`load(source)`** (spec Clarifications):
    - a `str` or `os.PathLike` path is opened, read as bytes, and decoded with `"utf-8-sig"`;
    - a file object whose `read()` returns `bytes` is decoded the same way;
    - one whose `read()` returns `str` has a leading `"\ufeff"` removed;
    - the file is never closed by `load`, and `OSError` and `UnicodeDecodeError` propagate unchanged;
    - then `parse`.
- [X] T017 [P] Port `js/src/fold.ts` to `python/src/persian_text_guard/_fold.py`, the fold, squeeze and character-class helpers. Tables become module-level `dict`s or `frozenset`s built at import.
- [X] T018 Port `js/src/source-map.ts` to `python/src/persian_text_guard/_source_map.py`: `MappedText` as a slotted dataclass (`text`, `start_map`, `end_map`) and `ReadingKind` as an `IntEnum`. Positions are view units.
- [X] T019 Port `js/test/internals.test.ts` to `python/tests/test_internals.py`, test for test, importing from the underscore modules. Every test passes.
- [X] T020 Run the lint command from T008 and `uv run pytest tests/test_unicode.py tests/test_internals.py`. Commit T011–T020 as "Port the types, Unicode layer, UTF-16 view, normalizer, word lists, fold helpers and source maps to Python".

**Checkpoint**: The shared layers pass their tests, and every port has the new corpus cases.

---

## Phase 3: User Story 1 - Check and censor messages from Python (Priority: P1) 🎯 MVP

**Goal**: The filter as users install it: `ProfanityFilter`, `WordList`, normalization, in a built wheel and sdist, typed.

**Independent Test**: spec US1: from the built wheel in a fresh environment, `ک.ی.ر` is flagged, `سلام، سفارشم کی میرسه؟` is not, and `kir and motherfucker` censors to `**** and ****`. A strict type check accepts the quick start.

### Tests for User Story 1 (write first; they fail until T024–T027)

- [X] T021 [P] [US1] Create `python/tests/test_api.py`. It imports only from `persian_text_guard` and covers spec US1 scenarios 1–7 and guarantees P1, P2 and P4–P9 and P12 from [contracts/public-api.md](contracts/public-api.md):
  - **Scenarios**:
    - scenario 3 exactly: `shit`, index 0, length 4, `(EvasionKind.LOOKALIKE_CHARACTERS,)`; and `fuck`, index 9, length 7, `(EvasionKind.SPLIT_WORD,)`;
    - scenario 4: `"😀 کیر"` gives index 2, length 3, and the slice equals `"کیر"`;
    - scenario 5: `BannedWord("اسپم")` with `BannedWord("casino", "anywhere")` flags `"onlinecasino.example"`;
    - scenario 6: `#` gives `"this is ####"`, and `"x"` raises `ValueError`.
  - **P1, `None` and edge-case strings**: for `None`, `""`, `"   "`, `"\ud800"`, `"k\ud840\udc00ir"` (the caller's own split pair), `"\ufffe"` and a 132,000-character string, every text function returns.
  - **P2, non-strings**: `b"kir"`, `5`, `{}` and `["kir"]` raise `TypeError` from each of `contains_profanity`, `find_match`, `find_matches`, `censor`, `normalize`, `tokenize`, `to_persian_digits`, `to_ascii_digits` and `WordList.parse`. A `str` subclass that overrides `__getitem__` gives the same results as the plain string.
  - **P5 on supplementary text**: for `"kir𠀀 and fuck"`, the matches, `index` and `length` slice the caller's string; `censor` leaves the caller's split surrogates untouched in unmasked text.
  - **P6, masks**: `censor(None, "x")` raises `ValueError` and `censor(None, 5)` raises `TypeError`. `"😀"`, `"ab"`, `""`, `" "`, `"\n"`, `"5"` and `"\ud800"` are rejected; `"#"`, `"*"` and `"■"` are accepted.
  - **P7, immutability**: mutate the list passed to the constructor afterwards, and results and `count` are unchanged. Assigning to `match.index` raises `FrozenInstanceError`; `find_matches` returns a tuple; options are frozen.
  - **P8**: `WordList.all() is WordList.all()`, and no `MILD` in `persian_default()`.
  - **P9, headings**: `WordListFormatError` for `"[3]\nword\n"` has `line == 1`, and is a `ValueError`.
  - **P12, names**: `WordList.bundled("slur") == WordList.bundled(WordCategory.SLUR)`, `BannedWord("x", category="nope")` raises `ValueError`, and `ProfanityFilterOptions(squeeze_repeated_letters="no")` raises `TypeError`.
  - **Constructors**: `ProfanityFilter(None)` and `ProfanityFilter(["kir"])` raise `TypeError`, the second naming position 0.
  - **P4**: over a handful of inputs, `contains_profanity`, `find_match` and `find_matches` agree, and `censor` changes the text exactly when there is a match. T037 extends this to every corpus input.
- [X] T022 [P] [US1] Create `python/tests/test_word_list_load.py` (P9, spec Clarifications), using `tmp_path`:
  - a UTF-8 file with a BOM and `\r\n`, loaded by `str` path, by `Path`, by an open binary file and by an open text file with `encoding="utf-8"`: all four equal `WordList.parse` of the same text without the BOM;
  - the open file is still open afterwards (`not f.closed`);
  - a missing path raises `FileNotFoundError` naming the path;
  - invalid UTF-8 bytes raise `UnicodeDecodeError`;
  - a file with `[3]` raises `WordListFormatError` with the right line.

### Implementation for User Story 1

- [X] T023 [US1] Port the token-level reading machinery of `js/src/scan.ts` to `python/src/persian_text_guard/_scan.py`: everything that does not need the filter's state (readings, candidate tokens, joining spaced letters). Follow the porting conventions; positions are view units.
- [X] T024 [US1] Complete `_scan.py` with the filter-dependent scan functions, as in `js/src/scan.ts`, taking the filter's internal state as an argument or as methods on an internal class.
- [X] T025 [US1] Port `js/src/regions.ts` to `python/src/persian_text_guard/_regions.py`: candidates, merging, and censoring over the view. The mask length is 4 (`MaskLength = 4` in .NET). Censoring returns **regions**; `_filter.py` splices them.
- [X] T026 [US1] Port `js/src/filter.ts` to `python/src/persian_text_guard/_filter.py`: `class ProfanityFilter` with `__slots__` and no setters (data-model.md → `ProfanityFilter`).
  - **`__init__(words, options=None)`**:
    - `None` or a non-iterable raises `TypeError`, and a non-`BannedWord` element raises `TypeError` naming its position; `options` must be `None` or a `ProfanityFilterOptions`;
    - build every table as the TypeScript does, then store `frozenset`, `tuple` or `dict` values that are never mutated afterwards.
  - **`count`**: a property.
  - **Text methods**: `contains_profanity`, `find_match`, `find_matches` and `censor(text, mask="*")`. Each:
    1. validates `mask` first, for `censor` (P6): a non-`str` raises `TypeError`; a mask whose length is not 1 or that is above U+FFFF, a surrogate, `is_letter_or_digit`, `is_white_space` or `is_control` raises `ValueError`, with the .NET message;
    2. calls `require_text` (`None` gives `False`, `None`, `()` or `""`);
    3. builds the view and runs the ported logic;
    4. converts positions with `PositionMap` into `ProfanityMatch(word, evasion, index, length)`, where `word` is the caller's own `BannedWord` object and `evasion` is a tuple in declaration order.
  - **`censor`**: splices the caller's **original** string, replacing each region, converted to code points, with `mask * 4` (research R2).
- [X] T027 [US1] Replace `python/src/persian_text_guard/__init__.py` with the public surface: a module docstring, and imports of exactly the names in [contracts/public-api.md](contracts/public-api.md) → "Declarations", plus `__version__`. `__all__` lists them, sorted. Run `uv run pytest tests/test_api.py tests/test_word_list_load.py` until every test passes.
- [X] T028 [P] [US1] Create a placeholder `python/README.md` with the title and one English quick-start example, because the build needs a README. T049 writes the full one.
- [X] T029 [P] [US1] Create the consumer files (research R12):
  - `python/consumers/smoke.py`: the README quick start with `assert`s, printing `ok`;
  - `python/consumers/typed_usage.py`: a strictly typed use of every public name;
  - `python/consumers/type_errors.py`, with exactly two errors, each marked with a trailing comment `# expect-error: <code>`: `ProfanityFilterOptions(squeeze_repeated=False)` (an unknown option name) and `WordList.bundled("rude")` (an unknown category name).
- [X] T030 [US1] Create `python/scripts/check_package.py` (research R12, contracts → "Package contents"):
  1. finds exactly one wheel and one sdist in `dist/`;
  2. runs `twine check --strict` on both, and `check-wheel-contents` on the wheel;
  3. compares the wheel's and the sdist's file lists with the allowlists exactly;
  4. parses `METADATA`: `Name`, `Version` equal to the PEP 440 form of `VERSION`, `Requires-Python: >=3.11`, no `Requires-Dist`, `License-Expression: MIT`, and both `License-File` entries;
  5. checks that the unpacked wheel is under 1 MB (SC-004).

  It prints each check and exits non-zero on the first failure.
- [X] T031 [US1] Create `python/scripts/check_consumers.py`:
  1. makes `consumers/.envs/wheel` and `consumers/.envs/sdist` with `python -m venv`;
  2. installs `dist/*.whl` into the first with `pip install --no-deps --no-index`. For the second, first builds a wheel **from the sdist alone** with `uv build --wheel dist/<sdist> -o consumers/.envs/from-sdist`, which unpacks the sdist in isolation and fetches hatchling for the build, proving that the sdist needs nothing from the repository. It then installs that wheel with `pip install --no-deps --no-index`. A plain `pip install --no-index dist/*.tar.gz` would fail, because build isolation must download hatchling;
  3. runs `smoke.py` in each and expects `ok`;
  4. runs `pyright --strict` and `mypy --strict` on `typed_usage.py`, against the wheel environment's site-packages, and expects 0 errors;
  5. runs both checkers on `type_errors.py` and expects exactly the two marked errors, on the marked lines.

  It prints 4 of 4 checks: wheel smoke, sdist smoke, typed usage and type errors. The first two are SC-007's "2 of 2" installs; the last two are its strict type check.
- [X] T032 [US1] Run `uv build`, `uv run python scripts/check_package.py` and `uv run python scripts/check_consumers.py`. Record in `verification.md` under "US1": the test results, both file names, both file lists, the unpacked size and the 4 consumer results. Commit T021–T032 as "Add the Python filter, package checks and consumer checks".

**Checkpoint**: The package installs from a wheel and an sdist, and works and type-checks for users (MVP). Spec US1 scenario 8 (many threads, one filter) needs the corpus inputs, so it is verified in T039 (US2).

---

## Phase 4: User Story 2 - The same answer as every other port, proven by the corpus (Priority: P1)

**Goal**: All 523 corpus cases pass on every supported Python, the bundled lists match, and one filter is safe across threads.

**Independent Test**: spec US2: `uv run pytest -m corpus` passes on 3.11–3.14 and 3.14t; a corrupted case reports its id, file, visible input and differences.

- [X] T033 [P] [US2] Create `python/tests/corpus/load.py`, a port of `js/test/corpus/load.ts`:
  - `find_repository_root()` walks up from `__file__` to a directory with `VERSION` and `wordlists/`, the same rule as JavaScript;
  - `load_corpus(directory)` reads `corpus.json`, `configurations.json` and `cases/*.json` in ordinal file-name order, with `json.load(encoding="utf-8")`. It raises `CorpusError` naming the file for every condition the TypeScript throws on. A missing directory gives the message "Conformance corpus not found: <path>".

  Add `python/tests/corpus/__init__.py`.
- [X] T034 [P] [US2] Create `python/tests/corpus/values.py`, a port of `js/test/corpus/values.ts`:
  - `build_input(node)`: `str`, `None`, or `{"build": [...]}` with `text`, `repeat` × `times` and `utf16` parts. The `utf16` parts decode into the corresponding code points; a lone surrogate stays a lone surrogate code point;
  - `is_text` and `display`;
  - `show_invisible(text)`: escapes Cf, Cc, Zl, Zp, whitespace other than U+0020, lone surrogates and noncharacters as `\uXXXX`, or `\U000XXXXX` above U+FFFF, using `_unicode`;
  - `compare(expected, actual)`, which returns a list of `(path, expected, actual)`. Positions need **no** conversion, since both are code points (P3).
- [X] T035 [US2] Create `python/tests/corpus/evaluate.py`, a port of `js/test/corpus/evaluate.ts`, against the public `persian_text_guard` API:
  - one filter per configuration, cached;
  - the same result shapes, with positions used as returned;
  - mask validation: `accepted` is `False` exactly when `censor("kir", mask)` raises `ValueError`. A `build` mask becomes a `str`;
  - `check_kind_rules(case)`, with .NET's violation wording.
- [X] T036 [US2] Create `python/tests/test_corpus.py` (marked `corpus`):
  - **Loading**: the corpus loads once, at module level.
  - **Cases**: `pytest.mark.parametrize` over every case, with `ids=[case.id …]`. Each test checks pending, then the kind rules ("breaks its kind rule"), then `compare`. It fails with `pytest.fail` and the message format of 003 T046: `Case '<id>' in <file>: <problem>`, then `  input "<show_invisible(input)>"`, then one `  <path>: expected <e> actual <a>` line per difference (FR-016, spec US2 scenario 2).
  - **Guards**, in `python/tests/test_corpus_guards.py`, one test each:
    - the corpus loads;
    - `formatVersion == 1`, and a newer version is refused;
    - at least 300 cases;
    - unique ids matching `^[a-z0-9]+(-[a-z0-9]+)*$`;
    - no pending case, with a message naming the ids and the fill command;
    - every configuration exists;
    - 0 not-applicable cases.
- [X] T037 [US2] Run `uv run pytest -m corpus`: every case passes (523 cases plus the guards).
  - **Failures**: for each failing case, find the root cause by comparing the Python module with the TypeScript and C# it ports, and fix the port. **Never** edit the corpus to match the port.
  - **Unicode differences**: if a failure is a genuine Unicode-data difference (research R1), stop and report it with the case id, the code points and the Python version.

  Also extend `test_api.py` with P4 over every corpus input (the consistency of `contains_profanity`, `find_match`, `find_matches` and `censor`). Record the pass count, Python version and run time in `verification.md`.
- [X] T038 [US2] Run the full suite on every supported Python (research R20, quickstart §2):
  - `uv run --python 3.11 pytest`, and likewise for 3.12, 3.13 and 3.14;
  - `PYTHON_GIL=0 uv run --python 3.14t pytest`.

  Every run passes. A failure on only some versions is a Unicode-data difference: stop and report it with the case id, code points and versions. Record `sys.version`, `unicodedata.unidata_version` and the pass counts per version in `verification.md`.
- [X] T039 [US2] Create `python/tests/test_threads.py` (marked `threads`; FR-015, SC-008, P8, P11):
  - build one filter per corpus configuration, then run a single-threaded pass over every corpus matching input, recording `contains_profanity`, `find_matches` and `censor`;
  - start 8 threads with a `threading.Barrier(8)`; each checks every input twice and compares with the single-threaded results;
  - separately, 8 threads call `WordList.all()` at the same moment, on a fresh interpreter state (a subprocess importing the package), and all receive the same object (`id` equal);
  - on 3.14t, assert `sys._is_gil_enabled() is False` when `PYTHON_GIL=0` is set.

  Run it on 3.14 and with `PYTHON_GIL=0` on 3.14t. Also run `PYTHON_GIL=0 uv run --python 3.14t pytest -p pytest_run_parallel --parallel-threads=8 -m "not corpus and not threads"`: every test passes. Mark tests that use `tmp_path` or monkeypatching `thread_unsafe` where the plugin requires it.
- [X] T040 [US2] Check failure reporting on scratch edits, reverted afterwards with `git checkout -- conformance`:
  1. Change `fa-emoji-before-word`'s `expected.censored` to `"😀 ####"`, and set `matching-persian-ordinary-messages-pass-001`'s `containsProfanity` to `true`. `uv run pytest -m corpus` fails exactly those 2 tests in one run, with the T036 messages; the second says "breaks its kind rule".
  2. Rename `conformance/` to `conformance.off`. The run fails with "Conformance corpus not found". Rename it back.

  Record both outputs in `verification.md`.
- [X] T041 [US2] Cross-check the bundled selections with JavaScript (FR-018, SC-002, quickstart §3). Run both one-liners and diff their output: the lines must be identical. Also dump every entry, as `text\tmode\tcategory` per line, from both ports into `artifacts/compare/python.txt` and `artifacts/compare/js.txt`. `git diff --no-index` shows no differences. Record the counts and "0 differences" in `verification.md`. Commit T033–T041 as "Run the conformance corpus against the Python port".

**Checkpoint**: Three ports pass one corpus. The Python port is behaviourally complete and thread-safe.

---

## Phase 5: User Story 3 - Released to PyPI together with NuGet and npm (Priority: P2)

**Goal**: CI builds, tests and gates the Python port, and a tag publishes all three packages together.

**Independent Test**: spec US3. Local checks first (T045); the real proof is the CI dry run (T058–T061).

- [X] T042 [US3] **Do this after T048 and T052**, whose scripts the job runs. Execution order is T048 and T052 (US4) before T042 and T043, although the phases are listed by story priority. Add the Python jobs to `.github/workflows/ci.yml`, leaving every existing job name unchanged (research R13, contracts → "CI"):
  - **Job `python`**, `name: Python (${{ matrix.python }})`, `runs-on: ubuntu-latest`:
    - `strategy.fail-fast: false`, `matrix.python: ["3.11", "3.12", "3.13", "3.14", "3.14t"]`, `defaults.run.working-directory: python`;
    - steps: `actions/checkout@v5` with `fetch-depth: 0`; `astral-sh/setup-uv` (the current major, pinned) with `python-version: ${{ matrix.python }}`, `enable-cache: true` and `cache-dependency-glob: python/uv.lock`; `uv sync --locked`.
  - **On every version except 3.14t**:
    - `uv run ruff check`, `uv run ruff format --check`, `uv run python scripts/check_unicode_usage.py` and `uv run mypy`;
    - `uv run pytest -m "not corpus"` and `uv run pytest -m corpus`.
  - **On 3.14t**:
    - `env: PYTHON_GIL: "0"`;
    - `uv run pytest`;
    - `uv run pytest -p pytest_run_parallel --parallel-threads=8 -m "not corpus and not threads"`.
  - **On 3.14 only** (`if: matrix.python == '3.14'`):
    - `uv build`, `uv run python scripts/check_package.py` and `uv run python scripts/check_consumers.py`;
    - `uv run python scripts/check_api.py` and `uv run python scripts/bench_gate.py`;
    - then `actions/upload-artifact@v4` with `name: python-package` and `path: python/dist/*`.

  Validate the file locally with the `yaml` package check from 003's dry run: the scratchpad `yamlcheck/check.mjs`, or `npx yaml valid`.
- [X] T043 [US3] Add the PyPI publish job, and gate the others (research R10):
  - **`publish` (NuGet) and `publish-npm`**: `needs: [build, netfx, javascript, python]`.
  - **New `publish-pypi`**, `name: Publish to PyPI`:
    - `needs: [build, netfx, javascript, python]`, `if: startsWith(github.ref, 'refs/tags/v')`, `runs-on: ubuntu-latest`, `environment: pypi`, `permissions: { contents: read, id-token: write }`;
    - steps:
      1. checkout;
      2. "Check tag matches VERSION", the same command as the other publish jobs;
      3. `actions/download-artifact@v4` (`python-package` → `dist`);
      4. "Check file versions": `ls dist`, then a Python one-liner that converts `VERSION` with `packaging` (`pip install packaging` first) and checks that both file names carry it;
      5. `pypa/gh-action-pypi-publish@release/v1` with `skip-existing: true`, and no `password`.
  - Validate the YAML as in T042.
- [X] T044 [US3] Move .NET's compatibility baseline to the previous release (research R15). In `dotnet/src/PersianTextGuard/PersianTextGuard.csproj`, set `PackageValidationBaselineVersion` to `1.3.0`. Run `dotnet pack dotnet/src/PersianTextGuard -c Release -o artifacts`: package validation passes against 1.3.0. Confirm that `js/scripts/check-api-compat.mjs` now picks `v1.3.0` as the previous release: `npm run api:compat` passes and names it.
- [X] T045 [US3] Check the release gates locally (research R19), and record in `verification.md`:
  - reading `ci.yml` back, every publish job needs all four build and test job ids, and runs only on `refs/tags/v`;
  - the tag check passes for `GITHUB_REF_NAME=v$(cat VERSION)` and fails for `v9.9.9`, run in Git Bash;
  - `python -c "from packaging.version import Version; print(Version('1.4.0-dev.2'))"` prints `1.4.0.dev2`.
- [X] T046 [US3] Set `VERSION` to `1.4.0` (`printf '1.4.0\n' > VERSION`). Then rebuild everything and confirm that all three packages carry the version:
  - `uv build` gives `persian_text_guard-1.4.0-*`;
  - `npm run pack` in `js/` gives `persian-text-guard-1.4.0.tgz`;
  - `dotnet pack` gives `PersianTextGuard.1.4.0.nupkg`, with package validation passing against 1.3.0.

  Commit T042–T046 as "Publish to PyPI in lockstep; version 1.4.0".
- [X] T047 [US3] 👤 Tell the user the two account steps from [contracts/package-and-release.md](contracts/package-and-release.md) → "Release 1.4.0", steps 1 and 2 (the PyPI pending publisher and the GitHub `pypi` environment), with the exact values. Both must be done **before the dry run (T059)**. A workflow that references `environment: pypi` before it exists makes GitHub create it automatically, **without** the `v*` tag rule, which would leave the publish environment unprotected. Ask the user to do both steps now. Continue with Phase 6 meanwhile, but do not start T059 until `gh api repos/AmirehsanK/PersianTextGuard/environments/pypi` shows a deployment branch policy with the custom tag rule `v*`.

**Checkpoint**: CI gates and publishes all three registries; only the dry run and the release remain.

---

## Phase 6: User Story 4 - Documented, measured and kept compatible (Priority: P3)

**Goal**: Bilingual PyPI README with tested examples, pyperf numbers and API compatibility.

**Independent Test**: spec US4: `help()` shows docstrings; the benchmarks match the README table; griffe catches a changed signature.

- [X] T048 [P] [US4] Create `python/bench/bench_filter.py` with pyperf (research R11): a `pyperf.Runner` with the ten benchmarks and the message constants copied verbatim from `js/bench/filter.bench.ts`, with the same names. Also create `python/scripts/bench_table.py`, which reads a pyperf JSON file and prints the README's Markdown table (Operation, Mean, Operations/s), with the Python version and CPU model. And create `python/scripts/bench_gate.py`, which runs `CleanShortMessage` with `--fast` and exits non-zero when its mean exceeds a threshold: 500 µs by default, the CI regression gate on shared runners (research R3). `--limit-us 250` applies SC-005's own value, which T050 checks on the named machine.
- [X] T049 [US4] Write the full `python/README.md` (FR-024, research R16):
  - the first line is `# persian-text-guard`, then a one-line English description;
  - **English**: installation (`pip install persian-text-guard`, and `uv add persian-text-guard`); the quick start; checking, finding matches, censoring and masks; your own entries; categories; `WordList.load` and `parse`; normalization and tokenizing; positions in code points; thread safety; the error table; the name table from [contracts/public-api.md](contracts/public-api.md); the performance table from T050; limitations (research R1's Unicode note, including Python 3.11's Cyrillic modifier letters; "Values that are not text"); links to the project README and the other packages;
  - **Persian**: installation and quick start, each paragraph in its own `<div dir="rtl">` block written to read correctly left to right too (research R16): no Latin word at a line's start or end, and code in separate blocks with comments in both languages;
  - every code block is fenced `python`, and is a complete, runnable snippet with `assert`s.
- [X] T050 [US4] Run the benchmarks on this machine (confirm the i7-9700K with `Get-CimInstance Win32_Processor`, and stop if it differs) with CPython 3.14: `uv run --python 3.14 python bench/bench_filter.py -o .bench/results.json --rigorous`, then `bench_table.py`. Check the SC-005 targets: build under 500 ms, `CleanShortMessage` under 250 µs, `VeryLongMessage` under 3 s. If one is missed, profile with `cProfile` and optimise the hot path before continuing; do not weaken the target without the user's decision. Put the table in `python/README.md` and `verification.md`.
- [X] T051 [US4] Create `python/tests/test_readme.py`. It reads **both** `python/README.md` and the root `README.md` (constitution Principle VI: every code example in any README has a test in its language's port). It extracts every fenced `python` block and runs each in a fresh namespace with `exec`, as one parametrized test per block, with ids `<file>-block-<n>-line-<line>`. A block that raises fails its test with the file and line number. It also asserts at least 8 blocks in `python/README.md` and at least 1 in the root `README.md` (the example T054 adds), so an extraction bug cannot pass vacuously. The test runs again after T054.
- [X] T052 [US4] Create `python/scripts/check_api.py` (research R14):
  - finds the newest `v*` tag whose tree contains `python/src/persian_text_guard/__init__.py` (`git tag --list 'v*' --sort=-v:refname`, then `git cat-file -e <tag>:python/src/persian_text_guard/__init__.py`);
  - with none, prints "baseline: no previous release" and exits 0;
  - otherwise runs `griffe check persian_text_guard --search src --against <tag> --verbose`;
  - on breakages, exits 1 unless `VERSION`'s major is greater than the tag's;
  - `--against <ref>` overrides the tag.

  **Prove it** (quickstart §5): on a throwaway commit, rename `censor`'s `mask` parameter to `character`; `check_api.py --against HEAD~1` fails and names it. Then `git reset --hard HEAD~1`. Record the output in `verification.md`.
- [X] T053 [US4] Check the documentation (FR-023, SC-006):
  - `uv run ruff check` passes with the `D` rules, so every public module, class, method and function has a docstring;
  - `uv run python -c "import persian_text_guard as p, inspect; missing=[n for n in p.__all__ if not inspect.getdoc(getattr(p,n))]; assert not missing, missing"` passes;
  - read `help(persian_text_guard.ProfanityFilter.censor)` and `help(persian_text_guard.WordList.load)`, and confirm they explain the edge cases, not only the signature.
- [X] T054 [US4] Update the root `README.md` (FR-025):
  - the packages section lists PyPI next to NuGet and npm, with `pip install persian-text-guard` and a short Python example in a fenced `python` block that ends with an `assert`, so it is a test (T051 runs the root README's `python` blocks). Re-run `uv run pytest tests/test_readme.py` afterwards;
  - the "Changes" section says that 1.4.0 adds Python, and that .NET and JavaScript behaviour is unchanged;
  - "Development" shows `python/` and the commands from contracts → "Commands";
  - "Releasing" adds PyPI trusted publishing and the `pypi` environment;
  - "Limitations" gains the Python Unicode note (research R1).
- [X] T055 [US4] Draft `specs/004-python-port/release-notes-1.4.0.md` (research R15), in English with a Persian summary in a `<div dir="rtl">` block:
  - the Python package, with install and quick start;
  - the new corpus cases (characters outside the Basic Multilingual Plane), which pin existing behaviour in every port and change none;
  - .NET and npm 1.4.0 are identical to 1.3.0 apart from the version;
  - .NET is now validated against 1.3.0.
- [X] T056 [US4] Run the lint command, `uv run pytest` (everything, README examples included), `uv build`, and the package, consumer and API checks. Everything passes. Commit T048–T056 as "Document, benchmark and API-check the Python port".

**Checkpoint**: All four user stories are complete.

---

## Phase 7: Polish, Pull Request and Release 1.4.0

**Purpose**: Prove everything from a clean state, prove the release gates on GitHub, then release with the user's go-ahead.

- [X] T057 Run the full matrix from a clean state:
  1. `git clean -xdf python js/dist` (never `graphify-out/`; check with `git clean -xdn` first);
  2. .NET tests and conformance on three targets;
  3. `npm ci && npm run test:all` in `js/`;
  4. `uv sync --locked`, then T038's five-version run;
  5. `uv build`, and the package and consumer checks.

  Record every count in `verification.md` under "Full matrix".
- [X] T058 Walk through [quickstart.md](quickstart.md) §1–§6 and §8, ticking each expected outcome in `verification.md` with the task that produced it. §7 is completed by T059–T066.
- [X] T059 Prepare the real CI dry run (research R19, SC-009), following 003's T067 with these changes:
  1. **Precondition** (M2): `gh api repos/AmirehsanK/PersianTextGuard/environments/pypi` shows the `v*` tag rule (T047); `gh api repos/AmirehsanK/PersianTextGuard/environments/pypi/deployment-branch-policies` lists `v*` with `type: tag`. Stop and ask the user if it does not.
  2. Commit and push `004-python-port`, with no pull request yet.
  3. Run `git switch -c dryrun/release-gates`.
  4. Edit `.github/workflows/ci.yml` only:
     - **NuGet**: replace login and push with a `run: |` block of `ls -l artifacts/*.nupkg` and `echo "DRY RUN - would push to NuGet"`, so no `": "` appears in a plain scalar (003's lesson);
     - **npm**: add `--dry-run` to the publish command;
     - **PyPI**: replace the `pypa/gh-action-pypi-publish` step with `run: ls -l dist/`;
     - **Python job**: add a first step after checkout, `- name: "DRY RUN: simulated failure"`, with `run: exit 1`.
  5. Set `VERSION` to `1.4.0-dev.1`.
  6. **Safety check** before committing: `grep -nE "dotnet nuget push|NuGet/login|gh-action-pypi-publish" .github/workflows/ci.yml` prints nothing, and `grep -n "npm publish" .github/workflows/ci.yml | grep -v -- "--dry-run"` prints nothing. Validate the YAML. Stop if anything fails.
  7. Commit as "DRY RUN ONLY: release gate test (do not merge)" and push the branch.
- [X] T060 Dry run 1, where a failing Python job blocks all three registries: tag and push `v1.4.0-dev.1`. Wait with `gh run watch`. Then confirm with `gh run view <id> --json jobs`:
  - all five `Python (…)` jobs are `failure`, and the .NET and JavaScript jobs are `success`;
  - `Publish to NuGet`, `Publish to npm` and `Publish to PyPI` are `skipped`.

  If a publish job ran, stop, delete the tag, and report. Record the job list.
- [X] T061 Dry runs 2 and 3:
  1. **Run 2, everything green.** Remove the failure step, set `VERSION` to `1.4.0-dev.2`, repeat the safety check, commit, push, then tag and push `v1.4.0-dev.2`. Every job succeeds:
     - NuGet lists `PersianTextGuard.1.4.0-dev.2.nupkg`;
     - npm reports `+ persian-text-guard@1.4.0-dev.2` with tag `next`, 8 files, `(dry-run)`;
     - PyPI lists `persian_text_guard-1.4.0.dev2-py3-none-any.whl` and `persian_text_guard-1.4.0.dev2.tar.gz`, and "Check file versions" passes;
     - the run shows a deployment to the `pypi` environment, which exists with its `v*` tag rule (T059 precondition), proving the rule admits release tags.
  2. **Run 3, mismatched tag.** Tag the same commit `v1.4.0-dev.3` and push. All three publish jobs fail at "Check tag matches VERSION" with "Tag v1.4.0-dev.3 does not match VERSION 1.4.0-dev.2".

  Record conclusions and log excerpts.
- [X] T062 Clean up the dry run:
  1. Delete the three tags, remote and local.
  2. Switch to `004-python-port`; delete `dryrun/release-gates`, remote and local.
  3. Confirm:
     - `git ls-remote origin | grep -i dev` prints nothing;
     - `npm view persian-text-guard versions` has no 1.4.0 or dev versions;
     - the NuGet index has no 1.4.0 or dev versions;
     - `curl -s -o /dev/null -w '%{http_code}' https://pypi.org/pypi/persian-text-guard/json` is still `404`;
     - `VERSION` on `004-python-port` is `1.4.0`, and `ci.yml` contains no `DRY RUN` or `--dry-run`;
     - the `pypi` environment still has exactly the `v*` tag policy.

  Record under "SC-009: release gates dry run".
- [X] T063 Commit `verification.md` and push. Open the pull request with `gh pr create`:
  - **Title**: "Python port on PyPI, corpus cases for supplementary characters, version 1.4.0".
  - **Body**: summary; the Python package; the new corpus cases; CI and release; the dry run; links to the spec, verification and release notes; ending with the Claude Code attribution line.

  Wait for CI: all nine build and test jobs pass (.NET ×2, JavaScript ×2, Python ×5), and the three publish jobs are skipped. **Do not merge without the user's go-ahead.**
- [X] T064 After the user approves, merge with `gh pr merge <n> --merge`, fast-forward local `main`, and confirm `main`'s CI is green.
- [X] T065 Add the five Python job names to `main`'s required checks with `gh api -X PATCH repos/AmirehsanK/PersianTextGuard/branches/main/protection/required_status_checks`, keeping the four existing ones, `strict: false`, and `app_id` 15368. The 1.3.0 delegation covered only the JavaScript checks, so ask the user first, unless they have already said to proceed with the release.
- [X] T066 (**irreversible**; needs the user's go-ahead to release) Push tag `v1.4.0` on the merge commit, only if all of these hold:
  - `main` is green;
  - `VERSION` is `1.4.0`;
  - the GitHub environment `pypi` exists with its `v*` tag rule, and the user confirms the PyPI pending publisher (T047);
  - **CPython 3.15**: if it has been released by now (`uv python list 3.15` shows a final, non-rc version), the constitution requires testing it (Principle III). Before tagging, add `"3.15"` to the CI matrix and a `Programming Language :: Python :: 3.15` classifier, run T038 on 3.15 locally, and merge that through a pull request first. Record the check either way;
  - neither npm, NuGet nor PyPI has 1.4.0.

  Watch the run. Every job succeeds, including all three publish jobs.
- [X] T067 Verify all three registries (SC-011, quickstart §7):
  - install `persian-text-guard==1.4.0` from PyPI into a fresh venv and run the smoke check;
  - `npm view persian-text-guard@1.4.0 version`, and a fresh install that flags `ک.ی.ر`;
  - the NuGet index lists 1.4.0;
  - the PyPI JSON shows the version, and the project page shows attestations and the README.

  Record the results in `verification.md`.
- [X] T068 Create the GitHub release with `gh release create v1.4.0 --title "PersianTextGuard 1.4.0" --notes-file specs/004-python-port/release-notes-1.4.0.md --verify-tag`. Mark T063–T068 done in `tasks.md`, record "Release 1.4.0" in `verification.md`, and commit and push to `main` as "Record 1.4.0 release verification".

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (T001–T008)**: no dependencies.
- **Foundational (T009–T020)**:
  - T009 → T010 (corpus), independent of the Python layers;
  - T011–T014 in parallel; T015 needs T011–T013; T016 needs T011 and T012; T017 needs T012; T018 needs T017; T019 needs T015–T018; T020 last.
- **US1 (T021–T032)**: needs Foundational. T021 and T022 first (they fail); T023 → T024 → T025 → T026 → T027; T028 and T029 in parallel with T023–T026; T030 → T031 → T032.
- **US2 (T033–T041)**: T033 and T034 can start with US1; T035 onwards need T027. T037 → T038 → T039 → T040 → T041.
- **US3 (T042–T047)**: T042 and T043 run **after** T048 and T052, because CI calls their scripts. T043 → T045 → T046. T044 is independent. T047 (the user's two account steps) must be finished before T059.
- **US4 (T048–T056)**: T049 → T051; T048 → T050; T052 is independent; T054 and T055 are independent; T056 last.
- **Release (T057–T068)**: strictly sequential. T063 needs every earlier task. T064–T066 need the user's decisions.

### User Story Dependencies

- **US1** needs only Foundational.
- **US2** needs US1's public API, T027.
- **US3** needs US1's packaging (T030 and T031) and US4's `check_api.py` and `bench_gate.py` for a green CI.
- **US4** needs US1. Its README tests need the finished API.

### Parallel Opportunities

- T005 and T007 during setup.
- T011, T012, T013 and T014, then T017 alongside T015 and T016.
- T021, T022, T028 and T029 alongside the scan port.
- T033 and T034 alongside US1's implementation.
- T048, T052, T054 and T055 alongside each other.

---

## Parallel Example: User Story 1

```text
Task: "T021 Create python/tests/test_api.py (fails until T027)"
Task: "T022 Create python/tests/test_word_list_load.py"
Task: "T028 Placeholder python/README.md"
Task: "T029 Consumer files in python/consumers/"
```

---

## Implementation Strategy

### MVP First (Foundational + User Story 1)

1. Setup, then Foundational: the corpus cases are pinned, and the shared layers pass.
2. US1: the package builds, installs and passes `test_api.py`.
3. **Stop and validate**: `consumers/smoke.py` from a wheel in a clean environment.

### Incremental Delivery

1. + US2: 523/523 on five Python builds, and thread-safe. The port is now releasable in principle.
2. + US4: README, benchmarks and API check, which CI needs.
3. + US3: CI and PyPI publishing, `VERSION` 1.4.0.
4. Release phase: dry run, PR, and the user's go-ahead for merge and tag.

---

## Notes

- **Never commit `graphify-out/`.**
- Commits end with `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`; the PR body ends with the Claude Code line.
- The corpus is never edited to match a port. Differences go to the user.
- Merging, required-check changes and the `v1.4.0` tag each need the user's decision in this feature; 1.3.0's delegation does not carry over.
- Mark each task `[X]` in this file as it is completed.
