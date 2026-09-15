namespace PersianTextGuard;

/// <summary>Which evasions had to be undone before a banned word showed up.</summary>
[Flags]
public enum EvasionKind
{
    /// <summary>The word was there as written, after normalization.</summary>
    None = 0,

    /// <summary>A letter was held down: "fuuuck", «کیییر».</summary>
    RepeatedLetters = 1 << 0,

    /// <summary>
    /// Digits, symbols or look-alike letters stood in for letters, or filler was typed inside
    /// the word: "sh1t", "$hit", "f*ck", Cyrillic "bitсh".
    /// </summary>
    LookalikeCharacters = 1 << 1
}

/// <summary>A banned word found by <see cref="ProfanityFilter.FindMatch"/>.</summary>
/// <param name="Word">The entry that matched, exactly as it was given to the filter.</param>
/// <param name="Evasion">What had to be undone to find it. Useful for moderation logs.</param>
public sealed record ProfanityMatch(BannedWord Word, EvasionKind Evasion);
