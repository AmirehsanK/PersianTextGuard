"""Fails on Unicode-dependent calls outside ``_unicode.py`` (spec 004, research R1).

Python's own case mapping, character classes and regex classes differ from .NET's (``str.isspace``
counts U+001C-U+001F, for example), so every such operation in the package must go through
``persian_text_guard._unicode``. This walks the package sources with ``ast`` and lists every
``file:line`` that does not.
"""

from __future__ import annotations

import ast
import sys
from pathlib import Path

PACKAGE = Path(__file__).resolve().parent.parent / "src" / "persian_text_guard"
EXEMPT = {"_unicode.py", "_wordlists.py", "_version.py"}

BANNED_METHODS = {
    "lower",
    "upper",
    "casefold",
    "title",
    "swapcase",
    "isspace",
    "isalpha",
    "isalnum",
    "isdigit",
    "isdecimal",
    "isnumeric",
    "isprintable",
    "isidentifier",
    "islower",
    "isupper",
    "istitle",
}
NO_ARGUMENT_METHODS = {"strip", "lstrip", "rstrip", "split", "rsplit", "splitlines"}
RE_FUNCTIONS = {"compile", "search", "match", "sub", "split", "findall", "fullmatch"}
RE_CLASSES = ("\\s", "\\S", "\\w", "\\W", "\\d", "\\D")


def _problems(path: Path) -> list[str]:
    tree = ast.parse(path.read_text(encoding="utf-8"), filename=str(path))
    found: list[str] = []
    for node in ast.walk(tree):
        if isinstance(node, (ast.Import, ast.ImportFrom)):
            names = [alias.name for alias in node.names] if isinstance(node, ast.Import) else [node.module]
            if "unicodedata" in names:
                found.append(f"{path.name}:{node.lineno}: import unicodedata")
            continue

        if not isinstance(node, ast.Call) or not isinstance(node.func, ast.Attribute):
            continue

        attribute = node.func.attr
        if attribute in BANNED_METHODS:
            found.append(f"{path.name}:{node.lineno}: .{attribute}() (use _unicode)")
        elif attribute in NO_ARGUMENT_METHODS and not node.args and not node.keywords:
            found.append(f"{path.name}:{node.lineno}: .{attribute}() with no separator (Python's whitespace)")
        elif (
            attribute in RE_FUNCTIONS
            and isinstance(node.func.value, ast.Name)
            and node.func.value.id == "re"
            and node.args
            and isinstance(node.args[0], ast.Constant)
            and isinstance(node.args[0].value, str)
            and any(token in node.args[0].value for token in RE_CLASSES)
        ):
            found.append(f"{path.name}:{node.lineno}: re.{attribute} with a Unicode class (\\s, \\w, \\d)")
    return found


def main() -> int:
    problems: list[str] = []
    files = sorted(p for p in PACKAGE.glob("*.py") if p.name not in EXEMPT)
    for path in files:
        problems.extend(_problems(path))

    if problems:
        print("Unicode-dependent operations outside _unicode.py:")
        for problem in problems:
            print(f"  {problem}")
        return 1

    print(f"check_unicode_usage: {len(files)} file(s) clean")
    return 0


if __name__ == "__main__":
    sys.exit(main())
