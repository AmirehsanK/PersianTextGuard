"""A strictly typed use of every public name: ``pyright --strict`` and ``mypy --strict`` accept it."""

# pyright: strict

from __future__ import annotations

import io
from pathlib import Path

from persian_text_guard import (
    BannedWord,
    EvasionKind,
    NormalizationPreset,
    NormalizationStep,
    NormalizationStepName,
    ProfanityFilter,
    ProfanityFilterOptions,
    ProfanityMatch,
    WordCategory,
    WordCategoryName,
    WordList,
    WordListFormatError,
    WordMatchMode,
    WordMatchModeName,
    __version__,
    normalize,
    to_ascii_digits,
    to_persian_digits,
    tokenize,
)


def build() -> ProfanityFilter:
    """Build a filter from every kind of entry source."""
    words: list[BannedWord] = [*WordList.persian_default(), *WordList.bundled(WordCategory.SLUR, "insult")]
    mode: WordMatchModeName = "anywhere"
    category: WordCategoryName = "slur"
    words.append(BannedWord("casino", mode, category))
    words.append(BannedWord("spam", WordMatchMode.WHOLE_WORD, WordCategory.UNCATEGORIZED))
    words.extend(WordList.parse("[insult]\nword\n"))
    words.extend(WordList.load(io.BytesIO(b"~other\n")))
    words.extend(WordList.load(io.StringIO("third\n")))
    options = ProfanityFilterOptions(squeeze_repeated_letters=True, join_spaced_letters=False)
    return ProfanityFilter(words, options)


def describe(match: ProfanityMatch) -> str:
    """Use every field of a match."""
    evasion: tuple[EvasionKind, ...] = match.evasion
    names = ",".join(kind.value for kind in evasion)
    word: BannedWord = match.word
    return f"{word.text} {word.mode.value} {word.category.value} {names} {match.index} {match.length}"


def main() -> None:
    """Call every function with the types it declares."""
    filter = build()
    count: int = filter.count
    found: bool = filter.contains_profanity("kir")
    first: ProfanityMatch | None = filter.find_match(None)
    every: tuple[ProfanityMatch, ...] = filter.find_matches("kir and fuck")
    censored: str = filter.censor("kir", "#")
    preset: NormalizationPreset = "standard"
    step: NormalizationStepName = "lowerCase"
    texts: list[str] = [
        normalize("x"),
        normalize("x", preset),
        normalize("x", [step, NormalizationStep.ASCII_DIGITS]),
        to_ascii_digits("۱"),
        to_persian_digits("1"),
        *tokenize("a b"),
    ]
    all_words: tuple[BannedWord, ...] = WordList.all()
    try:
        WordList.load(Path("missing.txt"))
    except (OSError, WordListFormatError) as error:
        texts.append(str(error))
    described = [describe(match) for match in every]
    print(count, found, first is None, len(described), len(censored), len(texts), len(all_words), __version__)


if __name__ == "__main__":
    main()
