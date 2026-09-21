//! The public API as users reach it: spec 005 US1 scenarios 1–7 and guarantees G3–G7 and G11 of
//! contracts/public-api.md.

#![allow(clippy::unwrap_used, clippy::expect_used)]

use std::collections::HashSet;

use persian_text_guard::*;

fn default_filter() -> ProfanityFilter {
    ProfanityFilter::with_defaults(WordList::persian_default())
}

/// G3 and G4 for one message.
pub fn assert_consistent(filter: &ProfanityFilter, text: &str) {
    let first = filter.find_match(text);
    let all = filter.find_matches(text);
    let contains = filter.contains_profanity(text);
    assert_eq!(contains, first.is_some(), "contains vs find_match on {text:?}");
    assert_eq!(contains, !all.is_empty(), "contains vs find_matches on {text:?}");
    assert_eq!(
        filter.censor(text) != text,
        contains,
        "censor vs contains on {text:?}"
    );

    let mut previous_end = 0;
    for found in &all {
        assert!(found.len >= 1, "{text:?}: empty match");
        assert!(
            found.start >= previous_end,
            "{text:?}: matches overlap or are out of order"
        );
        assert!(
            found.start + found.len <= text.len(),
            "{text:?}: match past the end"
        );
        assert!(text.is_char_boundary(found.start) && text.is_char_boundary(found.start + found.len));
        assert!(text.get(found.range()).is_some());
        previous_end = found.start + found.len;
    }
}

#[test]
fn scenario_1_spaced_letters_are_flagged() {
    assert!(default_filter().contains_profanity("f u c k"));
}

#[test]
fn scenario_2_ordinary_messages_pass_untouched() {
    let filter = default_filter();
    for text in ["هر کس پلات بالاست پیام بده", "سلام، سفارشم کی میرسه؟"]
    {
        assert!(!filter.contains_profanity(text));
        assert!(filter.find_matches(text).is_empty());
        assert_eq!(filter.censor(text), text);
    }
}

#[test]
fn independent_test_of_user_story_1() {
    let filter = default_filter();
    assert!(filter.contains_profanity("ک.ی.ر"));
    assert!(!filter.contains_profanity("سلام، سفارشم کی میرسه؟"));
    assert_eq!(filter.censor("kir and motherfucker"), "**** and ****");
}

#[test]
fn scenario_3_every_match_in_order_with_byte_positions() {
    let filter = default_filter();
    let matches = filter.find_matches("sh1t and f u c k");
    assert_eq!(matches.len(), 2);

    assert_eq!(matches[0].word.text, "shit");
    assert_eq!(matches[0].word.category, WordCategory::Profanity);
    assert_eq!((matches[0].start, matches[0].len), (0, 4));
    assert_eq!(
        matches[0].evasion.iter().collect::<Vec<_>>(),
        [EvasionKind::LookalikeCharacters]
    );

    assert_eq!(matches[1].word.text, "fuck");
    assert_eq!(matches[1].word.category, WordCategory::Profanity);
    assert_eq!((matches[1].start, matches[1].len), (9, 7));
    assert_eq!(
        matches[1].evasion.iter().collect::<Vec<_>>(),
        [EvasionKind::SplitWord]
    );
}

#[test]
fn scenario_4_positions_are_bytes_after_an_emoji() {
    let text = "😀 کیر";
    let filter = default_filter();
    let found = filter.find_match(text).unwrap();
    assert_eq!((found.start, found.len), (5, 6));
    assert_eq!(&text[found.range()], "کیر");
}

#[test]
fn scenario_5_own_entries() {
    let words = [
        BannedWord::new("اسپم"),
        BannedWord::new("casino").with_mode(WordMatchMode::Anywhere),
    ];
    let filter = ProfanityFilter::with_defaults(words.iter());
    assert!(filter.contains_profanity("onlinecasino.example"));
    assert!(filter.contains_profanity("این اسپم است"));
    assert_eq!(filter.count(), 2);
}

#[test]
fn scenario_6_masks() {
    let filter = default_filter();
    assert_eq!(filter.censor_with("this is kir", '#').unwrap(), "this is ####");
    assert_eq!(
        filter.censor_with("this is kir", 'x'),
        Err(InvalidMask { mask: 'x' })
    );
}

#[test]
fn g5_masks_are_checked_before_the_text() {
    let filter = default_filter();
    for mask in ['x', '5', ' ', '\n', '\u{1F600}', '\u{0627}', '\u{06F5}', '\u{85}'] {
        assert_eq!(
            filter.censor_with("", mask),
            Err(InvalidMask { mask }),
            "{mask:?}"
        );
        assert_eq!(
            filter.censor_with("kir", mask),
            Err(InvalidMask { mask }),
            "{mask:?}"
        );
    }

    for mask in ['#', '*', '■', '•'] {
        assert_eq!(
            filter.censor_with("kir", mask).unwrap(),
            mask.to_string().repeat(4),
            "{mask:?}"
        );
        assert_eq!(filter.censor_with("", mask).unwrap(), "");
    }

    assert!(
        char::from_u32(0xD83D).is_none(),
        "a Rust mask cannot be a lone surrogate"
    );
}

#[test]
fn g4_positions_slice_the_callers_string() {
    let text = "kir\u{20000} and fuck";
    let filter = default_filter();
    let matches = filter.find_matches(text);
    assert_eq!(matches.len(), 2);
    assert_eq!(matches[0].range(), 0..7);
    assert_eq!(&text[matches[0].range()], "kir\u{20000}");
    assert_eq!(matches[1].range(), 12..16);
    assert_eq!(&text[matches[1].range()], "fuck");
}

fn assert_send_sync<T: Send + Sync>() {}

#[test]
fn g6_every_public_type_is_send_and_sync() {
    assert_send_sync::<ProfanityFilter>();
    assert_send_sync::<ProfanityMatch<'static>>();
    assert_send_sync::<BannedWord>();
    assert_send_sync::<ProfanityFilterOptions>();
    assert_send_sync::<WordMatchMode>();
    assert_send_sync::<WordCategory>();
    assert_send_sync::<EvasionKind>();
    assert_send_sync::<NormalizationStep>();
    assert_send_sync::<EvasionSet>();
    assert_send_sync::<Normalization>();
    assert_send_sync::<InvalidMask>();
    assert_send_sync::<ParseNameError>();
    assert_send_sync::<WordListError>();
    assert_send_sync::<WordList>();
}

#[test]
fn g7_bundled_lists_are_parsed_once() {
    assert!(std::ptr::eq(WordList::all(), WordList::all()));
    assert!(std::ptr::eq(
        WordList::persian_default(),
        WordList::persian_default()
    ));
    assert!(
        WordList::persian_default()
            .iter()
            .all(|word| word.category != WordCategory::Mild)
    );
    assert!(
        WordList::all()
            .iter()
            .any(|word| word.category == WordCategory::Mild)
    );
}

#[test]
fn g11_names_parse_and_unknown_names_list_the_valid_ones() {
    assert_eq!("slur".parse::<WordCategory>(), Ok(WordCategory::Slur));
    let error = "nope".parse::<WordCategory>().unwrap_err();
    assert_eq!(error.text(), "nope");
    let message = error.to_string();
    for &category in WordCategory::ALL {
        assert!(message.contains(category.name()), "{message}");
    }
    assert_eq!(
        "comparison".parse::<Normalization>(),
        Ok(Normalization::COMPARISON)
    );
    assert!("Comparison".parse::<Normalization>().is_err());
    assert_eq!(
        "lookalikeCharacters".parse::<EvasionKind>(),
        Ok(EvasionKind::LookalikeCharacters)
    );
    assert_eq!("anywhere".parse::<WordMatchMode>(), Ok(WordMatchMode::Anywhere));
    assert_eq!(
        "collapseRepeats".parse::<NormalizationStep>(),
        Ok(NormalizationStep::CollapseRepeats)
    );
}

#[test]
fn count_merges_spellings_that_normalize_the_same() {
    let filter = ProfanityFilter::with_defaults(["كص", "کص", "  کص ", ""].map(BannedWord::new));
    assert_eq!(filter.count(), 1);
    assert_eq!(
        ProfanityFilter::with_defaults(Vec::<BannedWord>::new()).count(),
        0
    );
    assert!(!ProfanityFilter::with_defaults(Vec::<BannedWord>::new()).contains_profanity("kir"));
}

#[test]
fn any_iterable_of_entries_works() {
    let words = vec![BannedWord::new("spam"), BannedWord::new("scam")];
    let set: HashSet<BannedWord> = words.iter().cloned().collect();
    let from_vec = ProfanityFilter::with_defaults(words.clone());
    let from_slice = ProfanityFilter::with_defaults(&words[..]);
    let from_refs = ProfanityFilter::with_defaults(words.iter());
    let from_set = ProfanityFilter::with_defaults(set);
    let from_cloned = ProfanityFilter::with_defaults(words.iter().cloned());
    let from_bundled = ProfanityFilter::with_defaults(WordList::bundled(&[WordCategory::Sexual]));
    for filter in [&from_vec, &from_slice, &from_refs, &from_set, &from_cloned] {
        assert_eq!(filter.count(), 2);
        assert!(filter.contains_profanity("this is spam"));
    }
    assert!(from_bundled.contains_profanity("kir"));
}

#[test]
fn options_turn_evasions_off() {
    let words = WordList::persian_default();
    let plain = ProfanityFilterOptions::default()
        .squeeze_repeated_letters(false)
        .fold_lookalike_characters(false)
        .join_spaced_letters(false);
    let filter = ProfanityFilter::new(words, plain);
    assert!(!filter.contains_profanity("fuuuck"));
    assert!(!filter.contains_profanity("sh1t"));
    assert!(!filter.contains_profanity("f u c k"));
    assert!(filter.contains_profanity("fuck"));
}

#[test]
fn g3_holds_on_a_handful_of_inputs() {
    let filter = default_filter();
    for text in [
        "",
        "   ",
        "hi \u{FFFD} kir",
        "k kos i kos r",
        "جنده\u{200C}ها رو ببین",
        "kir and motherfucker",
        "sh1t and f u c k",
        "\u{FFFE}kir\u{10FFFF}",
        "سلام دوست عزیز",
    ] {
        assert_consistent(&filter, text);
    }
}

#[test]
fn censoring_is_always_clean() {
    let filter = default_filter();
    let censored = filter.censor("k kos i kos r");
    assert!(!filter.contains_profanity(&censored), "{censored}");
}

#[path = "corpus/load.rs"]
mod load;
#[path = "corpus/values.rs"]
mod values;

#[test]
fn g3_and_g4_hold_on_every_corpus_input() {
    let root = load::find_repository_root().unwrap();
    let corpus = load::load_corpus(&root.join("conformance")).unwrap();
    let filters = [
        default_filter(),
        ProfanityFilter::with_defaults(WordList::all()),
        ProfanityFilter::new(
            WordList::persian_default(),
            ProfanityFilterOptions::default().fold_lookalike_characters(false),
        ),
    ];

    let mut checked = 0;
    for case in corpus
        .cases
        .iter()
        .filter(|case| load::MATCHING_KINDS.contains(&case.kind.as_str()))
    {
        // Built with the runner's rules: lone surrogates as U+FFFD, null as "".
        let text = values::build_text(case.json.get("input").unwrap_or(&serde_json::Value::Null));
        for filter in &filters {
            assert_consistent(filter, &text);
        }
        checked += 1;
    }

    assert!(checked >= 300, "only {checked} matching inputs");
}

/// Quickstart §3 (FR-018, SC-002): dumps every bundled entry as `text\tmode\tcategory` to
/// `artifacts/compare/rust.txt` and prints the counts, to diff with the Python and JavaScript dumps.
/// Run with `cargo test --test api -- --ignored`.
#[test]
#[ignore = "writes artifacts/compare/rust.txt for the cross-port comparison"]
fn dump_bundled_entries_for_comparison() {
    let root = load::find_repository_root().unwrap();
    let directory = root.join("artifacts").join("compare");
    std::fs::create_dir_all(&directory).unwrap();

    let dump: String = WordList::all()
        .iter()
        .map(|word| format!("{}\t{}\t{}\n", word.text, word.mode, word.category))
        .collect();
    std::fs::write(directory.join("rust.txt"), dump).unwrap();

    let categories: Vec<String> = WordCategory::ALL
        .iter()
        .map(|&category| format!("\"{category}\": {}", WordList::bundled(&[category]).len()))
        .collect();
    println!(
        "{{\"all\": {}, \"default\": {}, \"categories\": {{{}}}}}",
        WordList::all().len(),
        WordList::persian_default().len(),
        categories.join(", ")
    );
}
