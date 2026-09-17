// JavaScript-specific behaviour and the spec's User Story 1 scenarios (spec 003, FR-019; public API
// contract G2, G4–G8).
import { describe, expect, test } from 'vitest';
import {
  ProfanityFilter,
  WordList,
  WordListFormatError,
  normalize,
  tokenize,
} from '../src/index.ts';
import type { BannedWord, ProfanityMatch } from '../src/index.ts';

const filter = new ProfanityFilter(WordList.persianDefault);

describe('User Story 1 acceptance scenarios', () => {
  test('1: spaced letters are caught', () => {
    expect(filter.containsProfanity('f u c k')).toBe(true);
  });

  test('2: an ordinary Persian message passes untouched', () => {
    const text = 'هر کس پلات بالاست پیام بده';
    expect(filter.findMatch(text)).toBeNull();
    expect(filter.findMatches(text)).toEqual([]);
    expect(filter.censor(text)).toBe(text);
  });

  test('3: every match, in order, with category, evasion and position', () => {
    const matches = filter.findMatches('sh1t and f u c k');
    expect(matches.map(m => [m.word.text, m.word.category, m.evasion, m.index, m.length])).toEqual([
      ['shit', 'profanity', ['lookalikeCharacters'], 0, 4],
      ['fuck', 'profanity', ['splitWord'], 9, 7],
    ]);
  });

  test('4: positions are JavaScript string units, so slice gives the word', () => {
    const text = '😀 کیر';
    const match = filter.findMatch(text)!;
    expect([match.index, match.length]).toEqual([3, 3]);
    expect(text.slice(match.index, match.index + match.length)).toBe('کیر');
  });

  test('5: your own words', () => {
    const own = new ProfanityFilter([{ text: 'اسپم' }, { text: 'casino', mode: 'anywhere' }]);
    expect(own.containsProfanity('onlinecasino.example')).toBe(true);
    expect(own.containsProfanity('این پیام اسپم است')).toBe(true);
  });

  test('6: a chosen mask', () => {
    expect(filter.censor('this is kir', '#')).toBe('this is ####');
    expect(filter.censor('kir and motherfucker')).toBe('**** and ****');
  });
});

describe('values that are not text throw TypeError (G2)', () => {
  const notText: unknown[] = [42, {}, ['kir'], true];

  test.each(notText)('%j', value => {
    const v = value as string;
    expect(() => filter.containsProfanity(v)).toThrow(TypeError);
    expect(() => filter.findMatch(v)).toThrow(TypeError);
    expect(() => filter.findMatches(v)).toThrow(TypeError);
    expect(() => filter.censor(v)).toThrow(TypeError);
    expect(() => normalize(v)).toThrow(TypeError);
    expect(() => tokenize(v)).toThrow(TypeError);
  });

  test('undefined behaves like null', () => {
    for (const missing of [null, undefined]) {
      expect(filter.containsProfanity(missing)).toBe(false);
      expect(filter.findMatch(missing)).toBeNull();
      expect(filter.findMatches(missing)).toEqual([]);
      expect(filter.censor(missing)).toBe('');
      expect(normalize(missing)).toBe('');
      expect(tokenize(missing)).toEqual([]);
    }
  });
});

describe('masks (G6)', () => {
  test.each(['x', '5', ' ', '\n', '\uD83D', '##', ''])('%j is refused with RangeError, even for null text', mask => {
    expect(() => filter.censor('kir', mask)).toThrow(RangeError);
    expect(() => filter.censor(null, mask)).toThrow(RangeError);
  });

  test('a mask that is not a string is a TypeError', () => {
    expect(() => filter.censor('kir', 5 as unknown as string)).toThrow(TypeError);
  });

  test.each(['*', '#', '-', '•'])('%j is accepted', mask => {
    expect(filter.censor('kir', mask)).toBe(mask.repeat(4));
  });
});

describe('construction', () => {
  test('missing or non-iterable words throw TypeError', () => {
    expect(() => new ProfanityFilter(undefined as unknown as BannedWord[])).toThrow(TypeError);
    expect(() => new ProfanityFilter(5 as unknown as BannedWord[])).toThrow(TypeError);
    expect(() => new ProfanityFilter('kir' as unknown as BannedWord[])).toThrow(TypeError);
  });

  test('malformed entries throw TypeError', () => {
    expect(() => new ProfanityFilter([{ text: 5 } as unknown as BannedWord])).toThrow(TypeError);
    expect(() => new ProfanityFilter([{ text: 'x', mode: 'sometimes' } as unknown as BannedWord])).toThrow(TypeError);
    expect(() => new ProfanityFilter([{ text: 'x', category: 'rude' } as unknown as BannedWord])).toThrow(TypeError);
    expect(() => new ProfanityFilter([null as unknown as BannedWord])).toThrow(TypeError);
  });

  test('duplicate spellings and blank entries count once', () => {
    expect(new ProfanityFilter([{ text: 'كص' }, { text: 'کص' }, { text: '  کص ' }, { text: '' }]).count).toBe(1);
  });

  test('an empty list is clean', () => {
    expect(new ProfanityFilter([]).findMatch('anything')).toBeNull();
  });

  test('any iterable of entries works', () => {
    const set = new Set<BannedWord>([{ text: 'damn' }]);
    expect(new ProfanityFilter(set).containsProfanity('daaaamn')).toBe(true);
  });
});

describe('immutability (G7)', () => {
  test('changing entries or options after building changes nothing', () => {
    const entry: { text: string; mode: 'wholeWord' | 'anywhere' } = { text: 'shit', mode: 'wholeWord' };
    const options = { joinSpacedLetters: true };
    const own = new ProfanityFilter([entry], options);

    entry.text = 'push';
    entry.mode = 'anywhere';
    options.joinSpacedLetters = false;

    expect(own.containsProfanity('s h i t')).toBe(true);
    expect(own.containsProfanity('push it')).toBe(false);
    expect(own.findMatch('shit')!.word).toEqual({ text: 'shit', mode: 'wholeWord', category: 'uncategorized' });
  });

  test('results are frozen', () => {
    const matches = filter.findMatches('kir and fuck');
    expect(Object.isFrozen(matches)).toBe(true);
    for (const match of matches) {
      expect(Object.isFrozen(match)).toBe(true);
      expect(Object.isFrozen(match.word)).toBe(true);
      expect(Object.isFrozen(match.evasion)).toBe(true);
    }

    expect(Object.isFrozen(filter.findMatches('clean'))).toBe(true);
  });
});

describe('bundled lists (G8)', () => {
  test('the same instances every time, and default has no mild entry', () => {
    expect(WordList.all).toBe(WordList.all);
    expect(WordList.persianDefault).toBe(WordList.persianDefault);
    expect(WordList.persianDefault.some(word => word.category === 'mild')).toBe(false);
  });

  test('an unknown category passed to bundled throws TypeError', () => {
    expect(() => WordList.bundled('rude' as 'slur')).toThrow(TypeError);
  });

  test('categories can be picked', () => {
    const slurs = new ProfanityFilter(WordList.bundled('slur'));
    expect(slurs.containsProfanity('faggot')).toBe(true);
    expect(slurs.containsProfanity('fuck')).toBe(false);
  });
});

describe('word-list parsing errors', () => {
  test('an unknown category is a WordListFormatError with its line', () => {
    let error: unknown;
    try {
      WordList.parse('[nonsense]\nword\n');
    } catch (e) {
      error = e;
    }

    expect(error).toBeInstanceOf(WordListFormatError);
    expect(error).toBeInstanceOf(Error);
    expect((error as WordListFormatError).line).toBe(1);
    expect((error as WordListFormatError).message).toBe("Line 1: unknown word category 'nonsense'.");
  });

  test('a non-string throws TypeError', () => {
    expect(() => WordList.parse(5 as unknown as string)).toThrow(TypeError);
  });
});

describe('the four capabilities agree (G4, G5)', () => {
  const inputs: (string | null)[] = [
    null,
    '',
    '   ',
    'kir kir kir',
    'hi \uD83D kir',
    'hi ￾ kir',
    'سلام، سفارشم کی میرسه؟',
    'you bitch, kos kesh',
    'sh1t and f u c k',
    'پدر سگ پدر',
    'k kos i kos r',
    'جنده‌ها رو ببین',
  ];

  test.each(inputs)('%j', text => {
    const contains = filter.containsProfanity(text);
    const first = filter.findMatch(text);
    const all: readonly ProfanityMatch[] = filter.findMatches(text);

    expect(first !== null).toBe(contains);
    expect(all.length > 0).toBe(contains);

    const length = text?.length ?? 0;
    for (let i = 0; i < all.length; i++) {
      expect(all[i]!.index).toBeGreaterThanOrEqual(0);
      expect(all[i]!.length).toBeGreaterThanOrEqual(1);
      expect(all[i]!.index + all[i]!.length).toBeLessThanOrEqual(length);
      if (i > 0) {
        expect(all[i - 1]!.index + all[i - 1]!.length).toBeLessThanOrEqual(all[i]!.index);
      }
    }

    if (text !== null) {
      expect(filter.censor(text) !== text).toBe(contains);
      expect(filter.containsProfanity(filter.censor(text))).toBe(false);
    }
  });
});
