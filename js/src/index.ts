/**
 * Persian text normalization and evasion-resistant profanity filtering.
 *
 * @remarks
 * The JavaScript/TypeScript port of PersianTextGuard. It gives the same answers as the .NET package
 * for every case in the shared conformance corpus, with positions in JavaScript string units.
 *
 * @packageDocumentation
 */
export { ProfanityFilter } from './filter';
export { normalize, toAsciiDigits, toPersianDigits, tokenize } from './normalizer';
export { WORD_CATEGORIES, WordListFormatError } from './types';
export type {
  BannedWord,
  EvasionKind,
  NormalizationStep,
  NormalizationSteps,
  ProfanityFilterOptions,
  ProfanityMatch,
  ResolvedWord,
  WordCategory,
  WordMatchMode,
} from './types';
export { WordList } from './word-list';
