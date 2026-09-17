// Consumer check: the same types through require.
import guard = require('persian-text-guard');

const filter = new guard.ProfanityFilter(guard.WordList.persianDefault);
const matches: readonly guard.ProfanityMatch[] = filter.findMatches('sh1t and f u c k');
const evasion: readonly guard.EvasionKind[] | undefined = matches[0]?.evasion;

// @ts-expect-error: a category that does not exist
const wrong: guard.WordCategory = 'rude';

export = { matches, evasion, wrong };
