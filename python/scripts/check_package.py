"""Checks the built wheel and sdist in ``dist/`` (spec 004, research R12, contracts "Package contents").

1. Exactly one wheel and one sdist.
2. ``twine check --strict`` on both, and ``check-wheel-contents`` on the wheel.
3. Their file lists equal the allowlists exactly.
4. The metadata: name, PEP 440 version of ``VERSION``, ``Requires-Python``, no dependencies, licence.
5. The unpacked wheel is under 1 MB (SC-004).

Prints each check and exits non-zero on the first failure. Run it from ``python/`` after ``uv build``.
"""

from __future__ import annotations

import subprocess
import sys
import tarfile
import zipfile
from email.parser import Parser
from pathlib import Path

from packaging.version import Version

HERE = Path(__file__).resolve().parent.parent
DIST = HERE / "dist"
ROOT = HERE.parent

MODULES = (
    "__init__.py",
    "_types.py",
    "_unicode.py",
    "_utf16.py",
    "_normalizer.py",
    "_source_map.py",
    "_filter.py",
    "_scan.py",
    "_regions.py",
    "_fold.py",
    "_word_list.py",
    "_wordlists.py",
    "_version.py",
    "py.typed",
)
MAX_UNPACKED_BYTES = 1_000_000


class CheckError(Exception):
    """A failed check."""


def check(name: str, ok: bool, detail: str = "") -> None:
    print(f"{'ok  ' if ok else 'FAIL'} {name}{f': {detail}' if detail else ''}")
    if not ok:
        raise CheckError(name)


def run(*args: str) -> None:
    result = subprocess.run([sys.executable, "-m", *args], capture_output=True, text=True, check=False)
    output = (result.stdout + result.stderr).strip()
    check(" ".join(args[:1] + args[1:2]), result.returncode == 0, output.splitlines()[-1] if output else "")


def main() -> int:
    wheels = sorted(DIST.glob("*.whl"))
    sdists = sorted(DIST.glob("*.tar.gz"))
    check("one wheel and one sdist", len(wheels) == 1 and len(sdists) == 1, f"{wheels + sdists}")
    wheel, sdist = wheels[0], sdists[0]

    version = str(Version((ROOT / "VERSION").read_text(encoding="utf-8").strip()))
    check(
        "file names carry the version",
        wheel.name == f"persian_text_guard-{version}-py3-none-any.whl"
        and sdist.name == f"persian_text_guard-{version}.tar.gz",
        f"{wheel.name}, {sdist.name}",
    )

    run("twine", "check", "--strict", str(wheel), str(sdist))
    run("check_wheel_contents", str(wheel))

    dist_info = f"persian_text_guard-{version}.dist-info"
    expected_wheel = {f"persian_text_guard/{name}" for name in MODULES} | {
        f"{dist_info}/{name}"
        for name in ("METADATA", "WHEEL", "RECORD", "licenses/LICENSE", "licenses/THIRD-PARTY-NOTICES.md")
    }
    with zipfile.ZipFile(wheel) as archive:
        infos = archive.infolist()
        actual_wheel = {info.filename for info in infos}
        metadata_text = archive.read(f"{dist_info}/METADATA").decode("utf-8")
    check(
        "wheel files equal the allowlist",
        actual_wheel == expected_wheel,
        f"extra {sorted(actual_wheel - expected_wheel)}, missing {sorted(expected_wheel - actual_wheel)}",
    )

    prefix = f"persian_text_guard-{version}"
    expected_sdist = {f"{prefix}/src/persian_text_guard/{name}" for name in MODULES} | {
        f"{prefix}/{name}"
        for name in (
            "pyproject.toml",
            "hatch_build.py",
            "README.md",
            "LICENSE",
            "THIRD-PARTY-NOTICES.md",
            "PKG-INFO",
        )
    }
    with tarfile.open(sdist) as archive:
        actual_sdist = {member.name for member in archive.getmembers() if member.isfile()}
    check(
        "sdist files equal the allowlist",
        actual_sdist == expected_sdist,
        f"extra {sorted(actual_sdist - expected_sdist)}, missing {sorted(expected_sdist - actual_sdist)}",
    )

    metadata = Parser().parsestr(metadata_text)
    check("Name", metadata["Name"] == "persian-text-guard", metadata["Name"])
    check("Version", metadata["Version"] == version, metadata["Version"])
    check("Requires-Python", metadata["Requires-Python"] == ">=3.11", metadata["Requires-Python"])
    check(
        "no Requires-Dist", metadata.get_all("Requires-Dist") is None, str(metadata.get_all("Requires-Dist"))
    )
    check("License-Expression", metadata["License-Expression"] == "MIT", metadata["License-Expression"])
    licenses = sorted(metadata.get_all("License-File") or [])
    check("License-File", licenses == ["LICENSE", "THIRD-PARTY-NOTICES.md"], str(licenses))
    check(
        "Description-Content-Type",
        metadata["Description-Content-Type"] == "text/markdown",
        metadata["Description-Content-Type"],
    )

    unpacked = sum(info.file_size for info in infos)
    check("unpacked wheel under 1 MB", unpacked < MAX_UNPACKED_BYTES, f"{unpacked:,} bytes")

    print(f"check_package: {wheel.name} and {sdist.name} passed")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except CheckError:
        sys.exit(1)
