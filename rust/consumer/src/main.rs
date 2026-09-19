//! Uses the packaged crate as a user would (spec 005, quickstart §4). `scripts/check-package.sh` points
//! the dependency at the unpacked `.crate`, never at the source tree.

use persian_text_guard::{ProfanityFilter, WordList};

fn main() {
    let filter = ProfanityFilter::with_defaults(WordList::persian_default());

    assert!(filter.contains_profanity("ک.ی.ر"));
    assert!(!filter.contains_profanity("سلام، سفارشم کی میرسه؟"));
    assert_eq!(filter.censor("kir and motherfucker"), "**** and ****");

    println!("ok");
}
