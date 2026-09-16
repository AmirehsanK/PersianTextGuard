namespace PersianTextGuard.Tests;

/// <summary>Every example in README.md, so the documentation cannot drift from the code.</summary>
public class ReadmeExampleTests
{
    private static readonly ProfanityFilter Filter = new(WordList.PersianDefault);

    [Fact]
    public void Profanity_filtering_examples()
    {
        Assert.False(Filter.ContainsProfanity("سلام، سفارشم کی میرسه؟"));
        Assert.True(Filter.ContainsProfanity("ک.ی.ر"));
        Assert.True(Filter.ContainsProfanity("f u c k"));

        var match = Filter.FindMatch("sh1iiit");
        Assert.NotNull(match);
        Assert.Equal("shit", match!.Word.Text);
        Assert.Equal(WordCategory.Profanity, match.Word.Category);
        Assert.Equal(EvasionKind.LookalikeCharacters | EvasionKind.RepeatedLetters, match.Evasion);
    }

    [Fact]
    public void Category_examples()
    {
        var strict = new ProfanityFilter(WordList.All);
        var slursAndAbuse = new ProfanityFilter(
            WordList.Bundled(WordCategory.Slur, WordCategory.Harassment));

        Assert.False(Filter.ContainsProfanity("این فیلم آشغال بود"));
        Assert.True(strict.ContainsProfanity("این فیلم آشغال بود"));
        Assert.True(slursAndAbuse.ContainsProfanity("kys"));
        Assert.False(slursAndAbuse.ContainsProfanity("کیر"));
    }

    [Theory]
    [InlineData("هر کس")]
    [InlineData("تخم مرغ")]
    [InlineData("class")]
    [InlineData("push it")]
    [InlineData("Scunthorpe")]
    public void Ordinary_text_named_in_the_readme_passes(string text) => Assert.False(Filter.ContainsProfanity(text));

    [Fact]
    public void Custom_words_example()
    {
        var filter = new ProfanityFilter(
        [
            new BannedWord("اسپم"),
            new BannedWord("casino", WordMatchMode.Anywhere),
        ]);

        Assert.True(filter.ContainsProfanity("این پیام اسپم است"));
        Assert.True(filter.ContainsProfanity("onlinecasino.example"));
        Assert.Equal(1, new ProfanityFilter([new BannedWord("كص"), new BannedWord("کص"), new BannedWord("ک‌ص")]).Count);
    }

    [Fact]
    public void Normalization_examples()
    {
        Assert.Equal("کتابهای 12 abc", PersianNormalizer.Normalize("كتاب‌هاي  ۱۲ ABC"));
        Assert.Equal("کتاب‌های ۱۲ ABC", PersianNormalizer.Normalize("كتاب‌هاي  ۱۲ ABC", PersianNormalization.Standard));
        Assert.Equal("۲ ساعت پیش", PersianNormalizer.ToPersianDigits("2 ساعت پیش"));
        Assert.Equal("1404/05/14", PersianNormalizer.ToAsciiDigits("۱۴۰۴/۰۵/۱۴"));
        Assert.Equal(["سلام", "دنیا", "خوبی"], PersianNormalizer.Tokenize("سلام، دنیا! خوبی؟"));
        Assert.Equal(PersianNormalizer.Normalize("كتاب", PersianNormalization.Standard), PersianNormalizer.Normalize("کتاب", PersianNormalization.Standard));
    }
}
