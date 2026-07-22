@echo off
setlocal enabledelayedexpansion

REM # ------------------------------------------------------------------------
REM # tcbuild.bat — single TeamCity entry point for Skyline.
REM #
REM # Mirrors pwiz-sharp\tcbuild.bat's shape: one Command Line step for the
REM # whole restore/build/test flow, plus TC-specific pre/post hygiene.
REM #
REM # Sequence:
REM #   1. dotnet --version (logs which SDK got picked, after global.json
REM #      pinning).
REM #   2. CleanSkyline.bat: wipe bin/obj/TestResults from every touched
REM #      Skyline sub-project so a stale build from a prior commit can't
REM #      leak into this one.
REM #   3. build.bat: dotnet restore + build + test. The test phase runs the
REM #      standard TeamCity per-commit check -- the full English suite PLUS the
REM #      three extra modes the old net472 SkylineWindows config ran (French pass0
REM #      build check over CommonTest+Test+TestData; the localized ja/zh import
REM #      tests; a pass1 functional subset), so the net8 build runs a superset of
REM #      what it did before. Every mode runs even if an earlier one has failing
REM #      tests (only a compile failure short-circuits); the build still ends red
REM #      if any mode failed. Args forwarded verbatim.
REM #   4. git ls-files --deleted: catches builds that delete tracked files.
REM #   5. git status --porcelain: catches builds that produce stray files
REM #      not covered by .gitignore.
REM #
REM # Usage:
REM #   tcbuild.bat [Debug|Release] [--automated] [--parallel]
REM #
REM # Args are forwarded verbatim to build.bat; see that script for flag
REM # semantics. TC should pass --automated for the standard CI run, and
REM # --parallel to spread the tests across Docker workers (needs Docker).
REM #
REM # Scope note:
REM #   TestPerf and TestTutorial are intentionally EXCLUDED from the standard
REM #   build (see build.bat header comment). If TC needs perf/tutorial runs
REM #   they should be separate build configurations invoking those csprojs
REM #   directly, not layered on top of this script.
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

REM # Clean before build: TC agents expect a fresh slate every run so stale
REM # bin/obj from a prior commit can't influence the current build.
REM # CleanSkyline.bat wipes every Skyline sub-project's bin/obj plus generated
REM # AssemblyInfo files. It doesn't touch the .NET runtime download cache or
REM # NuGet cache — those are content-addressed and safe across commits.
echo ##teamcity[progressMessage 'CleanSkyline.bat']
call "%SCRIPT_DIR%\CleanSkyline.bat"
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 (set "ERROR_TEXT=CleanSkyline.bat failed" & goto error)

echo ##teamcity[progressMessage 'Skyline build.bat %*']
call "%SCRIPT_DIR%\build.bat" %*
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 (set "ERROR_TEXT=build.bat failed" & goto error)

REM # Post-build hygiene checks (mirror pwiz-sharp\tcbuild.bat).
REM # Run from repo root so git sees the full working tree, not just Skyline\.
REM # Skyline lives at pwiz_tools\Skyline inside the pwiz checkout, so the
REM # repo root is two levels up from SCRIPT_DIR.
pushd "%SCRIPT_DIR%\..\.."

echo ##teamcity[progressMessage 'git ls-files --deleted (build should not delete tracked files)']
git ls-files --deleted >"%TEMP%\tcbuild-skyline-deleted.txt"
for /f %%A in ("%TEMP%\tcbuild-skyline-deleted.txt") do set DELETED_SIZE=%%~zA
if not "%DELETED_SIZE%"=="0" (
    echo ##teamcity[message text='Build deleted tracked files' status='ERROR']
    type "%TEMP%\tcbuild-skyline-deleted.txt"
    set EXIT=1
    set "ERROR_TEXT=Build deleted tracked files"
    popd
    goto error
)

echo ##teamcity[progressMessage 'git status --porcelain (build should not leave untracked files)']
git status --porcelain >"%TEMP%\tcbuild-skyline-dirty.txt"
for /f %%A in ("%TEMP%\tcbuild-skyline-dirty.txt") do set DIRTY_SIZE=%%~zA
if not "%DIRTY_SIZE%"=="0" (
    echo ##teamcity[message text='Build left uncommitted changes - extend .gitignore' status='ERROR']
    type "%TEMP%\tcbuild-skyline-dirty.txt"
    set EXIT=1
    set "ERROR_TEXT=Build produced files not in .gitignore"
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
