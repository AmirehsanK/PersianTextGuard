// Port of dotnet/tests/PersianTextGuard.Tests/SourceMapTests.cs, plus normalizer and word-list
// internals the corpus does not reach by construction.
import { describe, expect, test } from 'vitest';
import { fold, squeeze } from '../src/fold.ts';
import { COMPARISON_STEPS, Steps, normalize, normalizeWithMap, resolveSteps, toAsciiDigits, toPersianDigits } from '../src/normalizer.ts';
import { ReadingKind, build, chunkMap, wholeMessageMap } from '../src/source-map.ts';
import { WordListFormatError } from '../src/types.ts';
import { WordList } from '../src/word-list.ts';

const samples = [
  'كتاب‌هاي  ۱۲ ABC',
  'ﻛﻴﺮ',
  'fúck',
  'ú',
  'کــیــر',
  'کِیر',
  'سسسسلام',
  'sh!t 455',
  '\u{1F175}\u{1F184}\u{1F172}\u{1F17A}',
  'f\u{1F595}ck',
  'hi \uD83D',
  '   a \t b  ',
  'ک​یر',
  'ج‏نده',
];

function assertValidMap(map: readonly number[], outputLength: number, sourceLength: number): void {
  expect(map).toHaveLength(outputLength);
  for (let i = 0; i < map.length; i++) {
    expect(map[i]).toBeGreaterThanOrEqual(0);
    expect(map[i]).toBeLessThanOrEqual(sourceLength - 1);
    if (i > 0) {
      expect(map[i]!, `map goes backwards at ${i}`).toBeGreaterThanOrEqual(map[i - 1]!);
    }
  }
}

function expectedReading(text: string, kind: ReadingKind): string {
  const normalized = normalize(text);
  switch (kind) {
    case ReadingKind.Normalized:
      return normalized;
    case ReadingKind.Squeezed:
      return squeeze(normalized);
    case ReadingKind.Folded:
      return fold(normalized);
    default:
      return squeeze(fold(normalized));
  }
}

describe('source maps (SourceMapTests)', () => {
  test.each(samples)('mapped normalization produces the same text: %j', text => {
    const map: number[] = [];
    const mapped = normalizeWithMap(text, COMPARISON_STEPS, map);
    expect(mapped).toBe(normalize(text));
    assertValidMap(map, mapped.length, text.length);
  });

  test.each(samples)('mapped fold and squeeze produce the same text: %j', text => {
    const normalized = normalize(text);

    const foldMap: number[] = [];
    const folded = fold(normalized, foldMap);
    expect(folded).toBe(fold(normalized));
    assertValidMap(foldMap, folded.length, normalized.length);

    const squeezeMap: number[] = [];
    const squeezed = squeeze(normalized, squeezeMap);
    expect(squeezed).toBe(squeeze(normalized));
    assertValidMap(squeezeMap, squeezed.length, normalized.length);
  });

  test.each(samples)('every reading maps back into the original: %j', text => {
    for (const kind of [ReadingKind.Normalized, ReadingKind.Squeezed, ReadingKind.Folded, ReadingKind.FoldedSqueezed]) {
      const mapped = build(text, kind);
      expect(mapped.text).toBe(expectedReading(text, kind));
      assertValidMap(mapped.startMap, mapped.text.length, text.length);
      assertValidMap(mapped.endMap, mapped.text.length, text.length);
    }
  });

  test('presentation forms map one to one', () => {
    const map: number[] = [];
    expect(normalizeWithMap('ﻛﻴﺮ', COMPARISON_STEPS, map)).toBe('کیر');
    expect(map).toEqual([0, 1, 2]);
  });

  test('removed invisible characters are skipped in the map', () => {
    const map: number[] = [];
    expect(normalizeWithMap('ک​یر', COMPARISON_STEPS, map)).toBe('کیر');
    expect(map).toEqual([0, 2, 3]);
  });

  test('collapsed repeats map to the letters they kept', () => {
    const text = 'سسسسلام';
    const map: number[] = [];
    const normalized = normalizeWithMap(text, COMPARISON_STEPS, map);
    for (let i = 0; i < normalized.length; i++) {
      expect(normalized[i]).toBe(text[map[i]!]);
    }
  });

  test('fallback maps only ever widen', () => {
    const text = 'ab kir cd';
    const chunk = chunkMap(text, text);
    expect(chunk.startMap[3]!).toBeLessThanOrEqual(3);
    expect(chunk.endMap[5]!).toBeGreaterThanOrEqual(5);

    const whole = wholeMessageMap(text, text);
    expect(whole.startMap[3]).toBe(0);
    expect(whole.endMap[5]).toBe(text.length - 1);
  });

  test('a chunk count mismatch falls back to the whole message', () => {
    const mapped = chunkMap('ab kir', 'abkir x y');
    expect(mapped.startMap.every(start => start === 0)).toBe(true);
    expect(mapped.endMap.every(end => end === 5)).toBe(true);
  });

  test('a noncharacter keeps its place in the map', () => {
    const map: number[] = [];
    const text = 'ﻛ￾ﺮ';
    const normalized = normalizeWithMap(text, COMPARISON_STEPS, map);
    expect(normalized).toBe(normalize(text));
    assertValidMap(map, normalized.length, text.length);
  });
});

describe('normalizer internals', () => {
  test('null and undefined become empty', () => {
    expect(normalize(null)).toBe('');
    expect(normalize(undefined)).toBe('');
    expect(toPersianDigits(null)).toBe('');
    expect(toAsciiDigits(undefined)).toBe('');
  });

  test('digit helpers leave letters alone', () => {
    expect(toPersianDigits('2 ساعت, KR1')).toBe('۲ ساعت, KR۱');
    expect(toAsciiDigits('۲ KR١')).toBe('2 KR1');
  });

  test('step names resolve to the .NET flag bits', () => {
    expect(resolveSteps(['unifyLetters'])).toBe(Steps.UnifyLetters);
    expect(resolveSteps(['unifyLetters', 'lowerCase'])).toBe(Steps.UnifyLetters | Steps.LowerCase);
    expect(resolveSteps('comparison')).toBe(1023);
    expect(resolveSteps('standard')).toBe(299);
    expect(resolveSteps('none')).toBe(0);
  });

  test('unknown presets and steps throw TypeError', () => {
    // @ts-expect-error: deliberately invalid
    expect(() => resolveSteps('bogus')).toThrow(TypeError);
    // @ts-expect-error: deliberately invalid
    expect(() => resolveSteps(['unifyLetters', 'shout'])).toThrow(TypeError);
  });

  test('a very long message normalizes without overflowing', () => {
    const text = 'سلام hello. '.repeat(20000);
    const map: number[] = [];
    const normalized = normalizeWithMap(text, COMPARISON_STEPS, map);
    expect(normalized).toBe(normalize(text));
    expect(map).toHaveLength(normalized.length);
  });
});

describe('word-list headings are names only (FR-029)', () => {
  test.each(['[3]\nword\n', '[+4]\nword\n', '[ 03 ]\nword\n'])('%j is an unknown category on line 1', text => {
    let error: unknown;
    try {
      WordList.parse(text);
    } catch (e) {
      error = e;
    }

    expect(error).toBeInstanceOf(WordListFormatError);
    expect((error as WordListFormatError).line).toBe(1);
  });

  test('names in any case, with spaces around them', () => {
    expect(WordList.parse('[ Insult ]\nword\n[SLUR]\n~other\n')).toEqual([
      { text: 'word', mode: 'wholeWord', category: 'insult' },
      { text: 'other', mode: 'anywhere', category: 'slur' },
    ]);
  });

  test('a two-character line is a word, not a section, as in .NET', () => {
    expect(WordList.parse('[]\n')).toEqual([{ text: '[]', mode: 'wholeWord', category: 'uncategorized' }]);
  });

  test('the bundled lists parse and default excludes mild', () => {
    expect(WordList.all).toHaveLength(1250);
    expect(WordList.persianDefault).toHaveLength(1025);
    expect(WordList.persianDefault.some(word => word.category === 'mild')).toBe(false);
  });
});
