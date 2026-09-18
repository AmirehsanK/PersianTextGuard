"""Public value types: enumerations, entries, options, matches and the word-list error.

Port of ``js/src/types.ts`` with Python conventions (spec 004, research R5): the enumerations are
:class:`enum.StrEnum` whose values are the names the conformance corpus and the other ports use, and
the value objects are frozen, slotted dataclasses.
"""

from __future__ import annotations

from dataclasses import dataclass
from enum import StrEnum
from typing import Literal, TypeVar


class WordMatchMode(StrEnum):
    """How an entry is looked for in text.

    Every parameter that takes a mode also accepts its value, ``"wholeWord"`` or ``"anywhere"``.
    """

    WHOLE_WORD = "wholeWord"
    """Only as whole words, or a whole phrase. The safe default: «کس» as a whole word does not flag
    «کسی», and "ass" does not flag "class"."""

    ANYWHERE = "anywhere"
    """Anywhere, including inside longer words. Use it for stems whose every extension is also
    offensive, such as "fuck" covering "motherfucker"."""


class WordCategory(StrEnum):
    """What kind of word an entry is, so a site can choose what to block.

    The declaration order is the .NET enum's. Every parameter that takes a category also accepts its
    value, such as ``"slur"``.
    """

    UNCATEGORIZED = "uncategorized"
    """No category; the default for entries in your own lists. Never used by the bundled lists."""

    PROFANITY = "profanity"
    """General swearing and crude words: fuck, shit, «ریدم», «گوه»."""

    SEXUAL = "sexual"
    """Genitals, sex acts and pornography: «کیر», «سکس», cock, blowjob."""

    INSULT = "insult"
    """Strong insults, including the family and honour insults Persian is built on: «کسکش»,
    «مادرجنده», «بی‌ناموس», bastard."""

    SLUR = "slur"
    """Hate speech against a group (race, ethnicity, religion, sexual orientation, gender,
    disability)."""

    HARASSMENT = "harassment"
    """Telling someone to hurt themselves, or to shut up: kys, «خفه شو»."""

    MILD = "mild"
    """Rude in context but ordinary words otherwise: «آشغال», «گوز», «دلقک», damn, crap. Left out of
    ``WordList.persian_default()`` because blocking them rejects normal messages."""


class EvasionKind(StrEnum):
    """An evasion that had to be undone before a banned word showed up.

    A match lists its evasions in this declaration order, as the conformance corpus records them.
    """

    REPEATED_LETTERS = "repeatedLetters"
    """A letter was held down: "fuuuck", «کیییر»."""

    LOOKALIKE_CHARACTERS = "lookalikeCharacters"
    """Digits, symbols or look-alike letters stood in for letters, or filler was typed inside the
    word, or symbols masked some of its letters: "sh1t", "$hit", "f*ck", "fück"."""

    SPLIT_WORD = "splitWord"
    """The word was broken up with spaces or punctuation: "f u c k", "fu ck", «ج.نده»."""


class NormalizationStep(StrEnum):
    """One step ``normalize`` can apply. Steps always run in the order declared here."""

    COMPATIBILITY_FORMS = "compatibilityForms"
    """Unicode compatibility normalization (NFKC): Arabic presentation forms such as ﻙ become base
    letters, and full-width Latin becomes ASCII."""

    UNIFY_LETTERS = "unifyLetters"
    """Arabic yeh and alef maksura to Persian yeh, Arabic kaf to keheh, hamza alef forms to bare alef,
    teh marbuta to heh, and look-alike letters from the Urdu, Kurdish and Pashto blocks to the Persian
    ones."""

    REMOVE_DIACRITICS = "removeDiacritics"
    """Arabic diacritics (harakat)."""

    REMOVE_TATWEEL = "removeTatweel"
    """Tatweel (ـ), the stretching character."""

    REMOVE_ZERO_WIDTH = "removeZeroWidth"
    """Zero-width non-joiner, joiner, space, word joiner, byte-order mark and soft hyphen. The
    zero-width non-joiner is correct Persian spelling, so this belongs in comparison, not in text you
    display."""

    REMOVE_BIDI_CONTROLS = "removeBidiControls"
    """LRM, RLM, embeddings, overrides and isolates."""

    ASCII_DIGITS = "asciiDigits"
    """Persian and Arabic-Indic digits to ASCII 0-9."""

    LOWER_CASE = "lowerCase"
    """Invariant lower-casing, one character at a time."""

    COLLAPSE_WHITESPACE = "collapseWhitespace"
    """Every run of whitespace to a single space, and trimmed."""

    COLLAPSE_REPEATS = "collapseRepeats"
    """Runs of the same character cut to two, so «سسسسلام» and "heeeey" cannot slip past an entry,
    while real doubled letters survive."""


WordMatchModeName = Literal["wholeWord", "anywhere"]
"""The value of a :class:`WordMatchMode` member, accepted wherever a mode is."""

WordCategoryName = Literal["uncategorized", "profanity", "sexual", "insult", "slur", "harassment", "mild"]
"""The value of a :class:`WordCategory` member, accepted wherever a category is."""

NormalizationStepName = Literal[
    "compatibilityForms",
    "unifyLetters",
    "removeDiacritics",
    "removeTatweel",
    "removeZeroWidth",
    "removeBidiControls",
    "asciiDigits",
    "lowerCase",
    "collapseWhitespace",
    "collapseRepeats",
]
"""The value of a :class:`NormalizationStep` member, accepted wherever a step is."""

NormalizationPreset = Literal["comparison", "standard", "none"]
"""A named set of normalization steps: ``"comparison"`` (every step), ``"standard"`` or ``"none"``."""


_E = TypeVar("_E", bound=StrEnum)


def _to_member(enum: type[_E], value: object, what: str) -> _E:
    if isinstance(value, enum):
        return value
    if not isinstance(value, str):
        raise TypeError(f"{what} must be a {enum.__name__} or its name, not {type(value).__name__}")
    try:
        return enum(str(value))
    except ValueError:
        names = ", ".join(member.value for member in enum)
        raise ValueError(f"unknown {what} '{value}'; expected one of: {names}") from None


def to_mode(value: object) -> WordMatchMode:
    """The :class:`WordMatchMode` a member or its name stands for."""
    return _to_member(WordMatchMode, value, "word match mode")


def to_category(value: object) -> WordCategory:
    """The :class:`WordCategory` a member or its name stands for."""
    return _to_member(WordCategory, value, "word category")


def to_step(value: object) -> NormalizationStep:
    """The :class:`NormalizationStep` a member or its name stands for."""
    return _to_member(NormalizationStep, value, "normalization step")


def require_text(value: object, name: str) -> str | None:
    """``None`` for ``None``, the plain ``str`` value of a string, and ``TypeError`` otherwise.

    A ``str`` subclass is converted to ``str`` first, so an overridden method cannot change the
    results (research R18).
    """
    if value is None:
        return None
    if isinstance(value, str):
        return str(value)
    raise TypeError(f"{name} must be a str or None, not {type(value).__name__}")


@dataclass(frozen=True, slots=True, init=False)
class BannedWord:
    """A word or phrase for a ``ProfanityFilter`` to look for.

    Entries are immutable, and compare and hash by all three fields. The filter copies what it needs
    when it is built, and a match reports the entry object it was given.

    Attributes:
        text: The word or phrase, in any spelling; it is normalized when the filter is built. Blank
            text is ignored by the filter, not rejected.
        mode: Whole words only (the default), or anywhere in the text.
        category: What kind of word this is. Bundled entries always have one; your own default to
            ``WordCategory.UNCATEGORIZED``.
    """

    text: str
    mode: WordMatchMode
    category: WordCategory

    def __init__(
        self,
        text: str,
        mode: WordMatchMode | WordMatchModeName = WordMatchMode.WHOLE_WORD,
        category: WordCategory | WordCategoryName = WordCategory.UNCATEGORIZED,
    ) -> None:
        """Create an entry.

        Args:
            text: The word or phrase.
            mode: A :class:`WordMatchMode`, or its name (``"wholeWord"`` or ``"anywhere"``).
            category: A :class:`WordCategory`, or its name (``"slur"``, ...).

        Raises:
            TypeError: ``text`` is not a ``str``, or ``mode`` or ``category`` is not a member or a
                ``str``.
            ValueError: ``mode`` or ``category`` names no member.
        """
        if not isinstance(text, str):
            raise TypeError(f"text must be a str, not {type(text).__name__}")
        object.__setattr__(self, "text", str(text))
        object.__setattr__(self, "mode", to_mode(mode))
        object.__setattr__(self, "category", to_category(category))


@dataclass(frozen=True, slots=True, kw_only=True)
class ProfanityFilterOptions:
    """Which evasions a ``ProfanityFilter`` reads through. Every option defaults to ``True``.

    Attributes:
        squeeze_repeated_letters: Undo held keys: "fuuuck" is also read as "fuck". Only ever used to
            find a match, so squeezing "pass" to "pas" does no harm.
        fold_lookalike_characters: Read digits, symbols, accented letters and Cyrillic or Greek
            look-alikes as the Latin letters they imitate ("sh1t", "$hit", "k0s", "fück"), drop filler
            and emoji typed inside a word ("f*ck"), and read symbols masking letters as those letters
            ("f**k", "c*nt").
        join_spaced_letters: Put split words back together: runs of single letters ("f u c k",
            «ک ی ر»), punctuation inside a word («ج.نده», "kos_kesh"), and a word split once ("fu ck",
            «کی ر») when the halves join into exactly an entry. Ordinary words are never glued into
            something else: "push it" does not match "shit".
    """

    squeeze_repeated_letters: bool = True
    fold_lookalike_characters: bool = True
    join_spaced_letters: bool = True

    def __post_init__(self) -> None:
        """Reject values that are not ``bool``: a truthy string would be a silent mistake."""
        for name in ("squeeze_repeated_letters", "fold_lookalike_characters", "join_spaced_letters"):
            value = getattr(self, name)
            if not isinstance(value, bool):
                raise TypeError(f"{name} must be a bool, not {type(value).__name__}")


@dataclass(frozen=True, slots=True)
class ProfanityMatch:
    """A banned word found by ``ProfanityFilter.find_match`` or ``ProfanityFilter.find_matches``.

    Attributes:
        word: The entry that matched: the same object the filter was given.
        evasion: What had to be undone to find it, in the order ``REPEATED_LETTERS``,
            ``LOOKALIKE_CHARACTERS``, ``SPLIT_WORD``. Empty when the word was there as written, after
            normalization. Useful for moderation logs.
        index: The first character of the matched words in the text as it was passed, in code points
            (Python string indexes), so ``text[index:index + length]`` is the matched region. The
            region always covers whole words: a match inside "motherfucker" or «جنده‌ها» starts at the
            start of that word.
        length: How many code points the matched words span. Separators inside a disguised word
            ("f u c k", «ج.نده») are part of the span. Always at least 1.
    """

    word: BannedWord
    evasion: tuple[EvasionKind, ...]
    index: int
    length: int


class WordListFormatError(ValueError):
    """Raised by ``WordList.parse`` and ``WordList.load`` when a section names an unknown category.

    A heading is a category name in any letter case, with spaces allowed (``[insult]``,
    ``[ Insult ]``). Numbers are not category names: ``[3]`` raises this error.

    Attributes:
        line: The 1-based line of the unknown heading.
    """

    line: int

    def __init__(self, message: str, line: int) -> None:
        """Create the error.

        Args:
            message: The message, naming the line and the heading.
            line: The 1-based line of the unknown heading.
        """
        super().__init__(message)
        self.line = line
