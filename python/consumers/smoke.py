"""The README quick start, with asserts: run against an installed package, it prints ``ok``."""

from persian_text_guard import BannedWord, ProfanityFilter, WordList, __version__, normalize

filter = ProfanityFilter(WordList.persian_default())
assert filter.contains_profanity("ک.ی.ر")
assert not filter.contains_profanity("سلام، سفارشم کی میرسه؟")
assert filter.censor("kir and motherfucker") == "**** and ****"

match = filter.find_match("\N{GRINNING FACE} کیر")
assert match is not None
assert (match.index, match.length) == (2, 3)

own = ProfanityFilter([BannedWord("اسپم"), BannedWord("casino", "anywhere")])
assert own.contains_profanity("onlinecasino.example")

assert normalize("ABC") == "abc"
assert __version__

print("ok")
