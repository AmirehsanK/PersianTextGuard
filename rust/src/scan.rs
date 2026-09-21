//! The readings, token and phrase lookup, anywhere entries, joined single letters, words split once, and
//! masked or broken chunks. Port of `js/src/scan.ts`, which ports `ProfanityFilter.Scan.cs`. The
//! character-level helpers (fold, squeeze, filler, …) are in `fold.rs`. Positions are units of the
//! message's view.

use std::collections::HashMap;

use crate::fold::{enclosed_letter, fold, fold_character, squeeze};
use crate::normalizer::{COMPARISON_STEPS, normalize_with_map, tokenize_with_offsets};
use crate::source_map::ReadingKind;
use crate::unicode::{
    Category, category_at, category_of_unit, is_high_surrogate, is_letter, is_letter_or_digit,
    is_low_surrogate, is_white_space,
};

/// The .NET `[Flags] EvasionKind` bits, so evasions compare and combine as in .NET.
pub(crate) const REPEATED_LETTERS: u8 = 1;
/// See [`REPEATED_LETTERS`].
pub(crate) const LOOKALIKE_CHARACTERS: u8 = 2;
/// See [`REPEATED_LETTERS`].
pub(crate) const SPLIT_WORD: u8 = 4;

/// A word split in two or masked with symbols is only matched against entries at least this long:
/// shorter ones ("ass", «کون») turn up by accident in ordinary text broken that way.
pub(crate) const MINIMUM_BROKEN_WORD_LENGTH: usize = 4;

const SPACE: u16 = 0x20;
const VAV: u16 = 0x0648;
const HEH: u16 = 0x0647;
const ALEF: u16 = 0x0627;

/// Longest first, so «هایی» is tried before «ها».
const PERSIAN_SUFFIXES: [&[u16]; 18] = [
    &[0x647, 0x627, 0x634, 0x648, 0x646], // هاشون
    &[0x647, 0x627, 0x62A, 0x648, 0x646], // هاتون
    &[0x647, 0x627, 0x645, 0x648, 0x646], // هامون
    &[0x647, 0x627, 0x6CC, 0x6CC],        // هایی
    &[0x647, 0x627, 0x6CC],               // های
    &[0x647, 0x627, 0x634],               // هاش
    &[0x647, 0x627, 0x62A],               // هات
    &[0x647, 0x627, 0x645],               // هام
    &[0x647, 0x627],                      // ها
    &[0x62A, 0x648, 0x646],               // تون
    &[0x634, 0x648, 0x646],               // شون
    &[0x645, 0x648, 0x646],               // مون
    &[0x627, 0x6CC],                      // ای
    &[0x6CC, 0x6CC],                      // یی
    &[0x627, 0x634],                      // اش
    &[0x627, 0x62A],                      // ات
    &[0x627, 0x645],                      // ام
    &[0x627],                             // ا
];

/// An entry as the filter looks it up: its index in the filter's entries, and its position in the word
/// list, which breaks ties.
#[derive(Clone, Copy, Debug)]
pub(crate) struct Entry {
    /// The index of the entry, as the caller gave it, in the filter's entries.
    pub(crate) word: usize,
    /// The entry's position in the word list, duplicates included.
    pub(crate) order: usize,
}

/// What is searched for (.NET `Key`).
#[derive(Clone, Debug)]
pub(crate) struct Key {
    /// What is searched for.
    pub(crate) text: Box<[u16]>,
    /// The entry, as an index into the filter's entries.
    pub(crate) word: usize,
    /// Whether the key must match a whole token.
    pub(crate) whole_word: bool,
    /// The entry's position in the word list.
    pub(crate) order: usize,
}

/// A phrase entry, one token per word (.NET `Phrase`).
#[derive(Clone, Debug)]
pub(crate) struct Phrase {
    /// The phrase, one token per word.
    pub(crate) tokens: Box<[Box<[u16]>]>,
    /// The entry, as an index into the filter's entries.
    pub(crate) word: usize,
    /// The entry's position in the word list.
    pub(crate) order: usize,
}

/// The filter's lookups, built once.
#[derive(Clone, Debug, Default)]
pub(crate) struct ScanState {
    /// Read through held keys.
    pub(crate) squeeze_repeated_letters: bool,
    /// Read through look-alike characters and filler.
    pub(crate) fold_lookalike_characters: bool,
    /// Read through spaced, dotted and once-split words.
    pub(crate) join_spaced_letters: bool,
    /// The number of distinct entries.
    pub(crate) count: usize,
    /// Whole-word entries by token.
    pub(crate) words: HashMap<Box<[u16]>, Entry>,
    /// Phrase entries by their first token.
    pub(crate) phrases: HashMap<Box<[u16]>, Vec<Phrase>>,
    /// Entries matched anywhere, in list order.
    pub(crate) anywhere: Vec<Key>,
    /// Entries long enough to match with some letters masked.
    pub(crate) maskable: Vec<Key>,
}

/// A banned word found in one reading, before it is mapped back to the message (.NET `Hit`).
#[derive(Clone, Copy, Debug)]
pub(crate) struct Hit {
    /// Which reading it was found in, so the right source map is used.
    pub(crate) reading: ReadingKind,
    /// Its first unit in that reading.
    pub(crate) start: usize,
    /// One past its last unit in that reading.
    pub(crate) end: usize,
    /// The entry that matched, as an index into the filter's entries.
    pub(crate) word: usize,
    /// The entry's position in the word list, for breaking ties.
    pub(crate) order: usize,
    /// What had to be undone to find it, as evasion bits.
    pub(crate) evasion: u8,
}

/// A token of a reading: its text and where it starts and ends (exclusive) in the reading.
#[derive(Clone, Copy, Debug)]
struct Token<'a> {
    text: &'a [u16],
    start: usize,
    end: usize,
}

/// Collects hits: either the first one only, or all of them.
struct Sink<'h> {
    all: Option<&'h mut Vec<Hit>>,
    first: Option<Hit>,
}

impl Sink<'_> {
    /// Records a hit. Returns true when the scan should stop, which is only when it wants the first.
    fn report(&mut self, hit: Hit) -> bool {
        match &mut self.all {
            None => {
                self.first = Some(hit);
                true
            }
            Some(all) => {
                all.push(hit);
                false
            }
        }
    }
}

/// Searches `text` (a view) for banned words. With `all` absent it stops at the first hit and returns
/// it; otherwise it appends every hit to `all`. Returns whether anything was found, and the first hit.
///
/// Every capability goes through here, so they cannot disagree about whether a message is clean. The
/// order is the one `find_match` has always used — most literal reading first, and within a reading
/// tokens and phrases, anywhere entries, joined single letters, a word split once — so the first hit is
/// the least evasion needed.
pub(crate) fn scan(state: &ScanState, text: &[u16], all: Option<&mut Vec<Hit>>) -> (bool, Option<Hit>) {
    let wants_all = all.is_some();
    let mut sink = Sink { all, first: None };
    if text.iter().all(|&unit| is_white_space(unit)) || state.count == 0 {
        return (false, None);
    }

    let normalized = normalize_with_map(text, COMPARISON_STEPS, None);
    let folded = state.fold_lookalike_characters.then(|| fold(&normalized, None));

    let mut readings: Vec<(Vec<u16>, ReadingKind, u8)> = Vec::with_capacity(4);
    readings.push((normalized.clone(), ReadingKind::Normalized, 0));

    if state.squeeze_repeated_letters {
        readings.push((
            squeeze(&normalized, None),
            ReadingKind::Squeezed,
            REPEATED_LETTERS,
        ));
    }

    if let Some(folded) = folded {
        if state.squeeze_repeated_letters {
            let squeezed = squeeze(&folded, None);
            readings.push((folded, ReadingKind::Folded, LOOKALIKE_CHARACTERS));
            readings.push((
                squeezed,
                ReadingKind::FoldedSqueezed,
                LOOKALIKE_CHARACTERS | REPEATED_LETTERS,
            ));
        } else {
            readings.push((folded, ReadingKind::Folded, LOOKALIKE_CHARACTERS));
        }
    }

    for (index, (reading, kind, evasion)) in readings.iter().enumerate() {
        // On ordinary text most readings are the same string.
        if readings[..index].iter().any(|(earlier, _, _)| earlier == reading) {
            continue;
        }

        let tokens: Vec<Token<'_>> = tokenize_with_offsets(reading)
            .into_iter()
            .map(|(start, end)| Token {
                text: &reading[start..end],
                start,
                end,
            })
            .collect();
        if match_tokens(state, &tokens, *kind, *evasion, &mut sink)
            || match_anywhere(state, reading, None, *kind, *evasion, &mut sink)
        {
            return (true, sink.first);
        }

        if state.join_spaced_letters {
            let split = evasion | SPLIT_WORD;

            if let Some(joined) = try_join_single_letters(&tokens) {
                let joined_tokens = joined.tokens();
                if match_tokens(state, &joined_tokens, *kind, split, &mut sink)
                    || match_anywhere(state, &joined.text, Some(&joined.map), *kind, split, &mut sink)
                {
                    return (true, sink.first);
                }
            }

            if match_split_halves(state, &tokens, *kind, split, &mut sink) {
                return (true, sink.first);
            }
        }
    }

    let found_broken = match_broken_chunks(state, &normalized, &mut sink);
    let found = found_broken || (wants_all && sink.all.as_ref().is_some_and(|all| !all.is_empty()));
    (found, sink.first)
}

fn match_tokens(
    state: &ScanState,
    tokens: &[Token<'_>],
    kind: ReadingKind,
    evasion: u8,
    sink: &mut Sink<'_>,
) -> bool {
    for (i, token) in tokens.iter().enumerate() {
        if let Some(entry) = try_find_word(state, token.text) {
            let hit = Hit {
                reading: kind,
                start: token.start,
                end: token.end,
                word: entry.word,
                order: entry.order,
                evasion,
            };
            if sink.report(hit) {
                return true;
            }
        }

        let Some(phrases) = state.phrases.get(token.text) else {
            continue;
        };

        for phrase in phrases {
            if phrase_starts_at(tokens, i, &phrase.tokens) {
                let end = tokens[i + phrase.tokens.len() - 1].end;
                let hit = Hit {
                    reading: kind,
                    start: token.start,
                    end,
                    word: phrase.word,
                    order: phrase.order,
                    evasion,
                };
                if sink.report(hit) {
                    return true;
                }
            }
        }
    }

    false
}

/// The first index at or after `from` where `needle` occurs in `haystack`, like `indexOf`.
fn index_of(haystack: &[u16], needle: &[u16], from: usize) -> Option<usize> {
    let (&first, rest) = needle.split_first()?;
    let last_start = haystack.len().checked_sub(needle.len())?;
    let mut index = from;
    while index <= last_start {
        let offset = haystack[index..=last_start]
            .iter()
            .position(|&unit| unit == first)?;
        index += offset;
        if &haystack[index + 1..index + needle.len()] == rest {
            return Some(index);
        }
        index += 1;
    }

    None
}

/// Anywhere entries inside `haystack`. When the haystack is not the reading itself, `map` gives each of
/// its units' position in the reading.
fn match_anywhere(
    state: &ScanState,
    haystack: &[u16],
    map: Option<&[usize]>,
    kind: ReadingKind,
    evasion: u8,
    sink: &mut Sink<'_>,
) -> bool {
    for key in &state.anywhere {
        let mut next = index_of(haystack, &key.text, 0);

        while let Some(index) = next {
            let last = index + key.text.len() - 1;
            let (start, end) = match map {
                None => (index, last + 1),
                Some(map) => (
                    map.get(index).copied().unwrap_or(0),
                    map.get(last).copied().unwrap_or(0) + 1,
                ),
            };

            let hit = Hit {
                reading: kind,
                start,
                end,
                word: key.word,
                order: key.order,
                evasion,
            };
            if sink.report(hit) {
                return true;
            }

            next = index_of(haystack, &key.text, index + 1);
        }
    }

    false
}

/// The result of joining runs of single letters: the joined tokens separated by spaces, a map from each
/// of its units to its position in the reading, and the tokens as spans of the joined text.
struct Joined {
    text: Vec<u16>,
    map: Vec<usize>,
    /// For each token: its span in `text`, and its start and end in the reading.
    spans: Vec<(usize, usize, usize, usize)>,
}

impl Joined {
    fn tokens(&self) -> Vec<Token<'_>> {
        self.spans
            .iter()
            .map(|&(from, to, start, end)| Token {
                text: &self.text[from..to],
                start,
                end,
            })
            .collect()
    }
}

/// Runs of two or more single-letter tokens become one token: "f u c k" is "fuck". `None` when there is
/// no such run. The joined text is the tokens separated by spaces, and the map gives each of its units'
/// position in the reading.
fn try_join_single_letters(tokens: &[Token<'_>]) -> Option<Joined> {
    if !tokens
        .windows(2)
        .any(|pair| is_single_letter(&pair[0]) && is_single_letter(&pair[1]))
    {
        return None;
    }

    let mut spans = Vec::new();
    let mut text: Vec<u16> = Vec::new();
    let mut positions: Vec<usize> = Vec::new();
    let mut i = 0;

    while i < tokens.len() {
        if !text.is_empty() {
            // The separating space maps to where the previous token ended, keeping the map in order.
            text.push(SPACE);
            positions.push(positions.last().copied().unwrap_or(0));
        }

        if !is_single_letter(&tokens[i]) {
            let token = tokens[i];
            i += 1;
            let from = text.len();
            text.extend_from_slice(token.text);
            positions.extend((0..token.text.len()).map(|k| token.start + k));
            spans.push((from, text.len(), token.start, token.end));
            continue;
        }

        // A run of single letters, including a run of one, which stays as it was.
        let run_start = text.len();
        let first = tokens[i];
        let mut last = first;
        while i < tokens.len() && is_single_letter(&tokens[i]) {
            last = tokens[i];
            i += 1;
            text.extend_from_slice(last.text);
            positions.push(last.start);
        }

        spans.push((run_start, text.len(), first.start, last.end));
    }

    Some(Joined {
        text,
        map: positions,
        spans,
    })
}

fn is_single_letter(token: &Token<'_>) -> bool {
    token.text.len() == 1 && is_letter(token.text[0])
}

/// A word split once: "fu ck" in Latin letters, «کی ر» in Persian. Only when the halves join into exactly
/// an entry, so "push it" never becomes "pushit" and matches "shit".
///
/// Two Latin halves need at least two letters each ("it's hit" is not "shit"). Persian is the other way
/// round: two real words joined make ordinary phrases look like insults («هر کس ده تا» holds «کسده»), but
/// a stray single letter next to a word is a split. «و» ("and") is the one Persian letter that stands
/// alone in ordinary text.
fn match_split_halves(
    state: &ScanState,
    tokens: &[Token<'_>],
    kind: ReadingKind,
    evasion: u8,
    sink: &mut Sink<'_>,
) -> bool {
    for pair in tokens.windows(2) {
        let (first_half, second_half) = (pair[0].text, pair[1].text);
        let length = first_half.len() + second_half.len();

        let latin = first_half.len() >= 2
            && second_half.len() >= 2
            && length >= MINIMUM_BROKEN_WORD_LENGTH
            && is_latin_word(first_half)
            && is_latin_word(second_half);
        let persian = (first_half.len() == 1) != (second_half.len() == 1)
            && length >= 3
            && first_half != [VAV]
            && second_half != [VAV]
            && first_half.first().is_some_and(|&c| is_persian_letter(c))
            && second_half.first().is_some_and(|&c| is_persian_letter(c));

        if !latin && !persian {
            continue;
        }

        let joined = [first_half, second_half].concat();
        let (start, end) = (pair[0].start, pair[1].end);
        if let Some(entry) = state.words.get(joined.as_slice()) {
            if sink.report(Hit {
                reading: kind,
                start,
                end,
                word: entry.word,
                order: entry.order,
                evasion,
            }) {
                return true;
            }
        }

        for key in &state.anywhere {
            if *key.text == *joined
                && sink.report(Hit {
                    reading: kind,
                    start,
                    end,
                    word: key.word,
                    order: key.order,
                    evasion,
                })
            {
                return true;
            }
        }
    }

    false
}

/// Words broken up by punctuation or symbols inside them: «ج.نده», "kos_kesh", "f**k", "c*nt", "f@ck",
/// "a$$hole".
///
/// The whole-word reading splits these into pieces, and dropping the symbols only helps when they were
/// added rather than typed in place of a letter. So each space-separated chunk is tried twice: with the
/// symbols removed, and with each symbol standing for any one letter. A mask only matches entries of
/// [`MINIMUM_BROKEN_WORD_LENGTH`] or more letters with at least half of them showing.
fn match_broken_chunks(state: &ScanState, normalized: &[u16], sink: &mut Sink<'_>) -> bool {
    if !state.join_spaced_letters && !state.fold_lookalike_characters {
        return false;
    }

    let mut chunk_start = 0;

    for i in 0..=normalized.len() {
        if i < normalized.len() && normalized[i] != SPACE {
            continue;
        }

        let offset = chunk_start;
        let chunk = &normalized[chunk_start.min(i)..i];
        chunk_start = i + 1;

        let Some(masked) = masked_pattern(chunk) else {
            continue;
        };
        if masked.masks == 0 || masked.letters == 0 {
            continue;
        }

        let pattern = &masked.pattern;
        let start = offset + masked.trimmed_start;
        let end = offset + masked.trimmed_end;

        if state.join_spaced_letters {
            let stripped: Vec<u16> = pattern.iter().copied().filter(|&unit| unit != 0).collect();
            if stripped.len() >= 3 {
                if let Some(entry) = try_find_word(state, &stripped) {
                    let hit = Hit {
                        reading: ReadingKind::Normalized,
                        start,
                        end,
                        word: entry.word,
                        order: entry.order,
                        evasion: SPLIT_WORD,
                    };
                    if sink.report(hit) {
                        return true;
                    }
                }
            }
        }

        if !state.fold_lookalike_characters {
            continue;
        }

        for key in &state.maskable {
            let fits = if key.whole_word {
                key.text.len() == pattern.len() && fits_mask(pattern, 0, &key.text)
            } else {
                fits_mask_anywhere(pattern, &key.text)
            };

            let hit = Hit {
                reading: ReadingKind::Normalized,
                start,
                end,
                word: key.word,
                order: key.order,
                evasion: LOOKALIKE_CHARACTERS,
            };
            if fits && sink.report(hit) {
                return true;
            }
        }
    }

    false
}

/// Like .NET `char.IsLetterOrDigit(string, int)`: a valid surrogate pair is read as one code point.
fn is_letter_or_digit_at(text: &[u16], index: usize) -> bool {
    matches!(
        category_at(text, index),
        Category::Lu | Category::Ll | Category::Lt | Category::Lm | Category::Lo | Category::Nd
    )
}

struct MaskedPattern {
    pattern: Vec<u16>,
    masks: usize,
    letters: usize,
    trimmed_start: usize,
    trimmed_end: usize,
}

/// The chunk with outer punctuation trimmed, letters and digits folded, combining marks dropped, and
/// every other character replaced by 0. `None` when fewer than three units are left.
fn masked_pattern(chunk: &[u16]) -> Option<MaskedPattern> {
    let mut masks = 0;
    let mut letters = 0;

    let mut start = 0;
    let mut end = chunk.len();
    while start < end && !is_letter_or_digit_at(chunk, start) {
        start += 1;
    }

    while end > start && !is_letter_or_digit_at(chunk, end - 1) {
        end -= 1;
    }

    if end - start < 3 {
        return None;
    }

    let mut pattern = Vec::with_capacity(end - start);
    let mut i = start;

    while i < end {
        let c = chunk[i];

        if is_high_surrogate(c) && i + 1 < end && is_low_surrogate(chunk[i + 1]) {
            let letter = enclosed_letter(crate::fold::code_point_of(c, chunk[i + 1]));
            i += 2;
            if letter != 0 {
                pattern.push(letter);
                letters += 1;
            } else {
                pattern.push(0);
                masks += 1;
            }

            continue;
        }

        i += 1;

        // The char overload here (unlike the trimming above): a lone unit, not a code point.
        if is_letter_or_digit(c) {
            pattern.push(fold_character(c, true));
            if is_letter(c) {
                letters += 1;
            }

            continue;
        }

        match category_of_unit(c) {
            Category::Mn | Category::Mc | Category::Me | Category::Cf => {}
            _ => {
                pattern.push(0);
                masks += 1;
            }
        }
    }

    Some(MaskedPattern {
        pattern,
        masks,
        letters,
        trimmed_start: start,
        trimmed_end: end,
    })
}

fn fits_mask_anywhere(pattern: &[u16], key: &[u16]) -> bool {
    (0..pattern.len().saturating_sub(key.len()) + 1)
        .take_while(|offset| offset + key.len() <= pattern.len())
        .any(|offset| fits_mask(pattern, offset, key))
}

fn fits_mask(pattern: &[u16], offset: usize, key: &[u16]) -> bool {
    let mut masks = 0;

    for (i, &unit) in key.iter().enumerate() {
        match pattern.get(offset + i) {
            Some(0) => masks += 1,
            Some(&c) if c == unit => {}
            _ => return false,
        }
    }

    masks > 0 && masks * 2 <= key.len()
}

/// An entry for the token, as written or with a Persian suffix attached.
fn try_find_word<'s>(state: &'s ScanState, token: &[u16]) -> Option<&'s Entry> {
    if let Some(direct) = state.words.get(token) {
        return Some(direct);
    }

    if token.len() < 4 || !token.first().is_some_and(|&c| is_persian_letter(c)) {
        return None;
    }

    for suffix in PERSIAN_SUFFIXES {
        if !has_suffix(token, suffix) {
            continue;
        }

        let stem = &token[..token.len() - suffix.len()];

        // «جندها»: the final heh of «جنده» is often dropped before «ها».
        let entry = state.words.get(stem).or_else(|| {
            if suffix.starts_with(&[HEH, ALEF]) {
                state.words.get([stem, &[HEH]].concat().as_slice())
            } else {
                None
            }
        });
        if entry.is_some() {
            return entry;
        }
    }

    None
}

fn phrase_starts_at(tokens: &[Token<'_>], start: usize, phrase: &[Box<[u16]>]) -> bool {
    if phrase.is_empty() || start + phrase.len() > tokens.len() {
        return false;
    }

    for k in 1..phrase.len().saturating_sub(1) {
        if *tokens[start + k].text != *phrase[k] {
            return false;
        }
    }

    // The last word of a Persian phrase takes suffixes like a single word does: «بی ناموس‌ها».
    let last = tokens[start + phrase.len() - 1].text;
    let key: &[u16] = &phrase[phrase.len() - 1];
    if last == key {
        return true;
    }

    if !key.first().is_some_and(|&c| is_persian_letter(c))
        || last.len() <= key.len()
        || !last.starts_with(key)
    {
        return false;
    }

    let suffix = &last[key.len()..];
    PERSIAN_SUFFIXES.contains(&suffix) && has_suffix(last, suffix)
}

/// A one-letter suffix needs a four-letter stem: «کیرا» is also the name Kira.
fn has_suffix(token: &[u16], suffix: &[u16]) -> bool {
    token.ends_with(suffix) && token.len() - suffix.len() >= if suffix.len() == 1 { 4 } else { 3 }
}

fn is_persian_letter(c: u16) -> bool {
    (0x0600..=0x06FF).contains(&c) && is_letter(c)
}

fn is_latin_word(token: &[u16]) -> bool {
    token.iter().all(|&c| (0x61..=0x7A).contains(&c))
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn index_of_finds_overlapping_occurrences() {
        let haystack = [1, 2, 1, 2, 1];
        assert_eq!(index_of(&haystack, &[1, 2, 1], 0), Some(0));
        assert_eq!(index_of(&haystack, &[1, 2, 1], 1), Some(2));
        assert_eq!(index_of(&haystack, &[1, 2, 1], 3), None);
        assert_eq!(index_of(&haystack, &[9], 0), None);
        assert_eq!(index_of(&[], &[1], 0), None);
    }

    #[test]
    fn masks_need_half_the_letters_showing() {
        assert!(fits_mask(&[0x66, 0, 0, 0x6B], 0, &[0x66, 0x75, 0x63, 0x6B]));
        assert!(!fits_mask(&[0x66, 0, 0, 0], 0, &[0x66, 0x75, 0x63, 0x6B]));
        assert!(!fits_mask(
            &[0x66, 0x75, 0x63, 0x6B],
            0,
            &[0x66, 0x75, 0x63, 0x6B]
        ));
        assert!(fits_mask_anywhere(
            &[0x61, 0x66, 0, 0x63, 0x6B],
            &[0x66, 0x75, 0x63, 0x6B]
        ));
        assert!(!fits_mask_anywhere(&[0x66, 0], &[0x66, 0x75, 0x63, 0x6B]));
    }
}
