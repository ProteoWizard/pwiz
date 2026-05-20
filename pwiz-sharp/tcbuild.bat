@echo off
setlocal enabledelayedexpansion

REM # ------------------------------------------------------------------------
REM # tcbuild.bat — single TeamCity entry point for pwiz-sharp.
REM #
REM # Replaces the historical three-step TC config (Restore .NET / .NET msbuild
REM # / Test .NET) with one Command Line step. Internally calls build.bat
REM # (which does restore + build + test against Pwiz.sln with TC progress
REM # markers + TRX logger output) and adds TC-specific post-build hygiene
REM # checks mirroring scripts/misc/tcbuild.bat:
REM #
REM #   1. dotnet --version (logs which SDK got picked, after global.json
REM #      pinning).
REM #   2. build.bat (dotnet restore + build + test).
REM #   3. git ls-files --deleted: catches builds that delete tracked files
REM #      (e.g. a misbehaving clean target).
REM #   4. git status --porcelain: catches builds that produce stray files
REM #      not covered by .gitignore.
REM #
REM # Usage:
REM #   tcbuild.bat [Debug|Release] [--i-agree-to-the-vendor-licenses]
REM #               [--require-vendor-support] [--automated]
REM #
REM # Args are forwarded verbatim to build.bat; see that script for flag
REM # semantics. TC should pass --i-agree-to-the-vendor-licenses
REM # --require-vendor-support --automated for the standard CI run.
REM #
REM # dotCover: TeamCity's coverage wrapping only attaches to the built-in
REM # .NET runner, not Command Line steps, so build.bat invokes
REM # `dotnet-dotCover dotnet -- test` itself when TEAMCITY_VERSION is set
REM # (auto-enabled equivalent of --coverage). The snapshot drops at
REM # pwiz-sharp\TestResults\coverage.dcvr and is announced via
REM # ##teamcity[importData type='dotNetCoverage'] for TC's dotCover build
REM # feature to ingest into the report UI.
REM # ------------------------------------------------------------------------

set SCRIPT_DIR=%~dp0
set SCRIPT_DIR=%SCRIPT_DIR:~0,-1%
pushd "%SCRIPT_DIR%"

set EXIT=0
set ERROR_TEXT=

echo ##teamcity[progressMessage 'dotnet --version (resolves via global.json)']
dotnet --version
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 (set ERROR_TEXT=dotnet not on PATH & goto error)

REM # Bootstrap Inno Setup on the agent so build.bat can produce Setup.exe and
REM # Installer.Tests can drive it end-to-end. Ensure-InnoSetup.ps1 is
REM # idempotent and handles the full cascade itself:
REM #   - ISCC.exe already discoverable -> no-op
REM #   - winget present -> winget install JRSoftware.InnoSetup
REM #   - winget missing -> install winget via the Microsoft.WinGet.Client
REM #     PowerShell module (Repair-WinGetPackageManager), then install Inno
REM #     Setup via winget.
REM # If any step fails, the script exits non-zero. We log a TC warning and
REM # continue — build.bat handles the missing-ISCC case by skipping the
REM # installer build, and Installer.Tests then skips Inconclusive. So the
REM # worst case is "no installer coverage on this build," not "build fails."
echo ##teamcity[progressMessage 'Ensure-InnoSetup.ps1 ^(idempotent; bootstraps winget if missing^)']
pwsh -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%\installer\Ensure-InnoSetup.ps1"
if ERRORLEVEL 1 (
    echo ##teamcity[message text='Ensure-InnoSetup.ps1 failed; installer build and Installer.Tests will be skipped' status='WARNING']
)

echo ##teamcity[progressMessage 'pwiz-sharp build.bat %*']
call "%SCRIPT_DIR%\build.bat" %*
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 (set ERROR_TEXT=build.bat failed (exit %EXIT%) & goto error)

REM # ------------------------------------------------------------------------
REM # MsData.NativeAot end-to-end (Native AOT publish + C++ CTest).
REM #
REM # The managed shim is unit-tested via test\MsData.NativeAot.Tests inside
REM # build.bat above. THIS section additionally verifies the AOT compile +
REM # the native ABI surface by:
REM #   1. dotnet publish the shim for win-x64 (Native AOT -> pwiz_msdata.dll).
REM #   2. cmake configure + build of examples\cpp-aot-reader against the
REM #      AOT-published .lib.
REM #   3. run-tests.ps1 runs CTest, captures JUnit XML, and emits
REM #      ##teamcity[importData type='junit'] so each CTest case shows up as
REM #      its own test in TC's Tests tab.
REM #
REM # Requirements on the TC agent:
REM #   - vswhere.exe (ships with VS Installer; the AOT linker discovery uses it)
REM #   - cmake on PATH (VS-bundled or installed; run-tests.ps1 falls back to
REM #     the VS-bundled location automatically)
REM #
REM # The AOT publish is Release-only regardless of build.bat's config — the
REM # point is to validate the AOT toolchain + ABI, not to test that AOT works
REM # in Debug (which it doesn't optimize and would just slow the build).
REM #
REM # Tooling discovery: a stock TC agent has Visual Studio installed but no
REM # MSVC dev env initialized on PATH — neither link.exe (needed by ILC for
REM # the AOT native-compile step) nor cmake.exe (needed by the C++ build)
REM # resolve. We fix that by:
REM #   1. Locating VS via vswhere (the well-known Installer-dir path).
REM #   2. Calling VsDevCmd.bat -arch=amd64 to set up the MSVC env (link.exe,
REM #      Windows SDK, INCLUDE / LIB, etc.). Equivalent to the old
REM #      vcvarsall.bat amd64 — VsDevCmd is the VS 2017+ replacement.
REM #   3. Prepending the VS-bundled cmake bin to PATH (VsDevCmd doesn't add
REM #      cmake on its own; it lives under the IDE's CommonExtensions tree).
REM # ------------------------------------------------------------------------
set "VSWHERE_DIR=C:\Program Files (x86)\Microsoft Visual Studio\Installer"
set "VSWHERE_EXE=%VSWHERE_DIR%\vswhere.exe"
if not exist "%VSWHERE_EXE%" (
    set ERROR_TEXT=vswhere.exe not found at %VSWHERE_EXE% - Visual Studio Installer dir missing
    set EXIT=1
    goto error
)

REM # vswhere returns the VS installationPath on stdout; capture it.
set "VS_INSTALL="
for /f "usebackq tokens=*" %%i in (`"%VSWHERE_EXE%" -latest -property installationPath`) do set "VS_INSTALL=%%i"
if not defined VS_INSTALL (
    set ERROR_TEXT=vswhere returned no Visual Studio installation
    set EXIT=1
    goto error
)

REM # Put vswhere on PATH BEFORE calling VsDevCmd — VsDevCmd's own internal
REM # initialization invokes vswhere and prints a warning if it can't find it.
REM # The warning isn't fatal (the env still gets set up), but it makes the TC
REM # log look like something failed. ILC's Microsoft.NETCore.Native.targets
REM # also calls vswhere directly, so the PATH entry stays useful afterward.
set "PATH=%VSWHERE_DIR%;%PATH%"

echo ##teamcity[progressMessage 'VsDevCmd init ^(MSVC env + link.exe for AOT^)']
call "%VS_INSTALL%\Common7\Tools\VsDevCmd.bat" -arch=amd64 -no_logo
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 (set ERROR_TEXT=VsDevCmd init failed (exit %EXIT%) & goto error)

REM # VsDevCmd doesn't add the VS-bundled cmake (lives under the IDE tree, not
REM # the MSVC tools tree). Prepend it now so the `cmake` invocations below
REM # resolve. Agents that have a system-wide cmake on PATH already work
REM # transparently — this is the fallback for the typical "VS installed but
REM # cmake not separately installed" agent layout.
set "PATH=%VS_INSTALL%\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin;%PATH%"

echo ##teamcity[progressMessage 'dotnet publish MsData.NativeAot ^(win-x64 Native AOT^)']
dotnet publish "%SCRIPT_DIR%\src\MsData.NativeAot\MsData.NativeAot.csproj" -c Release -r win-x64 --verbosity minimal -nologo
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 (set ERROR_TEXT=Native AOT publish failed (exit %EXIT%) & goto error)

echo ##teamcity[progressMessage 'cmake configure ^(examples\cpp-aot-reader^)']
cmake -S "%SCRIPT_DIR%\examples\cpp-aot-reader" -B "%SCRIPT_DIR%\examples\cpp-aot-reader\build"
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 (set ERROR_TEXT=cmake configure failed (exit %EXIT%) & goto error)

echo ##teamcity[progressMessage 'cmake --build ^(examples\cpp-aot-reader^)']
cmake --build "%SCRIPT_DIR%\examples\cpp-aot-reader\build" --config Release
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 (set ERROR_TEXT=cmake build failed (exit %EXIT%) & goto error)

echo ##teamcity[progressMessage 'run-tests.ps1 ^(CTest + TC JUnit import^)']
pwsh -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%\examples\cpp-aot-reader\run-tests.ps1" -Config Release
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 (set ERROR_TEXT=CTest failed (exit %EXIT%) & goto error)

REM # Post-build hygiene checks (mirror scripts/misc/tcbuild.bat).
REM # Run from repo root so git sees the full working tree, not just pwiz-sharp/.
pushd "%SCRIPT_DIR%\.."

echo ##teamcity[progressMessage 'git ls-files --deleted (build should not delete tracked files)']
git ls-files --deleted >"%TEMP%\tcbuild-deleted.txt"
for /f %%A in ("%TEMP%\tcbuild-deleted.txt") do set DELETED_SIZE=%%~zA
if not "%DELETED_SIZE%"=="0" (
    echo ##teamcity[message text='Build deleted tracked files' status='ERROR']
    type "%TEMP%\tcbuild-deleted.txt"
    set EXIT=1
    set ERROR_TEXT=Build deleted tracked files
    popd
    goto error
)

echo ##teamcity[progressMessage 'git status --porcelain (build should not leave untracked files)']
git status --porcelain >"%TEMP%\tcbuild-dirty.txt"
for /f %%A in ("%TEMP%\tcbuild-dirty.txt") do set DIRTY_SIZE=%%~zA
if not "%DIRTY_SIZE%"=="0" (
    echo ##teamcity[message text='Build left uncommitted changes - extend .gitignore' status='ERROR']
    type "%TEMP%\tcbuild-dirty.txt"
    set EXIT=1
    set ERROR_TEXT=Build produced files not in .gitignore
    popd
    goto error
)

popd
popd
exit /b 0

:error
echo ##teamcity[message text='%ERROR_TEXT%' status='ERROR']
popd
exit /b %EXIT%
