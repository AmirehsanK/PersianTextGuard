//! Embeds the bundled word lists and checks the crate's version (spec 005, research R5).
//!
//! The lists live once, in the repository's `wordlists/` directory, shared by every port. Building in
//! the repository reads them from there; a crate downloaded from crates.io carries its own copy in
//! `wordlists/`, made by `scripts/prepare-package.sh`. The lists are copied into `OUT_DIR` with `\r\n`
//! normalized to `\n`, in the order .NET embeds them, and `src/lib.rs` includes them with `include_str!`.
//!
//! A manifest's version must be a literal, so `Cargo.toml` repeats the repository's `VERSION`. When
//! `../VERSION` exists (building in the repository), the build fails if the two differ, so a stale
//! version can never reach a test run or a release. `scripts/set-version.sh` updates the manifest.

use std::env;
use std::fs;
use std::path::{Path, PathBuf};

/// The lists in .NET's embedding order.
const LISTS: [&str; 3] = ["persian.txt", "finglish.txt", "english.txt"];

fn main() {
    let manifest_dir = PathBuf::from(env::var("CARGO_MANIFEST_DIR").unwrap_or_else(|_| ".".into()));
    let out_dir = PathBuf::from(env::var("OUT_DIR").unwrap_or_else(|_| panic!("OUT_DIR is not set")));

    println!("cargo:rerun-if-changed=build.rs");

    let repository_version = manifest_dir.join("../VERSION");
    println!("cargo:rerun-if-changed={}", repository_version.display());
    if repository_version.is_file() {
        let version = fs::read_to_string(&repository_version)
            .unwrap_or_else(|error| panic!("cannot read {}: {error}", repository_version.display()));
        // The version file is ASCII; the Unicode rules of research R1 are for message text.
        #[allow(clippy::disallowed_methods)]
        let version = version.trim();
        let manifest_version = env::var("CARGO_PKG_VERSION").unwrap_or_default();
        if version != manifest_version {
            panic!(
                "VERSION says {version} but Cargo.toml says {manifest_version}; run scripts/set-version.sh"
            );
        }
    }

    let directory = word_list_directory(&manifest_dir);
    let mut source =
        String::from("/// The bundled word lists, in .NET's order: Persian, Finglish, English.\n");
    source.push_str("pub(crate) const BUNDLED_WORD_LISTS: [&str; 3] = [\n");

    for name in LISTS {
        let path = directory.join(name);
        println!("cargo:rerun-if-changed={}", path.display());
        let text = fs::read_to_string(&path)
            .unwrap_or_else(|error| panic!("cannot read {}: {error}", path.display()));
        let copy = out_dir.join(name);
        fs::write(&copy, text.replace("\r\n", "\n"))
            .unwrap_or_else(|error| panic!("cannot write {}: {error}", copy.display()));
        source.push_str(&format!(
            "    include_str!(concat!(env!(\"OUT_DIR\"), \"/{name}\")),\n"
        ));
    }

    source.push_str("];\n");
    let generated = out_dir.join("wordlists.rs");
    fs::write(&generated, source)
        .unwrap_or_else(|error| panic!("cannot write {}: {error}", generated.display()));
}

/// `../wordlists/` in the repository, otherwise the packaged copy in `wordlists/`.
fn word_list_directory(manifest_dir: &Path) -> PathBuf {
    let repository = manifest_dir.join("../wordlists");
    if manifest_dir.join("../VERSION").is_file() && repository.join("persian.txt").is_file() {
        return repository;
    }

    let packaged = manifest_dir.join("wordlists");
    if packaged.join("persian.txt").is_file() {
        return packaged;
    }

    panic!(
        "word lists not found: looked in {} (the repository) and {} (the packaged copy); \
         run scripts/prepare-package.sh before packaging",
        repository.display(),
        packaged.display()
    );
}
