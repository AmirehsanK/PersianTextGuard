"""Loads the conformance corpus (``conformance/``). Port of ``js/test/corpus/load.ts``.

Which ports the loading part of ``dotnet/tests/PersianTextGuard.Conformance/Corpus.cs``.
"""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Any

KINDS = (
    "ordinary",
    "must-match",
    "robustness",
    "normalization",
    "tokenization",
    "word-list-parsing",
    "category-selection",
    "mask-validation",
)
"""Every case kind format version 1 defines."""

MATCHING_KINDS = ("ordinary", "must-match", "robustness")

SUPPORTED_FORMAT_VERSION = 1
"""The newest corpus format this runner understands."""

Json = Any


class CorpusError(Exception):
    """The corpus cannot be loaded; the message names the file."""


@dataclass(frozen=True)
class CorpusCase:
    """One case, as read from its file."""

    id: str
    kind: str
    file: str
    json: dict[str, Json]
    pending: bool


@dataclass(frozen=True)
class Corpus:
    """The whole corpus."""

    directory: Path
    metadata: dict[str, Json]
    configurations: dict[str, dict[str, Json]]
    files: list[Path]
    cases: list[CorpusCase]


def find_repository_root() -> Path:
    """The nearest directory above this file with both ``VERSION`` and ``wordlists/``.

    Never the working directory. ``conformance/`` is not required here, so a missing corpus is reported
    as such.
    """
    for directory in Path(__file__).resolve().parents:
        if (directory / "VERSION").is_file() and (directory / "wordlists").is_dir():
            return directory
    raise CorpusError("Could not find the repository root (a directory with VERSION and wordlists/).")


def _read_json(path: Path) -> Json:
    if not path.is_file():
        raise CorpusError(f"{path}: file not found.")
    try:
        with path.open(encoding="utf-8") as file:
            return json.load(file)
    except (OSError, ValueError) as error:
        raise CorpusError(f"{path}: {error}") from error


def load_corpus(directory: Path) -> Corpus:
    """Read ``corpus.json``, ``configurations.json`` and ``cases/*.json``; every error names its file."""
    if not directory.exists():
        raise CorpusError(f"Conformance corpus not found: {directory}")

    metadata_path = directory / "corpus.json"
    metadata = _read_json(metadata_path)
    if not isinstance(metadata, dict):
        raise CorpusError(f"{metadata_path}: expected a JSON object.")

    version = metadata.get("formatVersion")
    if isinstance(version, int) and version > SUPPORTED_FORMAT_VERSION:
        raise CorpusError(
            f"{metadata_path}: format version {version} is newer than this runner understands "
            f"({SUPPORTED_FORMAT_VERSION})."
        )

    configurations_path = directory / "configurations.json"
    configuration_list = _read_json(configurations_path)
    if not isinstance(configuration_list, list):
        raise CorpusError(f"{configurations_path}: expected a JSON array.")

    configurations = {
        configuration["name"]: configuration
        for configuration in configuration_list
        if isinstance(configuration, dict) and isinstance(configuration.get("name"), str)
    }

    cases_directory = directory / "cases"
    # Ordinal file-name order, as every runner reads them.
    paths = (
        sorted(cases_directory.glob("*.json"), key=lambda path: path.name) if cases_directory.is_dir() else []
    )

    files: list[Path] = []
    cases: list[CorpusCase] = []

    for path in paths:
        items = _read_json(path)
        if not isinstance(items, list):
            raise CorpusError(f"{path}: expected a JSON array of cases.")

        files.append(path)
        for index, item in enumerate(items):
            if not isinstance(item, dict):
                raise CorpusError(f"{path}: element {index} is not a case object.")
            if not isinstance(item.get("id"), str):
                raise CorpusError(f'{path}: element {index} has no string "id".')
            if not isinstance(item.get("kind"), str):
                raise CorpusError(f"{path}: case '{item['id']}' has no string \"kind\".")
            if item["kind"] not in KINDS:
                raise CorpusError(f"{path}: case '{item['id']}' has unknown kind '{item['kind']}'.")

            cases.append(CorpusCase(item["id"], item["kind"], path.name, item, "expected" not in item))

    return Corpus(directory, metadata, configurations, files, cases)
