"""Compares each Python dump with the .NET dump, per primitive, and summarises the differences."""
import os
import sys
import unicodedata

d = sys.argv[1]


def load(name):
    with open(os.path.join(d, name), encoding="utf-8") as f:
        return [line.rstrip("\n") for line in f]


def ranges(cps):
    out, start, prev = [], None, None
    for cp in cps:
        if start is None:
            start = prev = cp
        elif cp == prev + 1:
            prev = cp
        else:
            out.append((start, prev))
            start = prev = cp
    if start is not None:
        out.append((start, prev))
    return ", ".join(f"{a:04X}" if a == b else f"{a:04X}-{b:04X}" for a, b in out[:12]) + (" ..." if len(out) > 12 else "")


dn_units = [line.split(" ") for line in load("dotnet-units.txt")]
dn_points = {}
for line in load("dotnet-points.txt"):
    head, nfd = line.split(" | ")
    cp, cat, nfkc = head.split(" ", 2)
    dn_points[cp] = (cat, nfkc, nfd)

for tag in ["py3.11", "py3.12", "py3.13", "py3.14", "py3.14t"]:
    units = [line.split(" ") for line in load(f"{tag}-units.txt")]
    cat_u, ws_u, low_u = [], [], []
    for a, b in zip(dn_units, units):
        u = int(a[0], 16)
        if a[1] != b[1]:
            cat_u.append(u)
        if a[2] != b[2]:
            ws_u.append(u)
        if a[3] != b[3]:
            low_u.append(u)
    cat_p, nfkc_p, nfd_p, throws = [], [], [], []
    newer_in_dotnet = older_in_dotnet = 0
    for line in load(f"{tag}-points.txt"):
        head, nfd = line.split(" | ")
        cp, cat, nfkc = head.split(" ", 2)
        dcat, dnfkc, dnfd = dn_points[cp]
        c = int(cp, 16)
        if dcat != cat:
            cat_p.append(c)
            if cat == "Cn":
                newer_in_dotnet += 1
            elif dcat == "Cn":
                older_in_dotnet += 1
        if dnfkc == "THROWS":
            throws.append(c)
        elif dnfkc != nfkc:
            nfkc_p.append(c)
        if dnfd != "THROWS" and dnfd != nfd:
            nfd_p.append(c)
    print(f"== {tag}")
    print(f"  category per unit: {len(cat_u)}  [{ranges(cat_u)}]")
    print(f"  whitespace (.NET set) per unit: {len(ws_u)}  [{ranges(ws_u)}]")
    print(f"  lower per unit: {len(low_u)}  [{ranges(low_u)}]")
    print(f"  category per code point: {len(cat_p)} (assigned in .NET only: {newer_in_dotnet}; in Python only: {older_in_dotnet}; re-categorised: {len(cat_p) - newer_in_dotnet - older_in_dotnet})")
    print(f"  NFKC: {len(nfkc_p)}  [{ranges(nfkc_p)}]   .NET throws on {len(throws)}")
    print(f"  NFD: {len(nfd_p)}  [{ranges(nfd_p)}]")
