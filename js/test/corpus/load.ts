// Loads the conformance corpus (conformance/): port of the loading part of
// dotnet/tests/PersianTextGuard.Conformance/Corpus.cs.
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

/** Every case kind format version 1 defines. */
export const KINDS = [
  'ordinary',
  'must-match',
  'robustness',
  'normalization',
  'tokenization',
  'word-list-parsing',
  'category-selection',
  'mask-validation',
] as const;

/** The newest corpus format this runner understands. */
export const SUPPORTED_FORMAT_VERSION = 1;

export type Kind = (typeof KINDS)[number];

export type Json = null | boolean | number | string | Json[] | { [key: string]: Json };
export type JsonObject = { [key: string]: Json };

export interface CorpusCase {
  readonly id: string;
  readonly kind: Kind;
  readonly file: string;
  readonly json: JsonObject;
  readonly pending: boolean;
}

export interface Corpus {
  readonly directory: string;
  readonly metadata: JsonObject;
  readonly configurations: ReadonlyMap<string, JsonObject>;
  readonly files: readonly string[];
  readonly cases: readonly CorpusCase[];
}

export const MATCHING_KINDS: readonly Kind[] = ['ordinary', 'must-match', 'robustness'];

/**
 * The repository root: the nearest directory above this file that has both VERSION and wordlists/.
 * Never the working directory. conformance/ is not required here, so a missing corpus is reported as such.
 */
export function findRepositoryRoot(): string {
  for (let dir = dirname(fileURLToPath(import.meta.url)); ; dir = dirname(dir)) {
    if (existsSync(join(dir, 'VERSION')) && existsSync(join(dir, 'wordlists'))) {
      return dir;
    }

    if (dirname(dir) === dir) {
      throw new Error('Could not find the repository root (a directory with VERSION and wordlists/).');
    }
  }
}

function readJson(path: string): Json {
  if (!existsSync(path)) {
    throw new Error(`${path}: file not found.`);
  }

  try {
    return JSON.parse(readFileSync(path, 'utf8')) as Json;
  } catch (error) {
    throw new Error(`${path}: ${(error as Error).message}`);
  }
}

function isObject(value: Json | undefined): value is JsonObject {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/** Reads corpus.json, configurations.json and every cases/*.json; every error names its file. */
export function loadCorpus(directory: string): Corpus {
  if (!existsSync(directory)) {
    throw new Error(`Conformance corpus not found: '${directory}' does not exist.`);
  }

  const metadataPath = join(directory, 'corpus.json');
  const metadata = readJson(metadataPath);
  if (!isObject(metadata)) {
    throw new Error(`${metadataPath}: expected a JSON object.`);
  }

  const configurationsPath = join(directory, 'configurations.json');
  const configurationList = readJson(configurationsPath);
  if (!Array.isArray(configurationList)) {
    throw new Error(`${configurationsPath}: expected a JSON array.`);
  }

  const configurations = new Map<string, JsonObject>();
  for (const configuration of configurationList) {
    if (isObject(configuration) && typeof configuration.name === 'string') {
      configurations.set(configuration.name, configuration);
    }
  }

  const casesDirectory = join(directory, 'cases');
  const names = existsSync(casesDirectory)
    ? readdirSync(casesDirectory)
        .filter(name => name.endsWith('.json'))
        .sort((a, b) => (a < b ? -1 : a > b ? 1 : 0))
    : [];

  const files: string[] = [];
  const cases: CorpusCase[] = [];

  for (const name of names) {
    const path = join(casesDirectory, name);
    const list = readJson(path);
    if (!Array.isArray(list)) {
      throw new Error(`${path}: expected a JSON array of cases.`);
    }

    files.push(path);
    list.forEach((json, index) => {
      if (!isObject(json)) {
        throw new Error(`${path}: element ${index} is not a case object.`);
      }

      if (typeof json.id !== 'string') {
        throw new Error(`${path}: element ${index} has no string "id".`);
      }

      if (typeof json.kind !== 'string') {
        throw new Error(`${path}: case '${json.id}' has no string "kind".`);
      }

      if (!(KINDS as readonly string[]).includes(json.kind)) {
        throw new Error(`${path}: case '${json.id}' has unknown kind '${json.kind}'.`);
      }

      cases.push({ id: json.id, kind: json.kind as Kind, file: name, json, pending: !('expected' in json) });
    });
  }

  return { directory, metadata, configurations, files, cases };
}
