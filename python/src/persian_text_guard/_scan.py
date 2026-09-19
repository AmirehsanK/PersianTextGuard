"""The readings, token and phrase lookup, anywhere entries, joined letters and broken chunks.

Port of ``js/src/scan.ts``, which ports ``dotnet/src/PersianTextGuard/ProfanityFilter.Scan.cs``. The
character-level helpers (fold, squeeze, filler, ...) are in ``_fold``. Every text here is a UTF-16 view,
and every position a unit of it.
"""

from __future__ import annotations

import re
from dataclasses import dataclass
from typing import NamedTuple

from ._fold import enclosed_letter, fold, fold_character, squeeze
from ._normalizer import COMPARISON_STEPS, Token, normalize_with_map, tokenize_with_offsets
from ._source_map import ReadingKind
from ._types import BannedWord
from ._unicode import (
    WHITE_SPACE_RUN,
    category_at,
    category_of_unit,
    combine,
    is_high_surrogate,
    is_letter,
    is_letter_or_digit,
    is_low_surrogate,
)

# The .NET [Flags] EvasionKind bits, used internally so evasions compare and combine as in .NET.
EVASION_NONE = 0
REPEATED_LETTERS = 1 << 0
LOOKALIKE_CHARACTERS = 1 << 1
SPLIT_WORD = 1 << 2

MINIMUM_BROKEN_WORD_LENGTH = 4
"""A word split in two or masked with symbols is only matched against entries at least this long.

Shorter ones ("ass", «کون») turn up by accident in ordinary text broken that way.
"""

# Longest first, so «هایی» is tried before «ها».
PERSIAN_SUFFIXES = (
    "هاشون",
    "هاتون",
    "هامون",
    "هایی",
    "های",
    "هاش",
    "هات",
    "هام",
    "ها",
    "تون",
    "شون",
    "مون",
    "ای",
    "یی",
    "اش",
    "ات",
    "ام",
    "ا",
)
_PERSIAN_SUFFIX_SET = frozenset(PERSIAN_SUFFIXES)
_HA = "ها"
_HEH = "ه"
_VAV = "و"

_LATIN_WORD = re.compile("[a-z]+")
_LETTER_OR_DIGIT_CATEGORIES = frozenset({"Lu", "Ll", "Lt", "Lm", "Lo", "Nd"})
_DROPPED_CATEGORIES = frozenset({"Mn", "Mc", "Me", "Cf"})
_MASK = "\0"


class Entry(NamedTuple):
    """An entry as the filter looks it up: its position in the list breaks ties."""

    word: BannedWord
    order: int


class Key(NamedTuple):
    """What is searched for (.NET ``Key``)."""

    text: str
    word: BannedWord
    whole_word: bool
    order: int


class Phrase(NamedTuple):
    """A phrase entry, one token per word (.NET ``Phrase``)."""

    tokens: tuple[str, ...]
    word: BannedWord
    order: int


@dataclass(frozen=True, slots=True)
class ScanState:
    """The filter's lookups, built once and only read afterwards."""

    squeeze_repeated_letters: bool
    fold_lookalike_characters: bool
    join_spaced_letters: bool
    count: int
    words: dict[str, Entry]
    phrases: dict[str, tuple[Phrase, ...]]
    anywhere: tuple[Key, ...]
    maskable: tuple[Key, ...]


class Hit(NamedTuple):
    """A banned word found in one reading, before it is mapped back to the message (.NET ``Hit``)."""

    reading: ReadingKind
    start: int
    end: int
    word: BannedWord
    order: int
    evasion: int


class _Sink:
    """Collects hits: either the first one only, or all of them."""

    __slots__ = ("all", "first")

    def __init__(self, all_: list[Hit] | None) -> None:
        self.all = all_
        self.first: Hit | None = None

    def report(self, hit: Hit) -> bool:
        """Record a hit. Returns ``True`` when the scan should stop, which is only when it wants the first."""
        if self.all is None:
            self.first = hit
            return True

        self.all.append(hit)
        return False


def _is_null_or_white_space(text: str | None) -> bool:
    return not text or WHITE_SPACE_RUN.fullmatch(text) is not None


def scan(state: ScanState, text: str | None, all_: list[Hit] | None) -> tuple[bool, Hit | None]:
    """Search ``text`` for banned words.

    With ``all_`` ``None`` it stops at the first hit and returns it; otherwise it appends every hit to
    ``all_``. Returns whether anything was found, and the first hit.

    Every capability goes through here, so they cannot disagree about whether a message is clean. The
    order is the one ``find_match`` has always used: the most literal reading first, and within a reading
    tokens and phrases, anywhere entries, joined single letters, a word split once. So the first hit is
    the least evasion needed.
    """
    sink = _Sink(all_)
    if text is None or _is_null_or_white_space(text) or state.count == 0:
        return False, None

    normalized = normalize_with_map(text, COMPARISON_STEPS, None)
    folded = fold(normalized) if state.fold_lookalike_characters else normalized

    readings: list[tuple[str, ReadingKind, int]] = [(normalized, ReadingKind.NORMALIZED, EVASION_NONE)]

    if state.squeeze_repeated_letters:
        readings.append((squeeze(normalized), ReadingKind.SQUEEZED, REPEATED_LETTERS))

    if state.fold_lookalike_characters:
        readings.append((folded, ReadingKind.FOLDED, LOOKALIKE_CHARACTERS))
        if state.squeeze_repeated_letters:
            readings.append(
                (squeeze(folded), ReadingKind.FOLDED_SQUEEZED, LOOKALIKE_CHARACTERS | REPEATED_LETTERS)
            )

    checked_readings: set[str] = set()

    for reading, kind, evasion in readings:
        # On ordinary text most readings are the same string.
        if reading in checked_readings:
            continue

        checked_readings.add(reading)

        tokens = tokenize_with_offsets(reading)
        if _match_tokens(state, tokens, kind, evasion, sink) or _match_anywhere(
            state, reading, None, kind, evasion, sink
        ):
            return True, sink.first

        if state.join_spaced_letters:
            split = evasion | SPLIT_WORD

            joined = try_join_single_letters(tokens)
            if joined is not None and (
                _match_tokens(state, joined.tokens, kind, split, sink)
                or _match_anywhere(state, joined.text, joined.map, kind, split, sink)
            ):
                return True, sink.first

            if _match_split_halves(state, tokens, kind, split, sink):
                return True, sink.first

    found = _match_broken_chunks(state, normalized, sink) or (all_ is not None and len(all_) > 0)
    return found, sink.first


def _match_tokens(
    state: ScanState, tokens: list[Token], kind: ReadingKind, evasion: int, sink: _Sink
) -> bool:
    for i, token in enumerate(tokens):
        entry = _try_find_word(state, token.text)
        if entry is not None and sink.report(
            Hit(kind, token.start, token.end, entry.word, entry.order, evasion)
        ):
            return True

        phrases = state.phrases.get(token.text)
        if phrases is None:
            continue

        for phrase in phrases:
            if _phrase_starts_at(tokens, i, phrase.tokens) and sink.report(
                Hit(
                    kind,
                    token.start,
                    tokens[i + len(phrase.tokens) - 1].end,
                    phrase.word,
                    phrase.order,
                    evasion,
                )
            ):
                return True

    return False


def _match_anywhere(
    state: ScanState,
    haystack: str,
    map_: list[int] | None,
    kind: ReadingKind,
    evasion: int,
    sink: _Sink,
) -> bool:
    """Anywhere entries inside ``haystack``.

    When the haystack is not the reading itself, ``map_`` gives each of its units' position in the
    reading.
    """
    for key in state.anywhere:
        index = haystack.find(key.text)

        while index >= 0:
            last = index + len(key.text) - 1
            start = index if map_ is None else map_[index]
            end = last + 1 if map_ is None else map_[last] + 1

            if sink.report(Hit(kind, start, end, key.word, key.order, evasion)):
                return True

            index = haystack.find(key.text, index + 1)

    return False


class Joined(NamedTuple):
    """The result of joining runs of single letters: tokens, their text joined by spaces, and a map."""

    tokens: list[Token]
    text: str
    map: list[int]


def try_join_single_letters(tokens: list[Token]) -> Joined | None:
    """Join runs of two or more single-letter tokens into one token: "f u c k" is "fuck".

    Returns ``None`` when there is no such run. The joined text is the tokens separated by spaces, and
    the map gives each of its units' position in the reading.
    """
    has_run = False
    i = 0
    while i + 1 < len(tokens) and not has_run:
        has_run = _is_single_letter(tokens[i]) and _is_single_letter(tokens[i + 1])
        i += 1

    if not has_run:
        return None

    result: list[Token] = []
    parts: list[str] = []
    text_length = 0
    positions: list[int] = []

    i = 0
    while i < len(tokens):
        if text_length > 0:
            # The separating space maps to where the previous token ended, keeping the map in order.
            parts.append(" ")
            text_length += 1
            positions.append(positions[-1])

        if not _is_single_letter(tokens[i]):
            token = tokens[i]
            i += 1
            result.append(token)
            parts.append(token.text)
            text_length += len(token.text)
            positions.extend(range(token.start, token.start + len(token.text)))
            continue

        # A run of single letters, including a run of one, which stays as it was.
        run: list[str] = []
        first = tokens[i]
        last = first
        while i < len(tokens) and _is_single_letter(tokens[i]):
            last = tokens[i]
            i += 1
            run.append(last.text)
            positions.append(last.start)

        run_text = "".join(run)
        parts.append(run_text)
        text_length += len(run_text)
        result.append(Token(run_text, first.start, last.end))

    return Joined(result, "".join(parts), positions)


def _is_single_letter(token: Token) -> bool:
    return len(token.text) == 1 and is_letter(token.text)


def _match_split_halves(
    state: ScanState, tokens: list[Token], kind: ReadingKind, evasion: int, sink: _Sink
) -> bool:
    """A word split once: "fu ck" in Latin letters, «کی ر» in Persian.

    Only when the halves join into exactly an entry, so "push it" never becomes "pushit" and matches
    "shit".

    Two Latin halves need at least two letters each ("it's hit" is not "shit"). Persian is the other way
    round: two real words joined make ordinary phrases look like insults («هر کس ده تا» holds «کسده»),
    but a stray single letter next to a word is a split. «و» ("and") is the one Persian letter that
    stands alone in ordinary text.
    """
    for i in range(len(tokens) - 1):
        first_half = tokens[i].text
        second_half = tokens[i + 1].text
        length = len(first_half) + len(second_half)

        latin = (
            len(first_half) >= 2
            and len(second_half) >= 2
            and length >= MINIMUM_BROKEN_WORD_LENGTH
            and _is_latin_word(first_half)
            and _is_latin_word(second_half)
        )
        persian = (
            (len(first_half) == 1) != (len(second_half) == 1)
            and length >= 3
            and _VAV not in (first_half, second_half)
            and _is_persian_letter(first_half[0])
            and _is_persian_letter(second_half[0])
        )

        if not latin and not persian:
            continue

        joined = first_half + second_half
        entry = state.words.get(joined)
        if entry is not None and sink.report(
            Hit(kind, tokens[i].start, tokens[i + 1].end, entry.word, entry.order, evasion)
        ):
            return True

        for key in state.anywhere:
            if key.text == joined and sink.report(
                Hit(kind, tokens[i].start, tokens[i + 1].end, key.word, key.order, evasion)
            ):
                return True

    return False


def _match_broken_chunks(state: ScanState, normalized: str, sink: _Sink) -> bool:
    """Words broken up by punctuation or symbols inside them: «ج.نده», "kos_kesh", "f**k", "a$$hole".

    The whole-word reading splits these into pieces, and dropping the symbols only helps when they were
    added rather than typed in place of a letter. So each space-separated chunk is tried twice: with the
    symbols removed, and with each symbol standing for any one letter. A mask only matches entries of
    ``MINIMUM_BROKEN_WORD_LENGTH`` or more letters with at least half of them showing.
    """
    if not state.join_spaced_letters and not state.fold_lookalike_characters:
        return False

    offset = 0
    for chunk in normalized.split(" "):
        chunk_offset = offset
        offset += len(chunk) + 1

        masked = _masked_pattern(chunk)
        if masked is None or masked.masks == 0 or masked.letters == 0:
            continue

        pattern = masked.pattern
        start = chunk_offset + masked.trimmed_start
        end = chunk_offset + masked.trimmed_end

        if state.join_spaced_letters:
            stripped = pattern.replace(_MASK, "")
            if len(stripped) >= 3:
                entry = _try_find_word(state, stripped)
                if entry is not None and sink.report(
                    Hit(ReadingKind.NORMALIZED, start, end, entry.word, entry.order, SPLIT_WORD)
                ):
                    return True

        if not state.fold_lookalike_characters:
            continue

        for key in state.maskable:
            fits = (
                len(key.text) == len(pattern) and _fits_mask(pattern, 0, key.text)
                if key.whole_word
                else _fits_mask_anywhere(pattern, key.text)
            )

            if fits and sink.report(
                Hit(ReadingKind.NORMALIZED, start, end, key.word, key.order, LOOKALIKE_CHARACTERS)
            ):
                return True

    return False


def _is_letter_or_digit_at(text: str, index: int) -> bool:
    """Like .NET ``char.IsLetterOrDigit(string, int)``: a valid surrogate pair is read as one code point."""
    return category_at(text, index) in _LETTER_OR_DIGIT_CATEGORIES


class _MaskedPattern(NamedTuple):
    pattern: str
    masks: int
    letters: int
    trimmed_start: int
    trimmed_end: int


def _masked_pattern(chunk: str) -> _MaskedPattern | None:
    """The chunk with outer punctuation trimmed, letters and digits folded, and combining marks dropped.

    Every other character is replaced by the mask unit U+0000. ``None`` when fewer than three units
    are left.
    """
    masks = 0
    letters = 0

    start = 0
    end = len(chunk)
    while start < end and not _is_letter_or_digit_at(chunk, start):
        start += 1

    while end > start and not _is_letter_or_digit_at(chunk, end - 1):
        end -= 1

    if end - start < 3:
        return None

    pattern: list[str] = []

    i = start
    while i < end:
        c = chunk[i]

        if is_high_surrogate(c) and i + 1 < end and is_low_surrogate(chunk[i + 1]):
            letter = enclosed_letter(ord(combine(c, chunk[i + 1])))
            i += 2
            if letter:
                pattern.append(letter)
                letters += 1
            else:
                pattern.append(_MASK)
                masks += 1
            continue

        # The char overload here (unlike the trimming above): a lone unit, not a code point.
        if is_letter_or_digit(c):
            pattern.append(fold_character(c, True))
            if is_letter(c):
                letters += 1
        elif category_of_unit(c) not in _DROPPED_CATEGORIES:
            pattern.append(_MASK)
            masks += 1

        i += 1

    return _MaskedPattern("".join(pattern), masks, letters, start, end)


def _fits_mask_anywhere(pattern: str, key: str) -> bool:
    return any(_fits_mask(pattern, offset, key) for offset in range(len(pattern) - len(key) + 1))


def _fits_mask(pattern: str, offset: int, key: str) -> bool:
    masks = 0

    for i, k in enumerate(key):
        c = pattern[offset + i]
        if c == _MASK:
            masks += 1
        elif c != k:
            return False

    return masks > 0 and masks * 2 <= len(key)


def _try_find_word(state: ScanState, token: str) -> Entry | None:
    """An entry for the token, as written or with a Persian suffix attached."""
    direct = state.words.get(token)
    if direct is not None:
        return direct

    if len(token) < 4 or not _is_persian_letter(token[0]):
        return None

    for suffix in PERSIAN_SUFFIXES:
        if not _has_suffix(token, suffix):
            continue

        stem = token[: len(token) - len(suffix)]

        # «جندها»: the final heh of «جنده» is often dropped before «ها».
        entry = state.words.get(stem)
        if entry is None and suffix.startswith(_HA):
            entry = state.words.get(stem + _HEH)
        if entry is not None:
            return entry

    return None


def _phrase_starts_at(tokens: list[Token], start: int, phrase: tuple[str, ...]) -> bool:
    if start + len(phrase) > len(tokens):
        return False

    for k in range(1, len(phrase) - 1):
        if tokens[start + k].text != phrase[k]:
            return False

    # The last word of a Persian phrase takes suffixes like a single word does: «بی ناموس‌ها».
    last = tokens[start + len(phrase) - 1].text
    key = phrase[-1]
    if last == key:
        return True

    if not _is_persian_letter(key[0]) or len(last) <= len(key) or not last.startswith(key):
        return False

    suffix = last[len(key) :]
    return suffix in _PERSIAN_SUFFIX_SET and _has_suffix(last, suffix)


def _has_suffix(token: str, suffix: str) -> bool:
    """A one-letter suffix needs a four-letter stem: «کیرا» is also the name Kira."""
    return token.endswith(suffix) and len(token) - len(suffix) >= (4 if len(suffix) == 1 else 3)


def _is_persian_letter(c: str) -> bool:
    return 0x0600 <= ord(c) <= 0x06FF and is_letter(c)


def _is_latin_word(token: str) -> bool:
    return _LATIN_WORD.fullmatch(token) is not None
