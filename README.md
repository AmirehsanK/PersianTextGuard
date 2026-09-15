<img src="https://raw.githubusercontent.com/AmirehsanK/PersianTextGuard/main/icon.png" alt="" width="96" align="right">

# PersianTextGuard

[![CI](https://github.com/AmirehsanK/PersianTextGuard/actions/workflows/ci.yml/badge.svg)](https://github.com/AmirehsanK/PersianTextGuard/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/PersianTextGuard.svg)](https://www.nuget.org/packages/PersianTextGuard)
[![Downloads](https://img.shields.io/nuget/dt/PersianTextGuard.svg)](https://www.nuget.org/packages/PersianTextGuard)

Persian text normalization and evasion-resistant profanity filtering for .NET.

A word list is easy to get around. Type an Arabic `ي` instead of `ی`, slip a zero-width
character inside a word, write `f u c k`, `sh1t`, `کیییییر` or `k0s`, and a plain
`text.Contains(word)` check sees nothing. PersianTextGuard reads through those tricks,
and just as importantly, it doesn't flag ordinary messages: `هر کس`, `تخم مرغ`, `class`,
`push it` and `Scunthorpe` all pass.

```bash
dotnet add package PersianTextGuard
```

Targets .NET Standard 2.0 (so .NET Framework 4.6.1+ works), .NET 8 and .NET 10, and the
tests run on .NET Framework 4.8, .NET 8 and .NET 10. No dependencies.

## Profanity filtering

```csharp
using PersianTextGuard;

// Build once (for example as a singleton) and share it: it is immutable and thread-safe.
var filter = new ProfanityFilter(WordList.PersianDefault);

filter.ContainsProfanity("سلام، سفارشم کی میرسه؟");   // false
filter.ContainsProfanity("ک.ی.ر");                     // true
filter.ContainsProfanity("f u c k");                   // true

var match = filter.FindMatch("sh1iiit");
// match.Word    -> BannedWord { Text = "shit", Mode = Anywhere }
// match.Evasion -> LookalikeCharacters | RepeatedLetters
```

### What it reads through

| Evasion | Examples |
| --- | --- |
| Arabic keyboard letters | `كسكش` (Arabic kaf), `بازي` (Arabic yeh) |
| Look-alike letters from Urdu, Kurdish, Pashto | `ڪیر` (swash kaf), `ھ`, `ې` |
| Invisible characters | zero-width non-joiner, zero-width space, word joiner, soft hyphen, bidi marks |
| Tatweel and diacritics | `کــیــر`, `کِیر` |
| Spaced or dotted letters | `f u c k`, `f.u.c.k`, `ک ی ر` |
| Held keys | `fuuuuck`, `کیییییر` |
| Digits and symbols for letters | `sh1t`, `$hit`, `sh!t`, `k0s` |
| Filler inside a word | `f*ck`, `ک*ی*ر` |
| Cyrillic and Greek look-alikes | `bitсh` with a Cyrillic `с` |

Whole-word entries never match across ordinary words. `push it` contains `shit` once the
space is removed, so only runs of single letters are joined.

### Your own words

```csharp
var filter = new ProfanityFilter(
[
    new BannedWord("اسپم"),                               // whole word (the default)
    new BannedWord("casino", WordMatchMode.Anywhere),     // inside longer words too
]);

// Or combine your entries with the bundled list:
var combined = new ProfanityFilter(WordList.PersianDefault.Concat(myWords));
```

Entries are normalized when the filter is built, so `كص`, `کص` and `ک‌ص` are the same entry.

Word lists can also be kept in a text file, one entry per line:

```text
# comments start with #
whole word or phrase
~matched-anywhere
```

```csharp
var words = WordList.Load(File.OpenRead("banned-words.txt"));
```

### The bundled list

`WordList.PersianDefault` has about 400 Persian, Finglish and English entries. It is
**opt-in**: nothing uses it unless you pass it to a filter. The
[file](src/PersianTextGuard/WordLists/persian-default.txt) documents what is deliberately
left out and why. For example, `کس` also means "person", and ethnic names are not slurs.

### Options

Every evasion can be turned off:

```csharp
var strict = new ProfanityFilter(words, new ProfanityFilterOptions
{
    SqueezeRepeatedLetters = true,
    FoldLookalikeCharacters = true,
    JoinSpacedLetters = false,
});
```

## Normalization

```csharp
// Comparison (the default): everything folded. For searching, deduplicating and matching.
PersianNormalizer.Normalize("كتاب‌هاي  ۱۲ ABC");
// "کتابهای 12 abc"

// Standard: safe for text you store and show. Fixes Arabic keyboard letters and strips
// invisible junk, but keeps the zero-width non-joiner, Persian digits and case.
PersianNormalizer.Normalize("كتاب‌هاي  ۱۲ ABC", PersianNormalization.Standard);
// "کتاب‌های ۱۲ ABC"

// Or pick steps yourself.
PersianNormalizer.Normalize(text, PersianNormalization.UnifyLetters | PersianNormalization.AsciiDigits);

PersianNormalizer.ToPersianDigits("2 ساعت پیش");   // "۲ ساعت پیش"
PersianNormalizer.ToAsciiDigits("۱۴۰۴/۰۵/۱۴");     // "1404/05/14"
PersianNormalizer.Tokenize("سلام، دنیا! خوبی؟");   // ["سلام", "دنیا", "خوبی"]
```

A common use is normalizing user names, product titles or search queries with `Standard`
before saving them, so a search for `کتاب` also finds `كتاب`.

## Performance

Against the bundled list of about 400 entries, on .NET 10 (Intel Core i7-9700K, BenchmarkDotNet):

| Operation | Mean | Allocated |
| --- | ---: | ---: |
| Short clean message (5 words) | 4.9 µs | 2.3 KB |
| Long clean message (60 words) | 33.8 µs | 16.3 KB |
| Message with evasions | 3.1 µs | 2.3 KB |
| Normalize a long message | 5.2 µs | 3.3 KB |
| Build a filter from the bundled list | 183 µs | 393 KB |

Build the filter once. Checking a message is cheap enough to run on every chat message or
form submission.

```bash
dotnet run -c Release --project benchmarks/PersianTextGuard.Benchmarks
```

## Limitations

- It finds words and phrases; it does not understand meaning. Insults made of ordinary
  words, sarcasm and threats need human moderation or a classifier.
- Finglish has no fixed spelling. The bundled list covers common spellings; add the ones
  your community uses.
- `FindMatch` reports the first match, not every match or its position.

## Background

This started as the moderation layer of a Persian gaming community site, where every one
of these evasions showed up in real posts. It was extracted into a package because
Persian text needs this handling everywhere, and existing .NET profanity filters are
built for English.

## License

MIT. The bundled word list is curated from several open lists; see
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
