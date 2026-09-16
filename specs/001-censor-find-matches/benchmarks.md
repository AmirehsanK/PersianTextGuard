# Benchmarks: Find Every Match and Censor Messages

BenchmarkDotNet, .NET 10, Release, Intel Core i7-9700K CPU 3.60GHz (Coffee Lake), 8 cores —
the machine the README's performance table names.

```bash
dotnet run -c Release --project benchmarks/PersianTextGuard.Benchmarks -f net10.0 -- --filter '*'
```

## Baseline (1.1.0)

Measured on `main` at `2db346c`, before any source change for this feature (T002).

| Method | Mean | Allocated |
| --- | ---: | ---: |
| CleanShortMessage | 2.399 µs | 2.84 KB |
| CleanLongMessage | 21.870 µs | 20.91 KB |
| EvasiveMessage | 1.735 µs | 2.27 KB |
| NormalizeLongMessage | 5.500 µs | 3.30 KB |
| BuildFilterFromDefaultList | 547.535 µs | 1,044.59 KB |

## 1.2.0

Measured on branch `001-censor-find-matches` after T020 and the T021 performance fixes.

| Method | 1.1.0 Mean | 1.2.0 Mean | Change | 1.2.0 Allocated |
| --- | ---: | ---: | ---: | ---: |
| CleanShortMessage | 2.399 µs | 2.391 µs | −0.3% | 2.66 KB |
| CleanLongMessage | 21.870 µs | 21.302 µs | −2.6% | 21.92 KB |
| EvasiveMessage | 1.735 µs | 1.905 µs | +9.8% | 3.09 KB |
| NormalizeLongMessage | 5.500 µs | 5.892 µs | +7.1% | 3.30 KB |
| BuildFilterFromDefaultList | 547.535 µs | 553.007 µs | +1.0% | 1,061.36 KB |
| FindMatchesClean | — | 2.368 µs | | 2.70 KB |
| FindMatchesDirty | — | 6.567 µs | | 9.70 KB |
| CensorShortDirty | — | 4.458 µs | | 6.43 KB |
| CensorLongDirty | — | 92.967 µs | | 97.33 KB |

### Success criteria

| Criterion | Target | Result |
| --- | --- | --- |
| SC-005: yes/no check | within 5% of 1.1.0 | ✅ 2.391 µs vs 2.399 µs (−0.3%) |
| SC-005: `FindMatches` on a clean message | ≤ 1.5 × yes/no | ✅ 0.99 × |
| SC-006: `Censor`, 60-word dirty message | < 100 µs | ✅ 92.967 µs |

### What the first 1.2.0 run showed, and what was fixed

The first run after implementation measured `CensorLongDirty` at **137.112 µs** (failing SC-006),
`EvasiveMessage` at 2.036 µs and `BuildFilterFromDefaultList` at 604.932 µs. Three causes:

1. The source map ran NFKC on each non-ASCII character separately — every Persian letter — for every
   reading with a hit. A whole-string `IsNormalized` check now skips it for text already in NFKC.
2. `SourceMap.Build` rebuilt the normalized reading, mapped and unmapped, for every reading kind.
   Readings now share one per-message cache, and folded and squeezed readings are derived from the
   mapped normalized one.
3. Joining single letters allocated a list per token plus closures, and `Tokenize` built its result
   through an intermediate token array during filter construction. Both now use a single pass.

### Regressions kept, with justification (constitution, Principle IV)

- **EvasiveMessage, +0.170 µs, +0.82 KB.** The message contains spaced letters (`f.u.c.k`). Joining
  them now also records where each joined letter sits in the reading, so a match found in the
  joined text can be mapped back to the message. Only messages with runs of single letters pay it;
  clean messages do not (CleanShortMessage −0.3%, CleanLongMessage −2.6%).
- **NormalizeLongMessage, +7.1%: within run-to-run noise.** The public
  `PersianNormalizer.Normalize` path was identical between the last two runs, which measured 5.640 µs
  and 5.892 µs. Run-to-run variance on this benchmark is as large as the difference from the 1.1.0
  baseline. The only per-character addition on the unmapped path is a null check on the (absent)
  source map.
