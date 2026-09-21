//! Word lists: the bundled Persian, Finglish and English lists, and the shared list format. Port of
//! `js/src/word-list.ts`, which ports `WordList.cs`, plus `load` and `load_reader`.

use std::fs::File;
use std::io::Read;
use std::path::Path;
use std::sync::LazyLock;

use crate::errors::WordListError;
use crate::types::{BannedWord, WordCategory, WordMatchMode};
use crate::unicode::{is_letter, is_white_space, to_lower_invariant};
use crate::wordlists::BUNDLED_WORD_LISTS;

/// Whether `c` is whitespace in .NET's sense. Every such character is one UTF-16 unit.
fn is_white_space_char(c: char) -> bool {
    u16::try_from(u32::from(c)).is_ok_and(is_white_space)
}

/// Trims whitespace like .NET `string.Trim()`, using .NET's whitespace set (U+FEFF is not trimmed).
pub(crate) fn trim_dot_net(value: &str) -> &str {
    value.trim_matches(is_white_space_char)
}

/// The category a section heading names, or `None`. A heading is a category name only, in any letter
/// case; numbers are not names (spec 003, FR-029). Letters are tested per UTF-16 unit, as in .NET.
fn category_named(name: &str) -> Option<WordCategory> {
    let units: Vec<u16> = name.encode_utf16().collect();
    if units.is_empty() || !units.iter().all(|&unit| is_letter(unit)) {
        return None;
    }

    let lower: Vec<u16> = units.iter().map(|&unit| to_lower_invariant(unit)).collect();
    WordCategory::ALL.iter().copied().find(|category| {
        category
            .name()
            .encode_utf16()
            .map(to_lower_invariant)
            .eq(lower.iter().copied())
    })
}

fn parse(text: &str) -> Result<Vec<BannedWord>, WordListError> {
    let mut words = Vec::new();
    let mut category = WordCategory::Uncategorized;

    for (index, raw) in text.split('\n').enumerate() {
        let line = trim_dot_net(raw);
        if line.is_empty() || line.starts_with('#') {
            continue;
        }

        // More than two UTF-16 units, as in .NET: "[]" is a word, not a section.
        if line.len() > 2 && line.starts_with('[') && line.ends_with(']') {
            let name = trim_dot_net(&line[1..line.len() - 1]);
            category = category_named(name).ok_or_else(|| WordListError::UnknownCategory {
                line: index + 1,
                name: name.to_owned(),
            })?;
            continue;
        }

        let anywhere = line.starts_with('~');
        let word = trim_dot_net(if anywhere { &line[1..] } else { line });

        if !word.is_empty() {
            let mode = if anywhere {
                WordMatchMode::Anywhere
            } else {
                WordMatchMode::WholeWord
            };
            words.push(BannedWord::new(word).with_mode(mode).with_category(category));
        }
    }

    Ok(words)
}

/// Every bundled entry, parsed once.
static ALL: LazyLock<Box<[BannedWord]>> = LazyLock::new(|| {
    let mut words = Vec::new();
    for list in BUNDLED_WORD_LISTS {
        // The bundled lists are part of the crate and parse; the tests check them. An error would drop
        // that list rather than panic.
        words.extend(parse(list).unwrap_or_default());
    }
    words.into_boxed_slice()
});

/// The bundled entries without `Mild`, filtered once.
static PERSIAN_DEFAULT: LazyLock<Box<[BannedWord]>> = LazyLock::new(|| {
    ALL.iter()
        .filter(|word| word.category != WordCategory::Mild)
        .cloned()
        .collect()
});

/// Reads word lists for [`ProfanityFilter`](crate::ProfanityFilter), and gives access to the bundled
/// Persian, Finglish and English lists.
///
/// The format is one entry per line: a word or phrase is matched as whole words, a leading `~` matches it
/// anywhere (inside longer words too), and lines starting with `#` are comments. A line like `[insult]`
/// starts a section: every entry after it, until the next section, gets that category. A heading is a
/// category name in any letter case, spaces allowed; numbers are not categories. Entries before the first
/// section are `Uncategorized`. `\n` and `\r\n` line endings both work.
///
/// ```
/// use persian_text_guard::{WordCategory, WordList, WordMatchMode};
///
/// let words = WordList::parse("spam\n[ Insult ]\n~scam\n").unwrap();
/// assert_eq!(words.len(), 2);
/// assert_eq!(words[1].mode, WordMatchMode::Anywhere);
/// assert_eq!(words[1].category, WordCategory::Insult);
/// ```
#[derive(Debug)]
pub struct WordList {
    _private: (),
}

impl WordList {
    /// Every bundled entry, `Mild` included, in list order: Persian, Finglish, English.
    ///
    /// Parsed once, on first use, from any thread; every call returns the same slice.
    pub fn all() -> &'static [BannedWord] {
        &ALL
    }

    /// The bundled Persian, Finglish and English list without the `Mild` entries: profanity, sexual
    /// words, insults, slurs and harassment, curated to avoid flagging ordinary words.
    ///
    /// Nothing uses it unless you pass it to a filter. The files themselves document what is deliberately
    /// left out and why (for example «کس», which also means "person"). Every call returns the same slice.
    ///
    /// ```
    /// use persian_text_guard::{WordCategory, WordList};
    ///
    /// assert!(WordList::persian_default().iter().all(|word| word.category != WordCategory::Mild));
    /// assert!(std::ptr::eq(WordList::persian_default(), WordList::persian_default()));
    /// ```
    pub fn persian_default() -> &'static [BannedWord] {
        &PERSIAN_DEFAULT
    }

    /// The bundled entries in the given categories, in list order.
    ///
    /// ```
    /// use persian_text_guard::{ProfanityFilter, WordCategory, WordList};
    ///
    /// // A dating app: sexual words are fine, abuse is not.
    /// let words = WordList::bundled(&[WordCategory::Insult, WordCategory::Slur, WordCategory::Harassment]);
    /// let filter = ProfanityFilter::with_defaults(words);
    /// assert!(filter.count() > 0);
    /// ```
    pub fn bundled(categories: &[WordCategory]) -> Vec<&'static BannedWord> {
        Self::all()
            .iter()
            .filter(|word| categories.contains(&word.category))
            .collect()
    }

    /// Parses a word list from its text.
    ///
    /// # Errors
    ///
    /// [`WordListError::UnknownCategory`] when a section heading names a category that does not exist,
    /// with its 1-based line.
    pub fn parse(text: &str) -> Result<Vec<BannedWord>, WordListError> {
        parse(text)
    }

    /// Reads a word list from a file: UTF-8, with or without a byte-order mark.
    ///
    /// ```no_run
    /// use persian_text_guard::{ProfanityFilter, WordList};
    ///
    /// let words = WordList::load("my-words.txt")?;
    /// let filter = ProfanityFilter::with_defaults(&words);
    /// # Ok::<(), persian_text_guard::WordListError>(())
    /// ```
    ///
    /// # Errors
    ///
    /// [`WordListError::Io`] when the file cannot be opened or read (a missing file keeps the
    /// [`NotFound`](std::io::ErrorKind::NotFound) kind), [`WordListError::InvalidUtf8`] when it is not
    /// UTF-8, and [`WordListError::UnknownCategory`] as for [`parse`](Self::parse).
    pub fn load(path: impl AsRef<Path>) -> Result<Vec<BannedWord>, WordListError> {
        Self::load_reader(File::open(path)?)
    }

    /// Reads a word list from any reader to its end: UTF-8, with or without a byte-order mark. Pass the
    /// reader by value or as `&mut` to keep using it.
    ///
    /// ```
    /// use persian_text_guard::WordList;
    ///
    /// let bytes = b"\xEF\xBB\xBFspam\r\n[slur]\r\nword\r\n";
    /// let words = WordList::load_reader(&bytes[..]).unwrap();
    /// assert_eq!(words, WordList::parse("spam\n[slur]\nword\n").unwrap());
    /// ```
    ///
    /// # Errors
    ///
    /// [`WordListError::Io`] when reading fails, [`WordListError::InvalidUtf8`] when the input is not
    /// UTF-8 (its `valid_up_to` counts bytes after a byte-order mark), and
    /// [`WordListError::UnknownCategory`] as for [`parse`](Self::parse).
    pub fn load_reader(mut reader: impl Read) -> Result<Vec<BannedWord>, WordListError> {
        let mut bytes = Vec::new();
        reader.read_to_end(&mut bytes)?;
        let content = bytes.strip_prefix(b"\xEF\xBB\xBF").unwrap_or(&bytes);
        let text = std::str::from_utf8(content).map_err(|error| WordListError::InvalidUtf8 {
            valid_up_to: error.valid_up_to(),
        })?;
        parse(text)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn numeric_headings_are_unknown_categories_on_line_1() {
        for text in ["[3]\nword\n", "[+4]\nword\n", "[ 03 ]\nword\n"] {
            match WordList::parse(text) {
                Err(WordListError::UnknownCategory { line, .. }) => assert_eq!(line, 1, "{text:?}"),
                other => panic!("{text:?}: {other:?}"),
            }
        }
    }

    #[test]
    fn names_in_any_case_with_spaces_around_them() {
        let words = WordList::parse("[ Insult ]\nword\n[SLUR]\n~other\n").unwrap();
        assert_eq!(
            words,
            [
                BannedWord::new("word").with_category(WordCategory::Insult),
                BannedWord::new("other")
                    .with_mode(WordMatchMode::Anywhere)
                    .with_category(WordCategory::Slur),
            ]
        );
    }

    #[test]
    fn a_two_character_line_is_a_word_not_a_section() {
        assert_eq!(WordList::parse("[]\n").unwrap(), [BannedWord::new("[]")]);
    }

    #[test]
    fn the_bundled_lists_parse_and_default_excludes_mild() {
        for list in BUNDLED_WORD_LISTS {
            assert!(parse(list).is_ok());
        }
        assert_eq!(WordList::all().len(), 1250);
        assert_eq!(WordList::persian_default().len(), 1025);
        assert!(
            WordList::persian_default()
                .iter()
                .all(|word| word.category != WordCategory::Mild)
        );
    }

    #[test]
    fn trimming_follows_dot_net() {
        assert_eq!(trim_dot_net("\u{85} word\r"), "word");
        assert_eq!(trim_dot_net("\u{FEFF}word"), "\u{FEFF}word");
    }
}
