# Feature Specification: Find Every Match and Censor Messages

**Feature Branch**: `001-censor-find-matches` (not yet created; see Assumptions)

**Created**: 2026-09-16

**Status**: Draft

**Input**: User description: "Add Censor() and FindMatches() to ProfanityFilter for 1.2.0: return every banned word found in a message, not just the first, and return the message with the banned words masked"

## Clarifications

### Session 2026-09-16

- Q: When a banned word is part of a longer word, should the mask cover only the banned part or
  the whole word it sits in? → A: The whole word, prefixes and suffixes included.
- Q: Should a masked word keep its original length, or always become the same fixed mask? → A: A
  fixed-length mask, so the hidden word's length is not revealed.

## User Scenarios & Testing *(mandatory)*

The people using this feature are developers who put PersianTextGuard in front of user-written
text (comments, chat, reviews, usernames), and through them, the moderators and readers of that
text. Today the filter can only answer "does this message contain a banned word?" and name the
first one it finds. Developers who want to log everything a message contained, highlight the
words for a moderator, or publish a message with the words hidden must build that themselves.

### User Story 1 - See every banned word in a message (Priority: P1)

A developer passes a message to the filter and gets back every banned word it contains, in the
order they appear, each with the word-list entry it matched, its category, how it was disguised,
and where it sits in the original message. A moderation tool can then log the full picture,
highlight each word for a human reviewer, or decide what to do based on the most severe
category present rather than on whichever word happened to be found first.

**Why this priority**: Everything else depends on it. Censoring a message is hiding the matches
this story finds, and a moderator cannot act on a message whose second and third insults are
invisible to them. On its own it already closes the limitation the README documents today
("reports the first match, not every match or its position").

**Independent Test**: Can be fully tested by passing messages with known banned words (plain,
repeated, disguised, mixed with ordinary text) and checking that every occurrence is returned
with the right entry, category, disguise and position, and that ordinary messages return nothing.

**Acceptance Scenarios**:

1. **Given** a message containing two different banned words, **When** the developer asks for
   every match, **Then** both are returned, in the order they appear in the message.
2. **Given** a message containing the same banned word three times, **When** the developer asks
   for every match, **Then** three matches are returned, each with its own position.
3. **Given** a message where one word is disguised (spaced letters, a digit for a letter, a held
   key) and another is plain, **When** the developer asks for every match, **Then** both are
   returned, and the disguised one reports which disguise was undone.
4. **Given** an ordinary message the filter already lets through, **When** the developer asks
   for every match, **Then** the result is empty.
5. **Given** any message, **When** the developer asks for every match, **Then** the position of
   each match points at the characters in the message exactly as it was supplied, not at a
   cleaned-up copy of it.

---

### User Story 2 - Publish a message with the banned words hidden (Priority: P2)

A developer whose community allows a message to be posted but not its profanity passes the
message to the filter and gets back the same message with each banned word replaced by the same
fixed mask, everything else untouched. Readers see the message and its meaning; they do not see
the words, fragments of them, or how long they were.

**Why this priority**: It is the most common thing developers do after detecting profanity, and
it is where naive implementations go wrong — leaking part of the word, damaging the text around
it, or producing output the filter itself would still flag. It depends on Story 1 for where the
words are.

**Independent Test**: Can be fully tested by censoring messages with known banned words and
checking that each is hidden, that every character outside the hidden regions is unchanged,
that re-checking the output finds nothing, and that ordinary messages come back identical.

**Acceptance Scenarios**:

1. **Given** a message with one banned word, **When** the developer censors it, **Then** that
   word is replaced by the fixed mask and every other character of the message is returned
   exactly as supplied, including Persian letters, zero-width non-joiners, digits, letter case
   and spacing.
2. **Given** an ordinary message, **When** the developer censors it, **Then** the output is
   identical to the input.
3. **Given** a banned word with something attached — a Persian suffix («جنده‌ها») or a longer
   word around it ("motherfucker") — **When** the developer censors it, **Then** the whole word is
   hidden, and no prefix or suffix fragment is left showing.
4. **Given** a message with a disguised word such as spaced letters or a word broken by a dot,
   **When** the developer censors it, **Then** the whole disguised word, separators included, is
   replaced by one fixed mask.
5. **Given** a message containing a three-letter banned word and a ten-letter one, **When** the
   developer censors it, **Then** both are replaced by identical masks.
6. **Given** any censored output, **When** it is checked by the same filter, **Then** no banned
   word is found in it.
7. **Given** a developer who prefers a different mask character, **When** they censor a message
   with that character chosen, **Then** that character is used in place of the default.

---

### User Story 3 - Existing checks keep behaving the same (Priority: P3)

A developer already using the yes/no check or the first-match check upgrades to 1.2.0 and sees
no change in what those checks return. When they adopt the new capabilities alongside the old
ones, all of them agree about whether a message is clean.

**Why this priority**: The release is a minor version, and existing callers must be able to
upgrade without re-testing their moderation. It adds no new value on its own, but a regression
here would make the upgrade unsafe.

**Independent Test**: Can be fully tested by running the existing behaviour checks unchanged
against 1.2.0, and by checking over a corpus of clean and dirty messages that the yes/no check,
the first-match check, every-match and censoring agree on each one.

**Acceptance Scenarios**:

1. **Given** the existing behaviour checks, **When** they are run against 1.2.0 without
   modification, **Then** all of them pass.
2. **Given** any message, **When** the yes/no check says it is clean, **Then** every-match
   returns nothing and censoring returns the message unchanged; **and when** the yes/no check
   says it is not clean, **Then** every-match returns at least one match, and the first-match
   result lies inside one of them.

---

### Edge Cases

- **Overlapping matches**: "motherfucker" matches both a whole-word entry and the stem "fuck"
  inside it. One match is reported for the region, not two (see FR-007).
- **A banned word inside an ordinary word**: "class", "Scunthorpe" and «کسی» contain listed
  words but are not matches today; they are neither reported nor masked.
- **A word found only after putting it back together**: "f u c k", «ک.ی.ر», "fu ck" and «ج.نده»
  span several characters of separators. The whole span, separators included, becomes one fixed
  mask (FR-013).
- **Line breaks inside a disguised word** ("f\nu\nc\nk"): the line breaks are part of the hidden
  span and are replaced with it. The message can lose lines this way; the alternative would show
  the letter count the fixed mask exists to hide.
- **Invisible characters inside a banned word** (zero-width space, soft hyphen, direction marks):
  they are part of the hidden span and are replaced with it.
- **Persian suffixes and attached emoji**: «جنده‌ها» matches the entry «جنده» and the whole word,
  suffix included, is hidden. «کیر😂» matches «کیر»; the emoji is not part of the word and is kept:
  `****😂`.
- **Phrase entries** («پدر سگ», "kill yourself"): the whole phrase is one match and becomes one
  fixed mask, the space between its words included.
- **Two banned words next to each other** ("kir kos"): they are two matches and two masks; the
  space between them is outside both and is kept: `**** ****`.
- **Positions after censoring**: because every hidden word becomes a mask of the same length,
  censored text is usually a different length from the message. Match positions always refer to
  the message as supplied, never to the censored copy.
- **Right-to-left and mixed-script text**: masking must not reorder the message or change its
  direction; a Persian sentence with a masked word still reads right to left.
- **A message that is nothing but banned words**: every word is masked; the separators between
  them are kept.
- **Empty, whitespace-only, missing, invalid (a truncated emoji) or very long messages**: every
  match returns nothing or the matches found; censoring returns the message with any matches
  hidden; neither fails (FR-017).
- **A mask character that is itself a letter or a digit** would let censored output spell or
  look like words. It is refused when the developer chooses it (FR-014).
- **An evasion switched off in the filter's settings**: a word found only through that evasion
  is neither reported nor masked, the same as the yes/no check today.

## Requirements *(mandatory)*

### Functional Requirements

#### Finding every match

- **FR-001**: The filter MUST find every occurrence of a banned word in a message, not only the
  first.
- **FR-002**: Each match MUST identify the word-list entry it matched exactly as the developer
  supplied it, that entry's category, and which disguises had to be undone to find it — the same
  information the existing first-match check reports.
- **FR-003**: Each match MUST state where it is in the message as supplied: the position of its
  first character and how many characters it spans.
- **FR-004**: Matches MUST be returned in the order they appear in the message.
- **FR-005**: Every occurrence MUST be reported separately, including repeats of the same word.
- **FR-006**: A message with no banned words MUST produce an empty result.
- **FR-007**: When two matches cover overlapping characters, the filter MUST report a single
  match for that region: the one spanning the most characters, or, if they span the same
  number, the one whose entry comes first in the word list.

#### Censoring

- **FR-008**: The filter MUST produce a copy of a message in which every match found under FR-001
  is hidden.
- **FR-009**: Every character outside a hidden region MUST be returned exactly as supplied. The
  output MUST NOT contain any of the normalisation the filter applies internally (unified
  Arabic and Persian letters, removed zero-width non-joiners, converted digits, lower-casing).
- **FR-010**: A message with no banned words MUST be returned unchanged.
- **FR-011**: The hidden region for a match MUST be the whole word the match sits in: from the
  start of the first word the match touches to the end of the last. Letters attached to the
  banned word — a Persian suffix such as «ها», or the rest of a longer word such as "mother" in
  "motherfucker" — MUST be hidden with it. Words are separated by whitespace, punctuation, symbols
  and emoji, which are not part of a word unless they fall inside a match (FR-013).
- **FR-012**: Each hidden region MUST be replaced by a single fixed mask of four mask characters
  (`****` by default), whatever the length of the text it hides.
- **FR-013**: For a word found across separators (spaces, dots, dashes, underscores, line
  breaks, invisible formatting characters), the hidden region MUST run from the start of its
  first word to the end of its last, and everything inside it, separators included, MUST be
  replaced by the one fixed mask of FR-012.
- **FR-014**: The default mask character MUST be `*`. The developer MUST be able to choose a
  different mask character, and a letter or a digit MUST be refused as the mask character.
- **FR-015**: Checking a censored message with the same filter MUST find no banned word.

#### Consistency and safety

- **FR-016**: For every message, the yes/no check, the first-match check, every-match and
  censoring MUST agree: the message is clean for all of them or for none. The region the
  first-match check reports MUST lie inside a match every-match returns — one with the same entry,
  or the longer overlapping match FR-007 chose instead (for "kos kesh", the first-match check
  reports "kos" and every-match reports the phrase "kos kesh").
- **FR-017**: Finding every match and censoring MUST return a result for every message,
  including a missing message, an empty or whitespace-only one, one containing invalid text such
  as a truncated emoji, and one of at least 100,000 characters. A missing message MUST produce an
  empty result and an empty censored message.
- **FR-018**: Both capabilities MUST respect the filter's existing settings: the word list and
  categories it was built with, and which disguises it reads through.
- **FR-019**: The results of the existing yes/no and first-match checks MUST NOT change.
- **FR-020**: Both capabilities MUST be safe to use from many requests at once on a single shared
  filter, like the existing checks.
- **FR-021**: Match positions (FR-003) MUST always refer to the message as supplied. Censoring MUST
  NOT be required to preserve the message's length.

### Key Entities

- **Banned word entry** (exists today): a word or phrase in a word list, how it is matched (as a
  whole word or anywhere) and its category (profanity, sexual, insult, slur, harassment, mild).
- **Match**: one occurrence of a banned word in a message — the entry it matched, the category,
  the disguises undone to find it, and its location (first character and length) in the message
  as supplied. A message has zero or more matches, ordered by location and never overlapping.
- **Censored message**: the message as supplied, with the whole word each match sits in replaced
  by the fixed mask (FR-011 to FR-013). Usually a different length from the message.
- **Mask**: four repetitions of the mask character, identical for every hidden word. The mask
  character is `*` unless the developer chooses another, and is never a letter or a digit.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On a test set of messages with a known count of banned words — plain, repeated,
  disguised and mixed with ordinary text — 100% of the occurrences the yes/no check can detect
  are returned by every-match, each at the correct location.
- **SC-002**: Of the ordinary messages in the existing ordinary-message test set, 0 produce a
  match and 0 are altered by censoring.
- **SC-003**: For 100% of the messages in the evasion test set, checking the censored output
  finds no banned word.
- **SC-004**: For 100% of censored messages, every character outside the hidden regions matches
  the original character for character, and every hidden region reads as the identical
  four-character mask.
- **SC-005**: Finding every match in a clean five-word message takes no more than 1.5 times as
  long as the yes/no check on the same message, and the yes/no check itself is no more than 5%
  slower than in 1.1.0.
- **SC-006**: Censoring a 60-word message that contains banned words takes under 0.1 milliseconds
  on the hardware the README's performance table names.
- **SC-007**: 100% of the robustness inputs in FR-017 return a result without failing.
- **SC-008**: All behaviour checks that pass on 1.1.0 pass on 1.2.0 without modification.

## Assumptions

- **Users**: the direct users are developers integrating the library; moderators and readers see
  the results through the developer's application.
- **Scope**: the release adds finding every match and censoring. Out of scope: typo-tolerant
  (fuzzy) matching, replacing a word with a phrase such as "[removed]", a different mask per
  category, and censoring that understands HTML or Markdown markup — censoring treats the message
  as plain text.
- **Choosing categories**: which categories are found and masked is decided by the word list the
  filter is built with, as today. There is no per-call category option.
- **Positions** count characters the same way the host platform counts them for the string it was
  given, so a developer can use them directly to highlight or slice the original message.
- **Overlap rule** (FR-007) and **separator handling** (FR-013) are reasonable defaults chosen to
  keep the output readable and free of word fragments; they are recorded here so tests can pin
  them.
- **Mask length**: four characters was chosen for FR-012 because it reads clearly as "a word was
  removed" and matches the most common convention. The length is not configurable in this
  release; only the mask character is. Making it configurable later is additive.
- **Versioning**: every change is additive, so under the project constitution this is a MINOR
  release, 1.2.0. The release notes will still describe the new capabilities, and FR-019 keeps
  existing results unchanged.
- **Branch**: no git branch was created for this specification, because the project has no Spec
  Kit git hook installed. Under the constitution's workflow the implementation must happen on a
  branch and land through a pull request, so a `001-censor-find-matches` branch should be created
  before `/speckit-implement`.
