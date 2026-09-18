"""Installs the built package the way users do and uses it (spec 004, research R12, SC-007).

1. Makes two clean virtual environments in ``consumers/.envs/``.
2. Installs ``dist/*.whl`` into the first. For the second, builds a wheel from the sdist alone with
   ``uv build --wheel``, which unpacks it in isolation, so the sdist is proven to need nothing from the
   repository, then installs that wheel. Both installs are ``pip --no-deps --no-index``.
3. Runs ``consumers/smoke.py`` in each and expects ``ok``.
4. Runs ``pyright`` and ``mypy``, both strict, on ``consumers/typed_usage.py`` against the wheel
   environment, and expects no errors.
5. Runs both on ``consumers/type_errors.py`` and expects exactly the errors marked ``# expect-error``,
   on the marked lines.

Run it from ``python/`` after ``uv build``. It prints 4 of 4 checks.
"""

from __future__ import annotations

import json
import os
import re
import shutil
import subprocess
import sys
import venv
from pathlib import Path

HERE = Path(__file__).resolve().parent.parent
DIST = HERE / "dist"
CONSUMERS = HERE / "consumers"
ENVS = CONSUMERS / ".envs"


class CheckError(Exception):
    """A failed check."""


def run(args: list[str | Path], cwd: Path = HERE) -> subprocess.CompletedProcess[str]:
    environment = {
        key: value for key, value in os.environ.items() if key not in ("PYTHONPATH", "VIRTUAL_ENV")
    }
    environment["PYTHONIOENCODING"] = "utf-8"
    return subprocess.run(
        [str(arg) for arg in args],
        cwd=cwd,
        capture_output=True,
        text=True,
        encoding="utf-8",
        check=False,
        env=environment,
    )


def run_ok(args: list[str | Path], what: str) -> str:
    result = run(args)
    if result.returncode != 0:
        print(result.stdout, result.stderr, sep="\n")
        raise CheckError(what)
    return result.stdout


def python_of(environment: Path) -> Path:
    scripts = environment / ("Scripts" if os.name == "nt" else "bin")
    return scripts / ("python.exe" if os.name == "nt" else "python")


def make_environment(name: str) -> Path:
    path = ENVS / name
    shutil.rmtree(path, ignore_errors=True)
    venv.EnvBuilder(with_pip=True, clear=True).create(path)
    return python_of(path)


def smoke(python: Path, what: str) -> bool:
    result = run([python, CONSUMERS / "smoke.py"], cwd=CONSUMERS)
    ok = result.returncode == 0 and result.stdout.strip() == "ok"
    print(f"{'ok  ' if ok else 'FAIL'} {what}: {(result.stdout + result.stderr).strip()}")
    return ok


def marked_lines(path: Path) -> set[int]:
    lines = path.read_text(encoding="utf-8").splitlines()
    return {number for number, line in enumerate(lines, start=1) if "# expect-error:" in line}


def mypy_errors(python: Path, path: Path) -> set[int]:
    result = run(
        [sys.executable, "-m", "mypy", "--strict", "--no-incremental", "--python-executable", python, path]
    )
    lines = set()
    for line in result.stdout.splitlines():
        match = re.match(r"^.+?:(\d+): error:", line)
        if match:
            lines.add(int(match.group(1)))
    if result.returncode not in (0, 1):
        print(result.stdout, result.stderr)
        raise CheckError("mypy did not run")
    return lines


def pyright_errors(python: Path, path: Path) -> set[int]:
    result = run([sys.executable, "-m", "pyright", "--outputjson", "--pythonpath", python, path])
    try:
        report = json.loads(result.stdout)
    except json.JSONDecodeError:
        print(result.stdout, result.stderr)
        raise CheckError("pyright did not run") from None
    return {
        diagnostic["range"]["start"]["line"] + 1
        for diagnostic in report["generalDiagnostics"]
        if diagnostic["severity"] == "error"
    }


def main() -> int:
    wheels = sorted(DIST.glob("*.whl"))
    sdists = sorted(DIST.glob("*.tar.gz"))
    if len(wheels) != 1 or len(sdists) != 1:
        print(f"FAIL expected one wheel and one sdist in {DIST}, found {wheels + sdists}")
        return 1

    ENVS.mkdir(parents=True, exist_ok=True)
    results: list[bool] = []

    wheel_python = make_environment("wheel")
    run_ok([wheel_python, "-m", "pip", "install", "--no-deps", "--no-index", wheels[0]], "install the wheel")
    results.append(smoke(wheel_python, f"wheel smoke ({wheels[0].name})"))

    from_sdist = ENVS / "from-sdist"
    shutil.rmtree(from_sdist, ignore_errors=True)
    run_ok(["uv", "build", "--wheel", sdists[0], "-o", from_sdist], "build a wheel from the sdist alone")
    built = sorted(from_sdist.glob("*.whl"))
    sdist_python = make_environment("sdist")
    run_ok(
        [sdist_python, "-m", "pip", "install", "--no-deps", "--no-index", *built], "install the sdist's wheel"
    )
    results.append(smoke(sdist_python, f"sdist smoke ({sdists[0].name})"))

    typed = CONSUMERS / "typed_usage.py"
    mypy_typed = mypy_errors(wheel_python, typed)
    pyright_typed = pyright_errors(wheel_python, typed)
    ok = not mypy_typed and not pyright_typed
    print(
        f"{'ok  ' if ok else 'FAIL'} typed usage: mypy errors on {sorted(mypy_typed)}, "
        f"pyright errors on {sorted(pyright_typed)}"
    )
    results.append(ok)

    errors = CONSUMERS / "type_errors.py"
    expected = marked_lines(errors)
    mypy_found = mypy_errors(wheel_python, errors)
    pyright_found = pyright_errors(wheel_python, errors)
    ok = len(expected) == 2 and mypy_found == expected and pyright_found == expected
    print(
        f"{'ok  ' if ok else 'FAIL'} type errors: expected lines {sorted(expected)}, "
        f"mypy {sorted(mypy_found)}, pyright {sorted(pyright_found)}"
    )
    results.append(ok)

    print(f"check_consumers: {sum(results)} of {len(results)} checks passed")
    return 0 if all(results) else 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except CheckError as error:
        print(f"FAIL {error}")
        sys.exit(1)
