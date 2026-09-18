"""Where each character of a reading came from in the message. Port of ``js/src/source-map.ts``.

Which ports ``dotnet/src/PersianTextGuard/SourceMap.cs``. Positions are units of the UTF-16 view.
"""

from __future__ import annotations

from collections.abc import Callable
from dataclasses import dataclass
from enum import IntEnum

from ._fold import fold, squeeze
from ._normalizer import COMPARISON_STEPS, normalize_with_map
from ._unicode import is_white_space


class ReadingKind(IntEnum):
    """The lossy forms of a message the filter searches, in the order it searches them."""

    NORMALIZED = 0
    """Comparison-normalized."""

    SQUEEZED = 1
    """Normalized, with repeated letters squeezed to one."""

    FOLDED = 2
    """Normalized, with look-alike characters folded."""

    FOLDED_SQUEEZED = 3
    """Normalized, folded, then squeezed."""


@dataclass(frozen=True, slots=True)
class MappedText:
    """A reading of a message, with where each of its characters came from in the message.

    Two maps rather than one, so a fallback can widen in both directions: a match starting at reading
    index ``i`` starts no later than ``start_map[i]``, and one ending at ``j`` ends no earlier than
    ``end_map[j]``. When the mapping is exact they are the same list.

    Attributes:
        text: The reading, identical to the one the filter searched.
        start_map: For each reading unit, the earliest message index a match starting there may start at.
        end_map: For each reading unit, the latest message index a match ending there may end at.
    """

    text: str
    start_map: list[int]
    end_map: list[int]


MapCache = list[MappedText | None]

_Step = Callable[[str, list[int]], str]


def _fold_step(value: str, map_: list[int]) -> str:
    return fold(value, map_)


def _squeeze_step(value: str, map_: list[int]) -> str:
    return squeeze(value, map_)


def new_map_cache() -> MapCache:
    """A fresh cache with one slot per :class:`ReadingKind`."""
    return [None, None, None, None]


def build(original: str, kind: ReadingKind, cache: MapCache | None = None) -> MappedText:
    """The ``kind`` reading of ``original``, with its maps, reusing and filling ``cache``.

    Each reading is mapped once per message, and the folded and squeezed readings are derived from the
    mapped normalized one rather than rebuilt.

    The mapped reading is compared with the reading the filter searches. They only differ if
    segment-wise normalization disagrees with whole-string normalization, in which case the maps fall
    back to whole whitespace-separated chunks and then to the whole message: a coarser map can hide
    more than the word, but never less.
    """
    if cache is None:
        cache = new_map_cache()

    cached = cache[kind]
    if cached is not None:
        return cached

    if kind == ReadingKind.NORMALIZED:
        mapped = _normalized(original)
    elif kind == ReadingKind.SQUEEZED:
        mapped = _derive(build(original, ReadingKind.NORMALIZED, cache), _squeeze_step)
    elif kind == ReadingKind.FOLDED:
        mapped = _derive(build(original, ReadingKind.NORMALIZED, cache), _fold_step)
    else:
        mapped = _derive(build(original, ReadingKind.FOLDED, cache), _squeeze_step)

    cache[kind] = mapped
    return mapped


def _normalized(original: str) -> MappedText:
    """The mapped normalized reading, checked against the reading the filter searched.

    Folding and squeezing are plain functions of their input, so once this one matches, every reading
    derived from it matches too.
    """
    map_: list[int] = []
    text = normalize_with_map(original, COMPARISON_STEPS, map_)
    expected = normalize_with_map(original, COMPARISON_STEPS, None)

    if text != expected:
        return chunk_map(original, expected)

    return MappedText(text, map_, map_)


def _derive(source: MappedText, step: _Step) -> MappedText:
    step_map: list[int] = []
    text = step(source.text, step_map)

    starts = [source.start_map[i] for i in step_map]
    if source.start_map is source.end_map:
        return MappedText(text, starts, starts)

    ends = [source.end_map[i] for i in step_map]
    return MappedText(text, starts, ends)


def chunk_map(original: str, reading: str) -> MappedText:
    """Map each whitespace-separated chunk of ``reading`` to the matching chunk of ``original``.

    Falls back to the whole message when the chunk counts differ.
    """
    original_chunks = _chunks(original)
    reading_chunks = _chunks(reading)
    if len(original_chunks) != len(reading_chunks):
        return whole_message_map(original, reading)

    starts = [0] * len(reading)
    ends = [0] * len(reading)
    chunk = 0
    start = 0
    end = 0

    for i in range(len(reading)):
        if chunk < len(reading_chunks) and i >= reading_chunks[chunk][0]:
            start = original_chunks[chunk][0]
            end = original_chunks[chunk][1]
            if i == reading_chunks[chunk][1]:
                chunk += 1

        # Separators keep the previous chunk's values, which keeps both maps non-decreasing.
        starts[i] = start
        ends[i] = end

    return MappedText(reading, starts, ends)


def whole_message_map(original: str, reading: str) -> MappedText:
    """Map every reading character to the whole message."""
    last = max(0, len(original) - 1)
    return MappedText(reading, [0] * len(reading), [last] * len(reading))


def _chunks(text: str) -> list[tuple[int, int]]:
    result: list[tuple[int, int]] = []
    start = -1

    for i in range(len(text) + 1):
        separator = i == len(text) or is_white_space(text[i])
        if not separator and start < 0:
            start = i
        elif separator and start >= 0:
            result.append((start, i - 1))
            start = -1

    return result
