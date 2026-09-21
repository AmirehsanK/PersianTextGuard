//! The UTF-16 view the matcher runs on, and the conversion of its positions to bytes (research R2).
//!
//! .NET reads text as UTF-16 units and decides tokens by testing single units: half of a surrogate pair
//! is not a letter. A Rust `&str` is UTF-8, so a port testing `char`s would read U+20000 (a CJK letter)
//! as one letter where .NET sees two surrogate units, and would find different words. So the matcher,
//! ported line by line from TypeScript, runs on a *view*: the message's UTF-16 units, where `text[i]` is
//! unit *i* as `charCodeAt(i)` is in TypeScript.
//!
//! The view never leaves the crate. Match regions are converted to byte offsets of the caller's string
//! with [`ByteMap`]; `censor` splices the caller's own string; normalizer output is decoded with
//! [`from_view`]. For example, .NET flags `kir𠀀` as `kir` at units 0..5 (the two surrogates are not
//! letters, so they join the word); here that region is bytes 0..7 of the string, all of it.

/// The UTF-16 view of `text`.
pub(crate) fn to_view(text: &str) -> Vec<u16> {
    text.encode_utf16().collect()
}

/// A view decoded back to UTF-8. No lone surrogate is produced inside the crate, but a lossy decode can
/// never panic either way.
pub(crate) fn from_view(view: &[u16]) -> String {
    String::from_utf16_lossy(view)
}

/// Converts regions of a message's view, in UTF-16 units, to byte regions of the message.
///
/// The identity for ASCII messages. Otherwise it holds, for each unit, the byte offset of the character
/// it belongs to. Regions never split a surrogate pair, so every converted offset is a character
/// boundary.
pub(crate) struct ByteMap<'a> {
    text: &'a str,
    starts: Option<Vec<usize>>,
}

impl<'a> ByteMap<'a> {
    /// The map for `text`.
    pub(crate) fn new(text: &'a str) -> Self {
        if text.is_ascii() {
            return Self { text, starts: None };
        }

        let mut starts = Vec::with_capacity(text.len());
        for (offset, c) in text.char_indices() {
            starts.push(offset);
            if c.len_utf16() == 2 {
                starts.push(offset);
            }
        }

        Self {
            text,
            starts: Some(starts),
        }
    }

    /// The byte region `(start, len)` of the unit region `(index, length)`. `length` is at least 1.
    pub(crate) fn to_bytes(&self, index: usize, length: usize) -> (usize, usize) {
        let Some(starts) = &self.starts else {
            return (index, length);
        };

        let start = starts.get(index).copied().unwrap_or(self.text.len());
        let last = starts
            .get(index + length.max(1) - 1)
            .copied()
            .unwrap_or(self.text.len());
        let end = last + self.text[last..].chars().next().map_or(0, char::len_utf8);
        (start, end.max(start) - start)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn a_supplementary_letter_is_two_units_and_four_bytes() {
        let text = "kir\u{20000}";
        assert_eq!(to_view(text).len(), 5);
        let map = ByteMap::new(text);
        assert_eq!(map.to_bytes(0, 5), (0, 7));
        assert_eq!(map.to_bytes(3, 2), (3, 4));
        assert_eq!(map.to_bytes(0, 3), (0, 3));
    }

    #[test]
    fn ascii_is_the_identity() {
        let map = ByteMap::new("hello kir");
        assert_eq!(map.to_bytes(6, 3), (6, 3));
    }

    #[test]
    fn persian_letters_are_two_bytes() {
        let text = "\u{1F600} کیر";
        let map = ByteMap::new(text);
        assert_eq!(map.to_bytes(3, 3), (5, 6));
        assert_eq!(&text[5..11], "کیر");
    }

    #[test]
    fn views_round_trip() {
        let text = "سلام \u{1F600} kir";
        assert_eq!(from_view(&to_view(text)), text);
    }
}
