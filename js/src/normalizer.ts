// Port of dotnet/src/PersianTextGuard/PersianNormalizer.cs and PersianNormalization.cs.
import type { NormalizationStep, NormalizationSteps } from './types';
import {
  categoryAt,
  isHighSurrogate,
  isLetter,
  isLetterOrDigit,
  isLowSurrogate,
  isNfkc,
  isWhiteSpace,
  nfkc,
  toLowerInvariant,
} from './unicode';

/** The .NET `[Flags]` enum `PersianNormalization`, with the same bit values. */
export const Steps = {
  None: 0,
  CompatibilityForms: 1 << 0,
  UnifyLetters: 1 << 1,
  RemoveDiacritics: 1 << 2,
  RemoveTatweel: 1 << 3,
  RemoveZeroWidth: 1 << 4,
  RemoveBidiControls: 1 << 5,
  AsciiDigits: 1 << 6,
  LowerCase: 1 << 7,
  CollapseWhitespace: 1 << 8,
  CollapseRepeats: 1 << 9,
} as const;

const STANDARD =
  Steps.CompatibilityForms | Steps.UnifyLetters | Steps.RemoveTatweel | Steps.RemoveBidiControls | Steps.CollapseWhitespace;

const COMPARISON =
  STANDARD | Steps.RemoveDiacritics | Steps.RemoveZeroWidth | Steps.AsciiDigits | Steps.LowerCase | Steps.CollapseRepeats;

/** The bits of the `'comparison'` preset, the form the filter searches. */
export const COMPARISON_STEPS = COMPARISON;

const STEP_BITS: Readonly<Record<NormalizationStep, number>> = {
  compatibilityForms: Steps.CompatibilityForms,
  unifyLetters: Steps.UnifyLetters,
  removeDiacritics: Steps.RemoveDiacritics,
  removeTatweel: Steps.RemoveTatweel,
  removeZeroWidth: Steps.RemoveZeroWidth,
  removeBidiControls: Steps.RemoveBidiControls,
  asciiDigits: Steps.AsciiDigits,
  lowerCase: Steps.LowerCase,
  collapseWhitespace: Steps.CollapseWhitespace,
  collapseRepeats: Steps.CollapseRepeats,
};

const PERSIAN_YEH = 0x06cc;
const PERSIAN_KEHEH = 0x06a9;
const HEH = 0x0647;
const ALEF = 0x0627;

const ARABIC_INDIC_ZERO = 0x0660;
const EXTENDED_ARABIC_INDIC_ZERO = 0x06f0;

const PERSIAN_DIGITS = ['۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹'];

/** A token and where it starts and ends (exclusive) in the text it came from. */
export interface Token {
  /** The token. */
  readonly text: string;
  /** Its first unit in the text. */
  readonly start: number;
  /** One past its last unit in the text. */
  readonly end: number;
}

/**
 * Throws the port's `TypeError` unless `value` is a string, `null` or `undefined` (spec FR-013).
 * Returns the value typed as text.
 */
export function assertText(value: unknown, name: string): string | null | undefined {
  if (value === null || value === undefined || typeof value === 'string') {
    return value;
  }

  throw new TypeError(`${name} must be a string, null or undefined`);
}

/** The step bits for a preset or a list of step names; an unknown name throws `TypeError`. */
export function resolveSteps(steps: NormalizationSteps): number {
  if (typeof steps === 'string') {
    switch (steps) {
      case 'comparison':
        return COMPARISON;
      case 'standard':
        return STANDARD;
      case 'none':
        return Steps.None;
      default:
        throw new TypeError(`Unknown normalization preset '${String(steps)}'.`);
    }
  }

  if (!Array.isArray(steps)) {
    throw new TypeError("steps must be 'comparison', 'standard', 'none' or an array of step names");
  }

  let bits = 0;
  for (const step of steps as readonly unknown[]) {
    const bit = typeof step === 'string' && Object.prototype.hasOwnProperty.call(STEP_BITS, step)
      ? STEP_BITS[step as NormalizationStep]
      : undefined;
    if (bit === undefined) {
      throw new TypeError(`Unknown normalization step '${String(step)}'.`);
    }

    bits |= bit;
  }

  return bits;
}

/**
 * Normalizes Persian text so that strings which look the same compare the same.
 *
 * @remarks
 * Persian is written with character pairs that render near-identically but have different code
 * points (Arabic yeh and Persian yeh, Arabic kaf and keheh), plus optional diacritics, invisible
 * zero-width and bidi characters, and three digit ranges. A raw comparison against a word list or a
 * search index is defeated by typing one character differently.
 *
 * Never throws for a string, including lone surrogates and noncharacters. `null` and `undefined`
 * give `''`.
 *
 * @param text - The text to normalize.
 * @param steps - `'comparison'` (the default), `'standard'`, `'none'`, or a list of steps.
 * @returns The normalized text.
 * @throws `TypeError` when `text` is not a string, `null` or `undefined`, or `steps` names an unknown
 * preset or step.
 *
 * @example
 * ```ts
 * normalize('كتاب‌هاي  ۱۲ ABC');             // 'کتابهای 12 abc'
 * normalize('كتاب‌هاي  ۱۲ ABC', 'standard'); // 'کتاب‌های ۱۲ ABC'
 * ```
 */
export function normalize(text: string | null | undefined, steps: NormalizationSteps = 'comparison'): string {
  assertText(text, 'text');
  return normalizeWithMap(text, resolveSteps(steps), null);
}

/**
 * {@link normalize} with step bits, also recording in `map`, for every unit of the result, the index
 * in `text` it came from.
 */
export function normalizeWithMap(text: string | null | undefined, steps: number, map: number[] | null): string {
  if (text === null || text === undefined || text.length === 0) {
    return '';
  }

  // string.Normalize throws on a lone surrogate in .NET, which is what a message cut in the middle
  // of an emoji contains; the .NET port replaces them first, and so does this one, so both ports
  // normalize the same text.
  let source: string;
  let sourceMap: number[] | null = null;
  if ((steps & Steps.CompatibilityForms) === 0) {
    source = text;
  } else if (map === null) {
    source = nfkc(replaceLoneSurrogates(text));
  } else {
    sourceMap = [];
    source = normalizeCompatibilityBySegment(replaceLoneSurrogates(text), sourceMap);
  }

  const out: number[] = [];
  const stepMap: number[] | null = map === null ? null : [];
  const unify = (steps & Steps.UnifyLetters) !== 0;
  const lowerCase = (steps & Steps.LowerCase) !== 0;
  const asciiDigits = (steps & Steps.AsciiDigits) !== 0;

  for (let index = 0; index < source.length; index++) {
    const raw = source.charCodeAt(index);
    if (isRemoved(raw, steps)) {
      continue;
    }

    stepMap?.push(sourceMap === null ? index : sourceMap[index]!);

    let c = unify ? unifyLetter(raw) : raw;

    if (asciiDigits) {
      if (c >= ARABIC_INDIC_ZERO && c <= ARABIC_INDIC_ZERO + 9) {
        c = 0x30 + (c - ARABIC_INDIC_ZERO);
      } else if (c >= EXTENDED_ARABIC_INDIC_ZERO && c <= EXTENDED_ARABIC_INDIC_ZERO + 9) {
        c = 0x30 + (c - EXTENDED_ARABIC_INDIC_ZERO);
      }
    }

    out.push(lowerCase ? toLowerInvariant(c) : c);
  }

  let result = fromUnits(out);

  if ((steps & Steps.CollapseRepeats) !== 0) {
    result = collapseRepeats(result, stepMap);
  }

  if ((steps & Steps.CollapseWhitespace) !== 0) {
    result = collapseWhitespace(result, stepMap);
  }

  if (map !== null) {
    for (const source of stepMap!) {
      map.push(source);
    }
  }

  return result;
}

/** Builds a string from UTF-16 units without hitting the argument-count limit of `fromCharCode`. */
export function fromUnits(units: readonly number[]): string {
  let result = '';
  for (let i = 0; i < units.length; i += 8192) {
    result += String.fromCharCode(...units.slice(i, i + 8192));
  }

  return result;
}

/** NFKC one segment at a time: a code point and the combining marks that follow it. */
function normalizeCompatibilityBySegment(text: string, map: number[]): string {
  // Most messages are already in NFKC. Then every character maps to itself.
  if (isNfkc(text)) {
    for (let index = 0; index < text.length; index++) {
      map.push(index);
    }

    return text;
  }

  let result = '';
  let i = 0;

  while (i < text.length) {
    const start = i;
    i += codePointLength(text, i);
    while (i < text.length && isCombiningMark(text, i)) {
      i += codePointLength(text, i);
    }

    // ASCII is already in normal form. A segment starting with a noncharacter is normalized around
    // it by nfkc, which is what the .NET port does to avoid string.Normalize throwing.
    const segment = i - start === 1 && text.charCodeAt(start) < 128
      ? text.substring(start, i)
      : nfkc(text.substring(start, i));

    for (let k = 0; k < segment.length; k++) {
      map.push(start);
    }

    result += segment;
  }

  return result;
}

function codePointLength(text: string, index: number): number {
  return isHighSurrogate(text.charCodeAt(index)) && index + 1 < text.length && isLowSurrogate(text.charCodeAt(index + 1))
    ? 2
    : 1;
}

function isCombiningMark(text: string, index: number): boolean {
  const category = categoryAt(text, index);
  return category === 'Mn' || category === 'Mc' || category === 'Me';
}

/**
 * Splits text into word tokens on whitespace, punctuation (ASCII, Persian «» ، ؛ ؟ ٫, and the rest of
 * Unicode) and symbols, emoji included.
 *
 * @remarks
 * Letters, digits, combining marks and the zero-width non-joiner stay inside tokens. Normalize first:
 * tokens are only as consistent as the text they came from. Never throws for a string; `null` and
 * `undefined` give `[]`.
 *
 * @param text - The text to split.
 * @returns The tokens, in order.
 * @throws `TypeError` when `text` is not a string, `null` or `undefined`.
 *
 * @example
 * ```ts
 * tokenize('سلام، دنیا! خوبی؟'); // ['سلام', 'دنیا', 'خوبی']
 * ```
 */
export function tokenize(text: string | null | undefined): string[] {
  assertText(text, 'text');
  return tokenizeWithOffsets(text).map(token => token.text);
}

/** {@link tokenize}, keeping where each token starts and ends in `text`. */
export function tokenizeWithOffsets(text: string | null | undefined): Token[] {
  const tokens: Token[] = [];
  if (text === null || text === undefined || text.length === 0) {
    return tokens;
  }

  let start = -1;

  for (let i = 0; i <= text.length; i++) {
    if (i < text.length && isWordCharacter(text, i)) {
      if (start < 0) {
        start = i;
      }

      continue;
    }

    if (start >= 0) {
      tokens.push({ text: text.substring(start, i), start, end: i });
      start = -1;
    }
  }

  return tokens;
}

/**
 * Whether the character at `index` belongs inside a word. A split-off word used to survive next to
 * anything the tokenizer did not know about: «کیر», کیر😂.
 */
export function isWordCharacter(text: string, index: number): boolean {
  const c = text.charCodeAt(index);
  if (c < 128) {
    return isLetterOrDigit(c);
  }

  if (isLowSurrogate(c) && index > 0 && isHighSurrogate(text.charCodeAt(index - 1))) {
    return isWordCharacter(text, index - 1);
  }

  switch (categoryAt(text, index)) {
    case 'Lu':
    case 'Ll':
    case 'Lt':
    case 'Lm':
    case 'Lo':
    case 'Mn':
    case 'Mc':
    case 'Me':
    case 'Nd':
    case 'Nl':
    case 'No':
      return true;

    // Zero-width non-joiner and friends are part of Persian spelling, but a variation selector or
    // zero-width joiner after an emoji is not.
    case 'Cf':
      return (c === 0x200c || c === 0x200d) && index > 0 && isLetter(text.charCodeAt(index - 1));

    default:
      return false;
  }
}

/**
 * Renders ASCII digits as Persian digits (۰-۹), leaving everything else alone.
 *
 * @param text - The text to convert. `null` and `undefined` give `''`.
 * @returns The text with ASCII digits replaced.
 * @throws `TypeError` when `text` is not a string, `null` or `undefined`.
 *
 * @example
 * ```ts
 * toPersianDigits('2 ساعت پیش'); // '۲ ساعت پیش'
 * ```
 */
export function toPersianDigits(text: string | null | undefined): string {
  assertText(text, 'text');
  if (text === null || text === undefined || text.length === 0) {
    return '';
  }

  let result = '';
  for (let i = 0; i < text.length; i++) {
    const c = text.charCodeAt(i);
    result += c >= 0x30 && c <= 0x39 ? PERSIAN_DIGITS[c - 0x30]! : text[i]!;
  }

  return result;
}

/**
 * Converts Persian and Arabic-Indic digits to ASCII, leaving everything else alone.
 *
 * @param text - The text to convert. `null` and `undefined` give `''`.
 * @returns The text with Persian and Arabic-Indic digits replaced.
 * @throws `TypeError` when `text` is not a string, `null` or `undefined`.
 *
 * @example
 * ```ts
 * toAsciiDigits('۱۴۰۴/۰۵/۱۴'); // '1404/05/14'
 * ```
 */
export function toAsciiDigits(text: string | null | undefined): string {
  assertText(text, 'text');
  return normalizeWithMap(text, Steps.AsciiDigits, null);
}

function replaceLoneSurrogates(text: string): string {
  let result: string[] | null = null;

  for (let i = 0; i < text.length; i++) {
    const c = text.charCodeAt(i);
    const lone = isHighSurrogate(c)
      ? i + 1 >= text.length || !isLowSurrogate(text.charCodeAt(i + 1))
      : isLowSurrogate(c) && (i === 0 || !isHighSurrogate(text.charCodeAt(i - 1)));

    if (lone) {
      result ??= [text.substring(0, i)];
      result.push('�');
    } else if (result !== null) {
      result.push(text[i]!);
    }
  }

  return result === null ? text : result.join('');
}

function isRemoved(c: number, steps: number): boolean {
  if ((steps & Steps.RemoveDiacritics) !== 0 && c >= 0x064b && c <= 0x0652) {
    return true;
  }

  if ((steps & Steps.RemoveTatweel) !== 0 && c === 0x0640) {
    return true;
  }

  if (
    (steps & Steps.RemoveZeroWidth) !== 0 &&
    (c === 0x200b || c === 0x200c || c === 0x200d || c === 0x2060 || c === 0xfeff || c === 0x00ad)
  ) {
    return true;
  }

  return (
    (steps & Steps.RemoveBidiControls) !== 0 &&
    (c === 0x200e || c === 0x200f || c === 0x061c || (c >= 0x202a && c <= 0x202e) || (c >= 0x2066 && c <= 0x2069))
  );
}

function unifyLetter(c: number): number {
  switch (c) {
    case 0x064a: // ي Arabic yeh
    case 0x0649: // ى alef maksura
      return PERSIAN_YEH;
    case 0x0643: // ك Arabic kaf
      return PERSIAN_KEHEH;
    case 0x0623: // أ
    case 0x0625: // إ
    case 0x0622: // آ
      return ALEF;
    case 0x0629: // ة teh marbuta
      return HEH;

    // Letters from the Urdu, Kurdish and Pashto blocks that render as the Persian ones in most fonts.
    // NFKC leaves them alone because they are distinct letters, not compatibility forms, so a swash
    // kaf gets past an entry written with a normal one.
    case 0x06aa:
    case 0x06ab:
      return PERSIAN_KEHEH;
    case 0x06be:
    case 0x06c0:
    case 0x06c1:
    case 0x06c3:
    case 0x06d5:
      return HEH;
    case 0x06cd:
    case 0x06ce:
    case 0x06d0:
    case 0x06d2:
    case 0x06d3:
      return PERSIAN_YEH;
    case 0x0671:
    case 0x0672:
    case 0x0673:
    case 0x0675:
      return ALEF;
    default:
      return c;
  }
}

function collapseRepeats(value: string, map: number[] | null): string {
  let result = '';
  const kept: number[] | null = map === null ? null : [];
  let runChar = 0;
  let runLength = 0;

  for (let i = 0; i < value.length; i++) {
    const c = value.charCodeAt(i);
    runLength = c === runChar ? runLength + 1 : 1;
    runChar = c;

    if (runLength <= 2 || isWhiteSpace(c)) {
      result += value[i]!;
      kept?.push(map![i]!);
    }
  }

  replaceMap(map, kept);
  return result;
}

function collapseWhitespace(value: string, map: number[] | null): string {
  let result = '';
  const kept: number[] | null = map === null ? null : [];
  let pendingSpace = false;
  let pendingSource = 0;

  for (let i = 0; i < value.length; i++) {
    const c = value.charCodeAt(i);
    if (isWhiteSpace(c)) {
      pendingSpace = result.length > 0;
      if (map !== null) {
        pendingSource = map[i]!;
      }

      continue;
    }

    if (pendingSpace) {
      result += ' ';
      kept?.push(pendingSource);
      pendingSpace = false;
    }

    result += value[i]!;
    kept?.push(map![i]!);
  }

  replaceMap(map, kept);
  return result;
}

function replaceMap(map: number[] | null, kept: number[] | null): void {
  if (map === null) {
    return;
  }

  map.length = 0;
  for (const source of kept!) {
    map.push(source);
  }
}
