#!/usr/bin/env python3
"""
Self-healing path repair for csprojs.

Walks each csproj's non-ProjectReference paths. For each path that
doesn't resolve to an existing file/dir, tries removing one `..\` at
a time from the leading up-run (up to 3 removals) until it resolves.
Idempotent: if every path already resolves, no change.

Reason for existence: an earlier fix-outside-paths.py run had an XML-
entity escape bug — `&quot;` got included in the extracted relative
path during the resolve check, so paths that actually resolved were
flagged as broken and got an extra `..\` prepended on a second pass.
This script unwinds that over-application by removing the extra ups
until the path lines up with reality.

Safety: skips ProjectReference lines (already correct via os.path.relpath
in fix-paths.py Step 1).
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent


def shrink_up_run(path: str, drop: int) -> str:
    """Remove `drop` leading `..\\` (or `../`) segments from `path`."""
    out = path
    for _ in range(drop):
        if out.startswith("..\\"):
            out = out[3:]
        elif out.startswith("../"):
            out = out[3:]
        else:
            break
    return out


def try_resolve(base: Path, rel: str) -> bool:
    try:
        target = (base / rel.replace("\\", "/")).resolve()
    except (OSError, ValueError):
        return False
    if target.exists():
        return True
    # Glob pattern (e.g., `pwiz_tools\Shared\MSGraph\*.cs`): check that
    # the directory containing the wildcard exists. resolve() leaves `*`
    # as a literal path component; walk up to the first non-wildcard
    # parent and check that.
    s = str(target)
    if "*" in s or "?" in s:
        parent = target
        while "*" in parent.name or "?" in parent.name:
            parent = parent.parent
        return parent.exists()
    return False


def heal_csproj(csproj: Path) -> int:
    """Returns count of paths shrunk."""
    content = csproj.read_text(encoding="utf-8")
    base = csproj.parent

    # Skip ranges = lines containing <ProjectReference (already correct).
    skip_ranges: list[tuple[int, int]] = []
    for m in re.finditer(r'^.*<ProjectReference[^\n]*\n?', content, re.MULTILINE):
        skip_ranges.append((m.start(), m.end()))

    def in_skip(pos: int) -> bool:
        return any(s <= pos < e for s, e in skip_ranges)

    # Match up-runs of 2 or more. We won't shrink below 2 ups (those are
    # almost certainly intentional within-csproj references that we'd
    # damage if we touched them).
    UP_RUN = re.compile(r'(\.\.[\\/]){2,}')

    pieces: list[str] = []
    last = 0
    shrunk = 0
    for m in UP_RUN.finditer(content):
        if in_skip(m.start()):
            continue
        # Extract the full path token starting at m.start(), stopping at
        # the first delimiter / XML-entity ampersand / quote.
        end = m.end()
        stop_chars = set('"<>; \t\r\n&')
        while end < len(content) and content[end] not in stop_chars:
            end += 1
        full_path = content[m.start():end]
        if try_resolve(base, full_path):
            continue
        for drop in (1, 2, 3):
            shorter = shrink_up_run(full_path, drop)
            if shorter == full_path:
                break
            if try_resolve(base, shorter):
                pieces.append(content[last:m.start()])
                pieces.append(shorter)
                last = end
                shrunk += 1
                break
    pieces.append(content[last:])
    if shrunk == 0:
        return 0
    updated = "".join(pieces)
    if updated == content:
        return 0
    csproj.write_text(updated, encoding="utf-8")
    return shrunk


def main() -> int:
    total = 0
    for csproj in sorted(ROOT.rglob("*.csproj")):
        s = str(csproj)
        if "\\bin\\" in s or "\\obj\\" in s:
            continue
        rel = csproj.relative_to(ROOT)
        if rel.parts[0] not in ("pwiz", "Tools"):
            continue
        n = heal_csproj(csproj)
        if n:
            total += n
            print(f"  HEALED {n} path(s): {rel}")
    print(f"\n({total} paths shrunk across all csprojs)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
