# persian-text-guard

Persian text normalization and evasion-resistant profanity filtering for Rust.

```rust
use persian_text_guard::{ProfanityFilter, WordList};

// Build once and share it: a filter never changes after it is built.
let filter = ProfanityFilter::with_defaults(WordList::persian_default());

assert!(filter.contains_profanity("ک.ی.ر"));
assert!(!filter.contains_profanity("سلام، سفارشم کی میرسه؟"));
assert_eq!(filter.censor("kir and motherfucker"), "**** and ****");
```
