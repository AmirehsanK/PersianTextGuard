namespace PersianTextGuard;

/// <summary>
/// The steps <see cref="PersianNormalizer.Normalize(string?, PersianNormalization)"/> applies.
/// Combine them, or use <see cref="Standard"/> or <see cref="Comparison"/>.
/// </summary>
[Flags]
public enum PersianNormalization
{
    /// <summary>Leave the text exactly as it is.</summary>
    None = 0,

    /// <summary>
    /// Unicode compatibility normalization (NFKC): Arabic presentation forms such as ﻙ become
    /// base letters, full-width Latin becomes ASCII.
    /// </summary>
    CompatibilityForms = 1 << 0,

    /// <summary>
    /// Arabic yeh (ي) and alef maksura (ى) to Persian yeh (ی), Arabic kaf (ك) to keheh (ک), hamza
    /// alef forms to bare alef, teh marbuta to heh, and look-alike letters from the Urdu,
    /// Kurdish and Pashto blocks (ڪ ھ ۀ ې ٱ …) to the Persian ones.
    /// </summary>
    UnifyLetters = 1 << 1,

    /// <summary>Arabic diacritics (harakat: fatha, kasra, damma, tanwin, shadda, sukun).</summary>
    RemoveDiacritics = 1 << 2,

    /// <summary>Tatweel (ـ), the stretching character.</summary>
    RemoveTatweel = 1 << 3,

    /// <summary>
    /// Zero-width characters: non-joiner, joiner, space, word joiner, byte-order mark and soft
    /// hyphen. Note that the zero-width non-joiner is correct Persian spelling (می‌روم), so this
    /// belongs in comparison, not in text you display.
    /// </summary>
    RemoveZeroWidth = 1 << 4,

    /// <summary>Bidirectional control characters: LRM, RLM, embeddings, overrides and isolates.</summary>
    RemoveBidiControls = 1 << 5,

    /// <summary>Persian (۰-۹) and Arabic-Indic (٠-٩) digits to ASCII 0-9.</summary>
    AsciiDigits = 1 << 6,

    /// <summary>Invariant lower-casing.</summary>
    LowerCase = 1 << 7,

    /// <summary>Every run of whitespace to a single space, and trimmed.</summary>
    CollapseWhitespace = 1 << 8,

    /// <summary>
    /// Runs of the same character cut to two, so «سسسسلام» and "heeeey" cannot slip past an
    /// entry, while real doubled letters survive.
    /// </summary>
    CollapseRepeats = 1 << 9,

    /// <summary>
    /// Safe for text you store and show: fixes letters typed on an Arabic keyboard layout and
    /// strips invisible junk, but keeps the zero-width non-joiner, digits, case and repeats.
    /// </summary>
    Standard = CompatibilityForms | UnifyLetters | RemoveTatweel | RemoveBidiControls | CollapseWhitespace,

    /// <summary>
    /// Everything. Lossy on purpose: the form to compare, search and filter on, never to display.
    /// </summary>
    Comparison = Standard | RemoveDiacritics | RemoveZeroWidth | AsciiDigits | LowerCase | CollapseRepeats
}
