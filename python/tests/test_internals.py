"""Port of ``js/test/internals.test.ts``.

Which ports ``dotnet/tests/PersianTextGuard.Tests/SourceMapTests.cs``, plus normalizer and word-list
internals the corpus does not reach by construction. Internal functions take the UTF-16 view, so every
sample goes through ``to_view`` first.
"""

import pytest

from persian_text_guard._fold import fold, squeeze
from persian_text_guard._normalizer import (
    COMPARISON_STEPS,
    LOWER_CASE,
    UNIFY_LETTERS,
    normalize,
    normalize_with_map,
    resolve_steps,
    to_ascii_digits,
    to_persian_digits,
)
from persian_text_guard._source_map import ReadingKind, build, chunk_map, whole_message_map
from persian_text_guard._types import BannedWord, WordCategory, WordListFormatError, WordMatchMode
from persian_text_guard._utf16 import to_view
from persian_text_guard._word_list import WordList


def cp(*codes: int) -> str:
    return "".join(map(chr, codes))


KAF_YEH_REH_FORMS = cp(0xFEDB, 0xFEF4, 0xFEAE)  # presentation forms of «کیر»

SAMPLES = [
    cp(0x643, 0x62A, 0x627, 0x628, 0x200C, 0x647, 0x627, 0x64A, 0x20, 0x20, 0x6F1, 0x6F2, 0x20) + "ABC",
    KAF_YEH_REH_FORMS,
    "f" + cp(0xFA) + "ck",
    "u" + cp(0x301),
    cp(0x6A9, 0x640, 0x640, 0x6CC, 0x640, 0x640, 0x631),  # tatweel
    cp(0x6A9, 0x650, 0x6CC, 0x631),  # kasra
    cp(0x633, 0x633, 0x633, 0x633, 0x644, 0x627, 0x645),
    "sh!t 455",
    cp(0x1F175, 0x1F184, 0x1F172, 0x1F17A),
    "f" + cp(0x1F595) + "ck",
    "hi \ud83d",
    "   a \t b  ",
    cp(0x6A9, 0x200B, 0x6CC, 0x631),  # zero-width space
    cp(0x62C, 0x200F, 0x646, 0x62F, 0x647),  # right-to-left mark
]

KINDS = [ReadingKind.NORMALIZED, ReadingKind.SQUEEZED, ReadingKind.FOLDED, ReadingKind.FOLDED_SQUEEZED]


def assert_valid_map(map_: list[int], output_length: int, source_length: int) -> None:
    assert len(map_) == output_length
    for i, source in enumerate(map_):
        assert 0 <= source <= source_length - 1
        if i > 0:
            assert source >= map_[i - 1], f"map goes backwards at {i}"


def expected_reading(view: str, kind: ReadingKind) -> str:
    normalized = normalize_with_map(view, COMPARISON_STEPS, None)
    if kind == ReadingKind.NORMALIZED:
        return normalized
    if kind == ReadingKind.SQUEEZED:
        return squeeze(normalized)
    if kind == ReadingKind.FOLDED:
        return fold(normalized)
    return squeeze(fold(normalized))


# ------------------------------------------------------------------ source maps (SourceMapTests)


@pytest.mark.parametrize("text", SAMPLES)
def test_mapped_normalization_produces_the_same_text(text: str) -> None:
    view = to_view(text)
    map_: list[int] = []
    mapped = normalize_with_map(view, COMPARISON_STEPS, map_)
    assert mapped == normalize_with_map(view, COMPARISON_STEPS, None)
    assert_valid_map(map_, len(mapped), len(view))


@pytest.mark.parametrize("text", SAMPLES)
def test_mapped_fold_and_squeeze_produce_the_same_text(text: str) -> None:
    normalized = normalize_with_map(to_view(text), COMPARISON_STEPS, None)

    fold_map: list[int] = []
    folded = fold(normalized, fold_map)
    assert folded == fold(normalized)
    assert_valid_map(fold_map, len(folded), len(normalized))

    squeeze_map: list[int] = []
    squeezed = squeeze(normalized, squeeze_map)
    assert squeezed == squeeze(normalized)
    assert_valid_map(squeeze_map, len(squeezed), len(normalized))


@pytest.mark.parametrize("text", SAMPLES)
def test_every_reading_maps_back_into_the_original(text: str) -> None:
    view = to_view(text)
    for kind in KINDS:
        mapped = build(view, kind)
        assert mapped.text == expected_reading(view, kind)
        assert_valid_map(mapped.start_map, len(mapped.text), len(view))
        assert_valid_map(mapped.end_map, len(mapped.text), len(view))


def test_presentation_forms_map_one_to_one() -> None:
    map_: list[int] = []
    assert normalize_with_map(KAF_YEH_REH_FORMS, COMPARISON_STEPS, map_) == cp(0x6A9, 0x6CC, 0x631)
    assert map_ == [0, 1, 2]


def test_removed_invisible_characters_are_skipped_in_the_map() -> None:
    map_: list[int] = []
    assert normalize_with_map(cp(0x6A9, 0x200B, 0x6CC, 0x631), COMPARISON_STEPS, map_) == cp(
        0x6A9, 0x6CC, 0x631
    )
    assert map_ == [0, 2, 3]


def test_collapsed_repeats_map_to_the_letters_they_kept() -> None:
    text = cp(0x633, 0x633, 0x633, 0x633, 0x644, 0x627, 0x645)
    map_: list[int] = []
    normalized = normalize_with_map(text, COMPARISON_STEPS, map_)
    for i, c in enumerate(normalized):
        assert c == text[map_[i]]


def test_fallback_maps_only_ever_widen() -> None:
    text = "ab kir cd"
    chunk = chunk_map(text, text)
    assert chunk.start_map[3] <= 3
    assert chunk.end_map[5] >= 5

    whole = whole_message_map(text, text)
    assert whole.start_map[3] == 0
    assert whole.end_map[5] == len(text) - 1


def test_a_chunk_count_mismatch_falls_back_to_the_whole_message() -> None:
    mapped = chunk_map("ab kir", "abkir x y")
    assert all(start == 0 for start in mapped.start_map)
    assert all(end == 5 for end in mapped.end_map)


def test_a_noncharacter_keeps_its_place_in_the_map() -> None:
    map_: list[int] = []
    text = cp(0xFEDB, 0xFFFE, 0xFEAE)
    normalized = normalize_with_map(text, COMPARISON_STEPS, map_)
    assert normalized == normalize_with_map(text, COMPARISON_STEPS, None)
    assert_valid_map(map_, len(normalized), len(text))


# ------------------------------------------------------------------ normalizer internals


def test_none_becomes_empty() -> None:
    assert normalize(None) == ""
    assert to_persian_digits(None) == ""
    assert to_ascii_digits(None) == ""


def test_digit_helpers_leave_letters_alone() -> None:
    hours = cp(0x633, 0x627, 0x639, 0x62A)
    assert to_persian_digits(f"2 {hours}, KR1") == f"{cp(0x6F2)} {hours}, KR{cp(0x6F1)}"
    assert to_ascii_digits(f"{cp(0x6F2)} KR{cp(0x661)}") == "2 KR1"


def test_step_names_resolve_to_the_dot_net_flag_bits() -> None:
    assert resolve_steps(["unifyLetters"]) == UNIFY_LETTERS
    assert resolve_steps(["unifyLetters", "lowerCase"]) == UNIFY_LETTERS | LOWER_CASE
    assert resolve_steps("comparison") == 1023
    assert resolve_steps("standard") == 299
    assert resolve_steps("none") == 0


def test_unknown_presets_and_steps_raise_value_error() -> None:
    with pytest.raises(ValueError, match="preset"):
        resolve_steps("bogus")
    with pytest.raises(ValueError, match="shout"):
        resolve_steps(["unifyLetters", "shout"])


def test_a_step_name_alone_is_not_iterated_as_characters() -> None:
    with pytest.raises(ValueError, match="preset"):
        resolve_steps("lowerCase")


def test_a_very_long_message_normalizes_without_overflowing() -> None:
    text = (cp(0x633, 0x644, 0x627, 0x645) + " hello. ") * 20000
    map_: list[int] = []
    normalized = normalize_with_map(text, COMPARISON_STEPS, map_)
    assert normalized == normalize(text)
    assert len(map_) == len(normalized)


# ------------------------------------------------------------------ word-list headings are names (FR-029)


@pytest.mark.parametrize("text", ["[3]\nword\n", "[+4]\nword\n", "[ 03 ]\nword\n"])
def test_a_number_is_an_unknown_category_on_line_1(text: str) -> None:
    with pytest.raises(WordListFormatError) as error:
        WordList.parse(text)
    assert error.value.line == 1


def test_names_in_any_case_with_spaces_around_them() -> None:
    assert WordList.parse("[ Insult ]\nword\n[SLUR]\n~other\n") == (
        BannedWord("word", WordMatchMode.WHOLE_WORD, WordCategory.INSULT),
        BannedWord("other", WordMatchMode.ANYWHERE, WordCategory.SLUR),
    )


def test_a_two_character_line_is_a_word_not_a_section() -> None:
    assert WordList.parse("[]\n") == (BannedWord("[]"),)


def test_the_bundled_lists_parse_and_default_excludes_mild() -> None:
    assert len(WordList.all()) == 1250
    assert len(WordList.persian_default()) == 1025
    assert not any(word.category == WordCategory.MILD for word in WordList.persian_default())
