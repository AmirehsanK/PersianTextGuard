# Data Model: Python Port Published to PyPI

**Feature**: [spec.md](spec.md) | **Research**: [research.md](research.md) | **API**: [contracts/public-api.md](contracts/public-api.md)

The entities are the same as in every port (spec Key Entities). This document states their Python
shape, validation rules and invariants. The corpus names (`wholeWord`, `lookalikeCharacters`, …) are
the enumeration values in every port.

## Enumerations (`enum.StrEnum`)

| Enum | Members → values | Notes |
| --- | --- | --- |
| `WordMatchMode` | `WHOLE_WORD` → `"wholeWord"`, `ANYWHERE` → `"anywhere"` | |
| `WordCategory` | `UNCATEGORIZED` → `"uncategorized"`, `PROFANITY` → `"profanity"`, `SEXUAL` → `"sexual"`, `INSULT` → `"insult"`, `SLUR` → `"slur"`, `HARASSMENT` → `"harassment"`, `MILD` → `"mild"` | Declaration order is .NET's. `UNCATEGORIZED` never appears in bundled lists (Principle VII). |
| `EvasionKind` | `REPEATED_LETTERS` → `"repeatedLetters"`, `LOOKALIKE_CHARACTERS` → `"lookalikeCharacters"`, `SPLIT_WORD` → `"splitWord"` | The order in a match's `evasion` tuple is this declaration order, as the corpus records. |
| `NormalizationStep` | `COMPATIBILITY_FORMS`, `UNIFY_LETTERS`, `REMOVE_DIACRITICS`, `REMOVE_TATWEEL`, `REMOVE_ZERO_WIDTH`, `REMOVE_BIDI_CONTROLS`, `ASCII_DIGITS`, `LOWER_CASE`, `COLLAPSE_WHITESPACE`, `COLLAPSE_REPEATS`, each → its lowerCamelCase name | The presets are `"comparison"` (the default), `"standard"` and `"none"`. |

**Accepted input.** Wherever one of these is a parameter, the member or its value string is accepted,
and both are typed (`WordCategory | Literal["uncategorized", …]`). An unknown string raises
`ValueError` naming the valid values. Returned values are always members.

## `BannedWord` (frozen, slotted dataclass)

| Field | Type | Default | Rule |
| --- | --- | --- | --- |
| `text` | `str` | required | Must be a `str` (a `str` subclass is converted to `str`). May be blank: blank entries are ignored by the filter, not rejected, as in .NET. |
| `mode` | `WordMatchMode` | `WHOLE_WORD` | `__init__` (written by hand, `init=False`, so the type checker accepts a name) converts a name to the member. |
| `category` | `WordCategory` | `UNCATEGORIZED` | As `mode`. |

- **Equality and hashing** use all three fields, so two equal entries are the same entry for `set`
  purposes. The filter's own de-duplication is by normalized text and mode, as in .NET (spec Edge Cases).
- **Immutable**: assigning a field raises `dataclasses.FrozenInstanceError`.

## `ProfanityFilterOptions` (frozen, slotted dataclass)

| Field | Type | Default |
| --- | --- | --- |
| `squeeze_repeated_letters` | `bool` | `True` |
| `fold_lookalike_characters` | `bool` | `True` |
| `join_spaced_letters` | `bool` | `True` |

Keyword-only. A non-`bool` value raises `TypeError`, because a truthy string would be a silent mistake.

## `ProfanityFilter`

| Member | Type | Notes |
| --- | --- | --- |
| `ProfanityFilter(words, options=None)` | constructor | `words`: any iterable of `BannedWord`. It is consumed once, and entries are copied into internal tables. `None` or a non-iterable raises `TypeError`; an element that is not a `BannedWord` raises `TypeError` naming its position. |
| `count` | `int` property | Distinct entries after normalization and de-duplication, as .NET's `Count`. |
| `contains_profanity(text)` | `bool` | |
| `find_match(text)` | `ProfanityMatch \| None` | The first match in text order. |
| `find_matches(text)` | `tuple[ProfanityMatch, ...]` | Ordered by `index`, non-overlapping (003 G5). |
| `censor(text, mask="*")` | `str` | Each match region becomes four `mask` characters. The mask is validated first, even when `text` is `None`. |

**State after construction.** Whole-word lookup `dict[str, …]` keyed by normalized token, anywhere-entry
tuples, first-unit indexes and options. It is built in `__init__` and only read after that. `__slots__`
and no setters make every attribute read-only; there are no `__dict__` attributes to mutate. Changing
the caller's list or `BannedWord` objects afterwards has no effect, because entries are frozen and
copied (spec Edge Cases).

**Thread safety.** Safe to share across threads on standard and free-threaded CPython (research R17).

## `ProfanityMatch` (frozen, slotted dataclass)

| Field | Type | Rule |
| --- | --- | --- |
| `word` | `BannedWord` | The entry exactly as given to the filter: the same object when the caller passed it. |
| `evasion` | `tuple[EvasionKind, ...]` | Empty when there is no evasion; otherwise in declaration order. |
| `index` | `int` | Code points into the caller's string (research R2). |
| `length` | `int` | Code points. `text[index:index + length]` is the matched region. `0 ≤ index`, `length ≥ 1`, `index + length ≤ len(text)`. |

## Normalization

| Function | Input | Output |
| --- | --- | --- |
| `normalize(text, steps="comparison")` | `str \| None`; `steps` is a preset name or an iterable of steps (members or names) | `str`; `None` → `""` |
| `tokenize(text)` | `str \| None` | `list[str]`, a new list each call; `None` → `[]` |
| `to_persian_digits(text)` / `to_ascii_digits(text)` | `str \| None` | `str`; `None` → `""` |

## Word lists: `WordList` (static methods)

| Method | Returns | Rule |
| --- | --- | --- |
| `all()` | `tuple[BannedWord, ...]` | Every bundled entry, `MILD` included, in list order (Persian, Finglish, English). Parsed once; the same tuple object every call. |
| `persian_default()` | `tuple[BannedWord, ...]` | `all()` without `MILD`. The same tuple object every call. |
| `bundled(*categories)` | `tuple[BannedWord, ...]` | Entries in the given categories, in list order. A new tuple. An unknown category raises `ValueError`. |
| `parse(text)` | `tuple[BannedWord, ...]` | The shared word-list format (spec 002). `[name]` headings by name only, case-insensitive, spaces allowed; `~` means anywhere; `#` starts a comment; blank lines and `\r\n` are fine. An unknown heading raises `WordListFormatError` with `line`. `None` is not accepted (`TypeError`): there is no missing word list. |
| `load(source)` | `tuple[BannedWord, ...]` | `source` is a `str` or `os.PathLike` path, or an open file. Binary files and paths are decoded as UTF-8 with `utf-8-sig`, which drops a BOM. A text file is read as it is and a leading U+FEFF is dropped. An open file is not closed. Then `parse`. `OSError` and `UnicodeDecodeError` propagate unchanged, naming the file (spec Clarifications). |

## Errors

| Error | Raised by | When |
| --- | --- | --- |
| `TypeError` | every text function, `censor`, `WordList.parse`, the constructors | The argument is not a `str` (or `None` where `None` is allowed), a non-`bool` option, or a non-`BannedWord` entry. |
| `ValueError` | `censor`, enum-typed parameters, `normalize` | An invalid mask: not exactly one character, a character above U+FFFF (one UTF-16 unit in every port), or a letter, digit, whitespace, control character or surrogate. Or an unknown category, mode, step or preset name. |
| `WordListFormatError(ValueError)` | `WordList.parse`, `WordList.load` | An unknown section heading; `line` is 1-based. |
| `OSError`, `UnicodeDecodeError` | `WordList.load` | The file cannot be opened, or is not UTF-8. |

## Package

| Artifact | Name | Contents |
| --- | --- | --- |
| Wheel | `persian_text_guard-<v>-py3-none-any.whl` | `persian_text_guard/` (the modules, the generated `_wordlists.py` and `_version.py`, and `py.typed`), plus metadata with `LICENSE`, `THIRD-PARTY-NOTICES.md` and the README |
| Source distribution | `persian_text_guard-<v>.tar.gz` | The same sources, including the generated files, plus `pyproject.toml`, `hatch_build.py`, `README.md`, `LICENSE` and `THIRD-PARTY-NOTICES.md` |

`<v>` is `VERSION` in PEP 440 form (research R9). `Requires-Python: >=3.11`, with no `Requires-Dist`.
