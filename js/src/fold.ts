// Port of the character-level readings in dotnet/src/PersianTextGuard/ProfanityFilter.Scan.cs:
// Fold, FoldCharacter, IsFiller, IsRunBoundary, EnclosedLetter, BuildLatinBaseLetters and Squeeze.
// SourceMap and the scanner both use them, so they live in one place (spec 003, tasks T024).
import { fromUnits } from './normalizer';
import {
  categoryOfUnit,
  isHighSurrogate,
  isLetter,
  isLetterOrDigit,
  isLowSurrogate,
  isSurrogate,
  isWhiteSpace,
  nfd,
  toLowerInvariant,
} from './unicode';

/** The plain lower-case Latin letter for each unit in U+00C0–U+024F, or the unit itself. */
export const LATIN_BASE_LETTERS: Uint16Array = buildLatinBaseLetters();

function buildLatinBaseLetters(): Uint16Array {
  const table = new Uint16Array(0x024f - 0x00c0 + 1);

  for (let i = 0; i < table.length; i++) {
    const c = 0x00c0 + i;
    const first = nfd(String.fromCharCode(c)).charCodeAt(0);
    table[i] = first < 128 && isLetter(first) ? toLowerInvariant(first) : c;
  }

  // Letters with a stroke or hook have no decomposition.
  const plain: readonly (readonly [number, string])[] = [
    [0x00f8, 'o'], [0x00d8, 'o'], [0x0111, 'd'], [0x0110, 'd'], [0x0142, 'l'],
    [0x0141, 'l'], [0x0192, 'f'], [0x0127, 'h'], [0x0131, 'i'], [0x00df, 's'],
  ];
  for (const [letter, base] of plain) {
    table[letter - 0x00c0] = base.charCodeAt(0);
  }

  return table;
}

const CYRILLIC_AND_GREEK: ReadonlyMap<number, number> = new Map(
  (
    [
      // Cyrillic, already lower-cased by normalization.
      [0x0430, 'a'], [0x0432, 'b'], [0x0435, 'e'], [0x0451, 'e'], [0x043a, 'k'], [0x043c, 'm'],
      [0x043d, 'h'], [0x043e, 'o'], [0x0440, 'p'], [0x0441, 'c'], [0x0442, 't'], [0x0443, 'y'],
      [0x0445, 'x'], [0x0456, 'i'], [0x0458, 'j'], [0x0455, 's'], [0x0501, 'd'], [0x04bb, 'h'],
      // Greek.
      [0x03b1, 'a'], [0x03b5, 'e'], [0x03b9, 'i'], [0x03ba, 'k'], [0x03bd, 'v'], [0x03bf, 'o'],
      [0x03c1, 'p'], [0x03c4, 't'], [0x03c5, 'u'], [0x03c7, 'x'],
    ] as const
  ).map(([unit, letter]) => [unit, letter.charCodeAt(0)] as const),
);

/**
 * Reads look-alikes as the Latin letters they imitate, and drops filler inside words.
 *
 * @remarks
 * Harmless on Persian script, which has no Latin letters to fold. `!` is folded before tokenizing
 * because it is also a sentence separator. Digits are only read as letters in a run that has letters
 * in it: "sh1t" and "4ss" are words, "455" and «۴۵۵ تومان» are numbers. When `map` is given, it
 * receives, for each output unit, its index in the input.
 */
export function fold(normalized: string, map: number[] | null = null): string {
  const out: number[] = [];
  let i = 0;

  while (i < normalized.length) {
    if (isRunBoundary(normalized, i)) {
      out.push(normalized.charCodeAt(i));
      map?.push(i);
      i++;
      continue;
    }

    let end = i;
    let hasLetter = false;
    while (end < normalized.length && !isRunBoundary(normalized, end)) {
      const unit = normalized.charCodeAt(end);
      hasLetter ||=
        isLetter(unit) ||
        (isHighSurrogate(unit) &&
          end + 1 < normalized.length &&
          enclosedLetter(codePointOf(unit, normalized.charCodeAt(end + 1))) !== 0);
      end++;
    }

    for (; i < end; i++) {
      const c = normalized.charCodeAt(i);

      if (isHighSurrogate(c) && i + 1 < end && isLowSurrogate(normalized.charCodeAt(i + 1))) {
        // Enclosed and regional-indicator letters (🅵🆄🅲🅺) read as letters; emoji are filler.
        const letter = enclosedLetter(codePointOf(c, normalized.charCodeAt(i + 1)));
        if (letter !== 0) {
          out.push(letter);
          map?.push(i);
        }

        i++;
        continue;
      }

      if (isFiller(c)) {
        continue;
      }

      out.push(foldCharacter(c, hasLetter));
      map?.push(i);
    }
  }

  return fromUnits(out);
}

// char.ConvertToUtf32(high, low). Callers only pass high surrogates; a non-low second unit gives a
// value that is never an enclosed letter, where .NET would throw on text normalization never produces.
function codePointOf(high: number, low: number): number {
  return 0x10000 + ((high - 0xd800) << 10) + (low - 0xdc00);
}

/** One unit read as the Latin letter it imitates; digits only when `mapDigits` (a run with letters in it). */
export function foldCharacter(c: number, mapDigits: boolean): number {
  if (mapDigits) {
    switch (c) {
      case 0x30: return 0x6f; // 0 → o
      case 0x31: return 0x69; // 1 → i
      case 0x33: return 0x65; // 3 → e
      case 0x34: return 0x61; // 4 → a
      case 0x35: return 0x73; // 5 → s
      case 0x37: return 0x74; // 7 → t
      case 0x38: return 0x62; // 8 → b
    }
  }

  switch (c) {
    case 0x21: // !
    case 0x7c: // |
    case 0xa1: // ¡
      return 0x69; // i
    case 0x20ac: // €
      return 0x65; // e
    case 0x40: // @
      return 0x61; // a
    case 0x24: // $
      return 0x73; // s
  }

  const letter = CYRILLIC_AND_GREEK.get(c);
  if (letter !== undefined) {
    return letter;
  }

  // Accented Latin: fück, shíť, ƒuck.
  if (c >= 0x00c0 && c <= 0x024f) {
    return LATIN_BASE_LETTERS[c - 0x00c0]!;
  }

  return c;
}

/**
 * Filler: symbols with no letter to stand for, typed inside a word to break it up, and combining marks
 * stacked on letters to decorate them (f̶u̶c̶k̶).
 */
export function isFiller(c: number): boolean {
  switch (c) {
    case 0x2a: // *
    case 0x2b: // +
    case 0x7e: // ~
    case 0x5e: // ^
    case 0x60: // `
    case 0x3d: // =
    case 0x3c: // <
    case 0x3e: // >
    case 0x23: // #
    case 0x25: // %
    case 0x26: // &
    case 0x2022: // •
    case 0x00b7: // ·
    case 0x2665: // ♥
    case 0x2764: // ❤
      return true;
  }

  if (c < 128 || isLetterOrDigit(c)) {
    return false;
  }

  const category = categoryOfUnit(c);
  return category === 'Mn' || category === 'Me' || category === 'So' || category === 'Sk';
}

/**
 * Where a run of word-like characters ends, for deciding whether its digits are letters. Symbols that
 * fold to letters, filler and emoji belong to the run; spaces and punctuation end it.
 */
export function isRunBoundary(text: string, index: number): boolean {
  const c = text.charCodeAt(index);
  if (isWhiteSpace(c)) {
    return true;
  }

  if (
    isLetterOrDigit(c) ||
    isSurrogate(c) ||
    isFiller(c) ||
    c === 0x21 /* ! */ ||
    c === 0x7c /* | */ ||
    c === 0xa1 /* ¡ */ ||
    c === 0x20ac /* € */ ||
    c === 0x40 /* @ */ ||
    c === 0x24 /* $ */
  ) {
    return false;
  }

  switch (categoryOfUnit(c)) {
    case 'Pc':
    case 'Pd':
    case 'Ps':
    case 'Pe':
    case 'Pi':
    case 'Pf':
    case 'Po':
    case 'Sm':
    case 'Sc':
      return true;
    default:
      return false;
  }
}

/**
 * The Latin letter (as a UTF-16 unit) a squared, circled or regional-indicator letter shows, or 0.
 * NFKC already folds the ones with a compatibility mapping (Ⓐ, 𝐟); these have none.
 */
export function enclosedLetter(codePoint: number): number {
  if (codePoint >= 0x1f130 && codePoint <= 0x1f149) return 0x61 + (codePoint - 0x1f130); // 🄰 squared
  if (codePoint >= 0x1f150 && codePoint <= 0x1f169) return 0x61 + (codePoint - 0x1f150); // 🅐 negative circled
  if (codePoint >= 0x1f170 && codePoint <= 0x1f189) return 0x61 + (codePoint - 0x1f170); // 🅰 negative squared
  if (codePoint >= 0x1f1e6 && codePoint <= 0x1f1ff) return 0x61 + (codePoint - 0x1f1e6); // 🇦 regional indicator
  return 0;
}

/** Every run of a repeated letter down to one: "fuuck" to "fuck". Spaces are never squeezed. */
export function squeeze(value: string, map: number[] | null = null): string {
  const out: number[] = [];
  let previous = 0;

  for (let i = 0; i < value.length; i++) {
    const c = value.charCodeAt(i);
    if (c !== previous || c === 0x20) {
      out.push(c);
      map?.push(i);
    }

    previous = c;
  }

  return fromUnits(out);
}
