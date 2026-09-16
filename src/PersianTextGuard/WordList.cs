namespace PersianTextGuard;

/// <summary>Reads word lists for <see cref="ProfanityFilter"/>.</summary>
/// <remarks>
/// <para>
/// The format is one entry per line: a word or phrase is matched as whole words, a leading
/// <c>~</c> matches it anywhere (inside longer words too), and lines starting with <c>#</c> are
/// comments.
/// </para>
/// <para>
/// A line like <c>[insult]</c> starts a section: every entry after it, until the next section,
/// gets that <see cref="WordCategory"/>. Entries before the first section are
/// <see cref="WordCategory.Uncategorized"/>.
/// </para>
/// </remarks>
public static class WordList
{
    private static readonly string[] BundledResources =
    [
        "PersianTextGuard.WordLists.persian.txt",
        "PersianTextGuard.WordLists.finglish.txt",
        "PersianTextGuard.WordLists.english.txt",
    ];

    private static readonly Lazy<IReadOnlyList<BannedWord>> AllBundled = new(LoadBundled);

    private static readonly Lazy<IReadOnlyList<BannedWord>> Default = new(
        () => AllBundled.Value.Where(w => w.Category != WordCategory.Mild).ToList());

    /// <summary>
    /// The bundled Persian, Finglish and English list without the <see cref="WordCategory.Mild"/>
    /// entries: profanity, sexual words, insults, slurs and harassment, curated to avoid flagging
    /// ordinary words. Nothing uses it unless you pass it to a filter.
    /// </summary>
    /// <remarks>
    /// The files themselves document what is deliberately left out and why (for example «کس»,
    /// which also means "person"). Use <see cref="Bundled"/> to pick categories, or combine the
    /// list with your own entries.
    /// </remarks>
    public static IReadOnlyList<BannedWord> PersianDefault => Default.Value;

    /// <summary>Every bundled entry, <see cref="WordCategory.Mild"/> included.</summary>
    public static IReadOnlyList<BannedWord> All => AllBundled.Value;

    /// <summary>The bundled entries in the given categories.</summary>
    /// <example>
    /// <code>
    /// // A children's site: block everything, mild words too.
    /// new ProfanityFilter(WordList.All);
    ///
    /// // A dating app: sexual words are fine, abuse is not.
    /// new ProfanityFilter(WordList.Bundled(WordCategory.Insult, WordCategory.Slur, WordCategory.Harassment));
    /// </code>
    /// </example>
    public static IReadOnlyList<BannedWord> Bundled(params WordCategory[] categories)
    {
        if (categories is null)
        {
            throw new ArgumentNullException(nameof(categories));
        }

        return AllBundled.Value.Where(w => Array.IndexOf(categories, w.Category) >= 0).ToList();
    }

    /// <summary>Parses a word list from its text.</summary>
    /// <exception cref="FormatException">A section names a category that does not exist.</exception>
    public static IReadOnlyList<BannedWord> Parse(string text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        var words = new List<BannedWord>();
        var category = WordCategory.Uncategorized;
        var lineNumber = 0;

        foreach (var raw in text.Split('\n'))
        {
            lineNumber++;
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            if (line.Length > 2 && line[0] == '[' && line[line.Length - 1] == ']')
            {
                var name = line.Substring(1, line.Length - 2).Trim();
                if (!Enum.TryParse(name, ignoreCase: true, out category) || !Enum.IsDefined(typeof(WordCategory), category))
                {
                    throw new FormatException($"Line {lineNumber}: unknown word category '{name}'.");
                }

                continue;
            }

            var anywhere = line[0] == '~';
            var word = (anywhere ? line.Substring(1) : line).Trim();

            if (word.Length > 0)
            {
                words.Add(new BannedWord(word, anywhere ? WordMatchMode.Anywhere : WordMatchMode.WholeWord) { Category = category });
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

    private static IReadOnlyList<BannedWord> LoadBundled()
    {
        var words = new List<BannedWord>();

        foreach (var name in BundledResources)
        {
            using var stream = typeof(WordList).Assembly.GetManifestResourceStream(name)
                               ?? throw new InvalidOperationException($"Embedded word list '{name}' is missing.");
            words.AddRange(Load(stream));
        }

        return words;
    }
}
