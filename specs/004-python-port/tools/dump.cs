// Dumps the Unicode primitives the .NET matcher uses, one line per UTF-16 unit and per code point.
using System.Globalization;
using System.Text;

static string Cat(UnicodeCategory c) => c switch
{
    UnicodeCategory.UppercaseLetter => "Lu", UnicodeCategory.LowercaseLetter => "Ll",
    UnicodeCategory.TitlecaseLetter => "Lt", UnicodeCategory.ModifierLetter => "Lm",
    UnicodeCategory.OtherLetter => "Lo", UnicodeCategory.NonSpacingMark => "Mn",
    UnicodeCategory.SpacingCombiningMark => "Mc", UnicodeCategory.EnclosingMark => "Me",
    UnicodeCategory.DecimalDigitNumber => "Nd", UnicodeCategory.LetterNumber => "Nl",
    UnicodeCategory.OtherNumber => "No", UnicodeCategory.SpaceSeparator => "Zs",
    UnicodeCategory.LineSeparator => "Zl", UnicodeCategory.ParagraphSeparator => "Zp",
    UnicodeCategory.Control => "Cc", UnicodeCategory.Format => "Cf",
    UnicodeCategory.Surrogate => "Cs", UnicodeCategory.PrivateUse => "Co",
    UnicodeCategory.ConnectorPunctuation => "Pc", UnicodeCategory.DashPunctuation => "Pd",
    UnicodeCategory.OpenPunctuation => "Ps", UnicodeCategory.ClosePunctuation => "Pe",
    UnicodeCategory.InitialQuotePunctuation => "Pi", UnicodeCategory.FinalQuotePunctuation => "Pf",
    UnicodeCategory.OtherPunctuation => "Po", UnicodeCategory.MathSymbol => "Sm",
    UnicodeCategory.CurrencySymbol => "Sc", UnicodeCategory.ModifierSymbol => "Sk",
    UnicodeCategory.OtherSymbol => "So", _ => "Cn",
};

static string Hex(string s)
{
    var sb = new StringBuilder();
    for (var i = 0; i < s.Length; i++)
    {
        if (i > 0) sb.Append(' ');
        var cp = char.IsSurrogatePair(s, i) ? char.ConvertToUtf32(s, i++) : s[i];
        sb.Append(cp.ToString("X4"));
    }
    return sb.ToString();
}

static string Norm(string s, NormalizationForm f)
{
    try { return Hex(s.Normalize(f)); } catch (ArgumentException) { return "THROWS"; }
}

var dir = args[0];
using (var units = new StreamWriter(Path.Combine(dir, "dotnet-units.txt")))
{
    for (var u = 0; u <= 0xFFFF; u++)
    {
        var c = (char)u;
        var lower = char.ToLowerInvariant(c);
        units.Write($"{u:X4} {Cat(CharUnicodeInfo.GetUnicodeCategory(c))} {(char.IsWhiteSpace(c) ? 1 : 0)} {(int)lower:X4}\n");
    }
}
using (var points = new StreamWriter(Path.Combine(dir, "dotnet-points.txt")))
{
    for (var cp = 0; cp <= 0x10FFFF; cp++)
    {
        if (cp is >= 0xD800 and <= 0xDFFF) continue;
        var s = char.ConvertFromUtf32(cp);
        points.Write($"{cp:X4} {Cat(CharUnicodeInfo.GetUnicodeCategory(cp))} {Norm(s, NormalizationForm.FormKC)} | {Norm(s, NormalizationForm.FormD)}\n");
    }
}
Console.WriteLine($".NET {Environment.Version}");
