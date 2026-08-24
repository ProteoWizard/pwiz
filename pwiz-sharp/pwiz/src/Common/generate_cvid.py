#!/usr/bin/env python3
"""Regenerate CVID.generated.cs from pwiz's generated cv.hpp.

This is a mechanical transformation: the C++ enum body is copied verbatim
into a C# enum (the syntax for enum-value-with-doc-comment is compatible).
A future pass should port pwiz/data/common/cvgen.cpp to generate BOTH the
enum AND the full relational term info directly from the OBO sources.

Usage (run from repo root):
    python pwiz-sharp/src/Pwiz.Data.Common/Cv/generate_cvid.py
"""
from __future__ import annotations

import pathlib
import sys

REPO = pathlib.Path(__file__).resolve().parents[4]
SRC = REPO / "pwiz/data/common/cv.hpp"
OUT = REPO / "pwiz-sharp/src/Pwiz.Data.Common/Cv/CVID.generated.cs"

HEADER = """// This file is generated from pwiz/data/common/cv.hpp.
// Do not edit by hand — run pwiz-sharp/src/Pwiz.Data.Common/Cv/generate_cvid.py to regenerate.
// Source ontologies: psi-ms.obo, unimod.obo, unit.obo (see cv.hpp header for versions).

// ReSharper disable All
#pragma warning disable CS1591, CS1570, CS1572, CS1573, CS1574, CA1707, CA1028, CA1008

namespace Pwiz.Data.Common.Cv;

/// <summary>
/// Enumeration of controlled vocabulary (CV) terms, generated from OBO file(s).
/// Port of pwiz/cv::CVID; values are the numeric CV accession numbers.
/// </summary>
public enum CVID
{
"""

FOOTER = "}\n"


def extract_enum_body(text: str) -> str:
    """Extract the body of `enum PWIZ_API_DECL CVID { ... }` from cv.hpp.

    Returns the lines between the opening `{` and closing `};` (exclusive),
    preserving `/// doc` comments and the `NAME = VALUE,` entries.
    """
    lines = text.splitlines()
    out: list[str] = []
    in_enum = False
    for line in lines:
        stripped = line.strip()
        if not in_enum:
            if stripped.startswith("enum PWIZ_API_DECL CVID"):
                in_enum = True
            continue
        if stripped.startswith("{"):
            continue
        if stripped.startswith("};"):
            break
        out.append(line)
    return "\n".join(out) + "\n"


def main() -> int:
    if not SRC.exists():
        print(f"Missing source: {SRC}", file=sys.stderr)
        return 1

    text = SRC.read_text(encoding="utf-8")
    body = extract_enum_body(text)

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(HEADER + body + FOOTER, encoding="utf-8")

    lines = sum(1 for _ in OUT.open())
    print(f"{lines} {OUT}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
