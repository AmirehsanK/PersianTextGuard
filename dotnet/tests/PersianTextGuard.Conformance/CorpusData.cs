using System.Reflection;

namespace PersianTextGuard.Conformance;

/// <summary>The corpus this build points at, loaded once, and the checks every case theory shares.</summary>
internal static class CorpusData
{
    private static readonly Lazy<Corpus> Loaded = new(() => Corpus.Load(Directory));

    /// <summary>The <c>conformance/</c> path written into the assembly at build time.</summary>
    public static string Directory =>
        typeof(CorpusData).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "ConformanceDirectory").Value!;

    public static Corpus Corpus => Loaded.Value;

    /// <summary>Ids of the cases of the given kinds; plain ASCII, so discovery never serializes case text.</summary>
    public static IEnumerable<object[]> IdsOf(params string[] kinds) =>
        Corpus.Cases.Where(c => Array.IndexOf(kinds, c.Kind) >= 0).Select(c => new object[] { c.Id });

    /// <summary>Checks one case: recorded, kind rules hold, and every expected field equals this port's result.</summary>
    public static void Check(string id)
    {
        var corpusCase = Corpus.Find(id);

        if (corpusCase.IsPending)
        {
            Assert.Fail(Corpus.FailureMessage(corpusCase, "pending case — run the fill-in tool: dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill", []));
        }

        var violations = Corpus.CheckKindRules(corpusCase);
        if (violations.Count > 0)
        {
            Assert.Fail(Corpus.FailureMessage(corpusCase, "breaks its kind rule", violations));
        }

        var actual = Corpus.Evaluate(corpusCase);
        var differences = Corpus.Compare(corpusCase.Json["expected"], actual);
        if (differences.Count > 0)
        {
            Assert.Fail(Corpus.FailureMessage(
                corpusCase,
                $"{differences.Count} field(s) differ",
                differences.Select(d => $"{d.Path}: expected {d.Expected} actual {d.Actual}")));
        }
    }
}
