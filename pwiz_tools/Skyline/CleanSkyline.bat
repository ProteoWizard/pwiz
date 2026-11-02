@echo off
setlocal
@echo off

REM # Get the location of quickbuild.bat and drop trailing slash
set PWIZ_ROOT=%~dp0
set PWIZ_ROOT=%PWIZ_ROOT:~0,-1%
pushd %PWIZ_ROOT%

REM # Every bin and obj under this tree, found by walking it rather than listed, so a project
REM # added later is covered without anyone remembering to add it here. The same sweep
REM # pwiz_tools\clean-apps.bat runs over the other apps; it leaves Skyline to this script so
REM # that running this script on its own cleans Skyline completely.
call :CleanBinaries .

IF EXIST Microsoft.VC90.MFC rmdir /s /q Microsoft.VC90.MFC
IF EXIST Microsoft.VC100.MFC rmdir /s /q Microsoft.VC100.MFC
IF EXIST ClearCore.dll del /q ClearCore.dll
IF EXIST ClearCore.Storage.dll del /q ClearCore.Storage.dll
IF EXIST EULA.MHDAC del /q EULA.MHDAC
IF EXIST EULA.MSFileReader del /q EULA.MSFileReader
IF EXIST Interop.EDAL.SxS.manifest del /q Interop.EDAL.SxS.manifest
IF EXIST MassLynxRaw.dll del /q MassLynxRaw.dll
IF EXIST Skyline.sln.cache del /q Skyline.sln.cache
IF EXIST Model\Prosit\Config\PrositConfig.xml del /q Model\Prosit\Config\PrositConfig.xml
IF EXIST TestResults rmdir /s /q TestResults
IF EXIST "SkylineTester Results" rmdir /s /q "SkylineTester Results"
IF EXIST ..\Shared\ProteowizardWrapper\Interop.EDAL.SxS.manifest del /q ..\Shared\ProteowizardWrapper\Interop.EDAL.SxS.manifest
IF EXIST ..\Shared\ProteowizardWrapper\Microsoft.VC90.MFC rmdir /s /q ..\Shared\ProteowizardWrapper\Microsoft.VC90.MFC
IF EXIST ProtocolBuffers\tmp rmdir /s /q ProtocolBuffers\tmp
IF EXIST ProtocolBuffers\GeneratedCode\*.cs del /q ProtocolBuffers\GeneratedCode\*.cs
IF EXIST Test\ProtocolBuffers\tmp rmdir /s /q Test\ProtocolBuffers\tmp
IF EXIST Test\ProtocolBuffers\GeneratedCode\*.cs del /q Test\ProtocolBuffers\GeneratedCode\*.cs
IF EXIST TestSettings_x64.testsettings del /q TestSettings_x64.testsettings
IF EXIST TestSettings_x86.testsettings del /q TestSettings_x86.testsettings
IF EXIST Executables\Installer\FileList64.txt del /q Executables\Installer\FileList64.txt
IF EXIST Executables\Hardklor\x64 rmdir /s /q Executables\Hardklor\x64
IF EXIST Executables\SkylineBatch\SkylineBatch\Properties\AssemblyInfo.cs del /q Executables\SkylineBatch\SkylineBatch\Properties\AssemblyInfo.cs
IF EXIST Properties\AssemblyInfo.cs del /q Properties\AssemblyInfo.cs
IF EXIST SkylineCmd\Properties\AssemblyInfo.cs del /q SkylineCmd\Properties\AssemblyInfo.cs
IF EXIST SkylineNightly\Properties\AssemblyInfo.cs del /q SkylineNightly\Properties\AssemblyInfo.cs
IF EXIST SkylineNightlyShim\Properties\AssemblyInfo.cs del /q SkylineNightlyShim\Properties\AssemblyInfo.cs
IF EXIST SkylineTester\Properties\AssemblyInfo.cs del /q SkylineTester\Properties\AssemblyInfo.cs
IF EXIST TestRunner\Properties\AssemblyInfo.cs del /q TestRunner\Properties\AssemblyInfo.cs
IF EXIST Translation\Scratch rmdir /s /q Translation\Scratch
popd
REM # Exit 0 explicitly: the sweep leaves errorlevel 1 behind (git ls-files --error-unmatch sets it
REM # for every untracked directory), the IF EXIST lines above do not reset it, and the build
REM # configuration's Clean step fails on a nonzero exit from clean.bat, which ends up here.
exit /b 0


REM # Copied from pwiz_tools\clean-apps.bat, so this script can stand alone.
REM subroutine for cleaning out obj and bin dirs, but avoiding any source controlled files
:CleanBinaries
rem %~1 - The directory to clean
if not exist "%~1" exit /b

rem Iterate through all files and directories in the current directory
for /d /r %~1 %%d in (obj, bin) do (
    if exist "%%d" (
        pushd "%%d" >nul
        rem Check if the directory contains files tracked by Git (error 1 if not)
        git ls-files --error-unmatch . >nul 2>&1
        if errorlevel 1 (
            REM contains no source controlled files, delete directory
            popd >nul
            rmdir /s /q "%%d"
        ) else (
            REM remove any non-source-controlled files
            git clean -f -q
            popd >nul
        )
    )
)
exit /b
REM end of subroutine