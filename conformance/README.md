# Conformance corpus

This directory is the **specification of PersianTextGuard's behaviour**, written once in a form
every language can check (constitution, Principle V). Every port — .NET today, JavaScript, Python,
Java, Go and Rust later — runs every case here and must produce exactly the recorded result. When a
port and the corpus disagree, the port is wrong, or the behaviour change is made on purpose by
updating the case in the same pull request.

The initial expectations were recorded from release 1.2.0. The full contract for runners is
[`specs/002-monorepo-conformance-corpus/contracts/corpus-format.md`](../specs/002-monorepo-conformance-corpus/contracts/corpus-format.md).

## Layout

```text
conformance/
├── README.md               # this file
├── corpus.json             # metadata: format version, recorded-from release, position unit
├── configurations.json     # named filter configurations that cases refer to
└── cases/
    ├── matching-persian.json
    ├── matching-finglish.json
    ├── matching-english.json
    ├── matching-options.json      # cases under a configuration other than "default"
    ├── robustness.json            # missing value, invalid text, very long, blank
    ├── normalization.json
    ├── tokenization.json
    ├── word-list-parsing.json
    ├── category-selection.json
    └── mask-validation.json
```

Every file is UTF-8 without a byte-order mark, with LF line endings. Each case file is one JSON array
of cases.

Positions (`start`, `length`) count **Unicode code points** of the input. A lone surrogate counts as
one code point.

## Case kinds, by example

### `must-match`

The message must be flagged. Every field of `expected` is compared exactly.

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

### `ordinary`

The message must pass untouched: no match, and the censored text equals the input. Runners check
these rules before comparing, so a mislabelled case is reported as breaking its kind rule.

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

### `robustness`

Input that must never make a port throw. Either outcome is allowed, but the recorded one is compared
exactly. Text JSON cannot carry portably is described with a `build` object:

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

A build part is `{ "text": "…" }`, `{ "repeat": "…", "times": N }` or `{ "utf16": "XXXX" }` (one
UTF-16 code unit, which may be a lone surrogate). `null` as an input means the language's missing
value. Output text (`censored`, `censoredWith`, `output`, `tokens`) may also be a `build` object; it
is compared by the text it builds.

### The other kinds

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

(The selection count above is a placeholder. Real values are recorded by the fill-in tool.)

- `normalization` `steps` is `"comparison"`, `"standard"`, `"none"`, an array of step names
  (`compatibilityForms`, `unifyLetters`, `removeDiacritics`, `removeTatweel`, `removeZeroWidth`,
  `removeBidiControls`, `asciiDigits`, `lowerCase`, `collapseWhitespace`, `collapseRepeats`), or one
  of the digit helpers `"toPersianDigits"` and `"toAsciiDigits"`.
- `category-selection` records the exact count, the first and last five entries, and rules that hold
  for every entry: `categoriesInSelection`, `bundledOrder`, and `noMild` for `"default"`.

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

Rule 1 is enforced by a test: a zero-width non-joiner typed straight into a file fails the build, and
the failure names the file, line and code point. Line breaks and tabs may use `\n`, `\r` and `\t`.

Case ids are lowercase ASCII letters, digits and single hyphens, unique across all files, and stable
once committed.

## Adding a case

1. Append a case to the file it belongs in, with an `id`, `kind`, `configuration` (for matching kinds)
   and `input`, and **no** `expected`:

   ```json
   {
     "id": "fa-ordinary-kasi-someone",
     "kind": "ordinary",
     "configuration": "default",
     "input": "کسی هست بیاد دو نفره"
   }
   ```

2. From the repository root, fill in the expectation from the .NET port:

   ```bash
   dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill
   ```

3. Review the recorded `expected` in the diff: it is now the specification. Then run the corpus:

   ```bash
   dotnet test dotnet/tests/PersianTextGuard.Conformance
   ```

You can also write `expected` by hand; the runner checks it the same way.

## The fill-in tool

| Command | What it does | Exit code |
| --- | --- | --- |
| `dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill` | Fills `expected` for pending cases, rewriting only the files that had one, and prints `DISAGREES <id> <path>: expected … actual …` for every recorded case the .NET port disagrees with | `0` when nothing disagrees, `1` otherwise |
| `dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill -- --check` | The same comparison; writes nothing | `0` / `1` |
| `dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill -- --seed` | Adds pending cases from the existing .NET tests and the supplementary suite, skipping any whose content is already in the corpus, then fills them | `0` / `1` |

**The tool never overwrites an existing `expected`.** When the .NET port disagrees with a recorded
case, it reports the disagreement and leaves the case alone. A person decides whether the port or the
case is wrong, and fixes it on purpose.

A pending case whose computed result breaks its kind rule — say, an `ordinary` message the filter
flags — is reported as `KIND RULE` and left pending, so a mislabelled case is never recorded.
