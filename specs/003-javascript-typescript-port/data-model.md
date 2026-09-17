# Data Model: JavaScript/TypeScript Port Published to npm

**Feature**: [spec.md](spec.md) | **Research**: [research.md](research.md) | **API contract**: [contracts/public-api.md](contracts/public-api.md)

This port adds runtime types to a new package. They mirror the .NET package's types, using the names and
shapes the conformance corpus already uses (lowerCamelCase enum values, ordered evasion arrays). Nothing
here changes the corpus data model from feature 002, apart from the writing rule for noncharacters
(research R3).

## Entry (`BannedWord`)

A word or phrase to look for, as a caller writes it.

| Field | Type | Rule |
| --- | --- | --- |
| `text` | `string` | Required. Any spelling; normalized when a filter is built. Blank after trimming → ignored. |
| `mode` | `WordMatchMode` | Optional; default `'wholeWord'`. |
| `category` | `WordCategory` | Optional; default `'uncategorized'`. |

- `WordMatchMode` = `'wholeWord' | 'anywhere'`.
- `WordCategory` = `'uncategorized' | 'profanity' | 'sexual' | 'insult' | 'slur' | 'harassment' | 'mild'`.
  `WORD_CATEGORIES` lists them in this order, which is also .NET's enum order.

**Validation (at filter construction)**:
- Throws `TypeError` if an entry is not an object with a string `text`, or if `mode` or `category` is not
  one of the values above.
- Spellings that normalize to the same entry count once (`count`).

## Options (`ProfanityFilterOptions`)

| Field | Type | Default | Meaning |
| --- | --- | --- | --- |
| `squeezeRepeatedLetters` | `boolean` | `true` | Read through held keys (`fuuuck`). |
| `foldLookalikeCharacters` | `boolean` | `true` | Read through digits, symbols, accents and Cyrillic/Greek look-alikes, and filler inside words. |
| `joinSpacedLetters` | `boolean` | `true` | Read through spaced, dotted and once-split words. |

Unknown keys are a compile error in TypeScript. In JavaScript they are ignored.

## Filter (`ProfanityFilter`)

**Construction**: `new ProfanityFilter(words, options?)`.
- It copies each entry into a frozen, fully defaulted snapshot, and copies the options.
- It builds all lookups.
- After that it never changes (Principle IV): later mutation of the caller's entries or options has no
  effect.

| Member | Result |
| --- | --- |
| `count` | Number of distinct normalized entries. |
| `containsProfanity(text)` | `boolean` |
| `findMatch(text)` | `ProfanityMatch \| null` |
| `findMatches(text)` | `readonly ProfanityMatch[]`, ordered by `index`, never overlapping; frozen |
| `censor(text, mask = '*')` | `string`. Clean text is returned unchanged; `null`/`undefined` gives `""`. |

**Input rule** for every `text` parameter: a string, `null` or `undefined` never throws; any other value
throws `TypeError` (spec Clarifications).

## Match (`ProfanityMatch`)

Frozen.

| Field | Type | Rule |
| --- | --- | --- |
| `word` | `Readonly<Required<BannedWord>>` | The filter's snapshot of the matched entry. |
| `evasion` | `readonly EvasionKind[]` | A subset of `repeatedLetters`, `lookalikeCharacters`, `splitWord`, in that order; `[]` means none. |
| `index` | `number` | The region's first UTF-16 code unit in the text as passed; covers whole words. |
| `length` | `number` | UTF-16 code units, `≥ 1`; includes separators inside a split word. |

`text.slice(index, index + length)` is the matched region. Corpus comparison converts corpus code-point
positions to these units (corpus-format contract).

## Normalization steps

`NormalizationStep` =
`'compatibilityForms' | 'unifyLetters' | 'removeDiacritics' | 'removeTatweel' | 'removeZeroWidth' | 'removeBidiControls' | 'asciiDigits' | 'lowerCase' | 'collapseWhitespace' | 'collapseRepeats'`.

`NormalizationSteps` = `'comparison' | 'standard' | 'none' | readonly NormalizationStep[]`.

| Preset | Steps |
| --- | --- |
| `'standard'` | `compatibilityForms`, `unifyLetters`, `removeTatweel`, `removeBidiControls`, `collapseWhitespace` |
| `'comparison'` | `standard` + `removeDiacritics`, `removeZeroWidth`, `asciiDigits`, `lowerCase`, `collapseRepeats` |
| `'none'` | none |

Step order is fixed by the algorithm, not by the array order (as with .NET flags).

## Word lists (`WordList`)

| Member | Result |
| --- | --- |
| `WordList.all` | Every bundled entry, in list order: Persian, Finglish, English. The same frozen array every time. |
| `WordList.persianDefault` | `all` without `'mild'`. The same frozen array every time. |
| `WordList.bundled(...categories)` | A new frozen array of the bundled entries in those categories, in list order. |
| `WordList.parse(text)` | The entries of word-list text. `[section]` headings set the category by name only (any case, spaces allowed; `[3]` is an unknown category, research R17), `~` sets anywhere, `#` starts a comment, and `\r\n` is accepted. |

**Errors**:
- `WordList.parse` throws `WordListFormatError` (with a `line` number) for an unknown category, and
  `TypeError` for a non-string.
- `bundled` throws `TypeError` for an unknown category name.

## Errors

| Type | When | Extra fields |
| --- | --- | --- |
| `TypeError` | A non-string message or text; missing or non-iterable `words`; a malformed entry; a non-string mask; an unknown category passed to `WordList.bundled`; an unknown normalization preset or step name | — |
| `RangeError` | A mask string that is not exactly one UTF-16 unit, or is a letter, digit, whitespace, control character or surrogate | — |
| `WordListFormatError` | Word-list text names an unknown category | `line: number` (1-based) |

## Corpus runner model (tests only)

The JavaScript runner reads the same entities as the .NET runner (feature 002 data model: Corpus,
Configuration, Input, Case, Match). It is not shipped.
- Code-point positions convert to UTF-16: a valid surrogate pair counts as one code point and two units;
  any other unit, a lone surrogate included, counts as one of each.
- **Kind rules**: `ordinary` requires no match and censored output equal to the input; `must-match`
  requires `containsProfanity`; `robustness` allows either outcome.

## Package (`persian-text-guard`)

| Aspect | Value |
| --- | --- |
| Version | From `VERSION` at pack time (research R9) |
| Entry points | `exports["."]`: `import` → `dist/index.mjs` + `dist/index.d.mts`; `require` → `dist/index.cjs` + `dist/index.d.cts` |
| Files | `dist/`, `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md`, `package.json` |
| Runtime dependencies | none |
| `engines.node` | `>=22` |
| `sideEffects` | `false` |

## State transitions

Only the release has states:

```text
merged PR (VERSION = 1.3.0, main green)
  → tag v1.3.0 pushed
  → all CI jobs green
  → Publish to NuGet ∥ Publish to npm   (a failure before this point publishes nothing)
  → both registries verified (SC-010)
  → placeholder 0.0.1 deprecated
```
