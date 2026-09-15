namespace PersianTextGuard;

/// <summary>Reads word lists for <see cref="ProfanityFilter"/>.</summary>
/// <remarks>
/// The format is one entry per line: a word or phrase is matched as whole words, a leading
/// <c>~</c> matches it anywhere (inside longer words too), and lines starting with <c>#</c> are
/// comments.
/// </remarks>
public static class WordList
{
    private const string DefaultResourceName = "PersianTextGuard.WordLists.persian-default.txt";

    private static readonly Lazy<IReadOnlyList<BannedWord>> Default = new(LoadDefault);

    /// <summary>
    /// The bundled Persian, Finglish and English list: about 400 entries, curated to avoid
    /// flagging ordinary words. Nothing uses it unless you pass it to a filter.
    /// </summary>
    /// <remarks>
    /// The file itself documents what is deliberately left out and why (for example «کس», which
    /// also means "person"). Combine it with your own entries, or filter out entries that do not
    /// suit your community.
    /// </remarks>
    public static IReadOnlyList<BannedWord> PersianDefault => Default.Value;

    /// <summary>Parses a word list from its text.</summary>
    public static IReadOnlyList<BannedWord> Parse(string text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        var words = new List<BannedWord>();

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            var anywhere = line[0] == '~';
            var word = (anywhere ? line.Substring(1) : line).Trim();

            if (word.Length > 0)
            {
                words.Add(new BannedWord(word, anywhere ? WordMatchMode.Anywhere : WordMatchMode.WholeWord));
            }
        }

        return words;
    }

    /// <summary>Reads and parses a word list from a UTF-8 stream.</summary>
    public static IReadOnlyList<BannedWord> Load(Stream stream)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    private static IReadOnlyList<BannedWord> LoadDefault()
    {
        using var stream = typeof(WordList).Assembly.GetManifestResourceStream(DefaultResourceName)
                           ?? throw new InvalidOperationException($"Embedded word list '{DefaultResourceName}' is missing.");
        return Load(stream);
    }
}
