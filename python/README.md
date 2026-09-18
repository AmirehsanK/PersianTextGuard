# persian-text-guard

Persian text normalization and evasion-resistant profanity filtering for Python.

[فارسی](#فارسی) · [Full project README](https://github.com/AmirehsanK/PersianTextGuard#readme) · [.NET package](https://www.nuget.org/packages/PersianTextGuard) · [npm package](https://www.npmjs.com/package/persian-text-guard)

A word list is easy to get around. Type an Arabic `ي` instead of `ی`, slip a zero-width character
inside a word, write `f u c k`, `sh1t`, `کیییییر` or `k0s`, and a plain `word in text` check sees
nothing. `persian-text-guard` reads through those tricks, and just as importantly, it doesn't flag
ordinary messages: `هر کس`, `تخم مرغ`, `class`, `push it` and `Scunthorpe` all pass.

It is the Python port of [PersianTextGuard](https://github.com/AmirehsanK/PersianTextGuard), and gives
the same answers as the .NET and JavaScript packages for every case in the shared
[conformance corpus](https://github.com/AmirehsanK/PersianTextGuard/tree/main/conformance).

- Pure Python, with full type hints (`py.typed`), for CPython 3.11 and later, free-threaded 3.14
  included.
- No dependencies.
- Safe to share across threads.

## Installation

```bash
pip install persian-text-guard
```

or, in a project managed by uv:

```bash
uv add persian-text-guard
```

## Quick start

```python
from persian_text_guard import ProfanityFilter, WordList

# Build once and share it: a filter never changes after it is built.
filter = ProfanityFilter(WordList.persian_default())

assert not filter.contains_profanity("سلام، سفارشم کی میرسه؟")
assert filter.contains_profanity("ک.ی.ر")
assert filter.contains_profanity("f u c k")
```

### What matched, and why

```python
from persian_text_guard import EvasionKind, ProfanityFilter, WordCategory, WordList

filter = ProfanityFilter(WordList.persian_default())
match = filter.find_match("sh1iiit")

assert match is not None
assert match.word.text == "shit"
assert match.word.category == WordCategory.PROFANITY
assert match.evasion == (EvasionKind.REPEATED_LETTERS, EvasionKind.LOOKALIKE_CHARACTERS)
assert filter.find_match("hello") is None
```

### Every match, with its position

Positions are Python string indexes (code points), so slicing gives the matched text:

```python
from persian_text_guard import ProfanityFilter, WordList

filter = ProfanityFilter(WordList.persian_default())
message = "sh1t and f u c k"

found = [
    (match.word.text, match.index, match.length, message[match.index : match.index + match.length])
    for match in filter.find_matches(message)
]
assert found == [("shit", 0, 4, "sh1t"), ("fuck", 9, 7, "f u c k")]
```

This holds for emoji and every other character outside the Basic Multilingual Plane too: each counts
as one position, as it does everywhere else in Python.

```python
from persian_text_guard import ProfanityFilter, WordList

filter = ProfanityFilter(WordList.persian_default())
message = "\N{GRINNING FACE} کیر"

match = filter.find_match(message)
assert match is not None
assert (match.index, match.length) == (2, 3)
assert message[match.index : match.index + match.length] == "کیر"
```

### Censoring

```python
from persian_text_guard import ProfanityFilter, WordList

filter = ProfanityFilter(WordList.persian_default())

assert filter.censor("kir and motherfucker") == "**** and ****"
assert filter.censor("جنده\N{ZERO WIDTH NON-JOINER}ها رو ببین") == "**** رو ببین"
assert filter.censor("this is kir", "#") == "this is ####"
assert filter.censor("hello") == "hello"
```

Whole words are hidden, every mask is four characters, and the output is always clean. Clean text
comes back unchanged, and everything outside a hidden word is returned exactly as given. The mask can
be any symbol or punctuation; a letter, digit, whitespace or control character raises `ValueError`.

### Categories

Every bundled entry has a category: `profanity`, `sexual`, `insult`, `slur`, `harassment` or `mild`.

```python
from persian_text_guard import ProfanityFilter, WordCategory, WordList

everyday = ProfanityFilter(WordList.persian_default())  # everything except mild
strict = ProfanityFilter(WordList.all())  # mild too
slurs_and_abuse = ProfanityFilter(WordList.bundled(WordCategory.SLUR, "harassment"))

assert not everyday.contains_profanity("این فیلم آشغال بود")
assert strict.contains_profanity("این فیلم آشغال بود")
assert slurs_and_abuse.contains_profanity("kys")
assert not slurs_and_abuse.contains_profanity("کیر")
```

A category, like a match mode or a normalization step, can be given as an enum member or as its name
(`"slur"`); a type checker rejects a name that does not exist, and so does the code at run time.

### Your own words

```python
from persian_text_guard import BannedWord, ProfanityFilter, WordList, WordMatchMode

filter = ProfanityFilter(
    [
        BannedWord("اسپم"),  # whole word (the default)
        BannedWord("casino", WordMatchMode.ANYWHERE),  # inside longer words too
    ]
)
assert filter.contains_profanity("این پیام اسپم است")
assert filter.contains_profanity("onlinecasino.example")

# Or combine your entries with the bundled list:
combined = ProfanityFilter([*WordList.persian_default(), BannedWord("casino", "anywhere", "mild")])
assert combined.contains_profanity("casino night")
```

### Word-list files

Word lists can also be kept in a text file, one entry per line. `WordList.load` reads a path or an open
file as UTF-8, and `WordList.parse` reads text you already have:

```python
import tempfile
from pathlib import Path

from persian_text_guard import ProfanityFilter, WordCategory, WordList, WordMatchMode

text = "# comments start with #\n[insult]\nidiot\n~scam\n"

words = WordList.parse(text)
assert [(w.text, w.mode, w.category) for w in words] == [
    ("idiot", WordMatchMode.WHOLE_WORD, WordCategory.INSULT),
    ("scam", WordMatchMode.ANYWHERE, WordCategory.INSULT),
]
assert ProfanityFilter(words).contains_profanity("scammers everywhere")

with tempfile.TemporaryDirectory() as folder:
    path = Path(folder) / "words.txt"
    path.write_text(text, encoding="utf-8")
    assert WordList.load(path) == words
    with path.open("rb") as file:
        assert WordList.load(file) == words  # an open file is read, and left open
```

A heading such as `[insult]` names a category in any letter case. An unknown heading raises a
`WordListFormatError`, a `ValueError` with the heading's `line`. A UTF-8 byte order mark is ignored.

### Options

Every evasion the filter reads through can be turned off:

```python
from persian_text_guard import ProfanityFilter, ProfanityFilterOptions, WordList

literal = ProfanityFilter(WordList.persian_default(), ProfanityFilterOptions(join_spaced_letters=False))

assert not literal.contains_profanity("f u c k")
assert literal.contains_profanity("fuuuck")
```

### Normalization

```python
from persian_text_guard import NormalizationStep, normalize, to_ascii_digits, to_persian_digits, tokenize

text = "كتاب\N{ZERO WIDTH NON-JOINER}هاي  ۱۲ ABC"

# "comparison" (the default): everything folded, for searching and matching.
assert normalize(text) == "کتابهای 12 abc"
# "standard": safe to store and show; keeps the zero-width non-joiner, Persian digits and case.
assert normalize(text, "standard") == "کتاب\N{ZERO WIDTH NON-JOINER}های ۱۲ ABC"
# Or any combination of steps, as members or names.
assert normalize("ABC ي", [NormalizationStep.LOWER_CASE, "unifyLetters"]) == "abc ی"

assert tokenize("سلام، دنیا! خوبی؟") == ["سلام", "دنیا", "خوبی"]
assert to_persian_digits("2 ساعت پیش") == "۲ ساعت پیش"
assert to_ascii_digits("۱۴۰۴/۰۵/۱۴") == "1404/05/14"
```

## Thread safety

A filter is immutable after it is built, and the bundled lists are parsed once, under a lock. Build one
filter and share it between threads, including on free-threaded CPython 3.14 with the GIL disabled:

```python
from concurrent.futures import ThreadPoolExecutor

from persian_text_guard import ProfanityFilter, WordList

filter = ProfanityFilter(WordList.persian_default())
messages = ["سلام", "ک.ی.ر", "f u c k", "hello"] * 50

with ThreadPoolExecutor(max_workers=8) as pool:
    results = list(pool.map(filter.contains_profanity, messages))

assert results == [filter.contains_profanity(message) for message in messages]
```

## Validating input

Checking, finding matches, censoring, normalizing and tokenizing accept any `str`, including empty
text, broken emoji, lone surrogates and very long messages, and treat `None` as an empty message. They
never raise for text.

Anything else, such as `bytes`, a number or a list, raises `TypeError`, so a missing check shows up at
once instead of an unchecked value passing as clean. Values from a request are untrusted, so check that
the message is a string first:

```python
import json

from persian_text_guard import ProfanityFilter, WordList

filter = ProfanityFilter(WordList.persian_default())
body = json.loads('{"message": ["kir"]}')

message = body.get("message")
if not isinstance(message, str):
    outcome = "rejected: message must be text"
else:
    outcome = filter.censor(message)
assert outcome == "rejected: message must be text"
```

| Error | Raised by | When |
| --- | --- | --- |
| `TypeError` | every text function, `censor`, `WordList.parse`, the constructors | A value that is not a `str` (or `None`, where `None` is allowed), an option that is not a `bool`, or an entry that is not a `BannedWord` |
| `ValueError` | `censor`, enum parameters, `normalize` | An invalid mask, or an unknown category, mode, step or preset name |
| `WordListFormatError` (a `ValueError`) | `WordList.parse`, `WordList.load` | An unknown section heading; `.line` is 1-based |
| `OSError`, `UnicodeDecodeError` | `WordList.load` | The file cannot be read, or is not UTF-8 |

## .NET, JavaScript and Python

| .NET | JavaScript/TypeScript | Python |
| --- | --- | --- |
| `new ProfanityFilter(words, options)` | `new ProfanityFilter(words, options)` | `ProfanityFilter(words, options)` |
| `filter.Count` | `filter.count` | `filter.count` |
| `filter.ContainsProfanity(text)` | `filter.containsProfanity(text)` | `filter.contains_profanity(text)` |
| `filter.FindMatch(text)` | `filter.findMatch(text)` | `filter.find_match(text)` (`None` when there is no match) |
| `filter.FindMatches(text)` | `filter.findMatches(text)` | `filter.find_matches(text)` (a tuple) |
| `filter.Censor(text, '#')` | `filter.censor(text, '#')` | `filter.censor(text, "#")` |
| `new BannedWord("x", WordMatchMode.Anywhere) { Category = WordCategory.Slur }` | `{ text: 'x', mode: 'anywhere', category: 'slur' }` | `BannedWord("x", WordMatchMode.ANYWHERE, WordCategory.SLUR)` |
| `ProfanityFilterOptions { SqueezeRepeatedLetters = false }` | `{ squeezeRepeatedLetters: false }` | `ProfanityFilterOptions(squeeze_repeated_letters=False)` |
| `match.Word`, `match.Evasion`, `match.Index`, `match.Length` | `match.word`, `.evasion`, `.index`, `.length` | `match.word`, `.evasion`, `.index`, `.length` (code points) |
| `EvasionKind.LookalikeCharacters \| EvasionKind.RepeatedLetters` | `['repeatedLetters', 'lookalikeCharacters']` | `(EvasionKind.REPEATED_LETTERS, EvasionKind.LOOKALIKE_CHARACTERS)` |
| `WordList.All`, `WordList.PersianDefault` | `WordList.all`, `WordList.persianDefault` | `WordList.all()`, `WordList.persian_default()` |
| `WordList.Bundled(WordCategory.Slur)` | `WordList.bundled('slur')` | `WordList.bundled(WordCategory.SLUR)` or `WordList.bundled("slur")` |
| `WordList.Parse(text)` | `WordList.parse(text)` | `WordList.parse(text)` |
| `WordList.Load(stream)` | read the file, then `WordList.parse(text)` | `WordList.load(path_or_file)` |
| `PersianNormalizer.Normalize(text, PersianNormalization.Standard)` | `normalize(text, 'standard')` | `normalize(text, "standard")` |
| `PersianNormalizer.Tokenize(text)` | `tokenize(text)` | `tokenize(text)` |
| `PersianNormalizer.ToPersianDigits(text)` | `toPersianDigits(text)` | `to_persian_digits(text)` |
| `FormatException` | `WordListFormatError` (`.line`) | `WordListFormatError` (`.line`), a `ValueError` |
| `ArgumentException` (mask) | `RangeError` | `ValueError` |

## Performance

Against `WordList.persian_default()` (about 1,000 entries), on CPython 3.14.6 (Intel Core i7-9700K,
pyperf with `--rigorous`):

| Operation | Mean | Operations/s |
| --- | ---: | ---: |
| Short clean message (5 words) | 91.4 µs | 10,937 |
| Long clean message (60 words) | 909 µs | 1,100 |
| Message with evasions | 50.4 µs | 19,850 |
| Normalize a long message | 35.1 µs | 28,454 |
| Build a filter from the bundled list | 13.3 ms | 75 |
| `find_matches`, clean short message | 90.4 µs | 11,059 |
| `find_matches`, message with three banned words | 240 µs | 4,169 |
| `censor`, short message with one banned word | 118 µs | 8,461 |
| `censor`, 60-word message with three banned words | 3.2 ms | 311 |
| A 132,000-character message | 210 ms | 5 |

Build the filter once and reuse it. The messages are the same as the .NET and JavaScript benchmarks';
run them with `uv run python bench/bench_filter.py -o .bench/results.json` in `python/`, and print the
table with `uv run python scripts/bench_table.py .bench/results.json`.

## Limitations

- **Unicode versions.** The filter uses the running Python's own Unicode data (`unicodedata`),
  through a layer that reproduces .NET's rules for whitespace, case and categories. CPython 3.14 and
  .NET 10 agree on the category of every code point. Older versions differ only on characters assigned
  in Unicode 15 or later, and Python 3.11 also does not fold the 62 Cyrillic modifier letters
  U+1E030–U+1E06D in compatibility normalization. The conformance corpus contains none of these
  characters; if a difference matters to you, please open an issue.
- **Values that are not text.** `bytes`, numbers and other objects raise `TypeError` instead of being
  read as an empty or converted message; see [Validating input](#validating-input).
- **Interpreters.** CPython 3.11 to 3.14, including free-threaded 3.14, is tested. Other interpreters,
  such as PyPy, may work but are not tested.
- The evasions the filter reads through, and what it deliberately does not catch, are listed in the
  [project README](https://github.com/AmirehsanK/PersianTextGuard#what-it-reads-through).

## Links

- [Project README](https://github.com/AmirehsanK/PersianTextGuard#readme)
- [Conformance corpus](https://github.com/AmirehsanK/PersianTextGuard/tree/main/conformance)
- [Word lists](https://github.com/AmirehsanK/PersianTextGuard/tree/main/wordlists)
- [.NET package on NuGet](https://www.nuget.org/packages/PersianTextGuard) and
  [JavaScript package on npm](https://www.npmjs.com/package/persian-text-guard)
- [MIT licence](https://github.com/AmirehsanK/PersianTextGuard/blob/main/LICENSE)

---

<div dir="rtl">

## فارسی

</div>

<div dir="rtl">

این بسته نسخهٔ پایتونِ پروژهٔ PersianTextGuard است: یکسان‌سازی متن فارسی و فیلتر کلمات نامناسب که ترفندهای دور زدن فهرست کلمات را می‌شناسد، مثل «ک.ی.ر»، حروف فاصله‌دار، حروف عربی به‌جای فارسی و نویسه‌های نامرئی، و در عین حال پیام‌های عادی مثل «هر کس» و «تخم مرغ» را رد نمی‌کند.

</div>

<div dir="rtl">

برای هر پیام همان پاسخی را می‌دهد که بسته‌های دات‌نت و جاوااسکریپت می‌دهند، و جایگاه‌ها را مثل خودِ پایتون می‌شمارد.

</div>

<div dir="rtl">

### نصب

</div>

```bash
pip install persian-text-guard
```

<div dir="rtl">

### شروع سریع

</div>

<div dir="rtl">

فیلتر را یک بار بسازید و همه‌جا از همان استفاده کنید؛ فیلتر بعد از ساخته شدن هرگز تغییر نمی‌کند و استفادهٔ هم‌زمان از چند نخ هم امن است.

</div>

```python
from persian_text_guard import ProfanityFilter, WordList

# فیلتر را یک بار بسازید و به اشتراک بگذارید.
# Build once and share it: a filter never changes after it is built.
filter = ProfanityFilter(WordList.persian_default())

assert not filter.contains_profanity("سلام، سفارشم کی میرسه؟")
assert filter.contains_profanity("ک.ی.ر")
```

<div dir="rtl">

### سانسور

</div>

<div dir="rtl">

هر کلمهٔ نامناسب، با پسوندها و فاصله‌هایش، با چهار نویسهٔ ثابت پوشانده می‌شود و بقیهٔ متن دست نمی‌خورد.

</div>

```python
from persian_text_guard import ProfanityFilter, WordList

# کلمات نامناسب با چهار نویسه پوشانده می‌شوند.
# Every banned word is hidden behind a four-character mask.
filter = ProfanityFilter(WordList.persian_default())

assert filter.censor("جنده\N{ZERO WIDTH NON-JOINER}ها رو ببین") == "**** رو ببین"
assert filter.censor("this is kir", "#") == "this is ####"
```

<div dir="rtl">

### بررسی ورودی

</div>

<div dir="rtl">

هر رشته‌ای پذیرفته می‌شود و مقدار خالیِ پایتون مثل پیام خالی است، اما هر مقدار دیگری، مثل عدد یا بایت یا فهرست، خطای نوع می‌دهد. پیش از فیلتر کردنِ داده‌های درخواست، بررسی کنید که پیام از نوع رشته باشد.

</div>

<div dir="rtl">

مستندات کامل، فهرست ترفندهایی که شناخته می‌شوند و محدودیت‌ها در [راهنمای اصلی پروژه](https://github.com/AmirehsanK/PersianTextGuard#readme) آمده است.

</div>
