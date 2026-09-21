// Runs the four corpus inputs that contain a lone surrogate as recorded and with U+FFFD in its place
// (spec 005, research R4). Run from this folder: dotnet run fffd.cs
#:project ../../../dotnet/src/PersianTextGuard/PersianTextGuard.csproj
using PersianTextGuard;
var filter = new ProfanityFilter(WordList.PersianDefault);
string Show(string s) { var sb = new System.Text.StringBuilder(); foreach (var c in s) sb.Append(c < 128 && c >= 32 ? c.ToString() : $"<{(int)c:X4}>"); return sb.ToString(); }
string Describe(string text) {
  var ms = string.Join("; ", filter.FindMatches(text).Select(m => $"{m.Word.Text}@{m.Index}+{m.Length} {m.Evasion}"));
  return $"contains={filter.ContainsProfanity(text)} first={filter.FindMatch(text)?.Word.Text} matches=[{ms}] censored={Show(filter.Censor(text))}";
}
foreach (var (id, text) in new[] {
  ("robustness-find-matches-tests-001", "hi \uD83D kir"),
  ("robustness-consistency-tests-001", "hi \uD83D"),
  ("robustness-consistency-tests-002", "\uDE00 hi"),
  ("robustness-consistency-tests-003", "kir \uD83D kos"),
}) {
  var replaced = new string(text.Select(c => char.IsSurrogate(c) ? '\uFFFD' : c).ToArray());
  Console.WriteLine(id);
  Console.WriteLine("  lone : " + Describe(text));
  Console.WriteLine("  FFFD : " + Describe(replaced));
}
