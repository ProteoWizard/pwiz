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
REM # dotCover note: TeamCity's "dotCover" build feature attaches to the
REM # configuration as a whole, not to a specific runner step, so it still
REM # wraps the test process when build.bat invokes `dotnet test`. No
REM # dotCover invocation needed in this script.
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

echo ##teamcity[progressMessage 'pwiz-sharp build.bat %*']
call "%SCRIPT_DIR%\build.bat" %*
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 (set ERROR_TEXT=build.bat failed (exit %EXIT%) & goto error)

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
