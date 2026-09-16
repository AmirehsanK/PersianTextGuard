# Third-party notices

The code in PersianTextGuard is MIT licensed (see [LICENSE](LICENSE)).

The bundled word lists in `src/PersianTextGuard/WordLists/` were curated by
hand: entries were selected, removed and added (including Finglish and ص/ث spellings that
none of the sources carry). They draw on the following lists, credited here under their
licenses.

## List of Dirty, Naughty, Obscene, and Otherwise Bad Words (LDNOOBW)

- Source: https://github.com/LDNOOBW/List-of-Dirty-Naughty-Obscene-and-Otherwise-Bad-Words
- License: Creative Commons Attribution 4.0 International (CC BY 4.0),
  https://creativecommons.org/licenses/by/4.0/
- Changes: entries selected and modified; see the header of the word list for what was left
  out and why.

## LDNOOBW V2

- Source: https://github.com/LDNOOBWV2/List-of-Dirty-Naughty-Obscene-and-Otherwise-Bad-Words_V2
- License: CC0 1.0 Universal, https://creativecommons.org/publicdomain/zero/1.0/

## Persian-Swear-Words

- Source: https://github.com/amirshnll/Persian-Swear-Words
- License: Apache License 2.0, https://www.apache.org/licenses/LICENSE-2.0
- Changes: entries selected, normalized, and modified.

## profanity-list

- Source: https://github.com/dsojevic/profanity-list
- License: MIT License

## The Obscenity List (Surge AI)

- Source: https://github.com/surge-ai/profanity (mirrored at
  https://huggingface.co/datasets/mmathys/profanity)
- License: MIT License
- Changes: entries selected; the bundled English list follows its categories and severity
  ratings, with its `Mild` severity mapped to `WordCategory.Mild`.

## persian-bad-words

- Source: https://github.com/kaveh-dev/persian-bad-words
- License: MIT License
- Changes: entries selected and normalized.
