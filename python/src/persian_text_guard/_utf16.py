r"""The UTF-16 view the matcher runs on, and the mapping of its positions back to code points.

.NET reads text as UTF-16 code units, and decides tokens by testing single units: half of a surrogate
pair is not a letter. Python strings are code points, so a port that tested them directly would read
U+20000 (a CJK letter) as one letter where .NET sees two surrogate units, and would find different
words (spec 004, research R2). For example, .NET flags ``kir𠀀`` as ``kir`` at UTF-16 index 0,
length 5, and censors it to ``****``: the two surrogates are not letters, so they join the word.

So every public entry point converts the caller's text to a *view*: a ``str`` in which each code
point above U+FFFF is replaced by its two surrogate code points. Inside the package, ``text[i]`` is
then UTF-16 unit *i*, exactly as ``charCodeAt(i)`` is in the JavaScript port, and ``len(text)`` is the
unit count. The view never leaves the package:

- match positions are converted back to code points of the caller's string with :class:`PositionMap`
  (``kir𠀀`` gives index 0, length 4);
- ``censor`` splices the caller's own string, so text outside a match is returned exactly as given,
  including surrogates the caller split themselves;
- normalizer and tokenizer output is converted with :func:`from_view`, which recombines every valid
  surrogate pair. There the caller's own split pair ``"\ud840\udc00"`` becomes the one code point
  U+20000, which is what .NET does when its UTF-16 result is decoded.
"""

from __future__ import annotations

import re

from ._unicode import recombine, split_supplementary

SUPPLEMENTARY = re.compile("[\U00010000-\U0010ffff]")


def to_view(text: str) -> str:
    """The UTF-16 view of ``text``: ``text`` itself unless it has a code point above U+FFFF."""
    if text.isascii() or SUPPLEMENTARY.search(text) is None:
        return text
    return split_supplementary(text)


def from_view(view: str) -> str:
    """A view turned back into code points: every valid surrogate pair becomes one code point."""
    return recombine(view)


class PositionMap:
    """Converts positions in a view to positions in the caller's string.

    It is the identity when the view is the caller's string. Otherwise it holds, for each unit index of
    the view, the code point index of the character it belongs to; the second unit of a pair maps to
    its pair's start.
    """

    __slots__ = ("_starts",)

    _starts: list[int] | None

    def __init__(self, text: str, view: str) -> None:
        """Build the map for ``text`` and its ``view``."""
        if view is text or len(view) == len(text):
            self._starts = None
            return

        # Each code point contributes one entry per unit, each equal to its own index.
        starts: list[int] = []
        for index, c in enumerate(text):
            starts.append(index)
            if c > "\U0000ffff":
                starts.append(index)
        self._starts = starts

    def to_code_points(self, unit_index: int, unit_length: int) -> tuple[int, int]:
        """The ``(index, length)`` in code points of the view region ``(unit_index, unit_length)``."""
        starts = self._starts
        if starts is None:
            return unit_index, unit_length
        start = starts[unit_index]
        end = starts[unit_index + unit_length - 1] + 1
        return start, end - start
