# persian-text-guard

Persian text normalization and evasion-resistant profanity filtering for Rust.

[فارسی](#فارسی) · [Full project README](https://github.com/AmirehsanK/PersianTextGuard#readme) · [.NET package](https://www.nuget.org/packages/PersianTextGuard) · [npm package](https://www.npmjs.com/package/persian-text-guard) · [PyPI package](https://pypi.org/project/persian-text-guard/)

A word list is easy to get around. Type an Arabic `ي` instead of `ی`, slip a zero-width character inside
a word, write `f u c k`, `sh1t`, `کیییییر` or `k0s`, and a plain `text.contains(word)` check sees nothing.
`persian-text-guard` reads through those tricks, and just as importantly, it does not flag ordinary
messages: `هر کس`, `تخم مرغ`, `class`, `push it` and `Scunthorpe` all pass.

It is the Rust port of [PersianTextGuard](https://github.com/AmirehsanK/PersianTextGuard), and gives the
same answers as the .NET, JavaScript and Python packages for every case in the shared
[conformance corpus](https://github.com/AmirehsanK/PersianTextGuard/tree/main/conformance).

- One dependency, `unicode-normalization`; `#![forbid(unsafe_code)]`; no panics, for any input.
- `Send + Sync`: build one filter and share it across threads without a lock.
- Positions in bytes, so a match slices the string you passed.
- Byte versions for input that may not be UTF-8.

## Installation

```bash
cargo add persian-text-guard
```

The minimum supported Rust version is 1.85 (edition 2024).

## Quick start

```rust
use persian_text_guard::{ProfanityFilter, WordList};

// Build once and share it: a filter never changes after it is built.
let filter = ProfanityFilter::with_defaults(WordList::persian_default());

assert!(!filter.contains_profanity("سلام، سفارشم کی میرسه؟"));
assert!(filter.contains_profanity("ک.ی.ر"));
assert!(filter.contains_profanity("f u c k"));
```

### What matched, and why

```rust
use persian_text_guard::{EvasionKind, ProfanityFilter, WordCategory, WordList};

let filter = ProfanityFilter::with_defaults(WordList::persian_default());
let found = filter.find_match("sh1iiit").unwrap();

assert_eq!(found.word.text, "shit");
assert_eq!(found.word.category, WordCategory::Profanity);
assert!(found.evasion.contains(EvasionKind::LookalikeCharacters));
assert!(found.evasion.contains(EvasionKind::RepeatedLetters));
```

### Positions are bytes, and slice your string

`start` and `len` are byte offsets into the message exactly as you passed it, always on character
boundaries, so `&text[m.range()]` is the region. Matches are ordered by position and never overlap, and
every region covers whole words.

```rust
use persian_text_guard::{ProfanityFilter, WordList};

let filter = ProfanityFilter::with_defaults(WordList::persian_default());
let text = "😀 کیر and f u c k";
let matches = filter.find_matches(text);

assert_eq!(matches.len(), 2);
assert_eq!((matches[0].start, matches[0].len), (5, 6));
assert_eq!(&text[matches[0].range()], "کیر");
// A word split with spaces is hidden with its separators.
assert_eq!(&text[matches[1].range()], "f u c k");
```

### Censoring, and masks

Every banned word, with its suffixes and separators, is replaced by four mask characters, so the mask
says nothing about the word. Everything outside a region is returned byte for byte.

```rust
use persian_text_guard::{InvalidMask, ProfanityFilter, WordList};

let filter = ProfanityFilter::with_defaults(WordList::persian_default());

assert_eq!(filter.censor("kir and motherfucker"), "**** and ****");
assert_eq!(filter.censor_with("this is kir", '#').unwrap(), "this is ####");
assert_eq!(filter.censor_with("this is kir", '■').unwrap(), "this is ■■■■");

// The mask is checked before the text: it must not be a letter, a digit, whitespace, a control
// character or a character above U+FFFF.
assert_eq!(filter.censor_with("", 'x'), Err(InvalidMask { mask: 'x' }));
```

### Categories

The bundled list is categorised, so you can block what your site cares about. `WordList::all()` includes
the `Mild` words (rude in context, ordinary otherwise); `WordList::persian_default()` leaves them out.

```rust
use persian_text_guard::{ProfanityFilter, WordCategory, WordList};

// A dating app: sexual words are fine, abuse is not.
let words = WordList::bundled(&[WordCategory::Insult, WordCategory::Slur, WordCategory::Harassment]);
let filter = ProfanityFilter::with_defaults(words);

assert!(filter.contains_profanity("کسکش"));
assert!(!filter.contains_profanity("کیر"));
```

### Your own words

```rust
use persian_text_guard::{BannedWord, ProfanityFilter, WordList, WordMatchMode};

let mut words: Vec<BannedWord> = WordList::persian_default().to_vec();
words.push(BannedWord::new("اسپم"));
// Anywhere: inside longer words too.
words.push(BannedWord::new("casino").with_mode(WordMatchMode::Anywhere));

let filter = ProfanityFilter::with_defaults(&words);
assert!(filter.contains_profanity("onlinecasino.example"));
assert!(filter.contains_profanity("این اسپم است"));
```

### Word-list files

One entry per line; `~` matches anywhere; `#` starts a comment; `[category]` starts a section. Read a
file with [`WordList::load`], any reader with [`WordList::load_reader`], or text you already have with
[`WordList::parse`]. A UTF-8 byte-order mark and `\r\n` line endings are fine.

```rust
use persian_text_guard::{ProfanityFilter, WordCategory, WordList, WordMatchMode};

// A line starting with # is a comment, ~ matches anywhere, [category] starts a section.
let text = "# my own list\nspam\n[insult]\n~scam\n";

let words = WordList::parse(text).unwrap();
assert_eq!(words.len(), 2);
assert_eq!(words[1].mode, WordMatchMode::Anywhere);
assert_eq!(words[1].category, WordCategory::Insult);

// WordList::load("my-words.txt")? and WordList::load_reader(file)? read the same format.
let from_reader = WordList::load_reader(text.as_bytes()).unwrap();
assert_eq!(from_reader, words);
let filter = ProfanityFilter::with_defaults(&words);
assert!(filter.contains_profanity("this is spam"));
```

### Options

Each evasion the filter reads through can be turned off.

```rust
use persian_text_guard::{ProfanityFilter, ProfanityFilterOptions, WordList};

let options = ProfanityFilterOptions::default()
    .squeeze_repeated_letters(false)
    .fold_lookalike_characters(false);
let filter = ProfanityFilter::new(WordList::persian_default(), options);

assert!(!filter.contains_profanity("sh1t"));
assert!(filter.contains_profanity("shit"));
```

### Normalizing and tokenizing

```rust
use persian_text_guard::{Normalization, NormalizationStep, normalize, to_persian_digits, tokenize};

let text = "كتاب\u{200C}هاي  ۱۲ ABC";

// "comparison": what the filter searches. Lossy on purpose — never display it.
assert_eq!(normalize(text, Normalization::COMPARISON), "کتابهای 12 abc");
// "standard": safe to store and show; keeps the zero-width non-joiner, Persian digits and case.
assert_eq!(normalize(text, Normalization::STANDARD), "کتاب\u{200C}های ۱۲ ABC");
// Or any combination of steps.
assert_eq!(normalize("ABC ي", NormalizationStep::LowerCase | NormalizationStep::UnifyLetters), "abc ی");

assert_eq!(tokenize("سلام، دنیا! خوبی؟"), ["سلام", "دنیا", "خوبی"]);
assert_eq!(to_persian_digits("2 ساعت پیش"), "۲ ساعت پیش");
```

### Byte versions

Text that arrives from a socket, a file or a database may not be valid UTF-8. The byte versions take
`&[u8]`: invalid sequences are read as U+FFFD, exactly as [`String::from_utf8_lossy`] reads them, and the
bytes you get back outside a censored region are the bytes you passed, invalid ones included.

```rust
use persian_text_guard::{ProfanityFilter, WordList};

let filter = ProfanityFilter::with_defaults(WordList::persian_default());
let message = b"kir \xFF fuck";

assert!(filter.contains_profanity_bytes(message));
assert_eq!(filter.find_matches_bytes(message).iter().map(|m| m.range()).collect::<Vec<_>>(), [0..3, 6..10]);
assert_eq!(filter.censor_bytes(message), b"**** \xFF ****");
```

### Matching on the enumerations

The enumerations, `BannedWord`, `ProfanityFilterOptions`, `ProfanityMatch` and `WordListError` are
`#[non_exhaustive]`, so a later release can add a category, an evasion, a step, a field or an error kind
without a major version. A `match` needs a wildcard arm.

```rust
use persian_text_guard::{ProfanityFilter, WordCategory, WordList};

let filter = ProfanityFilter::with_defaults(WordList::persian_default());
let found = filter.find_match("kir").unwrap();

let severity = match found.word.category {
    WordCategory::Slur | WordCategory::Harassment => "report",
    WordCategory::Mild => "allow",
    _ => "hide",
};
assert_eq!(severity, "hide");
```

### Threads

A filter never changes after it is built, so it is `Send + Sync`: share it by reference or in an `Arc`,
with no lock.

```rust
use std::thread;

use persian_text_guard::{ProfanityFilter, WordList};

let filter = ProfanityFilter::with_defaults(WordList::persian_default());
let messages = ["سلام، سفارشم کی میرسه؟", "ک.ی.ر", "f u c k"];

thread::scope(|scope| {
    for message in messages {
        scope.spawn(|| filter.contains_profanity(message));
    }
});
```

### Errors

| Error | From | When |
| --- | --- | --- |
| `InvalidMask` | `censor_with`, `censor_bytes_with` | The mask is a letter, a digit, whitespace, a control character, or above U+FFFF. Checked before the text. |
| `ParseNameError` | `FromStr` on the enumerations and `Normalization` | An unknown name; the message lists the valid ones. |
| `WordListError::UnknownCategory { line, name }` | `parse`, `load`, `load_reader` | A section heading names a category that does not exist; `line` is 1-based. |
| `WordListError::Io` | `load`, `load_reader` | The file cannot be opened or read. |
| `WordListError::InvalidUtf8 { valid_up_to }` | `load`, `load_reader` | The input is not UTF-8. |

Nothing else fails. No function panics, for any message.

## Names across ports

| .NET | JavaScript/TypeScript | Python | Rust |
| --- | --- | --- | --- |
| `new ProfanityFilter(words, options)` | `new ProfanityFilter(words, options)` | `ProfanityFilter(words, options)` | `ProfanityFilter::new(words, options)` / `with_defaults(words)` |
| `filter.Count` | `filter.count` | `filter.count` | `filter.count()` |
| `filter.ContainsProfanity(text)` | `filter.containsProfanity(text)` | `filter.contains_profanity(text)` | `filter.contains_profanity(text)` |
| `filter.FindMatch(text)` | `filter.findMatch(text)` | `filter.find_match(text)` | `filter.find_match(text)` (`None` when there is no match) |
| `filter.FindMatches(text)` | `filter.findMatches(text)` | `filter.find_matches(text)` | `filter.find_matches(text)` (a `Vec`) |
| `filter.Censor(text, '#')` | `filter.censor(text, '#')` | `filter.censor(text, "#")` | `filter.censor_with(text, '#')?` (`filter.censor(text)` for `*`) |
| — | — | — | `contains_profanity_bytes`, `find_match_bytes`, `find_matches_bytes`, `censor_bytes`, `censor_bytes_with` |
| `new BannedWord("x", WordMatchMode.Anywhere) { Category = WordCategory.Slur }` | `{ text: 'x', mode: 'anywhere', category: 'slur' }` | `BannedWord("x", WordMatchMode.ANYWHERE, WordCategory.SLUR)` | `BannedWord::new("x").with_mode(WordMatchMode::Anywhere).with_category(WordCategory::Slur)` |
| `ProfanityFilterOptions { SqueezeRepeatedLetters = false }` | `{ squeezeRepeatedLetters: false }` | `ProfanityFilterOptions(squeeze_repeated_letters=False)` | `ProfanityFilterOptions::default().squeeze_repeated_letters(false)` |
| `match.Word`, `.Evasion`, `.Index`, `.Length` | `match.word`, `.evasion`, `.index`, `.length` | `match.word`, `.evasion`, `.index`, `.length` | `m.word`, `m.evasion`, `m.start`, `m.len`, `m.range()` (bytes) |
| `EvasionKind.LookalikeCharacters \| EvasionKind.RepeatedLetters` | `['repeatedLetters', 'lookalikeCharacters']` | `(EvasionKind.REPEATED_LETTERS, EvasionKind.LOOKALIKE_CHARACTERS)` | `EvasionSet` iterating `RepeatedLetters, LookalikeCharacters` |
| `WordList.All`, `WordList.PersianDefault` | `WordList.all`, `WordList.persianDefault` | `WordList.all()`, `WordList.persian_default()` | `WordList::all()`, `WordList::persian_default()` |
| `WordList.Bundled(WordCategory.Slur)` | `WordList.bundled('slur')` | `WordList.bundled("slur")` | `WordList::bundled(&[WordCategory::Slur])` |
| `WordList.Parse(text)`, `WordList.Load(stream)` | `WordList.parse(text)` | `WordList.parse(text)`, `WordList.load(path_or_file)` | `WordList::parse(text)?`, `WordList::load(path)?`, `WordList::load_reader(reader)?` |
| `PersianNormalizer.Normalize(text, PersianNormalization.Standard)` | `normalize(text, 'standard')` | `normalize(text, "standard")` | `normalize(text, Normalization::STANDARD)` |
| `PersianNormalizer.Tokenize(text)` | `tokenize(text)` | `tokenize(text)` | `tokenize(text)` (slices of `text`) |
| `PersianNormalizer.ToPersianDigits(text)` | `toPersianDigits(text)` | `to_persian_digits(text)` | `to_persian_digits(text)` |
| `FormatException` | `WordListFormatError` | `WordListFormatError` | `WordListError::UnknownCategory { line, name }` |
| `ArgumentException` (mask) | `RangeError` | `ValueError` | `Err(InvalidMask)` |

## Performance

rustc 1.98.1, Intel Core i7-9700K @ 3.60GHz, Criterion, release build.

| Operation | Mean | Operations/s |
| --- | ---: | ---: |
| Short clean message (5 words) | 3.8 µs | 262,929 |
| Long clean message (60 words) | 41.4 µs | 24,145 |
| Message with evasions | 3.6 µs | 280,985 |
| Normalize a long message | 11.7 µs | 85,746 |
| Build a filter from the bundled list | 1.4 ms | 734 |
| `find_matches`, clean short message | 3.8 µs | 266,191 |
| `find_matches`, message with three banned words | 9.3 µs | 107,110 |
| `censor`, short message with one banned word | 5.5 µs | 182,664 |
| `censor`, 60-word message with three banned words | 168 µs | 5,957 |
| A 132,000-character message | 7.5 ms | 134 |

Build the filter once, when your word list loads, and share it.

## Limitations

- **Unicode versions.** Categories and invariant lower-casing come from tables generated from .NET 10,
  so they do not change with the Rust compiler that builds the crate and agree with the other ports on
  every code point. Compatibility normalization (NFKC) and decomposition (NFD) come from
  `unicode-normalization`, whose Unicode version is newer than .NET 10's: they differ on 37 characters
  in NFKC and 20 in NFD, all assigned in Unicode 16 or 17. The conformance corpus contains none of them;
  if a difference matters to you, please open an issue.
- **No `no_std`.** The crate needs the standard library, and file loading needs a file system.
- The evasions the filter reads through, and what it deliberately does not catch, are listed in the
  [project README](https://github.com/AmirehsanK/PersianTextGuard#what-it-reads-through).

## Links

- [Project README](https://github.com/AmirehsanK/PersianTextGuard#readme)
- [API documentation](https://docs.rs/persian-text-guard)
- [Conformance corpus](https://github.com/AmirehsanK/PersianTextGuard/tree/main/conformance)
- [.NET package](https://www.nuget.org/packages/PersianTextGuard) · [npm package](https://www.npmjs.com/package/persian-text-guard) · [PyPI package](https://pypi.org/project/persian-text-guard/)

## License

MIT © Amirehsan Kohannasab

<div dir="rtl">

## فارسی

</div>

<div dir="rtl">

این بسته نسخهٔ راستِ پروژهٔ PersianTextGuard است: یکسان‌سازی متن فارسی و فیلتر کلمات نامناسب که ترفندهای دور زدن فهرست کلمات را می‌شناسد، مثل «ک.ی.ر»، حروف فاصله‌دار، حروف عربی به‌جای فارسی و نویسه‌های نامرئی، و در عین حال پیام‌های عادی مثل «هر کس» و «تخم مرغ» را رد نمی‌کند.

</div>

<div dir="rtl">

برای هر پیام همان پاسخی را می‌دهد که بسته‌های دات‌نت، جاوااسکریپت و پایتون می‌دهند، و جایگاه‌ها را با بایت‌های همان رشته‌ای می‌شمارد که به آن داده‌اید.

</div>

<div dir="rtl">

### نصب

</div>

```bash
cargo add persian-text-guard
```

<div dir="rtl">

کمینهٔ نسخهٔ پشتیبانی‌شدهٔ راست ۱.۸۵ است.

</div>

<div dir="rtl">

### شروع سریع

</div>

<div dir="rtl">

فیلتر را یک بار بسازید و همه‌جا از همان استفاده کنید؛ فیلتر بعد از ساخته شدن هرگز تغییر نمی‌کند و استفادهٔ هم‌زمان از چند نخ هم امن است.

</div>

```rust
use persian_text_guard::{ProfanityFilter, WordList};

// فیلتر را یک بار بسازید و به اشتراک بگذارید.
// Build once and share it: a filter never changes after it is built.
let filter = ProfanityFilter::with_defaults(WordList::persian_default());

assert!(!filter.contains_profanity("سلام، سفارشم کی میرسه؟"));
assert!(filter.contains_profanity("ک.ی.ر"));
```

<div dir="rtl">

### سانسور

</div>

<div dir="rtl">

هر کلمهٔ نامناسب، با پسوندها و فاصله‌هایش، با چهار نویسهٔ ثابت پوشانده می‌شود و بقیهٔ متن دست نمی‌خورد.

</div>

```rust
use persian_text_guard::{ProfanityFilter, WordList};

let filter = ProfanityFilter::with_defaults(WordList::persian_default());

// نویسهٔ پوشاننده را خودتان انتخاب کنید.
// Choose the mask character yourself.
assert_eq!(filter.censor("kir and motherfucker"), "**** and ****");
assert_eq!(filter.censor_with("این کیر است", '#').unwrap(), "این #### است");
```
