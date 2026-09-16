# Contract: Conformance Corpus Format, v1

**Feature**: [../spec.md](../spec.md) | **Data model**: [../data-model.md](../data-model.md)

This is the interface between the corpus and every port's runner, today .NET and later JavaScript,
Python, Java, Go and Rust. A runner that follows this contract can check its port without reading any
other port's code.

## Files

All corpus files are UTF-8 without a byte-order mark and use LF line endings.

| Path | Content |
| --- | --- |
| `conformance/corpus.json` | Metadata object |
| `conformance/configurations.json` | Array of configuration objects |
| `conformance/cases/*.json` | Each file is one array of case objects; a file may mix kinds |
| `conformance/README.md` | Human guide: format, adding a case, the fill-in tool |

## Examples

### `corpus.json`

```json
{
  "formatVersion": 1,
  "recordedFrom": "1.2.0",
  "positionUnit": "codePoint"
}
```

### `configurations.json`

```json
[
  { "name": "default", "wordLists": { "bundled": "default" } },
  { "name": "all", "wordLists": { "bundled": "all" } },
  { "name": "no-folding", "wordLists": { "bundled": "default" }, "options": { "foldLookalikeCharacters": false } },
  {
    "name": "custom-whole-and-anywhere",
    "wordLists": { "entries": [
      { "text": "whole", "mode": "wholeWord", "category": "uncategorized" },
      { "text": "fuck", "mode": "anywhere", "category": "uncategorized" }
    ] }
  }
]
```

### A must-match case

```json
{
  "id": "fa-suffix-plural-zwnj",
  "kind": "must-match",
  "configuration": "default",
  "note": "Plural with a zero-width non-joiner; the suffix is part of the region.",
  "input": "جنده‌ها رو ببین",
  "masks": ["#"],
  "expected": {
    "containsProfanity": true,
    "firstMatch": {
      "entry": { "text": "جنده", "mode": "wholeWord", "category": "insult" },
      "evasion": [],
      "start": 0,
      "length": 7
    },
    "matches": [
      {
        "entry": { "text": "جنده", "mode": "wholeWord", "category": "insult" },
        "evasion": [],
        "start": 0,
        "length": 7
      }
    ],
    "censored": "**** رو ببین",
    "censoredWith": { "#": "#### رو ببین" }
  }
}
```

### An ordinary case

```json
{
  "id": "fa-ordinary-kasi-someone",
  "kind": "ordinary",
  "configuration": "default",
  "note": "کسی means 'someone'; only the insults built on کس are listed.",
  "input": "کسی هست بیاد دو نفره",
  "expected": {
    "containsProfanity": false,
    "firstMatch": null,
    "matches": [],
    "censored": "کسی هست بیاد دو نفره"
  }
}
```

### A robustness case, built

```json
{
  "id": "robust-lone-high-surrogate-before-word",
  "kind": "robustness",
  "configuration": "default",
  "note": "A message cut in the middle of an emoji. Position counts the lone surrogate as one code point.",
  "input": { "build": [ { "text": "hi " }, { "utf16": "D83D" }, { "text": " kir" } ] },
  "expected": {
    "containsProfanity": true,
    "firstMatch": {
      "entry": { "text": "kir", "mode": "wholeWord", "category": "sexual" },
      "evasion": [],
      "start": 5,
      "length": 3
    },
    "matches": [
      {
        "entry": { "text": "kir", "mode": "wholeWord", "category": "sexual" },
        "evasion": [],
        "start": 5,
        "length": 3
      }
    ],
    "censored": { "build": [ { "text": "hi " }, { "utf16": "D83D" }, { "text": " ****" } ] }
  }
}
```

`expected.censored` and each value of `censoredWith` may themselves be Input objects, for outputs
that contain unrepresentable text.

### Other kinds

```json
{ "id": "normalize-comparison-readme", "kind": "normalization",
  "input": "كتاب‌هاي  ۱۲ ABC", "steps": "comparison",
  "expected": { "output": "کتابهای 12 abc" } }

{ "id": "tokenize-persian-punctuation", "kind": "tokenization",
  "input": "سلام، دنیا! خوبی؟",
  "expected": { "tokens": ["سلام", "دنیا", "خوبی"] } }

{ "id": "wordlist-unknown-category", "kind": "word-list-parsing",
  "text": "[nonsense]\nword\n",
  "expected": { "error": { "kind": "unknown-category", "line": 1 } } }

{ "id": "selection-slur", "kind": "category-selection",
  "selection": ["slur"],
  "expected": { "count": 0, "first": [], "last": [],
                "rules": ["categoriesInSelection", "bundledOrder"] } }

{ "id": "mask-letter-rejected", "kind": "mask-validation",
  "mask": "x", "expected": { "accepted": false } }
```

(The selection count above is a placeholder in this example. The real value is recorded by the fill-in
tool.)

## Writing rules

1. A character in Unicode category Cf, Cc (other than JSON's mandatory escapes), Zl or Zp, and any
   whitespace other than U+0020, MUST be written as a `\uXXXX` escape. Every other character SHOULD be
   written literally.
2. A lone surrogate MUST NOT appear as a JSON string escape; use an Input `build` object with a
   `utf16` part. This applies to every text-valued field, a `mask-validation` case's `mask` included.
3. Enum-like strings use lowerCamelCase: `wholeWord`, `repeatedLetters`, `sexual`.
4. `evasion` arrays list names in the order `repeatedLetters`, `lookalikeCharacters`, `splitWord`.
5. A case without `expected` is pending. Pending cases are allowed on disk only between adding a case
   and running the fill-in tool; CI fails on them.

## Runner obligations

Every port's runner MUST do the following.

**Checking the corpus before any case:**

1. Refuse to run if `formatVersion` is newer than it understands.
2. Fail if the corpus cannot be found or parsed, has fewer than 300 cases, has duplicate ids, or has
   pending cases.

**Running cases:**

3. Build each Input as described, or report the case as **not applicable** by id when its language
   cannot represent a part. Not-applicable cases are listed in the run output, never silently skipped.
4. Check the kind rules (data model) before comparing results.
5. Compare **every** field of `expected` exactly (spec Clarification 1). The only conversion allowed is
   of positions, from code points to the port's native unit, counting a lone surrogate as one code
   point.

**Reporting:**

6. Report each failing case separately, with its id, file, built input (invisible characters shown as
   `\uXXXX`) and a field-by-field expected-versus-actual listing, and keep running after a failure.
7. Run every case on every target the port supports (constitution, Principle III).

## Compatibility of the format

- Adding a new optional field, case kind or configuration option is a **minor** format change. It
  leaves `formatVersion` unchanged, provided runners fail on case kinds they do not know rather than
  skip them.
- Renaming or removing a field, or changing a field's meaning, bumps `formatVersion`, and every runner
  is updated in the same pull request (constitution, Principle V).
