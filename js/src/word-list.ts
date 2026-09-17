// Port of dotnet/src/PersianTextGuard/WordList.cs.
import { BUNDLED_WORD_LISTS } from './generated/wordlists';
import { WORD_CATEGORIES, WordListFormatError } from './types';
import type { ResolvedWord, WordCategory } from './types';
import { isLetter, isWhiteSpace, toLowerInvariant } from './unicode';

/** Trims whitespace like .NET `string.Trim()`, using .NET's whitespace set (U+FEFF is not trimmed). */
export function trimDotNet(value: string): string {
  let start = 0;
  let end = value.length;
  while (start < end && isWhiteSpace(value.charCodeAt(start))) {
    start++;
  }

  while (end > start && isWhiteSpace(value.charCodeAt(end - 1))) {
    end--;
  }

  return start === 0 && end === value.length ? value : value.substring(start, end);
}

function lowerInvariant(value: string): string {
  let result = '';
  for (let i = 0; i < value.length; i++) {
    result += String.fromCharCode(toLowerInvariant(value.charCodeAt(i)));
  }

  return result;
}

/**
 * The category a section heading names, or `undefined`. A heading is a category name only, in any
 * letter case; numbers are not names (spec 003, FR-029).
 */
function categoryNamed(name: string): WordCategory | undefined {
  if (name.length === 0) {
    return undefined;
  }

  for (let i = 0; i < name.length; i++) {
    if (!isLetter(name.charCodeAt(i))) {
      return undefined;
    }
  }

  const lower = lowerInvariant(name);
  return WORD_CATEGORIES.find(category => lowerInvariant(category) === lower);
}

function parse(text: string): readonly ResolvedWord[] {
  if (typeof text !== 'string') {
    throw new TypeError('text must be a string');
  }

  const words: ResolvedWord[] = [];
  let category: WordCategory = 'uncategorized';
  let lineNumber = 0;

  for (const raw of text.split('\n')) {
    lineNumber++;
    const line = trimDotNet(raw);
    if (line.length === 0 || line.charCodeAt(0) === 0x23 /* # */) {
      continue;
    }

    if (line.length > 2 && line.charCodeAt(0) === 0x5b /* [ */ && line.charCodeAt(line.length - 1) === 0x5d /* ] */) {
      const name = trimDotNet(line.substring(1, line.length - 1));
      const named = categoryNamed(name);
      if (named === undefined) {
        throw new WordListFormatError(lineNumber, name);
      }

      category = named;
      continue;
    }

    const anywhere = line.charCodeAt(0) === 0x7e; /* ~ */
    const word = trimDotNet(anywhere ? line.substring(1) : line);

    if (word.length > 0) {
      words.push(Object.freeze({ text: word, mode: anywhere ? 'anywhere' : 'wholeWord', category }));
    }
  }

  return Object.freeze(words);
}

let allBundled: readonly ResolvedWord[] | undefined;
let persianDefault: readonly ResolvedWord[] | undefined;

/**
 * Reads word lists for {@link ProfanityFilter}, and gives access to the bundled Persian, Finglish and
 * English lists.
 *
 * @remarks
 * The format is one entry per line: a word or phrase is matched as whole words, a leading `~` matches
 * it anywhere (inside longer words too), and lines starting with `#` are comments. A line like
 * `[insult]` starts a section: every entry after it, until the next section, gets that category. A
 * heading is a category name in any letter case; numbers are not categories. Entries before the first
 * section are `'uncategorized'`.
 */
export const WordList = {
  /**
   * Every bundled entry, `'mild'` included, in list order: Persian, Finglish, English.
   *
   * @remarks
   * Parsed once, on first use; every access returns the same frozen array.
   */
  get all(): readonly ResolvedWord[] {
    return (allBundled ??= Object.freeze(BUNDLED_WORD_LISTS.flatMap(list => parse(list))));
  },

  /**
   * The bundled Persian, Finglish and English list without the `'mild'` entries: profanity, sexual
   * words, insults, slurs and harassment, curated to avoid flagging ordinary words.
   *
   * @remarks
   * Nothing uses it unless you pass it to a filter. The files themselves document what is
   * deliberately left out and why (for example «کس», which also means "person"). Every access returns
   * the same frozen array.
   */
  get persianDefault(): readonly ResolvedWord[] {
    return (persianDefault ??= Object.freeze(WordList.all.filter(word => word.category !== 'mild')));
  },

  /**
   * The bundled entries in the given categories, in list order.
   *
   * @param categories - The categories to include.
   * @returns A new frozen array.
   * @throws `TypeError` when a category name does not exist.
   *
   * @example
   * ```ts
   * // A dating app: sexual words are fine, abuse is not.
   * new ProfanityFilter(WordList.bundled('insult', 'slur', 'harassment'));
   * ```
   */
  bundled(...categories: WordCategory[]): readonly ResolvedWord[] {
    for (const category of categories as unknown[]) {
      if (typeof category !== 'string' || !WORD_CATEGORIES.includes(category as WordCategory)) {
        throw new TypeError(`Unknown word category '${String(category)}'.`);
      }
    }

    return Object.freeze(WordList.all.filter(word => categories.includes(word.category)));
  },

  /**
   * Parses a word list from its text.
   *
   * @remarks
   * Accepts `\n` and `\r\n` line endings. To load a file, read it yourself (for example with
   * `fs.readFileSync(path, 'utf8')` or `await file.text()`) and pass the text.
   *
   * @param text - The word-list text.
   * @returns The entries, in order, as a frozen array of frozen entries.
   * @throws {@link WordListFormatError} when a section names a category that does not exist, and
   * `TypeError` when `text` is not a string.
   */
  parse(text: string): readonly ResolvedWord[] {
    return parse(text);
  },
};
