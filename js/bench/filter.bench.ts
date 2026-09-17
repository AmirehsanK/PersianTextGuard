// Benchmarks for the JavaScript port with tinybench (constitution Principle IV), mirroring
// dotnet/benchmarks/PersianTextGuard.Benchmarks/Program.cs. The messages are copied from it verbatim.
//   npm run bench
import { cpus } from 'node:os';
import { Bench } from 'tinybench';
import { ProfanityFilter, WordList, normalize } from '../dist/index.mjs';

const CleanShort = "سلام، سفارشم کی ارسال میشه؟";
const CleanLong = "سلام وقت بخیر. من هفته پیش یک گوشی از فروشگاه شما سفارش دادم و هنوز به دستم نرسیده. کد رهگیری را هم در پنل کاربری پیدا نکردم. لطفا بررسی کنید و نتیجه را از طریق ایمیل یا پیامک اطلاع بدید. اگر امکانش هست هزینه ارسال را هم برگردانید. ممنون از پشتیبانی خوبتان.";
const Evasion = "ye k0s kesh inja f.u.c.k";
const DirtyShort = "this is kir";
const DirtyMixed = "این کیر و f u c k و sh1t";
const DirtyLong = "سلام وقت بخیر. کیر من هفته پیش یک گوشی از فروشگاه شما سفارش دادم و هنوز به دستم نرسیده. f.u.c.k کد رهگیری را هم در پنل کاربری پیدا نکردم. جنده‌ها لطفا بررسی کنید و نتیجه را از طریق ایمیل یا پیامک اطلاع بدید. اگر امکانش هست هزینه ارسال را هم برگردانید. ممنون از پشتیبانی خوبتان.";

// The corpus's 132,000-character message with a banned word at the end (spec: very long messages).
const VeryLong = 'سلام این یک متن معمولی است و هیچ مشکلی ندارد. hello this is fine. '.repeat(2000) + ' کیر';

const filter = new ProfanityFilter(WordList.persianDefault);

const bench = new Bench({ time: 2000, warmupTime: 500 });

bench
  .add('CleanShortMessage', () => filter.containsProfanity(CleanShort))
  .add('CleanLongMessage', () => filter.containsProfanity(CleanLong))
  .add('EvasiveMessage', () => filter.containsProfanity(Evasion))
  .add('NormalizeLongMessage', () => normalize(CleanLong))
  .add('BuildFilterFromDefaultList', () => new ProfanityFilter(WordList.persianDefault).count)
  .add('FindMatchesClean', () => filter.findMatches(CleanShort).length)
  .add('FindMatchesDirty', () => filter.findMatches(DirtyMixed).length)
  .add('CensorShortDirty', () => filter.censor(DirtyShort))
  .add('CensorLongDirty', () => filter.censor(DirtyLong))
  .add('VeryLongMessage', () => filter.containsProfanity(VeryLong));

await bench.run();

function format(ms: number): string {
  if (ms >= 1) return `${ms.toFixed(ms >= 100 ? 0 : 1)} ms`;
  const us = ms * 1000;
  return `${us.toFixed(us >= 100 ? 0 : 1)} µs`;
}

console.log(`Node.js ${process.version}, ${cpus()[0]?.model.trim() ?? 'unknown CPU'}\n`);
console.log('| Operation | Mean | Operations/s |');
console.log('| --- | ---: | ---: |');
for (const task of bench.tasks) {
  const result = task.result;
  if (result?.state !== 'completed') {
    console.log(`| ${task.name} | failed | |`);
    continue;
  }

  console.log(`| ${task.name} | ${format(result.latency.mean)} | ${Math.round(result.throughput.mean).toLocaleString('en-US')} |`);
}
