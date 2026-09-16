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
    /// Read digits, symbols, accented letters and Cyrillic or Greek look-alikes as the Latin
    /// letters they imitate ("sh1t", "$hit", "k0s", "fück"), drop filler and emoji typed inside
    /// a word ("f*ck"), and read symbols masking letters as those letters ("f**k", "c*nt").
    /// </summary>
    public bool FoldLookalikeCharacters { get; init; } = true;

    /// <summary>
    /// Put split words back together: runs of single letters ("f u c k", «ک ی ر»), punctuation
    /// inside a word («ج.نده», "kos_kesh"), and a word split once ("fu ck", «کی ر») when the
    /// halves join into exactly an entry. Ordinary words are never glued into something else:
    /// "push it" contains "shit" once the space goes, and does not match.
    /// </summary>
    public bool JoinSpacedLetters { get; init; } = true;
}
