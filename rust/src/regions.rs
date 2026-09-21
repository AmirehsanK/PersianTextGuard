//! Mapping hits back to the message, widening to whole words, and merging overlaps. Port of
//! `js/src/regions.ts`, which ports `ProfanityFilter.Regions.cs`. Positions are units of the message's
//! view; `filter.rs` converts regions to bytes and applies the mask to the caller's string.

use crate::normalizer::is_word_character;
use crate::scan::Hit;
use crate::source_map::{MapCache, build};
use crate::types::BannedWord;
use crate::unicode::{is_high_surrogate, is_low_surrogate};

/// Every hidden word gets the same mask, so its length says nothing about the word.
pub(crate) const MASK_LENGTH: usize = 4;

/// A hit mapped back to the message and widened to whole words (.NET `Candidate`).
#[derive(Clone, Copy, Debug)]
pub(crate) struct Candidate {
    /// First unit of the region in the message.
    pub(crate) start: usize,
    /// One past the region's last unit.
    pub(crate) end: usize,
    /// Units the hit itself covered, before widening to whole words.
    pub(crate) hit_length: usize,
    /// The entry that matched, as an index into the filter's entries.
    pub(crate) word: usize,
    /// The entry's position in the word list.
    pub(crate) order: usize,
    /// What had to be undone to find it, as evasion bits.
    pub(crate) evasion: u8,
}

/// A merged region, in units of the message's view.
#[derive(Clone, Copy, Debug)]
pub(crate) struct Region {
    /// The entry reported for the region, as an index into the filter's entries.
    pub(crate) word: usize,
    /// The least evasion that entry needed, as evasion bits.
    pub(crate) evasion: u8,
    /// First unit of the region in the message.
    pub(crate) index: usize,
    /// Units the region spans.
    pub(crate) length: usize,
}

/// Where `hit` is in `original`, widened to the whole words it touches. The reading's source map is built
/// on first use and kept in `cache`, so a message with many hits maps each reading once.
pub(crate) fn to_candidate(original: &[u16], hit: &Hit, cache: &mut MapCache) -> Candidate {
    let mapped = build(original, hit.reading, cache);
    let last_unit = original.len().saturating_sub(1);

    let mut start = mapped.start_map().get(hit.start).copied().unwrap_or(0);
    let last = mapped
        .end_map()
        .get(hit.end.saturating_sub(1))
        .copied()
        .unwrap_or(last_unit);
    let mut end = (last
        + if is_surrogate_pair_at(original, last) {
            2
        } else {
            1
        })
    .min(original.len());
    start = start.min(end.saturating_sub(1));
    let hit_length = end - start;

    // Whole words: the tokenizer's own rule decides where a word ends, so a suffix joined by a zero-width
    // non-joiner comes along and an emoji or comma does not. The rule answers the same for both halves of
    // a surrogate pair, so the region cannot split one.
    while start > 0 && is_word_character(original, start - 1) {
        start -= 1;
    }

    while end < original.len() && is_word_character(original, end) {
        end += 1;
    }

    Candidate {
        start,
        end,
        hit_length,
        word: hit.word,
        order: hit.order,
        evasion: hit.evasion,
    }
}

/// Candidates that overlap become one region: the region is their union, the entry is the one whose hit
/// covered the most characters before widening — so "motherfucker" beats the "fuck" inside it even though
/// both widen to the same word — with ties going to the entry listed first, and the evasion is the least
/// that entry needed.
pub(crate) fn merge(candidates: &mut [Candidate], entries: &[BannedWord]) -> Vec<Region> {
    // Stable, as the TypeScript's sort: start ascending, end descending.
    candidates.sort_by(|a, b| a.start.cmp(&b.start).then(b.end.cmp(&a.end)));

    let same_word = |a: usize, b: usize| a == b || entries.get(a) == entries.get(b);

    let mut regions = Vec::new();
    let mut i = 0;

    while i < candidates.len() {
        let cluster_start = candidates[i].start;
        let mut cluster_end = candidates[i].end;
        let mut best = candidates[i];

        let mut next = i + 1;
        while next < candidates.len() && candidates[next].start < cluster_end {
            let candidate = candidates[next];
            cluster_end = cluster_end.max(candidate.end);
            if candidate.hit_length > best.hit_length
                || (candidate.hit_length == best.hit_length && candidate.order < best.order)
            {
                best = candidate;
            }

            next += 1;
        }

        let mut evasion = best.evasion;
        for candidate in &candidates[i..next] {
            if same_word(candidate.word, best.word) && candidate.evasion < evasion {
                evasion = candidate.evasion;
            }
        }

        regions.push(Region {
            word: best.word,
            evasion,
            index: cluster_start,
            length: cluster_end - cluster_start,
        });
        i = next;
    }

    regions
}

fn is_surrogate_pair_at(text: &[u16], index: usize) -> bool {
    text.get(index).is_some_and(|&unit| is_high_surrogate(unit))
        && text.get(index + 1).is_some_and(|&unit| is_low_surrogate(unit))
}
