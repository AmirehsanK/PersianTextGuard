# PersianTextGuard Constitution

## Core Principles

### I. Ordinary Messages Must Pass

A filter that rejects a normal message teaches people the site is broken, and nobody reports
it. False positives are therefore treated as more severe than missed evasions, in every language.

- Every change to matching or to the word lists MUST keep every ordinary-message case in the
  conformance corpus (Principle V) passing in every port, and every new way of catching a word
  MUST add to the corpus the ordinary text it could plausibly catch (for example «هر کس ده تا»
  alongside word splitting, `455` alongside digit folding).
- Word-list entries MUST be whole-word by default. An `~anywhere` entry is allowed only for a
  stem whose every extension is offensive, and MUST NOT be used where a common word contains
  it (Scunthorpe, shiitake, sniggered).
- Words that are ordinary in some context MUST be categorised `Mild` or left out, with the
  reason recorded in the word-list header. Ethnic and national names MUST NOT be listed.
- A new evasion MUST NOT be adopted if it cannot be implemented without a measurable rise in
  false positives on the corpus's ordinary messages; it is documented as a limitation instead.

### II. User Input Never Throws

Moderation runs on untrusted input on a request path. A crash there is an outage, whatever the
language.

- Every function in every port that takes message text — checking, finding matches, censoring,
  normalizing, tokenizing — MUST return a result for any text the language can represent. That
  includes the language's missing value (`null`, `None`, `nil`, `undefined`), empty and
  whitespace-only text, invalid encodings (lone surrogates in .NET, JavaScript, Java and Python;
  invalid UTF-8 in Go strings and byte slices), and very long input.
- "Throw" includes every failure mode a caller has to guard against: exceptions, Go panics,
  Rust panics, and Python exceptions.
- Failures are permitted only for programmer errors at construction or load time: a missing
  word collection, an unreadable or malformed word-list file, an invalid mask character. Each
  port reports them the way its language does (exceptions, `error` values, `Result`).
- Every crash found in the field MUST ship with a conformance-corpus case that feeds the exact
  input, so every port is checked against it.

### III. Native and Dependency-Free in Every Language

People adopt a moderation library when it installs cleanly and adds nothing to their supply chain.

- Each supported language MUST have a native implementation in that language. Native binaries,
  FFI bindings and WebAssembly MUST NOT be used to provide the matcher.
- A port MUST have no runtime dependencies beyond its language's standard library, except those on
  this allowlist, each of which fills a gap in that standard library:
  - Go: `golang.org/x/text` (NFKC normalization; maintained by the Go project).
  - Rust: `unicode-normalization` (NFKC normalization; used by the Rust compiler itself).
  Adding to the allowlist requires amending this constitution. Build-, test- and benchmark-only
  dependencies are allowed in every port.
- Supported ports and their minimum targets:
  - **.NET**: `netstandard2.0`, `net8.0` and `net10.0`; tests pass on .NET Framework 4.8, .NET 8
    and .NET 10; builds with `TreatWarningsAsErrors`; AOT-compatible on the modern targets.
  - **JavaScript and TypeScript**: one package authored in TypeScript, shipped as ES modules and
    CommonJS with bundled type declarations; tested on every Node.js release in active or
    maintenance LTS; uses no Node-only APIs in the matcher, so it also runs in browsers.
  - **Python**: pure Python, with type hints; tested on every CPython version in upstream support.
  - **Java**: Java 11 and later; tested on every Java LTS release from 11 to the newest.
  - **Go**: tested on the two most recent Go releases, matching Go's own support policy.
  - **Rust**: stable Rust, with a minimum supported Rust version declared in `Cargo.toml` and tested
    in CI.
- A port MUST NOT use language or runtime features unavailable at its declared minimum target.

### IV. Build Once, Match Fast, Share Safely

A filter is built once and checks every chat message, so the per-message path is the product.

- A filter MUST be immutable after construction and safe to share: across threads in .NET, Java
  and Python (including free-threaded CPython), across goroutines in Go, as `Send + Sync` in Rust,
  and across async callers in JavaScript.
- Work that can happen at construction (normalising entries, building lookups) MUST happen there,
  not per message.
- Checking a message MUST NOT scale linearly with the number of whole-word entries; they are looked
  up by token.
- Any change to a port's per-message path MUST be benchmarked with that ecosystem's standard tool,
  named in the port's README: BenchmarkDotNet for .NET, JMH for Java, `testing.B` for Go, Criterion
  for Rust, pyperf for Python, tinybench for JavaScript. A regression in mean time or allocation MUST
  be justified in the PR, and the port's performance table MUST be refreshed from the machine it
  names.

### V. One Behaviour, Verified in Every Language

A user switching languages, or running two services in different languages, must get the same
answer for the same message.

- The **conformance corpus** is the specification of matching behaviour. It is a language-neutral
  data set in `conformance/` that records, for each message:
  - whether it contains profanity,
  - every match: its entry, category, evasion kinds and position,
  - the censored output.
- Every port MUST pass every corpus case. A port that fails any case MUST NOT be released.
- A change to matching behaviour MUST change the corpus first and every port in the same pull
  request. No port may match differently from the corpus, and there is no reference implementation
  whose behaviour overrides it.
- Positions MUST be exposed in each language's native string index unit: UTF-16 code units in .NET,
  JavaScript and Java; code points in Python; bytes in Go and Rust. The corpus records positions in
  Unicode code points, and each port's corpus runner converts them before comparing.
- Every port MUST offer the same capabilities: building a filter from word lists and options,
  checking a message, finding the first match, finding every match, censoring with a chosen mask,
  normalizing and tokenizing Persian text, and selecting bundled lists by category. Names follow each
  language's conventions (`FindMatches` in C#, `findMatches` in TypeScript and Java, `find_matches`
  in Python and Rust, `FindMatches` in Go). A capability MUST NOT be released in one port before it
  is available in all of them.

### VI. Documented in Persian and English, Pinned by Tests

Most people using this project read Persian first. Documentation that is only in English leaves
them guessing about the details that matter most: which words are caught, which are left out, and
why.

- `README.md` MUST document the project completely in both Persian and English: every section,
  table and limitation exists in both. Persian sections MUST render right to left on GitHub. Code
  examples are shared between the two languages, with comments in both.
- A pull request that changes the documentation in one language MUST make the same change in the
  other in the same pull request. Neither language may fall behind.
- Each package's own README (NuGet, npm, PyPI, Maven Central, pkg.go.dev, crates.io) MUST include
  installation and a quick start in both Persian and English, and link to the full README.
- GitHub release notes MUST include a Persian summary alongside the English notes.
- Every code example in the README, in any language, MUST have a matching test in that language's
  port.
- Every evasion the filter reads through MUST appear in the README's evasion table, and every known
  gap under Limitations, in both languages.
- Every bug fix MUST ship with a regression test that fails without the fix: a corpus case when the
  bug is in matching behaviour, a port test when it is specific to one port.
- Every public type and function in every port MUST have API documentation in that ecosystem's
  format — XML docs, TSDoc, docstrings, Javadoc, Go doc comments, rustdoc — that explains behaviour
  and edge cases, not only the signature. API documentation is written in English.

### VII. Curated, Categorised, Credited Word Lists

The bundled list is opt-in content that people trust without reading every line.

- The bundled word lists live once, in `wordlists/`, and every port embeds them at build time.
  Hand-edited copies inside a port MUST NOT exist.
- Every bundled entry MUST carry a category; `Uncategorized` is reserved for users' own lists and
  MUST NOT appear in the bundled files.
- Every port's default bundled list MUST exclude `Mild`. Changing what the default includes is a
  behaviour change under Public API & Versioning.
- An entry MUST NOT be added when the matcher already catches it (a leet spelling, a held key, a
  Persian suffix); the list carries spellings, not typography.
- Entries drawn from an external source MUST come from a licence compatible with MIT distribution
  (MIT, Apache 2.0, CC0, CC BY) and MUST be credited in `THIRD-PARTY-NOTICES.md`. Sources under
  share-alike or unknown licences MUST NOT be imported.

## Public API & Versioning

- Every package shares one version (**lockstep**), read by every port's build from a single version
  source at the repository root. A `vX.Y.Z` tag on `main` releases every package at that version:
  - NuGet
  - npm
  - PyPI
  - Maven Central
  - the Go module, whose monorepo tag is created with its directory prefix
  - crates.io
- A release MUST NOT publish some packages without the others. If any port fails CI or the corpus,
  the release is blocked. A new port joins at the first version in which it passes the whole corpus
  and offers every capability (Principle V).
- Versions follow Semantic Versioning, judged across all ports together:
  - **MAJOR**: removing or changing the signature of a public member in any port, or changing the
    meaning of an existing option or category. In Go this also means a new `/vN` module path.
  - **MINOR**: new public capabilities, options, categories or evasions; additions to the bundled
    lists; a newly supported language.
  - **PATCH**: bug fixes and word-list corrections that do not add public API.
- A change that alters which inputs match (a word moved between categories, an entry changed from
  anywhere to whole-word, a new evasion) MUST be called out in the release notes, in English and
  Persian, even when it is MINOR or PATCH.
- New public API MUST be additive and source-compatible with existing callers in every port.
  Wherever an ecosystem has an API compatibility checker, it MUST run in CI against the previous
  release: .NET package validation, `japicmp` for Java, `cargo-semver-checks` for Rust, `gorelease`
  for Go, and API Extractor for TypeScript.

## Development Workflow & Quality Gates

- The repository is one monorepo:
  - `wordlists/` and `conformance/` hold the shared data;
  - each port lives in its own top-level directory: `dotnet/`, `js/`, `python/`, `java/`, `go/`,
    `rust/`;
  - each port has its own tests, benchmarks and README.
- Work happens on a branch and lands on `main` through a pull request.
- A pull request MUST NOT be merged unless CI is green for every port: build, the port's tests on
  every target Principle III names, the full conformance corpus, API compatibility checks, and
  packaging.
- A pull request that changes behaviour MUST update, in the same change:
  - the conformance corpus
  - every port
  - the README in both languages
  - the affected word-list headers
  - `THIRD-PARTY-NOTICES.md`, where a source was used
- A pull request that changes a port's per-message path MUST include before-and-after benchmark
  numbers for that port (Principle IV).
- Releases update the single version source, are tagged `vX.Y.Z` after merge, publish every package
  from CI, and get a GitHub release with English notes and a Persian summary, both stating any
  change in matching behaviour.

## Governance

- This constitution supersedes other development practices for PersianTextGuard in every language.
  Where a practice conflicts with it, the constitution wins until it is amended.
- Amendments are made by pull request that edits this file, bumps the version below, and includes a
  Sync Impact Report. Versioning of this document: MAJOR for removing or redefining a principle,
  MINOR for adding a principle or materially expanding guidance, PATCH for clarifications.
- **Adoption.** Some requirements introduced in 2.0.0 describe a repository that does not exist yet:
  the monorepo layout, `wordlists/`, the conformance corpus, the ports other than .NET, the Persian
  README and multi-registry CI. Each such requirement binds a pull request from the moment the
  pull request that first satisfies it is merged. Until then, pull requests MUST NOT move the
  project further away from it.
- Every `/speckit-plan` MUST pass its Constitution Check against these principles before design work
  starts, and again after design. Every pull request review MUST verify compliance.
- A deviation from a principle MUST be justified in the plan's Complexity Tracking section or the PR
  description, with the simpler alternative that was rejected and why.
- Guidance for users lives in `README.md`, in Persian and English. Guidance on what the bundled lists
  contain and deliberately exclude lives in the headers of the files in `wordlists/`.

**Version**: 2.0.0 | **Ratified**: 2026-09-16 | **Last Amended**: 2026-09-16
