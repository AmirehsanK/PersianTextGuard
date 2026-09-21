#!/usr/bin/env bash
# The CI benchmark gate (spec 005, research R9): runs the short clean message benchmark and fails if its
# mean is over the limit. The limit is 50 µs on shared runners, ten times SC-005's 5 µs target, which is
# checked on the maintainer's machine with `--limit-us 5`.
set -euo pipefail

cd "$(dirname "$0")/../bench"

limit="${1:-50}"

cargo bench --locked --bench filter -- CleanShortMessage --warm-up-time 1 --measurement-time 3
cargo run --locked --release --bin bench_table -- --gate CleanShortMessage --limit-us "$limit"
