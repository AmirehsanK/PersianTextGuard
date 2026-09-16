namespace PersianTextGuard.Conformance;

/// <summary>Cases of kind <c>word-list-parsing</c>, <c>category-selection</c> and <c>mask-validation</c>.</summary>
public class ListCaseTests
{
    public static IEnumerable<object[]> Ids() => CorpusData.IdsOf("word-list-parsing", "category-selection", "mask-validation");

    [Theory]
    [MemberData(nameof(Ids), DisableDiscoveryEnumeration = false)]
    public void Case_matches_the_corpus(string id) => CorpusData.Check(id);
}
