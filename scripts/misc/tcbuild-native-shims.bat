@echo off
setlocal enabledelayedexpansion

REM # ------------------------------------------------------------------------
REM # tcbuild-native-shims.bat - build the three native Windows binaries that
REM # cannot be cross-compiled, and stage them for publication as artifacts.
REM #
REM # WHY THIS EXISTS
REM # Everything managed in this repo now builds for Windows from a Linux host
REM # (the vendor staging gates on $(PwizTargetIsWindows), not $(OS)). These
REM # three are the exception, because each is COMPILED during the build rather
REM # than vendored as a prebuilt:
REM #
REM #   MascotShim.dll   - links msparser.lib; msparser returns std::string and
REM #                      std::vector BY VALUE across the DLL boundary.
REM #   MobilionShim.dll - MBI_SDK.dll exports `class MBI_DLLCPP MBIFile` whose
REM #                      methods return std::shared_ptr<std::vector<double>>.
REM #   Hardklor.exe     - no vendor SDK; the only genuinely cross-compilable
REM #                      one, built here so all three ship together.
REM #
REM # The first two cross the MSVC C++ ABI, which rules out mingw-w64: it links
REM # cleanly (COFF import libs are readable by GNU ld) and then corrupts memory,
REM # because libstdc++'s std::string layout has nothing to do with MSVC's. That
REM # is not hypothetical - MascotShim/CMakeLists.txt documents the same class of
REM # bug from a CRT mismatch WITHIN MSVC. So the answer is a cached prebuilt,
REM # not a cross-compile; both sources are effectively frozen anyway
REM # (MascotShim.cpp: 1 commit ever, MobilionShim.cpp: 2).
REM #
REM # Consumers set -p:PwizPrebuiltNativeShimDir=<dir> to use the published
REM # artifact instead of compiling. The vendor companions (msparser.dll,
REM # MBI_SDK.dll) are staged alongside so a consumer needs no archive
REM # extraction - which also means MascotArchive/MascotParserPath can stay
REM # host-based, as they must, since MascotShim is host-compiled.
REM #
REM # Artifacts are declared by artifactRules in .teamcity/settings.kts rather
REM # than by a ##teamcity[publishArtifacts] service message here, so the
REM # artifact contract lives with the build config. This script only stages.
REM #
REM # Usage: tcbuild-native-shims.bat [Release|Debug]
REM # ------------------------------------------------------------------------

set "SCRIPT_DIR=%~dp0"
set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"
pushd "%SCRIPT_DIR%\..\.."
set "ROOT=%CD%"

set "CONFIG=Release"
if not "%~1"=="" set "CONFIG=%~1"

set EXIT=0
set "ERROR_TEXT="
set "STAGE=%ROOT%\artifacts\native-shims"

REM # Start from empty so a stale file from a previous run cannot masquerade as
REM # this run's output - the whole point of the artifact is that consumers
REM # trust it without rebuilding.
if exist "%STAGE%" rmdir /s /q "%STAGE%"
mkdir "%STAGE%"

call :locate_vs_msbuild
if %EXIT% NEQ 0 goto error

echo ##teamcity[progressMessage 'MascotShim.dll ^(cmake + MSVC^)']
dotnet build "%ROOT%\pwiz_tools\BiblioSpec\src\BiblioSpec\BiblioSpec.csproj" -t:BuildMascotShim -p:Configuration=%CONFIG% -p:IAgreeToVendorLicenses=true -p:MascotSupport=true -nologo -v:minimal
if errorlevel 1 (set EXIT=1 & set "ERROR_TEXT=BuildMascotShim failed" & goto error)

echo ##teamcity[progressMessage 'MobilionShim.dll ^(MobilionShim.vcxproj^)']
dotnet build "%ROOT%\pwiz\data\vendor_readers\Mobilion\Mobilion.csproj" -t:BuildMobilionShim -p:Configuration=%CONFIG% -p:IAgreeToVendorLicenses=true -nologo -v:minimal
if errorlevel 1 (set EXIT=1 & set "ERROR_TEXT=BuildMobilionShim failed" & goto error)

REM # -nodeReuse:false is load-bearing, not tidiness. This is VS MSBuild while
REM # the two steps above are the .NET SDK's; a parked node from one version
REM # mis-evaluates a project for the other (the SDK's "**/*.resx" glob reaches
REM # GenerateResource unexpanded -> MSB3552). See the longer note at
REM # pwiz_tools\Skyline\build.bat :build_hardklor, which this mirrors. Kept as a
REM # copy rather than factored out because build.bat has uncommitted work in it.
echo ##teamcity[progressMessage 'Hardklor.exe ^(Hardklor.vcxproj^)']
"%VS_MSBUILD%" "%ROOT%\pwiz_tools\Skyline\Executables\Hardklor\Hardklor.vcxproj" -p:Configuration=%CONFIG% -p:Platform=x64 -m -nologo -v:minimal -nodeReuse:false
if errorlevel 1 (set EXIT=1 & set "ERROR_TEXT=MSBuild Hardklor.vcxproj failed" & goto error)

echo ##teamcity[progressMessage 'staging artifacts/native-shims']
call :stage "%ROOT%\pwiz_tools\BiblioSpec\native\MascotShim\build\%CONFIG%\MascotShim.dll"
if %EXIT% NEQ 0 goto error
call :stage "%ROOT%\libraries\msparser_3_1_0_x86_win64\vs2015\lib\msparser.dll"
if %EXIT% NEQ 0 goto error
REM # msparser resolves these by path at runtime (MascotResultsReader hands the directory to
REM # mascot_dat_set_quant_config_dir), and they come out of the same encrypted archive as the
REM # DLL - so a consumer that cannot extract the Windows archive cannot get them either. They
REM # ship in the bundle for exactly the same reason the vendor DLLs do.
mkdir "%STAGE%\msparser-config"
call :stage_to "%ROOT%\libraries\msparser_3_1_0_x86_win64\config\unimod_2.xsd" "%STAGE%\msparser-config"
if %EXIT% NEQ 0 goto error
call :stage_to "%ROOT%\libraries\msparser_3_1_0_x86_win64\config\quantitation_1.xsd" "%STAGE%\msparser-config"
if %EXIT% NEQ 0 goto error
call :stage_to "%ROOT%\libraries\msparser_3_1_0_x86_win64\config\quantitation_2.xsd" "%STAGE%\msparser-config"
if %EXIT% NEQ 0 goto error
call :stage "%ROOT%\pwiz\data\vendor_readers\Mobilion\MobilionShim\bin\%CONFIG%\MobilionShim.dll"
if %EXIT% NEQ 0 goto error
call :stage "%ROOT%\vendor-assemblies\Mobilion\MBI_SDK.dll"
if %EXIT% NEQ 0 goto error
call :stage "%ROOT%\pwiz_tools\Skyline\Executables\Hardklor\bin\x64\%CONFIG%\Hardklor.exe"
if %EXIT% NEQ 0 goto error

echo Staged native shims in %STAGE%:
dir /b "%STAGE%"
popd
exit /b 0

REM # ------------------------------------------------------------------------
:locate_vs_msbuild
REM # NOTE on quoting: the `set "VAR=value"` form is required throughout,
REM # because these values expand a path under `C:\Program Files (x86)\...` and
REM # an unquoted assignment lets batch close an enclosing if-block at the `)`
REM # inside `(x86)`, mid-token.
set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
    set EXIT=1
    set "ERROR_TEXT=vswhere.exe not found; Visual Studio with C++ tools is required to build the native shims"
    goto :eof
)
set "VSINSTALL="
for /f "usebackq tokens=*" %%i in (`"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath`) do set "VSINSTALL=%%i"
if not defined VSINSTALL (
    set EXIT=1
    set "ERROR_TEXT=No Visual Studio install with C++ tools (VC.Tools.x86.x64) found"
    goto :eof
)
set "VS_MSBUILD=%VSINSTALL%\MSBuild\Current\Bin\amd64\MSBuild.exe"
if not exist "%VS_MSBUILD%" set "VS_MSBUILD=%VSINSTALL%\MSBuild\Current\Bin\MSBuild.exe"
if not exist "%VS_MSBUILD%" (
    set EXIT=1
    set "ERROR_TEXT=VS MSBuild.exe not found under %VSINSTALL%"
)
goto :eof

REM # ------------------------------------------------------------------------
:stage
call :stage_to "%~1" "%STAGE%"
goto :eof

:stage_to
REM # %~1 = expected file, %~2 = destination dir. Missing means a build step reported
REM # success without producing its output; fail here rather than publish a bundle that
REM # is quietly short a file, since consumers do not rebuild.
if not exist "%~1" (
    set EXIT=1
    set "ERROR_TEXT=expected build output missing: %~1"
    goto :eof
)
copy /y "%~1" "%~2\" >nul
if errorlevel 1 (
    set EXIT=1
    set "ERROR_TEXT=failed to stage %~1"
)
goto :eof

:error
echo ##teamcity[message text='%ERROR_TEXT%' status='ERROR']
popd
exit /b %EXIT%
