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

# Put dotnet on PATH if the caller's environment doesn't already have it. A TeamCity agent
# can launch the build with a minimal PATH that omits the install location, and the usual
# symptom is a bare "dotnet: command not found" that looks like dotnet isn't installed at
# all. Probe the standard locations (and DOTNET_ROOT) before giving up, and if a candidate
# exists but isn't runnable -- a dangling /usr/bin/dotnet symlink is the classic case --
# say so explicitly instead of reporting it as missing.
resolve_dotnet() {
    command -v dotnet >/dev/null 2>&1 && return 0
    local cand
    for cand in /usr/bin/dotnet /usr/local/bin/dotnet /usr/share/dotnet/dotnet \
                /usr/lib/dotnet/dotnet "${DOTNET_ROOT:-}/dotnet" "$HOME/.dotnet/dotnet"; do
        [ -n "$cand" ] || continue
        if [ -x "$cand" ]; then
            export PATH="$(dirname "$cand"):$PATH"
            echo "##teamcity[message text='dotnet was not on PATH; using $cand']"
            return 0
        fi
        if [ -e "$cand" ] || [ -L "$cand" ]; then
            echo "##teamcity[message text='$cand exists but is not executable (dangling symlink?): $(ls -ld "$cand" 2>&1)' status='WARNING']"
        fi
    done
    echo "##teamcity[message text='dotnet not found on PATH or at /usr/bin, /usr/local/bin, /usr/share/dotnet, /usr/lib/dotnet, \$DOTNET_ROOT, ~/.dotnet' status='ERROR']"
    return 1
}

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

resolve_dotnet
echo "##teamcity[progressMessage 'dotnet --version']"
dotnet --version || { echo "##teamcity[message text='dotnet --version failed' status='ERROR']"; exit 1; }

# Build targets. Without vendor support, msconvert alone covers the core
# stack. With it, MsConvert plus the Thermo reader (the one native-free vendor
# SDK) — deliberately NOT Pwiz.sln, which pulls in the Windows-only vendors.
if [ "$IAGREE" = 1 ]; then
    BUILD_TARGET=(
        "Tools/Commandline/MsConvert/src/MsConvert.csproj"
        "pwiz/src/Vendor/Thermo/Thermo.csproj"
    )
else
    BUILD_TARGET=("Tools/Commandline/MsConvert/src/MsConvert.csproj")
fi

for proj in "${BUILD_TARGET[@]}"; do
    echo "##teamcity[progressMessage 'dotnet restore $proj']"
    dotnet restore "$proj" "${MSBUILD_PROPS[@]}" \
        || { echo "##teamcity[message text='dotnet restore $proj failed' status='ERROR']"; exit 1; }
done

for proj in "${BUILD_TARGET[@]}"; do
    echo "##teamcity[progressMessage 'dotnet build $proj ($CONFIG)']"
    dotnet build "$proj" --no-restore -nologo "${MSBUILD_PROPS[@]}" \
        || { echo "##teamcity[message text='dotnet build $proj failed' status='ERROR']"; exit 1; }
done

# Test projects: the platform-agnostic suites, plus Thermo when vendor support
# is on. The native-Windows vendor suites (Agilent/Bruker/Sciex/Shimadzu/
# Waters/UIMF/Mobilion/UNIFI) and Installer.Tests are excluded by design.
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

TC_TEST_RESULTS="$SCRIPT_DIR/TestResults"
rm -rf "$TC_TEST_RESULTS"
mkdir -p "$TC_TEST_RESULTS"

# NOTE: deliberately no --logger:teamcity here, unlike scripts/Run-Tests-Parallel.ps1 on the
# Windows side. TeamCity.VSTest.TestAdapter 1.0.40 (Directory.Build.targets) registers that
# logger only on Windows; on Linux vstest fails argument parsing with "Could not find a test
# logger ... 'teamcity'" and every suite dies before running a single test. Verified on
# SDK 8.0.423 against a scratch MSTest project with the package restored: requesting the
# logger fails, omitting it runs clean.
#
# Consequence: no per-test ##teamcity service messages on Linux (the adapter does not
# auto-emit them either -- 0 messages observed with TEAMCITY_VERSION set). Results are still
# written as trx under $TC_TEST_RESULTS; surfacing them in the TeamCity UI needs an XML report
# processing feature on the build config pointing at pwiz-sharp/TestResults/*.trx.
TC_LOGGER=()

TESTS_FAILED=0
FAILED_PROJECTS=""
for proj in "${TEST_TARGET[@]}"; do
    echo "##teamcity[progressMessage 'dotnet test $proj ($CONFIG)']"
    # Echo the exact argv before running. vstest reports a bad argument by printing the
    # offending token with no indication of which option it belonged to, which is not
    # enough to diagnose from a CI log alone.
    TEST_ARGS=(test "$proj" --no-build -nologo "${MSBUILD_PROPS[@]}"
               "--results-directory:$TC_TEST_RESULTS" --logger:trx
               "${TC_LOGGER[@]+"${TC_LOGGER[@]}"}")
    printf '+ dotnet'; printf ' %q' "${TEST_ARGS[@]}"; printf '\n'
    dotnet "${TEST_ARGS[@]}"
    if [ $? -ne 0 ]; then
        TESTS_FAILED=1
        FAILED_PROJECTS="$FAILED_PROJECTS $(basename "$proj")"
    fi
done

if [ "$TESTS_FAILED" -ne 0 ]; then
    echo "##teamcity[message text='dotnet test reported failures in:$FAILED_PROJECTS' status='ERROR']"
    exit 1
fi

exit 0
