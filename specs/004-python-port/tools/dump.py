"""Dumps the Python equivalents of the .NET primitives, in the same format as dump.cs."""
import os
import sys
import unicodedata

out = sys.argv[1]
tag = sys.argv[2]

# .NET char.IsWhiteSpace: Zs, Zl, Zp, U+0009-U+000D, U+0085, U+00A0 (the set the JS port uses).
def dotnet_ws(c: str) -> bool:
    return c in "\t\n\v\f\r\x85\xa0" or unicodedata.category(c) in ("Zs", "Zl", "Zp")

def lower_unit(c: str) -> str:
    low = c.lower()
    return low if len(low) == 1 else c

def hexs(s: str) -> str:
    return " ".join(f"{ord(ch):04X}" for ch in s)

isspace_diff = []
with open(os.path.join(out, f"{tag}-units.txt"), "w", newline="\n") as f:
    for u in range(0x10000):
        c = chr(u)
        f.write(f"{u:04X} {unicodedata.category(c)} {1 if dotnet_ws(c) else 0} {ord(lower_unit(c)):04X}\n")
        if c.isspace() != dotnet_ws(c):
            isspace_diff.append(f"{u:04X}")

with open(os.path.join(out, f"{tag}-points.txt"), "w", newline="\n") as f:
    for cp in range(0x110000):
        if 0xD800 <= cp <= 0xDFFF:
            continue
        s = chr(cp)
        f.write(f"{cp:04X} {unicodedata.category(s)} {hexs(unicodedata.normalize('NFKC', s))} | "
                f"{hexs(unicodedata.normalize('NFD', s))}\n")

print(tag, sys.version.split()[0], "unicode", unicodedata.unidata_version,
      "str.isspace differs from .NET on", isspace_diff)
