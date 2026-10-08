#!/usr/bin/env bash
# Build and test CarafeSharp on Linux (including WSL2). Arguments pass through to build.ps1:
#   ./build.sh                     CPU build and tests
#   ./build.sh -Torch cuda         CUDA build and the Cuda test category
#   ./build.sh -NoTests            build only
#
# Installs what is missing without root: the .NET SDK that global.json names (into
# ~/.dotnet, through scripts/ensure-dotnet.sh) and PowerShell (as a dotnet
# global tool), as pwiz_tools/Osprey/tcbuild.sh does.
#
# No `set -e`: ensure-dotnet.sh expects to run without it (it probes for dotnet and reports its
# own errors), as tcbuild.sh calls it, so every step here checks its own result instead.
set -uo pipefail

fail() {
    echo "ERROR: $*" >&2
    exit 1
}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)" || fail "cannot find the script folder"

# shellcheck source=../../scripts/ensure-dotnet.sh
source "$SCRIPT_DIR/../../scripts/ensure-dotnet.sh" || fail "cannot load scripts/ensure-dotnet.sh"
resolve_dotnet || true
ensure_dotnet_sdk "$SCRIPT_DIR" || fail "the .NET SDK that global.json names could not be installed"

# The pwsh apphost needs DOTNET_ROOT when dotnet is not in a standard location.
if [ -z "${DOTNET_ROOT:-}" ]; then
    DOTNET_ROOT="$(dirname "$(readlink -f "$(command -v dotnet)")")" || fail "dotnet is not on PATH"
    export DOTNET_ROOT
fi
export PATH="$HOME/.dotnet/tools:$PATH"
if ! command -v pwsh > /dev/null 2>&1; then
    echo "==> Installing PowerShell as a dotnet global tool"
    dotnet tool install --global PowerShell || fail "PowerShell could not be installed as a dotnet global tool"
fi

# Under WSL2 the NVIDIA driver's CUDA libraries live in /usr/lib/wsl/lib.
if [ -d /usr/lib/wsl/lib ]; then
    export LD_LIBRARY_PATH="/usr/lib/wsl/lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
    export PATH="/usr/lib/wsl/lib:$PATH"
fi

exec pwsh -NoProfile -File "$SCRIPT_DIR/build.ps1" "$@"
