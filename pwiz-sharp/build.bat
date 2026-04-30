@echo off
setlocal enabledelayedexpansion

REM # ------------------------------------------------------------------------
REM # pwiz-sharp build entry point. TeamCity calls this from the
REM # ProteoWizard_CoreWindowsNet config; runs locally too.
REM #
REM # Usage:
REM #   build.bat [Debug|Release] [--i-agree-to-the-vendor-licenses]
REM #             [--require-vendor-support] [--automated]
REM #
REM # Flags:
REM #   --i-agree-to-the-vendor-licenses
REM #       Acknowledge the vendor SDK EULAs. Required for the build to extract
REM #       the encrypted vendor archives (Sciex / Bruker / Waters / Agilent /
REM #       Thermo) from pwiz_aux\msrc\utility\vendor_api_*.7z and link the
REM #       readers against them. Without this flag, msconvert-sharp builds in
REM #       NO_VENDOR_SUPPORT mode (built-in mzML / mzXML / MGF readers only).
REM #
REM #   --require-vendor-support
REM #       Fail the build if vendor support isn't enabled. Use in CI to make
REM #       sure --i-agree was passed, instead of silently building a stripped
REM #       artifact.
REM #
REM #   --automated
REM #       Tag the assembly InformationalVersion with "(automated build)"
REM #       instead of "(developer build)". Mirrors cpp generate-version.jam.
REM # ------------------------------------------------------------------------

REM # Resolve to the directory this script lives in so we work from pwiz-sharp/
set SCRIPT_DIR=%~dp0
set SCRIPT_DIR=%SCRIPT_DIR:~0,-1%
pushd "%SCRIPT_DIR%"

set EXIT=0
set CONFIG=Release
set IAGREE=0
set REQUIRE_VENDOR=0
set AUTOMATED=0
set ERROR_TEXT=

REM # Parse args. First non-flag arg is the configuration (Debug|Release).
:parseargs
if "%~1"=="" goto endparse
if /i "%~1"=="--i-agree-to-the-vendor-licenses" (set IAGREE=1) else ^
if /i "%~1"=="--require-vendor-support" (set REQUIRE_VENDOR=1) else ^
if /i "%~1"=="--automated" (set AUTOMATED=1) else ^
if /i "%~1"=="Debug" (set CONFIG=Debug) else ^
if /i "%~1"=="Release" (set CONFIG=Release) else (
    echo Unrecognized argument: %~1 1>&2
    set EXIT=2
    set ERROR_TEXT=Unrecognized argument: %~1
    goto error
)
shift
goto parseargs
:endparse

if %REQUIRE_VENDOR%==1 if %IAGREE%==0 (
    set EXIT=2
    set ERROR_TEXT=--require-vendor-support set but --i-agree-to-the-vendor-licenses was not passed; refusing to build a stripped artifact.
    goto error
)

set MSBUILD_PROPS=-p:Configuration=%CONFIG%
if %IAGREE%==1 set MSBUILD_PROPS=%MSBUILD_PROPS% -p:IAgreeToVendorLicenses=true
if %AUTOMATED%==1 set MSBUILD_PROPS=%MSBUILD_PROPS% -p:AutomatedBuild=true

if %IAGREE%==1 (
    echo ##teamcity[message text='Vendor support: ENABLED']
    set BUILD_TARGET=Pwiz.slnx
) else (
    echo ##teamcity[message text='Vendor support: DISABLED ^(no --i-agree-to-the-vendor-licenses^); building core only']
    REM # No-vendor build: build msconvert + tests that don't depend on vendors.
    REM # Vendor projects in Pwiz.slnx are skipped via explicit project list.
    set BUILD_TARGET=src\MsConvert\MsConvert.csproj
)

echo ##teamcity[progressMessage 'dotnet --version']
dotnet --version
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 (set ERROR_TEXT=dotnet not on PATH & goto error)

echo ##teamcity[progressMessage 'dotnet restore (%CONFIG%, vendor=%IAGREE%)']
dotnet restore %BUILD_TARGET% %MSBUILD_PROPS%
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 (set ERROR_TEXT=dotnet restore failed & goto error)

echo ##teamcity[progressMessage 'dotnet build (%CONFIG%, vendor=%IAGREE%)']
dotnet build %BUILD_TARGET% --no-restore -nologo %MSBUILD_PROPS%
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 (set ERROR_TEXT=dotnet build failed & goto error)

REM # Test discovery: with vendor support, run the full slnx (all 8 projects).
REM # Without vendor support, run only the projects that don't pull vendor refs.
if %IAGREE%==1 (
    set TEST_TARGET=Pwiz.slnx
    set TEST_SUITE_NAME=pwiz-sharp ^(full^)
) else (
    REM # Bruker.Tests / Thermo.Tests / Waters.Tests reference vendor projects, so they
    REM # can't build in NO_VENDOR_SUPPORT mode.
    set TEST_TARGET=test\Util.Tests\Util.Tests.csproj test\Common.Tests\Common.Tests.csproj test\MsData.Tests\MsData.Tests.csproj test\Analysis.Tests\Analysis.Tests.csproj test\MsConvert.Tests\MsConvert.Tests.csproj
    set TEST_SUITE_NAME=pwiz-sharp ^(core^)
)

REM # TeamCity test decoration: wrap the dotnet test run in a testSuite so the
REM # individual test results (already TeamCity-formatted by the dotnet test
REM # runner when TEAMCITY_VERSION is set in env) roll up under one suite name
REM # in the build's Tests tab. Mirrors Skyline TestRunner's pattern.
if defined TEAMCITY_VERSION echo ##teamcity[testSuiteStarted name='%TEST_SUITE_NAME%']

echo ##teamcity[progressMessage 'dotnet test (%CONFIG%)']
REM # --logger trx writes test results files that TeamCity's test-results step
REM # can ingest; --logger console gives readable stdout. dotnet test auto-emits
REM # ##teamcity[testStarted/Finished] markers when TEAMCITY_VERSION is set.
dotnet test %TEST_TARGET% --no-build %MSBUILD_PROPS% --logger:"trx" --logger:"console;verbosity=normal"
set EXIT=%ERRORLEVEL%

if defined TEAMCITY_VERSION echo ##teamcity[testSuiteFinished name='%TEST_SUITE_NAME%']

if %EXIT% NEQ 0 (set ERROR_TEXT=dotnet test failed & goto error)

popd
exit /b 0

:error
echo ##teamcity[message text='%ERROR_TEXT%' status='ERROR']
popd
exit /b %EXIT%
