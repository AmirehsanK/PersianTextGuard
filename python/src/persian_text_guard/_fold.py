"""Character-level readings: folding look-alikes, filler and squeezing. Port of ``js/src/fold.ts``.

Which ports ``Fold``, ``FoldCharacter``, ``IsFiller``, ``IsRunBoundary``, ``EnclosedLetter``,
``BuildLatinBaseLetters`` and ``Squeeze`` from ``dotnet/src/PersianTextGuard/ProfanityFilter.Scan.cs``.
The source maps and the scanner both use them. Every function takes a UTF-16 view.
"""

from __future__ import annotations

import re

from ._unicode import (
    category_of_unit,
    is_high_surrogate,
    is_letter,
    is_letter_or_digit,
    is_low_surrogate,
    is_surrogate,
    is_white_space,
    nfd,
    to_lower_invariant,
)


def _build_latin_base_letters() -> dict[str, str]:
    """The plain lower-case Latin letter for each unit in U+00C0-U+024F, or the unit itself."""
    table: dict[str, str] = {}
    for code in range(0x00C0, 0x024F + 1):
        c = chr(code)
        first = nfd(c)[0]
        table[c] = to_lower_invariant(first) if first < "\x80" and is_letter(first) else c

    # Letters with a stroke or hook have no decomposition.
    plain = (
        (0x00F8, "o"),
        (0x00D8, "o"),
        (0x0111, "d"),
        (0x0110, "d"),
        (0x0142, "l"),
        (0x0141, "l"),
        (0x0192, "f"),
        (0x0127, "h"),
        (0x0131, "i"),
        (0x00DF, "s"),
    )
    for letter, base in plain:
        table[chr(letter)] = base

    return table


LATIN_BASE_LETTERS = _build_latin_base_letters()
"""The plain lower-case Latin letter for each unit in U+00C0-U+024F, or the unit itself."""

_CYRILLIC_AND_GREEK = {
    chr(unit): letter
    for unit, letter in (
        # Cyrillic, already lower-cased by normalization.
        (0x0430, "a"),
        (0x0432, "b"),
        (0x0435, "e"),
        (0x0451, "e"),
        (0x043A, "k"),
        (0x043C, "m"),
        (0x043D, "h"),
        (0x043E, "o"),
        (0x0440, "p"),
        (0x0441, "c"),
        (0x0442, "t"),
        (0x0443, "y"),
        (0x0445, "x"),
        (0x0456, "i"),
        (0x0458, "j"),
        (0x0455, "s"),
        (0x0501, "d"),
        (0x04BB, "h"),
        # Greek.
        (0x03B1, "a"),
        (0x03B5, "e"),
        (0x03B9, "i"),
        (0x03BA, "k"),
        (0x03BD, "v"),
        (0x03BF, "o"),
        (0x03C1, "p"),
        (0x03C4, "t"),
        (0x03C5, "u"),
        (0x03C7, "x"),
    )
}

_DIGIT_LETTERS = {"0": "o", "1": "i", "3": "e", "4": "a", "5": "s", "7": "t", "8": "b"}

_SYMBOL_LETTERS = {
    "!": "i",
    "|": "i",
    "\xa1": "i",  # inverted exclamation mark
    "\N{EURO SIGN}": "e",
    "@": "a",
    "$": "s",
}

_FILLER = frozenset("*+~^`=<>#%&\N{BULLET}\N{MIDDLE DOT}\N{BLACK HEART SUIT}\N{HEAVY BLACK HEART}")
_FILLER_CATEGORIES = frozenset({"Mn", "Me", "So", "Sk"})
_BOUNDARY_CATEGORIES = frozenset({"Pc", "Pd", "Ps", "Pe", "Pi", "Pf", "Po", "Sm", "Sc"})

# Runs of one character other than a space, for squeezing with one C-level pass.
_REPEATED = re.compile(r"([^ ])\1+", re.DOTALL)


def fold(normalized: str, map_: list[int] | None = None) -> str:
    """Read look-alikes as the Latin letters they imitate, and drop filler inside words.

    Harmless on Persian script, which has no Latin letters to fold. ``!`` is folded before tokenizing
    because it is also a sentence separator. Digits are only read as letters in a run that has letters
    in it: "sh1t" and "4ss" are words, "455" and «۴۵۵ تومان» are numbers. When ``map_`` is given, it
    receives, for each output unit, its index in the input.
    """
    out: list[str] = []
    i = 0
    length = len(normalized)

    while i < length:
        if is_run_boundary(normalized, i):
            out.append(normalized[i])
            if map_ is not None:
                map_.append(i)
            i += 1
            continue

        end = i
        has_letter = False
        while end < length and not is_run_boundary(normalized, end):
            unit = normalized[end]
            has_letter = has_letter or (
                is_letter(unit)
                or (
                    is_high_surrogate(unit)
                    and end + 1 < length
                    and enclosed_letter(code_point_of(unit, normalized[end + 1])) != ""
                )
            )
            end += 1

        while i < end:
            c = normalized[i]

            if is_high_surrogate(c) and i + 1 < end and is_low_surrogate(normalized[i + 1]):
                # Enclosed and regional-indicator letters read as letters; emoji are filler.
                letter = enclosed_letter(code_point_of(c, normalized[i + 1]))
                if letter:
                    out.append(letter)
                    if map_ is not None:
                        map_.append(i)

                i += 2
                continue

            if not is_filler(c):
                out.append(fold_character(c, has_letter))
                if map_ is not None:
                    map_.append(i)
            i += 1

    return "".join(out)


def code_point_of(high: str, low: str) -> int:
    """``char.ConvertToUtf32(high, low)``.

    Callers only pass high surrogates; a second unit that is not a low surrogate gives a value that is
    never an enclosed letter, where .NET would throw on text normalization never produces.
    """
    return 0x10000 + ((ord(high) - 0xD800) << 10) + (ord(low) - 0xDC00)


def fold_character(c: str, map_digits: bool) -> str:
    """One unit read as the Latin letter it imitates; digits only when ``map_digits``.

    ``map_digits`` is set for a run with letters in it.
    """
    if map_digits:
        digit = _DIGIT_LETTERS.get(c)
        if digit is not None:
            return digit

    symbol = _SYMBOL_LETTERS.get(c)
    if symbol is not None:
        return symbol

    letter = _CYRILLIC_AND_GREEK.get(c)
    if letter is not None:
        return letter

    # Accented Latin: fück, shíť, ƒuck.
    return LATIN_BASE_LETTERS.get(c, c)


def is_filler(c: str) -> bool:
    """Filler: symbols with no letter to stand for, typed inside a word to break it up.

    Also combining marks stacked on letters to decorate them (f̶u̶c̶k̶).
    """
    if c in _FILLER:
        return True

    if c < "\x80" or is_letter_or_digit(c):
        return False

    return category_of_unit(c) in _FILLER_CATEGORIES


def is_run_boundary(text: str, index: int) -> bool:
    """Where a run of word-like characters ends, for deciding whether its digits are letters.

    Symbols that fold to letters, filler and emoji belong to the run; spaces and punctuation end it.
    """
    c = text[index]
    if is_white_space(c):
        return True

    if is_letter_or_digit(c) or is_surrogate(c) or is_filler(c) or c in _SYMBOL_LETTERS:
        return False

    return category_of_unit(c) in _BOUNDARY_CATEGORIES


def enclosed_letter(code_point: int) -> str:
    """The Latin letter a squared, circled or regional-indicator letter shows, or ``""``.

    NFKC already folds the ones with a compatibility mapping (Ⓐ, 𝐟); these have none.
    """
    if 0x1F130 <= code_point <= 0x1F149:  # squared
        return chr(0x61 + (code_point - 0x1F130))
    if 0x1F150 <= code_point <= 0x1F169:  # negative circled
        return chr(0x61 + (code_point - 0x1F150))
    if 0x1F170 <= code_point <= 0x1F189:  # negative squared
        return chr(0x61 + (code_point - 0x1F170))
    if 0x1F1E6 <= code_point <= 0x1F1FF:  # regional indicator
        return chr(0x61 + (code_point - 0x1F1E6))
    return ""


def squeeze(value: str, map_: list[int] | None = None) -> str:
    """Every run of a repeated letter down to one: "fuuck" to "fuck". Spaces are never squeezed."""
    if map_ is None:
        # The unit before the first is U+0000 in .NET and JavaScript, so leading U+0000 units are
        # squeezed away too.
        return _REPEATED.sub(r"\1", value.lstrip("\0"))

    out: list[str] = []
    previous = "\0"

    for i, c in enumerate(value):
        if c != previous or c == " ":
            out.append(c)
            map_.append(i)

        previous = c

    return "".join(out)
