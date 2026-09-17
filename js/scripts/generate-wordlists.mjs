// Embeds the shared word lists (wordlists/*.txt) into the package at build time (research R6).
// The output is git-ignored, so the repository keeps exactly one copy of each list (FR-005).
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));

function findRoot(start) {
  for (let dir = start; ; dir = dirname(dir)) {
    if (existsSync(join(dir, 'VERSION')) && existsSync(join(dir, 'wordlists'))) {
      return dir;
    }
    if (dirname(dir) === dir) {
      return null;
    }
  }
}

const root = findRoot(here);
if (root === null) {
  console.error('Could not find the repository root (VERSION and wordlists/)');
  process.exit(1);
}

// Bundled order is Persian, Finglish, English, as in the .NET package.
const texts = ['persian.txt', 'finglish.txt', 'english.txt'].map(name =>
  readFileSync(join(root, 'wordlists', name), 'utf8'),
);

const content =
  '// Generated from wordlists/ by scripts/generate-wordlists.mjs. Do not edit.\n' +
  '/** The bundled word-list files, in order: Persian, Finglish, English. */\n' +
  `export const BUNDLED_WORD_LISTS: readonly string[] = [\n${texts.map(t => `  ${JSON.stringify(t)},`).join('\n')}\n];\n`;

const out = join(here, '..', 'src', 'generated', 'wordlists.ts');
mkdirSync(dirname(out), { recursive: true });
if (!existsSync(out) || readFileSync(out, 'utf8') !== content) {
  writeFileSync(out, content);
}
