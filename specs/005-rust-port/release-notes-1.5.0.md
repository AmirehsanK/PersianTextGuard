## PersianTextGuard 1.5.0

### New: Rust

PersianTextGuard is now on crates.io as **`persian-text-guard`**, used as `persian_text_guard`. It is a
native Rust crate for Rust 1.85 and later (edition 2024), with one dependency,
`unicode-normalization`, and `#![forbid(unsafe_code)]`.

```bash
cargo add persian-text-guard
```

```rust
use persian_text_guard::{ProfanityFilter, WordList};

let filter = ProfanityFilter::with_defaults(WordList::persian_default());
assert!(filter.contains_profanity("ک.ی.ر"));
assert_eq!(filter.censor("kir and motherfucker"), "**** and ****");
```

It gives the same answers as the .NET, JavaScript and Python packages for every case in the shared
conformance corpus. Positions are **byte offsets** of the string you passed, so `&text[m.range()]` is the
matched region. A filter is immutable and `Send + Sync`, so threads share one without a lock; no function
panics for any input, and 100,000 arbitrary strings and 100,000 arbitrary byte sequences are run through
every entry point on each CI run. `WordList::load` and `load_reader` read a word-list file from a path or
any reader. See the
[Rust README](https://github.com/AmirehsanK/PersianTextGuard/tree/main/rust#readme).

The crate's Unicode categories and invariant lower-casing come from tables generated from .NET 10 itself
and checked in CI, so they never drift with the compiler's Unicode version.

### New: byte versions, for input that may not be UTF-8

The Rust crate adds `contains_profanity_bytes`, `find_match_bytes`, `find_matches_bytes`, `censor_bytes`
and `censor_bytes_with`, which take `&[u8]`. Invalid sequences are read as U+FFFD, exactly as
`String::from_utf8_lossy` reads them, and every byte outside a censored region is returned unchanged,
invalid ones included:

```rust
# use persian_text_guard::{ProfanityFilter, WordList};
# let filter = ProfanityFilter::with_defaults(WordList::persian_default());
assert_eq!(filter.censor_bytes(b"kir \xFF fuck"), b"**** \xFF ****");
```

### The conformance corpus runner rules

A Rust `String` cannot hold a lone surrogate, and Rust has no missing string value. The runner
obligations in the corpus contract gained two readings for ports in that position: such an Input is built
as UTF-16 units with each lone surrogate replaced by U+FFFD (in the Input and in the expected text), a
mask that does not build into one character counts as refused, and a `null` Input is read as the empty
string. Both readings were measured against .NET itself and give its own results.

**No corpus case changed**, the format version stays 1, and the .NET, JavaScript and Python runners are
unaffected. The Rust runner runs all 523 cases with none reported as not applicable.

### .NET, JavaScript and Python

NuGet `PersianTextGuard` 1.5.0 and `persian-text-guard` 1.5.0 on npm and PyPI are identical to 1.4.0
apart from the version number: no change to the API, behaviour or bundled word lists. Each package is now
validated against 1.4.0, the previous release.

---

<div dir="rtl">

### خلاصهٔ فارسی

</div>

<div dir="rtl">

**جدید: راست.** این کتابخانه اکنون برای زبان راست هم منتشر شده است، با نام persian-text-guard روی مخزن crates.io، برای راست ۱٫۸۵ و بالاتر، تنها با یک وابستگی و بدون هیچ کد ناامن. برای هر پیام همان پاسخی را می‌دهد که بسته‌های دات‌نت، جاوااسکریپت و پایتون می‌دهند، و جایگاه‌ها را با بایت‌های همان رشته‌ای می‌شمارد که به آن داده‌اید.

</div>

```bash
cargo add persian-text-guard
```

<div dir="rtl">

**جدید: نسخه‌های بایتی.** برای ورودی‌هایی که شاید یوتی‌اف‑۸ معتبر نباشند، پنج تابع تازه روی بایت‌ها کار می‌کنند: دنباله‌های نامعتبر مثل جایگزینی خود زبان خوانده می‌شوند و بایت‌های بیرون از ناحیهٔ سانسورشده دست‌نخورده برمی‌گردند.

</div>

<div dir="rtl">

**قواعد اجرای مجموعهٔ آزمون.** چون رشتهٔ راست نمی‌تواند نیم‌جانشین تنها را نگه دارد و مقدار تهی هم ندارد، دو خوانش تازه به قواعد اجراکننده‌ها افزوده شد؛ هیچ موردی از مجموعهٔ آزمون تغییر نکرد و هر ۵۲۳ مورد در راست اجرا می‌شوند.

</div>

<div dir="rtl">

**دات‌نت، جاوااسکریپت و پایتون.** نسخهٔ ۱٫۵٫۰ این سه بسته جز شمارهٔ نسخه با ۱٫۴٫۰ یکسان است و رفتار و فهرست کلمات آن‌ها تغییری نکرده است.

</div>
