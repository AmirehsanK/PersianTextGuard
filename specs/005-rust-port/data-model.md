# Data Model: Rust Port Published to crates.io

**Feature**: [spec.md](spec.md) | **Research**: [research.md](research.md) | **API**: [contracts/public-api.md](contracts/public-api.md)

The entities are the same as in every port (spec Key Entities). This document states their Rust shape,
validation rules and invariants. The corpus names (`wholeWord`, `lookalikeCharacters`, …) are what
`FromStr` accepts and `Display` prints.

## Enumerations

| Enum | Variants → names | Notes |
| --- | --- | --- |
| `WordMatchMode` | `WholeWord` → `"wholeWord"`, `Anywhere` → `"anywhere"` | Default `WholeWord`. |
| `WordCategory` | `Uncategorized`, `Profanity`, `Sexual`, `Insult`, `Slur`, `Harassment`, `Mild` → lower-case names | .NET's declaration order; default `Uncategorized`, which never appears in bundled lists. |
| `EvasionKind` | `RepeatedLetters`, `LookalikeCharacters`, `SplitWord` → `"repeatedLetters"`, … | The iteration order of `EvasionSet`, as the corpus records. |
| `NormalizationStep` | the ten steps → lowerCamelCase names | Steps run in declaration order whatever order a set is built in. |

All four are `Copy`, `#[non_exhaustive]`, and parse case-sensitively from their names; an unknown name is
`ParseNameError`, whose message lists the valid names.

## `EvasionSet` and `Normalization`

Small `Copy` bit sets. `EvasionSet` iterates in `EvasionKind` order and is empty when the word was found as
written. `Normalization` has the constants `NONE`, `STANDARD` and `COMPARISON` (the .NET flag values:
`STANDARD` = 299, `COMPARISON` = 1023, as bits) and parses `"comparison"`, `"standard"` and `"none"`.

## `BannedWord`

| Field | Type | Default | Rule |
| --- | --- | --- | --- |
| `text` | `String` | required | Any text; blank entries are ignored by the filter, not rejected, as in .NET. |
| `mode` | `WordMatchMode` | `WholeWord` | |
| `category` | `WordCategory` | `Uncategorized` | |

`Clone`, `Eq`, `Hash` over all three fields. Public fields: an entry is plain data. A filter copies the
entries it is given, so changing a caller's entries afterwards cannot affect it.

## `ProfanityFilterOptions`

| Field | Type | Default |
| --- | --- | --- |
| `squeeze_repeated_letters` | `bool` | `true` |
| `fold_lookalike_characters` | `bool` | `true` |
| `join_spaced_letters` | `bool` | `true` |

`#[non_exhaustive]`: built with `Default` and the three setters, never with a struct literal.

## `ProfanityFilter`

| Member | Notes |
| --- | --- |
| `new(words, options)`, `with_defaults(words)` | `words`: anything iterable whose items borrow as `BannedWord`. Read once; each entry cloned into the filter. Cannot fail. |
| `count()` | Distinct entries after normalization and de-duplication by (mode, normalized text), as .NET's `Count`. |
| `contains_profanity`, `find_match`, `find_matches` | On `&str`; the byte versions on `&[u8]`. |
| `censor`, `censor_with` | Each region becomes four mask characters; the mask is checked first. The byte versions splice the caller's bytes. |

**State after construction**: the entries (`Box<[BannedWord]>`), a whole-word lookup `HashMap` from
normalized token (UTF-16 units) to entry index and list order, phrase lists by first token, anywhere keys,
maskable keys, and the options. Nothing is mutated after `new`; there is no interior mutability, so the
filter is `Send + Sync` by construction.

## `ProfanityMatch<'f>`

| Field | Type | Rule |
| --- | --- | --- |
| `word` | `&'f BannedWord` | The entry as given, borrowed from the filter. |
| `evasion` | `EvasionSet` | Empty when there was no evasion. |
| `start` | `usize` | Bytes into the message (the `&str`, or the `&[u8]` for the byte versions). A character boundary. |
| `len` | `usize` | Bytes; `≥ 1`; `start + len ≤` message length; `range()` = `start..start + len`. |

## Positions

| Stage | Unit |
| --- | --- |
| Inside the matcher | UTF-16 units of the view (research R2) |
| `ProfanityMatch` from the `&str` API | bytes of the caller's string |
| `ProfanityMatch` from the byte API | bytes of the caller's slice, invalid sequences included (research R3) |
| Corpus | code points; the runner converts to bytes before comparing |

## Normalization functions

| Function | Input | Output |
| --- | --- | --- |
| `normalize(text, steps)` | `&str`, `Normalization` | `String` |
| `tokenize(text)` | `&str` | `Vec<&str>`, slices of `text`, in order |
| `to_persian_digits(text)` / `to_ascii_digits(text)` | `&str` | `String` |

## Word lists: `WordList`

| Function | Returns | Rule |
| --- | --- | --- |
| `all()` | `&'static [BannedWord]` | Every bundled entry, `Mild` included, in list order (Persian, Finglish, English). Parsed once through `LazyLock`; the same slice every call. |
| `persian_default()` | `&'static [BannedWord]` | `all()` without `Mild`, also parsed once. |
| `bundled(categories)` | `Vec<&'static BannedWord>` | Entries in the given categories, in list order. |
| `parse(text)` | `Result<Vec<BannedWord>, WordListError>` | The shared format (spec 002): `[name]` headings by name only, case-insensitive, spaces allowed; `~` for anywhere; `#` comments; blank lines and `\r\n` fine. |
| `load(path)`, `load_reader(reader)` | the same | Read to the end as bytes, a leading UTF-8 BOM dropped, decoded as UTF-8, then `parse`. The reader is borrowed or moved by the caller's choice (`&mut File` works). |

## Errors

| Error | From | When |
| --- | --- | --- |
| `InvalidMask { mask }` | `censor_with`, `censor_bytes_with` | The mask is a letter, digit, whitespace, control character, or above U+FFFF. Checked before the text. |
| `ParseNameError` | `FromStr` on the enums and `Normalization` | An unknown name. |
| `WordListError::UnknownCategory { line, name }` | `parse`, `load`, `load_reader` | An unknown section heading; `line` is 1-based. |
| `WordListError::Io(io::Error)` | `load`, `load_reader` | The file cannot be opened or read; the `io::Error` keeps its kind. |
| `WordListError::InvalidUtf8 { valid_up_to }` | `load`, `load_reader` | The input is not UTF-8. |

Nothing else fails. No function panics for any input (G1).

## Crate

| Artifact | Name | Contents |
| --- | --- | --- |
| crates.io crate | `persian-text-guard-<v>.crate` | the files in [contracts/package-and-release.md](contracts/package-and-release.md) → "Package contents" |
| docs.rs | `persian_text_guard` | rustdoc of every public item, with the README as the crate page |

`<v>` is `VERSION` exactly; SemVer accepts its prerelease form (`1.5.0-dev.2`) as it is.
