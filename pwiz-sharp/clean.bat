@echo off
setlocal enabledelayedexpansion

REM # ------------------------------------------------------------------------
REM # clean.bat — wipe pwiz-sharp build artifacts.
REM #
REM # Mirrors cpp pwiz's clean.bat (one level up): removes everything the build
REM # produces so the next build starts from a known-clean state.
REM #
REM # Default: wipe build outputs but KEEP the two caches (.NET runtime download
REM # + extracted vendor SDK assemblies). This is the mode TC runs — tcbuild.bat
REM # calls clean.bat with no arguments before every build, so CI already gets a
REM # from-scratch compile on every commit.
REM #
REM # Pass --all (or -a) to clear the caches too. Measured cost of doing so:
REM #   - vendor-assemblies/ re-extract (171 MB across 7 vendors): ~2 s. Cheap,
REM #     because it's a local 7z unpack of checked-in archives.
REM #   - installer/cache/: a ~56 MB re-download of the .NET desktop runtime from
REM #     aka.ms, and only on builds that run the installer. That's the real cost
REM #     of --all, and it's a reliability cost as much as a time one — it puts an
REM #     external endpoint on the build's critical path.
REM # Both caches are content-addressed (vendor archives by SHA-256 via the pins
REM # table, the runtime by a fixed versioned URL), so neither can drift
REM # commit-to-commit. --all is a paranoia reset: better suited to a nightly
REM # than to every commit.
REM #
REM # The list below tracks pwiz-sharp/.gitignore: everything the build writes is
REM # gitignored, so that file is the spec for what belongs here. Two gitignored
REM # entries are deliberately kept (see "NOT touched" below).
REM #
REM # What gets removed (always):
REM #   - bin/ and obj/ under every project (dotnet build outputs, AOT publish
REM #     output, AOT-generated link.lib / .exp)
REM #   - TestResults/ at every level — the top-level one plus the per-project
REM #     dirs `dotnet test` drops in pwiz/test/*/ (run logs + dotCover snapshots)
REM #   - installer/build/ (Inno Setup .exe + the version.txt sidecar that
REM #     Installer.Tests reads) and installer/staging/ (payload staging tree)
REM #   - examples/**/build/ and Tools/BiblioSpec/native/**/build/ (cmake build
REM #     trees — the AOT example, MascotShim, etc.)
REM #   - pwiz/src/Vendor/Common/VendorSdkPins.generated.cs (regenerated on every
REM #     build from the vendor 7z archives' SHA-256 + git history)
REM #
REM # What gets removed only with --all:
REM #   - installer/cache/windowsdesktop-runtime-win-x64.exe (~56 MB .NET 8
REM #     runtime installer; re-downloaded by installer/build.ps1 on the next
REM #     run if missing)
REM #   - vendor-assemblies/ (DLLs extracted from the vendor 7z archives by the
REM #     .csproj ExtractVendorAssemblies targets — one top-level dir, per
REM #     $(PwizVendorAssembliesPath); re-extracted on the next dotnet build)
REM #
REM # What is NOT touched, ever:
REM #   - vendor-archives/ — looks like a cache, is NOT: those archives are
REM #     TRACKED IN GIT and are build inputs. Deleting them is unrecoverable
REM #     without a fresh checkout. Do not add it to the --all branch.
REM #   - build/ at the top level — also tracked source (MSBuild .targets and the
REM #     VendorPinsGenerator/AgilentPatcher projects). This is why the cmake
REM #     sweep below is scoped to named subtrees instead of walking for any dir
REM #     called "build".
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

REM # Walk the tree for any dir named bin, obj or TestResults. /d limits the walk
REM # to directories; /r %SCRIPT_DIR% bounds it to pwiz-sharp/. No tracked file
REM # lives under a dir with any of these names, so the sweep is safe.
for /d /r "%SCRIPT_DIR%" %%d in (bin obj TestResults) do (
    if exist "%%d" rmdir /s /q "%%d" 2>nul
)

REM # Top-level output trees.
if exist installer\build   rmdir /s /q installer\build
if exist installer\staging rmdir /s /q installer\staging

REM # CMake build trees. Scoped to these two subtrees on purpose: the top-level
REM # build\ is tracked source, so a bare "walk for dirs named build" would
REM # delete it.
for /d /r "%SCRIPT_DIR%\examples" %%d in (build) do (
    if exist "%%d" rmdir /s /q "%%d" 2>nul
)
if exist "%SCRIPT_DIR%\Tools\BiblioSpec\native" (
    for /d /r "%SCRIPT_DIR%\Tools\BiblioSpec\native" %%d in (build) do (
        if exist "%%d" rmdir /s /q "%%d" 2>nul
    )
)

REM # Vendor SDK pins are regenerated on every build (build/VendorPinsGenerator
REM # is invoked as a pre-CoreCompile target in Vendor.Common.csproj).
if exist pwiz\src\Vendor\Common\VendorSdkPins.generated.cs (
    del /q pwiz\src\Vendor\Common\VendorSdkPins.generated.cs
)

REM # Caches: preserved by default; wiped only with --all. NB vendor-assemblies is
REM # a single top-level dir ($(PwizVendorAssembliesPath)), not a per-project one.
if %CLEAN_CACHE%==1 (
    if exist installer\cache     rmdir /s /q installer\cache
    if exist vendor-assemblies   rmdir /s /q vendor-assemblies
)

if %CLEAN_CACHE%==1 (
    echo Clean complete ^(caches wiped too^).
) else (
    echo Clean complete ^(caches preserved^).
)

popd
exit /b 0
