//! Persian normalization and tokenizing. Port of `js/src/normalizer.ts`, which ports
//! `PersianNormalizer.cs` and `PersianNormalization.cs`. Everything but the public wrappers works on the
//! UTF-16 view (`utf16.rs`).

use std::borrow::Cow;

use crate::types::{Normalization, NormalizationStep};
use crate::unicode::{
    Category, category_at, is_high_surrogate, is_letter, is_letter_or_digit, is_low_surrogate, is_nfkc,
    is_white_space, nfkc, to_lower_invariant,
};
use crate::utf16::{ByteMap, from_view, to_view};

const COMPATIBILITY_FORMS: u16 = NormalizationStep::CompatibilityForms.bit();
const UNIFY_LETTERS: u16 = NormalizationStep::UnifyLetters.bit();
const REMOVE_DIACRITICS: u16 = NormalizationStep::RemoveDiacritics.bit();
const REMOVE_TATWEEL: u16 = NormalizationStep::RemoveTatweel.bit();
const REMOVE_ZERO_WIDTH: u16 = NormalizationStep::RemoveZeroWidth.bit();
const REMOVE_BIDI_CONTROLS: u16 = NormalizationStep::RemoveBidiControls.bit();
const ASCII_DIGITS: u16 = NormalizationStep::AsciiDigits.bit();
const LOWER_CASE: u16 = NormalizationStep::LowerCase.bit();
const COLLAPSE_WHITESPACE: u16 = NormalizationStep::CollapseWhitespace.bit();
const COLLAPSE_REPEATS: u16 = NormalizationStep::CollapseRepeats.bit();

/// The bits of the `COMPARISON` preset, the form the filter searches.
pub(crate) const COMPARISON_STEPS: u16 = Normalization::COMPARISON.bits();

const PERSIAN_YEH: u16 = 0x06CC;
const PERSIAN_KEHEH: u16 = 0x06A9;
const HEH: u16 = 0x0647;
const ALEF: u16 = 0x0627;

const ARABIC_INDIC_ZERO: u16 = 0x0660;
const EXTENDED_ARABIC_INDIC_ZERO: u16 = 0x06F0;

const REPLACEMENT_CHARACTER: u16 = 0xFFFD;

/// Normalizes Persian text so that strings which look the same compare the same.
///
/// Persian is written with character pairs that render near-identically but have different code points
/// (Arabic yeh and Persian yeh, Arabic kaf and keheh), plus optional diacritics, invisible zero-width and
/// bidi characters, and three digit ranges. A raw comparison against a word list or a search index is
/// defeated by typing one character differently.
///
/// Never panics, for any text. [`Normalization::COMPARISON`] is the form the filter searches;
/// [`Normalization::STANDARD`] is safe to store and display.
///
/// ```
/// use persian_text_guard::{Normalization, normalize};
///
/// let text = "كتاب\u{200C}هاي  ۱۲ ABC";
/// assert_eq!(normalize(text, Normalization::COMPARISON), "کتابهای 12 abc");
/// assert_eq!(normalize(text, Normalization::STANDARD), "کتاب\u{200C}های ۱۲ ABC");
/// assert_eq!(normalize(text, Normalization::NONE), text);
/// ```
pub fn normalize(text: &str, steps: Normalization) -> String {
    if steps == Normalization::NONE {
        return text.to_owned();
    }

    from_view(&normalize_with_map(&to_view(text), steps.bits(), None))
}

/// [`normalize`] on a view with step bits, also recording in `map`, for every unit of the result, the
/// index in `text` it came from.
pub(crate) fn normalize_with_map(text: &[u16], steps: u16, map: Option<&mut Vec<usize>>) -> Vec<u16> {
    if text.is_empty() {
        return Vec::new();
    }

    let recording = map.is_some();

    // string.Normalize throws on a lone surrogate in .NET, which is what a message cut in the middle of
    // an emoji contains; the .NET port replaces them first, and so does this one, so both ports
    // normalize the same text.
    let mut source_map: Option<Vec<usize>> = None;
    let source: Cow<'_, [u16]> = if steps & COMPATIBILITY_FORMS == 0 {
        Cow::Borrowed(text)
    } else if !recording {
        Cow::Owned(nfkc(&replace_lone_surrogates(text)))
    } else {
        let mut segment_map = Vec::with_capacity(text.len());
        let normalized = normalize_compatibility_by_segment(&replace_lone_surrogates(text), &mut segment_map);
        source_map = Some(segment_map);
        Cow::Owned(normalized)
    };

    let mut out = Vec::with_capacity(source.len());
    let mut step_map: Option<Vec<usize>> = recording.then(|| Vec::with_capacity(source.len()));
    let unify = steps & UNIFY_LETTERS != 0;
    let lower_case = steps & LOWER_CASE != 0;
    let ascii_digits = steps & ASCII_DIGITS != 0;

    for (index, &raw) in source.iter().enumerate() {
        if is_removed(raw, steps) {
            continue;
        }

        if let Some(step_map) = &mut step_map {
            step_map.push(match &source_map {
                Some(source_map) => source_map.get(index).copied().unwrap_or(index),
                None => index,
            });
        }

        let mut c = if unify { unify_letter(raw) } else { raw };

        if ascii_digits {
            if (ARABIC_INDIC_ZERO..=ARABIC_INDIC_ZERO + 9).contains(&c) {
                c = 0x30 + (c - ARABIC_INDIC_ZERO);
            } else if (EXTENDED_ARABIC_INDIC_ZERO..=EXTENDED_ARABIC_INDIC_ZERO + 9).contains(&c) {
                c = 0x30 + (c - EXTENDED_ARABIC_INDIC_ZERO);
            }
        }

        out.push(if lower_case { to_lower_invariant(c) } else { c });
    }

    if steps & COLLAPSE_REPEATS != 0 {
        out = collapse_repeats(&out, step_map.as_mut());
    }

    if steps & COLLAPSE_WHITESPACE != 0 {
        out = collapse_whitespace(&out, step_map.as_mut());
    }

    if let (Some(map), Some(step_map)) = (map, step_map) {
        map.extend(step_map);
    }

    out
}

/// NFKC one segment at a time: a code point and the combining marks that follow it.
fn normalize_compatibility_by_segment(text: &[u16], map: &mut Vec<usize>) -> Vec<u16> {
    // Most messages are already in NFKC. Then every character maps to itself.
    if is_nfkc(text) {
        map.extend(0..text.len());
        return text.to_vec();
    }

    let mut result = Vec::with_capacity(text.len());
    let mut i = 0;

    while i < text.len() {
        let start = i;
        i += code_point_length(text, i);
        while i < text.len() && is_combining_mark(text, i) {
            i += code_point_length(text, i);
        }

        // ASCII is already in normal form. A segment starting with a noncharacter is normalized around
        // it by nfkc, which is what the .NET port does to avoid string.Normalize throwing.
        let segment: Cow<'_, [u16]> = if i - start == 1 && text[start] < 128 {
            Cow::Borrowed(&text[start..i])
        } else {
            Cow::Owned(nfkc(&text[start..i]))
        };

        map.extend(std::iter::repeat_n(start, segment.len()));
        result.extend_from_slice(&segment);
    }

    result
}

fn code_point_length(text: &[u16], index: usize) -> usize {
    if is_high_surrogate(text[index]) && index + 1 < text.len() && is_low_surrogate(text[index + 1]) {
        2
    } else {
        1
    }
}

fn is_combining_mark(text: &[u16], index: usize) -> bool {
    matches!(
        category_at(text, index),
        Category::Mn | Category::Mc | Category::Me
    )
}

/// Splits text into word tokens on whitespace, punctuation (ASCII, Persian «» ، ؛ ؟ ٫, and the rest of
/// Unicode) and symbols, emoji included.
///
/// Letters, digits, combining marks and the zero-width non-joiner stay inside tokens. The tokens are
/// slices of `text`, in order. Normalize first: tokens are only as consistent as the text they came from.
/// Never panics.
///
/// ```
/// use persian_text_guard::tokenize;
///
/// assert_eq!(tokenize("سلام، دنیا! خوبی؟"), ["سلام", "دنیا", "خوبی"]);
/// assert!(tokenize("  ").is_empty());
/// ```
pub fn tokenize(text: &str) -> Vec<&str> {
    let view = to_view(text);
    let tokens = tokenize_with_offsets(&view);
    let bytes = ByteMap::new(text);
    tokens
        .into_iter()
        .filter_map(|(start, end)| {
            let (start, len) = bytes.to_bytes(start, end - start);
            text.get(start..start + len)
        })
        .collect()
}

/// [`tokenize`] on a view, keeping where each token starts and ends (exclusive), in units.
pub(crate) fn tokenize_with_offsets(text: &[u16]) -> Vec<(usize, usize)> {
    let mut tokens = Vec::new();
    let mut start: Option<usize> = None;

    for i in 0..=text.len() {
        if i < text.len() && is_word_character(text, i) {
            if start.is_none() {
                start = Some(i);
            }

            continue;
        }

        if let Some(first) = start.take() {
            tokens.push((first, i));
        }
    }

    tokens
}

/// Whether the unit at `index` belongs inside a word. A split-off word used to survive next to anything
/// the tokenizer did not know about: «کیر», کیر😂.
pub(crate) fn is_word_character(text: &[u16], index: usize) -> bool {
    let c = text[index];
    if c < 128 {
        return is_letter_or_digit(c);
    }

    if is_low_surrogate(c) && index > 0 && is_high_surrogate(text[index - 1]) {
        return is_word_character(text, index - 1);
    }

    match category_at(text, index) {
        Category::Lu
        | Category::Ll
        | Category::Lt
        | Category::Lm
        | Category::Lo
        | Category::Mn
        | Category::Mc
        | Category::Me
        | Category::Nd
        | Category::Nl
        | Category::No => true,

        // Zero-width non-joiner and friends are part of Persian spelling, but a variation selector or
        // zero-width joiner after an emoji is not.
        Category::Cf => (c == 0x200C || c == 0x200D) && index > 0 && is_letter(text[index - 1]),

        _ => false,
    }
}

/// Renders ASCII digits as Persian digits (۰-۹), leaving everything else alone.
///
/// ```
/// use persian_text_guard::to_persian_digits;
///
/// assert_eq!(to_persian_digits("2 ساعت پیش"), "۲ ساعت پیش");
/// ```
pub fn to_persian_digits(text: &str) -> String {
    text.chars()
        .map(|c| match c {
            '0'..='9' => char::from_u32(0x06F0 + (c as u32 - 0x30)).unwrap_or(c),
            _ => c,
        })
        .collect()
}

/// Converts Persian and Arabic-Indic digits to ASCII, leaving everything else alone.
///
/// ```
/// use persian_text_guard::to_ascii_digits;
///
/// assert_eq!(to_ascii_digits("۱۴۰۴/۰۵/۱۴"), "1404/05/14");
/// ```
pub fn to_ascii_digits(text: &str) -> String {
    text.chars()
        .map(|c| match c as u32 {
            0x0660..=0x0669 => char::from(b'0' + (c as u32 - 0x0660) as u8),
            0x06F0..=0x06F9 => char::from(b'0' + (c as u32 - 0x06F0) as u8),
            _ => c,
        })
        .collect()
}

/// `text` with every lone surrogate replaced by U+FFFD, borrowed when it has none.
fn replace_lone_surrogates(text: &[u16]) -> Cow<'_, [u16]> {
    let mut result: Option<Vec<u16>> = None;

    for (i, &c) in text.iter().enumerate() {
        let lone = if is_high_surrogate(c) {
            i + 1 >= text.len() || !is_low_surrogate(text[i + 1])
        } else {
            is_low_surrogate(c) && (i == 0 || !is_high_surrogate(text[i - 1]))
        };

        if lone {
            result
                .get_or_insert_with(|| text[..i].to_vec())
                .push(REPLACEMENT_CHARACTER);
        } else if let Some(result) = &mut result {
            result.push(c);
        }
    }

    match result {
        Some(replaced) => Cow::Owned(replaced),
        None => Cow::Borrowed(text),
    }
}

fn is_removed(c: u16, steps: u16) -> bool {
    if steps & REMOVE_DIACRITICS != 0 && (0x064B..=0x0652).contains(&c) {
        return true;
    }

    if steps & REMOVE_TATWEEL != 0 && c == 0x0640 {
        return true;
    }

    if steps & REMOVE_ZERO_WIDTH != 0 && matches!(c, 0x200B | 0x200C | 0x200D | 0x2060 | 0xFEFF | 0x00AD) {
        return true;
    }

    steps & REMOVE_BIDI_CONTROLS != 0
        && matches!(c, 0x200E | 0x200F | 0x061C | 0x202A..=0x202E | 0x2066..=0x2069)
}

fn unify_letter(c: u16) -> u16 {
    match c {
        0x064A | 0x0649 => PERSIAN_YEH, // ي Arabic yeh, ى alef maksura
        0x0643 => PERSIAN_KEHEH,        // ك Arabic kaf
        0x0623 | 0x0625 | 0x0622 => ALEF,
        0x0629 => HEH, // ة teh marbuta

        // Letters from the Urdu, Kurdish and Pashto blocks that render as the Persian ones in most fonts.
        // NFKC leaves them alone because they are distinct letters, not compatibility forms, so a swash
        // kaf gets past an entry written with a normal one.
        0x06AA | 0x06AB => PERSIAN_KEHEH,
        0x06BE | 0x06C0 | 0x06C1 | 0x06C3 | 0x06D5 => HEH,
        0x06CD | 0x06CE | 0x06D0 | 0x06D2 | 0x06D3 => PERSIAN_YEH,
        0x0671 | 0x0672 | 0x0673 | 0x0675 => ALEF,
        _ => c,
    }
}

fn collapse_repeats(value: &[u16], map: Option<&mut Vec<usize>>) -> Vec<u16> {
    let mut result = Vec::with_capacity(value.len());
    let mut kept: Option<Vec<usize>> = map.as_ref().map(|_| Vec::with_capacity(value.len()));
    let mut run_char = 0u16;
    let mut run_length = 0usize;

    for (i, &c) in value.iter().enumerate() {
        run_length = if c == run_char { run_length + 1 } else { 1 };
        run_char = c;

        if run_length <= 2 || is_white_space(c) {
            result.push(c);
            if let (Some(kept), Some(map)) = (&mut kept, &map) {
                kept.push(map.get(i).copied().unwrap_or(0));
            }
        }
    }

    replace_map(map, kept);
    result
}

fn collapse_whitespace(value: &[u16], map: Option<&mut Vec<usize>>) -> Vec<u16> {
    let mut result = Vec::with_capacity(value.len());
    let mut kept: Option<Vec<usize>> = map.as_ref().map(|_| Vec::with_capacity(value.len()));
    let mut pending_space = false;
    let mut pending_source = 0usize;

    for (i, &c) in value.iter().enumerate() {
        let source = map.as_ref().and_then(|map| map.get(i).copied()).unwrap_or(0);
        if is_white_space(c) {
            pending_space = !result.is_empty();
            pending_source = source;
            continue;
        }

        if pending_space {
            result.push(0x20);
            if let Some(kept) = &mut kept {
                kept.push(pending_source);
            }
            pending_space = false;
        }

        result.push(c);
        if let Some(kept) = &mut kept {
            kept.push(source);
        }
    }

    replace_map(map, kept);
    result
}

fn replace_map(map: Option<&mut Vec<usize>>, kept: Option<Vec<usize>>) {
    if let (Some(map), Some(kept)) = (map, kept) {
        *map = kept;
    }
}

#[cfg(test)]
pub(crate) mod tests {
    use super::*;
    use crate::fold::{fold, squeeze};
    use crate::source_map::{MapCache, ReadingKind, build};

    /// Builds a view from code points, keeping lone surrogates.
    pub(crate) fn cp(codes: &[u32]) -> Vec<u16> {
        let mut out = Vec::new();
        for &code in codes {
            match char::from_u32(code) {
                Some(c) => {
                    let mut buffer = [0u16; 2];
                    out.extend_from_slice(c.encode_utf16(&mut buffer));
                }
                None => out.push(code as u16),
            }
        }
        out
    }

    pub(crate) fn v(text: &str) -> Vec<u16> {
        to_view(text)
    }

    fn samples() -> Vec<Vec<u16>> {
        let mut first = cp(&[
            0x643, 0x62A, 0x627, 0x628, 0x200C, 0x647, 0x627, 0x64A, 0x20, 0x20, 0x6F1, 0x6F2, 0x20,
        ]);
        first.extend(v("ABC"));
        let mut fuck = v("f");
        fuck.extend(cp(&[0xFA]));
        fuck.extend(v("ck"));
        let mut finger = v("f");
        finger.extend(cp(&[0x1F595]));
        finger.extend(v("ck"));
        let mut lone = v("hi ");
        lone.push(0xD83D);
        vec![
            first,
            cp(&[0xFEDB, 0xFEF4, 0xFEAE]),
            fuck,
            cp(&[u32::from(b'u'), 0x301]),
            cp(&[0x6A9, 0x640, 0x640, 0x6CC, 0x640, 0x640, 0x631]),
            cp(&[0x6A9, 0x650, 0x6CC, 0x631]),
            cp(&[0x633, 0x633, 0x633, 0x633, 0x644, 0x627, 0x645]),
            v("sh!t 455"),
            cp(&[0x1F175, 0x1F184, 0x1F172, 0x1F17A]),
            finger,
            lone,
            v("   a \t b  "),
            cp(&[0x6A9, 0x200B, 0x6CC, 0x631]),
            cp(&[0x62C, 0x200F, 0x646, 0x62F, 0x647]),
        ]
    }

    pub(crate) fn assert_valid_map(map: &[usize], output_length: usize, source_length: usize) {
        assert_eq!(map.len(), output_length);
        for (i, &source) in map.iter().enumerate() {
            assert!(
                source < source_length.max(1),
                "map[{i}] = {source} is outside the source"
            );
            if i > 0 {
                assert!(source >= map[i - 1], "map goes backwards at {i}");
            }
        }
    }

    fn normalized(text: &[u16]) -> Vec<u16> {
        normalize_with_map(text, COMPARISON_STEPS, None)
    }

    fn expected_reading(text: &[u16], kind: ReadingKind) -> Vec<u16> {
        let normalized = normalized(text);
        match kind {
            ReadingKind::Normalized => normalized,
            ReadingKind::Squeezed => squeeze(&normalized, None),
            ReadingKind::Folded => fold(&normalized, None),
            ReadingKind::FoldedSqueezed => squeeze(&fold(&normalized, None), None),
        }
    }

    #[test]
    fn mapped_normalization_produces_the_same_text() {
        for text in samples() {
            let mut map = Vec::new();
            let mapped = normalize_with_map(&text, COMPARISON_STEPS, Some(&mut map));
            assert_eq!(mapped, normalized(&text), "{text:X?}");
            assert_valid_map(&map, mapped.len(), text.len());
        }
    }

    #[test]
    fn mapped_fold_and_squeeze_produce_the_same_text() {
        for text in samples() {
            let normalized = normalized(&text);

            let mut fold_map = Vec::new();
            let folded = fold(&normalized, Some(&mut fold_map));
            assert_eq!(folded, fold(&normalized, None));
            assert_valid_map(&fold_map, folded.len(), normalized.len());

            let mut squeeze_map = Vec::new();
            let squeezed = squeeze(&normalized, Some(&mut squeeze_map));
            assert_eq!(squeezed, squeeze(&normalized, None));
            assert_valid_map(&squeeze_map, squeezed.len(), normalized.len());
        }
    }

    #[test]
    fn every_reading_maps_back_into_the_original() {
        for text in samples() {
            let mut cache = MapCache::default();
            for kind in [
                ReadingKind::Normalized,
                ReadingKind::Squeezed,
                ReadingKind::Folded,
                ReadingKind::FoldedSqueezed,
            ] {
                let mapped = build(&text, kind, &mut cache);
                assert_eq!(mapped.text, expected_reading(&text, kind), "{text:X?} {kind:?}");
                assert_valid_map(mapped.start_map(), mapped.text.len(), text.len());
                assert_valid_map(mapped.end_map(), mapped.text.len(), text.len());
            }
        }
    }

    #[test]
    fn presentation_forms_map_one_to_one() {
        let mut map = Vec::new();
        assert_eq!(
            normalize_with_map(&cp(&[0xFEDB, 0xFEF4, 0xFEAE]), COMPARISON_STEPS, Some(&mut map)),
            v("کیر")
        );
        assert_eq!(map, [0, 1, 2]);
    }

    #[test]
    fn removed_invisible_characters_are_skipped_in_the_map() {
        let mut map = Vec::new();
        assert_eq!(
            normalize_with_map(
                &cp(&[0x6A9, 0x200B, 0x6CC, 0x631]),
                COMPARISON_STEPS,
                Some(&mut map)
            ),
            v("کیر")
        );
        assert_eq!(map, [0, 2, 3]);
    }

    #[test]
    fn collapsed_repeats_map_to_the_letters_they_kept() {
        let text = cp(&[0x633, 0x633, 0x633, 0x633, 0x644, 0x627, 0x645]);
        let mut map = Vec::new();
        let normalized = normalize_with_map(&text, COMPARISON_STEPS, Some(&mut map));
        for (i, &unit) in normalized.iter().enumerate() {
            assert_eq!(unit, text[map[i]]);
        }
    }

    #[test]
    fn a_noncharacter_keeps_its_place_in_the_map() {
        let text = cp(&[0xFEDB, 0xFFFE, 0xFEAE]);
        let mut map = Vec::new();
        let mapped = normalize_with_map(&text, COMPARISON_STEPS, Some(&mut map));
        assert_eq!(mapped, normalized(&text));
        assert_valid_map(&map, mapped.len(), text.len());
    }

    #[test]
    fn empty_text_becomes_empty() {
        assert_eq!(normalize("", Normalization::COMPARISON), "");
        assert_eq!(to_persian_digits(""), "");
        assert_eq!(to_ascii_digits(""), "");
        assert!(tokenize("").is_empty());
    }

    #[test]
    fn digit_helpers_leave_letters_alone() {
        assert_eq!(to_persian_digits("2 ساعت, KR1"), "۲ ساعت, KR۱");
        assert_eq!(to_ascii_digits("۲ KR١"), "2 KR1");
    }

    #[test]
    fn step_names_resolve_to_the_dot_net_flag_bits() {
        let steps: Normalization = ["unifyLetters"]
            .iter()
            .map(|name| name.parse::<NormalizationStep>())
            .collect::<Result<_, _>>()
            .unwrap();
        assert_eq!(steps.bits(), UNIFY_LETTERS);
        assert_eq!(
            (NormalizationStep::UnifyLetters | NormalizationStep::LowerCase).bits(),
            UNIFY_LETTERS | LOWER_CASE
        );
        assert_eq!(
            "comparison".parse::<Normalization>().map(Normalization::bits),
            Ok(1023)
        );
        assert_eq!(
            "standard".parse::<Normalization>().map(Normalization::bits),
            Ok(299)
        );
        assert_eq!("none".parse::<Normalization>().map(Normalization::bits), Ok(0));
    }

    #[test]
    fn unknown_presets_and_steps_are_errors() {
        assert!("bogus".parse::<Normalization>().is_err());
        assert!("shout".parse::<NormalizationStep>().is_err());
    }

    #[test]
    fn a_very_long_message_normalizes() {
        let text = v(&"سلام hello. ".repeat(20000));
        let mut map = Vec::new();
        let normalized_text = normalize_with_map(&text, COMPARISON_STEPS, Some(&mut map));
        assert_eq!(normalized_text, normalized(&text));
        assert_eq!(map.len(), normalized_text.len());
    }

    #[test]
    fn tokens_are_slices_of_the_text() {
        let text = "\u{1F600} کیر kir\u{20000}";
        assert_eq!(tokenize(text), ["کیر", "kir\u{20000}"]);
    }
}
