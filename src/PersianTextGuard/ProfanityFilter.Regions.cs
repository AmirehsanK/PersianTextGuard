namespace PersianTextGuard;

/// <summary>A hit mapped back to the message and widened to whole words.</summary>
/// <param name="Start">First character of the region in the message as passed.</param>
/// <param name="End">One past the region's last character.</param>
/// <param name="HitLength">Characters the hit itself covered, before widening to whole words.</param>
/// <param name="Word">The entry that matched.</param>
/// <param name="Order">The entry's position in the word list.</param>
/// <param name="Evasion">What had to be undone to find it.</param>
internal readonly record struct Candidate(int Start, int End, int HitLength, BannedWord Word, int Order, EvasionKind Evasion);

public sealed partial class ProfanityFilter
{
    // One source-map slot per ReadingKind; keep in step with the enum.
    private const int ReadingKindCount = (int)ReadingKind.FoldedSqueezed + 1;

    /// <summary>
    /// Where <paramref name="hit"/> is in <paramref name="original"/>, widened to the whole words it
    /// touches. The reading's source map is built on first use and kept in <paramref name="mapCache"/>,
    /// so a message with many hits maps each reading once.
    /// </summary>
    private static Candidate ToCandidate(string original, in Hit hit, MappedText?[] mapCache)
    {
        var mapped = mapCache[(int)hit.Reading] ??= SourceMap.Build(original, hit.Reading);

        var start = mapped.StartMap[hit.Start];
        var last = mapped.EndMap[hit.End - 1];
        var end = last + (IsSurrogatePairAt(original, last) ? 2 : 1);
        var hitLength = end - start;

        // Whole words (FR-011): the tokenizer's own rule decides where a word ends, so a suffix
        // joined by a zero-width non-joiner comes along and an emoji or comma does not. The rule
        // answers the same for both halves of a surrogate pair, so the region cannot split one.
        while (start > 0 && PersianNormalizer.IsWordCharacter(original, start - 1))
        {
            start--;
        }

        while (end < original.Length && PersianNormalizer.IsWordCharacter(original, end))
        {
            end++;
        }

        return new Candidate(start, end, hitLength, hit.Word, hit.Order, hit.Evasion);
    }

    private static bool IsSurrogatePairAt(string text, int index) =>
        char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]);
}
