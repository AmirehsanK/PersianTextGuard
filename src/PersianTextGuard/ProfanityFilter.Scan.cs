using System.Globalization;
using System.Text;

namespace PersianTextGuard;

/// <summary>The lossy forms of a message the filter searches, in the order it searches them.</summary>
internal enum ReadingKind
{
    /// <summary>Comparison-normalized.</summary>
    Normalized,

    /// <summary>Normalized, with repeated letters squeezed to one.</summary>
    Squeezed,

    /// <summary>Normalized, with look-alike characters folded.</summary>
    Folded,

    /// <summary>Normalized, folded, then squeezed.</summary>
    FoldedSqueezed
}

/// <summary>A word token of a reading and where it sits in that reading.</summary>
/// <param name="Text">The token.</param>
/// <param name="Start">Its first character in the reading.</param>
/// <param name="End">One past its last character in the reading.</param>
internal readonly record struct Token(string Text, int Start, int End);

/// <summary>A banned word found in one reading, before it is mapped back to the message.</summary>
/// <param name="Reading">Which reading it was found in, so the right source map is used.</param>
/// <param name="Start">Its first character in that reading.</param>
/// <param name="End">One past its last character in that reading.</param>
/// <param name="Word">The entry that matched.</param>
/// <param name="Order">The entry's position in the word list, for breaking ties.</param>
/// <param name="Evasion">What had to be undone to find it.</param>
internal readonly record struct Hit(ReadingKind Reading, int Start, int End, BannedWord Word, int Order, EvasionKind Evasion);

public sealed partial class ProfanityFilter
{
    // Longest first, so «هایی» is tried before «ها».
    private static readonly string[] PersianSuffixes =
        ["هاشون", "هاتون", "هامون", "هایی", "های", "هاش", "هات", "هام", "ها", "تون", "شون", "مون", "ای", "یی", "اش", "ات", "ام", "ا"];

    private static readonly char[] LatinBaseLetters = BuildLatinBaseLetters();

    /// <summary>
    /// Searches <paramref name="text"/> for banned words. With <paramref name="all"/> null it stops at
    /// the first hit and returns it in <paramref name="first"/>; otherwise it appends every hit to
    /// <paramref name="all"/>. Either way it returns whether anything was found.
    /// </summary>
    /// <remarks>
    /// Every capability goes through here, so they cannot disagree about whether a message is clean.
    /// The order is the one <see cref="FindMatch"/> has always used — most literal reading first, and
    /// within a reading tokens and phrases, anywhere entries, joined single letters, a word split
    /// once — so the first hit is the least evasion needed.
    /// </remarks>
    private bool Scan(string? text, List<Hit>? all, out Hit first)
    {
        first = default;
        if (string.IsNullOrWhiteSpace(text) || Count == 0)
        {
            return false;
        }

        var normalized = PersianNormalizer.Normalize(text);
        var folded = _options.FoldLookalikeCharacters ? Fold(normalized) : normalized;

        var readings = new List<(string Text, ReadingKind Kind, EvasionKind Evasion)>(4)
        {
            (normalized, ReadingKind.Normalized, EvasionKind.None)
        };

        if (_options.SqueezeRepeatedLetters)
        {
            readings.Add((Squeeze(normalized), ReadingKind.Squeezed, EvasionKind.RepeatedLetters));
        }

        if (_options.FoldLookalikeCharacters)
        {
            readings.Add((folded, ReadingKind.Folded, EvasionKind.LookalikeCharacters));
            if (_options.SqueezeRepeatedLetters)
            {
                readings.Add((Squeeze(folded), ReadingKind.FoldedSqueezed, EvasionKind.LookalikeCharacters | EvasionKind.RepeatedLetters));
            }
        }

        var checkedReadings = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (reading, kind, evasion) in readings)
        {
            // On ordinary text most readings are the same string.
            if (!checkedReadings.Add(reading))
            {
                continue;
            }

            var tokens = PersianNormalizer.TokenizeWithOffsets(reading);
            if (MatchTokens(tokens, kind, evasion, all, ref first)
                || MatchAnywhere(reading, map: null, kind, evasion, all, ref first))
            {
                return true;
            }

            if (_options.JoinSpacedLetters)
            {
                var split = evasion | EvasionKind.SplitWord;

                if (TryJoinSingleLetters(tokens, out var joined, out var joinedText, out var joinedMap)
                    && (MatchTokens(joined, kind, split, all, ref first)
                        || MatchAnywhere(joinedText, joinedMap, kind, split, all, ref first)))
                {
                    return true;
                }

                if (MatchSplitHalves(tokens, kind, split, all, ref first))
                {
                    return true;
                }
            }
        }

        return MatchBrokenChunks(normalized, all, ref first) || all is { Count: > 0 };
    }

    /// <summary>
    /// Records a hit. Returns true when the scan should stop, which is only when it wants the first.
    /// </summary>
    private static bool Report(in Hit hit, List<Hit>? all, ref Hit first)
    {
        if (all is null)
        {
            first = hit;
            return true;
        }

        all.Add(hit);
        return false;
    }

    private bool MatchTokens(Token[] tokens, ReadingKind kind, EvasionKind evasion, List<Hit>? all, ref Hit first)
    {
        for (var i = 0; i < tokens.Length; i++)
        {
            if (TryFindWord(tokens[i].Text, out var word, out var order)
                && Report(new Hit(kind, tokens[i].Start, tokens[i].End, word, order, evasion), all, ref first))
            {
                return true;
            }

            if (!_phrases.TryGetValue(tokens[i].Text, out var phrases))
            {
                continue;
            }

            foreach (var phrase in phrases)
            {
                if (PhraseStartsAt(tokens, i, phrase.Tokens)
                    && Report(new Hit(kind, tokens[i].Start, tokens[i + phrase.Tokens.Length - 1].End, phrase.Word, phrase.Order, evasion), all, ref first))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Anywhere entries inside <paramref name="haystack"/>. When the haystack is not the reading
    /// itself, <paramref name="map"/> gives each of its characters' position in the reading.
    /// </summary>
    private bool MatchAnywhere(string haystack, int[]? map, ReadingKind kind, EvasionKind evasion, List<Hit>? all, ref Hit first)
    {
        foreach (var key in _anywhere)
        {
            var index = haystack.IndexOf(key.Text, StringComparison.Ordinal);

            while (index >= 0)
            {
                var last = index + key.Text.Length - 1;
                var start = map is null ? index : map[index];
                var end = map is null ? last + 1 : map[last] + 1;

                if (Report(new Hit(kind, start, end, key.Word, key.Order, evasion), all, ref first))
                {
                    return true;
                }

                index = haystack.IndexOf(key.Text, index + 1, StringComparison.Ordinal);
            }
        }

        return false;
    }

    /// <summary>
    /// Runs of two or more single-letter tokens become one token: "f u c k" is "fuck". Returns false
    /// when there is no such run. <paramref name="text"/> is the joined tokens separated by spaces,
    /// and <paramref name="map"/> gives each of its characters' position in the reading.
    /// </summary>
    internal static bool TryJoinSingleLetters(Token[] tokens, out Token[] joined, out string text, out int[] map)
    {
        var hasRun = false;
        for (var i = 0; i + 1 < tokens.Length && !hasRun; i++)
        {
            hasRun = IsSingleLetter(tokens[i]) && IsSingleLetter(tokens[i + 1]);
        }

        if (!hasRun)
        {
            joined = tokens;
            text = string.Empty;
            map = [];
            return false;
        }

        var result = new List<Token>(tokens.Length);
        var sb = new StringBuilder();
        var positions = new List<int>();
        var run = new StringBuilder();
        var runPositions = new List<int>();
        var runStart = 0;
        var runEnd = 0;

        void Append(string value, List<int> valuePositions)
        {
            if (sb.Length > 0)
            {
                sb.Append(' ');
                positions.Add(positions[positions.Count - 1]);
            }

            sb.Append(value);
            positions.AddRange(valuePositions);
        }

        void FlushRun()
        {
            if (run.Length == 0)
            {
                return;
            }

            var value = run.ToString();
            result.Add(new Token(value, runStart, runEnd));
            Append(value, runPositions);
            run.Clear();
            runPositions.Clear();
        }

        foreach (var token in tokens)
        {
            if (IsSingleLetter(token))
            {
                if (run.Length == 0)
                {
                    runStart = token.Start;
                }

                run.Append(token.Text);
                runPositions.Add(token.Start);
                runEnd = token.End;
                continue;
            }

            FlushRun();
            result.Add(token);

            var tokenPositions = new List<int>(token.Text.Length);
            for (var k = 0; k < token.Text.Length; k++)
            {
                tokenPositions.Add(token.Start + k);
            }

            Append(token.Text, tokenPositions);
        }

        FlushRun();

        joined = result.ToArray();
        text = sb.ToString();
        map = positions.ToArray();
        return true;
    }

    private static bool IsSingleLetter(in Token token) => token.Text.Length == 1 && char.IsLetter(token.Text[0]);

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
    private bool MatchSplitHalves(Token[] tokens, ReadingKind kind, EvasionKind evasion, List<Hit>? all, ref Hit first)
    {
        for (var i = 0; i + 1 < tokens.Length; i++)
        {
            var firstHalf = tokens[i].Text;
            var secondHalf = tokens[i + 1].Text;
            var length = firstHalf.Length + secondHalf.Length;

            var latin = firstHalf.Length >= 2 && secondHalf.Length >= 2 && length >= MinimumBrokenWordLength
                        && IsLatinWord(firstHalf) && IsLatinWord(secondHalf);
            var persian = (firstHalf.Length == 1) != (secondHalf.Length == 1) && length >= 3
                          && firstHalf != "و" && secondHalf != "و"
                          && IsPersianLetter(firstHalf[0]) && IsPersianLetter(secondHalf[0]);

            if (!latin && !persian)
            {
                continue;
            }

            var joined = firstHalf + secondHalf;
            if (_words.TryGetValue(joined, out var entry)
                && Report(new Hit(kind, tokens[i].Start, tokens[i + 1].End, entry.Word, entry.Order, evasion), all, ref first))
            {
                return true;
            }

            foreach (var key in _anywhere)
            {
                if (key.Text == joined
                    && Report(new Hit(kind, tokens[i].Start, tokens[i + 1].End, key.Word, key.Order, evasion), all, ref first))
                {
                    return true;
                }
            }
        }

        return false;
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
    private bool MatchBrokenChunks(string normalized, List<Hit>? all, ref Hit first)
    {
        if (!_options.JoinSpacedLetters && !_options.FoldLookalikeCharacters)
        {
            return false;
        }

        var chunkStart = 0;

        for (var i = 0; i <= normalized.Length; i++)
        {
            if (i < normalized.Length && normalized[i] != ' ')
            {
                continue;
            }

            var offset = chunkStart;
            var chunk = normalized.Substring(chunkStart, i - chunkStart);
            chunkStart = i + 1;

            var pattern = MaskedPattern(chunk, out var masks, out var letters, out var trimmedStart, out var trimmedEnd);
            if (pattern is null || masks == 0 || letters == 0)
            {
                continue;
            }

            var start = offset + trimmedStart;
            var end = offset + trimmedEnd;

            if (_options.JoinSpacedLetters)
            {
                var stripped = pattern.Replace("\0", string.Empty);
                if (stripped.Length >= 3
                    && TryFindWord(stripped, out var word, out var order)
                    && Report(new Hit(ReadingKind.Normalized, start, end, word, order, EvasionKind.SplitWord), all, ref first))
                {
                    return true;
                }
            }

            if (!_options.FoldLookalikeCharacters)
            {
                continue;
            }

            foreach (var key in _maskable)
            {
                var fits = key.WholeWord
                    ? key.Text.Length == pattern.Length && FitsMask(pattern, 0, key.Text)
                    : FitsMaskAnywhere(pattern, key.Text);

                if (fits && Report(new Hit(ReadingKind.Normalized, start, end, key.Word, key.Order, EvasionKind.LookalikeCharacters), all, ref first))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// The chunk with outer punctuation trimmed, letters and digits folded, combining marks
    /// dropped, and every other character replaced by <c>'\0'</c>. Null when nothing is left.
    /// <paramref name="trimmedStart"/> and <paramref name="trimmedEnd"/> give the part of the chunk
    /// the pattern was built from.
    /// </summary>
    private static string? MaskedPattern(string chunk, out int masks, out int letters, out int trimmedStart, out int trimmedEnd)
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

        trimmedStart = start;
        trimmedEnd = end;

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
    private bool TryFindWord(string token, out BannedWord word, out int order)
    {
        if (_words.TryGetValue(token, out var entry))
        {
            (word, order) = entry;
            return true;
        }

        word = default;
        order = 0;

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

            // «جندها»: the final heh of «جنده» is often dropped before «ها».
            if (_words.TryGetValue(stem, out entry)
                || (suffix.StartsWith("ها", StringComparison.Ordinal) && _words.TryGetValue(stem + "ه", out entry)))
            {
                (word, order) = entry;
                return true;
            }
        }

        return false;
    }

    private static bool PhraseStartsAt(Token[] tokens, int start, string[] phrase)
    {
        if (start + phrase.Length > tokens.Length)
        {
            return false;
        }

        for (var k = 1; k < phrase.Length - 1; k++)
        {
            if (tokens[start + k].Text != phrase[k])
            {
                return false;
            }
        }

        // The last word of a Persian phrase takes suffixes like a single word does: «بی ناموس‌ها».
        var last = tokens[start + phrase.Length - 1].Text;
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
    internal static string Fold(string normalized) => Fold(normalized, map: null);

    /// <summary><see cref="Fold(string)"/>, recording for each output character its index in the input.</summary>
    internal static string Fold(string normalized, List<int>? map)
    {
        var sb = new StringBuilder(normalized.Length);
        var i = 0;

        while (i < normalized.Length)
        {
            if (IsRunBoundary(normalized, i))
            {
                sb.Append(normalized[i]);
                map?.Add(i);
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
                        map?.Add(i);
                    }

                    i++;
                    continue;
                }

                if (IsFiller(c))
                {
                    continue;
                }

                sb.Append(FoldCharacter(c, hasLetter));
                map?.Add(i);
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
    internal static string Squeeze(string value) => Squeeze(value, map: null);

    /// <summary><see cref="Squeeze(string)"/>, recording for each output character its index in the input.</summary>
    internal static string Squeeze(string value, List<int>? map)
    {
        var sb = new StringBuilder(value.Length);
        var previous = '\0';

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c != previous || c == ' ')
            {
                sb.Append(c);
                map?.Add(i);
            }

            previous = c;
        }

        return sb.ToString();
    }
}
