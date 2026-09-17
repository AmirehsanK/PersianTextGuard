// Checks the packed package's structure (publint) and its type declarations under every module
// resolution mode users compile with (attw: node16 CommonJS, node16 ESM and bundler). Research R4.
import { execSync } from 'node:child_process';
import { existsSync, readdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const js = join(dirname(fileURLToPath(import.meta.url)), '..');
const artifacts = join(js, 'artifacts');
const tarballs = existsSync(artifacts) ? readdirSync(artifacts).filter(name => /^persian-text-guard-.+\.tgz$/.test(name)) : [];

if (tarballs.length !== 1) {
  console.error(`Expected exactly one persian-text-guard-*.tgz in artifacts/ (run npm run pack), found ${tarballs.length}.`);
  process.exit(1);
}

const tarball = join(artifacts, tarballs[0]);

try {
  execSync('npx --no-install publint run .pack --strict', { cwd: js, stdio: 'inherit' });
  execSync(`npx --no-install attw "${tarball}" --profile node16`, { cwd: js, stdio: 'inherit' });
} catch {
  console.error('\nPackage checks failed.');
  process.exit(1);
}

console.log('\npublint and attw passed');
