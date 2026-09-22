# Contributing to PersianTextGuard

Thank you for being here. PersianTextGuard is a Persian profanity filter with four ports — .NET,
JavaScript/TypeScript, Python and Rust — that all give the same answer for the same message.

**The most valuable thing you can contribute is a real message.** A message that slipped through, or an
ordinary message that was wrongly flagged, is worth more than any amount of code: it becomes a test case
that every port must pass forever.

- [Ways to help](#ways-to-help)
- [Two rules that shape everything](#two-rules-that-shape-everything)
- [Reporting a miss or a false positive](#reporting-a-miss-or-a-false-positive)
- [Getting set up](#getting-set-up)
- [Changing the word lists](#changing-the-word-lists)
- [Changing matching behaviour](#changing-matching-behaviour)
- [Working on one port](#working-on-one-port)
- [Pull requests](#pull-requests)
- [What is out of scope](#what-is-out-of-scope)
- [Conduct](#conduct) — and the [Code of Conduct](CODE_OF_CONDUCT.md)
- [Security](#security)
- [خلاصهٔ فارسی](#خلاصهٔ-فارسی)

## Ways to help

| | What | Effort |
| --- | --- | --- |
| 1 | **Report a missed word or a false positive** — open an issue with the exact text | minutes |
| 2 | **Add or fix word-list entries**, especially Finglish spellings your community uses | small |
| 3 | **Fix a bug** in a port, or improve documentation and translations | small to medium |
| 4 | **Improve matching**: a new evasion, a better algorithm, a performance win | medium |
| 5 | **Add a port** (Java and Go are planned) — read the [constitution](.specify/memory/constitution.md) first | large |

No contribution is too small. Fixing one wrong word in the Persian README is a real contribution.

## Two rules that shape everything

**1. Ordinary messages must pass.** A filter that rejects normal messages teaches people the site is
broken, and nobody reports it. False positives are treated as more severe than missed evasions. This is
why «کسی» (someone), «هر کس», «تخم مرغ», `class`, `push it` and `Scunthorpe` all pass, and why every new
way of catching a word must also add the ordinary text it could plausibly catch.

**2. One behaviour, in every language.** [`conformance/`](conformance/README.md) is the specification:
523 language-neutral cases with exact expected results. There is no reference implementation whose
behaviour wins — the corpus wins. **A change to matching behaviour changes the corpus and every port in
the same pull request.**

Both rules, and the rest of the project's ground rules, are written down in the
[constitution](.specify/memory/constitution.md). You do not need to read it to fix a typo, but do read
it before proposing a new evasion, a dependency or a port.

## Reporting a miss or a false positive

Open an issue and include:

- **The exact text**, copied and pasted, not retyped or described.
- **Which package and version** you used (`persian-text-guard` 1.5.0 on npm, `PersianTextGuard` 1.5.0 on
  NuGet, and so on), and which word list (`persian_default()`, `all()`, your own).
- **What you expected** and what you got.
- For invisible characters, the **code points** help a lot:

```bash
node -e 'console.log([...process.argv[1]].map(c=>c.codePointAt(0).toString(16).toUpperCase().padStart(4,"0")).join(" "))' 'YOUR TEXT'
```

```python
python -c "import sys; print(' '.join(f'{ord(c):04X}' for c in sys.argv[1]))" "YOUR TEXT"
```

Two things to keep in mind: issues are public, so **do not paste other people's personal data**,
usernames or screenshots of private conversations — the offending text alone is enough. And a miss is
not always a bug: the project deliberately does not catch transposed letters (`fcuk`), letters typed as
different letters (`cvnt`), or words split in a way that would glue ordinary words together (`fuc k`).
Those are listed under [Limitations](README.md#limitations).

## Getting set up

You only need the toolchain of the port you are touching. Word-list and corpus work needs the .NET SDK,
because the fill-in tool lives there.

| Port | You need | Directory |
| --- | --- | --- |
| .NET | .NET 10 SDK | [`dotnet/`](dotnet/) |
| JavaScript | Node.js 22 or later | [`js/`](js/) |
| Python | [uv](https://docs.astral.sh/uv/) | [`python/`](python/) |
| Rust | Rust 1.85 or later | [`rust/`](rust/) |

```bash
git clone https://github.com/AmirehsanK/PersianTextGuard.git
cd PersianTextGuard

# Whichever port you are working on:
dotnet test dotnet/tests/PersianTextGuard.Tests          # .NET
cd js && npm ci && npm run test:all                      # JavaScript
cd python && uv sync --locked && uv run pytest           # Python
cd rust && cargo test --locked                           # Rust
```

The full command tables are in the README's [Development](README.md#development) section.

## Changing the word lists

The lists live once, in [`wordlists/`](wordlists/), and every port embeds them at build time. Never edit a
copy inside a port.

The format is one entry per line; `~` before a word matches it anywhere, including inside longer words;
`#` starts a comment; `[category]` starts a section. Read the header of
[`wordlists/persian.txt`](wordlists/persian.txt) first — it explains the format, the categories, and what
is deliberately left out and why.

Before you add an entry, check it against these rules:

- **Whole-word by default.** Use `~anywhere` only for a stem whose *every* extension is offensive
  (`~fuck` covers `motherfucker`). Never for a stem that hides inside ordinary words — `ass` in `class`,
  `cunt` in `Scunthorpe`, `nigger` in `sniggered`, `کس` in «کسی».
- **Do not add what the matcher already catches.** Leetspeak (`sh1t`), held keys (`fuuuck`), spaced or
  dotted letters (`ک.ی.ر`), accents (`fück`) and Persian suffixes («جنده‌ها») are handled by the
  algorithm. The lists carry *spellings*, not typography.
- **Ordinary-in-context words go in `[mild]`**, which the default list excludes, and the reason belongs in
  the file header.
- **Every bundled entry needs a category.** `uncategorized` is reserved for users' own lists.
- **Names of ethnicities, nationalities and religions are not banned words.** Slurs *about* a group are,
  under `[slur]`.
- **Copied from somewhere?** The source licence must be compatible with MIT (MIT, Apache 2.0, CC0, CC BY),
  and it must be credited in [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).

Then check your change:

```bash
# Every port must still agree with the corpus.
dotnet test dotnet/tests/PersianTextGuard.Conformance
cd js && npm run corpus
cd python && uv run pytest -m corpus
cd rust && cargo test --locked --test corpus
```

Adding or removing entries changes the counts recorded by the `category-selection` cases in the corpus.
The fill-in tool reports the disagreement:

```bash
dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill -- --check
```

Update those cases in the same pull request, on purpose — the tool never overwrites a recorded result for
you.

## Changing matching behaviour

Anything that changes *which messages match* follows this order:

1. **Write the corpus cases first**, in [`conformance/cases/`](conformance/cases/): the message that
   should now match (`must-match`), **and** the ordinary messages your change could plausibly catch by
   mistake (`ordinary`). A case without an `expected` block is "pending".
2. **Fill in the expected results** with the tool, which records what the .NET port computes and refuses
   to record anything that breaks a case's kind rule:

   ```bash
   dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill
   ```

3. **Implement it in all four ports** in the same pull request. The ports are deliberately parallel: the
   JavaScript files are a line-by-line port of the .NET ones, and Python and Rust are ports of those, so
   a change usually looks the same in each.
4. **Run every port's corpus runner.** All of them must pass; a port that fails any case cannot be
   released.
5. **Update the README** evasion table or limitations, in both languages, if the behaviour is
   user-visible.

Every bug fix ships with a regression test that fails without the fix: a corpus case when the bug is in
matching, a port test when it is specific to one port.

## Working on one port

- **No new runtime dependencies.** Each port uses its standard library only, except
  `unicode-normalization` in Rust (and `golang.org/x/text` when Go arrives). Test, build and benchmark
  dependencies are fine.
- **Nothing may throw or panic on message text**, ever — including empty, whitespace-only, very long,
  invalid-encoding and `null`/`None` input. Failures are allowed only for programmer errors: a bad mask
  character, an unreadable word-list file.
- **A filter is immutable and shared.** Work that can happen when the filter is built belongs there, not
  in the per-message path.
- **If you touch the per-message path, benchmark it** with that port's tool (BenchmarkDotNet, tinybench,
  pyperf, Criterion) and put the before-and-after numbers in the pull request.
- **Every public item needs documentation** in the ecosystem's format (XML docs, TSDoc, docstrings,
  rustdoc) explaining behaviour and edge cases, not just the signature. API docs are in English.
- **Every README code example has a matching test.** If you change an example, update its test.

## Pull requests

1. Work on a branch and open a pull request against `main`.
2. CI must be green: sixteen build and test jobs across .NET, JavaScript (Node 22, 24), Python (3.11 to
   3.14 and free-threaded 3.14t) and Rust (stable and 1.85 on Linux, Windows and macOS).
3. Keep the change focused. A word-list addition, a bug fix and a refactor are three pull requests.
4. Write commit messages in plain sentences that say *why* ("Keep the ZWNJ inside tokens so «جنده‌ها»
   censors as one word"), not just what changed.

A quick self-check before you press the button:

- [ ] Behaviour change? The corpus and all four ports are updated in this pull request.
- [ ] New way of catching words? Ordinary messages that it might catch are in the corpus too.
- [ ] Bug fix? A test fails without the fix.
- [ ] Word list touched? Categories, `~anywhere` rules and the file header are consistent; sources
      credited.
- [ ] Per-message path touched? Benchmark numbers are in the description.
- [ ] Documentation touched in one language? The other language is updated too.

Maintainers: releases are lockstep across NuGet, npm, PyPI and crates.io from a `vX.Y.Z` tag; see
[Releasing](README.md#releasing).

## What is out of scope

So you do not spend effort on something that will not be merged:

- **Spam, link and scam detection**, and machine-learning classifiers. This project matches words in
  Persian text; spam detection belongs in a layer above it. (You can, of course, add your own
  `~casino`-style entries in your own list.)
- **Stripping emoji, links or formatting from messages.** The filter never rewrites your text; it reports
  regions and, if you ask, replaces exactly those regions with a mask.
- **Entries the matcher already catches**, or entries that fire inside ordinary words.
- **A behaviour change in one port only.** All four must move together.
- **Anything that raises false positives** on the corpus's ordinary messages. If an evasion cannot be
  caught without that cost, it is documented as a limitation instead.

## Conduct

Please read the [Code of Conduct](CODE_OF_CONDUCT.md); taking part means following it.

The short version: be respectful, assume good faith, and discuss the work rather than the person. This
project handles offensive language by necessity — the word lists contain slurs and sexual insults because
that is what they exist to catch, and discussing them clinically is part of the work. Using them at
another person is not. If someone behaves badly, [report it privately](CODE_OF_CONDUCT.md#reporting)
rather than replying in kind.

## Security

Please do **not** open a public issue for a security problem. Use GitHub's private vulnerability
reporting on the repository's **Security** tab ("Report a vulnerability"). If that is unavailable, open
an issue asking for a private contact, without any details of the problem.

A crash on untrusted input counts as a security issue here: the filter runs on a request path, so "user
input never throws" is a safety property, not just a nicety.

---

<div dir="rtl">

## خلاصهٔ فارسی

</div>

<div dir="rtl">

خوش آمدید. مفیدترین کمک، فرستادن یک پیام واقعی است: پیامی که فیلتر آن را نگرفته، یا پیامی عادی که به اشتباه علامت خورده است. متن دقیق را در یک ایشوی جدید بگذارید، همراه با نام و نسخهٔ بسته‌ای که استفاده کرده‌اید و اینکه چه انتظاری داشته‌اید. ایشوها عمومی‌اند، پس لطفاً اطلاعات شخصی دیگران را نفرستید.

</div>

<div dir="rtl">

دو قاعده بر همهٔ تصمیم‌ها حاکم است. نخست، پیام‌های عادی باید رد شوند و علامت نخورند؛ خطای مثبت از نگرفتنِ یک کلمه بدتر است. دوم، رفتار همهٔ نسخه‌ها باید یکی باشد: مجموعهٔ آزمون مشترک در پوشهٔ conformance مرجع است، و هر تغییر در رفتار باید در همان درخواست ادغام، هم مجموعهٔ آزمون و هم هر چهار نسخه را با هم تغییر دهد.

</div>

<div dir="rtl">

برای افزودن کلمه به فهرست‌ها، فایل‌های پوشهٔ wordlists را ویرایش کنید و سرآیند فایل persian.txt را بخوانید: قالب، دسته‌ها و آنچه عمداً کنار گذاشته شده آنجا توضیح داده شده است. کلمه‌ای را اضافه نکنید که موتور جست‌وجو خودش می‌گیرد، مثل نوشتن با عدد، کشیدن حرف، فاصله یا نقطه میان حروف، و پسوندهای فارسی. کلمه‌هایی که در متن عادی هم به کار می‌روند باید در دستهٔ mild بمانند.

</div>

<div dir="rtl">

پیش از فرستادن درخواست ادغام، آزمون‌های نسخه‌ای که تغییر داده‌اید و اجراکنندهٔ مجموعهٔ آزمون را اجرا کنید. اگر مسیر پردازش هر پیام را تغییر داده‌اید، عددهای سنجش کارایی را پیش و پس از تغییر در توضیح درخواست بیاورید. برای گزارش مشکل امنیتی، از بخش Security مخزن استفاده کنید و ایشوی عمومی باز نکنید.

</div>
