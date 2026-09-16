namespace PersianTextGuard;

/// <summary>
/// Finds banned words in user text, including the spellings people use to get past a word list.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PersianNormalizer"/> folds the Unicode tricks: Arabic yeh and kaf, zero-width
/// characters, tatweel. What it cannot fold without damaging ordinary text are the tricks a
/// person types on purpose: letters split apart, a key held down, digits and symbols for
/// letters, filler inside a word, accents and look-alike Cyrillic letters. So the filter reads
/// the text in several forms and reports a match if a word appears in any of them. The forms
/// are only used to find matches and are never returned, which is what lets them be lossy.
/// </para>
/// <para>
/// Whole-word entries are looked up by token, so checking a message costs about the same
/// against a list of 400 entries or 4,000. Persian whole-word entries also match with the
/// common suffixes attached («جنده‌ها», «کیرتون»).
/// </para>
/// <para>
/// Build one filter when your word list loads and share it: it is immutable and thread-safe.
/// </para>
/// </remarks>
public sealed partial class ProfanityFilter
{
    // A word split in two or masked with symbols is only matched against entries at least this
    // long: shorter ones ("ass", «کون») turn up by accident in ordinary text broken that way.
    private const int MinimumBrokenWordLength = 4;

    private readonly ProfanityFilterOptions _options;
    private readonly Dictionary<string, (BannedWord Word, int Order)> _words = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Phrase>> _phrases = new(StringComparer.Ordinal);
    private readonly List<Key> _anywhere = [];
    private readonly List<Key> _maskable = [];

    /// <summary>Prepares <paramref name="words"/> for matching. Do this once, not per message.</summary>
    /// <param name="words">The words to look for, for example <see cref="WordList.PersianDefault"/>.</param>
    /// <param name="options">Which evasions to read through; all of them by default.</param>
    public ProfanityFilter(IEnumerable<BannedWord> words, ProfanityFilterOptions? options = null)
    {
        if (words is null)
        {
            throw new ArgumentNullException(nameof(words));
        }

        _options = options ?? ProfanityFilterOptions.Default;

        var seen = new HashSet<(string, WordMatchMode)>();
        var anywhereKeys = new HashSet<string>(StringComparer.Ordinal);

        // An entry's position in the list, duplicates included: it breaks ties between
        // overlapping matches of the same length, in favour of the entry listed first.
        var order = 0;

        foreach (var word in words)
        {
            var current = order++;
            var normalized = PersianNormalizer.Normalize(word.Text);
            if (normalized.Length == 0 || !seen.Add((normalized, word.Mode)))
            {
                continue;
            }

            var added = Add(normalized, word, current, anywhereKeys);

            // The text is folded before matching, so an entry stored the way an evader types it
            // ("k0s", pasted from the message a moderator was reading) would never match the
            // plain spelling the folded text turns into. Keep a folded key beside the original.
            if (_options.FoldLookalikeCharacters)
            {
                added |= Add(Fold(normalized), word, current, anywhereKeys);
            }

            if (added)
            {
                Count++;
            }
        }
    }

    /// <summary>The number of distinct entries the filter looks for.</summary>
    public int Count { get; }

    /// <summary>True when <paramref name="text"/> contains a banned word.</summary>
    /// <remarks>Never throws: <c>null</c>, empty and malformed text are simply clean or not.</remarks>
    public bool ContainsProfanity(string? text) => Scan(text, all: null, out _);

    /// <summary>The first banned word found in <paramref name="text"/>, or null when it is clean.</summary>
    /// <remarks>
    /// The match's <see cref="ProfanityMatch.Index"/> and <see cref="ProfanityMatch.Length"/> give
    /// where it is in <paramref name="text"/> as passed, widened to whole words. "First" means found
    /// with the least evasion undone, not earliest in the text. Never throws.
    /// </remarks>
    public ProfanityMatch? FindMatch(string? text)
    {
        if (!Scan(text, all: null, out var hit))
        {
            return null;
        }

        var candidate = ToCandidate(text!, hit, new MappedText?[ReadingKindCount]);
        return new ProfanityMatch(hit.Word, hit.Evasion)
        {
            Index = candidate.Start,
            Length = candidate.End - candidate.Start,
        };
    }

    private bool Add(string form, BannedWord word, int order, HashSet<string> anywhereKeys)
    {
        if (word.Mode == WordMatchMode.Anywhere)
        {
            if (form.Length == 0)
            {
                return false;
            }

            if (anywhereKeys.Add(form))
            {
                var key = new Key(form, word, WholeWord: false, order);
                _anywhere.Add(key);
                if (form.Length >= MinimumBrokenWordLength && form.IndexOf(' ') < 0)
                {
                    _maskable.Add(key);
                }
            }

            return true;
        }

        var tokens = PersianNormalizer.Tokenize(form);
        if (tokens.Length == 0)
        {
            return false;
        }

        if (tokens.Length == 1)
        {
            if (!_words.ContainsKey(tokens[0]))
            {
                _words.Add(tokens[0], (word, order));
                if (tokens[0].Length >= MinimumBrokenWordLength)
                {
                    _maskable.Add(new Key(tokens[0], word, WholeWord: true, order));
                }
            }

            return true;
        }

        if (!_phrases.TryGetValue(tokens[0], out var phrases))
        {
            _phrases.Add(tokens[0], phrases = []);
        }

        phrases.Add(new Phrase(tokens, word, order));
        return true;
    }

    /// <param name="Text">What is searched for.</param>
    /// <param name="Word">The entry as the caller gave it, reported when it matches.</param>
    /// <param name="WholeWord">Whether the key must match a whole token.</param>
    /// <param name="Order">The entry's position in the word list.</param>
    private readonly record struct Key(string Text, BannedWord Word, bool WholeWord, int Order);

    /// <param name="Tokens">The phrase, one token per word.</param>
    /// <param name="Word">The entry as the caller gave it, reported when it matches.</param>
    /// <param name="Order">The entry's position in the word list.</param>
    private sealed record Phrase(string[] Tokens, BannedWord Word, int Order);
}
