// Bundled for the browser and run in a context without Node.js globals.
import { ProfanityFilter, WordList } from 'persian-text-guard';

const filter = new ProfanityFilter(WordList.persianDefault);
const check = (actual, expected, what) => {
  if (actual !== expected) throw new Error(`${what}: expected ${expected}, got ${actual}`);
};

check(filter.containsProfanity('ک.ی.ر'), true, 'flags ک.ی.ر');
check(filter.containsProfanity('سلام، سفارشم کی میرسه؟'), false, 'ordinary message');
check(filter.censor('kir and motherfucker'), '**** and ****', 'censor');
check(filter.findMatch('😀 کیر').index, 3, 'index');

globalThis.result = 'browser ok';
