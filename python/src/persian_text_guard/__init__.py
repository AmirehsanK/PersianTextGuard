"""Persian text normalization and evasion-resistant profanity filtering.

The Python port of PersianTextGuard. It gives the same answers as the .NET and JavaScript packages for
every case in the shared conformance corpus, with positions in code points (Python string indexes).

Example:
    >>> from persian_text_guard import ProfanityFilter, WordList
    >>> filter = ProfanityFilter(WordList.persian_default())
    >>> filter.contains_profanity("ک.ی.ر")
    True
    >>> filter.censor("kir and motherfucker")
    '**** and ****'
"""

from ._filter import ProfanityFilter
from ._normalizer import normalize, to_ascii_digits, to_persian_digits, tokenize
from ._types import (
    BannedWord,
    EvasionKind,
    NormalizationPreset,
    NormalizationStep,
    NormalizationStepName,
    ProfanityFilterOptions,
    ProfanityMatch,
    WordCategory,
    WordCategoryName,
    WordListFormatError,
    WordMatchMode,
    WordMatchModeName,
)
from ._version import __version__
from ._word_list import WordList

__all__ = [
    "BannedWord",
    "EvasionKind",
    "NormalizationPreset",
    "NormalizationStep",
    "NormalizationStepName",
    "ProfanityFilter",
    "ProfanityFilterOptions",
    "ProfanityMatch",
    "WordCategory",
    "WordCategoryName",
    "WordList",
    "WordListFormatError",
    "WordMatchMode",
    "WordMatchModeName",
    "__version__",
    "normalize",
    "to_ascii_digits",
    "to_persian_digits",
    "tokenize",
]
