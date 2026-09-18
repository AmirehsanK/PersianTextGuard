# persian-text-guard

Persian text normalization and evasion-resistant profanity filtering for Python.

```python
from persian_text_guard import ProfanityFilter, WordList

filter = ProfanityFilter(WordList.persian_default())
assert filter.contains_profanity("ک.ی.ر")
assert not filter.contains_profanity("سلام، سفارشم کی میرسه؟")
assert filter.censor("kir and motherfucker") == "**** and ****"
```
