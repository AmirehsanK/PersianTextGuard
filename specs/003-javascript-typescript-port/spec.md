# Feature Specification: JavaScript/TypeScript Port Published to npm

**Feature Branch**: `003-javascript-typescript-port` (not yet created; see Assumptions)

**Created**: 2026-09-16

**Status**: Draft

**Input**: User description: "a JavaScript/TypeScript port published to npm with bundled types."

## Clarifications

### Session 2026-09-16

- Q: Does this feature end with the first real npm release, or with everything ready so the next release
  publishes to npm? → A: It includes the first release. The feature ends with `1.3.0` published to npm
  and NuGet from CI and verified from both registries (User Story 3, FR-027, SC-010).
- Q: What should checking, matching and censoring do with a value that is not a string? → A: Throw a
  `TypeError`. `null` and `undefined` are still treated as a missing message and never throw; any other
  non-string value is a programmer error (Edge Cases, FR-013). The plan's Constitution Check must record
  this against Principle II, which permits failures only for programmer errors.
- Q: What should the npm package be called? → A: `persian-text-guard` (FR-001).
- Q (raised during planning): PersianTextGuard 1.2.0 throws `ArgumentException` when a message contains a
  Unicode noncharacter — U+FFFE on .NET 8 and .NET 10, and all 66 noncharacters on .NET Framework 4.8 —
  from `ContainsProfanity`, `FindMatch`, `FindMatches`, `Censor` and `Normalize`. How should this feature
  handle it? → A: Fix it in this feature. Corpus cases for noncharacters are added first, the .NET package
  is fixed so it never throws on them while every input that works today gives the same result, and the
  fix ships in `1.3.0` with release notes (FR-026, FR-028).
- Q (raised by analysis): .NET 1.2.0 accepts a category's internal number as a word-list section heading
  (`[3]` reads as `[insult]`, `[+4]` as `[slur]`). Should both ports accept numbers, or only names? → A:
  Only names, in both ports. `[3]` becomes an unknown-category error in .NET too; corpus cases pin it and
  the 1.3.0 release notes call it out (FR-029).
- Q (raised by analysis): SC-008 says the publish gating is demonstrated before the first release. How? →
  A: A real dry run on GitHub Actions with throwaway `v0.0.0-dryrun.*` tags, whose publish steps are
  replaced by dry-run commands on a scratch branch. It shows that a failing JavaScript job skips both
  publish jobs, and that a tag differing from `VERSION` fails both, before `v1.3.0` is ever pushed
  (SC-008).

## User Scenarios & Testing *(mandatory)*

The people affected are:

- **JavaScript developers** moderating chat, comments or form input in Node.js services or in the
  browser, who today cannot use PersianTextGuard at all;
- **TypeScript developers**, who want the same package with full type information and nothing extra to
  install;
- **teams running services in more than one language**, who need a message to get the same answer in
  their .NET and JavaScript services;
- **the maintainer**, who releases every package together and must keep the ports in step;
- **existing .NET users**, who must notice nothing.

The project constitution (v2.0.0) already fixes much of the shape of this port: one package authored for
TypeScript that ships both module styles with bundled type declarations, no runtime dependencies,
support for every Node.js release in active or maintenance long-term support and for browsers, the same
capabilities as every other port, positions in JavaScript's native string units, and a release in
lockstep with every other package. Feature 002 created what makes the port checkable: the shared word
lists in `wordlists/` and the 497-case conformance corpus in `conformance/`, which the .NET package
passes.

### User Story 1 - Check and censor messages from JavaScript or TypeScript (Priority: P1)

A developer installs one package, builds a filter from the bundled Persian, Finglish and English word
list, and uses it to check a message, find the first match or every match, and censor a message. The
filter reads through the same evasions as the .NET package — spaced and dotted letters, held keys,
look-alike letters and digits, invisible characters, Persian suffixes — and leaves ordinary messages
alone. A JavaScript caller and a TypeScript caller use the same package; the TypeScript caller also gets
types, autocomplete and compile-time checks.

**Why this priority**: this is the product. Checking and censoring messages is what every user of the
package needs, and it is the smallest slice that is worth publishing.

**Independent Test**: install the packed package into a fresh JavaScript project and a fresh TypeScript
project; in each, build a filter from the bundled default list, and confirm that `"ک.ی.ر"` is flagged,
`"سلام، سفارشم کی میرسه؟"` is not, `"kir and motherfucker"` censors to `"**** and ****"`, and the
TypeScript project compiles without adding any type package.

**Acceptance Scenarios**:

1. **Given** a filter built from the bundled default list, **When** a caller checks `"f u c k"`, **Then**
   the result is that the message contains profanity.
2. **Given** the same filter, **When** a caller checks `"هر کس پلات بالاست پیام بده"`, **Then** the message
   is ordinary: no match, and censoring returns it unchanged.
3. **Given** the same filter, **When** a caller asks for every match in `"sh1t and f u c k"`, **Then** it
   gets two matches, in order: `shit` (profanity) at index 0, length 4, with look-alike characters as the
   evasion; and `fuck` (profanity) at index 9, length 7, with a split word as the evasion.
4. **Given** a message with an emoji before a banned word, `"😀 کیر"`, **When** a caller asks for the
   first match, **Then** its index is 3 and its length 3, counted in JavaScript string units, so
   `message.slice(index, index + length)` is exactly the matched word.
5. **Given** a filter built from the caller's own entries (`"اسپم"` as a whole word, `"casino"` anywhere),
   **When** a caller checks `"onlinecasino.example"`, **Then** it is flagged.
6. **Given** a caller who chooses `#` as the mask, **When** they censor `"this is kir"`, **Then** the
   result is `"this is ####"`; **and when** they choose a letter as the mask, **Then** they get an error
   saying the mask is invalid.
7. **Given** a TypeScript project, **When** a developer passes an unknown option name or a category that
   does not exist, **Then** the project fails to compile.
8. **Given** a project that loads packages with `require` and another that uses `import`, **When** each
   uses the package, **Then** both work and give identical results.

---

### User Story 2 - The same answer as every other port, proven by the corpus (Priority: P1)

The JavaScript port runs every case in the shared conformance corpus and passes all of them, on every
supported Node.js release. A team with a .NET service and a JavaScript service gets identical decisions,
matches, positions and censored output for the same message and the same configuration.

**Why this priority**: the constitution forbids releasing a port that fails any corpus case, so without
this story nothing can be published. It is also the promise that makes a second language trustworthy.

**Independent Test**: run the port's corpus runner against `conformance/`; every case passes on every
supported Node.js release, no case is reported as not applicable, and a deliberately corrupted case is
reported with its id, file, visible input and the differing fields.

**Acceptance Scenarios**:

1. **Given** the corpus in `conformance/`, **When** the JavaScript corpus runner runs, **Then** all 497
   cases pass, including the robustness cases with lone surrogates and the 132,000-character message.
2. **Given** a corpus case whose recorded result differs from what the port does, **When** the runner
   runs, **Then** that case fails with its id, its file, its input with invisible characters shown as
   `\uXXXX`, and each differing field with its expected and actual value, and the run continues to report
   every other failing case.
3. **Given** the corpus directory is missing, unreadable, has a newer format version, fewer than 300
   cases, duplicate ids or pending cases, **When** the runner runs, **Then** it fails and says why, rather
   than passing with zero cases.
4. **Given** the bundled word lists, **When** the port's default, full and per-category selections are
   compared with the .NET package's, **Then** they contain the same entries in the same order.

---

### User Story 3 - Released to npm together with NuGet as 1.3.0 (Priority: P2)

When the maintainer tags a release, CI publishes the npm package and the NuGet package at the same
version, taken from the single `VERSION` file. If the JavaScript port fails its build, tests, corpus,
API compatibility check or packaging, nothing is published. This feature ends with that first release
done: `1.3.0` of `persian-text-guard` is live on npm and `1.3.0` of `PersianTextGuard` is live on NuGet.
Developers find the npm package with a README that shows installation and a quick start in both Persian
and English.

**Why this priority**: publishing is what the user asked for, but it depends on stories 1 and 2 being
complete and correct; a release that happens early would ship a port that the constitution does not
allow.

**Independent Test**: before tagging, run the release workflow's checks without publishing; confirm the
npm package it would publish has the version in `VERSION`, contains only the intended files, and that a
mismatch between the tag and `VERSION`, or a failing JavaScript job, stops both packages from publishing.
Then tag `v1.3.0` and install both published packages from their registries into fresh projects.

**Acceptance Scenarios**:

1. **Given** `VERSION` holds `X.Y.Z` and every job is green, **When** the maintainer pushes tag
   `vX.Y.Z`, **Then** the npm package and the NuGet package are both published at `X.Y.Z`.
2. **Given** the JavaScript corpus fails, **When** a release tag is pushed, **Then** neither package is
   published.
3. **Given** the published package's page on npm, **When** a Persian-speaking developer reads it,
   **Then** installation and a quick start are there in Persian, right to left, as well as in English,
   with a link to the full project README.
4. **Given** the published package, **When** a developer checks where it came from, **Then** npm shows
   it was built and published from this repository's CI.
5. **Given** the merged feature and `VERSION` set to `1.3.0`, **When** the maintainer pushes tag `v1.3.0`,
   **Then** `npm install persian-text-guard@1.3.0` and the NuGet package `PersianTextGuard` `1.3.0` both
   install from their public registries, and each flags `"ک.ی.ر"` in a fresh project.

---

### User Story 4 - Documented, measured and kept compatible (Priority: P3)

A JavaScript developer can read what every exported function and type does, including edge cases, from
their editor. The port's README states how fast checking a message is, measured on a named machine. CI
records the package's public API, so a later change that would break existing callers is caught before
release.

**Why this priority**: required by the constitution for every port, but the package is usable without
it; it protects users from the second release onward.

**Independent Test**: hover over each exported name in an editor and see its documentation; run the
benchmarks and compare with the README table; change the signature of an exported function on a scratch
branch and confirm CI fails the API check.

**Acceptance Scenarios**:

1. **Given** a TypeScript editor, **When** a developer hovers over any exported function, type or option,
   **Then** its documentation explains behaviour and edge cases, not only the signature.
2. **Given** the recorded public API, **When** a pull request removes or changes an exported member,
   **Then** CI fails and names the change.
3. **Given** every code example in the port's README, **When** the test suite runs, **Then** each example
   has a test that proves it.

---

### Edge Cases

- **Missing and blank input**: `null`, `undefined`, `""` and whitespace-only text are never flagged, never
  throw, and censor to `""` (missing) or the text unchanged (blank), as the corpus records.
- **Values that are not text**: a JavaScript caller may pass a number, an object or an array where a
  message is expected, for example an unvalidated request body. Checking, matching, censoring,
  normalizing and tokenizing throw a `TypeError` for any value that is not a string, `null` or
  `undefined`, so a caller's missing validation shows up at once instead of an unchecked value passing
  as clean (clarified 2026-09-16). Callers that pass untrusted values are expected to validate that they
  are strings first; the README says so.
- **Lone surrogates**: a message cut in the middle of an emoji is handled without error, and positions
  still slice the original string correctly.
- **Noncharacters**: a message containing U+FFFE, U+FFFF, U+FDD0 or another noncharacter is handled
  without error in every port. PersianTextGuard 1.2.0 throws on these; this feature fixes that (FR-028).
- **Very long messages**: a 132,000-character message is checked without error, in under 100 ms on the
  machine the port README names.
- **Unicode differences between runtimes**: JavaScript engines and .NET can disagree on Unicode details —
  compatibility normalization of rare characters, lower-casing (for example `İ`), which characters count
  as whitespace. The port matches the corpus, not its runtime's default. Where a difference is found
  that the corpus does not yet cover, a case is added to the corpus (the .NET package must pass it too)
  before the port is changed.
- **Browser engines and older Node.js**: the port relies only on standard language features available
  in every supported Node.js release and current browsers; where a runtime lacks something the port
  needs, it fails at install or load, not while checking a message.
- **Entries changed after building**: if a caller mutates the entry objects or the options they passed
  after building a filter, the filter's behaviour does not change.
- **Duplicate and blank entries**: spellings that normalize to the same entry count once, and blank
  entries are ignored, as in .NET.
- **Mask edge cases**: a mask that is empty, longer than one string unit, a letter, digit, whitespace,
  control character or half of a surrogate pair is rejected before the message is looked at, even when
  the message is missing.
- **Word-list text errors**: an unknown category heading is reported with its line number; Windows line
  endings, comments, blank lines and `~` markers with extra spaces are read as .NET reads them. A heading
  is a category **name**, in any letter case and with surrounding spaces allowed (`[insult]`,
  `[ Insult ]`). A number such as `[3]` or `[+4]` is an unknown category in every port. PersianTextGuard
  1.2.0 accepted numbers there, as .NET's internal category numbers; this feature removes that
  (FR-029).
- **Both module styles in one application**: if one dependency loads the package with `require` and
  another with `import`, filters, entries and matches from either work with the other, and bundled lists
  give the same results.
- **Bundle size**: an application that only uses normalization or its own word list is not forced to
  ship more than necessary when its bundler supports removing unused code; the bundled lists are still
  available to those who import them.

## Requirements *(mandatory)*

### Functional Requirements

**Package and installation**

- **FR-001**: The port MUST be a single npm package, named `persian-text-guard`, usable from both
  JavaScript and TypeScript. TypeScript users MUST get complete type declarations from the package
  itself, with no separate type package.
- **FR-002**: The package MUST work when loaded with `import` (ES modules) and with `require` (CommonJS),
  with the same public API, the same types and identical behaviour in both.
- **FR-003**: The package MUST run on every Node.js release in active or maintenance long-term support,
  and in current browsers through common bundlers, without Node.js-specific APIs or polyfills in the
  library. CI proves this on V8 (Node.js and a browser bundle run without Node.js globals); other browser
  engines are supported on the same standard features but not tested in CI (see Assumptions).
- **FR-004**: The package MUST have no runtime dependencies.
- **FR-005**: The bundled word lists MUST be taken from `wordlists/` when the package is built. The port
  MUST NOT keep its own copy of the list files in the repository.
- **FR-006**: The published package MUST contain only what users need at run time and for types, the
  README, the licence and the third-party notices; tests, benchmarks, corpus files and build
  configuration MUST NOT be published.

**Capabilities** (the same as every port, constitution Principle V)

- **FR-007**: Users MUST be able to build a filter from any collection of entries, each with its text, a
  match mode (whole word or anywhere) and a category, and from options that switch reading through held
  keys, look-alike characters and split words on or off, each on by default.
- **FR-008**: A filter MUST let users check whether a message contains profanity, find the first match,
  find every match, censor a message with the default mask `*`, and censor with a chosen mask. It MUST
  report how many distinct entries it holds.
- **FR-009**: Each match MUST report the entry exactly as the caller gave it, the evasions that had to be
  undone (held keys, look-alike characters, split word, or none), and the matched region's start index
  and length in JavaScript string units (UTF-16 code units), covering whole words as in .NET.
- **FR-010**: Users MUST be able to normalize Persian text with the standard and comparison presets or
  any combination of the individual steps, tokenize text, and convert digits to Persian and to ASCII.
- **FR-011**: Users MUST be able to parse word-list text in the shared format, and to select the bundled
  lists as all entries, the default selection (everything except mild), or chosen categories. A section
  heading names a category by name only (FR-029).
- **FR-012**: Public names MUST follow JavaScript and TypeScript conventions: camelCase functions and
  methods (`containsProfanity`, `findMatches`, `normalize`) and PascalCase types and classes
  (`ProfanityFilter`, `WordList`). The port's README MUST include a table that maps each .NET name to its
  JavaScript name.
- **FR-013**: Programmer errors MUST be reported as errors at the call that makes them, and only these:
  - an invalid mask (not exactly one string unit, or a letter, digit, whitespace, control character or
    surrogate);
  - a missing or non-iterable entry collection, or an entry that is not an object with string `text`
    and a known `mode` and `category`;
  - word-list text naming an unknown category, with its line number;
  - an unknown category name passed to a bundled-list selection, or an unknown normalization preset or
    step name;
  - a message or word-list text argument that is not a string, `null` or `undefined`, which MUST throw a
    `TypeError` from checking, finding matches, censoring, normalizing, tokenizing and parsing.

**Robustness and sharing**

- **FR-014**: Checking, finding matches, censoring, normalizing and tokenizing MUST return a result, never
  an error, for any string, including empty, whitespace-only, lone surrogates and very long strings, and
  for `null` and `undefined`.
- **FR-015**: A filter MUST be immutable after it is built and safe to reuse for any number of messages
  and to share across concurrent asynchronous callers. Work that can be done once (normalizing entries,
  building lookups) MUST happen when the filter is built, and checking a message MUST NOT take time
  proportional to the number of whole-word entries.

**Conformance**

- **FR-016**: The port MUST include a corpus runner that reads `conformance/` and runs every case,
  following the runner obligations in the corpus format contract: refusing a newer format version,
  failing on a missing or unreadable corpus, fewer than 300 cases, duplicate ids or pending cases,
  checking kind rules before results, comparing every recorded field exactly after converting positions
  from code points to string units, and reporting each failing case with its id, file, visible input and
  differing fields while continuing past failures.
- **FR-017**: The port MUST pass 100% of corpus cases on every supported Node.js release, with no case
  reported as not applicable.
- **FR-018**: The port's bundled default, full and per-category selections MUST contain the same entries,
  in the same order, as the .NET package built from the same `wordlists/`.
- **FR-019**: Behaviour specific to the JavaScript port — both module styles, the type declarations,
  missing values (`undefined`), `TypeError` for values that are not strings, mask and word-list errors,
  immutability after building — MUST be covered
  by the port's own tests, run against the packed package as users install it.

**Release, compatibility and documentation**

- **FR-020**: The package version MUST come from `VERSION`. A release tag MUST publish the npm package
  and the NuGet package at the same version, only when the tag matches `VERSION` and every job for every
  port is green; otherwise neither is published. The npm package MUST be published from CI with a record
  that it was built from this repository.
- **FR-021**: CI MUST run, for the JavaScript port on every pull request: the build, the port's tests and
  the full corpus on every supported Node.js release, a check that the packed package loads with both
  `import` and `require` and compiles for a TypeScript consumer, a check that it loads in a browser
  bundle, and packaging. Existing job names MUST stay unchanged.
- **FR-022**: The package's public API MUST be recorded and compared in CI with the previous release. The
  first release records the baseline; from then on, a change that would break existing callers fails CI.
- **FR-023**: Every exported function, type and option MUST have API documentation, in English, that
  explains behaviour and edge cases.
- **FR-024**: The port MUST have its own README, shown on npm, with installation and a quick start in both
  Persian (right to left) and English, the .NET-to-JavaScript name table, a performance table measured on
  a named machine with the ecosystem's standard benchmarking tool, and a link to the full project README.
  Every code example in it MUST have a matching test.
- **FR-025**: The project README MUST list the JavaScript/TypeScript package next to the .NET one, and its
  Development section MUST show the `js/` directory and the commands to build, test, run the corpus and
  benchmark the port.
- **FR-026**: The .NET package, the conformance corpus's existing cases and the shared word lists MUST NOT
  change behaviour, except for the noncharacter fix in FR-028 and the section-heading rule in FR-029.
  Corpus cases MAY be added (see Edge Cases,
  "Unicode differences between runtimes"), and the .NET package MUST pass every added case.
- **FR-028**: Messages containing Unicode noncharacters (U+FDD0–U+FDEF, and the last two code points of
  every plane) MUST NOT make any port throw. The corpus MUST gain robustness and normalization cases with
  noncharacters, alone, inside a banned word and next to one; the .NET package MUST be fixed to pass them
  on every target; every input that did not throw in 1.2.0 MUST give the same result as in 1.2.0; and the
  corpus writing rules MUST require noncharacters to be written as escapes, like other invisible
  characters.
- **FR-029**: In word-list text, a section heading MUST name a category by its name, in any letter case
  and with surrounding spaces allowed. Any other heading, including a number such as `[3]` or `[+4]`, MUST
  be reported as an unknown category with its line number, in every port. The corpus MUST gain
  word-list-parsing cases for numeric headings (errors) and for spaced and upper-case names (accepted), and
  the .NET package MUST be changed to pass them.
- **FR-027**: The feature MUST end with the first release that includes npm, `1.3.0`: after the pull
  request merges, `VERSION` holds `1.3.0`, tag `v1.3.0` is pushed, CI publishes `persian-text-guard`
  `1.3.0` to npm and `PersianTextGuard` `1.3.0` to NuGet, and both are installed from their public
  registries into fresh projects and shown to work. A GitHub release for `v1.3.0` MUST have English notes
  and a Persian summary that announce the npm package and state that .NET matching behaviour is unchanged.

### Key Entities

- **Filter**: built once from entries and options; answers every question about a message. Immutable,
  shareable.
- **Entry**: a word or phrase to look for, with its match mode (whole word or anywhere) and category
  (uncategorized, profanity, sexual, insult, slur, harassment, mild).
- **Options**: the three evasions a filter reads through, each on by default.
- **Match**: the entry found, the evasions undone, and the region's start and length in string units.
- **Normalization steps**: the individual steps and the standard and comparison presets.
- **Bundled selection**: all entries, the default selection, or the entries in chosen categories.
- **Package**: the single npm artifact, its two module styles, its type declarations and its README.
- **Corpus runner**: the port's reader of `conformance/`, which decides whether the port may be released.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the corpus's cases pass on every supported Node.js release, with 0 cases reported
  as not applicable.
- **SC-002**: The port's bundled selections match the .NET package's entry by entry: 1,250 entries in all,
  1,025 in the default selection, and identical per-category counts and order (0 differences).
- **SC-003**: A developer new to the package can install it and flag a message, following the README
  quick start, in under 5 minutes, in both a JavaScript and a TypeScript project, without installing
  anything else.
- **SC-004**: The package has 0 runtime dependencies, and its unpacked size is under 1 MB.
- **SC-005**: On the machine the README names, building a filter from the bundled default list takes under
  50 ms, and checking a short clean chat message takes under 50 µs on average.
- **SC-006**: 100% of exported functions, types and options have API documentation, and 100% of the port
  README's code examples have a passing test.
- **SC-007**: The packed package loads and gives identical results through `import`, through `require`,
  from a TypeScript project, and inside a browser bundle: 4 of 4 consumer checks pass in CI.
- **SC-008**: A release tag publishes the npm and NuGet packages at the same version, and a failing
  JavaScript job or a tag that differs from `VERSION` publishes neither. Both are demonstrated by a real
  CI dry run on throwaway tags before `v1.3.0` is pushed, with no package published by the dry run.
- **SC-009**: .NET users notice nothing apart from FR-028 and FR-029: all existing .NET tests and all
  corpus cases still pass on .NET, and the .NET package still passes its compatibility check against
  1.2.0.
- **SC-010**: At the end of the feature, `persian-text-guard` `1.3.0` and `PersianTextGuard` `1.3.0` are
  both installable from their public registries, published from CI within the same release, and each
  passes a smoke check in a fresh project (2 of 2).

## Assumptions

- **Port layout**: the port lives in `js/` at the repository root, next to `dotnet/`, as the constitution
  names.
- **Supported Node.js releases**: on 2026-09-16 these are Node.js 22 (maintenance) and 24 (active). CI
  follows the Node.js release schedule, so Node.js 26 joins when it enters long-term support (October
  2026) and 22 leaves when it reaches end of life.
- **Browsers**: "current browsers" means the latest two versions of Chrome, Edge, Firefox and Safari.
  CI verifies the V8 engine (Chrome, Edge, Node.js) only. Firefox and Safari use the same standard
  language features but their own Unicode data, so the port README's Limitations section says they are
  supported but not CI-tested, and that the corpus is the reference if a difference is reported.
- **Behaviour source**: the corpus is the specification. The .NET package remains the source the corpus
  fill-in tool records from; the JavaScript port does not add a second fill-in tool.
- **Versioning**: a newly supported language is a MINOR change under Public API & Versioning, so the first
  release that includes npm is `1.3.0`. The .NET package at that version differs from 1.2.0 only in its
  version number and the two fixes in FR-028 and FR-029, both called out in the release notes.
- **npm account**: the maintainer owns or creates the npm account, claims `persian-text-guard` (free on
  npm on 2026-09-16), and configures npm to trust this repository's CI for publishing. These are
  account-settings steps the maintainer performs during the feature, before tagging `v1.3.0`; the plan
  states exactly what they are and in which order.
- **Release is irreversible**: publishing `1.3.0` cannot be undone on either registry (a version can be
  deprecated, not reused). The tag is pushed only after the merged `main` is green and the maintainer
  agrees.
- **Persian documentation**: the constitution's bilingual rule for package READMEs is applied to the new
  npm README from the start. Rewriting the full project README in Persian remains its own feature.
- **Performance**: numbers are measured on the maintainer's machine, as for .NET. The JavaScript port is
  not required to be as fast as .NET, only to meet SC-005.
- **Other ports**: Python, Java, Go and Rust are separate features and out of scope.
- **Branch**: no git branch was created, because the project has no Spec Kit git hook. Under the
  constitution's workflow, implementation happens on a branch and lands through a pull request, so a
  `003-javascript-typescript-port` branch should be created before `/speckit-implement`. `main` is
  protected and requires both existing CI checks.
