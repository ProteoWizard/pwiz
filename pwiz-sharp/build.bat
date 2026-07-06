@echo off
setlocal enabledelayedexpansion

REM # ------------------------------------------------------------------------
REM # pwiz-sharp build entry point. TeamCity calls this from the
REM # ProteoWizard_CoreWindowsNet config; runs locally too.
REM #
REM # Usage:
REM #   build.bat [Debug|Release] [--i-agree-to-the-vendor-licenses]
REM #             [--require-vendor-support] [--without-mascot]
REM #             [--automated] [--coverage]
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
REM #   --without-mascot
REM #       Disable Mascot (.dat) support in BiblioSpec. msparser is bundled
REM #       under libraries\msparser_3_1_0_x86_win64\, so Mascot support is ON
REM #       by default — pass this flag to skip building the MascotShim native
REM #       wrapper (e.g. on a contributor machine without CMake, or in a
REM #       no-vendor release pipeline).
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
set WITHOUT_MASCOT=0
set ERROR_TEXT=

REM # Parse args. First non-flag arg is the configuration (Debug|Release).
:parseargs
if "%~1"=="" goto endparse
if /i "%~1"=="--i-agree-to-the-vendor-licenses" (set IAGREE=1) else ^
if /i "%~1"=="--require-vendor-support" (set REQUIRE_VENDOR=1) else ^
if /i "%~1"=="--without-mascot" (set WITHOUT_MASCOT=1) else ^
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
if %WITHOUT_MASCOT%==1 set MSBUILD_PROPS=%MSBUILD_PROPS% -p:MascotSupport=false

if %IAGREE%==1 (
    echo ##teamcity[message text='Vendor support: ENABLED']
    set BUILD_TARGET=Pwiz.sln
) else (
    echo ##teamcity[message text='Vendor support: DISABLED ^(no --i-agree-to-the-vendor-licenses^); building core only']
    REM # No-vendor build: build msconvert + tests that don't depend on vendors.
    REM # Vendor projects in Pwiz.sln are skipped via explicit project list.
    set BUILD_TARGET=Tools\Commandline\MsConvert\src\MsConvert.csproj
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

REM # Installer build. Only runs when vendor support is enabled (the installer
REM # ships vendor-enabled binaries) AND Inno Setup's ISCC.exe is locatable.
REM # If ISCC is missing, log a TC warning and continue — Installer.Tests will
REM # then skip Inconclusive ("Setup.exe not found") instead of failing the
REM # whole build. tcbuild.bat tries to bootstrap Inno Setup via winget so this
REM # branch should normally not be taken on CI.
if %IAGREE%==1 (
    set HAVE_ISCC=0
    where ISCC.exe >NUL 2>&1
    if not ERRORLEVEL 1 set HAVE_ISCC=1
    if exist "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" set HAVE_ISCC=1
    if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe"          set HAVE_ISCC=1
    if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"     set HAVE_ISCC=1
    if !HAVE_ISCC!==1 (
        echo ##teamcity[progressMessage 'installer/build.ps1 -SkipBuild ^(uses Release artifacts^)']
        pwsh -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%\installer\build.ps1" -SkipBuild
        if !ERRORLEVEL! NEQ 0 (
            echo ##teamcity[message text='installer build failed; Installer.Tests will skip' status='WARNING']
        )
    ) else (
        echo ##teamcity[message text='Inno Setup ^(ISCC.exe^) not found; skipping installer build and Installer.Tests will skip' status='WARNING']
    )
)

REM # Test discovery: with vendor support, every test csproj under test\.
REM # Without vendor support, only the projects that don't pull vendor refs
REM # (Bruker.Tests / Thermo.Tests / Waters.Tests / Agilent.Tests /
REM # Sciex.Tests / Shimadzu.Tests / UNIFI.Tests / UIMF.Tests / Mobilion.Tests
REM # all reference vendor projects).
REM #
REM # Installer.Tests is vendor-only because its smoke test runs the installed
REM # msconvert-sharp against a Thermo .raw to exercise VendorSdkLoader +
REM # Reader_Thermo end-to-end. With no vendor support the conversion would
REM # fail (and the installer itself wouldn't have vendor-enabled binaries).
set TEST_TARGET=pwiz\test\Util.Tests\Util.Tests.csproj pwiz\test\Common.Tests\Common.Tests.csproj pwiz\test\MsData.Tests\MsData.Tests.csproj pwiz\test\MsData.NativeAot.Tests\MsData.NativeAot.Tests.csproj pwiz\test\IdentData.Tests\IdentData.Tests.csproj pwiz\test\Analysis.Tests\Analysis.Tests.csproj Tools\Commandline\MsConvert\test\MsConvert.Tests.csproj
if %IAGREE%==1 set TEST_TARGET=%TEST_TARGET% pwiz\test\Agilent.Tests\Agilent.Tests.csproj pwiz\test\Bruker.Tests\Bruker.Tests.csproj pwiz\test\Mobilion.Tests\Mobilion.Tests.csproj pwiz\test\Sciex.Tests\Sciex.Tests.csproj pwiz\test\Shimadzu.Tests\Shimadzu.Tests.csproj pwiz\test\Thermo.Tests\Thermo.Tests.csproj pwiz\test\UIMF.Tests\UIMF.Tests.csproj pwiz\test\UNIFI.Tests\UNIFI.Tests.csproj pwiz\test\Waters.Tests\Waters.Tests.csproj pwiz\test\Installer.Tests\Installer.Tests.csproj

REM # Test step: scripts\Run-Tests-Parallel.ps1 spawns one parallel
REM # `dotnet test <project>` job per csproj, each redirected to its own log
REM # file. After every job finishes, the script concatenates per-project logs
REM # to its own stdout in declared order. Wall-clock matches the previous
REM # solution-level parallel run; TC service messages from each project's
REM # teamcity logger appear contiguously instead of byte-interleaving with
REM # sibling projects.
REM #
REM # Background: MSBuild's solution-level VSTest target runs per-project tests
REM # in parallel by default. Each project's TC.VSTest.TestAdapter writes
REM # ##teamcity[testFinished ...] messages to the SAME process stdout.
REM # Concurrent writes interleaved at the byte level — build 3976906 line 665
REM # showed mid-stream merge of MsData.Tests with UNIFI.Tests producing
REM # "test\MsData.Te##teamcity[testFinished name='UNIFI.Tests..." — TC parser
REM # dropped the malformed messages — variable test counts across builds
REM # 3975154 / 3976893 / 3976906 (249 / 266 / 260). Per-project file logging
REM # eliminates the interleaving; serial concatenation preserves message
REM # ordering within each project.
REM #
REM # The Sciex SDK silencing (Sciex.Tests/SilenceSciexSdkLogging) remains
REM # necessary: it suppresses log4net [INFO]/[DEBUG] lines that would
REM # otherwise interleave with teamcity messages WITHIN a single project.
REM #
REM # Coverage: when --coverage / TEAMCITY_VERSION is set, each per-project
REM # `dotnet test` runs under `dotnet dotcover dotnet -- test ...`. The script
REM # writes per-project snapshots and merges them into coverage.dcvr via
REM # `dotnet dotcover merge` — same final artifact shape as the previous
REM # single-snapshot flow.
set TC_TEST_RESULTS=%SCRIPT_DIR%\TestResults
if exist "%TC_TEST_RESULTS%" rmdir /s /q "%TC_TEST_RESULTS%"

set PS_FLAGS=-Configuration %CONFIG%
if %IAGREE%==1 set PS_FLAGS=%PS_FLAGS% -IAgreeToVendorLicenses
if %AUTOMATED%==1 set PS_FLAGS=%PS_FLAGS% -AutomatedBuild
if %WITHOUT_MASCOT%==1 set PS_FLAGS=%PS_FLAGS% -WithoutMascot

echo ##teamcity[progressMessage 'dotnet test (%CONFIG%)']

if %COVERAGE%==1 (
    echo ##teamcity[progressMessage 'dotnet tool restore - local manifest .config\dotnet-tools.json']
    dotnet tool restore
    if !ERRORLEVEL! NEQ 0 (
        set EXIT=2
        set ERROR_TEXT=`dotnet tool restore` failed; see .config\dotnet-tools.json. Coverage cannot run.
        goto error
    )

    set COVER_DIR=%TC_TEST_RESULTS%
    if not exist "!COVER_DIR!" mkdir "!COVER_DIR!"
    set COVER_REPORT_DIR=!COVER_DIR!\coverage-report
    set COVER_FILTERS=+:module=Pwiz.*;-:module=*.Tests;-:module=msconvert-sharp

    echo ##teamcity[progressMessage 'pwsh Run-Tests-Parallel.ps1 ^(with coverage^)']
    pwsh -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%\scripts\Run-Tests-Parallel.ps1" -TestProjects "%TEST_TARGET%" %PS_FLAGS% -TestResultsDir "!COVER_DIR!" -CoverageSnapshotDir "!COVER_DIR!" -CoverageFilters "!COVER_FILTERS!"
    set EXIT=!ERRORLEVEL!

    set COVER_SNAPSHOT=!COVER_DIR!\coverage.dcvr
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
    echo ##teamcity[progressMessage 'pwsh Run-Tests-Parallel.ps1']
    pwsh -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%\scripts\Run-Tests-Parallel.ps1" -TestProjects "%TEST_TARGET%" %PS_FLAGS% -TestResultsDir "%TC_TEST_RESULTS%"
    set EXIT=!ERRORLEVEL!
)

if %EXIT% NEQ 0 (set ERROR_TEXT=dotnet test failed & goto error)

popd
exit /b 0

:error
echo ##teamcity[message text='%ERROR_TEXT%' status='ERROR']
popd
exit /b %EXIT%
