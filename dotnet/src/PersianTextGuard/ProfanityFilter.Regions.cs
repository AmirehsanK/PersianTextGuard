using System.Text;

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

    // Every hidden word gets the same mask, so its length says nothing about the word (FR-012).
    private const int MaskLength = 4;

    /// <summary>
    /// Where <paramref name="hit"/> is in <paramref name="original"/>, widened to the whole words it
    /// touches. The reading's source map is built on first use and kept in <paramref name="mapCache"/>,
    /// so a message with many hits maps each reading once.
    /// </summary>
    private static Candidate ToCandidate(string original, in Hit hit, MappedText?[] mapCache)
    {
        var mapped = SourceMap.Build(original, hit.Reading, mapCache);

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

    /// <summary>
    /// Candidates that overlap become one match (FR-007): the region is their union, the entry is
    /// the one whose hit covered the most characters before widening — so "motherfucker" beats the
    /// "fuck" inside it even though both widen to the same word — with ties going to the entry
    /// listed first, and the evasion is the least that entry needed.
    /// </summary>
    private static List<ProfanityMatch> Merge(List<Candidate> candidates)
    {
        candidates.Sort(static (a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : b.End.CompareTo(a.End));

        var matches = new List<ProfanityMatch>();
        var i = 0;

        while (i < candidates.Count)
        {
            var clusterStart = candidates[i].Start;
            var clusterEnd = candidates[i].End;
            var best = candidates[i];

            var next = i + 1;
            while (next < candidates.Count && candidates[next].Start < clusterEnd)
            {
                var candidate = candidates[next];
                clusterEnd = Math.Max(clusterEnd, candidate.End);
                if (candidate.HitLength > best.HitLength
                    || (candidate.HitLength == best.HitLength && candidate.Order < best.Order))
                {
                    best = candidate;
                }

                next++;
            }

            var evasion = best.Evasion;
            for (var k = i; k < next; k++)
            {
                if (candidates[k].Word == best.Word && candidates[k].Evasion < evasion)
                {
                    evasion = candidates[k].Evasion;
                }
            }

            matches.Add(new ProfanityMatch(best.Word, evasion)
            {
                Index = clusterStart,
                Length = clusterEnd - clusterStart,
            });

            i = next;
        }

        return matches;
    }

    /// <summary>
    /// Replaces each match's region with the mask, then keeps going until the output is clean.
    /// </summary>
    /// <remarks>
    /// The mask character is usually filler the filter drops to catch "f*ck", so masking can join
    /// the letters around a mask into a new word: "k kos i kos r" masks to "k **** i **** r", which
    /// reads as "kir" (research R6). Each extra pass replaces letters with a mask that has none, so
    /// the letters only ever run out; the cap is a guard against a bug, not an expected limit, and
    /// if it is ever reached the whole text becomes one mask rather than leak a word.
    /// </remarks>
    private string CensorMatches(string text, IReadOnlyList<ProfanityMatch> matches, char maskCharacter)
    {
        var mask = new string(maskCharacter, MaskLength);
        var censored = ApplyMask(text, matches, mask);
        var passesLeft = PersianNormalizer.Tokenize(text).Length + 1;

        while (ContainsProfanity(censored))
        {
            if (passesLeft-- == 0)
            {
                return mask;
            }

            censored = ApplyMask(censored, FindMatches(censored), mask);
        }

        return censored;
    }

    private static string ApplyMask(string text, IReadOnlyList<ProfanityMatch> matches, string mask)
    {
        var sb = new StringBuilder(text.Length);
        var copied = 0;

        foreach (var match in matches)
        {
            sb.Append(text, copied, match.Index - copied);
            sb.Append(mask);
            copied = match.Index + match.Length;
        }

        sb.Append(text, copied, text.Length - copied);
        return sb.ToString();
    }

    private static bool IsSurrogatePairAt(string text, int index) =>
        char.IsHighSurrogate(text[index]) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]);
}
