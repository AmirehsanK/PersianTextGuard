namespace PersianTextGuard.Conformance;

/// <summary>Cases of kind <c>normalization</c> and <c>tokenization</c>.</summary>
public class TextCaseTests
{
    public static IEnumerable<object[]> Ids() => CorpusData.IdsOf("normalization", "tokenization");

    [Theory]
    [MemberData(nameof(Ids), DisableDiscoveryEnumeration = false)]
    public void Case_matches_the_corpus(string id) => CorpusData.Check(id);
}
