"""Word lists: parsing, loading files, and the bundled lists. Port of ``js/src/word-list.ts``.

Which ports ``dotnet/src/PersianTextGuard/WordList.cs``.
"""

from __future__ import annotations

import os
import threading
from typing import BinaryIO, TextIO

from ._types import (
    BannedWord,
    WordCategory,
    WordCategoryName,
    WordListFormatError,
    WordMatchMode,
    require_text,
    to_category,
)
from ._unicode import is_letter, is_white_space, to_lower_invariant
from ._wordlists import BUNDLED_WORD_LISTS

_BOM = "\N{ZERO WIDTH NO-BREAK SPACE}"


def trim_dot_net(value: str) -> str:
    """Trim whitespace like .NET ``string.Trim()``, with .NET's whitespace set (U+FEFF is not trimmed)."""
    start = 0
    end = len(value)
    while start < end and is_white_space(value[start]):
        start += 1

    while end > start and is_white_space(value[end - 1]):
        end -= 1

    return value if start == 0 and end == len(value) else value[start:end]


def _lower_invariant(value: str) -> str:
    return "".join(to_lower_invariant(c) for c in value)


def _category_named(name: str) -> WordCategory | None:
    """The category a section heading names, or ``None``.

    A heading is a category name only, in any letter case; numbers are not names (spec 003, FR-029).
    """
    if not name:
        return None

    for c in name:
        if not is_letter(c):
            return None

    lower = _lower_invariant(name)
    for category in WordCategory:
        if _lower_invariant(category.value) == lower:
            return category
    return None


def _parse(text: str) -> tuple[BannedWord, ...]:
    # Only ASCII markers and BMP whitespace are tested, so code points and a UTF-16 view parse alike:
    # a heading with a character above U+FFFF is unknown either way.
    words: list[BannedWord] = []
    category = WordCategory.UNCATEGORIZED
    line_number = 0

    for raw in text.split("\n"):
        line_number += 1
        line = trim_dot_net(raw)
        if not line or line[0] == "#":
            continue

        if len(line) > 2 and line[0] == "[" and line[-1] == "]":
            name = trim_dot_net(line[1:-1])
            named = _category_named(name)
            if named is None:
                raise WordListFormatError(f"Line {line_number}: unknown word category '{name}'.", line_number)

            category = named
            continue

        anywhere = line[0] == "~"
        word = trim_dot_net(line[1:] if anywhere else line)

        if word:
            words.append(
                BannedWord(word, WordMatchMode.ANYWHERE if anywhere else WordMatchMode.WHOLE_WORD, category)
            )

    return tuple(words)


_lock = threading.Lock()
_all: tuple[BannedWord, ...] | None = None
_persian_default: tuple[BannedWord, ...] | None = None


def _bundled() -> tuple[tuple[BannedWord, ...], tuple[BannedWord, ...]]:
    # Parsed once, under a lock, so every caller on every thread gets the same tuples (research R17).
    global _all, _persian_default  # noqa: PLW0603
    all_words, persian_default = _all, _persian_default
    if all_words is not None and persian_default is not None:
        return all_words, persian_default

    with _lock:
        if _all is None or _persian_default is None:
            parsed = tuple(word for text in BUNDLED_WORD_LISTS for word in _parse(text))
            _persian_default = tuple(word for word in parsed if word.category != WordCategory.MILD)
            _all = parsed
        return _all, _persian_default


def _file_name(name: object) -> str:
    return os.fsdecode(name) if isinstance(name, (str, bytes, os.PathLike)) else str(name)


class WordList:
    r"""Reads word lists for ``ProfanityFilter``, and gives the bundled Persian, Finglish and English lists.

    The format is one entry per line: a word or phrase is matched as whole words, a leading ``~``
    matches it anywhere (inside longer words too), and lines starting with ``#`` are comments. A line
    like ``[insult]`` starts a section: every entry after it, until the next section, gets that
    category. A heading is a category name in any letter case, with spaces allowed; numbers are not
    categories. Entries before the first section are ``WordCategory.UNCATEGORIZED``. Blank lines and
    ``\r\n`` line endings are fine.

    ``WordList`` only has static methods and cannot be instantiated.
    """

    __slots__ = ()

    def __init__(self) -> None:
        """Refuse to create an instance: every method is static."""
        raise TypeError("WordList is not instantiable")

    @staticmethod
    def all() -> tuple[BannedWord, ...]:
        """Every bundled entry, ``MILD`` included, in list order: Persian, Finglish, English.

        Parsed once, on first use; every call, from any thread, returns the same tuple.
        """
        return _bundled()[0]

    @staticmethod
    def persian_default() -> tuple[BannedWord, ...]:
        """The bundled list without the ``MILD`` entries.

        Profanity, sexual words, insults, slurs and harassment, curated to avoid flagging ordinary
        words. Nothing uses it unless you pass it to a filter. The word-list files themselves document
        what is deliberately left out and why (for example «کس», which also means "person"). Every
        call, from any thread, returns the same tuple.
        """
        return _bundled()[1]

    @staticmethod
    def bundled(*categories: WordCategory | WordCategoryName) -> tuple[BannedWord, ...]:
        """The bundled entries in the given categories, in list order.

        Args:
            *categories: :class:`WordCategory` members or their names (``"slur"``, ...).

        Returns:
            A new tuple. Empty when no category is given.

        Raises:
            ValueError: A category name does not exist.
            TypeError: A category is not a member or a ``str``.

        Example:
            >>> # A dating app: sexual words are fine, abuse is not.
            >>> filter = ProfanityFilter(WordList.bundled("insult", "slur", "harassment"))
        """
        wanted = {to_category(category) for category in categories}
        return tuple(word for word in WordList.all() if word.category in wanted)

    @staticmethod
    def parse(text: str) -> tuple[BannedWord, ...]:
        """Parse a word list from its text.

        Args:
            text: The word-list text, in the shared format.

        Returns:
            The entries, in order.

        Raises:
            WordListFormatError: A section names a category that does not exist; its ``line`` is
                1-based.
            TypeError: ``text`` is not a ``str``. There is no missing word list, so ``None`` raises too.
        """
        value = require_text(text, "text")
        if value is None:
            raise TypeError("text must be a str, not NoneType")
        return _parse(value)

    @staticmethod
    def load(source: str | os.PathLike[str] | BinaryIO | TextIO) -> tuple[BannedWord, ...]:
        """Read a word-list file and parse it exactly as :meth:`parse` does.

        Args:
            source: A path (``str`` or ``os.PathLike``), or an open file. A path, or a file opened in
                binary mode, is decoded as UTF-8, and a UTF-8 byte order mark at the start is ignored.
                A file opened in text mode is read as it is, and a leading U+FEFF is ignored. An open
                file is read from its current position and is not closed: the caller owns it.

        Returns:
            The entries, in order.

        Raises:
            OSError: The file cannot be opened or read (``FileNotFoundError`` names the path).
            UnicodeDecodeError: The file is not valid UTF-8; the reason names the file.
            WordListFormatError: A section names a category that does not exist.
            TypeError: ``source`` is not a path or a readable file.
        """
        if isinstance(source, (str, os.PathLike)):
            with open(source, "rb") as file:
                data: object = file.read()
            name: object = source
        else:
            read = getattr(source, "read", None)
            if not callable(read):
                raise TypeError(f"source must be a path or an open file, not {type(source).__name__}")
            data = read()
            name = getattr(source, "name", "<file>")

        if isinstance(data, bytes):
            try:
                text = data.decode("utf-8-sig")
            except UnicodeDecodeError as error:
                raise UnicodeDecodeError(
                    error.encoding,
                    error.object,
                    error.start,
                    error.end,
                    f"{error.reason} in {_file_name(name)}",
                ) from None
        elif isinstance(data, str):
            text = data.removeprefix(_BOM)
        else:
            raise TypeError(f"source.read() must return bytes or str, not {type(data).__name__}")

        return _parse(text)
