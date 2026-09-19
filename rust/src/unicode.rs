//! Unicode primitives with the semantics of the .NET APIs the matcher was written against.
//!
//! Every Unicode-dependent operation in the crate goes through this module (`clippy.toml` bans the
//! standard library's versions everywhere else). Port of `js/src/unicode.ts`. The answers follow .NET 10
//! on every code point (spec 005, research R1):
//!
//! - categories and lower-casing come from `tables.rs`, generated from .NET itself, so they do not
//!   depend on the Unicode version of the compiler that builds the crate;
//! - whitespace is .NET's `char.IsWhiteSpace` set (U+0085 is whitespace, U+FEFF is not);
//! - lower-casing is one UTF-16 unit at a time, and leaves a unit alone when .NET does (U+0130 stays
//!   U+0130), so there is no final-sigma or length change;
//! - categories are per UTF-16 unit where .NET tests a `char`, and per code point where .NET uses
//!   `GetUnicodeCategory(string, index)`;
//! - NFKC runs between Unicode noncharacters, which pass through unchanged (003 research R2). NFKC and
//!   NFD come from `unicode-normalization`, which differs from .NET 10 only on characters added in
//!   Unicode 16 and 17 (research R1).
//!
//! Text is the crate's UTF-16 view (`utf16.rs`): `text[i]` is unit *i*, as `charCodeAt(i)` is in
//! TypeScript.

// The one place the standard library's Unicode methods and unicode-normalization are allowed.
#![allow(clippy::disallowed_methods)]

use std::sync::LazyLock;

use unicode_normalization::{IsNormalized, UnicodeNormalization, is_nfkc_quick};

use crate::tables::{ASCII_CATEGORIES, CATEGORY_RANGES, LOWER};

/// A Unicode general category, as its two-letter code (`Cn` when unassigned).
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
#[repr(u8)]
pub(crate) enum Category {
    Lu,
    Ll,
    Lt,
    Lm,
    Lo,
    Mn,
    Mc,
    Me,
    Nd,
    Nl,
    No,
    Zs,
    Zl,
    Zp,
    Cc,
    Cf,
    Cs,
    Co,
    Cn,
    Pc,
    Pd,
    Ps,
    Pe,
    Pi,
    Pf,
    Po,
    Sm,
    Sc,
    Sk,
    So,
}

/// The category of every BMP unit, expanded from the ranges on first use (64 KB), so the per-unit tests
/// of the matcher are one lookup, as the TypeScript's cache makes them.
static BMP_CATEGORIES: LazyLock<Box<[Category]>> = LazyLock::new(|| {
    let mut table = vec![Category::Cn; 0x10000];
    for (index, &(start, category)) in CATEGORY_RANGES.iter().enumerate() {
        if start > 0xFFFF {
            break;
        }

        let end = CATEGORY_RANGES
            .get(index + 1)
            .map_or(0x10000, |&(next, _)| next.min(0x10000));
        for slot in &mut table[start as usize..end as usize] {
            *slot = category;
        }
    }

    table.into_boxed_slice()
});

/// The category of a code point, from the generated ranges.
fn category_of_code_point(code_point: u32) -> Category {
    if code_point <= 0xFFFF {
        return category_of_unit(code_point as u16);
    }

    let after = CATEGORY_RANGES.partition_point(|&(start, _)| start <= code_point);
    CATEGORY_RANGES
        .get(after.wrapping_sub(1))
        .map_or(Category::Cn, |&(_, category)| category)
}

/// The general category of one UTF-16 unit, like .NET `CharUnicodeInfo.GetUnicodeCategory(char)`. A
/// lone surrogate unit is `Cs`.
pub(crate) fn category_of_unit(unit: u16) -> Category {
    if unit < 0x80 {
        return ASCII_CATEGORIES[unit as usize];
    }

    BMP_CATEGORIES[unit as usize]
}

/// The general category of the code point at `index`, like .NET
/// `CharUnicodeInfo.GetUnicodeCategory(string, int)`: a valid surrogate pair is read as one code point.
pub(crate) fn category_at(text: &[u16], index: usize) -> Category {
    let unit = text[index];
    if is_high_surrogate(unit) {
        if let Some(&low) = text.get(index + 1) {
            if is_low_surrogate(low) {
                return category_of_code_point(code_point_of(unit, low));
            }
        }
    }

    category_of_unit(unit)
}

/// Whether a unit is a letter (L*), like .NET `char.IsLetter`.
pub(crate) fn is_letter(unit: u16) -> bool {
    if unit < 0x80 {
        return (unit as u8).is_ascii_alphabetic();
    }

    matches!(
        category_of_unit(unit),
        Category::Lu | Category::Ll | Category::Lt | Category::Lm | Category::Lo
    )
}

/// Whether a unit is a letter (L*) or a decimal digit (Nd), like .NET `char.IsLetterOrDigit`.
pub(crate) fn is_letter_or_digit(unit: u16) -> bool {
    if unit < 0x80 {
        return (unit as u8).is_ascii_alphanumeric();
    }

    is_letter(unit) || category_of_unit(unit) == Category::Nd
}

/// Whether a unit is a control character (Cc), like .NET `char.IsControl`.
pub(crate) fn is_control(unit: u16) -> bool {
    unit <= 0x1F || (0x7F..=0x9F).contains(&unit)
}

/// Whether a unit is whitespace, like .NET `char.IsWhiteSpace`: categories Zs, Zl and Zp, plus
/// U+0009–U+000D, U+0085 and U+00A0. U+0085 is whitespace and U+FEFF is not.
pub(crate) fn is_white_space(unit: u16) -> bool {
    matches!(
        unit,
        0x09..=0x0D | 0x20 | 0x85 | 0xA0 | 0x1680 | 0x2000..=0x200A | 0x2028 | 0x2029 | 0x202F | 0x205F | 0x3000
    )
}

/// Whether a unit is a high or low surrogate, like .NET `char.IsSurrogate`.
pub(crate) fn is_surrogate(unit: u16) -> bool {
    (0xD800..=0xDFFF).contains(&unit)
}

/// Whether a unit is a high (leading) surrogate, like .NET `char.IsHighSurrogate`.
pub(crate) fn is_high_surrogate(unit: u16) -> bool {
    (0xD800..=0xDBFF).contains(&unit)
}

/// Whether a unit is a low (trailing) surrogate, like .NET `char.IsLowSurrogate`.
pub(crate) fn is_low_surrogate(unit: u16) -> bool {
    (0xDC00..=0xDFFF).contains(&unit)
}

/// The code point of a surrogate pair, like .NET `char.ConvertToUtf32(high, low)`. Callers pass a high
/// and a low surrogate; other units give a value that is never an enclosed letter, and never overflow.
pub(crate) fn code_point_of(high: u16, low: u16) -> u32 {
    0x10000
        + ((u32::from(high).wrapping_sub(0xD800) & 0x3FF) << 10)
        + (u32::from(low).wrapping_sub(0xDC00) & 0x3FF)
}

/// Lower-cases one UTF-16 unit, like .NET `char.ToLowerInvariant`.
pub(crate) fn to_lower_invariant(unit: u16) -> u16 {
    if unit < 0x80 {
        return u16::from((unit as u8).to_ascii_lowercase());
    }

    match LOWER.binary_search_by_key(&unit, |&(upper, _)| upper) {
        Ok(index) => LOWER[index].1,
        Err(_) => unit,
    }
}

/// Whether a code point is a Unicode noncharacter: U+FDD0–U+FDEF, or one ending in FFFE or FFFF.
pub(crate) fn is_noncharacter(code_point: u32) -> bool {
    (0xFDD0..=0xFDEF).contains(&code_point) || (code_point & 0xFFFE) == 0xFFFE
}

/// The length (0, 1 or 2 units) of a noncharacter starting at `index`, or 0 when there is none.
pub(crate) fn noncharacter_length_at(text: &[u16], index: usize) -> usize {
    let unit = text[index];
    if is_high_surrogate(unit) {
        if let Some(&low) = text.get(index + 1) {
            if is_low_surrogate(low) {
                return if is_noncharacter(code_point_of(unit, low)) {
                    2
                } else {
                    0
                };
            }
        }
    }

    usize::from(is_noncharacter(u32::from(unit)))
}

/// Appends the UTF-16 encoding of `text` to `out`.
fn push_encoded(out: &mut Vec<u16>, text: impl Iterator<Item = char>) {
    let mut buffer = [0u16; 2];
    for c in text {
        out.extend_from_slice(c.encode_utf16(&mut buffer));
    }
}

/// Normalizes `text` with `form` between noncharacters and lone surrogates, which are copied unchanged.
fn normalize_between_noncharacters(text: &[u16], form: fn(&str) -> String) -> Vec<u16> {
    let mut out = Vec::with_capacity(text.len());
    let mut run = String::new();

    for decoded in char::decode_utf16(text.iter().copied()) {
        match decoded {
            Ok(c) if !is_noncharacter(u32::from(c)) => run.push(c),
            Ok(c) => {
                push_encoded(&mut out, form(&run).chars());
                run.clear();
                push_encoded(&mut out, std::iter::once(c));
            }
            // A lone surrogate never reaches here (the normalizer replaces them first, as in .NET), but
            // if one does it is copied through rather than panicking.
            Err(error) => {
                push_encoded(&mut out, form(&run).chars());
                run.clear();
                out.push(error.unpaired_surrogate());
            }
        }
    }

    push_encoded(&mut out, form(&run).chars());
    out
}

fn nfkc_string(text: &str) -> String {
    text.nfkc().collect()
}

fn nfd_string(text: &str) -> String {
    text.nfd().collect()
}

/// Whether the quick check proves `text` is already in NFKC. Noncharacters are NFKC starters that
/// never change, so this also holds for the segment-wise NFKC of [`nfkc`].
fn is_nfkc_quickly(text: &[u16]) -> bool {
    if text.iter().all(|&unit| unit < 0x80) {
        return true;
    }

    let mut valid = true;
    let chars = char::decode_utf16(text.iter().copied()).map_while(|decoded| {
        valid = decoded.is_ok();
        decoded.ok()
    });
    let quick = is_nfkc_quick(chars);
    valid && quick == IsNormalized::Yes
}

/// NFKC, like .NET `string.Normalize(NormalizationForm.FormKC)` as PersianTextGuard applies it: text
/// between noncharacters is normalized and each noncharacter is copied unchanged. A noncharacter
/// composes with nothing, so the result equals whole-string NFKC. Lone surrogates must already have been
/// replaced by the caller.
pub(crate) fn nfkc(text: &[u16]) -> Vec<u16> {
    if is_nfkc_quickly(text) {
        return text.to_vec();
    }

    normalize_between_noncharacters(text, nfkc_string)
}

/// Whether `text` is already in NFKC, like .NET `string.IsNormalized(NormalizationForm.FormKC)`.
pub(crate) fn is_nfkc(text: &[u16]) -> bool {
    is_nfkc_quickly(text) || nfkc(text) == text
}

/// NFD, like .NET `string.Normalize(NormalizationForm.FormD)`.
pub(crate) fn nfd(text: &[u16]) -> Vec<u16> {
    normalize_between_noncharacters(text, nfd_string)
}

#[cfg(test)]
mod tests {
    use super::*;

    fn view(text: &str) -> Vec<u16> {
        text.encode_utf16().collect()
    }

    fn units(code_points: &[u32]) -> Vec<u16> {
        let mut out = Vec::new();
        for &code_point in code_points {
            match char::from_u32(code_point) {
                Some(c) => push_encoded(&mut out, std::iter::once(c)),
                None => out.push(code_point as u16),
            }
        }
        out
    }

    #[test]
    fn whitespace_follows_dot_net() {
        assert!(is_white_space(0x85));
        assert!(is_white_space(0xA0));
        assert!(!is_white_space(0x1C));
        assert!(!is_white_space(0xFEFF));
        assert!(!is_white_space(0x200B));
    }

    #[test]
    fn whitespace_set_equals_the_separator_categories_plus_controls() {
        for unit in 0..=0xFFFFu16 {
            let expected = matches!(category_of_unit(unit), Category::Zs | Category::Zl | Category::Zp)
                || (0x09..=0x0D).contains(&unit)
                || unit == 0x85;
            assert_eq!(is_white_space(unit), expected, "U+{unit:04X}");
        }
    }

    #[test]
    fn lower_casing_follows_dot_net_10() {
        assert_eq!(to_lower_invariant(u16::from(b'A')), u16::from(b'a'));
        assert_eq!(to_lower_invariant(0x130), 0x130);
        assert_eq!(to_lower_invariant(0x3A3), 0x3C3);
        assert_eq!(to_lower_invariant(0x410), 0x430);
        // Case pairs new in Unicode 16 and 17 stay unchanged, as in .NET 10.
        for unit in [0x1C89, 0xA7CB, 0xA7CC, 0xA7CE, 0xA7D2, 0xA7D4, 0xA7DA, 0xA7DC] {
            assert_eq!(to_lower_invariant(unit), unit, "U+{unit:04X}");
        }
    }

    #[test]
    fn categories_follow_dot_net_10() {
        assert_eq!(category_of_unit(0xD83D), Category::Cs);
        assert_eq!(category_of_unit(0x0378), Category::Cn);
        assert_eq!(category_of_unit(0x0627), Category::Lo);
        assert_eq!(category_of_unit(0x200C), Category::Cf);
        assert_eq!(category_of_unit(u16::from(b'!')), Category::Po);

        let pair = view("\u{20000}");
        assert_eq!(category_at(&pair, 0), Category::Lo);
        assert_eq!(category_at(&pair, 1), Category::Cs);
        assert_eq!(category_at(&units(&[0xD83D]), 0), Category::Cs);
        assert_eq!(category_at(&view("\u{1171E}"), 0), Category::Mc);
        assert_eq!(category_at(&view("\u{1F600}"), 0), Category::So);
        assert_eq!(category_of_code_point(0x10FFFF), Category::Cn);
        assert_eq!(category_of_code_point(0xF0000), Category::Co);
    }

    #[test]
    fn letters_and_digits() {
        assert!(is_letter(u16::from(b'k')));
        assert!(is_letter(0x06A9));
        assert!(!is_letter(u16::from(b'1')));
        assert!(is_letter_or_digit(0x06F1));
        assert!(!is_letter_or_digit(0x200C));
        assert!(is_control(0x85));
        assert!(!is_control(0xA0));
    }

    #[test]
    fn nfkc_folds_compatibility_forms() {
        assert_eq!(nfkc(&view("\u{FEDB}")), view("\u{0643}"));
        assert_eq!(nfkc(&view("\u{1D424}")), view("k"));
        assert_eq!(nfkc(&view("\u{FF21}BC")), view("ABC"));
        assert!(is_nfkc(&view("kir")));
        assert!(is_nfkc(&view("سلام")));
        assert!(!is_nfkc(&view("\u{FEDB}")));
    }

    #[test]
    fn nfkc_keeps_every_noncharacter_and_normalizes_around_it() {
        let mut noncharacters: Vec<u32> = (0xFDD0..=0xFDEF).collect();
        for plane in 0..=0x10u32 {
            noncharacters.push((plane << 16) | 0xFFFE);
            noncharacters.push((plane << 16) | 0xFFFF);
        }
        assert_eq!(noncharacters.len(), 66);

        for code_point in noncharacters {
            let input = units(&[0xFEDB, code_point, 0x1D424]);
            let expected = units(&[0x0643, code_point, u32::from(b'k')]);
            assert_eq!(nfkc(&input), expected, "U+{code_point:04X}");
        }
    }

    #[test]
    fn nfkc_copies_a_lone_surrogate() {
        assert_eq!(nfkc(&units(&[0xFEDB, 0xD83D])), units(&[0x0643, 0xD83D]));
    }

    #[test]
    fn nfd_decomposes() {
        assert_eq!(nfd(&view("ü")), view("u\u{0308}"));
    }

    #[test]
    fn noncharacter_lengths() {
        assert_eq!(noncharacter_length_at(&units(&[0xFFFE]), 0), 1);
        assert_eq!(noncharacter_length_at(&units(&[0xFDD0]), 0), 1);
        assert_eq!(noncharacter_length_at(&units(&[0x1FFFE]), 0), 2);
        assert_eq!(noncharacter_length_at(&units(&[0x10FFFF]), 0), 2);
        assert_eq!(noncharacter_length_at(&view("a"), 0), 0);
        assert_eq!(noncharacter_length_at(&view("\u{1F600}"), 0), 0);
    }
}
