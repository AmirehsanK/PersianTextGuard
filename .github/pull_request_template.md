<!--
Thank you for contributing. Keep this short — a paragraph of "why" is worth more than a long "what",
because the diff already says what changed.

Delete any section that does not apply. The checklists below are the ones CI cannot check for you.
-->

## What and why

<!-- What does this change, and what problem does it solve? Link the issue: "Fixes #12". -->

## Type of change

- [ ] Word-list entries (`wordlists/`)
- [ ] Matching behaviour (the corpus and every port change together)
- [ ] Bug fix in one port
- [ ] Performance
- [ ] Documentation or translation
- [ ] Build, CI or tooling

## How it was checked

<!--
Which commands did you run, and what did they say? For example:

  cd rust && cargo test --locked          # 658 passed
  cd python && uv run pytest -m corpus    # 532 passed
-->

## Checklist

- [ ] CI is green, or I have said below why it cannot be yet.
- [ ] Bug fix: a test fails without this change — a **corpus case** if the bug is in matching, a port
      test if it is specific to one port.
- [ ] Documentation I touched is updated in **both** English and Persian.

### If this changes matching behaviour

- [ ] The [conformance corpus](../tree/main/conformance) is updated **in this pull request**, and every
      port matches it: .NET, JavaScript, Python and Rust.
- [ ] A new way of catching words also adds the **ordinary** messages it could plausibly catch by
      mistake (`455` alongside digit folding, «هر کس ده تا» alongside word splitting).
- [ ] Every port's corpus runner passes locally, not only in CI.
- [ ] The README's evasion table or limitations are updated if users would notice the change.

### If this touches the word lists

- [ ] The entries are different **spellings**, not typography the matcher already handles (leetspeak,
      held keys, spaced or dotted letters, accents, Persian suffixes).
- [ ] `~anywhere` is used only where every longer word containing the stem is also offensive.
- [ ] Ordinary-in-context words are in `[mild]`, and the file header says why.
- [ ] The `category-selection` corpus cases are updated for the new counts
      (`dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill -- --check` reports them).
- [ ] Entries taken from another list: the licence is MIT-compatible and credited in
      `THIRD-PARTY-NOTICES.md`.

### If this touches the per-message path

- [ ] Benchmark numbers before and after, from that port's tool (BenchmarkDotNet, tinybench, pyperf,
      Criterion):

<!--
| Benchmark | Before | After |
| --- | ---: | ---: |
| CleanShortMessage | 3.8 µs | 3.6 µs |
-->

- [ ] No new runtime dependency (test, build and benchmark dependencies are fine).
- [ ] Nothing new can throw or panic on message text.

<!--
Releases are the maintainer's job: a vX.Y.Z tag publishes every package from CI. Please do not change
VERSION in a pull request unless you were asked to.
-->
