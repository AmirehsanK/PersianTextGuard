"""Python-specific behaviour and the spec's User Story 1 scenarios (spec 004, FR-019).

Covers guarantees P1, P2 and P4-P9 and P12 of contracts/public-api.md. Imports only the public package.
"""

import dataclasses
from collections.abc import Callable
from typing import Any

import pytest

from persian_text_guard import (
    BannedWord,
    EvasionKind,
    NormalizationStep,
    ProfanityFilter,
    ProfanityFilterOptions,
    ProfanityMatch,
    WordCategory,
    WordList,
    WordListFormatError,
    WordMatchMode,
    normalize,
    to_ascii_digits,
    to_persian_digits,
    tokenize,
)

FILTER = ProfanityFilter(WordList.persian_default())

KIR = "کیر"
HIGH = "\ud840"
LOW = "\udc00"
U20000 = "\U00020000"
LONG = "سلام hello. " * 11000  # 132,000 characters


# ------------------------------------------------------------------ User Story 1 acceptance scenarios


def test_scenario_1_spaced_letters_are_caught() -> None:
    assert FILTER.contains_profanity("f u c k")


def test_scenario_2_an_ordinary_persian_message_passes_untouched() -> None:
    text = "هر کس پلات بالاست پیام بده"
    assert FILTER.find_match(text) is None
    assert FILTER.find_matches(text) == ()
    assert FILTER.censor(text) == text


def test_scenario_3_every_match_in_order_with_category_evasion_and_position() -> None:
    matches = FILTER.find_matches("sh1t and f u c k")
    assert [(m.word.text, m.word.category, m.evasion, m.index, m.length) for m in matches] == [
        ("shit", WordCategory.PROFANITY, (EvasionKind.LOOKALIKE_CHARACTERS,), 0, 4),
        ("fuck", WordCategory.PROFANITY, (EvasionKind.SPLIT_WORD,), 9, 7),
    ]


def test_scenario_4_positions_are_code_points_so_slicing_gives_the_word() -> None:
    text = "\N{GRINNING FACE} " + KIR
    match = FILTER.find_match(text)
    assert match is not None
    assert (match.index, match.length) == (2, 3)
    assert text[match.index : match.index + match.length] == KIR


def test_scenario_5_your_own_words() -> None:
    own = ProfanityFilter([BannedWord("اسپم"), BannedWord("casino", "anywhere")])
    assert own.contains_profanity("onlinecasino.example")
    assert own.contains_profanity("این پیام اسپم است")


def test_scenario_6_a_chosen_mask() -> None:
    assert FILTER.censor("this is kir", "#") == "this is ####"
    assert FILTER.censor("kir and motherfucker") == "**** and ****"
    with pytest.raises(ValueError, match="mask"):
        FILTER.censor("this is kir", "x")


def test_the_independent_test_of_user_story_1() -> None:
    assert FILTER.contains_profanity("ک.ی.ر")
    assert not FILTER.contains_profanity("سلام، سفارشم کی میرسه؟")
    assert FILTER.censor("kir and motherfucker") == "**** and ****"


# ------------------------------------------------------------------ P1: None and edge-case strings

EDGE_CASES = [
    None,
    "",
    "   ",
    "\ud800",
    "k" + HIGH + LOW + "ir",
    chr(0xFFFE),
    LONG,
]


@pytest.mark.parametrize(
    "text", EDGE_CASES, ids=["none", "empty", "blank", "lone", "split", "nonchar", "long"]
)
def test_every_text_function_returns_for_edge_cases(text: str | None) -> None:
    assert isinstance(FILTER.contains_profanity(text), bool)
    FILTER.find_match(text)
    assert isinstance(FILTER.find_matches(text), tuple)
    assert isinstance(FILTER.censor(text), str)
    assert isinstance(normalize(text), str)
    assert isinstance(normalize(text, "standard"), str)
    assert isinstance(tokenize(text), list)
    assert isinstance(to_persian_digits(text), str)
    assert isinstance(to_ascii_digits(text), str)


def test_none_is_the_missing_value() -> None:
    assert FILTER.contains_profanity(None) is False
    assert FILTER.find_match(None) is None
    assert FILTER.find_matches(None) == ()
    assert FILTER.censor(None) == ""
    assert normalize(None) == ""
    assert tokenize(None) == []
    assert to_persian_digits(None) == ""
    assert to_ascii_digits(None) == ""


# ------------------------------------------------------------------ P2: values that are not text

NOT_TEXT = [b"kir", 5, {}, ["kir"]]


@pytest.mark.parametrize("value", NOT_TEXT, ids=["bytes", "int", "dict", "list"])
def test_values_that_are_not_text_raise_type_error(value: object) -> None:
    calls: list[Callable[[Any], object]] = [
        FILTER.contains_profanity,
        FILTER.find_match,
        FILTER.find_matches,
        FILTER.censor,
        normalize,
        tokenize,
        to_persian_digits,
        to_ascii_digits,
        WordList.parse,
    ]
    for call in calls:
        with pytest.raises(TypeError):
            call(value)


def test_word_list_parse_refuses_none() -> None:
    with pytest.raises(TypeError):
        WordList.parse(None)  # type: ignore[arg-type]


class Sneaky(str):
    __slots__ = ()

    def __getitem__(self, key):  # type: ignore[no-untyped-def]
        return "x"

    def __len__(self) -> int:
        return 1


def test_a_str_subclass_is_treated_as_its_str_value() -> None:
    text = "kir and motherfucker"
    sneaky = Sneaky(text)
    assert FILTER.find_matches(sneaky) == FILTER.find_matches(text)
    assert FILTER.censor(sneaky) == FILTER.censor(text)
    assert type(FILTER.censor(sneaky)) is str
    assert normalize(sneaky) == normalize(text)
    assert tokenize(sneaky) == tokenize(text)


# ------------------------------------------------------------------ P5: supplementary characters


def test_positions_slice_the_callers_string_with_supplementary_characters() -> None:
    text = "kir" + U20000 + " and fuck"
    matches = FILTER.find_matches(text)
    assert [(m.word.text, m.index, m.length) for m in matches] == [("kir", 0, 4), ("fuck", 9, 4)]
    assert text[0:4] == "kir" + U20000
    assert text[9:13] == "fuck"
    assert FILTER.censor(text) == "**** and ****"


def test_censor_leaves_the_callers_split_surrogates_untouched() -> None:
    text = "k" + HIGH + LOW + "ir hello " + HIGH + LOW
    match = FILTER.find_match(text)
    assert match is not None
    assert (match.index, match.length) == (0, 5)
    censored = FILTER.censor(text)
    assert censored == "**** hello " + HIGH + LOW
    assert censored[-2:] == HIGH + LOW


def test_matches_are_ordered_do_not_overlap_and_stay_inside_the_text() -> None:
    text = "kir " + U20000 + " kos kesh \N{GRINNING FACE} fuck"
    matches = FILTER.find_matches(text)
    assert len(matches) >= 2
    previous_end = 0
    for match in matches:
        assert match.index >= previous_end
        assert match.length >= 1
        assert match.index + match.length <= len(text)
        previous_end = match.index + match.length


# ------------------------------------------------------------------ P6: masks


@pytest.mark.parametrize("mask", ["\N{GRINNING FACE}", "ab", "", " ", "\n", "5", "x", "\ud800"])
def test_invalid_masks_raise_value_error_even_for_none_text(mask: str) -> None:
    with pytest.raises(ValueError, match="mask"):
        FILTER.censor("kir", mask)
    with pytest.raises(ValueError, match="mask"):
        FILTER.censor(None, mask)


def test_a_mask_that_is_not_a_str_raises_type_error() -> None:
    with pytest.raises(TypeError):
        FILTER.censor("kir", 5)  # type: ignore[arg-type]
    with pytest.raises(TypeError):
        FILTER.censor(None, 5)  # type: ignore[arg-type]


@pytest.mark.parametrize("mask", ["#", "*", "-", "\N{BLACK SQUARE}", "\N{BULLET}"])
def test_valid_masks_are_accepted(mask: str) -> None:
    assert FILTER.censor("kir", mask) == mask * 4


# ------------------------------------------------------------------ P7: immutability


def test_changing_the_list_after_building_changes_nothing() -> None:
    words = [BannedWord("shit")]
    own = ProfanityFilter(words)
    words.append(BannedWord("push", "anywhere"))
    words[0] = BannedWord("other")

    assert own.count == 1
    assert own.contains_profanity("s h i t")
    assert not own.contains_profanity("push it")


def test_a_match_reports_the_callers_own_entry_object() -> None:
    entry = BannedWord("shit")
    match = ProfanityFilter([entry]).find_match("shit")
    assert match is not None
    assert match.word is entry


def test_results_and_options_are_immutable() -> None:
    matches = FILTER.find_matches("kir and fuck")
    assert isinstance(matches, tuple)
    match = matches[0]
    with pytest.raises(dataclasses.FrozenInstanceError):
        match.index = 1  # type: ignore[misc]
    assert isinstance(match.evasion, tuple)
    with pytest.raises(dataclasses.FrozenInstanceError):
        match.word.text = "x"  # type: ignore[misc]

    options = ProfanityFilterOptions()
    with pytest.raises(dataclasses.FrozenInstanceError):
        options.join_spaced_letters = False  # type: ignore[misc]


def test_the_filter_has_no_attributes_to_change() -> None:
    with pytest.raises(AttributeError):
        FILTER.count = 5  # type: ignore[misc]
    with pytest.raises(AttributeError):
        FILTER.extra = 1


def test_match_equality_and_repr() -> None:
    word = BannedWord("kir", category=WordCategory.SEXUAL)
    match = ProfanityMatch(word, (), 0, 3)
    assert match == ProfanityMatch(BannedWord("kir", "wholeWord", "sexual"), (), 0, 3)
    assert "kir" in repr(match)


# ------------------------------------------------------------------ P8: bundled lists


def test_bundled_lists_are_the_same_tuple_every_time() -> None:
    assert WordList.all() is WordList.all()
    assert WordList.persian_default() is WordList.persian_default()
    assert not any(word.category == WordCategory.MILD for word in WordList.persian_default())


def test_categories_can_be_picked() -> None:
    slurs = ProfanityFilter(WordList.bundled("slur"))
    assert slurs.contains_profanity("faggot")
    assert not slurs.contains_profanity("fuck")


def test_word_list_cannot_be_instantiated() -> None:
    with pytest.raises(TypeError):
        WordList()


# ------------------------------------------------------------------ P9: word-list headings


def test_an_unknown_heading_is_a_word_list_format_error_with_its_line() -> None:
    with pytest.raises(WordListFormatError) as error:
        WordList.parse("[3]\nword\n")
    assert error.value.line == 1
    assert isinstance(error.value, ValueError)

    with pytest.raises(WordListFormatError) as error:
        WordList.parse("word\n\n[nonsense]\n")
    assert error.value.line == 3
    assert str(error.value) == "Line 3: unknown word category 'nonsense'."


# ------------------------------------------------------------------ P12: names


def test_names_and_members_are_interchangeable() -> None:
    assert WordList.bundled("slur") == WordList.bundled(WordCategory.SLUR)
    assert BannedWord("x", "anywhere", "slur") == BannedWord("x", WordMatchMode.ANYWHERE, WordCategory.SLUR)
    assert BannedWord("x", "anywhere").mode is WordMatchMode.ANYWHERE
    assert normalize("ABC", ["lowerCase"]) == normalize("ABC", [NormalizationStep.LOWER_CASE]) == "abc"


def test_unknown_names_raise_value_error() -> None:
    with pytest.raises(ValueError, match="nope"):
        BannedWord("x", category="nope")  # type: ignore[arg-type]
    with pytest.raises(ValueError, match="sometimes"):
        BannedWord("x", mode="sometimes")  # type: ignore[arg-type]
    with pytest.raises(ValueError, match="rude"):
        WordList.bundled("rude")  # type: ignore[arg-type]
    with pytest.raises(ValueError, match="shout"):
        normalize("x", ["shout"])  # type: ignore[list-item]
    with pytest.raises(ValueError, match="loud"):
        normalize("x", "loud")  # type: ignore[arg-type]


def test_options_must_be_bool() -> None:
    with pytest.raises(TypeError):
        ProfanityFilterOptions(squeeze_repeated_letters="no")  # type: ignore[arg-type]
    with pytest.raises(TypeError):
        ProfanityFilterOptions(squeeze_repeated=False)  # type: ignore[call-arg]


def test_banned_word_text_must_be_a_str() -> None:
    with pytest.raises(TypeError):
        BannedWord(5)  # type: ignore[arg-type]


# ------------------------------------------------------------------ constructors


def test_constructor_refuses_missing_or_non_iterable_words() -> None:
    with pytest.raises(TypeError):
        ProfanityFilter(None)  # type: ignore[arg-type]
    with pytest.raises(TypeError):
        ProfanityFilter(5)  # type: ignore[arg-type]
    with pytest.raises(TypeError):
        ProfanityFilter("kir")  # type: ignore[arg-type]


def test_constructor_names_the_position_of_an_entry_that_is_not_a_banned_word() -> None:
    with pytest.raises(TypeError, match="position 0"):
        ProfanityFilter(["kir"])  # type: ignore[list-item]
    with pytest.raises(TypeError, match="position 1"):
        ProfanityFilter([BannedWord("kir"), None])  # type: ignore[list-item]


def test_constructor_refuses_options_of_the_wrong_type() -> None:
    with pytest.raises(TypeError):
        ProfanityFilter([], {"join_spaced_letters": False})  # type: ignore[arg-type]


def test_duplicate_spellings_and_blank_entries_count_once() -> None:
    words = [BannedWord("كص"), BannedWord("کص"), BannedWord("  کص "), BannedWord("")]
    assert ProfanityFilter(words).count == 1


def test_an_empty_list_is_clean() -> None:
    assert ProfanityFilter([]).find_match("anything") is None


def test_any_iterable_of_entries_works() -> None:
    assert ProfanityFilter({BannedWord("damn")}).contains_profanity("daaaamn")
    assert ProfanityFilter(word for word in [BannedWord("damn")]).contains_profanity("damn")


def test_options_turn_readings_off() -> None:
    plain = ProfanityFilter(
        [BannedWord("fuck")],
        ProfanityFilterOptions(
            squeeze_repeated_letters=False, fold_lookalike_characters=False, join_spaced_letters=False
        ),
    )
    assert plain.contains_profanity("fuck")
    assert not plain.contains_profanity("fuuuck")
    assert not plain.contains_profanity("f*ck")
    assert not plain.contains_profanity("f u c k")


# ------------------------------------------------------------------ P4: the four capabilities agree

AGREEMENT = [
    None,
    "",
    "   ",
    "kir kir kir",
    "hi \ud83d kir",
    "hi " + chr(0xFFFE) + " kir",
    "سلام، سفارشم کی میرسه؟",
    "you bitch, kos kesh",
    "sh1t and f u c k",
    "پدر سگ پدر",
    "k kos i kos r",
    "جنده\N{ZERO WIDTH NON-JOINER}ها رو ببین",
    "kir" + U20000 + " and " + U20000 + "fuck",
]


@pytest.mark.parametrize("text", AGREEMENT)
def test_the_four_capabilities_agree(text: str | None) -> None:
    assert_consistent(FILTER, text)


def assert_consistent(filter_: ProfanityFilter, text: str | None) -> None:
    contains = filter_.contains_profanity(text)
    first = filter_.find_match(text)
    every = filter_.find_matches(text)

    assert (first is not None) == contains
    assert (len(every) > 0) == contains

    length = 0 if text is None else len(text)
    for i, match in enumerate(every):
        assert match.index >= 0
        assert match.length >= 1
        assert match.index + match.length <= length
        if i > 0:
            assert every[i - 1].index + every[i - 1].length <= match.index

    if text is not None:
        censored = filter_.censor(text)
        assert (censored != text) == contains
        assert not filter_.contains_profanity(censored)
