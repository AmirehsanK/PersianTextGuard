using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PersianTextGuard.Conformance;

/// <summary>A case read from a corpus file.</summary>
/// <remarks>Shared by the xUnit runner and the fill-in tool, so it has no xUnit dependency.</remarks>
public sealed class CorpusCase
{
    public CorpusCase(string id, string kind, string file, JsonObject json)
    {
        Id = id;
        Kind = kind;
        File = file;
        Json = json;
    }

    /// <summary>The case id, unique across the corpus.</summary>
    public string Id { get; }

    /// <summary>One of <see cref="Corpus.Kinds"/>.</summary>
    public string Kind { get; }

    /// <summary>The case file's name, such as <c>matching-persian.json</c>.</summary>
    public string File { get; }

    /// <summary>The case as it is in its file. The fill-in tool adds <c>expected</c> to it.</summary>
    public JsonObject Json { get; }

    /// <summary>A case without <c>expected</c> has not been recorded yet.</summary>
    public bool IsPending => !Json.ContainsKey("expected");

    public bool IsMatching => Kind is "ordinary" or "must-match" or "robustness";
}

/// <summary>A corpus case file and the cases it holds, in order.</summary>
public sealed class CorpusFile
{
    public CorpusFile(string path, JsonArray cases)
    {
        Path = path;
        Cases = cases;
    }

    public string Path { get; }

    public string Name => System.IO.Path.GetFileName(Path);

    /// <summary>The file's array. The fill-in tool writes it back after filling cases in.</summary>
    public JsonArray Cases { get; }
}

/// <summary>
/// The conformance corpus (<c>conformance/</c>): loading, building inputs, evaluating a case
/// against this port, and comparing results exactly.
/// </summary>
public sealed class Corpus
{
    /// <summary>The case kinds format version 1 defines.</summary>
    public static readonly string[] Kinds =
    [
        "ordinary", "must-match", "robustness", "normalization", "tokenization",
        "word-list-parsing", "category-selection", "mask-validation",
    ];

    /// <summary>The newest format this code understands.</summary>
    public const int SupportedFormatVersion = 1;

    private static readonly string[] EvasionNames = ["repeatedLetters", "lookalikeCharacters", "splitWord"];

    private static readonly EvasionKind[] EvasionFlags =
        [EvasionKind.RepeatedLetters, EvasionKind.LookalikeCharacters, EvasionKind.SplitWord];

    /// <summary>Outputs longer than this are written as a build object rather than one huge string.</summary>
    private const int LongOutput = 1000;

    private static readonly JsonSerializerOptions DisplayOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly Dictionary<string, JsonObject> _configurations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ProfanityFilter> _filters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CorpusCase> _byId = new(StringComparer.Ordinal);

    private Corpus(string directory, JsonObject metadata, JsonArray configurations, List<CorpusFile> files, List<CorpusCase> cases)
    {
        Directory = directory;
        Metadata = metadata;
        Configurations = configurations;
        Files = files;
        Cases = cases;

        foreach (var configuration in configurations)
        {
            if (configuration is JsonObject named && named["name"] is JsonValue name && name.GetValueKind() == JsonValueKind.String)
            {
                _configurations[name.GetValue<string>()] = named;
            }
        }

        foreach (var corpusCase in cases)
        {
            if (!_byId.ContainsKey(corpusCase.Id))
            {
                _byId.Add(corpusCase.Id, corpusCase);
            }
        }
    }

    public string Directory { get; }

    /// <summary><c>corpus.json</c>.</summary>
    public JsonObject Metadata { get; }

    /// <summary><c>configurations.json</c>.</summary>
    public JsonArray Configurations { get; }

    public IReadOnlyList<CorpusFile> Files { get; }

    /// <summary>Every case, in file-name order and then in file order.</summary>
    public IReadOnlyList<CorpusCase> Cases { get; }

    public int FormatVersion => Metadata["formatVersion"] is JsonValue value && value.GetValueKind() == JsonValueKind.Number
        ? value.GetValue<int>()
        : 0;

    public bool HasConfiguration(string name) => _configurations.ContainsKey(name);

    /// <summary>The first case with this id.</summary>
    public CorpusCase Find(string id) =>
        _byId.TryGetValue(id, out var found) ? found : throw new KeyNotFoundException($"No corpus case has the id '{id}'.");

    // ---------------------------------------------------------------------------------------------
    // Loading
    // ---------------------------------------------------------------------------------------------

    /// <summary>Reads <c>corpus.json</c>, <c>configurations.json</c> and every <c>cases/*.json</c>.</summary>
    /// <exception cref="DirectoryNotFoundException">The corpus directory does not exist.</exception>
    /// <exception cref="InvalidDataException">A file is missing, does not parse, or holds an unknown case kind.</exception>
    public static Corpus Load(string directory)
    {
        if (!System.IO.Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Conformance corpus not found: '{directory}' does not exist.");
        }

        var metadata = ReadJson(Path.Combine(directory, "corpus.json")) as JsonObject
                       ?? throw new InvalidDataException($"{Path.Combine(directory, "corpus.json")}: expected a JSON object.");

        var configurationsPath = Path.Combine(directory, "configurations.json");
        var configurations = ReadJson(configurationsPath) as JsonArray
                             ?? throw new InvalidDataException($"{configurationsPath}: expected a JSON array.");

        var files = new List<CorpusFile>();
        var cases = new List<CorpusCase>();
        var casesDirectory = Path.Combine(directory, "cases");

        var paths = System.IO.Directory.Exists(casesDirectory)
            ? System.IO.Directory.GetFiles(casesDirectory, "*.json").OrderBy(p => p, StringComparer.Ordinal).ToArray()
            : [];

        foreach (var path in paths)
        {
            var array = ReadJson(path) as JsonArray ?? throw new InvalidDataException($"{path}: expected a JSON array of cases.");
            var file = new CorpusFile(path, array);
            files.Add(file);

            var index = 0;
            foreach (var node in array)
            {
                if (node is not JsonObject json)
                {
                    throw new InvalidDataException($"{path}: element {index} is not a case object.");
                }

                var id = StringField(json, "id") ?? throw new InvalidDataException($"{path}: element {index} has no string \"id\".");
                var kind = StringField(json, "kind") ?? throw new InvalidDataException($"{path}: case '{id}' has no string \"kind\".");
                if (Array.IndexOf(Kinds, kind) < 0)
                {
                    throw new InvalidDataException($"{path}: case '{id}' has unknown kind '{kind}'.");
                }

                cases.Add(new CorpusCase(id, kind, file.Name, json));
                index++;
            }
        }

        return new Corpus(directory, metadata, configurations, files, cases);
    }

    private static JsonNode? ReadJson(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"{path}: file not found.");
        }

        try
        {
            var node = JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8));

            // Reading every string now reports an unreadable one (a lone-surrogate escape) against its file.
            Validate(node);
            return node;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"{path}: {e.Message}", e);
        }
    }

    private static void Validate(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj)
                {
                    Validate(property.Value);
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    Validate(item);
                }

                break;
            case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                _ = value.GetValue<string>();
                break;
        }
    }

    private static string? StringField(JsonObject json, string name) =>
        json[name] is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;

    // ---------------------------------------------------------------------------------------------
    // Inputs and positions
    // ---------------------------------------------------------------------------------------------

    /// <summary>Builds an Input: a string, <c>null</c>, or <c>{ "build": [ parts… ] }</c>.</summary>
    public static string? BuildInput(JsonNode? input)
    {
        switch (input)
        {
            case null:
                return null;
            case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                return value.GetValue<string>();
            case JsonObject obj when obj.Count == 1 && obj["build"] is JsonArray parts:
                var sb = new StringBuilder();
                foreach (var part in parts)
                {
                    AppendPart(sb, part as JsonObject ?? throw new InvalidDataException($"A build part must be an object: {Display(part)}"));
                }

                return sb.ToString();
            default:
                throw new InvalidDataException($"Not an Input: {Display(input)}");
        }
    }

    private static void AppendPart(StringBuilder sb, JsonObject part)
    {
        if (part.Count == 1 && StringField(part, "text") is { } text)
        {
            sb.Append(text);
        }
        else if (part.Count == 2 && StringField(part, "repeat") is { } repeat
                 && part["times"] is JsonValue times && times.GetValueKind() == JsonValueKind.Number && times.GetValue<int>() >= 1)
        {
            for (var i = times.GetValue<int>(); i > 0; i--)
            {
                sb.Append(repeat);
            }
        }
        else if (part.Count == 1 && StringField(part, "utf16") is { Length: 4 } hex
                 && int.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var unit))
        {
            sb.Append((char)unit);
        }
        else
        {
            throw new InvalidDataException($"Not a build part: {Display(part)}");
        }
    }

    /// <summary>Whether a node is text: a string or a build object.</summary>
    public static bool IsText(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String
        || node is JsonObject obj && obj.Count == 1 && obj["build"] is JsonArray;

    /// <summary>A code-point region of <paramref name="text"/> as a UTF-16 index and length.</summary>
    /// <remarks>A valid surrogate pair is one code point and two units; any other unit, a lone surrogate included, is one of each.</remarks>
    public static (int Index, int Length) CodePointsToUtf16(string text, int start, int length)
    {
        var index = AdvanceCodePoints(text, 0, start);
        return (index, AdvanceCodePoints(text, index, length) - index);
    }

    /// <summary>A UTF-16 region of <paramref name="text"/> as a code-point start and length.</summary>
    public static (int Start, int Length) Utf16ToCodePoints(string text, int index, int length)
    {
        var start = CountCodePoints(text, 0, index);
        return (start, CountCodePoints(text, index, index + length));
    }

    private static int AdvanceCodePoints(string text, int unit, int codePoints)
    {
        for (; codePoints > 0 && unit < text.Length; codePoints--)
        {
            unit += IsPairAt(text, unit) ? 2 : 1;
        }

        return unit;
    }

    private static int CountCodePoints(string text, int from, int to)
    {
        var count = 0;
        for (var i = from; i < to; count++)
        {
            i += IsPairAt(text, i) && i + 1 < to ? 2 : 1;
        }

        return count;
    }

    private static bool IsPairAt(string text, int i) =>
        i + 1 < text.Length && char.IsHighSurrogate(text[i]) && char.IsLowSurrogate(text[i + 1]);

    // ---------------------------------------------------------------------------------------------
    // Filters
    // ---------------------------------------------------------------------------------------------

    /// <summary>The filter a named configuration describes, built once per corpus.</summary>
    public ProfanityFilter FilterFor(string configurationName)
    {
        lock (_filters)
        {
            if (!_filters.TryGetValue(configurationName, out var filter))
            {
                if (!_configurations.TryGetValue(configurationName, out var configuration))
                {
                    throw new InvalidDataException($"No configuration is named '{configurationName}'.");
                }

                filter = BuildFilter(configuration);
                _filters.Add(configurationName, filter);
            }

            return filter;
        }
    }

    /// <summary>Builds the filter a configuration object describes.</summary>
    public static ProfanityFilter BuildFilter(JsonObject configuration)
    {
        var wordLists = configuration["wordLists"] as JsonObject
                        ?? throw new InvalidDataException($"Configuration has no \"wordLists\" object: {Display(configuration)}");

        IReadOnlyList<BannedWord> words;
        if (wordLists["bundled"] is { } bundled)
        {
            words = Selection(bundled);
        }
        else if (wordLists["entries"] is JsonArray entries)
        {
            words = entries.Select(entry => ParseEntry(entry as JsonObject ?? throw new InvalidDataException($"Not an entry: {Display(entry)}"))).ToList();
        }
        else
        {
            throw new InvalidDataException($"\"wordLists\" needs \"bundled\" or \"entries\": {Display(wordLists)}");
        }

        var options = configuration["options"] as JsonObject;
        return new ProfanityFilter(words, new ProfanityFilterOptions
        {
            SqueezeRepeatedLetters = Option(options, "squeezeRepeatedLetters"),
            FoldLookalikeCharacters = Option(options, "foldLookalikeCharacters"),
            JoinSpacedLetters = Option(options, "joinSpacedLetters"),
        });
    }

    private static bool Option(JsonObject? options, string name) =>
        options?[name] is not JsonValue value || value.GetValue<bool>();

    /// <summary>A bundled selection: <c>"default"</c>, <c>"all"</c> or an array of category names.</summary>
    public static IReadOnlyList<BannedWord> Selection(JsonNode selection)
    {
        if (selection is JsonArray categories)
        {
            return WordList.Bundled(categories.Select(c => ParseEnum<WordCategory>(c?.GetValue<string>())).ToArray());
        }

        return (selection as JsonValue)?.GetValue<string>() switch
        {
            "default" => WordList.PersianDefault,
            "all" => WordList.All,
            _ => throw new InvalidDataException($"Not a selection: {Display(selection)}"),
        };
    }

    private static BannedWord ParseEntry(JsonObject entry) =>
        new(StringField(entry, "text") ?? throw new InvalidDataException($"Entry has no text: {Display(entry)}"),
            ParseEnum<WordMatchMode>(StringField(entry, "mode")))
        {
            Category = ParseEnum<WordCategory>(StringField(entry, "category")),
        };

    private static T ParseEnum<T>(string? lowerCamelName)
        where T : struct
    {
        if (lowerCamelName is { Length: > 0 } && char.IsLower(lowerCamelName[0])
            && Enum.TryParse<T>(char.ToUpperInvariant(lowerCamelName[0]) + lowerCamelName.Substring(1), ignoreCase: false, out var parsed)
            && Enum.IsDefined(typeof(T), parsed))
        {
            return parsed;
        }

        throw new InvalidDataException($"'{lowerCamelName}' is not a {typeof(T).Name} name.");
    }

    private static string LowerCamel<T>(T value)
        where T : struct
    {
        var name = value.ToString()!;
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    // ---------------------------------------------------------------------------------------------
    // Evaluation
    // ---------------------------------------------------------------------------------------------

    /// <summary>Runs a case against this port and returns the result in the kind's <c>expected</c> shape.</summary>
    public JsonObject Evaluate(CorpusCase corpusCase)
    {
        var json = corpusCase.Json;
        switch (corpusCase.Kind)
        {
            case "ordinary":
            case "must-match":
            case "robustness":
                return EvaluateMatching(json);

            case "normalization":
            {
                var input = json["input"];
                var text = BuildInput(input);
                var output = json["steps"] switch
                {
                    JsonValue { } steps when steps.GetValue<string>() == "toPersianDigits" => PersianNormalizer.ToPersianDigits(text),
                    JsonValue { } steps when steps.GetValue<string>() == "toAsciiDigits" => PersianNormalizer.ToAsciiDigits(text),
                    var steps => PersianNormalizer.Normalize(text, ParseSteps(steps)),
                };

                return new JsonObject { ["output"] = TextNode(output, input) };
            }

            case "tokenization":
            {
                var input = json["input"];
                var tokens = new JsonArray();
                foreach (var token in PersianNormalizer.Tokenize(BuildInput(input)))
                {
                    tokens.Add(TextNode(token, input));
                }

                return new JsonObject { ["tokens"] = tokens };
            }

            case "word-list-parsing":
                try
                {
                    return new JsonObject { ["entries"] = Entries(WordList.Parse(BuildInput(json["text"]) ?? throw new InvalidDataException("\"text\" is missing."))) };
                }
                catch (FormatException e) when (e.Message.StartsWith("Line ", StringComparison.Ordinal))
                {
                    var digits = new string(e.Message.Substring(5).TakeWhile(char.IsDigit).ToArray());
                    return new JsonObject
                    {
                        ["error"] = new JsonObject
                        {
                            ["kind"] = "unknown-category",
                            ["line"] = int.Parse(digits, CultureInfo.InvariantCulture),
                        },
                    };
                }

            case "category-selection":
            {
                var selection = json["selection"] ?? throw new InvalidDataException("\"selection\" is missing.");
                return EvaluateSelection(selection);
            }

            case "mask-validation":
            {
                var mask = BuildInput(json["mask"]);
                if (mask is not { Length: 1 })
                {
                    throw new InvalidDataException("\"mask\" must be exactly one UTF-16 code unit.");
                }

                bool accepted;
                try
                {
                    _ = FilterFor("default").Censor("kir", mask[0]);
                    accepted = true;
                }
                catch (ArgumentException)
                {
                    accepted = false;
                }

                return new JsonObject { ["accepted"] = accepted };
            }

            default:
                throw new InvalidDataException($"Unknown case kind '{corpusCase.Kind}'.");
        }
    }

    private JsonObject EvaluateMatching(JsonObject json)
    {
        var configuration = StringField(json, "configuration") ?? throw new InvalidDataException("\"configuration\" is missing.");
        var filter = FilterFor(configuration);
        var input = json["input"];
        var text = BuildInput(input);

        var first = filter.FindMatch(text);
        var matches = new JsonArray();
        foreach (var match in filter.FindMatches(text))
        {
            matches.Add(MatchNode(text!, match));
        }

        var result = new JsonObject
        {
            ["containsProfanity"] = filter.ContainsProfanity(text),
            ["firstMatch"] = first is null ? null : MatchNode(text!, first),
            ["matches"] = matches,
            ["censored"] = TextNode(filter.Censor(text), input),
        };

        if (json["masks"] is JsonArray masks)
        {
            var censoredWith = new JsonObject();
            foreach (var maskNode in masks)
            {
                var mask = maskNode?.GetValue<string>();
                if (mask is not { Length: 1 })
                {
                    throw new InvalidDataException($"A mask must be exactly one UTF-16 code unit: {Display(maskNode)}");
                }

                censoredWith[mask] = TextNode(filter.Censor(text, mask[0]), input);
            }

            result["censoredWith"] = censoredWith;
        }

        return result;
    }

    private static JsonObject MatchNode(string text, ProfanityMatch match)
    {
        var evasion = new JsonArray();
        for (var i = 0; i < EvasionFlags.Length; i++)
        {
            if ((match.Evasion & EvasionFlags[i]) != 0)
            {
                evasion.Add((JsonNode)EvasionNames[i]);
            }
        }

        var (start, length) = Utf16ToCodePoints(text, match.Index, match.Length);
        return new JsonObject
        {
            ["entry"] = EntryNode(match.Word),
            ["evasion"] = evasion,
            ["start"] = start,
            ["length"] = length,
        };
    }

    private static JsonObject EntryNode(BannedWord word) => new()
    {
        ["text"] = word.Text,
        ["mode"] = LowerCamel(word.Mode),
        ["category"] = LowerCamel(word.Category),
    };

    private static JsonArray Entries(IEnumerable<BannedWord> words)
    {
        var array = new JsonArray();
        foreach (var word in words)
        {
            array.Add(EntryNode(word));
        }

        return array;
    }

    private static PersianNormalization ParseSteps(JsonNode? steps)
    {
        switch (steps)
        {
            case JsonArray names:
                var combined = PersianNormalization.None;
                foreach (var name in names)
                {
                    combined |= ParseEnum<PersianNormalization>(name?.GetValue<string>());
                }

                return combined;
            case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                return value.GetValue<string>() switch
                {
                    "comparison" => PersianNormalization.Comparison,
                    "standard" => PersianNormalization.Standard,
                    "none" => PersianNormalization.None,
                    var other => throw new InvalidDataException($"Unknown steps '{other}'."),
                };
            default:
                throw new InvalidDataException($"Not a steps value: {Display(steps)}");
        }
    }

    private static JsonObject EvaluateSelection(JsonNode selection)
    {
        var words = Selection(selection);
        var isDefault = selection is JsonValue value && value.GetValue<string>() == "default";

        var rules = new JsonArray();
        if (InSelection(words, selection))
        {
            rules.Add((JsonNode)"categoriesInSelection");
        }

        if (InBundledOrder(words))
        {
            rules.Add((JsonNode)"bundledOrder");
        }

        if (isDefault && words.All(w => w.Category != WordCategory.Mild))
        {
            rules.Add((JsonNode)"noMild");
        }

        return new JsonObject
        {
            ["count"] = words.Count,
            ["first"] = Entries(words.Take(5)),
            ["last"] = Entries(words.Skip(Math.Max(0, words.Count - 5))),
            ["rules"] = rules,
        };
    }

    private static bool InSelection(IReadOnlyList<BannedWord> words, JsonNode selection)
    {
        if (selection is JsonArray categories)
        {
            var allowed = new HashSet<WordCategory>(categories.Select(c => ParseEnum<WordCategory>(c?.GetValue<string>())));
            return words.All(w => allowed.Contains(w.Category));
        }

        // "default" and "all" select from every bundled category.
        return words.All(w => w.Category != WordCategory.Uncategorized);
    }

    private static bool InBundledOrder(IReadOnlyList<BannedWord> words)
    {
        var all = WordList.All;
        var position = 0;
        foreach (var word in words)
        {
            while (position < all.Count && !all[position].Equals(word))
            {
                position++;
            }

            if (position == all.Count)
            {
                return false;
            }

            position++;
        }

        return true;
    }

    /// <summary>
    /// An output string as a corpus value. Text that JSON cannot carry (a lone surrogate), or that is
    /// very long, is written as a build object, reusing the input's parts where the output starts with them.
    /// </summary>
    public static JsonNode TextNode(string output, JsonNode? input)
    {
        if (!HasLoneSurrogate(output) && output.Length <= LongOutput)
        {
            return output;
        }

        var parts = new JsonArray();
        var position = 0;

        if (input is JsonObject obj && obj["build"] is JsonArray inputParts)
        {
            foreach (var part in inputParts)
            {
                var expansion = BuildInput(new JsonObject { ["build"] = new JsonArray(part!.DeepClone()) })!;
                if (position + expansion.Length > output.Length
                    || string.CompareOrdinal(output, position, expansion, 0, expansion.Length) != 0)
                {
                    break;
                }

                parts.Add(part.DeepClone());
                position += expansion.Length;
            }
        }

        var run = new StringBuilder();
        for (var i = position; i < output.Length; i++)
        {
            if (IsPairAt(output, i))
            {
                run.Append(output[i]).Append(output[i + 1]);
                i++;
            }
            else if (char.IsSurrogate(output[i]))
            {
                FlushRun(parts, run);
                parts.Add(new JsonObject { ["utf16"] = ((int)output[i]).ToString("X4", CultureInfo.InvariantCulture) });
            }
            else
            {
                run.Append(output[i]);
            }
        }

        FlushRun(parts, run);
        return new JsonObject { ["build"] = parts };
    }

    private static void FlushRun(JsonArray parts, StringBuilder run)
    {
        if (run.Length > 0)
        {
            parts.Add(new JsonObject { ["text"] = run.ToString() });
            run.Clear();
        }
    }

    public static bool HasLoneSurrogate(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (IsPairAt(text, i))
            {
                i++;
            }
            else if (char.IsSurrogate(text[i]))
            {
                return true;
            }
        }

        return false;
    }

    // ---------------------------------------------------------------------------------------------
    // Kind rules and comparison
    // ---------------------------------------------------------------------------------------------

    /// <summary>The kind rules from the data model a matching case's <c>expected</c> breaks.</summary>
    public static List<string> CheckKindRules(CorpusCase corpusCase) =>
        CheckKindRules(corpusCase, corpusCase.Json["expected"] as JsonObject);

    /// <summary>The kind rules <paramref name="expected"/> breaks for this case.</summary>
    public static List<string> CheckKindRules(CorpusCase corpusCase, JsonObject? expected)
    {
        var violations = new List<string>();
        if (expected is null || !corpusCase.IsMatching)
        {
            return violations;
        }

        var contains = expected["containsProfanity"] is JsonValue value && value.GetValueKind() is JsonValueKind.True;

        if (corpusCase.Kind == "must-match" && !contains)
        {
            violations.Add("must-match requires containsProfanity to be true");
        }

        if (corpusCase.Kind == "ordinary")
        {
            if (contains)
            {
                violations.Add("ordinary requires containsProfanity to be false");
            }

            if (expected["firstMatch"] is not null)
            {
                violations.Add("ordinary requires firstMatch to be null");
            }

            if (expected["matches"] is not JsonArray { Count: 0 })
            {
                violations.Add("ordinary requires matches to be empty");
            }

            var input = BuildInput(corpusCase.Json["input"]) ?? string.Empty;
            if (!IsText(expected["censored"]) || BuildInput(expected["censored"]) != input)
            {
                violations.Add("ordinary requires censored to equal the input");
            }

            if (expected["censoredWith"] is JsonObject censoredWith)
            {
                foreach (var entry in censoredWith)
                {
                    if (!IsText(entry.Value) || BuildInput(entry.Value) != input)
                    {
                        violations.Add($"ordinary requires censoredWith[{Display(entry.Key)}] to equal the input");
                    }
                }
            }
        }

        return violations;
    }

    /// <summary>Every difference between an expected and an actual result, field by field.</summary>
    /// <remarks>Text compares by its built value, so a string and an equivalent build object are equal.</remarks>
    public static List<(string Path, string Expected, string Actual)> Compare(JsonNode? expected, JsonNode? actual)
    {
        var differences = new List<(string, string, string)>();
        Compare(expected, actual, "expected", differences);
        return differences;
    }

    private static void Compare(JsonNode? expected, JsonNode? actual, string path, List<(string, string, string)> differences)
    {
        if (expected is null || actual is null)
        {
            if (expected is not null || actual is not null)
            {
                differences.Add((path, Display(expected), Display(actual)));
            }

            return;
        }

        if (IsText(expected) && IsText(actual))
        {
            if (BuildInput(expected) != BuildInput(actual))
            {
                differences.Add((path, ShowText(BuildInput(expected)), ShowText(BuildInput(actual))));
            }

            return;
        }

        switch (expected)
        {
            case JsonObject expectedObject when actual is JsonObject actualObject:
                foreach (var property in expectedObject)
                {
                    var child = $"{path}.{property.Key}";
                    if (actualObject.ContainsKey(property.Key))
                    {
                        Compare(property.Value, actualObject[property.Key], child, differences);
                    }
                    else
                    {
                        differences.Add((child, Display(property.Value), "(missing)"));
                    }
                }

                foreach (var property in actualObject)
                {
                    if (!expectedObject.ContainsKey(property.Key))
                    {
                        differences.Add(($"{path}.{property.Key}", "(missing)", Display(property.Value)));
                    }
                }

                return;

            case JsonArray expectedArray when actual is JsonArray actualArray:
                for (var i = 0; i < Math.Max(expectedArray.Count, actualArray.Count); i++)
                {
                    var child = $"{path}[{i}]";
                    if (i >= expectedArray.Count)
                    {
                        differences.Add((child, "(missing)", Display(actualArray[i])));
                    }
                    else if (i >= actualArray.Count)
                    {
                        differences.Add((child, Display(expectedArray[i]), "(missing)"));
                    }
                    else
                    {
                        Compare(expectedArray[i], actualArray[i], child, differences);
                    }
                }

                return;

            case JsonValue when actual is JsonValue && expected.GetValueKind() == actual.GetValueKind()
                                && expected.ToJsonString() == actual.ToJsonString():
                return;

            default:
                differences.Add((path, Display(expected), Display(actual)));
                return;
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Display
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The text with invisible characters shown: Cf, Cc, Zl and Zp, whitespace other than U+0020,
    /// and lone surrogates become <c>\uXXXX</c>.
    /// </summary>
    public static string ShowInvisible(string? text)
    {
        if (text is null)
        {
            return "null";
        }

        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (IsPairAt(text, i))
            {
                sb.Append(c).Append(text[i + 1]);
                i++;
            }
            else if (char.IsSurrogate(c) || IsInvisible(c))
            {
                sb.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    /// <summary>Characters the corpus writing rules require as escapes (lone surrogates aside).</summary>
    public static bool IsInvisible(char c)
    {
        var category = CharUnicodeInfo.GetUnicodeCategory(c);
        return category is UnicodeCategory.Format or UnicodeCategory.Control or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator
               || (char.IsWhiteSpace(c) && c != ' ');
    }

    /// <summary>A short, readable description of what a case feeds in.</summary>
    public static string DescribeInput(CorpusCase corpusCase)
    {
        var json = corpusCase.Json;
        return corpusCase.Kind switch
        {
            "word-list-parsing" => "text " + ShowText(BuildInput(json["text"])),
            "category-selection" => "selection " + Display(json["selection"]),
            "mask-validation" => "mask " + ShowText(BuildInput(json["mask"])),
            _ => "input " + ShowText(BuildInput(json["input"])),
        };
    }

    private static string ShowText(string? text)
    {
        const int limit = 200;
        if (text is null)
        {
            return "null";
        }

        return text.Length <= limit
            ? $"\"{ShowInvisible(text)}\""
            : $"\"{ShowInvisible(text.Substring(0, limit))}…\" ({text.Length} UTF-16 units)";
    }

    /// <summary>A node on one line, for messages.</summary>
    public static string Display(JsonNode? node)
    {
        if (node is null)
        {
            return "null";
        }

        if (IsText(node))
        {
            return ShowText(BuildInput(node));
        }

        return node.ToJsonString(DisplayOptions);
    }

    /// <summary>One failure message for a case: id, file, input and every difference.</summary>
    public static string FailureMessage(CorpusCase corpusCase, string problem, IEnumerable<string> lines)
    {
        var sb = new StringBuilder();
        sb.Append("Case '").Append(corpusCase.Id).Append("' in ").Append(corpusCase.File).Append(": ").Append(problem).Append('\n');
        sb.Append("  ").Append(DescribeInput(corpusCase)).Append('\n');
        foreach (var line in lines)
        {
            sb.Append("  ").Append(line).Append('\n');
        }

        return sb.ToString();
    }
}
