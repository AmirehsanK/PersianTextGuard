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
    /// the word, or symbols masked some of its letters: "sh1t", "$hit", "f*ck", "f**k", "fück",
    /// Cyrillic "bitсh".
    /// </summary>
    LookalikeCharacters = 1 << 1,

    /// <summary>
    /// The word was broken up with spaces or punctuation: "f u c k", "fu ck", «ج.نده».
    /// </summary>
    SplitWord = 1 << 2
}

/// <summary>
/// A banned word found by <see cref="ProfanityFilter.FindMatch"/>.
/// </summary>
/// <param name="Word">The entry that matched, exactly as it was given to the filter.</param>
/// <param name="Evasion">What had to be undone to find it. Useful for moderation logs.</param>
public sealed record ProfanityMatch(BannedWord Word, EvasionKind Evasion)
{
    /// <summary>
    /// The first character of the matched words in the text as it was passed, counted in UTF-16
    /// code units, so it can be used directly to highlight or slice that string.
    /// </summary>
    /// <remarks>
    /// The region always covers whole words: a match inside "motherfucker" or «جنده‌ها» starts at
    /// the start of that word. It is 0 on a match the caller constructs themselves.
    /// </remarks>
    public int Index { get; init; }

    /// <summary>
    /// How many characters the matched words span, from <see cref="Index"/>, in UTF-16 code units.
    /// </summary>
    /// <remarks>
    /// Separators inside a disguised word ("f u c k", «ج.نده») are part of the span. It is 0 on a
    /// match the caller constructs themselves.
    /// </remarks>
    public int Length { get; init; }
}
