using System.Globalization;
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
public sealed class ProfanityFilter
{
    // A word split in two or masked with symbols is only matched against entries at least this
    // long: shorter ones ("ass", «کون») turn up by accident in ordinary text broken that way.
    private const int MinimumBrokenWordLength = 4;

    // Longest first, so «هایی» is tried before «ها».
    private static readonly string[] PersianSuffixes =
        ["هاشون", "هاتون", "هامون", "هایی", "های", "هاش", "هات", "هام", "ها", "تون", "شون", "مون", "ای", "یی", "اش", "ات", "ام", "ا"];

    private static readonly char[] LatinBaseLetters = BuildLatinBaseLetters();

    private readonly ProfanityFilterOptions _options;
    private readonly Dictionary<string, BannedWord> _words = new(StringComparer.Ordinal);
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

        foreach (var word in words)
        {
            var normalized = PersianNormalizer.Normalize(word.Text);
            if (normalized.Length == 0 || !seen.Add((normalized, word.Mode)))
            {
                continue;
            }

            var added = Add(normalized, word, anywhereKeys);

            // The text is folded before matching, so an entry stored the way an evader types it
            // ("k0s", pasted from the message a moderator was reading) would never match the
            // plain spelling the folded text turns into. Keep a folded key beside the original.
            if (_options.FoldLookalikeCharacters)
            {
                added |= Add(Fold(normalized), word, anywhereKeys);
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
    public bool ContainsProfanity(string? text) => FindMatch(text) is not null;

    /// <summary>The first banned word found in <paramref name="text"/>, or null when it is clean.</summary>
    public ProfanityMatch? FindMatch(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || Count == 0)
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

            var tokens = PersianNormalizer.Tokenize(reading);
            var match = MatchTokens(tokens, evasion) ?? MatchAnywhere(reading, evasion);
            if (match is not null)
            {
                return match;
            }

            if (_options.JoinSpacedLetters)
            {
                var split = evasion | EvasionKind.SplitWord;
                var joined = JoinSingleLetters(tokens).ToArray();

                if (joined.Length != tokens.Length)
                {
                    match = MatchTokens(joined, split) ?? MatchAnywhere(string.Join(" ", joined), split);
                }

                match ??= MatchSplitHalves(tokens, split);
                if (match is not null)
                {
                    return match;
                }
            }
        }

        return MatchBrokenChunks(normalized);
    }

    private bool Add(string form, BannedWord word, HashSet<string> anywhereKeys)
    {
        if (word.Mode == WordMatchMode.Anywhere)
        {
            if (form.Length == 0)
            {
                return false;
            }

            if (anywhereKeys.Add(form))
            {
                var key = new Key(form, word, WholeWord: false);
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
                _words.Add(tokens[0], word);
                if (tokens[0].Length >= MinimumBrokenWordLength)
                {
                    _maskable.Add(new Key(tokens[0], word, WholeWord: true));
                }
            }

            return true;
        }

        if (!_phrases.TryGetValue(tokens[0], out var phrases))
        {
            _phrases.Add(tokens[0], phrases = []);
        }

        phrases.Add(new Phrase(tokens, word));
        return true;
    }

    private ProfanityMatch? MatchTokens(string[] tokens, EvasionKind evasion)
    {
        for (var i = 0; i < tokens.Length; i++)
        {
            if (TryFindWord(tokens[i], out var word))
            {
                return new ProfanityMatch(word, evasion);
            }

            if (!_phrases.TryGetValue(tokens[i], out var phrases))
            {
                continue;
            }

            foreach (var phrase in phrases)
            {
                if (PhraseStartsAt(tokens, i, phrase.Tokens))
                {
                    return new ProfanityMatch(phrase.Word, evasion);
                }
            }
        }

        return null;
    }

    private ProfanityMatch? MatchAnywhere(string text, EvasionKind evasion)
    {
        foreach (var key in _anywhere)
        {
            if (text.IndexOf(key.Text, StringComparison.Ordinal) >= 0)
            {
                return new ProfanityMatch(key.Word, evasion);
            }
        }

        return null;
    }

    /// <summary>
    /// A word split once: "fu ck" in Latin letters, «کی ر» in Persian. Only when the halves join
    /// into exactly an entry, so "push it" never becomes "pushit" and matches "shit".
    /// </summary>
    /// <remarks>
    /// Two Latin halves need at least two letters each ("it's hit" is not "shit"). Persian is
    /// the other way round: two real words joined make ordinary phrases look like insults
    /// («هر کس ده تا» holds «کسده»), but a stray single letter next to a word is a split. «و»
    /// ("and") is the one Persian letter that stands alone in ordinary text.
    /// </remarks>
    private ProfanityMatch? MatchSplitHalves(string[] tokens, EvasionKind evasion)
    {
        for (var i = 0; i + 1 < tokens.Length; i++)
        {
            var first = tokens[i];
            var second = tokens[i + 1];
            var length = first.Length + second.Length;

            var latin = first.Length >= 2 && second.Length >= 2 && length >= MinimumBrokenWordLength
                        && IsLatinWord(first) && IsLatinWord(second);
            var persian = (first.Length == 1) != (second.Length == 1) && length >= 3
                          && first != "و" && second != "و"
                          && IsPersianLetter(first[0]) && IsPersianLetter(second[0]);

            if (!latin && !persian)
            {
                continue;
            }

            var joined = first + second;
            if (_words.TryGetValue(joined, out var word))
            {
                return new ProfanityMatch(word, evasion);
            }

            foreach (var key in _anywhere)
            {
                if (key.Text == joined)
                {
                    return new ProfanityMatch(key.Word, evasion);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Words broken up by punctuation or symbols inside them: «ج.نده», "kos_kesh", "f**k",
    /// "c*nt", "f@ck", "a$$hole".
    /// </summary>
    /// <remarks>
    /// The whole-word reading splits these into pieces, and dropping the symbols only helps
    /// when they were added rather than typed in place of a letter. So each space-separated
    /// chunk is tried twice: with the symbols removed, and with each symbol standing for any one
    /// letter. A mask only matches entries of <see cref="MinimumBrokenWordLength"/> or more
    /// letters with at least half of them showing, which is how people censor a word they still
    /// want read.
    /// </remarks>
    private ProfanityMatch? MatchBrokenChunks(string normalized)
    {
        if (!_options.JoinSpacedLetters && !_options.FoldLookalikeCharacters)
        {
            return null;
        }

        foreach (var chunk in normalized.Split(' '))
        {
            var pattern = MaskedPattern(chunk, out var masks, out var letters);
            if (pattern is null || masks == 0 || letters == 0)
            {
                continue;
            }

            if (_options.JoinSpacedLetters)
            {
                var stripped = pattern.Replace("\0", string.Empty);
                if (stripped.Length >= 3 && TryFindWord(stripped, out var word))
                {
                    return new ProfanityMatch(word, EvasionKind.SplitWord);
                }
            }

            if (!_options.FoldLookalikeCharacters)
            {
                continue;
            }

            foreach (var key in _maskable)
            {
                if (key.WholeWord
                        ? key.Text.Length == pattern.Length && FitsMask(pattern, 0, key.Text)
                        : FitsMaskAnywhere(pattern, key.Text))
                {
                    return new ProfanityMatch(key.Word, EvasionKind.LookalikeCharacters);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The chunk with outer punctuation trimmed, letters and digits folded, combining marks
    /// dropped, and every other character replaced by <c>'\0'</c>. Null when nothing is left.
    /// </summary>
    private static string? MaskedPattern(string chunk, out int masks, out int letters)
    {
        masks = 0;
        letters = 0;

        var start = 0;
        var end = chunk.Length;
        while (start < end && !char.IsLetterOrDigit(chunk, start))
        {
            start++;
        }

        while (end > start && !char.IsLetterOrDigit(chunk, end - 1))
        {
            end--;
        }

        if (end - start < 3)
        {
            return null;
        }

        var sb = new StringBuilder(end - start);

        for (var i = start; i < end; i++)
        {
            var c = chunk[i];

            if (char.IsHighSurrogate(c) && i + 1 < end && char.IsLowSurrogate(chunk[i + 1]))
            {
                var letter = EnclosedLetter(char.ConvertToUtf32(c, chunk[i + 1]));
                i++;
                if (letter != '\0')
                {
                    sb.Append(letter);
                    letters++;
                }
                else
                {
                    sb.Append('\0');
                    masks++;
                }

                continue;
            }

            if (char.IsLetterOrDigit(c))
            {
                sb.Append(FoldCharacter(c, mapDigits: true));
                if (char.IsLetter(c))
                {
                    letters++;
                }

                continue;
            }

            switch (CharUnicodeInfo.GetUnicodeCategory(c))
            {
                case UnicodeCategory.NonSpacingMark:
                case UnicodeCategory.SpacingCombiningMark:
                case UnicodeCategory.EnclosingMark:
                case UnicodeCategory.Format:
                    continue;
                default:
                    sb.Append('\0');
                    masks++;
                    break;
            }
        }

        return sb.ToString();
    }

    private static bool FitsMaskAnywhere(string pattern, string key)
    {
        for (var offset = 0; offset + key.Length <= pattern.Length; offset++)
        {
            if (FitsMask(pattern, offset, key))
            {
                return true;
            }
        }

        return false;
    }

    private static bool FitsMask(string pattern, int offset, string key)
    {
        var masks = 0;

        for (var i = 0; i < key.Length; i++)
        {
            var c = pattern[offset + i];
            if (c == '\0')
            {
                masks++;
            }
            else if (c != key[i])
            {
                return false;
            }
        }

        return masks > 0 && masks * 2 <= key.Length;
    }

    /// <summary>An entry for the token, as written or with a Persian suffix attached.</summary>
    private bool TryFindWord(string token, out BannedWord word)
    {
        if (_words.TryGetValue(token, out word))
        {
            return true;
        }

        if (token.Length < 4 || !IsPersianLetter(token[0]))
        {
            return false;
        }

        foreach (var suffix in PersianSuffixes)
        {
            if (!HasSuffix(token, suffix))
            {
                continue;
            }

            var stem = token.Substring(0, token.Length - suffix.Length);
            if (_words.TryGetValue(stem, out word))
            {
                return true;
            }

            // «جندها»: the final heh of «جنده» is often dropped before «ها».
            if (suffix.StartsWith("ها", StringComparison.Ordinal) && _words.TryGetValue(stem + "ه", out word))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PhraseStartsAt(string[] tokens, int start, string[] phrase)
    {
        if (start + phrase.Length > tokens.Length)
        {
            return false;
        }

        for (var k = 1; k < phrase.Length - 1; k++)
        {
            if (tokens[start + k] != phrase[k])
            {
                return false;
            }
        }

        // The last word of a Persian phrase takes suffixes like a single word does: «بی ناموس‌ها».
        var last = tokens[start + phrase.Length - 1];
        var key = phrase[phrase.Length - 1];
        if (last == key)
        {
            return true;
        }

        if (!IsPersianLetter(key[0]) || last.Length <= key.Length || !last.StartsWith(key, StringComparison.Ordinal))
        {
            return false;
        }

        var suffix = last.Substring(key.Length);
        return Array.IndexOf(PersianSuffixes, suffix) >= 0 && HasSuffix(last, suffix);
    }

    /// <summary>A one-letter suffix needs a four-letter stem: «کیرا» is also the name Kira.</summary>
    private static bool HasSuffix(string token, string suffix) =>
        token.EndsWith(suffix, StringComparison.Ordinal)
        && token.Length - suffix.Length >= (suffix.Length == 1 ? 4 : 3);

    private static bool IsPersianLetter(char c) => c >= '؀' && c <= 'ۿ' && char.IsLetter(c);

    private static bool IsLatinWord(string token)
    {
        foreach (var c in token)
        {
            if (c is not (>= 'a' and <= 'z'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Digits, symbols, accented and look-alike letters to the Latin letters they stand for;
    /// filler and emoji dropped.
    /// </summary>
    /// <remarks>
    /// Harmless on Persian script, which has no Latin letters to fold: a digit becoming a letter
    /// in «رنک 4» makes a token no entry contains. It earns its keep on Finglish and English.
    /// <c>!</c> is folded before tokenizing because it is also a sentence separator, and left to
    /// the tokenizer <c>sh!t</c> would be two tokens. Digits are only read as letters in a run
    /// that has letters in it: "sh1t" and "4ss" are words, "455" and «۴۵۵ تومان» are numbers.
    /// </remarks>
    internal static string Fold(string normalized)
    {
        var sb = new StringBuilder(normalized.Length);
        var i = 0;

        while (i < normalized.Length)
        {
            if (IsRunBoundary(normalized, i))
            {
                sb.Append(normalized[i]);
                i++;
                continue;
            }

            var end = i;
            var hasLetter = false;
            while (end < normalized.Length && !IsRunBoundary(normalized, end))
            {
                hasLetter |= char.IsLetter(normalized[end])
                             || (char.IsHighSurrogate(normalized[end]) && end + 1 < normalized.Length
                                 && EnclosedLetter(char.ConvertToUtf32(normalized[end], normalized[end + 1])) != '\0');
                end++;
            }

            for (; i < end; i++)
            {
                var c = normalized[i];

                if (char.IsHighSurrogate(c) && i + 1 < end && char.IsLowSurrogate(normalized[i + 1]))
                {
                    // Enclosed and regional-indicator letters (🅵🆄🅲🅺) read as letters; emoji are filler.
                    var letter = EnclosedLetter(char.ConvertToUtf32(c, normalized[i + 1]));
                    if (letter != '\0')
                    {
                        sb.Append(letter);
                    }

                    i++;
                    continue;
                }

                if (IsFiller(c))
                {
                    continue;
                }

                sb.Append(FoldCharacter(c, hasLetter));
            }
        }

        return sb.ToString();
    }

    private static char FoldCharacter(char c, bool mapDigits) => c switch
    {
        '0' when mapDigits => 'o',
        '1' when mapDigits => 'i',
        '3' when mapDigits => 'e',
        '4' when mapDigits => 'a',
        '5' when mapDigits => 's',
        '7' when mapDigits => 't',
        '8' when mapDigits => 'b',
        '!' or '|' or '¡' => 'i',
        '€' => 'e',
        '@' => 'a',
        '$' => 's',

        // Cyrillic, already lower-cased by normalization.
        'а' => 'a', 'в' => 'b', 'е' => 'e', 'ё' => 'e', 'к' => 'k', 'м' => 'm',
        'н' => 'h', 'о' => 'o', 'р' => 'p', 'с' => 'c', 'т' => 't', 'у' => 'y',
        'х' => 'x', 'і' => 'i', 'ј' => 'j', 'ѕ' => 's', 'ԁ' => 'd', 'һ' => 'h',

        // Greek.
        'α' => 'a', 'ε' => 'e', 'ι' => 'i', 'κ' => 'k', 'ν' => 'v', 'ο' => 'o',
        'ρ' => 'p', 'τ' => 't', 'υ' => 'u', 'χ' => 'x',

        // Accented Latin: fück, shíť, ƒuck.
        >= 'À' and <= 'ɏ' => LatinBaseLetters[c - 'À'],

        _ => c
    };

    /// <summary>
    /// Filler: symbols with no letter to stand for, typed inside a word to break it up, and
    /// combining marks stacked on letters to decorate them (f̶u̶c̶k̶).
    /// </summary>
    private static bool IsFiller(char c)
    {
        if (c is '*' or '+' or '~' or '^' or '`' or '=' or '<' or '>' or '#' or '%' or '&' or '•' or '·' or '♥' or '❤')
        {
            return true;
        }

        if (c < 128 || char.IsLetterOrDigit(c))
        {
            return false;
        }

        var category = CharUnicodeInfo.GetUnicodeCategory(c);
        return category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark
            or UnicodeCategory.OtherSymbol or UnicodeCategory.ModifierSymbol;
    }

    /// <summary>
    /// Where a run of word-like characters ends, for deciding whether its digits are letters.
    /// Symbols that fold to letters, filler and emoji belong to the run; spaces and punctuation
    /// end it.
    /// </summary>
    private static bool IsRunBoundary(string text, int index)
    {
        var c = text[index];
        if (char.IsWhiteSpace(c))
        {
            return true;
        }

        if (char.IsLetterOrDigit(c) || char.IsSurrogate(c) || IsFiller(c)
            || c is '!' or '|' or '¡' or '€' or '@' or '$')
        {
            return false;
        }

        return CharUnicodeInfo.GetUnicodeCategory(c) switch
        {
            UnicodeCategory.ConnectorPunctuation or UnicodeCategory.DashPunctuation
                or UnicodeCategory.OpenPunctuation or UnicodeCategory.ClosePunctuation
                or UnicodeCategory.InitialQuotePunctuation or UnicodeCategory.FinalQuotePunctuation
                or UnicodeCategory.OtherPunctuation or UnicodeCategory.MathSymbol
                or UnicodeCategory.CurrencySymbol => true,
            _ => false
        };
    }

    /// <summary>
    /// The Latin letter a squared, circled or regional-indicator letter shows, or <c>'\0'</c>.
    /// NFKC already folds the ones with a compatibility mapping (Ⓐ, 𝐟); these have none.
    /// </summary>
    private static char EnclosedLetter(int codePoint) => codePoint switch
    {
        >= 0x1F130 and <= 0x1F149 => (char)('a' + (codePoint - 0x1F130)), // 🄰 squared
        >= 0x1F150 and <= 0x1F169 => (char)('a' + (codePoint - 0x1F150)), // 🅐 negative circled
        >= 0x1F170 and <= 0x1F189 => (char)('a' + (codePoint - 0x1F170)), // 🅰 negative squared
        >= 0x1F1E6 and <= 0x1F1FF => (char)('a' + (codePoint - 0x1F1E6)), // 🇦 regional indicator
        _ => '\0'
    };

    private static char[] BuildLatinBaseLetters()
    {
        var table = new char[0x024F - 0x00C0 + 1];

        for (var i = 0; i < table.Length; i++)
        {
            var c = (char)(0x00C0 + i);
            var decomposed = c.ToString().Normalize(NormalizationForm.FormD);
            var first = decomposed[0];
            table[i] = first < 128 && char.IsLetter(first) ? char.ToLowerInvariant(first) : c;
        }

        // Letters with a stroke or hook have no decomposition.
        foreach (var (letter, plain) in new[] { ('ø', 'o'), ('Ø', 'o'), ('đ', 'd'), ('Đ', 'd'), ('ł', 'l'), ('Ł', 'l'), ('ƒ', 'f'), ('ħ', 'h'), ('ı', 'i'), ('ß', 's') })
        {
            table[letter - 0x00C0] = plain;
        }

        return table;
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

    /// <param name="Text">What is searched for.</param>
    /// <param name="Word">The entry as the caller gave it, reported when it matches.</param>
    /// <param name="WholeWord">Whether the key must match a whole token.</param>
    private readonly record struct Key(string Text, BannedWord Word, bool WholeWord);

    /// <param name="Tokens">The phrase, one token per word.</param>
    /// <param name="Word">The entry as the caller gave it, reported when it matches.</param>
    private sealed record Phrase(string[] Tokens, BannedWord Word);
}
