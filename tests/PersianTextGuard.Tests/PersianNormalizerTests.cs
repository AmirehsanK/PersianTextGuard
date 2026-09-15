namespace PersianTextGuard.Tests;

public class PersianNormalizerTests
{
    private static string N(string? text) => PersianNormalizer.Normalize(text);

    [Fact]
    public void Arabic_and_Persian_yeh_normalize_to_the_same_form()
    {
        // The most common bypass of all: the two characters render identically in most fonts.
        Assert.Equal(N("بازی"), N("بازي"));
    }

    [Fact]
    public void Arabic_kaf_and_Persian_keheh_normalize_to_the_same_form()
    {
        Assert.Equal(N("کمک"), N("كمك"));
    }

    [Theory]
    [InlineData("ڪ", "ک")] // swash kaf
    [InlineData("ھ", "ه")] // heh doachashmee
    [InlineData("ە", "ه")] // Kurdish ae
    [InlineData("ې", "ی")] // Pashto yeh
    [InlineData("ٱ", "ا")] // alef wasla
    [InlineData("ﻙ", "ک")] // presentation form, folded by NFKC
    public void Look_alike_letters_from_other_scripts_fold_to_persian(string input, string expected)
    {
        Assert.Equal(N(expected), N(input));
    }

    [Theory]
    [InlineData("می‌روم")] // zero-width non-joiner
    [InlineData("می​روم")] // zero-width space
    [InlineData("می⁠روم")] // word joiner
    [InlineData("می­روم")] // soft hyphen
    [InlineData("‫میروم‬")] // right-to-left embedding
    [InlineData("مـیـروم")] // tatweel
    [InlineData("مِیرُوم")] // diacritics
    public void Invisible_and_decorative_characters_are_removed_for_comparison(string input)
    {
        Assert.Equal("میروم", N(input));
    }

    [Theory]
    [InlineData("۱۲۳", "123")]
    [InlineData("١٢٣", "123")]
    [InlineData("123", "123")]
    public void All_three_digit_ranges_fold_to_ascii(string input, string expected)
    {
        Assert.Equal(expected, N(input));
    }

    [Fact]
    public void Held_keys_collapse_to_two_so_real_double_letters_survive()
    {
        Assert.Equal(N("سسلام"), N("سسسسلام"));
        Assert.Equal("book", N("BOOOOOK"));
    }

    [Fact]
    public void Whitespace_collapses_and_trims()
    {
        Assert.Equal("سلام دنیا", N("  سلام \t\n  دنیا  "));
    }

    [Fact]
    public void Null_and_blank_become_empty()
    {
        Assert.Equal(string.Empty, N(null));
        Assert.Equal(string.Empty, N("   "));
    }

    [Fact]
    public void Standard_fixes_letters_but_keeps_what_belongs_in_displayed_text()
    {
        var result = PersianNormalizer.Normalize("كتاب‌هاي  ۱۲ ABC", PersianNormalization.Standard);

        // Keheh and yeh fixed, whitespace collapsed; ZWNJ, Persian digits and case kept.
        Assert.Equal("کتاب‌های ۱۲ ABC", result);
    }

    [Fact]
    public void Steps_can_be_combined_individually()
    {
        Assert.Equal("کتاب‌های", PersianNormalizer.Normalize("كتاب‌هاي", PersianNormalization.UnifyLetters));
        Assert.Equal("كتاب‌هاي", PersianNormalizer.Normalize("كتاب‌هاي", PersianNormalization.None));
    }

    [Fact]
    public void Tokenize_splits_on_persian_and_ascii_punctuation()
    {
        Assert.Equal(["سلام", "دنیا", "خوبی"], PersianNormalizer.Tokenize("سلام، دنیا! خوبی؟"));
        Assert.Empty(PersianNormalizer.Tokenize(null));
    }

    [Fact]
    public void Digits_convert_in_both_directions_without_touching_letters()
    {
        Assert.Equal("۲ ساعت پیش، KR۱", PersianNormalizer.ToPersianDigits("2 ساعت پیش، KR1"));
        Assert.Equal("2 ساعت پیش، KR1", PersianNormalizer.ToAsciiDigits("۲ ساعت پیش، KR١"));
    }
}
