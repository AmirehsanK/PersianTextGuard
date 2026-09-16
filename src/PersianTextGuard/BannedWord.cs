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

/// <summary>What kind of word a <see cref="BannedWord"/> is, so a site can choose what to block.</summary>
public enum WordCategory
{
    /// <summary>No category: the default for entries in your own lists.</summary>
    Uncategorized = 0,

    /// <summary>General swearing and crude words: fuck, shit, «ریدم», «گوه».</summary>
    Profanity,

    /// <summary>Genitals, sex acts and pornography: «کیر», «سکس», cock, blowjob.</summary>
    Sexual,

    /// <summary>
    /// Strong insults, including the family and honour insults Persian is built on: «کسکش»,
    /// «مادرجنده», «بی‌ناموس», bastard.
    /// </summary>
    Insult,

    /// <summary>
    /// Hate speech against a group: race, ethnicity, religion, sexual orientation, gender,
    /// disability. «کونی» used against gay men, faggot, nigger, retard.
    /// </summary>
    Slur,

    /// <summary>Telling someone to hurt themselves, or to shut up: kys, «خفه شو».</summary>
    Harassment,

    /// <summary>
    /// Rude in context but ordinary words otherwise: «آشغال», «گوز», «دلقک», damn, crap. Left
    /// out of <see cref="WordList.PersianDefault"/> because blocking them rejects normal messages.
    /// </summary>
    Mild
}

/// <summary>A word or phrase for <see cref="ProfanityFilter"/> to look for.</summary>
/// <param name="Text">The word or phrase, in any spelling; it is normalized when the filter is built.</param>
/// <param name="Mode">Whole words only, or anywhere in the text.</param>
public readonly record struct BannedWord(string Text, WordMatchMode Mode = WordMatchMode.WholeWord)
{
    /// <summary>What kind of word this is. Bundled entries always have one; your own may not.</summary>
    public WordCategory Category { get; init; }
}
