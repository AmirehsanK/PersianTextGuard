"""API compatibility of ``persian_text_guard`` against the previous release (FR-022, research R14).

Finds the newest ``v*`` tag whose tree has the Python package, and runs ``griffe check`` against it.

- With no such tag, it prints "baseline: no previous release" and passes: the first release is the
  baseline.
- A breaking change fails, unless ``VERSION``'s major version is greater than the tag's.
- ``--against REF`` checks against any Git reference instead (a tag, a branch or ``HEAD~1``).

Run it from ``python/``.
"""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent.parent
ROOT = HERE.parent
PACKAGE_INIT = "python/src/persian_text_guard/__init__.py"
TAG_VERSION = re.compile(r"^v(\d+)\.\d+\.\d+")


def git(*args: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run(["git", *args], cwd=ROOT, capture_output=True, text=True, check=False)


def previous_release() -> str | None:
    tags = git("tag", "--list", "v*", "--sort=-v:refname").stdout.split()
    for tag in tags:
        if git("cat-file", "-e", f"{tag}:{PACKAGE_INIT}").returncode == 0:
            return tag
    return None


def major_of(version: str) -> int | None:
    match = re.match(r"^v?(\d+)\.", version.strip())
    return int(match.group(1)) if match else None


def main() -> int:
    parser = argparse.ArgumentParser(
        description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter
    )
    parser.add_argument("--against", metavar="REF", help="the Git reference to compare with")
    args = parser.parse_args()

    against: str | None = args.against or previous_release()
    if against is None:
        print("baseline: no previous release has python/, so there is nothing to compare with")
        return 0

    print(f"griffe check persian_text_guard against {against}")
    result = subprocess.run(
        [
            sys.executable,
            "-m",
            "griffe",
            "check",
            "persian_text_guard",
            "--search",
            "python/src",
            "--against",
            against,
            "--verbose",
        ],
        # From the repository root: griffe reads the old reference from a worktree of the repository,
        # and resolves the search path inside it as well as here.
        cwd=ROOT,
        check=False,
    )
    if result.returncode == 0:
        print(f"ok: no breaking change against {against}")
        return 0

    current = major_of((ROOT / "VERSION").read_text(encoding="utf-8"))
    previous = major_of(against)
    if current is not None and previous is not None and current > previous:
        print(f"ok: breaking changes allowed, VERSION major {current} > {against} major {previous}")
        return 0

    print(f"FAIL: griffe reported breaking changes against {against}, or could not run,")
    print("and VERSION has the same major version")
    return 1


if __name__ == "__main__":
    sys.exit(main())
