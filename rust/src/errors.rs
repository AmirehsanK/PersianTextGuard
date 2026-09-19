//! The crate's errors. Only masks, names and word-list input can be wrong; no text is ever rejected.

use std::error::Error;
use std::fmt;
use std::io;

/// A censoring mask that is a letter, a digit, whitespace, a control character, or a character above
/// U+FFFF. Returned by [`ProfanityFilter::censor_with`](crate::ProfanityFilter::censor_with) and
/// [`censor_bytes_with`](crate::ProfanityFilter::censor_bytes_with), which check the mask before looking
/// at the text.
///
/// A mask made of letters could spell a word, and one above U+FFFF would be two UTF-16 units in the
/// other ports, which all mask with one unit.
///
/// ```
/// use persian_text_guard::{InvalidMask, ProfanityFilter, WordList};
///
/// let filter = ProfanityFilter::with_defaults(WordList::persian_default());
/// assert_eq!(filter.censor_with("", 'x'), Err(InvalidMask { mask: 'x' }));
/// assert_eq!(filter.censor_with("this is kir", '#').as_deref(), Ok("this is ####"));
/// ```
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct InvalidMask {
    /// The mask that was refused.
    pub mask: char,
}

impl fmt::Display for InvalidMask {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(
            formatter,
            "invalid mask {:?}: the mask must not be a letter, a digit, whitespace, a control character or a character above U+FFFF",
            self.mask
        )
    }
}

impl Error for InvalidMask {}

/// A name that [`FromStr`](std::str::FromStr) does not know, for one of the enumerations or
/// [`Normalization`](crate::Normalization) presets. Names are the corpus names (`"wholeWord"`,
/// `"slur"`), case-sensitive; the message lists the valid ones.
///
/// ```
/// use persian_text_guard::WordCategory;
///
/// let error = "nope".parse::<WordCategory>().unwrap_err();
/// assert_eq!(error.text(), "nope");
/// assert!(error.to_string().contains("slur"));
/// ```
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct ParseNameError {
    kind: &'static str,
    text: String,
    valid: &'static [&'static str],
}

impl ParseNameError {
    pub(crate) fn new(kind: &'static str, text: &str, valid: &'static [&'static str]) -> Self {
        Self {
            kind,
            text: text.to_owned(),
            valid,
        }
    }

    /// The text that was given.
    pub fn text(&self) -> &str {
        &self.text
    }

    /// The valid names.
    pub fn valid_names(&self) -> &'static [&'static str] {
        self.valid
    }
}

impl fmt::Display for ParseNameError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(
            formatter,
            "unknown {} '{}'; expected one of: {}",
            self.kind,
            self.text,
            self.valid.join(", ")
        )
    }
}

impl Error for ParseNameError {}

/// Why a word list could not be read, from [`WordList::parse`](crate::WordList::parse),
/// [`load`](crate::WordList::load) or [`load_reader`](crate::WordList::load_reader).
///
/// `#[non_exhaustive]`: a `match` needs a wildcard arm.
///
/// ```
/// use persian_text_guard::{WordList, WordListError};
///
/// match WordList::parse("[nonsense]\nword\n") {
///     Err(WordListError::UnknownCategory { line, name }) => {
///         assert_eq!((line, name.as_str()), (1, "nonsense"));
///     }
///     other => panic!("unexpected {other:?}"),
/// }
/// ```
#[derive(Debug)]
#[non_exhaustive]
pub enum WordListError {
    /// A section heading names a category that does not exist. Headings are category names in any
    /// letter case (`[insult]`, `[ Insult ]`); numbers are not names, so `[3]` is this error.
    UnknownCategory {
        /// The 1-based line of the heading.
        line: usize,
        /// The heading's name, as written between the brackets and trimmed.
        name: String,
    },
    /// The file could not be opened or read. The [`io::Error`] keeps its kind.
    Io(io::Error),
    /// The input is not UTF-8.
    InvalidUtf8 {
        /// The byte offset of the first invalid byte, counted after a dropped byte-order mark.
        valid_up_to: usize,
    },
}

impl fmt::Display for WordListError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::UnknownCategory { line, name } => {
                write!(formatter, "Line {line}: unknown word category '{name}'.")
            }
            Self::Io(error) => write!(formatter, "cannot read the word list: {error}"),
            Self::InvalidUtf8 { valid_up_to } => {
                write!(
                    formatter,
                    "the word list is not valid UTF-8 (invalid byte at offset {valid_up_to})"
                )
            }
        }
    }
}

impl Error for WordListError {
    fn source(&self) -> Option<&(dyn Error + 'static)> {
        match self {
            Self::Io(error) => Some(error),
            _ => None,
        }
    }
}

impl From<io::Error> for WordListError {
    fn from(error: io::Error) -> Self {
        Self::Io(error)
    }
}
