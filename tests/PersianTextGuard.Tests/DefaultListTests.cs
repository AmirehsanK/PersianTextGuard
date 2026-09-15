namespace PersianTextGuard.Tests;

/// <summary>
/// The filter against the bundled list. A filter fails in two ways, and both come from the list
/// meeting the matcher: an evasion slipping through, and an ordinary message being rejected. The
/// second is worse, because nobody reports it and people just learn the site is broken.
/// </summary>
public class DefaultListTests
{
    private static readonly ProfanityFilter Filter = new(WordList.PersianDefault);

    [Theory]
    [InlineData("kos kesh")]
    [InlineData("kiram")]
    [InlineData("کیر")]
    [InlineData("کسکش")]
    [InlineData("fuck off")]
    [InlineData("you are a bitch")]
    public void Plain_entries_match(string text) => Assert.True(Filter.ContainsProfanity(text));

    [Theory]
    // Letters spaced or dotted apart.
    [InlineData("f u c k")]
    [InlineData("f.u.c.k this")]
    [InlineData("ک ی ر")]
    [InlineData("ک.ی.ر")]
    // A key held down.
    [InlineData("fuuuuuck")]
    [InlineData("کیییییر")]
    [InlineData("کووووون")]
    // Digits and symbols for letters.
    [InlineData("sh1t")]
    [InlineData("$hit")]
    [InlineData("sh!t")]
    [InlineData("k0s")]
    [InlineData("b1tch")]
    [InlineData("n1gga")]
    // Filler inside the word.
    [InlineData("f*ck")]
    [InlineData("ک*ی*ر")]
    // Unicode: Arabic kaf and yeh, zero-width characters, tatweel, a swash kaf.
    [InlineData("كسكش")]
    [InlineData("کس‌کش")]
    [InlineData("کس​کش")]
    [InlineData("کــیــر")]
    [InlineData("ڪیر")]
    // Cyrillic look-alikes in a Latin word: с and о.
    [InlineData("bitсh")]
    [InlineData("kоs")]
    // Spellings rather than typography: ص and ث for س, Finglish, settled misspellings.
    [InlineData("کصکش")]
    [InlineData("کث کش")]
    [InlineData("کص ننت")]
    [InlineData("koskesh")]
    [InlineData("biatch")]
    [InlineData("niqqa")]
    public void Evasions_are_caught(string text) => Assert.True(Filter.ContainsProfanity(text));

    [Theory]
    [InlineData("اینجا یه کسکش پیام داد")]
    [InlineData("motherfucker")]
    [InlineData("مادرجنده")]
    public void Anywhere_entries_match_inside_longer_words(string text) => Assert.True(Filter.ContainsProfanity(text));

    [Fact]
    public void A_phrase_entry_matches_as_a_phrase() => Assert.True(Filter.ContainsProfanity("برو پدر سگ"));

    [Theory]
    // «کس» is "person"; only its insults are listed.
    [InlineData("هر کس پلات بالاست پیام بده")]
    [InlineData("کسی هست بیاد دو نفره")]
    [InlineData("هر کس کشته زیاد میده نیاد")]
    // Words that contain a listed word, or sit next to one.
    [InlineData("اسکلت")]
    [InlineData("اسکله")]
    [InlineData("کوسه")]
    [InlineData("سکسکه")]
    [InlineData("گوهر")]
    [InlineData("جاکفشی")]
    [InlineData("فاکتور")]
    [InlineData("درگیرم این هفته")]
    [InlineData("رنجنده نباش")]
    // Words public lists ban that are ordinary Persian.
    [InlineData("ساک ورزشی")]
    [InlineData("تخم مرغ")]
    [InlineData("گه گاهی آنلاینم")]
    [InlineData("کار کردن")]
    [InlineData("گاید نصب برنامه")]
    [InlineData("ترک لر عرب فارس همه خوش اومدین")]
    // Everyday Persian sentences.
    [InlineData("سلام، وقت بخیر. سفارشم کی ارسال میشه؟")]
    [InlineData("قیمت این گوشی با تخفیف چنده؟")]
    [InlineData("کسانی که ثبت نام کردند ایمیل فعال‌سازی گرفتند")]
    [InlineData("لطفا کد تخفیف رو برام بفرستید")]
    // English that contains a listed word.
    [InlineData("Kassadin main")]
    [InlineData("Cassiopeia and assassin players")]
    [InlineData("pass the class")]
    [InlineData("grape")]
    [InlineData("therapist")]
    [InlineData("push it")]
    [InlineData("peacock cockpit")]
    [InlineData("document the build")]
    [InlineData("analysis of the results")]
    [InlineData("Scunthorpe")]
    [InlineData("title")]
    [InlineData("hello")]
    // Identifiers and numbers the look-alike folding touches.
    [InlineData("Faker#KR1")]
    [InlineData("1v1 5v5 EUW1 D4 P1 G4")]
    [InlineData("order #4521 costs $30")]
    [InlineData("رنک ۴ الماس")]
    // Finglish close to a listed word.
    [InlineData("chikar koni")]
    [InlineData("kosar")]
    [InlineData("kiriakos")]
    [InlineData("a d c")]
    [InlineData("gg wp")]
    public void Ordinary_messages_pass(string text) => Assert.Null(Filter.FindMatch(text));

    [Fact]
    public void The_bundled_list_loads()
    {
        Assert.True(WordList.PersianDefault.Count > 300);
        Assert.Contains(WordList.PersianDefault, w => w.Mode == WordMatchMode.Anywhere);
        Assert.Same(WordList.PersianDefault, WordList.PersianDefault);
    }
}
