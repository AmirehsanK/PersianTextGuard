using System.Reflection;
using System.Text;

namespace PersianTextGuard.Tests;

/// <summary>
/// User Story 3: the four ways of asking about a message — yes/no, first match, every match,
/// censored — agree on every message the other tests already use, on malformed and huge input, and
/// under concurrent use of one shared filter.
/// </summary>
/// <remarks>
/// The corpus is every <see cref="InlineDataAttribute"/> string in <see cref="DefaultListTests"/>,
/// read by reflection so that file stays untouched, plus robustness inputs built in code: attribute
/// arguments are stored as UTF-8 and cannot hold a lone surrogate.
/// </remarks>
public class ConsistencyTests
{
    private const string Mask = "****";

    private static readonly ProfanityFilter Filter = new(WordList.PersianDefault);

    private static readonly string LongClean = string.Concat(
        Enumerable.Repeat("سلام این یک متن معمولی است و هیچ مشکلی ندارد. hello this is fine. ", 2000));

    public static IEnumerable<object?[]> Corpus() =>
        Messages().Select(message => new object?[] { message.Text, message.Ordinary });

    [Theory]
    [MemberData(nameof(Corpus), DisableDiscoveryEnumeration = true)]
    public void All_four_capabilities_agree(string? text, bool ordinary)
    {
        _ = ordinary;
        var contains = Filter.ContainsProfanity(text);
        var first = Filter.FindMatch(text);
        var all = Filter.FindMatches(text);

        Assert.Equal(contains, first is not null);
        Assert.Equal(contains, all.Count > 0);

        if (text is not null)
        {
            Assert.Equal(contains, !ReferenceEquals(text, Filter.Censor(text)));
        }

        // The first match lies inside one of the matches every-match reports: the same entry, or a
        // longer overlapping one that was chosen instead.
        if (first is not null)
        {
            Assert.Contains(all, match =>
                match.Index <= first.Index && first.Index + first.Length <= match.Index + match.Length);
        }
    }

    [Theory]
    [MemberData(nameof(Corpus), DisableDiscoveryEnumeration = true)]
    public void Censored_output_is_clean(string? text, bool ordinary)
    {
        _ = ordinary;
        Assert.False(Filter.ContainsProfanity(Filter.Censor(text)));
    }

    [Theory]
    [MemberData(nameof(Corpus), DisableDiscoveryEnumeration = true)]
    public void Text_outside_hidden_regions_is_unchanged(string? text, bool ordinary)
    {
        _ = ordinary;
        if (text is null)
        {
            return;
        }

        var expected = new StringBuilder();
        var copied = 0;
        foreach (var match in Filter.FindMatches(text))
        {
            expected.Append(text, copied, match.Index - copied).Append(Mask);
            copied = match.Index + match.Length;
        }

        expected.Append(text, copied, text.Length - copied);

        // When one pass leaves nothing to flag, Censor must be exactly that pass. Otherwise it had to
        // mask again (see CensorTests), and the clean-output test above covers it.
        if (!Filter.ContainsProfanity(expected.ToString()))
        {
            Assert.Equal(expected.ToString(), Filter.Censor(text));
        }
    }

    [Theory]
    [MemberData(nameof(Corpus), DisableDiscoveryEnumeration = true)]
    public void Ordinary_messages_are_neither_matched_nor_changed(string? text, bool ordinary)
    {
        if (!ordinary)
        {
            return;
        }

        Assert.Empty(Filter.FindMatches(text));
        Assert.Same(text, Filter.Censor(text));
    }

    [Fact]
    public void The_corpus_is_read_from_the_existing_tests() =>
        Assert.True(Messages().Count(message => message.Ordinary) > 50 && Messages().Count > 150,
            "reflection over DefaultListTests found too few messages; the consistency checks would prove little");

    [Fact]
    public void A_shared_filter_gives_the_same_answers_under_concurrent_use()
    {
        // The 132,000-character messages are checked above; here they would only make 20,000 calls slow.
        var messages = Messages().Select(message => message.Text).Where(text => text is null || text.Length < 1000).ToList();
        var expected = messages.Select(Answer).ToList();

        Parallel.For(0, 20_000, i =>
        {
            var index = i % messages.Count;
            Assert.Equal(expected[index], Answer(messages[index]));
        });
    }

    private static string Answer(string? text)
    {
        var sb = new StringBuilder();
        sb.Append(Filter.ContainsProfanity(text)).Append('|');

        var first = Filter.FindMatch(text);
        sb.Append(first is null ? "-" : $"{first.Word.Text}@{first.Index}+{first.Length}/{first.Evasion}").Append('|');

        foreach (var match in Filter.FindMatches(text))
        {
            sb.Append(match.Word.Text).Append('@').Append(match.Index).Append('+').Append(match.Length)
              .Append('/').Append(match.Evasion).Append(';');
        }

        sb.Append('|').Append(Filter.Censor(text));
        return sb.ToString();
    }

    private static List<(string? Text, bool Ordinary)> Messages()
    {
        var messages = new List<(string? Text, bool Ordinary)>();

        foreach (var method in typeof(DefaultListTests).GetMethods())
        {
            var ordinary = method.Name == nameof(DefaultListTests.Ordinary_messages_pass);

            foreach (var attribute in method.GetCustomAttributes<InlineDataAttribute>())
            {
                foreach (var row in attribute.GetData(method))
                {
                    messages.AddRange(row.OfType<string>().Select(text => ((string?)text, ordinary)));
                }
            }
        }

        messages.Add((null, false));
        messages.Add((string.Empty, false));
        messages.Add(("   ", false));
        messages.Add(("hi " + '\uD83D', false));
        messages.Add(('\uDE00' + " hi", false));
        messages.Add(("kir " + '\uD83D' + " kos", false));
        messages.Add((LongClean, true));
        messages.Add((LongClean + " کیر", false));

        return messages;
    }
}
