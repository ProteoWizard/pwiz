@echo off
setlocal enabledelayedexpansion

REM # ------------------------------------------------------------------------
REM # clean.bat — wipe pwiz-sharp build artifacts.
REM #
REM # Mirrors cpp pwiz's clean.bat (one level up): removes everything the build
REM # produces so the next build starts from a known-clean state.
REM #
REM # Default: wipe build outputs but KEEP the slow-to-refetch caches (.NET
REM # runtime download + extracted vendor SDK assemblies). Pass --all (or -a)
REM # to clear caches too — that's what TC does for "this build only used
REM # inputs from this commit" certainty, but locally it's an extra ~60s of
REM # re-download / re-extract that's rarely worth paying.
REM #
REM # What gets removed (always):
REM #   - bin/ and obj/ under every project (dotnet build outputs, AOT publish
REM #     output, AOT-generated link.lib / .exp)
REM #   - TestResults/ (test run logs + dotCover snapshots)
REM #   - installer/build/ (Inno Setup .exe + staging tree + the version.txt
REM #     sidecar that Installer.Tests reads)
REM #   - examples/**/build/ (cmake build trees, including the AOT example)
REM #   - src/Vendor/Common/VendorSdkPins.generated.cs (regenerated on every
REM #     build from the vendor 7z archives' SHA-256 + git history)
REM #
REM # What gets removed only with --all:
REM #   - installer/cache/windowsdesktop-runtime-win-x64.exe (~56 MB .NET 8
REM #     runtime installer; re-downloaded by installer/build.ps1 on the next
REM #     run if missing)
REM #   - src/Vendor/*/vendor-assemblies/ (DLLs extracted from the vendor 7z
REM #     archives by the .csproj pre-build steps; re-extracted on the next
REM #     dotnet build if missing)
REM #
REM # What is NOT touched, ever:
REM #   - Directory.Build.user.props (per-user "I agreed to vendor licenses"
REM #     flag; wiping it would force the user to re-run
REM #     i-agree-to-the-vendor-licenses.bat after every clean)
REM #   - .vs/ and *.user/*.suo files (IDE local state; not build output)
REM #
REM # Usage:
REM #   clean.bat            Wipe build outputs, keep caches (default).
REM #   clean.bat --all      Wipe caches too (TC-equivalent full reset).
REM #   clean.bat -a         Short alias.
REM # ------------------------------------------------------------------------

set SCRIPT_DIR=%~dp0
set SCRIPT_DIR=%SCRIPT_DIR:~0,-1%
pushd "%SCRIPT_DIR%"

set CLEAN_CACHE=0
if /I "%1"=="--all" set CLEAN_CACHE=1
if /I "%1"=="-a"    set CLEAN_CACHE=1

echo Cleaning pwiz-sharp build artifacts...

REM # Walk the tree for any dir named bin or obj. /d limits the walk to
REM # directories; /r %SCRIPT_DIR% bounds it to pwiz-sharp/.
for /d /r "%SCRIPT_DIR%" %%d in (bin obj) do (
    if exist "%%d" rmdir /s /q "%%d" 2>nul
)

REM # Top-level output trees.
if exist TestResults     rmdir /s /q TestResults
if exist installer\build rmdir /s /q installer\build

REM # CMake build trees under examples/.
for /d /r "%SCRIPT_DIR%\examples" %%d in (build) do (
    if exist "%%d" rmdir /s /q "%%d" 2>nul
)

REM # Vendor SDK pins are regenerated on every build (Refresh-VendorPins.ps1
REM # is invoked as a pre-CoreCompile target in Vendor.Common.csproj).
if exist src\Vendor\Common\VendorSdkPins.generated.cs (
    del /q src\Vendor\Common\VendorSdkPins.generated.cs
)

REM # Caches: preserved by default; wiped only with --all.
if %CLEAN_CACHE%==1 (
    if exist installer\cache rmdir /s /q installer\cache
    for /d /r "%SCRIPT_DIR%\src\Vendor" %%d in (vendor-assemblies) do (
        if exist "%%d" rmdir /s /q "%%d" 2>nul
    )
)

if %CLEAN_CACHE%==1 (
    echo Clean complete ^(caches wiped too^).
) else (
    echo Clean complete ^(caches preserved^).
)

popd
exit /b 0
