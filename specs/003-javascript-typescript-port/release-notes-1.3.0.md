## PersianTextGuard 1.3.0

### New: JavaScript and TypeScript

PersianTextGuard is now on npm as **`persian-text-guard`**, with TypeScript types included. It works with
`import` and `require`, on Node.js 22 and later, and in browsers through a bundler, with no dependencies.

```bash
npm install persian-text-guard
```

```ts
import { ProfanityFilter, WordList } from 'persian-text-guard';

const filter = new ProfanityFilter(WordList.persianDefault);
filter.containsProfanity('ک.ی.ر'); // true
filter.censor('kir and motherfucker'); // '**** and ****'
```

It gives the same answers as the .NET package for every case in the shared conformance corpus. Positions
are JavaScript string indexes. See the [JavaScript README](https://github.com/AmirehsanK/PersianTextGuard/tree/main/js#readme).

### Changes for .NET users

These change behaviour only for the inputs named here:

- **Fixed: messages containing Unicode noncharacters no longer throw.** In 1.2.0, `ContainsProfanity`,
  `FindMatch`, `FindMatches`, `Censor` and `PersianNormalizer.Normalize` threw `ArgumentException` for a
  message containing U+FFFE (on .NET 8 and .NET 10), or any of the 66 noncharacters (on .NET Framework
  4.8). They now return normally. Every message that worked in 1.2.0 gives the same result.
- **Changed: word-list section headings must be category names.** `WordList.Parse` and `WordList.Load`
  accept `[insult]` in any letter case, as before. A number such as `[3]` or `[+4]`, which 1.2.0 read as
  the category with that internal number, now throws `FormatException` as an unknown category. The
  bundled lists are not affected.

No other change to the .NET API, behaviour or bundled word lists. The package passes API compatibility
validation against 1.2.0.

---

<div dir="rtl">

### خلاصهٔ فارسی

**جدید: جاوااسکریپت و تایپ‌اسکریپت.** PersianTextGuard اکنون با نام `persian-text-guard` روی npm منتشر
شده است، همراه با تایپ‌های تایپ‌اسکریپت و بدون هیچ وابستگی. با `import` و `require`، روی Node.js 22 و
بالاتر و در مرورگر کار می‌کند و برای هر پیام همان پاسخی را می‌دهد که بستهٔ ‎.NET‎ می‌دهد.

**تغییرات برای کاربران ‎.NET‎:**

- **رفع اشکال:** پیامی که «نانویسه» (noncharacter) یونیکد مثل U+FFFE دارد دیگر خطا نمی‌دهد. در نسخهٔ
  1.2.0 این پیام‌ها باعث `ArgumentException` می‌شدند. پاسخ همهٔ پیام‌هایی که قبلاً درست کار می‌کردند
  تغییری نکرده است.
- **تغییر:** در فایل فهرست کلمات، عنوان هر بخش باید نام دسته باشد، مثل `[insult]`. عددی مثل `[3]` که
  نسخهٔ 1.2.0 می‌پذیرفت، اکنون دستهٔ ناشناخته به شمار می‌آید. فهرست‌های همراه بسته تغییری نکرده‌اند.

رابط برنامه‌نویسی و رفتار ‎.NET‎ جز این دو مورد تغییری نکرده است.

</div>
