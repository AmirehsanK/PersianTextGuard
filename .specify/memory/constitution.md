# PersianTextGuard Constitution

## Core Principles

### I. Ordinary Messages Must Pass

A filter that rejects a normal message teaches people the site is broken, and nobody reports
it. False positives are therefore treated as more severe than missed evasions.

- Every change to the matcher or the word lists MUST keep `Ordinary_messages_pass` green, and
  every new way of catching a word MUST add the ordinary text it could plausibly catch to that
  test (for example «هر کس ده تا» alongside word splitting, `455` alongside digit folding).
- Word-list entries MUST be whole-word by default. An `~anywhere` entry is allowed only for a
  stem whose every extension is offensive, and MUST NOT be used where a common word contains
  it (Scunthorpe, shiitake, sniggered).
- Words that are ordinary in some context MUST be categorised `Mild` or left out, with the
  reason recorded in the word-list header. Ethnic and national names MUST NOT be listed.
- A new evasion MUST NOT be adopted if it cannot be implemented without a measurable rise in
  false positives on the ordinary-message suite; it is documented as a limitation instead.

### II. User Input Never Throws

Moderation runs on untrusted input on a request path. A crash there is an outage.

- `ContainsProfanity`, `FindMatch`, every `PersianNormalizer` method, and any future method
  that takes message text MUST return a result for every string, including `null`, empty,
  whitespace-only, invalid UTF-16 (lone surrogates), and very long input.
- Exceptions are permitted only for programmer errors at construction or load time: a `null`
  word collection, a `null` stream, a malformed word-list file.
- Every crash found in the field MUST ship with a regression test that feeds the exact input.

### III. Zero Dependencies, Broad Compatibility

The package is used from .NET Framework enterprise code as well as modern .NET.

- The library MUST have no runtime package dependencies. Build-only analyzers and polyfills
  (for example PolySharp, `PrivateAssets="all"`) are allowed.
- It MUST target `netstandard2.0`, `net8.0` and `net10.0`, and the test suite MUST pass on
  .NET Framework 4.8, .NET 8 and .NET 10.
- It MUST build with `TreatWarningsAsErrors` and remain AOT-compatible on the modern targets.
  APIs unavailable on `netstandard2.0` MUST NOT be used without a polyfill or fallback.

### IV. Build Once, Match Fast, Share Safely

A filter is built once and checks every chat message, so the per-message path is the product.

- `ProfanityFilter` MUST be immutable and thread-safe after construction; it MUST be safe to
  share as a singleton.
- Work that can happen at construction (normalising entries, building lookups) MUST happen
  there, not per message.
- Checking a message MUST NOT scale linearly with the number of whole-word entries; they are
  looked up by token.
- Any change to the per-message path MUST be benchmarked with BenchmarkDotNet. A regression in
  mean time or allocation MUST be justified in the PR, and the README performance table MUST be
  refreshed from the same machine it names.

### V. Behaviour Is Documented and Test-Pinned

Users read the README and the word-list headers; both are part of the contract.

- Every code example in `README.md` MUST have a matching test in `ReadmeExampleTests`.
- Every evasion the filter reads through MUST appear in the README's evasion table, and every
  known gap MUST appear under Limitations.
- Every bug fix MUST ship with a regression test that fails without the fix.
- Every public type and member MUST have XML documentation that explains behaviour and edge
  cases, not only the signature.

### VI. Curated, Categorised, Credited Word Lists

The bundled list is opt-in content that people trust without reading every line.

- Every bundled entry MUST carry a `WordCategory`; `Uncategorized` is reserved for users' own
  lists and MUST NOT appear in the bundled files.
- `WordList.PersianDefault` MUST exclude `Mild`. Changing what the default includes is a
  behaviour change under Public API & Versioning.
- An entry MUST NOT be added when the matcher already catches it (a leet spelling, a held key,
  a Persian suffix); the list carries spellings, not typography.
- Entries drawn from an external source MUST come from a licence compatible with MIT
  distribution (MIT, Apache 2.0, CC0, CC BY) and MUST be credited in `THIRD-PARTY-NOTICES.md`.
  Sources under share-alike or unknown licences MUST NOT be imported.

## Public API & Versioning

- Versions follow Semantic Versioning, and a `vX.Y.Z` tag on `main` publishes to NuGet.
- **MAJOR**: removing or changing the signature of a public member, or changing the meaning of
  an existing option or category.
- **MINOR**: new public members, options, categories or evasions; additions to the bundled
  lists.
- **PATCH**: bug fixes and word-list corrections that do not add public API.
- A change that alters which inputs match (a word moved between categories, an entry changed
  from anywhere to whole-word, a new evasion) MUST be called out in the release notes even
  when it is MINOR or PATCH.
- New public API MUST be additive and source-compatible with existing callers. Package
  validation (`EnablePackageValidation`) MUST stay enabled.

## Development Workflow & Quality Gates

- Work happens on a branch and lands on `main` through a pull request.
- A pull request MUST NOT be merged unless CI is green: build, tests on .NET 8 and .NET 10,
  tests on .NET Framework 4.8, and pack.
- A pull request that changes behaviour MUST update, in the same change, the README, the
  affected word-list headers, and `THIRD-PARTY-NOTICES.md` where a source was used.
- A pull request that changes the per-message path MUST include before-and-after benchmark
  numbers (Principle IV).
- Releases bump `<Version>` in the project file, are tagged `vX.Y.Z` after merge, and get a
  GitHub release with notes that state any change in matching behaviour.

## Governance

- This constitution supersedes other development practices for PersianTextGuard. Where a
  practice conflicts with it, the constitution wins until it is amended.
- Amendments are made by pull request that edits this file, bumps the version below, and
  includes a Sync Impact Report. Versioning of this document: MAJOR for removing or redefining
  a principle, MINOR for adding a principle or materially expanding guidance, PATCH for
  clarifications.
- Every `/speckit-plan` MUST pass its Constitution Check against these principles before
  design work starts, and again after design. Every pull request review MUST verify
  compliance.
- A deviation from a principle MUST be justified in the plan's Complexity Tracking section or
  the PR description, with the simpler alternative that was rejected and why.
- Runtime guidance for users lives in `README.md`; guidance on what the bundled lists contain
  and deliberately exclude lives in the headers of `src/PersianTextGuard/WordLists/*.txt`.

**Version**: 1.0.0 | **Ratified**: 2026-09-16 | **Last Amended**: 2026-09-16
