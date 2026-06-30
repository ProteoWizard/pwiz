#!/usr/bin/env python3
"""Regenerate ElementData.generated.cs from pwiz/utility/chemistry/ChemistryData.cpp.

The C++ source has two sections:
  1. Isotope arrays:  `Isotope isotopes_X[] = { {mass, abundance}, ... };`
  2. Element table:   `Element elements_[] = { { TYPE, "SYM", Z, W, isotopes_X, isotopes_X_size [, "SYN"] }, ... };`

This script transforms both to C# arrays backing the ElementType enum and the
ElementInfo lookup. Re-run after any edit to the upstream C++ file.

Usage (run from repo root):
    python pwiz-sharp/src/Pwiz.Util/Chemistry/generate_elements.py
"""
from __future__ import annotations

import pathlib
import re
import sys

REPO = pathlib.Path(__file__).resolve().parents[4]
SRC = REPO / "pwiz/utility/chemistry/ChemistryData.cpp"
OUT = REPO / "pwiz-sharp/src/Pwiz.Util/Chemistry/ElementData.generated.cs"

HEADER = """// Generated from pwiz/utility/chemistry/ChemistryData.cpp — do not edit by hand.
// Re-run pwiz-sharp/src/Pwiz.Util/Chemistry/generate_elements.py to regenerate.

namespace Pwiz.Util.Chemistry;

internal static partial class ElementData
{
"""


# Matches one isotope line:   Isotope isotopes_XYZ[] = { {m, a}, {m, a}, };
ISOTOPE_LINE = re.compile(
    r"^\s*Isotope\s+isotopes_(?P<name>\w+)\s*\[\s*\]\s*=\s*\{(?P<body>.*)\}\s*;\s*$"
)
# Matches one element-record line inside the `elements_[]` array:
#   { H, "H", 1, 1.00794, isotopes_H, isotopes_H_size },
#   { _2H, "_2H", 1, isotopes_2H[0].mass, isotopes_2H, isotopes_2H_size, "D" },
ELEMENT_LINE = re.compile(
    r"^\s*\{\s*(?P<body>.*?)\s*\}\s*,?\s*(//.*)?\s*$"
)


def convert_isotope_body(body: str) -> str:
    """Convert `{1.0078250321, 0.999885}, {2.014, 0.000115}, ...` → `new(...), new(...), ...`."""
    # Strip trailing comma inside the outer braces.
    s = body.strip().rstrip(",").strip()
    if not s:
        return ""
    # Each entry looks like `{m, a}`. Rewrite curly braces → `new(...)` parens.
    entries = re.findall(r"\{\s*([^}]+?)\s*\}", s)
    return ", ".join(f"new({e.strip()})" for e in entries)


def convert_element_body(body: str) -> str:
    """Convert one element record body (text between `{` and `}`) to a C# constructor call.

    Input  : `H, "H", 1, 1.00794, isotopes_H, isotopes_H_size`
    Output : `ElementType.H, "H", 1, 1.00794, Isotopes_H`

    Also strips the `isotopes_X_size` size parameter and renames `isotopes_` → `Isotopes_`.
    Handles optional synonym and the `isotopes_X[0].mass` atomic-weight shortcut.
    """
    s = body.strip()
    # Drop the ", isotopes_X_size" fragment (C# uses .Length implicitly).
    s = re.sub(r",\s*isotopes_\w+_size", "", s)
    # Rename isotopes_X → Isotopes_X anywhere.
    s = re.sub(r"\bisotopes_(\w+)", r"Isotopes_\1", s)
    # Prefix the leading type identifier with ElementType.
    # s now looks like:  H, "H", 1, 1.00794, Isotopes_H
    s = re.sub(r"^(\w+)", r"ElementType.\1", s, count=1)
    # Fix struct-field access: [0].mass → [0].Mass
    s = s.replace("[0].mass", "[0].Mass")
    return s


def transform(text: str) -> str:
    lines = text.splitlines()
    isotope_lines: list[str] = []
    element_lines: list[str] = []
    in_elements = False

    for raw in lines:
        stripped = raw.rstrip("\n")

        # Section boundary: entering the Element array.
        if re.match(r"^\s*PWIZ_API_DECL\s+Element\s+elements_\s*\[\s*\]\s*=", stripped):
            in_elements = True
            continue
        if in_elements and re.match(r"^\s*\};?\s*$", stripped):
            in_elements = False
            continue

        if not in_elements:
            m = ISOTOPE_LINE.match(stripped)
            if m:
                name = m.group("name")
                converted = convert_isotope_body(m.group("body"))
                isotope_lines.append(
                    f"    internal static readonly MassAbundance[] Isotopes_{name} = "
                    f"{{ {converted} }};"
                )
            continue

        # Inside the element table.
        m = ELEMENT_LINE.match(stripped)
        if not m:
            continue
        body = m.group("body").strip()
        if not body:
            continue
        element_lines.append(f"        new({convert_element_body(body)}),")

    parts = [HEADER]
    parts.extend(isotope_lines)
    parts.append("")
    parts.append("    internal static readonly ElementRecord[] Elements = new ElementRecord[]")
    parts.append("    {")
    parts.extend(element_lines)
    parts.append("    };")
    parts.append("}")
    parts.append("")
    return "\n".join(parts)


def main() -> int:
    if not SRC.exists():
        print(f"Missing source: {SRC}", file=sys.stderr)
        return 1

    text = SRC.read_text(encoding="utf-8")
    result = transform(text)

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(result, encoding="utf-8")
    lines = sum(1 for _ in OUT.open())
    print(f"{lines} {OUT}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
