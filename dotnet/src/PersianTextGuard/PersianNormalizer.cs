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
        // of an emoji contains, and on noncharacters (see NormalizeKeepingNoncharacters). User input
        // must never be able to throw here.
        string source;
        List<int>? sourceMap = null;
        if ((steps & PersianNormalization.CompatibilityForms) == 0)
        {
            source = text!;
        }
        else if (map is null)
        {
            source = NormalizeKeepingNoncharacters(ReplaceLoneSurrogates(text!));
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
        // Most messages are already in NFKC. Then every character maps to itself, and normalizing
        // each Persian letter on its own would cost a string and a Normalize call apiece.
        // IsNormalized throws on noncharacters just as Normalize does, so text containing one takes
        // the segment path, which normalizes around them.
        if (!ContainsNoncharacter(text) && text.IsNormalized(NormalizationForm.FormKC))
        {
            for (var index = 0; index < text.Length; index++)
            {
                map.Add(index);
            }

            return text;
        }

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

            // ASCII is already in normal form; skip the allocation for the common case. A segment
            // whose starter is a noncharacter keeps it unchanged and normalizes only its marks.
            var segment = i - start == 1 && text[start] < 128
                ? text.Substring(start, 1)
                : NormalizeKeepingNoncharacters(text.Substring(start, i - start));

            foreach (var c in segment)
            {
                sb.Append(c);
                map.Add(start);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// NFKC that never throws on Unicode noncharacters (U+FDD0–U+FDEF, and every code point ending in
    /// FFFE or FFFF).
    /// </summary>
    /// <remarks>
    /// <see cref="string.Normalize(NormalizationForm)"/> throws <see cref="ArgumentException"/> on
    /// noncharacters: U+FFFE on .NET 8 and .NET 10, all of them on .NET Framework, so PersianTextGuard
    /// 1.2.0 threw on messages containing one. A noncharacter has no decomposition and composes with
    /// nothing, so normalizing the text around each one and copying it through gives exactly the
    /// result whole-string normalization gives where that does not throw.
    /// </remarks>
    private static string NormalizeKeepingNoncharacters(string text)
    {
        if (!ContainsNoncharacter(text))
        {
            return text.Normalize(NormalizationForm.FormKC);
        }

        var sb = new StringBuilder(text.Length);
        var start = 0;
        var i = 0;

        while (i < text.Length)
        {
            if (!IsNoncharacterAt(text, i, out var length))
            {
                i++;
                continue;
            }

            if (i > start)
            {
                sb.Append(text.Substring(start, i - start).Normalize(NormalizationForm.FormKC));
            }

            sb.Append(text, i, length);
            i += length;
            start = i;
        }

        if (start < text.Length)
        {
            sb.Append(text.Substring(start).Normalize(NormalizationForm.FormKC));
        }

        return sb.ToString();
    }

    private static bool ContainsNoncharacter(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (IsNoncharacterAt(text, i, out _))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a noncharacter starts at <paramref name="index"/>, and how many UTF-16 units it takes.</summary>
    private static bool IsNoncharacterAt(string text, int index, out int length)
    {
        var c = text[index];
        if (char.IsHighSurrogate(c) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
        {
            var codePoint = char.ConvertToUtf32(c, text[index + 1]);
            length = 2;
            return (codePoint & 0xFFFE) == 0xFFFE;
        }

        length = 1;
        return (c >= '﷐' && c <= '﷯') || c >= '￾';
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
        var words = new List<string>();
        CollectTokens(text, words, tokens: null);
        return words.ToArray();
    }

    /// <summary><see cref="Tokenize"/>, keeping where each token starts and ends in <paramref name="text"/>.</summary>
    internal static Token[] TokenizeWithOffsets(string? text)
    {
        var tokens = new List<Token>();
        CollectTokens(text, words: null, tokens);
        return tokens.ToArray();
    }

    // One loop for both shapes, so a token means the same thing to the word list and the scanner.
    private static void CollectTokens(string? text, List<string>? words, List<Token>? tokens)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var start = -1;

        for (var i = 0; i <= text!.Length; i++)
        {
            if (i < text.Length && IsWordCharacter(text, i))
            {
                if (start < 0)
                {
                    start = i;
                }

                continue;
            }

            if (start >= 0)
            {
                var word = text.Substring(start, i - start);
                words?.Add(word);
                tokens?.Add(new Token(word, start, i));
                start = -1;
            }
        }
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
