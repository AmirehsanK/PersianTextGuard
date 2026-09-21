//! Corpus values: building inputs, code-point positions as bytes, visible text and exact comparison.
//! Port of `js/test/corpus/values.ts`, with the amended runner rules for Rust (spec 005, FR-017): every
//! Input is built as UTF-16 units and decoded with `String::from_utf16_lossy`, so a lone surrogate becomes
//! U+FFFD and a valid pair is kept; the same applies to text in `expected`; `null` is read as `""` by the
//! callers that need text.

#![allow(dead_code)]

use serde_json::{Map, Value};

/// Whether a value is text: a string or a `{ "build": [...] }` object.
pub fn is_text(node: &Value) -> bool {
    match node {
        Value::String(_) => true,
        Value::Object(object) => object.len() == 1 && object.get("build").is_some_and(Value::is_array),
        _ => false,
    }
}

/// The UTF-16 units an Input builds: a string, `null` (`None`), or `{ "build": [ parts… ] }`.
pub fn build_units(input: &Value) -> Result<Option<Vec<u16>>, String> {
    match input {
        Value::Null => Ok(None),
        Value::String(text) => Ok(Some(text.encode_utf16().collect())),
        Value::Object(object) if is_text(input) => {
            let mut units = Vec::new();
            for part in object
                .get("build")
                .and_then(Value::as_array)
                .into_iter()
                .flatten()
            {
                let Value::Object(part) = part else {
                    return Err(format!("A build part must be an object: {}", display(part)));
                };

                if let (1, Some(text)) = (part.len(), part.get("text").and_then(Value::as_str)) {
                    units.extend(text.encode_utf16());
                } else if let (2, Some(repeat), Some(times)) = (
                    part.len(),
                    part.get("repeat").and_then(Value::as_str),
                    part.get("times").and_then(Value::as_u64),
                ) {
                    if times < 1 {
                        return Err(format!(
                            "Not a build part: {}",
                            display(&Value::Object(part.clone()))
                        ));
                    }
                    for _ in 0..times {
                        units.extend(repeat.encode_utf16());
                    }
                } else if let (1, Some(hex)) = (part.len(), part.get("utf16").and_then(Value::as_str)) {
                    // One UTF-16 unit, which may be a lone surrogate.
                    let unit = (hex.len() == 4)
                        .then(|| u16::from_str_radix(hex, 16).ok())
                        .flatten()
                        .ok_or_else(|| {
                            format!("Not a build part: {}", display(&Value::Object(part.clone())))
                        })?;
                    units.push(unit);
                } else {
                    return Err(format!(
                        "Not a build part: {}",
                        display(&Value::Object(part.clone()))
                    ));
                }
            }
            Ok(Some(units))
        }
        _ => Err(format!("Not an Input: {}", display(input))),
    }
}

/// An Input built as Rust text: its units decoded with `String::from_utf16_lossy`, so each lone surrogate
/// becomes U+FFFD (the amended runner rule). `None` for `null`.
pub fn build_input(input: &Value) -> Result<Option<String>, String> {
    Ok(build_units(input)?.map(|units| String::from_utf16_lossy(&units)))
}

/// An Input built as text, `null` and unbuildable values read as `""`.
pub fn build_text(input: &Value) -> String {
    build_input(input).ok().flatten().unwrap_or_default()
}

/// A code-point region of `text` as a byte start and length. A replaced lone surrogate is one code
/// point, U+FFFD.
pub fn code_points_to_bytes(text: &str, start: usize, length: usize) -> (usize, usize) {
    let offset = |code_points: usize| {
        text.char_indices()
            .nth(code_points)
            .map_or(text.len(), |(offset, _)| offset)
    };
    let begin = offset(start);
    (begin, offset(start + length) - begin)
}

/// Whether a character is shown as an escape. Display only: the Cf ranges are the ones the corpus uses
/// and their neighbours, not a full table.
fn is_invisible(c: char) -> bool {
    let code = u32::from(c);
    let format = matches!(
        code,
        0x00AD | 0x0600..=0x0605 | 0x061C | 0x06DD | 0x070F | 0x180E | 0x200B..=0x200F | 0x202A..=0x202E
            | 0x2060..=0x2064 | 0x2066..=0x206F | 0xFEFF | 0xFFF9..=0xFFFB | 0x1BCA0..=0x1BCA3
            | 0x1D173..=0x1D17A | 0xE0001 | 0xE0020..=0xE007F
    );
    let control = code <= 0x1F || (0x7F..=0x9F).contains(&code);
    let separator = code == 0x2028 || code == 0x2029;
    let white_space = matches!(
        code,
        0x09..=0x0D | 0x85 | 0xA0 | 0x1680 | 0x2000..=0x200A | 0x202F | 0x205F | 0x3000
    );
    let noncharacter = (0xFDD0..=0xFDEF).contains(&code) || (code & 0xFFFE) == 0xFFFE;
    format || control || separator || white_space || noncharacter || code == 0xFFFD
}

/// The text with invisible characters shown as `\u{XXXX}`: Cf, Cc, Zl, Zp, whitespace other than U+0020,
/// U+FFFD (a replaced lone surrogate) and noncharacters.
pub fn show_invisible(text: &str) -> String {
    let mut result = String::with_capacity(text.len());
    for c in text.chars() {
        if is_invisible(c) {
            result.push_str(&format!("\\u{{{:04X}}}", u32::from(c)));
        } else {
            result.push(c);
        }
    }
    result
}

/// Text for messages, shortened when long.
pub fn show_text(text: Option<&str>) -> String {
    const LIMIT: usize = 200;
    match text {
        None => "null".to_owned(),
        Some(text) if text.chars().count() <= LIMIT => format!("\"{}\"", show_invisible(text)),
        Some(text) => {
            let head: String = text.chars().take(LIMIT).collect();
            format!("\"{}…\" ({} bytes)", show_invisible(&head), text.len())
        }
    }
}

fn visible(node: &Value) -> Value {
    match node {
        Value::String(text) => Value::String(show_invisible(text)),
        Value::Array(items) => Value::Array(items.iter().map(visible).collect()),
        Value::Object(object) => Value::Object(
            object
                .iter()
                .map(|(key, value)| (key.clone(), visible(value)))
                .collect(),
        ),
        other => other.clone(),
    }
}

/// A value on one line, for messages.
pub fn display(node: &Value) -> String {
    if is_text(node) {
        return match build_input(node) {
            Ok(text) => show_text(text.as_deref()),
            Err(_) => visible(node).to_string(),
        };
    }

    visible(node).to_string()
}

/// A difference between an expected and an actual result.
#[derive(Debug)]
pub struct Difference {
    pub path: String,
    pub expected: String,
    pub actual: String,
}

/// Every difference between an expected and an actual result, field by field. Text compares by its built
/// value, so a string and an equivalent build object are equal.
pub fn compare(expected: &Value, actual: &Value) -> Vec<Difference> {
    let mut differences = Vec::new();
    walk(Some(expected), Some(actual), "expected", &mut differences);
    differences
}

fn show(node: Option<&Value>) -> String {
    node.map_or_else(|| "(missing)".to_owned(), display)
}

fn walk(expected: Option<&Value>, actual: Option<&Value>, path: &str, out: &mut Vec<Difference>) {
    let (Some(expected), Some(actual)) = (expected, actual) else {
        if expected != actual {
            out.push(Difference {
                path: path.to_owned(),
                expected: show(expected),
                actual: show(actual),
            });
        }
        return;
    };

    if expected.is_null() || actual.is_null() {
        if expected != actual {
            out.push(Difference {
                path: path.to_owned(),
                expected: display(expected),
                actual: display(actual),
            });
        }
        return;
    }

    if is_text(expected) && is_text(actual) {
        let (e, a) = (
            build_input(expected).ok().flatten(),
            build_input(actual).ok().flatten(),
        );
        if e != a {
            out.push(Difference {
                path: path.to_owned(),
                expected: show_text(e.as_deref()),
                actual: show_text(a.as_deref()),
            });
        }
        return;
    }

    match (expected, actual) {
        (Value::Object(e), Value::Object(a)) => walk_objects(e, a, path, out),
        (Value::Array(e), Value::Array(a)) => {
            for i in 0..e.len().max(a.len()) {
                walk(e.get(i), a.get(i), &format!("{path}[{i}]"), out);
            }
        }
        _ if expected != actual => {
            out.push(Difference {
                path: path.to_owned(),
                expected: display(expected),
                actual: display(actual),
            });
        }
        _ => {}
    }
}

fn walk_objects(
    expected: &Map<String, Value>,
    actual: &Map<String, Value>,
    path: &str,
    out: &mut Vec<Difference>,
) {
    for (key, value) in expected {
        walk(Some(value), actual.get(key), &format!("{path}.{key}"), out);
    }

    for (key, value) in actual {
        if !expected.contains_key(key) {
            walk(None, Some(value), &format!("{path}.{key}"), out);
        }
    }
}
