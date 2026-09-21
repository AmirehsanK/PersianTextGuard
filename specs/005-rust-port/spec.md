# Feature Specification: Rust Port Published to crates.io

**Feature Branch**: `005-rust-port`

**Created**: 2026-09-19

**Status**: Draft

**Input**: User description: "Rust port of PersianTextGuard, published to crates.io and released in lockstep with NuGet, npm and PyPI. Add a native Rust crate in a new top-level rust/ directory of the monorepo, next to dotnet/, js/ and python/, that gives the same answers as the .NET, JavaScript and Python packages for every case in the shared conformance corpus (conformance/), with 0 cases not applicable. It offers the same capabilities with Rust conventions: a ProfanityFilter built once from bundled or caller-supplied word lists (with the same options for squeezing repeated letters, folding look-alike characters and joining spaced letters), checking a message, finding the first match or every match with its entry, evasion kinds and position, censoring with a fixed four-character mask, the bundled Persian/Finglish/English word lists by category, word-list parsing and loading from a file or reader, and normalization, tokenizing and digit conversion. The bundled word lists are embedded at build time from the shared wordlists/ files, and the version comes from the shared VERSION file. The crate has no runtime dependencies beyond what Unicode handling strictly needs, is safe to share across threads (Send + Sync) and never panics on any input string. Positions must be natural for Rust callers and still map exactly onto the corpus's code-point positions. The port is tested by a corpus runner, Rust-specific tests, doc tests for every README example and benchmarks, documented in English and Persian on crates.io and docs.rs, checked for semver compatibility against the previous release, and gated in CI so that no registry publishes unless every port's jobs pass. Release as the next minor version, with .NET, JavaScript and Python behaviour unchanged."

## Clarifications

### Session 2026-09-19

- Q: A Rust string cannot hold a lone surrogate, and five corpus cases contain one (four inputs, one
  mask). How should the Rust port cover them, given the request for 0 cases not applicable? → A: Run
  them with U+FFFD in place of each lone surrogate, which is what a Rust service receives after lossy
  decoding and what .NET does before matching; the mask case passes by construction, since a Rust mask
  is a character. The corpus format contract is amended to allow this one replacement (FR-017).
- Q: Where should the Rust port get its Unicode character categories, which Rust's standard library does
  not provide? → A: From compact tables generated at development time from the Unicode data .NET uses,
  embedded in the crate, with CI checking that the committed tables match the generator (FR-004).
- Q: How should the very first crates.io release be published, given that trusted publishing may need
  the crate to exist first? → A: CI publishes 1.5.0 with a short-lived crates.io token limited to
  publishing new crates, stored only in a protected `crates-io` GitHub environment; the maintainer deletes
  it right after and sets up trusted publishing for every later release (FR-020).
- Q: Should the crate also accept raw bytes that may not be valid UTF-8, or only Rust strings? → A:
  Strings, plus byte-slice versions of checking, finding matches and censoring, which decode lossily
  inside and report positions in the caller's bytes (FR-008a).
- Q: Which minimum Rust version should the crate promise to support, and when may later releases raise
  it? → A: Rust 1.85 (February 2025, the 2024 edition), raised only in a MINOR release, announced in the
  release notes, and never to a version less than a year old (FR-002).

## User Scenarios & Testing *(mandatory)*

The people affected are:

- **Rust developers** who moderate chat, comments or form input in web services, bots and pipelines,
  and cannot use PersianTextGuard today;
- **teams running services in more than one language**, who need a message to get the same answer from
  their Rust, .NET, JavaScript and Python services;
- **the maintainer**, who releases every package together and must keep four ports in step;
- **existing .NET, npm and PyPI users**, who must notice nothing.

The project constitution already fixes much of this port's shape:

- a native implementation in stable Rust, with a declared minimum supported Rust version tested in CI;
- no runtime dependencies beyond the standard library and the one allowed crate for Unicode
  normalization;
- a filter that is immutable and `Send + Sync`;
- no panic for any message text;
- the same capabilities as every other port, with Rust naming (`find_matches`);
- positions in Rust's native string index unit, the byte;
- benchmarks with Criterion, and API compatibility checks with `cargo-semver-checks`;
- a release in lockstep with every other registry.

Features 002 to 004 provide what makes the port checkable:

- the shared word lists in `wordlists/`;
- the 523-case conformance corpus in `conformance/`, which the .NET, JavaScript and Python ports pass;
- a release workflow that publishes only when every port is green, proven by a dry run in 004.

### User Story 1 - Check and censor messages from Rust (Priority: P1)

A developer adds one crate and builds a filter from the bundled Persian, Finglish and English word list.
With it they check a message, find the first match or every match, and censor a message. The filter
reads through the same evasions as the other ports: spaced and dotted letters, held keys, look-alike
letters and digits, invisible characters and Persian suffixes. Ordinary messages pass untouched. Using
the crate wrongly, such as passing an entry with an unknown category, is caught by the compiler where
the language allows it.

**Why this priority**: this is the product. Checking and censoring messages is what every user of the
crate needs, and it is the smallest slice worth publishing.

**Independent Test**: in a new project that depends on the packaged crate, build a filter from the
bundled default list and confirm three results:

- `"ک.ی.ر"` is flagged;
- `"سلام، سفارشم کی میرسه؟"` is not flagged;
- `"kir and motherfucker"` censors to `"**** and ****"`.

**Acceptance Scenarios**:

1. **Given** a filter built from the bundled default list, **When** a caller checks `"f u c k"`, **Then**
   the result is that the message contains profanity.
2. **Given** the same filter, **When** a caller checks `"هر کس پلات بالاست پیام بده"`, **Then** the message
   is ordinary: there is no match, and censoring returns it unchanged.
3. **Given** the same filter, **When** a caller asks for every match in `"sh1t and f u c k"`, **Then** it
   gets two matches, in order:
   - `shit` (profanity) starting at byte 0, 4 bytes long, with look-alike characters as the evasion;
   - `fuck` (profanity) starting at byte 9, 7 bytes long, with a split word as the evasion.
4. **Given** a message with an emoji before a Persian banned word, `"😀 کیر"`, **When** a caller asks for
   the first match, **Then** it starts at byte 5 and is 6 bytes long, so slicing the message with the
   match's range gives exactly `"کیر"`.
5. **Given** a filter built from the caller's own entries (`"اسپم"` as a whole word, `"casino"` anywhere),
   **When** a caller checks `"onlinecasino.example"`, **Then** it is flagged.
6. **Given** a caller who chooses `#` as the mask, **When** they censor `"this is kir"`, **Then** the
   result is `"this is ####"`. **When** they choose a letter as the mask instead, **Then** they get an
   error value saying the mask is invalid, not a panic.
7. **Given** one filter, **When** many threads check messages with it at the same time, **Then** every
   thread gets the same results it would get alone, and the compiler accepts sharing the filter across
   threads without wrapping it in a lock.

---

### User Story 2 - The same answer as every other port, proven by the corpus (Priority: P1)

The Rust port runs every case in the shared conformance corpus and passes all of them. A team with
services in Rust, .NET, JavaScript and Python gets identical results for the same message and
configuration: the decisions, the matches, the positions (each in its language's unit) and the censored
output.

**Why this priority**: the constitution forbids releasing a port that fails any corpus case, so without
this story nothing can be published.

**Independent Test**: run the port's corpus runner against `conformance/`. Every case passes on every
tested Rust version and platform, and no case is reported as not applicable. A deliberately corrupted case is reported with its id, file, visible input and the fields that
differ.

**Acceptance Scenarios**:

1. **Given** the corpus in `conformance/`, **When** the Rust corpus runner runs, **Then** every case
   passes, including the robustness cases with noncharacters, characters outside the Basic Multilingual
   Plane and the 132,000-character message. Positions recorded in code points are converted to bytes
   before comparing.
2. **Given** a corpus case whose recorded result differs from what the port does, **When** the runner
   runs, **Then** that case fails with:
   - its id and its file;
   - its input, with invisible characters shown as `\u{...}` escapes;
   - each differing field, with its expected and actual value.

   The run continues and reports every other failing case.
3. **Given** a corpus directory that is missing or unreadable, or that has a newer format version, fewer
   than 300 cases, duplicate ids or pending cases, **When** the runner runs, **Then** it fails and says
   why, rather than passing with zero cases.
4. **Given** the bundled word lists, **When** the port's default, full and per-category selections are
   compared with the other ports', **Then** they contain the same entries in the same order.

---

### User Story 3 - Released to crates.io together with NuGet, npm and PyPI (Priority: P2)

When the maintainer tags a release, CI publishes the crates.io, PyPI, npm and NuGet packages at the same
version, taken from the single `VERSION` file. If the Rust port fails any of the following, nothing is
published anywhere:

- its tests, doc tests or the corpus on any tested Rust version or platform;
- the lints and formatting checks;
- the API compatibility check;
- packaging.

This feature ends with that first release done: `1.5.0` of every package, with crates.io included for
the first time. Developers find the crate with a README that shows installation and a quick start in both
Persian and English, and its API documentation on docs.rs.

**Why this priority**: publishing is the goal, but it depends on stories 1 and 2 being complete and
correct.

**Independent Test**: before the real release, run the release workflow on throwaway tags with publishing
replaced by dry runs, as in feature 004. Confirm that:

- the crate it would publish has the version in `VERSION` and contains only the intended files;
- a failing Rust job stops all four packages from publishing;
- a tag that differs from `VERSION` also stops all four.

Then tag `v1.5.0` and install all four published packages from their registries into fresh projects.

**Acceptance Scenarios**:

1. **Given** `VERSION` holds `X.Y.Z` and every job is green, **When** the maintainer pushes tag `vX.Y.Z`,
   **Then** the crates.io, PyPI, npm and NuGet packages are all published at `X.Y.Z`.
2. **Given** the Rust corpus fails, **When** a release tag is pushed, **Then** none of the four packages
   is published.
3. **Given** the crate's page on crates.io, **When** a Persian-speaking developer reads it, **Then**
   installation and a quick start are there in Persian as well as in English, with a link to the full
   project README and to the API documentation.
4. **Given** the published crate, **When** a developer opens its documentation on docs.rs, **Then** the
   documentation built successfully and covers every public item.
5. **Given** the merged feature and `VERSION` set to `1.5.0`, **When** the maintainer pushes tag
   `v1.5.0`, **Then** the crate `persian-text-guard` `1.5.0`, the PyPI and npm packages
   `persian-text-guard` `1.5.0` and the NuGet package `PersianTextGuard` `1.5.0` all install from their
   public registries, and each flags `"ک.ی.ر"` in a fresh project.

---

### User Story 4 - Documented, measured and kept compatible (Priority: P3)

A Rust developer can read what every public type, function and option does, including edge cases, in the
generated API documentation and in their editor. Every example in that documentation and in the README
is compiled and run as a test. The README states how fast checking a message is, measured on a named
machine. CI compares the public API with the previous release, so that a later change that would break
existing callers is caught before release.

**Why this priority**: the constitution requires it for every port, but the crate is usable without it.
It protects users from the second release onward.

**Independent Test**:

- Build the API documentation with warnings treated as errors, including for missing documentation.
- Run the benchmarks and compare them with the README table.
- On a scratch branch, change the signature of a public function and confirm that the API check fails.

**Acceptance Scenarios**:

1. **Given** the API documentation, **When** a developer looks up any public item, **Then** its
   documentation explains behaviour and edge cases, not only the signature.
2. **Given** the previous release, **When** a pull request removes or changes a public item, **Then**
   the API check fails and names the change. For the first release, the check records the baseline.
3. **Given** every code example in the crate's README and API documentation, **When** the test suite
   runs, **Then** each example compiles and runs.

---

### Edge Cases

- **Empty and blank input**: `""` is never flagged and censors to `""`; whitespace-only text is never
  flagged and censors to itself unchanged, as the corpus records. Rust has no null string, so there is no
  separate missing value; an absent message is the caller's `Option` to handle. The four corpus cases
  whose input is `null` (the language's missing value) run with the empty string, whose recorded results
  are the same (found during planning).
- **Text Rust strings cannot hold**: a Rust string is always valid UTF-8, so it cannot contain a lone
  surrogate. Five corpus cases do: four robustness inputs, such as a message cut in the middle of an
  emoji, and one mask that is half of a surrogate pair. The Rust port runs all five (clarified
  2026-09-19):
  - each lone surrogate in a corpus input or expected value is replaced by U+FFFD, which is what a Rust
    service receives after decoding such text lossily, and what .NET itself does to a lone surrogate
    before matching. The case must then pass like any other; positions still count the replacement as
    one code point;
  - the mask case passes by construction: a Rust mask is a character, which can never be a surrogate, so
    such a mask cannot be passed at all.

  This replacement is the only change a Rust runner makes to a case, and the corpus format contract is
  amended to allow it for ports whose strings cannot hold lone surrogates. If the .NET results for any
  of these inputs with U+FFFD differ from the recorded ones other than in the replaced character, the
  difference is reported to the maintainer before anything else changes.
- **Invalid UTF-8 from outside**: a caller holding raw bytes, such as an undecoded request body, can pass
  them straight to the byte versions of checking, finding matches and censoring (FR-008a). Each invalid
  sequence is read as U+FFFD, exactly as Rust's standard lossy decoding reads it, and the answer is the
  same as for that decoded string; positions and censored output refer to the caller's own bytes, with
  every byte outside a masked region, invalid ones included, returned unchanged.
- **Noncharacters and characters outside the Basic Multilingual Plane**: a message containing U+FFFE,
  U+FDD0, emoji, CJK Extension B letters or mathematical letters is handled without error, and matches
  and positions equal the corpus's.
- **Positions and slicing**: every reported position falls on a character boundary, so slicing the
  message with a match's range never panics and always gives whole characters.
- **Very long messages**: a 132,000-character message is checked without error, within the time in
  SC-005.
- **Unicode differences between runtimes**: Rust's own character properties and the normalization data
  the crate uses may follow a different Unicode version from .NET, JavaScript and Python. The port matches
  the corpus, not its runtime's default. If a difference comes up that the corpus does not cover yet, a
  case is added to the corpus first, the other ports must pass it too, and only then is the port changed.
- **Sharing across threads**: a filter shared by many threads gives the same results as when used alone.
  The bundled lists are prepared once, on first use, even when many threads ask at the same moment.
- **Duplicate and blank entries**: spellings that normalize to the same entry count once, and blank
  entries are ignored, as in the other ports.
- **Mask edge cases**: the mask is rejected before the message is looked at, even when the message is
  empty, if it is a letter, a digit, whitespace, a control character, or a character outside the Basic
  Multilingual Plane (a .NET mask is one UTF-16 unit, and every port rejects such a character).
- **Word-list text errors**: an unknown category heading is reported with its line number. Windows line
  endings, comments, blank lines and `~` markers with extra spaces are read as the other ports read them.
  A heading is a category name only, in any letter case and with surrounding spaces allowed; a number such
  as `[3]` is an unknown category.
- **Word-list files**: a file that cannot be opened or read, or that is not valid UTF-8, is reported as an
  error value that names the problem; a UTF-8 byte order mark at the start is ignored.
- **Building without the repository**: the published crate builds on its own, from crates.io, without
  `wordlists/`, `VERSION` or anything else from this repository.

## Requirements *(mandatory)*

### Functional Requirements

**Crate and installation**

- **FR-001**: The port MUST be a single crate named `persian-text-guard` on crates.io, used in code as
  `persian_text_guard`, living in `rust/` at the repository root.
- **FR-002**: The crate MUST build on stable Rust, with a minimum supported Rust version of 1.85 (the
  2024 edition) declared in its manifest and stated in the README (clarified 2026-09-19). CI MUST test
  both 1.85 and the current stable release. A later release MAY raise the minimum only in a MINOR
  version, announced in the release notes in English and Persian, and never to a Rust release less than
  a year old at that time.
- **FR-003**: CI MUST build and test the crate on Linux, Windows and macOS.
- **FR-004**: The crate MUST have no runtime dependencies beyond the standard library and those the
  constitution's allowlist names for Rust. The Unicode general categories the matcher tests, which the
  standard library does not provide, and the one-unit lower-casing, which the standard library ties to the
  compiler's Unicode version, MUST come from compact tables generated at development time from the Unicode
  data .NET uses, and embedded in the crate (clarified 2026-09-19; lower-casing added during planning,
  research R1). The generator MUST live
  in the repository, CI MUST fail when the committed tables differ from what it produces, and the tables
  MUST agree with .NET's category and lower-casing for every code point that .NET's Unicode version
  assigns.
- **FR-005**: The bundled word lists MUST be taken from `wordlists/` when the crate is built or packaged,
  and the crate's version MUST come from `VERSION`. The port MUST NOT keep its own hand-edited copy of
  the list files, and the published crate MUST build without the rest of the repository.
- **FR-006**: The published crate MUST contain only what users need to build and use it: the library
  source, the bundled word lists, the README, the licence and the third-party notices. Tests,
  benchmarks, corpus files and CI configuration MUST NOT be published.

**Capabilities** (the same as every port, constitution Principle V)

- **FR-007**: Users MUST be able to build a filter from any collection of entries and from options.
  - Each entry has its text, a match mode (whole word or anywhere) and a category.
  - The options switch reading through held keys, look-alike characters and split words on or off. Each
    is on by default.
- **FR-008**: A filter MUST let users:
  - check whether a message contains profanity;
  - find the first match, or every match;
  - censor a message, with the default mask `*` or a chosen one.

  It MUST also report how many distinct entries it holds.
- **FR-008a**: A filter MUST also offer byte versions of checking, finding the first match, finding every
  match and censoring, which take a byte slice that may not be valid UTF-8 (clarified 2026-09-19):
  - the bytes are read as Rust's standard lossy decoding reads them, each invalid sequence as one U+FFFD,
    and the decision and matches equal those for the decoded string;
  - match positions are start and length in the caller's bytes: a region that covers a U+FFFD covers the
    whole invalid sequence it came from, and never splits a valid character;
  - censoring returns bytes: each region becomes four copies of the mask's UTF-8 encoding, and every other
    byte, valid or not, is copied unchanged.

  These are a Rust convenience over the same capabilities, like Python's file loading, not a new
  capability under Principle V; they never fail and never panic, for any bytes.
- **FR-009**: Each match MUST report:
  - the entry exactly as the caller gave it;
  - the evasions that had to be undone: held keys, look-alike characters, a split word, or none;
  - the matched region's start and length in bytes of the message, covering whole words as the other
    ports do, always on character boundaries.
- **FR-010**: Users MUST be able to:
  - normalize Persian text with the standard or comparison preset, or with any combination of the
    individual steps;
  - tokenize text;
  - convert digits to Persian and to ASCII.
- **FR-011**: Users MUST be able to parse word-list text in the shared format, and to select the bundled
  lists as all entries, as the default selection (everything except mild), or by chosen categories. Users
  MUST also be able to load a word-list file from a path or from any reader:
  - the input is read as UTF-8, and a UTF-8 byte order mark at the start is ignored;
  - the text is then parsed exactly as the text-parsing function parses it;
  - a reader the caller passes is read, and remains the caller's.
- **FR-012**: Public names MUST follow Rust conventions: snake_case functions and methods
  (`contains_profanity`, `find_matches`, `normalize`), CamelCase types (`ProfanityFilter`, `WordList`,
  `WordCategory`), and enums for match modes, categories, evasion kinds and normalization steps. The
  port's README MUST include a table that maps each .NET, JavaScript and Python name to its Rust name.
- **FR-013**: Programmer errors MUST be reported as error values (`Result`) at the call that makes them,
  and nothing else may fail. The programmer errors are:
  - an invalid mask: a letter, a digit, whitespace, a control character, or a character outside the
    Basic Multilingual Plane;
  - word-list text naming an unknown category, reported with its line number;
  - a word-list file or reader that cannot be read, or that is not valid UTF-8, when loading one;
  - an unknown category or normalization step given by name, where the API accepts names.

  Mistakes the type system can rule out, such as an entry with an unknown category, MUST be ruled out by
  the type system instead.

**Robustness and sharing**

- **FR-014**: Checking, finding matches, censoring, normalizing and tokenizing MUST return a result, and
  MUST NOT panic, for every string: empty, whitespace-only, with noncharacters or characters outside the
  Basic Multilingual Plane, or very long. The corpus robustness cases and a randomized test over
  arbitrary strings MUST show this.
- **FR-015**: A filter MUST be immutable after it is built, `Send + Sync`, and safe to reuse for any
  number of messages and to share across threads without a lock. Work that can be done once, such as
  normalizing entries and building lookups, MUST happen when the filter is built. Checking a message MUST
  NOT take time proportional to the number of whole-word entries.

**Conformance**

- **FR-016**: The port MUST include a corpus runner that reads `conformance/` and runs every case. It
  MUST follow the runner obligations in the corpus format contract:
  - refuse a newer format version;
  - fail on a missing or unreadable corpus, on fewer than 300 cases, on duplicate ids and on pending
    cases;
  - check the kind rules before the results;
  - compare every recorded field exactly, converting only positions, from code points to bytes, and
    replacing lone surrogates with U+FFFD as FR-017 allows;
  - report each failing case with its id, file, visible input and differing fields, and continue past
    failures.
- **FR-017**: The port MUST pass 100% of corpus cases on every tested Rust version and platform, with 0
  cases reported as not applicable. The five cases with lone surrogates run with U+FFFD in place of each
  lone surrogate, the mask case is satisfied by the mask's type, and the four cases with a `null` input
  run with the empty string (see Edge Cases). The corpus format contract (spec 002) MUST be amended in
  this feature to allow these two readings, and only these, for ports whose strings cannot hold lone
  surrogates or have no missing string value.
- **FR-018**: The port's bundled default, full and per-category selections MUST contain the same entries,
  in the same order, as the .NET, JavaScript and Python packages built from the same `wordlists/`.
- **FR-019**: The port's own tests MUST cover the behaviour specific to Rust:
  - byte positions that slice the message exactly, including around multi-byte and supplementary
    characters;
  - the byte versions (FR-008a): results equal to the lossily decoded string, positions that slice the
    caller's bytes, invalid bytes outside masked regions copied unchanged;
  - the mask and word-list errors, including loading from a path and from a reader;
  - immutability, and `Send + Sync`, checked at compile time;
  - concurrent use from many threads;
  - no panic on arbitrary input.

**Release, compatibility and documentation**

- **FR-020**: The crate version MUST come from `VERSION`. A release tag MUST publish the crates.io, PyPI,
  npm and NuGet packages at the same version. It publishes only when the tag matches `VERSION` and every
  job for every port is green; otherwise none of them is published. The crate MUST be published from CI
  (clarified 2026-09-19):
  - `1.5.0`, the first version, with a short-lived crates.io token limited to publishing new crates,
    stored only as a secret of a `crates-io` GitHub environment that only `v*` tags may deploy to. The
    maintainer deletes the token, and the secret, right after the release;
  - every later version through crates.io trusted publishing from this repository's CI, with no stored
    token, configured by the maintainer once `1.5.0` exists.

  Re-running the publish job after a partial release MUST be safe: a version already on crates.io is
  skipped.
- **FR-021**: On every pull request, CI MUST run for the Rust port:
  - the tests, doc tests and the full corpus, on the minimum supported and the current stable Rust, on
    Linux, Windows and macOS;
  - the standard lints with warnings treated as errors, and a formatting check;
  - a documentation build with warnings treated as errors, including missing documentation;
  - a packaging check that the crate contains only the intended files and builds on its own;
  - the API compatibility check (FR-022).

  Existing job names MUST stay unchanged.
- **FR-022**: The crate's public API MUST be compared in CI with the previous release using
  `cargo-semver-checks`. The first release records the baseline. From then on, a change that would break
  existing callers fails CI unless the major version changes.
- **FR-023**: Every public type, function, method, field, enum variant and option MUST have
  documentation, in English, that explains behaviour and edge cases, and the main entry points MUST have
  runnable examples.
- **FR-024**: The port MUST have its own README, shown on crates.io, containing:
  - installation and a quick start in both Persian and English;
  - the name table from FR-012;
  - a performance table measured with Criterion on a named machine;
  - links to the full project README and to the API documentation.

  Every code example in it MUST be compiled and run as a test. The Persian text MUST stay readable where
  crates.io shows it left to right.
- **FR-025**: The project README MUST list the Rust crate next to the NuGet, npm and PyPI packages. Its
  Development section MUST show the `rust/` directory and the commands that test the port, run the corpus
  and run the benchmarks.
- **FR-026**: The .NET, JavaScript and Python packages, the corpus's existing cases and the shared word
  lists MUST NOT change behaviour. Corpus cases MAY be added for Unicode or string differences the Rust
  port brings to light (see Edge Cases), and every port MUST pass each added case.
- **FR-027**: The feature MUST end with the first release that includes crates.io, `1.5.0`:
  - after the pull request merges, `VERSION` holds `1.5.0` and tag `v1.5.0` is pushed;
  - CI publishes `persian-text-guard` `1.5.0` to crates.io, PyPI and npm, and `PersianTextGuard` `1.5.0`
    to NuGet;
  - all four are installed from their public registries into fresh projects and shown to work, and the
    crate's documentation is live on docs.rs.

  A GitHub release for `v1.5.0` MUST have English notes and a Persian summary. They announce the Rust
  crate and state that .NET, JavaScript and Python behaviour is unchanged.

### Key Entities

- **Filter**: built once from entries and options, and answers every question about a message. It is
  immutable and shareable across threads.
- **Entry**: a word or phrase to look for, with its match mode (whole word or anywhere) and its category:
  uncategorized, profanity, sexual, insult, slur, harassment or mild.
- **Options**: the three evasions a filter reads through, each on by default.
- **Match**: the entry found, the evasions undone, and the region's start and length in bytes of the
  message: the caller's string, or the caller's byte slice for the byte versions.
- **Normalization steps**: the individual steps, and the standard and comparison presets.
- **Bundled selection**: all entries, the default selection, or the entries in chosen categories.
- **Errors**: an invalid mask, an unknown category heading with its line, an unreadable or non-UTF-8
  word-list input, an unknown name.
- **Crate**: the package on crates.io, its README and its documentation on docs.rs.
- **Corpus runner**: the port's reader of `conformance/`, which decides whether the port may be released.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the corpus's cases pass on every tested Rust version and platform, with 0 cases
  reported as not applicable.
- **SC-002**: The port's bundled selections match the other ports' entry by entry: the same total, the
  same default-selection count, and the same per-category counts and order (0 differences).
- **SC-003**: A developer new to the crate can add it and flag a message by following the README quick
  start, in under 5 minutes, with nothing else to install beyond the Rust toolchain.
- **SC-004**: The crate has no runtime dependencies beyond the constitution's allowlist, and the published
  crate file is under 1 MB.
- **SC-005**: These are the targets on the machine the README names, confirmed by a measurement during
  planning:
  - building a filter from the bundled default list takes under 5 ms;
  - checking a short, clean chat message takes under 5 µs on average;
  - checking the 132,000-character corpus message takes under 50 ms.
- **SC-006**: 100% of public items are documented (the documentation build fails otherwise), and 100% of
  the code examples in the README and the API documentation compile and pass.
- **SC-007**: Zero panics across the corpus inputs, at least 100,000 randomly generated strings
  (including multi-byte, supplementary, noncharacter and very long ones) and at least 100,000 random byte
  sequences, most of them invalid UTF-8; and for every byte sequence, the byte versions agree with the
  string versions on the lossily decoded text.
- **SC-008**: Two runs from many threads give results identical to a single-threaded run: 8 threads, each
  checking every corpus message, in a CI job.
- **SC-009**: A release tag publishes all four packages at the same version, and either of these publishes
  none of them:
  - a failing Rust job;
  - a tag that differs from `VERSION`.

  Both are shown by a real CI dry run on throwaway tags before `v1.5.0` is pushed, and the dry run
  publishes no package.
- **SC-010**: .NET, npm and PyPI users notice nothing: all their existing tests and corpus runs still
  pass, and all three packages still pass their compatibility checks against 1.4.0.
- **SC-011**: At the end of the feature, all four packages at `1.5.0` install from their public
  registries, were published from CI in the same release, and pass a smoke check in a fresh project (4 of
  4), and the crate's documentation is live on docs.rs.

## Assumptions

- **Port layout**: the port lives in `rust/` at the repository root, next to `dotnet/`, `js/` and
  `python/`, as the constitution names.
- **Positions**: the constitution fixes bytes as Rust's position unit. A match gives its start and length
  in bytes, so callers can slice the message directly; the corpus runner converts the corpus's code-point
  positions to bytes before comparing.
- **Missing message**: Rust has no null string. The crate takes string slices; an empty string is the
  empty message, and an absent message is the caller's `Option`, outside the crate.
- **Minimum supported Rust version**: 1.85 (FR-002). It was released in February 2025, about 18 months
  before this feature, and provides every standard-library feature the port needs, including lazy
  one-time initialization for the bundled lists.
- **Unicode data**: the Rust standard library does not expose general categories, which the matcher
  relies on, and the constitution allows only `unicode-normalization` as a runtime dependency. So the
  categories come from generated, embedded tables pinned to .NET's Unicode version (FR-004), and no
  constitution amendment is needed. Normalization still comes from `unicode-normalization`, whose Unicode
  version may differ from .NET's; the research phase measures it, and Rust's own case mapping and
  whitespace rules, against .NET on every code point, as 003 and 004 did.
- **Crate name**: `persian-text-guard` is unclaimed on crates.io (checked 2026-09-19); `persian_text_guard`
  and `persiantextguard` are also free.
- **crates.io account and publishing**: the maintainer owns or creates the crates.io account, and
  performs the account steps FR-020 implies, in the order the plan gives: create the `crates-io` GitHub
  environment and the narrowly scoped token before the dry run and release, delete the token after
  `1.5.0`, then configure trusted publishing. This is the one release in which a token is stored, for a
  limited time; the release notes and verification record say so.
- **Toolchain**: Rust is not installed on the maintainer's machine yet (checked 2026-09-19); installing
  it is part of the work, with the maintainer's approval.
- **Behaviour source**: the corpus is the specification. The .NET package remains the source that the
  corpus fill-in tool records from.
- **Versioning**: a newly supported language is a MINOR change, so the first release that includes
  crates.io is `1.5.0`. Releases are lockstep, so NuGet, npm and PyPI also get `1.5.0`, identical to
  `1.4.0` except for the version number. The release notes say so.
- **Release is irreversible**: publishing `1.5.0` cannot be undone on any of the four registries. A crate
  version can be yanked but never reused. The tag is pushed only after the merged `main` is green and the
  maintainer agrees.
- **Performance**: the targets in SC-005 assume a native, compiled port on the maintainer's machine and
  are of the same order as the .NET port's measured numbers (2.5 µs for a short clean message, 600 µs to
  build a filter).
- **Persian documentation**: the constitution's bilingual rule for package READMEs applies to the
  crates.io README from the start.
- **Other ports**: Java and Go are separate features and out of scope.
- **Branch**: `005-rust-port` was created from `main` at `f611f88`, after the 1.4.0 release. `main`
  requires nine CI checks (.NET, JavaScript and Python); the Rust checks are added to the required checks
  after this feature merges.
