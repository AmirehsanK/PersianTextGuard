# Contract: `persian-text-guard` Public API

**Feature**: [../spec.md](../spec.md) | **Data model**: [../data-model.md](../data-model.md) | **Research**: [../research.md](../research.md) R5

This is the TypeScript surface users compile against. API Extractor records it in
`js/etc/persian-text-guard.api.md` (research R14). Both entry points, `import` and `require`, export
exactly this set. The TSDoc text on each member is required (FR-023) but is not repeated here.

## Declarations

```ts
// ---------------------------------------------------------------- word lists and entries
export type WordMatchMode = 'wholeWord' | 'anywhere';

export type WordCategory =
  | 'uncategorized' | 'profanity' | 'sexual' | 'insult' | 'slur' | 'harassment' | 'mild';

/** Every category, in declaration order. */
export declare const WORD_CATEGORIES: readonly WordCategory[];

export interface BannedWord {
  readonly text: string;
  readonly mode?: WordMatchMode;       // default 'wholeWord'
  readonly category?: WordCategory;    // default 'uncategorized'
}

/** An entry with every field present, as the bundled lists and matches hold it. Frozen. */
export type ResolvedWord = Readonly<Required<BannedWord>>;

export declare class WordListFormatError extends Error {
  readonly line: number;
}

export declare const WordList: {
  readonly all: readonly Readonly<Required<BannedWord>>[];
  readonly persianDefault: readonly Readonly<Required<BannedWord>>[];
  bundled(...categories: WordCategory[]): readonly Readonly<Required<BannedWord>>[];
  parse(text: string): readonly Readonly<Required<BannedWord>>[];
};

// ---------------------------------------------------------------- filter
export interface ProfanityFilterOptions {
  readonly squeezeRepeatedLetters?: boolean;   // default true
  readonly foldLookalikeCharacters?: boolean;  // default true
  readonly joinSpacedLetters?: boolean;        // default true
}

export type EvasionKind = 'repeatedLetters' | 'lookalikeCharacters' | 'splitWord';

export interface ProfanityMatch {
  readonly word: Readonly<Required<BannedWord>>;
  readonly evasion: readonly EvasionKind[];
  readonly index: number;   // UTF-16 code units
  readonly length: number;  // UTF-16 code units
}

export declare class ProfanityFilter {
  constructor(words: Iterable<BannedWord>, options?: ProfanityFilterOptions);
  readonly count: number;
  containsProfanity(text: string | null | undefined): boolean;
  findMatch(text: string | null | undefined): ProfanityMatch | null;
  findMatches(text: string | null | undefined): readonly ProfanityMatch[];
  censor(text: string | null | undefined, mask?: string): string;
}

// ---------------------------------------------------------------- normalization
export type NormalizationStep =
  | 'compatibilityForms' | 'unifyLetters' | 'removeDiacritics' | 'removeTatweel'
  | 'removeZeroWidth' | 'removeBidiControls' | 'asciiDigits' | 'lowerCase'
  | 'collapseWhitespace' | 'collapseRepeats';

export type NormalizationSteps = 'comparison' | 'standard' | 'none' | readonly NormalizationStep[];

export declare function normalize(text: string | null | undefined, steps?: NormalizationSteps): string; // default 'comparison'
export declare function tokenize(text: string | null | undefined): string[];
export declare function toPersianDigits(text: string | null | undefined): string;
export declare function toAsciiDigits(text: string | null | undefined): string;
```

Nothing else is exported. In particular, source maps, scanner internals and `unicode.ts` are not.

## Behavioural guarantees

| # | Guarantee | Checked by |
| --- | --- | --- |
| G1 | For every string, including empty, whitespace-only, lone surrogates, noncharacters and very long strings, and for `null` and `undefined`: `containsProfanity`, `findMatch`, `findMatches`, `censor` (with a valid mask), `normalize`, `tokenize`, `toPersianDigits` and `toAsciiDigits` return and never throw. | corpus robustness cases; unit tests |
| G2 | Every other value passed as `text` throws `TypeError`. | unit tests |
| G3 | Results equal the conformance corpus for every case, after position conversion. | corpus runner |
| G4 | `containsProfanity(t) === (findMatch(t) !== null) === (findMatches(t).length > 0)`, and `censor(t) !== t` exactly when there is a match (for a string `t`). | corpus (all fields recorded together); unit test over corpus inputs |
| G5 | `findMatches` results are ordered by `index`, never overlap, and satisfy `0 ≤ index`, `length ≥ 1`, `index + length ≤ text.length`. | corpus; unit tests |
| G6 | `censor` validates `mask` before looking at `text`: a non-string mask throws `TypeError`; a mask that is not one UTF-16 unit, or is a letter, digit, whitespace, control character or surrogate, throws `RangeError`. This holds even when `text` is `null`. | corpus mask-validation cases (acceptance); unit tests (error type) |
| G7 | A filter never changes after construction. Its results, `count`, and matched `word` values do not change if the caller later mutates the entries or options it passed. All returned arrays and match objects are frozen. | unit tests |
| G8 | `WordList.all` and `WordList.persianDefault` return the same array instance on every access. `persianDefault` contains no `'mild'` entry. | unit tests; corpus category-selection cases |
| G9 | `WordList.parse` reads `[category]` sections by category **name** only, case-insensitively and with surrounding spaces allowed (`[ Insult ]`); a number such as `[3]` is an unknown category (research R17). It also reads `~` for anywhere, `#` comments, blank lines and `\r\n`. An unknown category throws `WordListFormatError` with a 1-based `line`. | corpus word-list-parsing cases; unit tests (error type and `line`) |
| G10 | The bundled selections equal the .NET package's entry for entry, in order, for the same `wordlists/`. | corpus category-selection cases; one-time comparison in quickstart |
| G11 | `import` and `require` expose the same members and give identical results. | consumer checks (research R12) |

## .NET-to-JavaScript names

This table is copied into `js/README.md` (FR-012).

| .NET | JavaScript/TypeScript |
| --- | --- |
| `new ProfanityFilter(IEnumerable<BannedWord>, ProfanityFilterOptions?)` | `new ProfanityFilter(Iterable<BannedWord>, ProfanityFilterOptions?)` |
| `filter.Count` | `filter.count` |
| `filter.ContainsProfanity(text)` | `filter.containsProfanity(text)` |
| `filter.FindMatch(text)` | `filter.findMatch(text)` (`null` when none) |
| `filter.FindMatches(text)` | `filter.findMatches(text)` |
| `filter.Censor(text)` / `filter.Censor(text, '#')` | `filter.censor(text)` / `filter.censor(text, '#')` |
| `new BannedWord("x", WordMatchMode.Anywhere) { Category = WordCategory.Slur }` | `{ text: 'x', mode: 'anywhere', category: 'slur' }` |
| `ProfanityFilterOptions { SqueezeRepeatedLetters = false }` | `{ squeezeRepeatedLetters: false }` |
| `match.Word`, `match.Evasion`, `match.Index`, `match.Length` | `match.word`, `match.evasion`, `match.index`, `match.length` |
| `EvasionKind.LookalikeCharacters \| EvasionKind.RepeatedLetters` | `['repeatedLetters', 'lookalikeCharacters']` |
| `WordCategory.Harassment` | `'harassment'` |
| `WordList.All`, `WordList.PersianDefault` | `WordList.all`, `WordList.persianDefault` |
| `WordList.Bundled(WordCategory.Slur, WordCategory.Harassment)` | `WordList.bundled('slur', 'harassment')` |
| `WordList.Parse(text)` | `WordList.parse(text)` |
| `WordList.Load(stream)` | read the file yourself, then `WordList.parse(text)` |
| `PersianNormalizer.Normalize(text)` | `normalize(text)` |
| `PersianNormalizer.Normalize(text, PersianNormalization.Standard)` | `normalize(text, 'standard')` |
| `PersianNormalization.UnifyLetters \| PersianNormalization.LowerCase` | `['unifyLetters', 'lowerCase']` |
| `PersianNormalizer.Tokenize(text)` | `tokenize(text)` |
| `PersianNormalizer.ToPersianDigits(text)` / `ToAsciiDigits(text)` | `toPersianDigits(text)` / `toAsciiDigits(text)` |
| `FormatException` from `WordList.Parse` | `WordListFormatError` with `.line` |
| `ArgumentException` from `Censor` | `RangeError` (or `TypeError` for a non-string mask) |

## Compatibility

- **1.3.0 is the first release of this package.** Its API report is the baseline (FR-022).
- **After that**, removing or changing any declaration above is MAJOR, and adding to it is MINOR
  (constitution, Public API & Versioning).
- **Changes to string literal unions**: widening a union that callers pass in (a new `WordCategory`) is
  MINOR. Adding a member to a union the package returns (`EvasionKind`) is also MINOR under the
  constitution, but is called out in release notes, because exhaustive `switch` statements in callers
  stop compiling.
