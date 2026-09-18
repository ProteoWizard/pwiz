#!/bin/bash
# TeamCity entry point: Linux counterpart of tcbuild.bat. Builds + tests Osprey with
# TeamCity service messages, then packages the redistributable so it is published as an
# artifact of this per-commit config.
#
# The real work is in build.ps1 / package.ps1, which are cross-platform: pwsh is the
# project standard for Osprey scripting ("no powershell.exe fallback", tcbuild.bat), and
# every Osprey project is plain net10.0, so the same two scripts serve both platforms.
# Only the entry point and the packaging target differ, which is all this file is.
#
# Differences from tcbuild.bat, both forced rather than chosen:
#   * No wix / .msi. WiX is Windows-only, and package.ps1 already gates -Msi to win-* RIDs.
#   * -Rid linux-x64 instead of win-x64.
# Coverage is still requested; build.ps1 drops it with a warning off Windows, because the
# pinned dotCover console runner is Windows-only. Coverage is reported by the Windows config.
#
# Agent prerequisites are bootstrapped here rather than assumed, because the Linux agents
# carry neither:
#   * .NET SDK - via pwiz-sharp/scripts/ensure-dotnet.sh, the same helper Core Linux .NET
#     uses from pwiz-sharp/tcbuild.sh. It resolves an existing dotnet and installs one
#     satisfying the repo-root global.json if none does. Reusing it rather than repeating
#     it keeps one bootstrap for both Linux configs; build.ps1 already reaches across to
#     pwiz-sharp/scripts for Ensure-DotCover.ps1 the same way.
#   * pwsh - as a dotnet global tool, so it needs no package manager and no root, the same
#     way tcbuild.bat self-provisions wix.
#
# Outputs consumed by TeamCity:
#   * pwiz_tools/Osprey/TestResults/*.trx                           (vstest importData)
#   * pwiz_tools/Osprey/dist/Osprey-<ver>-linux-x64.zip             (publishArtifacts)
#   The publishArtifacts service messages are emitted by package.ps1 -TeamCity, so no
#   server-side artifact-path configuration is required.
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

fail() {
    echo "##teamcity[message text='$1' status='ERROR']"
    exit "${2:-1}"
}

# shellcheck source=../../pwiz-sharp/scripts/ensure-dotnet.sh
. "$SCRIPT_DIR/../../pwiz-sharp/scripts/ensure-dotnet.sh"
resolve_dotnet || fail "dotnet not found on PATH or at the usual locations"
# Walks up from here to the repo-root global.json, so the SDK pin is the same one every
# other build in this repo resolves.
ensure_dotnet_sdk "$SCRIPT_DIR" || fail "no .NET SDK satisfying global.json, and installing one failed"

echo "##teamcity[progressMessage 'dotnet --version (resolves via global.json)']"
dotnet --version || fail "dotnet --version failed"

# pwsh is an apphost: it finds the shared runtime through DOTNET_ROOT, a registered
# location, or /usr/share/dotnet. ensure_dotnet_sdk exports DOTNET_ROOT only when it had to
# install, so an agent that already had a usable SDK in a non-standard place satisfies
# `dotnet --version` and then fails pwsh with "You must install .NET to run this
# application" - which reads as a missing SDK rather than an unset variable.
if [ -z "${DOTNET_ROOT:-}" ] && command -v dotnet >/dev/null 2>&1; then
    DOTNET_ROOT="$(dirname "$(readlink -f "$(command -v dotnet)")")"
    export DOTNET_ROOT
fi

# dotnet global tools (pwsh, and anything package.ps1 reaches for) live here.
export PATH="$PATH:$HOME/.dotnet/tools"
if ! command -v pwsh >/dev/null 2>&1; then
    echo "##teamcity[progressMessage 'Installing PowerShell as a dotnet global tool']"
    dotnet tool install --global PowerShell || fail "installing pwsh failed"
fi

echo "##teamcity[progressMessage 'Osprey build.ps1']"
pwsh -NoProfile -File "$SCRIPT_DIR/build.ps1" -TeamCity -Coverage -Configuration Release \
    || fail "build.ps1 failed" $?

echo "##teamcity[progressMessage 'Osprey package.ps1 (linux-x64)']"
pwsh -NoProfile -File "$SCRIPT_DIR/package.ps1" -TeamCity -Rid linux-x64 \
    || fail "package.ps1 failed" $?

exit 0
