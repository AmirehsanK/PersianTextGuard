---

description: "Task list for the Rust port published to crates.io (release 1.5.0)"
---

# Tasks: Rust Port Published to crates.io

**Input**: Design documents from `/specs/005-rust-port/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/public-api.md](contracts/public-api.md), [contracts/package-and-release.md](contracts/package-and-release.md), [contracts/corpus-runner-amendment.md](contracts/corpus-runner-amendment.md), [quickstart.md](quickstart.md)

**Tests**: Included. The spec requires them:
- a corpus runner (FR-016, FR-017);
- Rust-specific tests, including byte versions, threads and a no-panic property test (FR-019, SC-007, SC-008);
- a doc test for every README and rustdoc example (FR-024, SC-006).

**Organization**: Phases follow the spec's user stories.
- **Foundational**: the corpus runner amendment (research R4), the generated Unicode tables (R1), and the
  layers every story shares: types, errors, Unicode, the UTF-16 view, the normalizer, fold helpers,
  source maps and word lists.
- **US1**: the filter, the byte versions and the crate users get.
- **US2**: the corpus runner, identical answers, threads and no panics.
- **US3**: CI and publishing.
- **US4**: documentation, benchmarks and API compatibility.
- **Release**: runs last, because it is irreversible, and starts with a real CI run and dry run.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4)
- Paths are relative to the repository root, `D:\Git\PersianTextGuard`. Cargo commands run in `rust/`.
- **Environment notes** (from 004 and research R15):
  - Rust is installed with the GNU host (`x86_64-pc-windows-gnu`): toolchains `stable` and `1.85`. Put
    `C:\Users\amire\.cargo\bin` on `PATH` in each shell (`export PATH="/c/Users/amire/.cargo/bin:$PATH"`).
  - uv needs `UV_CACHE_DIR='C:\Users\amire\AppData\Local\Temp\claude\uvcache'` on this machine.
  - The tools that write files decode a backslash-u followed by four hex digits into the literal character.
    In Rust source write `\u{...}` (braces are safe), `char::from_u32(...)`, or named constants; in
    generated text build backslashes with `char::from(92)` or `chr(92)`. After writing, grep for stray
    non-ASCII or NUL bytes.
  - Never use `git reset --hard` to undo a scratch commit: use `git reset HEAD~1` and check out the files.

## Porting conventions (apply to every task that ports a JavaScript file)

- **Source.** Port from `js/src/*.ts`, the faithful port of .NET that passes the corpus (004 R7). When the
  TypeScript and the C# (`dotnet/src/PersianTextGuard/*.cs`) read differently, the C# wins. Keep the
  algorithm, control flow, constants, tables and comments, so files can be reviewed side by side. Do not
  "improve" behaviour: the corpus is the specification. `python/src/persian_text_guard/*.py` is a second,
  already-proven port of the same code on a UTF-16 view, useful for the view-specific parts.
- **Units.** Inside the crate, text is the UTF-16 view from `utf16.rs` (research R2): a `&[u16]` or
  `Vec<u16>`. `text[i]` is unit *i*, `text.len()` the unit count, exactly `charCodeAt` in TypeScript.
  Readings, tokens and keys are `Vec<u16>` / `Box<[u16]>`; `HashMap` keys are `Box<[u16]>` looked up by
  `&[u16]`. The view never leaves the crate: public functions take `&str` or `&[u8]` and return byte
  positions and UTF-8 strings.
- **Primitives.** Every Unicode-dependent operation goes through `unicode.rs`: never `char::is_alphabetic`,
  `is_alphanumeric`, `is_numeric`, `is_whitespace`, `is_lowercase`, `is_uppercase`, `to_lowercase`,
  `to_uppercase`, `str::to_lowercase`, `to_uppercase`, `trim` or `split_whitespace` elsewhere, and
  `unicode_normalization` only inside `unicode.rs`. `rust/clippy.toml` bans them with
  `disallowed-methods` (T003), and `unicode.rs` allows them locally with `#[allow(clippy::disallowed_methods)]`.

  | TypeScript (`unicode.ts`) | Rust (`unicode.rs`) |
  | --- | --- |
  | `isWhiteSpace(unit)` | `is_white_space(u: u16) -> bool` |
  | `isLetter` / `isLetterOrDigit` / `isControl` | `is_letter` / `is_letter_or_digit` / `is_control` |
  | `isSurrogate` / `isHighSurrogate` / `isLowSurrogate` | `is_surrogate` / `is_high_surrogate` / `is_low_surrogate` |
  | `categoryOfUnit(unit)` / `categoryAt(text, i)` | `category_of_unit(u) -> Category` / `category_at(text: &[u16], i) -> Category` |
  | `toLowerInvariant(unit)` | `to_lower_invariant(u) -> u16` |
  | `nfkc` / `isNfkc` / `nfd` | `nfkc(&[u16]) -> Vec<u16>` / `is_nfkc` / `nfd` |
  | `isNoncharacter(cp)` / `noncharacterLengthAt(text, i)` | `is_noncharacter(cp: u32)` / `noncharacter_length_at(text, i) -> usize` |

  `Category` is a private `#[repr(u8)] enum` of the 30 two-letter categories (`Lu`, `Ll`, …, `Cn`).
- **No panics.** No `unwrap`/`expect` on data derived from input; indexing only inside bounds checked the
  way the TypeScript checks them; `#![forbid(unsafe_code)]`; `clippy::unwrap_used` and
  `clippy::expect_used` warned in `src/` (allowed in tests).
- **Style.** `rustfmt` defaults with `max_width = 110` (`rust/rustfmt.toml`); every public item documented
  (`#![warn(missing_docs)]`, denied in CI); private items documented where the TypeScript comments them.

---

## Phase 1: Setup

**Purpose**: Scaffold `rust/` with pinned tools, and record the starting point.

- [X] T001 Confirm the branch is `005-rust-port` and `git status --short` shows nothing except the untracked
  `graphify-out/`, which must never be committed. Record the baseline in a new
  `specs/005-rust-port/verification.md` under "Baseline":
  - the commit hash;
  - `dotnet test dotnet/tests/PersianTextGuard.Tests` (expect 1,029 on each of `net8.0`, `net10.0` and `net48`);
  - `dotnet test dotnet/tests/PersianTextGuard.Conformance` (expect 532 × 3);
  - in `js/`: `npm ci`, then `npm run test:all` (expect 675);
  - in `python/`: `uv sync --locked --group package`, then `uv run pytest -q` (expect 727);
  - `rustc +stable --version`, `rustc +1.85 --version`, `cargo --version`, `rustup show active-toolchain`.
- [X] T002 Append the Rust build outputs to the root `.gitignore`: `rust/target/`, `rust/bench/target/`,
  `rust/consumer/target/`, `specs/005-rust-port/tools/rust-dump/target/`, and the packaging copies
  `rust/wordlists/`, `rust/LICENSE`, `rust/THIRD-PARTY-NOTICES.md`.
- [X] T003 Create `rust/Cargo.toml`, `rust/rustfmt.toml` and `rust/clippy.toml`:
  - **`[package]`**: `name = "persian-text-guard"`, `version = "1.4.0"` (the current `VERSION`; T046 moves it),
    `edition = "2024"`, `rust-version = "1.85"`, `license = "MIT"`, `authors = ["Amirehsan Kohannasab"]`,
    `description` (the npm description adapted for Rust, under 300 characters), `repository =
    "https://github.com/AmirehsanK/PersianTextGuard"`, `homepage =
    "https://github.com/AmirehsanK/PersianTextGuard/tree/main/rust#readme"`, `documentation =
    "https://docs.rs/persian-text-guard"`, `readme = "README.md"`, `keywords = ["persian", "farsi",
    "profanity", "moderation", "normalization"]`, `categories = ["text-processing"]`, `include` exactly
    the files of contracts/package-and-release.md → "Package contents" (sources, `build.rs`,
    `README.md`, `LICENSE`, `THIRD-PARTY-NOTICES.md`, `wordlists/*.txt`, `Cargo.toml`, `Cargo.lock`).
  - **`[dependencies]`**: `unicode-normalization = "0.1.25"`. **`[dev-dependencies]`**: `serde_json = "1"`,
    `libtest-mimic = "0.8"`, `proptest = "1.11"`.
  - **`[[test]]`**: `name = "corpus"`, `harness = false`.
  - **`[lints.rust]`**: `unsafe_code = "forbid"`, `missing_docs = "warn"`. **`[lints.clippy]`**:
    `unwrap_used = "warn"`, `expect_used = "warn"`.
  - **`[package.metadata.docs.rs]`**: `all-features = true`.
  - `rustfmt.toml`: `max_width = 110`. `clippy.toml`: `disallowed-methods` listing the `char` and `str`
    methods of the porting conventions, each with a reason naming `unicode.rs` and research R1.
- [X] T004 Create `rust/build.rs` (research R5), with a comment stating why it exists:
  - locate the word lists: `../wordlists/` if `../VERSION` and `../wordlists/persian.txt` exist, else
    `wordlists/` (the packaged copy); panic with a clear message naming both paths if neither exists;
  - when `../VERSION` exists, read and trim it, and panic ("VERSION says X but Cargo.toml says Y; run
    scripts/set-version.sh") if it differs from `CARGO_PKG_VERSION`;
  - copy `persian.txt`, `finglish.txt` and `english.txt`, in that order (the .NET embedding order), into
    `OUT_DIR` with `\r\n` normalized to `\n`, and write `OUT_DIR/wordlists.rs` defining
    `pub(crate) const BUNDLED_WORD_LISTS: [&str; 3]` with `include_str!` of the three copies;
  - emit `cargo:rerun-if-changed` for `build.rs`, `../VERSION` and each list file used.
- [X] T005 [P] Create the skeleton `rust/src/lib.rs`: crate docs `#![doc = include_str!("../README.md")]`,
  `#![forbid(unsafe_code)]`, and a private `mod wordlists { include!(concat!(env!("OUT_DIR"), "/wordlists.rs")); }`;
  and a placeholder `rust/README.md` with the title, a one-line description and one quick-start `rust`
  block that only asserts `true` (T029 replaces it).
- [X] T006 In `rust/`, run `cargo +1.85 generate-lockfile` (edition 2024's resolver picks dependency versions
  that support `rust-version` 1.85), then `cargo +1.85 build --locked` and `cargo +stable build --locked`.
  Confirm `Cargo.lock` resolves `unicode-normalization` 0.1.25 and dev-dependencies that build on 1.85.
- [X] T007 [P] Create `rust/scripts/set-version.sh` (bash, `set -euo pipefail`): read `../VERSION` (trimmed),
  rewrite the first `version = "…"` line of `Cargo.toml`, then run `cargo update -p persian-text-guard
  --offline` in `rust/` and, when they exist, in `rust/bench/` and `rust/consumer/`, so **every** lock file
  that records the crate's version follows (their path dependency on the crate is recorded with its
  version, so a `--locked` build there would otherwise fail after a bump); print the old and new versions,
  or "no change". **Self-check**: with `VERSION` unchanged it reports "no change"; a hand-edited `version =
  "0.0.1"` in `Cargo.toml` makes `cargo build` fail with the message from T004; running the script restores
  the version and the build. T030 and T048 re-run this self-check once their packages exist.
- [X] T008 Add the lint command to `verification.md` and confirm it runs clean on the skeleton:
  `cargo fmt --check && cargo clippy --locked --all-targets -- -D warnings && RUSTDOCFLAGS="-D warnings"
  cargo doc --locked --no-deps && cargo test --locked` (and `cargo +1.85 test --locked`). Commit T002–T008 as
  "Scaffold the Rust port (spec 005)".

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Amend the runner rules, generate the Unicode tables, and build the layers every story uses.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

### The corpus runner amendment (research R4)

- [X] T009 Apply [contracts/corpus-runner-amendment.md](contracts/corpus-runner-amendment.md) to
  `specs/002-monorepo-conformance-corpus/contracts/corpus-format.md` ("Runner obligations" 3 and 5) and to
  the matching runner rules in `conformance/README.md`, wording both the same. Add a line under the 002
  contract's title: "Amended by spec 005 (FR-017) for ports whose strings cannot hold lone surrogates or
  have no missing string value." No case file changes. Run the .NET, JavaScript and Python corpus runners
  to confirm nothing moved (532 × 3, 530, 532 with `pytest -m corpus`). Commit as "Amend the corpus runner
  rules for ports like Rust (spec 005)".

### The Unicode tables (research R1, FR-004)

- [ ] T010 Create `rust/tools/gen_tables.cs`, a .NET 10 file-based program (no project file), with a header
  comment stating what it generates and why (R1). Run as `dotnet run tools/gen_tables.cs -- src/tables.rs`
  from `rust/`. It writes, deterministically (LF line endings, no timestamp):
  - a header "Generated by tools/gen_tables.cs from .NET <Environment.Version> (Unicode data of
    System.Globalization.CharUnicodeInfo); do not edit.";
  - `pub(crate) static CATEGORY_RANGES: [(u32, Category); N]`: the start of every run of equal
    `CharUnicodeInfo.GetUnicodeCategory(int)` over U+0000–U+10FFFF, with surrogates as `Cs` (expect 4,099
    runs); `Category` variants named `Lu`, `Ll`, … as in the porting conventions;
  - `pub(crate) static LOWER: [(u16, u16); M]`: every unit whose `char.ToLowerInvariant` differs from itself,
    sorted (expect 1,172);
  - rustfmt-clean output, and a `#[rustfmt::skip]` on the two arrays so formatting never rewrites them.
  Commit the generated `rust/src/tables.rs`.
- [ ] T011 [P] Create `rust/src/unicode.rs`, the port of `js/src/unicode.ts` using the porting table:
  - `category_of_unit(u)`: binary search in `CATEGORY_RANGES` (partition point on the start); ASCII fast path
    through a `const` 128-entry table; `category_at(text, i)` combines a valid surrogate pair first;
  - `is_white_space(u)`: `matches!(u, 0x09..=0x0D | 0x20 | 0x85 | 0xA0 | 0x1680 | 0x2000..=0x200A | 0x2028 |
    0x2029 | 0x202F | 0x205F | 0x3000)`;
  - `to_lower_invariant(u)`: ASCII fast path, else binary search in `LOWER`;
  - `is_noncharacter`, `noncharacter_length_at` (a valid pair read as one code point);
  - `nfkc(view)`: decode the view with `char::decode_utf16`; runs of `char`s between noncharacters go through
    `unicode_normalization::UnicodeNormalization::nfkc`, noncharacters are copied unchanged; encode back to
    UTF-16. A lone surrogate never reaches it (the normalizer replaces them first, as in TypeScript); if one
    does, it is copied through unchanged (no panic). `is_nfkc` (compare with `nfkc`, with an ASCII fast
    path), and `nfd` the same way.
- [ ] T012 [P] Create unit tests in `rust/src/unicode.rs` (`#[cfg(test)] mod tests`), porting
  `js/test/unicode.test.ts` and 004's `python/tests/test_unicode.py`:
  - U+0085 is whitespace; U+001C, U+FEFF and U+200B are not; the whitespace set equals the Zs/Zl/Zp
    categories of the generated table plus U+0009–U+000D and U+0085;
  - `to_lower_invariant(0x130) == 0x130`, `to_lower_invariant(0x3A3) == 0x3C3`, and the 8 Unicode 16/17
    case pairs of R1 (U+1C89, U+A7CB, …) stay unchanged, as in .NET 10;
  - `category_of_unit(0xD83D)` is `Cs`; `category_at` of U+20000 as a pair is `Lo`, and of its second unit
    `Cs`; U+0378 is `Cn`; U+1171E is `Mc` (as in .NET 10);
  - `nfkc` folds presentation forms and U+1D424 (as a view) to `k`, and keeps all 66 noncharacters while
    normalizing around them; `noncharacter_length_at` for U+FFFE, U+FDD0, U+1FFFE, U+10FFFF, `a`, 😀.
- [ ] T013 [P] Create `rust/src/types.rs` and `rust/src/errors.rs` exactly as
  [contracts/public-api.md](contracts/public-api.md) → "Declarations" and [data-model.md](data-model.md):
  - the four `#[non_exhaustive]` enums with `ALL: &'static [Self]`, `name()`, `Display` and `FromStr` on the
    corpus names ("Enums … parse case-sensitively from their names; an unknown name is `ParseNameError`,
    whose message lists the valid names");
  - `EvasionSet` and `Normalization` bit sets (`STANDARD` = the .NET flags 299, `COMPARISON` = 1023; steps
    "run in declaration order whatever order a set is built in"), with `BitOr`, `FromIterator`, iteration;
  - `BannedWord` (`#[non_exhaustive]`, public fields readable, built only with `new`, `with_mode`,
    `with_category`); `ProfanityFilterOptions` (`#[non_exhaustive]`, `Default` all `true`, three setters);
    `ProfanityMatch<'f>` (`#[non_exhaustive]`, produced only by the filter) with `range()`. The non-exhaustive
    attributes keep a future field or option a MINOR change (constitution: new public API is additive);
  - `InvalidMask`, `ParseNameError`, `WordListError` (`#[non_exhaustive]`: `UnknownCategory { line, name }`
    with a 1-based line and the message "Line {line}: unknown word category '{name}'.", `Io`, `InvalidUtf8 {
    valid_up_to }`), each `Display`, `std::error::Error` (with `source()` for `Io`), `Send + Sync + 'static`;
  - rustdoc on every public item explaining behaviour and edge cases, with a runnable example on each type.
- [ ] T014 [P] Create `rust/src/utf16.rs` (research R2): `to_view(&str) -> Cow<[u16]>`-like helper (a `Vec<u16>`
  from `encode_utf16`; callers keep a separate ASCII fast path that avoids it where the TypeScript allows);
  `ByteMap` built from `&str` only when needed, converting a unit region `(index, length)` to a byte region
  `(start, len)` (identity for ASCII); `from_view(&[u16]) -> String` with `String::from_utf16_lossy` (no lone
  surrogates are produced internally, but a lossy decode can never panic). Docs with R2's `kir𠀀` example:
  units 0..5 are bytes 0..7.
- [ ] T015 Port `js/src/normalizer.ts` to `rust/src/normalizer.rs`: the step bits, presets, `normalize_with_map`
  on a view (with and without a source map), `normalize_compatibility_by_segment`, `replace_lone_surrogates`,
  `tokenize_with_offsets` returning `(start, end)` unit pairs, `is_word_character`, `collapse_repeats`,
  `collapse_whitespace`. Public wrappers: `normalize(&str, Normalization) -> String`, `tokenize(&str) ->
  Vec<&str>` (token unit ranges converted to byte ranges of the input and sliced from it),
  `to_persian_digits`, `to_ascii_digits`. Speed (R9): a `const` per-unit translation where the TypeScript
  loops unit by unit and the mapping is context-free.
- [ ] T016 Port `js/src/word-list.ts` to `rust/src/word_list.rs`: `WordList` with `all()` and
  `persian_default()` through `std::sync::LazyLock<Box<[BannedWord]>>` (parsed once, the same slice every
  call), `bundled(&[WordCategory]) -> Vec<&'static BannedWord>` in list order, `parse(&str)` with the .NET
  trimming semantics (`trim_dot_net` using `is_white_space` per unit) and headings by category name only, in
  any case, spaces allowed (`[3]` is `UnknownCategory { line: 1, name: "3" }`), `load(path)` and
  `load_reader(reader)`: read all bytes, drop a leading `EF BB BF`, `std::str::from_utf8` (error →
  `InvalidUtf8 { valid_up_to }`), then `parse`; I/O errors → `Io`.
- [ ] T017 [P] Port `js/src/fold.ts` to `rust/src/fold.rs`: the fold, squeeze and character-class helpers on
  views, with `const` tables built at compile time where the TypeScript builds them at load. The Latin base
  letters for U+00C0–U+024F come from `unicode::nfd` at first use through `LazyLock`, as the TypeScript
  builds them from `nfd` at load: the decompositions of this range are identical in Unicode 16 and 17, so
  the result equals .NET's. A unit test pins five of them (`ü`→`u`, `ş`→`s`, `ƒ`→`f`, `ø`→`o`, `ß`→`s`).
- [ ] T018 Port `js/src/source-map.ts` to `rust/src/source_map.rs`: `MappedText { text, start_map, end_map }`,
  `ReadingKind`, `build` with a per-message cache of four slots, `chunk_map`, `whole_message_map`.
- [ ] T019 Port `js/test/internals.test.ts` to unit tests in `rust/src/normalizer.rs`,
  `rust/src/source_map.rs` and `rust/src/word_list.rs`, test for test, with the same samples (004's
  `python/tests/test_internals.py` lists their code points). Every test passes.
- [ ] T020 Run the lint command from T008 and `cargo test --locked` (unit tests) on stable and 1.85, and
  `git diff --exit-code src/tables.rs` after regenerating. Commit T010–T020 as "Port the types, Unicode
  tables, UTF-16 view, normalizer, word lists, fold helpers and source maps to Rust".

**Checkpoint**: The shared layers pass their tests; the runner rules allow 0 not-applicable cases.

---

## Phase 3: User Story 1 - Check and censor messages from Rust (Priority: P1) 🎯 MVP

**Goal**: The filter as users get it: `ProfanityFilter`, the byte versions, `WordList`, normalization, in a
packaged crate.

**Independent Test**: spec US1: from the packaged crate, in a new project, `ک.ی.ر` is flagged,
`سلام، سفارشم کی میرسه؟` is not, and `kir and motherfucker` censors to `**** and ****`.

### Tests for User Story 1 (write first; they fail until T024–T028)

- [ ] T021 [P] [US1] Create `rust/tests/api.rs` using only `persian_text_guard::*`, covering spec US1
  scenarios 1–7 and guarantees G3–G7 and G11 of [contracts/public-api.md](contracts/public-api.md):
  - scenario 3 exactly: `shit` at byte 0, 4 bytes, evasion `{LookalikeCharacters}`; `fuck` at byte 9, 7 bytes,
    `{SplitWord}`; scenario 4: `"😀 کیر"` gives start 5, len 6, and `&text[m.range()] == "کیر"`;
  - scenario 5: `BannedWord::new("اسپم")` and `BannedWord::new("casino").with_mode(Anywhere)` flag
    `"onlinecasino.example"`; scenario 6: `censor_with("this is kir", '#')` is `"this is ####"` and
    `censor_with(_, 'x')` is `Err(InvalidMask { mask: 'x' })`;
  - G5, masks: `'x'`, `'5'`, `' '`, `'\n'`, `'\u{1F600}'` rejected, even for `""`; `'#'`, `'*'`, `'■'`, `'•'`
    accepted (`censor_with("kir", m) == m.to_string().repeat(4)`);
  - G4 on `"kir\u{20000} and fuck"`: matches `kir` at 0..7 and `fuck` at 12..16, slicing the caller's string;
  - G6, a compile-time assertion `fn assert_send_sync<T: Send + Sync>()` for `ProfanityFilter`,
    `ProfanityMatch<'static>`, `BannedWord`, every enum, both sets and all three errors;
  - G7: `std::ptr::eq(WordList::all(), WordList::all())`, and no `Mild` in `persian_default()`;
  - G11: `"slur".parse::<WordCategory>()`, `"nope".parse::<WordCategory>()` is an error naming the valid names,
    `"comparison".parse::<Normalization>() == Ok(Normalization::COMPARISON)`;
  - `count()`: `كص`, `کص`, `  کص ` and `""` count once; an empty list is clean; any iterable works (`Vec`,
    slice, `HashSet`, `iter().cloned()`);
  - G3 over a handful of inputs (T036 extends it to every corpus input), including `""`, `"   "`,
    `"hi \u{FFFD} kir"`, `"k kos i kos r"`, `"جنده\u{200C}ها رو ببین"`.
- [ ] T022 [P] [US1] Create `rust/tests/word_list_load.rs` (G8): a UTF-8 file with a BOM and `\r\n` in a
  temporary directory (`std::env::temp_dir()` plus a unique name, removed afterwards), loaded by path
  (`&str`, `PathBuf`) and by reader (`File`, `&mut File`, `&[u8]`, `Cursor`): all equal `parse` of the text
  without the BOM; a missing path is `Io` with kind `NotFound`; invalid UTF-8 is `InvalidUtf8` with the right
  `valid_up_to`; `[3]` on line 2 is `UnknownCategory { line: 2, .. }`.
- [ ] T023 [P] [US1] Create `rust/tests/bytes.rs` (G9, FR-008a):
  - valid UTF-8 bytes give exactly the `&str` results;
  - `b"kir \xFF fuck"`: two matches, `kir` 0..3 and `fuck` 6..10; `censor_bytes` gives `b"**** \xFF ****"`,
    the invalid byte kept;
  - `b"k\xC3ir"` (a truncated sequence inside a word): the decision equals the string version on
    `String::from_utf8_lossy`, and the region covers the whole word including the invalid byte;
  - a mask with a multi-byte UTF-8 encoding (`'■'`) is spliced as bytes; `censor_bytes_with(_, 'x')` is
    `Err(InvalidMask)`; empty input gives empty output.

### Implementation for User Story 1

- [ ] T024 [US1] Port `js/src/scan.ts` to `rust/src/scan.rs`: `Entry`, `Key`, `Phrase`, `ScanState`, `Hit`, the
  sink, `scan`, `match_tokens`, `match_anywhere` (a search for a `[u16]` needle, the TypeScript's `indexOf`
  loop), `try_join_single_letters`, `match_split_halves`, `match_broken_chunks`, `masked_pattern`,
  `fits_mask`, `try_find_word` with the Persian suffixes, `phrase_starts_at`, `has_suffix`. Positions are
  view units. Where the TypeScript relies on `charCodeAt` of an empty string giving `NaN`, guard explicitly.
- [ ] T025 [US1] Port `js/src/regions.ts` to `rust/src/regions.rs`: `Candidate`, `Region`, `to_candidate`
  (with the source-map cache), `merge` (sort by start ascending, end descending, stable, as in TypeScript),
  `MASK_LENGTH = 4`. Censoring regions are applied by `filter.rs` on the caller's string.
- [ ] T026 [US1] Port `js/src/filter.ts` to `rust/src/filter.rs`: `ProfanityFilter` with `new` and
  `with_defaults` (entries cloned into `Box<[BannedWord]>`; lookups store indexes into it; the rest as the
  TypeScript builds it: de-duplication by (mode, normalized text), a folded key beside the original when
  folding is on), `count`, `contains_profanity`, `find_match`, `find_matches` (positions converted with
  `ByteMap`), `censor` and `censor_with`: validate the mask first ("a letter, digit, whitespace, control
  character, or a character above U+FFFF" is `InvalidMask`), then splice the caller's string with the
  mask repeated four times, then keep re-scanning until clean with the TypeScript's pass cap.
- [ ] T027 [US1] Create `rust/src/bytes.rs` (research R3) and the five byte methods on `ProfanityFilter`:
  decode with `<[u8]>::utf8_chunks` into a `Cow<str>` (borrowed when the input is valid) and a table from
  decoded byte offsets to source byte offsets (each U+FFFD's three bytes map to the whole invalid sequence);
  run the `&str` path; map regions back so a region covering a U+FFFD covers the whole invalid sequence;
  `censor_bytes*` splices the caller's bytes with the mask's UTF-8 encoding four times.
- [ ] T028 [US1] Complete `rust/src/lib.rs`: `pub use` exactly the declarations of the contract (nothing else
  public), crate-level docs from the README, `#![warn(missing_docs)]`. Run `cargo test --locked --test api
  --test word_list_load --test bytes` until every test passes.
- [ ] T029 [P] [US1] Replace the placeholder `rust/README.md` with a short English README whose quick start
  is a `rust` block building a filter from `WordList::persian_default()` and asserting the three US1
  results; it runs as a doc test. (T049 writes the full README.)
- [ ] T030 [P] [US1] Create `rust/consumer/` (`publish = false`, its own `Cargo.toml` and lock file, not a
  workspace member): `src/main.rs` runs the quick start with asserts and prints `ok`. Its dependency is
  `persian-text-guard = { path = "../target/package/persian-text-guard-<v>" }` written by
  `scripts/check-package.sh` (T031) into a generated `Cargo.toml`, so it builds against the unpacked crate,
  never the source tree. `consumer/Cargo.lock` is committed; `check-package.sh` runs `cargo update -p
  persian-text-guard --offline` in `consumer/` after writing the path, before `cargo run --locked`.
- [ ] T031 [US1] Create `rust/scripts/prepare-package.sh` (copy `../wordlists/{persian,finglish,english}.txt`
  into `wordlists/`, and `../LICENSE`, `../THIRD-PARTY-NOTICES.md`; print what it copied) and
  `rust/scripts/check-package.sh`:
  1. `cargo package --locked --list` equals the allowlist of contracts/package-and-release.md exactly
     (ignoring `.cargo_vcs_info.json`, which only a clean tree adds);
  2. `cargo package --locked` succeeds, which builds the unpacked crate on its own (no `../wordlists`).
     Cargo warns that the `corpus` test target is not in the package and drops it from the published
     manifest: that warning is expected, and the check confirms the packaged `Cargo.toml` has no
     `[[test]]` section;
  3. the `.crate` file is under 1 MB (SC-004);
  4. `cargo metadata` of the manifest: name, version = `../VERSION`, `rust-version` 1.85, exactly one
     normal dependency, `unicode-normalization`;
  5. writes `consumer/Cargo.toml`'s dependency path to the unpacked crate and runs `cargo run --locked` in
     `consumer/`, expecting `ok`.
  It prints each check and exits non-zero on the first failure.
- [ ] T032 [US1] Run `scripts/prepare-package.sh && scripts/check-package.sh`. Record in `verification.md`
  under "US1": the test results, the file list, the `.crate` size and the consumer output. Commit T021–T032 as
  "Add the Rust filter, byte versions, package checks and consumer check".

**Checkpoint**: The crate packages, builds on its own, and works for users (MVP).

---

## Phase 4: User Story 2 - The same answer as every other port, proven by the corpus (Priority: P1)

**Goal**: All 523 corpus cases pass, 0 not applicable, on Rust 1.85 and stable; one filter is safe across
threads; nothing panics.

**Independent Test**: spec US2: `cargo test --test corpus` passes every case; a corrupted case reports its
id, file, visible input and differences.

- [ ] T033 [P] [US2] Create `rust/tests/corpus/load.rs` and `rust/tests/corpus/values.rs` (included from
  `tests/corpus.rs` with `mod corpus;` and `#[path]` as needed), ports of `js/test/corpus/load.ts` and
  `values.ts`, with 004's Python runner as a second reference:
  - `find_repository_root()` walks up from `env!("CARGO_MANIFEST_DIR")` to a directory with `VERSION` and
    `wordlists/`; `load_corpus` reads `corpus.json`, `configurations.json` and `cases/*.json` in ordinal file-name
    order with `serde_json`, refusing a newer `formatVersion` and naming the file in every error; a missing
    directory gives "Conformance corpus not found: <path>";
  - `build_input(node) -> Option<String>`: `null` → `None`; parts become UTF-16 units (`text` parts encoded,
    `repeat` × `times`, `utf16` units), then `String::from_utf16_lossy` (the amended rule: lone surrogates →
    U+FFFD, valid pairs kept);
  - `code_points_to_bytes(text, start, len)`; `show_invisible` (Cf, Cc, Zl, Zp, whitespace other than
    U+0020, U+FFFD from a replaced surrogate and noncharacters shown as `\u{XXXX}`); `compare(expected,
    actual)` returning `(path, expected, actual)` triples, text compared by built value.
- [ ] T034 [US2] Create `rust/tests/corpus/evaluate.rs`, a port of `js/test/corpus/evaluate.ts` against the
  public API: one filter per configuration (built once); the `null` Input read as `""` (amended rule);
  matching kinds return `containsProfanity`, `firstMatch`, `matches` and `censored`, where the **expected**
  positions are converted from code points to bytes of the built input before comparing (FR-016, the
  amended obligation 5), and the port's byte positions are compared as they are,
  plus `censoredWith` for `masks`; normalization with a preset name or step names parsed through `FromStr`;
  `mask-validation`: a mask that builds into one `char` is `accepted` exactly when `censor_with("kir", c)` is
  `Ok`; a mask that does not build into one `char` (the lone surrogate) is `accepted: false` (amended
  rule); `check_kind_rules` with .NET's wording.
- [ ] T035 [US2] Create `rust/tests/corpus.rs` (`harness = false`) with `libtest-mimic`: the corpus loads once;
  one `Trial` per case named by its id, whose failure message is `Case '<id>' in <file>: <problem>`, then
  `  input "<show_invisible>"`, then one `  <path>: expected <e> actual <a>` line per difference ("breaks its
  kind rule" for kind-rule violations); plus guard trials: loads, `formatVersion == 1` and a newer version
  refused (a copy in a temp directory), at least 300 cases, unique ids matching
  `^[a-z0-9]+(-[a-z0-9]+)*$`, no pending case (naming the ids and the fill command), every configuration
  exists, and 0 cases not applicable.
- [ ] T036 [US2] Run `cargo test --locked --test corpus`: every case passes (523 plus the guards).
  - **Failures**: find the root cause by comparing the Rust module with the TypeScript, the C# and the
    Python port, and fix the port. **Never** edit the corpus to match the port.
  - **Unicode differences**: if a failure is a genuine difference in Unicode data (it can only come from
    NFKC or NFD, research R1), stop and report it with the case id and code points.

  Extend `rust/tests/api.rs` with G3 over every matching corpus input (built with the same rules). Record the
  count and time in `verification.md`.
- [ ] T037 [US2] Create `rust/tests/no_panic.rs` with `proptest` (SC-007, G1, G9): strategies for arbitrary
  `String`s biased toward Persian letters, ZWNJ, digits, symbols, supplementary characters, noncharacters
  and runs of repeats, and for arbitrary `Vec<u8>` (mostly invalid UTF-8); every public text function
  called with the default filter; for bytes, the byte versions agree with the string versions on
  `String::from_utf8_lossy`, and every region is inside the input. The case count comes from proptest's
  standard `PROPTEST_CASES` environment variable, defaulting to 2,000 per strategy, so every `cargo test` on
  every matrix job stays quick; the `Rust checks` job (T042) runs `PROPTEST_CASES=100000 cargo test --locked
  --release --test no_panic`, which is SC-007's 100,000 strings and 100,000 byte sequences. Run the full count
  once locally too, and record both run times.
- [ ] T038 [US2] Create `rust/tests/threads.rs` (SC-008, G6, G7): one filter per corpus configuration, a
  single-threaded pass over every matching input recording `contains_profanity`, `find_matches` and
  `censor`; 8 threads behind `std::sync::Barrier` (with `std::thread::scope`), each checking every input
  twice and comparing; and 8 threads calling `WordList::all()` and `persian_default()` at the same moment in
  a fresh process, all getting the same pointers. The fresh process is the test binary itself:
  `std::env::current_exe()` run with the environment variable `PTG_FIRST_USE_CHILD=1` and the arguments
  `first_use_child --exact --nocapture`; the `first_use_child` test does nothing unless that variable is set,
  and then prints `<count> <distinct pointers>` for the parent to check (`8 1`).
- [ ] T039 [US2] Run the whole suite on stable and 1.85 (`cargo +1.85 test --locked`) on this machine; record
  both versions and pass counts. Linux, macOS and Windows MSVC are covered by CI (T042), which runs before
  the dry run.
- [ ] T040 [US2] Check failure reporting on scratch edits, reverted afterwards with `git checkout --
  conformance`: change `fa-emoji-before-word`'s `censored` to `"😀 ####"` and set
  `matching-persian-ordinary-messages-pass-001`'s `containsProfanity` to `true`; `cargo test --test corpus`
  fails exactly those 2 trials, with the T035 messages; then rename `conformance/` and confirm "Conformance
  corpus not found", and rename it back. Record both outputs.
- [ ] T041 [US2] Cross-check the bundled selections (FR-018, SC-002, quickstart §3): a `#[test] #[ignore]` in
  `rust/tests/api.rs` (run with `--ignored`) writes `artifacts/compare/rust.txt` (`text\tmode\tcategory` per
  entry) and prints the counts JSON; diff with `artifacts/compare/python.txt` and `js.txt` (regenerate them as
  in 004 T041 if missing): 0 differences, same counts. Record, and commit T033–T041 as "Run the conformance
  corpus against the Rust port".

**Checkpoint**: Four ports pass one corpus; the Rust port is behaviourally complete, thread-safe and panic-free.

---

## Phase 5: User Story 3 - Released to crates.io together with NuGet, npm and PyPI (Priority: P2)

**Goal**: CI builds, tests and gates the Rust port, and a tag publishes all four packages together.

**Independent Test**: spec US3. Local checks first (T045); the real proof is the CI run and dry run
(T059–T062).

- [ ] T042 [US3] **Do this after T048 and T052**, whose scripts the jobs run. Add the Rust jobs to
  `.github/workflows/ci.yml`, leaving every existing job name unchanged (research R11):
  - **Job `rust`**, `name: Rust (${{ matrix.toolchain }}, ${{ matrix.os }})`, `strategy.fail-fast: false`,
    `matrix.toolchain: ["stable", "1.85"]`, `matrix.os: [ubuntu-latest, windows-latest, macos-latest]`,
    `defaults.run.working-directory: rust`; steps: `actions/checkout@v5`; `dtolnay/rust-toolchain` **pinned by
    commit** (look up the current `master` commit with `gh api`; 004 found that `astral-sh/setup-uv` has no
    moving major tag, so check every new action's tags before choosing a ref) with `toolchain: ${{
    matrix.toolchain }}`; `Swatinem/rust-cache` pinned likewise, with `workspaces: rust`; `cargo test
    --locked`.
  - **Job `rust-checks`**, `name: Rust checks`, ubuntu, stable with `components: rustfmt, clippy`,
    `fetch-depth: 0`: `cargo fmt --check`; `cargo clippy --locked --all-targets -- -D warnings`;
    `RUSTDOCFLAGS="-D warnings" cargo doc --locked --no-deps`; `actions/setup-dotnet@v5` with `10.0.x`, then
    `dotnet run tools/gen_tables.cs -- src/tables.rs` and `git diff --exit-code src/tables.rs`;
    `PROPTEST_CASES=100000 cargo test --locked --release --test no_panic` (SC-007, T037);
    `scripts/prepare-package.sh` and `scripts/check-package.sh`; the API check (T052); the benchmark gate
    (`scripts/bench-gate.sh`, T048); `actions/upload-artifact@v4` with `name: crate-package`, `path:
    rust/target/package/*.crate`.
  - Use `run: |` block scalars for any command containing `": "` (003's and 004's lesson), and validate the
    YAML with the scratchpad's `yamlcheck/check.mjs` (strict, unique keys; `npm install yaml@2` there if
    missing).
- [ ] T043 [US3] Add the crates.io publish job, and gate the others (research R12):
  - **`publish`, `publish-npm`, `publish-pypi`**: `needs: [build, netfx, javascript, python, rust, rust-checks]`.
  - **New `publish-crates`**, `name: Publish to crates.io`, same `needs` and `if: startsWith(github.ref,
    'refs/tags/v')`, `runs-on: ubuntu-latest`, `environment: crates-io`, `permissions: { contents: read,
    id-token: write }`; steps:
    1. checkout;
    2. "Check tag matches VERSION", the same command as the other publish jobs;
    3. "Check Cargo.toml matches VERSION" (`grep` the manifest's version in `rust/Cargo.toml`);
    4. `dtolnay/rust-toolchain` (stable, pinned);
    5. "Skip if already published": `curl` `https://crates.io/api/v1/crates/persian-text-guard/<v>` with a
       `User-Agent` naming the repository; a 200 sets an output `exists=true` and later steps are skipped;
    6. `rust/scripts/prepare-package.sh`;
    7. "Authenticate (trusted publishing)": `rust-lang/crates-io-auth-action` pinned by commit, `id: auth`,
       only when the environment secret `CARGO_REGISTRY_TOKEN` is empty (expose `HAS_TOKEN: ${{
       secrets.CARGO_REGISTRY_TOKEN != '' }}` as a job env and test it in `if:`);
    8. `cargo publish --locked` in `rust/`, with `CARGO_REGISTRY_TOKEN: ${{ secrets.CARGO_REGISTRY_TOKEN ||
       steps.auth.outputs.token }}`.
  - Validate the YAML as in T042.
- [ ] T044 [US3] Move every compatibility baseline to the previous release, 1.4.0 (research R16): in
  `dotnet/src/PersianTextGuard/PersianTextGuard.csproj` set `PackageValidationBaselineVersion` to `1.4.0`
  and run `dotnet pack`; `npm run api:compat` in `js/` names `v1.4.0`; `uv run python scripts/check_api.py`
  in `python/` now finds `v1.4.0` and runs griffe for real: it must pass (the Python API is unchanged).
  Record all three.
- [ ] T045 [US3] Check the release gates locally (as 004 T045), and record: reading `ci.yml` back, every
  publish job needs all six build and test job ids and runs only on `refs/tags/v`; the tag check passes for
  `v$(cat VERSION)` and fails for `v9.9.9`; the Cargo.toml check passes and fails likewise; the "already
  published" step returns 404 for `persian-text-guard/9.9.9` today.
- [ ] T046 [US3] Set `VERSION` to `1.5.0` and run `rust/scripts/set-version.sh`. Rebuild all four packages and
  confirm the version: `cargo package` gives `persian-text-guard-1.5.0.crate`; `uv build` in `python/` gives
  `persian_text_guard-1.5.0-*`; `npm run pack` in `js/` gives `persian-text-guard-1.5.0.tgz`; `dotnet pack`
  gives `PersianTextGuard.1.5.0.nupkg` with validation passing against 1.4.0. Commit T042–T046 as "Publish
  to crates.io in lockstep; version 1.5.0".
- [ ] T047 [US3] Create the GitHub environment `crates-io` with `gh api` exactly as `pypi` was created in 004
  (custom deployment policy, one rule `v*` of type `tag`, no secrets), before any push that runs the new
  workflow, and verify it with `gh api .../environments/crates-io/deployment-branch-policies`. Then 👤 tell the
  maintainer the crates.io steps of [contracts/package-and-release.md](contracts/package-and-release.md) →
  "Release 1.5.0", steps 5 and 8, with exact values: they are needed only before T066 and after T066.

**Checkpoint**: CI gates and publishes all four registries; only the dry run and the release remain.

---

## Phase 6: User Story 4 - Documented, measured and kept compatible (Priority: P3)

**Goal**: Bilingual crates.io README with tested examples, Criterion numbers and API compatibility.

**Independent Test**: spec US4: `cargo doc` with warnings denied passes; the benchmarks match the README
table; `cargo-semver-checks` catches a changed signature.

- [ ] T048 [P] [US4] Create `rust/bench/` (`persian-text-guard-bench`, `publish = false`, not a workspace
  member, its own committed `Cargo.lock`, kept in step by `set-version.sh` (T007); `criterion = "0.8"` and
  `persian-text-guard = { path = ".." }`):
  `benches/filter.rs` with the ten benchmarks and message constants copied verbatim from
  `js/bench/filter.bench.ts`, same names; `src/bin/bench_table.rs` reading
  `target/criterion/<name>/new/estimates.json` (mean point estimate) and printing the README table
  (Operation with the descriptive names of 004's `bench_table.py`, Mean, Operations/s) with the Rust version
  and CPU model; and `rust/scripts/bench-gate.sh`, which runs `cargo bench --locked --bench filter --
  CleanShortMessage --warm-up-time 1 --measurement-time 3` in `rust/bench/` and then `cargo run --locked
  --release --bin bench_table -- --gate CleanShortMessage --limit-us 50`, which reads that benchmark's
  `estimates.json` and exits non-zero when the mean exceeds the limit (50 µs, the CI gate; `--limit-us 5`
  applies SC-005).
- [ ] T049 [US4] Write the full `rust/README.md` (FR-024, research R13), every code block a `rust` doc test:
  - title `# persian-text-guard`, a one-line English description, badges-free;
  - **English**: installation (`cargo add persian-text-guard`, minimum Rust 1.85); the quick start; what
    matched and why; positions in bytes and slicing; censoring and masks (`censor`, `censor_with`); categories;
    your own words; word-list files (`parse`, `load`, `load_reader`); options; normalization and tokenizing;
    raw bytes, under the heading "Byte versions" (the name the spec and the name table use; invalid UTF-8
    kept in the output); matching on `#[non_exhaustive]` enums
    with a wildcard arm; thread safety (`std::thread::scope`); errors table; the four-language name table from
    the contract; the performance table from T050; limitations (R1's NFKC/NFD note, Unicode 16/17
    characters; no `no_std`); links (project README, docs.rs, the other packages);
  - **Persian**: installation and quick start, each paragraph in its own `<div dir="rtl">` block starting and
    ending with a Persian word, code in separate blocks with comments in both languages (004 R16).
- [ ] T050 [US4] Run the benchmarks on this machine (confirm the i7-9700K with `Get-CimInstance
  Win32_Processor`; stop if it differs) with stable Rust: `cd rust/bench && cargo bench`, then `cargo run
  --release --bin bench_table`. Check SC-005: build under 5 ms, `CleanShortMessage` under 5 µs,
  `VeryLongMessage` under 50 ms. If one is missed, profile (`cargo bench` with `--profile-time`, or
  counting allocations) and optimise the hot path before continuing; do not weaken a target without the
  maintainer. Put the table in `rust/README.md` and `verification.md`.
- [ ] T051 [US4] Confirm the README examples are doc tests: `cargo test --locked --doc` lists one test for every
  `rust` block in `rust/README.md` (count them with `grep -c '^```rust' rust/README.md`; T049's outline gives
  at least 14: quick start, what matched, positions, censoring, categories, own words, word-list files,
  options, normalization, byte versions, non-exhaustive matching, threads, and the Persian quick start and
  censoring), plus one per public type's example, and all pass on stable and 1.85. Record both counts.
- [ ] T052 [US4] Add the API check (research R10): a CI step in `rust-checks` that queries
  `https://crates.io/api/v1/crates/persian-text-guard` and, on 404, prints "baseline: no previous release"
  and passes; otherwise runs `obi1kenobi/cargo-semver-checks-action` (pinned by commit, `package:
  persian-text-guard`, `manifest-path: rust/Cargo.toml`). Put the decision in `rust/scripts/check-api.sh`
  so it runs locally too (`cargo semver-checks` must be installed: `cargo install cargo-semver-checks
  --locked`). **Prove it** (quickstart §5): on a throwaway commit change `censor` to take `String`, run
  `cargo semver-checks --baseline-rev HEAD~1`, confirm it fails and names the change, then `git reset HEAD~1`
  and `git checkout -- rust/src`. Record the output.
- [ ] T053 [US4] Check the documentation (FR-023, SC-006): `RUSTDOCFLAGS="-D warnings -D missing_docs" cargo
  doc --locked --no-deps` passes; read the generated pages for `ProfanityFilter::censor_with`,
  `ProfanityFilter::find_matches_bytes` and `WordList::load_reader`, and confirm they explain the edge cases
  (mask rules, byte positions, invalid sequences, BOM), not only the signatures.
- [ ] T054 [US4] Update the root `README.md` (FR-025): the packages section lists crates.io next to NuGet,
  npm and PyPI, with `cargo add persian-text-guard` and a short Rust example; "Changes in 1.5.0" (Rust added;
  corpus runner rules amended; .NET, JavaScript and Python unchanged); "Development" shows `rust/` and its
  commands; "Releasing" adds crates.io, the `crates-io` environment and the first-release token; "Limitations"
  gains the Rust note (R1). The root README has no Rust doc tests; its Python example stays tested by
  `python/tests/test_readme.py` (re-run it).
- [ ] T055 [US4] Draft `specs/005-rust-port/release-notes-1.5.0.md` in English with a Persian summary in
  `<div dir="rtl">` blocks (as 1.4.0's): the Rust crate with install and quick start; the byte versions; the
  corpus runner amendment (no case changed); NuGet, npm and PyPI 1.5.0 identical to 1.4.0 apart from the
  version; every package now validated against 1.4.0.
- [ ] T056 [US4] Run the lint command, `cargo test --locked` (stable and 1.85), the package and API checks,
  the benchmark gate, and in `python/` `uv run pytest tests/test_readme.py`. Everything passes. Commit
  T048–T056 as "Document, benchmark and API-check the Rust port".

**Checkpoint**: All four user stories are complete.

---

## Phase 7: Polish, Pull Request and Release 1.5.0

**Purpose**: Prove everything from a clean state, prove CI and the release gates on GitHub, then release
with the maintainer's go-ahead.

- [ ] T057 Run the full matrix from a clean state: `git clean -xdn rust js/dist python` first (never
  `graphify-out/`), then `git clean -xdf` of the same paths; .NET tests and conformance on three targets;
  `npm ci && npm run test:all` in `js/`; `uv sync --locked --group package` and the five-Python run of 004
  T057; in `rust/`: `cargo +stable test --locked`, `cargo +1.85 test --locked`, the lint command, the table
  regeneration diff, and the package and consumer checks. Record every count under "Full matrix".
- [ ] T058 Walk through [quickstart.md](quickstart.md) §1–§6 and §8, ticking each expected outcome in
  `verification.md` with the task that produced it. §7 is completed by T059–T068.
- [ ] T059 Push `005-rust-port` (no pull request yet) and run CI on it with `gh workflow run ci.yml --ref
  005-rust-port` **before any tag** (004's lesson: the first run found two workflow errors). Fix and re-run
  until every job is green on Linux, Windows and macOS; record each run. Then prepare the dry run as 004
  T059, with these changes:
  1. **Precondition**: `gh api .../environments/crates-io/deployment-branch-policies` lists `v*` with `type:
     tag` (T047), and `pypi` still does.
  2. `git switch -c dryrun/release-gates`; edit `.github/workflows/ci.yml` only: NuGet push → `ls` and echo;
     npm → `--dry-run`; PyPI upload → `ls -l dist/`; crates.io → remove the authenticate step and replace
     `cargo publish --locked` with `cargo publish --locked --dry-run`; add `"DRY RUN: simulated failure"`
     (`run: exit 1`) as the first step after checkout in the `rust` job.
  3. `VERSION` → `1.5.0-dev.1`, then `rust/scripts/set-version.sh`.
  4. **Safety check** before committing: `grep -nE "dotnet nuget push|NuGet/login|gh-action-pypi-publish|crates-io-auth-action" .github/workflows/ci.yml`
     prints nothing, and neither `grep -n "npm publish" … | grep -v -- --dry-run` nor `grep -n "cargo publish" …
     | grep -v -- --dry-run` prints anything. Validate the YAML. Stop if anything fails.
  5. Commit as "DRY RUN ONLY: release gate test (do not merge)" and push the branch.
- [ ] T060 Dry run 1, a failing Rust job blocks all four registries: tag and push `v1.5.0-dev.1`; wait with
  `gh run watch`; with `gh run view <id> --json jobs` confirm: all six `Rust (…)` jobs `failure`, the other
  build and test jobs `success`, and `Publish to NuGet`, `Publish to npm`, `Publish to PyPI` and `Publish to
  crates.io` `skipped`. If a publish job ran, stop, delete the tag, and report.
- [ ] T061 Dry runs 2 and 3:
  1. **Run 2, everything green.** Remove the failure step, `VERSION` → `1.5.0-dev.2`, `set-version.sh`, safety
     check, commit, push, tag and push `v1.5.0-dev.2`. Every job succeeds; NuGet lists
     `PersianTextGuard.1.5.0-dev.2.nupkg`; npm reports `+ persian-text-guard@1.5.0-dev.2` with tag `next`
     `(dry-run)`; PyPI lists `persian_text_guard-1.5.0.dev2` files; crates.io's `cargo publish --dry-run`
     packages and verifies `persian-text-guard v1.5.0-dev.2` and stops at "aborting upload due to dry run";
     deployments to all four environments are recorded.
  2. **Run 3, mismatched tag.** Tag the same commit `v1.5.0-dev.3` and push: all four publish jobs fail at
     "Check tag matches VERSION".
- [ ] T062 Clean up the dry run: delete the three tags (remote and local) and `dryrun/release-gates` (remote and
  local); confirm `git ls-remote origin | grep -i -E "dev|dryrun"` prints nothing; npm, NuGet and PyPI list
  no 1.5.0 or dev version; `https://crates.io/api/v1/crates/persian-text-guard` is still 404; on
  `005-rust-port`, `VERSION` is `1.5.0` and `ci.yml` has no `DRY RUN` or `--dry-run`; both environments keep
  exactly their `v*` rule. Record under "SC-009: release gates dry run".
- [ ] T063 Commit `verification.md` and push. Open the pull request with `gh pr create`: title "Rust port on
  crates.io, corpus runner amendment, version 1.5.0"; body: summary, the crate, the byte versions, the
  runner amendment, CI and release, the dry run, links to spec, verification and release notes, ending with
  the Claude Code attribution line. Wait for CI: all sixteen build and test jobs pass (.NET ×2, JavaScript
  ×2, Python ×5, Rust ×7) and the four publish jobs are skipped. **Do not merge without the maintainer's
  go-ahead.**
- [ ] T064 After the maintainer approves, `gh pr merge <n> --merge`, fast-forward local `main`, and confirm
  `main`'s CI is green.
- [ ] T065 Add the seven Rust job names to `main`'s required checks with `gh api -X PATCH
  repos/AmirehsanK/PersianTextGuard/branches/main/protection/required_status_checks`, keeping the nine
  existing ones, `strict: false`, `app_id` 15368 (sixteen in all). Ask the maintainer first unless they have
  already approved the release steps.
- [ ] T066 (**irreversible**; needs the maintainer's go-ahead) Push tag `v1.5.0` on the merge commit, only if:
  `main` is green; `VERSION` and `rust/Cargo.toml` are `1.5.0`; the `crates-io` environment has its `v*` rule
  **and** the secret `CARGO_REGISTRY_TOKEN` (👤 created by the maintainer: publish-new scope, crate
  `persian-text-guard`, expiring within 7 days; check with `gh api .../environments/crates-io/secrets`);
  `pypi` still has its rule; **CPython 3.15**: if `uv python list 3.15` shows a final release, add it to the
  Python matrix and classifiers through a pull request first (constitution Principle III); no registry has
  1.5.0. Watch the run: every job succeeds, including all four publish jobs.
- [ ] T067 Verify all four registries (SC-011, quickstart §7): a fresh `cargo new` project with
  `persian-text-guard = "1.5.0"` runs the quick start; `pip install persian-text-guard==1.5.0` and the smoke
  check; `npm view persian-text-guard@1.5.0 version` and a fresh install that flags `ک.ی.ر`; the NuGet index
  lists 1.5.0 and a fresh console app flags it; docs.rs has built `persian-text-guard 1.5.0` (poll until the
  page returns 200, allowing for docs.rs's queue). Record results; allow for registry indexing delays as in
  004 (npm and NuGet took minutes).
- [ ] T068 After the release: delete the `crates-io` environment secret with `gh secret delete
  CARGO_REGISTRY_TOKEN --env crates-io`; 👤 the maintainer revokes the token on crates.io and adds trusted
  publishing on the crate's settings (repository `AmirehsanK/PersianTextGuard`, workflow `ci.yml`,
  environment `crates-io`). Create the GitHub release with `gh release create v1.5.0 --title "PersianTextGuard
  1.5.0" --notes-file specs/005-rust-port/release-notes-1.5.0.md --verify-tag`. Mark T063–T068 done in
  `tasks.md`, record "Release 1.5.0" in `verification.md`, and commit and push to `main` as "Record 1.5.0
  release verification".

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (T001–T008)**: no dependencies. T005 and T007 in parallel.
- **Foundational (T009–T020)**: T009 is independent of the Rust code. T010 → T011 → T012. T013 and T014 in
  parallel with T010–T012. T015 needs T011, T013, T014; T016 needs T011 and T013; T017 needs T011; T018 needs
  T015 and T017; T019 needs T015–T018; T020 last.
- **US1 (T021–T032)**: needs Foundational. T021–T023 first (they fail); T024 → T025 → T026 → T027 → T028;
  T029 and T030 in parallel with T024–T027; T031 → T032.
- **US2 (T033–T041)**: T033 can start with US1; T034 onwards need T028. T035 → T036 → T037 → T038 → T039 →
  T040 → T041.
- **US3 (T042–T047)**: T042 and T043 run **after** T048 and T052. T043 → T045 → T046. T044 is independent.
  T047 must be done before T059's first push that includes the new workflow.
- **US4 (T048–T056)**: T049 needs the API from T028; T048 → T050; T052 is independent; T054 and T055 are
  independent; T056 last.
- **Release (T057–T068)**: strictly sequential. T063 needs every earlier task. T064–T066 need the
  maintainer's decisions; T066 also needs the token secret.

### User Story Dependencies

- **US1** needs only Foundational.
- **US2** needs US1's public API (T028).
- **US3** needs US1's packaging (T031) and US4's benchmark gate and API check (T048, T052) for a green CI.
- **US4** needs US1. Its README doc tests need the finished API.

### Parallel Opportunities

- T005 and T007 during setup.
- T010–T012, T013 and T014 at the start of Foundational; T017 alongside T015 and T016.
- T021, T022, T023, T029 and T030 alongside the scan port.
- T033 alongside US1's implementation.
- T048, T052, T054 and T055 alongside each other.

---

## Parallel Example: User Story 1

```text
Task: "T021 Create rust/tests/api.rs (fails until T028)"
Task: "T022 Create rust/tests/word_list_load.rs"
Task: "T023 Create rust/tests/bytes.rs"
Task: "T029 Short README with the quick-start doc test"
Task: "T030 Consumer crate in rust/consumer/"
```

---

## Implementation Strategy

### MVP First (Foundational + User Story 1)

1. Setup, then Foundational: the runner rules amended, the tables generated, the shared layers passing.
2. US1: the crate packages, builds on its own and passes `tests/api.rs`, `bytes.rs` and `word_list_load.rs`.
3. **Stop and validate**: the consumer crate from the packaged `.crate`.

### Incremental Delivery

1. + US2: 523/523 with 0 not applicable, on 1.85 and stable, thread-safe and panic-free.
2. + US4: README, benchmarks and API check, which CI needs.
3. + US3: CI and crates.io publishing, `VERSION` 1.5.0.
4. Release phase: CI on the branch, dry run, pull request, and the maintainer's go-ahead for merge and tag.

---

## Notes

- **Never commit `graphify-out/`.**
- Commits end with `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`; the PR body ends with the Claude Code line.
- The corpus is never edited to match a port; the runner amendment (T009) changes rules, not cases.
  Differences go to the maintainer.
- Merging, required-check changes and the `v1.5.0` tag each need the maintainer's decision in this feature.
- Mark each task `[X]` in this file as it is completed.
