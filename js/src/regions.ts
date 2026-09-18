// Port of dotnet/src/PersianTextGuard/ProfanityFilter.Regions.cs: mapping hits back to the message,
// widening to whole words, merging overlaps, and masking.
import { isWordCharacter } from './normalizer';
import type { Hit } from './scan';
import { build } from './source-map';
import type { MappedText } from './source-map';
import type { ResolvedWord } from './types';
import { isHighSurrogate, isLowSurrogate } from './unicode';

/** Every hidden word gets the same mask, so its length says nothing about the word. */
export const MASK_LENGTH = 4;

/** A hit mapped back to the message and widened to whole words (.NET `Candidate`). */
export interface Candidate {
  /** First unit of the region in the message as passed. */
  readonly start: number;
  /** One past the region's last unit. */
  readonly end: number;
  /** Units the hit itself covered, before widening to whole words. */
  readonly hitLength: number;
  /** The entry that matched. */
  readonly word: ResolvedWord;
  /** The entry's position in the word list. */
  readonly order: number;
  /** What had to be undone to find it, as evasion bits. */
  readonly evasion: number;
}

/** A merged region, before it becomes a public, frozen match. */
export interface Region {
  /** The entry reported for the region. */
  readonly word: ResolvedWord;
  /** The least evasion that entry needed, as evasion bits. */
  readonly evasion: number;
  /** First unit of the region in the message. */
  readonly index: number;
  /** Units the region spans. */
  readonly length: number;
}

/**
 * Where `hit` is in `original`, widened to the whole words it touches. The reading's source map is built
 * on first use and kept in `mapCache`, so a message with many hits maps each reading once.
 */
export function toCandidate(original: string, hit: Hit, mapCache: (MappedText | undefined)[]): Candidate {
  const mapped = build(original, hit.reading, mapCache);

  let start = mapped.startMap[hit.start]!;
  const last = mapped.endMap[hit.end - 1]!;
  let end = last + (isSurrogatePairAt(original, last) ? 2 : 1);
  const hitLength = end - start;

  // Whole words: the tokenizer's own rule decides where a word ends, so a suffix joined by a
  // zero-width non-joiner comes along and an emoji or comma does not. The rule answers the same for
  // both halves of a surrogate pair, so the region cannot split one.
  while (start > 0 && isWordCharacter(original, start - 1)) {
    start--;
  }

  while (end < original.length && isWordCharacter(original, end)) {
    end++;
  }

  return { start, end, hitLength, word: hit.word, order: hit.order, evasion: hit.evasion };
}

function sameWord(a: ResolvedWord, b: ResolvedWord): boolean {
  // .NET compares the BannedWord record struct by value.
  return a === b || (a.text === b.text && a.mode === b.mode && a.category === b.category);
}

/**
 * Candidates that overlap become one match: the region is their union, the entry is the one whose hit
 * covered the most characters before widening — so "motherfucker" beats the "fuck" inside it even though
 * both widen to the same word — with ties going to the entry listed first, and the evasion is the least
 * that entry needed.
 */
export function merge(candidates: Candidate[]): Region[] {
  candidates.sort((a, b) => (a.start !== b.start ? a.start - b.start : b.end - a.end));

  const regions: Region[] = [];
  let i = 0;

  while (i < candidates.length) {
    const clusterStart = candidates[i]!.start;
    let clusterEnd = candidates[i]!.end;
    let best = candidates[i]!;

    let next = i + 1;
    while (next < candidates.length && candidates[next]!.start < clusterEnd) {
      const candidate = candidates[next]!;
      clusterEnd = Math.max(clusterEnd, candidate.end);
      if (candidate.hitLength > best.hitLength || (candidate.hitLength === best.hitLength && candidate.order < best.order)) {
        best = candidate;
      }

      next++;
    }

    let evasion = best.evasion;
    for (let k = i; k < next; k++) {
      if (sameWord(candidates[k]!.word, best.word) && candidates[k]!.evasion < evasion) {
        evasion = candidates[k]!.evasion;
      }
    }

    regions.push({ word: best.word, evasion, index: clusterStart, length: clusterEnd - clusterStart });
    i = next;
  }

  return regions;
}

/** Replaces each region of `text` with `mask`, copying everything else unchanged. */
export function applyMask(text: string, regions: readonly { readonly index: number; readonly length: number }[], mask: string): string {
  let result = '';
  let copied = 0;

  for (const region of regions) {
    result += text.substring(copied, region.index) + mask;
    copied = region.index + region.length;
  }

  return result + text.substring(copied);
}

function isSurrogatePairAt(text: string, index: number): boolean {
  return isHighSurrogate(text.charCodeAt(index)) && index + 1 < text.length && isLowSurrogate(text.charCodeAt(index + 1));
}
