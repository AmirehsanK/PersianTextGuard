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

    private static readonly char[] WordSeparators =
        [' ', '\t', '\n', '\r', '.', ',', '!', '?', ':', ';', '-', '_', '/', '\\', '(', ')', '[', ']', '"', '\'', '،', '؛', '؟'];

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
    public static string Normalize(string? text, PersianNormalization steps = PersianNormalization.Comparison)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var source = (steps & PersianNormalization.CompatibilityForms) != 0
            ? text!.Normalize(NormalizationForm.FormKC)
            : text!;

        var sb = new StringBuilder(source.Length);
        var unify = (steps & PersianNormalization.UnifyLetters) != 0;
        var lowerCase = (steps & PersianNormalization.LowerCase) != 0;
        var asciiDigits = (steps & PersianNormalization.AsciiDigits) != 0;

        foreach (var raw in source)
        {
            if (IsRemoved(raw, steps))
            {
                continue;
            }

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
            result = CollapseRepeats(result);
        }

        if ((steps & PersianNormalization.CollapseWhitespace) != 0)
        {
            result = CollapseWhitespace(result);
        }

        return result;
    }

    /// <summary>
    /// Splits text into word tokens on whitespace and on ASCII and Persian punctuation (، ؛ ؟).
    /// Normalize first: tokens are only as consistent as the text they came from.
    /// </summary>
    public static string[] Tokenize(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var parts = text!.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries);
        var tokens = new List<string>(parts.Length);

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 0)
            {
                tokens.Add(trimmed);
            }
        }

        return tokens.ToArray();
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

    private static string CollapseRepeats(string value)
    {
        var sb = new StringBuilder(value.Length);
        var runChar = '\0';
        var runLength = 0;

        foreach (var c in value)
        {
            runLength = c == runChar ? runLength + 1 : 1;
            runChar = c;

            if (runLength <= 2 || char.IsWhiteSpace(c))
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static string CollapseWhitespace(string value)
    {
        var sb = new StringBuilder(value.Length);
        var pendingSpace = false;

        foreach (var c in value)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = sb.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                sb.Append(' ');
                pendingSpace = false;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }
}
