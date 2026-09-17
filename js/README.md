# persian-text-guard

Persian text normalization and evasion-resistant profanity filtering for JavaScript and TypeScript.

```ts
import { ProfanityFilter, WordList } from 'persian-text-guard';

const filter = new ProfanityFilter(WordList.persianDefault);
console.log(filter.containsProfanity('ک.ی.ر')); // → true
```
