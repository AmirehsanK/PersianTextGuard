"""The .NET-versus-Python differences of research R1, and the UTF-16 view of R2, pinned as tests."""

import unicodedata

import pytest

from persian_text_guard._unicode import (
    WHITE_SPACE,
    category_at,
    category_of_unit,
    is_letter,
    is_letter_or_digit,
    is_white_space,
    nfkc,
    noncharacter_length_at,
    to_lower_invariant,
)
from persian_text_guard._utf16 import PositionMap, from_view, to_view

HIGH = "\ud840"  # U+20000 is HIGH + LOW
LOW = "\udc00"
U20000 = "\U00020000"


@pytest.mark.parametrize(
    ("unit", "expected"),
    [
        (0x20, True),
        (0x09, True),
        (0x0D, True),
        (0x85, True),  # NEL: .NET whitespace
        (0xA0, True),
        (0x2028, True),
        (0x3000, True),
        (0x1C, False),  # str.isspace says yes; .NET says no
        (0x1F, False),
        (0xFEFF, False),
        (0x200B, False),
        (0x41, False),
    ],
)
def test_is_white_space_matches_dot_net(unit: int, expected: bool) -> None:
    assert is_white_space(chr(unit)) is expected


def test_white_space_set_matches_the_categories_on_this_python() -> None:
    expected = {chr(c) for c in range(0x10000) if unicodedata.category(chr(c)) in ("Zs", "Zl", "Zp")}
    expected |= {chr(c) for c in (0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x85)}
    assert expected == WHITE_SPACE


def test_str_isspace_differs_which_is_why_it_is_banned() -> None:
    assert "\x1c".isspace()
    assert not is_white_space("\x1c")


def test_to_lower_invariant_keeps_dotted_capital_i() -> None:
    assert to_lower_invariant("\N{LATIN CAPITAL LETTER I WITH DOT ABOVE}") == (
        "\N{LATIN CAPITAL LETTER I WITH DOT ABOVE}"
    )


def test_to_lower_invariant_sigma_is_never_final() -> None:
    assert to_lower_invariant("\N{GREEK CAPITAL LETTER SIGMA}") == "\N{GREEK SMALL LETTER SIGMA}"


def test_to_lower_invariant_ascii() -> None:
    assert to_lower_invariant("A") == "a"
    assert to_lower_invariant("a") == "a"


def test_a_lone_surrogate_unit_is_cs() -> None:
    assert category_of_unit("\ud800") == "Cs"
    assert category_of_unit(LOW) == "Cs"


def test_category_at_reads_a_pair_as_one_code_point() -> None:
    assert category_at(HIGH + LOW, 0) == "Lo"
    assert category_at(HIGH + LOW, 1) == "Cs"
    emoji = to_view("\N{GRINNING FACE}")
    assert category_at(emoji, 0) == "So"
    assert category_at(emoji, 1) == "Cs"


def test_persian_letters_marks_and_digits() -> None:
    assert category_of_unit("\N{ARABIC LETTER KAF}") == "Lo"
    assert category_of_unit("\N{ARABIC FATHATAN}") == "Mn"
    assert category_of_unit("\N{EXTENDED ARABIC-INDIC DIGIT ONE}") == "Nd"
    assert is_letter("\N{ARABIC LETTER KEHEH}")
    assert is_letter_or_digit("\N{EXTENDED ARABIC-INDIC DIGIT ONE}")
    assert not is_letter_or_digit("\N{ARABIC COMMA}")


def test_unassigned_is_cn() -> None:
    assert category_of_unit(chr(0x0378)) == "Cn"


def test_nfkc_folds_mathematical_bold_letters_in_a_view() -> None:
    assert nfkc(to_view("\N{MATHEMATICAL BOLD SMALL K}")) == "k"


def test_nfkc_folds_presentation_forms() -> None:
    forms = (
        "\N{ARABIC LETTER KAF INITIAL FORM}\N{ARABIC LETTER YEH MEDIAL FORM}\N{ARABIC LETTER REH FINAL FORM}"
    )
    assert nfkc(forms) == "\N{ARABIC LETTER KAF}\N{ARABIC LETTER YEH}\N{ARABIC LETTER REH}"


def test_nfkc_keeps_noncharacters_and_normalizes_around_them() -> None:
    assert nfkc("a" + chr(0xFFFE) + "b") == "a" + chr(0xFFFE) + "b"
    kaf = "\N{ARABIC LETTER KAF INITIAL FORM}"
    assert nfkc("a" + chr(0xFFFE) + kaf) == "a" + chr(0xFFFE) + "\N{ARABIC LETTER KAF}"


def test_nfkc_never_raises_for_any_of_the_66_noncharacters() -> None:
    noncharacters = list(range(0xFDD0, 0xFDF0))
    for plane in range(0x11):
        noncharacters += [(plane << 16) | 0xFFFE, (plane << 16) | 0xFFFF]
    assert len(noncharacters) == 66

    kaf = "\N{ARABIC LETTER KAF INITIAL FORM}"
    for code_point in noncharacters:
        text = to_view(f"{kaf} {chr(code_point)} {kaf}")
        expected = to_view(f"\N{ARABIC LETTER KAF} {chr(code_point)} \N{ARABIC LETTER KAF}")
        assert nfkc(text) == expected


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        (chr(0xFFFE), 1),
        (chr(0xFDD0), 1),
        (chr(0x1FFFE), 2),
        (chr(0x10FFFF), 2),
        ("a", 0),
        ("\N{GRINNING FACE}", 0),
    ],
)
def test_noncharacter_length_at(text: str, expected: int) -> None:
    assert noncharacter_length_at(to_view(text), 0) == expected


def test_to_view_splits_supplementary_characters() -> None:
    assert to_view("kir" + U20000) == "kir" + HIGH + LOW


def test_to_view_returns_plain_text_itself() -> None:
    text = "abc"
    assert to_view(text) is text
    persian = "\N{ARABIC LETTER KEHEH}\N{ARABIC LETTER FARSI YEH}"
    assert to_view(persian) is persian


def test_from_view_recombines_pairs_and_keeps_lone_surrogates() -> None:
    assert from_view("kir" + HIGH + LOW) == "kir" + U20000
    assert from_view("a" + HIGH + "b") == "a" + HIGH + "b"


def test_position_map_converts_units_to_code_points() -> None:
    text = "kir" + U20000
    view = to_view(text)
    assert PositionMap(text, view).to_code_points(0, 5) == (0, 4)
    assert PositionMap(text, view).to_code_points(3, 2) == (3, 1)


def test_position_map_is_the_identity_for_the_callers_own_split_surrogates() -> None:
    text = "k" + HIGH + LOW
    view = to_view(text)
    assert len(view) == len(text)
    assert PositionMap(text, view).to_code_points(0, 3) == (0, 3)
