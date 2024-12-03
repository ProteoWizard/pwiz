@echo off
setlocal
@echo off

REM # Get the location of this file and drop trailing slash
set PWIZ_ROOT=%~dp0
set PWIZ_ROOT=%PWIZ_ROOT:~0,-1%
pushd %PWIZ_ROOT%

echo   Cleaning .NET applications...
call :CleanBinaries Shared
call :CleanBinaries Skyline
call :CleanBinaries SeeMS
call :CleanBinaries MSConvertGUI
call :CleanBinaries Bumbershoot

IF EXIST Shared\CommonTest rmdir /s/q Shared\CommonTest

IF EXIST SeeMS\CleanSeeMS.bat call SeeMS\CleanSeeMS.bat
IF EXIST Skyline\CleanSkyline.bat call Skyline\CleanSkyline.bat
IF EXIST BiblioSpec\CleanBiblioSpec.bat call BiblioSpec\CleanBiblioSpec.bat
IF EXIST Bumbershoot\CleanBumbershoot.bat call Bumbershoot\CleanBumbershoot.bat

popd
rem Exit the script
exit /b


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
