// Consumer check: the packed package loaded with import (spec 003, SC-007).
import assert from 'node:assert/strict';
import { ProfanityFilter, WordList } from 'persian-text-guard';

const filter = new ProfanityFilter(WordList.persianDefault);

assert.equal(filter.containsProfanity('ک.ی.ر'), true);
assert.equal(filter.containsProfanity('سلام، سفارشم کی میرسه؟'), false);
assert.equal(filter.censor('kir and motherfucker'), '**** and ****');
assert.equal(filter.findMatch('😀 کیر').index, 3);

console.log('esm ok');
