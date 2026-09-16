namespace PersianTextGuard.Conformance;

/// <summary>Cases of kind <c>ordinary</c>, <c>must-match</c> and <c>robustness</c>: checking, first match, every match, censoring.</summary>
public class MatchingCaseTests
{
    public static IEnumerable<object[]> Ids() => CorpusData.IdsOf("ordinary", "must-match", "robustness");

    [Theory]
    [MemberData(nameof(Ids), DisableDiscoveryEnumeration = false)]
    public void Case_matches_the_corpus(string id) => CorpusData.Check(id);
}
