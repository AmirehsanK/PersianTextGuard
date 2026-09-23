<img src="https://raw.githubusercontent.com/AmirehsanK/PersianTextGuard/main/icon.png" alt="" width="96" align="right">

# PersianTextGuard

[![CI](https://github.com/AmirehsanK/PersianTextGuard/actions/workflows/ci.yml/badge.svg)](https://github.com/AmirehsanK/PersianTextGuard/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue)](https://github.com/AmirehsanK/PersianTextGuard/blob/main/LICENSE)

[![NuGet](https://img.shields.io/nuget/v/PersianTextGuard?logo=nuget&label=nuget)](https://www.nuget.org/packages/PersianTextGuard)
[![npm](https://img.shields.io/npm/v/persian-text-guard?logo=npm&label=npm)](https://www.npmjs.com/package/persian-text-guard)
[![PyPI](https://img.shields.io/pypi/v/persian-text-guard?logo=pypi&logoColor=white&label=pypi)](https://pypi.org/project/persian-text-guard/)
[![crates.io](https://img.shields.io/crates/v/persian-text-guard?logo=rust&label=crates.io)](https://crates.io/crates/persian-text-guard)

[![NuGet downloads](https://img.shields.io/nuget/dt/PersianTextGuard?label=nuget%20downloads)](https://www.nuget.org/packages/PersianTextGuard)
[![npm downloads](https://img.shields.io/npm/dm/persian-text-guard?label=npm%20downloads)](https://www.npmjs.com/package/persian-text-guard)
[![PyPI downloads](https://static.pepy.tech/personalized-badge/persian-text-guard?period=total&units=international_system&left_color=grey&right_color=blue&left_text=pypi%20downloads)](https://pepy.tech/project/persian-text-guard)
[![crates.io downloads](https://img.shields.io/crates/d/persian-text-guard?label=crates.io%20downloads)](https://crates.io/crates/persian-text-guard)

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

For JavaScript and TypeScript, the same filter is on npm, with types included and the same answers
for every message ([JavaScript README](js/README.md)):

```bash
npm install persian-text-guard
```

For Python 3.11 and later, including free-threaded 3.14, the same filter is on PyPI, typed, with no
dependencies and the same answers for every message ([Python README](python/README.md)):

```bash
pip install persian-text-guard
```

```python
from persian_text_guard import ProfanityFilter, WordList

filter = ProfanityFilter(WordList.persian_default())
assert filter.contains_profanity("ک.ی.ر")
assert filter.censor("kir and motherfucker") == "**** and ****"
```

For Rust, the same filter is on crates.io, with one dependency, no `unsafe`, and the same answers for
every message ([Rust README](rust/README.md)):

```bash
cargo add persian-text-guard
```

```rust
use persian_text_guard::{ProfanityFilter, WordList};

let filter = ProfanityFilter::with_defaults(WordList::persian_default());
assert!(filter.contains_profanity("ک.ی.ر"));
assert_eq!(filter.censor("kir and motherfucker"), "**** and ****");
```

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
| Short clean message (5 words) | 2.5 µs | 2.7 KB |
| Long clean message (60 words) | 23.0 µs | 21.9 KB |
| Message with evasions | 1.9 µs | 3.1 KB |
| Normalize a long message | 6.0 µs | 3.3 KB |
| Build a filter from the bundled list | 600 µs | 1,061 KB |
| `FindMatches`, clean short message | 2.9 µs | 2.7 KB |
| `FindMatches`, message with three banned words | 7.8 µs | 9.7 KB |
| `Censor`, short message with one banned word | 4.7 µs | 6.4 KB |
| `Censor`, 60-word message with three banned words | 99.5 µs | 97.3 KB |

Whole-word entries are looked up by token rather than searched for one by one, so checking a
message barely notices how long the list is. Positions are only worked out for a message that
contains a banned word, so `FindMatches` and `Censor` cost the same as `ContainsProfanity` on
clean text.

Build the filter once. Checking a message is cheap enough to run on every chat message or
form submission.

```bash
dotnet run -c Release --project dotnet/benchmarks/PersianTextGuard.Benchmarks -f net10.0
```

## Changes in 1.5.0

- **Rust.** `persian-text-guard` is now on crates.io, for Rust 1.85 and later, released together with
  the .NET, npm and PyPI packages at the same version. It adds byte versions of checking, finding
  matches and censoring, for input that may not be UTF-8.
- The conformance corpus runner rules gained two readings for ports whose strings cannot hold a lone
  surrogate or have no missing string value, so the Rust runner runs every case and reports none as not
  applicable. No case changed.
- .NET, JavaScript and Python behaviour is unchanged: 1.5.0 of each is 1.4.0 with a new version number.

## Changes in 1.4.0

- **Python.** `persian-text-guard` is now on PyPI, for CPython 3.11 and later, released together with
  the .NET and JavaScript packages at the same version.
- The conformance corpus gained cases for characters outside the Basic Multilingual Plane, such as
  emoji and CJK Extension B letters next to or inside a word. They record what every port already did.
- .NET and JavaScript behaviour is unchanged: 1.4.0 of both is 1.3.0 with a new version number.

## Changes in 1.3.0 for .NET users

- A message containing a Unicode noncharacter (such as U+FFFE) no longer throws. 1.2.0 threw
  `ArgumentException` for U+FFFE on .NET 8 and .NET 10, and for every noncharacter on .NET Framework 4.8.
- A section heading in a word-list file must be a category name, such as `[insult]` in any letter
  case. A number such as `[3]`, which 1.2.0 accepted as the category with that internal number, is
  now reported as an unknown category.

Nothing else changes for .NET users. See the
[release notes](https://github.com/AmirehsanK/PersianTextGuard/releases).

## Development

The repository is laid out so that ports to other languages can sit next to the .NET one and share
the same word lists, version and behaviour:

```text
/
├── VERSION                         # the only version value, e.g. "1.4.0"
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
├── js/                             # the npm package persian-text-guard
│   ├── src/  test/  bench/  consumers/  scripts/
│   └── etc/persian-text-guard.api.md
├── python/                         # the PyPI package persian-text-guard
│   ├── pyproject.toml  hatch_build.py  uv.lock
│   └── src/persian_text_guard/  tests/  bench/  consumers/  scripts/
├── rust/                           # the crates.io crate persian-text-guard
│   ├── Cargo.toml  Cargo.lock  build.rs  README.md
│   └── src/  tests/  bench/  consumer/  tools/  scripts/
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

For the JavaScript port, run these in `js/` (Node.js 22 or later):

| Purpose | Command |
| --- | --- |
| Install the development tools | `npm ci` |
| Build | `npm run build` |
| Lint and type check | `npm run lint` |
| Unit tests | `npm test` |
| Conformance corpus | `npm run corpus` |
| README examples | `npm run test:readme` |
| Benchmarks | `npm run bench` |
| Pack | `npm run pack` |
| API report and compatibility | `npm run api`, `npm run api:compat` |

For the Python port, run these in `python/` with [uv](https://docs.astral.sh/uv/):

| Purpose | Command |
| --- | --- |
| Install the development tools | `uv sync --locked` (add `--group package` for the package checks) |
| Lint and type check | `uv run ruff check && uv run ruff format --check && uv run python scripts/check_unicode_usage.py && uv run mypy` |
| Unit tests | `uv run pytest -m "not corpus"` |
| Conformance corpus | `uv run pytest -m corpus` |
| All tests, README examples included | `uv run pytest` |
| Another Python | `uv run --python 3.11 pytest` (also 3.12, 3.13, `3.14+gil` and, with `PYTHON_GIL=0`, 3.14t) |
| Build | `uv build` |
| Package and consumer checks | `uv run python scripts/check_package.py`, `uv run python scripts/check_consumers.py` |
| API compatibility | `uv run python scripts/check_api.py` |
| Benchmarks | `uv run python bench/bench_filter.py -o .bench/results.json`, then `uv run python scripts/bench_table.py .bench/results.json` |

For the Rust port, run these in `rust/` (Rust 1.85 or later):

| Purpose | Command |
| --- | --- |
| All tests, corpus and README examples included | `cargo test --locked` |
| Conformance corpus only | `cargo test --locked --test corpus` (one trial per case; filter with `-- <id>`) |
| The minimum supported Rust version | `cargo +1.85 test --locked` |
| Lint and format | `cargo fmt --check && cargo clippy --locked --all-targets -- -D warnings` |
| Documentation | `RUSTDOCFLAGS="-D warnings" cargo doc --locked --no-deps` |
| Regenerate the Unicode tables (.NET 10) | `dotnet run tools/gen_tables.cs -- src/tables.rs` |
| Set the version from `VERSION` | `scripts/set-version.sh` |
| Package and consumer checks | `scripts/prepare-package.sh && scripts/check-package.sh` |
| API compatibility | `scripts/check-api.sh` |
| Benchmarks | `cd bench && cargo bench`, then `cargo run --release --bin bench_table` |

### Releasing

Every package is released together, at the version in `VERSION`:

1. Set `VERSION` in the pull request, and merge it once CI is green for every port.
2. Tag the merge commit `vX.Y.Z`, matching `VERSION`, and push the tag.
3. CI publishes `PersianTextGuard` to NuGet and `persian-text-guard` to npm, PyPI and crates.io, only
   when every build and test job for every port is green and the tag matches `VERSION`. The npm package
   is published with provenance, and the PyPI files with attestations, from this workflow. PyPI and
   crates.io use trusted publishing through the `pypi` and `crates-io` GitHub environments, which only
   `v*` tags may deploy to; no token is stored.
   The first crates.io release is the exception: trusted publishing can only be configured on a crate
   that exists, so 1.5.0 is published with a crates.io token of scope publish-new, restricted to
   `persian-text-guard` and expiring within days, kept as the `crates-io` environment secret
   `CARGO_REGISTRY_TOKEN` and deleted and revoked right after the release.
4. If one registry's publish job fails after another succeeded, fix the cause and re-run the failed
   job: NuGet skips a version it already has, and so do the npm and PyPI jobs.

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
- The JavaScript package uses the JavaScript engine's Unicode data, so characters added in Unicode 16
  or later, and results in Firefox and Safari, can differ; see its
  [limitations](js/README.md#limitations).
- The Python package uses the running Python's Unicode data. CPython 3.14 agrees with .NET 10 on
  every character's category; older versions differ only on characters added in Unicode 15 or later,
  and Python 3.11 does not fold the Cyrillic modifier letters of Unicode 15. See its
  [limitations](python/README.md#limitations).
- The Rust crate carries category and case tables generated from .NET 10, so those never drift, and
  takes compatibility normalization from `unicode-normalization`, which follows a newer Unicode version
  than .NET 10: they differ on 37 characters in NFKC and 20 in NFD, all added in Unicode 16 or 17. See
  its [limitations](rust/README.md#limitations).

## Contributing

Contributions are welcome, and the most useful one takes a minute: **a real message that slipped through,
or an ordinary message that was wrongly flagged**. Paste the exact text into an issue and it becomes a
conformance case that all four ports must pass forever.

Also welcome: word-list entries (especially the Finglish spellings your community uses), bug fixes,
documentation and translation fixes, and new ports.

Two rules shape everything here:

1. **Ordinary messages must pass.** A false positive is treated as more severe than a missed evasion, so
   every new way of catching a word must also add the ordinary text it could plausibly catch.
2. **One behaviour, in every language.** [`conformance/`](conformance/README.md) is the specification;
   a change to matching behaviour updates the corpus *and* every port in the same pull request.

Taking part means following the [Code of Conduct](CODE_OF_CONDUCT.md).

See [CONTRIBUTING.md](CONTRIBUTING.md) for how to report a miss, how to change the word lists, how to add
a corpus case, what is deliberately out of scope (spam and link detection, emoji stripping, entries the
matcher already catches), and the pull-request checklist. The ground rules behind it all are in the
[constitution](.specify/memory/constitution.md).

## Background

This started as the moderation layer of a Persian gaming community site, where every one
of these evasions showed up in real posts. It was extracted into a package because
Persian text needs this handling everywhere, and existing .NET profanity filters are
built for English.

## License

MIT. The bundled word list is curated from several open lists; see
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
