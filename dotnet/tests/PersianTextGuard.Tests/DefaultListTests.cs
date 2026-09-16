namespace PersianTextGuard.Tests;

/// <summary>
/// The filter against the bundled list. A filter fails in two ways, and both come from the list
/// meeting the matcher: an evasion slipping through, and an ordinary message being rejected. The
/// second is worse, because nobody reports it and people just learn the site is broken.
/// </summary>
public class DefaultListTests
{
    private static readonly ProfanityFilter Filter = new(WordList.PersianDefault);
    private static readonly ProfanityFilter Everything = new(WordList.All);

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
    // Symbols standing in for letters, rather than added between them.
    [InlineData("f**k")]
    [InlineData("c*nt")]
    [InlineData("f@ck")]
    [InlineData("a$$hole")]
    [InlineData("ج*نده")]
    // Punctuation inside a word, which splits it into pieces no entry matches.
    [InlineData("ج.نده")]
    [InlineData("kos_kesh")]
    [InlineData("bi-namoos")]
    // A word split once, rather than letter by letter.
    [InlineData("fu ck")]
    [InlineData("کی ر")]
    // Accents and letters from other blocks.
    [InlineData("fück")]
    [InlineData("shíť")]
    [InlineData("ƒuck")]
    [InlineData("🅵🆄🅲🅺")]
    // An emoji next to, or inside, the word.
    [InlineData("کیر😂")]
    [InlineData("f🖕ck")]
    // Persian quotation marks, and an @ before the word.
    [InlineData("«کیر»")]
    [InlineData("@kir")]
    public void Evasions_found_by_testing_1_0_1_are_caught(string text) => Assert.True(Filter.ContainsProfanity(text));

    [Theory]
    // Persian takes suffixes; the matcher strips them rather than the list carrying every form.
    [InlineData("جنده‌ها")]
    [InlineData("جندها")]
    [InlineData("کیرتون")]
    [InlineData("کونیا")]
    [InlineData("کسکشا")]
    [InlineData("حرامزاده‌ها")]
    [InlineData("بی ناموس‌ها")]
    [InlineData("اسکلات")]
    public void Persian_suffixes_are_matched(string text) => Assert.True(Filter.ContainsProfanity(text));

    [Theory]
    [InlineData("کثافت")]
    [InlineData("کسخول")]
    [InlineData("گه نخور")]
    [InlineData("گوه‌خوری")]
    [InlineData("ریدی")]
    [InlineData("بگا")]
    [InlineData("کیرخور")]
    [InlineData("بی پدر")]
    [InlineData("keer")]
    [InlineData("kuni")]
    [InlineData("kossher")]
    [InlineData("kesafat")]
    [InlineData("ridi")]
    [InlineData("goh")]
    [InlineData("kill urself")]
    public void Spellings_missing_from_1_0_1_are_covered(string text) => Assert.True(Filter.ContainsProfanity(text));

    [Theory]
    // Entries amirshnll/Persian-Swear-Words carries that the matcher did not already reach.
    [InlineData("گایدن")]
    [InlineData("بکیرم")]
    [InlineData("به تخم اقام")]
    [InlineData("سگ تو روحت")]
    [InlineData("نرکده")]
    [InlineData("اوب")]
    [InlineData("دول ننه")]
    public void Entries_from_the_persian_swear_words_dataset_match(string text) =>
        Assert.True(Filter.ContainsProfanity(text));

    [Theory]
    [InlineData("چس")]
    [InlineData("گوزو")]
    [InlineData("خفه شو")]
    [InlineData("دهنتو ببند")]
    [InlineData("سیکتیر")]
    [InlineData("بچه کونی")]
    [InlineData("دهن سرویس")]
    [InlineData("پدر سوخته")]
    [InlineData("کوس خل")]
    [InlineData("عن دونی")]
    [InlineData("حروم‌لقمه")]
    [InlineData("دهنتو گاییدم")]
    public void Additional_persian_entries_match(string text) => Assert.True(Everything.ContainsProfanity(text));

    [Theory]
    [InlineData("مادربه‌خطا")]
    [InlineData("مادر به خطا")]
    [InlineData("راگوزتو ببند")]
    [InlineData("ببند گاله رو")]
    [InlineData("آشغال کله")]
    [InlineData("عن تیلیت")]
    [InlineData("پسته خانم")]
    [InlineData("چلغوز")]
    [InlineData("madarbe khata")]
    [InlineData("ragoozeto bebband")]
    [InlineData("beband gale ro")]
    [InlineData("ashghal kole")]
    [InlineData("ooshkol")]
    [InlineData("shaskol")]
    public void Web_sourced_finglish_entries_match(string text) => Assert.True(Everything.ContainsProfanity(text));

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
    [InlineData("هر کس ده تا سکه بده برنده است")]
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
    [InlineData("کوثر و کیانا")]
    [InlineData("کیرکگور فیلسوف بود")]
    // Words public lists ban that are ordinary Persian.
    [InlineData("ساک ورزشی")]
    [InlineData("تخم مرغ")]
    [InlineData("گه گاهی آنلاینم")]
    [InlineData("کار کردن")]
    [InlineData("گاید نصب برنامه")]
    [InlineData("دول اروپایی")]
    [InlineData("خر پول شده")]
    [InlineData("ترک لر عرب فارس همه خوش اومدین")]
    // Everyday Persian sentences.
    [InlineData("سلام، وقت بخیر. سفارشم کی ارسال میشه؟")]
    [InlineData("قیمت این گوشی با تخفیف چنده؟")]
    [InlineData("کسانی که ثبت نام کردند ایمیل فعال‌سازی گرفتند")]
    [InlineData("لطفا کد تخفیف رو برام بفرستید")]
    [InlineData("این کتاب و دفتر رو بردار")]
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
    [InlineData("shiitake mushrooms")]
    [InlineData("he mishit the ball")]
    [InlineData("Matsushita")]
    [InlineData("he sniggered at the joke")]
    [InlineData("Pussycat Dolls")]
    [InlineData("the ball was hit for six")]
    // Identifiers and numbers the look-alike folding touches.
    [InlineData("Faker#KR1")]
    [InlineData("1v1 5v5 EUW1 D4 P1 G4")]
    [InlineData("order #4521 costs $30")]
    [InlineData("رنک ۴ الماس")]
    [InlineData("I paid 455 dollars")]
    [InlineData("call 0912 455 1234")]
    [InlineData("۴۵۵ تومان شد")]
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

    [Fact]
    public void Every_bundled_entry_has_a_category()
    {
        Assert.DoesNotContain(WordList.All, w => w.Category == WordCategory.Uncategorized);
        Assert.All(
            Enum.GetValues(typeof(WordCategory)).Cast<WordCategory>().Where(c => c != WordCategory.Uncategorized),
            category => Assert.Contains(WordList.All, w => w.Category == category));
    }

    [Fact]
    public void The_default_list_is_everything_except_mild()
    {
        Assert.DoesNotContain(WordList.PersianDefault, w => w.Category == WordCategory.Mild);
        Assert.Contains(WordList.All, w => w.Category == WordCategory.Mild);
        Assert.True(WordList.All.Count > WordList.PersianDefault.Count);
    }

    [Fact]
    public void Mild_words_are_ordinary_until_you_ask_for_them()
    {
        Assert.False(Filter.ContainsProfanity("این فیلم آشغال بود"));
        Assert.True(Everything.ContainsProfanity("این فیلم آشغال بود"));
    }

    [Fact]
    public void Categories_can_be_picked()
    {
        var slursOnly = new ProfanityFilter(WordList.Bundled(WordCategory.Slur));

        Assert.True(slursOnly.ContainsProfanity("faggot"));
        Assert.False(slursOnly.ContainsProfanity("fuck"));
    }

    [Fact]
    public void A_match_reports_the_category()
    {
        Assert.Equal(WordCategory.Slur, Filter.FindMatch("nigger")!.Word.Category);
        Assert.Equal(WordCategory.Sexual, Filter.FindMatch("کیر")!.Word.Category);
        Assert.Equal(WordCategory.Harassment, Filter.FindMatch("kys")!.Word.Category);
    }
}
