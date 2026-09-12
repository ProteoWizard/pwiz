#!/usr/bin/env python3
"""
Post-restructure path repair.

The restructure.py path rewrites cascaded (e.g., `src\MsData.NativeAot`
got rewritten, then `src\MsData` matched inside the result) and the
csproj ProjectReference rewrites were incomplete (proj_map only included
csprojs at OLD locations at script-start time, missing ones that moved
in a prior partial run). This script does an idempotent, filename-based
repair:

  1. Build a global index: csproj filename -> new resolved path.
  2. For Pwiz.sln, rewrite every `Project(...)` path entry to the actual
     new csproj location.
  3. For every csproj, rewrite every <ProjectReference Include="..."> to
     the actual new csproj location.
  4. For build.bat / tcbuild.bat / clean.bat / .ps1 / installer / cmake:
     rewrite literal csproj path strings in one regex pass.

Idempotent: re-running on already-correct content is a no-op.
"""
from __future__ import annotations

import os
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent


def build_csproj_index() -> dict[str, Path]:
    """Map csproj filename -> resolved absolute path (under pwiz/, Tools/,
    or anywhere else). Excludes bin/obj. Refuses to register duplicates."""
    index: dict[str, Path] = {}
    duplicates: dict[str, list[Path]] = {}
    for csproj in ROOT.rglob("*.csproj"):
        s = str(csproj)
        if f"{os.sep}bin{os.sep}" in s or f"{os.sep}obj{os.sep}" in s:
            continue
        # Only look in pwiz/, Tools/, and top-level (no src/test leftovers).
        rel = csproj.relative_to(ROOT)
        first = rel.parts[0]
        if first not in ("pwiz", "Tools"):
            continue
        name = csproj.name
        if name in index:
            duplicates.setdefault(name, [index[name]]).append(csproj.resolve())
            continue
        index[name] = csproj.resolve()
    for name, paths in duplicates.items():
        print(f"  WARNING: csproj name '{name}' found at multiple locations:")
        for p in paths:
            print(f"    {p.relative_to(ROOT)}")
    return index


# --------------------------------------------------------------------------

def fix_csproj(csproj: Path, index: dict[str, Path]) -> bool:
    original = csproj.read_text(encoding="utf-8")
    pat = re.compile(r'(<ProjectReference\s+Include=")([^"]+)(")')

    def repl(m: re.Match) -> str:
        before, ref_path, after = m.group(1), m.group(2), m.group(3)
        leaf = Path(ref_path.replace("\\", "/")).name
        target = index.get(leaf)
        if target is None:
            return m.group(0)
        # Compute the relative path from this csproj's location to target.
        new_rel = os.path.relpath(target, csproj.parent).replace("/", "\\")
        # Preserve the original form if it's already correct.
        if new_rel == ref_path:
            return m.group(0)
        return f"{before}{new_rel}{after}"

    updated = pat.sub(repl, original)
    if updated == original:
        return False
    csproj.write_text(updated, encoding="utf-8")
    return True


# --------------------------------------------------------------------------

SLN_PROJECT_LINE = re.compile(
    r'^(Project\("\{[^}]+\}"\)\s*=\s*"[^"]+",\s*")([^"]+\.csproj)(",\s*"\{[^}]+\}")',
    re.MULTILINE,
)


def fix_sln(sln: Path, index: dict[str, Path]) -> bool:
    original = sln.read_text(encoding="utf-8-sig")

    def repl(m: re.Match) -> str:
        before, path, after = m.group(1), m.group(2), m.group(3)
        leaf = Path(path.replace("\\", "/")).name
        target = index.get(leaf)
        if target is None:
            return m.group(0)
        new_rel = os.path.relpath(target, sln.parent).replace("/", "\\")
        if new_rel == path:
            return m.group(0)
        return f"{before}{new_rel}{after}"

    updated = SLN_PROJECT_LINE.sub(repl, original)
    if updated == original:
        return False
    # Preserve the UTF-8 BOM that VS solution files use.
    sln.write_text(updated, encoding="utf-8-sig")
    return True


# --------------------------------------------------------------------------
# Literal-path rewrite (for .bat / .ps1 / .iss / .cmake). Single regex pass
# with alternation: longest-first, no cascade. The "old" string is the
# pre-restructure flat path (src\X or test\X.Tests); "new" is the post-
# restructure path.

def build_literal_rewrites(index: dict[str, Path]) -> list[tuple[str, str]]:
    """
    For every csproj in the index, derive a list of (old_literal, new_literal)
    path strings to rewrite in build/config files. Covers both the csproj
    path itself and the parent directory.
    """
    rewrites: list[tuple[str, str]] = []
    seen_pairs = set()

    def add_pair(old: str, new: str) -> None:
        if old == new:
            return
        key = (old, new)
        if key in seen_pairs:
            return
        seen_pairs.add(key)
        rewrites.append(key)

    for name, new_path in index.items():
        new_rel = new_path.relative_to(ROOT)
        parts = new_rel.parts
        # Old path = everything from the first "src" or "test" component onward.
        # That collapses any wrapping like pwiz/src/X, Tools/X/src/X,
        # Tools/<Group>/X/src/X, pwiz/src/Vendor/X/Wiff2/, etc., back to the
        # flat pre-restructure shape (src\X\..., test\X.Tests\...).
        try:
            anchor = next(
                i for i, p in enumerate(parts) if p in ("src", "test")
            )
        except StopIteration:
            continue
        old_dir_parts = list(parts[anchor:-1])
        # If the csproj sits directly inside src/ or test/ (flattened layout,
        # e.g., Tools/Commandline/MsConvert/src/MsConvert.csproj), the OLD
        # flat path had an intermediate <stem>/ dir that the new path lacks.
        # Append the stem so the derived old_dir matches the pre-restructure
        # shape (src\MsConvert\, test\MsConvert.Tests\, etc.).
        if old_dir_parts and old_dir_parts[-1] in ("src", "test"):
            old_dir_parts.append(new_path.stem)
        old_dir = "\\".join(old_dir_parts)
        new_dir = "\\".join(parts[:-1])
        # Project file:
        old_csproj = f"{old_dir}\\{new_path.name}"
        new_csproj = f"{new_dir}\\{new_path.name}"
        add_pair(old_csproj, new_csproj)
        # Project directory (for any path-string usage):
        add_pair(old_dir, new_dir)

    # Longest old-string first to avoid wrong-prefix issues even in single-pass.
    rewrites.sort(key=lambda p: len(p[0]), reverse=True)
    return rewrites


REWRITE_GLOBS = [
    "*.bat",
    "*.ps1",
    "scripts/**/*.ps1",
    "installer/**/*.iss",
    "installer/**/*.ps1",
    "examples/**/*.cmake",
    "examples/**/CMakeLists.txt",
    "examples/**/*.ps1",
]


def find_text_files() -> list[Path]:
    out: list[Path] = []
    seen = set()
    for pat in REWRITE_GLOBS:
        for p in ROOT.glob(pat):
            rp = p.resolve()
            if rp in seen or not p.is_file():
                continue
            seen.add(rp)
            out.append(p)
    return out


def fix_text_file(path: Path, rewrites: list[tuple[str, str]]) -> bool:
    try:
        original = path.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        return False
    # Single-pass alternation regex so we don't cascade.
    if not rewrites:
        return False
    lookup = dict(rewrites)
    pat = re.compile("|".join(re.escape(k) for k, _ in rewrites))
    updated = pat.sub(lambda m: lookup[m.group(0)], original)
    if updated == original:
        return False
    path.write_text(updated, encoding="utf-8")
    return True


# --------------------------------------------------------------------------

def main() -> int:
    print("Building csproj index under pwiz/ and Tools/...")
    index = build_csproj_index()
    print(f"  Indexed {len(index)} csprojs.\n")

    # 1. Fix every csproj's ProjectReference paths.
    print("Step 1: rewrite ProjectReference paths in every csproj")
    csproj_count = 0
    for csproj in sorted(index.values()):
        if fix_csproj(csproj, index):
            csproj_count += 1
            print(f"  REWROTE: {csproj.relative_to(ROOT)}")
    print(f"  ({csproj_count} csprojs updated)\n")

    # 2. Fix Pwiz.sln.
    print("Step 2: rewrite Pwiz.sln project paths")
    sln = ROOT / "Pwiz.sln"
    if fix_sln(sln, index):
        print(f"  REWROTE: {sln.name}")
    else:
        print("  (no change needed)")
    print()

    # 3. Rewrite literal paths in build/config files.
    print("Step 3: rewrite literal csproj/dir paths in .bat/.ps1/.iss/.cmake")
    rewrites = build_literal_rewrites(index)
    print(f"  ({len(rewrites)} unique old->new mappings)")
    file_count = 0
    for f in find_text_files():
        if fix_text_file(f, rewrites):
            file_count += 1
            print(f"  REWROTE: {f.relative_to(ROOT)}")
    print(f"  ({file_count} text files updated)\n")

    print("DONE. Run `dotnet build Pwiz.sln` to verify.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
