"""The CI regression gate: ``CleanShortMessage`` must stay under a limit (research R3).

Runs ``bench/bench_filter.py --fast --only CleanShortMessage`` and fails when its mean exceeds the
limit: 500 µs by default, the CI gate, which leaves headroom for shared runners; ``--limit-us 250``
applies SC-005's own value, which is checked on the machine the README names.
"""

from __future__ import annotations

import argparse
import subprocess
import sys
import tempfile
from pathlib import Path

import pyperf

HERE = Path(__file__).resolve().parent.parent
BENCHMARK = "CleanShortMessage"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--limit-us", type=float, default=500.0, help="the limit in microseconds (default 500)"
    )
    args = parser.parse_args()

    with tempfile.TemporaryDirectory() as directory:
        output = Path(directory) / "gate.json"
        subprocess.run(
            [
                sys.executable,
                str(HERE / "bench" / "bench_filter.py"),
                "--fast",
                "--quiet",
                "--only",
                BENCHMARK,
                "-o",
                str(output),
            ],
            check=True,
        )
        mean_us = pyperf.Benchmark.load(str(output)).mean() * 1e6

    ok = mean_us <= args.limit_us
    print(f"{'ok  ' if ok else 'FAIL'} {BENCHMARK}: mean {mean_us:.1f} µs, limit {args.limit_us:.0f} µs")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
