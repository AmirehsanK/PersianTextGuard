// Consumer check: the packed package loaded with require (spec 003, SC-007).
const assert = require('node:assert/strict');
const { ProfanityFilter, WordList } = require('persian-text-guard');

const filter = new ProfanityFilter(WordList.persianDefault);

assert.equal(filter.containsProfanity('ک.ی.ر'), true);
assert.equal(filter.containsProfanity('سلام، سفارشم کی میرسه؟'), false);
assert.equal(filter.censor('kir and motherfucker'), '**** and ****');
assert.equal(filter.findMatch('😀 کیر').index, 3);

console.log('cjs ok');
