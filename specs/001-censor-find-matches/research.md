# Research: Find Every Match and Censor Messages

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Date**: 2026-09-16

The Technical Context had no open unknowns about the stack: the library, its targets and its test
tooling are fixed by the constitution. The research below is about the design questions the
feature raises, several of which were checked against the 1.1.0 code before deciding.

---

## R1. Recovering positions in the original message

**Decision**: Keep matching on the lossy readings exactly as 1.1.0 does, and translate a hit back to
the original message only when there is one. Each transform that builds a reading — Comparison
normalisation, `Squeeze`, `Fold` — gets an internal variant that also emits, for every output
character, the index of the original character it came from. A hit at reading positions
`[start, end)` maps to original positions `[map[start], map[end - 1] + width)`, where `width` is 2 for
a surrogate pair and 1 otherwise. Maps are built lazily, only for a reading that produced a hit.

**Rationale**:
- The matcher's correctness lives in the readings (normalised, squeezed, folded, squeezed-folded)
  and in tokenising them. Rebuilding matching on the original text would re-implement every evasion
  and risk changing results, which FR-019 forbids.
- Clean messages — the overwhelming majority and the benchmarked path — produce no hit, so they
  never allocate a map. The yes/no check stays at its 1.1.0 cost (SC-005).
- Every transform is a left-to-right character filter or 1-to-1 substitution except NFKC, so a map
  is cheap to emit alongside the output.

**NFKC**: `string.Normalize` cannot report where its output came from. The mapped variant normalises
one *segment* at a time — a starter character plus the combining marks that follow it — and maps
every output character of a segment to the segment's first index. Composition in NFKC only happens
inside such a segment, so the result equals whole-string normalisation for Persian, Arabic, Latin,
Cyrillic and Greek text.

The mapped reading is compared with the unmapped reading the matcher used. If they ever differ,
because some script composes across segments, the map falls back to whitespace-delimited chunks,
and then to the whole message. The fallback can only make a hidden region larger, never smaller,
so it cannot leak a word.

**Alternatives considered**:
- *Map every reading up front*: simplest code, but allocates an `int[]` per reading for every clean
  message and breaks the SC-005 budget.
- *Match on original-text tokens and normalise each token separately*: loses the evasions that
  cross token boundaries in a reading (`sh!t` is one token only after folding) and diverges from
  1.1.0 results.
- *Chunk-level (whitespace) positions only*: cheap, but `hello,fuck` would mask `hello` as well,
  violating FR-011's word boundaries.

---

## R2. One scanning engine for all four capabilities

**Decision**: Refactor matching into one internal scan that runs in two modes, **first** (stop at
the first hit) and **all** (collect every hit). `ContainsProfanity` and `FindMatch` use *first*,
`FindMatches` and `Censor` use *all*. The order readings, tokens, phrases, anywhere entries, joined
letters, split halves and broken chunks are examined in is unchanged from 1.1.0.

**Rationale**: FR-016 requires the four capabilities to agree on whether a message is clean. Two
code paths would agree only as far as tests happen to check; one engine agrees by construction.
Keeping the 1.1.0 examination order keeps `FindMatch` returning the same entry and evasion (FR-019),
which the existing 225 tests pin.

**Alternatives considered**: a separate `FindMatches` implementation beside the untouched 1.1.0
code. It would guarantee FR-019 trivially, but it would duplicate about 400 lines of matching and let
the two drift apart.

---

## R3. Whole-word regions

**Decision**: After a hit is mapped to original positions, widen it to word boundaries in the
original message: move the start left and the end right while the neighbouring character is a word
character by the tokenizer's rule — letters, digits, combining marks, and a zero-width non-joiner or
joiner after a letter. Whitespace, punctuation, symbols, emoji and invalid surrogates stop it.

**Rationale**: It is exactly FR-011's definition of a word, and it reuses the rule 1.1.0 already
ships, so a region ends where the filter already considers a word to end. `motherfucker` found
through `~fuck` widens to the whole word; `جنده‌ها` widens across its zero-width non-joiner to take the
suffix; `hello,fuck` stops at the comma; `کیر😂` stops at the emoji.

Separators *inside* a hit (`f u c k`, `ج.نده`, `f**k`) are already inside the mapped span, because the
hit's first and last reading characters map to the word's first and last letters (FR-013).

---

## R4. Overlapping hits and the region a match reports

**Decision**: In *all* mode, each hit becomes a candidate: region, entry, evasion, and the entry's
position in the word list. Candidates are sorted by region start and merged into clusters of
overlapping regions. Each cluster becomes one match whose:

- **region** is the union of the cluster's regions,
- **entry** is the candidate whose hit covered the most characters *before* widening to whole words,
  ties broken by earliest word-list position (FR-007), and
- **evasion** is the smallest evasion among candidates with that entry and region. The same word is
  often found in several readings, and the earliest reading needed the least undoing.

**Rationale**: With whole-word regions, overlaps are almost always identical regions, where the
same word is found through several readings or entries. The union only matters for chained phrases
such as «پدر سگ پدر», which matches both «پدر سگ» and «سگ پدر». Reporting the union keeps every hit
character inside a reported match, so Censor hides everything every-match reported, and nothing
more.

The word-list position is recorded when the filter is built. It is the order entries arrived in,
which for the bundled list is file order.

**Why length is measured before widening** (found while generating tasks): after widening, every
hit inside "motherfucker" covers the same 12 characters, so `~fuck` and `motherfucker` would tie and
list order would pick `fuck`. FR-007's "spanning the most characters" means the characters the match
itself covers: 12 for `motherfucker`, 4 for `fuck`. Genuine ties remain possible and are settled by
list order. For example, in "kos kesh" the phrase `kos kesh` and the split-halves hit `~koskesh` both
cover 8 characters, and the bundled Finglish list has `~koskesh` first. Tests for such inputs assert
the region and category, not the exact entry text.

---

## R5. Spec conflict: FR-016 against FR-007

**Finding**: Checked against 1.1.0. For `kos kesh`, the first-match check returns the whole-word
entry `kos`, because tokens are examined before phrases. FR-007 makes every-match report the phrase
`kos kesh`, the longer region. FR-016 as written requires the first-match entry to be *among*
every-match's results, so the two requirements cannot both hold for this input.

**Decision**: Amend FR-016 and User Story 3's second scenario. The first-match check's hit must lie
inside a region every-match reports. That region carries either the same entry or the longer
overlapping one FR-007 chose instead.

**Rationale**: FR-007 gives moderators the more informative answer (`kos kesh` is an insult phrase,
`kos` alone is a sexual term). Changing `FindMatch` to prefer longer entries would violate FR-019.
The amendment keeps what FR-016 exists to protect: all four capabilities agree the message is dirty,
and on where.

**Applied**: [spec.md](spec.md) FR-016 and User Story 3 scenario 2 are updated in this planning
session.

---

## R6. Guaranteeing censored output is clean (FR-015)

**Finding**: Checked against 1.1.0. `*` is one of the filler characters the filter drops to catch
`f*ck`, so masking can join letters on either side of a mask into a new match:

| Censored text | Detected as |
| --- | --- |
| `a **** s s` | `ass` (single letters joined across the dropped mask) |
| `k **** i **** r` | `kir` |

A message like "k kos i kos r" would therefore censor to text the same filter still flags.

**Decision**: `Censor` repeats until the output is clean. After masking, the output is checked with
the *first* mode; if it is dirty, the regions of that output are masked in the same way. Every pass
replaces at least one region containing a letter with a mask containing no letters or digits, so the
number of letters strictly falls and the loop ends. It is additionally capped at the number of words
in the message plus one, as a defence against a bug rather than an expected limit.

**Rationale**: It guarantees FR-015 for any word list, any mask character and any future evasion,
without teaching the matcher a special case for masks. The extra check costs one *first*-mode scan
of a string that is usually already clean, and only on dirty messages.

**Alternatives considered**:
- *Treat the mask character as a word boundary during folding*: `*` is a genuine evasion filler in
  user text (`f*ck`), so this would stop catching evasions.
- *Surround masks with spaces*: changes characters outside hidden regions (violates FR-009) and still
  lets single letters join.

---

## R7. Where positions live in the public API

**Decision**: Add two non-positional, init-only properties to the existing `ProfanityMatch` record:
`Index`, the first character of the region in the message as supplied, and `Length`, the region's
character count. `FindMatches` returns `IReadOnlyList<ProfanityMatch>`. `FindMatch` also fills them,
by building maps lazily for the reading its first hit came from.

**Rationale**:
- One match type: `FindMatch` and `FindMatches` return the same thing, and callers already
  handling `ProfanityMatch` get positions for free.
- Non-positional properties keep the constructor, deconstruction and `with` expressions
  source- and binary-compatible, so package validation stays green.
- `ContainsProfanity` never builds maps, so its cost is unchanged.

**Behaviour change to call out in the release notes**: record equality now includes `Index` and
`Length`. Code comparing a `FindMatch` result with `new ProfanityMatch(word, evasion)` will no longer
see them as equal. The entry and evasion themselves are unchanged (FR-019).

**Alternatives considered**:
- *A new `ProfanityOccurrence` type for `FindMatches` only*: leaves `ProfanityMatch` equality untouched,
  but gives the two methods different result types and duplicates `Word` and `Evasion`.
- *`System.Range` for the location*: not available on `netstandard2.0` except through the PolySharp
  polyfill, whose type must not appear in public API.

---

## R8. Mask character validation and null input

**Decision**:
- `Censor(string? text)` uses `*`. `Censor(string? text, char maskCharacter)` throws
  `ArgumentException` when the mask character is a letter, a digit, whitespace, a control character
  or a surrogate half.
- `FindMatches(null)` returns an empty list; `Censor(null)` returns `string.Empty`. This follows
  `PersianNormalizer.Normalize`, which already returns an empty string for null.
- A clean message returns the same string instance from `Censor`, not a copy.

**Rationale**: FR-014 requires refusing letters and digits. Whitespace, control characters and
surrogate halves are refused too, because they would make hidden words disappear from view, break
line structure, or produce invalid text. A mask character is chosen by a programmer, so the exception
fits Principle II's carve-out for programmer errors. Returning the same instance for clean messages
costs nothing and lets callers skip a string comparison.

---

## R9. Performance approach and measurement

**Decision**:
- *Clean messages*: `FindMatches` and `Censor` run the same scan as the yes/no check, and build no
  maps, candidate list or result. Target ≤ 1.5× the yes/no check (SC-005); expected close to 1.0×,
  since the only extra work is a lazily-allocated candidate list that stays `null`.
- *Dirty messages*: maps for hit readings, candidate merge, one output pass and the R6 verification
  scan. Target: censoring a 60-word dirty message in under 0.1 ms (SC-006). The long clean message
  benchmark is 22 µs today, so the budget allows roughly four scans.
- New BenchmarkDotNet cases: `FindMatchesClean`, `FindMatchesDirty`, `CensorShortDirty`,
  `CensorLongDirty`. The existing five cases are rerun to show the yes/no check within 5% of 1.1.0
  (SC-005, Principle IV).

---

## R10. Test strategy without modifying existing tests

**Decision**: SC-008 requires the 1.1.0 tests to pass unmodified, so every new test is additive:

- `FindMatchesTests` and `CensorTests`: behaviour per acceptance scenario and edge case, including
  every example in the spec.
- `ConsistencyTests`: collects every `[InlineData]` string from the existing `DefaultListTests` by
  reflection, adds the robustness inputs, and asserts over the whole corpus:
  - FR-016 agreement
  - FR-015 (censored output is clean)
  - SC-004 (text outside regions unchanged, regions read `****`)
  - SC-002 (ordinary messages produce no match and come back as the same instance)
  - FR-003 (each region's text contains the characters the entry matched)
- `ReadmeExampleTests`: new test methods for the new README examples; existing methods untouched.

**Rationale**: Reflection over the existing `InlineData` turns 169 hand-written messages into a
free consistency corpus without editing the files SC-008 protects.
