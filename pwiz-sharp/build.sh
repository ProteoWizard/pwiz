#!/usr/bin/env bash
# ------------------------------------------------------------------------
# pwiz-sharp build entry point for Linux. Analogue of build.bat; TeamCity
# calls it from tcbuild.sh (ProteoWizard_CoreLinuxNet). Runs locally too.
#
# Usage:
#   ./build.sh [Debug|Release] [--i-agree-to-the-vendor-licenses]
#              [--require-vendor-support] [--without-mascot] [--automated]
#
# Flags (same semantics as build.bat):
#   --i-agree-to-the-vendor-licenses
#       Acknowledge the vendor SDK EULAs so the encrypted vendor archives are
#       extracted. NOTE the Linux caveat below: this enables only the vendor
#       SDKs that actually work off-Windows.
#   --require-vendor-support
#       Fail if vendor support isn't enabled, instead of silently building a
#       stripped artifact.
#   --without-mascot
#       Skip the MascotShim native wrapper (needs CMake + msparser).
#   --automated
#       Tag InformationalVersion "(automated build)".
#
# WHAT IS AND ISN'T BUILT ON LINUX
#   Vendor support is not one switch once Linux is in scope (see
#   $(NativeVendorsAvailable) in Directory.Build.props). Thermo's SDK is
#   managed ThermoFisher.CommonCore and works cross-platform; Waters, Agilent,
#   Bruker, Sciex, Shimadzu, Mobilion and UNIFI wrap native Windows DLLs (or
#   Windows-only tooling) and cannot load here at all, so their projects and
#   test suites are excluded from this script rather than being built and then
#   failing at run time.
#
#   Also intentionally NOT done here (all Windows-only, unlike build.bat):
#     - the Inno Setup installer + Installer.Tests
#     - dotCover coverage (build.bat auto-enables it under TEAMCITY_VERSION;
#       the Linux config reports plain test results instead)
#
# Test runner: build.bat fans out through scripts/Run-Tests-Parallel.ps1 to
# keep TeamCity service messages from interleaving. Here we run `dotnet test`
# per project sequentially, which avoids the same interleaving without
# depending on pwsh being installed on the agent. Every project runs even if
# an earlier one fails, so one red suite doesn't hide the rest; the script
# still exits non-zero if any failed.
# ------------------------------------------------------------------------
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# dotnet resolution + SDK provisioning, shared with the other Linux entry point.
# resolve_dotnet finds an installed dotnet that is off PATH; ensure_dotnet_sdk
# installs the SDK global.json pins when the agent image does not have it.
. "$SCRIPT_DIR/scripts/ensure-dotnet.sh"

CONFIG=Release
IAGREE=0
REQUIRE_VENDOR=0
AUTOMATED=0
WITHOUT_MASCOT=0

for arg in "$@"; do
    case "$arg" in
        --i-agree-to-the-vendor-licenses) IAGREE=1 ;;
        --require-vendor-support)         REQUIRE_VENDOR=1 ;;
        --without-mascot)                 WITHOUT_MASCOT=1 ;;
        --automated)                      AUTOMATED=1 ;;
        --coverage)
            echo "##teamcity[message text='--coverage is not supported on Linux; ignoring' status='WARNING']" ;;
        Debug|debug)                      CONFIG=Debug ;;
        Release|release)                  CONFIG=Release ;;
        *)
            echo "Unrecognized argument: $arg" >&2
            echo "##teamcity[message text='Unrecognized argument: $arg' status='ERROR']"
            exit 2 ;;
    esac
done

if [ "$REQUIRE_VENDOR" = 1 ] && [ "$IAGREE" = 0 ]; then
    echo "##teamcity[message text='--require-vendor-support set but --i-agree-to-the-vendor-licenses was not passed; refusing to build a stripped artifact.' status='ERROR']"
    exit 2
fi

MSBUILD_PROPS=(-p:Configuration="$CONFIG")
[ "$IAGREE" = 1 ]          && MSBUILD_PROPS+=(-p:IAgreeToVendorLicenses=true)
[ "$AUTOMATED" = 1 ]       && MSBUILD_PROPS+=(-p:AutomatedBuild=true)
[ "$WITHOUT_MASCOT" = 1 ]  && MSBUILD_PROPS+=(-p:MascotSupport=false)

if [ "$IAGREE" = 1 ]; then
    echo "##teamcity[message text='Vendor support: ENABLED (cross-platform SDKs only; native Windows vendors are excluded on Linux)']"
else
    echo "##teamcity[message text='Vendor support: DISABLED (no --i-agree-to-the-vendor-licenses); building core only']"
fi

# The Linux 7-Zip binary ships next to 7za.exe in libraries/ and is referenced
# by $(SevenZaExe) from Directory.Build.props. Git preserves its executable bit,
# but a checkout that lost file modes (or an archive export) leaves it
# non-executable and every vendor extraction fails with "Permission denied".
SEVEN_ZZ="$SCRIPT_DIR/../libraries/7zz"
if [ "$IAGREE" = 1 ] && [ -f "$SEVEN_ZZ" ] && [ ! -x "$SEVEN_ZZ" ]; then
    echo "##teamcity[message text='libraries/7zz was not executable; fixing mode' status='WARNING']"
    chmod +x "$SEVEN_ZZ"
fi

resolve_dotnet || { echo "##teamcity[message text='dotnet not found on PATH or at /usr/bin, /usr/local/bin, /usr/share/dotnet, /usr/lib/dotnet, \$DOTNET_ROOT, ~/.dotnet' status='ERROR']"; exit 1; }
ensure_dotnet_sdk "$SCRIPT_DIR/.." || { echo "##teamcity[message text='no .NET SDK satisfying global.json, and installing one failed' status='ERROR']"; exit 1; }
echo "##teamcity[progressMessage 'dotnet --version']"
dotnet --version || { echo "##teamcity[message text='dotnet --version failed' status='ERROR']"; exit 1; }

# Build targets. Without vendor support, msconvert alone covers the core
# stack. With it, MsConvert plus the vendor readers whose SDKs exist off-Windows
# (Thermo, Waters, Bruker) — deliberately NOT Pwiz.sln, which pulls in the
# Windows-only vendors.
if [ "$IAGREE" = 1 ]; then
    BUILD_TARGET=(
        "Tools/Commandline/MsConvert/src/MsConvert.csproj"
        "pwiz/src/Vendor/Thermo/Thermo.csproj"
        "pwiz/src/Vendor/Bruker/Bruker.csproj"
    )
else
    BUILD_TARGET=("Tools/Commandline/MsConvert/src/MsConvert.csproj")
fi

# Test projects: the platform-agnostic suites, plus the vendors whose SDKs ship
# an off-Windows build (Thermo/Waters/Bruker). The remaining native-Windows
# vendor suites (Agilent/Sciex/Shimadzu/UIMF/Mobilion/UNIFI) and
# Installer.Tests are excluded by design.
TEST_TARGET=(
    "pwiz/test/Util.Tests/Util.Tests.csproj"
    "pwiz/test/Common.Tests/Common.Tests.csproj"
    "pwiz/test/MsData.Tests/MsData.Tests.csproj"
    "pwiz/test/MsData.NativeAot.Tests/MsData.NativeAot.Tests.csproj"
    "pwiz/test/IdentData.Tests/IdentData.Tests.csproj"
    "pwiz/test/Analysis.Tests/Analysis.Tests.csproj"
    "Tools/Commandline/MsConvert/test/MsConvert.Tests.csproj"
)
[ "$IAGREE" = 1 ] && TEST_TARGET+=("pwiz/test/Thermo.Tests/Thermo.Tests.csproj")
# Waters is the other vendor that builds off-Windows: MassLynx 5.0.0 ships
# libMassLynxRaw.so. NOTE it needs a reasonably modern distro -- the .so requires
# GLIBC_2.32 and GLIBCXX_3.4.29, which Ubuntu 20.04 (glibc 2.31) does not provide; there
# the load fails with "libMassLynxRaw.so: cannot open shared object file", which is dlopen
# reporting an unmet dependency rather than a missing file. Ubuntu 22.04 satisfies both.
[ "$IAGREE" = 1 ] && TEST_TARGET+=("pwiz/test/Waters.Tests/Waters.Tests.csproj")
# Bruker likewise: Bruker's tdf-sdk 2.21 ships linux64/libtimsdata.so, and baf2sql has a
# matching .so, so TDF/TSF and BAF all read off-Windows. Both are far less demanding than
# the Waters .so -- libtimsdata.so needs only GLIBC_2.14 and links no libstdc++ at all.
[ "$IAGREE" = 1 ] && TEST_TARGET+=("pwiz/test/Bruker.Tests/Bruker.Tests.csproj")

# The test projects have to be restored and built here too, not just the product ones.
# `dotnet test --no-build` below does no building, and when the test assembly is missing
# vstest reports it as a bad command-line argument rather than a missing file:
#   The argument .../Util.Tests.dll is invalid. Please use the /help option ...
# which reads like an argv problem and is not one.
for proj in "${BUILD_TARGET[@]}" "${TEST_TARGET[@]}"; do
    echo "##teamcity[progressMessage 'dotnet restore $proj']"
    dotnet restore "$proj" "${MSBUILD_PROPS[@]}" \
        || { echo "##teamcity[message text='dotnet restore $proj failed' status='ERROR']"; exit 1; }
done

for proj in "${BUILD_TARGET[@]}" "${TEST_TARGET[@]}"; do
    echo "##teamcity[progressMessage 'dotnet build $proj ($CONFIG)']"
    dotnet build "$proj" --no-restore -nologo "${MSBUILD_PROPS[@]}" \
        || { echo "##teamcity[message text='dotnet build $proj failed' status='ERROR']"; exit 1; }
done

# Linux package build, mirroring build.bat's installer step. Vendor-only for the same reason:
# the payload ships with the vendor binaries STRIPPED and fetches them at runtime, but the
# managed readers still have to be compiled WITH vendor support or Read() throws regardless.
# A packaging failure is a warning rather than a build failure, matching how a missing Inno
# Setup degrades on Windows - the test run below is still worth having.
if [ "$IAGREE" = 1 ]; then
    echo "##teamcity[progressMessage 'installer/build-linux.sh']"
    bash "$SCRIPT_DIR/installer/build-linux.sh" \
        || echo "##teamcity[message text='Linux package build failed' status='WARNING']"
fi

TC_TEST_RESULTS="$SCRIPT_DIR/TestResults"
rm -rf "$TC_TEST_RESULTS"
mkdir -p "$TC_TEST_RESULTS"

# Results reach the TeamCity UI by importing each trx with an importData service message,
# rather than via --logger:teamcity. The logger from TeamCity.VSTest.TestAdapter
# (Directory.Build.targets) resolves on the Windows agents but not here, and importData needs
# no build-config change. Each suite gets a deterministic trx name so it can be imported by
# path right after it runs.

TESTS_FAILED=0
FAILED_PROJECTS=""
for proj in "${TEST_TARGET[@]}"; do
    echo "##teamcity[progressMessage 'dotnet test $proj ($CONFIG)']"
    # Echo the exact argv before running. vstest reports a bad argument by printing the
    # offending token with no indication of which option it belonged to, which is not
    # enough to diagnose from a CI log alone.
    TRX_NAME="$(basename "$proj" .csproj).trx"
    TEST_ARGS=(test "$proj" --no-build -nologo "${MSBUILD_PROPS[@]}"
               "--results-directory:$TC_TEST_RESULTS" "--logger:trx;LogFileName=$TRX_NAME")
    printf '+ dotnet'; printf ' %q' "${TEST_ARGS[@]}"; printf '\n'
    dotnet "${TEST_ARGS[@]}"
    TEST_RC=$?
    # Hand the trx to TeamCity so per-test results show up in the UI. Done even when the run
    # failed -- that is exactly when the individual failures matter.
    if [ -n "${TEAMCITY_VERSION:-}" ] && [ -f "$TC_TEST_RESULTS/$TRX_NAME" ]; then
        echo "##teamcity[importData type='vstest' path='$TC_TEST_RESULTS/$TRX_NAME']"
    fi
    if [ "$TEST_RC" -ne 0 ]; then
        TESTS_FAILED=1
        FAILED_PROJECTS="$FAILED_PROJECTS $(basename "$proj")"
    fi
done

if [ "$TESTS_FAILED" -ne 0 ]; then
    echo "##teamcity[message text='dotnet test reported failures in:$FAILED_PROJECTS' status='ERROR']"
    exit 1
fi

exit 0
