//! No public function panics, for any text or bytes (SC-007, G1, G9).
//!
//! The case count is proptest's `PROPTEST_CASES`, defaulting here to 2,000 per strategy so every
//! `cargo test` stays quick. The `Rust checks` CI job runs SC-007's full count:
//! `PROPTEST_CASES=100000 cargo test --locked --release --test no_panic`.

#![allow(clippy::unwrap_used, clippy::expect_used)]

use std::sync::LazyLock;

use persian_text_guard::{
    Normalization, ProfanityFilter, WordList, normalize, to_ascii_digits, to_persian_digits, tokenize,
};
use proptest::prelude::*;

static FILTER: LazyLock<ProfanityFilter> =
    LazyLock::new(|| ProfanityFilter::with_defaults(WordList::persian_default()));

fn config() -> ProptestConfig {
    let cases = std::env::var("PROPTEST_CASES")
        .ok()
        .and_then(|cases| cases.parse().ok())
        .unwrap_or(2000);
    ProptestConfig {
        cases,
        failure_persistence: None,
        ..ProptestConfig::default()
    }
}

/// Characters biased toward what the matcher looks at: Persian letters, ZWNJ, digits, symbols,
/// supplementary characters and noncharacters, plus any character at all.
fn interesting_char() -> impl Strategy<Value = char> {
    prop_oneof![
        6 => (0x0600u32..=0x06FF).prop_filter_map("a char", char::from_u32),
        4 => prop::sample::select(b"kirfuckshitkos aeiou".to_vec()).prop_map(char::from),
        2 => prop::sample::select(vec!['\u{200C}', '\u{200D}', '\u{200B}', '\u{FEFF}', '\u{0640}', '\u{064B}', '\u{00A0}', '\u{0085}']),
        2 => prop::sample::select("0123456789۰۱۲۳۴۵۶۷۸۹".chars().collect::<Vec<_>>()),
        2 => prop::sample::select("!@#$%^&*()_+-=.,;:'\"<>?/|\\~`•·♥❤€¡".chars().collect::<Vec<_>>()),
        2 => (0x1F100u32..=0x1F6FF).prop_filter_map("a char", char::from_u32),
        1 => (0x20000u32..=0x2A6DF).prop_filter_map("a char", char::from_u32),
        1 => prop::sample::select(vec!['\u{FDD0}', '\u{FDEF}', '\u{FFFE}', '\u{FFFF}', '\u{1FFFE}', '\u{10FFFF}', '\u{FFFD}']),
        1 => (0x0300u32..=0x036F).prop_filter_map("a combining mark", char::from_u32),
        1 => (0x0400u32..=0x04FF).prop_filter_map("a Cyrillic letter", char::from_u32),
        2 => any::<char>(),
    ]
}

/// Strings of interesting characters, some in runs of repeats.
fn text() -> impl Strategy<Value = String> {
    prop::collection::vec(
        (
            interesting_char(),
            prop_oneof![8 => Just(1usize), 1 => 2usize..6, 1 => 6usize..40],
        ),
        0..48,
    )
    .prop_map(|runs| {
        runs.into_iter()
            .flat_map(|(c, times)| std::iter::repeat_n(c, times))
            .collect()
    })
}

/// Byte sequences, mostly invalid UTF-8: raw bytes mixed with pieces of valid text.
fn bytes() -> impl Strategy<Value = Vec<u8>> {
    prop::collection::vec(
        prop_oneof![
            3 => any::<u8>().prop_map(|byte| vec![byte]),
            2 => prop::sample::select(vec![0x80u8, 0xBF, 0xC0, 0xC3, 0xE0, 0xED, 0xF0, 0xF4, 0xF8, 0xFF]).prop_map(|byte| vec![byte]),
            3 => interesting_char().prop_map(|c| c.to_string().into_bytes()),
        ],
        0..48,
    )
    .prop_map(|pieces| pieces.concat())
}

fn check_text(text: &str) {
    let filter = &*FILTER;
    let contains = filter.contains_profanity(text);
    let first = filter.find_match(text);
    let all = filter.find_matches(text);
    assert_eq!(contains, first.is_some());
    assert_eq!(contains, !all.is_empty());

    let mut previous_end = 0;
    for found in first.iter().chain(&all) {
        assert!(found.len >= 1 && found.start + found.len <= text.len());
        assert!(text.get(found.range()).is_some(), "a region splits a character");
    }
    for found in &all {
        assert!(found.start >= previous_end, "matches overlap");
        previous_end = found.start + found.len;
    }

    let censored = filter.censor(text);
    assert_eq!(censored != text, contains);
    assert!(
        !filter.contains_profanity(&censored),
        "censored text still matches: {censored:?}"
    );
    assert!(filter.censor_with(text, '#').is_ok());

    for steps in [
        Normalization::COMPARISON,
        Normalization::STANDARD,
        Normalization::NONE,
    ] {
        normalize(text, steps);
    }
    for token in tokenize(text) {
        assert!(!token.is_empty());
    }
    to_persian_digits(text);
    to_ascii_digits(text);
}

fn check_bytes(bytes: &[u8]) {
    let filter = &*FILTER;
    let lossy = String::from_utf8_lossy(bytes);

    assert_eq!(
        filter.contains_profanity_bytes(bytes),
        filter.contains_profanity(&lossy)
    );

    let first = filter.find_match_bytes(bytes);
    let first_string = filter.find_match(&lossy);
    assert_eq!(
        first.map(|m| (m.word, m.evasion)),
        first_string.map(|m| (m.word, m.evasion))
    );

    let all = filter.find_matches_bytes(bytes);
    let all_string = filter.find_matches(&lossy);
    assert_eq!(all.len(), all_string.len());
    for (found, expected) in all.iter().zip(&all_string) {
        assert_eq!((found.word, found.evasion), (expected.word, expected.evasion));
        assert!(
            found.len >= 1 && found.start + found.len <= bytes.len(),
            "region outside the input"
        );
    }

    let censored = filter.censor_bytes(bytes);
    assert!(!filter.contains_profanity_bytes(&censored));
    if all.is_empty() {
        assert_eq!(censored, bytes, "clean bytes must come back unchanged");
    }
    assert!(filter.censor_bytes_with(bytes, '■').is_ok());
}

proptest! {
    #![proptest_config(config())]

    #[test]
    fn no_text_makes_a_function_panic(text in text()) {
        check_text(&text);
    }

    #[test]
    fn no_bytes_make_a_byte_version_panic(bytes in bytes()) {
        check_bytes(&bytes);
    }
}

#[test]
fn a_very_long_message_returns() {
    let text = "سلام hello kir. ".repeat(9000);
    check_text(&text);
    check_bytes(text.as_bytes());
}
