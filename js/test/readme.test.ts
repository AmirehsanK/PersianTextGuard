// Every TypeScript example in js/README.md runs against the built package, and prints exactly what its
// `// → value` comments say (spec 003, FR-024; constitution Principle VI).
import { execFileSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { describe, expect, test } from 'vitest';

const js = join(dirname(fileURLToPath(import.meta.url)), '..');
const readme = readFileSync(join(js, 'README.md'), 'utf8').replace(/\r\n/g, '\n');
const blocks = [...readme.matchAll(/```ts\n([\s\S]*?)```/g)].map(match => match[1]!);
const packageUrl = pathToFileURL(join(js, 'dist', 'index.mjs')).href;
const tsx = join(js, 'node_modules', 'tsx', 'dist', 'loader.mjs');
const temp = join(js, 'temp', 'readme');

function title(block: string): string {
  const first = block.split('\n').find(line => line.startsWith('//') && !/^\/\/.*[؀-ۿ]/.test(line));
  return (first ?? block.split('\n').find(line => line.trim().length > 0 && !line.startsWith('import')) ?? '').slice(0, 70);
}

describe('README examples', () => {
  test('the README has examples', () => {
    expect(blocks.length).toBeGreaterThanOrEqual(10);
  });

  test.each(blocks.map((block, i) => [i + 1, title(block), block] as const))('block %i: %s', (n, _title, block) => {
    mkdirSync(temp, { recursive: true });
    const file = join(temp, `block-${n}.mts`);
    writeFileSync(file, block.replaceAll("from 'persian-text-guard'", `from '${packageUrl}'`));

    const stdout = execFileSync(process.execPath, ['--import', pathToFileURL(tsx).href, file], { encoding: 'utf8' });
    const printed = stdout.replace(/\r\n/g, '\n').split('\n').filter(line => line.length > 0);
    const expected = [...block.matchAll(/\/\/ → (.*)$/gm)].map(match => match[1]!);

    expect(expected.length, 'every example shows what it prints').toBeGreaterThan(0);
    expect(printed).toEqual(expected);
  });
});
