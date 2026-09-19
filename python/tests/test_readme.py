"""Every ``python`` code block in the READMEs runs, with its asserts (FR-024, constitution Principle VI).

It reads ``python/README.md`` and the root ``README.md``, and runs each block in a fresh namespace.
"""

import re
from pathlib import Path

import pytest

PYTHON = Path(__file__).resolve().parent.parent
READMES = {"python-README": PYTHON / "README.md", "root-README": PYTHON.parent / "README.md"}
MINIMUM_BLOCKS = {"python-README": 8, "root-README": 1}

FENCE = re.compile(r"^```python\n(.*?)^```$", re.DOTALL | re.MULTILINE)


def blocks(path: Path) -> list[tuple[int, str]]:
    text = path.read_text(encoding="utf-8").replace("\r\n", "\n")
    return [(text.count("\n", 0, match.start()) + 2, match.group(1)) for match in FENCE.finditer(text)]


CASES = [
    pytest.param(path, line, code, id=f"{name}-block-{number}-line-{line}")
    for name, path in READMES.items()
    for number, (line, code) in enumerate(blocks(path), start=1)
]


@pytest.mark.parametrize(("path", "line", "code"), CASES)
def test_readme_block_runs(path: Path, line: int, code: str) -> None:
    try:
        exec(compile(code, f"{path.name}:{line}", "exec"), {"__name__": "readme_example"})
    except Exception as error:
        pytest.fail(f"{path}, block starting on line {line}: {type(error).__name__}: {error}", pytrace=False)


@pytest.mark.parametrize("name", list(READMES))
def test_readmes_have_their_examples(name: str) -> None:
    found = len(blocks(READMES[name]))
    assert found >= MINIMUM_BLOCKS[name], f"{READMES[name]} has {found} python block(s)"
