# Data Model: Find Every Match and Censor Messages

**Feature**: [spec.md](spec.md) | **Research**: [research.md](research.md) | **Date**: 2026-09-16

A library feature: the "data" is the values the filter returns and the values it keeps internally
while scanning. Nothing is stored between calls; the filter stays immutable (Principle IV).

## Public entities

### BannedWord *(unchanged)*

| Field | Type | Notes |
| --- | --- | --- |
| `Text` | string | The entry as the developer supplied it. |
| `Mode` | `WordMatchMode` | `WholeWord` or `Anywhere`. |
| `Category` | `WordCategory` | `Uncategorized` for developers' own entries without a section. |

### ProfanityMatch *(extended)*

One banned word found in one message.

| Field | Type | New | Rule |
| --- | --- | --- | --- |
| `Word` | `BannedWord` | | The entry that matched, exactly as supplied (FR-002). With overlaps, the entry R4 chose. |
| `Evasion` | `EvasionKind` | | Disguises undone to find it; the least among the merged hits for that entry (R4). |
| `Index` | int | ✓ | First character of the region in the message **as supplied** (FR-003, FR-021). |
| `Length` | int | ✓ | Characters in the region, counted as the host string counts them (UTF-16 code units). |

**Validation rules**
- `0 ≤ Index` and `Index + Length ≤ message.Length`; `Length ≥ 1`.
- The region never starts or ends inside a surrogate pair.
- The region is whole words (FR-011): the character before `Index` and the character at
  `Index + Length` are either outside the message or not word characters by the tokenizer's rule.
- The region contains at least one letter or digit.

**Relationships**
- Returned by `FindMatch` (zero or one) and `FindMatches` (zero or more).
- Within one `FindMatches` result: ordered by `Index` ascending (FR-004), and no two regions overlap
  (FR-007). Two regions may touch only if a non-word character separates them.
- `Word.Category` is how callers read the category (FR-002); no separate field.

**Equality**: record value equality over all four fields. `Index` and `Length` are new members of it
(R7, called out in release notes).

### Censored message

Not a type. A `string` built from the message and a `FindMatches` result:

```text
censored = message[0 .. m1.Index]
         + mask
         + message[m1.Index + m1.Length .. m2.Index]
         + mask
         + …
         + message[mN.Index + mN.Length .. end]
```

**Rules**
- `mask` = the mask character four times (FR-012).
- Every segment outside a region is copied verbatim (FR-009).
- A clean message is returned as the same instance (R8).
- If the result is not clean when checked with the same filter, the construction is applied again to
  the result (R6). The final result is always clean (FR-015).

### Mask character

A `char` parameter, not stored. Default `*`. It must not be a letter, a digit, whitespace, a control
character or a surrogate half (FR-014, R8); otherwise `ArgumentException` is thrown before any
scanning.

## Internal entities

These are implementation-facing. They are recorded here so tasks can reference them; none is public.

### Reading

One lossy form of the message the matcher searches, as in 1.1.0.

| Field | Meaning |
| --- | --- |
| `Text` | The reading's characters. |
| `Evasion` | What producing this reading undid: `None`, `RepeatedLetters`, `LookalikeCharacters`, or both. |
| `Source` | How to rebuild it with a map: Comparison-normalise the message, then optionally `Fold`, then optionally `Squeeze`. |

The four readings are examined in the 1.1.0 order; identical strings are examined once.

### Source map

`int[]` with one element per character of a reading. Each element is the index in the original
message of the character that produced it (R1). Composed through the transform chain: for
`Squeeze(Fold(normalized))`, `map[i] = normalizedMap[foldMap[squeezeMap[i]]]`.

- Built only for readings that produced a hit (lazy).
- Invariant: non-decreasing, because every transform preserves order.
- The fallback maps (chunk-level, then whole-message) satisfy the same invariant, and are only used
  when the mapped reading differs from the reading that was searched.

### Hit

A raw match inside one reading, before mapping.

| Field | Meaning |
| --- | --- |
| `Reading` | Which reading, so the right map is used. |
| `Start`, `End` | Positions in that reading, `End` exclusive. For tokens, phrases, joined letters and split halves this is the span of the first to the last token involved; for anywhere entries, the `IndexOf` position; for broken chunks, the chunk after trimming outer punctuation. |
| `Entry` | The entry that matched, with its word-list order (see below). |
| `Evasion` | The reading's evasion, plus `SplitWord` or `LookalikeCharacters` where 1.1.0 adds them. |

In **first** mode the scan returns the first hit. In **all** mode it keeps scanning: every token,
every phrase start, every `IndexOf` occurrence of every anywhere key, every chunk.

### Candidate

A hit mapped to the original message and widened to whole words (R3).

| Field | Meaning |
| --- | --- |
| `Start`, `End` | Region in the original message after widening to whole words, `End` exclusive. |
| `HitLength` | Characters the hit covered in the original message *before* widening. |
| `Entry`, `Order` | The entry and its word-list order. |
| `Evasion` | From the hit. |

**Merge (R4)**: sort by `Start`; group candidates whose regions overlap into clusters. Each cluster
becomes one `ProfanityMatch` with:

- the union region,
- the entry of the candidate with the largest `HitLength` (ties: lowest `Order`), and
- the smallest evasion among that entry's candidates.

### Entry order

An `int` recorded for each distinct entry when the filter is built: its position in the sequence
passed to the constructor. Stored next to the entry in the whole-word dictionary, the phrase lists
and the anywhere keys. It is used only for the FR-007 tie-break.

## State transitions

None. A filter has no state after construction, and a scan's working values (readings, maps, hits,
candidates) live only for the duration of one call. That is what keeps concurrent use safe (FR-020).
