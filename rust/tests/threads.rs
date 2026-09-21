//! One filter shared by many threads gives every thread the answers it gets alone, and the bundled
//! lists are parsed once even when threads race to use them first (SC-008, G6, G7).

#![allow(clippy::unwrap_used, clippy::expect_used)]

#[path = "corpus/evaluate.rs"]
mod evaluate;
#[path = "corpus/load.rs"]
mod load;
#[path = "corpus/values.rs"]
mod values;

use std::process::Command;
use std::sync::Barrier;
use std::thread;

use persian_text_guard::{BannedWord, ProfanityFilter, WordList};

const THREADS: usize = 8;
const FIRST_USE_CHILD: &str = "PTG_FIRST_USE_CHILD";

/// What one filter answers for one input.
type Answer = (bool, Vec<(String, usize, usize)>, String);

fn answer(filter: &ProfanityFilter, text: &str) -> Answer {
    let matches = filter
        .find_matches(text)
        .iter()
        .map(|found| (found.word.text.clone(), found.start, found.len))
        .collect();
    (filter.contains_profanity(text), matches, filter.censor(text))
}

#[test]
fn eight_threads_share_one_filter_per_configuration() {
    let root = load::find_repository_root().unwrap();
    let corpus = load::load_corpus(&root.join("conformance")).unwrap();
    let filters = evaluate::build_filters(&corpus);

    let inputs: Vec<(String, String)> = corpus
        .cases
        .iter()
        .filter(|case| load::MATCHING_KINDS.contains(&case.kind.as_str()))
        .map(|case| {
            let configuration = case.json["configuration"].as_str().unwrap().to_owned();
            let text = values::build_text(case.json.get("input").unwrap_or(&serde_json::Value::Null));
            (configuration, text)
        })
        .collect();

    let single: Vec<Answer> = inputs
        .iter()
        .map(|(configuration, text)| answer(filters[configuration].as_ref().unwrap(), text))
        .collect();

    let barrier = Barrier::new(THREADS);
    thread::scope(|scope| {
        for _ in 0..THREADS {
            scope.spawn(|| {
                barrier.wait();
                for _ in 0..2 {
                    for ((configuration, text), expected) in inputs.iter().zip(&single) {
                        let filter = filters[configuration].as_ref().unwrap();
                        assert_eq!(&answer(filter, text), expected, "{text:?} under {configuration}");
                    }
                }
            });
        }
    });
}

#[test]
fn a_filter_can_be_moved_into_threads_through_an_arc() {
    let filter = std::sync::Arc::new(ProfanityFilter::with_defaults([BannedWord::new("spam")]));
    let handles: Vec<_> = (0..THREADS)
        .map(|_| {
            let filter = std::sync::Arc::clone(&filter);
            thread::spawn(move || filter.contains_profanity("this is spam"))
        })
        .collect();
    assert!(handles.into_iter().all(|handle| handle.join().unwrap()));
}

/// Runs in a fresh process started by [`bundled_lists_are_parsed_once_under_contention`]: 8 threads use
/// the bundled lists for the first time at the same moment, and it prints how many results there were
/// and how many distinct slices they got. Does nothing in an ordinary test run.
#[test]
fn first_use_child() {
    if std::env::var_os(FIRST_USE_CHILD).is_none() {
        return;
    }

    let barrier = Barrier::new(THREADS);
    let pointers: Vec<(usize, usize)> = thread::scope(|scope| {
        let handles: Vec<_> = (0..THREADS)
            .map(|_| {
                scope.spawn(|| {
                    barrier.wait();
                    let all = WordList::all().as_ptr() as usize;
                    let default = WordList::persian_default().as_ptr() as usize;
                    (all, default)
                })
            })
            .collect();
        handles.into_iter().map(|handle| handle.join().unwrap()).collect()
    });

    let mut distinct = pointers.clone();
    distinct.sort_unstable();
    distinct.dedup();
    println!("FIRST-USE {} {}", pointers.len(), distinct.len());
}

#[test]
fn bundled_lists_are_parsed_once_under_contention() {
    let output = Command::new(std::env::current_exe().unwrap())
        .env(FIRST_USE_CHILD, "1")
        .args(["first_use_child", "--exact", "--nocapture", "--test-threads=1"])
        .output()
        .unwrap();
    let stdout = String::from_utf8_lossy(&output.stdout);
    assert!(
        output.status.success(),
        "{stdout}\n{}",
        String::from_utf8_lossy(&output.stderr)
    );

    // The harness may print "test first_use_child ... " on the same line.
    let line = stdout
        .lines()
        .find_map(|line| line.find("FIRST-USE ").map(|at| line[at..].trim_end()))
        .unwrap_or_default();
    assert_eq!(line, format!("FIRST-USE {THREADS} 1"), "{stdout}");
}
