# PersianTextGuard 1.2.0

Find every banned word in a message, with where it is, and censor messages.

## New

### `FindMatches` — every match, with its position

```csharp
foreach (var match in filter.FindMatches("sh1t and f u c k"))
{
    Console.WriteLine($"{match.Word.Text} ({match.Word.Category}) at {match.Index}, length {match.Length}");
}
// shit (Profanity) at 0, length 4
// fuck (Profanity) at 9, length 7
```

- Every banned word, ordered by position, each with its entry, category and the evasion undone to
  find it.
- `Index` and `Length` refer to the text **exactly as you passed it**, not a normalized copy, and
  cover whole words, so they can be used directly to highlight or slice the original string.
- Overlapping entries become one match: "motherfucker" is one match, not "motherfucker" plus the
  "fuck" inside it.

### `Censor` — hide the words, keep the message

```csharp
filter.Censor("kir and motherfucker");   // "**** and ****"
filter.Censor("جنده‌ها رو ببین");         // "**** رو ببین"
filter.Censor("this is kir", '#');       // "this is ####"
```

- **Whole words are hidden** — a Persian suffix or the rest of a longer word goes with the banned
  word, so no fragment is left — and disguised words are hidden with their separators.
- **Every mask is four characters**, so a reader cannot tell how long the hidden word was.
- **The output is always clean:** checking it with the same filter finds nothing, even where masking
  would otherwise join the surrounding letters into a new word.
- Clean text is returned as the same string; nothing outside a hidden word is normalized.
- Letters, digits, whitespace and control characters are refused as mask characters.

### `FindMatch` reports a position too

`ProfanityMatch` has new `Index` and `Length` properties, and `FindMatch` fills them.

## Behaviour changes

- **`ProfanityMatch` equality now includes `Index` and `Length`.** A `FindMatch` result no longer
  equals a hand-built `new ProfanityMatch(word, evasion)`. Compare `Word` and `Evasion` instead if
  you relied on that. Which entry and evasion `FindMatch` returns is unchanged.
- Nothing else changes in what `ContainsProfanity` or `FindMatch` return: all 1.1.0 tests pass
  unmodified, and a new consistency suite checks that all four methods agree on every message in
  the test corpus.

## Performance

BenchmarkDotNet, .NET 10, Intel Core i7-9700K. See `specs/001-censor-find-matches/benchmarks.md`.

| Operation | 1.1.0 | 1.2.0 |
| --- | ---: | ---: |
| Short clean message | 2.399 µs | 2.391 µs |
| Long clean message (60 words) | 21.870 µs | 21.302 µs |
| Message with evasions | 1.735 µs | 1.905 µs |
| `FindMatches`, clean short message | — | 2.368 µs |
| `FindMatches`, message with three banned words | — | 6.567 µs |
| `Censor`, 60-word message with three banned words | — | 92.967 µs |

Positions are only worked out for a message that contains a banned word, so `FindMatches` and
`Censor` cost the same as `ContainsProfanity` on clean text. The message-with-evasions case is
+0.17 µs: joining spaced-out letters now also records where each letter sits so a match can be
located in the original text.
