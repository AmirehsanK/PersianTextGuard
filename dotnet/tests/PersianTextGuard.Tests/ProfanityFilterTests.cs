using System.Text;

namespace PersianTextGuard.Tests;

/// <summary>The matcher itself, with small lists written for each case.</summary>
public class ProfanityFilterTests
{
    private static ProfanityFilter FilterOf(params BannedWord[] words) => new(words);

    [Fact]
    public void A_match_reports_the_entry_as_it_was_given()
    {
        var entry = new BannedWord("كثافت");
        var match = FilterOf(entry).FindMatch("چه کثافتی");

        Assert.Null(match); // whole word only: «کثافتی» is a different token

        var found = FilterOf(entry).FindMatch("این کثافت");
        Assert.NotNull(found);
        Assert.Equal(entry, found!.Word);
    }

    [Theory]
    [InlineData("damn", EvasionKind.None)]
    [InlineData("daaaamn", EvasionKind.RepeatedLetters)]
    [InlineData("d4mn", EvasionKind.LookalikeCharacters)]
    [InlineData("d44aamn", EvasionKind.LookalikeCharacters | EvasionKind.RepeatedLetters)]
    public void The_match_says_which_evasion_was_undone(string text, EvasionKind expected)
    {
        Assert.Equal(expected, FilterOf(new BannedWord("damn")).FindMatch(text)!.Evasion);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void An_entry_saved_in_its_look_alike_spelling_matches_the_plain_one(bool wholeWord)
    {
        // A moderator pastes the spelling they were looking at; it must still catch the plain word.
        var filter = FilterOf(new BannedWord("k0s", wholeWord ? WordMatchMode.WholeWord : WordMatchMode.Anywhere));

        Assert.True(filter.ContainsProfanity("ye kos inja"));
        Assert.True(filter.ContainsProfanity("ye k0s inja"));
    }

    [Fact]
    public void Whole_word_entries_never_match_across_ordinary_words()
    {
        var filter = FilterOf(new BannedWord("shit"));

        Assert.False(filter.ContainsProfanity("push it"));
        Assert.True(filter.ContainsProfanity("s h i t"));
    }

    [Fact]
    public void Anywhere_entries_match_inside_words_but_whole_word_entries_do_not()
    {
        Assert.True(FilterOf(new BannedWord("fuck", WordMatchMode.Anywhere)).ContainsProfanity("clusterfuck"));
        Assert.False(FilterOf(new BannedWord("ass")).ContainsProfanity("classic"));
    }

    [Fact]
    public void Each_evasion_can_be_switched_off()
    {
        var words = new[] { new BannedWord("fuck") };

        Assert.False(new ProfanityFilter(words, new ProfanityFilterOptions { SqueezeRepeatedLetters = false }).ContainsProfanity("fuuuuck"));
        Assert.False(new ProfanityFilter(words, new ProfanityFilterOptions { FoldLookalikeCharacters = false }).ContainsProfanity("f*ck"));
        Assert.False(new ProfanityFilter(words, new ProfanityFilterOptions { JoinSpacedLetters = false }).ContainsProfanity("f u c k"));

        Assert.True(new ProfanityFilter(words).ContainsProfanity("fuuuuck"));
    }

    [Fact]
    public void Duplicate_spellings_become_one_entry()
    {
        var filter = FilterOf(new BannedWord("كص"), new BannedWord("کص"), new BannedWord("  کص "), new BannedWord(""));

        Assert.Equal(1, filter.Count);
    }

    [Fact]
    public void Blank_text_and_empty_lists_are_clean()
    {
        Assert.Null(FilterOf(new BannedWord("x")).FindMatch(null));
        Assert.Null(FilterOf(new BannedWord("x")).FindMatch("   "));
        Assert.Null(FilterOf().FindMatch("anything"));
    }

    [Fact]
    public void A_filter_is_safe_to_share_across_threads()
    {
        var filter = new ProfanityFilter(WordList.PersianDefault);

        Parallel.For(0, 2_000, i =>
        {
            Assert.True(filter.ContainsProfanity(i % 2 == 0 ? "f u c k" : "کــیــر"));
            Assert.False(filter.ContainsProfanity("سلام، سفارش من کی میرسه؟"));
        });
    }

    [Fact]
    public void Word_lists_parse_markers_comments_and_blank_lines()
    {
        var words = WordList.Parse("# a comment\r\n\r\nwhole\n~anywhere\n  ~  spaced  \n");

        Assert.Equal(
            [new BannedWord("whole"), new BannedWord("anywhere", WordMatchMode.Anywhere), new BannedWord("spaced", WordMatchMode.Anywhere)],
            words);
    }

    [Fact]
    public void Word_lists_load_from_a_stream()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("کیر\n~fuck\n"));

        Assert.Equal(2, WordList.Load(stream).Count);
    }
}
