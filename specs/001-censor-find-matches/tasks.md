---

description: "Task list for Find Every Match and Censor Messages (PersianTextGuard 1.2.0)"
---

# Tasks: Find Every Match and Censor Messages

**Input**: Design documents from `/specs/001-censor-find-matches/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/public-api.md](contracts/public-api.md), [quickstart.md](quickstart.md)

**Tests**: Included. The constitution requires them — Principle V says every README example has a
test and every bug fix a regression test — and the plan and quickstart name the test files. Within
each phase, tests are written first and must fail before the implementation task that follows.

**Organization**: Tasks are grouped by user story. US2 (censoring) builds on US1's `FindMatches`,
and US3 (consistency) verifies US1 and US2 together; see Dependencies.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- All paths are relative to the repository root, `D:\Git\PersianTextGuard`

## Path Conventions

Single library project, per plan.md:

- `src/PersianTextGuard/` — the package
- `tests/PersianTextGuard.Tests/` — xUnit, targets `net10.0`, `net8.0`, `net48`
- `benchmarks/PersianTextGuard.Benchmarks/` — BenchmarkDotNet

**Files that MUST NOT be modified** (SC-008):

- `tests/PersianTextGuard.Tests/DefaultListTests.cs`
- `tests/PersianTextGuard.Tests/ProfanityFilterTests.cs`
- `tests/PersianTextGuard.Tests/PersianNormalizerTests.cs`

`tests/PersianTextGuard.Tests/ReadmeExampleTests.cs` may only have methods added.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Branch, baseline numbers, and the partial-class split the plan's structure needs.

- [ ] T001 Create and switch to git branch `001-censor-find-matches` from an up-to-date `main` (the name already recorded in `.specify/feature.json`); commit the untracked `.specify/`, `.claude/` and `specs/001-censor-find-matches/` on it first, removing the HTML Sync Impact Report comment from the top of `.specify/memory/constitution.md` before committing
- [ ] T002 Record the 1.1.0 baseline before any source change: run `dotnet run -c Release --project benchmarks/PersianTextGuard.Benchmarks -f net10.0 -- --filter '*'` and save the results table (Mean, Allocated for CleanShortMessage, CleanLongMessage, EvasiveMessage, NormalizeLongMessage, BuildFilterFromDefaultList) to `specs/001-censor-find-matches/benchmarks.md` under a "Baseline (1.1.0)" heading
- [ ] T003 Change `public sealed class ProfanityFilter` to `public sealed partial class ProfanityFilter` in `src/PersianTextGuard/ProfanityFilter.cs`, and create `src/PersianTextGuard/ProfanityFilter.Scan.cs` and `src/PersianTextGuard/ProfanityFilter.Regions.cs`, each containing only `namespace PersianTextGuard;` and `public sealed partial class ProfanityFilter { }`; confirm `dotnet build src/PersianTextGuard` succeeds with no warnings (`TreatWarningsAsErrors`)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Source maps, entry order, the one scan engine (R1, R2) and whole-word regions (R3).
Every user story needs these.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete, and the 225 existing
tests must still pass unmodified when it ends (FR-019).

### Tests for the foundation

- [ ] T004 [P] Write `tests/PersianTextGuard.Tests/SourceMapTests.cs`, covering the internal source map from T005 (reachable through the existing `InternalsVisibleTo`). The tests must fail until T005 exists.
  - **Text equality:** for every string below, the mapped Comparison-normalised text equals `PersianNormalizer.Normalize(text)`, and the mapped fold and squeeze results equal `ProfanityFilter.Fold` and `ProfanityFilter.Squeeze` of that text.
    - Persian and Latin: `"كتاب‌هاي  ۱۲ ABC"`, `"ﻛﻴﺮ"`, `"fúck"`, `"u\u0301"`, `"کــیــر"`, `"کِیر"`.
    - Repeats, symbols and surrogates: `"سسسسلام"`, `"sh!t 455"`, `"🅵🆄🅲🅺"`, `"f🖕ck"`, `"hi \uD83D"`.
    - Unusual whitespace and invisible characters: `"   a \t b  "`, `"ک\u200Bیر"`, `"ج\u200Fنده"`.
  - **Map shape:** every map has exactly one element per output character, its values are non-decreasing, and every value is a valid index into the original string.
  - **Map content:** for `"ﻛﻴﺮ"` the map is `[0,1,2]`; for `"سسسسلام"` every output character maps to a source index whose character is the same letter; for `"ک\u200Bیر"` the output `"کیر"` maps to `[0,2,3]`.
  - **Fallback:** call `SourceMap.ChunkMap(original, reading)` and `SourceMap.WholeMessageMap(original, reading)` directly (T005) with `original = "ab kir cd"` and `reading = "ab kir cd"`. For both maps, the element for reading index 3 (`k`) is ≤ 3 and the element for reading index 5 (`r`) is ≥ 5, so fallbacks only ever widen. Use static methods rather than a global switch, because xUnit runs test classes in parallel.

### Implementation for the foundation

- [ ] T005 Create `src/PersianTextGuard/SourceMap.cs`: `internal readonly struct MappedText` holding `string Text` and `int[] Map`, where `Map[i]` is the index in the original message of the character that produced `Text[i]`. Build it with shared code, not copied code:
  - **Normalisation:** add an internal overload `PersianNormalizer.Normalize(string text, PersianNormalization steps, List<int>? map)` in `src/PersianTextGuard/PersianNormalizer.cs`. When `map` is null, the existing public `Normalize` must be byte-for-byte unchanged. When `map` is non-null:
    - Apply NFKC one segment at a time. A segment is a starter code point plus the following `NonSpacingMark`/`SpacingCombiningMark`/`EnclosingMark` code points, and every output character of a segment maps to the segment's first index.
    - Carry the map through `IsRemoved`, `UnifyLetter`, digit conversion, lower-casing, `CollapseRepeats` and `CollapseWhitespace`, including trimming.
  - **Fold and squeeze:** add `internal static string Fold(string normalized, List<int>? map)` and `internal static string Squeeze(string value, List<int>? map)` overloads in `src/PersianTextGuard/ProfanityFilter.cs`. The existing single-argument `Fold` and `Squeeze` call them with `null`. Composing maps: `mapped[i] = previousMap[foldOrSqueezeMap[i]]`.
  - **Fallbacks (R1):** in `SourceMap`, compare the mapped text with the unmapped reading the matcher searched. If they differ, use `internal static int[] ChunkMap(string original, string reading)`. It pairs the n-th whitespace-separated chunk of `reading` with the n-th of `original`: the first character of a reading chunk maps to its original chunk's start, and every later character maps to that original chunk's last index. If the chunk counts differ, use `internal static int[] WholeMessageMap(string original, string reading)` instead: element 0 maps to 0 and every other element to `original.Length - 1`. Both maps must stay non-decreasing.
- [ ] T006 Record entry order in `src/PersianTextGuard/ProfanityFilter.cs` for the FR-007 tie-break:
  - Count the words enumerated in the constructor. The running index starts at 0 and increments for every `BannedWord` enumerated, duplicates included; this is the entry's order.
  - Change `_words` from `Dictionary<string, BannedWord>` to `Dictionary<string, (BannedWord Word, int Order)>`.
  - Add `int Order` to the private records `Key` and `Phrase`.
  - Keep the first-added-wins behaviour and `Count` exactly as today.
- [ ] T007 [P] Add init-only, non-positional properties `public int Index { get; init; }` and `public int Length { get; init; }` to `public sealed record ProfanityMatch(BannedWord Word, EvasionKind Evasion)` in `src/PersianTextGuard/ProfanityMatch.cs`.
  - Do not change the positional parameters.
  - XML docs must say: `Index` is "the first character of the matched words in the text as it was passed, counted in UTF-16 code units", and `Length` is "how many characters the matched words span". Both must say they are 0 on a match the caller constructs themselves.
- [ ] T008 Move matching into a single scan engine in `src/PersianTextGuard/ProfanityFilter.Scan.cs` (R2).
  - **Types:**
    - `internal enum ReadingKind { Normalized, Squeezed, Folded, FoldedSqueezed }`.
    - `internal readonly record struct Hit(ReadingKind Reading, int Start, int End, BannedWord Word, int Order, EvasionKind Evasion)`, where `Start`/`End` are positions in that reading and `End` is exclusive.
    - `private ProfanityMatch? Scan(string text, List<Hit>? all, out Hit first)`: stops at the first hit when `all` is null, and otherwise appends every hit to `all`.
  - **Examination order:** it MUST be exactly 1.1.0's `FindMatch` order: the readings in the order normalized, squeezed, folded, folded-squeezed, skipping duplicate strings; within a reading, `MatchTokens` then `MatchAnywhere(reading)`, then (when `JoinSpacedLetters`) joined single letters via `MatchTokens` and `MatchAnywhere`, then `MatchSplitHalves`; after all readings, `MatchBrokenChunks(normalized)`.
  - **Positions each matcher must report:**
    - Tokens: use a token-with-offset variant of `PersianNormalizer.Tokenize`, internal `TokenizeWithOffsets`, returning each token's start and length in the reading.
    - Phrases: from the first token's start to the last token's end.
    - Anywhere entries: in *all* mode, every `IndexOf` occurrence, not only the first.
    - Joined single letters: build the joined string with a parallel `int[]` from joined-string index to reading index, so hits in it translate back to the reading.
    - Split halves: the span of both tokens.
    - Broken chunks: the chunk's trimmed start and end within the normalized reading, with `ReadingKind.Normalized`.
  - **Callers:** `ContainsProfanity` calls `Scan(text, null, out _) is not null`, and `FindMatch` calls the same scan (positions are added in T010). Delete the now-duplicated bodies from `src/PersianTextGuard/ProfanityFilter.cs`.
  - **Done when:** `dotnet test tests/PersianTextGuard.Tests -f net10.0` passes all 225 existing tests unmodified.
- [ ] T009 Implement hit-to-region translation in `src/PersianTextGuard/ProfanityFilter.Regions.cs` (R3).
  - **Signature:** `internal readonly record struct Candidate(int Start, int End, int HitLength, BannedWord Word, int Order, EvasionKind Evasion)` and `private Candidate ToCandidate(string original, in Hit hit, MappedText?[] mapCache)`. `HitLength` is `end - start` measured after translating and *before* widening; FR-007 compares on it.
  - **Map:** build the `MappedText` for `hit.Reading` lazily through `SourceMap` (T005) and cache it in `mapCache[(int)hit.Reading]`, so each reading is mapped at most once per call.
  - **Translate:** `start = map[hit.Start]`, and `end = map[hit.End - 1] + width`, where `width` is 2 when `original[map[hit.End - 1]]` is a high surrogate followed by a low surrogate, else 1.
  - **Widen to whole words:** while `start > 0` and `PersianNormalizer.IsWordCharacter(original, start - 1)`, decrement `start`. While `end < original.Length` and `PersianNormalizer.IsWordCharacter(original, end)`, increment `end`. Never stop between a high surrogate and its low surrogate.
  - **Resulting region MUST satisfy the data-model rules:** "`0 ≤ Index` and `Index + Length ≤ message.Length`; `Length ≥ 1`", "The region never starts or ends inside a surrogate pair", and "The region contains at least one letter or digit".
- [ ] T010 Make `FindMatch` return positions in `src/PersianTextGuard/ProfanityFilter.cs` (R7).
  - After `Scan(text, null, out var first)` returns a match, translate `first` with `ToCandidate` (T009) and return `new ProfanityMatch(first.Word, first.Evasion) { Index = candidate.Start, Length = candidate.End - candidate.Start }`.
  - `ContainsProfanity` MUST NOT call `ToCandidate` or build any map.
  - Update `FindMatch`'s XML docs to mention `Index` and `Length`.
  - **Done when:** all 225 existing tests still pass.

**Checkpoint**: the foundation is ready. The four readings map back to the original text, the scan
engine serves `ContainsProfanity` and `FindMatch` with 1.1.0 results, and `FindMatch` reports where.

---

## Phase 3: User Story 1 - See every banned word in a message (Priority: P1) 🎯 MVP

**Goal**: `FindMatches(string?)` returns every banned word, ordered by position, each with entry,
category, evasion and whole-word region, and never overlapping (FR-001 to FR-007).

**Independent Test**: Pass messages with known banned words (plain, repeated, disguised, mixed with
ordinary text) and check that every occurrence is returned with the right entry, category, evasion
and position, and that ordinary messages return an empty list.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T011 [P] [US1] Write `tests/PersianTextGuard.Tests/FindMatchesTests.cs` using `new ProfanityFilter(WordList.PersianDefault)`. Assert each case exactly:
  - **Basic scenarios (from the quickstart):**
    - `"you bitch, kos kesh"` → 2 matches, in order: `Word.Text == "bitch"` at `(4,5)`, then a match with `Word.Category == WordCategory.Insult` at `(11,8)`. Do not assert that entry's text: the phrase `kos kesh` and `~koskesh` tie on length and list order decides (research R4).
    - `"kir kir kir"` → 3 matches with `(Index, Length)` = `(0,3)`, `(4,3)`, `(8,3)`.
    - `"sh1t and f u c k"` → 2 matches; the second has `Evasion.HasFlag(EvasionKind.SplitWord)`, `Index == 9`, `Length == 7`.
    - `"سلام، سفارشم کی میرسه؟"` → empty.
    - `"ﻛﻴﺮ"` → 1 match, `(0,3)`.
    - `"motherfucker"` → 1 match, `Word.Text == "motherfucker"`, `(0,12)`.
  - **Word boundaries:**
    - `"hello,fuck"` → 1 match, `(6,4)`.
    - `"جنده‌ها رو ببین"` → 1 match, `Word.Text == "جنده"`, `(0,7)` (ZWNJ and suffix inside).
    - `"کیر😂"` → `(0,3)`.
  - **Overlaps:**
    - `"پدر سگ پدر"` → 1 match whose region is the whole string `(0,10)`.
    - `"fuckfuck"` → 1 match `(0,8)`.
  - **Input handling:**
    - `null`, `""`, `"   "` → an empty list that is not null.
    - `"hi \uD83D kir"` → no exception, 1 match `(5,3)`: the lone surrogate is index 3.
  - **Settings:** a filter built with `new ProfanityFilterOptions { FoldLookalikeCharacters = false }` → `"sh1t"` returns empty (FR-018).
  - **Result invariants, for every non-empty result above:** matches ordered by `Index` ascending, no two regions overlapping, each region satisfying "`0 ≤ Index` and `Index + Length ≤ message.Length`; `Length ≥ 1`".
  - **FindMatch positions (R7):** `FindMatch("this is kir")` has `(8,3)`.

### Implementation for User Story 1

- [ ] T012 [US1] Implement `private static List<ProfanityMatch> Merge(List<Candidate> candidates)` in `src/PersianTextGuard/ProfanityFilter.Regions.cs` (R4).
  - **Clusters:** sort by `Start`, then by `End` descending. Walk the list, starting a new cluster when a candidate's `Start` ≥ the running cluster `End`, and otherwise extending the cluster `End` to the maximum.
  - **One match per cluster, with:**
    - **Region:** the union.
    - **Entry:** from the candidate with the largest `HitLength`, the length before widening, so `motherfucker` (12) beats `~fuck` (4) even though both widen to the same word; ties go to the lowest `Order`.
    - **Evasion:** the numerically smallest `Evasion` among that entry's candidates in the cluster (`Word` equal).
  - **Result:** `new ProfanityMatch(word, evasion) { Index = clusterStart, Length = clusterEnd - clusterStart }`, in ascending `Index` order.
- [ ] T013 [US1] Add `public IReadOnlyList<ProfanityMatch> FindMatches(string? text)` to `src/PersianTextGuard/ProfanityFilter.cs`.
  - **Clean path:** if `string.IsNullOrWhiteSpace(text)` or `Count == 0`, return `Array.Empty<ProfanityMatch>()`. Otherwise call `Scan(text, hits, out _)` with a `List<Hit>` allocated only on the first hit. If there are none, return `Array.Empty<ProfanityMatch>()` without building maps (R9).
  - **Dirty path:** otherwise convert every hit with `ToCandidate` (T009), sharing one `MappedText?[4]` cache for the call, then return `Merge(candidates).ToArray()`.
  - **XML docs:** must state every row of the `FindMatches` table in `specs/001-censor-find-matches/contracts/public-api.md`: order, no overlap, whole-word regions, positions in the text as passed, the settings used, never throws, safe to share.
  - **Done when:** T011 passes on `net10.0`.

**Checkpoint**: User Story 1 is fully functional. `FindMatches` can ship on its own and closes the
README's "first match only" limitation.

---

## Phase 4: User Story 2 - Publish a message with the banned words hidden (Priority: P2)

**Goal**: `Censor(string?)` and `Censor(string?, char)` return the message with each banned word's
whole word replaced by the mask character four times, everything else untouched, and output the same
filter finds clean (FR-008 to FR-015, FR-021).

**Independent Test**: Censor messages with known banned words, then check that each is hidden, that
every character outside hidden regions is unchanged, that re-checking the output finds nothing, and
that ordinary messages come back as the same instance.

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T014 [P] [US2] Write `tests/PersianTextGuard.Tests/CensorTests.cs` using `new ProfanityFilter(WordList.PersianDefault)`. Assert each case exactly:
  - **Masking:**
    - `"this is kir"` → `"this is ****"`.
    - `"جنده‌ها رو ببین"` → `"**** رو ببین"`.
    - `"kir and motherfucker"` → `"**** and ****"`.
    - `"f.u.c.k off"` → `"**** off"`.
    - `"ج.نده"` → `"****"`.
    - `"کیر😂"` → `"****😂"`.
    - `"kir kos"` → `"**** ****"`.
    - `"f\nu\nc\nk"` → `"****"`, with the line breaks hidden (spec edge case).
    - `"برو پدر سگ"` → `"برو ****"`.
  - **Clean text:**
    - `"كتاب‌هاي خوب"` → `Assert.Same(input, result)`.
    - `"   "` → `Assert.Same`.
    - `null` → `""`.
  - **Output guarantee (R6):** `"k kos i kos r"` → `filter.ContainsProfanity(result)` is `false`.
  - **Mask character:**
    - `Censor("kir", '#')` → `"####"`.
    - `Censor(x, c)` throws `ArgumentException` with `ParamName == "maskCharacter"` for each `c` in `'x'`, `'5'`, `' '`, `'\n'`, `'\uD83D'`, both with `x = "kir"` and with `x = null` (validation happens before scanning).
  - **FR-021:** `Censor("kir")` has `Length` 4 while `"kir"` has 3, and `FindMatches("kir")[0].Index` still refers to the original string.

### Implementation for User Story 2

- [ ] T015 [US2] Implement `public string Censor(string? text, char maskCharacter)` in `src/PersianTextGuard/ProfanityFilter.cs`, delegating the output building to a private helper in `src/PersianTextGuard/ProfanityFilter.Regions.cs`.
  - **Validation (R8):** first throw `new ArgumentException(message, nameof(maskCharacter))` when `char.IsLetterOrDigit(maskCharacter) || char.IsWhiteSpace(maskCharacter) || char.IsControl(maskCharacter) || char.IsSurrogate(maskCharacter)`. Then, if `text` is null, return `string.Empty`.
  - **Clean text:** `var matches = FindMatches(text)`; if empty, return `text` itself (same instance).
  - **Building:** use a `StringBuilder`. Copy `text[previousEnd .. m.Index]` verbatim, then append `new string(maskCharacter, 4)`. The data model's rule is "`mask` = the mask character four times".
  - **Clean-output loop (R6):**
    - While `ContainsProfanity(output)`, mask `FindMatches(output)` in `output` the same way.
    - Cap the passes at `PersianNormalizer.Tokenize(text).Length + 1`.
    - If the cap is reached and the output is still dirty, return `new string(maskCharacter, 4)`, so FR-015 holds unconditionally.
  - **XML docs:** must state every row of the `Censor` table in `specs/001-censor-find-matches/contracts/public-api.md`.
- [ ] T016 [US2] Add `public string Censor(string? text) => Censor(text, '*');` to `src/PersianTextGuard/ProfanityFilter.cs`, with XML docs saying the default mask is `****`. Run `dotnet test tests/PersianTextGuard.Tests -f net10.0`; T014 must pass.

**Checkpoint**: User Stories 1 and 2 both work. Censoring hides whole words with a fixed mask and
never returns text the filter still flags.

---

## Phase 5: User Story 3 - Existing checks keep behaving the same (Priority: P3)

**Goal**: The 1.1.0 behaviour is unchanged, and all four capabilities agree on every message
(FR-016, FR-019, FR-020; SC-002, SC-003, SC-004, SC-007, SC-008).

**Independent Test**: Run the unmodified 1.1.0 tests, then run corpus-wide agreement checks over
every inline message in `DefaultListTests` plus robustness inputs and a concurrency run.

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL (or expose disagreements) before fixing**

- [ ] T017 [P] [US3] Write `tests/PersianTextGuard.Tests/ConsistencyTests.cs`.
  - **Corpus:** use reflection, in a `public static IEnumerable<object?[]> Corpus()` member data source, over every method of `typeof(DefaultListTests)` carrying `Xunit.InlineDataAttribute`. Read each attribute's string arguments via `attribute.GetData(method)`. Tag each message as ordinary when the method name is `Ordinary_messages_pass`. Add the robustness inputs:
    - `null`, `""`, `"   "`.
    - `"hi \uD83D"`, `"\uDE00 hi"`.
    - A 132,000-character clean message: `"سلام این یک متن معمولی است و هیچ مشکلی ندارد. hello this is fine. "` repeated 2,000 times.
    - The same message followed by `" کیر"`.
  - **For each message `t`, against `new ProfanityFilter(WordList.PersianDefault)`, assert:**
    - **(a) FR-016 agreement:** `ContainsProfanity(t) == (FindMatch(t) != null) == (FindMatches(t).Count > 0)`, and, for non-null `t`, `== !ReferenceEquals(Censor(t), t)`.
    - **(b) FR-016 containment:** if `FindMatch(t)` is not null, some match `m` in `FindMatches(t)` satisfies `m.Index <= first.Index && first.Index + first.Length <= m.Index + m.Length`.
    - **(c) FR-015:** `ContainsProfanity(Censor(t))` is `false`.
    - **(d) SC-004:** build `expected` in a single pass from `FindMatches(t)`, copying gaps verbatim and writing `"****"` for each region. When `ContainsProfanity(expected)` is `false`, `Censor(t)` equals `expected`.
    - **(e) SC-002:** for ordinary messages, `FindMatches(t)` is empty and `Assert.Same(t, Censor(t))`.
    - **(f) SC-007:** none of the calls throws.
  - **Concurrency (FR-020):** one shared filter; `Parallel.For(0, 20_000, …)` calling all four methods on corpus messages chosen by `i % corpus.Count`; every result equals the result computed sequentially beforehand (for `FindMatches`, compare `Index`, `Length`, `Word` and `Evasion` element by element).

### Implementation for User Story 3

- [ ] T018 [US3] Run `dotnet test tests/PersianTextGuard.Tests -f net10.0 --filter FullyQualifiedName~ConsistencyTests`. Fix every failure at its source — `src/PersianTextGuard/ProfanityFilter.Scan.cs` for agreement, `src/PersianTextGuard/ProfanityFilter.Regions.cs` for regions and censoring, `src/PersianTextGuard/SourceMap.cs` for positions — never by weakening an assertion. Record each non-trivial fix, with the input that exposed it, as an added `[InlineData]` case in `tests/PersianTextGuard.Tests/FindMatchesTests.cs` or `tests/PersianTextGuard.Tests/CensorTests.cs` (Principle V: every bug fix gets a regression test).
- [ ] T019 [US3] Verify SC-008: run `git diff main --stat -- tests/PersianTextGuard.Tests/DefaultListTests.cs tests/PersianTextGuard.Tests/ProfanityFilterTests.cs tests/PersianTextGuard.Tests/PersianNormalizerTests.cs` and confirm there is no output. Then run `dotnet test tests/PersianTextGuard.Tests`, which covers `net10.0`, `net8.0` and `net48` (the last exercises the `netstandard2.0` build), and confirm every test passes on all three.

**Checkpoint**: All three user stories are functional and verified to agree with each other and
with 1.1.0.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Benchmarks, documentation, versioning and package checks the constitution requires.

- [ ] T020 [P] Add four benchmark methods to `FilterBenchmarks` in `benchmarks/PersianTextGuard.Benchmarks/Program.cs`, keeping the five existing ones unchanged:
  - `FindMatchesClean` — `Filter.FindMatches(CleanShort)`.
  - `FindMatchesDirty` — `Filter.FindMatches("این کیر و f u c k و sh1t")`.
  - `CensorShortDirty` — `Filter.Censor("this is kir")`.
  - `CensorLongDirty` — `Filter.Censor(DirtyLong)`, where `DirtyLong` is the existing `CleanLong` text with `" کیر "`, `" f.u.c.k "` and `" جنده‌ها "` inserted after its first, second and third sentences, for about 60 words with three banned words.
- [ ] T021 Run `dotnet run -c Release --project benchmarks/PersianTextGuard.Benchmarks -f net10.0 -- --filter '*'` on the machine named in `README.md`. Append the table to `specs/001-censor-find-matches/benchmarks.md` under "1.2.0", next to the T002 baseline. Verify, and fix in `src/PersianTextGuard/ProfanityFilter.Scan.cs` or `src/PersianTextGuard/ProfanityFilter.Regions.cs` if any fails:
  - SC-005: `CleanShortMessage` Mean within 5% of the baseline, and `FindMatchesClean` ≤ 1.5 × `CleanShortMessage`.
  - SC-006: `CensorLongDirty` Mean < 100 µs.
- [ ] T022 [P] Update `README.md`:
  - **New section:** a "Finding every match and censoring" section under "Profanity filtering", with one `FindMatches` example printing `Word.Text`, `Word.Category`, `Index` and `Length`, and one `Censor` example showing `"kir and motherfucker"` → `"**** and ****"` and a custom mask character.
  - **Mask behaviour:** state that the whole word is hidden, the mask is always four characters, censored text can differ in length from the message, and positions refer to the original message.
  - **Limitations:** replace the bullet "`FindMatch` reports the first match, not every match or its position." with a bullet saying a disguised word spread over several lines is hidden together with its line breaks.
  - **Performance table:** add rows for the new benchmarks from T021.
- [ ] T023 Add test methods to `tests/PersianTextGuard.Tests/ReadmeExampleTests.cs`, adding only and editing no existing method, asserting every new README example from T022 exactly as printed (Principle V).
- [ ] T024 [P] Set `<Version>1.2.0</Version>` and add `<PackageValidationBaselineVersion>1.1.0</PackageValidationBaselineVersion>` in `src/PersianTextGuard/PersianTextGuard.csproj`. Run `dotnet pack src/PersianTextGuard -c Release -o artifacts`; it must succeed with package validation reporting no breaking change against 1.1.0 (contract, Compatibility).
- [ ] T025 [P] Write `specs/001-censor-find-matches/release-notes.md` for the GitHub release, covering:
  - **Headline:** the new `FindMatches` and `Censor`.
  - **Changes to existing members:** `FindMatch` now sets `Index` and `Length`.
  - **Behaviour change:** `ProfanityMatch` equality now includes `Index` and `Length`, so a `FindMatch` result no longer equals `new ProfanityMatch(word, evasion)` (R7 — constitution, Public API & Versioning).
  - **Censoring rules:** whole-word masking with a fixed four-character mask.
  - **Performance:** the before-and-after numbers from `specs/001-censor-find-matches/benchmarks.md`.
- [ ] T026 Run every step of `specs/001-censor-find-matches/quickstart.md` (sections 1–5) and tick each expected outcome. Open a pull request from `001-censor-find-matches` to `main`, with the benchmark table from T021 in its description (constitution, Development Workflow & Quality Gates), and confirm CI is green on build, `net8.0`/`net10.0` tests, `net48` tests and pack before merging.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies. T001 → T002 → T003 in order; T002 must run before any source
  change.
- **Foundational (Phase 2)**: Depends on Setup. It blocks every user story.
- **User Story 1 (Phase 3)**: Depends on Foundational.
- **User Story 2 (Phase 4)**: Depends on User Story 1, because `Censor` is built on `FindMatches`
  (T013). The spec notes this dependency ("It depends on Story 1 for where the words are").
- **User Story 3 (Phase 5)**: Depends on User Stories 1 and 2, because it verifies all four
  capabilities together.
- **Polish (Phase 6)**: Depends on all three stories. T023 (README tests) depends on T022 (README).

### Within Phase 2

- T004 [P] and T007 [P] can start immediately.
- T005 depends on nothing else in the phase and must make T004 pass.
- T006 → T008, because the scan records `Order` in hits.
- T005 + T008 → T009 → T010.

### Within Each User Story

- Tests (T011, T014, T017) are written first and must fail.
- US1: T011 [P] alongside T012; then T013.
- US2: T014 [P]; then T015 → T016.
- US3: T017 [P]; then T018 → T019.

### Parallel Opportunities

- Phase 2: T004 (tests) and T007 (`ProfanityMatch.cs`) touch no file anyone else is editing.
- Phase 3: T011 (`FindMatchesTests.cs`) in parallel with T012 (`ProfanityFilter.Regions.cs`).
- Phases 3–5: each story's test file can be written while the previous story is being implemented,
  since tests are new files (T011, T014, T017).
- Phase 6: T020 (benchmarks), T022 (README), T024 (csproj) and T025 (release notes) are all
  different files.

---

## Parallel Example: Foundational + User Story 1

```bash
# Phase 2 — start together:
Task: "Write tests/PersianTextGuard.Tests/SourceMapTests.cs (T004)"
Task: "Add Index and Length to src/PersianTextGuard/ProfanityMatch.cs (T007)"

# Phase 3 — start together once Phase 2 is done:
Task: "Write tests/PersianTextGuard.Tests/FindMatchesTests.cs (T011)"
Task: "Implement Merge in src/PersianTextGuard/ProfanityFilter.Regions.cs (T012)"

# Phase 6 — start together:
Task: "Add benchmarks in benchmarks/PersianTextGuard.Benchmarks/Program.cs (T020)"
Task: "Update README.md (T022)"
Task: "Bump version and package baseline in src/PersianTextGuard/PersianTextGuard.csproj (T024)"
Task: "Write specs/001-censor-find-matches/release-notes.md (T025)"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup, including the baseline benchmarks.
2. Complete Phase 2: Foundational. **Stop if any of the 225 existing tests fails.**
3. Complete Phase 3: User Story 1.
4. **Stop and validate**: `FindMatchesTests` green, and the existing tests still green.
5. `FindMatches` alone is a shippable improvement.

### Incremental Delivery

1. Setup + Foundational → `FindMatch` reports positions, and nothing else changes.
2. + User Story 1 → `FindMatches` (MVP).
3. + User Story 2 → `Censor`.
4. + User Story 3 → corpus-wide consistency and concurrency proven.
5. Polish → benchmarks, docs, 1.2.0 version, release notes, PR.

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks.
- [Story] labels map tasks to spec.md's user stories for traceability.
- Commit after each task or logical group, on the `001-censor-find-matches` branch.
- Never edit `DefaultListTests.cs`, `ProfanityFilterTests.cs` or `PersianNormalizerTests.cs` (SC-008).
- A failing consistency check is fixed in the source, never by loosening the test.
