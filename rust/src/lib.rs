#![doc = include_str!("../README.md")]
#![forbid(unsafe_code)]
#![warn(missing_docs)]

mod errors;
// Used by the filter (T024–T026); remove the allowance when it lands.
#[allow(dead_code)]
mod fold;
#[allow(dead_code)]
mod normalizer;
#[allow(dead_code)]
mod source_map;
mod tables;
#[allow(dead_code)]
mod types;
#[allow(dead_code)]
mod unicode;
#[allow(dead_code)]
mod utf16;
mod word_list;

mod wordlists {
    include!(concat!(env!("OUT_DIR"), "/wordlists.rs"));
}

pub use errors::{InvalidMask, ParseNameError, WordListError};
pub use normalizer::{normalize, to_ascii_digits, to_persian_digits, tokenize};
pub use types::{
    BannedWord, EvasionKind, EvasionSet, Normalization, NormalizationStep, ProfanityFilterOptions,
    ProfanityMatch, WordCategory, WordMatchMode,
};
pub use word_list::WordList;
