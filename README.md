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
// match.Word          -> BannedWord { Text = "shit", Mode = WholeWord }
// match.Word.Category -> WordCategory.Profanity
// match.Evasion       -> LookalikeCharacters | RepeatedLetters
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
| Symbols masking letters | `f**k`, `c*nt`, `f@ck`, `a$$hole` |
| Punctuation inside a word | `ج.نده`, `kos_kesh`, `bi-namoos` |
| A word split once | `fu ck`, `کی ر` |
| Accents and letters from other blocks | `fück`, `shíť`, `ƒuck`, `🅵🆄🅲🅺` |
| Emoji beside or inside a word | `کیر😂`, `f🖕ck` |
| Cyrillic and Greek look-alikes | `bitсh` with a Cyrillic `с` |

Whole-word entries never match across ordinary words. `push it` contains `shit` once the
space is removed, so words are only joined when the result is exactly an entry, and digits
are only read as letters in a run that has letters in it — `455` and `۴۵۵ تومان` are
numbers, `4ss` is a word.

### Every match, and censoring

`FindMatches` returns every banned word in a message, in order, each with where it is in the text
you passed — ready to log, to highlight for a moderator, or to decide on by the most severe
category present:

```csharp
foreach (var match in filter.FindMatches("sh1t and f u c k"))
{
    Console.WriteLine($"{match.Word.Text} ({match.Word.Category}) at {match.Index}, length {match.Length}");
}
// shit (Profanity) at 0, length 4
// fuck (Profanity) at 9, length 7
```

`Censor` returns the message with each banned word hidden and everything else untouched:

```csharp
filter.Censor("kir and motherfucker");   // "**** and ****"
filter.Censor("جنده‌ها رو ببین");         // "**** رو ببین"
filter.Censor("this is kir", '#');       // "this is ####"
```

- **Whole words are hidden.** A banned word inside a longer word or with a Persian suffix takes the
  whole word with it, so no fragment gives it away. A disguised word such as `f u c k` or `ج.نده`
  is hidden together with its separators.
- **Every mask is four characters,** whatever it hides, so a reader cannot tell a short word from
  a long one. Censored text is therefore usually a different length from the message; positions
  from `FindMatches` always refer to the original.
- **The output is always clean.** Checking a censored message with the same filter finds nothing.
- **Clean text comes back as the same string**, and nothing outside a hidden word is normalized.
- The mask character can be any symbol or punctuation; letters, digits, whitespace and control
  characters are refused.

`FindMatch` also reports `Index` and `Length`, for when the first match is all you need.

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

About 1,250 Persian, Finglish and English entries, in three files that document what is
deliberately left out and why — `کس` also means "person", `ساک` is a bag, `shit` is in
shiitake, and ethnic names are not slurs:
[persian.txt](wordlists/persian.txt),
[finglish.txt](wordlists/finglish.txt),
[english.txt](wordlists/english.txt). The list is **opt-in**: nothing
uses it unless you pass it to a filter.

Every entry has a `WordCategory`, so you can block what your community needs blocked:

```csharp
WordList.PersianDefault   // everything except Mild — the sane default
WordList.All              // Mild too: آشغال, دلقک, damn, crap, boobs
WordList.Bundled(WordCategory.Slur, WordCategory.Harassment)

filter.FindMatch("nigger")!.Word.Category;   // WordCategory.Slur
```

| Category | What it holds |
| --- | --- |
| `Profanity` | General swearing: `fuck`, `shit`, `گوه`, `ریدم` |
| `Sexual` | Genitals, sex acts, pornography: `کیر`, `سکس`, `cock`, `blowjob` |
| `Insult` | Strong insults, and the family and honour insults Persian is built on: `کسکش`, `مادرجنده`, `بی‌ناموس`, `bastard` |
| `Slur` | Hate speech: race, ethnicity, religion, orientation, gender, disability |
| `Harassment` | `kys`, `kill yourself`, `خفه شو`, `سیکتیر` |
| `Mild` | Rude in context, ordinary otherwise. **Not** in `PersianDefault` |

Persian suffixes are handled by the matcher, not by the list, so `جنده‌ها`, `کیرتون`,
`کونیا` and `حرامزاده‌ای` match without entries of their own.

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

Against `WordList.PersianDefault` (about 1,000 entries), on .NET 10 (Intel Core i7-9700K,
BenchmarkDotNet):

| Operation | Mean | Allocated |
| --- | ---: | ---: |
| Short clean message (5 words) | 2.4 µs | 2.7 KB |
| Long clean message (60 words) | 21.3 µs | 21.9 KB |
| Message with evasions | 1.9 µs | 3.1 KB |
| Normalize a long message | 5.9 µs | 3.3 KB |
| Build a filter from the bundled list | 553 µs | 1,061 KB |
| `FindMatches`, clean short message | 2.4 µs | 2.7 KB |
| `FindMatches`, message with three banned words | 6.6 µs | 9.7 KB |
| `Censor`, short message with one banned word | 4.5 µs | 6.4 KB |
| `Censor`, 60-word message with three banned words | 93.0 µs | 97.3 KB |

Whole-word entries are looked up by token rather than searched for one by one, so checking a
message barely notices how long the list is. Positions are only worked out for a message that
contains a banned word, so `FindMatches` and `Censor` cost the same as `ContainsProfanity` on
clean text.

Build the filter once. Checking a message is cheap enough to run on every chat message or
form submission.

```bash
dotnet run -c Release --project dotnet/benchmarks/PersianTextGuard.Benchmarks -f net10.0
```

## Development

The repository is laid out so that ports to other languages can sit next to the .NET one and share
the same word lists, version and behaviour:

```text
/
├── VERSION                         # the only version value, e.g. "1.2.0"
├── wordlists/
│   ├── persian.txt
│   ├── finglish.txt
│   └── english.txt
├── conformance/                    # the behaviour every port must pass
├── dotnet/
│   ├── PersianTextGuard.slnx
│   ├── Directory.Build.props
│   ├── src/PersianTextGuard/
│   ├── tests/PersianTextGuard.Tests/
│   ├── tests/PersianTextGuard.Conformance/
│   ├── benchmarks/PersianTextGuard.Benchmarks/
│   └── tools/PersianTextGuard.CorpusFill/
├── README.md  LICENSE  THIRD-PARTY-NOTICES.md  icon.png
├── .github/workflows/ci.yml
└── .specify/  .claude/  specs/
```

[`conformance/`](conformance/README.md) is the specification: a language-neutral set of cases, with
exact expected results, that every port must pass. A change in behaviour is a change to the corpus,
made on purpose in the same pull request.

Every command runs from the repository root:

| Purpose | Command |
| --- | --- |
| Build everything | `dotnet build dotnet/PersianTextGuard.slnx` |
| Existing tests | `dotnet test dotnet/tests/PersianTextGuard.Tests` |
| Conformance corpus | `dotnet test dotnet/tests/PersianTextGuard.Conformance` |
| Benchmarks | `dotnet run -c Release --project dotnet/benchmarks/PersianTextGuard.Benchmarks -f net10.0` |
| Pack | `dotnet pack dotnet/src/PersianTextGuard -c Release -o artifacts` |
| Fill in pending corpus cases | `dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill` |

## Limitations

- It finds words and phrases; it does not understand meaning. Insults made of ordinary
  words, sarcasm and threats need human moderation or a classifier.
- Finglish has no fixed spelling. The bundled list covers common spellings; add the ones
  your community uses.
- `Censor` hides a disguised word spread over several lines (`f`, `u`, `c`, `k` on separate
  lines) together with its line breaks, so the censored message has fewer lines. Keeping them would
  show how many letters were hidden.
- A word split in the middle stays hidden unless the halves join into exactly an entry, so
  `fu ck` is caught but `fuc k` is not, and two ordinary Persian words are never glued
  together (`هر کس ده تا` is not `کسده`).
- Transposed letters (`fcuk`) and a letter typed as a different letter (`cvnt`) are
  spellings, not typography: add the ones your community uses.

## Background

This started as the moderation layer of a Persian gaming community site, where every one
of these evasions showed up in real posts. It was extracted into a package because
Persian text needs this handling everywhere, and existing .NET profanity filters are
built for English.

## License

MIT. The bundled word list is curated from several open lists; see
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
