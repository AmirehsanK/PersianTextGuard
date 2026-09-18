// Consumer check: a strict TypeScript project using import, with no @types package (spec 003, scenario 7).
import { ProfanityFilter, WordList } from 'persian-text-guard';
import type { ProfanityMatch, WordCategory } from 'persian-text-guard';

const filter = new ProfanityFilter(WordList.persianDefault);

const flagged: boolean = filter.containsProfanity('ک.ی.ر');
const censored: string = filter.censor('kir and motherfucker', '#');
const match: ProfanityMatch | null = filter.findMatch('😀 کیر');
const category: WordCategory = 'slur';
const index: number | undefined = match?.index;

// @ts-expect-error: unknown option
new ProfanityFilter(WordList.all, { squeezeLetters: false });

// @ts-expect-error: a category that does not exist
WordList.bundled('rude');

// @ts-expect-error: the mask is a string
filter.censor('kir', 5);

export { flagged, censored, category, index };
