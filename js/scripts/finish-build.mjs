// tsup names the ES module declarations index.d.ts in a "type": "module" package, and cannot be told
// otherwise from a config function (it is not passed to its declaration worker). The package exports
// index.d.mts for import and index.d.cts for require, so rename after building.
import { existsSync, renameSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const dist = join(dirname(fileURLToPath(import.meta.url)), '..', 'dist');
const from = join(dist, 'index.d.ts');
const to = join(dist, 'index.d.mts');

if (existsSync(from)) {
  renameSync(from, to);
}

for (const file of ['index.mjs', 'index.cjs', 'index.d.mts', 'index.d.cts']) {
  if (!existsSync(join(dist, file))) {
    console.error(`dist/${file} is missing after the build`);
    process.exit(1);
  }
}
