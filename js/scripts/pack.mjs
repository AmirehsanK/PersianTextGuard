// Packs the npm package with its version taken from the repository's VERSION file (research R9).
// The committed package.json keeps a placeholder version, so VERSION stays the only version source.
import { execSync } from 'node:child_process';
import { copyFileSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const js = join(dirname(fileURLToPath(import.meta.url)), '..');

function findRoot(start) {
  for (let dir = start; ; dir = dirname(dir)) {
    if (existsSync(join(dir, 'VERSION')) && existsSync(join(dir, 'wordlists'))) return dir;
    if (dirname(dir) === dir) return null;
  }
}

const root = findRoot(js);
if (root === null) {
  console.error('Could not find the repository root (VERSION and wordlists/)');
  process.exit(1);
}

const version = readFileSync(join(root, 'VERSION'), 'utf8').trim();
const run = (command, cwd = js) => execSync(command, { cwd, stdio: ['ignore', 'pipe', 'inherit'], encoding: 'utf8' });

run('npm run build');

const stage = join(js, '.pack');
// Empty the staging directory rather than delete it: on Windows a directory some process has open cannot be removed.
mkdirSync(stage, { recursive: true });
for (const entry of readdirSync(stage)) {
  rmSync(join(stage, entry), { recursive: true, force: true });
}
mkdirSync(join(stage, 'dist'), { recursive: true });

for (const file of ['index.mjs', 'index.cjs', 'index.d.mts', 'index.d.cts']) {
  copyFileSync(join(js, 'dist', file), join(stage, 'dist', file));
}

copyFileSync(join(js, 'README.md'), join(stage, 'README.md'));
copyFileSync(join(root, 'LICENSE'), join(stage, 'LICENSE'));
copyFileSync(join(root, 'THIRD-PARTY-NOTICES.md'), join(stage, 'THIRD-PARTY-NOTICES.md'));

const manifest = JSON.parse(readFileSync(join(js, 'package.json'), 'utf8'));
manifest.version = version;
delete manifest.scripts;
delete manifest.devDependencies;
delete manifest.private;
// npm adds README, LICENSE and package.json on its own; the notices must be listed.
manifest.files = ['dist', 'THIRD-PARTY-NOTICES.md'];
writeFileSync(join(stage, 'package.json'), `${JSON.stringify(manifest, null, 2)}\n`);

const expected = [
  'LICENSE',
  'README.md',
  'THIRD-PARTY-NOTICES.md',
  'dist/index.cjs',
  'dist/index.d.cts',
  'dist/index.d.mts',
  'dist/index.mjs',
  'package.json',
];

// npm 10 and 11 print an array; npm 12 prints an object keyed by package name.
const firstResult = json => (Array.isArray(json) ? json[0] : Object.values(json)[0]);

const dryRun = firstResult(JSON.parse(run('npm pack --dry-run --json', stage)));
const files = dryRun.files.map(f => f.path).sort();
if (JSON.stringify(files) !== JSON.stringify(expected)) {
  console.error(`Unexpected package contents:\n  ${files.join('\n  ')}\nExpected:\n  ${expected.join('\n  ')}`);
  process.exit(1);
}

const artifacts = join(js, 'artifacts');
mkdirSync(artifacts, { recursive: true });
const packed = firstResult(JSON.parse(run(`npm pack --json --pack-destination "${artifacts}"`, stage)));
console.log(join(artifacts, packed.filename));
console.log(`unpacked size: ${packed.unpackedSize} bytes; ${packed.entryCount} files; version ${packed.version}`);
