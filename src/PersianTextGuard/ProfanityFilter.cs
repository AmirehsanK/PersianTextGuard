using System.Text;

namespace PersianTextGuard;

/// <summary>
/// Finds banned words in user text, including the spellings people use to get past a word list.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PersianNormalizer"/> folds the Unicode tricks: Arabic yeh and kaf, zero-width
/// characters, tatweel. What it cannot fold without damaging ordinary text are the tricks a
/// person types on purpose: letters split apart, a key held down, digits and symbols for
/// letters, filler inside a word, and look-alike Cyrillic letters. So the filter reads the text
/// in several forms and reports a match if a word appears in any of them. The forms are only
/// used to find matches and are never returned, which is what lets them be lossy.
/// </para>
/// <para>
/// Build one filter when your word list loads and share it: it is immutable and thread-safe.
/// </para>
/// </remarks>
public sealed class ProfanityFilter
{
    private readonly Entry[] _entries;
    private readonly ProfanityFilterOptions _options;

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
        var entries = new List<Entry>();

        foreach (var word in words)
        {
            var normalized = PersianNormalizer.Normalize(word.Text);
            if (normalized.Length == 0 || !seen.Add((normalized, word.Mode)))
            {
                continue;
            }

            var wholeWord = word.Mode == WordMatchMode.WholeWord;
            var key = KeyOf(normalized, wholeWord);

            // The text is folded before matching, so an entry stored the way an evader types it
            // ("k0s", pasted from the message a moderator was reading) would never match the
            // plain spelling the folded text turns into. Keep a folded key beside the original.
            var foldedKey = _options.FoldLookalikeCharacters ? KeyOf(Fold(normalized), wholeWord) : key;

            if (key.Length > 0)
            {
                entries.Add(Entry.Create(word, key, foldedKey, wholeWord));
            }
        }

        _entries = entries.ToArray();
    }

    /// <summary>The number of distinct entries the filter looks for.</summary>
    public int Count => _entries.Length;

    /// <summary>True when <paramref name="text"/> contains a banned word.</summary>
    public bool ContainsProfanity(string? text) => FindMatch(text) is not null;

    /// <summary>The first banned word found in <paramref name="text"/>, or null when it is clean.</summary>
    public ProfanityMatch? FindMatch(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || _entries.Length == 0)
        {
            return null;
        }

        var normalized = PersianNormalizer.Normalize(text);
        var folded = _options.FoldLookalikeCharacters ? Fold(normalized) : normalized;

        // Most literal reading first, so the reported evasion is the least that was needed.
        var readings = new List<(string Text, EvasionKind Evasion)>(4) { (normalized, EvasionKind.None) };
        if (_options.SqueezeRepeatedLetters)
        {
            readings.Add((Squeeze(normalized), EvasionKind.RepeatedLetters));
        }

        if (_options.FoldLookalikeCharacters)
        {
            readings.Add((folded, EvasionKind.LookalikeCharacters));
            if (_options.SqueezeRepeatedLetters)
            {
                readings.Add((Squeeze(folded), EvasionKind.LookalikeCharacters | EvasionKind.RepeatedLetters));
            }
        }

        var checkedReadings = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (reading, evasion) in readings)
        {
            // On ordinary text most readings are the same string.
            if (!checkedReadings.Add(reading))
            {
                continue;
            }

            // Padded with spaces so a whole-word key is found with a plain substring search:
            // " kos " is in " ye kos kesh " but not in " kosar ".
            var tokens = PersianNormalizer.Tokenize(reading);
            var words = " " + string.Join(" ", _options.JoinSpacedLetters ? JoinSingleLetters(tokens) : tokens) + " ";

            foreach (var entry in _entries)
            {
                if (Matches(entry.SearchKey, entry.WholeWord, reading, words)
                    || (entry.FoldedSearchKey is not null
                        && Matches(entry.FoldedSearchKey, entry.WholeWord, reading, words)))
                {
                    return new ProfanityMatch(entry.Word, evasion);
                }
            }
        }

        return null;
    }

    private static string KeyOf(string word, bool wholeWord) =>
        wholeWord ? string.Join(" ", PersianNormalizer.Tokenize(word)) : word;

    /// <summary>One prepared key against one reading of the text.</summary>
    /// <remarks>
    /// A whole-word key arrives already padded with spaces and is looked for in the padded word
    /// list. A substring key is looked for in the reading, and in the word list so single letters
    /// joined into a word still count.
    /// </remarks>
    private static bool Matches(string searchKey, bool wholeWord, string reading, string words) =>
        wholeWord
            ? words.IndexOf(searchKey, StringComparison.Ordinal) >= 0
            : reading.IndexOf(searchKey, StringComparison.Ordinal) >= 0
              || words.IndexOf(searchKey, StringComparison.Ordinal) >= 0;

    /// <summary>
    /// Digits, symbols and look-alike letters to the Latin letters they stand for; filler dropped.
    /// </summary>
    /// <remarks>
    /// Harmless on Persian script, which has no Latin letters to fold: a digit becoming a letter
    /// in «رنک 4» makes a token no entry contains. It earns its keep on Finglish and English.
    /// <c>!</c> is folded before tokenizing because it is also a sentence separator, and left to
    /// the tokenizer <c>sh!t</c> would be two tokens.
    /// </remarks>
    internal static string Fold(string normalized)
    {
        var sb = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            var mapped = c switch
            {
                '0' => 'o',
                '1' or '!' or '|' or '¡' => 'i',
                '3' or '€' => 'e',
                '4' or '@' => 'a',
                '5' or '$' => 's',
                '7' => 't',

                // Cyrillic, already lower-cased by normalization.
                'а' => 'a', 'в' => 'b', 'е' => 'e', 'ё' => 'e', 'к' => 'k', 'м' => 'm',
                'н' => 'h', 'о' => 'o', 'р' => 'p', 'с' => 'c', 'т' => 't', 'у' => 'y',
                'х' => 'x', 'і' => 'i', 'ј' => 'j', 'ѕ' => 's', 'ԁ' => 'd', 'һ' => 'h',

                // Greek.
                'α' => 'a', 'ε' => 'e', 'ι' => 'i', 'κ' => 'k', 'ν' => 'v', 'ο' => 'o',
                'ρ' => 'p', 'τ' => 't', 'υ' => 'u', 'χ' => 'x',

                _ => c
            };

            // Filler: symbols with no letter to stand for, typed inside a word to break it up.
            if (mapped is '*' or '+' or '~' or '^' or '`' or '=' or '<' or '>' or '#' or '%' or '&' or '•' or '·' or '♥' or '❤')
            {
                continue;
            }

            sb.Append(mapped);
        }

        return sb.ToString();
    }

    /// <summary>Every run of a repeated letter down to one: "fuuck" to "fuck".</summary>
    internal static string Squeeze(string value)
    {
        var sb = new StringBuilder(value.Length);
        var previous = '\0';

        foreach (var c in value)
        {
            if (c != previous || c == ' ')
            {
                sb.Append(c);
            }

            previous = c;
        }

        return sb.ToString();
    }

    /// <summary>Runs of two or more single-letter tokens become one token: "f u c k" is "fuck".</summary>
    internal static IEnumerable<string> JoinSingleLetters(string[] tokens)
    {
        var run = new StringBuilder();

        foreach (var token in tokens)
        {
            if (token.Length == 1 && char.IsLetter(token[0]))
            {
                run.Append(token);
                continue;
            }

            if (run.Length > 0)
            {
                yield return run.ToString();
                run.Clear();
            }

            yield return token;
        }

        if (run.Length > 0)
        {
            yield return run.ToString();
        }
    }

    /// <param name="Word">The entry as the caller gave it, reported when it matches.</param>
    /// <param name="SearchKey">What is searched for; whole-word keys are padded with spaces up front.</param>
    /// <param name="FoldedSearchKey">The same for the folded spelling, or null when folding changes nothing.</param>
    /// <param name="WholeWord">Whether the key must match whole words.</param>
    private readonly record struct Entry(BannedWord Word, string SearchKey, string? FoldedSearchKey, bool WholeWord)
    {
        // Padding once here rather than per check: building " key " inside the loop allocated a
        // string for every entry, for every reading, for every message.
        public static Entry Create(BannedWord word, string key, string foldedKey, bool wholeWord)
        {
            var searchKey = wholeWord ? " " + key + " " : key;
            string? foldedSearchKey = null;
            if (foldedKey.Length > 0 && foldedKey != key)
            {
                foldedSearchKey = wholeWord ? " " + foldedKey + " " : foldedKey;
            }

            return new Entry(word, searchKey, foldedSearchKey, wholeWord);
        }
    }
}
