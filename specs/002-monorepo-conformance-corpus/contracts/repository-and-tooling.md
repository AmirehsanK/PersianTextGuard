# Contract: Repository Layout, Version Source, Fill-in Tool and CI

**Feature**: [../spec.md](../spec.md) | **Research**: [../research.md](../research.md)

Contributors, the future port features and CI all rely on these paths, commands and exit codes.

## Layout

```text
/
├── VERSION                         # the only version value, e.g. "1.2.0\n"
├── wordlists/
│   ├── persian.txt
│   ├── finglish.txt
│   └── english.txt
├── conformance/                    # corpus-format.md
├── dotnet/
│   ├── PersianTextGuard.slnx
│   ├── Directory.Build.props
│   ├── src/PersianTextGuard/
│   ├── tests/PersianTextGuard.Tests/
│   ├── tests/PersianTextGuard.Conformance/
│   ├── benchmarks/PersianTextGuard.Benchmarks/
│   └── tools/PersianTextGuard.CorpusFill/
├── README.md  LICENSE  THIRD-PARTY-NOTICES.md  icon.png
├── .github/workflows/ci.yml
└── .specify/  .claude/  specs/
```

**Guarantees**:
- **Nothing is left at the old paths.** `src/`, `tests/`, `benchmarks/`, the root
  `PersianTextGuard.slnx` and the root `Directory.Build.props` do not exist after the restructure.
- **Future ports** add `js/`, `python/`, `java/`, `go/` and `rust/` next to `dotnet/`, and read
  `wordlists/`, `conformance/` and `VERSION` from the root.

## Version source

| Aspect | Contract |
| --- | --- |
| File | `VERSION`: one Semantic Version and a trailing newline |
| .NET | `dotnet/Directory.Build.props` sets `<Version>` from the trimmed file contents; no project sets `<Version>` itself |
| Release tag | Must equal `v` + `VERSION`; CI fails the release otherwise |

## Commands

Every command runs from the repository root.

| Purpose | Command | Result |
| --- | --- | --- |
| Build everything | `dotnet build dotnet/PersianTextGuard.slnx` | builds the library, tests, runner, benchmarks and tool |
| Existing tests | `dotnet test dotnet/tests/PersianTextGuard.Tests` | 1,029 passed on each target |
| Corpus, .NET | `dotnet test dotnet/tests/PersianTextGuard.Conformance` | every case passed on each target |
| Both | `dotnet test dotnet/PersianTextGuard.slnx` | |
| Benchmarks | `dotnet run -c Release --project dotnet/benchmarks/PersianTextGuard.Benchmarks -f net10.0` | |
| Pack | `dotnet pack dotnet/src/PersianTextGuard -c Release -o artifacts` | `PersianTextGuard.<VERSION>.nupkg`, validated against 1.2.0 |
| Fill in pending cases | `dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill` | see below |
| Seed the corpus (once) | `dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill -- --seed` | see below |

## Fill-in tool

| Mode | Behaviour | Writes | Exit code |
| --- | --- | --- | --- |
| *(default)* | Fills `expected` for pending cases from the .NET package; compares every recorded case and prints each disagreement (id, field, expected, actual) | only the files that had pending cases, in canonical form | `0` when no recorded case disagrees; `1` otherwise |
| `--check` | Same comparison, fills nothing | nothing | `0` / `1` as above |
| `--seed` | Creates pending cases from the existing .NET tests, explicit literals and the supplementary suite (research R9). It skips any case whose **content key** (configuration, built input and masks, or the equivalent for other kinds) already exists, whatever its id, then fills them exactly as the default mode does | the case files it creates (starting from `[]`) or extends | same as default mode: `0` when no recorded case disagrees, `1` otherwise |

**Guarantees**:
- An existing `expected` is never modified in any mode (spec FR-010a, Clarification 2).
- Output follows the writing rules of `corpus-format.md`.
- The tool resolves `conformance/` relative to the repository root, not the working directory, and
  fails with a clear message when it cannot find it.

## CI

`.github/workflows/ci.yml` keeps its job names.

| Job | Runs on | Steps |
| --- | --- | --- |
| `Build, test, pack` | ubuntu | restore and build `dotnet/PersianTextGuard.slnx`; test existing tests and corpus on `net8.0` and `net10.0`; pack `dotnet/src/PersianTextGuard` with validation against 1.2.0; upload artifacts |
| `Test on .NET Framework 4.8 (netstandard2.0 build)` | windows | test existing tests and corpus on `net48` |
| `Publish to NuGet` | ubuntu | on `v*` tags only: check the tag equals `v` + `VERSION`, then push the `.nupkg` as today |

A pull request merges only when the first two jobs are green (constitution, Development Workflow).
