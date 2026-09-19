## PersianTextGuard 1.4.0

### New: Python

PersianTextGuard is now on PyPI as **`persian-text-guard`**, imported as `persian_text_guard`. It is pure
Python with full type hints, for CPython 3.11 and later, free-threaded 3.14 included, with no
dependencies.

```bash
pip install persian-text-guard
```

```python
from persian_text_guard import ProfanityFilter, WordList

filter = ProfanityFilter(WordList.persian_default())
filter.contains_profanity("ک.ی.ر")  # True
filter.censor("kir and motherfucker")  # '**** and ****'
```

It gives the same answers as the .NET and JavaScript packages for every case in the shared conformance
corpus. Positions are Python string indexes (code points). A filter is immutable and safe to share
across threads, and `WordList.load` reads a word-list file from a path or an open file. See the
[Python README](https://github.com/AmirehsanK/PersianTextGuard/tree/main/python#readme).

The PyPI files are published from CI with trusted publishing, and carry attestations that name this
repository and workflow.

### New conformance cases

The corpus gained ten cases for characters outside the Basic Multilingual Plane: emoji, a CJK
Extension B letter, mathematical bold letters and a Deseret letter, next to or inside a banned word, and
inside two ordinary Persian and English messages that must stay unflagged. They pin what every port
already did, as .NET reads such text in UTF-16 units, and change no behaviour in any package.

### .NET and JavaScript

NuGet `PersianTextGuard` 1.4.0 and npm `persian-text-guard` 1.4.0 are identical to 1.3.0 apart from the
version number: no change to the API, behaviour or bundled word lists. The .NET package is now validated
against 1.3.0, the previous release, and the npm package's API is checked against v1.3.0.

---

<div dir="rtl">

### خلاصهٔ فارسی

</div>

<div dir="rtl">

**جدید: پایتون.** این کتابخانه اکنون برای پایتون هم منتشر شده است، با نام persian-text-guard روی مخزن PyPI، برای پایتون ۳٫۱۱ و بالاتر و بدون هیچ وابستگی. برای هر پیام همان پاسخی را می‌دهد که بسته‌های دات‌نت و جاوااسکریپت می‌دهند.

</div>

```bash
pip install persian-text-guard
```

<div dir="rtl">

**موارد تازهٔ آزمون.** ده مورد تازه به مجموعهٔ آزمون مشترک اضافه شده است، برای ایموجی و نویسه‌های بیرون از صفحهٔ پایهٔ یونیکد. این موارد رفتار فعلی همهٔ نسخه‌ها را ثبت می‌کنند و چیزی را تغییر نمی‌دهند.

</div>

<div dir="rtl">

**دات‌نت و جاوااسکریپت.** نسخهٔ ۱٫۴٫۰ این دو بسته جز شمارهٔ نسخه با ۱٫۳٫۰ یکسان است و رفتار و فهرست کلمات آن‌ها تغییری نکرده است.

</div>
