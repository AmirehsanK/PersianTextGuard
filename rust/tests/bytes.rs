//! The byte versions (G9, FR-008a): invalid UTF-8 read as U+FFFD, positions in the caller's bytes.

#![allow(clippy::unwrap_used, clippy::expect_used)]

use persian_text_guard::{InvalidMask, ProfanityFilter, WordList};

fn default_filter() -> ProfanityFilter {
    ProfanityFilter::with_defaults(WordList::persian_default())
}

#[test]
fn valid_utf8_gives_exactly_the_string_results() {
    let filter = default_filter();
    for text in [
        "",
        "kir and motherfucker",
        "😀 کیر",
        "sh1t and f u c k",
        "سلام، سفارشم کی میرسه؟",
    ] {
        let bytes = text.as_bytes();
        assert_eq!(
            filter.contains_profanity_bytes(bytes),
            filter.contains_profanity(text)
        );
        assert_eq!(filter.find_match_bytes(bytes), filter.find_match(text));
        assert_eq!(filter.find_matches_bytes(bytes), filter.find_matches(text));
        assert_eq!(filter.censor_bytes(bytes), filter.censor(text).into_bytes());
    }
}

#[test]
fn invalid_bytes_are_kept_and_positions_are_bytes() {
    let filter = default_filter();
    let text = b"kir \xFF fuck";
    let matches = filter.find_matches_bytes(text);
    assert_eq!(matches.len(), 2);
    assert_eq!(matches[0].word.text, "kir");
    assert_eq!(matches[0].range(), 0..3);
    assert_eq!(matches[1].word.text, "fuck");
    assert_eq!(matches[1].range(), 6..10);
    assert_eq!(filter.censor_bytes(text), b"**** \xFF ****");
}

#[test]
fn a_truncated_sequence_inside_a_word() {
    let filter = default_filter();
    let text = b"k\xC3ir";
    let lossy = String::from_utf8_lossy(text);
    assert_eq!(
        filter.contains_profanity_bytes(text),
        filter.contains_profanity(&lossy)
    );

    let bytes = filter.find_matches_bytes(text);
    let string = filter.find_matches(&lossy);
    assert_eq!(bytes.len(), string.len());
    if let Some(found) = bytes.first() {
        assert_eq!(found.range(), 0..text.len(), "the region covers the whole word");
        assert_eq!(found.word, string[0].word);
        assert_eq!(found.evasion, string[0].evasion);
    }
}

#[test]
fn masks_are_spliced_as_utf8() {
    let filter = default_filter();
    assert_eq!(
        filter.censor_bytes_with(b"kir \xFE", '\u{25A0}').unwrap(),
        "■■■■ ".bytes().chain([0xFE]).collect::<Vec<_>>()
    );
    assert_eq!(
        filter.censor_bytes_with(b"kir", 'x'),
        Err(InvalidMask { mask: 'x' })
    );
    assert_eq!(filter.censor_bytes_with(b"", 'x'), Err(InvalidMask { mask: 'x' }));
    assert_eq!(filter.censor_bytes(b""), b"");
    assert!(!filter.contains_profanity_bytes(b""));
    assert!(filter.find_match_bytes(b"\xFF\xFE").is_none());
}
