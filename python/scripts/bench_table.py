"""Prints the README's performance table from a pyperf JSON file (research R11).

uv run python scripts/bench_table.py .bench/results.json
"""

from __future__ import annotations

import sys

import pyperf


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
    python = metadata.get("python_implementation", "cpython")
    version = metadata.get("python_version", "?")
    cpu = metadata.get("cpu_model_name", "unknown CPU")
    print(f"{python} {version}, {cpu}\n")
    print("| Operation | Mean | Operations/s |")
    print("| --- | ---: | ---: |")
    for benchmark in benchmarks:
        mean = benchmark.mean()
        print(f"| {benchmark.get_name()} | {_format(mean)} | {round(1 / mean):,} |")
    return 0


if __name__ == "__main__":
    sys.exit(main())
