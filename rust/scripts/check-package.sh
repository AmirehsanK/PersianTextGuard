#!/usr/bin/env bash
# Checks the crate as crates.io would receive it (spec 005, contracts/package-and-release.md):
#   1. the packaged file list equals the allowlist exactly;
#   2. `cargo package` builds the unpacked crate on its own, without the repository's word lists;
#   3. the .crate is under 1 MB (SC-004);
#   4. the manifest's name, version, rust-version and single dependency;
#   5. a consumer crate builds against the unpacked crate and runs the quick start.
# Run scripts/prepare-package.sh first. Prints each check and stops at the first failure.
set -euo pipefail

cd "$(dirname "$0")/.."

fail() {
    echo "FAIL: $*" >&2
    exit 1
}

version="$(tr -d '[:space:]' < ../VERSION)"
crate="persian-text-guard-$version"

echo "1. file list"
expected="$(printf '%s\n' \
    .cargo_vcs_info.json Cargo.lock Cargo.toml Cargo.toml.orig LICENSE README.md THIRD-PARTY-NOTICES.md \
    build.rs src/bytes.rs src/errors.rs src/filter.rs src/fold.rs src/lib.rs src/normalizer.rs \
    src/regions.rs src/scan.rs src/source_map.rs src/tables.rs src/types.rs src/unicode.rs src/utf16.rs \
    src/word_list.rs wordlists/english.txt wordlists/finglish.txt wordlists/persian.txt | sort)"
# .cargo_vcs_info.json is only added when packaging from a clean Git tree, as CI does.
# Windows lists paths with backslashes.
actual="$(cargo package --locked --allow-dirty --list | tr -d '\r' | tr '\\' / | sort)"
if ! grep -qx '.cargo_vcs_info.json' <<< "$actual"; then
    expected="$(grep -vx '.cargo_vcs_info.json' <<< "$expected")"
fi
if [ "$actual" != "$expected" ]; then
    diff <(echo "$expected") <(echo "$actual") || true
    fail "the packaged files differ from the allowlist"
fi
echo "   ok: $(wc -l <<< "$actual" | tr -d ' ') files"

echo "2. package and build the unpacked crate"
cargo package --locked --allow-dirty 2>&1 | tr -d '\r' | tee target/package-output.txt | grep -E "Packaged|Verifying|Finished|warning" || true
grep -q "Finished" target/package-output.txt || fail "cargo package did not build the unpacked crate"
if grep -q '^\[\[test\]\]' "target/package/$crate/Cargo.toml"; then
    fail "the packaged Cargo.toml still has a [[test]] section"
fi
echo "   ok: built on its own; the packaged manifest has no [[test]] section"

echo "3. size"
size="$(wc -c < "target/package/$crate.crate" | tr -d ' ')"
[ "$size" -lt 1048576 ] || fail "$crate.crate is $size bytes, over 1 MB"
echo "   ok: $crate.crate is $size bytes"

echo "4. manifest"
cargo metadata --no-deps --format-version 1 --manifest-path "target/package/$crate/Cargo.toml" > target/package-metadata.json
node - "$version" <<'SCRIPT' || fail "the packaged manifest is not as expected"
const [version] = process.argv.slice(2);
const metadata = JSON.parse(require('fs').readFileSync('target/package-metadata.json', 'utf8'));
const crate = metadata.packages[0];
const normal = crate.dependencies.filter(dependency => dependency.kind === null).map(dependency => dependency.name);
const problems = [];
if (crate.name !== 'persian-text-guard') problems.push(`name ${crate.name}`);
if (crate.version !== version) problems.push(`version ${crate.version}, VERSION says ${version}`);
if (crate.rust_version !== '1.85') problems.push(`rust-version ${crate.rust_version}`);
if (normal.join() !== 'unicode-normalization') problems.push(`normal dependencies: ${normal.join(', ')}`);
for (const problem of problems) console.error(problem);
process.exit(problems.length === 0 ? 0 : 1);
SCRIPT
echo "   ok: persian-text-guard $version, rust-version 1.85, one dependency: unicode-normalization"

echo "5. consumer"
sed -i "s|^persian-text-guard = .*|persian-text-guard = { path = \"../target/package/$crate\" }|" consumer/Cargo.toml
if [ -f consumer/Cargo.lock ]; then
    (cd consumer && cargo update -p persian-text-guard --offline --quiet)
else
    (cd consumer && cargo generate-lockfile --offline --quiet)
fi
output="$(cd consumer && cargo run --locked --quiet)"
[ "$output" = "ok" ] || fail "the consumer printed '$output'"
echo "   ok: the consumer printed ok"

echo "all package checks passed"
