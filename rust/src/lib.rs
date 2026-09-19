#![doc = include_str!("../README.md")]
#![forbid(unsafe_code)]

// Used by word_list.rs once the port lands.
#[allow(dead_code)]
mod wordlists {
    include!(concat!(env!("OUT_DIR"), "/wordlists.rs"));
}
