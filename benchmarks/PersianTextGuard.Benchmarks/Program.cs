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
}
