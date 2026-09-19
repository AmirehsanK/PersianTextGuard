# Contract: `persian-text-guard` Public API (Rust)

**Feature**: [../spec.md](../spec.md) | **Data model**: [../data-model.md](../data-model.md) | **Research**: [../research.md](../research.md) R2, R3, R6, R7

This is the surface users reach as `persian_text_guard::*`. `cargo-semver-checks` compares it with the
previous release (research R10). Every item has rustdoc (FR-023), not repeated here. Bodies are omitted.

## Declarations

```rust
// ------------------------------------------------------------------ enumerations
#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash, Default)]
#[non_exhaustive]
pub enum WordMatchMode { #[default] WholeWord, Anywhere }

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash, PartialOrd, Ord, Default)]
#[non_exhaustive]
pub enum WordCategory { #[default] Uncategorized, Profanity, Sexual, Insult, Slur, Harassment, Mild }

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash, PartialOrd, Ord)]
#[non_exhaustive]
pub enum EvasionKind { RepeatedLetters, LookalikeCharacters, SplitWord }

#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash, PartialOrd, Ord)]
#[non_exhaustive]
pub enum NormalizationStep {
    CompatibilityForms, UnifyLetters, RemoveDiacritics, RemoveTatweel, RemoveZeroWidth,
    RemoveBidiControls, AsciiDigits, LowerCase, CollapseWhitespace, CollapseRepeats,
}

// Each of the four enums:
//   impl FromStr (Err = ParseNameError) and Display, using the corpus names: "wholeWord", "slur",
//     "lookalikeCharacters", "unifyLetters", …
//   impl <Enum> { pub const ALL: &'static [Self]; pub fn name(self) -> &'static str; }   // a slice, so a new variant is not a type change

/// The evasions a match needed, in declaration order when iterated.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash, Default)]
pub struct EvasionSet { /* private */ }
impl EvasionSet {
    pub const EMPTY: Self;
    pub fn contains(self, kind: EvasionKind) -> bool;
    pub fn is_empty(self) -> bool;
    pub fn len(self) -> usize;
    pub fn iter(self) -> impl Iterator<Item = EvasionKind>;       // RepeatedLetters, LookalikeCharacters, SplitWord
}
impl IntoIterator for EvasionSet { /* Item = EvasionKind */ }

/// A set of normalization steps; steps always run in declaration order.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash, Default)]
pub struct Normalization { /* private */ }
impl Normalization {
    pub const NONE: Self;
    pub const STANDARD: Self;       // CompatibilityForms | UnifyLetters | RemoveTatweel | RemoveBidiControls | CollapseWhitespace
    pub const COMPARISON: Self;     // every step
    pub fn contains(self, step: NormalizationStep) -> bool;
    pub fn steps(self) -> impl Iterator<Item = NormalizationStep>;
}
impl From<NormalizationStep> for Normalization {}
impl FromIterator<NormalizationStep> for Normalization {}
impl BitOr for Normalization {}  impl BitOr<NormalizationStep> for Normalization {}
impl BitOr for NormalizationStep { type Output = Normalization; }
impl FromStr for Normalization {} // "comparison", "standard", "none"; Err = ParseNameError

// ------------------------------------------------------------------ entries and word lists
#[derive(Clone, Debug, PartialEq, Eq, Hash)]
pub struct BannedWord {
    pub text: String,
    pub mode: WordMatchMode,
    pub category: WordCategory,
}
impl BannedWord {
    pub fn new(text: impl Into<String>) -> Self;                  // WholeWord, Uncategorized
    pub fn with_mode(self, mode: WordMatchMode) -> Self;
    pub fn with_category(self, category: WordCategory) -> Self;
}

pub struct WordList;                                              // no instances; associated functions only
impl WordList {
    pub fn all() -> &'static [BannedWord];                        // parsed once; the same slice every call
    pub fn persian_default() -> &'static [BannedWord];            // all() without Mild
    pub fn bundled(categories: &[WordCategory]) -> Vec<&'static BannedWord>;   // list order
    pub fn parse(text: &str) -> Result<Vec<BannedWord>, WordListError>;
    pub fn load(path: impl AsRef<Path>) -> Result<Vec<BannedWord>, WordListError>;
    pub fn load_reader(reader: impl Read) -> Result<Vec<BannedWord>, WordListError>;
}

// ------------------------------------------------------------------ filter
#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
#[non_exhaustive]
pub struct ProfanityFilterOptions {
    pub squeeze_repeated_letters: bool,     // true
    pub fold_lookalike_characters: bool,    // true
    pub join_spaced_letters: bool,          // true
}
impl Default for ProfanityFilterOptions {}
impl ProfanityFilterOptions {
    pub fn squeeze_repeated_letters(self, on: bool) -> Self;
    pub fn fold_lookalike_characters(self, on: bool) -> Self;
    pub fn join_spaced_letters(self, on: bool) -> Self;
}

#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct ProfanityMatch<'f> {
    pub word: &'f BannedWord,               // the entry as given, owned by the filter
    pub evasion: EvasionSet,
    pub start: usize,                        // bytes into the message
    pub len: usize,                          // bytes; ≥ 1
}
impl ProfanityMatch<'_> { pub fn range(&self) -> Range<usize>; }

#[derive(Clone, Debug)]
pub struct ProfanityFilter { /* private, immutable */ }
impl ProfanityFilter {
    pub fn new<I>(words: I, options: ProfanityFilterOptions) -> Self
    where I: IntoIterator, I::Item: Borrow<BannedWord>;
    pub fn with_defaults<I>(words: I) -> Self
    where I: IntoIterator, I::Item: Borrow<BannedWord>;
    pub fn count(&self) -> usize;

    pub fn contains_profanity(&self, text: &str) -> bool;
    pub fn find_match(&self, text: &str) -> Option<ProfanityMatch<'_>>;
    pub fn find_matches(&self, text: &str) -> Vec<ProfanityMatch<'_>>;
    pub fn censor(&self, text: &str) -> String;                                   // mask '*'
    pub fn censor_with(&self, text: &str, mask: char) -> Result<String, InvalidMask>;

    // Byte versions (FR-008a): invalid UTF-8 read as U+FFFD; positions in the caller's bytes.
    pub fn contains_profanity_bytes(&self, text: &[u8]) -> bool;
    pub fn find_match_bytes(&self, text: &[u8]) -> Option<ProfanityMatch<'_>>;
    pub fn find_matches_bytes(&self, text: &[u8]) -> Vec<ProfanityMatch<'_>>;
    pub fn censor_bytes(&self, text: &[u8]) -> Vec<u8>;
    pub fn censor_bytes_with(&self, text: &[u8], mask: char) -> Result<Vec<u8>, InvalidMask>;
}

// ------------------------------------------------------------------ normalization
pub fn normalize(text: &str, steps: Normalization) -> String;
pub fn tokenize(text: &str) -> Vec<&str>;                         // slices of `text`
pub fn to_persian_digits(text: &str) -> String;
pub fn to_ascii_digits(text: &str) -> String;

// ------------------------------------------------------------------ errors
// Each: Debug, Display, std::error::Error, Send + Sync + 'static.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct InvalidMask { pub mask: char }
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct ParseNameError { /* the kind of name and the text given */ }
#[derive(Debug)]
#[non_exhaustive]
pub enum WordListError {
    UnknownCategory { line: usize, name: String },                // 1-based line
    Io(std::io::Error),
    InvalidUtf8 { valid_up_to: usize },                           // byte offset of the first invalid byte
}
```

Nothing else is public. The UTF-16 view, the scanner, source maps and the generated tables live in private
modules.

## Behavioural guarantees

| # | Guarantee | Checked by |
| --- | --- | --- |
| G1 | For every `&str` (empty, whitespace-only, noncharacters, supplementary characters, 132,000 characters): `contains_profanity`, `find_match`, `find_matches`, `censor`, `censor_with` with a valid mask, `normalize`, `tokenize`, `to_persian_digits` and `to_ascii_digits` return, and never panic. For every `&[u8]` the byte versions do too. | corpus robustness cases; `tests/no_panic.rs` (proptest, SC-007) |
| G2 | Results equal the conformance corpus for every case, with positions converted from code points to bytes, lone surrogates read as U+FFFD and `null` as `""` (the amended runner rules). | `tests/corpus.rs` |
| G3 | `contains_profanity(t) == find_match(t).is_some() == !find_matches(t).is_empty()`, and `censor(t) != t` exactly when there is a match. | `tests/api.rs` over every corpus input |
| G4 | `find_matches` is ordered by `start`, non-overlapping; `start` and `start + len` are character boundaries of `t`, `len ≥ 1`, `start + len ≤ t.len()`; `&t[m.range()]` is the region. | `tests/api.rs`, corpus |
| G5 | `censor_with` checks the mask before the text: a letter, digit, whitespace, control character or a character above U+FFFF is `Err(InvalidMask)`, even for `""`. Text outside a region is returned byte for byte. | `tests/api.rs`, corpus mask cases |
| G6 | A filter never changes after `new`. `ProfanityFilter`, `ProfanityMatch`, `BannedWord`, every enum, set and error are `Send + Sync`. | `tests/api.rs` compile-time assertions; `tests/threads.rs` |
| G7 | `WordList::all()` and `persian_default()` return the same slice (same pointer) on every call, from every thread; `persian_default()` has no `Mild` entry. | `tests/api.rs`, `tests/threads.rs` |
| G8 | `WordList::parse` reads headings by category name only, case-insensitively, spaces allowed; `[3]` is `UnknownCategory { line: 1, .. }`. `load` and `load_reader` equal `parse` of the input's UTF-8 text without a BOM; invalid UTF-8 is `InvalidUtf8`, a read failure `Io`. | corpus word-list cases; `tests/word_list_load.rs` |
| G9 | The byte versions give, for any bytes `b`, the same decision, entries, evasions and match count as the string versions on `String::from_utf8_lossy(b)`; their regions slice `b` and never split a valid UTF-8 sequence; `censor_bytes` copies every byte outside a region unchanged, invalid ones included. | `tests/bytes.rs`, `tests/no_panic.rs` |
| G10 | The bundled selections equal the .NET, JavaScript and Python packages' entry for entry, in order. | corpus category-selection cases; quickstart §3 |
| G11 | Unknown names in `FromStr` are `Err(ParseNameError)` naming the valid names. | `tests/api.rs` |

## Names across ports

This table is copied into `rust/README.md` (FR-012).

| .NET | JavaScript/TypeScript | Python | Rust |
| --- | --- | --- | --- |
| `new ProfanityFilter(words, options)` | `new ProfanityFilter(words, options)` | `ProfanityFilter(words, options)` | `ProfanityFilter::new(words, options)` / `with_defaults(words)` |
| `filter.Count` | `filter.count` | `filter.count` | `filter.count()` |
| `filter.ContainsProfanity(text)` | `filter.containsProfanity(text)` | `filter.contains_profanity(text)` | `filter.contains_profanity(text)` |
| `filter.FindMatch(text)` | `filter.findMatch(text)` | `filter.find_match(text)` | `filter.find_match(text)` (`None` when there is no match) |
| `filter.FindMatches(text)` | `filter.findMatches(text)` | `filter.find_matches(text)` | `filter.find_matches(text)` (a `Vec`) |
| `filter.Censor(text, '#')` | `filter.censor(text, '#')` | `filter.censor(text, "#")` | `filter.censor_with(text, '#')?` (`filter.censor(text)` for `*`) |
| — | — | — | `contains_profanity_bytes`, `find_match_bytes`, `find_matches_bytes`, `censor_bytes`, `censor_bytes_with` |
| `new BannedWord("x", WordMatchMode.Anywhere) { Category = WordCategory.Slur }` | `{ text: 'x', mode: 'anywhere', category: 'slur' }` | `BannedWord("x", WordMatchMode.ANYWHERE, WordCategory.SLUR)` | `BannedWord::new("x").with_mode(WordMatchMode::Anywhere).with_category(WordCategory::Slur)` |
| `ProfanityFilterOptions { SqueezeRepeatedLetters = false }` | `{ squeezeRepeatedLetters: false }` | `ProfanityFilterOptions(squeeze_repeated_letters=False)` | `ProfanityFilterOptions::default().squeeze_repeated_letters(false)` |
| `match.Word`, `.Evasion`, `.Index`, `.Length` | `match.word`, `.evasion`, `.index`, `.length` | `match.word`, `.evasion`, `.index`, `.length` | `m.word`, `m.evasion`, `m.start`, `m.len`, `m.range()` (bytes) |
| `EvasionKind.LookalikeCharacters \| EvasionKind.RepeatedLetters` | `['repeatedLetters', 'lookalikeCharacters']` | `(EvasionKind.REPEATED_LETTERS, EvasionKind.LOOKALIKE_CHARACTERS)` | `EvasionSet` iterating `RepeatedLetters, LookalikeCharacters` |
| `WordList.All`, `WordList.PersianDefault` | `WordList.all`, `WordList.persianDefault` | `WordList.all()`, `WordList.persian_default()` | `WordList::all()`, `WordList::persian_default()` |
| `WordList.Bundled(WordCategory.Slur)` | `WordList.bundled('slur')` | `WordList.bundled("slur")` | `WordList::bundled(&[WordCategory::Slur])` |
| `WordList.Parse(text)`, `WordList.Load(stream)` | `WordList.parse(text)` | `WordList.parse(text)`, `WordList.load(path_or_file)` | `WordList::parse(text)?`, `WordList::load(path)?`, `WordList::load_reader(reader)?` |
| `PersianNormalizer.Normalize(text, PersianNormalization.Standard)` | `normalize(text, 'standard')` | `normalize(text, "standard")` | `normalize(text, Normalization::STANDARD)` |
| `PersianNormalizer.Tokenize(text)` | `tokenize(text)` | `tokenize(text)` | `tokenize(text)` (slices of `text`) |
| `PersianNormalizer.ToPersianDigits(text)` | `toPersianDigits(text)` | `to_persian_digits(text)` | `to_persian_digits(text)` |
| `FormatException` | `WordListFormatError` | `WordListFormatError` | `WordListError::UnknownCategory { line, name }` |
| `ArgumentException` (mask) | `RangeError` | `ValueError` | `Err(InvalidMask)` |

## Compatibility

- **1.5.0 is the first release of this crate**, and the baseline for `cargo-semver-checks` (FR-022).
- **After that**, removing or changing any declaration above is MAJOR, and adding to it is MINOR. The
  four enums, `ProfanityFilterOptions` and `WordListError` are `#[non_exhaustive]` from the first release,
  so a new category, evasion, step, option or error kind is MINOR in Rust too, as the constitution rules
  across ports; callers who `match` on them need a wildcard arm, which the README shows. A release that
  adds a category or evasion still calls it out in the release notes, because it changes which inputs
  match.
- The minimum supported Rust version is 1.85, raised only as FR-002 says.
