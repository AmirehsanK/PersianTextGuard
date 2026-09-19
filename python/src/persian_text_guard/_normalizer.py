"""Normalization and tokenizing. Port of ``js/src/normalizer.ts``.

Which ports ``dotnet/src/PersianTextGuard/PersianNormalizer.cs`` and ``PersianNormalization.cs``.
The internal functions take the UTF-16 view from ``_utf16``; the public ones convert to it on the way
in and back on the way out.
"""

from __future__ import annotations

import re
from collections.abc import Iterable
from typing import NamedTuple

from ._types import NormalizationPreset, NormalizationStep, NormalizationStepName, require_text, to_step
from ._unicode import (
    WHITE_SPACE_RUN,
    category_at,
    is_high_surrogate,
    is_letter,
    is_letter_or_digit,
    is_low_surrogate,
    is_nfkc,
    is_white_space,
    nfkc,
    to_lower_invariant,
)
from ._utf16 import from_view, to_view

# The .NET [Flags] enum PersianNormalization, with the same bit values.
NONE = 0
COMPATIBILITY_FORMS = 1 << 0
UNIFY_LETTERS = 1 << 1
REMOVE_DIACRITICS = 1 << 2
REMOVE_TATWEEL = 1 << 3
REMOVE_ZERO_WIDTH = 1 << 4
REMOVE_BIDI_CONTROLS = 1 << 5
ASCII_DIGITS = 1 << 6
LOWER_CASE = 1 << 7
COLLAPSE_WHITESPACE = 1 << 8
COLLAPSE_REPEATS = 1 << 9

STANDARD = COMPATIBILITY_FORMS | UNIFY_LETTERS | REMOVE_TATWEEL | REMOVE_BIDI_CONTROLS | COLLAPSE_WHITESPACE
COMPARISON = STANDARD | REMOVE_DIACRITICS | REMOVE_ZERO_WIDTH | ASCII_DIGITS | LOWER_CASE | COLLAPSE_REPEATS

COMPARISON_STEPS = COMPARISON
"""The bits of the ``"comparison"`` preset, the form the filter searches."""

_PRESETS = {"comparison": COMPARISON, "standard": STANDARD, "none": NONE}

_STEP_BITS = {
    NormalizationStep.COMPATIBILITY_FORMS: COMPATIBILITY_FORMS,
    NormalizationStep.UNIFY_LETTERS: UNIFY_LETTERS,
    NormalizationStep.REMOVE_DIACRITICS: REMOVE_DIACRITICS,
    NormalizationStep.REMOVE_TATWEEL: REMOVE_TATWEEL,
    NormalizationStep.REMOVE_ZERO_WIDTH: REMOVE_ZERO_WIDTH,
    NormalizationStep.REMOVE_BIDI_CONTROLS: REMOVE_BIDI_CONTROLS,
    NormalizationStep.ASCII_DIGITS: ASCII_DIGITS,
    NormalizationStep.LOWER_CASE: LOWER_CASE,
    NormalizationStep.COLLAPSE_WHITESPACE: COLLAPSE_WHITESPACE,
    NormalizationStep.COLLAPSE_REPEATS: COLLAPSE_REPEATS,
}

# The steps that map one unit to at most one unit, whatever its neighbours.
_PER_UNIT_STEPS = (
    UNIFY_LETTERS
    | REMOVE_DIACRITICS
    | REMOVE_TATWEEL
    | REMOVE_ZERO_WIDTH
    | REMOVE_BIDI_CONTROLS
    | ASCII_DIGITS
    | LOWER_CASE
)

PERSIAN_YEH = 0x06CC
PERSIAN_KEHEH = 0x06A9
HEH = 0x0647
ALEF = 0x0627

ARABIC_INDIC_ZERO = 0x0660
EXTENDED_ARABIC_INDIC_ZERO = 0x06F0

_UNIFY = {
    0x064A: PERSIAN_YEH,  # Arabic yeh
    0x0649: PERSIAN_YEH,  # alef maksura
    0x0643: PERSIAN_KEHEH,  # Arabic kaf
    0x0623: ALEF,  # alef with hamza above
    0x0625: ALEF,  # alef with hamza below
    0x0622: ALEF,  # alef with madda
    0x0629: HEH,  # teh marbuta
    # Letters from the Urdu, Kurdish and Pashto blocks that render as the Persian ones in most fonts.
    # NFKC leaves them alone because they are distinct letters, not compatibility forms, so a swash
    # kaf gets past an entry written with a normal one.
    0x06AA: PERSIAN_KEHEH,
    0x06AB: PERSIAN_KEHEH,
    0x06BE: HEH,
    0x06C0: HEH,
    0x06C1: HEH,
    0x06C3: HEH,
    0x06D5: HEH,
    0x06CD: PERSIAN_YEH,
    0x06CE: PERSIAN_YEH,
    0x06D0: PERSIAN_YEH,
    0x06D2: PERSIAN_YEH,
    0x06D3: PERSIAN_YEH,
    0x0671: ALEF,
    0x0672: ALEF,
    0x0673: ALEF,
    0x0675: ALEF,
}

_ZERO_WIDTH = frozenset({0x200B, 0x200C, 0x200D, 0x2060, 0xFEFF, 0x00AD})
_BIDI_CONTROLS = frozenset({0x200E, 0x200F, 0x061C, *range(0x202A, 0x202F), *range(0x2066, 0x206A)})

ZERO_WIDTH_NON_JOINER = "\N{ZERO WIDTH NON-JOINER}"
ZERO_WIDTH_JOINER = "\N{ZERO WIDTH JOINER}"

_TO_PERSIAN_DIGITS = str.maketrans(
    "0123456789", "".join(chr(EXTENDED_ARABIC_INDIC_ZERO + d) for d in range(10))
)

_LONE_SURROGATE = re.compile("[\ud800-\udbff](?![\udc00-\udfff])|(?<![\ud800-\udbff])[\udc00-\udfff]")
_SURROGATE = re.compile("[\ud800-\udfff]")
_REPEAT = re.compile(r"(.)\1{2,}", re.DOTALL)
_ASCII_WORD = re.compile("[A-Za-z0-9]+")

_WORD_CATEGORIES = frozenset({"Lu", "Ll", "Lt", "Lm", "Lo", "Mn", "Mc", "Me", "Nd", "Nl", "No"})
_COMBINING_CATEGORIES = frozenset({"Mn", "Mc", "Me"})


class Token(NamedTuple):
    """A token and where it starts and ends (exclusive) in the text it came from."""

    text: str
    start: int
    end: int


def resolve_steps(steps: object) -> int:
    """The step bits for a preset name or an iterable of steps (members or names).

    Raises:
        ValueError: An unknown preset or step name, or a ``str`` that is not a preset.
        TypeError: ``steps`` is neither a ``str`` nor an iterable, or a step is not a ``str``.
    """
    if isinstance(steps, str):
        preset = _PRESETS.get(str(steps))
        if preset is None:
            raise ValueError(
                f"unknown normalization preset '{steps}'; expected 'comparison', 'standard', 'none' "
                "or an iterable of steps"
            )
        return preset

    if not isinstance(steps, Iterable):
        raise TypeError(
            "steps must be 'comparison', 'standard', 'none' or an iterable of steps, "
            f"not {type(steps).__name__}"
        )

    bits = 0
    for step in steps:
        bits |= _STEP_BITS[to_step(step)]
    return bits


def normalize(
    text: str | None,
    steps: NormalizationPreset | Iterable[NormalizationStep | NormalizationStepName] = "comparison",
) -> str:
    """Normalize Persian text so that strings which look the same compare the same.

    Persian is written with character pairs that render near-identically but have different code
    points (Arabic yeh and Persian yeh, Arabic kaf and keheh), plus optional diacritics, invisible
    zero-width and bidi characters, and three digit ranges. A raw comparison against a word list or a
    search index is defeated by typing one character differently.

    Steps always run in the same order, whatever order an iterable gives them in. Never raises for a
    ``str``, including lone surrogates and noncharacters: a lone surrogate becomes U+FFFD when
    ``COMPATIBILITY_FORMS`` runs, as in .NET.

    Args:
        text: The text to normalize. ``None`` gives ``""``.
        steps: ``"comparison"`` (the default: every step, the lossy form to compare, search and filter
            on), ``"standard"`` (safe for text you store and show: compatibility forms, unified letters,
            no tatweel or bidi controls, collapsed whitespace), ``"none"``, or an iterable of
            :class:`NormalizationStep` members or their names.

    Returns:
        The normalized text.

    Raises:
        TypeError: ``text`` is not a ``str`` or ``None``, or ``steps`` is not a ``str`` or an iterable.
        ValueError: ``steps`` names an unknown preset or step.

    Example:
        >>> normalize("كتاب\N{ZERO WIDTH NON-JOINER}هاي  ۱۲ ABC")
        'کتابهای 12 abc'
    """
    value = require_text(text, "text")
    bits = resolve_steps(steps)
    if not value:
        return ""
    return from_view(normalize_with_map(to_view(value), bits, None))


def normalize_with_map(text: str, steps: int, map_: list[int] | None) -> str:
    """Normalize a view with step bits.

    When ``map_`` is given, it receives, for every unit of the result, the index in ``text`` it came
    from.
    """
    if not text:
        return ""

    if map_ is None:
        return _normalize(text, steps)

    # string.Normalize throws on a lone surrogate in .NET, which is what a message cut in the middle of
    # an emoji contains; the .NET port replaces them first, and so does this one, so every port
    # normalizes the same text.
    source_map: list[int] | None = None
    if steps & COMPATIBILITY_FORMS == 0:
        source = text
    else:
        source_map = []
        source = _normalize_compatibility_by_segment(replace_lone_surrogates(text), source_map)

    out: list[str] = []
    step_map: list[int] = []
    unify = steps & UNIFY_LETTERS != 0
    lower_case = steps & LOWER_CASE != 0
    ascii_digits = steps & ASCII_DIGITS != 0

    for index, raw in enumerate(source):
        if _is_removed(raw, steps):
            continue

        step_map.append(index if source_map is None else source_map[index])

        c = ord(raw)
        if unify:
            c = _UNIFY.get(c, c)

        if ascii_digits:
            if ARABIC_INDIC_ZERO <= c <= ARABIC_INDIC_ZERO + 9:
                c = 0x30 + (c - ARABIC_INDIC_ZERO)
            elif EXTENDED_ARABIC_INDIC_ZERO <= c <= EXTENDED_ARABIC_INDIC_ZERO + 9:
                c = 0x30 + (c - EXTENDED_ARABIC_INDIC_ZERO)

        unit = chr(c)
        out.append(to_lower_invariant(unit) if lower_case else unit)

    result = "".join(out)

    if steps & COLLAPSE_REPEATS != 0:
        result = _collapse_repeats_mapped(result, step_map)

    if steps & COLLAPSE_WHITESPACE != 0:
        result = _collapse_whitespace_mapped(result, step_map)

    map_.extend(step_map)
    return result


def _normalize(text: str, steps: int) -> str:
    # The same steps without a map, with whole-string passes: the per-unit steps are one str.translate
    # (research R3), and the collapsing steps are regular expressions.
    source = nfkc(replace_lone_surrogates(text)) if steps & COMPATIBILITY_FORMS else text

    per_unit = steps & _PER_UNIT_STEPS
    result = source.translate(_translation(per_unit)) if per_unit else source

    if steps & COLLAPSE_REPEATS != 0:
        result = _REPEAT.sub(_collapse_run, result)

    if steps & COLLAPSE_WHITESPACE != 0:
        result = WHITE_SPACE_RUN.sub(" ", result).strip(" ")

    return result


class _Translation(dict[int, str | None]):
    """A ``str.translate`` table for per-unit steps, filled in on first sight of each unit.

    Every thread computes the same value for a unit, so a racing write is harmless (research R17).
    """

    __slots__ = ("_steps",)

    def __init__(self, steps: int) -> None:
        super().__init__()
        self._steps = steps

    def __missing__(self, key: int) -> str | None:
        value = _translate_unit(chr(key), self._steps)
        self[key] = value
        return value


_translations: dict[int, _Translation] = {}


def _translation(steps: int) -> _Translation:
    table = _translations.get(steps)
    if table is None:
        table = _translations.setdefault(steps, _Translation(steps))
    return table


def _translate_unit(raw: str, steps: int) -> str | None:
    if _is_removed(raw, steps):
        return None

    c = ord(raw)
    if steps & UNIFY_LETTERS:
        c = _UNIFY.get(c, c)

    if steps & ASCII_DIGITS:
        if ARABIC_INDIC_ZERO <= c <= ARABIC_INDIC_ZERO + 9:
            c = 0x30 + (c - ARABIC_INDIC_ZERO)
        elif EXTENDED_ARABIC_INDIC_ZERO <= c <= EXTENDED_ARABIC_INDIC_ZERO + 9:
            c = 0x30 + (c - EXTENDED_ARABIC_INDIC_ZERO)

    unit = chr(c)
    return to_lower_invariant(unit) if steps & LOWER_CASE else unit


def _collapse_run(match: re.Match[str]) -> str:
    run = match.group()
    return run if is_white_space(run[0]) else run[:2]


def _normalize_compatibility_by_segment(text: str, map_: list[int]) -> str:
    """NFKC one segment at a time: a code point and the combining marks that follow it."""
    # Most messages are already in NFKC. Then every character maps to itself.
    if is_nfkc(text):
        map_.extend(range(len(text)))
        return text

    parts: list[str] = []
    i = 0
    length = len(text)

    while i < length:
        start = i
        i += _code_point_length(text, i)
        while i < length and _is_combining_mark(text, i):
            i += _code_point_length(text, i)

        # ASCII is already in normal form. A segment starting with a noncharacter is normalized around
        # it by nfkc, which is what the .NET port does to avoid string.Normalize throwing.
        segment = text[start:i] if i - start == 1 and text[start] < "\x80" else nfkc(text[start:i])

        map_.extend([start] * len(segment))
        parts.append(segment)

    return "".join(parts)


def _code_point_length(text: str, index: int) -> int:
    return (
        2
        if is_high_surrogate(text[index]) and index + 1 < len(text) and is_low_surrogate(text[index + 1])
        else 1
    )


def _is_combining_mark(text: str, index: int) -> bool:
    return category_at(text, index) in _COMBINING_CATEGORIES


def tokenize(text: str | None) -> list[str]:
    """Split text into word tokens.

    Tokens are split on whitespace, punctuation (ASCII, Persian «» ، ؛ ؟ ٫, and the rest of Unicode)
    and symbols, emoji included. Letters, digits, combining marks and the zero-width non-joiner stay
    inside tokens. Normalize first: tokens are only as consistent as the text they came from.

    Args:
        text: The text to split. ``None`` gives ``[]``.

    Returns:
        The tokens, in order, as a new list.

    Raises:
        TypeError: ``text`` is not a ``str`` or ``None``.

    Example:
        >>> tokenize("سلام، دنیا! خوبی؟")
        ['سلام', 'دنیا', 'خوبی']
    """
    value = require_text(text, "text")
    if not value:
        return []
    return [from_view(token.text) for token in tokenize_with_offsets(to_view(value))]


def tokenize_with_offsets(text: str) -> list[Token]:
    """:func:`tokenize` on a view, keeping where each token starts and ends in it."""
    if not text:
        return []

    if text.isascii():
        return [Token(m.group(), m.start(), m.end()) for m in _ASCII_WORD.finditer(text)]

    tokens: list[Token] = []
    start = -1

    for i in range(len(text) + 1):
        if i < len(text) and is_word_character(text, i):
            if start < 0:
                start = i
            continue

        if start >= 0:
            tokens.append(Token(text[start:i], start, i))
            start = -1

    return tokens


def is_word_character(text: str, index: int) -> bool:
    """Whether the unit at ``index`` belongs inside a word.

    A split-off word used to survive next to anything the tokenizer did not know about: «کیر», کیر😂.
    """
    c = text[index]
    if c < "\x80":
        return is_letter_or_digit(c)

    if is_low_surrogate(c) and index > 0 and is_high_surrogate(text[index - 1]):
        return is_word_character(text, index - 1)

    category = category_at(text, index)
    if category in _WORD_CATEGORIES:
        return True

    # The zero-width non-joiner and joiner are part of Persian spelling, but a variation selector or
    # zero-width joiner after an emoji is not.
    if category == "Cf":
        return c in (ZERO_WIDTH_NON_JOINER, ZERO_WIDTH_JOINER) and index > 0 and is_letter(text[index - 1])

    return False


def to_persian_digits(text: str | None) -> str:
    """Render ASCII digits as Persian digits (۰-۹), leaving everything else alone.

    Args:
        text: The text to convert. ``None`` gives ``""``.

    Returns:
        The text with ASCII digits replaced.

    Raises:
        TypeError: ``text`` is not a ``str`` or ``None``.

    Example:
        >>> to_persian_digits("2 ساعت پیش")
        '۲ ساعت پیش'
    """
    value = require_text(text, "text")
    if not value:
        return ""
    return from_view(value.translate(_TO_PERSIAN_DIGITS))


def to_ascii_digits(text: str | None) -> str:
    """Convert Persian and Arabic-Indic digits to ASCII, leaving everything else alone.

    Args:
        text: The text to convert. ``None`` gives ``""``.

    Returns:
        The text with Persian and Arabic-Indic digits replaced.

    Raises:
        TypeError: ``text`` is not a ``str`` or ``None``.

    Example:
        >>> to_ascii_digits("۱۴۰۴/۰۵/۱۴")
        '1404/05/14'
    """
    value = require_text(text, "text")
    if not value:
        return ""
    return from_view(normalize_with_map(to_view(value), ASCII_DIGITS, None))


def replace_lone_surrogates(text: str) -> str:
    """``text`` with every lone surrogate unit replaced by U+FFFD, as the .NET port does."""
    if _SURROGATE.search(text) is None:
        return text
    return _LONE_SURROGATE.sub("\N{REPLACEMENT CHARACTER}", text)


def _is_removed(raw: str, steps: int) -> bool:
    c = ord(raw)
    if steps & REMOVE_DIACRITICS and 0x064B <= c <= 0x0652:
        return True

    if steps & REMOVE_TATWEEL and c == 0x0640:
        return True

    if steps & REMOVE_ZERO_WIDTH and c in _ZERO_WIDTH:
        return True

    return bool(steps & REMOVE_BIDI_CONTROLS) and c in _BIDI_CONTROLS


def _collapse_repeats_mapped(value: str, map_: list[int]) -> str:
    result: list[str] = []
    kept: list[int] = []
    run_char = "\0"
    run_length = 0

    for i, c in enumerate(value):
        run_length = run_length + 1 if c == run_char else 1
        run_char = c

        if run_length <= 2 or is_white_space(c):
            result.append(c)
            kept.append(map_[i])

    map_[:] = kept
    return "".join(result)


def _collapse_whitespace_mapped(value: str, map_: list[int]) -> str:
    result: list[str] = []
    kept: list[int] = []
    pending_space = False
    pending_source = 0

    for i, c in enumerate(value):
        if is_white_space(c):
            pending_space = len(result) > 0
            pending_source = map_[i]
            continue

        if pending_space:
            result.append(" ")
            kept.append(pending_source)
            pending_space = False

        result.append(c)
        kept.append(map_[i])

    map_[:] = kept
    return "".join(result)
