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
REM #   2. build.bat: dotnet restore + build + test. The test phase runs the
REM #      standard TeamCity per-commit check -- the full English suite PLUS the
REM #      three extra modes the old net472 SkylineWindows config ran (French pass0
REM #      build check over CommonTest+Test+TestData; the localized ja/zh import
REM #      tests; a pass1 functional subset), so the net8 build runs a superset of
REM #      what it did before. Every mode runs even if an earlier one has failing
REM #      tests (only a compile failure short-circuits); the build still ends red
REM #      if any mode failed. Args forwarded verbatim, plus the distro zip
REM #      names below - one invocation, not a separate step. build.bat writes
REM #      the zips to bin\staging\<Config> (gitignored, so the hygiene
REM #      checks below stay clean) after staging but BEFORE the test run, so
REM #      TC still gets its artifacts from a build whose tests failed.
REM #   3. git ls-files --deleted: catches builds that delete tracked files.
REM #   4. git status --porcelain: catches builds that produce stray files
REM #      not covered by .gitignore.
REM #
REM # No clean step: the build configuration runs clean.bat as its first step,
REM # before the code inspection, so the whole build - inspection included -
REM # starts from one clean slate. Run clean.bat first when reproducing a CI
REM # build locally.
REM #
REM # Usage:
REM #   tcbuild.bat [Debug|Release] [--automated] [--parallel] [--with-tutorial-perf]
REM #
REM # The zip names are appended by this script, not taken from the caller. For a
REM # one-off artifact (e.g. SkylineTesterWithTestData.zip) call build.bat directly.
REM #
REM # Args are forwarded verbatim to build.bat; see that script for flag
REM # semantics. TC should pass --automated for the standard CI run, and
REM # --parallel to spread the tests across Docker workers (needs Docker).
REM #
REM # Scope note:
REM #   build.bat builds and stages every test project, TestTutorial and TestPerf
REM #   included, so the SkylineTester.zip this script produces carries every
REM #   test DLL. The tests are another matter: by default this script adds
REM #   --skip-tutorial-tests, so the per-commit run leaves the tutorial suite to
REM #   the Perf/Tutorial configuration (tc-perftests.bat), and perf tests stay
REM #   out because nothing sets perftests=on. Pass --with-tutorial-perf to run
REM #   both: the skip is dropped and build.bat adds perftests=on.
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

REM # Directories the tree used to have tracked files in (pwiz-sharp/ before the C++
REM # retirement, installer/ and pwiz/src|test before the relayout, the removed
REM # BullseyeSharp submodule). A persistent agent checkout that built those revisions
REM # still carries their gitignored outputs or the submodule's working tree, which the
REM # current .gitignore no longer covers, and the hygiene check at the end would report
REM # them as files the build left behind. Only a directory with no tracked files is
REM # swept, so this is a no-op on a fresh checkout and harmless on any branch.
REM # Mirrors the sweep in the root tcbuild.bat.
set "REPO_ROOT=%SCRIPT_DIR%\..\.."
for %%d in (pwiz-sharp installer pwiz\src pwiz\test pwiz_tools\Skyline\Executables\BullseyeSharp) do (
    if exist "%REPO_ROOT%\%%d" (
        git -C "%REPO_ROOT%" ls-files --error-unmatch "%%d" >nul 2>&1 || (
            echo ##teamcity[message text='Removing stale untracked directory %%d left by an earlier layout']
            rmdir /s /q "%REPO_ROOT%\%%d"
        )
    )
)

REM # The three distro zips the Jamfile used to produce. Appended here rather
REM # than left to the caller so every TC configuration yields the same artifacts.
set DISTRO_ZIPS=SkylineTester.zip SkylineNightly.zip BiblioSpec.zip

REM # Tutorial tests stay out of the per-commit run unless --with-tutorial-perf asks for
REM # them - see the scope note above. That flag is forwarded with the rest, and
REM # build.bat turns it into perftests=on.
set TUTORIAL_ARG=--skip-tutorial-tests
for %%A in (%*) do if /i "%%~A"=="--with-tutorial-perf" set TUTORIAL_ARG=

echo ##teamcity[progressMessage 'Skyline build.bat %* %TUTORIAL_ARG% %DISTRO_ZIPS%']
call "%SCRIPT_DIR%\build.bat" %* %TUTORIAL_ARG% %DISTRO_ZIPS%
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
