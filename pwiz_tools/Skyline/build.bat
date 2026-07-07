@echo off
REM # NOTE: intentionally NOT `setlocal enabledelayedexpansion` at file scope.
REM # The test filter contains `!=` and delayed expansion would eat every `!`
REM # here (setting TEST_FILTER=`FullyQualifiedName!=X&FullyQualifiedName!=Y`
REM # would triger `!name!` parsing and mangle the whole string). Delayed
REM # expansion is enabled locally inside the small blocks that need it.
setlocal

REM # ------------------------------------------------------------------------
REM # Skyline build entry point. TeamCity calls this from tcbuild.bat; runs
REM # locally too. Mirrors pwiz-sharp\build.bat's shape (dotnet restore + build
REM # + parallel test with TC service messages) but scoped to the Skyline tree.
REM #
REM # Usage:
REM #   build.bat [Debug|Release] [--i-agree-to-the-vendor-licenses]
REM #             [--require-vendor-support] [--automated] [--coverage]
REM #
REM # Flags:
REM #   --i-agree-to-the-vendor-licenses
REM #       Acknowledge the vendor SDK EULAs. Forwarded to MSBuild as
REM #       -p:IAgreeToVendorLicenses=true, which lets the referenced pwiz-sharp
REM #       vendor projects extract the encrypted vendor archives and link the
REM #       real readers. Without it the transitive vendor references build in
REM #       their no-vendor-support mode. TeamCity passes this for CI.
REM #
REM #   --require-vendor-support
REM #       Fail the build if vendor support isn't enabled (i.e. if
REM #       --i-agree-to-the-vendor-licenses wasn't also passed). Use in CI to
REM #       guard against silently producing a stripped, no-vendor artifact.
REM #
REM #   --automated
REM #       Tag the assembly InformationalVersion with "(automated build)"
REM #       instead of "(developer build)". Passed to MSBuild as
REM #       -p:AutomatedBuild=true.
REM #
REM #   --coverage
REM #       Run the test step under JetBrains dotCover instead of `dotnet test`
REM #       directly. Emits a snapshot at TestResults\coverage.dcvr and an HTML
REM #       report at TestResults\coverage-report\. Auto-enabled when
REM #       TEAMCITY_VERSION is set (TC's Command Line runner isn't wrapped by
REM #       TC's built-in dotCover feature, so the script has to invoke it).
REM #
REM # Scope:
REM #   Only the Skyline projects already migrated to SDK-style csproj + net8
REM #   are built/tested here: Skyline.csproj and TestData.csproj. Test/,
REM #   TestFunctional/, TestConnected/ are still legacy csproj (net472-only)
REM #   and are skipped until they're ported. TestPerf/ and TestTutorial/ are
REM #   intentionally EXCLUDED from the standard build; run them separately
REM #   when needed.
REM # ------------------------------------------------------------------------

REM # Resolve to the directory this script lives in so we work from pwiz_tools\Skyline\
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

REM # Auto-enable coverage under TeamCity — TC's dotCover build feature only
REM # wraps its built-in .NET runner, not the Command Line runner that invokes
REM # this script. Without this, CI builds would produce no coverage data.
if defined TEAMCITY_VERSION set COVERAGE=1

set MSBUILD_PROPS=-p:Configuration=%CONFIG%
if %IAGREE%==1 set MSBUILD_PROPS=%MSBUILD_PROPS% -p:IAgreeToVendorLicenses=true
if %AUTOMATED%==1 set MSBUILD_PROPS=%MSBUILD_PROPS% -p:AutomatedBuild=true

if %IAGREE%==1 (
    echo ##teamcity[message text='Vendor support: ENABLED']
) else (
    echo ##teamcity[message text='Vendor support: DISABLED ^(no --i-agree-to-the-vendor-licenses^); building core only']
)

REM # Build targets: only the SDK-style / net8-capable projects. Skyline.csproj
REM # pulls in every ProjectReference (BiblioSpec, CommonMsData, ProteomeDb,
REM # ProteowizardWrapper, ZedGraph, plus the BlibBuild/BlibFilter tool projects
REM # under pwiz-sharp/Tools/BiblioSpec). TestData.csproj and Test.csproj add the
REM # integration + unit test suites next to Skyline's runtime output.
set BUILD_TARGET=Skyline.csproj TestData\TestData.csproj Test\Test.csproj

echo ##teamcity[progressMessage 'dotnet --version']
dotnet --version
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 (set ERROR_TEXT=dotnet not on PATH & goto error)

echo ##teamcity[progressMessage 'dotnet restore (%CONFIG%)']
dotnet restore Skyline.csproj %MSBUILD_PROPS%
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 (set ERROR_TEXT=dotnet restore Skyline failed & goto error)
dotnet restore TestData\TestData.csproj %MSBUILD_PROPS%
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 (set ERROR_TEXT=dotnet restore TestData failed & goto error)
dotnet restore Test\Test.csproj %MSBUILD_PROPS%
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 (set ERROR_TEXT=dotnet restore Test failed & goto error)

echo ##teamcity[progressMessage 'dotnet build Skyline (%CONFIG%)']
dotnet build Skyline.csproj -f net8.0-windows --no-restore -nologo %MSBUILD_PROPS%
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 (set ERROR_TEXT=dotnet build Skyline failed & goto error)

echo ##teamcity[progressMessage 'dotnet build TestData (%CONFIG%)']
dotnet build TestData\TestData.csproj -f net8.0-windows --no-restore -nologo %MSBUILD_PROPS%
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 (set ERROR_TEXT=dotnet build TestData failed & goto error)

echo ##teamcity[progressMessage 'dotnet build Test (%CONFIG%)']
dotnet build Test\Test.csproj -f net8.0-windows --no-restore -nologo %MSBUILD_PROPS%
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 (set ERROR_TEXT=dotnet build Test failed & goto error)

REM # ------------------------------------------------------------------------
REM # Test step
REM #
REM # TestData\TestData.csproj (integration) and Test\Test.csproj (unit) are net8-ready.
REM # TestFunctional\, TestConnected\ get added here as they get ported. TestPerf\ and
REM # TestTutorial\ are permanently excluded from the default build per project policy —
REM # kick them off manually when needed. Each project runs as a separate `dotnet test`.
REM #
REM # No filter: the entire TestData suite runs. Previously three
REM # WatersCalcurveTest cases (WatersCacheTest, WatersMultiReplicateTest,
REM # WatersMultiFileTest) hung at MemoryDocumentContainer.WaitForComplete
REM # because Skyline's IsFinal check required (IsFinal AND IsError) — a
REM # loader that finished successfully but left doc.IsLoaded=false would
REM # never satisfy either condition and the test blocked forever. Fixed
REM # in MemoryDocumentContainer.IsFinal to accept ANY final loader state.
REM # ------------------------------------------------------------------------
set TEST_TARGET=TestData\TestData.csproj Test\Test.csproj

set TC_TEST_RESULTS=%SCRIPT_DIR%\TestResults
if exist "%TC_TEST_RESULTS%" rmdir /s /q "%TC_TEST_RESULTS%"
mkdir "%TC_TEST_RESULTS%"

set TEST_FILTER=
set TEST_FILTER_ARG=
if defined TEST_FILTER if not "%TEST_FILTER%"=="" set TEST_FILTER_ARG=--filter "%TEST_FILTER%"

REM # Build up the logger flags without delayed expansion by using two SETs.
REM # (Delayed expansion is deliberately off at file scope so `!=` in the test
REM # filter stays literal.)
set TEST_LOGGERS=--logger trx --results-directory "%TC_TEST_RESULTS%"
if defined TEAMCITY_VERSION set TEST_LOGGERS=%TEST_LOGGERS% --logger teamcity
if not defined TEAMCITY_VERSION set TEST_LOGGERS=%TEST_LOGGERS% --logger "console;verbosity=normal"

echo ##teamcity[progressMessage 'dotnet test (%CONFIG%)']

REM # Dispatch to :run_coverage or :run_test via call so the SET EXIT=%ERRORLEVEL%
REM # inside each subroutine happens after the actual command runs. Doing this
REM # inline inside an if-block would need delayed expansion (%ERRORLEVEL% in an
REM # if-block is parsed at block start, not after the command).
if %COVERAGE%==1 (call :run_coverage) else (call :run_test)

if %EXIT% NEQ 0 (set ERROR_TEXT=dotnet test failed & goto error)

popd
exit /b 0

:run_coverage
echo ##teamcity[progressMessage 'dotnet tool restore - local manifest .config\dotnet-tools.json']
dotnet tool restore
if %ERRORLEVEL% NEQ 0 (
    set EXIT=2
    set ERROR_TEXT=`dotnet tool restore` failed; see .config\dotnet-tools.json. Coverage cannot run.
    goto :eof
)

set COVER_DIR=%TC_TEST_RESULTS%
set COVER_REPORT_DIR=%COVER_DIR%\coverage-report
set COVER_SNAPSHOT=%COVER_DIR%\coverage.dcvr
set COVER_FILTERS=+:module=Skyline*;+:module=pwiz.*;+:module=BiblioSpec;-:module=*Test*

REM # dotcover's dotnet-test wrapper covers one project at a time, so snapshot each
REM # project into cover-<project>.dcvr and merge them into COVER_SNAPSHOT for the report.
set EXIT=0
set MERGE_SOURCES=
for %%P in (%TEST_TARGET%) do call :cover_one "%%~P"
if %EXIT% NEQ 0 goto :eof

echo ##teamcity[progressMessage 'dotnet dotcover merge']
dotnet dotcover merge --Source="%MERGE_SOURCES%" --Output="%COVER_SNAPSHOT%"

echo ##teamcity[progressMessage 'dotnet dotcover report - HTML at %COVER_REPORT_DIR%']
if not exist "%COVER_REPORT_DIR%" mkdir "%COVER_REPORT_DIR%"
dotnet dotcover report --Source="%COVER_SNAPSHOT%" --Output="%COVER_REPORT_DIR%\index.html" --ReportType=HTML --HideAutoProperties
if %ERRORLEVEL% NEQ 0 echo ##teamcity[message text='dotCover report generation failed - snapshot is still at %COVER_SNAPSHOT%' status='WARNING']

REM # Emit the snapshot path as a TC service message so the dotCover build
REM # feature can pick it up. Harmless locally.
if defined TEAMCITY_VERSION echo ##teamcity[importData type='dotNetCoverage' tool='dotcover' path='%COVER_SNAPSHOT%']
goto :eof

REM # Cover a single test project (%1) into its own snapshot and append it to MERGE_SOURCES.
:cover_one
set _COVER_SNAP=%COVER_DIR%\cover-%~n1.dcvr
echo ##teamcity[progressMessage 'dotnet dotcover dotnet -- test %~1 ^(with coverage^)']
dotnet dotcover dotnet --Output="%_COVER_SNAP%" --Filters="%COVER_FILTERS%" --ReturnTargetExitCode -- test %~1 -f net8.0-windows --no-build %TEST_FILTER_ARG%%TEST_LOGGERS% --blame-hang --blame-hang-timeout 3min
if errorlevel 1 set EXIT=1
if "%MERGE_SOURCES%"=="" (set MERGE_SOURCES=%_COVER_SNAP%) else (set MERGE_SOURCES=%MERGE_SOURCES%;%_COVER_SNAP%)
goto :eof

:run_test
REM # dotnet test takes one project at a time; run each in TEST_TARGET and keep
REM # a non-zero exit if any of them fails.
set EXIT=0
for %%P in (%TEST_TARGET%) do (
    echo ##teamcity[progressMessage 'dotnet test %%P']
    dotnet test %%P -f net8.0-windows --no-build %TEST_FILTER_ARG%%TEST_LOGGERS% --blame-hang --blame-hang-timeout 3min
    if errorlevel 1 set EXIT=1
)
goto :eof

:error
echo ##teamcity[message text='%ERROR_TEXT%' status='ERROR']
popd
exit /b %EXIT%
