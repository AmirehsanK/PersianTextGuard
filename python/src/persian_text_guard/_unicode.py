"""Unicode primitives with the semantics of the .NET APIs the matcher was written against.

Port of ``js/src/unicode.ts``. Every Unicode-dependent operation in the package goes through this
module (``scripts/check_unicode_usage.py`` and ruff enforce it). The differences from Python's own
defaults were measured against .NET 10 on every code point (spec 004, research R1):

- whitespace is .NET's ``char.IsWhiteSpace`` set, not ``str.isspace``, which also counts
  U+001C-U+001F (U+0085 is whitespace, U+FEFF is not);
- lower-casing is one UTF-16 unit at a time, and leaves a unit alone when Python would expand it
  (U+0130 stays U+0130), so there is no final-sigma or length change;
- categories are per UTF-16 unit where .NET tests a ``char``, and per code point where .NET uses
  ``GetUnicodeCategory(string, index)``;
- NFKC runs between Unicode noncharacters, which pass through unchanged.

The functions take the text the matcher works on: the UTF-16 view from ``_utf16``, in which a
character above U+FFFF is two surrogate code points. A "unit" is a one-character ``str`` of that view.
"""

from __future__ import annotations

import re
import unicodedata

_LETTER_CATEGORIES = frozenset({"Lu", "Ll", "Lt", "Lm", "Lo"})

# .NET char.IsWhiteSpace: U+0009-U+000D, U+0085, and the Zs, Zl and Zp characters. Written out, because
# every category set here is identical on CPython 3.11-3.14 (Unicode 14-16), and a set lookup is the
# fastest test; tests/test_unicode.py checks it against unicodedata on the running interpreter.
WHITE_SPACE = frozenset(
    "\x09\x0a\x0b\x0c\x0d\x20\x85\xa0\U00001680"
    "\U00002000\U00002001\U00002002\U00002003\U00002004\U00002005\U00002006\U00002007\U00002008\U00002009\U0000200a"
    "\U00002028\U00002029\U0000202f\U0000205f\U00003000"
)

# One-unit categories, cached: every thread computes the same value, so a racing write is harmless
# (research R17). Units are UTF-16 units, so the cache holds at most 65,536 entries.
_categories: dict[str, str] = {}

_SURROGATE = re.compile("[\ud800-\udfff]")
_SURROGATE_PAIR = re.compile("[\ud800-\udbff][\udc00-\udfff]")
_SUPPLEMENTARY = re.compile("[\U00010000-\U0010ffff]")
_NONCHARACTER_BMP = re.compile("[\U0000fdd0-\U0000fdef\U0000fffe\U0000ffff]")


def category_of_unit(c: str) -> str:
    """The general category of one UTF-16 unit, like .NET ``GetUnicodeCategory(char)``.

    A lone surrogate unit is ``"Cs"``; an unassigned one is ``"Cn"``.
    """
    category = _categories.get(c)
    if category is None:
        category = _categories.setdefault(c, unicodedata.category(c))
    return category


def category_at(text: str, index: int) -> str:
    """The general category of the code point at ``index``, like .NET ``GetUnicodeCategory(string, int)``.

    A valid surrogate pair is read as one code point.
    """
    c = text[index]
    if "\ud800" <= c <= "\udbff" and index + 1 < len(text) and "\udc00" <= text[index + 1] <= "\udfff":
        return unicodedata.category(combine(c, text[index + 1]))
    return category_of_unit(c)


def combine(high: str, low: str) -> str:
    """The code point a high and a low surrogate unit encode, like .NET ``char.ConvertToUtf32``."""
    return chr(0x10000 + ((ord(high) - 0xD800) << 10) + (ord(low) - 0xDC00))


def is_letter(c: str) -> bool:
    """Whether a unit is a letter (L*), like .NET ``char.IsLetter``."""
    if c < "\x80":
        return "a" <= c <= "z" or "A" <= c <= "Z"
    return category_of_unit(c) in _LETTER_CATEGORIES


def is_letter_or_digit(c: str) -> bool:
    """Whether a unit is a letter (L*) or a decimal digit (Nd), like .NET ``char.IsLetterOrDigit``."""
    if c < "\x80":
        return "a" <= c <= "z" or "A" <= c <= "Z" or "0" <= c <= "9"
    category = category_of_unit(c)
    return category in _LETTER_CATEGORIES or category == "Nd"


def is_control(c: str) -> bool:
    """Whether a unit is a control character (Cc), like .NET ``char.IsControl``."""
    return c <= "\x1f" or "\x7f" <= c <= "\x9f"


def is_white_space(c: str) -> bool:
    """Whether a unit is whitespace, like .NET ``char.IsWhiteSpace``.

    Categories Zs, Zl and Zp, plus U+0009-U+000D, U+0085 and U+00A0. Unlike ``str.isspace``,
    U+001C-U+001F are not whitespace.
    """
    return c in WHITE_SPACE


def is_surrogate(c: str) -> bool:
    """Whether a unit is a high or low surrogate, like .NET ``char.IsSurrogate``."""
    return "\ud800" <= c <= "\udfff"


def is_high_surrogate(c: str) -> bool:
    """Whether a unit is a high (leading) surrogate, like .NET ``char.IsHighSurrogate``."""
    return "\ud800" <= c <= "\udbff"


def is_low_surrogate(c: str) -> bool:
    """Whether a unit is a low (trailing) surrogate, like .NET ``char.IsLowSurrogate``."""
    return "\udc00" <= c <= "\udfff"


def to_lower_invariant(c: str) -> str:
    """Lower-cases one UTF-16 unit, like .NET ``char.ToLowerInvariant``.

    When Python's lower-casing would not give exactly one character (U+0130 ``İ`` gives two), the
    unit is returned unchanged.
    """
    if c < "\x80":
        return c.lower()
    lower = c.lower()
    return lower if len(lower) == 1 else c


def is_noncharacter(code_point: int) -> bool:
    """Whether a code point is a Unicode noncharacter: U+FDD0-U+FDEF, or one ending in FFFE or FFFF."""
    return 0xFDD0 <= code_point <= 0xFDEF or (code_point & 0xFFFE) == 0xFFFE


def noncharacter_length_at(text: str, index: int) -> int:
    """The length (1 or 2 units) of a noncharacter starting at ``index``, or 0 when there is none.

    A surrogate pair in the view is read as one code point.
    """
    c = text[index]
    if "\ud800" <= c <= "\udbff" and index + 1 < len(text):
        low = text[index + 1]
        if "\udc00" <= low <= "\udfff":
            return 2 if is_noncharacter(ord(combine(c, low))) else 0
    return 1 if is_noncharacter(ord(c)) else 0


def recombine(text: str) -> str:
    """``text`` with every valid surrogate pair replaced by the code point it encodes.

    Lone surrogates are kept as they are.
    """
    if _SURROGATE.search(text) is None:
        return text
    return _SURROGATE_PAIR.sub(lambda m: combine(m.group()[0], m.group()[1]), text)


def split_supplementary(text: str) -> str:
    """``text`` with every code point above U+FFFF replaced by its two surrogate units."""
    if text.isascii() or _SUPPLEMENTARY.search(text) is None:
        return text
    return _SUPPLEMENTARY.sub(_split, text)


def _split(match: re.Match[str]) -> str:
    code_point = ord(match.group()) - 0x10000
    return chr(0xD800 + (code_point >> 10)) + chr(0xDC00 + (code_point & 0x3FF))


def nfkc(text: str) -> str:
    """NFKC, like .NET ``string.Normalize(NormalizationForm.FormKC)`` as PersianTextGuard applies it.

    ``text`` is a UTF-16 view, and so is the result: valid surrogate pairs are recombined into code
    points, normalized, and split again. Noncharacters are copied unchanged and normalized around.
    ``unicodedata`` keeps them, and a noncharacter composes with nothing, so this equals NFKC of the
    runs between them. Lone surrogates must already have been replaced by the caller.
    """
    if text.isascii():
        return text
    if _SURROGATE.search(text) is None:
        if _NONCHARACTER_BMP.search(text) is None:
            return unicodedata.normalize("NFKC", text)
        return _nfkc_around_noncharacters(text)
    return split_supplementary(_nfkc_around_noncharacters(recombine(text)))


def _nfkc_around_noncharacters(text: str) -> str:
    # text holds code points here, not a view.
    parts: list[str] = []
    start = 0
    for i, c in enumerate(text):
        if is_noncharacter(ord(c)):
            if i > start:
                parts.append(unicodedata.normalize("NFKC", text[start:i]))
            parts.append(c)
            start = i + 1
    if start == 0:
        return unicodedata.normalize("NFKC", text)
    parts.append(unicodedata.normalize("NFKC", text[start:]))
    return "".join(parts)


def is_nfkc(text: str) -> bool:
    """Whether ``text`` is already in NFKC, like .NET ``string.IsNormalized(NormalizationForm.FormKC)``."""
    if text.isascii():
        return True
    return unicodedata.is_normalized("NFKC", recombine(text))


def nfd(text: str) -> str:
    """NFD, like .NET ``string.Normalize(NormalizationForm.FormD)``, on a UTF-16 view."""
    return split_supplementary(unicodedata.normalize("NFD", recombine(text)))


def unicode_version() -> str:
    """The Unicode version of the running interpreter's character database."""
    return unicodedata.unidata_version


# A run of .NET whitespace, for collapsing it with one C-level pass.
WHITE_SPACE_RUN = re.compile("[" + "".join(re.escape(c) for c in sorted(WHITE_SPACE)) + "]+")
