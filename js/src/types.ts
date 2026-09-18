/**
 * How an entry is looked for in text.
 *
 * - `'wholeWord'`: only as whole words, or a whole phrase. The safe default: «کس» as a whole word
 *   does not flag «کسی», and "ass" does not flag "class".
 * - `'anywhere'`: anywhere, including inside longer words. Use it for stems whose every extension
 *   is also offensive, such as "fuck" covering "motherfucker".
 */
export type WordMatchMode = 'wholeWord' | 'anywhere';

/**
 * What kind of word an entry is, so a site can choose what to block.
 *
 * - `'uncategorized'`: no category; the default for entries in your own lists.
 * - `'profanity'`: general swearing and crude words: fuck, shit, «ریدم», «گوه».
 * - `'sexual'`: genitals, sex acts and pornography: «کیر», «سکس», cock, blowjob.
 * - `'insult'`: strong insults, including the family and honour insults Persian is built on:
 *   «کسکش», «مادرجنده», «بی‌ناموس», bastard.
 * - `'slur'`: hate speech against a group (race, ethnicity, religion, sexual orientation, gender,
 *   disability).
 * - `'harassment'`: telling someone to hurt themselves, or to shut up: kys, «خفه شو».
 * - `'mild'`: rude in context but ordinary words otherwise: «آشغال», «گوز», «دلقک», damn, crap.
 *   Left out of {@link WordList.persianDefault} because blocking them rejects normal messages.
 */
export type WordCategory = 'uncategorized' | 'profanity' | 'sexual' | 'insult' | 'slur' | 'harassment' | 'mild';

/**
 * Every {@link WordCategory}, in declaration order (the same order as the .NET enum). Frozen.
 */
export const WORD_CATEGORIES: readonly WordCategory[] = Object.freeze([
  'uncategorized',
  'profanity',
  'sexual',
  'insult',
  'slur',
  'harassment',
  'mild',
] as const);

/** Every {@link WordMatchMode}. Frozen. */
export const WORD_MATCH_MODES: readonly WordMatchMode[] = Object.freeze(['wholeWord', 'anywhere'] as const);

/**
 * A word or phrase for a {@link ProfanityFilter} to look for.
 *
 * @remarks
 * A plain object is enough: `{ text: 'casino', mode: 'anywhere' }`. The filter copies each entry
 * when it is built, so changing the object afterwards has no effect on the filter.
 */
export interface BannedWord {
  /** The word or phrase, in any spelling; it is normalized when the filter is built. Blank text is ignored. */
  readonly text: string;
  /** Whole words only (the default), or anywhere in the text. */
  readonly mode?: WordMatchMode;
  /** What kind of word this is. Bundled entries always have one; your own default to `'uncategorized'`. */
  readonly category?: WordCategory;
}

/**
 * An entry with every field present, as the filter and the bundled word lists hold it. Frozen.
 */
export type ResolvedWord = Readonly<Required<BannedWord>>;

/**
 * Which evasions a {@link ProfanityFilter} reads through. Every option defaults to `true`.
 */
export interface ProfanityFilterOptions {
  /**
   * Undo held keys: "fuuuck" is also read as "fuck". Only ever used to find a match, so squeezing
   * "pass" to "pas" does no harm. Default `true`.
   */
  readonly squeezeRepeatedLetters?: boolean;
  /**
   * Read digits, symbols, accented letters and Cyrillic or Greek look-alikes as the Latin letters
   * they imitate ("sh1t", "$hit", "k0s", "fück"), drop filler and emoji typed inside a word ("f*ck"),
   * and read symbols masking letters as those letters ("f**k", "c*nt"). Default `true`.
   */
  readonly foldLookalikeCharacters?: boolean;
  /**
   * Put split words back together: runs of single letters ("f u c k", «ک ی ر»), punctuation inside a
   * word («ج.نده», "kos_kesh"), and a word split once ("fu ck", «کی ر») when the halves join into
   * exactly an entry. Ordinary words are never glued into something else: "push it" does not match
   * "shit". Default `true`.
   */
  readonly joinSpacedLetters?: boolean;
}

/**
 * An evasion that had to be undone before a banned word showed up.
 *
 * - `'repeatedLetters'`: a letter was held down: "fuuuck", «کیییر».
 * - `'lookalikeCharacters'`: digits, symbols or look-alike letters stood in for letters, or filler
 *   was typed inside the word, or symbols masked some of its letters: "sh1t", "$hit", "f*ck", "fück".
 * - `'splitWord'`: the word was broken up with spaces or punctuation: "f u c k", "fu ck", «ج.نده».
 */
export type EvasionKind = 'repeatedLetters' | 'lookalikeCharacters' | 'splitWord';

/**
 * A banned word found by {@link ProfanityFilter.findMatch} or {@link ProfanityFilter.findMatches}. Frozen.
 */
export interface ProfanityMatch {
  /** The entry that matched, as it was given to the filter, with defaults filled in. */
  readonly word: ResolvedWord;
  /**
   * What had to be undone to find it, in the order `repeatedLetters`, `lookalikeCharacters`,
   * `splitWord`. Empty when the word was there as written, after normalization. Useful for
   * moderation logs.
   */
  readonly evasion: readonly EvasionKind[];
  /**
   * The first character of the matched words in the text as it was passed, in UTF-16 code units
   * (JavaScript string indexes), so `text.slice(index, index + length)` is the matched region.
   *
   * @remarks
   * The region always covers whole words: a match inside "motherfucker" or «جنده‌ها» starts at the
   * start of that word.
   */
  readonly index: number;
  /**
   * How many UTF-16 code units the matched words span, from {@link ProfanityMatch.index}. Separators
   * inside a disguised word ("f u c k", «ج.نده») are part of the span. Always at least 1.
   */
  readonly length: number;
}

/**
 * One step {@link normalize} can apply.
 *
 * - `'compatibilityForms'`: Unicode compatibility normalization (NFKC): Arabic presentation forms
 *   such as ﻙ become base letters, full-width Latin becomes ASCII.
 * - `'unifyLetters'`: Arabic yeh and alef maksura to Persian yeh, Arabic kaf to keheh, hamza alef
 *   forms to bare alef, teh marbuta to heh, and look-alike letters from the Urdu, Kurdish and Pashto
 *   blocks to the Persian ones.
 * - `'removeDiacritics'`: Arabic diacritics (harakat).
 * - `'removeTatweel'`: tatweel (ـ), the stretching character.
 * - `'removeZeroWidth'`: zero-width non-joiner, joiner, space, word joiner, byte-order mark and soft
 *   hyphen. The zero-width non-joiner is correct Persian spelling, so this belongs in comparison, not
 *   in text you display.
 * - `'removeBidiControls'`: LRM, RLM, embeddings, overrides and isolates.
 * - `'asciiDigits'`: Persian and Arabic-Indic digits to ASCII 0-9.
 * - `'lowerCase'`: invariant lower-casing, one character at a time.
 * - `'collapseWhitespace'`: every run of whitespace to a single space, and trimmed.
 * - `'collapseRepeats'`: runs of the same character cut to two, so «سسسسلام» and "heeeey" cannot
 *   slip past an entry, while real doubled letters survive.
 */
export type NormalizationStep =
  | 'compatibilityForms'
  | 'unifyLetters'
  | 'removeDiacritics'
  | 'removeTatweel'
  | 'removeZeroWidth'
  | 'removeBidiControls'
  | 'asciiDigits'
  | 'lowerCase'
  | 'collapseWhitespace'
  | 'collapseRepeats';

/**
 * The steps {@link normalize} applies: a preset, or any combination of individual steps.
 *
 * - `'standard'`: safe for text you store and show. Fixes letters typed on an Arabic keyboard layout
 *   and strips invisible junk, but keeps the zero-width non-joiner, digits, case and repeats
 *   (`compatibilityForms`, `unifyLetters`, `removeTatweel`, `removeBidiControls`, `collapseWhitespace`).
 * - `'comparison'`: everything. Lossy on purpose: the form to compare, search and filter on, never to
 *   display.
 * - `'none'`: leave the text exactly as it is.
 *
 * The order of an array does not matter: steps always run in the same order.
 */
export type NormalizationSteps = 'comparison' | 'standard' | 'none' | readonly NormalizationStep[];

/**
 * Thrown by {@link WordList.parse} when a section heading names a category that does not exist.
 *
 * @remarks
 * A heading is a category name in any letter case (`[insult]`, `[ Insult ]`). Numbers are not
 * category names: `[3]` throws this error.
 */
export class WordListFormatError extends Error {
  /** The 1-based line of the unknown heading. */
  readonly line: number;

  /**
   * @param line - The 1-based line of the unknown heading.
   * @param name - The heading's name, as written between the brackets and trimmed.
   */
  constructor(line: number, name: string) {
    super(`Line ${line}: unknown word category '${name}'.`);
    this.name = 'WordListFormatError';
    this.line = line;
  }
}
