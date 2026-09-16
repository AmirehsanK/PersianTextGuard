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
    internal static MappedText Build(string original, ReadingKind kind) =>
        Build(original, kind, new MappedText?[(int)ReadingKind.FoldedSqueezed + 1]);

    /// <summary>
    /// As <see cref="Build(string, ReadingKind)"/>, reusing and filling <paramref name="cache"/> (one
    /// slot per <see cref="ReadingKind"/>), so each reading is mapped once per message and the folded
    /// and squeezed readings are derived from the mapped normalized one rather than rebuilt.
    /// </summary>
    internal static MappedText Build(string original, ReadingKind kind, MappedText?[] cache)
    {
        if (cache[(int)kind] is { } cached)
        {
            return cached;
        }

        var mapped = kind switch
        {
            ReadingKind.Normalized => Normalized(original),
            ReadingKind.Squeezed => Derive(Build(original, ReadingKind.Normalized, cache), SqueezeStep),
            ReadingKind.Folded => Derive(Build(original, ReadingKind.Normalized, cache), FoldStep),
            _ => Derive(Build(original, ReadingKind.Folded, cache), SqueezeStep),
        };

        cache[(int)kind] = mapped;
        return mapped;
    }

    private static readonly Func<string, List<int>, string> FoldStep = static (value, map) => ProfanityFilter.Fold(value, map);

    private static readonly Func<string, List<int>, string> SqueezeStep = static (value, map) => ProfanityFilter.Squeeze(value, map);

    /// <summary>
    /// The mapped normalized reading, checked against the reading the filter searched. Folding and
    /// squeezing are plain functions of their input, so once this one matches, every reading derived
    /// from it matches too.
    /// </summary>
    private static MappedText Normalized(string original)
    {
        var map = new List<int>(original.Length);
        var text = PersianNormalizer.Normalize(original, PersianNormalization.Comparison, map);
        var expected = PersianNormalizer.Normalize(original);

        if (text != expected)
        {
            return ChunkMap(original, expected);
        }

        var exact = map.ToArray();
        return new MappedText(text, exact, exact);
    }

    private static MappedText Derive(MappedText source, Func<string, List<int>, string> step)
    {
        var stepMap = new List<int>(source.Text.Length);
        var text = step(source.Text, stepMap);

        var exact = ReferenceEquals(source.StartMap, source.EndMap);
        var starts = new int[stepMap.Count];
        var ends = exact ? starts : new int[stepMap.Count];

        for (var i = 0; i < stepMap.Count; i++)
        {
            starts[i] = source.StartMap[stepMap[i]];
            if (!exact)
            {
                ends[i] = source.EndMap[stepMap[i]];
            }
        }

        return new MappedText(text, starts, ends);
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
