// Port of dotnet/src/PersianTextGuard/SourceMap.cs.
import { fold, squeeze } from './fold';
import { COMPARISON_STEPS, normalizeWithMap } from './normalizer';
import { isWhiteSpace } from './unicode';

/** The lossy forms of a message the filter searches, in the order it searches them (.NET `ReadingKind`). */
export const ReadingKind = {
  /** Comparison-normalized. */
  Normalized: 0,
  /** Normalized, with repeated letters squeezed to one. */
  Squeezed: 1,
  /** Normalized, with look-alike characters folded. */
  Folded: 2,
  /** Normalized, folded, then squeezed. */
  FoldedSqueezed: 3,
} as const;

/** One of the {@link ReadingKind} values. */
export type ReadingKind = (typeof ReadingKind)[keyof typeof ReadingKind];

/**
 * A reading of a message, with where each of its characters came from in the message.
 *
 * @remarks
 * Two maps rather than one, so a fallback can widen in both directions: a match starting at reading
 * index `i` starts no later than `startMap[i]`, and one ending at `j` ends no earlier than `endMap[j]`.
 * When the mapping is exact they are the same array.
 */
export interface MappedText {
  /** The reading, identical to the one the filter searched. */
  readonly text: string;
  /** For each reading character, the earliest message index a match starting there may start at. */
  readonly startMap: readonly number[];
  /** For each reading character, the latest message index a match ending there may end at. */
  readonly endMap: readonly number[];
}

type Step = (value: string, map: number[]) => string;

const foldStep: Step = (value, map) => fold(value, map);
const squeezeStep: Step = (value, map) => squeeze(value, map);

/** A fresh cache with one slot per {@link ReadingKind}. */
export function newMapCache(): (MappedText | undefined)[] {
  return [undefined, undefined, undefined, undefined];
}

/**
 * The `kind` reading of `original`, with its maps, reusing and filling `cache` (one slot per reading),
 * so each reading is mapped once per message and the folded and squeezed readings are derived from the
 * mapped normalized one rather than rebuilt.
 *
 * @remarks
 * The mapped reading is compared with the reading the filter searches. They only differ if
 * segment-wise normalization disagrees with whole-string normalization, in which case the maps fall
 * back to whole whitespace-separated chunks and then to the whole message: a coarser map can hide more
 * than the word, but never less.
 */
export function build(original: string, kind: ReadingKind, cache: (MappedText | undefined)[] = newMapCache()): MappedText {
  const cached = cache[kind];
  if (cached !== undefined) {
    return cached;
  }

  let mapped: MappedText;
  switch (kind) {
    case ReadingKind.Normalized:
      mapped = normalized(original);
      break;
    case ReadingKind.Squeezed:
      mapped = derive(build(original, ReadingKind.Normalized, cache), squeezeStep);
      break;
    case ReadingKind.Folded:
      mapped = derive(build(original, ReadingKind.Normalized, cache), foldStep);
      break;
    default:
      mapped = derive(build(original, ReadingKind.Folded, cache), squeezeStep);
      break;
  }

  cache[kind] = mapped;
  return mapped;
}

/**
 * The mapped normalized reading, checked against the reading the filter searched. Folding and
 * squeezing are plain functions of their input, so once this one matches, every reading derived from
 * it matches too.
 */
function normalized(original: string): MappedText {
  const map: number[] = [];
  const text = normalizeWithMap(original, COMPARISON_STEPS, map);
  const expected = normalizeWithMap(original, COMPARISON_STEPS, null);

  if (text !== expected) {
    return chunkMap(original, expected);
  }

  return { text, startMap: map, endMap: map };
}

function derive(source: MappedText, step: Step): MappedText {
  const stepMap: number[] = [];
  const text = step(source.text, stepMap);

  const exact = source.startMap === source.endMap;
  const starts = new Array<number>(stepMap.length);
  const ends = exact ? starts : new Array<number>(stepMap.length);

  for (let i = 0; i < stepMap.length; i++) {
    starts[i] = source.startMap[stepMap[i]!]!;
    if (!exact) {
      ends[i] = source.endMap[stepMap[i]!]!;
    }
  }

  return { text, startMap: starts, endMap: ends };
}

/**
 * Maps each whitespace-separated chunk of `reading` to the matching chunk of `original` as a whole; the
 * whole message when the chunk counts differ.
 */
export function chunkMap(original: string, reading: string): MappedText {
  const originalChunks = chunks(original);
  const readingChunks = chunks(reading);
  if (originalChunks.length !== readingChunks.length) {
    return wholeMessageMap(original, reading);
  }

  const starts = new Array<number>(reading.length);
  const ends = new Array<number>(reading.length);
  let chunk = 0;
  let start = 0;
  let end = 0;

  for (let i = 0; i < reading.length; i++) {
    if (chunk < readingChunks.length && i >= readingChunks[chunk]![0]) {
      start = originalChunks[chunk]![0];
      end = originalChunks[chunk]![1];
      if (i === readingChunks[chunk]![1]) {
        chunk++;
      }
    }

    // Separators keep the previous chunk's values, which keeps both maps non-decreasing.
    starts[i] = start;
    ends[i] = end;
  }

  return { text: reading, startMap: starts, endMap: ends };
}

/** Maps every reading character to the whole message. */
export function wholeMessageMap(original: string, reading: string): MappedText {
  const starts = new Array<number>(reading.length).fill(0);
  const last = Math.max(0, original.length - 1);
  const ends = new Array<number>(reading.length).fill(last);
  return { text: reading, startMap: starts, endMap: ends };
}

function chunks(text: string): (readonly [number, number])[] {
  const result: (readonly [number, number])[] = [];
  let start = -1;

  for (let i = 0; i <= text.length; i++) {
    const separator = i === text.length || isWhiteSpace(text.charCodeAt(i));
    if (!separator && start < 0) {
      start = i;
    } else if (separator && start >= 0) {
      result.push([start, i - 1]);
      start = -1;
    }
  }

  return result;
}
