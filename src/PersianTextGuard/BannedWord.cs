namespace PersianTextGuard;

/// <summary>How a <see cref="BannedWord"/> is looked for in text.</summary>
public enum WordMatchMode
{
    /// <summary>
    /// Only as whole words (or a whole phrase). The safe default: «کس» as a whole word does not
    /// flag «کسی», and "ass" does not flag "class".
    /// </summary>
    WholeWord,

    /// <summary>
    /// Anywhere, including inside longer words. Use it for stems whose every extension is also
    /// offensive, such as "fuck" covering "motherfucker".
    /// </summary>
    Anywhere
}

/// <summary>A word or phrase for <see cref="ProfanityFilter"/> to look for.</summary>
/// <param name="Text">The word or phrase, in any spelling; it is normalized when the filter is built.</param>
/// <param name="Mode">Whole words only, or anywhere in the text.</param>
public readonly record struct BannedWord(string Text, WordMatchMode Mode = WordMatchMode.WholeWord);
