# Contract: Public API additions in 1.2.0

**Feature**: [../spec.md](../spec.md) | **Data model**: [../data-model.md](../data-model.md)

The package's external interface is its public .NET API. This contract lists every public change in
1.2.0. Anything not listed keeps its 1.1.0 signature and behaviour (FR-019).

## `ProfanityFilter`

```csharp
public sealed class ProfanityFilter
{
    // Unchanged:
    public ProfanityFilter(IEnumerable<BannedWord> words, ProfanityFilterOptions? options = null);
    public int Count { get; }
    public bool ContainsProfanity(string? text);
    public ProfanityMatch? FindMatch(string? text);          // now also sets Index and Length

    // New:
    public IReadOnlyList<ProfanityMatch> FindMatches(string? text);
    public string Censor(string? text);
    public string Censor(string? text, char maskCharacter);
}
```

### `FindMatches(string? text)`

| Aspect | Contract |
| --- | --- |
| Returns | Every banned word in `text`, ordered by `Index`, with no overlapping regions (FR-001, FR-004, FR-007). |
| Clean text | An empty list. Never `null` (FR-006). |
| `null`, empty or whitespace-only text | An empty list (FR-017). |
| Invalid text (lone surrogates) | Matches found in the rest of the text; never throws (FR-017). |
| Repeats | One match per occurrence (FR-005). Occurrences inside the same word ("fuckfuck") share one whole-word region and are one match (FR-007, FR-011). |
| Overlaps | One match per overlapping cluster: union region; the entry whose hit covered the most characters before widening to whole words, ties by list order (FR-007). |
| Positions | `Index` and `Length` refer to `text` exactly as passed (FR-003, FR-021) and cover whole words (FR-011). |
| Settings | Uses the filter's word list and `ProfanityFilterOptions` (FR-018). |
| Throws | Never. |
| Thread safety | Safe to call concurrently on a shared filter (FR-020). |
| Result mutability | The returned list is read-only; callers must not rely on its concrete type. |

### `Censor(string? text)`

Same as `Censor(text, '*')`.

### `Censor(string? text, char maskCharacter)`

| Aspect | Contract |
| --- | --- |
| Returns | `text` with each `FindMatches` region replaced by `maskCharacter` repeated four times (FR-008, FR-011 to FR-013). |
| Outside regions | Every character copied exactly as passed — no normalisation (FR-009). |
| Clean text | The same string instance that was passed (FR-010). |
| `null` text | `string.Empty` (FR-017). |
| Empty or whitespace-only text | The same instance that was passed. |
| Output guarantee | `ContainsProfanity(result)` is `false` for the same filter (FR-015). |
| Length | May differ from `text.Length`; positions from `FindMatches(text)` do not apply to the result (FR-021). |
| Throws | `ArgumentException` (parameter `maskCharacter`) when `maskCharacter` is a letter, digit, whitespace, control character or surrogate half (FR-014). Checked before scanning, including for `null` text. Never throws because of `text`. |
| Thread safety | Safe to call concurrently on a shared filter (FR-020). |

## `ProfanityMatch`

```csharp
public sealed record ProfanityMatch(BannedWord Word, EvasionKind Evasion)
{
    // New:
    /// <summary>Where the matched words start in the text as it was passed.</summary>
    public int Index { get; init; }

    /// <summary>How many characters the matched words span.</summary>
    public int Length { get; init; }
}
```

| Aspect | Contract |
| --- | --- |
| Construction | The positional constructor is unchanged. `Index` and `Length` default to 0 when a caller constructs the record themselves. |
| From `FindMatch` / `FindMatches` | `Index ≥ 0`, `Length ≥ 1`, `Index + Length ≤ text.Length`. |
| Equality | Includes `Index` and `Length`. **Behaviour change**: a 1.2.0 `FindMatch` result no longer equals `new ProfanityMatch(word, evasion)`. Called out in release notes. |

## Consistency guarantees (FR-016)

For any filter `f` and any string `t`:

```text
f.ContainsProfanity(t) == (f.FindMatch(t) != null)
                       == (f.FindMatches(t).Count > 0)
                       == !ReferenceEquals(f.Censor(t), t)      // for non-null t

f.FindMatch(t) is { } first
    ⇒ some m in f.FindMatches(t) has
         m.Index ≤ first.Index  and  first.Index + first.Length ≤ m.Index + m.Length
```

## Compatibility

| Check | Expectation |
| --- | --- |
| Source compatibility | All 1.1.0 call sites compile unchanged. |
| Binary compatibility | Package validation (`EnablePackageValidation`) passes against 1.1.0: only additions. |
| Target frameworks | `netstandard2.0`, `net8.0`, `net10.0`; no new public types from polyfills (R7). |
| Semantic version | MINOR → 1.2.0. |
