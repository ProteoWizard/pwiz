@echo off
setlocal
@echo off

REM # Get to the pwiz root directory
set SCRIPTS_MISC_ROOT=%~dp0
set SCRIPTS_MISC_ROOT=%SCRIPTS_MISC_ROOT:~0,-1%
pushd %SCRIPTS_MISC_ROOT%\..\..

REM # argument of 32 or 64 sets target platform (default 32)
set TARGETPLATFORM=32
set ARGS=
set TARGETS=

:setArgs
if "%1"=="" goto doneSetArgs
set A=%1
if "%A%"=="32" (
    set TARGETPLATFORM=32
) else if "%A%"=="64" (
    set TARGETPLATFORM=64
) else if "%A:~0,1%"=="-" (
    set ARGS=%ARGS% %A%
) else (
    set TARGETS=%TARGETS% %A%
)
shift
goto setArgs
:doneSetArgs

set EXIT=0
set PROGRAM_FILES_DRIVE=%ProgramFiles:~0,2%

REM # register correct MSFileReader
echo.
echo Registering MSFileReader
echo.
if "%TARGETPLATFORM%"=="64" (
    IF EXIST "%PROGRAM_FILES_DRIVE%\Program Files (x86)\Thermo\MSFileReader\XRawfile2.dll" regsvr32 /s /u "%PROGRAM_FILES_DRIVE%\Program Files (x86)\Thermo\MSFileReader\XRawfile2.dll"
    REM # regsvr32 must be called through cmd /c for it to impact %ERRORLEVEL% with the /s option
    IF EXIST "%PROGRAM_FILES_DRIVE%\Program Files\Thermo\MSFileReader\XRawfile2_x64.dll" cmd /c "regsvr32 /s ""%PROGRAM_FILES_DRIVE%\Program Files\Thermo\MSFileReader\XRawfile2_x64.dll"""
    set EXIT=%ERRORLEVEL%
    if %EXIT% NEQ 0 ERROR_TEXT=Error registering MSFileReader & goto error
    echo *** Registered 64-bit MSFileReader

) else (
    IF EXIST "%PROGRAM_FILES_DRIVE%\Program Files\Thermo\MSFileReader\XRawfile2_x64.dll" regsvr32 /s /u "%PROGRAM_FILES_DRIVE%\Program Files\Thermo\MSFileReader\XRawfile2_x64.dll"
    REM # regsvr32 must be called through cmd /c for it to impact %ERRORLEVEL% with the /s option
    IF EXIST "%PROGRAM_FILES_DRIVE%\Program Files (x86)\Thermo\MSFileReader\XRawfile2.dll" cmd /c "regsvr32 /s ""%PROGRAM_FILES_DRIVE%\Program Files (x86)\Thermo\MSFileReader\XRawfile2.dll"""
    set EXIT=%ERRORLEVEL%
    if %EXIT% NEQ 0 ERROR_TEXT=Error registering MSFileReader & goto error
    echo *** Registered 32-bit MSFileReader
)

REM # call clean
echo ##teamcity[progressMessage 'Cleaning project...']
call clean.bat
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 set ERROR_TEXT=Error performing clean & goto error

REM # the -p1 argument overrides bjam's default behavior of merging stderr into stdout
REM # the --abbreviate-paths argument abbreviates paths like .../ftr1-value/ftr2-value/...

REM # call quickbuild to build and run tests
echo ##teamcity[progressMessage 'Running build-apps...']
call quickbuild.bat %TARGETS% %ARGS% -p1 --abbreviate-paths --teamcity-test-decoration
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 set ERROR_TEXT=Error running quickbuild & goto error

popd

:error
if %EXIT% NEQ 0 echo ##teamcity[message text='%ERROR_TEXT%' status='ERROR']
exit /b %EXIT%
