@echo off
setlocal enabledelayedexpansion

REM # ------------------------------------------------------------------------
REM # pwiz-sharp build entry point. TeamCity calls this from the
REM # ProteoWizard_CoreWindowsNet config; runs locally too.
REM #
REM # Usage:
REM #   build.bat [Debug|Release] [--i-agree-to-the-vendor-licenses]
REM #             [--require-vendor-support] [--automated] [--coverage]
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
REM #
REM #   --coverage
REM #       Run the test step under JetBrains dotCover instead of `dotnet test`
REM #       directly. Emits a snapshot at TestResults\coverage.dcvr and an HTML
REM #       report at TestResults\coverage-report\. dotCover is restored from
REM #       the local tool manifest at .config\dotnet-tools.json — no global
REM #       install needed, and the version is pinned in source.
REM #
REM #       Auto-enabled when TEAMCITY_VERSION is set. TeamCity's coverage
REM #       wrapping only attaches to the built-in .NET runner, not Command
REM #       Line steps, so the script has to invoke dotCover itself in CI;
REM #       the importData service message hands the snapshot to TC's
REM #       dotCover build feature for the report UI.
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
set COVERAGE=0
set ERROR_TEXT=

REM # Parse args. First non-flag arg is the configuration (Debug|Release).
:parseargs
if "%~1"=="" goto endparse
if /i "%~1"=="--i-agree-to-the-vendor-licenses" (set IAGREE=1) else ^
if /i "%~1"=="--require-vendor-support" (set REQUIRE_VENDOR=1) else ^
if /i "%~1"=="--automated" (set AUTOMATED=1) else ^
if /i "%~1"=="--coverage" (set COVERAGE=1) else ^
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

REM # Auto-enable coverage under TeamCity since the TC dotCover build feature
REM # only wraps its built-in .NET runner — not the Command Line runner that
REM # invokes this script. Without this, CI builds would produce no coverage data.
if defined TEAMCITY_VERSION set COVERAGE=1

set MSBUILD_PROPS=-p:Configuration=%CONFIG%
if %IAGREE%==1 set MSBUILD_PROPS=%MSBUILD_PROPS% -p:IAgreeToVendorLicenses=true
if %AUTOMATED%==1 set MSBUILD_PROPS=%MSBUILD_PROPS% -p:AutomatedBuild=true

if %IAGREE%==1 (
    echo ##teamcity[message text='Vendor support: ENABLED']
    set BUILD_TARGET=Pwiz.sln
) else (
    echo ##teamcity[message text='Vendor support: DISABLED ^(no --i-agree-to-the-vendor-licenses^); building core only']
    REM # No-vendor build: build msconvert + tests that don't depend on vendors.
    REM # Vendor projects in Pwiz.sln are skipped via explicit project list.
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

REM # Test discovery: with vendor support, run all 12 test projects via the
REM # solution. Without vendor support, run only the projects that don't pull
REM # vendor refs (Bruker.Tests / Thermo.Tests / Waters.Tests / Agilent.Tests /
REM # Sciex.Tests / Shimadzu.Tests / UNIFI.Tests reference vendor projects).
if %IAGREE%==1 (
    set TEST_TARGET=Pwiz.sln
) else (
    set TEST_TARGET=test\Util.Tests\Util.Tests.csproj test\Common.Tests\Common.Tests.csproj test\MsData.Tests\MsData.Tests.csproj test\Analysis.Tests\Analysis.Tests.csproj test\MsConvert.Tests\MsConvert.Tests.csproj
)

REM # Single `dotnet test` invocation: msbuild runs the per-project VSTest target
REM # in parallel, and each project's TC.VSTest.TestAdapter emits
REM # ##teamcity[testStarted name='<asm>: <fqn>' suiteName='<asm>' ...] events.
REM # We deliberately do NOT wrap with our own ##teamcity[testSuiteStarted ...] —
REM # the outer wrapper would override the per-test suiteName attribute and lump
REM # everything under one suite. With no wrapper, TC honors the adapter's
REM # suiteName and produces per-assembly suites in the Tests tab.
REM #
REM # Stdout-corruption mitigation: the SCIEX Clearcore2 SDK's log4net default
REM # appender used to flood stdout with `[INFO]`/`[DEBUG]` lines that mid-line
REM # interleaved with `##teamcity[testFinished ...]` messages and got TC to drop
REM # the malformed messages. Sciex.Tests/SilenceSciexSdkLogging silences the SDK
REM # via log4net.LogManager.GetRepository().Threshold = Off, so the only thing
REM # writing TC service messages on the build's stdout is the test adapter
REM # itself — there's no concurrent writer left to corrupt the stream.
REM #
REM # Loggers:
REM #   trx     — per-assembly TRX files for IDE consumption / coverage replay.
REM #   console — readable stdout for humans / local CI logs.
REM #   teamcity — TC service messages (only when TEAMCITY_VERSION is set).
set TC_TEST_RESULTS=%SCRIPT_DIR%\TestResults
if exist "%TC_TEST_RESULTS%" rmdir /s /q "%TC_TEST_RESULTS%"
set TEST_LOGGERS=--logger:"trx" --logger:"console;verbosity=normal" --results-directory:"%TC_TEST_RESULTS%"
if defined TEAMCITY_VERSION set TEST_LOGGERS=%TEST_LOGGERS% --logger:teamcity

echo ##teamcity[progressMessage 'dotnet test (%CONFIG%)']

if %COVERAGE%==1 (
    REM # Run the test step under JetBrains dotCover. Snapshot is dropped at
    REM # TestResults\coverage.dcvr; an HTML report is emitted alongside in
    REM # TestResults\coverage-report\.
    REM #
    REM # Filters keep the report focused on production code:
    REM #   +:module=Pwiz.*    — every assembly we ship (Pwiz.Util, Pwiz.Data.MsData,
    REM #                       Pwiz.Vendor.*, Pwiz.Tools.MsConvert, Pwiz.TestHarness)
    REM #   -:module=*.Tests   — exclude the test fixtures themselves
    REM #   -:module=msconvert-sharp — exclude the wrapper exe (entry-point only)
    REM #
    REM # dotCover ships as a dotnet tool. We use a LOCAL tool manifest
    REM # (.config\dotnet-tools.json) and `dotnet tool restore` so the build is
    REM # self-contained — TC agents don't need a separate global install, and
    REM # the version is pinned in source. After restore the tool is invoked as
    REM # `dotnet dotcover` (the local-tool dispatcher matches the manifest's
    REM # "dotnet-dotCover" command name minus the `dotnet-` prefix).
    echo ##teamcity[progressMessage 'dotnet tool restore - local manifest .config\dotnet-tools.json']
    dotnet tool restore
    if !ERRORLEVEL! NEQ 0 (
        set EXIT=2
        set ERROR_TEXT=`dotnet tool restore` failed; see .config\dotnet-tools.json. Coverage cannot run.
        goto error
    )

    set COVER_DIR=%TC_TEST_RESULTS%
    if not exist "!COVER_DIR!" mkdir "!COVER_DIR!"
    set COVER_SNAPSHOT=!COVER_DIR!\coverage.dcvr
    set COVER_REPORT_DIR=!COVER_DIR!\coverage-report
    set COVER_FILTERS=+:module=Pwiz.*;-:module=*.Tests;-:module=msconvert-sharp

    echo ##teamcity[progressMessage 'dotnet dotcover dotnet test - snapshot at !COVER_SNAPSHOT!']
    dotnet dotcover dotnet --Output="!COVER_SNAPSHOT!" --Filters="!COVER_FILTERS!" --ReturnTargetExitCode -- test %TEST_TARGET% --no-build %MSBUILD_PROPS% %TEST_LOGGERS%
    set EXIT=!ERRORLEVEL!

    REM # Generate an HTML report from the snapshot — useful locally; on TC the
    REM # dotCover build feature renders this from the snapshot directly.
    if !EXIT! EQU 0 (
        echo ##teamcity[progressMessage 'dotnet dotcover report - HTML at !COVER_REPORT_DIR!']
        if not exist "!COVER_REPORT_DIR!" mkdir "!COVER_REPORT_DIR!"
        dotnet dotcover report --Source="!COVER_SNAPSHOT!" --Output="!COVER_REPORT_DIR!\index.html" --ReportType=HTML --HideAutoProperties
        set REPORT_EXIT=!ERRORLEVEL!
        if !REPORT_EXIT! NEQ 0 (
            echo ##teamcity[message text='dotCover report generation failed - snapshot is still at !COVER_SNAPSHOT!' status='WARNING']
        )
    )

    REM # Emit the snapshot path as a TC service message so the dotCover build
    REM # feature can pick it up. Harmless locally.
    if defined TEAMCITY_VERSION echo ##teamcity[importData type='dotNetCoverage' tool='dotcover' path='!COVER_SNAPSHOT!']
) else (
    dotnet test %TEST_TARGET% --no-build %MSBUILD_PROPS% %TEST_LOGGERS%
    set EXIT=!ERRORLEVEL!
)

if %EXIT% NEQ 0 (set ERROR_TEXT=dotnet test failed & goto error)

popd
exit /b 0

:error
echo ##teamcity[message text='%ERROR_TEXT%' status='ERROR']
popd
exit /b %EXIT%
