"""Benchmarks for the Python port with pyperf (constitution Principle IV, research R11).

Mirrors ``dotnet/benchmarks/PersianTextGuard.Benchmarks/Program.cs`` and ``js/bench/filter.bench.ts``:
the same ten operations, with the same names, and the messages copied verbatim.

    uv run python bench/bench_filter.py -o .bench/results.json
    uv run python scripts/bench_table.py .bench/results.json

``--only NAME`` runs one benchmark (repeatable), which ``scripts/bench_gate.py`` uses.
"""

from __future__ import annotations

import argparse
from collections.abc import Callable

import pyperf

from persian_text_guard import ProfanityFilter, WordList, normalize

CleanShort = "سلام، سفارشم کی ارسال میشه؟"
CleanLong = "سلام وقت بخیر. من هفته پیش یک گوشی از فروشگاه شما سفارش دادم و هنوز به دستم نرسیده. کد رهگیری را هم در پنل کاربری پیدا نکردم. لطفا بررسی کنید و نتیجه را از طریق ایمیل یا پیامک اطلاع بدید. اگر امکانش هست هزینه ارسال را هم برگردانید. ممنون از پشتیبانی خوبتان."  # noqa: E501
Evasion = "ye k0s kesh inja f.u.c.k"
DirtyShort = "this is kir"
DirtyMixed = "این کیر و f u c k و sh1t"
DirtyLong = "سلام وقت بخیر. کیر من هفته پیش یک گوشی از فروشگاه شما سفارش دادم و هنوز به دستم نرسیده. f.u.c.k کد رهگیری را هم در پنل کاربری پیدا نکردم. جنده‌ها لطفا بررسی کنید و نتیجه را از طریق ایمیل یا پیامک اطلاع بدید. اگر امکانش هست هزینه ارسال را هم برگردانید. ممنون از پشتیبانی خوبتان."  # noqa: E501

# The corpus's 132,000-character message with a banned word at the end (spec: very long messages).
VeryLong = "سلام این یک متن معمولی است و هیچ مشکلی ندارد. hello this is fine. " * 2000 + " کیر"

FILTER = ProfanityFilter(WordList.persian_default())

BENCHMARKS: dict[str, Callable[[], object]] = {
    "CleanShortMessage": lambda: FILTER.contains_profanity(CleanShort),
    "CleanLongMessage": lambda: FILTER.contains_profanity(CleanLong),
    "EvasiveMessage": lambda: FILTER.contains_profanity(Evasion),
    "NormalizeLongMessage": lambda: normalize(CleanLong),
    "BuildFilterFromDefaultList": lambda: ProfanityFilter(WordList.persian_default()).count,
    "FindMatchesClean": lambda: len(FILTER.find_matches(CleanShort)),
    "FindMatchesDirty": lambda: len(FILTER.find_matches(DirtyMixed)),
    "CensorShortDirty": lambda: FILTER.censor(DirtyShort),
    "CensorLongDirty": lambda: FILTER.censor(DirtyLong),
    "VeryLongMessage": lambda: FILTER.contains_profanity(VeryLong),
}


def _pass_only(command: list[str], args: argparse.Namespace) -> None:
    for name in args.only or []:
        command.extend(["--only", name])


def main() -> None:
    runner = pyperf.Runner(add_cmdline_args=_pass_only)
    runner.argparser.add_argument(
        "--only", action="append", choices=list(BENCHMARKS), help="run only this one"
    )
    args = runner.parse_args()
    for name, function in BENCHMARKS.items():
        if not args.only or name in args.only:
            runner.bench_func(name, function)


if __name__ == "__main__":
    main()
