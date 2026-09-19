"""``WordList.load``: paths and open files, byte order marks and file errors (P9, spec Clarifications)."""

from pathlib import Path

import pytest

from persian_text_guard import WordList, WordListFormatError

TEXT = "# a comment\r\n[insult]\r\nکسکش\r\n~fuck\r\n\r\n[slur]\r\nword\r\n"
BOM = b"\xef\xbb\xbf"


@pytest.fixture
def word_file(tmp_path: Path) -> Path:
    path = tmp_path / "words.txt"
    path.write_bytes(BOM + TEXT.encode("utf-8"))
    return path


def test_a_path_as_str_and_as_path_equal_parse(word_file: Path) -> None:
    expected = WordList.parse(TEXT)
    assert len(expected) == 3
    assert WordList.load(str(word_file)) == expected
    assert WordList.load(word_file) == expected


def test_an_open_binary_file_equals_parse_and_stays_open(word_file: Path) -> None:
    with word_file.open("rb") as file:
        assert WordList.load(file) == WordList.parse(TEXT)
        assert not file.closed


def test_an_open_text_file_equals_parse_and_stays_open(word_file: Path) -> None:
    with word_file.open(encoding="utf-8", newline="") as file:
        assert WordList.load(file) == WordList.parse(TEXT)
        assert not file.closed


def test_a_text_file_opened_with_utf_8_sig_equals_parse(word_file: Path) -> None:
    with word_file.open(encoding="utf-8-sig") as file:
        assert WordList.load(file) == WordList.parse(TEXT)


def test_a_missing_path_raises_file_not_found_naming_it(tmp_path: Path) -> None:
    missing = tmp_path / "missing.txt"
    with pytest.raises(FileNotFoundError) as error:
        WordList.load(missing)
    assert "missing.txt" in str(error.value)


def test_invalid_utf_8_raises_unicode_decode_error_naming_the_file(tmp_path: Path) -> None:
    path = tmp_path / "broken.txt"
    path.write_bytes(b"word\n\xff\xfe\n")
    with pytest.raises(UnicodeDecodeError) as error:
        WordList.load(path)
    assert "broken.txt" in str(error.value)


def test_an_unknown_heading_raises_word_list_format_error_with_its_line(tmp_path: Path) -> None:
    path = tmp_path / "bad.txt"
    path.write_text("word\n[3]\nother\n", encoding="utf-8")
    with pytest.raises(WordListFormatError) as error:
        WordList.load(path)
    assert error.value.line == 2


def test_something_that_is_neither_a_path_nor_a_file_raises_type_error() -> None:
    with pytest.raises(TypeError):
        WordList.load(5)  # type: ignore[arg-type]
