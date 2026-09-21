//! Turns Criterion's `estimates.json` files into the README's performance table, and gates one
//! benchmark against a limit for CI (spec 005, research R9).
//!
//!   cargo run --release --bin bench_table                       # the table
//!   cargo run --release --bin bench_table -- --gate CleanShortMessage --limit-us 50
//!
//! Run `cargo bench` first: the means come from `target/criterion/<name>/new/estimates.json`.

use std::path::{Path, PathBuf};
use std::process::Command;

/// The ten operations, in the order the benchmarks run, with the README's descriptive names.
const OPERATIONS: [(&str, &str); 10] = [
    ("CleanShortMessage", "Short clean message (5 words)"),
    ("CleanLongMessage", "Long clean message (60 words)"),
    ("EvasiveMessage", "Message with evasions"),
    ("NormalizeLongMessage", "Normalize a long message"),
    ("BuildFilterFromDefaultList", "Build a filter from the bundled list"),
    ("FindMatchesClean", "`find_matches`, clean short message"),
    ("FindMatchesDirty", "`find_matches`, message with three banned words"),
    ("CensorShortDirty", "`censor`, short message with one banned word"),
    ("CensorLongDirty", "`censor`, 60-word message with three banned words"),
    ("VeryLongMessage", "A 132,000-character message"),
];

fn criterion_directory() -> PathBuf {
    // The binary runs from the bench package, whose target directory holds Criterion's results.
    Path::new(env!("CARGO_MANIFEST_DIR")).join("target").join("criterion")
}

/// The mean of one benchmark, in nanoseconds.
fn mean_nanoseconds(name: &str) -> Result<f64, String> {
    let path = criterion_directory().join(name).join("new").join("estimates.json");
    let text = std::fs::read_to_string(&path)
        .map_err(|error| format!("{}: {error}\nrun `cargo bench` first", path.display()))?;
    let estimates: serde_json::Value =
        serde_json::from_str(&text).map_err(|error| format!("{}: {error}", path.display()))?;
    estimates["mean"]["point_estimate"]
        .as_f64()
        .ok_or_else(|| format!("{}: no mean.point_estimate", path.display()))
}

fn format_mean(nanoseconds: f64) -> String {
    let milliseconds = nanoseconds / 1_000_000.0;
    if milliseconds >= 1.0 {
        return if milliseconds >= 100.0 {
            format!("{milliseconds:.0} ms")
        } else {
            format!("{milliseconds:.1} ms")
        };
    }

    let microseconds = nanoseconds / 1_000.0;
    if microseconds >= 100.0 { format!("{microseconds:.0} µs") } else { format!("{microseconds:.1} µs") }
}

/// A number with thousands separators, as the other ports' tables print it.
fn thousands(value: f64) -> String {
    let digits = format!("{:.0}", value.max(0.0));
    let mut out = String::new();
    for (index, digit) in digits.chars().enumerate() {
        if index > 0 && (digits.len() - index) % 3 == 0 {
            out.push(',');
        }
        out.push(digit);
    }
    out
}

fn rust_version() -> String {
    Command::new(std::env::var("RUSTC").unwrap_or_else(|_| "rustc".to_owned()))
        .arg("--version")
        .output()
        .ok()
        .and_then(|output| String::from_utf8(output.stdout).ok())
        .map_or_else(|| "rustc (unknown version)".to_owned(), |version| version.trim().to_owned())
}

fn cpu_model() -> String {
    if let Ok(name) = std::env::var("PTG_BENCH_CPU") {
        return name;
    }

    if let Ok(info) = std::fs::read_to_string("/proc/cpuinfo") {
        if let Some(model) = info.lines().find_map(|line| line.strip_prefix("model name")) {
            return model.trim_start_matches([' ', ':']).trim().to_owned();
        }
    }

    std::env::var("PROCESSOR_IDENTIFIER").unwrap_or_else(|_| "unknown CPU".to_owned())
}

fn table() -> Result<(), String> {
    println!("{}, {}\n", rust_version(), cpu_model());
    println!("| Operation | Mean | Operations/s |");
    println!("| --- | ---: | ---: |");
    for (name, description) in OPERATIONS {
        let mean = mean_nanoseconds(name)?;
        let per_second = 1_000_000_000.0 / mean;
        println!("| {description} | {} | {} |", format_mean(mean), thousands(per_second));
    }

    Ok(())
}

fn gate(name: &str, limit_microseconds: f64) -> Result<(), String> {
    let mean = mean_nanoseconds(name)?;
    let microseconds = mean / 1_000.0;
    println!("{name}: mean {} (limit {limit_microseconds} µs)", format_mean(mean));
    if microseconds > limit_microseconds {
        return Err(format!("{name} took {microseconds:.1} µs, over the limit of {limit_microseconds} µs"));
    }

    println!("ok: within the limit");
    Ok(())
}

fn run() -> Result<(), String> {
    let arguments: Vec<String> = std::env::args().skip(1).collect();
    let value = |flag: &str| {
        arguments
            .iter()
            .position(|argument| argument == flag)
            .and_then(|at| arguments.get(at + 1))
            .cloned()
    };

    match (value("--gate"), value("--limit-us")) {
        (Some(name), Some(limit)) => {
            let limit = limit.parse::<f64>().map_err(|error| format!("--limit-us: {error}"))?;
            gate(&name, limit)
        }
        (Some(_), None) => Err("--gate needs --limit-us".to_owned()),
        (None, Some(_)) => Err("--limit-us needs --gate".to_owned()),
        (None, None) => table(),
    }
}

fn main() {
    if let Err(error) = run() {
        eprintln!("{error}");
        std::process::exit(1);
    }
}
