//! The byte versions' decoding: input that may not be UTF-8, read as Rust's lossy decoding reads it,
//! with positions moved back to the caller's bytes (research R3, FR-008a).
//!
//! The bytes are decoded with `<[u8]>::utf8_chunks`, the iterator behind `String::from_utf8_lossy`, so
//! each maximal invalid sequence becomes exactly one U+FFFD. Valid input is borrowed, not copied. A table
//! maps each byte of the decoded string back to the caller's bytes: valid bytes one to one, and each
//! U+FFFD's three bytes to the whole invalid sequence it replaced. So a region covering a U+FFFD covers
//! the whole invalid sequence, and a region never ends inside a valid character.

use std::borrow::Cow;

use crate::types::ProfanityMatch;

/// Bytes decoded lossily, with the way back to the caller's bytes.
pub(crate) struct DecodedBytes<'a> {
    /// The decoded text: the caller's bytes when they are valid UTF-8.
    pub(crate) text: Cow<'a, str>,
    /// For each byte of `text`, the caller's byte range `(start, end)` it came from. `None` when the
    /// input was valid, and positions are the same.
    sources: Option<Vec<(usize, usize)>>,
}

impl<'a> DecodedBytes<'a> {
    pub(crate) fn new(bytes: &'a [u8]) -> Self {
        if let Ok(text) = std::str::from_utf8(bytes) {
            return Self {
                text: Cow::Borrowed(text),
                sources: None,
            };
        }

        let mut text = String::with_capacity(bytes.len() + 8);
        let mut sources = Vec::with_capacity(bytes.len() + 8);
        let mut offset = 0;

        for chunk in bytes.utf8_chunks() {
            let valid = chunk.valid();
            text.push_str(valid);
            sources.extend((offset..offset + valid.len()).map(|source| (source, source + 1)));
            offset += valid.len();

            let invalid = chunk.invalid();
            if !invalid.is_empty() {
                text.push(char::REPLACEMENT_CHARACTER);
                let span = (offset, offset + invalid.len());
                sources.extend(std::iter::repeat_n(span, char::REPLACEMENT_CHARACTER.len_utf8()));
                offset += invalid.len();
            }
        }

        Self {
            text: Cow::Owned(text),
            sources: Some(sources),
        }
    }

    /// The caller's byte region `(start, len)` for the region `(start, len)` of the decoded text.
    pub(crate) fn region_to_source(&self, start: usize, len: usize) -> (usize, usize) {
        let Some(sources) = &self.sources else {
            return (start, len);
        };

        let first = sources.get(start).map_or(0, |&(source, _)| source);
        let last = sources
            .get((start + len).saturating_sub(1))
            .map_or(first, |&(_, end)| end);
        (first, last.max(first) - first)
    }

    /// `found`, a match in the decoded text, moved to the caller's bytes.
    pub(crate) fn to_source<'f>(&self, found: ProfanityMatch<'f>) -> ProfanityMatch<'f> {
        let (start, len) = self.region_to_source(found.start, found.len);
        ProfanityMatch { start, len, ..found }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn valid_input_is_borrowed() {
        let decoded = DecodedBytes::new("kir".as_bytes());
        assert!(matches!(decoded.text, Cow::Borrowed("kir")));
        assert_eq!(decoded.region_to_source(1, 2), (1, 2));
    }

    #[test]
    fn a_replacement_covers_the_whole_invalid_sequence() {
        // \xF0\x9F\x98 is a truncated four-byte sequence: one U+FFFD for three bytes.
        let decoded = DecodedBytes::new(b"a\xF0\x9F\x98b");
        assert_eq!(decoded.text, "a\u{FFFD}b");
        assert_eq!(decoded.region_to_source(1, 3), (1, 3));
        assert_eq!(decoded.region_to_source(0, 5), (0, 5));
        assert_eq!(decoded.region_to_source(4, 1), (4, 1));
    }

    #[test]
    fn each_maximal_invalid_sequence_is_one_replacement() {
        let decoded = DecodedBytes::new(b"\xFF\xFE");
        assert_eq!(decoded.text, String::from_utf8_lossy(b"\xFF\xFE"));
    }
}
