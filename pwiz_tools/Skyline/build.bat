@echo off
setlocal

REM # ------------------------------------------------------------------------
REM # Skyline build + test entry point. TeamCity calls this from tcbuild.bat;
REM # runs locally too. Builds the net8 SDK-style Skyline tree, stages the test
REM # binaries, and runs the whole suite (Test + TestData + TestFunctional)
REM # through the Skyline TestRunner harness -- the harness the functional tests
REM # are written for (offscreen mode, per-test form lifecycle, requeue-on-flake)
REM # -- rather than `dotnet test`, which can't run the functional (UI) tests.
REM #
REM # Usage:
REM #   build.bat [Debug|Release] [--i-agree-to-the-vendor-licenses]
REM #             [--require-vendor-support] [--automated] [--parallel]
REM #
REM # Flags:
REM #   --i-agree-to-the-vendor-licenses
REM #       Acknowledge the vendor SDK EULAs (-p:IAgreeToVendorLicenses=true) so
REM #       the referenced pwiz-sharp vendor projects link the real readers.
REM #       TeamCity passes this for CI; without it the vendor readers build in
REM #       their no-vendor-support mode.
REM #   --require-vendor-support
REM #       Fail if vendor support isn't enabled (guards against silently shipping
REM #       a stripped, no-vendor artifact).
REM #   --automated
REM #       Tag InformationalVersion "(automated build)" (-p:AutomatedBuild=true).
REM #   --parallel
REM #       Run the tests in parallel across Docker workers (TestRunner
REM #       parallelmode=server) instead of the default host-only sequential run.
REM #       Needs Docker Desktop in Windows-container mode + the always_up_runner
REM #       image. Much faster for the full functional suite. Also settable via
REM #       SKYLINE_TEST_PARALLEL=1.
REM #
REM # Environment:
REM #   SKYLINE_TEST_WORKERS   parallel worker count (1 host + N-1 Docker); default 8.
REM #   SKYLINE_TEST_PARALLEL  set to 1 to prefer the parallel Docker run (same as --parallel).
REM #   SKYLINE_TEST_ARGS      extra args appended verbatim to the TestRunner
REM #                          command (e.g. test=Foo,Bar for a smoke run).
REM #
REM # Scope:
REM #   Builds + tests Skyline.csproj and the net8-ported test projects Test,
REM #   TestData, TestFunctional, TestConnected (plus the TestRunner harness).
REM #   TestConnected's network-service tests self-skip when their credentials
REM #   aren't configured. TestPerf and TestTutorial are intentionally EXCLUDED
REM #   from the standard build -- run those separately when needed.
REM #
REM # NOTE: dotCover coverage (--coverage) is temporarily removed while the
REM #   TestRunner path beds in; re-add it as a separate step once proven in CI.
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

REM # Parse args. First non-flag arg is the configuration (Debug|Release).
:parseargs
if "%~1"=="" goto endparse
if /i "%~1"=="--i-agree-to-the-vendor-licenses" (set IAGREE=1) else ^
if /i "%~1"=="--require-vendor-support" (set REQUIRE_VENDOR=1) else ^
if /i "%~1"=="--automated" (set AUTOMATED=1) else ^
if /i "%~1"=="--parallel" (set SEQUENTIAL=0) else ^
if /i "%~1"=="--coverage" (echo ##teamcity[message text='--coverage is temporarily disabled in build.bat; ignoring' status='WARNING']) else ^
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
) else (
    echo ##teamcity[message text='Vendor support: DISABLED ^(no --i-agree-to-the-vendor-licenses^); building core only']
)

REM # Build targets: Skyline.csproj pulls in every ProjectReference (BiblioSpec,
REM # CommonMsData, ProteomeDb, ProteowizardWrapper, ZedGraph, the pwiz-sharp
REM # vendor + BiblioSpec tool projects, ...). The test projects add the suites,
REM # and TestRunner is the harness that stages + runs them.
set BUILD_TARGET=Skyline.csproj Test\Test.csproj TestData\TestData.csproj TestFunctional\TestFunctional.csproj TestConnected\TestConnected.csproj TestRunner\TestRunner.csproj

echo ##teamcity[progressMessage 'dotnet --version']
dotnet --version
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 (set ERROR_TEXT=dotnet not on PATH & goto error)

REM # ------------------------------------------------------------------------
REM # Native Hardklor.exe (C++). `dotnet build` (the .NET SDK MSBuild) cannot
REM # build a C++ vcxproj, so build it here with VS MSBuild (located via
REM # vswhere). The vcxproj is x64, static, self-extracts its bundled zlib/expat
REM # sources, and drops Hardklor.exe under Executables\Hardklor\bin\x64\%CONFIG%.
REM # Skyline.csproj deploys that exe next to Skyline via a Content include so the
REM # Hardklor/Bullseye feature-detection pipeline can shell out to it.
REM # ------------------------------------------------------------------------
call :build_hardklor
if %EXIT% NEQ 0 goto error

for %%P in (%BUILD_TARGET%) do call :restore_one "%%~P"
if %EXIT% NEQ 0 goto error

for %%P in (%BUILD_TARGET%) do call :build_one "%%~P"
if %EXIT% NEQ 0 goto error

REM # ------------------------------------------------------------------------
REM # Test step
REM #
REM # Stage every project's net8 output into one bin\staging-net8\<Config> (the
REM # single-bin layout TestRunner + the Docker workers assume) plus a bundled
REM # portable .NET 8 runtime for the workers, then run the staged TestRunner.
REM # No filter: the whole discovered suite runs. TestFunctional (UI) tests need
REM # this harness -- offscreen mode + the form lifecycle -- which `dotnet test`
REM # can't provide.
REM # ------------------------------------------------------------------------
echo ##teamcity[progressMessage 'Stage-Net8Tests.ps1 (%CONFIG%)']
pwsh -NoProfile -File "%SCRIPT_DIR%\Stage-Net8Tests.ps1" -Configuration %CONFIG%
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 (set ERROR_TEXT=Stage-Net8Tests.ps1 failed & goto error)

set STAGE_DIR=%SCRIPT_DIR%\bin\staging-net8\%CONFIG%

set TC_TEST_RESULTS=%SCRIPT_DIR%\TestResults
if exist "%TC_TEST_RESULTS%" rmdir /s /q "%TC_TEST_RESULTS%"
mkdir "%TC_TEST_RESULTS%"

REM # No skip list: every discovered test runs. A test that can't run in parallel
REM # (shared resource, or needs a host-only vendor SDK / network) marks itself with
REM # [NoParallelTesting("reason")] -- TestRunner routes those to the host worker
REM # instead of a Docker worker, so they still run rather than being skipped.
set RUNNER_ARGS=loop=1 language=en offscreen=on results="%TC_TEST_RESULTS%"
if defined TEAMCITY_VERSION set RUNNER_ARGS=%RUNNER_ARGS% teamcitytestdecoration=on
if defined SKYLINE_TEST_ARGS set RUNNER_ARGS=%RUNNER_ARGS% %SKYLINE_TEST_ARGS%

if %SEQUENTIAL%==1 (
    set RUNNER_MODE=parallelmode=off
    echo ##teamcity[progressMessage 'TestRunner ^(host, sequential^)']
) else (
    set RUNNER_MODE=parallelmode=server workercount=%SKYLINE_TEST_WORKERS%
    echo ##teamcity[progressMessage 'TestRunner ^(parallel, %SKYLINE_TEST_WORKERS% workers^)']
)

pushd "%STAGE_DIR%"
echo "%STAGE_DIR%\TestRunner.exe" %RUNNER_MODE% %RUNNER_ARGS%
"%STAGE_DIR%\TestRunner.exe" %RUNNER_MODE% %RUNNER_ARGS%
set EXIT=%ERRORLEVEL%
popd
if %EXIT% NEQ 0 (set ERROR_TEXT=TestRunner reported test failures & goto error)

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
