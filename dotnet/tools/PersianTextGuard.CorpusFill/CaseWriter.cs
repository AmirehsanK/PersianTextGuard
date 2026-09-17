using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PersianTextGuard.Conformance;

/// <summary>
/// Writes corpus JSON in its canonical form: two-space indentation, a fixed key order, LF line
/// endings, a trailing newline, and invisible characters as escapes with everything else literal.
/// </summary>
/// <remarks>
/// Strings go through this escaper rather than System.Text.Json's encoders, which escape Persian
/// (the default) or still escape some characters the corpus writes literally (the relaxed one).
/// </remarks>
public static class CaseWriter
{
    /// <summary>A container whose one-line form is at most this long, and holds no containers, stays on one line.</summary>
    private const int InlineLimit = 100;

    private static readonly string[] CaseKeys =
        ["id", "kind", "configuration", "note", "selection", "mask", "masks", "steps", "text", "input", "expected"];

    private static readonly string[] ExpectedKeys =
    [
        "containsProfanity", "firstMatch", "matches", "censored", "censoredWith", "output", "tokens", "entries",
        "error", "count", "first", "last", "rules", "accepted",
    ];

    private static readonly string[] MatchKeys = ["entry", "evasion", "start", "length"];

    private static readonly string[] EntryKeys = ["text", "mode", "category"];

    /// <summary>A whole case file: the array of cases, with a trailing newline.</summary>
    public static string WriteFile(JsonArray cases)
    {
        var sb = new StringBuilder();
        WriteNode(sb, cases, indent: 0, role: Role.CaseList);
        return sb.Append('\n').ToString();
    }

    /// <summary>One value in canonical form, without a trailing newline.</summary>
    public static string Write(JsonNode? node)
    {
        var sb = new StringBuilder();
        WriteNode(sb, node, indent: 0, role: Role.Other);
        return sb.ToString();
    }

    /// <summary>A string literal, quoted, with the corpus escaping rules.</summary>
    /// <exception cref="ArgumentException">The text holds a lone surrogate, which a build object must describe instead.</exception>
    public static string WriteString(string text)
    {
        var sb = new StringBuilder(text.Length + 2);
        AppendString(sb, text);
        return sb.ToString();
    }

    private enum Role
    {
        Other,
        CaseList,
        Case,
        Expected,
        Match,
        MatchList,
        Entry,
        EntryList,
    }

    private static void WriteNode(StringBuilder sb, JsonNode? node, int indent, Role role)
    {
        switch (node)
        {
            case null:
                sb.Append("null");
                break;
            case JsonObject obj:
                WriteObject(sb, obj, indent, role);
                break;
            case JsonArray array:
                WriteArray(sb, array, indent, role);
                break;
            case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                AppendString(sb, value.GetValue<string>());
                break;
            case JsonValue value:
                sb.Append(value.ToJsonString());
                break;
        }
    }

    private static void WriteObject(StringBuilder sb, JsonObject obj, int indent, Role role)
    {
        if (obj.Count == 0)
        {
            sb.Append("{}");
            return;
        }

        var keys = OrderedKeys(obj, role);

        if (role != Role.Case && obj.All(p => p.Value is not JsonObject and not JsonArray))
        {
            var line = new StringBuilder("{ ");
            for (var i = 0; i < keys.Count; i++)
            {
                AppendString(line.Append(i == 0 ? string.Empty : ", "), keys[i]);
                WriteNode(line.Append(": "), obj[keys[i]], 0, Role.Other);
            }

            line.Append(" }");
            if (line.Length <= InlineLimit)
            {
                sb.Append(line);
                return;
            }
        }

        sb.Append("{\n");
        for (var i = 0; i < keys.Count; i++)
        {
            Indent(sb, indent + 1);
            AppendString(sb, keys[i]);
            sb.Append(": ");
            WriteNode(sb, obj[keys[i]], indent + 1, ChildRole(role, keys[i]));
            sb.Append(i < keys.Count - 1 ? ",\n" : "\n");
        }

        Indent(sb, indent);
        sb.Append('}');
    }

    private static void WriteArray(StringBuilder sb, JsonArray array, int indent, Role role)
    {
        if (array.Count == 0)
        {
            sb.Append("[]");
            return;
        }

        var elementRole = role switch
        {
            Role.CaseList => Role.Case,
            Role.MatchList => Role.Match,
            Role.EntryList => Role.Entry,
            _ => Role.Other,
        };

        if (role != Role.CaseList && array.All(item => item is not JsonObject and not JsonArray))
        {
            var line = new StringBuilder("[");
            for (var i = 0; i < array.Count; i++)
            {
                WriteNode(line.Append(i == 0 ? string.Empty : ", "), array[i], 0, Role.Other);
            }

            line.Append(']');
            if (line.Length <= InlineLimit)
            {
                sb.Append(line);
                return;
            }
        }

        sb.Append("[\n");
        for (var i = 0; i < array.Count; i++)
        {
            Indent(sb, indent + 1);
            WriteNode(sb, array[i], indent + 1, elementRole);
            sb.Append(i < array.Count - 1 ? ",\n" : "\n");
        }

        Indent(sb, indent);
        sb.Append(']');
    }

    private static Role ChildRole(Role parent, string key) => (parent, key) switch
    {
        (Role.Case, "expected") => Role.Expected,
        (Role.Expected, "firstMatch") => Role.Match,
        (Role.Expected, "matches") => Role.MatchList,
        (Role.Expected, "entries" or "first" or "last") => Role.EntryList,
        (Role.Match, "entry") => Role.Entry,
        _ => Role.Other,
    };

    private static List<string> OrderedKeys(JsonObject obj, Role role)
    {
        var order = role switch
        {
            Role.Case => CaseKeys,
            Role.Expected => ExpectedKeys,
            Role.Match => MatchKeys,
            Role.Entry => EntryKeys,
            _ => [],
        };

        var keys = order.Where(obj.ContainsKey).ToList();
        keys.AddRange(obj.Select(p => p.Key).Where(k => Array.IndexOf(order, k) < 0));
        return keys;
    }

    private static void Indent(StringBuilder sb, int level) => sb.Append(' ', level * 2);

    private static void AppendString(StringBuilder sb, string text)
    {
        sb.Append('"');
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    continue;
                case '\\':
                    sb.Append("\\\\");
                    continue;
                // JSON's own short escapes for line breaks and tabs, so word-list texts stay readable.
                case '\n':
                    sb.Append("\\n");
                    continue;
                case '\r':
                    sb.Append("\\r");
                    continue;
                case '\t':
                    sb.Append("\\t");
                    continue;
            }

            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                if (Corpus.IsNoncharacter(char.ConvertToUtf32(c, text[i + 1])))
                {
                    // A supplementary noncharacter is as invisible as a BMP one; its two escapes form a valid pair.
                    sb.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture))
                      .Append("\\u").Append(((int)text[i + 1]).ToString("X4", CultureInfo.InvariantCulture));
                }
                else
                {
                    sb.Append(c).Append(text[i + 1]);
                }

                i++;
            }
            else if (char.IsSurrogate(c))
            {
                throw new ArgumentException(
                    $"Lone surrogate U+{(int)c:X4} at index {i}; text JSON cannot carry must be written as a build object.", nameof(text));
            }
            else if (NeedsEscape(c))
            {
                sb.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
            }
            else
            {
                sb.Append(c);
            }
        }

        sb.Append('"');
    }

    private static bool NeedsEscape(char c)
    {
        var category = CharUnicodeInfo.GetUnicodeCategory(c);
        return category is UnicodeCategory.Format or UnicodeCategory.Control or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator
               || (char.IsWhiteSpace(c) && c != ' ')
               || Corpus.IsNoncharacter(c);
    }
}
