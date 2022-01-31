@if not defined DEBUG_HELPER @ECHO OFF
setlocal
set "InstallerPath=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer"
if not exist "%InstallerPath%" set "InstallerPath=%ProgramFiles%\Microsoft Visual Studio\Installer"
if not exist "%InstallerPath%" goto :no-vswhere
:: Manipulate %Path% for easier " handeling
set Path=%Path%;%InstallerPath%
where vswhere 2> nul > nul
if errorlevel 1 goto :no-vswhere
set VSWHERE_REQ=-requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64
set VSWHERE_PRP=-property installationPath

REM Visual Studio Unknown Version, Beyond 2019
set VSWHERE_LMT=-version "[17.0,18.0)"
set VSWHERE_PRERELEASE=-prerelease
SET VSWHERE_ARGS=-latest -products * %VSWHERE_REQ% %VSWHERE_PRP% %VSWHERE_LMT% %VSWHERE_PRERELEASE%
IF "%2"=="143" (
    for /f "usebackq tokens=*" %%i in (`vswhere %VSWHERE_ARGS%`) do (
        endlocal
            echo Found with vswhere %%i
        set "VSUNKCOMNTOOLS=%%i\Common7\Tools\"
        IF EXIST "%%i\Common7\Tools\VsDevCmd.bat" (CALL "%%i\Common7\Tools\VsDevCmd.bat" -arch=%1 && exit /b 0)
    )
    echo VS2022 not found.
    exit /b 1
)

REM Visual Studio 2019 (16.X, toolset 14.2)
set VSWHERE_LMT=-version "[16.0,17.0)"
SET VSWHERE_ARGS=-latest -products * %VSWHERE_REQ% %VSWHERE_PRP% %VSWHERE_LMT% %VSWHERE_PRERELEASE%
IF "%2"=="142" (
    for /f "usebackq tokens=*" %%i in (`vswhere %VSWHERE_ARGS%`) do (
        endlocal
            echo Found with vswhere %%i
        set "VS160COMNTOOLS=%%i\Common7\Tools\"
        IF EXIST "%%i\Common7\Tools\VsDevCmd.bat" (CALL "%%i\Common7\Tools\VsDevCmd.bat" -arch=%1 && exit /b 0)
    )
    echo VS2019 not found.
    exit /b 1
)

REM Visual Studio 2017 (15.X, toolset 14.1)
set VSWHERE_LMT=-version "[15.0,16.0)"
SET VSWHERE_ARGS=-latest -products * %VSWHERE_REQ% %VSWHERE_PRP% %VSWHERE_LMT%
IF "%2"=="141" (
    for /f "usebackq tokens=*" %%i in (`vswhere %VSWHERE_ARGS%`) do (
        endlocal
            echo Found with vswhere %%i
        set "VS150COMNTOOLS=%%i\Common7\Tools\"
        IF EXIST "%%i\Common7\Tools\VsDevCmd.bat" (CALL "%%i\Common7\Tools\VsDevCmd.bat" -arch=%1 && exit /b 0)
    )
    echo VS2017 not found.
    exit /b 1
)

IF "%2"=="" echo Must specify platform toolset version without period (e.g. 141, 142, 143)

echo Unsupported requested platform toolset version %2; current supported versions: 141, 142, 143
exit /b 1

:no-vswhere
endlocal
echo could not find "vswhere"
exit /B 1
