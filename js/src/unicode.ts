/**
 * Unicode primitives with the semantics of the .NET APIs the matcher was written against.
 *
 * Every Unicode-dependent operation in the port goes through this module (lint enforces it).
 * Differences from JavaScript's defaults were measured against .NET 10 on every code point
 * (spec 003, research R1):
 *
 * - whitespace is .NET's `char.IsWhiteSpace` set, not `\s` (U+0085 is whitespace, U+FEFF is not);
 * - lower-casing is one UTF-16 unit at a time, and leaves a unit alone when JavaScript would expand
 *   it (U+0130 stays U+0130), so there is no final-sigma or length change;
 * - categories are per UTF-16 unit where .NET tests a `char`, and per code point where .NET uses
 *   `GetUnicodeCategory(string, index)`;
 * - NFKC runs between Unicode noncharacters, which pass through unchanged (research R2).
 *
 * @packageDocumentation
 */

/** A Unicode general category, as the two-letter code (`'Lu'`, `'Mn'`, …; `'Cn'` when unassigned). */
export type GeneralCategory =
  | 'Lu' | 'Ll' | 'Lt' | 'Lm' | 'Lo'
  | 'Mn' | 'Mc' | 'Me'
  | 'Nd' | 'Nl' | 'No'
  | 'Zs' | 'Zl' | 'Zp'
  | 'Cc' | 'Cf' | 'Cs' | 'Co' | 'Cn'
  | 'Pc' | 'Pd' | 'Ps' | 'Pe' | 'Pi' | 'Pf' | 'Po'
  | 'Sm' | 'Sc' | 'Sk' | 'So';

// Every category except 'Cn', which is what remains when none matches. Codes are position + 1, so a
// cache entry of 0 means "not computed yet".
const TESTED: readonly GeneralCategory[] = [
  'Lo', 'Ll', 'Lu', 'Mn', 'Po', 'Nd', 'Zs', 'So', 'Cf', 'Cc', 'Lm', 'Lt', 'Mc', 'Me', 'Nl', 'No',
  'Zl', 'Zp', 'Cs', 'Co', 'Pc', 'Pd', 'Ps', 'Pe', 'Pi', 'Pf', 'Sm', 'Sc', 'Sk',
];

const PATTERNS: readonly RegExp[] = TESTED.map(name => new RegExp(`^\\p{gc=${name}}$`, 'u'));

const UNASSIGNED = TESTED.length + 1;

let unitCache: Uint8Array | undefined;

function classify(text: string): number {
  for (let i = 0; i < PATTERNS.length; i++) {
    if (PATTERNS[i]!.test(text)) {
      return i + 1;
    }
  }

  return UNASSIGNED;
}

function nameOf(code: number): GeneralCategory {
  return code === UNASSIGNED ? 'Cn' : TESTED[code - 1]!;
}

/**
 * The general category of one UTF-16 code unit, like .NET `CharUnicodeInfo.GetUnicodeCategory(char)`.
 * A lone surrogate unit is `'Cs'`.
 */
export function categoryOfUnit(unit: number): GeneralCategory {
  const cache = (unitCache ??= new Uint8Array(0x10000));
  let code = cache[unit]!;
  if (code === 0) {
    code = classify(String.fromCharCode(unit));
    cache[unit] = code;
  }

  return nameOf(code);
}

/**
 * The general category of the code point at `index`, like .NET
 * `CharUnicodeInfo.GetUnicodeCategory(string, int)`: a valid surrogate pair is read as one code point.
 */
export function categoryAt(text: string, index: number): GeneralCategory {
  const unit = text.charCodeAt(index);
  if (isHighSurrogate(unit) && index + 1 < text.length && isLowSurrogate(text.charCodeAt(index + 1))) {
    return nameOf(classify(text.substring(index, index + 2)));
  }

  return categoryOfUnit(unit);
}

/** Whether a unit is a letter (L*), like .NET `char.IsLetter`. */
export function isLetter(unit: number): boolean {
  if (unit < 0x80) {
    return (unit >= 0x41 && unit <= 0x5a) || (unit >= 0x61 && unit <= 0x7a);
  }

  const category = categoryOfUnit(unit);
  return category === 'Lu' || category === 'Ll' || category === 'Lt' || category === 'Lm' || category === 'Lo';
}

/** Whether a unit is a letter (L*) or a decimal digit (Nd), like .NET `char.IsLetterOrDigit`. */
export function isLetterOrDigit(unit: number): boolean {
  if (unit < 0x80) {
    return (unit >= 0x30 && unit <= 0x39) || (unit >= 0x41 && unit <= 0x5a) || (unit >= 0x61 && unit <= 0x7a);
  }

  return isLetter(unit) || categoryOfUnit(unit) === 'Nd';
}

/** Whether a unit is a control character (Cc), like .NET `char.IsControl`. */
export function isControl(unit: number): boolean {
  return unit <= 0x1f || (unit >= 0x7f && unit <= 0x9f);
}

/**
 * Whether a unit is whitespace, like .NET `char.IsWhiteSpace`: categories Zs, Zl and Zp, plus
 * U+0009–U+000D, U+0085 and U+00A0. Unlike JavaScript's `\s`, U+0085 is whitespace and U+FEFF is not.
 */
export function isWhiteSpace(unit: number): boolean {
  if (unit < 0x80) {
    return unit === 0x20 || (unit >= 0x09 && unit <= 0x0d);
  }

  if (unit === 0x85 || unit === 0xa0) {
    return true;
  }

  const category = categoryOfUnit(unit);
  return category === 'Zs' || category === 'Zl' || category === 'Zp';
}

/** Whether a unit is a high or low surrogate, like .NET `char.IsSurrogate`. */
export function isSurrogate(unit: number): boolean {
  return unit >= 0xd800 && unit <= 0xdfff;
}

/** Whether a unit is a high (leading) surrogate, like .NET `char.IsHighSurrogate`. */
export function isHighSurrogate(unit: number): boolean {
  return unit >= 0xd800 && unit <= 0xdbff;
}

/** Whether a unit is a low (trailing) surrogate, like .NET `char.IsLowSurrogate`. */
export function isLowSurrogate(unit: number): boolean {
  return unit >= 0xdc00 && unit <= 0xdfff;
}

/**
 * Lower-cases one UTF-16 unit, like .NET `char.ToLowerInvariant`: when JavaScript's lower-casing
 * would not give exactly one unit (U+0130 `İ` gives two), the unit is returned unchanged.
 */
export function toLowerInvariant(unit: number): number {
  if (unit < 0x80) {
    return unit >= 0x41 && unit <= 0x5a ? unit + 0x20 : unit;
  }

  const lower = String.fromCharCode(unit).toLowerCase();
  return lower.length === 1 ? lower.charCodeAt(0) : unit;
}

/** Whether a code point is a Unicode noncharacter: U+FDD0–U+FDEF, or one ending in FFFE or FFFF. */
export function isNoncharacter(codePoint: number): boolean {
  return (codePoint >= 0xfdd0 && codePoint <= 0xfdef) || (codePoint & 0xfffe) === 0xfffe;
}

/**
 * The length (0, 1 or 2 units) of a noncharacter starting at `index`, or 0 when there is none.
 */
export function noncharacterLengthAt(text: string, index: number): number {
  const unit = text.charCodeAt(index);
  if (isHighSurrogate(unit) && index + 1 < text.length) {
    const low = text.charCodeAt(index + 1);
    if (isLowSurrogate(low)) {
      const codePoint = 0x10000 + ((unit - 0xd800) << 10) + (low - 0xdc00);
      return isNoncharacter(codePoint) ? 2 : 0;
    }
  }

  return isNoncharacter(unit) ? 1 : 0;
}

/**
 * NFKC, like .NET `string.Normalize(NormalizationForm.FormKC)` as PersianTextGuard applies it:
 * text between noncharacters is normalized and each noncharacter is copied unchanged. A noncharacter
 * composes with nothing, so the result equals whole-string NFKC. Lone surrogates must already have
 * been replaced by the caller.
 */
export function nfkc(text: string): string {
  let start = 0;
  let result = '';
  for (let i = 0; i < text.length; ) {
    const length = noncharacterLengthAt(text, i);
    if (length === 0) {
      i++;
      continue;
    }

    if (i > start) {
      result += text.substring(start, i).normalize('NFKC');
    }

    result += text.substring(i, i + length);
    i += length;
    start = i;
  }

  return start === 0 ? text.normalize('NFKC') : result + text.substring(start).normalize('NFKC');
}

/** Whether `text` is already in NFKC, like .NET `string.IsNormalized(NormalizationForm.FormKC)`. */
export function isNfkc(text: string): boolean {
  return nfkc(text) === text;
}

/** NFD, like .NET `string.Normalize(NormalizationForm.FormD)`. */
export function nfd(text: string): string {
  return text.normalize('NFD');
}
