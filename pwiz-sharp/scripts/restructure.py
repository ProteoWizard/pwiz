#!/usr/bin/env python3
"""
pwiz-sharp restructure: flat src/+test/ -> pwiz/src+pwiz/test for core,
Tools/<Name>/src+test for tools.

Usage:
    python scripts/restructure.py            # dry-run, prints plan only
    python scripts/restructure.py --apply    # execute git mv + edits

Idempotent on a clean tree (git mv refuses to move a non-existent dir, so
re-running after a successful apply is a no-op). Refuses to run on a dirty
tree (other than the BiblioSpec scaffolding we just added) to avoid
mixing the restructure with unrelated edits.
"""
from __future__ import annotations

import argparse
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent  # pwiz-sharp/

# --------------------------------------------------------------------------
# Move plan: maps old relative path -> new relative path.
# All paths are POSIX-style; converted to OS at apply time. Source must be
# a directory that currently exists; destination's parent is created if
# needed.

PWIZ_CORE = [
    "Util",
    "Common",
    "MsData",
    "MsData.NativeAot",
    "Analysis",
    "IdentData",
    "TraData",
    "StlContainers",
    "TestHarness",
    "MSGraph",
    "ZedGraph",
    "Vendor",  # moves the whole vendor tree as one unit
]

PWIZ_CORE_TESTS = [
    "Util.Tests",
    "Common.Tests",
    "MsData.Tests",
    "MsData.NativeAot.Tests",
    "Analysis.Tests",
    "IdentData.Tests",
    "TraData.Tests",
    "Agilent.Tests",
    "Bruker.Tests",
    "Bruker.PrmScheduling.Tests",
    "Mobilion.Tests",
    "Sciex.Tests",
    "Shimadzu.Tests",
    "Thermo.Tests",
    "UIMF.Tests",
    "UNIFI.Tests",
    "Waters.Tests",
    "Installer.Tests",
]

# Tools with a test project today.
TOOLS_WITH_TESTS = {
    "BiblioSpec": "BiblioSpec.Tests",
    "MsConvert": "MsConvert.Tests",
    "MsConvertGUI": "MsConvertGUI.Tests",
}

# Tools without test projects today.
TOOLS_WITHOUT_TESTS = [
    "BullseyeSharp",
    "SeeMS",
    "MsBenchmark",
]


def build_move_plan() -> list[tuple[str, str]]:
    moves: list[tuple[str, str]] = []
    for d in PWIZ_CORE:
        moves.append((f"src/{d}", f"pwiz/src/{d}"))
    for d in PWIZ_CORE_TESTS:
        moves.append((f"test/{d}", f"pwiz/test/{d}"))
    for tool, test in TOOLS_WITH_TESTS.items():
        moves.append((f"src/{tool}", f"Tools/{tool}/src/{tool}"))
        moves.append((f"test/{test}", f"Tools/{tool}/test/{test}"))
    for tool in TOOLS_WITHOUT_TESTS:
        moves.append((f"src/{tool}", f"Tools/{tool}/src/{tool}"))
    return moves


# --------------------------------------------------------------------------
# Path rewrite table: applies to .sln, .csproj, .bat, .ps1 contents.
# Order matters: rewrite longer / more-specific paths before shorter ones.

def build_rewrite_table(moves: list[tuple[str, str]]) -> list[tuple[str, str]]:
    pairs: list[tuple[str, str]] = []
    for old, new in moves:
        # Backslash form (sln, csproj on Windows-style paths, bat).
        pairs.append((old.replace("/", "\\"), new.replace("/", "\\")))
        # Forward-slash form (some csproj, ps1, MSBuild evaluates either).
        pairs.append((old, new))
    # Longest-first so e.g. "src\MsData.NativeAot" wins over "src\MsData".
    pairs.sort(key=lambda p: len(p[0]), reverse=True)
    return pairs


# --------------------------------------------------------------------------
# csproj ProjectReference rewriting: project files have relative paths
# like ..\Util\Util.csproj. After the move, every ProjectReference's
# absolute target is the same .csproj at a new absolute path; we recompute
# the relative from the csproj's new location to the target's new location.

def project_map(moves: list[tuple[str, str]]) -> dict[Path, Path]:
    """csproj abs path (old) -> csproj abs path (new)."""
    # Discover all csprojs under src/ and test/ (before the move).
    mapping: dict[Path, Path] = {}
    for old_rel, new_rel in moves:
        old_dir = ROOT / old_rel
        if not old_dir.exists():
            continue
        for csproj in old_dir.rglob("*.csproj"):
            rel = csproj.relative_to(old_dir)
            new_csproj = ROOT / new_rel / rel
            mapping[csproj.resolve()] = new_csproj
    return mapping


def fix_csproj_refs(csproj_new_path: Path, content: str,
                    proj_map: dict[Path, Path]) -> str:
    """Rewrite every <ProjectReference Include="..."> in `content` so the
    relative path is correct given csproj_new_path's new location."""
    def repl(m: re.Match) -> str:
        before, rel_path, after = m.group(1), m.group(2), m.group(3)
        # Resolve from the OLD csproj location, which is what the include
        # was written against. To get OLD location: invert proj_map.
        # But we only have new->we need old. Build inverse on first call.
        nonlocal old_of_new
        old_csproj = old_of_new.get(csproj_new_path.resolve())
        if old_csproj is None:
            return m.group(0)  # csproj wasn't moved; leave as-is
        old_target_abs = (old_csproj.parent / rel_path).resolve()
        new_target = proj_map.get(old_target_abs)
        if new_target is None:
            # Reference to a csproj we're not moving (or path is wrong).
            return m.group(0)
        # Recompute relative from new csproj location to new target.
        new_rel = os.path.relpath(new_target, csproj_new_path.parent)
        # Match original slash style. csprojs typically use backslashes
        # on Windows. We normalize to backslashes for consistency.
        new_rel = new_rel.replace("/", "\\")
        return f"{before}{new_rel}{after}"

    old_of_new = {v.resolve(): k for k, v in proj_map.items()}
    pat = re.compile(r'(<ProjectReference\s+Include=")([^"]+)(")')
    return pat.sub(repl, content)


# --------------------------------------------------------------------------
# Path-string substitution: for .sln, .bat, .ps1, generic anything that
# embeds the old-shape literal paths (e.g. src\MsConvert\MsConvert.csproj).

def apply_path_rewrites(content: str,
                        rewrites: list[tuple[str, str]]) -> str:
    for old, new in rewrites:
        content = content.replace(old, new)
    return content


# --------------------------------------------------------------------------
# Discovery: what files contain literal old paths and need rewriting?

REWRITE_GLOBS = [
    "*.sln",
    "*.bat",
    "*.ps1",
    "Directory.Build.props",
    "Directory.Build.user.props",
    "Directory.Build.targets",
    "scripts/**/*.ps1",
    "installer/**/*.iss",
    "installer/**/*.ps1",
    "examples/**/*.cmake",
    "examples/**/CMakeLists.txt",
    "examples/**/*.ps1",
]


def find_rewrite_files() -> list[Path]:
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


# --------------------------------------------------------------------------
# Git operations.

def git_mv(src: Path, dst: Path, apply: bool) -> None:
    cmd = ["git", "mv", str(src.relative_to(ROOT)),
           str(dst.relative_to(ROOT))]
    if not apply:
        print(f"  WOULD: {' '.join(cmd)}")
        return
    dst.parent.mkdir(parents=True, exist_ok=True)
    result = subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True)
    if result.returncode == 0:
        print(f"  RUN:   {' '.join(cmd)}")
        return
    # Fall back to plain shutil.move for untracked dirs (e.g. BiblioSpec
    # scaffolding we just added this session that isn't committed yet).
    # Git will detect the rename at commit time via content-similarity.
    print(f"  MOVE (untracked): {src.relative_to(ROOT)} -> "
          f"{dst.relative_to(ROOT)}")
    shutil.move(str(src), str(dst))


def check_clean_enough() -> bool:
    """Refuse to apply if there are uncommitted changes outside the
    BiblioSpec scaffolding + the unrelated analysis cpp/hpp files from
    the prior session. Anything else, the user should commit or stash."""
    out = subprocess.run(
        ["git", "status", "--porcelain"],
        cwd=ROOT.parent, capture_output=True, text=True, check=True,
    ).stdout
    expected_prefixes = (
        "pwiz/analysis/spectrum_processing/",  # prior unrelated work
        "pwiz/data/vendor_readers/Thermo/",   # prior unrelated work
        "CLAUDE.md",                          # parent-repo CLAUDE.md
        "pwiz-sharp/CLAUDE.md",
        "pwiz-sharp/run-msbench-final.ps1",
        "pwiz-sharp/src/",                    # any leftover src/ moves
        "pwiz-sharp/test/",                   # any leftover test/ moves
        "pwiz-sharp/pwiz/",                   # mid-restructure
        "pwiz-sharp/Tools/",                  # mid-restructure
        "pwiz-sharp/Pwiz.sln",
        "pwiz-sharp/scripts/restructure.py",
    )
    unexpected = []
    for line in out.splitlines():
        if not line.strip():
            continue
        path = line[3:].strip().strip('"')
        if not any(path.startswith(p) for p in expected_prefixes):
            unexpected.append(line)
    if unexpected:
        print("Refusing to apply: unexpected dirty files in working tree:")
        for u in unexpected:
            print(f"  {u}")
        return False
    return True


# --------------------------------------------------------------------------

def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--apply", action="store_true",
                    help="Execute moves + edits. Without this, dry-run.")
    args = ap.parse_args()

    moves = build_move_plan()
    proj_map = project_map(moves)
    rewrites = build_rewrite_table(moves)

    print(f"=== Restructure plan ({len(moves)} dir moves, "
          f"{len(proj_map)} csprojs to retarget) ===\n")

    print("Directory moves:")
    for old, new in moves:
        marker = "  " if (ROOT / old).exists() else "??"
        print(f"  {marker}  {old}  ->  {new}")
    print()

    if args.apply and not check_clean_enough():
        return 1

    # --- 1. git mv directories ---
    print("Step 1: git mv directories")
    for old, new in moves:
        src = ROOT / old
        if not src.exists():
            print(f"  SKIP (missing): {old}")
            continue
        dst = ROOT / new
        git_mv(src, dst, args.apply)
    print()

    # --- 2. Rewrite csproj ProjectReferences ---
    print("Step 2: rewrite csproj ProjectReference paths")
    csproj_changes = 0
    for old_path, new_path in proj_map.items():
        # In apply mode the file is now at new_path; in dry-run it's still old_path.
        path_to_read = new_path if args.apply else old_path
        if not path_to_read.exists():
            print(f"  SKIP (missing): {path_to_read.relative_to(ROOT)}")
            continue
        original = path_to_read.read_text(encoding="utf-8")
        updated = fix_csproj_refs(new_path, original, proj_map)
        if original == updated:
            continue
        csproj_changes += 1
        if args.apply:
            path_to_read.write_text(updated, encoding="utf-8")
            print(f"  REWROTE: {new_path.relative_to(ROOT)}")
        else:
            print(f"  WOULD REWRITE: {new_path.relative_to(ROOT)}")
    print(f"  ({csproj_changes} csprojs need ProjectReference fixes)\n")

    # --- 3. Rewrite paths in sln / bat / ps1 / props / iss ---
    print("Step 3: rewrite literal paths in build/config files")
    txt_changes = 0
    for f in find_rewrite_files():
        try:
            original = f.read_text(encoding="utf-8")
        except UnicodeDecodeError:
            continue
        updated = apply_path_rewrites(original, rewrites)
        if original == updated:
            continue
        txt_changes += 1
        if args.apply:
            f.write_text(updated, encoding="utf-8")
            print(f"  REWROTE: {f.relative_to(ROOT)}")
        else:
            print(f"  WOULD REWRITE: {f.relative_to(ROOT)}")
    print(f"  ({txt_changes} files need path rewrites)\n")

    if not args.apply:
        print("DRY RUN. Re-run with --apply to execute.")
    else:
        print("DONE. Run `dotnet build Pwiz.sln` to verify.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
