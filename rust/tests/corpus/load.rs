//! Loads the conformance corpus (`conformance/`). Port of `js/test/corpus/load.ts`, which ports the
//! loading part of `dotnet/tests/PersianTextGuard.Conformance/Corpus.cs`.

#![allow(dead_code)]

use std::collections::HashMap;
use std::fs;
use std::path::{Path, PathBuf};

use serde_json::{Map, Value};

/// Every case kind format version 1 defines.
pub const KINDS: [&str; 8] = [
    "ordinary",
    "must-match",
    "robustness",
    "normalization",
    "tokenization",
    "word-list-parsing",
    "category-selection",
    "mask-validation",
];

/// The kinds that run messages through a filter.
pub const MATCHING_KINDS: [&str; 3] = ["ordinary", "must-match", "robustness"];

/// The newest corpus format this runner understands.
pub const SUPPORTED_FORMAT_VERSION: u64 = 1;

/// One case, with the file it came from.
#[derive(Debug)]
pub struct CorpusCase {
    pub id: String,
    pub kind: String,
    pub file: String,
    pub json: Map<String, Value>,
    pub pending: bool,
}

/// The corpus: metadata, configurations by name, the case files read, and every case in file order.
#[derive(Debug)]
pub struct Corpus {
    pub directory: PathBuf,
    pub metadata: Map<String, Value>,
    pub configurations: HashMap<String, Map<String, Value>>,
    pub files: Vec<PathBuf>,
    pub cases: Vec<CorpusCase>,
}

/// The repository root: the nearest directory at or above the crate that has both `VERSION` and
/// `wordlists/`. Never the working directory. `conformance/` is not required here, so a missing corpus is
/// reported as such.
pub fn find_repository_root() -> Result<PathBuf, String> {
    let mut directory = Some(Path::new(env!("CARGO_MANIFEST_DIR")));
    while let Some(current) = directory {
        if current.join("VERSION").is_file() && current.join("wordlists").is_dir() {
            return Ok(current.to_path_buf());
        }
        directory = current.parent();
    }

    Err("Could not find the repository root (a directory with VERSION and wordlists/).".to_owned())
}

fn read_json(path: &Path) -> Result<Value, String> {
    let text = fs::read_to_string(path).map_err(|error| format!("{}: {error}", path.display()))?;
    serde_json::from_str(&text).map_err(|error| format!("{}: {error}", path.display()))
}

/// Reads `corpus.json`, `configurations.json` and every `cases/*.json` in ordinal file-name order. Every
/// error names its file. A `formatVersion` newer than [`SUPPORTED_FORMAT_VERSION`] is refused.
pub fn load_corpus(directory: &Path) -> Result<Corpus, String> {
    if !directory.exists() {
        return Err(format!("Conformance corpus not found: {}", directory.display()));
    }

    let metadata_path = directory.join("corpus.json");
    let Value::Object(metadata) = read_json(&metadata_path)? else {
        return Err(format!("{}: expected a JSON object.", metadata_path.display()));
    };

    match metadata.get("formatVersion").and_then(Value::as_u64) {
        Some(version) if version <= SUPPORTED_FORMAT_VERSION => {}
        Some(version) => {
            return Err(format!(
                "{}: formatVersion {version} is newer than this runner understands ({SUPPORTED_FORMAT_VERSION}).",
                metadata_path.display()
            ));
        }
        None => {
            return Err(format!(
                "{}: no numeric \"formatVersion\".",
                metadata_path.display()
            ));
        }
    }

    let configurations_path = directory.join("configurations.json");
    let Value::Array(configuration_list) = read_json(&configurations_path)? else {
        return Err(format!(
            "{}: expected a JSON array.",
            configurations_path.display()
        ));
    };

    let mut configurations = HashMap::new();
    for configuration in configuration_list {
        if let Value::Object(configuration) = configuration {
            if let Some(name) = configuration.get("name").and_then(Value::as_str) {
                configurations.insert(name.to_owned(), configuration.clone());
            }
        }
    }

    let cases_directory = directory.join("cases");
    let mut names: Vec<String> = match fs::read_dir(&cases_directory) {
        Ok(entries) => entries
            .filter_map(Result::ok)
            .filter_map(|entry| entry.file_name().into_string().ok())
            .filter(|name| name.ends_with(".json"))
            .collect(),
        Err(_) => Vec::new(),
    };
    // Ordinal order, as .NET's StringComparer.Ordinal and the JavaScript runner sort.
    names.sort();

    let mut files = Vec::new();
    let mut cases = Vec::new();

    for name in names {
        let path = cases_directory.join(&name);
        let Value::Array(list) = read_json(&path)? else {
            return Err(format!("{}: expected a JSON array of cases.", path.display()));
        };

        for (index, json) in list.into_iter().enumerate() {
            let Value::Object(json) = json else {
                return Err(format!(
                    "{}: element {index} is not a case object.",
                    path.display()
                ));
            };

            let Some(id) = json.get("id").and_then(Value::as_str).map(str::to_owned) else {
                return Err(format!(
                    "{}: element {index} has no string \"id\".",
                    path.display()
                ));
            };

            let Some(kind) = json.get("kind").and_then(Value::as_str).map(str::to_owned) else {
                return Err(format!("{}: case '{id}' has no string \"kind\".", path.display()));
            };

            if !KINDS.contains(&kind.as_str()) {
                return Err(format!(
                    "{}: case '{id}' has unknown kind '{kind}'.",
                    path.display()
                ));
            }

            let pending = !json.contains_key("expected");
            cases.push(CorpusCase {
                id,
                kind,
                file: name.clone(),
                json,
                pending,
            });
        }

        files.push(path);
    }

    Ok(Corpus {
        directory: directory.to_path_buf(),
        metadata,
        configurations,
        files,
        cases,
    })
}
