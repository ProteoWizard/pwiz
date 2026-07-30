#!/usr/bin/env bash
# ------------------------------------------------------------------------
# tcbuild.sh — single TeamCity entry point for pwiz-sharp on Linux
# (ProteoWizard_CoreLinuxNet). Analogue of tcbuild.bat.
#
# Sequence (mirrors tcbuild.bat, minus the Windows-only steps):
#   1. dotnet --version (logs which SDK got picked, after global.json pinning).
#   2. clean.sh: wipe what the build produces so a stale artifact from a prior
#      commit can't leak into this one, while keeping the content-addressed
#      caches (.NET runtime download, extracted vendor SDK DLLs).
#   3. build.sh: dotnet restore + build + test. Args forwarded verbatim.
#   4. git ls-files --deleted: catches builds that delete tracked files.
#   5. git status --porcelain: catches builds that leave stray files not
#      covered by .gitignore.
#
# Usage:
#   ./tcbuild.sh [Debug|Release] [--i-agree-to-the-vendor-licenses]
#                [--require-vendor-support] [--automated]
#
# Args are forwarded verbatim to build.sh; see that script for flag semantics
# and for which vendors/tests are in scope on Linux.
#
# Deliberately NOT here (all Windows-only, unlike tcbuild.bat):
#   - Ensure-InnoSetup.ps1 / the installer build / Installer.Tests
#   - the MsData.NativeAot + cpp-aot-reader CTest leg, which needs VsDevCmd,
#     link.exe and a win-x64 Native AOT publish
#   - dotCover coverage
# ------------------------------------------------------------------------
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

fail() {
    echo "##teamcity[message text='$1' status='ERROR']"
    exit "${2:-1}"
}

echo "##teamcity[progressMessage 'dotnet --version (resolves via global.json)']"
dotnet --version || fail "dotnet not on PATH"

# clean.sh is optional: keep the build working on a checkout that only has the
# Windows clean.bat, rather than failing the whole run over hygiene.
if [ -x "$SCRIPT_DIR/clean.sh" ] || [ -f "$SCRIPT_DIR/clean.sh" ]; then
    echo "##teamcity[progressMessage 'pwiz-sharp clean.sh']"
    bash "$SCRIPT_DIR/clean.sh" || fail "clean.sh failed"
else
    echo "##teamcity[message text='clean.sh not found; skipping pre-build clean' status='WARNING']"
fi

echo "##teamcity[progressMessage 'pwiz-sharp build.sh $*']"
bash "$SCRIPT_DIR/build.sh" "$@" || fail "build.sh failed"

# Post-build hygiene checks. Run from the repo root so git sees the full
# working tree, not just pwiz-sharp/.
cd "$SCRIPT_DIR/.."

echo "##teamcity[progressMessage 'git ls-files --deleted (build should not delete tracked files)']"
DELETED="$(git ls-files --deleted)"
if [ -n "$DELETED" ]; then
    echo "$DELETED"
    fail "Build deleted tracked files"
fi

echo "##teamcity[progressMessage 'git status --porcelain (build should not leave untracked files)']"
DIRTY="$(git status --porcelain)"
if [ -n "$DIRTY" ]; then
    echo "$DIRTY"
    fail "Build produced files not in .gitignore"
fi

exit 0
