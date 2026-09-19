#!/usr/bin/env bash
# Sets the crate's version in Cargo.toml to the repository's VERSION file, and updates every lock file
# that records it (rust/, rust/bench/, rust/consumer/). build.rs refuses to build when the two differ,
# so run this after changing VERSION (spec 005, research R5).
set -euo pipefail

cd "$(dirname "$0")/.."

version="$(tr -d '[:space:]' < ../VERSION)"
current="$(sed -n 's/^version = "\(.*\)"$/\1/p' Cargo.toml | head -n 1)"

if [ "$current" = "$version" ]; then
    echo "no change: Cargo.toml is already $version"
else
    # Only the first version line, the [package] one.
    sed -i "0,/^version = \".*\"$/s//version = \"$version\"/" Cargo.toml
    echo "Cargo.toml: $current -> $version"
fi

for directory in . bench consumer; do
    if [ -f "$directory/Cargo.toml" ] && [ -f "$directory/Cargo.lock" ]; then
        (cd "$directory" && cargo update -p persian-text-guard --offline --quiet)
    fi
done
