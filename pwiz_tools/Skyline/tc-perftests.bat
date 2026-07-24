@echo off
setlocal

REM # ------------------------------------------------------------------------
REM # tc-perftests.bat -- TeamCity entry point for the Skyline net8 perf +
REM # tutorial test suites.
REM #
REM # TestPerf and TestTutorial are intentionally EXCLUDED from the per-commit
REM # build (build.bat / tcbuild.bat) because they download large vendor datasets
REM # and run for a long time. This is the "separate build configuration invoking
REM # those csprojs directly" that tcbuild.bat's scope note calls for: it builds
REM # the net8 SDK Skyline tree + TestPerf + TestTutorial, stages them, and runs
REM # both suites through the Skyline TestRunner harness (the functional/perf UI
REM # tests are written for it; `dotnet test` can't run them) with perftests=on.
REM #
REM # Usage:
REM #   tc-perftests.bat [Debug|Release] [--i-agree-to-the-vendor-licenses]
REM #                    [--require-vendor-support] [--automated] [--parallel]
REM #
REM # Flags (same semantics as build.bat):
REM #   --i-agree-to-the-vendor-licenses
REM #       Link the real vendor readers (-p:IAgreeToVendorLicenses=true). Perf and
REM #       tutorial tests import vendor .raw/.wiff/.d data, so this is effectively
REM #       REQUIRED -- without it the vendor-format tests fail to open their inputs.
REM #   --require-vendor-support  Fail unless vendor support is enabled.
REM #   --automated  Tag InformationalVersion "(automated build)".
REM #   --parallel   Spread tests across Docker workers (parallelmode=server).
REM #       NOTE: most perf tests are marked NoParallelTesting(RESOURCE_INTENSIVE);
REM #       the default host-sequential run is the reliable mode for them.
REM #
REM # Environment:
REM #   SKYLINE_TEST_WORKERS   parallel worker count (1 host + N-1 Docker); default 8.
REM #   SKYLINE_TEST_PARALLEL  set to 1 to prefer the parallel Docker run (= --parallel).
REM #   SKYLINE_PERF_LANGUAGE  UI language for the run; default en.
REM #   SKYLINE_TEST_ARGS      if set, skip the two standard suites and run a single
REM #                          TestRunner with these args instead (still perftests=on),
REM #                          e.g. test=TestSciexPrmCeOptimization.
REM #
REM # Scope:
REM #   Builds Skyline.csproj + TestPerf.csproj + TestTutorial.csproj +
REM #   TestRunner.csproj (plus best-effort native Hardklor.exe, needed by the
REM #   feature-detection perf tests), stages Skyline + TestRunner + TestPerf +
REM #   TestTutorial into bin\staging-net8\<Config>, and runs:
REM #     (a) TestPerf.dll     perftests=on   (all perf tests)
REM #     (b) TestTutorial.dll perftests=on   (all tutorial tests)
REM #   Both suites always run so one red suite doesn't hide the other; the build
REM #   ends red if either had a failure. Each is filtered to its own DLL so the
REM #   TestFunctional/TestUtil DLLs copied alongside as dependencies don't run.
REM # ------------------------------------------------------------------------

set SCRIPT_DIR=%~dp0
set SCRIPT_DIR=%SCRIPT_DIR:~0,-1%
pushd "%SCRIPT_DIR%"

set EXIT=0
set CONFIG=Release
set IAGREE=0
set REQUIRE_VENDOR=0
set AUTOMATED=0
set SEQUENTIAL=1
set ERROR_TEXT=

if "%SKYLINE_TEST_PARALLEL%"=="1" set SEQUENTIAL=0
if not defined SKYLINE_TEST_WORKERS set SKYLINE_TEST_WORKERS=8
if not defined SKYLINE_PERF_LANGUAGE set SKYLINE_PERF_LANGUAGE=en

REM # Parse args. First non-flag arg is the configuration (Debug|Release).
:parseargs
if "%~1"=="" goto endparse
if /i "%~1"=="--i-agree-to-the-vendor-licenses" (set IAGREE=1) else ^
if /i "%~1"=="--require-vendor-support" (set REQUIRE_VENDOR=1) else ^
if /i "%~1"=="--automated" (set AUTOMATED=1) else ^
if /i "%~1"=="--parallel" (set SEQUENTIAL=0) else ^
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
    set ERROR_TEXT=--require-vendor-support set but --i-agree-to-the-vendor-licenses was not passed; refusing to run a stripped artifact.
    goto error
)

set MSBUILD_PROPS=-p:Configuration=%CONFIG%
if %IAGREE%==1 set MSBUILD_PROPS=%MSBUILD_PROPS% -p:IAgreeToVendorLicenses=true
if %AUTOMATED%==1 set MSBUILD_PROPS=%MSBUILD_PROPS% -p:AutomatedBuild=true

if %IAGREE%==1 (
    echo ##teamcity[message text='Vendor support: ENABLED']
) else (
    echo ##teamcity[message text='Vendor support: DISABLED ^(no --i-agree-to-the-vendor-licenses^); perf/tutorial vendor-format tests will FAIL to open their inputs' status='WARNING']
)

REM # Skyline.csproj pulls in every ProjectReference (BiblioSpec, CommonMsData,
REM # ProteomeDb, ProteowizardWrapper, ZedGraph, the pwiz-sharp vendor + BiblioSpec
REM # tool projects, ...). TestPerf/TestTutorial add the two suites (and copy
REM # TestUtil/TestFunctional into their own output). TestRunner is the harness.
set BUILD_TARGET=Skyline.csproj TestPerf\TestPerf.csproj TestTutorial\TestTutorial.csproj TestRunner\TestRunner.csproj

echo ##teamcity[progressMessage 'dotnet --version']
dotnet --version
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 (set ERROR_TEXT=dotnet not on PATH & goto error)

REM # Native Hardklor.exe (C++): the feature-detection perf tests shell out to it.
REM # Same VS-MSBuild-via-vswhere build as build.bat, but BEST EFFORT here -- if the
REM # agent has no C++ tools, warn and keep going (only the feature-detection perf
REM # tests need it) rather than failing the whole perf/tutorial run.
call :build_hardklor
if %EXIT% NEQ 0 (
    echo ##teamcity[message text='Native Hardklor build skipped/failed; feature-detection perf tests will fail. %ERROR_TEXT%' status='WARNING']
    set EXIT=0
    set ERROR_TEXT=
)

for %%P in (%BUILD_TARGET%) do call :restore_one "%%~P"
if %EXIT% NEQ 0 goto error

for %%P in (%BUILD_TARGET%) do call :build_one "%%~P"
if %EXIT% NEQ 0 goto error

REM # ------------------------------------------------------------------------
REM # Stage step. Merge Skyline + TestRunner + TestPerf + TestTutorial into one
REM # bin\staging-net8\<Config> (the single-bin layout TestRunner + the Docker
REM # workers assume). Stage-Net8Tests.ps1 via `-File` binds a COMMA-joined
REM # -Projects list as a single string (and silently stages nothing), so stage
REM # ONE project per call. The first call also bundles the portable .NET runtime
REM # (for the Docker workers under --parallel); the rest pass -NoRuntime.
REM # ------------------------------------------------------------------------
set STAGE_PROJECTS=Skyline TestRunner TestPerf TestTutorial
set STAGE_FIRST=1
for %%P in (%STAGE_PROJECTS%) do call :stage_one "%%P"
if %EXIT% NEQ 0 goto error

set STAGE_DIR=%SCRIPT_DIR%\bin\staging-net8\%CONFIG%
set TC_TEST_RESULTS=%SCRIPT_DIR%\TestResults
if exist "%TC_TEST_RESULTS%" rmdir /s /q "%TC_TEST_RESULTS%"
mkdir "%TC_TEST_RESULTS%"

pushd "%STAGE_DIR%"

REM # Full-suite run mode: host-sequential by default, Docker workers with --parallel.
if %SEQUENTIAL%==1 (
    set RUNNER_MODE=parallelmode=off
) else (
    set RUNNER_MODE=parallelmode=server workercount=%SKYLINE_TEST_WORKERS%
)
set TC_DECORATION=
if defined TEAMCITY_VERSION set TC_DECORATION=teamcitytestdecoration=on

REM # Escape hatch: run a single custom TestRunner (still perftests=on) instead of
REM # the two standard suites. e.g. SKYLINE_TEST_ARGS=test=TestSciexPrmCeOptimization.
if defined SKYLINE_TEST_ARGS goto custom_run

set TESTS_FAILED=0
set FAILED_SUITES=

echo ##teamcity[progressMessage 'TestRunner perf suite ^(TestPerf, perftests=on^)']
call :run_tests %RUNNER_MODE% loop=1 language=%SKYLINE_PERF_LANGUAGE% offscreen=on perftests=on test=TestPerf.dll log=TestPerf.log results="%TC_TEST_RESULTS%" %TC_DECORATION%
if %EXIT% NEQ 0 (set TESTS_FAILED=1 & set "FAILED_SUITES=%FAILED_SUITES% TestPerf")

echo ##teamcity[progressMessage 'TestRunner tutorial suite ^(TestTutorial, perftests=on^)']
call :run_tests %RUNNER_MODE% loop=1 language=%SKYLINE_PERF_LANGUAGE% offscreen=on perftests=on test=TestTutorial.dll log=TestTutorial.log results="%TC_TEST_RESULTS%" %TC_DECORATION%
if %EXIT% NEQ 0 (set TESTS_FAILED=1 & set "FAILED_SUITES=%FAILED_SUITES% TestTutorial")

popd
if %TESTS_FAILED% NEQ 0 (set EXIT=1 & set "ERROR_TEXT=TestRunner reported failures in:%FAILED_SUITES%" & goto error)
goto tests_done

:custom_run
set RUNNER_ARGS=loop=1 language=%SKYLINE_PERF_LANGUAGE% offscreen=on perftests=on results="%TC_TEST_RESULTS%" %TC_DECORATION% %SKYLINE_TEST_ARGS%
echo ##teamcity[progressMessage 'TestRunner ^(custom SKYLINE_TEST_ARGS^)']
call :run_tests %RUNNER_MODE% %RUNNER_ARGS%
popd
if %EXIT% NEQ 0 (set "ERROR_TEXT=TestRunner reported test failures" & goto error)

:tests_done
popd
exit /b 0

:restore_one
echo ##teamcity[progressMessage 'dotnet restore %~1']
dotnet restore "%~1" %MSBUILD_PROPS%
if errorlevel 1 (set EXIT=1 & set "ERROR_TEXT=dotnet restore %~1 failed")
goto :eof

:build_one
echo ##teamcity[progressMessage 'dotnet build %~1 (%CONFIG%)']
dotnet build "%~1" -f net8.0-windows --no-restore -nologo %MSBUILD_PROPS%
if errorlevel 1 (set EXIT=1 & set "ERROR_TEXT=dotnet build %~1 failed")
goto :eof

REM # Stage one project into %STAGE_DIR%. The first call (STAGE_FIRST=1) bundles the
REM # portable .NET runtime; the rest pass -NoRuntime to skip re-staging it.
:stage_one
if "%STAGE_FIRST%"=="1" (
    echo ##teamcity[progressMessage 'Stage-Net8Tests.ps1 %~1 ^(+runtime^)']
    pwsh -NoProfile -File "%SCRIPT_DIR%\Stage-Net8Tests.ps1" -Configuration %CONFIG% -Projects "%~1"
    set STAGE_FIRST=0
) else (
    echo ##teamcity[progressMessage 'Stage-Net8Tests.ps1 %~1']
    pwsh -NoProfile -File "%SCRIPT_DIR%\Stage-Net8Tests.ps1" -Configuration %CONFIG% -Projects "%~1" -NoRuntime
)
if errorlevel 1 (set EXIT=1 & set "ERROR_TEXT=Stage-Net8Tests.ps1 %~1 failed")
goto :eof

REM # Run one TestRunner pass from the staging dir (cwd is already %STAGE_DIR%).
REM # All args are forwarded verbatim via %*; sets EXIT to the runner's result.
:run_tests
echo "%STAGE_DIR%\TestRunner.exe" %*
"%STAGE_DIR%\TestRunner.exe" %*
if errorlevel 1 (set EXIT=1) else (set EXIT=0)
goto :eof

:build_hardklor
echo ##teamcity[progressMessage 'MSBuild Hardklor.vcxproj (%CONFIG%^|x64)']
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (set EXIT=1 & set "ERROR_TEXT=vswhere.exe not found; Visual Studio with C++ tools is required to build native Hardklor.exe" & goto :eof)
set "VSINSTALL="
for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "VSINSTALL=%%i"
if not defined VSINSTALL (set EXIT=1 & set "ERROR_TEXT=No Visual Studio install with C++ tools (VC.Tools.x86.x64) found for native Hardklor build" & goto :eof)
set "HK_MSBUILD=%VSINSTALL%\MSBuild\Current\Bin\amd64\MSBuild.exe"
if not exist "%HK_MSBUILD%" set "HK_MSBUILD=%VSINSTALL%\MSBuild\Current\Bin\MSBuild.exe"
if not exist "%HK_MSBUILD%" (set EXIT=1 & set "ERROR_TEXT=VS MSBuild.exe not found under %VSINSTALL%" & goto :eof)
"%HK_MSBUILD%" "%SCRIPT_DIR%\Executables\Hardklor\Hardklor.vcxproj" -p:Configuration=%CONFIG% -p:Platform=x64 -m -nologo -v:minimal
if errorlevel 1 (set EXIT=1 & set "ERROR_TEXT=MSBuild Hardklor.vcxproj failed")
goto :eof

:error
echo ##teamcity[message text='%ERROR_TEXT%' status='ERROR']
popd
exit /b %EXIT%
