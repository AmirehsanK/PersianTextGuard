using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PersianTextGuard.Tests;

namespace PersianTextGuard.Conformance;

/// <summary>
/// The corpus itself: present, readable, big enough, fully recorded, visibly escaped, and covering
/// every message the existing tests use. A missing or broken corpus fails here instead of passing
/// with zero cases.
/// </summary>
public class CorpusGuardTests
{
    private const string FillInHint = "run dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill";

    [Fact]
    public void The_corpus_directory_exists_and_loads()
    {
        var directory = CorpusData.Directory;

        Assert.True(Directory.Exists(directory), $"Conformance corpus not found at '{directory}'.");
        Assert.NotEmpty(Corpus.Load(directory).Files);
    }

    [Fact]
    public void The_format_version_is_understood() =>
        Assert.Equal(Corpus.SupportedFormatVersion, CorpusData.Corpus.FormatVersion);

    [Fact]
    public void There_are_at_least_300_cases() =>
        Assert.True(CorpusData.Corpus.Cases.Count >= 300, $"The corpus has {CorpusData.Corpus.Cases.Count} cases; at least 300 are required.");

    [Fact]
    public void Case_ids_are_unique()
    {
        var pattern = new Regex("^[a-z0-9]+(-[a-z0-9]+)*$");
        var cases = CorpusData.Corpus.Cases;

        var duplicates = cases.GroupBy(c => c.Id).Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} ({string.Join(", ", g.Select(c => c.File))})").ToList();
        var malformed = cases.Where(c => !pattern.IsMatch(c.Id)).Select(c => $"{c.Id} ({c.File})").ToList();

        Assert.True(duplicates.Count == 0, "Duplicate case ids:\n" + string.Join("\n", duplicates));
        Assert.True(malformed.Count == 0, "Case ids must be lowercase ASCII letters, digits and single hyphens:\n" + string.Join("\n", malformed));
    }

    [Fact]
    public void No_case_is_pending()
    {
        var pending = CorpusData.Corpus.Cases.Where(c => c.IsPending).Select(c => $"{c.Id} ({c.File})").ToList();

        Assert.True(pending.Count == 0, $"{pending.Count} pending case(s); {FillInHint}:\n" + string.Join("\n", pending));
    }

    [Fact]
    public void No_file_contains_an_unescaped_invisible_character()
    {
        var problems = new List<string>();
        var directory = CorpusData.Directory;
        var paths = new[] { Path.Combine(directory, "corpus.json"), Path.Combine(directory, "configurations.json") }
            .Concat(CorpusData.Corpus.Files.Select(f => f.Path));

        foreach (var path in paths)
        {
            var text = File.ReadAllText(path, Encoding.UTF8);
            var line = 1;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '\n')
                {
                    line++;
                    continue;
                }

                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    i++;
                }
                else if (char.IsSurrogate(c) || Corpus.IsInvisible(c))
                {
                    problems.Add($"{Path.GetFileName(path)} line {line}: U+{(int)c:X4}");
                }
            }
        }

        Assert.True(problems.Count == 0, "Write these characters as \\uXXXX escapes:\n" + string.Join("\n", problems));
    }

    [Fact]
    public void Every_configuration_a_case_names_exists()
    {
        var corpus = CorpusData.Corpus;
        var missing = corpus.Cases
            .Where(c => c.IsMatching)
            .Select(c => (Case: c, Name: c.Json["configuration"] is JsonValue v ? v.GetValue<string>() : null))
            .Where(x => x.Name is null || !corpus.HasConfiguration(x.Name))
            .Select(x => $"{x.Case.Id} ({x.Case.File}): '{x.Name}'")
            .ToList();

        Assert.True(corpus.HasConfiguration("default"), "configurations.json must define 'default'.");
        Assert.True(missing.Count == 0, "Cases name configurations that do not exist:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void Every_existing_test_input_is_in_the_corpus()
    {
        var corpus = CorpusData.Corpus;
        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var corpusCase in corpus.Cases)
        {
            var field = corpusCase.Kind == "word-list-parsing" ? "text" : "input";
            if (corpusCase.Json[field] is { } node && Corpus.BuildInput(node) is { } text)
            {
                known.Add(text);
            }
        }

        var missing = new List<string>();
        var inputs = 0;
        foreach (var type in typeof(DefaultListTests).Assembly.GetTypes())
        {
            foreach (var method in type.GetMethods())
            {
                foreach (var attribute in method.GetCustomAttributes<InlineDataAttribute>())
                {
                    foreach (var row in attribute.GetData(method))
                    {
                        // [InlineData(null)] passes a null row.
                        if (row?.OfType<string>().FirstOrDefault() is not { } text)
                        {
                            continue;
                        }

                        inputs++;
                        if (!known.Contains(text))
                        {
                            missing.Add($"{type.Name}.{method.Name}: \"{Corpus.ShowInvisible(text)}\"");
                        }
                    }
                }
            }
        }

        Assert.True(inputs > 150, $"Reflection found only {inputs} [InlineData] inputs; the coverage check would prove little.");
        Assert.True(missing.Count == 0, $"{missing.Count} [InlineData] input(s) are not in the corpus:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void Canonical_writer_escapes_invisible_characters_and_keeps_persian_literal()
    {
        Assert.Equal("\"کی\\u200Cر\"", CaseWriter.Write(JsonValue.Create("کی‌ر")));
        Assert.Throws<ArgumentException>(() => CaseWriter.WriteString("hi \uD83D"));
    }
}
