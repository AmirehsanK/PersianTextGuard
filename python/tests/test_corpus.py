"""The conformance corpus runner for the Python port (spec 004, FR-016, FR-017).

It follows the runner obligations in specs/002-monorepo-conformance-corpus/contracts/corpus-format.md:
one test per case, named by its id, so a failure names the case and the run goes on.
"""

import pytest

from corpus.evaluate import check_kind_rules, evaluate
from corpus.load import Corpus, CorpusCase, find_repository_root, load_corpus
from corpus.values import build_input, compare, display, show_text

pytestmark = pytest.mark.corpus

FILL_IN_HINT = "run dotnet run --project dotnet/tools/PersianTextGuard.CorpusFill"

# Loading errors (a missing or unreadable corpus) fail here, at collection, and name the file.
CORPUS: Corpus = load_corpus(find_repository_root() / "conformance")


def describe_input(case: CorpusCase) -> str:
    if case.kind == "word-list-parsing":
        return f"text {show_text(build_input(case.json.get('text')))}"
    if case.kind == "category-selection":
        return f"selection {display(case.json.get('selection'))}"
    if case.kind == "mask-validation":
        return f"mask {show_text(build_input(case.json.get('mask')))}"
    return f"input {show_text(build_input(case.json.get('input')))}"


def fail(case: CorpusCase, problem: str, lines: list[str]) -> None:
    message = "\n".join(
        [f"Case '{case.id}' in {case.file}: {problem}", f"  {describe_input(case)}"]
        + [f"  {line}" for line in lines]
    )
    pytest.fail(message, pytrace=False)


@pytest.mark.parametrize("case", CORPUS.cases, ids=[case.id for case in CORPUS.cases])
def test_case(case: CorpusCase) -> None:
    if case.pending:
        fail(case, f"pending case - {FILL_IN_HINT}", [])

    violations = check_kind_rules(case)
    if violations:
        fail(case, "breaks its kind rule", violations)

    differences = compare(case.json.get("expected"), evaluate(CORPUS, case))
    if differences:
        fail(
            case,
            f"{len(differences)} field(s) differ",
            [f"{d.path}: expected {d.expected} actual {d.actual}" for d in differences],
        )
