using System.Globalization;
using System.Text;

namespace PersianTextGuard;

/// <summary>
/// Normalizes Persian text so that strings which look the same compare the same.
/// </summary>
/// <remarks>
/// Persian is written with character pairs that render near-identically but have different code
/// points (Arabic yeh and Persian yeh, Arabic kaf and keheh), plus optional diacritics, invisible
/// zero-width and bidi characters, and three digit ranges. A raw comparison against a word list
/// or a search index is defeated by typing one character differently, which is the first thing
/// anyone tries.
/// </remarks>
public static class PersianNormalizer
{
    private const char PersianYeh = 'ی';
    private const char PersianKeheh = 'ک';
    private const char Heh = 'ه';
    private const char Alef = 'ا';

    private const char ArabicIndicZero = '٠';
    private const char ExtendedArabicIndicZero = '۰';

    private static readonly string[] PersianDigitStrings = ["۰", "۱", "۲", "۳", "۴", "۵", "۶", "۷", "۸", "۹"];

    /// <summary>
    /// Normalizes <paramref name="text"/> with the given steps. Returns an empty string for null.
    /// </summary>
    /// <example>
    /// <code>
    /// PersianNormalizer.Normalize("كتاب‌هاي ۱۲")                                // "کتابهای 12"
    /// PersianNormalizer.Normalize("كتاب‌هاي ۱۲", PersianNormalization.Standard) // "کتاب‌های ۱۲"
    /// </code>
    /// </example>
    public static string Normalize(string? text, PersianNormalization steps = PersianNormalization.Comparison) =>
        Normalize(text, steps, map: null);

    /// <summary>
    /// <see cref="Normalize(string?, PersianNormalization)"/>, also recording in <paramref name="map"/>,
    /// for every character of the result, the index in <paramref name="text"/> it came from.
    /// </summary>
    /// <remarks>
    /// <see cref="string.Normalize()"/> cannot say where its output came from, so with a map the
    /// compatibility step normalizes one segment at a time: a starter and the combining marks after
    /// it, which is where composition happens. Every character a segment produces maps to the
    /// segment's first index. For the scripts the filter handles the text is the same as whole-string
    /// normalization; callers that need certainty compare the two (see <see cref="SourceMap"/>).
    /// </remarks>
    internal static string Normalize(string? text, PersianNormalization steps, List<int>? map)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        // string.Normalize throws on a lone surrogate, which is what a message cut in the middle
        // of an emoji contains. User input must never be able to throw here.
        string source;
        List<int>? sourceMap = null;
        if ((steps & PersianNormalization.CompatibilityForms) == 0)
        {
            source = text!;
        }
        else if (map is null)
        {
            source = ReplaceLoneSurrogates(text!).Normalize(NormalizationForm.FormKC);
        }
        else
        {
            sourceMap = new List<int>(text!.Length);
            source = NormalizeCompatibilityBySegment(ReplaceLoneSurrogates(text!), sourceMap);
        }

        var sb = new StringBuilder(source.Length);
        var stepMap = map is null ? null : new List<int>(source.Length);
        var unify = (steps & PersianNormalization.UnifyLetters) != 0;
        var lowerCase = (steps & PersianNormalization.LowerCase) != 0;
        var asciiDigits = (steps & PersianNormalization.AsciiDigits) != 0;

        for (var index = 0; index < source.Length; index++)
        {
            var raw = source[index];
            if (IsRemoved(raw, steps))
            {
                continue;
            }

            stepMap?.Add(sourceMap is null ? index : sourceMap[index]);

            var c = unify ? UnifyLetter(raw) : raw;

            if (asciiDigits)
            {
                if (c >= ArabicIndicZero && c <= ArabicIndicZero + 9)
                {
                    c = (char)('0' + (c - ArabicIndicZero));
                }
                else if (c >= ExtendedArabicIndicZero && c <= ExtendedArabicIndicZero + 9)
                {
                    c = (char)('0' + (c - ExtendedArabicIndicZero));
                }
            }

            sb.Append(lowerCase ? char.ToLowerInvariant(c) : c);
        }

        var result = sb.ToString();

        if ((steps & PersianNormalization.CollapseRepeats) != 0)
        {
            result = CollapseRepeats(result, stepMap);
        }

        if ((steps & PersianNormalization.CollapseWhitespace) != 0)
        {
            result = CollapseWhitespace(result, stepMap);
        }

        map?.AddRange(stepMap!);
        return result;
    }

    /// <summary>NFKC one segment at a time: a code point and the combining marks that follow it.</summary>
    private static string NormalizeCompatibilityBySegment(string text, List<int> map)
    {
        var sb = new StringBuilder(text.Length);
        var i = 0;

        while (i < text.Length)
        {
            var start = i;
            i += CodePointLength(text, i);
            while (i < text.Length && IsCombiningMark(text, i))
            {
                i += CodePointLength(text, i);
            }

            // ASCII is already in normal form; skip the allocation for the common case.
            var segment = i - start == 1 && text[start] < 128
                ? text.Substring(start, 1)
                : text.Substring(start, i - start).Normalize(NormalizationForm.FormKC);

            foreach (var c in segment)
            {
                sb.Append(c);
                map.Add(start);
            }
        }

        return sb.ToString();
    }

    private static int CodePointLength(string text, int index) =>
        char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]) ? 2 : 1;

    private static bool IsCombiningMark(string text, int index) =>
        CharUnicodeInfo.GetUnicodeCategory(text, index) is UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;

    /// <summary>
    /// Splits text into word tokens on whitespace, punctuation (ASCII, Persian «» ، ؛ ؟ ٫, and
    /// the rest of Unicode) and symbols, emoji included. Letters, digits, combining marks and
    /// the zero-width non-joiner stay inside tokens. Normalize first: tokens are only as
    /// consistent as the text they came from.
    /// </summary>
    public static string[] Tokenize(string? text)
    {
        var tokens = TokenizeWithOffsets(text);
        var texts = new string[tokens.Length];

        for (var i = 0; i < tokens.Length; i++)
        {
            texts[i] = tokens[i].Text;
        }

        return texts;
    }

    /// <summary><see cref="Tokenize"/>, keeping where each token starts and ends in <paramref name="text"/>.</summary>
    internal static Token[] TokenizeWithOffsets(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var tokens = new List<Token>();
        var start = -1;

        for (var i = 0; i < text!.Length; i++)
        {
            if (IsWordCharacter(text, i))
            {
                if (start < 0)
                {
                    start = i;
                }

                continue;
            }

            if (start >= 0)
            {
                tokens.Add(new Token(text.Substring(start, i - start), start, i));
                start = -1;
            }
        }

        if (start >= 0)
        {
            tokens.Add(new Token(text.Substring(start), start, text.Length));
        }

        return tokens.ToArray();
    }

    /// <summary>
    /// Whether the character at <paramref name="index"/> belongs inside a word. A split-off
    /// word used to survive next to anything the tokenizer did not know about: «کیر», کیر😂.
    /// </summary>
    internal static bool IsWordCharacter(string text, int index)
    {
        var c = text[index];
        if (c < 128)
        {
            return char.IsLetterOrDigit(c);
        }

        if (char.IsLowSurrogate(c) && index > 0 && char.IsHighSurrogate(text[index - 1]))
        {
            return IsWordCharacter(text, index - 1);
        }

        switch (CharUnicodeInfo.GetUnicodeCategory(text, index))
        {
            case UnicodeCategory.UppercaseLetter:
            case UnicodeCategory.LowercaseLetter:
            case UnicodeCategory.TitlecaseLetter:
            case UnicodeCategory.ModifierLetter:
            case UnicodeCategory.OtherLetter:
            case UnicodeCategory.NonSpacingMark:
            case UnicodeCategory.SpacingCombiningMark:
            case UnicodeCategory.EnclosingMark:
            case UnicodeCategory.DecimalDigitNumber:
            case UnicodeCategory.LetterNumber:
            case UnicodeCategory.OtherNumber:
                return true;

            // Zero-width non-joiner and friends are part of Persian spelling, but a variation
            // selector or zero-width joiner after an emoji is not.
            case UnicodeCategory.Format:
                return c is '‌' or '‍' && index > 0 && char.IsLetter(text[index - 1]);

            default:
                return false;
        }
    }

    /// <summary>Renders ASCII digits as Persian digits (۰-۹), leaving everything else alone.</summary>
    public static string ToPersianDigits(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(text!.Length);

        foreach (var c in text)
        {
            if (c >= '0' && c <= '9')
            {
                sb.Append(PersianDigitStrings[c - '0']);
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    /// <summary>Converts Persian and Arabic-Indic digits to ASCII, leaving everything else alone.</summary>
    public static string ToAsciiDigits(string? text) => Normalize(text, PersianNormalization.AsciiDigits);

    private static string ReplaceLoneSurrogates(string text)
    {
        StringBuilder? sb = null;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var lone = char.IsHighSurrogate(c)
                ? i + 1 >= text.Length || !char.IsLowSurrogate(text[i + 1])
                : char.IsLowSurrogate(c) && (i == 0 || !char.IsHighSurrogate(text[i - 1]));

            if (lone)
            {
                sb ??= new StringBuilder(text, 0, i, text.Length);
                sb.Append('�');
            }
            else
            {
                sb?.Append(c);
            }
        }

        return sb?.ToString() ?? text;
    }

    private static bool IsRemoved(char c, PersianNormalization steps)
    {
        if ((steps & PersianNormalization.RemoveDiacritics) != 0 && c >= 'ً' && c <= 'ْ')
        {
            return true;
        }

        if ((steps & PersianNormalization.RemoveTatweel) != 0 && c == 'ـ')
        {
            return true;
        }

        if ((steps & PersianNormalization.RemoveZeroWidth) != 0
            && c is '​' or '‌' or '‍' or '⁠' or '﻿' or '­')
        {
            return true;
        }

        return (steps & PersianNormalization.RemoveBidiControls) != 0
               && (c is '‎' or '‏' or '؜'
                   || (c >= '‪' && c <= '‮')
                   || (c >= '⁦' && c <= '⁩'));
    }

    private static char UnifyLetter(char c) => c switch
    {
        'ي' or 'ى' => PersianYeh,
        'ك' => PersianKeheh,
        'أ' or 'إ' or 'آ' => Alef,
        'ة' => Heh,

        // Letters from the Urdu, Kurdish and Pashto blocks that render as the Persian ones in
        // most fonts. NFKC leaves them alone because they are distinct letters, not
        // compatibility forms, so a swash kaf gets past an entry written with a normal one.
        'ڪ' or 'ګ' => PersianKeheh,
        'ھ' or 'ۀ' or 'ہ' or 'ۃ' or 'ە' => Heh,
        'ۍ' or 'ێ' or 'ې' or 'ے' or 'ۓ' => PersianYeh,
        'ٱ' or 'ٲ' or 'ٳ' or 'ٵ' => Alef,
        _ => c
    };

    private static string CollapseRepeats(string value, List<int>? map)
    {
        var sb = new StringBuilder(value.Length);
        var kept = map is null ? null : new List<int>(map.Count);
        var runChar = '\0';
        var runLength = 0;

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            runLength = c == runChar ? runLength + 1 : 1;
            runChar = c;

            if (runLength <= 2 || char.IsWhiteSpace(c))
            {
                sb.Append(c);
                kept?.Add(map![i]);
            }
        }

        ReplaceMap(map, kept);
        return sb.ToString();
    }

    private static string CollapseWhitespace(string value, List<int>? map)
    {
        var sb = new StringBuilder(value.Length);
        var kept = map is null ? null : new List<int>(map.Count);
        var pendingSpace = false;
        var pendingSource = 0;

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = sb.Length > 0;
                if (map is not null)
                {
                    pendingSource = map[i];
                }

                continue;
            }

            if (pendingSpace)
            {
                sb.Append(' ');
                kept?.Add(pendingSource);
                pendingSpace = false;
            }

            sb.Append(c);
            kept?.Add(map![i]);
        }

        ReplaceMap(map, kept);
        return sb.ToString();
    }

    private static void ReplaceMap(List<int>? map, List<int>? kept)
    {
        if (map is null)
        {
            return;
        }

        map.Clear();
        map.AddRange(kept!);
    }
}
