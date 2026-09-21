//! Criterion benchmarks for the Rust port (constitution Principle IV), the same ten operations and
//! messages as the .NET, JavaScript and Python benchmarks. The message constants are copied verbatim
//! from `js/bench/filter.bench.ts`, which copied them from
//! `dotnet/benchmarks/PersianTextGuard.Benchmarks/Program.cs`.
//!
//!   cd rust/bench && cargo bench

use std::hint::black_box;

use criterion::{Criterion, criterion_group, criterion_main};
use persian_text_guard::{Normalization, ProfanityFilter, WordList, normalize};

const CLEAN_SHORT: &str = "سلام، سفارشم کی ارسال میشه؟";
const CLEAN_LONG: &str = "سلام وقت بخیر. من هفته پیش یک گوشی از فروشگاه شما سفارش دادم و هنوز به دستم نرسیده. کد رهگیری را هم در پنل کاربری پیدا نکردم. لطفا بررسی کنید و نتیجه را از طریق ایمیل یا پیامک اطلاع بدید. اگر امکانش هست هزینه ارسال را هم برگردانید. ممنون از پشتیبانی خوبتان.";
const EVASION: &str = "ye k0s kesh inja f.u.c.k";
const DIRTY_SHORT: &str = "this is kir";
const DIRTY_MIXED: &str = "این کیر و f u c k و sh1t";
const DIRTY_LONG: &str = "سلام وقت بخیر. کیر من هفته پیش یک گوشی از فروشگاه شما سفارش دادم و هنوز به دستم نرسیده. f.u.c.k کد رهگیری را هم در پنل کاربری پیدا نکردم. جنده‌ها لطفا بررسی کنید و نتیجه را از طریق ایمیل یا پیامک اطلاع بدید. اگر امکانش هست هزینه ارسال را هم برگردانید. ممنون از پشتیبانی خوبتان.";

/// The corpus's 132,000-character message with a banned word at the end (spec: very long messages).
fn very_long() -> String {
    "سلام این یک متن معمولی است و هیچ مشکلی ندارد. hello this is fine. ".repeat(2000) + " کیر"
}

fn benchmarks(c: &mut Criterion) {
    let filter = ProfanityFilter::with_defaults(WordList::persian_default());
    let very_long = very_long();

    c.bench_function("CleanShortMessage", |b| b.iter(|| filter.contains_profanity(black_box(CLEAN_SHORT))));
    c.bench_function("CleanLongMessage", |b| b.iter(|| filter.contains_profanity(black_box(CLEAN_LONG))));
    c.bench_function("EvasiveMessage", |b| b.iter(|| filter.contains_profanity(black_box(EVASION))));
    c.bench_function("NormalizeLongMessage", |b| {
        b.iter(|| normalize(black_box(CLEAN_LONG), Normalization::COMPARISON))
    });
    c.bench_function("BuildFilterFromDefaultList", |b| {
        b.iter(|| ProfanityFilter::with_defaults(black_box(WordList::persian_default())).count())
    });
    c.bench_function("FindMatchesClean", |b| b.iter(|| filter.find_matches(black_box(CLEAN_SHORT)).len()));
    c.bench_function("FindMatchesDirty", |b| b.iter(|| filter.find_matches(black_box(DIRTY_MIXED)).len()));
    c.bench_function("CensorShortDirty", |b| b.iter(|| filter.censor(black_box(DIRTY_SHORT))));
    c.bench_function("CensorLongDirty", |b| b.iter(|| filter.censor(black_box(DIRTY_LONG))));
    c.bench_function("VeryLongMessage", |b| b.iter(|| filter.contains_profanity(black_box(&very_long))));
}

criterion_group!(benches, benchmarks);
criterion_main!(benches);
