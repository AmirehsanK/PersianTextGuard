"""Guards on the corpus itself, as in .NET's ``CorpusGuardTests``: it loads, and it is complete."""

import json
import re
import shutil
from pathlib import Path

import pytest

from corpus.load import (
    MATCHING_KINDS,
    SUPPORTED_FORMAT_VERSION,
    CorpusError,
    find_repository_root,
    load_corpus,
)
from corpus.values import build_input, display

pytestmark = pytest.mark.corpus

FILL_IN_HINT = "run dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill"
DIRECTORY = find_repository_root() / "conformance"
CORPUS = load_corpus(DIRECTORY)
ID = re.compile("^[a-z0-9]+(-[a-z0-9]+)*$")


def test_the_corpus_loads() -> None:
    assert len(CORPUS.files) > 0
    assert len(CORPUS.cases) > 0


def test_the_format_version_is_one_this_runner_understands() -> None:
    assert CORPUS.metadata["formatVersion"] == SUPPORTED_FORMAT_VERSION == 1


def test_a_newer_format_version_is_refused(tmp_path: Path) -> None:
    copy = tmp_path / "conformance"
    shutil.copytree(DIRECTORY, copy)
    metadata = json.loads((copy / "corpus.json").read_text(encoding="utf-8"))
    metadata["formatVersion"] = SUPPORTED_FORMAT_VERSION + 1
    (copy / "corpus.json").write_text(json.dumps(metadata), encoding="utf-8")
    with pytest.raises(CorpusError, match="newer"):
        load_corpus(copy)


def test_a_missing_corpus_is_reported(tmp_path: Path) -> None:
    with pytest.raises(CorpusError, match="Conformance corpus not found"):
        load_corpus(tmp_path / "conformance")


def test_there_are_at_least_300_cases() -> None:
    assert len(CORPUS.cases) >= 300, f"The corpus has {len(CORPUS.cases)} cases; at least 300 are required."


def test_ids_are_unique_and_well_formed() -> None:
    seen: dict[str, list[str]] = {}
    for case in CORPUS.cases:
        seen.setdefault(case.id, []).append(case.file)

    duplicates = [f"{id_} ({', '.join(files)})" for id_, files in seen.items() if len(files) > 1]
    malformed = [f"{case.id} ({case.file})" for case in CORPUS.cases if not ID.match(case.id)]
    assert duplicates == [], "Duplicate case ids:\n" + "\n".join(duplicates)
    assert malformed == [], "Malformed case ids:\n" + "\n".join(malformed)


def test_no_case_is_pending() -> None:
    pending = [f"{case.id} ({case.file})" for case in CORPUS.cases if case.pending]
    assert pending == [], f"{len(pending)} pending case(s); {FILL_IN_HINT}:\n" + "\n".join(pending)


def test_every_configuration_exists() -> None:
    missing = [
        f"{case.id} ({case.file}): {display(case.json.get('configuration'))}"
        for case in CORPUS.cases
        if case.kind in MATCHING_KINDS
        and (
            not isinstance(case.json.get("configuration"), str)
            or case.json["configuration"] not in CORPUS.configurations
        )
    ]
    assert "default" in CORPUS.configurations
    assert missing == []


def test_no_case_is_not_applicable_to_python() -> None:
    # A Python str holds every build part, lone surrogates included, so every input must build.
    not_applicable: list[str] = []
    for case in CORPUS.cases:
        for field in ("input", "text", "mask"):
            if field in case.json:
                try:
                    build_input(case.json[field])
                except ValueError as error:
                    not_applicable.append(f"{case.id} ({case.file}): {error}")
    assert not_applicable == []
