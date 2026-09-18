// Installs the packed tarball into each consumer project and runs its check (research R12, SC-007).
//   node scripts/check-consumers.mjs            build, pack, then check
//   node scripts/check-consumers.mjs --no-pack  check the tarball already in artifacts/
import { execSync } from 'node:child_process';
import { readdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const js = join(dirname(fileURLToPath(import.meta.url)), '..');

function run(command, cwd) {
  execSync(command, { cwd, stdio: 'inherit' });
}

if (!process.argv.includes('--no-pack')) {
  run('node scripts/pack.mjs', js);
}

const artifacts = join(js, 'artifacts');
const tarballs = readdirSync(artifacts).filter(name => /^persian-text-guard-.+\.tgz$/.test(name));
if (tarballs.length !== 1) {
  console.error(`Expected exactly one persian-text-guard-*.tgz in artifacts/, found ${tarballs.length}.`);
  process.exit(1);
}

const tarball = join(artifacts, tarballs[0]);

const consumers = [
  ['esm', 'node index.js'],
  ['cjs', 'node index.js'],
  ['typescript', 'npx --no-install tsc -p .'],
  ['browser', 'node bundle.mjs'],
];

for (const [name, check] of consumers) {
  const cwd = join(js, 'consumers', name);
  console.log(`\n== ${name}`);
  try {
    // Install the consumer's own dev tools, then the tarball as users install it.
    run('npm install --no-audit --no-fund --no-save', cwd);
    run(`npm install --no-audit --no-fund --no-save "${tarball}"`, cwd);
    run(check, cwd);
    if (name === 'typescript') {
      console.log('typescript ok');
    }
  } catch {
    console.error(`\nConsumer check failed: ${name}`);
    process.exit(1);
  }
}

console.log('\n4 of 4 consumer checks passed');
