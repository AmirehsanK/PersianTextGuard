//! The public value types: enumerations, evasion and normalization sets, entries, options and matches.
//! Port of `js/src/types.ts` (spec 005, contracts/public-api.md).

use std::fmt;
use std::ops::{BitOr, Range};
use std::str::FromStr;

use crate::errors::ParseNameError;

/// Declares a public enumeration whose variants have corpus names, with `ALL`, `name()`, `Display` and
/// `FromStr`.
macro_rules! named_enum {
    (
        $(#[$attribute:meta])*
        pub enum $name:ident ($kind:literal) {
            $( $(#[$variant_attribute:meta])* $variant:ident => $text:literal, )+
        }
    ) => {
        $(#[$attribute])*
        #[non_exhaustive]
        pub enum $name {
            $( $(#[$variant_attribute])* $variant, )+
        }

        impl $name {
            /// Every variant, in declaration order.
            pub const ALL: &'static [Self] = &[$( Self::$variant, )+];

            const NAMES: &'static [&'static str] = &[$( $text, )+];

            /// The variant's name, as the conformance corpus and the other ports spell it.
            pub fn name(self) -> &'static str {
                match self {
                    $( Self::$variant => $text, )+
                }
            }
        }

        impl fmt::Display for $name {
            fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
                formatter.write_str(self.name())
            }
        }

        impl FromStr for $name {
            type Err = ParseNameError;

            /// Parses the variant's name, case-sensitively.
            fn from_str(text: &str) -> Result<Self, Self::Err> {
                Self::ALL
                    .iter()
                    .copied()
                    .find(|variant| variant.name() == text)
                    .ok_or_else(|| ParseNameError::new($kind, text, Self::NAMES))
            }
        }
    };
}

named_enum! {
    /// How an entry is looked for in text.
    ///
    /// `WholeWord` is the safe default: «کس» as a whole word does not flag «کسی», and "ass" does not flag
    /// "class". `Anywhere` also matches inside longer words; use it for stems whose every extension is
    /// also offensive, such as "fuck" covering "motherfucker".
    ///
    /// The names are those of the other ports and the conformance corpus:
    ///
    /// ```
    /// use persian_text_guard::WordMatchMode;
    ///
    /// assert_eq!("anywhere".parse::<WordMatchMode>(), Ok(WordMatchMode::Anywhere));
    /// assert_eq!(WordMatchMode::WholeWord.to_string(), "wholeWord");
    /// assert_eq!(WordMatchMode::default(), WordMatchMode::WholeWord);
    /// ```
    #[derive(Clone, Copy, Debug, PartialEq, Eq, Hash, Default)]
    pub enum WordMatchMode ("word match mode") {
        /// Only as whole words, or a whole phrase. The default.
        #[default]
        WholeWord => "wholeWord",
        /// Anywhere, including inside longer words.
        Anywhere => "anywhere",
    }
}

named_enum! {
    /// What kind of word an entry is, so a site can choose what to block.
    ///
    /// Entries in your own lists default to `Uncategorized`; bundled entries always have one of the
    /// others. `Mild` words are rude in context but ordinary otherwise («آشغال», «دلقک», damn), and are
    /// left out of [`WordList::persian_default`](crate::WordList::persian_default).
    ///
    /// ```
    /// use persian_text_guard::WordCategory;
    ///
    /// assert_eq!("slur".parse::<WordCategory>(), Ok(WordCategory::Slur));
    /// assert!("Slur".parse::<WordCategory>().is_err()); // names are case-sensitive
    /// assert_eq!(WordCategory::ALL.len(), 7);
    /// ```
    ///
    /// The enum is `#[non_exhaustive]`: a later release may add a category, so a `match` needs a
    /// wildcard arm.
    #[derive(Clone, Copy, Debug, PartialEq, Eq, Hash, PartialOrd, Ord, Default)]
    pub enum WordCategory ("word category") {
        /// No category; the default for entries in your own lists.
        #[default]
        Uncategorized => "uncategorized",
        /// General swearing and crude words: fuck, shit, «ریدم», «گوه».
        Profanity => "profanity",
        /// Genitals, sex acts and pornography: «کیر», «سکس», cock, blowjob.
        Sexual => "sexual",
        /// Strong insults, including the family and honour insults Persian is built on: «کسکش», bastard.
        Insult => "insult",
        /// Hate speech against a group (race, ethnicity, religion, sexual orientation, gender, disability).
        Slur => "slur",
        /// Telling someone to hurt themselves, or to shut up: kys, «خفه شو».
        Harassment => "harassment",
        /// Rude in context but ordinary words otherwise: «آشغال», «گوز», «دلقک», damn, crap.
        Mild => "mild",
    }
}

named_enum! {
    /// An evasion that had to be undone before a banned word showed up.
    ///
    /// ```
    /// use persian_text_guard::EvasionKind;
    ///
    /// assert_eq!("splitWord".parse::<EvasionKind>(), Ok(EvasionKind::SplitWord));
    /// assert_eq!(EvasionKind::RepeatedLetters.name(), "repeatedLetters");
    /// ```
    #[derive(Clone, Copy, Debug, PartialEq, Eq, Hash, PartialOrd, Ord)]
    pub enum EvasionKind ("evasion kind") {
        /// A letter was held down: "fuuuck", «کیییر».
        RepeatedLetters => "repeatedLetters",
        /// Digits, symbols or look-alike letters stood in for letters, filler was typed inside the word,
        /// or symbols masked some of its letters: "sh1t", "$hit", "f*ck", "fück".
        LookalikeCharacters => "lookalikeCharacters",
        /// The word was broken up with spaces or punctuation: "f u c k", "fu ck", «ج.نده».
        SplitWord => "splitWord",
    }
}

named_enum! {
    /// One step [`normalize`](crate::normalize) can apply. Steps always run in declaration order.
    ///
    /// ```
    /// use persian_text_guard::{Normalization, NormalizationStep, normalize};
    ///
    /// let steps = NormalizationStep::LowerCase | NormalizationStep::AsciiDigits;
    /// assert_eq!(normalize("ABC ۱۲", steps), "abc 12");
    /// assert!(Normalization::STANDARD.contains(NormalizationStep::UnifyLetters));
    /// ```
    #[derive(Clone, Copy, Debug, PartialEq, Eq, Hash, PartialOrd, Ord)]
    pub enum NormalizationStep ("normalization step") {
        /// Unicode compatibility normalization (NFKC): Arabic presentation forms such as ﻙ become base
        /// letters, full-width Latin becomes ASCII.
        CompatibilityForms => "compatibilityForms",
        /// Arabic yeh and alef maksura to Persian yeh, Arabic kaf to keheh, hamza alef forms to bare
        /// alef, teh marbuta to heh, and look-alike letters from the Urdu, Kurdish and Pashto blocks to
        /// the Persian ones.
        UnifyLetters => "unifyLetters",
        /// Arabic diacritics (harakat).
        RemoveDiacritics => "removeDiacritics",
        /// Tatweel (ـ), the stretching character.
        RemoveTatweel => "removeTatweel",
        /// Zero-width non-joiner, joiner, space, word joiner, byte-order mark and soft hyphen. The
        /// zero-width non-joiner is correct Persian spelling, so this belongs in comparison, not in text
        /// you display.
        RemoveZeroWidth => "removeZeroWidth",
        /// LRM, RLM, embeddings, overrides and isolates.
        RemoveBidiControls => "removeBidiControls",
        /// Persian and Arabic-Indic digits to ASCII 0-9.
        AsciiDigits => "asciiDigits",
        /// Invariant lower-casing, one UTF-16 unit at a time, as .NET does it.
        LowerCase => "lowerCase",
        /// Every run of whitespace to a single space, and trimmed.
        CollapseWhitespace => "collapseWhitespace",
        /// Runs of the same character cut to two, so «سسسسلام» and "heeeey" cannot slip past an entry,
        /// while real doubled letters survive.
        CollapseRepeats => "collapseRepeats",
    }
}

impl EvasionKind {
    const fn bit(self) -> u8 {
        match self {
            Self::RepeatedLetters => 1,
            Self::LookalikeCharacters => 2,
            Self::SplitWord => 4,
        }
    }
}

impl NormalizationStep {
    /// The bit of the .NET `[Flags]` enum `PersianNormalization`.
    pub(crate) const fn bit(self) -> u16 {
        match self {
            Self::CompatibilityForms => 1 << 0,
            Self::UnifyLetters => 1 << 1,
            Self::RemoveDiacritics => 1 << 2,
            Self::RemoveTatweel => 1 << 3,
            Self::RemoveZeroWidth => 1 << 4,
            Self::RemoveBidiControls => 1 << 5,
            Self::AsciiDigits => 1 << 6,
            Self::LowerCase => 1 << 7,
            Self::CollapseWhitespace => 1 << 8,
            Self::CollapseRepeats => 1 << 9,
        }
    }
}

/// The evasions a match needed, iterated in the order `RepeatedLetters`, `LookalikeCharacters`,
/// `SplitWord`. Empty when the word was there as written, after normalization.
///
/// ```
/// use persian_text_guard::{EvasionKind, EvasionSet, ProfanityFilter, WordList};
///
/// let filter = ProfanityFilter::with_defaults(WordList::persian_default());
/// let found = filter.find_match("fuuuck").unwrap();
/// assert!(found.evasion.contains(EvasionKind::RepeatedLetters));
/// assert_eq!(found.evasion.iter().collect::<Vec<_>>(), [EvasionKind::RepeatedLetters]);
/// assert!(EvasionSet::EMPTY.is_empty());
/// ```
#[derive(Clone, Copy, PartialEq, Eq, Hash, Default)]
pub struct EvasionSet {
    bits: u8,
}

/// The iterator over an [`EvasionSet`]: the kinds present, in declaration order.
type EvasionIter = std::iter::Flatten<std::array::IntoIter<Option<EvasionKind>, 3>>;

impl EvasionSet {
    /// No evasion.
    pub const EMPTY: Self = Self { bits: 0 };

    /// The set with the given .NET `EvasionKind` bits.
    pub(crate) const fn from_bits(bits: u8) -> Self {
        Self { bits: bits & 7 }
    }

    /// Whether `kind` is in the set.
    pub fn contains(self, kind: EvasionKind) -> bool {
        self.bits & kind.bit() != 0
    }

    /// Whether the set is empty: the word was found as written.
    pub fn is_empty(self) -> bool {
        self.bits == 0
    }

    /// How many evasions the set holds.
    pub fn len(self) -> usize {
        self.bits.count_ones() as usize
    }

    /// The evasions in the set, in declaration order.
    pub fn iter(self) -> impl Iterator<Item = EvasionKind> {
        self.into_iter()
    }
}

impl IntoIterator for EvasionSet {
    type Item = EvasionKind;
    type IntoIter = EvasionIter;

    fn into_iter(self) -> Self::IntoIter {
        [
            EvasionKind::RepeatedLetters,
            EvasionKind::LookalikeCharacters,
            EvasionKind::SplitWord,
        ]
        .map(|kind| self.contains(kind).then_some(kind))
        .into_iter()
        .flatten()
    }
}

impl fmt::Debug for EvasionSet {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        formatter.debug_set().entries(self.iter()).finish()
    }
}

/// A set of [`NormalizationStep`]s for [`normalize`](crate::normalize). Steps always run in declaration
/// order, whatever order the set is built in.
///
/// - [`STANDARD`](Self::STANDARD): safe for text you store and show. Fixes letters typed on an Arabic
///   keyboard layout and strips invisible junk, but keeps the zero-width non-joiner, digits, case and
///   repeats.
/// - [`COMPARISON`](Self::COMPARISON): every step. Lossy on purpose: the form to compare, search and
///   filter on, never to display. It is what the filter searches.
/// - [`NONE`](Self::NONE): leave the text exactly as it is.
///
/// ```
/// use persian_text_guard::{Normalization, NormalizationStep};
///
/// let steps: Normalization = [NormalizationStep::LowerCase, NormalizationStep::UnifyLetters]
///     .into_iter()
///     .collect();
/// assert_eq!(
///     steps.steps().collect::<Vec<_>>(),
///     [NormalizationStep::UnifyLetters, NormalizationStep::LowerCase]
/// );
/// assert_eq!("comparison".parse::<Normalization>(), Ok(Normalization::COMPARISON));
/// ```
#[derive(Clone, Copy, PartialEq, Eq, Hash, Default)]
pub struct Normalization {
    bits: u16,
}

impl Normalization {
    /// No step: the text is returned exactly as it is.
    pub const NONE: Self = Self { bits: 0 };

    /// `CompatibilityForms`, `UnifyLetters`, `RemoveTatweel`, `RemoveBidiControls` and
    /// `CollapseWhitespace`: safe for text you store and show.
    pub const STANDARD: Self = Self { bits: 299 };

    /// Every step: the form the filter compares.
    pub const COMPARISON: Self = Self { bits: 1023 };

    /// The .NET `PersianNormalization` flag bits.
    pub(crate) const fn bits(self) -> u16 {
        self.bits
    }

    /// Whether `step` is in the set.
    pub fn contains(self, step: NormalizationStep) -> bool {
        self.bits & step.bit() != 0
    }

    /// The steps in the set, in the order they run.
    pub fn steps(self) -> impl Iterator<Item = NormalizationStep> {
        NormalizationStep::ALL
            .iter()
            .copied()
            .filter(move |&step| self.contains(step))
    }
}

impl fmt::Debug for Normalization {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        formatter.debug_set().entries(self.steps()).finish()
    }
}

impl From<NormalizationStep> for Normalization {
    fn from(step: NormalizationStep) -> Self {
        Self { bits: step.bit() }
    }
}

impl FromIterator<NormalizationStep> for Normalization {
    fn from_iter<I: IntoIterator<Item = NormalizationStep>>(steps: I) -> Self {
        steps.into_iter().fold(Self::NONE, |set, step| set | step)
    }
}

impl BitOr for Normalization {
    type Output = Self;

    fn bitor(self, other: Self) -> Self {
        Self {
            bits: self.bits | other.bits,
        }
    }
}

impl BitOr<NormalizationStep> for Normalization {
    type Output = Self;

    fn bitor(self, step: NormalizationStep) -> Self {
        Self {
            bits: self.bits | step.bit(),
        }
    }
}

impl BitOr for NormalizationStep {
    type Output = Normalization;

    fn bitor(self, other: Self) -> Normalization {
        Normalization::from(self) | other
    }
}

impl FromStr for Normalization {
    type Err = ParseNameError;

    /// Parses a preset name: `"comparison"`, `"standard"` or `"none"`, case-sensitively.
    fn from_str(text: &str) -> Result<Self, Self::Err> {
        match text {
            "comparison" => Ok(Self::COMPARISON),
            "standard" => Ok(Self::STANDARD),
            "none" => Ok(Self::NONE),
            _ => Err(ParseNameError::new(
                "normalization preset",
                text,
                &["comparison", "standard", "none"],
            )),
        }
    }
}

/// A word or phrase for a [`ProfanityFilter`](crate::ProfanityFilter) to look for.
///
/// The fields are public to read. The struct is `#[non_exhaustive]`, so it is built with
/// [`new`](Self::new) and the `with_` methods; a later field is then not a breaking change. A filter
/// copies the entries it is given, so changing yours afterwards has no effect on it. Blank text is
/// ignored by the filter, not rejected.
///
/// ```
/// use persian_text_guard::{BannedWord, WordCategory, WordMatchMode};
///
/// let word = BannedWord::new("casino")
///     .with_mode(WordMatchMode::Anywhere)
///     .with_category(WordCategory::Profanity);
/// assert_eq!(word.text, "casino");
/// assert_eq!(BannedWord::new("اسپم").mode, WordMatchMode::WholeWord);
/// ```
#[derive(Clone, Debug, PartialEq, Eq, Hash)]
#[non_exhaustive]
pub struct BannedWord {
    /// The word or phrase, in any spelling; it is normalized when the filter is built.
    pub text: String,
    /// Whole words only (the default), or anywhere in the text.
    pub mode: WordMatchMode,
    /// What kind of word this is. Bundled entries always have one; your own default to `Uncategorized`.
    pub category: WordCategory,
}

impl BannedWord {
    /// An entry matched as whole words, with no category.
    pub fn new(text: impl Into<String>) -> Self {
        Self {
            text: text.into(),
            mode: WordMatchMode::WholeWord,
            category: WordCategory::Uncategorized,
        }
    }

    /// The entry with `mode`.
    #[must_use]
    pub fn with_mode(self, mode: WordMatchMode) -> Self {
        Self { mode, ..self }
    }

    /// The entry with `category`.
    #[must_use]
    pub fn with_category(self, category: WordCategory) -> Self {
        Self { category, ..self }
    }
}

/// Which evasions a [`ProfanityFilter`](crate::ProfanityFilter) reads through. Every option defaults to
/// `true`.
///
/// `#[non_exhaustive]`: build it with [`Default`] and the setters, so a later option is not a breaking
/// change.
///
/// ```
/// use persian_text_guard::{ProfanityFilter, ProfanityFilterOptions, WordList};
///
/// let options = ProfanityFilterOptions::default().fold_lookalike_characters(false);
/// assert!(!options.fold_lookalike_characters);
/// assert!(options.squeeze_repeated_letters);
///
/// let filter = ProfanityFilter::new(WordList::persian_default(), options);
/// assert!(!filter.contains_profanity("sh1t"));
/// ```
#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
#[non_exhaustive]
pub struct ProfanityFilterOptions {
    /// Undo held keys: "fuuuck" is also read as "fuck". Only ever used to find a match, so squeezing
    /// "pass" to "pas" does no harm.
    pub squeeze_repeated_letters: bool,
    /// Read digits, symbols, accented letters and Cyrillic or Greek look-alikes as the Latin letters they
    /// imitate ("sh1t", "$hit", "k0s", "fück"), drop filler and emoji typed inside a word ("f*ck"), and
    /// read symbols masking letters as those letters ("f**k", "c*nt").
    pub fold_lookalike_characters: bool,
    /// Put split words back together: runs of single letters ("f u c k", «ک ی ر»), punctuation inside a
    /// word («ج.نده», "kos_kesh"), and a word split once ("fu ck", «کی ر») when the halves join into
    /// exactly an entry. Ordinary words are never glued into something else: "push it" does not match
    /// "shit".
    pub join_spaced_letters: bool,
}

impl Default for ProfanityFilterOptions {
    fn default() -> Self {
        Self {
            squeeze_repeated_letters: true,
            fold_lookalike_characters: true,
            join_spaced_letters: true,
        }
    }
}

impl ProfanityFilterOptions {
    /// The options with [`squeeze_repeated_letters`](Self::squeeze_repeated_letters) set to `on`.
    #[must_use]
    pub fn squeeze_repeated_letters(self, on: bool) -> Self {
        Self {
            squeeze_repeated_letters: on,
            ..self
        }
    }

    /// The options with [`fold_lookalike_characters`](Self::fold_lookalike_characters) set to `on`.
    #[must_use]
    pub fn fold_lookalike_characters(self, on: bool) -> Self {
        Self {
            fold_lookalike_characters: on,
            ..self
        }
    }

    /// The options with [`join_spaced_letters`](Self::join_spaced_letters) set to `on`.
    #[must_use]
    pub fn join_spaced_letters(self, on: bool) -> Self {
        Self {
            join_spaced_letters: on,
            ..self
        }
    }
}

/// A banned word found by [`ProfanityFilter::find_match`](crate::ProfanityFilter::find_match) or
/// [`find_matches`](crate::ProfanityFilter::find_matches).
///
/// Positions are **bytes** of the message as passed, so `&text[m.range()]` is the matched region. The
/// region always covers whole words: a match inside "motherfucker" or «جنده‌ها» starts at the start of
/// that word, and a disguised word ("f u c k", «ج.نده») includes its separators.
///
/// ```
/// use persian_text_guard::{EvasionKind, ProfanityFilter, WordList};
///
/// let filter = ProfanityFilter::with_defaults(WordList::persian_default());
/// let text = "hi f u c k";
/// let found = filter.find_match(text).unwrap();
/// assert_eq!(found.word.text, "fuck");
/// assert_eq!(&text[found.range()], "f u c k");
/// assert!(found.evasion.contains(EvasionKind::SplitWord));
/// ```
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
#[non_exhaustive]
pub struct ProfanityMatch<'f> {
    /// The entry that matched, as it was given to the filter.
    pub word: &'f BannedWord,
    /// What had to be undone to find it. Empty when the word was there as written, after
    /// normalization. Useful for moderation logs.
    pub evasion: EvasionSet,
    /// The first byte of the matched words in the message. Always a character boundary.
    pub start: usize,
    /// How many bytes the matched words span, from `start`. Always at least 1.
    pub len: usize,
}

impl ProfanityMatch<'_> {
    /// `start..start + len`: the matched region's bytes in the message.
    pub fn range(&self) -> Range<usize> {
        self.start..self.start + self.len
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn presets_have_the_dot_net_bits() {
        let standard = NormalizationStep::CompatibilityForms
            | NormalizationStep::UnifyLetters
            | NormalizationStep::RemoveTatweel
            | NormalizationStep::RemoveBidiControls
            | NormalizationStep::CollapseWhitespace;
        assert_eq!(standard, Normalization::STANDARD);
        assert_eq!(Normalization::STANDARD.bits(), 299);
        assert_eq!(
            NormalizationStep::ALL.iter().copied().collect::<Normalization>(),
            Normalization::COMPARISON
        );
    }

    #[test]
    fn evasion_sets_iterate_in_declaration_order() {
        let set = EvasionSet::from_bits(4 | 1);
        assert_eq!(
            set.iter().collect::<Vec<_>>(),
            [EvasionKind::RepeatedLetters, EvasionKind::SplitWord]
        );
        assert_eq!(set.len(), 2);
        assert_eq!(format!("{set:?}"), "{RepeatedLetters, SplitWord}");
    }

    #[test]
    fn names_round_trip() {
        for &category in WordCategory::ALL {
            assert_eq!(category.name().parse::<WordCategory>(), Ok(category));
        }
        for &step in NormalizationStep::ALL {
            assert_eq!(step.to_string().parse::<NormalizationStep>(), Ok(step));
        }
    }
}
