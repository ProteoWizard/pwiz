#!/usr/bin/env python3
"""
Fix non-ProjectReference paths in csprojs that escape the new csproj's
parent dir (pointing at pwiz-sharp top-level or the parent repo).

After the restructure, csprojs at:
  pwiz/src/X/X.csproj       (was src/X/X.csproj)       — old dir depth 2
  pwiz/src/Vendor/X/X.csproj (was src/Vendor/X)        — old dir depth 3
  pwiz/test/X.Tests/X.csproj (was test/X.Tests)        — old dir depth 2
  Tools/X/src/X/X.csproj    (was src/X)                — old dir depth 2
  Tools/X/test/X.Tests       (was test/X.Tests)        — old dir depth 2

Any non-ProjectReference path with up-count >= old_dir_depth used to
escape pwiz-sharp's flat src/test layout. After the move, those paths
need (new_depth - old_depth) extra `..\` segments prepended.

ProjectReference paths are left alone: fix-paths.py Step 1 already
rewrote those via os.path.relpath.

Idempotent: skips csprojs where every long-up path already resolves
to an existing file or directory.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

# Match `..\`/`../` segments at the start of a quoted/embedded path value.
# Captures runs of 1+ — we count them in code and apply the threshold.
UP_RUN = re.compile(r'(\.\.[\\/])+')


def csproj_depths(csproj: Path) -> tuple[int, int]:
    """Return (old_depth, new_depth) — count of parent directories from
    pwiz-sharp root to the csproj's parent dir.

    Old depth is everything from the first "src" or "test" component to
    (but not including) the csproj filename. This handles arbitrary
    Tools/ wrapping: Tools/X/src/X/, Tools/Group/X/src/X/, etc."""
    rel = csproj.relative_to(ROOT)
    parts = rel.parts
    new_dir_depth = len(parts) - 1  # parent dir depth (strip csproj filename)
    try:
        anchor = next(
            i for i, p in enumerate(parts) if p in ("src", "test")
        )
    except StopIteration:
        return new_dir_depth, new_dir_depth
    # Old dir was parts[anchor:-1] (src or test onwards, minus filename).
    old_dir_depth = (len(parts) - 1) - anchor
    return old_dir_depth, new_dir_depth


def find_escape_paths(content: str, threshold: int) -> list[tuple[int, int, int]]:
    """Find all (start, end, up_count) of UP_RUNs whose up-count >= threshold,
    skipping ones inside <ProjectReference> lines."""
    results: list[tuple[int, int, int]] = []
    # Build a set of line-start offsets for ProjectReference lines.
    skip_ranges: list[tuple[int, int]] = []
    for m in re.finditer(r'^.*<ProjectReference[^\n]*\n?', content, re.MULTILINE):
        skip_ranges.append((m.start(), m.end()))

    def in_skip(pos: int) -> bool:
        for s, e in skip_ranges:
            if s <= pos < e:
                return True
        return False

    for m in UP_RUN.finditer(content):
        if in_skip(m.start()):
            continue
        up_count = (m.end() - m.start()) // 3  # each "../" or "..\" is 3 chars
        if up_count < threshold:
            continue
        results.append((m.start(), m.end(), up_count))
    return results


def path_at(content: str, start: int) -> str:
    """Extract the full relative-path token starting at `start` (the first
    `..` of an up-run). Stops at `"`, `<`, `>`, whitespace, `;` etc."""
    end = start
    stop = set('"<>; \t\r\n')
    while end < len(content) and content[end] not in stop:
        end += 1
    return content[start:end]


def needs_fix(csproj: Path, threshold: int) -> bool:
    content = csproj.read_text(encoding="utf-8")
    base = csproj.parent
    for start, _, _ in find_escape_paths(content, threshold):
        rel = path_at(content, start)
        try:
            target = (base / rel.replace("\\", "/")).resolve()
        except (OSError, ValueError):
            return True
        if not target.exists():
            return True
    return False


def fix_csproj(csproj: Path) -> bool:
    old_d, new_d = csproj_depths(csproj)
    delta = new_d - old_d
    if delta == 0:
        return False
    threshold = old_d  # paths with up-count >= old_dir_depth escape pwiz-sharp
    if not needs_fix(csproj, threshold):
        return False
    original = csproj.read_text(encoding="utf-8")
    prefix = "..\\" * delta

    # Build a list of skip ranges (ProjectReference lines).
    skip_ranges: list[tuple[int, int]] = []
    for m in re.finditer(r'^.*<ProjectReference[^\n]*\n?', original, re.MULTILINE):
        skip_ranges.append((m.start(), m.end()))

    def in_skip(pos: int) -> bool:
        for s, e in skip_ranges:
            if s <= pos < e:
                return True
        return False

    # Walk UP_RUN matches; rebuild content with prepended prefix where the
    # match qualifies (not in a ProjectReference and up-count >= threshold).
    pieces: list[str] = []
    last = 0
    for m in UP_RUN.finditer(original):
        if in_skip(m.start()):
            continue
        up_count = (m.end() - m.start()) // 3
        if up_count < threshold:
            continue
        pieces.append(original[last:m.start()])
        pieces.append(prefix)
        pieces.append(m.group(0))
        last = m.end()
    pieces.append(original[last:])
    updated = "".join(pieces)
    if updated == original:
        return False
    csproj.write_text(updated, encoding="utf-8")
    return True


def main() -> int:
    count = 0
    for csproj in sorted(ROOT.rglob("*.csproj")):
        s = str(csproj)
        if f"{Path('/').name}bin" in s or "\\bin\\" in s or "\\obj\\" in s:
            continue
        rel = csproj.relative_to(ROOT)
        if rel.parts[0] not in ("pwiz", "Tools"):
            continue
        if fix_csproj(csproj):
            count += 1
            old_d, new_d = csproj_depths(csproj)
            print(f"  REWROTE (+{new_d - old_d} ups, threshold {old_d}): {rel}")
    print(f"\n({count} csprojs updated)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
