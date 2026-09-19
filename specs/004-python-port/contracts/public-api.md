# Contract: `persian-text-guard` Public API (Python)

**Feature**: [../spec.md](../spec.md) | **Data model**: [../data-model.md](../data-model.md) | **Research**: [../research.md](../research.md) R2, R5, R17, R18

This is the surface users import from `persian_text_guard`. `__all__` lists exactly these names, and
griffe compares them with the previous release (research R14). A docstring on every member is required
(FR-023) but not repeated here.

## Declarations

```python
from collections.abc import Iterable
from dataclasses import dataclass
from enum import StrEnum
from os import PathLike
from typing import BinaryIO, Literal, TextIO

# ------------------------------------------------------------------ enumerations
class WordMatchMode(StrEnum):
    WHOLE_WORD = "wholeWord"
    ANYWHERE = "anywhere"

class WordCategory(StrEnum):
    UNCATEGORIZED = "uncategorized"
    PROFANITY = "profanity"
    SEXUAL = "sexual"
    INSULT = "insult"
    SLUR = "slur"
    HARASSMENT = "harassment"
    MILD = "mild"

class EvasionKind(StrEnum):
    REPEATED_LETTERS = "repeatedLetters"
    LOOKALIKE_CHARACTERS = "lookalikeCharacters"
    SPLIT_WORD = "splitWord"

class NormalizationStep(StrEnum):
    COMPATIBILITY_FORMS = "compatibilityForms"
    UNIFY_LETTERS = "unifyLetters"
    REMOVE_DIACRITICS = "removeDiacritics"
    REMOVE_TATWEEL = "removeTatweel"
    REMOVE_ZERO_WIDTH = "removeZeroWidth"
    REMOVE_BIDI_CONTROLS = "removeBidiControls"
    ASCII_DIGITS = "asciiDigits"
    LOWER_CASE = "lowerCase"
    COLLAPSE_WHITESPACE = "collapseWhitespace"
    COLLAPSE_REPEATS = "collapseRepeats"

WordMatchModeName = Literal["wholeWord", "anywhere"]
WordCategoryName = Literal["uncategorized", "profanity", "sexual", "insult", "slur", "harassment", "mild"]
NormalizationStepName = Literal["compatibilityForms", "unifyLetters", "removeDiacritics", "removeTatweel",
                                "removeZeroWidth", "removeBidiControls", "asciiDigits", "lowerCase",
                                "collapseWhitespace", "collapseRepeats"]
NormalizationPreset = Literal["comparison", "standard", "none"]

# ------------------------------------------------------------------ entries and word lists
@dataclass(frozen=True, slots=True, init=False)
class BannedWord:
    text: str
    mode: WordMatchMode
    category: WordCategory
    def __init__(self, text: str,
                 mode: WordMatchMode | WordMatchModeName = WordMatchMode.WHOLE_WORD,
                 category: WordCategory | WordCategoryName = WordCategory.UNCATEGORIZED) -> None: ...
    # The fields always hold members: a name is converted in __init__.

class WordListFormatError(ValueError):
    line: int                                               # 1-based

class WordList:                                             # static methods only; not instantiable
    @staticmethod
    def all() -> tuple[BannedWord, ...]: ...
    @staticmethod
    def persian_default() -> tuple[BannedWord, ...]: ...
    @staticmethod
    def bundled(*categories: WordCategory | WordCategoryName) -> tuple[BannedWord, ...]: ...
    @staticmethod
    def parse(text: str) -> tuple[BannedWord, ...]: ...
    @staticmethod
    def load(source: str | PathLike[str] | BinaryIO | TextIO) -> tuple[BannedWord, ...]: ...

# ------------------------------------------------------------------ filter
@dataclass(frozen=True, slots=True, kw_only=True)
class ProfanityFilterOptions:
    squeeze_repeated_letters: bool = True
    fold_lookalike_characters: bool = True
    join_spaced_letters: bool = True

@dataclass(frozen=True, slots=True)
class ProfanityMatch:
    word: BannedWord
    evasion: tuple[EvasionKind, ...]
    index: int                                              # code points
    length: int                                             # code points

class ProfanityFilter:
    def __init__(self, words: Iterable[BannedWord], options: ProfanityFilterOptions | None = None) -> None: ...
    @property
    def count(self) -> int: ...
    def contains_profanity(self, text: str | None) -> bool: ...
    def find_match(self, text: str | None) -> ProfanityMatch | None: ...
    def find_matches(self, text: str | None) -> tuple[ProfanityMatch, ...]: ...
    def censor(self, text: str | None, mask: str = "*") -> str: ...

# ------------------------------------------------------------------ normalization
def normalize(text: str | None,
              steps: NormalizationPreset | Iterable[NormalizationStep | NormalizationStepName] = "comparison") -> str: ...
def tokenize(text: str | None) -> list[str]: ...
def to_persian_digits(text: str | None) -> str: ...
def to_ascii_digits(text: str | None) -> str: ...

__version__: str                                            # PEP 440 form of VERSION
```

Nothing else is public. Source maps, the UTF-16 view, the scanner and `_unicode` stay in underscore
modules.

## Behavioural guarantees

| # | Guarantee | Checked by |
| --- | --- | --- |
| P1 | For `None` and every `str`, including empty, whitespace-only, lone surrogates, surrogate code points the caller split, noncharacters and 132,000-character text: `contains_profanity`, `find_match`, `find_matches`, `censor` (with a valid mask), `normalize`, `tokenize`, `to_persian_digits` and `to_ascii_digits` return and never raise. | corpus robustness cases; `test_api.py` |
| P2 | Any other value passed as text raises `TypeError`, and so does any non-`str` mask. A `str` subclass is treated as its `str` value. | `test_api.py` |
| P3 | Results equal the conformance corpus for every case. Positions are compared directly, since the corpus uses code points. | corpus runner |
| P4 | `contains_profanity(t) == (find_match(t) is not None) == (len(find_matches(t)) > 0)`, and `censor(t) != t` exactly when there is a match, for a `str` `t`. | corpus; `test_api.py` over the corpus inputs |
| P5 | `find_matches` is ordered by `index`, with no overlaps, and `0 ≤ index`, `length ≥ 1`, `index + length ≤ len(text)`. `text[index:index + length]` is the region. It holds for text with supplementary characters too (research R2). | corpus; `test_api.py` |
| P6 | `censor` validates `mask` before looking at `text`: a non-`str` raises `TypeError`, and a string that is not exactly one character, is above U+FFFF (a .NET mask is one UTF-16 unit, and JavaScript rejects `"😀"` too), or is a letter, digit, whitespace, control character or surrogate, raises `ValueError`. This holds even when `text` is `None`. Unmasked text is returned exactly as given. | corpus mask-validation cases; `test_api.py` |
| P7 | A filter never changes after construction. Its results and `count` do not change if the caller later changes the list it passed, and every returned object is immutable (a tuple or a frozen dataclass). | `test_api.py` |
| P8 | `WordList.all()` and `WordList.persian_default()` return the same tuple object on every call, from any thread. `persian_default()` contains no `MILD` entry. | `test_api.py`; `test_threads.py`; corpus category-selection cases |
| P9 | `WordList.parse` reads headings by category name only, case-insensitively and with spaces allowed; `[3]` is an unknown category. It reads `~`, `#`, blank lines and `\r\n`. An unknown heading raises `WordListFormatError` with a 1-based `line`. `WordList.load(x)` equals `WordList.parse` of the file's UTF-8 text, without a BOM. | corpus word-list-parsing cases; `test_word_list_load.py` |
| P10 | The bundled selections equal the .NET and JavaScript packages' entry for entry, in order. | corpus category-selection cases; quickstart §3 |
| P11 | One filter shared by 8 threads gives, for every corpus message, the same results as a single-threaded run, on standard and free-threaded CPython. | `test_threads.py` (SC-008) |
| P12 | Enumeration parameters accept the member or its value string. An unknown string raises `ValueError` at run time and is an error under `mypy --strict` and `pyright --strict`, as is an unknown option name. | `test_api.py`; consumer type checks (research R12) |

## Names across ports

This table is copied into `python/README.md` (FR-012).

| .NET | JavaScript/TypeScript | Python |
| --- | --- | --- |
| `new ProfanityFilter(words, options)` | `new ProfanityFilter(words, options)` | `ProfanityFilter(words, options)` |
| `filter.Count` | `filter.count` | `filter.count` |
| `filter.ContainsProfanity(text)` | `filter.containsProfanity(text)` | `filter.contains_profanity(text)` |
| `filter.FindMatch(text)` | `filter.findMatch(text)` | `filter.find_match(text)` (`None` when there is no match) |
| `filter.FindMatches(text)` | `filter.findMatches(text)` | `filter.find_matches(text)` (a tuple) |
| `filter.Censor(text, '#')` | `filter.censor(text, '#')` | `filter.censor(text, "#")` |
| `new BannedWord("x", WordMatchMode.Anywhere) { Category = WordCategory.Slur }` | `{ text: 'x', mode: 'anywhere', category: 'slur' }` | `BannedWord("x", WordMatchMode.ANYWHERE, WordCategory.SLUR)` |
| `ProfanityFilterOptions { SqueezeRepeatedLetters = false }` | `{ squeezeRepeatedLetters: false }` | `ProfanityFilterOptions(squeeze_repeated_letters=False)` |
| `match.Word`, `match.Evasion`, `match.Index`, `match.Length` | `match.word`, `.evasion`, `.index`, `.length` | `match.word`, `.evasion`, `.index`, `.length` (code points) |
| `EvasionKind.LookalikeCharacters \| EvasionKind.RepeatedLetters` | `['repeatedLetters', 'lookalikeCharacters']` | `(EvasionKind.REPEATED_LETTERS, EvasionKind.LOOKALIKE_CHARACTERS)` |
| `WordList.All`, `WordList.PersianDefault` | `WordList.all`, `WordList.persianDefault` | `WordList.all()`, `WordList.persian_default()` |
| `WordList.Bundled(WordCategory.Slur)` | `WordList.bundled('slur')` | `WordList.bundled(WordCategory.SLUR)` or `WordList.bundled("slur")` |
| `WordList.Parse(text)` | `WordList.parse(text)` | `WordList.parse(text)` |
| `WordList.Load(stream)` | read the file, then `WordList.parse(text)` | `WordList.load(path_or_file)` |
| `PersianNormalizer.Normalize(text, PersianNormalization.Standard)` | `normalize(text, 'standard')` | `normalize(text, "standard")` |
| `PersianNormalizer.Tokenize(text)` | `tokenize(text)` | `tokenize(text)` |
| `PersianNormalizer.ToPersianDigits(text)` | `toPersianDigits(text)` | `to_persian_digits(text)` |
| `FormatException` | `WordListFormatError` (`.line`) | `WordListFormatError` (`.line`), a `ValueError` |
| `ArgumentException` (mask) | `RangeError` | `ValueError` |

## Compatibility

- **1.4.0 is the first release of this package**, and the baseline for griffe (FR-022).
- **After that**, removing or changing any declaration above is MAJOR, and adding to it is MINOR.
  Adding a member to an enumeration the package returns (`EvasionKind`) is MINOR, but is called out in
  the release notes, because callers' exhaustive `match` statements stop being exhaustive for a type
  checker.
