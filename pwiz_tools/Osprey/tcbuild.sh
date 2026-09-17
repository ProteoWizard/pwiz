#!/bin/bash
# TeamCity entry point: Linux counterpart of tcbuild.bat. Builds + tests Osprey with
# TeamCity service messages, then packages the redistributable tarball so it is published
# as an artifact of this per-commit config.
#
# The real work is in build.ps1 / package.ps1, which are cross-platform: pwsh is the
# project standard for Osprey scripting ("no powershell.exe fallback", tcbuild.bat), and
# every Osprey project is plain net10.0, so the same two scripts serve both platforms.
# Only the entry point and the packaging targets differ, which is all this file is.
#
# Differences from tcbuild.bat, both forced rather than chosen:
#   * No wix / .msi. WiX is Windows-only, and package.ps1 already gates -Msi to win-* RIDs.
#   * -Rid linux-x64 instead of win-x64.
# Coverage is still requested; build.ps1 drops it with a warning off Windows, because the
# pinned dotCover console runner is Windows-only. Coverage is reported by the Windows config.
#
# Pre-requisites on the build agent:
#   * .NET SDK. The one global.json pins is self-provisioned if absent, through the
#     same pwiz-sharp/scripts/ensure-dotnet.sh that ProteoWizard_CoreLinuxNet uses:
#     the agent image ships an 8.x SDK, the tree pins 10.x, and rollForward never
#     crosses majors, so without it `dotnet tool install` below dies with "A
#     compatible .NET SDK was not found".
#   * pwsh (PowerShell 7+). Self-provisioned below if absent, the same way tcbuild.bat
#     self-provisions the wix tool - it installs as a dotnet global tool, so it needs no
#     package manager, no root, and no new provisioning channel beyond the SDK.
#
# Outputs consumed by TeamCity:
#   * pwiz_tools/Osprey/TestResults/*.trx              (vstest importData)
#   * pwiz_tools/Osprey/dist/Osprey-<ver>-linux-x64.tar.gz or .zip  (publishArtifacts)
#   The publishArtifacts service messages are emitted by package.ps1 -TeamCity, so no
#   server-side artifact-path configuration is required.
set -u

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

fail() {
    echo "##teamcity[message text='$1' status='ERROR']"
    exit "${2:-1}"
}

# dotnet resolution + SDK provisioning, shared with pwiz-sharp/tcbuild.sh: resolve_dotnet
# finds an installed dotnet that is off PATH, ensure_dotnet_sdk installs the SDK
# global.json pins when the agent image does not have it (into $HOME/.dotnet, outside
# the tree). Probed from this directory, the one the pwsh scripts below run dotnet from;
# global.json resolution walks up from there to the repo root.
. "$REPO_ROOT/pwiz-sharp/scripts/ensure-dotnet.sh"
resolve_dotnet || fail "dotnet not found on PATH or at /usr/bin, /usr/local/bin, /usr/share/dotnet, /usr/lib/dotnet, \$DOTNET_ROOT, ~/.dotnet"
ensure_dotnet_sdk "$SCRIPT_DIR" || fail "no .NET SDK satisfying global.json, and installing one failed"

# dotnet global tools (pwsh, and anything package.ps1 reaches for) live here.
export PATH="$PATH:$HOME/.dotnet/tools"

# pwsh is an apphost: it locates the shared runtime through DOTNET_ROOT, a registered
# location, or /usr/share/dotnet. An agent whose SDK lives anywhere else satisfies
# `command -v dotnet` but fails pwsh with "You must install .NET to run this application",
# which reads as a missing SDK rather than an unset variable. Point it at the SDK actually
# on PATH when nothing else has.
if [ -z "${DOTNET_ROOT:-}" ] && command -v dotnet >/dev/null 2>&1; then
    DOTNET_ROOT="$(dirname "$(readlink -f "$(command -v dotnet)")")"
    export DOTNET_ROOT
fi

echo "##teamcity[progressMessage 'dotnet --version (resolves via global.json)']"
( cd "$SCRIPT_DIR" && dotnet --version ) || fail "dotnet --version failed"

if ! command -v pwsh >/dev/null 2>&1; then
    echo "##teamcity[progressMessage 'Installing PowerShell as a dotnet global tool']"
    dotnet tool install --global PowerShell || exit $?
fi

pwsh -NoProfile -File "$SCRIPT_DIR/build.ps1" -TeamCity -Coverage -Configuration Release || exit $?

pwsh -NoProfile -File "$SCRIPT_DIR/package.ps1" -TeamCity -Rid linux-x64
exit $?
