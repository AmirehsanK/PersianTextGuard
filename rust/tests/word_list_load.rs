//! Loading word lists from files and readers (G8).

#![allow(clippy::unwrap_used, clippy::expect_used)]

use std::fs::{self, File};
use std::io::{Cursor, ErrorKind};
use std::path::PathBuf;

use persian_text_guard::{WordList, WordListError};

const TEXT: &str = "spam\n[slur]\nword\n~stem\n";

/// A temporary file, removed when dropped.
struct TempFile(PathBuf);

impl TempFile {
    fn new(name: &str, bytes: &[u8]) -> Self {
        let path = std::env::temp_dir().join(format!("ptg-{}-{}-{name}", std::process::id(), line!()));
        fs::write(&path, bytes).unwrap();
        Self(path)
    }
}

impl Drop for TempFile {
    fn drop(&mut self) {
        let _ = fs::remove_file(&self.0);
    }
}

fn with_bom_and_crlf() -> Vec<u8> {
    let mut bytes = b"\xEF\xBB\xBF".to_vec();
    bytes.extend_from_slice(TEXT.replace('\n', "\r\n").as_bytes());
    bytes
}

#[test]
fn files_and_readers_equal_parse_of_the_text() {
    let expected = WordList::parse(TEXT).unwrap();
    let file = TempFile::new("list.txt", &with_bom_and_crlf());
    let path_text = file.0.to_str().unwrap();

    assert_eq!(WordList::load(path_text).unwrap(), expected);
    assert_eq!(WordList::load(&file.0).unwrap(), expected);
    assert_eq!(WordList::load(file.0.clone()).unwrap(), expected);
    assert_eq!(
        WordList::load_reader(File::open(&file.0).unwrap()).unwrap(),
        expected
    );

    let mut open = File::open(&file.0).unwrap();
    assert_eq!(WordList::load_reader(&mut open).unwrap(), expected);

    let bytes = with_bom_and_crlf();
    assert_eq!(WordList::load_reader(&bytes[..]).unwrap(), expected);
    assert_eq!(WordList::load_reader(Cursor::new(bytes)).unwrap(), expected);
    assert_eq!(WordList::load_reader(TEXT.as_bytes()).unwrap(), expected);
}

#[test]
fn a_missing_file_is_io_not_found() {
    let path = std::env::temp_dir().join(format!("ptg-{}-missing.txt", std::process::id()));
    match WordList::load(&path) {
        Err(WordListError::Io(error)) => assert_eq!(error.kind(), ErrorKind::NotFound),
        other => panic!("{other:?}"),
    }
}

#[test]
fn invalid_utf8_reports_where() {
    match WordList::load_reader(&b"spam\n\xFFword\n"[..]) {
        Err(WordListError::InvalidUtf8 { valid_up_to }) => assert_eq!(valid_up_to, 5),
        other => panic!("{other:?}"),
    }

    let file = TempFile::new("invalid.txt", b"\xEF\xBB\xBFab\xC3");
    match WordList::load(&file.0) {
        Err(WordListError::InvalidUtf8 { valid_up_to }) => assert_eq!(valid_up_to, 2),
        other => panic!("{other:?}"),
    }
}

#[test]
fn an_unknown_heading_reports_its_line() {
    match WordList::load_reader(&b"word\n[3]\nother\n"[..]) {
        Err(WordListError::UnknownCategory { line, name }) => assert_eq!((line, name.as_str()), (2, "3")),
        other => panic!("{other:?}"),
    }

    let error = WordList::parse("[3]").unwrap_err();
    assert_eq!(error.to_string(), "Line 1: unknown word category '3'.");
}

#[test]
fn io_errors_keep_their_source() {
    use std::error::Error;

    let error = WordList::load(std::env::temp_dir().join("ptg-surely-missing/list.txt")).unwrap_err();
    assert!(error.source().is_some());
}
