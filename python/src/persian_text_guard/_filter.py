"""The profanity filter. Port of ``js/src/filter.ts``, which ports ``ProfanityFilter.cs``."""

from __future__ import annotations

from collections.abc import Iterable

from ._fold import fold
from ._normalizer import COMPARISON_STEPS, normalize_with_map, tokenize_with_offsets
from ._regions import MASK_LENGTH, Region, apply_mask, merge, to_candidate
from ._scan import (
    LOOKALIKE_CHARACTERS,
    MINIMUM_BROKEN_WORD_LENGTH,
    REPEATED_LETTERS,
    SPLIT_WORD,
    Entry,
    Hit,
    Key,
    Phrase,
    ScanState,
    scan,
)
from ._source_map import new_map_cache
from ._types import (
    BannedWord,
    EvasionKind,
    ProfanityFilterOptions,
    ProfanityMatch,
    WordMatchMode,
    require_text,
)
from ._unicode import is_control, is_letter_or_digit, is_surrogate, is_white_space
from ._utf16 import PositionMap, to_view

_EVASION_NAMES = (
    (REPEATED_LETTERS, EvasionKind.REPEATED_LETTERS),
    (LOOKALIKE_CHARACTERS, EvasionKind.LOOKALIKE_CHARACTERS),
    (SPLIT_WORD, EvasionKind.SPLIT_WORD),
)

_DEFAULT_OPTIONS = ProfanityFilterOptions()

_MASK_MESSAGE = (
    "The mask must be one character, and not a letter, a digit, whitespace, a control character, "
    "a surrogate or a character above U+FFFF."
)


class _Builder:
    """Collects the lookup tables while a filter is built."""

    __slots__ = ("anywhere", "anywhere_keys", "maskable", "phrases", "words")

    def __init__(self) -> None:
        self.words: dict[str, Entry] = {}
        self.phrases: dict[str, list[Phrase]] = {}
        self.anywhere: list[Key] = []
        self.maskable: list[Key] = []
        self.anywhere_keys: set[str] = set()

    def add(self, form: str, word: BannedWord, order: int) -> bool:
        if word.mode == WordMatchMode.ANYWHERE:
            if not form:
                return False

            if form not in self.anywhere_keys:
                self.anywhere_keys.add(form)
                key = Key(form, word, False, order)
                self.anywhere.append(key)
                if len(form) >= MINIMUM_BROKEN_WORD_LENGTH and " " not in form:
                    self.maskable.append(key)

            return True

        tokens = [token.text for token in tokenize_with_offsets(form)]
        if not tokens:
            return False

        if len(tokens) == 1:
            token = tokens[0]
            if token not in self.words:
                self.words[token] = Entry(word, order)
                if len(token) >= MINIMUM_BROKEN_WORD_LENGTH:
                    self.maskable.append(Key(token, word, True, order))

            return True

        self.phrases.setdefault(tokens[0], []).append(Phrase(tuple(tokens), word, order))
        return True


class ProfanityFilter:
    """Finds banned words in user text, including the spellings people use to get past a word list.

    ``normalize`` folds the Unicode tricks: Arabic yeh and kaf, zero-width characters, tatweel. What it
    cannot fold without damaging ordinary text are the tricks a person types on purpose: letters split
    apart, a key held down, digits and symbols for letters, filler inside a word, accents and look-alike
    Cyrillic letters. So the filter reads the text in several forms and reports a match if a word
    appears in any of them. The forms are only used to find matches and are never returned.

    Whole-word entries are looked up by token, so checking a message costs about the same against a
    list of 400 entries or 4,000. Persian whole-word entries also match with the common suffixes
    attached («جنده‌ها», «کیرتون»).

    Build one filter when your word list loads and share it: it never changes after construction, so it
    is safe to use from any number of threads, including on free-threaded CPython.

    Example:
        >>> filter = ProfanityFilter(WordList.persian_default())
        >>> filter.contains_profanity("ک.ی.ر")
        True
    """

    __slots__ = ("_state",)

    _state: ScanState

    def __init__(self, words: Iterable[BannedWord], options: ProfanityFilterOptions | None = None) -> None:
        """Prepare ``words`` for matching. Do this once, not per message.

        Args:
            words: The words to look for, for example ``WordList.persian_default()``. Any iterable of
                :class:`BannedWord`; it is read once, and entries are frozen, so changing the list
                afterwards has no effect.
            options: Which evasions to read through; all of them by default.

        Raises:
            TypeError: ``words`` is ``None``, a ``str`` or not iterable; an element is not a
                :class:`BannedWord` (the message names its position); or ``options`` is not a
                :class:`ProfanityFilterOptions`.
        """
        if words is None or isinstance(words, (str, bytes)) or not isinstance(words, Iterable):
            raise TypeError(f"words must be an iterable of BannedWord, not {type(words).__name__}")

        if options is None:
            options = _DEFAULT_OPTIONS
        elif not isinstance(options, ProfanityFilterOptions):
            raise TypeError(f"options must be a ProfanityFilterOptions or None, not {type(options).__name__}")

        fold_lookalike_characters = options.fold_lookalike_characters
        builder = _Builder()
        seen: set[tuple[WordMatchMode, str]] = set()
        count = 0

        # An entry's position in the list, duplicates included: it breaks ties between overlapping
        # matches of the same length, in favour of the entry listed first.
        for order, word in enumerate(words):
            if not isinstance(word, BannedWord):
                raise TypeError(
                    f"the word at position {order} must be a BannedWord, not {type(word).__name__}"
                )

            normalized = normalize_with_map(to_view(word.text), COMPARISON_STEPS, None)
            seen_key = (word.mode, normalized)
            if not normalized or seen_key in seen:
                continue

            seen.add(seen_key)
            added = builder.add(normalized, word, order)

            # The text is folded before matching, so an entry stored the way an evader types it ("k0s",
            # pasted from the message a moderator was reading) would never match the plain spelling the
            # folded text turns into. Keep a folded key beside the original.
            if fold_lookalike_characters:
                added = builder.add(fold(normalized), word, order) or added

            if added:
                count += 1

        self._state = ScanState(
            squeeze_repeated_letters=options.squeeze_repeated_letters,
            fold_lookalike_characters=fold_lookalike_characters,
            join_spaced_letters=options.join_spaced_letters,
            count=count,
            words=builder.words,
            phrases={token: tuple(phrases) for token, phrases in builder.phrases.items()},
            anywhere=tuple(builder.anywhere),
            maskable=tuple(builder.maskable),
        )

    def __setattr__(self, name: str, value: object) -> None:
        """Refuse every change after construction: a filter is immutable."""
        if name == "_state" and not hasattr(self, "_state"):
            object.__setattr__(self, name, value)
            return
        raise AttributeError(f"'ProfanityFilter' object attribute '{name}' is read-only")

    def __delattr__(self, name: str) -> None:
        """Refuse every change after construction: a filter is immutable."""
        raise AttributeError(f"'ProfanityFilter' object attribute '{name}' is read-only")

    def __repr__(self) -> str:
        """A short description with the entry count."""
        return f"ProfanityFilter(count={self._state.count})"

    @property
    def count(self) -> int:
        """The number of distinct entries the filter looks for.

        Spellings that normalize the same count once, and blank entries are ignored.
        """
        return self._state.count

    def contains_profanity(self, text: str | None) -> bool:
        """Whether ``text`` contains a banned word.

        Args:
            text: The message. ``None``, empty and whitespace-only text are clean. Any ``str`` is
                accepted, including lone surrogates and very long messages.

        Returns:
            ``True`` when a banned word is found.

        Raises:
            TypeError: ``text`` is not a ``str`` or ``None``.
        """
        value = require_text(text, "text")
        if value is None:
            return False
        return scan(self._state, to_view(value), None)[0]

    def find_match(self, text: str | None) -> ProfanityMatch | None:
        """The first banned word found in ``text``, or ``None`` when it is clean.

        The match's ``index`` and ``length`` give where it is in ``text`` as passed, in code points,
        widened to whole words. "First" means found with the least evasion undone, not earliest in the
        text.

        Args:
            text: The message. ``None`` is clean.

        Returns:
            The match, or ``None``.

        Raises:
            TypeError: ``text`` is not a ``str`` or ``None``.
        """
        value = require_text(text, "text")
        if value is None:
            return None

        view = to_view(value)
        found, first = scan(self._state, view, None)
        if not found or first is None:
            return None

        candidate = to_candidate(view, first, new_map_cache())
        region = Region(first.word, first.evasion, candidate.start, candidate.end - candidate.start)
        return _to_match(region, PositionMap(value, view))

    def find_matches(self, text: str | None) -> tuple[ProfanityMatch, ...]:
        """Every banned word in ``text``, in the order they appear.

        Each match's ``index`` and ``length`` refer to ``text`` exactly as passed, in code points, and
        cover whole words: a banned word inside "motherfucker" or «جنده‌ها» covers the whole word, and a
        disguised word such as "f u c k" or «ج.نده» covers its separators too.

        Matches are ordered by position and never overlap. Where entries overlap, one match covers the
        union of their regions, reporting the entry whose own text covered the most characters, or on a
        tie the entry listed first. Several occurrences inside one word ("fuckfuck") are one match, and
        repeats in separate words are separate matches.

        Args:
            text: The message. ``None`` is clean.

        Returns:
            The matches; empty for clean, empty or ``None`` text.

        Raises:
            TypeError: ``text`` is not a ``str`` or ``None``.
        """
        value = require_text(text, "text")
        if value is None:
            return ()

        view = to_view(value)
        regions = self._find_regions(view)
        if not regions:
            return ()

        positions = PositionMap(value, view)
        return tuple(_to_match(region, positions) for region in regions)

    def censor(self, text: str | None, mask: str = "*") -> str:
        """``text`` with every banned word hidden behind four ``mask`` characters.

        Each region ``find_matches`` reports is replaced by the same four-character mask, however long
        the text it hides. Whole words are hidden, and a disguised word is hidden with its separators.
        Every character outside a hidden region is returned exactly as passed, so the result is usually
        a different length from ``text``; positions from ``find_matches`` refer to the original.

        The result is always clean: ``contains_profanity`` returns ``False`` for it. Clean text is
        returned unchanged.

        Args:
            text: The message. ``None`` gives ``""``.
            mask: One character: any symbol or punctuation. Default ``"*"``. It is checked before
                ``text`` is looked at, even when ``text`` is ``None``.

        Returns:
            The censored text.

        Raises:
            TypeError: ``mask`` is not a ``str``, or ``text`` is not a ``str`` or ``None``.
            ValueError: ``mask`` is not exactly one character, or is above U+FFFF (a .NET mask is one
                UTF-16 unit), a letter, a digit, whitespace, a control character or a surrogate.
        """
        if not isinstance(mask, str):
            raise TypeError(f"mask must be a one-character str, not {type(mask).__name__}")

        mask = str(mask)
        if (
            len(mask) != 1
            or mask > "\U0000ffff"
            or is_letter_or_digit(mask)
            or is_white_space(mask)
            or is_control(mask)
            or is_surrogate(mask)
        ):
            raise ValueError(_MASK_MESSAGE)

        value = require_text(text, "text")
        if value is None:
            return ""

        regions = self._find_code_point_regions(value)
        if not regions:
            return value

        return self._censor_regions(value, regions, mask * MASK_LENGTH)

    def _find_regions(self, view: str) -> list[Region]:
        hits: list[Hit] = []
        if not scan(self._state, view, hits)[0]:
            return []

        map_cache = new_map_cache()
        return merge([to_candidate(view, hit, map_cache) for hit in hits])

    def _find_code_point_regions(self, text: str) -> list[tuple[int, int]]:
        # The regions of text, converted from view units to code points of text itself.
        view = to_view(text)
        regions = self._find_regions(view)
        if not regions:
            return []

        positions = PositionMap(text, view)
        return [positions.to_code_points(region.index, region.length) for region in regions]

    def _censor_regions(self, text: str, regions: list[tuple[int, int]], mask: str) -> str:
        """Replace each region with the mask, then keep going until the output is clean.

        The mask character is usually filler the filter drops to catch "f*ck", so masking can join the
        letters around a mask into a new word: "k kos i kos r" masks to "k **** i **** r", which reads
        as "kir". Each extra pass replaces letters with a mask that has none, so the letters only ever
        run out; the cap is a guard against a bug, and if it is ever reached the whole text becomes one
        mask rather than leak a word.
        """
        censored = apply_mask(text, regions, mask)
        passes_left = len(tokenize_with_offsets(to_view(text))) + 1

        while scan(self._state, to_view(censored), None)[0]:
            if passes_left == 0:
                return mask
            passes_left -= 1

            censored = apply_mask(censored, self._find_code_point_regions(censored), mask)

        return censored


def _to_match(region: Region, positions: PositionMap) -> ProfanityMatch:
    evasion = tuple(name for bit, name in _EVASION_NAMES if region.evasion & bit)
    index, length = positions.to_code_points(region.index, region.length)
    return ProfanityMatch(region.word, evasion, index, length)
