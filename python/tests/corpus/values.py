"""Corpus values: building inputs, visible text and exact comparison. Port of ``js/test/corpus/values.ts``.

Positions need no conversion here: the corpus records code points, and Python returns code points
(guarantee P3).
"""

from __future__ import annotations

import json
import re
from typing import NamedTuple

from persian_text_guard._unicode import category_of_unit, is_noncharacter, is_white_space, recombine

from .load import Json

_HEX4 = re.compile("[0-9A-Fa-f]{4}")
_BACKSLASH = chr(92)
_INVISIBLE_CATEGORIES = frozenset({"Cf", "Cc", "Zl", "Zp"})
_SHOW_LIMIT = 200


def _is_build(node: Json) -> bool:
    return isinstance(node, dict) and len(node) == 1 and isinstance(node.get("build"), list)


def build_input(node: Json) -> str | None:
    """Build an Input: a string, ``None``, or ``{"build": [parts...]}``.

    A ``utf16`` part is one UTF-16 unit, which may be a lone surrogate. Units are joined the way a UTF-16
    string reads them: a high surrogate followed by a low one is one code point, and any other
    surrogate stays a lone surrogate code point.
    """
    if node is None:
        return None

    if isinstance(node, str):
        return node

    if _is_build(node):
        parts: list[str] = []
        for part in node["build"]:
            if not isinstance(part, dict):
                raise ValueError(f"A build part must be an object: {display(part)}")

            if len(part) == 1 and isinstance(part.get("text"), str):
                parts.append(part["text"])
            elif (
                len(part) == 2
                and isinstance(part.get("repeat"), str)
                and isinstance(part.get("times"), int)
                and part["times"] >= 1
            ):
                parts.append(part["repeat"] * part["times"])
            elif len(part) == 1 and isinstance(part.get("utf16"), str) and _HEX4.fullmatch(part["utf16"]):
                parts.append(chr(int(part["utf16"], 16)))
            else:
                raise ValueError(f"Not a build part: {display(part)}")

        return recombine("".join(parts))

    raise ValueError(f"Not an Input: {display(node)}")


def is_text(node: Json) -> bool:
    """Whether a value is text: a string or a build object."""
    return isinstance(node, str) or _is_build(node)


def _escape(code_point: int) -> str:
    return _BACKSLASH + (f"u{code_point:04X}" if code_point <= 0xFFFF else f"U{code_point:08X}")


def _is_invisible(c: str) -> bool:
    return (
        category_of_unit(c) in _INVISIBLE_CATEGORIES
        or (is_white_space(c) and c != " ")
        or is_noncharacter(ord(c))
        or 0xD800 <= ord(c) <= 0xDFFF
    )


def show_invisible(text: str | None) -> str:
    """The text with invisible characters escaped.

    Cf, Cc, Zl, Zp, whitespace other than U+0020, lone surrogates and noncharacters become a
    backslash-u escape, or backslash-U above U+FFFF.
    """
    if text is None:
        return "null"
    return "".join(_escape(ord(c)) if _is_invisible(c) else c for c in text)


def show_text(text: str | None) -> str:
    """Text for messages, shortened when long."""
    if text is None:
        return "null"
    if len(text) <= _SHOW_LIMIT:
        return f'"{show_invisible(text)}"'
    return f'"{show_invisible(text[:_SHOW_LIMIT])}..." ({len(text)} code points)'


_MISSING = object()


def display(node: object) -> str:
    """A value on one line, for messages."""
    if node is _MISSING:
        return "(missing)"
    if is_text(node):
        return show_text(build_input(node))
    return json.dumps(_visible(node), ensure_ascii=False)


def _visible(node: object) -> object:
    if isinstance(node, str):
        return show_invisible(node)
    if isinstance(node, list):
        return [_visible(item) for item in node]
    if isinstance(node, dict):
        return {key: _visible(value) for key, value in node.items()}
    return node


class Difference(NamedTuple):
    """One field that differs."""

    path: str
    expected: str
    actual: str


def compare(expected: Json, actual: Json, path: str = "expected") -> list[Difference]:
    """Every difference between an expected and an actual result, field by field.

    Text compares by its built value, so a string and an equivalent build object are equal.
    """
    differences: list[Difference] = []
    _walk(expected, actual, path, differences)
    return differences


def _walk(expected: object, actual: object, path: str, out: list[Difference]) -> None:
    if expected is None or actual is None or expected is _MISSING or actual is _MISSING:
        if expected is not actual:
            out.append(Difference(path, display(expected), display(actual)))
        return

    if is_text(expected) and is_text(actual):
        e = build_input(expected)
        a = build_input(actual)
        if e != a:
            out.append(Difference(path, show_text(e), show_text(a)))
        return

    if isinstance(expected, dict) and isinstance(actual, dict):
        for key, value in expected.items():
            _walk(value, actual.get(key, _MISSING), f"{path}.{key}", out)
        for key, value in actual.items():
            if key not in expected:
                out.append(Difference(f"{path}.{key}", "(missing)", display(value)))
        return

    if isinstance(expected, list) and isinstance(actual, list):
        for i in range(max(len(expected), len(actual))):
            _walk(
                expected[i] if i < len(expected) else _MISSING,
                actual[i] if i < len(actual) else _MISSING,
                f"{path}[{i}]",
                out,
            )
        return

    # bool is an int in Python, and the corpus never confuses the two, so compare types exactly.
    if type(expected) is not type(actual) or expected != actual:
        out.append(Difference(path, display(expected), display(actual)))
