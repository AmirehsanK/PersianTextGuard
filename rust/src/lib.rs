#![doc = include_str!("../README.md")]
#![forbid(unsafe_code)]
#![warn(missing_docs)]

mod bytes;
mod errors;
mod filter;
mod fold;
mod normalizer;
mod regions;
mod scan;
mod source_map;
mod tables;
mod types;
mod unicode;
mod utf16;
mod word_list;

mod wordlists {
    include!(concat!(env!("OUT_DIR"), "/wordlists.rs"));
}

pub use errors::{InvalidMask, ParseNameError, WordListError};
pub use filter::ProfanityFilter;
pub use normalizer::{normalize, to_ascii_digits, to_persian_digits, tokenize};
pub use types::{
    BannedWord, EvasionKind, EvasionSet, Normalization, NormalizationStep, ProfanityFilterOptions,
    ProfanityMatch, WordCategory, WordMatchMode,
};
pub use word_list::WordList;
