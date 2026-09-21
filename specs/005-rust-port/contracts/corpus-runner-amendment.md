# Contract: Amendment to the Corpus Runner Obligations

**Feature**: [../spec.md](../spec.md) (FR-016, FR-017) | **Research**: [../research.md](../research.md) R4 | **Amends**: [002 corpus-format contract](../../002-monorepo-conformance-corpus/contracts/corpus-format.md), "Runner obligations", and [`conformance/README.md`](../../../conformance/README.md)

## Why

A Rust string is valid UTF-8, so it cannot hold a lone surrogate, and Rust has no missing value for a
string. Under the current rules, the Rust runner would report nine cases as not applicable: four inputs
and one mask with a lone surrogate, and four `null` inputs. The spec requires 0 (FR-017), and research R4
shows both readings below give .NET's own results.

## Change

Runner obligation 3 becomes:

> 3. Build each Input as described. A port whose strings cannot hold a lone surrogate builds the Input as
>    UTF-16 units and replaces each lone surrogate with U+FFFD, in the Input and in every text value of
>    `expected`; a mask that does not build into one character of the port counts as refused. A port with
>    no missing string value reads a `null` Input as the empty string. Any other part the port's language
>    cannot represent makes the case **not applicable**, reported by id: listed in the run output, never
>    silently skipped.

Obligation 5 gains one sentence:

> A lone surrogate replaced under obligation 3 still counts as one code point.

The corpus format version stays 1: no case, field or kind changes, and the .NET, JavaScript and Python
runners, whose strings hold lone surrogates and which have a missing value, are unaffected.

## Why these readings are faithful

- **U+FFFD**: .NET replaces every lone surrogate with U+FFFD before it normalizes (004 R1), and neither
  character is a word character, so matching is unchanged. Measured on all four such corpus inputs:
  identical decisions, matches and positions, and censored output that differs only in the replaced
  character (research R4). A Rust service receives exactly this text when it decodes such input lossily.
- **Mask**: the one case, `mask-lone-high-surrogate`, records `accepted: false`. A Rust mask is a `char`,
  and `char` cannot hold a surrogate, so the refusal happens in the type system.
- **`null`**: the four cases record exactly the empty string's results.
