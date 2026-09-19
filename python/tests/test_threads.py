"""One filter shared by many threads (FR-015, SC-008, guarantees P8 and P11).

Run on free-threaded CPython with ``PYTHON_GIL=0`` too, where the threads really run in parallel.
"""

import os
import subprocess
import sys
import textwrap
import threading

import pytest

from corpus.evaluate import filter_for
from corpus.load import MATCHING_KINDS, find_repository_root, load_corpus
from corpus.values import build_input
from persian_text_guard import ProfanityFilter, ProfanityMatch

pytestmark = pytest.mark.threads

THREADS = 8
CORPUS = load_corpus(find_repository_root() / "conformance")
INPUTS = [
    (case.json["configuration"], build_input(case.json.get("input")))
    for case in CORPUS.cases
    if case.kind in MATCHING_KINDS
]

Result = tuple[bool, tuple[ProfanityMatch, ...], str]


def check(filter_: ProfanityFilter, text: str | None) -> Result:
    return filter_.contains_profanity(text), filter_.find_matches(text), filter_.censor(text)


def test_eight_threads_share_one_filter_per_configuration() -> None:
    # On free-threaded CPython with PYTHON_GIL=0 the threads below really run in parallel.
    if os.environ.get("PYTHON_GIL") == "0":
        is_gil_enabled = getattr(sys, "_is_gil_enabled", None)  # 3.13 and later
        assert is_gil_enabled is not None
        assert is_gil_enabled() is False

    filters = {name: filter_for(CORPUS, name) for name, _ in INPUTS}
    expected = [check(filters[name], text) for name, text in INPUTS]
    assert len(expected) > 300

    barrier = threading.Barrier(THREADS)
    failures: list[str] = []
    lock = threading.Lock()

    def work(worker: int) -> None:
        barrier.wait()
        for _ in range(2):
            for index, (name, text) in enumerate(INPUTS):
                actual = check(filters[name], text)
                if actual != expected[index]:
                    with lock:
                        failures.append(
                            f"thread {worker}, input {index} ({name}): {actual!r} != {expected[index]!r}"
                        )

    threads = [threading.Thread(target=work, args=(worker,)) for worker in range(THREADS)]
    for thread in threads:
        thread.start()
    for thread in threads:
        thread.join()

    assert failures == []


def test_eight_threads_get_the_same_bundled_lists_on_first_use() -> None:
    # A fresh interpreter, so the bundled lists are parsed by whichever thread comes first.
    script = textwrap.dedent(
        f"""
        import threading
        from persian_text_guard import WordList

        barrier = threading.Barrier({THREADS})
        ids = []
        lock = threading.Lock()

        def work():
            barrier.wait()
            everything, default = WordList.all(), WordList.persian_default()
            with lock:
                ids.append((id(everything), id(default)))

        threads = [threading.Thread(target=work) for _ in range({THREADS})]
        for thread in threads:
            thread.start()
        for thread in threads:
            thread.join()
        print(len(ids), len(set(ids)))
        """
    )
    result = subprocess.run([sys.executable, "-c", script], capture_output=True, text=True, check=True)
    assert result.stdout.split() == [str(THREADS), "1"]
