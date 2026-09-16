# Data Model: Monorepo with Shared Word Lists and a Conformance Corpus

**Feature**: [spec.md](spec.md) | **Research**: [research.md](research.md) | **Contract**: [contracts/corpus-format.md](contracts/corpus-format.md)

The data this feature introduces is repository data: the corpus files, the shared word lists and the
version source. Nothing here is a runtime type in the published package. The package's public API
does not change (FR-022).

## Corpus

Metadata, in `conformance/corpus.json`.

| Field | Type | Rule |
| --- | --- | --- |
| `formatVersion` | integer | `1`. Bumped only when the file shapes change incompatibly; runners refuse a newer format. |
| `recordedFrom` | string | The release the initial expectations were recorded from: `"1.2.0"` (FR-009). |
| `positionUnit` | string | Always `"codePoint"` (FR-004). |

**Relationships**: a corpus has one set of named configurations and many case files, each holding
cases of one kind.

**Validation**:
- Case ids are unique across all files.
- There are at least 300 cases (SC-002).
- No case is pending.
- No file contains an unescaped invisible character (research R3).

## Configuration

Named in `conformance/configurations.json`. Cases refer to a configuration by `name`.

| Field | Type | Rule |
| --- | --- | --- |
| `name` | string | Unique. `default` is required. |
| `wordLists` | object | Exactly one of the three forms below. |
| `options` | object | Optional. `squeezeRepeatedLetters`, `foldLookalikeCharacters`, `joinSpacedLetters`, each a boolean defaulting to `true`. |

`wordLists` takes one of these forms:

- `{ "bundled": "default" }` — the bundled default selection;
- `{ "bundled": "all" }` — every bundled entry;
- `{ "bundled": ["slur", "harassment"] }` — the chosen categories;
- `{ "entries": [ Entry… ] }` — a custom list.

**Required configurations** (at least):

| Name | Word lists | Options |
| --- | --- | --- |
| `default` | bundled `default` | all on |
| `all` | bundled `all` | all on |
| `slurs-only` | bundled `["slur"]` | all on |
| `no-folding` | bundled `default` | `foldLookalikeCharacters: false` |
| `no-squeezing` | bundled `default` | `squeezeRepeatedLetters: false` |
| `no-joining` | bundled `default` | `joinSpacedLetters: false` |

Custom-list configurations reproduce the `ProfanityFilterTests` scenarios.

## Entry

A word-list entry, as used in configurations and expected results.

| Field | Type | Rule |
| --- | --- | --- |
| `text` | string | As written in the list, before normalization. |
| `mode` | string | `wholeWord` or `anywhere`. |
| `category` | string | `uncategorized`, `profanity`, `sexual`, `insult`, `slur`, `harassment` or `mild`. |

## Input

The message or text a case feeds in (research R4).

| Form | Meaning |
| --- | --- |
| JSON string | That text. Invisible characters are written as `\uXXXX` escapes (R3). |
| `null` | The language's missing value. |
| `{ "build": [ Part… ] }` | Text assembled from its parts, in order. |

A **part** is one of:

- `{ "text": "…" }` — literal text;
- `{ "repeat": "…", "times": N }` — the text repeated `N` times, with `N ≥ 1`;
- `{ "utf16": "XXXX" }` — one UTF-16 code unit given as four hex digits, which may be a lone
  surrogate.

**Applicability**: a runner that cannot build a part reports the case as not applicable, by id. .NET
can build every part.

## Case

Common fields, shared by every kind.

| Field | Type | Rule |
| --- | --- | --- |
| `id` | string | Unique; lowercase ASCII letters, digits and hyphens; stable once committed. |
| `kind` | string | One of the kinds below. |
| `note` | string | Optional. Why the case exists: `"کسی also means someone"`. |
| `expected` | object | The kind's expected results. Absent means **pending** (research R6). |

### Matching kinds: `ordinary`, `must-match`, `robustness`

| Field | Type | Rule |
| --- | --- | --- |
| `configuration` | string | The name of a configuration. |
| `input` | Input | |
| `masks` | array of strings | Optional. Extra mask characters to censor with, each exactly one UTF-16 code unit. |
| `expected.containsProfanity` | boolean | |
| `expected.firstMatch` | Match or `null` | |
| `expected.matches` | array of Match | Ordered by `start`, never overlapping. |
| `expected.censored` | string | Censored with `*`. |
| `expected.censoredWith` | object | A key for each entry of `masks`: the mask character maps to the censored text. |

**Validation**:
- `ordinary` ⇒ `containsProfanity` is `false`, `firstMatch` is `null`, `matches` is empty, and
  `censored` and every value of `censoredWith` equal the built input. A missing input's censored text
  is `""`.
- `must-match` ⇒ `containsProfanity` is `true`.
- `robustness` ⇒ either outcome is allowed. It marks inputs that test "never throws" (Principle II).

The runner checks these kind rules before comparing, so a mislabelled case is reported as mislabelled,
not merely as a mismatch.

### Match

| Field | Type | Rule |
| --- | --- | --- |
| `entry` | Entry | The matched entry, exactly as listed. |
| `evasion` | array of strings | Subset of `repeatedLetters`, `lookalikeCharacters`, `splitWord`, in that order; `[]` = none. |
| `start` | integer | First code point of the region in the built input. |
| `length` | integer | Code points in the region; `≥ 1`. |

### `normalization`

| Field | Type | Rule |
| --- | --- | --- |
| `input` | Input | |
| `steps` | string or array | `"comparison"`, `"standard"`, `"none"`, or step names (`compatibilityForms`, `unifyLetters`, `removeDiacritics`, `removeTatweel`, `removeZeroWidth`, `removeBidiControls`, `asciiDigits`, `lowerCase`, `collapseWhitespace`, `collapseRepeats`). |
| `expected.output` | string | |

Also used for the digit helpers, with `steps` set to `"toPersianDigits"` or `"toAsciiDigits"`.

### `tokenization`

| Field | Type | Rule |
| --- | --- | --- |
| `input` | Input | |
| `expected.tokens` | array of strings | |

### `word-list-parsing`

| Field | Type | Rule |
| --- | --- | --- |
| `text` | string | Word-list file text. Line breaks are written as `\n` or `\r\n` escapes. |
| `expected.entries` | array of Entry | Present when parsing succeeds. |
| `expected.error` | object | `{ "kind": "unknown-category", "line": N }`, present when parsing must fail. |

Exactly one of `entries` and `error` is present.

### `category-selection`

| Field | Type | Rule |
| --- | --- | --- |
| `selection` | `"default"`, `"all"` or array of categories | |
| `expected.count` | integer | Exact. |
| `expected.first` | array of Entry | The first 5 entries, in order. |
| `expected.last` | array of Entry | The last 5 entries, in order. |
| `expected.rules` | array of strings | Always checked for every entry; see below. |

The rules are:

- `categoriesInSelection` — every entry's category is one the selection includes;
- `noMild` — no entry is `mild`; required when the selection is `default`;
- `bundledOrder` — entries appear in the same relative order as in `all`.

### `mask-validation`

| Field | Type | Rule |
| --- | --- | --- |
| `mask` | string or Input `build` object | Exactly one UTF-16 code unit. A lone surrogate MUST be given as `{ "build": [ { "utf16": "D83D" } ] }`, never as a JSON string escape. |
| `expected.accepted` | boolean | |

## Shared word list

`wordlists/{persian,finglish,english}.txt`. The format is unchanged from 1.2.0:

- `[category]` sections;
- `~` marks an entry matched anywhere;
- `#` starts a comment.

The header comments move with the files.

**Rules**:
- Exactly one copy of each file exists in the repository (FR-014, SC-004).
- Entry order is part of behaviour: it breaks ties (spec, Edge Cases).
- LF line endings (research R11).

## Version source

`VERSION` at the repository root. It holds one Semantic Version (`1.2.0`) and a trailing newline,
nothing else.

**Rules**:
- It is the only place the version is set (FR-018).
- A release tag must equal `v` followed by its contents (research R13).

## State transitions

Only a case changes state:

```text
pending ──(fill-in tool computes expected from .NET)──► recorded
recorded ──(maintainer edits expected, reviewed in the PR)──► recorded
recorded ──(fill-in tool finds .NET disagrees)──► recorded, reported (never rewritten)
```
