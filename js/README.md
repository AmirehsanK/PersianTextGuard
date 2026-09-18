# persian-text-guard

Persian text normalization and evasion-resistant profanity filtering for JavaScript and TypeScript.

[فارسی](#فارسی) · [Full project README](https://github.com/AmirehsanK/PersianTextGuard#readme) · [.NET package](https://www.nuget.org/packages/PersianTextGuard)

A word list is easy to get around. Type an Arabic `ي` instead of `ی`, slip a zero-width character
inside a word, write `f u c k`, `sh1t`, `کیییییر` or `k0s`, and a plain `text.includes(word)` check
sees nothing. `persian-text-guard` reads through those tricks, and just as importantly, it doesn't
flag ordinary messages: `هر کس`, `تخم مرغ`, `class`, `push it` and `Scunthorpe` all pass.

It is the JavaScript port of [PersianTextGuard](https://github.com/AmirehsanK/PersianTextGuard), and
gives the same answers as the .NET package for every case in the shared
[conformance corpus](https://github.com/AmirehsanK/PersianTextGuard/tree/main/conformance).

- One package for JavaScript and TypeScript, with types included.
- Works with `import` and `require`, on Node.js 22 and later, and in browsers through a bundler.
- No dependencies.

## Installation

```bash
npm install persian-text-guard
```

## Quick start

```ts
import { ProfanityFilter, WordList } from 'persian-text-guard';

// Build once and share it: a filter never changes after it is built.
const filter = new ProfanityFilter(WordList.persianDefault);

console.log(filter.containsProfanity('سلام، سفارشم کی میرسه؟')); // → false
console.log(filter.containsProfanity('ک.ی.ر')); // → true
console.log(filter.containsProfanity('f u c k')); // → true
```

### What matched, and why

```ts
import { ProfanityFilter, WordList } from 'persian-text-guard';

const filter = new ProfanityFilter(WordList.persianDefault);
const match = filter.findMatch('sh1iiit');

console.log(match?.word.text); // → shit
console.log(match?.word.category); // → profanity
console.log(match?.evasion.join(', ')); // → repeatedLetters, lookalikeCharacters
```

### Every match, with its position

Positions are JavaScript string indexes (UTF-16 code units), so `slice` gives the matched text:

```ts
import { ProfanityFilter, WordList } from 'persian-text-guard';

const filter = new ProfanityFilter(WordList.persianDefault);
const message = 'sh1t and f u c k';

for (const match of filter.findMatches(message)) {
  const found = message.slice(match.index, match.index + match.length);
  console.log(`${match.word.text} (${match.word.category}) at ${match.index}, length ${match.length}: ${found}`);
}
// → shit (profanity) at 0, length 4: sh1t
// → fuck (profanity) at 9, length 7: f u c k
```

### Censoring

```ts
import { ProfanityFilter, WordList } from 'persian-text-guard';

const filter = new ProfanityFilter(WordList.persianDefault);

console.log(filter.censor('kir and motherfucker')); // → **** and ****
console.log(filter.censor('جنده‌ها رو ببین')); // → **** رو ببین
console.log(filter.censor('this is kir', '#')); // → this is ####
```

Whole words are hidden, every mask is four characters, and the output is always clean. Clean text
comes back unchanged. The mask can be any symbol or punctuation; letters, digits, whitespace and
control characters throw a `RangeError`.

### Categories

Every bundled entry has a category: `profanity`, `sexual`, `insult`, `slur`, `harassment` or `mild`.

```ts
import { ProfanityFilter, WordList } from 'persian-text-guard';

const everyday = new ProfanityFilter(WordList.persianDefault); // everything except mild
const strict = new ProfanityFilter(WordList.all); // mild too
const slursAndAbuse = new ProfanityFilter(WordList.bundled('slur', 'harassment'));

console.log(everyday.containsProfanity('این فیلم آشغال بود')); // → false
console.log(strict.containsProfanity('این فیلم آشغال بود')); // → true
console.log(slursAndAbuse.containsProfanity('kys')); // → true
console.log(slursAndAbuse.containsProfanity('کیر')); // → false
```

### Your own words

```ts
import { ProfanityFilter, WordList } from 'persian-text-guard';

const filter = new ProfanityFilter([
  { text: 'اسپم' }, // whole word (the default)
  { text: 'casino', mode: 'anywhere' }, // inside longer words too
]);

console.log(filter.containsProfanity('این پیام اسپم است')); // → true
console.log(filter.containsProfanity('onlinecasino.example')); // → true

// Or combine your entries with the bundled list:
const combined = new ProfanityFilter([...WordList.persianDefault, { text: 'casino', mode: 'anywhere', category: 'mild' }]);
console.log(combined.containsProfanity('casino night')); // → true
```

Word lists can also be kept in a text file, one entry per line. Read the file yourself (for example
with `readFileSync(path, 'utf8')` in Node.js, or `await file.text()` in a browser) and parse it:

```ts
import { ProfanityFilter, WordList } from 'persian-text-guard';

const text = '# comments start with #\n[insult]\nidiot\n~scam\n';
const words = WordList.parse(text);

console.log(words.map(word => `${word.text}/${word.mode}/${word.category}`).join(' ')); // → idiot/wholeWord/insult scam/anywhere/insult
console.log(new ProfanityFilter(words).containsProfanity('scammers everywhere')); // → true
```

A heading such as `[insult]` names a category in any letter case. An unknown heading throws a
`WordListFormatError` with its `line`.

### Options

Every evasion the filter reads through can be turned off:

```ts
import { ProfanityFilter, WordList } from 'persian-text-guard';

const literal = new ProfanityFilter(WordList.persianDefault, { joinSpacedLetters: false });

console.log(literal.containsProfanity('f u c k')); // → false
console.log(literal.containsProfanity('fuuuck')); // → true
```

### Normalization

```ts
import { normalize, tokenize, toAsciiDigits, toPersianDigits } from 'persian-text-guard';

// 'comparison' (the default): everything folded, for searching and matching.
console.log(normalize('كتاب‌هاي  ۱۲ ABC')); // → کتابهای 12 abc
// 'standard': safe to store and show; keeps the zero-width non-joiner, Persian digits and case.
console.log(normalize('كتاب‌هاي  ۱۲ ABC', 'standard')); // → کتاب‌های ۱۲ ABC
console.log(tokenize('سلام، دنیا! خوبی؟').join(' | ')); // → سلام | دنیا | خوبی
console.log(toPersianDigits('2 ساعت پیش')); // → ۲ ساعت پیش
console.log(toAsciiDigits('۱۴۰۴/۰۵/۱۴')); // → 1404/05/14
```

## Validating input

Checking, finding matches, censoring, normalizing and tokenizing accept any string, including empty
text, broken emoji and very long messages, and treat `null` and `undefined` as an empty message. They
never throw for text.

Anything else — a number, an object, an array — throws a `TypeError`, so a missing check shows up at
once instead of an unchecked value passing as clean. Values from a request are untrusted, so check
that the message is a string first:

```ts
import { ProfanityFilter, WordList } from 'persian-text-guard';

const filter = new ProfanityFilter(WordList.persianDefault);
const body: { message?: unknown } = JSON.parse('{"message": ["kir"]}');

if (typeof body.message !== 'string') {
  console.log('rejected: message must be text'); // → rejected: message must be text
} else {
  console.log(filter.censor(body.message));
}
```

## .NET to JavaScript

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

## Performance

Against `WordList.persianDefault` (about 1,000 entries), on Node.js 24.21.0 (Intel Core i7-9700K,
tinybench, mean of at least 2 seconds per operation):

| Operation | Mean |
| --- | ---: |
| Short clean message (5 words) | 11.0 µs |
| Long clean message (60 words) | 92.8 µs |
| Message with evasions | 7.9 µs |
| Normalize a long message | 32.5 µs |
| Build a filter from the bundled list | 2.4 ms |
| `findMatches`, clean short message | 11.0 µs |
| `findMatches`, message with three banned words | 29.0 µs |
| `censor`, short message with one banned word | 13.2 µs |
| `censor`, 60-word message with three banned words | 372 µs |
| A 132,000-character message | 30.7 ms |

Build the filter once and reuse it. The messages are the same as the .NET benchmarks'; run them with
`npm run bench` in `js/`.

## Limitations

- **Unicode versions.** The filter uses the JavaScript engine's own Unicode data. Characters assigned
  in Unicode 16 or later can be classified or normalized differently across JavaScript engines, Node.js
  versions and .NET targets. The conformance corpus contains none of them; if a difference matters to
  you, please open an issue.
- **Browsers.** Chrome, Edge and Node.js (the V8 engine) are tested in CI. Firefox and Safari are
  supported but not tested in CI; they use their own Unicode data, and the conformance corpus is the
  reference if their results differ.
- The evasions the filter reads through, and what it deliberately does not catch, are listed in the
  [project README](https://github.com/AmirehsanK/PersianTextGuard#what-it-reads-through).

## Links

- [Project README](https://github.com/AmirehsanK/PersianTextGuard#readme)
- [Conformance corpus](https://github.com/AmirehsanK/PersianTextGuard/tree/main/conformance)
- [Word lists](https://github.com/AmirehsanK/PersianTextGuard/tree/main/wordlists)
- [MIT licence](https://github.com/AmirehsanK/PersianTextGuard/blob/main/LICENSE)

---

<div dir="rtl">

## فارسی

`persian-text-guard` نسخهٔ جاوااسکریپت و تایپ‌اسکریپتِ PersianTextGuard است: یکسان‌سازی متن فارسی و
فیلتر کلمات نامناسب که ترفندهای دور زدن فهرست کلمات را می‌شناسد، مثل `ک.ی.ر`، `f u c k`، `sh1t`، حروف
عربی به‌جای فارسی و نویسه‌های نامرئی، و در عین حال پیام‌های عادی مثل `هر کس` و `تخم مرغ` را رد نمی‌کند.
برای هر پیام همان پاسخی را می‌دهد که بستهٔ ‎.NET‎ می‌دهد.

### نصب

</div>

```bash
npm install persian-text-guard
```

<div dir="rtl">

### شروع سریع

فیلتر را یک بار بسازید و همه‌جا از همان استفاده کنید؛ فیلتر بعد از ساخته شدن هرگز تغییر نمی‌کند.

</div>

```ts
import { ProfanityFilter, WordList } from 'persian-text-guard';

// فیلتر را یک بار بسازید و به اشتراک بگذارید.
// Build once and share it: a filter never changes after it is built.
const filter = new ProfanityFilter(WordList.persianDefault);

console.log(filter.containsProfanity('سلام، سفارشم کی میرسه؟')); // → false
console.log(filter.containsProfanity('ک.ی.ر')); // → true
```

<div dir="rtl">

### سانسور

هر کلمهٔ نامناسب، با پسوندها و فاصله‌هایش، با چهار نویسهٔ ثابت پوشانده می‌شود و بقیهٔ متن دست نمی‌خورد.

</div>

```ts
import { ProfanityFilter, WordList } from 'persian-text-guard';

// کلمات نامناسب با چهار نویسه پوشانده می‌شوند.
// Every banned word is hidden behind a four-character mask.
const filter = new ProfanityFilter(WordList.persianDefault);

console.log(filter.censor('جنده‌ها رو ببین')); // → **** رو ببین
console.log(filter.censor('this is kir', '#')); // → this is ####
```

<div dir="rtl">

### بررسی ورودی

هر رشته‌ای پذیرفته می‌شود و `null` و `undefined` مثل پیام خالی هستند، اما هر مقدار دیگری (عدد، شیء،
آرایه) خطای `TypeError` می‌دهد. پیش از فیلتر کردنِ داده‌های درخواست، بررسی کنید که پیام از نوع رشته باشد.

مستندات کامل، فهرست ترفندهایی که شناخته می‌شوند و محدودیت‌ها در
[README اصلی پروژه](https://github.com/AmirehsanK/PersianTextGuard#readme) آمده است.

</div>
