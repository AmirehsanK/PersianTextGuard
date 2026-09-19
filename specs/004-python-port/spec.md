# Feature Specification: Python Port Published to PyPI

**Feature Branch**: `004-python-port`

**Created**: 2026-09-18

**Status**: Draft

**Input**: User description: "now lets go for the next language, python."

## Clarifications

### Session 2026-09-18

- Q: CPython 3.10 reaches end of life on 2026-10-31, around the time this feature ships. Should the
  package support 3.10, or start at 3.11? → A: Start at 3.11. The package requires 3.11 or later and is
  tested on every supported version from 3.11 (FR-003).
- Q: Should the package read a word-list file for the caller, as .NET's `WordList.Load` does, or only
  parse text the caller has already read, as the JavaScript package does? → A: Read it. `load` takes a file
  path or an open file, reads it as UTF-8 and parses it (FR-011, FR-013).

## User Scenarios & Testing *(mandatory)*

The people affected are:

- **Python developers** who moderate chat, comments or form input in web services, bots and data
  pipelines, and cannot use PersianTextGuard today;
- **developers who check types**, who want full type information from the package itself;
- **teams running services in more than one language**, who need a message to get the same answer from
  their Python, .NET and JavaScript services;
- **the maintainer**, who releases every package together and must keep three ports in step;
- **existing .NET and npm users**, who must notice nothing.

The project constitution (v2.0.0) already fixes much of this port's shape:

- pure Python with type hints;
- tested on every CPython version in upstream support;
- no runtime dependencies;
- safe to share across threads, including on free-threaded CPython;
- the same capabilities as every other port;
- positions in Python's native string unit, the code point;
- benchmarks with pyperf;
- a release in lockstep with NuGet and npm.

Features 002 and 003 provide what makes the port checkable:

- the shared word lists in `wordlists/`;
- the 513-case conformance corpus in `conformance/`, which both existing ports pass;
- a release workflow that publishes only when every port is green.

### User Story 1 - Check and censor messages from Python (Priority: P1)

A developer installs one package and builds a filter from the bundled Persian, Finglish and English word
list. With it they check a message, find the first match or every match, and censor a message. The filter
reads through the same evasions as the other ports: spaced and dotted letters, held keys, look-alike
letters and digits, invisible characters and Persian suffixes. Ordinary messages pass untouched. A
developer who runs a type checker gets types and autocomplete, and mistakes are caught before running.

**Why this priority**: this is the product. Checking and censoring messages is what every user of the
package needs, and it is the smallest slice worth publishing.

**Independent Test**: install the built package into a fresh virtual environment. Build a filter from the
bundled default list and confirm three results:

- `"ک.ی.ر"` is flagged;
- `"سلام، سفارشم کی میرسه؟"` is not flagged;
- `"kir and motherfucker"` censors to `"**** and ****"`.

A type checker in strict mode accepts the quick-start code without any extra type package.

**Acceptance Scenarios**:

1. **Given** a filter built from the bundled default list, **When** a caller checks `"f u c k"`, **Then**
   the result is that the message contains profanity.
2. **Given** the same filter, **When** a caller checks `"هر کس پلات بالاست پیام بده"`, **Then** the message
   is ordinary: there is no match, and censoring returns it unchanged.
3. **Given** the same filter, **When** a caller asks for every match in `"sh1t and f u c k"`, **Then** it
   gets two matches, in order:
   - `shit` (profanity) at index 0, length 4, with look-alike characters as the evasion;
   - `fuck` (profanity) at index 9, length 7, with a split word as the evasion.
4. **Given** a message with an emoji before a banned word, `"😀 کیر"`, **When** a caller asks for the
   first match, **Then** its index is 2 and its length 3, counted in code points. So
   `message[index:index + length]` is exactly the matched word.
5. **Given** a filter built from the caller's own entries (`"اسپم"` as a whole word, `"casino"` anywhere),
   **When** a caller checks `"onlinecasino.example"`, **Then** it is flagged.
6. **Given** a caller who chooses `#` as the mask, **When** they censor `"this is kir"`, **Then** the
   result is `"this is ####"`. **When** they choose a letter as the mask instead, **Then** they get an
   error saying the mask is invalid.
7. **Given** a project checked by a type checker in strict mode, **When** a developer passes an unknown
   option name or a category that does not exist, **Then** the type checker reports an error.
8. **Given** one filter, **When** many threads check messages with it at the same time, including on
   free-threaded CPython, **Then** every thread gets the same results it would get alone.

---

### User Story 2 - The same answer as every other port, proven by the corpus (Priority: P1)

The Python port runs every case in the shared conformance corpus and passes all of them, on every
supported CPython version. A team with services in Python, .NET and JavaScript gets identical results for
the same message and configuration: the decisions, the matches, the positions (each in its language's
unit) and the censored output.

**Why this priority**: the constitution forbids releasing a port that fails any corpus case, so without
this story nothing can be published. It is also the promise that makes a third language trustworthy.

**Independent Test**: run the port's corpus runner against `conformance/`. Every case passes on every
supported CPython version, and no case is reported as not applicable. A deliberately corrupted case is
reported with its id, file, visible input and the fields that differ.

**Acceptance Scenarios**:

1. **Given** the corpus in `conformance/`, **When** the Python corpus runner runs, **Then** all 513
   cases pass. That includes the robustness cases with lone surrogates and noncharacters, and the
   132,000-character message.
2. **Given** a corpus case whose recorded result differs from what the port does, **When** the runner
   runs, **Then** that case fails with:
   - its id and its file;
   - its input, with invisible characters shown as `\uXXXX`;
   - each differing field, with its expected and actual value.

   The run continues and reports every other failing case.
3. **Given** a corpus directory that is missing or unreadable, or that has a newer format version, fewer
   than 300 cases, duplicate ids or pending cases, **When** the runner runs, **Then** it fails and says
   why, rather than passing with zero cases.
4. **Given** the bundled word lists, **When** the port's default, full and per-category selections are
   compared with the other ports', **Then** they contain the same entries in the same order.

---

### User Story 3 - Released to PyPI together with NuGet and npm (Priority: P2)

When the maintainer tags a release, CI publishes the PyPI, npm and NuGet packages at the same version,
taken from the single `VERSION` file. If the Python port fails any of the following, nothing is published
anywhere:

- its tests or the corpus on any supported CPython version;
- the type check;
- the API compatibility check;
- packaging.

This feature ends with that first release done: `1.4.0` of every package, with PyPI included for the
first time. Developers find the PyPI package with a README that shows installation and a quick start in
both Persian and English.

**Why this priority**: publishing is the goal, but it depends on stories 1 and 2 being complete and
correct. A release that happened early would ship a port the constitution does not allow.

**Independent Test**: before the real release, run the release workflow on throwaway tags with publishing
replaced by dry runs. Confirm three things:

- the PyPI package it would publish has the version in `VERSION` and contains only the intended files;
- a failing Python job stops all three packages from publishing;
- a tag that differs from `VERSION` also stops all three.

Then tag `v1.4.0` and install all three published packages from their registries into fresh projects.

**Acceptance Scenarios**:

1. **Given** `VERSION` holds `X.Y.Z` and every job is green, **When** the maintainer pushes tag `vX.Y.Z`,
   **Then** the PyPI, npm and NuGet packages are all published at `X.Y.Z`.
2. **Given** the Python corpus fails, **When** a release tag is pushed, **Then** none of the three
   packages is published.
3. **Given** the package's page on PyPI, **When** a Persian-speaking developer reads it, **Then**
   installation and a quick start are there in Persian, right to left, as well as in English, with a link
   to the full project README.
4. **Given** the published package, **When** a developer checks where it came from, **Then** PyPI shows
   that it was built and published from this repository's CI.
5. **Given** the merged feature and `VERSION` set to `1.4.0`, **When** the maintainer pushes tag
   `v1.4.0`, **Then** `pip install persian-text-guard==1.4.0`, `npm install persian-text-guard@1.4.0` and
   the NuGet package `PersianTextGuard` `1.4.0` all install from their public registries, and each flags
   `"ک.ی.ر"` in a fresh project.

---

### User Story 4 - Documented, measured and kept compatible (Priority: P3)

A Python developer can read what every public function, class and option does, including edge cases,
through `help()` and in their editor. The port's README states how fast checking a message is, measured
on a named machine. CI records the package's public API, so that a later change that would break existing
callers is caught before release.

**Why this priority**: the constitution requires it for every port, but the package is usable without it.
It protects users from the second release onward.

**Independent Test**:

- Call `help()` on each public name and read its documentation.
- Run the benchmarks and compare them with the README table.
- On a scratch branch, change the signature of a public function and confirm that CI fails the API check.

**Acceptance Scenarios**:

1. **Given** an editor or `help()`, **When** a developer looks up any public function, class or option,
   **Then** its documentation explains behaviour and edge cases, not only the signature.
2. **Given** the recorded public API, **When** a pull request removes or changes a public member, **Then**
   CI fails and names the change.
3. **Given** every code example in the port's README, **When** the test suite runs, **Then** each example
   has a test that proves it.

---

### Edge Cases

- **Missing and blank input**:
  - `None` and `""` are never flagged, never throw, and censor to `""`;
  - whitespace-only text is never flagged and censors to itself unchanged;
  - all of this is as the corpus records.
- **Values that are not text**: a caller may pass `bytes`, a number, a dict or a list where a message is
  expected, for example a request body that was never validated. Checking, finding matches, censoring,
  normalizing and tokenizing raise `TypeError` for any value that is neither a string nor `None`. This
  matches the JavaScript port's decision: a missing validation step shows up at once instead of an
  unchecked value passing as clean. Subclasses of `str` are accepted as strings. The README tells callers
  to decode bytes before checking.
- **Lone surrogates**: Python strings can hold lone surrogates, for example text decoded with
  `surrogateescape` or cut in the middle of a JSON escape pair. Such a message is handled without error,
  and positions still slice the original string correctly.
- **Noncharacters**: a message that contains U+FFFE, U+FFFF, U+FDD0 or any other noncharacter is handled
  without error, as the corpus requires.
- **Very long messages**: a 132,000-character message is checked without error, within the time in
  SC-005.
- **Unicode differences between runtimes**: Python and .NET can disagree on Unicode details:
  - compatibility normalization of rare characters;
  - lower-casing (Python lower-cases `İ` to two characters, `i̇`);
  - which characters count as whitespace, as letters or as digits;
  - the Unicode version itself, which differs between CPython releases.

  The port matches the corpus, not its runtime's default. If a difference comes up that the corpus does
  not cover yet, a case is added to the corpus first, and the .NET and JavaScript packages must pass it
  too; only then is the port changed.
- **Free-threaded CPython**: when the global interpreter lock is disabled, a shared filter used from many
  threads at once gives the same results as when used alone, and never raises.
- **Entries changed after building**: if a caller changes the entry objects, lists or options they passed
  after building a filter, the filter's behaviour does not change.
- **Duplicate and blank entries**: spellings that normalize to the same entry count once, and blank
  entries are ignored, as in the other ports.
- **Mask edge cases**: the mask is rejected before the message is looked at, even when the message is
  missing, if it is:
  - empty, or longer than one character;
  - a letter, a digit, whitespace or a control character;
  - a surrogate.
- **Word-list text errors**: an unknown category heading is reported with its line number. Windows line
  endings, comments, blank lines and `~` markers with extra spaces are read as the other ports read them.
  A heading is a category name only, in any letter case and with surrounding spaces allowed; a number such
  as `[3]` is an unknown category.
- **Installing without the repository**: the package installs from a wheel and from a source
  distribution. Both carry the bundled word lists and need nothing from this repository at install time.

## Requirements *(mandatory)*

### Functional Requirements

**Package and installation**

- **FR-001**: The port MUST be a single PyPI package named `persian-text-guard`, imported as
  `persian_text_guard`. It MUST ship its type information as part of the package, marked as typed, with
  no separate stub package.
- **FR-002**: The package MUST be pure Python: one universal wheel that installs on every supported
  CPython version and operating system, plus a source distribution that builds that same wheel.
- **FR-003**: The package MUST require CPython 3.11 or later, declared in the package metadata, and MUST
  be tested on every CPython version from 3.11 that is in upstream support (clarified 2026-09-18). CI
  MUST also run the tests and the corpus on free-threaded CPython.
- **FR-004**: The package MUST have no runtime dependencies.
- **FR-005**: The bundled word lists MUST be taken from `wordlists/` when the package is built. The port
  MUST NOT keep its own hand-edited copy of the list files in the repository.
- **FR-006**: The published wheel and source distribution MUST contain only what users need:
  - the library itself and its type information;
  - the bundled word lists;
  - the README, the licence and the third-party notices.

  Tests, benchmarks, corpus files and CI configuration MUST NOT be published.

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
- **FR-009**: Each match MUST report:
  - the entry exactly as the caller gave it;
  - the evasions that had to be undone: held keys, look-alike characters, a split word, or none;
  - the matched region's start index and length, in code points, covering whole words as the other ports
    do.
- **FR-010**: Users MUST be able to:
  - normalize Persian text with the standard or comparison preset, or with any combination of the
    individual steps;
  - tokenize text;
  - convert digits to Persian and to ASCII.
- **FR-011**: Users MUST be able to parse word-list text in the shared format, and to select the bundled
  lists as all entries, as the default selection (everything except mild), or by chosen categories. Users
  MUST also be able to load a word-list file by giving its path or an open file (clarified 2026-09-18):
  - the file is read as UTF-8, and a UTF-8 byte order mark at the start is ignored;
  - the text is then parsed exactly as the text-parsing function parses it;
  - an open file is read but not closed, because the caller owns it.
- **FR-012**: Public names MUST follow Python conventions: snake_case functions and methods
  (`contains_profanity`, `find_matches`, `normalize`), PascalCase classes (`ProfanityFilter`, `WordList`)
  and upper-case enum members. The port's README MUST include a table that maps each .NET and JavaScript
  name to its Python name.
- **FR-013**: Programmer errors MUST raise an exception at the call that makes them, and nothing else
  may raise. The programmer errors are:
  - an invalid mask: not exactly one character, or a letter, a digit, whitespace, a control character or
    a surrogate;
  - a missing or non-iterable entry collection, or an entry without string text, a known mode and a known
    category;
  - word-list text naming an unknown category, reported with its line number;
  - a word-list file that cannot be opened, or that is not valid UTF-8, when loading one. The error is
    the one Python raises for that file problem, with the file named;
  - an unknown category passed to a bundled-list selection, or an unknown normalization preset or step;
  - a message or word-list text that is neither a string nor `None`. This raises `TypeError` from
    checking, finding matches, censoring, normalizing, tokenizing and parsing.

**Robustness and sharing**

- **FR-014**: Checking, finding matches, censoring, normalizing and tokenizing MUST return a result, never
  raise, for `None` and for any string: empty, whitespace-only, with lone surrogates or noncharacters,
  or very long.
- **FR-015**: A filter MUST be immutable after it is built, and safe to reuse for any number of messages
  and to share across threads, on both standard and free-threaded CPython. Work that can be done once,
  such as normalizing entries and building lookups, MUST happen when the filter is built. Checking a
  message MUST NOT take time proportional to the number of whole-word entries.

**Conformance**

- **FR-016**: The port MUST include a corpus runner that reads `conformance/` and runs every case. It
  MUST follow the runner obligations in the corpus format contract:
  - refuse a newer format version;
  - fail on a missing or unreadable corpus, on fewer than 300 cases, on duplicate ids and on pending
    cases;
  - check the kind rules before the results;
  - compare every recorded field exactly (positions are already in code points, so no conversion is
    needed);
  - report each failing case with its id, file, visible input and differing fields, and continue past
    failures.
- **FR-017**: The port MUST pass 100% of corpus cases on every supported CPython version, including
  free-threaded CPython, with no case reported as not applicable.
- **FR-018**: The port's bundled default, full and per-category selections MUST contain the same entries,
  in the same order, as the .NET and JavaScript packages built from the same `wordlists/`.
- **FR-019**: The port's own tests MUST cover the behaviour specific to Python, and MUST run against the
  built package as users install it. That behaviour is:
  - `None` as a missing value;
  - `TypeError` for values that are not strings;
  - the mask and word-list errors, including loading a word-list file from a path and from an open file;
  - immutability after building;
  - concurrent use from many threads;
  - the type information seen by a type checker.

**Release, compatibility and documentation**

- **FR-020**: The package version MUST come from `VERSION`. A release tag MUST publish the PyPI, npm and
  NuGet packages at the same version. It publishes only when the tag matches `VERSION` and every job for
  every port is green; otherwise none of them is published. The PyPI package MUST be published from CI,
  without a stored password or token, and with a record that it was built from this repository.
- **FR-021**: On every pull request, CI MUST run the following for the Python port:
  - the port's tests and the full corpus on every supported CPython version, and on free-threaded
    CPython;
  - a strict type check of the package;
  - a check that the built wheel and source distribution install into a clean environment and pass a
    smoke test;
  - packaging checks.

  Existing job names MUST stay unchanged.
- **FR-022**: The package's public API MUST be recorded and compared in CI with the previous release. The
  first release records the baseline. From then on, a change that would break existing callers fails CI.
- **FR-023**: Every public function, class, method and option MUST have a docstring, in English, that
  explains behaviour and edge cases.
- **FR-024**: The port MUST have its own README, shown on PyPI, containing:
  - installation and a quick start in both Persian (right to left) and English;
  - the name table from FR-012;
  - a performance table measured with pyperf on a named machine;
  - a link to the full project README.

  Every code example in it MUST have a matching test. Because PyPI may not render right-to-left layout,
  the Persian text MUST stay readable even where PyPI shows it left to right.
- **FR-025**: The project README MUST list the Python package next to the .NET and npm packages, in both
  languages. Its Development section MUST show the `python/` directory and the commands that test the
  port, run the corpus and run the benchmarks.
- **FR-026**: The .NET and JavaScript packages, the corpus's existing cases and the shared word lists
  MUST NOT change behaviour. Corpus cases MAY be added for Unicode differences (see Edge Cases), and every
  port MUST pass each added case.
- **FR-027**: The feature MUST end with the first release that includes PyPI, `1.4.0`:
  - after the pull request merges, `VERSION` holds `1.4.0` and tag `v1.4.0` is pushed;
  - CI publishes `persian-text-guard` `1.4.0` to PyPI and npm, and `PersianTextGuard` `1.4.0` to NuGet;
  - all three are installed from their public registries into fresh projects and shown to work.

  A GitHub release for `v1.4.0` MUST have English notes and a Persian summary. They announce the Python
  package and state that .NET and JavaScript behaviour is unchanged.

### Key Entities

- **Filter**: built once from entries and options, and answers every question about a message. It is
  immutable and shareable across threads.
- **Entry**: a word or phrase to look for, with its match mode (whole word or anywhere) and its category:
  uncategorized, profanity, sexual, insult, slur, harassment or mild.
- **Options**: the three evasions a filter reads through, each on by default.
- **Match**: the entry found, the evasions undone, and the region's start and length in code points.
- **Normalization steps**: the individual steps, and the standard and comparison presets.
- **Bundled selection**: all entries, the default selection, or the entries in chosen categories.
- **Package**: the wheel and source distribution on PyPI, their type information and their README.
- **Corpus runner**: the port's reader of `conformance/`, which decides whether the port may be released.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the corpus's cases pass on every supported CPython version and on free-threaded
  CPython, with 0 cases reported as not applicable.
- **SC-002**: The port's bundled selections match the other ports' entry by entry: the same total, the
  same default-selection count, and the same per-category counts and order (0 differences).
- **SC-003**: A developer new to the package can install it and flag a message by following the README
  quick start, in under 5 minutes, with nothing else to install.
- **SC-004**: The package has 0 runtime dependencies, and its installed size is under 1 MB.
- **SC-005**: These are the targets on the machine the README names. They are set for pure Python and are
  confirmed by a measurement during planning:
  - building a filter from the bundled default list takes under 500 ms;
  - checking a short, clean chat message takes under 250 µs on average;
  - checking the 132,000-character corpus message takes under 3 s.
- **SC-006**: 100% of public functions, classes and options have docstrings, and 100% of the port
  README's code examples have a passing test.
- **SC-007**: A strict type check of the package and of the README quick start reports 0 errors, and the
  built wheel and source distribution each install and pass the smoke test in a clean environment (2 of
  2).
- **SC-008**: Two runs from many threads on free-threaded CPython give results identical to a
  single-threaded run: 8 threads, each checking every corpus message, in a CI job.
- **SC-009**: A release tag publishes all three packages at the same version, and either of these
  publishes none of them:
  - a failing Python job;
  - a tag that differs from `VERSION`.

  Both are shown by a real CI dry run on throwaway tags before `v1.4.0` is pushed, and the dry run
  publishes no package.
- **SC-010**: .NET and npm users notice nothing: all their existing tests and corpus runs still pass, and
  both packages still pass their compatibility checks against 1.3.0.
- **SC-011**: At the end of the feature, all three packages at `1.4.0` install from their public
  registries, were published from CI in the same release, and pass a smoke check in a fresh project (3
  of 3).

## Assumptions

- **Port layout**: the port lives in `python/` at the repository root, next to `dotnet/` and `js/`, as
  the constitution names.
- **Supported CPython versions**: on 2026-09-18, the versions in upstream support are 3.10 (end of life
  2026-10-31), 3.11, 3.12, 3.13 and 3.14. The package starts at 3.11, so CI tests 3.11, 3.12, 3.13 and
  3.14. CPython 3.15 is due in October 2026. CI follows the upstream schedule, so 3.15 joins when it is
  released and each version leaves at its end of life. Free-threaded
  CPython is tested on the newest version that offers it.
- **Package name**: `persian-text-guard` is unclaimed on PyPI (checked 2026-09-18). PyPI treats
  `persian_text_guard` as the same name, and `persiantextguard` is also free.
- **PyPI account and trusted publishing**: the maintainer owns or creates the PyPI account. They register
  this repository's CI as a trusted publisher for the new project, which PyPI calls a "pending publisher",
  and create a matching `pypi` environment on GitHub. These are account-settings steps the maintainer
  performs before the first real release; the plan states exactly what they are and in what order.
- **Non-string input**: `None` is the missing message. Any other non-string raises `TypeError`, as in the
  JavaScript port. The plan's Constitution Check records this against Principle II, which permits
  failures only for programmer errors.
- **Loading word-list files**: .NET already reads files (`WordList.Load`), and the JavaScript package
  leaves reading to the caller, because browsers have no file system. So reading a file is a convenience
  each port offers the way its language does, not a matching capability under Principle V; the corpus
  covers the parsing, which every port shares. The plan's Constitution Check records this.
- **Behaviour source**: the corpus is the specification. The .NET package remains the source that the
  corpus fill-in tool records from; the Python port does not add a second fill-in tool.
- **Versioning**: a newly supported language is a MINOR change, so the first release that includes PyPI
  is `1.4.0`. Releases are lockstep, so NuGet and npm also get `1.4.0`, identical to `1.3.0` except for
  the version number. The release notes say so.
- **Release is irreversible**: publishing `1.4.0` cannot be undone on any of the three registries. A
  version can be yanked or deprecated, but never reused. The tag is pushed only after the merged `main`
  is green and the maintainer agrees.
- **Performance**: the numbers are measured on the maintainer's machine, as for the other ports. A pure
  Python port is expected to be several times slower than the .NET and JavaScript ports. SC-005 sets
  targets that suit a chat message on a request path; they are not parity with the other ports.
- **API compatibility checker**: the constitution names no checker for Python. The plan chooses a
  standard tool that compares the public API with the previous release (FR-022).
- **Persian documentation**: the constitution's bilingual rule for package READMEs applies to the new
  PyPI README from the start.
- **Other ports**: Java, Go and Rust are separate features and out of scope.
- **Branch**: `004-python-port` was created from `main` at `fe550a6`. `main` is protected and requires
  the .NET and JavaScript CI checks; the Python checks are added to the required checks after this
  feature merges.
