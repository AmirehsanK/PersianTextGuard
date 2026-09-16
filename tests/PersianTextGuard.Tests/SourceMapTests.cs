namespace PersianTextGuard.Tests;

/// <summary>
/// The source maps that turn a position in one of the filter's lossy readings back into a position
/// in the message as it was passed. Positions are only as good as these maps, so the maps are
/// checked directly: same text as the unmapped readings, and indexes that stay inside the message
/// and never go backwards.
/// </summary>
public class SourceMapTests
{
    public static IEnumerable<object[]> Samples() => new[]
    {
        "كتاب‌هاي  ۱۲ ABC",
        "ﻛﻴﺮ",
        "fúck",
        "ú",
        "کــیــر",
        "کِیر",
        "سسسسلام",
        "sh!t 455",
        "🅵🆄🅲🅺",
        "f🖕ck",
        "hi \uD83D",
        "   a \t b  ",
        "ک​یر",
        "ج‏نده",
    }.Select(sample => new object[] { sample });

    [Theory]
    [MemberData(nameof(Samples), DisableDiscoveryEnumeration = true)]
    public void Mapped_normalization_produces_the_same_text(string text)
    {
        var map = new List<int>();

        var mapped = PersianNormalizer.Normalize(text, PersianNormalization.Comparison, map);

        Assert.Equal(PersianNormalizer.Normalize(text), mapped);
        AssertValidMap(map, mapped.Length, text.Length);
    }

    [Theory]
    [MemberData(nameof(Samples), DisableDiscoveryEnumeration = true)]
    public void Mapped_fold_and_squeeze_produce_the_same_text(string text)
    {
        var normalized = PersianNormalizer.Normalize(text);

        var foldMap = new List<int>();
        var folded = ProfanityFilter.Fold(normalized, foldMap);
        Assert.Equal(ProfanityFilter.Fold(normalized), folded);
        AssertValidMap(foldMap, folded.Length, normalized.Length);

        var squeezeMap = new List<int>();
        var squeezed = ProfanityFilter.Squeeze(normalized, squeezeMap);
        Assert.Equal(ProfanityFilter.Squeeze(normalized), squeezed);
        AssertValidMap(squeezeMap, squeezed.Length, normalized.Length);
    }

    [Theory]
    [MemberData(nameof(Samples), DisableDiscoveryEnumeration = true)]
    public void Every_reading_maps_back_into_the_original(string text)
    {
        foreach (ReadingKind kind in Enum.GetValues(typeof(ReadingKind)))
        {
            var mapped = SourceMap.Build(text, kind);

            Assert.Equal(ExpectedReading(text, kind), mapped.Text);
            AssertValidMap(mapped.StartMap, mapped.Text.Length, text.Length);
            AssertValidMap(mapped.EndMap, mapped.Text.Length, text.Length);
        }
    }

    [Fact]
    public void Presentation_forms_map_one_to_one()
    {
        var map = new List<int>();

        Assert.Equal("کیر", PersianNormalizer.Normalize("ﻛﻴﺮ", PersianNormalization.Comparison, map));
        Assert.Equal([0, 1, 2], map);
    }

    [Fact]
    public void Removed_invisible_characters_are_skipped_in_the_map()
    {
        var map = new List<int>();

        Assert.Equal("کیر", PersianNormalizer.Normalize("ک​یر", PersianNormalization.Comparison, map));
        Assert.Equal([0, 2, 3], map);
    }

    [Fact]
    public void Collapsed_repeats_map_to_the_letters_they_kept()
    {
        const string text = "سسسسلام";
        var map = new List<int>();

        var normalized = PersianNormalizer.Normalize(text, PersianNormalization.Comparison, map);

        for (var i = 0; i < normalized.Length; i++)
        {
            Assert.Equal(normalized[i], text[map[i]]);
        }
    }

    [Fact]
    public void Fallback_maps_only_ever_widen()
    {
        const string text = "ab kir cd";

        var chunk = SourceMap.ChunkMap(text, text);
        Assert.True(chunk.StartMap[3] <= 3);
        Assert.True(chunk.EndMap[5] >= 5);

        var whole = SourceMap.WholeMessageMap(text, text);
        Assert.Equal(0, whole.StartMap[3]);
        Assert.Equal(text.Length - 1, whole.EndMap[5]);
    }

    [Fact]
    public void A_chunk_count_mismatch_falls_back_to_the_whole_message()
    {
        var mapped = SourceMap.ChunkMap("ab kir", "abkir x y");

        Assert.All(mapped.StartMap, start => Assert.Equal(0, start));
        Assert.All(mapped.EndMap, end => Assert.Equal(5, end));
    }

    private static string ExpectedReading(string text, ReadingKind kind)
    {
        var normalized = PersianNormalizer.Normalize(text);
        return kind switch
        {
            ReadingKind.Normalized => normalized,
            ReadingKind.Squeezed => ProfanityFilter.Squeeze(normalized),
            ReadingKind.Folded => ProfanityFilter.Fold(normalized),
            _ => ProfanityFilter.Squeeze(ProfanityFilter.Fold(normalized)),
        };
    }

    private static void AssertValidMap(IReadOnlyList<int> map, int outputLength, int sourceLength)
    {
        Assert.Equal(outputLength, map.Count);

        for (var i = 0; i < map.Count; i++)
        {
            Assert.InRange(map[i], 0, sourceLength - 1);
            if (i > 0)
            {
                Assert.True(map[i] >= map[i - 1], $"map goes backwards at {i}: {map[i - 1]} then {map[i]}");
            }
        }
    }
}
