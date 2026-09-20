//! Runs a corpus case against the public API and returns the result in the kind's `expected` shape.
//! Port of `js/test/corpus/evaluate.ts`, the counterpart of `Corpus.Evaluate` and `CheckKindRules` in
//! `dotnet/tests/PersianTextGuard.Conformance/Corpus.cs`.
//!
//! Positions: the port reports bytes of the built input; the **expected** positions, recorded in code
//! points, are converted to bytes of the same built input before comparing ([`expected_in_bytes`]), as
//! the amended runner obligation 5 allows.

#![allow(dead_code)]

use std::collections::HashMap;

use persian_text_guard::{
    BannedWord, Normalization, NormalizationStep, ProfanityFilter, ProfanityFilterOptions, ProfanityMatch,
    WordCategory, WordList, WordListError, WordMatchMode, normalize, to_ascii_digits, to_persian_digits,
    tokenize,
};
use serde_json::{Map, Value, json};

use super::load::{Corpus, CorpusCase, MATCHING_KINDS};
use super::values::{build_input, build_text, build_units, code_points_to_bytes};

/// One filter per configuration, built once; a configuration that cannot be built keeps its error.
pub fn build_filters(corpus: &Corpus) -> HashMap<String, Result<ProfanityFilter, String>> {
    corpus
        .configurations
        .iter()
        .map(|(name, configuration)| (name.clone(), build_filter(configuration)))
        .collect()
}

fn build_filter(configuration: &Map<String, Value>) -> Result<ProfanityFilter, String> {
    let Some(Value::Object(word_lists)) = configuration.get("wordLists") else {
        return Err(format!(
            "Configuration has no \"wordLists\" object: {}",
            Value::Object(configuration.clone())
        ));
    };

    let words: Vec<BannedWord> = if let Some(bundled) = word_lists.get("bundled") {
        selection(bundled)?
    } else if let Some(Value::Array(entries)) = word_lists.get("entries") {
        entries.iter().map(entry_from_json).collect::<Result<_, _>>()?
    } else {
        return Err(format!(
            "\"wordLists\" needs \"bundled\" or \"entries\": {}",
            Value::Object(word_lists.clone())
        ));
    };

    let mut options = ProfanityFilterOptions::default();
    if let Some(Value::Object(given)) = configuration.get("options") {
        if let Some(on) = given.get("squeezeRepeatedLetters").and_then(Value::as_bool) {
            options = options.squeeze_repeated_letters(on);
        }
        if let Some(on) = given.get("foldLookalikeCharacters").and_then(Value::as_bool) {
            options = options.fold_lookalike_characters(on);
        }
        if let Some(on) = given.get("joinSpacedLetters").and_then(Value::as_bool) {
            options = options.join_spaced_letters(on);
        }
    }

    Ok(ProfanityFilter::new(words, options))
}

fn entry_from_json(node: &Value) -> Result<BannedWord, String> {
    let text = node
        .get("text")
        .and_then(Value::as_str)
        .ok_or_else(|| format!("An entry needs \"text\": {node}"))?;
    let mut word = BannedWord::new(text);
    if let Some(mode) = node.get("mode").and_then(Value::as_str) {
        word = word.with_mode(mode.parse::<WordMatchMode>().map_err(|error| error.to_string())?);
    }
    if let Some(category) = node.get("category").and_then(Value::as_str) {
        word = word.with_category(
            category
                .parse::<WordCategory>()
                .map_err(|error| error.to_string())?,
        );
    }
    Ok(word)
}

/// A bundled selection: `"default"`, `"all"` or an array of category names.
fn selection(value: &Value) -> Result<Vec<BannedWord>, String> {
    match value {
        Value::String(name) if name == "default" => Ok(WordList::persian_default().to_vec()),
        Value::String(name) if name == "all" => Ok(WordList::all().to_vec()),
        Value::Array(names) => {
            let categories = names
                .iter()
                .map(|name| {
                    name.as_str()
                        .ok_or_else(|| format!("Not a category name: {name}"))?
                        .parse::<WordCategory>()
                        .map_err(|error| error.to_string())
                })
                .collect::<Result<Vec<_>, _>>()?;
            Ok(WordList::bundled(&categories).into_iter().cloned().collect())
        }
        _ => Err(format!("Not a selection: {value}")),
    }
}

fn entry(word: &BannedWord) -> Value {
    json!({ "text": word.text, "mode": word.mode.name(), "category": word.category.name() })
}

fn match_node(found: &ProfanityMatch<'_>) -> Value {
    json!({
        "entry": entry(found.word),
        "evasion": found.evasion.iter().map(|kind| kind.name()).collect::<Vec<_>>(),
        "start": found.start,
        "length": found.len,
    })
}

/// A matching case's `expected` with every match position converted from code points to bytes of the
/// built input.
pub fn expected_in_bytes(corpus_case: &CorpusCase) -> Value {
    let mut expected = corpus_case.json.get("expected").cloned().unwrap_or(Value::Null);
    if !MATCHING_KINDS.contains(&corpus_case.kind.as_str()) {
        return expected;
    }

    let text = build_text(corpus_case.json.get("input").unwrap_or(&Value::Null));
    let convert = |node: &mut Value| {
        let (Some(start), Some(length)) = (
            node.get("start").and_then(Value::as_u64),
            node.get("length").and_then(Value::as_u64),
        ) else {
            return;
        };
        let (start, length) = code_points_to_bytes(&text, start as usize, length as usize);
        node["start"] = json!(start);
        node["length"] = json!(length);
    };

    if let Some(first) = expected.get_mut("firstMatch").filter(|node| node.is_object()) {
        convert(first);
    }
    if let Some(Value::Array(matches)) = expected.get_mut("matches") {
        matches.iter_mut().for_each(convert);
    }

    expected
}

/// Runs a case and returns the result in its kind's `expected` shape.
pub fn evaluate(
    corpus_case: &CorpusCase,
    filters: &HashMap<String, Result<ProfanityFilter, String>>,
) -> Result<Value, String> {
    let json = &corpus_case.json;
    let field = |name: &str| json.get(name).unwrap_or(&Value::Null);
    let filter_named = |name: &str| match filters.get(name) {
        Some(Ok(filter)) => Ok(filter),
        Some(Err(error)) => Err(format!("Configuration '{name}' could not be built: {error}")),
        None => Err(format!("No configuration is named '{name}'.")),
    };

    match corpus_case.kind.as_str() {
        "ordinary" | "must-match" | "robustness" => {
            let name = field("configuration")
                .as_str()
                .ok_or("\"configuration\" is missing.")?;
            let filter = filter_named(name)?;
            // The amended rule: a null Input is read as the empty string.
            let text = build_input(field("input"))?.unwrap_or_default();

            let mut result = json!({
                "containsProfanity": filter.contains_profanity(&text),
                "firstMatch": filter.find_match(&text).map_or(Value::Null, |found| match_node(&found)),
                "matches": filter.find_matches(&text).iter().map(match_node).collect::<Vec<_>>(),
                "censored": filter.censor(&text),
            });

            if let Some(Value::Array(masks)) = json.get("masks") {
                let mut censored_with = Map::new();
                for mask in masks {
                    let name = mask
                        .as_str()
                        .ok_or_else(|| format!("A mask must be a string: {mask}"))?;
                    let mut chars = name.chars();
                    let (Some(c), None) = (chars.next(), chars.next()) else {
                        return Err(format!("A mask must be exactly one character: {mask}"));
                    };
                    let censored = filter.censor_with(&text, c).map_err(|error| error.to_string())?;
                    censored_with.insert(name.to_owned(), Value::String(censored));
                }
                result["censoredWith"] = Value::Object(censored_with);
            }

            Ok(result)
        }

        "normalization" => {
            let text = build_input(field("input"))?.unwrap_or_default();
            let output = match field("steps") {
                Value::String(name) if name == "toPersianDigits" => to_persian_digits(&text),
                Value::String(name) if name == "toAsciiDigits" => to_ascii_digits(&text),
                Value::String(preset) => normalize(
                    &text,
                    preset
                        .parse::<Normalization>()
                        .map_err(|error| error.to_string())?,
                ),
                Value::Array(names) => {
                    let steps = names
                        .iter()
                        .map(|name| {
                            name.as_str()
                                .ok_or_else(|| format!("Not a step name: {name}"))?
                                .parse::<NormalizationStep>()
                                .map_err(|error| error.to_string())
                        })
                        .collect::<Result<Normalization, _>>()?;
                    normalize(&text, steps)
                }
                other => return Err(format!("Not a normalization: {other}")),
            };
            Ok(json!({ "output": output }))
        }

        "tokenization" => {
            let text = build_input(field("input"))?.unwrap_or_default();
            Ok(json!({ "tokens": tokenize(&text) }))
        }

        "word-list-parsing" => {
            let text = build_input(field("text"))?.ok_or("\"text\" is missing.")?;
            match WordList::parse(&text) {
                Ok(words) => Ok(json!({ "entries": words.iter().map(entry).collect::<Vec<_>>() })),
                Err(WordListError::UnknownCategory { line, .. }) => {
                    Ok(json!({ "error": { "kind": "unknown-category", "line": line } }))
                }
                Err(error) => Err(error.to_string()),
            }
        }

        "category-selection" => {
            let chosen = json.get("selection").ok_or("\"selection\" is missing.")?;
            let words = selection(chosen)?;
            let is_default = chosen.as_str() == Some("default");

            let mut rules = Vec::new();
            let allowed: Option<Vec<WordCategory>> = chosen.as_array().map(|names| {
                names
                    .iter()
                    .filter_map(Value::as_str)
                    .filter_map(|name| name.parse().ok())
                    .collect()
            });
            let in_selection = words.iter().all(|word| match &allowed {
                None => word.category != WordCategory::Uncategorized,
                Some(allowed) => allowed.contains(&word.category),
            });
            if in_selection {
                rules.push("categoriesInSelection");
            }
            if in_bundled_order(&words) {
                rules.push("bundledOrder");
            }
            if is_default && words.iter().all(|word| word.category != WordCategory::Mild) {
                rules.push("noMild");
            }

            Ok(json!({
                "count": words.len(),
                "first": words.iter().take(5).map(entry).collect::<Vec<_>>(),
                "last": words[words.len().saturating_sub(5)..].iter().map(entry).collect::<Vec<_>>(),
                "rules": rules,
            }))
        }

        "mask-validation" => {
            let units = build_units(field("mask"))?.ok_or("\"mask\" is missing.")?;
            let mut decoded = char::decode_utf16(units.iter().copied());
            // The amended rule: a mask that does not build into one char (a lone surrogate) is refused.
            let accepted = match (decoded.next(), decoded.next()) {
                (Some(Ok(mask)), None) => filter_named("default")?.censor_with("kir", mask).is_ok(),
                _ => false,
            };
            Ok(json!({ "accepted": accepted }))
        }

        other => Err(format!("unknown kind '{other}'")),
    }
}

fn in_bundled_order(words: &[BannedWord]) -> bool {
    let all = WordList::all();
    let mut position = 0;
    for word in words {
        while position < all.len() && all[position] != *word {
            position += 1;
        }

        if position == all.len() {
            return false;
        }

        position += 1;
    }

    true
}

/// The kind rules from the data model that a matching case's recorded `expected` breaks, in .NET's
/// wording.
pub fn check_kind_rules(corpus_case: &CorpusCase) -> Vec<String> {
    let mut violations = Vec::new();
    let Some(Value::Object(expected)) = corpus_case.json.get("expected") else {
        return violations;
    };
    if !MATCHING_KINDS.contains(&corpus_case.kind.as_str()) {
        return violations;
    }

    let contains = expected.get("containsProfanity") == Some(&Value::Bool(true));

    if corpus_case.kind == "must-match" && !contains {
        violations.push("must-match requires containsProfanity to be true".to_owned());
    }

    if corpus_case.kind == "ordinary" {
        if contains {
            violations.push("ordinary requires containsProfanity to be false".to_owned());
        }

        if !matches!(expected.get("firstMatch"), None | Some(Value::Null)) {
            violations.push("ordinary requires firstMatch to be null".to_owned());
        }

        if !matches!(expected.get("matches"), Some(Value::Array(matches)) if matches.is_empty()) {
            violations.push("ordinary requires matches to be empty".to_owned());
        }

        let input = build_text(corpus_case.json.get("input").unwrap_or(&Value::Null));
        let censored = expected
            .get("censored")
            .and_then(|node| build_input(node).ok().flatten());
        if censored.as_deref() != Some(input.as_str()) {
            violations.push("ordinary requires censored to equal the input".to_owned());
        }

        if let Some(Value::Object(censored_with)) = expected.get("censoredWith") {
            for (mask, value) in censored_with {
                if build_input(value).ok().flatten().as_deref() != Some(input.as_str()) {
                    violations.push(format!(
                        "ordinary requires censoredWith[\"{mask}\"] to equal the input"
                    ));
                }
            }
        }
    }

    violations
}
