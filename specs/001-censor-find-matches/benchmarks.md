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
