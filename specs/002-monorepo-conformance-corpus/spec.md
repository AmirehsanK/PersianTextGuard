# Feature Specification: Monorepo with Shared Word Lists and a Conformance Corpus

**Feature Branch**: `002-monorepo-conformance-corpus` (not yet created; see Assumptions)

**Created**: 2026-09-16

**Status**: Draft

**Input**: User description: "Restructure into a monorepo with shared wordlists/ and a language-neutral conformance corpus that the .NET port passes. Rewrite the README in both Persian and English, with every section, table and limitation in both languages and Persian rendering right to left. Add a JavaScript/TypeScript port of PersianTextGuard that passes the full conformance corpus. Then Python, Java, Go and Rust, one spec each."

**Scope of this specification**: the first of those features only — the monorepo restructure, the
shared word lists and the conformance corpus, with the existing .NET package passing it. The bilingual
README and each language port are separate features with their own specifications (see Assumptions).

## Clarifications

### Session 2026-09-16

- Q: When a future port is checked against the corpus, must it reproduce every detail the .NET filter
  reports exactly, including which match `FindMatch` returns first and which disguise kinds it
  reports? → A: Exact everywhere — the yes/no result, the first match, every match (entry, category,
  disguise kinds, position) and the censored output must all equal the corpus.
- Q: After the corpus is first created, how should expected results for new or changed cases be
  filled in: by hand only, or with a tool that asks the .NET filter? → A: Fill in blanks only. A tool
  fills in expectations for cases that have none yet, taken from the .NET filter. It never overwrites
  an existing expectation, and only reports the ones that disagree.
- Q: Once the corpus covers the same behaviour, should the .NET behaviour tests that duplicate it stay,
  or be removed so each behaviour is checked in one place? → A: Keep them for now and remove them
  later. All existing .NET tests stay through this feature; removing the duplicates is a follow-up
  change once the corpus is running in CI.

## User Scenarios & Testing *(mandatory)*

The people affected are:

- the maintainer, who today can only describe the filter's behaviour through .NET code and tests;
- future port authors (JavaScript/TypeScript, Python, Java, Go, Rust), who need a precise, readable
  definition of behaviour to build and check against without reading C#;
- contributors adding word-list entries or test cases;
- existing .NET users of the published package, who must notice nothing.

The project constitution (v2.0.0) already requires a shared, language-neutral definition of behaviour
that every port passes, word lists that live in one place, and one version for every package. None of
that exists yet: behaviour is pinned only by .NET tests, the word lists sit inside the .NET source, and
the version lives in the .NET project file.

### User Story 1 - Behaviour defined once, in a form every language can check (Priority: P1)

A port author opens the conformance corpus and finds, for each test message, everything the filter
must do with it:

- whether it contains profanity;
- the first match;
- every match, with entry, category, disguise and position;
- the censored output.

It also defines normalization and tokenization results, how word-list files are read, which entries
each bundled category selection contains, and how the filter's options change results. The existing
.NET package passes every case. From then on, "behaves like PersianTextGuard" has a precise,
checkable meaning that does not depend on reading C#.

**Why this priority**: The constitution forbids releasing any port that does not pass the corpus, so
no port can start without it. It also turns today's behaviour into a record that survives refactoring:
the 169 hand-written messages in the .NET list tests, plus the censoring, matching, normalization and
robustness cases, become data anyone can read.

**Independent Test**: Run the corpus against the .NET package. It passes every case. Then check that
every message used by the existing .NET behaviour tests appears in the corpus with its expectations.

**Acceptance Scenarios**:

1. **Given** the corpus and the .NET package, **When** the corpus is run against the package,
   **Then** every case passes on every .NET target the package supports.
2. **Given** a message in the corpus containing an emoji or another character outside the Basic
   Multilingual Plane, **When** its match positions are checked, **Then** the positions recorded in
   the corpus (counted in Unicode code points) agree with the positions the .NET package reports
   (counted in UTF-16 code units) once converted.
3. **Given** a deliberately wrong expectation added to one case, **When** the corpus is run, **Then**
   that case fails, and the report names the case and shows the expected and actual results.
4. **Given** an ordinary message the filter must let through, **When** a reviewer reads its case,
   **Then** it is marked as an ordinary message, so a later change that starts flagging it is visibly
   a false positive, not merely a changed expectation.
5. **Given** a case whose message contains invisible characters (zero-width non-joiner, zero-width
   space, soft hyphen, direction marks), **When** a reviewer reads it, **Then** each invisible
   character can be seen and identified in the case, not just inferred from a failing run.

---

### User Story 2 - One copy of the word lists, shared by every package (Priority: P2)

A contributor who adds a banned word edits one word-list file in one shared location. Every package
built from the repository — today the .NET package, later every port — picks up the change when it is
next built. No package carries its own editable copy, so the lists cannot drift between languages.

**Why this priority**: Every port needs the same entries in the same order: the order breaks ties
between overlapping matches, so it changes results. It comes second because the corpus (Story 1) is
what proves the entries behave the same everywhere.

**Independent Test**: Build the .NET package from the restructured repository and compare its
bundled entries — text, match mode, category and order, for every category selection — with the
entries of version 1.2.0. They are identical. Then add a test entry to a shared list file, rebuild,
and confirm the package picks it up.

**Acceptance Scenarios**:

1. **Given** the restructured repository, **When** the .NET package is built, **Then** its bundled
   entries, in order and with their categories, are identical to those of the published 1.2.0
   package.
2. **Given** a contributor adds an entry to a shared word-list file, **When** the .NET package is
   rebuilt, **Then** the package contains the new entry without any other file being edited.
3. **Given** the repository, **When** it is searched for word-list files, **Then** exactly one copy
   of each exists, in the shared location.
4. **Given** a change to a shared word list that alters which messages match, **When** the corpus is
   run, **Then** the affected cases fail until their expectations are updated in the same change
   (constitution, Principle V).

---

### User Story 3 - A monorepo that can hold every port, with nothing changing for .NET users (Priority: P3)

The repository is organised the way the constitution describes. Shared data sits at the top level,
the .NET port sits in its own directory with its source, tests and benchmarks, and one version value
at the root is read by the .NET build. An existing .NET user who upgrades to the next release sees no
change: same package name, same API, same behaviour, same documentation. A contributor's existing
commands still have documented equivalents, and the history of every moved file is still there.

**Why this priority**: It unblocks the port features, each of which needs its own top-level
directory, and the lockstep version rule, which needs one version source. It delivers nothing a user
can see, which is why it comes after the stories that define behaviour.

**Independent Test**:

- Build, test, benchmark and pack the .NET port from its new location. All 1,029 existing tests
  pass on all three .NET targets.
- The API compatibility check against 1.2.0 passes.
- The package's version comes from the root version value.
- The history of a moved source file is intact.

**Acceptance Scenarios**:

1. **Given** the restructured repository, **When** CI runs, **Then** it builds, tests (all three
   .NET targets), runs the corpus, checks API compatibility against 1.2.0 and packs the .NET port,
   and all of it succeeds.
2. **Given** the root version value is changed, **When** the .NET package is packed, **Then** the
   package carries that version, with no version edited anywhere else.
3. **Given** a moved file, **When** a contributor looks at its history, **Then** its commits from
   before the move are still shown.
4. **Given** the release process (a version tag on `main`), **When** it runs after the restructure,
   **Then** it publishes the .NET package exactly as before. No other package is published, because
   no other port exists yet.
5. **Given** the published package page and the package's own README, **When** a user reads them,
   **Then** nothing they rely on has changed, and links to files in the repository still work.

---

### Edge Cases

- **Positions of characters outside the Basic Multilingual Plane**: an emoji is one code point but two
  UTF-16 code units. Corpus positions are code points; the .NET runner converts, and a case with an
  emoji before a match proves the conversion (Story 1, scenario 2).
- **Invalid text**: a lone surrogate cannot be written literally in every data format or represented
  in every language. The corpus must still describe these inputs so each port can build the invalid
  text its language allows, and a language that cannot represent a given invalid input records that
  case as not applicable to it rather than silently skipping it.
- **The missing value** (`null` in .NET): the corpus has cases for it, which ports interpret as their
  language's missing value.
- **Very long messages** (tested today at 132,000 characters): writing them literally would make the
  corpus unreadable, so a case can describe a message built by repeating shorter text.
- **Ties decided by word-list order**: overlapping matches of equal length are settled by entry order,
  so reordering a word list can change expected results. The corpus catches that (Story 2, scenario 4).
- **Cases that depend on filter configuration**: options switched off, custom word lists and category
  selections. Each case states the configuration it runs under; the default is the bundled default
  list with all options on.
- **Expectations that disagree with .NET when the corpus is first built**: the corpus is recorded from
  version 1.2.0's actual behaviour, so there are none. A disagreement found later is a bug, fixed
  under the constitution's rules: the corpus changes first, in the same pull request as the code.
- **Duplicate messages**: the same message used by several existing tests appears once per distinct
  configuration, not once per test.
- **Running the fill-in tool when the .NET package disagrees with an existing case**: the tool reports
  the disagreement and leaves the case as it is. A bug in .NET can therefore never quietly become the
  expected behaviour for every port.
- **Word-list headers and notices**: the explanatory headers in the list files move with the files,
  and every reference to the lists' old location is updated — in the README, `THIRD-PARTY-NOTICES.md`,
  the constitution and the package's embedded documentation.
- **Existing specifications and Spec Kit files** keep working after the move. Specification 001's
  paths are historical records and are not rewritten.

## Requirements *(mandatory)*

### Functional Requirements

#### Conformance corpus

- **FR-001**: The repository MUST contain a conformance corpus: a language-neutral set of test cases,
  in a widely supported data format that every supported language can read in its tests, describing the
  filter's behaviour. *(Amended during planning: the original wording required each language's standard
  library, which no suitable format satisfies — Java's and Rust's standard libraries read no structured
  format. The corpus is only read by tests and tooling, where the constitution allows test-only
  dependencies. See research R1.)*
- **FR-002**: For each message case, the corpus MUST record:
  - the configuration it runs under (word-list selection, custom entries if any, options);
  - whether the message contains profanity;
  - the first match's entry and region;
  - every match's entry text, match mode, category, disguise kinds and region;
  - the censored output with the default mask, and with any custom mask the case names.

  Every recorded field is an exact expectation. A port passes a case only if every field equals the
  corpus, including which match is first and the full set of disguise kinds. No field is checked
  loosely or as "at least", so the same message gets the same answer in every language.
- **FR-003**: The corpus MUST also cover:
  - normalization, with each normalization step and preset;
  - tokenization;
  - reading word-list text (sections, markers, comments, blank lines, and invalid categories that must
    be rejected);
  - bundled category selection, including the entries each selection contains;
  - rejected mask characters.
- **FR-004**: Positions in the corpus MUST be recorded in Unicode code points. Each port's runner MUST
  convert them to the unit its language reports (constitution, Principle V).
- **FR-005**: Every distinct message used by the existing .NET behaviour tests MUST be present in the
  corpus with its expectations. That covers the list tests, find-all-matches tests, censoring tests,
  normalizer tests, filter tests and README example tests, plus the robustness inputs of the consistency
  tests.
- **FR-006**: Each message case MUST be marked with its kind: an ordinary message that must pass, a
  message that must match, or a robustness input. A reviewer MUST be able to see false-positive
  protection at a glance.
- **FR-007**: Invisible and direction-control characters in any case MUST be written in a visible,
  unambiguous form that a reviewer can read.
- **FR-008**: The corpus MUST be able to describe inputs that cannot be written literally: invalid text
  such as lone surrogates, the missing value, and very long messages built by repetition. A case that a
  language cannot represent MUST be marked not applicable for that language, never silently skipped.
- **FR-009**: The initial expectations MUST be recorded from the actual behaviour of version 1.2.0, and
  the corpus MUST contain a note saying so, so later changes can be traced back to the release they
  diverged from.
- **FR-010**: A maintainer MUST be able to add or change a case by editing corpus data alone, without
  writing code.
- **FR-010a**: A fill-in tool MUST fill in the expected results of cases that have none yet, taken
  from what the .NET package does.
  - It MUST NOT overwrite an existing expectation, so a change in behaviour always shows up as a failing
    case that someone fixes on purpose.
  - It MUST report every existing expectation that disagrees with the .NET package, without changing
    it.
  - Filled-in expectations are committed and reviewed in the pull request like hand-written ones.

#### .NET corpus runner

- **FR-011**: The .NET port MUST run the full corpus as part of its test suite, on every .NET target it
  supports.
- **FR-012**: A failing case MUST be reported with its identifier, the input, and the expected and
  actual results. The run MUST continue past the first failure, so one run reports every failing case.
- **FR-013**: The runner MUST fail if the corpus cannot be found or read, or contains no cases, so a
  broken path can never pass as an empty success.

#### Shared word lists

- **FR-014**: The bundled word lists MUST live once, in a shared top-level location, with their headers.
- **FR-015**: The .NET package MUST embed the shared word lists when it is built. No editable copy of
  any list may exist inside the .NET port.
- **FR-016**: The .NET package built after the restructure MUST bundle exactly the entries of 1.2.0 —
  the same text, match mode, category and order, for every category selection.

#### Monorepo layout and version

- **FR-017**: The repository MUST follow the constitution's layout: shared word lists and the corpus at
  the top level, and the .NET port's source, tests and benchmarks inside its own top-level directory.
- **FR-018**: One version value at the repository root MUST be the only place the version is set, and
  the .NET package MUST take its version from it.
- **FR-019**: Moves MUST preserve each file's history, so a contributor can follow a moved file's
  commits from before the move.
- **FR-020**: Every reference to a moved path MUST be updated in the same change. That covers CI, the
  solution file, the README and its links, `THIRD-PARTY-NOTICES.md`, the constitution, the package
  metadata, and the Spec Kit configuration that names source paths. Specification 001 is exempt as a
  historical record.
- **FR-021**: The README MUST document how to build, test, benchmark and pack the .NET port from its new
  location, and how to run the corpus against it.

#### No change for .NET users

- **FR-022**: The .NET package's name, public API, target frameworks, bundled data, behaviour and
  packaged README content MUST be unchanged. The API compatibility check MUST pass against 1.2.0.
- **FR-023**: All 1,029 tests that pass on 1.2.0 MUST still pass, on all three .NET targets.
- **FR-024**: CI MUST gate merges on:
  - the .NET build;
  - the .NET tests, on all three targets;
  - the corpus run;
  - the API compatibility check;
  - packing.

  The tag-triggered release MUST keep publishing the .NET package.

### Key Entities

- **Conformance corpus**: the whole set of cases; the specification of behaviour every port must pass. Carries a note of the release its initial expectations were recorded from.
- **Case**: one checkable behaviour, with:
  - a stable identifier;
  - a kind: ordinary, must-match, robustness, normalization, tokenization, word-list reading, category selection or mask validation;
  - an input, and the configuration it runs under;
  - its expected results;
  - an optional note explaining why the case exists (for example "`کسی` also means someone").
- **Configuration**: which bundled lists or custom entries the filter is built from, and which evasion options are on. Cases share a small set of named configurations rather than repeating them.
- **Expected match**: entry text, match mode, category, disguise kinds, and region (start and length in code points).
- **Shared word list**: one categorised list file in the shared location — Persian, Finglish or English — with its explanatory header.
- **Version source**: the single root value every package takes its version from.
- **Port**: one language's implementation in its own top-level directory. After this feature, only the .NET port exists.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of corpus cases pass against the .NET package on each of its three targets.
- **SC-002**: 100% of the distinct messages in the existing .NET behaviour tests appear in the corpus.
  The corpus holds at least 300 cases, with at least one case for every capability named in FR-002 and
  FR-003.
- **SC-003**: The .NET package built after the restructure bundles the same entries, in the same order,
  as 1.2.0 (0 differences). It passes the API compatibility check against 1.2.0, and all 1,029 existing
  tests pass.
- **SC-004**: The repository contains exactly one copy of each word-list file and exactly one place
  where the version is set.
- **SC-005**: A maintainer can add a new case — an ordinary message that must pass — by editing corpus
  data alone, and have CI check it, in under 5 minutes, without writing code. With the fill-in tool,
  adding a must-match case, including exact positions and disguise kinds, also takes under 5 minutes.
- **SC-006**: 100% of failing cases are reported with identifier, input, expected and actual results, and
  a single run reports all failing cases.
- **SC-007**: Every file moved by the restructure (100%) still shows its history from before the move.
- **SC-008**: Running the corpus against the .NET package takes under 30 seconds on a developer machine.
- **SC-009**: For .NET users, the first release after the restructure differs from 1.2.0 only in version
  number: the same API, bundled entries and behaviour, and corpus results identical to 1.2.0's.

## Assumptions

- **Scope split**: the user's request covers seven features. This specification covers only the
  restructure, shared word lists and conformance corpus. Each remaining feature needs its own
  `/speckit-specify` run:
  - the bilingual README;
  - the JavaScript/TypeScript port;
  - the Python port;
  - the Java port;
  - the Go port;
  - the Rust port.
  One feature per specification is what Spec Kit supports, and each port is sized as its own plan,
  task list and release.
- **Order**: the ports depend on this feature (corpus, shared lists, layout). The bilingual README does
  not, and can be specified and delivered before or alongside it. Until that README feature lands, the
  README changes this feature makes (FR-020, FR-021) are in English. That is allowed by the constitution's
  adoption clause, because the bilingual requirement binds only once the bilingual README exists.
- **Existing .NET tests stay through this feature** (clarified 2026-09-16): all 1,029 tests remain,
  because they are the proof that the restructure changed nothing (SC-003). Removing the tests that
  duplicate the corpus is out of scope here. It is a follow-up change, made once the corpus is running
  in CI, that keeps only the .NET-specific tests: source maps, thread safety, README examples and the
  corpus runner.
- **Where expectations come from**: the initial corpus is recorded from what version 1.2.0 actually does.
  It is correct by definition for this feature, and future behaviour changes follow the constitution's
  rule that the corpus changes first.
- **Directory names** follow the constitution: `wordlists/`, `conformance/` and `dotnet/`.
- **Release**: this feature does not require a release. Whenever the next release happens, SC-009 applies.
  Under Public API & Versioning a restructure with no user-visible change is at most a PATCH.
- **Spec 001**: its documents keep their original paths as a historical record and are not rewritten.
- **Branch**: no git branch was created, because the project has no Spec Kit git hook. Under the
  constitution's workflow, implementation must happen on a branch and land through a pull request, so a
  `002-monorepo-conformance-corpus` branch should be created before `/speckit-implement`.
