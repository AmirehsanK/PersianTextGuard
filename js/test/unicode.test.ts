// The .NET-versus-JavaScript differences measured in spec 003, research R1, pinned as tests.
import { describe, expect, test } from 'vitest';
import {
  categoryAt,
  categoryOfUnit,
  isLetter,
  isLetterOrDigit,
  isWhiteSpace,
  nfkc,
  noncharacterLengthAt,
  toLowerInvariant,
} from '../src/unicode.ts';

describe('isWhiteSpace matches .NET char.IsWhiteSpace, not \\s', () => {
  test.each([
    [0x20, true],
    [0x09, true],
    [0x0d, true],
    [0x85, true], // NEL: .NET whitespace, not \s
    [0xa0, true],
    [0x2028, true],
    [0x3000, true],
    [0xfeff, false], // BOM: \s, not .NET whitespace
    [0x200b, false],
    [0x41, false],
  ])('U+%s → %s', (unit, expected) => {
    expect(isWhiteSpace(unit)).toBe(expected);
  });
});

describe('toLowerInvariant matches .NET char.ToLowerInvariant', () => {
  test('U+0130 İ stays unchanged instead of expanding to two units', () => {
    expect(toLowerInvariant(0x130)).toBe(0x130);
  });

  test('Σ lower-cases to σ, never the final form ς', () => {
    expect(toLowerInvariant(0x3a3)).toBe(0x3c3);
  });

  test('ASCII', () => {
    expect(toLowerInvariant(0x41)).toBe(0x61);
    expect(toLowerInvariant(0x61)).toBe(0x61);
  });
});

describe('categories', () => {
  test('per UTF-16 unit, a lone surrogate is Cs', () => {
    expect(categoryOfUnit(0xd83d)).toBe('Cs');
    expect(categoryOfUnit(0xdc00)).toBe('Cs');
  });

  test('Persian letters, marks and digits', () => {
    expect(categoryOfUnit(0x0643)).toBe('Lo');
    expect(categoryOfUnit(0x064b)).toBe('Mn');
    expect(categoryOfUnit(0x06f1)).toBe('Nd');
    expect(isLetter(0x06a9)).toBe(true);
    expect(isLetterOrDigit(0x06f1)).toBe(true);
    expect(isLetterOrDigit(0x060c)).toBe(false);
  });

  test('per code point at an index reads a surrogate pair as one character', () => {
    expect(categoryAt('😀', 0)).toBe('So');
    expect(categoryAt('😀', 1)).toBe('Cs');
  });

  test('unassigned is Cn', () => {
    expect(categoryOfUnit(0x0378)).toBe('Cn');
  });
});

describe('nfkc', () => {
  test('folds presentation forms', () => {
    expect(nfkc('ﻛﻴﺮ')).toBe('كير');
  });

  test('keeps noncharacters and normalizes around them', () => {
    expect(nfkc('a￾ﻛ')).toBe('a￾ك');
    expect(nfkc('ﻛﻴ﷐ﺮ')).toBe('كي﷐ر');
  });

  test('never throws for any of the 66 noncharacters', () => {
    const noncharacters: number[] = [];
    for (let cp = 0xfdd0; cp <= 0xfdef; cp++) noncharacters.push(cp);
    for (let plane = 0; plane <= 0x10; plane++) noncharacters.push((plane << 16) | 0xfffe, (plane << 16) | 0xffff);

    expect(noncharacters).toHaveLength(66);
    for (const cp of noncharacters) {
      const text = `ﻛ ${String.fromCodePoint(cp)} ﻛ`;
      expect(nfkc(text)).toBe(`ك ${String.fromCodePoint(cp)} ك`);
    }
  });
});

describe('noncharacterLengthAt', () => {
  test.each([
    ['￾', 0, 1],
    ['﷐', 0, 1],
    ['🿾', 0, 2],
    ['􏿿', 0, 2],
    ['a', 0, 0],
    ['😀', 0, 0],
  ])('%j at %i → %i', (text, index, expected) => {
    expect(noncharacterLengthAt(text, index)).toBe(expected);
  });
});
