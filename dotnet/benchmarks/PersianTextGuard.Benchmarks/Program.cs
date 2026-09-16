using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using PersianTextGuard;

BenchmarkSwitcher.FromAssembly(typeof(FilterBenchmarks).Assembly).Run(args);

[MemoryDiagnoser]
public class FilterBenchmarks
{
    private static readonly ProfanityFilter Filter = new(WordList.PersianDefault);

    private const string CleanShort = "سلام، سفارشم کی ارسال میشه؟";

    private const string CleanLong =
        "سلام وقت بخیر. من هفته پیش یک گوشی از فروشگاه شما سفارش دادم و هنوز به دستم نرسیده. " +
        "کد رهگیری را هم در پنل کاربری پیدا نکردم. لطفا بررسی کنید و نتیجه را از طریق ایمیل " +
        "یا پیامک اطلاع بدید. اگر امکانش هست هزینه ارسال را هم برگردانید. ممنون از پشتیبانی خوبتان.";

    private const string Evasion = "ye k0s kesh inja f.u.c.k";

    private const string DirtyShort = "this is kir";

    private const string DirtyMixed = "این کیر و f u c k و sh1t";

    // CleanLong with three banned words inserted after its first three sentences: about 60 words.
    private const string DirtyLong =
        "سلام وقت بخیر. کیر من هفته پیش یک گوشی از فروشگاه شما سفارش دادم و هنوز به دستم نرسیده. f.u.c.k " +
        "کد رهگیری را هم در پنل کاربری پیدا نکردم. جنده‌ها لطفا بررسی کنید و نتیجه را از طریق ایمیل " +
        "یا پیامک اطلاع بدید. اگر امکانش هست هزینه ارسال را هم برگردانید. ممنون از پشتیبانی خوبتان.";

    [Benchmark(Baseline = true)]
    public bool CleanShortMessage() => Filter.ContainsProfanity(CleanShort);

    [Benchmark]
    public bool CleanLongMessage() => Filter.ContainsProfanity(CleanLong);

    [Benchmark]
    public bool EvasiveMessage() => Filter.ContainsProfanity(Evasion);

    [Benchmark]
    public string NormalizeLongMessage() => PersianNormalizer.Normalize(CleanLong);

    [Benchmark]
    public int BuildFilterFromDefaultList() => new ProfanityFilter(WordList.PersianDefault).Count;

    [Benchmark]
    public int FindMatchesClean() => Filter.FindMatches(CleanShort).Count;

    [Benchmark]
    public int FindMatchesDirty() => Filter.FindMatches(DirtyMixed).Count;

    [Benchmark]
    public string CensorShortDirty() => Filter.Censor(DirtyShort);

    [Benchmark]
    public string CensorLongDirty() => Filter.Censor(DirtyLong);
}
