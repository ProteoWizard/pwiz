@echo off
setlocal
@echo off

REM # Get the location of this file and drop trailing slash
set PWIZ_ROOT=%~dp0
set PWIZ_ROOT=%PWIZ_ROOT:~0,-1%
pushd %PWIZ_ROOT%

echo   Cleaning .NET applications...
for /d /r Shared\BiblioSpec %%d in (obj, bin) do @if exist "%%d" rmdir /s/q "%%d"
for /d /r Shared\Common %%d in (obj, bin) do @if exist "%%d" rmdir /s/q "%%d"
for /d /r Shared\CommonTest %%d in (obj, bin) do @if exist "%%d" rmdir /s/q "%%d"
for /d /r Shared\Crawdad %%d in (obj, bin) do @if exist "%%d" rmdir /s/q "%%d"
for /d /r Shared\MSGraph %%d in (obj, bin) do @if exist "%%d" rmdir /s/q "%%d"
for /d /r Shared\ProteomeDb %%d in (obj, bin) do @if exist "%%d" rmdir /s/q "%%d"
for /d /r Shared\ProteowizardWrapper %%d in (obj, bin) do @if exist "%%d" rmdir /s/q "%%d"
for /d /r Shared\zedgraph %%d in (obj, bin) do @if exist "%%d" rmdir /s/q "%%d"
for /d /r Skyline %%d in (obj) do @if exist "%%d" rmdir /s/q "%%d"
REM Skyline has at least one checked in devtools "bin" folder, don't stomp if contents are source controlled
for /d /r Skyline %%d in (bin) do (
    if exist "%%d" (
        pushd "%%d" >nul
        rem Check if the directory contains files tracked by Git (error 1 if not)
        git ls-files --error-unmatch . >nul 2>&1
        if errorlevel 1 (
            popd >nul
            rmdir /s /q "%%d"
        ) else (
            popd >nul
        )
    )
)
for /d /r SeeMS %%d in (obj, bin) do @if exist "%%d" rmdir /s/q "%%d"
for /d /r MSConvertGUI %%d in (obj, bin) do @if exist "%%d" rmdir /s/q "%%d"

IF EXIST Shared\CommonTest rmdir /s/q Shared\CommonTest

IF EXIST SeeMS\CleanSeeMS.bat call SeeMS\CleanSeeMS.bat
IF EXIST Skyline\CleanSkyline.bat call Skyline\CleanSkyline.bat
IF EXIST BiblioSpec\CleanBiblioSpec.bat call BiblioSpec\CleanBiblioSpec.bat

popd
