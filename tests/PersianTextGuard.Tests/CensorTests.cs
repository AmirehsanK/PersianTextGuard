namespace PersianTextGuard.Tests;

/// <summary>
/// User Story 2: the message with each banned word's whole word replaced by a fixed four-character
/// mask, everything else exactly as passed, and nothing left that the same filter would flag.
/// </summary>
public class CensorTests
{
    private static readonly ProfanityFilter Filter = new(WordList.PersianDefault);

    [Theory]
    [InlineData("this is kir", "this is ****")]
    // The whole word is hidden, suffix and all, so no fragment gives it away.
    [InlineData("جنده‌ها رو ببین", "**** رو ببین")]
    // Every hidden word gets the same mask, whatever its length.
    [InlineData("kir and motherfucker", "**** and ****")]
    // A disguised word is hidden with its separators.
    [InlineData("f.u.c.k off", "**** off")]
    [InlineData("ج.نده", "****")]
    [InlineData("f\nu\nc\nk", "****")]
    // An emoji next to the word is not part of it.
    [InlineData("کیر😂", "****😂")]
    // Two words next to each other stay two masks; the space between them is kept.
    [InlineData("kir kos", "**** ****")]
    // A phrase is one match.
    [InlineData("برو پدر سگ", "برو ****")]
    public void Banned_words_are_replaced_by_the_mask(string text, string expected) =>
        Assert.Equal(expected, Filter.Censor(text));

    [Theory]
    // Arabic yeh and kaf, a zero-width non-joiner: none of the normalization leaks into the output.
    [InlineData("كتاب‌هاي خوب")]
    [InlineData("   ")]
    [InlineData("")]
    public void Clean_text_comes_back_as_the_same_instance(string text) =>
        Assert.Same(text, Filter.Censor(text));

    [Fact]
    public void Null_text_becomes_empty() =>
        Assert.Equal(string.Empty, Filter.Censor(null));

    [Fact]
    public void Censored_output_is_clean_even_when_masking_would_join_letters()
    {
        // The mask is a filler character the filter drops, so "k **** i **** r" reads as "kir".
        var censored = Filter.Censor("k kos i kos r");

        Assert.False(Filter.ContainsProfanity(censored));
    }

    [Fact]
    public void A_mask_character_can_be_chosen() =>
        Assert.Equal("####", Filter.Censor("kir", '#'));

    [Theory]
    [InlineData('x')]
    [InlineData('5')]
    [InlineData(' ')]
    [InlineData('\n')]
    [InlineData('\uD83D')]
    public void Letters_digits_whitespace_controls_and_surrogates_are_refused_as_masks(char mask)
    {
        Assert.Equal("maskCharacter", Assert.Throws<ArgumentException>(() => Filter.Censor("kir", mask)).ParamName);

        // Checked before looking at the text, so a bad mask is caught even when there is nothing to censor.
        Assert.Equal("maskCharacter", Assert.Throws<ArgumentException>(() => Filter.Censor(null, mask)).ParamName);
    }

    [Fact]
    public void Censored_text_can_change_length_and_positions_still_refer_to_the_original()
    {
        Assert.Equal(4, Filter.Censor("kir").Length);

        var match = Assert.Single(Filter.FindMatches("kir"));
        Assert.Equal(0, match.Index);
        Assert.Equal(3, match.Length);
    }
}
