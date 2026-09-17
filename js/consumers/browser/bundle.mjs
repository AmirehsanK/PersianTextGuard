// Consumer check: bundle for the browser, then run the bundle with no Node.js globals (research R12).
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
import { build } from 'esbuild';

for (const entry of ['entry-full', 'entry-normalize']) {
  await build({
    entryPoints: [`${entry}.js`],
    outfile: `out/${entry}.js`,
    bundle: true,
    platform: 'browser',
    format: 'iife',
    minify: false,
    logLevel: 'error',
  });
}

const full = readFileSync('out/entry-full.js', 'utf8');
const context = {};
vm.createContext(context);
vm.runInContext(full, context);
for (const name of ['require', 'process', 'Buffer']) {
  if (vm.runInContext(`typeof ${name}`, context) !== 'undefined') {
    throw new Error(`${name} exists in the bare context`);
  }
}

if (context.result !== 'browser ok') {
  throw new Error(`full bundle: ${String(context.result)}`);
}

const normalizeOnly = readFileSync('out/entry-normalize.js', 'utf8');
if (normalizeOnly.includes('[profanity]')) {
  throw new Error('the normalize-only bundle contains the bundled word lists');
}

const normalizeContext = {};
vm.createContext(normalizeContext);
vm.runInContext(normalizeOnly, normalizeContext);
if (normalizeContext.result !== 'کتابهای 12 abc') {
  throw new Error(`normalize-only bundle: ${String(normalizeContext.result)}`);
}

console.log(`browser ok (full bundle ${full.length} chars, normalize-only ${normalizeOnly.length} chars)`);
