#!/usr/bin/env bash
# The public API check (spec 005, FR-022, research R10): compares the crate with the newest version on
# crates.io with cargo-semver-checks. Before the first release crates.io has no persian-text-guard, so
# there is no baseline and the check passes with a note. CI runs the same decision, with the
# cargo-semver-checks action.
#
# Needs cargo-semver-checks: cargo install cargo-semver-checks --locked
set -euo pipefail

cd "$(dirname "$0")/.."

crate="persian-text-guard"
agent="PersianTextGuard release check (https://github.com/AmirehsanK/PersianTextGuard)"
status="$(curl -s -o /dev/null -w '%{http_code}' -A "$agent" "https://crates.io/api/v1/crates/$crate")"

if [ "$status" = "404" ]; then
    echo "baseline: no previous release (crates.io has no $crate yet)"
    exit 0
fi

if [ "$status" != "200" ]; then
    echo "crates.io answered $status for $crate; cannot decide the baseline" >&2
    exit 1
fi

echo "baseline: the newest $crate on crates.io"
cargo semver-checks "$@"
