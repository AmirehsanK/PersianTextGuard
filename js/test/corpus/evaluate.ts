// Runs a corpus case against the JavaScript port and returns the result in the kind's `expected` shape,
// with positions in code points: the counterpart of Corpus.Evaluate and CheckKindRules in
// dotnet/tests/PersianTextGuard.Conformance/Corpus.cs.
import {
  ProfanityFilter,
  WordList,
  WordListFormatError,
  normalize,
  toAsciiDigits,
  toPersianDigits,
  tokenize,
} from '../../src/index.ts';
import type {
  BannedWord,
  NormalizationSteps,
  ProfanityFilterOptions,
  ProfanityMatch,
  ResolvedWord,
  WordCategory,
} from '../../src/index.ts';
import type { Corpus, CorpusCase, Json, JsonObject } from './load.ts';
import { buildInput, utf16ToCodePoints } from './values.ts';

function isObject(value: Json | undefined): value is JsonObject {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

const filters = new WeakMap<Corpus, Map<string, ProfanityFilter>>();

/** The filter a named configuration describes, built once per corpus. */
export function filterFor(corpus: Corpus, name: string): ProfanityFilter {
  let cache = filters.get(corpus);
  if (cache === undefined) {
    cache = new Map();
    filters.set(corpus, cache);
  }

  let filter = cache.get(name);
  if (filter === undefined) {
    const configuration = corpus.configurations.get(name);
    if (configuration === undefined) {
      throw new Error(`No configuration is named '${name}'.`);
    }

    filter = buildFilter(configuration);
    cache.set(name, filter);
  }

  return filter;
}

function buildFilter(configuration: JsonObject): ProfanityFilter {
  const wordLists = configuration.wordLists;
  if (!isObject(wordLists)) {
    throw new Error(`Configuration has no "wordLists" object: ${JSON.stringify(configuration)}`);
  }

  let words: readonly BannedWord[];
  if (wordLists.bundled !== undefined) {
    words = selection(wordLists.bundled);
  } else if (Array.isArray(wordLists.entries)) {
    words = wordLists.entries as unknown as BannedWord[];
  } else {
    throw new Error(`"wordLists" needs "bundled" or "entries": ${JSON.stringify(wordLists)}`);
  }

  const options: { -readonly [K in keyof ProfanityFilterOptions]: boolean } = {};
  if (isObject(configuration.options)) {
    for (const name of ['squeezeRepeatedLetters', 'foldLookalikeCharacters', 'joinSpacedLetters'] as const) {
      const value = configuration.options[name];
      if (typeof value === 'boolean') {
        options[name] = value;
      }
    }
  }

  return new ProfanityFilter(words, options);
}

function selection(value: Json): readonly ResolvedWord[] {
  if (Array.isArray(value)) {
    return WordList.bundled(...(value as WordCategory[]));
  }

  if (value === 'default') {
    return WordList.persianDefault;
  }

  if (value === 'all') {
    return WordList.all;
  }

  throw new Error(`Not a selection: ${JSON.stringify(value)}`);
}

function entry(word: ResolvedWord): JsonObject {
  return { text: word.text, mode: word.mode, category: word.category };
}

function matchNode(text: string, match: ProfanityMatch): JsonObject {
  const [start, length] = utf16ToCodePoints(text, match.index, match.length);
  return { entry: entry(match.word), evasion: [...match.evasion], start, length };
}

/** Runs a case and returns the result in its kind's `expected` shape. */
export function evaluate(corpus: Corpus, corpusCase: CorpusCase): JsonObject {
  const json = corpusCase.json;

  switch (corpusCase.kind) {
    case 'ordinary':
    case 'must-match':
    case 'robustness': {
      if (typeof json.configuration !== 'string') {
        throw new Error('"configuration" is missing.');
      }

      const filter = filterFor(corpus, json.configuration);
      const text = buildInput(json.input);
      const first = filter.findMatch(text);

      const result: JsonObject = {
        containsProfanity: filter.containsProfanity(text),
        firstMatch: first === null ? null : matchNode(text!, first),
        matches: filter.findMatches(text).map(match => matchNode(text!, match)),
        censored: filter.censor(text),
      };

      if (Array.isArray(json.masks)) {
        const censoredWith: JsonObject = {};
        for (const mask of json.masks) {
          if (typeof mask !== 'string' || mask.length !== 1) {
            throw new Error(`A mask must be exactly one UTF-16 code unit: ${JSON.stringify(mask)}`);
          }

          censoredWith[mask] = filter.censor(text, mask);
        }

        result.censoredWith = censoredWith;
      }

      return result;
    }

    case 'normalization': {
      const text = buildInput(json.input);
      const steps = json.steps;
      const output =
        steps === 'toPersianDigits'
          ? toPersianDigits(text)
          : steps === 'toAsciiDigits'
            ? toAsciiDigits(text)
            : normalize(text, steps as NormalizationSteps);
      return { output };
    }

    case 'tokenization':
      return { tokens: tokenize(buildInput(json.input)) };

    case 'word-list-parsing': {
      const text = buildInput(json.text);
      if (text === null) {
        throw new Error('"text" is missing.');
      }

      try {
        return { entries: WordList.parse(text).map(entry) };
      } catch (error) {
        if (error instanceof WordListFormatError) {
          return { error: { kind: 'unknown-category', line: error.line } };
        }

        throw error;
      }
    }

    case 'category-selection': {
      if (json.selection === undefined) {
        throw new Error('"selection" is missing.');
      }

      const words = selection(json.selection);
      const isDefault = json.selection === 'default';

      const rules: Json[] = [];
      const allowed = Array.isArray(json.selection) ? new Set(json.selection as WordCategory[]) : null;
      if (words.every(word => (allowed === null ? word.category !== 'uncategorized' : allowed.has(word.category)))) {
        rules.push('categoriesInSelection');
      }

      if (inBundledOrder(words)) {
        rules.push('bundledOrder');
      }

      if (isDefault && words.every(word => word.category !== 'mild')) {
        rules.push('noMild');
      }

      return {
        count: words.length,
        first: words.slice(0, 5).map(entry),
        last: words.slice(Math.max(0, words.length - 5)).map(entry),
        rules,
      };
    }

    case 'mask-validation': {
      const mask = buildInput(json.mask);
      if (mask === null || mask.length !== 1) {
        throw new Error('"mask" must be exactly one UTF-16 code unit.');
      }

      try {
        filterFor(corpus, 'default').censor('kir', mask);
        return { accepted: true };
      } catch (error) {
        if (error instanceof RangeError) {
          return { accepted: false };
        }

        throw error;
      }
    }
  }
}

function inBundledOrder(words: readonly ResolvedWord[]): boolean {
  const all = WordList.all;
  let position = 0;
  for (const word of words) {
    while (
      position < all.length &&
      !(all[position]!.text === word.text && all[position]!.mode === word.mode && all[position]!.category === word.category)
    ) {
      position++;
    }

    if (position === all.length) {
      return false;
    }

    position++;
  }

  return true;
}

/** The kind rules from the data model that a matching case's recorded `expected` breaks. */
export function checkKindRules(corpusCase: CorpusCase): string[] {
  const violations: string[] = [];
  const expected = corpusCase.json.expected;
  if (!isObject(expected) || !['ordinary', 'must-match', 'robustness'].includes(corpusCase.kind)) {
    return violations;
  }

  const contains = expected.containsProfanity === true;

  if (corpusCase.kind === 'must-match' && !contains) {
    violations.push('must-match requires containsProfanity to be true');
  }

  if (corpusCase.kind === 'ordinary') {
    if (contains) {
      violations.push('ordinary requires containsProfanity to be false');
    }

    if (expected.firstMatch !== null && expected.firstMatch !== undefined) {
      violations.push('ordinary requires firstMatch to be null');
    }

    if (!Array.isArray(expected.matches) || expected.matches.length !== 0) {
      violations.push('ordinary requires matches to be empty');
    }

    const input = buildInput(corpusCase.json.input) ?? '';
    const censored = expected.censored;
    if (censored === undefined || buildInput(censored) !== input) {
      violations.push('ordinary requires censored to equal the input');
    }

    if (isObject(expected.censoredWith)) {
      for (const [mask, value] of Object.entries(expected.censoredWith)) {
        if (buildInput(value) !== input) {
          violations.push(`ordinary requires censoredWith["${mask}"] to equal the input`);
        }
      }
    }
  }

  return violations;
}
