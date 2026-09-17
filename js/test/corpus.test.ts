// The conformance corpus runner for the JavaScript port (spec 003, FR-016, FR-017). It follows the
// runner obligations in specs/002-monorepo-conformance-corpus/contracts/corpus-format.md.
import { join } from 'node:path';
import { describe, expect, test } from 'vitest';
import { checkKindRules, evaluate } from './corpus/evaluate.ts';
import { KINDS, MATCHING_KINDS, SUPPORTED_FORMAT_VERSION, findRepositoryRoot, loadCorpus } from './corpus/load.ts';
import type { CorpusCase } from './corpus/load.ts';
import { buildInput, compare, display, showText } from './corpus/values.ts';

const FILL_IN_HINT = 'run dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill';

// Loading errors (a missing or unreadable corpus) fail here, at collection, and name the file.
const corpus = loadCorpus(join(findRepositoryRoot(), 'conformance'));

describe('corpus', () => {
  test('loads', () => {
    expect(corpus.files.length).toBeGreaterThan(0);
  });

  test('has a format version this runner understands', () => {
    expect(corpus.metadata.formatVersion).toBe(SUPPORTED_FORMAT_VERSION);
  });

  test('has at least 300 cases', () => {
    expect(corpus.cases.length, `The corpus has ${corpus.cases.length} cases; at least 300 are required.`).toBeGreaterThanOrEqual(300);
  });

  test('has unique, well-formed ids', () => {
    const seen = new Map<string, string[]>();
    for (const c of corpus.cases) {
      seen.set(c.id, [...(seen.get(c.id) ?? []), c.file]);
    }

    const duplicates = [...seen].filter(([, files]) => files.length > 1).map(([id, files]) => `${id} (${files.join(', ')})`);
    const malformed = corpus.cases.filter(c => !/^[a-z0-9]+(-[a-z0-9]+)*$/.test(c.id)).map(c => `${c.id} (${c.file})`);

    expect(duplicates, `Duplicate case ids:\n${duplicates.join('\n')}`).toEqual([]);
    expect(malformed, `Malformed case ids:\n${malformed.join('\n')}`).toEqual([]);
  });

  test('has no pending case', () => {
    const pending = corpus.cases.filter(c => c.pending).map(c => `${c.id} (${c.file})`);
    expect(pending, `${pending.length} pending case(s); ${FILL_IN_HINT}:\n${pending.join('\n')}`).toEqual([]);
  });

  test('names only configurations that exist', () => {
    const missing = corpus.cases
      .filter(c => MATCHING_KINDS.includes(c.kind))
      .filter(c => typeof c.json.configuration !== 'string' || !corpus.configurations.has(c.json.configuration))
      .map(c => `${c.id} (${c.file}): ${display(c.json.configuration)}`);

    expect(corpus.configurations.has('default')).toBe(true);
    expect(missing).toEqual([]);
  });

  test('has no case that is not applicable to JavaScript', () => {
    // JavaScript strings hold every build part, lone surrogates included, so every input must build.
    const notApplicable: string[] = [];
    for (const c of corpus.cases) {
      for (const field of ['input', 'text', 'mask'] as const) {
        if (field in c.json) {
          try {
            buildInput(c.json[field]);
          } catch (error) {
            notApplicable.push(`${c.id} (${c.file}): ${(error as Error).message}`);
          }
        }
      }
    }

    expect(notApplicable).toEqual([]);
  });
});

function describeInput(c: CorpusCase): string {
  switch (c.kind) {
    case 'word-list-parsing':
      return `text ${showText(buildInput(c.json.text))}`;
    case 'category-selection':
      return `selection ${display(c.json.selection)}`;
    case 'mask-validation':
      return `mask ${showText(buildInput(c.json.mask))}`;
    default:
      return `input ${showText(buildInput(c.json.input))}`;
  }
}

function failure(c: CorpusCase, problem: string, lines: readonly string[]): Error {
  return new Error([`Case '${c.id}' in ${c.file}: ${problem}`, `  ${describeInput(c)}`, ...lines.map(line => `  ${line}`)].join('\n'));
}

function check(c: CorpusCase): void {
  if (c.pending) {
    throw failure(c, `pending case — ${FILL_IN_HINT}`, []);
  }

  const violations = checkKindRules(c);
  if (violations.length > 0) {
    throw failure(c, 'breaks its kind rule', violations);
  }

  const differences = compare(c.json.expected, evaluate(corpus, c));
  if (differences.length > 0) {
    throw failure(
      c,
      `${differences.length} field(s) differ`,
      differences.map(d => `${d.path}: expected ${d.expected} actual ${d.actual}`),
    );
  }
}

for (const kind of KINDS) {
  const cases = corpus.cases.filter(c => c.kind === kind);
  describe.runIf(cases.length > 0)(kind, () => {
    test.each(cases.map(c => [c.id, c] as const))('%s', (_id, c) => {
      check(c);
    });
  });
}
