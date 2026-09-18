"""Prints the README's performance table from a pyperf JSON file (research R11).

uv run python scripts/bench_table.py .bench/results.json
"""

from __future__ import annotations

import sys

import pyperf

OPERATIONS = {
    "CleanShortMessage": "Short clean message (5 words)",
    "CleanLongMessage": "Long clean message (60 words)",
    "EvasiveMessage": "Message with evasions",
    "NormalizeLongMessage": "Normalize a long message",
    "BuildFilterFromDefaultList": "Build a filter from the bundled list",
    "FindMatchesClean": "`find_matches`, clean short message",
    "FindMatchesDirty": "`find_matches`, message with three banned words",
    "CensorShortDirty": "`censor`, short message with one banned word",
    "CensorLongDirty": "`censor`, 60-word message with three banned words",
    "VeryLongMessage": "A 132,000-character message",
}


def _format(seconds: float) -> str:
    ms = seconds * 1000
    if ms >= 1:
        return f"{ms:.0f} ms" if ms >= 100 else f"{ms:.1f} ms"
    us = ms * 1000
    return f"{us:.0f} µs" if us >= 100 else f"{us:.1f} µs"


def main() -> int:
    if len(sys.argv) != 2:
        print("usage: bench_table.py RESULTS.json")
        return 2

    suite = pyperf.BenchmarkSuite.load(sys.argv[1])
    benchmarks = suite.get_benchmarks()
    metadata = benchmarks[0].get_metadata()
    implementation = metadata.get("python_implementation", "cpython")
    python = "CPython" if implementation == "cpython" else implementation
    version = metadata.get("python_version", "?")
    cpu = metadata.get("cpu_model_name", "unknown CPU")
    print(f"{python} {version}, {cpu}\n")
    print("| Operation | Mean | Operations/s |")
    print("| --- | ---: | ---: |")
    for benchmark in benchmarks:
        mean = benchmark.mean()
        name = benchmark.get_name()
        print(f"| {OPERATIONS.get(name, name)} | {_format(mean)} | {round(1 / mean):,} |")
    return 0


if __name__ == "__main__":
    sys.exit(main())
