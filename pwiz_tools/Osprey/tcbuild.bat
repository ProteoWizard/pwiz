@echo off
REM TeamCity entry point: build + test Osprey (with dotCover on master) and TeamCity service
REM messages, then package the unsigned redistributable installers so they are
REM published as artifacts of this per-commit config (the Tier-C "testing"
REM installers a collaborator can grab before an official release). TeamCity
REM invokes this batch directly as the build step; smart-build trigger wiring for
REM pwiz_tools/Osprey/** is in scripts/misc/vcs_trigger_and_paths_config.py.
REM
REM Usage:
REM   tcbuild.bat [--i-agree-to-the-vendor-licenses] [--require-vendor-support]
REM
REM   --i-agree-to-the-vendor-licenses
REM       Build and package with the instrument vendor readers, so the installers read
REM       .raw, .d, .wiff and the other vendor formats. The agreement is an argument of the
REM       TeamCity build step, as it is for the root tcbuild.bat, never a default of this
REM       script: a file in the repository cannot agree to the vendor licenses on anyone's
REM       behalf. Without it every vendor reader builds in its no-vendor-support mode and
REM       the installers read mzML only.
REM   --require-vendor-support
REM       Fail unless the agreement was given, so a configuration that loses it fails
REM       instead of silently publishing installers that cannot read vendor files.
REM   TeamCity passes both.
REM
REM Pre-requisites on the build agent:
REM   * pwsh (PowerShell 7+) on PATH (project standard; no powershell.exe fallback)
REM   * Visual Studio Build Tools (MSBuild + vstest.console.exe)
REM   * .NET 10 SDK
REM   (dotCover and the ReSharper command line tools are restored from
REM   .config\dotnet-tools.json, so they need no agent provisioning.)
REM   Inno Setup 6 (for the Setup.exe) is self-provisioned by package.ps1 through
REM   scripts/installer/Ensure-InnoSetup.ps1 if absent, so no manual agent step
REM   is needed.
REM
REM Outputs consumed by TeamCity:
REM   * pwiz_tools/Osprey/TestResults/*.trx                  (vstest importData)
REM   * pwiz_tools/Osprey/TestResults/*.dcvr                 (dotCover importData, master only)
REM   * pwiz_tools/Osprey/dist/Osprey-<ver>-win-x64.zip      (publishArtifacts)
REM   * pwiz_tools/Osprey/dist/Osprey-Setup-<ver>.exe         (publishArtifacts)
REM   * ReSharper inspectcode report                         (importData, Inspections tab)
REM   The installers are unsigned here; signing is a later CI step. The
REM   publishArtifacts service messages are emitted by package.ps1 -TeamCity, so
REM   no server-side artifact-path configuration is required.

setlocal

REM Captured before the argument loop: `shift` shifts %0 as well, so %~dp0 afterwards
REM would point at whatever argument ended up in slot 0.
set SCRIPT_DIR=%~dp0
set VENDOR=
set REQUIRE_VENDOR=0
:parseargs
if "%~1"=="" goto endparse
if /i "%~1"=="--i-agree-to-the-vendor-licenses" (
    set VENDOR=-IAgreeToVendorLicenses
) else if /i "%~1"=="--require-vendor-support" (
    set REQUIRE_VENDOR=1
) else (
    echo ##teamcity[buildProblem description='tcbuild.bat: unknown argument %~1']
    exit /b 2
)
shift
goto parseargs
:endparse

if %REQUIRE_VENDOR%==1 if "%VENDOR%"=="" (
    echo ##teamcity[buildProblem description='--require-vendor-support was given without --i-agree-to-the-vendor-licenses: refusing to publish installers that cannot read vendor files']
    exit /b 2
)
if "%VENDOR%"=="" (
    echo ##teamcity[message text='Vendor support: DISABLED ^(no --i-agree-to-the-vendor-licenses^); the installers read mzML only' status='WARNING']
) else (
    echo Vendor support: ENABLED
)

REM dotCover coverage only on master: it makes the subset-pipeline tests ~8x slower,
REM and master builds after each merge are enough to watch coverage over time. See
REM tc-is-default-branch.ps1.
set COVERAGE=-Coverage
pwsh -NoProfile -File "%SCRIPT_DIR%tc-is-default-branch.ps1"
REM Exit 2 is "not the default branch"; any other failure keeps coverage.
if %ERRORLEVEL% EQU 2 set COVERAGE=
if "%COVERAGE%"=="" (echo Not the default branch: running tests without coverage.) else (echo Collecting coverage.)

REM A from-scratch compile, as the Skyline configuration's Clean step gives it: a project that is
REM already up to date emits no warnings, so an incremental build could not be checked for them.
call "%SCRIPT_DIR%..\..\clean.bat"
if %ERRORLEVEL% NEQ 0 exit /b %ERRORLEVEL%

REM Every compile appends its warnings here, and tcinspect.ps1 judges them at the end. Nothing
REM promotes a warning to an error: that would stop the build before the tests ran.
set BUILD_WARNINGS=%TEMP%\osprey-inspectcode\build-warnings.log
if exist "%BUILD_WARNINGS%" del /q "%BUILD_WARNINGS%"

REM A failed step no longer stops the build: the steps after it still run and report, so the log
REM carries everything, and the first failure's exit code is the build's. Each step raises its
REM own buildProblem, so a test failure and a warning failure each still read as themselves.
set RESULT=0

pwsh -NoProfile -File "%SCRIPT_DIR%build.ps1" -TeamCity %COVERAGE% -Configuration Release %VENDOR% -BuildWarningsPath "%BUILD_WARNINGS%"
if %ERRORLEVEL% NEQ 0 if %RESULT%==0 set RESULT=%ERRORLEVEL%

REM CarafeSharp (pwiz_tools/CarafeSharp) trains the spectral libraries Osprey searches and
REM has no config of its own, so this one builds and tests it too, with its own test results
REM and coverage snapshot. It needs no test data: the Carafe parity tests run on committed
REM subsets, and the full-size ones are developer-run (scripts/Compare-CarafeParity.ps1). It
REM reads no instrument files, so it takes no vendor agreement.
pwsh -NoProfile -File "%SCRIPT_DIR%..\CarafeSharp\build.ps1" -TeamCity %COVERAGE% -BuildWarningsPath "%BUILD_WARNINGS%"
if %ERRORLEVEL% NEQ 0 if %RESULT%==0 set RESULT=%ERRORLEVEL%

REM No installers from a build whose compile or tests failed.
if %RESULT% NEQ 0 (
    echo Skipping the installers: an earlier step failed.
    goto inspect
)
pwsh -NoProfile -File "%SCRIPT_DIR%package.ps1" -TeamCity -Rid win-x64 -Setup %VENDOR%
if %ERRORLEVEL% NEQ 0 if %RESULT%==0 set RESULT=%ERRORLEVEL%

:inspect
REM Last, so compiler warnings and ReSharper findings fail the build only after the tests and the
REM installers have run: without this check, warnings the local gate (Build-Osprey.ps1
REM -RunInspection) catches reached PRs and master with every check green.
pwsh -NoProfile -File "%SCRIPT_DIR%tcinspect.ps1" -TeamCity -Configuration Release %VENDOR% -BuildWarningsPath "%BUILD_WARNINGS%"
if %ERRORLEVEL% NEQ 0 if %RESULT%==0 set RESULT=%ERRORLEVEL%

exit /b %RESULT%
