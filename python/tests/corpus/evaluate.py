"""Runs a corpus case against the Python port. Port of ``js/test/corpus/evaluate.ts``.

Returns the result in the kind's ``expected`` shape: the counterpart of ``Corpus.Evaluate`` and
``CheckKindRules`` in ``dotnet/tests/PersianTextGuard.Conformance/Corpus.cs``. It uses the public API
only, and positions as the port returns them: code points, as the corpus records (P3).
"""

from __future__ import annotations

from typing import Any

from persian_text_guard import (
    BannedWord,
    ProfanityFilter,
    ProfanityFilterOptions,
    ProfanityMatch,
    WordCategory,
    WordList,
    WordListFormatError,
    normalize,
    to_ascii_digits,
    to_persian_digits,
    tokenize,
)

from .load import MATCHING_KINDS, Corpus, CorpusCase, Json
from .values import build_input

_OPTION_NAMES = {
    "squeezeRepeatedLetters": "squeeze_repeated_letters",
    "foldLookalikeCharacters": "fold_lookalike_characters",
    "joinSpacedLetters": "join_spaced_letters",
}

_filters: dict[tuple[int, str], ProfanityFilter] = {}


def filter_for(corpus: Corpus, name: str) -> ProfanityFilter:
    """The filter a named configuration describes, built once per corpus."""
    key = (id(corpus), name)
    cached = _filters.get(key)
    if cached is not None:
        return cached

    configuration = corpus.configurations.get(name)
    if configuration is None:
        raise ValueError(f"No configuration is named '{name}'.")

    built = _build_filter(configuration)
    _filters[key] = built
    return built


def _build_filter(configuration: dict[str, Json]) -> ProfanityFilter:
    word_lists = configuration.get("wordLists")
    if not isinstance(word_lists, dict):
        raise ValueError(f'Configuration has no "wordLists" object: {configuration}')

    words: tuple[BannedWord, ...]
    if "bundled" in word_lists:
        words = selection(word_lists["bundled"])
    elif isinstance(word_lists.get("entries"), list):
        words = tuple(BannedWord(e["text"], e["mode"], e["category"]) for e in word_lists["entries"])
    else:
        raise ValueError(f'"wordLists" needs "bundled" or "entries": {word_lists}')

    options: dict[str, bool] = {}
    raw_options = configuration.get("options")
    if isinstance(raw_options, dict):
        for corpus_name, python_name in _OPTION_NAMES.items():
            value = raw_options.get(corpus_name)
            if isinstance(value, bool):
                options[python_name] = value

    return ProfanityFilter(words, ProfanityFilterOptions(**options))


def selection(value: Json) -> tuple[BannedWord, ...]:
    """The bundled entries a selection names: ``"default"``, ``"all"`` or a list of categories."""
    if isinstance(value, list):
        return WordList.bundled(*value)
    if value == "default":
        return WordList.persian_default()
    if value == "all":
        return WordList.all()
    raise ValueError(f"Not a selection: {value}")


def _entry(word: BannedWord) -> dict[str, Json]:
    return {"text": word.text, "mode": word.mode.value, "category": word.category.value}


def _match_node(match: ProfanityMatch) -> dict[str, Json]:
    return {
        "entry": _entry(match.word),
        "evasion": [kind.value for kind in match.evasion],
        "start": match.index,
        "length": match.length,
    }


def evaluate(corpus: Corpus, case: CorpusCase) -> dict[str, Json]:
    """Run a case and return the result in its kind's ``expected`` shape."""
    data = case.json
    kind = case.kind

    if kind in MATCHING_KINDS:
        if not isinstance(data.get("configuration"), str):
            raise ValueError('"configuration" is missing.')

        filter_ = filter_for(corpus, data["configuration"])
        text = build_input(data.get("input"))
        first = filter_.find_match(text)

        result: dict[str, Json] = {
            "containsProfanity": filter_.contains_profanity(text),
            "firstMatch": None if first is None else _match_node(first),
            "matches": [_match_node(match) for match in filter_.find_matches(text)],
            "censored": filter_.censor(text),
        }

        masks = data.get("masks")
        if isinstance(masks, list):
            censored_with: dict[str, Json] = {}
            for mask in masks:
                if not isinstance(mask, str) or len(mask) != 1:
                    raise ValueError(f"A mask must be exactly one UTF-16 code unit: {mask!r}")
                censored_with[mask] = filter_.censor(text, mask)
            result["censoredWith"] = censored_with

        return result

    if kind == "normalization":
        text = build_input(data.get("input"))
        steps: Any = data.get("steps")
        if steps == "toPersianDigits":
            output = to_persian_digits(text)
        elif steps == "toAsciiDigits":
            output = to_ascii_digits(text)
        else:
            output = normalize(text, steps)
        return {"output": output}

    if kind == "tokenization":
        return {"tokens": tokenize(build_input(data.get("input")))}

    if kind == "word-list-parsing":
        text = build_input(data.get("text"))
        if text is None:
            raise ValueError('"text" is missing.')
        try:
            return {"entries": [_entry(word) for word in WordList.parse(text)]}
        except WordListFormatError as error:
            return {"error": {"kind": "unknown-category", "line": error.line}}

    if kind == "category-selection":
        if "selection" not in data:
            raise ValueError('"selection" is missing.')
        return _category_selection(data["selection"])

    if kind == "mask-validation":
        mask = build_input(data.get("mask"))
        if mask is None or len(mask) != 1:
            raise ValueError('"mask" must be exactly one UTF-16 code unit.')
        try:
            filter_for(corpus, "default").censor("kir", mask)
        except ValueError:
            return {"accepted": False}
        return {"accepted": True}

    raise ValueError(f"Unknown kind '{kind}'.")


def _category_selection(value: Json) -> dict[str, Json]:
    words = selection(value)
    is_default = value == "default"

    rules: list[Json] = []
    allowed = {WordCategory(name) for name in value} if isinstance(value, list) else None
    if all(
        word.category != WordCategory.UNCATEGORIZED if allowed is None else word.category in allowed
        for word in words
    ):
        rules.append("categoriesInSelection")

    if _in_bundled_order(words):
        rules.append("bundledOrder")

    if is_default and all(word.category != WordCategory.MILD for word in words):
        rules.append("noMild")

    return {
        "count": len(words),
        "first": [_entry(word) for word in words[:5]],
        "last": [_entry(word) for word in words[max(0, len(words) - 5) :]],
        "rules": rules,
    }


def _in_bundled_order(words: tuple[BannedWord, ...]) -> bool:
    every = WordList.all()
    position = 0
    for word in words:
        while position < len(every) and every[position] != word:
            position += 1
        if position == len(every):
            return False
        position += 1
    return True


def check_kind_rules(case: CorpusCase) -> list[str]:
    """The kind rules from the data model that a matching case's recorded ``expected`` breaks."""
    violations: list[str] = []
    expected: Any = case.json.get("expected")
    if not isinstance(expected, dict) or case.kind not in MATCHING_KINDS:
        return violations

    contains = expected.get("containsProfanity") is True

    if case.kind == "must-match" and not contains:
        violations.append("must-match requires containsProfanity to be true")

    if case.kind == "ordinary":
        if contains:
            violations.append("ordinary requires containsProfanity to be false")

        if expected.get("firstMatch") is not None:
            violations.append("ordinary requires firstMatch to be null")

        matches = expected.get("matches")
        if not isinstance(matches, list) or len(matches) != 0:
            violations.append("ordinary requires matches to be empty")

        text = build_input(case.json.get("input")) or ""
        if "censored" not in expected or build_input(expected["censored"]) != text:
            violations.append("ordinary requires censored to equal the input")

        censored_with = expected.get("censoredWith")
        if isinstance(censored_with, dict):
            for mask, value in censored_with.items():
                if build_input(value) != text:
                    violations.append(f'ordinary requires censoredWith["{mask}"] to equal the input')

    return violations
