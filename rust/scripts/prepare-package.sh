#!/usr/bin/env bash
# Copies the shared files the published crate carries into rust/: the three word lists, the licence and
# the third-party notices. The copies are git-ignored; the repository keeps one copy of each, at the root
# (spec 005, research R5). Run before `cargo package` or `cargo publish`.
set -euo pipefail

cd "$(dirname "$0")/.."

mkdir -p wordlists
for list in persian finglish english; do
    cp "../wordlists/$list.txt" "wordlists/$list.txt"
    echo "copied wordlists/$list.txt"
done

for file in LICENSE THIRD-PARTY-NOTICES.md; do
    cp "../$file" "$file"
    echo "copied $file"
done
