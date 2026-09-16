namespace PersianTextGuard.Tests;

/// <summary>
/// User Story 1: every banned word in a message, in order, each with its entry, category, evasion
/// and whole-word position in the message as it was passed.
/// </summary>
public class FindMatchesTests
{
    private static readonly ProfanityFilter Filter = new(WordList.PersianDefault);

    [Fact]
    public void Every_word_is_returned_in_order()
    {
        var matches = Filter.FindMatches("you bitch, kos kesh");

        Assert.Equal(2, matches.Count);
        Assert.Equal("bitch", matches[0].Word.Text);
        AssertRegion(matches[0], 4, 5);

        // The phrase «kos kesh» and the rejoined «~koskesh» cover the same 8 characters; list order
        // decides between them (research R4), so only the category and region are pinned here.
        Assert.Equal(WordCategory.Insult, matches[1].Word.Category);
        AssertRegion(matches[1], 11, 8);
    }

    [Fact]
    public void Repeats_are_reported_separately()
    {
        var matches = Filter.FindMatches("kir kir kir");

        Assert.Equal(3, matches.Count);
        AssertRegion(matches[0], 0, 3);
        AssertRegion(matches[1], 4, 3);
        AssertRegion(matches[2], 8, 3);
    }

    [Fact]
    public void A_disguised_word_reports_the_evasion_and_spans_its_separators()
    {
        var matches = Filter.FindMatches("sh1t and f u c k");

        Assert.Equal(2, matches.Count);
        AssertRegion(matches[0], 0, 4);
        Assert.True(matches[1].Evasion.HasFlag(EvasionKind.SplitWord));
        AssertRegion(matches[1], 9, 7);
    }

    [Fact]
    public void An_ordinary_message_has_no_matches() =>
        Assert.Empty(Filter.FindMatches("سلام، سفارشم کی میرسه؟"));

    [Fact]
    public void Positions_refer_to_the_message_as_passed_not_its_normalized_form() =>
        AssertRegion(Assert.Single(Filter.FindMatches("ﻛﻴﺮ")), 0, 3);

    [Fact]
    public void Overlapping_entries_become_one_match_for_the_longest_entry()
    {
        var match = Assert.Single(Filter.FindMatches("motherfucker"));

        Assert.Equal("motherfucker", match.Word.Text);
        AssertRegion(match, 0, 12);
    }

    [Theory]
    // Punctuation and emoji end the word; a zero-width non-joiner and a Persian suffix do not.
    [InlineData("hello,fuck", 6, 4)]
    [InlineData("کیر😂", 0, 3)]
    public void Regions_cover_whole_words(string text, int index, int length) =>
        AssertRegion(Assert.Single(Filter.FindMatches(text)), index, length);

    [Fact]
    public void A_lone_surrogate_keeps_its_place_and_is_not_part_of_the_word()
    {
        // Built in code, not [InlineData]: attribute arguments are stored as UTF-8, which cannot
        // hold a lone surrogate, so the attribute would silently test a different string.
        var text = "hi " + '\uD83D' + " kir";

        var match = Assert.Single(Filter.FindMatches(text));

        AssertRegion(match, 5, 3);
        AssertValidResult(text, [match]);
    }

    [Fact]
    public void A_suffixed_persian_word_is_one_region_with_its_suffix()
    {
        var match = Assert.Single(Filter.FindMatches("جنده‌ها رو ببین"));

        Assert.Equal("جنده", match.Word.Text);
        AssertRegion(match, 0, 7);
    }

    [Fact]
    public void Chained_phrases_merge_into_their_union() =>
        AssertRegion(Assert.Single(Filter.FindMatches("پدر سگ پدر")), 0, 10);

    [Fact]
    public void Several_hits_inside_one_word_are_one_match() =>
        AssertRegion(Assert.Single(Filter.FindMatches("fuckfuck")), 0, 8);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_or_blank_text_returns_an_empty_list(string? text)
    {
        var matches = Filter.FindMatches(text);

        Assert.NotNull(matches);
        Assert.Empty(matches);
    }

    [Fact]
    public void The_filter_settings_apply()
    {
        var noFolding = new ProfanityFilter(WordList.PersianDefault, new ProfanityFilterOptions { FoldLookalikeCharacters = false });

        Assert.Empty(noFolding.FindMatches("sh1t"));
    }

    [Theory]
    [InlineData("you bitch, kos kesh")]
    [InlineData("kir kir kir")]
    [InlineData("sh1t and f u c k")]
    [InlineData("motherfucker")]
    [InlineData("جنده‌ها رو ببین")]
    [InlineData("پدر سگ پدر")]
    public void Results_are_ordered_non_overlapping_and_inside_the_message(string text)
    {
        var matches = Filter.FindMatches(text);

        Assert.NotEmpty(matches);
        AssertValidResult(text, matches);
    }

    [Fact]
    public void FindMatch_reports_a_position_too() =>
        AssertRegion(Filter.FindMatch("this is kir")!, 8, 3);

    private static void AssertValidResult(string text, IReadOnlyList<ProfanityMatch> matches)
    {
        for (var i = 0; i < matches.Count; i++)
        {
            Assert.True(matches[i].Index >= 0, "Index must not be negative");
            Assert.True(matches[i].Length >= 1, "Length must be at least 1");
            Assert.True(matches[i].Index + matches[i].Length <= text.Length, "region must end inside the message");

            if (i > 0)
            {
                Assert.True(matches[i - 1].Index + matches[i - 1].Length <= matches[i].Index, "regions must be ordered and must not overlap");
            }
        }
    }

    private static void AssertRegion(ProfanityMatch match, int index, int length)
    {
        Assert.Equal(index, match.Index);
        Assert.Equal(length, match.Length);
    }
}
