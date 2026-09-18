"""Exactly two type errors, each on a marked line: the consumer check expects both checkers to report them."""

# pyright: strict

from persian_text_guard import ProfanityFilterOptions, WordList

options = ProfanityFilterOptions(squeeze_repeated=False)  # expect-error: unknown-option
words = WordList.bundled("rude")  # expect-error: unknown-category
print(options, words)
