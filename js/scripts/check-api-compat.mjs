// Checks the public API against the previous release's API report (research R14; constitution: the API
// check runs "against the previous release"). A declaration from the previous release that is removed
// or changed is a breaking change, allowed only with a MAJOR version.
import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const js = join(dirname(fileURLToPath(import.meta.url)), '..');
const reportPath = 'js/etc/persian-text-guard.api.md';

function git(...args) {
  return execFileSync('git', args, { cwd: js, encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim();
}

function tryGit(...args) {
  try {
    return git(...args);
  } catch {
    return null;
  }
}

/** The declaration lines of an API report: inside its ts block, without blanks and comments. */
function declarations(report) {
  const block = /```ts\r?\n([\s\S]*?)```/.exec(report);
  if (block === null) {
    throw new Error('The API report has no ts block.');
  }

  return block[1]
    .split(/\r?\n/)
    .map(line => line.trimEnd())
    .filter(line => line.trim().length > 0 && !line.trim().startsWith('//'));
}

const major = version => Number.parseInt(version.replace(/^v/, '').split('.')[0], 10);

// A release build is tagged on HEAD itself; compare it with the release before it.
const tagsAtHead = (tryGit('tag', '--points-at', 'HEAD') ?? '').split(/\r?\n/).filter(tag => /^v\d/.test(tag));
const ref = tagsAtHead.length > 0 ? 'HEAD^' : 'HEAD';
const previousTag = tryGit('describe', '--tags', '--abbrev=0', '--match', 'v[0-9]*', ref);

if (previousTag === null) {
  console.log('No previous release tag; baseline only');
  process.exit(0);
}

const previousReport = tryGit('show', `${previousTag}:${reportPath}`);
if (previousReport === null) {
  console.log(`No previous npm release report at ${previousTag} (first release); baseline only`);
  process.exit(0);
}

const currentPath = join(js, 'etc', 'persian-text-guard.api.md');
if (!existsSync(currentPath)) {
  console.error(`${reportPath} is missing; run npm run api -- --local`);
  process.exit(1);
}

const previous = declarations(previousReport);
const current = new Set(declarations(readFileSync(currentPath, 'utf8')));
const missing = previous.filter(line => !current.has(line));

if (missing.length > 0) {
  for (const line of missing) {
    console.log(`BREAKING: ${line.trim()}`);
  }

  const version = readFileSync(join(js, '..', 'VERSION'), 'utf8').trim();
  if (major(version) > major(previousTag)) {
    console.log(`Allowed by MAJOR version: ${version} after ${previousTag}`);
    process.exit(0);
  }

  console.error(`\n${missing.length} declaration(s) from ${previousTag} were removed or changed; that needs a MAJOR version.`);
  process.exit(1);
}

console.log(`API compatible with ${previousTag}: ${previous.length} declarations kept, ${current.size - new Set(previous).size} added`);
