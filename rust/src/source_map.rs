//! Where each character of a reading came from in the message. Port of `js/src/source-map.ts`, which
//! ports `SourceMap.cs`. Positions are units of the message's view.

use crate::fold::{fold, squeeze};
use crate::normalizer::{COMPARISON_STEPS, normalize_with_map};
use crate::unicode::is_white_space;

/// The lossy forms of a message the filter searches, in the order it searches them (.NET `ReadingKind`).
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub(crate) enum ReadingKind {
    /// Comparison-normalized.
    Normalized = 0,
    /// Normalized, with repeated letters squeezed to one.
    Squeezed = 1,
    /// Normalized, with look-alike characters folded.
    Folded = 2,
    /// Normalized, folded, then squeezed.
    FoldedSqueezed = 3,
}

/// A reading of a message, with where each of its characters came from in the message.
///
/// Two maps rather than one, so a fallback can widen in both directions: a match starting at reading
/// index `i` starts no later than `start_map[i]`, and one ending at `j` ends no earlier than
/// `end_map[j]`. When the mapping is exact they are the same, and `end_map` is not stored.
#[derive(Debug)]
pub(crate) struct MappedText {
    /// The reading, identical to the one the filter searched.
    pub(crate) text: Vec<u16>,
    starts: Vec<usize>,
    ends: Option<Vec<usize>>,
}

impl MappedText {
    /// For each reading unit, the earliest message index a match starting there may start at.
    pub(crate) fn start_map(&self) -> &[usize] {
        &self.starts
    }

    /// For each reading unit, the latest message index a match ending there may end at.
    pub(crate) fn end_map(&self) -> &[usize] {
        self.ends.as_deref().unwrap_or(&self.starts)
    }
}

/// One slot per [`ReadingKind`], filled as readings are mapped for one message.
#[derive(Default)]
pub(crate) struct MapCache {
    slots: [Option<MappedText>; 4],
}

/// The `kind` reading of `original`, with its maps, reusing and filling `cache`, so each reading is
/// mapped once per message and the folded and squeezed readings are derived from the mapped normalized
/// one rather than rebuilt.
///
/// The mapped reading is compared with the reading the filter searches. They only differ if segment-wise
/// normalization disagrees with whole-string normalization, in which case the maps fall back to whole
/// whitespace-separated chunks and then to the whole message: a coarser map can hide more than the
/// word, but never less.
pub(crate) fn build<'c>(original: &[u16], kind: ReadingKind, cache: &'c mut MapCache) -> &'c MappedText {
    let slot = kind as usize;
    let mapped = match cache.slots[slot].take() {
        Some(mapped) => mapped,
        None => match kind {
            ReadingKind::Normalized => normalized(original),
            ReadingKind::Squeezed => derive(build(original, ReadingKind::Normalized, cache), squeeze),
            ReadingKind::Folded => derive(build(original, ReadingKind::Normalized, cache), fold),
            ReadingKind::FoldedSqueezed => derive(build(original, ReadingKind::Folded, cache), squeeze),
        },
    };

    cache.slots[slot].insert(mapped)
}

/// The mapped normalized reading, checked against the reading the filter searched. Folding and
/// squeezing are plain functions of their input, so once this one matches, every reading derived from
/// it matches too.
fn normalized(original: &[u16]) -> MappedText {
    let mut map = Vec::with_capacity(original.len());
    let text = normalize_with_map(original, COMPARISON_STEPS, Some(&mut map));
    let expected = normalize_with_map(original, COMPARISON_STEPS, None);

    if text != expected {
        return chunk_map(original, expected);
    }

    MappedText {
        text,
        starts: map,
        ends: None,
    }
}

type Step = fn(&[u16], Option<&mut Vec<usize>>) -> Vec<u16>;

fn derive(source: &MappedText, step: Step) -> MappedText {
    let mut step_map = Vec::with_capacity(source.text.len());
    let text = step(&source.text, Some(&mut step_map));

    let pick = |map: &[usize]| {
        step_map
            .iter()
            .map(|&i| map.get(i).copied().unwrap_or(0))
            .collect::<Vec<_>>()
    };
    let starts = pick(&source.starts);
    let ends = source.ends.as_deref().map(pick);

    MappedText { text, starts, ends }
}

/// Maps each whitespace-separated chunk of `reading` to the matching chunk of `original` as a whole; the
/// whole message when the chunk counts differ.
pub(crate) fn chunk_map(original: &[u16], reading: Vec<u16>) -> MappedText {
    let original_chunks = chunks(original);
    let reading_chunks = chunks(&reading);
    if original_chunks.len() != reading_chunks.len() {
        return whole_message_map(original, reading);
    }

    let mut starts = Vec::with_capacity(reading.len());
    let mut ends = Vec::with_capacity(reading.len());
    let mut chunk = 0;
    let mut start = 0;
    let mut end = 0;

    for i in 0..reading.len() {
        if chunk < reading_chunks.len() && i >= reading_chunks[chunk].0 {
            start = original_chunks[chunk].0;
            end = original_chunks[chunk].1;
            if i == reading_chunks[chunk].1 {
                chunk += 1;
            }
        }

        // Separators keep the previous chunk's values, which keeps both maps non-decreasing.
        starts.push(start);
        ends.push(end);
    }

    MappedText {
        text: reading,
        starts,
        ends: Some(ends),
    }
}

/// Maps every reading character to the whole message.
pub(crate) fn whole_message_map(original: &[u16], reading: Vec<u16>) -> MappedText {
    let last = original.len().saturating_sub(1);
    let starts = vec![0; reading.len()];
    let ends = vec![last; reading.len()];
    MappedText {
        text: reading,
        starts,
        ends: Some(ends),
    }
}

/// The `(first, last)` units of each whitespace-separated chunk.
fn chunks(text: &[u16]) -> Vec<(usize, usize)> {
    let mut result = Vec::new();
    let mut start: Option<usize> = None;

    for i in 0..=text.len() {
        let separator = i == text.len() || is_white_space(text[i]);
        match start {
            None if !separator => start = Some(i),
            Some(first) if separator => {
                result.push((first, i - 1));
                start = None;
            }
            _ => {}
        }
    }

    result
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::utf16::to_view;

    #[test]
    fn fallback_maps_only_ever_widen() {
        let text = to_view("ab kir cd");
        let chunk = chunk_map(&text, text.clone());
        assert!(chunk.start_map()[3] <= 3);
        assert!(chunk.end_map()[5] >= 5);

        let whole = whole_message_map(&text, text.clone());
        assert_eq!(whole.start_map()[3], 0);
        assert_eq!(whole.end_map()[5], text.len() - 1);
    }

    #[test]
    fn a_chunk_count_mismatch_falls_back_to_the_whole_message() {
        let mapped = chunk_map(&to_view("ab kir"), to_view("abkir x y"));
        assert!(mapped.start_map().iter().all(|&start| start == 0));
        assert!(mapped.end_map().iter().all(|&end| end == 5));
    }
}
