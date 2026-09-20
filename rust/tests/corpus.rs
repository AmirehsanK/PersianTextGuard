//! The conformance corpus runner for the Rust port (spec 005, FR-016, FR-017). It follows the runner
//! obligations in `specs/002-monorepo-conformance-corpus/contracts/corpus-format.md`, as amended by
//! spec 005: one `libtest-mimic` trial per case, named by its id, so `cargo test --test corpus -- <id>`
//! runs one case and a failure never stops the others; plus guard trials for the corpus itself.

#![allow(clippy::unwrap_used, clippy::expect_used)]

#[path = "corpus/evaluate.rs"]
mod evaluate;
#[path = "corpus/load.rs"]
mod load;
#[path = "corpus/values.rs"]
mod values;

use std::collections::HashMap;
use std::fs;
use std::path::Path;
use std::sync::Arc;

use libtest_mimic::{Arguments, Failed, Trial};
use persian_text_guard::ProfanityFilter;

use evaluate::{build_filters, check_kind_rules, evaluate, expected_in_bytes};
use load::{Corpus, CorpusCase, MATCHING_KINDS, SUPPORTED_FORMAT_VERSION, find_repository_root, load_corpus};
use values::{build_input, build_units, compare, display, show_text};

const FILL_IN_HINT: &str = "run dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill";

type Filters = HashMap<String, Result<ProfanityFilter, String>>;

fn main() {
    let arguments = Arguments::from_args();

    let loaded = find_repository_root().and_then(|root| load_corpus(&root.join("conformance")));
    let trials = match loaded {
        Ok(corpus) => trials(Arc::new(corpus)),
        // Loading errors (a missing or unreadable corpus) fail one trial, which names the file.
        Err(error) => vec![Trial::test("corpus-loads", move || Err(error.into()))],
    };

    libtest_mimic::run(&arguments, trials).exit();
}

fn guard(name: &str, check: impl FnOnce() -> Result<(), String> + Send + 'static) -> Trial {
    Trial::test(format!("corpus-{name}"), move || check().map_err(Failed::from)).with_kind("guard")
}

fn trials(corpus: Arc<Corpus>) -> Vec<Trial> {
    let filters: Arc<Filters> = Arc::new(build_filters(&corpus));
    let mut trials = Vec::new();

    let c = Arc::clone(&corpus);
    trials.push(guard("loads", move || {
        if c.files.is_empty() {
            Err("no case files were read".to_owned())
        } else {
            Ok(())
        }
    }));

    let c = Arc::clone(&corpus);
    trials.push(guard(
        "has-a-format-version-this-runner-understands",
        move || match c
            .metadata
            .get("formatVersion")
            .and_then(serde_json::Value::as_u64)
        {
            Some(SUPPORTED_FORMAT_VERSION) => Ok(()),
            other => Err(format!(
                "formatVersion is {other:?}, expected {SUPPORTED_FORMAT_VERSION}"
            )),
        },
    ));

    let c = Arc::clone(&corpus);
    trials.push(guard("refuses-a-newer-format-version", move || {
        refuses_newer_version(&c.directory)
    }));

    let c = Arc::clone(&corpus);
    trials.push(guard("has-at-least-300-cases", move || {
        if c.cases.len() >= 300 {
            Ok(())
        } else {
            Err(format!(
                "The corpus has {} cases; at least 300 are required.",
                c.cases.len()
            ))
        }
    }));

    let c = Arc::clone(&corpus);
    trials.push(guard("has-unique-well-formed-ids", move || {
        unique_well_formed_ids(&c)
    }));

    let c = Arc::clone(&corpus);
    trials.push(guard("has-no-pending-case", move || {
        let pending: Vec<String> = c
            .cases
            .iter()
            .filter(|case| case.pending)
            .map(|case| format!("{} ({})", case.id, case.file))
            .collect();
        if pending.is_empty() {
            Ok(())
        } else {
            Err(format!(
                "{} pending case(s); {FILL_IN_HINT}:\n{}",
                pending.len(),
                pending.join("\n")
            ))
        }
    }));

    let c = Arc::clone(&corpus);
    let f = Arc::clone(&filters);
    trials.push(guard("names-only-configurations-that-exist", move || {
        configurations_exist(&c, &f)
    }));

    let c = Arc::clone(&corpus);
    trials.push(guard("has-no-case-that-is-not-applicable-to-rust", move || {
        no_case_not_applicable(&c)
    }));

    for index in 0..corpus.cases.len() {
        let case = &corpus.cases[index];
        let (name, kind) = (case.id.clone(), case.kind.clone());
        let c = Arc::clone(&corpus);
        let f = Arc::clone(&filters);
        trials.push(
            Trial::test(name, move || check(&c.cases[index], &f).map_err(Failed::from)).with_kind(kind),
        );
    }

    trials
}

fn refuses_newer_version(directory: &Path) -> Result<(), String> {
    let copy = std::env::temp_dir().join(format!("ptg-corpus-newer-{}", std::process::id()));
    let _ = fs::remove_dir_all(&copy);
    fs::create_dir_all(copy.join("cases")).map_err(|error| error.to_string())?;
    fs::copy(
        directory.join("configurations.json"),
        copy.join("configurations.json"),
    )
    .map_err(|error| error.to_string())?;
    let metadata = fs::read_to_string(directory.join("corpus.json")).map_err(|error| error.to_string())?;
    let mut metadata: serde_json::Value =
        serde_json::from_str(&metadata).map_err(|error| error.to_string())?;
    metadata["formatVersion"] = serde_json::json!(SUPPORTED_FORMAT_VERSION + 1);
    fs::write(copy.join("corpus.json"), metadata.to_string()).map_err(|error| error.to_string())?;

    let result = load_corpus(&copy);
    let _ = fs::remove_dir_all(&copy);
    match result {
        Err(error) if error.contains("formatVersion") => Ok(()),
        Err(error) => Err(format!("refused for another reason: {error}")),
        Ok(_) => Err("a corpus with a newer formatVersion was loaded".to_owned()),
    }
}

fn unique_well_formed_ids(corpus: &Corpus) -> Result<(), String> {
    let mut seen: HashMap<&str, Vec<&str>> = HashMap::new();
    for case in &corpus.cases {
        seen.entry(case.id.as_str()).or_default().push(case.file.as_str());
    }

    let mut problems: Vec<String> = seen
        .iter()
        .filter(|(_, files)| files.len() > 1)
        .map(|(id, files)| format!("duplicate id {id} ({})", files.join(", ")))
        .collect();

    let well_formed = |id: &str| {
        !id.is_empty()
            && id.split('-').all(|part| {
                !part.is_empty() && part.bytes().all(|b| b.is_ascii_lowercase() || b.is_ascii_digit())
            })
    };
    problems.extend(
        corpus
            .cases
            .iter()
            .filter(|case| !well_formed(&case.id))
            .map(|case| format!("malformed id {} ({})", case.id, case.file)),
    );

    if problems.is_empty() {
        Ok(())
    } else {
        Err(problems.join("\n"))
    }
}

fn configurations_exist(corpus: &Corpus, filters: &Filters) -> Result<(), String> {
    let mut problems: Vec<String> = corpus
        .cases
        .iter()
        .filter(|case| MATCHING_KINDS.contains(&case.kind.as_str()))
        .filter(|case| {
            !case
                .json
                .get("configuration")
                .and_then(serde_json::Value::as_str)
                .is_some_and(|name| corpus.configurations.contains_key(name))
        })
        .map(|case| {
            format!(
                "{} ({}): {}",
                case.id,
                case.file,
                display(case.json.get("configuration").unwrap_or(&serde_json::Value::Null))
            )
        })
        .collect();

    if !corpus.configurations.contains_key("default") {
        problems.push("no configuration is named 'default'".to_owned());
    }
    for (name, filter) in filters {
        if let Err(error) = filter {
            problems.push(format!("configuration '{name}' cannot be built: {error}"));
        }
    }

    if problems.is_empty() {
        Ok(())
    } else {
        Err(problems.join("\n"))
    }
}

/// Under the amended rules every part a case uses builds in Rust: lone surrogates become U+FFFD, `null`
/// is `""`, and a mask that is not one `char` is refused. So no case is reported as not applicable; a
/// part that fails to build is listed here by id.
fn no_case_not_applicable(corpus: &Corpus) -> Result<(), String> {
    let mut not_applicable = Vec::new();
    for case in &corpus.cases {
        for field in ["input", "text", "mask"] {
            if let Some(node) = case.json.get(field) {
                if let Err(error) = build_units(node) {
                    not_applicable.push(format!("{} ({}): {error}", case.id, case.file));
                }
            }
        }
    }

    if not_applicable.is_empty() {
        Ok(())
    } else {
        Err(format!(
            "{} case(s) not applicable:\n{}",
            not_applicable.len(),
            not_applicable.join("\n")
        ))
    }
}

fn describe_input(case: &CorpusCase) -> String {
    let null = serde_json::Value::Null;
    let text = |field: &str| {
        show_text(
            build_input(case.json.get(field).unwrap_or(&null))
                .ok()
                .flatten()
                .as_deref(),
        )
    };
    match case.kind.as_str() {
        "word-list-parsing" => format!("text {}", text("text")),
        "category-selection" => format!(
            "selection {}",
            display(case.json.get("selection").unwrap_or(&null))
        ),
        "mask-validation" => format!("mask {}", text("mask")),
        _ => format!("input {}", text("input")),
    }
}

fn failure(case: &CorpusCase, problem: &str, lines: &[String]) -> String {
    let mut message = format!(
        "Case '{}' in {}: {problem}\n  {}",
        case.id,
        case.file,
        describe_input(case)
    );
    for line in lines {
        message.push_str("\n  ");
        message.push_str(line);
    }
    message
}

fn check(case: &CorpusCase, filters: &Filters) -> Result<(), String> {
    if case.pending {
        return Err(failure(case, &format!("pending case — {FILL_IN_HINT}"), &[]));
    }

    let violations = check_kind_rules(case);
    if !violations.is_empty() {
        return Err(failure(case, "breaks its kind rule", &violations));
    }

    let actual = evaluate(case, filters).map_err(|error| failure(case, &error, &[]))?;
    let differences = compare(&expected_in_bytes(case), &actual);
    if differences.is_empty() {
        return Ok(());
    }

    let lines: Vec<String> = differences
        .iter()
        .map(|difference| {
            format!(
                "{}: expected {} actual {}",
                difference.path, difference.expected, difference.actual
            )
        })
        .collect();
    Err(failure(
        case,
        &format!("{} field(s) differ", differences.len()),
        &lines,
    ))
}
