// Port of dotnet/src/PersianTextGuard/ProfanityFilter.Scan.cs: the readings, token and phrase lookup,
// anywhere entries, joined single letters, words split once, and masked or broken chunks.
// The character-level helpers (Fold, Squeeze, IsFiller, …) are in fold.ts.
import { enclosedLetter, fold, foldCharacter, squeeze } from './fold';
import { COMPARISON_STEPS, normalizeWithMap, tokenizeWithOffsets } from './normalizer';
import type { Token } from './normalizer';
import { ReadingKind } from './source-map';
import type { ResolvedWord } from './types';
import { categoryAt, categoryOfUnit, isHighSurrogate, isLetter, isLetterOrDigit, isLowSurrogate, isWhiteSpace } from './unicode';

/** The .NET `[Flags] EvasionKind` bits, used internally so evasions compare and combine as in .NET. */
export const Evasion = {
  None: 0,
  RepeatedLetters: 1 << 0,
  LookalikeCharacters: 1 << 1,
  SplitWord: 1 << 2,
} as const;

/**
 * A word split in two or masked with symbols is only matched against entries at least this long:
 * shorter ones ("ass", «کون») turn up by accident in ordinary text broken that way.
 */
export const MINIMUM_BROKEN_WORD_LENGTH = 4;

// Longest first, so «هایی» is tried before «ها».
const PERSIAN_SUFFIXES: readonly string[] = [
  'هاشون', 'هاتون', 'هامون', 'هایی', 'های', 'هاش', 'هات', 'هام', 'ها', 'تون', 'شون', 'مون', 'ای', 'یی', 'اش', 'ات', 'ام', 'ا',
];

/** An entry as the filter looks it up: its position in the list breaks ties. */
export interface Entry {
  /** The entry as the caller gave it, reported when it matches. */
  readonly word: ResolvedWord;
  /** The entry's position in the word list. */
  readonly order: number;
}

/** What is searched for (.NET `Key`). */
export interface Key {
  /** What is searched for. */
  readonly text: string;
  /** The entry as the caller gave it, reported when it matches. */
  readonly word: ResolvedWord;
  /** Whether the key must match a whole token. */
  readonly wholeWord: boolean;
  /** The entry's position in the word list. */
  readonly order: number;
}

/** A phrase entry, one token per word (.NET `Phrase`). */
export interface Phrase {
  /** The phrase, one token per word. */
  readonly tokens: readonly string[];
  /** The entry as the caller gave it, reported when it matches. */
  readonly word: ResolvedWord;
  /** The entry's position in the word list. */
  readonly order: number;
}

/** The filter's lookups, built once. */
export interface ScanState {
  /** Read through held keys. */
  readonly squeezeRepeatedLetters: boolean;
  /** Read through look-alike characters and filler. */
  readonly foldLookalikeCharacters: boolean;
  /** Read through spaced, dotted and once-split words. */
  readonly joinSpacedLetters: boolean;
  /** The number of distinct entries. */
  readonly count: number;
  /** Whole-word entries by token. */
  readonly words: ReadonlyMap<string, Entry>;
  /** Phrase entries by their first token. */
  readonly phrases: ReadonlyMap<string, readonly Phrase[]>;
  /** Entries matched anywhere, in list order. */
  readonly anywhere: readonly Key[];
  /** Entries long enough to match with some letters masked. */
  readonly maskable: readonly Key[];
}

/** A banned word found in one reading, before it is mapped back to the message (.NET `Hit`). */
export interface Hit {
  /** Which reading it was found in, so the right source map is used. */
  readonly reading: ReadingKind;
  /** Its first unit in that reading. */
  readonly start: number;
  /** One past its last unit in that reading. */
  readonly end: number;
  /** The entry that matched. */
  readonly word: ResolvedWord;
  /** The entry's position in the word list, for breaking ties. */
  readonly order: number;
  /** What had to be undone to find it, as {@link Evasion} bits. */
  readonly evasion: number;
}

/** Collects hits: either the first one only, or all of them. */
interface Sink {
  readonly all: Hit[] | null;
  first: Hit | null;
}

/**
 * Records a hit. Returns true when the scan should stop, which is only when it wants the first.
 */
function report(hit: Hit, sink: Sink): boolean {
  if (sink.all === null) {
    sink.first = hit;
    return true;
  }

  sink.all.push(hit);
  return false;
}

function isNullOrWhiteSpace(text: string | null | undefined): boolean {
  if (text === null || text === undefined) {
    return true;
  }

  for (let i = 0; i < text.length; i++) {
    if (!isWhiteSpace(text.charCodeAt(i))) {
      return false;
    }
  }

  return true;
}


/**
 * Searches `text` for banned words. With `all` null it stops at the first hit and returns it;
 * otherwise it appends every hit to `all`. Returns whether anything was found, and the first hit.
 *
 * @remarks
 * Every capability goes through here, so they cannot disagree about whether a message is clean. The
 * order is the one `findMatch` has always used — most literal reading first, and within a reading
 * tokens and phrases, anywhere entries, joined single letters, a word split once — so the first hit
 * is the least evasion needed.
 */
export function scan(state: ScanState, text: string | null | undefined, all: Hit[] | null): { found: boolean; first: Hit | null } {
  const sink: Sink = { all, first: null };
  if (isNullOrWhiteSpace(text) || state.count === 0) {
    return { found: false, first: null };
  }

  const normalized = normalizeWithMap(text, COMPARISON_STEPS, null);
  const folded = state.foldLookalikeCharacters ? fold(normalized) : normalized;

  const readings: [string, ReadingKind, number][] = [[normalized, ReadingKind.Normalized, Evasion.None]];

  if (state.squeezeRepeatedLetters) {
    readings.push([squeeze(normalized), ReadingKind.Squeezed, Evasion.RepeatedLetters]);
  }

  if (state.foldLookalikeCharacters) {
    readings.push([folded, ReadingKind.Folded, Evasion.LookalikeCharacters]);
    if (state.squeezeRepeatedLetters) {
      readings.push([squeeze(folded), ReadingKind.FoldedSqueezed, Evasion.LookalikeCharacters | Evasion.RepeatedLetters]);
    }
  }

  const checkedReadings = new Set<string>();

  for (const [reading, kind, evasion] of readings) {
    // On ordinary text most readings are the same string.
    if (checkedReadings.has(reading)) {
      continue;
    }

    checkedReadings.add(reading);

    const tokens = tokenizeWithOffsets(reading);
    if (matchTokens(state, tokens, kind, evasion, sink) || matchAnywhere(state, reading, null, kind, evasion, sink)) {
      return { found: true, first: sink.first };
    }

    if (state.joinSpacedLetters) {
      const split = evasion | Evasion.SplitWord;

      const joined = tryJoinSingleLetters(tokens);
      if (
        joined !== null &&
        (matchTokens(state, joined.tokens, kind, split, sink) || matchAnywhere(state, joined.text, joined.map, kind, split, sink))
      ) {
        return { found: true, first: sink.first };
      }

      if (matchSplitHalves(state, tokens, kind, split, sink)) {
        return { found: true, first: sink.first };
      }
    }
  }

  const found = matchBrokenChunks(state, normalized, sink) || (all !== null && all.length > 0);
  return { found, first: sink.first };
}

function matchTokens(state: ScanState, tokens: readonly Token[], kind: ReadingKind, evasion: number, sink: Sink): boolean {
  for (let i = 0; i < tokens.length; i++) {
    const token = tokens[i]!;
    const entry = tryFindWord(state, token.text);
    if (entry !== null && report({ reading: kind, start: token.start, end: token.end, word: entry.word, order: entry.order, evasion }, sink)) {
      return true;
    }

    const phrases = state.phrases.get(token.text);
    if (phrases === undefined) {
      continue;
    }

    for (const phrase of phrases) {
      if (
        phraseStartsAt(tokens, i, phrase.tokens) &&
        report(
          { reading: kind, start: token.start, end: tokens[i + phrase.tokens.length - 1]!.end, word: phrase.word, order: phrase.order, evasion },
          sink,
        )
      ) {
        return true;
      }
    }
  }

  return false;
}

/**
 * Anywhere entries inside `haystack`. When the haystack is not the reading itself, `map` gives each of
 * its characters' position in the reading.
 */
function matchAnywhere(
  state: ScanState,
  haystack: string,
  map: readonly number[] | null,
  kind: ReadingKind,
  evasion: number,
  sink: Sink,
): boolean {
  for (const key of state.anywhere) {
    let index = haystack.indexOf(key.text);

    while (index >= 0) {
      const last = index + key.text.length - 1;
      const start = map === null ? index : map[index]!;
      const end = map === null ? last + 1 : map[last]! + 1;

      if (report({ reading: kind, start, end, word: key.word, order: key.order, evasion }, sink)) {
        return true;
      }

      index = haystack.indexOf(key.text, index + 1);
    }
  }

  return false;
}

/** The result of joining runs of single letters: tokens, their text joined by spaces, and a map. */
export interface Joined {
  /** The tokens, with runs of single letters joined. */
  readonly tokens: readonly Token[];
  /** The joined tokens separated by spaces. */
  readonly text: string;
  /** For each unit of `text`, its position in the reading. */
  readonly map: readonly number[];
}

/**
 * Runs of two or more single-letter tokens become one token: "f u c k" is "fuck". Returns null when
 * there is no such run. The joined text is the tokens separated by spaces, and the map gives each of
 * its characters' position in the reading.
 */
export function tryJoinSingleLetters(tokens: readonly Token[]): Joined | null {
  let hasRun = false;
  for (let i = 0; i + 1 < tokens.length && !hasRun; i++) {
    hasRun = isSingleLetter(tokens[i]!) && isSingleLetter(tokens[i + 1]!);
  }

  if (!hasRun) {
    return null;
  }

  const result: Token[] = [];
  let text = '';
  const positions: number[] = [];

  for (let i = 0; i < tokens.length; ) {
    if (text.length > 0) {
      // The separating space maps to where the previous token ended, keeping the map in order.
      text += ' ';
      positions.push(positions[positions.length - 1]!);
    }

    if (!isSingleLetter(tokens[i]!)) {
      const token = tokens[i++]!;
      result.push(token);
      text += token.text;
      for (let k = 0; k < token.text.length; k++) {
        positions.push(token.start + k);
      }

      continue;
    }

    // A run of single letters, including a run of one, which stays as it was.
    const runStart = text.length;
    const first = tokens[i]!;
    let last = first;
    while (i < tokens.length && isSingleLetter(tokens[i]!)) {
      last = tokens[i++]!;
      text += last.text;
      positions.push(last.start);
    }

    result.push({ text: text.substring(runStart), start: first.start, end: last.end });
  }

  return { tokens: result, text, map: positions };
}

function isSingleLetter(token: Token): boolean {
  return token.text.length === 1 && isLetter(token.text.charCodeAt(0));
}

/**
 * A word split once: "fu ck" in Latin letters, «کی ر» in Persian. Only when the halves join into
 * exactly an entry, so "push it" never becomes "pushit" and matches "shit".
 *
 * @remarks
 * Two Latin halves need at least two letters each ("it's hit" is not "shit"). Persian is the other way
 * round: two real words joined make ordinary phrases look like insults («هر کس ده تا» holds «کسده»),
 * but a stray single letter next to a word is a split. «و» ("and") is the one Persian letter that
 * stands alone in ordinary text.
 */
function matchSplitHalves(state: ScanState, tokens: readonly Token[], kind: ReadingKind, evasion: number, sink: Sink): boolean {
  for (let i = 0; i + 1 < tokens.length; i++) {
    const firstHalf = tokens[i]!.text;
    const secondHalf = tokens[i + 1]!.text;
    const length = firstHalf.length + secondHalf.length;

    const latin =
      firstHalf.length >= 2 &&
      secondHalf.length >= 2 &&
      length >= MINIMUM_BROKEN_WORD_LENGTH &&
      isLatinWord(firstHalf) &&
      isLatinWord(secondHalf);
    const persian =
      (firstHalf.length === 1) !== (secondHalf.length === 1) &&
      length >= 3 &&
      firstHalf !== 'و' &&
      secondHalf !== 'و' &&
      isPersianLetter(firstHalf.charCodeAt(0)) &&
      isPersianLetter(secondHalf.charCodeAt(0));

    if (!latin && !persian) {
      continue;
    }

    const joined = firstHalf + secondHalf;
    const entry = state.words.get(joined);
    if (
      entry !== undefined &&
      report({ reading: kind, start: tokens[i]!.start, end: tokens[i + 1]!.end, word: entry.word, order: entry.order, evasion }, sink)
    ) {
      return true;
    }

    for (const key of state.anywhere) {
      if (
        key.text === joined &&
        report({ reading: kind, start: tokens[i]!.start, end: tokens[i + 1]!.end, word: key.word, order: key.order, evasion }, sink)
      ) {
        return true;
      }
    }
  }

  return false;
}

/**
 * Words broken up by punctuation or symbols inside them: «ج.نده», "kos_kesh", "f**k", "c*nt", "f@ck",
 * "a$$hole".
 *
 * @remarks
 * The whole-word reading splits these into pieces, and dropping the symbols only helps when they were
 * added rather than typed in place of a letter. So each space-separated chunk is tried twice: with the
 * symbols removed, and with each symbol standing for any one letter. A mask only matches entries of
 * {@link MINIMUM_BROKEN_WORD_LENGTH} or more letters with at least half of them showing.
 */
function matchBrokenChunks(state: ScanState, normalized: string, sink: Sink): boolean {
  if (!state.joinSpacedLetters && !state.foldLookalikeCharacters) {
    return false;
  }

  let chunkStart = 0;

  for (let i = 0; i <= normalized.length; i++) {
    if (i < normalized.length && normalized.charCodeAt(i) !== 0x20) {
      continue;
    }

    const offset = chunkStart;
    const chunk = normalized.substring(chunkStart, i);
    chunkStart = i + 1;

    const masked = maskedPattern(chunk);
    if (masked === null || masked.masks === 0 || masked.letters === 0) {
      continue;
    }

    const { pattern } = masked;
    const start = offset + masked.trimmedStart;
    const end = offset + masked.trimmedEnd;

    if (state.joinSpacedLetters) {
      const stripped = pattern.split('\0').join('');
      if (stripped.length >= 3) {
        const entry = tryFindWord(state, stripped);
        if (
          entry !== null &&
          report({ reading: ReadingKind.Normalized, start, end, word: entry.word, order: entry.order, evasion: Evasion.SplitWord }, sink)
        ) {
          return true;
        }
      }
    }

    if (!state.foldLookalikeCharacters) {
      continue;
    }

    for (const key of state.maskable) {
      const fits = key.wholeWord
        ? key.text.length === pattern.length && fitsMask(pattern, 0, key.text)
        : fitsMaskAnywhere(pattern, key.text);

      if (
        fits &&
        report(
          { reading: ReadingKind.Normalized, start, end, word: key.word, order: key.order, evasion: Evasion.LookalikeCharacters },
          sink,
        )
      ) {
        return true;
      }
    }
  }

  return false;
}

/** Like .NET `char.IsLetterOrDigit(string, int)`: a valid surrogate pair is read as one code point. */
function isLetterOrDigitAt(text: string, index: number): boolean {
  const category = categoryAt(text, index);
  return category === 'Lu' || category === 'Ll' || category === 'Lt' || category === 'Lm' || category === 'Lo' || category === 'Nd';
}

interface MaskedPattern {
  readonly pattern: string;
  readonly masks: number;
  readonly letters: number;
  readonly trimmedStart: number;
  readonly trimmedEnd: number;
}

/**
 * The chunk with outer punctuation trimmed, letters and digits folded, combining marks dropped, and
 * every other character replaced by `'\0'`. Null when nothing is left.
 */
function maskedPattern(chunk: string): MaskedPattern | null {
  let masks = 0;
  let letters = 0;

  let start = 0;
  let end = chunk.length;
  while (start < end && !isLetterOrDigitAt(chunk, start)) {
    start++;
  }

  while (end > start && !isLetterOrDigitAt(chunk, end - 1)) {
    end--;
  }

  if (end - start < 3) {
    return null;
  }

  let pattern = '';

  for (let i = start; i < end; i++) {
    const c = chunk.charCodeAt(i);

    if (isHighSurrogate(c) && i + 1 < end && isLowSurrogate(chunk.charCodeAt(i + 1))) {
      const letter = enclosedLetter(0x10000 + ((c - 0xd800) << 10) + (chunk.charCodeAt(i + 1) - 0xdc00));
      i++;
      if (letter !== 0) {
        pattern += String.fromCharCode(letter);
        letters++;
      } else {
        pattern += '\0';
        masks++;
      }

      continue;
    }

    // The char overload here (unlike the trimming above): a lone unit, not a code point.
    if (isLetterOrDigit(c)) {
      pattern += String.fromCharCode(foldCharacter(c, true));
      if (isLetter(c)) {
        letters++;
      }

      continue;
    }

    switch (categoryOfUnit(c)) {
      case 'Mn':
      case 'Mc':
      case 'Me':
      case 'Cf':
        continue;
      default:
        pattern += '\0';
        masks++;
        break;
    }
  }

  return { pattern, masks, letters, trimmedStart: start, trimmedEnd: end };
}

function fitsMaskAnywhere(pattern: string, key: string): boolean {
  for (let offset = 0; offset + key.length <= pattern.length; offset++) {
    if (fitsMask(pattern, offset, key)) {
      return true;
    }
  }

  return false;
}

function fitsMask(pattern: string, offset: number, key: string): boolean {
  let masks = 0;

  for (let i = 0; i < key.length; i++) {
    const c = pattern.charCodeAt(offset + i);
    if (c === 0) {
      masks++;
    } else if (c !== key.charCodeAt(i)) {
      return false;
    }
  }

  return masks > 0 && masks * 2 <= key.length;
}

/** An entry for the token, as written or with a Persian suffix attached. */
function tryFindWord(state: ScanState, token: string): Entry | null {
  const direct = state.words.get(token);
  if (direct !== undefined) {
    return direct;
  }

  if (token.length < 4 || !isPersianLetter(token.charCodeAt(0))) {
    return null;
  }

  for (const suffix of PERSIAN_SUFFIXES) {
    if (!hasSuffix(token, suffix)) {
      continue;
    }

    const stem = token.substring(0, token.length - suffix.length);

    // «جندها»: the final heh of «جنده» is often dropped before «ها».
    const entry = state.words.get(stem) ?? (suffix.startsWith('ها') ? state.words.get(stem + 'ه') : undefined);
    if (entry !== undefined) {
      return entry;
    }
  }

  return null;
}

function phraseStartsAt(tokens: readonly Token[], start: number, phrase: readonly string[]): boolean {
  if (start + phrase.length > tokens.length) {
    return false;
  }

  for (let k = 1; k < phrase.length - 1; k++) {
    if (tokens[start + k]!.text !== phrase[k]) {
      return false;
    }
  }

  // The last word of a Persian phrase takes suffixes like a single word does: «بی ناموس‌ها».
  const last = tokens[start + phrase.length - 1]!.text;
  const key = phrase[phrase.length - 1]!;
  if (last === key) {
    return true;
  }

  if (!isPersianLetter(key.charCodeAt(0)) || last.length <= key.length || !last.startsWith(key)) {
    return false;
  }

  const suffix = last.substring(key.length);
  return PERSIAN_SUFFIXES.includes(suffix) && hasSuffix(last, suffix);
}

/** A one-letter suffix needs a four-letter stem: «کیرا» is also the name Kira. */
function hasSuffix(token: string, suffix: string): boolean {
  return token.endsWith(suffix) && token.length - suffix.length >= (suffix.length === 1 ? 4 : 3);
}

function isPersianLetter(c: number): boolean {
  return c >= 0x0600 && c <= 0x06ff && isLetter(c);
}

function isLatinWord(token: string): boolean {
  for (let i = 0; i < token.length; i++) {
    const c = token.charCodeAt(i);
    if (c < 0x61 || c > 0x7a) {
      return false;
    }
  }

  return true;
}
