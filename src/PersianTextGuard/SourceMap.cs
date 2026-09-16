namespace PersianTextGuard;

/// <summary>A reading of a message, with where each of its characters came from in the message.</summary>
/// <remarks>
/// Two maps rather than one, so a fallback can widen in both directions: a match starting at
/// reading index <c>i</c> starts no later than <c>StartMap[i]</c>, and one ending at <c>j</c> ends no
/// earlier than <c>EndMap[j]</c>. When the mapping is exact they are the same array.
/// </remarks>
internal sealed class MappedText
{
    public MappedText(string text, int[] startMap, int[] endMap)
    {
        Text = text;
        StartMap = startMap;
        EndMap = endMap;
    }

    /// <summary>The reading, identical to the one the filter searched.</summary>
    public string Text { get; }

    /// <summary>For each reading character, the earliest message index a match starting there may start at.</summary>
    public int[] StartMap { get; }

    /// <summary>For each reading character, the latest message index a match ending there may end at.</summary>
    public int[] EndMap { get; }
}

/// <summary>
/// Rebuilds the filter's lossy readings of a message with source maps, so a position found in a
/// reading can be turned into a position in the message as it was passed.
/// </summary>
/// <remarks>
/// Maps are only built for a message that contained a match, and only for the reading the match
/// came from, so clean messages never pay for them.
/// </remarks>
internal static class SourceMap
{
    /// <summary>The <paramref name="kind"/> reading of <paramref name="original"/>, with its maps.</summary>
    /// <remarks>
    /// The mapped reading is compared with the reading the filter searches. They only differ if
    /// segment-wise normalization disagrees with whole-string normalization, in which case the maps
    /// fall back to whole whitespace-separated chunks and then to the whole message: a coarser map
    /// can hide more than the word, but never less.
    /// </remarks>
    internal static MappedText Build(string original, ReadingKind kind)
    {
        var map = new List<int>(original.Length);
        var text = PersianNormalizer.Normalize(original, PersianNormalization.Comparison, map);
        var expected = PersianNormalizer.Normalize(original);

        if (kind is ReadingKind.Folded or ReadingKind.FoldedSqueezed)
        {
            text = Apply(text, ref map, static (value, stepMap) => ProfanityFilter.Fold(value, stepMap));
            expected = ProfanityFilter.Fold(expected);
        }

        if (kind is ReadingKind.Squeezed or ReadingKind.FoldedSqueezed)
        {
            text = Apply(text, ref map, static (value, stepMap) => ProfanityFilter.Squeeze(value, stepMap));
            expected = ProfanityFilter.Squeeze(expected);
        }

        if (text == expected)
        {
            var exact = map.ToArray();
            return new MappedText(text, exact, exact);
        }

        return ChunkMap(original, expected);
    }

    /// <summary>
    /// Maps each whitespace-separated chunk of <paramref name="reading"/> to the matching chunk of
    /// <paramref name="original"/> as a whole; the whole message when the chunk counts differ.
    /// </summary>
    internal static MappedText ChunkMap(string original, string reading)
    {
        var originalChunks = Chunks(original);
        var readingChunks = Chunks(reading);
        if (originalChunks.Count != readingChunks.Count)
        {
            return WholeMessageMap(original, reading);
        }

        var starts = new int[reading.Length];
        var ends = new int[reading.Length];
        var chunk = 0;
        var start = 0;
        var end = 0;

        for (var i = 0; i < reading.Length; i++)
        {
            if (chunk < readingChunks.Count && i >= readingChunks[chunk].Start)
            {
                start = originalChunks[chunk].Start;
                end = originalChunks[chunk].Last;
                if (i == readingChunks[chunk].Last)
                {
                    chunk++;
                }
            }

            // Separators keep the previous chunk's values, which keeps both maps non-decreasing.
            starts[i] = start;
            ends[i] = end;
        }

        return new MappedText(reading, starts, ends);
    }

    /// <summary>Maps every reading character to the whole message.</summary>
    internal static MappedText WholeMessageMap(string original, string reading)
    {
        var starts = new int[reading.Length];
        var ends = new int[reading.Length];
        var last = Math.Max(0, original.Length - 1);

        for (var i = 0; i < ends.Length; i++)
        {
            ends[i] = last;
        }

        return new MappedText(reading, starts, ends);
    }

    private static string Apply(string text, ref List<int> map, Func<string, List<int>, string> step)
    {
        var stepMap = new List<int>(text.Length);
        var result = step(text, stepMap);

        var composed = new List<int>(stepMap.Count);
        foreach (var index in stepMap)
        {
            composed.Add(map[index]);
        }

        map = composed;
        return result;
    }

    private static List<(int Start, int Last)> Chunks(string text)
    {
        var chunks = new List<(int Start, int Last)>();
        var start = -1;

        for (var i = 0; i <= text.Length; i++)
        {
            var separator = i == text.Length || char.IsWhiteSpace(text[i]);
            if (!separator && start < 0)
            {
                start = i;
            }
            else if (separator && start >= 0)
            {
                chunks.Add((start, i - 1));
                start = -1;
            }
        }

        return chunks;
    }
}
