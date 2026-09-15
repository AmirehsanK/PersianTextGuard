namespace PersianTextGuard;

/// <summary>Which evasions <see cref="ProfanityFilter"/> reads through. All are on by default.</summary>
public sealed class ProfanityFilterOptions
{
    /// <summary>The defaults: every evasion is read through.</summary>
    public static ProfanityFilterOptions Default { get; } = new();

    /// <summary>
    /// Undo held keys: "fuuuck" is also read as "fuck". Only ever used to find a match, so
    /// squeezing "pass" to "pas" does no harm.
    /// </summary>
    public bool SqueezeRepeatedLetters { get; init; } = true;

    /// <summary>
    /// Read digits, symbols and Cyrillic or Greek look-alikes as the Latin letters they imitate
    /// ("sh1t", "$hit", "k0s"), and drop filler typed inside a word ("f*ck").
    /// </summary>
    public bool FoldLookalikeCharacters { get; init; } = true;

    /// <summary>
    /// Join runs of single letters into one word: "f u c k", "f.u.c.k", «ک ی ر». Whole words
    /// are never glued together, because "push it" contains "shit" once the space goes.
    /// </summary>
    public bool JoinSpacedLetters { get; init; } = true;
}
