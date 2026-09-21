//! The character-level readings: fold, squeeze and the character classes they use. Port of
//! `js/src/fold.ts`, which ports `Fold`, `FoldCharacter`, `IsFiller`, `IsRunBoundary`, `EnclosedLetter`,
//! `BuildLatinBaseLetters` and `Squeeze` in `ProfanityFilter.Scan.cs`. The source maps and the scanner
//! both use them. Everything works on the UTF-16 view.

use std::sync::LazyLock;

use crate::unicode::{
    Category, category_of_unit, is_high_surrogate, is_letter, is_letter_or_digit, is_low_surrogate,
    is_surrogate, is_white_space, nfd, to_lower_invariant,
};

const LATIN_FIRST: u16 = 0x00C0;
const LATIN_LAST: u16 = 0x024F;

/// The plain lower-case Latin letter for each unit in U+00C0–U+024F, or the unit itself. Built from NFD
/// on first use, as the TypeScript builds it at load; the decompositions of this range are the same in
/// every Unicode version since 4.1, so the table equals .NET's.
static LATIN_BASE_LETTERS: LazyLock<[u16; (LATIN_LAST - LATIN_FIRST + 1) as usize]> = LazyLock::new(|| {
    let mut table = [0u16; (LATIN_LAST - LATIN_FIRST + 1) as usize];

    for (offset, slot) in table.iter_mut().enumerate() {
        let c = LATIN_FIRST + offset as u16;
        let first = nfd(&[c]).first().copied().unwrap_or(c);
        *slot = if first < 128 && is_letter(first) {
            to_lower_invariant(first)
        } else {
            c
        };
    }

    // Letters with a stroke or hook have no decomposition.
    let plain: [(u16, u8); 10] = [
        (0x00F8, b'o'),
        (0x00D8, b'o'),
        (0x0111, b'd'),
        (0x0110, b'd'),
        (0x0142, b'l'),
        (0x0141, b'l'),
        (0x0192, b'f'),
        (0x0127, b'h'),
        (0x0131, b'i'),
        (0x00DF, b's'),
    ];
    for (letter, base) in plain {
        table[usize::from(letter - LATIN_FIRST)] = u16::from(base);
    }

    table
});

/// A Cyrillic or Greek look-alike read as the Latin letter it imitates, or `None`.
fn cyrillic_or_greek(c: u16) -> Option<u8> {
    Some(match c {
        // Cyrillic, already lower-cased by normalization.
        0x0430 => b'a',
        0x0432 => b'b',
        0x0435 | 0x0451 => b'e',
        0x043A => b'k',
        0x043C => b'm',
        0x043D | 0x04BB => b'h',
        0x043E => b'o',
        0x0440 => b'p',
        0x0441 => b'c',
        0x0442 => b't',
        0x0443 => b'y',
        0x0445 => b'x',
        0x0456 => b'i',
        0x0458 => b'j',
        0x0455 => b's',
        0x0501 => b'd',
        // Greek.
        0x03B1 => b'a',
        0x03B5 => b'e',
        0x03B9 => b'i',
        0x03BA => b'k',
        0x03BD => b'v',
        0x03BF => b'o',
        0x03C1 => b'p',
        0x03C4 => b't',
        0x03C5 => b'u',
        0x03C7 => b'x',
        _ => return None,
    })
}

/// Reads look-alikes as the Latin letters they imitate, and drops filler inside words.
///
/// Harmless on Persian script, which has no Latin letters to fold. `!` is folded before tokenizing
/// because it is also a sentence separator. Digits are only read as letters in a run that has letters
/// in it: "sh1t" and "4ss" are words, "455" and «۴۵۵ تومان» are numbers. When `map` is given, it
/// receives, for each output unit, its index in the input.
pub(crate) fn fold(normalized: &[u16], mut map: Option<&mut Vec<usize>>) -> Vec<u16> {
    let mut out = Vec::with_capacity(normalized.len());
    let mut i = 0;

    while i < normalized.len() {
        if is_run_boundary(normalized, i) {
            out.push(normalized[i]);
            if let Some(map) = &mut map {
                map.push(i);
            }
            i += 1;
            continue;
        }

        let mut end = i;
        let mut has_letter = false;
        while end < normalized.len() && !is_run_boundary(normalized, end) {
            let unit = normalized[end];
            has_letter = has_letter
                || is_letter(unit)
                || (is_high_surrogate(unit)
                    && end + 1 < normalized.len()
                    && enclosed_letter(code_point_of(unit, normalized[end + 1])) != 0);
            end += 1;
        }

        while i < end {
            let c = normalized[i];

            if is_high_surrogate(c) && i + 1 < end && is_low_surrogate(normalized[i + 1]) {
                // Enclosed and regional-indicator letters (🅵🆄🅲🅺) read as letters; emoji are filler.
                let letter = enclosed_letter(code_point_of(c, normalized[i + 1]));
                if letter != 0 {
                    out.push(letter);
                    if let Some(map) = &mut map {
                        map.push(i);
                    }
                }

                i += 2;
                continue;
            }

            if !is_filler(c) {
                out.push(fold_character(c, has_letter));
                if let Some(map) = &mut map {
                    map.push(i);
                }
            }

            i += 1;
        }
    }

    out
}

/// `char.ConvertToUtf32(high, low)` with the TypeScript's arithmetic. Callers only pass high surrogates;
/// a non-low second unit gives a value that is never an enclosed letter, where .NET would throw on text
/// normalization never produces.
pub(crate) fn code_point_of(high: u16, low: u16) -> i32 {
    0x10000 + ((i32::from(high) - 0xD800) << 10) + (i32::from(low) - 0xDC00)
}

/// One unit read as the Latin letter it imitates; digits only when `map_digits` (a run with letters in
/// it).
pub(crate) fn fold_character(c: u16, map_digits: bool) -> u16 {
    if map_digits {
        match c {
            0x30 => return 0x6F, // 0 → o
            0x31 => return 0x69, // 1 → i
            0x33 => return 0x65, // 3 → e
            0x34 => return 0x61, // 4 → a
            0x35 => return 0x73, // 5 → s
            0x37 => return 0x74, // 7 → t
            0x38 => return 0x62, // 8 → b
            _ => {}
        }
    }

    match c {
        0x21 | 0x7C | 0xA1 => return 0x69, // ! | ¡ → i
        0x20AC => return 0x65,             // € → e
        0x40 => return 0x61,               // @ → a
        0x24 => return 0x73,               // $ → s
        _ => {}
    }

    if let Some(letter) = cyrillic_or_greek(c) {
        return u16::from(letter);
    }

    // Accented Latin: fück, shíť, ƒuck.
    if (LATIN_FIRST..=LATIN_LAST).contains(&c) {
        return LATIN_BASE_LETTERS[usize::from(c - LATIN_FIRST)];
    }

    c
}

/// Filler: symbols with no letter to stand for, typed inside a word to break it up, and combining marks
/// stacked on letters to decorate them (f̶u̶c̶k̶).
pub(crate) fn is_filler(c: u16) -> bool {
    // * + ~ ^ ` = < > # % & • · ♥ ❤
    if matches!(
        c,
        0x2A | 0x2B
            | 0x7E
            | 0x5E
            | 0x60
            | 0x3D
            | 0x3C
            | 0x3E
            | 0x23
            | 0x25
            | 0x26
            | 0x2022
            | 0x00B7
            | 0x2665
            | 0x2764
    ) {
        return true;
    }

    if c < 128 || is_letter_or_digit(c) {
        return false;
    }

    matches!(
        category_of_unit(c),
        Category::Mn | Category::Me | Category::So | Category::Sk
    )
}

/// Where a run of word-like characters ends, for deciding whether its digits are letters. Symbols that
/// fold to letters, filler and emoji belong to the run; spaces and punctuation end it.
pub(crate) fn is_run_boundary(text: &[u16], index: usize) -> bool {
    let c = text[index];
    if is_white_space(c) {
        return true;
    }

    // ! | ¡ € @ $
    if is_letter_or_digit(c)
        || is_surrogate(c)
        || is_filler(c)
        || matches!(c, 0x21 | 0x7C | 0xA1 | 0x20AC | 0x40 | 0x24)
    {
        return false;
    }

    matches!(
        category_of_unit(c),
        Category::Pc
            | Category::Pd
            | Category::Ps
            | Category::Pe
            | Category::Pi
            | Category::Pf
            | Category::Po
            | Category::Sm
            | Category::Sc
    )
}

/// The Latin letter (as a UTF-16 unit) a squared, circled or regional-indicator letter shows, or 0. NFKC
/// already folds the ones with a compatibility mapping (Ⓐ, 𝐟); these have none.
pub(crate) fn enclosed_letter(code_point: i32) -> u16 {
    let base = match code_point {
        0x1F130..=0x1F149 => 0x1F130, // 🄰 squared
        0x1F150..=0x1F169 => 0x1F150, // 🅐 negative circled
        0x1F170..=0x1F189 => 0x1F170, // 🅰 negative squared
        0x1F1E6..=0x1F1FF => 0x1F1E6, // 🇦 regional indicator
        _ => return 0,
    };

    0x61 + (code_point - base) as u16
}

/// Every run of a repeated letter down to one: "fuuck" to "fuck". Spaces are never squeezed.
pub(crate) fn squeeze(value: &[u16], mut map: Option<&mut Vec<usize>>) -> Vec<u16> {
    let mut out = Vec::with_capacity(value.len());
    let mut previous = 0u16;

    for (i, &c) in value.iter().enumerate() {
        if c != previous || c == 0x20 {
            out.push(c);
            if let Some(map) = &mut map {
                map.push(i);
            }
        }

        previous = c;
    }

    out
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::utf16::to_view;

    #[test]
    fn latin_base_letters_come_from_nfd_and_the_plain_list() {
        for (letter, base) in [('ü', 'u'), ('ş', 's'), ('ƒ', 'f'), ('ø', 'o'), ('ß', 's')] {
            assert_eq!(fold_character(letter as u16, false), base as u16, "{letter}");
        }
    }

    #[test]
    fn fold_reads_look_alikes_and_drops_filler() {
        assert_eq!(fold(&to_view("sh1t 455 f*ck"), None), to_view("shit 455 fck"));
        assert_eq!(
            fold(&to_view("\u{1F175}\u{1F184}\u{1F172}\u{1F17A}"), None),
            to_view("fuck")
        );
        assert_eq!(fold(&to_view("f\u{1F595}ck"), None), to_view("fck"));
    }

    #[test]
    fn squeeze_keeps_spaces() {
        assert_eq!(squeeze(&to_view("fuuuck  it"), None), to_view("fuck  it"));
    }
}
