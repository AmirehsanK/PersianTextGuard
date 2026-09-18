"""Mapping hits back to the message, widening to whole words, merging overlaps. Port of ``js/src/regions.ts``.

Which ports ``dotnet/src/PersianTextGuard/ProfanityFilter.Regions.cs``. Positions are units of the
UTF-16 view; ``_filter`` converts regions to code points and splices the caller's own string.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import NamedTuple

from ._normalizer import is_word_character
from ._scan import Hit
from ._source_map import MapCache, build
from ._types import BannedWord
from ._unicode import is_high_surrogate, is_low_surrogate

MASK_LENGTH = 4
"""Every hidden word gets the same mask, so its length says nothing about the word (.NET ``MaskLength``)."""


class Candidate(NamedTuple):
    """A hit mapped back to the message and widened to whole words (.NET ``Candidate``)."""

    start: int
    end: int
    hit_length: int
    word: BannedWord
    order: int
    evasion: int


@dataclass(frozen=True, slots=True)
class Region:
    """A merged region, before it becomes a public match."""

    word: BannedWord
    evasion: int
    index: int
    length: int


def to_candidate(original: str, hit: Hit, map_cache: MapCache) -> Candidate:
    """Where ``hit`` is in ``original``, widened to the whole words it touches.

    The reading's source map is built on first use and kept in ``map_cache``, so a message with many hits
    maps each reading once.
    """
    mapped = build(original, hit.reading, map_cache)

    start = mapped.start_map[hit.start]
    last = mapped.end_map[hit.end - 1]
    end = last + (2 if _is_surrogate_pair_at(original, last) else 1)
    hit_length = end - start

    # Whole words: the tokenizer's own rule decides where a word ends, so a suffix joined by a
    # zero-width non-joiner comes along and an emoji or comma does not. The rule answers the same for
    # both halves of a surrogate pair, so the region cannot split one.
    while start > 0 and is_word_character(original, start - 1):
        start -= 1

    while end < len(original) and is_word_character(original, end):
        end += 1

    return Candidate(start, end, hit_length, hit.word, hit.order, hit.evasion)


def _same_word(a: BannedWord, b: BannedWord) -> bool:
    # .NET compares the BannedWord record struct by value, and so does the dataclass.
    return a is b or a == b


def merge(candidates: list[Candidate]) -> list[Region]:
    """Merge overlapping candidates into one match each.

    The region is their union. The entry is the one whose hit covered the most characters before
    widening, so "motherfucker" beats the "fuck" inside it even though both widen to the same word, with
    ties going to the entry listed first. The evasion is the least that entry needed.
    """
    candidates.sort(key=lambda c: (c.start, -c.end))

    regions: list[Region] = []
    i = 0

    while i < len(candidates):
        cluster_start = candidates[i].start
        cluster_end = candidates[i].end
        best = candidates[i]

        following = i + 1
        while following < len(candidates) and candidates[following].start < cluster_end:
            candidate = candidates[following]
            cluster_end = max(cluster_end, candidate.end)
            if candidate.hit_length > best.hit_length or (
                candidate.hit_length == best.hit_length and candidate.order < best.order
            ):
                best = candidate

            following += 1

        evasion = best.evasion
        for k in range(i, following):
            if _same_word(candidates[k].word, best.word) and candidates[k].evasion < evasion:
                evasion = candidates[k].evasion

        regions.append(Region(best.word, evasion, cluster_start, cluster_end - cluster_start))
        i = following

    return regions


def apply_mask(text: str, regions: list[tuple[int, int]], mask: str) -> str:
    """Replace each ``(index, length)`` region of ``text`` with ``mask``, copying the rest unchanged."""
    parts: list[str] = []
    copied = 0

    for index, length in regions:
        parts.append(text[copied:index])
        parts.append(mask)
        copied = index + length

    parts.append(text[copied:])
    return "".join(parts)


def _is_surrogate_pair_at(text: str, index: int) -> bool:
    return is_high_surrogate(text[index]) and index + 1 < len(text) and is_low_surrogate(text[index + 1])
