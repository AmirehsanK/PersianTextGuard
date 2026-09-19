//! The profanity filter. Port of `js/src/filter.ts`, which ports `ProfanityFilter.cs`.

use std::borrow::Borrow;
use std::collections::HashSet;

use crate::bytes::DecodedBytes;
use crate::errors::InvalidMask;
use crate::fold::fold;
use crate::normalizer::{COMPARISON_STEPS, normalize_with_map, tokenize_with_offsets};
use crate::regions::{Candidate, MASK_LENGTH, Region, merge, to_candidate};
use crate::scan::{Entry, Hit, Key, MINIMUM_BROKEN_WORD_LENGTH, Phrase, ScanState, scan};
use crate::source_map::MapCache;
use crate::types::{BannedWord, EvasionSet, ProfanityFilterOptions, ProfanityMatch, WordMatchMode};
use crate::unicode::{is_control, is_letter_or_digit, is_white_space};
use crate::utf16::{ByteMap, to_view};

/// Collects the lookup tables while a filter is built.
#[derive(Default)]
struct Builder {
    state: ScanState,
    anywhere_keys: HashSet<Box<[u16]>>,
}

impl Builder {
    fn add(&mut self, form: &[u16], mode: WordMatchMode, word: usize, order: usize) -> bool {
        if mode == WordMatchMode::Anywhere {
            if form.is_empty() {
                return false;
            }

            if !self.anywhere_keys.contains(form) {
                self.anywhere_keys.insert(form.into());
                let key = Key {
                    text: form.into(),
                    word,
                    whole_word: false,
                    order,
                };
                if form.len() >= MINIMUM_BROKEN_WORD_LENGTH && !form.contains(&0x20) {
                    self.state.maskable.push(key.clone());
                }
                self.state.anywhere.push(key);
            }

            return true;
        }

        let tokens: Vec<Box<[u16]>> = tokenize_with_offsets(form)
            .into_iter()
            .map(|(start, end)| form[start..end].into())
            .collect();

        match tokens.len() {
            0 => false,
            1 => {
                let token = &tokens[0];
                if !self.state.words.contains_key(token) {
                    self.state.words.insert(token.clone(), Entry { word, order });
                    if token.len() >= MINIMUM_BROKEN_WORD_LENGTH {
                        self.state.maskable.push(Key {
                            text: token.clone(),
                            word,
                            whole_word: true,
                            order,
                        });
                    }
                }

                true
            }
            _ => {
                let first = tokens[0].clone();
                let phrase = Phrase {
                    tokens: tokens.into_boxed_slice(),
                    word,
                    order,
                };
                self.state.phrases.entry(first).or_default().push(phrase);
                true
            }
        }
    }
}

/// Finds banned words in user text, including the spellings people use to get past a word list.
///
/// [`normalize`](crate::normalize) folds the Unicode tricks: Arabic yeh and kaf, zero-width characters,
/// tatweel. What it cannot fold without damaging ordinary text are the tricks a person types on purpose:
/// letters split apart, a key held down, digits and symbols for letters, filler inside a word, accents and
/// look-alike Cyrillic letters. So the filter reads the text in several forms and reports a match if a
/// word appears in any of them. The forms are only used to find matches and are never returned.
///
/// Whole-word entries are looked up by token, so checking a message costs about the same against a list
/// of 400 entries or 4,000. Persian whole-word entries also match with the common suffixes attached
/// («جنده‌ها», «کیرتون»).
///
/// Build one filter when your word list loads and share it: it never changes after construction, and it
/// is `Send + Sync`, so threads can share it by reference (`std::thread::scope`) or in an `Arc`, without
/// a lock. No method panics, for any text.
///
/// ```
/// use persian_text_guard::{ProfanityFilter, WordList};
///
/// let filter = ProfanityFilter::with_defaults(WordList::persian_default());
/// assert!(filter.contains_profanity("ک.ی.ر"));
/// assert!(!filter.contains_profanity("سلام، سفارشم کی میرسه؟"));
/// assert_eq!(filter.censor("kir and motherfucker"), "**** and ****");
/// ```
#[derive(Clone, Debug)]
pub struct ProfanityFilter {
    entries: Box<[BannedWord]>,
    state: ScanState,
}

impl ProfanityFilter {
    /// Prepares `words` for matching with the given options. Do this once, not per message.
    ///
    /// `words` is anything iterable whose items borrow as a [`BannedWord`]:
    /// [`WordList::persian_default()`](crate::WordList::persian_default), a `Vec<BannedWord>`, a slice,
    /// an iterator of references. Each entry is copied, so changing yours afterwards has no effect.
    /// Entries whose text is blank after normalization are ignored. Construction cannot fail.
    ///
    /// ```
    /// use persian_text_guard::{ProfanityFilter, ProfanityFilterOptions, WordList};
    ///
    /// let options = ProfanityFilterOptions::default().join_spaced_letters(false);
    /// let filter = ProfanityFilter::new(WordList::persian_default(), options);
    /// assert!(!filter.contains_profanity("f u c k"));
    /// assert!(filter.contains_profanity("fuuuck"));
    /// ```
    pub fn new<I>(words: I, options: ProfanityFilterOptions) -> Self
    where
        I: IntoIterator,
        I::Item: Borrow<BannedWord>,
    {
        let mut builder = Builder::default();
        let mut entries: Vec<BannedWord> = Vec::new();
        let mut seen: HashSet<(WordMatchMode, Vec<u16>)> = HashSet::new();
        let mut count = 0;

        // An entry's position in the list, duplicates included: it breaks ties between overlapping
        // matches of the same length, in favour of the entry listed first.
        for (order, item) in words.into_iter().enumerate() {
            let word: &BannedWord = item.borrow();
            let normalized = normalize_with_map(&to_view(&word.text), COMPARISON_STEPS, None);
            if normalized.is_empty() {
                continue;
            }

            let seen_key = (word.mode, normalized);
            if seen.contains(&seen_key) {
                continue;
            }

            let (mode, normalized) = seen_key;
            let index = entries.len();
            let mut added = builder.add(&normalized, mode, index, order);

            // The text is folded before matching, so an entry stored the way an evader types it ("k0s",
            // pasted from the message a moderator was reading) would never match the plain spelling the
            // folded text turns into. Keep a folded key beside the original.
            if options.fold_lookalike_characters {
                added = builder.add(&fold(&normalized, None), mode, index, order) || added;
            }

            seen.insert((mode, normalized));
            if added {
                entries.push(word.clone());
                count += 1;
            }
        }

        let state = ScanState {
            squeeze_repeated_letters: options.squeeze_repeated_letters,
            fold_lookalike_characters: options.fold_lookalike_characters,
            join_spaced_letters: options.join_spaced_letters,
            count,
            ..builder.state
        };

        Self {
            entries: entries.into_boxed_slice(),
            state,
        }
    }

    /// [`new`](Self::new) with every evasion read through ([`ProfanityFilterOptions::default`]).
    pub fn with_defaults<I>(words: I) -> Self
    where
        I: IntoIterator,
        I::Item: Borrow<BannedWord>,
    {
        Self::new(words, ProfanityFilterOptions::default())
    }

    /// The number of distinct entries the filter looks for. Spellings that normalize the same, in the same
    /// mode, count once.
    ///
    /// ```
    /// use persian_text_guard::{BannedWord, ProfanityFilter};
    ///
    /// // Arabic and Persian kaf normalize the same.
    /// let filter = ProfanityFilter::with_defaults(["كص", "کص", "  کص "].map(BannedWord::new));
    /// assert_eq!(filter.count(), 1);
    /// ```
    pub fn count(&self) -> usize {
        self.state.count
    }

    /// Whether `text` contains a banned word. Empty and whitespace-only text is clean.
    ///
    /// ```
    /// use persian_text_guard::{ProfanityFilter, WordList};
    ///
    /// let filter = ProfanityFilter::with_defaults(WordList::persian_default());
    /// assert!(filter.contains_profanity("sh1t"));
    /// assert!(!filter.contains_profanity("class"));
    /// ```
    pub fn contains_profanity(&self, text: &str) -> bool {
        scan(&self.state, &to_view(text), None).0
    }

    /// The first banned word found in `text`, or `None` when it is clean.
    ///
    /// "First" means found with the least evasion undone, not earliest in the text. The match's `start`
    /// and `len` are bytes of `text`, widened to whole words, so `&text[m.range()]` is the region.
    ///
    /// ```
    /// use persian_text_guard::{ProfanityFilter, WordList};
    ///
    /// let filter = ProfanityFilter::with_defaults(WordList::persian_default());
    /// let text = "😀 کیر";
    /// let found = filter.find_match(text).unwrap();
    /// assert_eq!((found.start, found.len), (5, 6));
    /// assert_eq!(&text[found.range()], "کیر");
    /// assert!(filter.find_match("سلام").is_none());
    /// ```
    pub fn find_match(&self, text: &str) -> Option<ProfanityMatch<'_>> {
        let view = to_view(text);
        let region = self.first_region(&view)?;
        let bytes = ByteMap::new(text);
        let (start, len) = bytes.to_bytes(region.index, region.length);
        Some(self.to_match(&region, start, len))
    }

    /// Every banned word in `text`, in the order they appear.
    ///
    /// Each match's `start` and `len` are bytes of `text`, and cover whole words: a banned word inside
    /// "motherfucker" or «جنده‌ها» covers the whole word, and a disguised word such as "f u c k" or «ج.نده»
    /// covers its separators too.
    ///
    /// Matches are ordered by position and never overlap. Where entries overlap, one match covers the
    /// union of their regions, reporting the entry whose own text covered the most characters, or on a tie
    /// the entry listed first. Several occurrences inside one word ("fuckfuck") are one match, and repeats
    /// in separate words are separate matches.
    ///
    /// ```
    /// use persian_text_guard::{EvasionKind, ProfanityFilter, WordList};
    ///
    /// let filter = ProfanityFilter::with_defaults(WordList::persian_default());
    /// let matches = filter.find_matches("sh1t and f u c k");
    /// assert_eq!(matches.len(), 2);
    /// assert_eq!((matches[0].word.text.as_str(), matches[0].range()), ("shit", 0..4));
    /// assert_eq!((matches[1].word.text.as_str(), matches[1].range()), ("fuck", 9..16));
    /// assert!(matches[1].evasion.contains(EvasionKind::SplitWord));
    /// ```
    pub fn find_matches(&self, text: &str) -> Vec<ProfanityMatch<'_>> {
        let view = to_view(text);
        let regions = self.find_regions(&view);
        if regions.is_empty() {
            return Vec::new();
        }

        let bytes = ByteMap::new(text);
        regions
            .iter()
            .map(|region| {
                let (start, len) = bytes.to_bytes(region.index, region.length);
                self.to_match(region, start, len)
            })
            .collect()
    }

    /// `text` with every banned word hidden behind four `*`, and everything else exactly as passed.
    ///
    /// Each region [`find_matches`](Self::find_matches) reports is replaced by the same four-character
    /// mask, however long the text it hides, so the mask says nothing about the word. Whole words are
    /// hidden, and a disguised word is hidden with its separators. Every byte outside a hidden region is
    /// copied unchanged, so the result is usually a different length from `text`; positions from
    /// `find_matches` refer to the original.
    ///
    /// The result is always clean: `contains_profanity` returns `false` for it. Clean text is returned
    /// unchanged.
    ///
    /// ```
    /// use persian_text_guard::{ProfanityFilter, WordList};
    ///
    /// let filter = ProfanityFilter::with_defaults(WordList::persian_default());
    /// assert_eq!(filter.censor("kir and motherfucker"), "**** and ****");
    /// assert_eq!(filter.censor("سلام"), "سلام");
    /// ```
    pub fn censor(&self, text: &str) -> String {
        self.censor_checked(text, "*".repeat(MASK_LENGTH).as_str())
    }

    /// [`censor`](Self::censor) with a mask of your choice.
    ///
    /// The mask is any symbol or punctuation character of the Basic Multilingual Plane: `#`, `*`, `■`,
    /// `•`. It is checked before the text is looked at, so an invalid mask is an error even for `""`.
    ///
    /// ```
    /// use persian_text_guard::{InvalidMask, ProfanityFilter, WordList};
    ///
    /// let filter = ProfanityFilter::with_defaults(WordList::persian_default());
    /// assert_eq!(filter.censor_with("this is kir", '#').unwrap(), "this is ####");
    /// assert_eq!(filter.censor_with("this is kir", '■').unwrap(), "this is ■■■■");
    /// assert_eq!(filter.censor_with("", 'x'), Err(InvalidMask { mask: 'x' }));
    /// ```
    ///
    /// # Errors
    ///
    /// [`InvalidMask`] when `mask` is a letter, a digit, whitespace, a control character, or a character
    /// above U+FFFF (which the other ports cannot hold in one UTF-16 unit).
    pub fn censor_with(&self, text: &str, mask: char) -> Result<String, InvalidMask> {
        let mask = mask_text(mask)?;
        Ok(self.censor_checked(text, &mask))
    }

    /// Whether `text`, which may not be UTF-8, contains a banned word.
    ///
    /// Invalid sequences are read as U+FFFD, as [`String::from_utf8_lossy`] reads them, so the answer is
    /// the one [`contains_profanity`](Self::contains_profanity) gives for that string.
    ///
    /// ```
    /// use persian_text_guard::{ProfanityFilter, WordList};
    ///
    /// let filter = ProfanityFilter::with_defaults(WordList::persian_default());
    /// assert!(filter.contains_profanity_bytes(b"kir \xFF"));
    /// assert!(!filter.contains_profanity_bytes(b"\xFF\xFE"));
    /// ```
    pub fn contains_profanity_bytes(&self, text: &[u8]) -> bool {
        self.contains_profanity(&DecodedBytes::new(text).text)
    }

    /// The first banned word in `text`, which may not be UTF-8, with its position in `text`'s bytes.
    ///
    /// The decision, entry and evasion are those of [`find_match`](Self::find_match) on
    /// `String::from_utf8_lossy(text)`. The region is in the caller's bytes: a region covering an invalid
    /// sequence covers the whole sequence, and never ends inside a valid character.
    ///
    /// ```
    /// use persian_text_guard::{ProfanityFilter, WordList};
    ///
    /// let filter = ProfanityFilter::with_defaults(WordList::persian_default());
    /// let found = filter.find_match_bytes(b"\xFF kir").unwrap();
    /// assert_eq!(found.range(), 2..5);
    /// ```
    pub fn find_match_bytes(&self, text: &[u8]) -> Option<ProfanityMatch<'_>> {
        let decoded = DecodedBytes::new(text);
        let found = self.find_match(&decoded.text)?;
        Some(decoded.to_source(found))
    }

    /// Every banned word in `text`, which may not be UTF-8, with positions in `text`'s bytes.
    ///
    /// The matches are those of [`find_matches`](Self::find_matches) on `String::from_utf8_lossy(text)`,
    /// in the same order, with regions moved to the caller's bytes: a region covering an invalid sequence
    /// covers the whole sequence, and `&text[m.range()]` never splits a valid UTF-8 character.
    ///
    /// ```
    /// use persian_text_guard::{ProfanityFilter, WordList};
    ///
    /// let filter = ProfanityFilter::with_defaults(WordList::persian_default());
    /// let matches = filter.find_matches_bytes(b"kir \xFF fuck");
    /// assert_eq!(matches.iter().map(|m| m.range()).collect::<Vec<_>>(), [0..3, 6..10]);
    /// ```
    pub fn find_matches_bytes(&self, text: &[u8]) -> Vec<ProfanityMatch<'_>> {
        let decoded = DecodedBytes::new(text);
        self.find_matches(&decoded.text)
            .into_iter()
            .map(|found| decoded.to_source(found))
            .collect()
    }

    /// `text`, which may not be UTF-8, with every banned word hidden behind four `*`.
    ///
    /// Every byte outside a hidden region is copied unchanged, invalid ones included, so bytes you store
    /// stay the bytes you received. On valid UTF-8 the result is exactly [`censor`](Self::censor)'s.
    ///
    /// ```
    /// use persian_text_guard::{ProfanityFilter, WordList};
    ///
    /// let filter = ProfanityFilter::with_defaults(WordList::persian_default());
    /// assert_eq!(filter.censor_bytes(b"kir \xFF fuck"), b"**** \xFF ****");
    /// ```
    pub fn censor_bytes(&self, text: &[u8]) -> Vec<u8> {
        self.censor_bytes_checked(text, "*".repeat(MASK_LENGTH).as_bytes())
    }

    /// [`censor_bytes`](Self::censor_bytes) with a mask of your choice, spliced as its UTF-8 encoding.
    ///
    /// ```
    /// use persian_text_guard::{ProfanityFilter, WordList};
    ///
    /// let filter = ProfanityFilter::with_defaults(WordList::persian_default());
    /// assert_eq!(filter.censor_bytes_with(b"kir", '#').unwrap(), b"####");
    /// assert!(filter.censor_bytes_with(b"kir", 'x').is_err());
    /// ```
    ///
    /// # Errors
    ///
    /// [`InvalidMask`] under the rules of [`censor_with`](Self::censor_with), checked before the text.
    pub fn censor_bytes_with(&self, text: &[u8], mask: char) -> Result<Vec<u8>, InvalidMask> {
        let mask = mask_text(mask)?;
        Ok(self.censor_bytes_checked(text, mask.as_bytes()))
    }

    fn to_match(&self, region: &Region, start: usize, len: usize) -> ProfanityMatch<'_> {
        ProfanityMatch {
            word: &self.entries[region.word],
            evasion: EvasionSet::from_bits(region.evasion),
            start,
            len,
        }
    }

    /// The first hit's region, as `find_match` reports it: one candidate, not merged.
    fn first_region(&self, view: &[u16]) -> Option<Region> {
        let (found, first) = scan(&self.state, view, None);
        let first = first.filter(|_| found)?;
        let candidate = to_candidate(view, &first, &mut MapCache::default());
        Some(Region {
            word: first.word,
            evasion: first.evasion,
            index: candidate.start,
            length: candidate.end - candidate.start,
        })
    }

    fn find_regions(&self, view: &[u16]) -> Vec<Region> {
        let mut hits: Vec<Hit> = Vec::new();
        if !scan(&self.state, view, Some(&mut hits)).0 {
            return Vec::new();
        }

        let mut cache = MapCache::default();
        let mut candidates: Vec<Candidate> = hits
            .iter()
            .map(|hit| to_candidate(view, hit, &mut cache))
            .collect();
        merge(&mut candidates, &self.entries)
    }

    /// The byte regions of `text`'s matches.
    fn byte_regions(&self, text: &str) -> Vec<(usize, usize)> {
        let view = to_view(text);
        let regions = self.find_regions(&view);
        if regions.is_empty() {
            return Vec::new();
        }

        let bytes = ByteMap::new(text);
        regions
            .iter()
            .map(|region| bytes.to_bytes(region.index, region.length))
            .collect()
    }

    /// Replaces each region with the mask, then keeps going until the output is clean.
    ///
    /// The mask character is usually filler the filter drops to catch "f*ck", so masking can join the
    /// letters around a mask into a new word: "k kos i kos r" masks to "k **** i **** r", which reads as
    /// "kir". Each extra pass replaces letters with a mask that has none, so the letters only ever run
    /// out; the cap is a guard against a bug, and if it is ever reached the whole text becomes one mask
    /// rather than leak a word.
    fn censor_checked(&self, text: &str, mask: &str) -> String {
        let regions = self.byte_regions(text);
        if regions.is_empty() {
            return text.to_owned();
        }

        let mut censored = splice(text.as_bytes(), &regions, mask.as_bytes());
        let mut passes_left = tokenize_with_offsets(&to_view(text)).len() + 1;

        loop {
            let current = String::from_utf8_lossy(&censored).into_owned();
            if !self.contains_profanity(&current) {
                return current;
            }

            if passes_left == 0 {
                return mask.to_owned();
            }

            passes_left -= 1;
            censored = splice(current.as_bytes(), &self.byte_regions(&current), mask.as_bytes());
        }
    }

    /// [`censor_checked`](Self::censor_checked) on bytes that may not be UTF-8: each pass decodes them as
    /// the byte versions do, and splices the caller's bytes.
    fn censor_bytes_checked(&self, text: &[u8], mask: &[u8]) -> Vec<u8> {
        let source_regions = |bytes: &[u8]| {
            let decoded = DecodedBytes::new(bytes);
            self.byte_regions(&decoded.text)
                .into_iter()
                .map(|(start, len)| decoded.region_to_source(start, len))
                .collect::<Vec<_>>()
        };

        let regions = source_regions(text);
        if regions.is_empty() {
            return text.to_vec();
        }

        let decoded = DecodedBytes::new(text);
        let mut passes_left = tokenize_with_offsets(&to_view(&decoded.text)).len() + 1;
        let mut censored = splice(text, &regions, mask);

        while self.contains_profanity_bytes(&censored) {
            if passes_left == 0 {
                return mask.to_vec();
            }

            passes_left -= 1;
            censored = splice(&censored, &source_regions(&censored), mask);
        }

        censored
    }
}

/// The four-character mask for `mask`, or [`InvalidMask`].
fn mask_text(mask: char) -> Result<String, InvalidMask> {
    let invalid = match u16::try_from(u32::from(mask)) {
        Ok(unit) => is_letter_or_digit(unit) || is_white_space(unit) || is_control(unit),
        Err(_) => true,
    };

    if invalid {
        Err(InvalidMask { mask })
    } else {
        Ok(mask.to_string().repeat(MASK_LENGTH))
    }
}

/// Replaces each byte region of `text` with `mask`, copying everything else unchanged.
fn splice(text: &[u8], regions: &[(usize, usize)], mask: &[u8]) -> Vec<u8> {
    let mut result = Vec::with_capacity(text.len());
    let mut copied = 0;

    for &(start, len) in regions {
        let start = start.clamp(copied, text.len());
        result.extend_from_slice(&text[copied..start]);
        result.extend_from_slice(mask);
        copied = (start + len).min(text.len());
    }

    result.extend_from_slice(&text[copied..]);
    result
}
