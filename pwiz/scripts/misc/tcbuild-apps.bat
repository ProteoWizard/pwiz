@echo off
setlocal
@echo off

REM # Get to the pwiz root directory
set SCRIPTS_MISC_ROOT=%~dp0
set SCRIPTS_MISC_ROOT=%SCRIPTS_MISC_ROOT:~0,-1%
pushd %SCRIPTS_MISC_ROOT%\..\..

REM # argument of 32 or 64 sets target platform (default 32)
set TARGETPLATFORM=32

set ALL_ARGS= %*

if "%ALL_ARGS: 32=%" neq "%ALL_ARGS%" (
    set TARGETPLATFORM=32
    set ALL_ARGS=%ALL_ARGS: 32=%
)
if "%ALL_ARGS: 64=%" neq "%ALL_ARGS%" (
    set TARGETPLATFORM=64
    set ALL_ARGS=%ALL_ARGS: 64=%
)

set EXIT=0
set PROGRAM_FILES_DRIVE=%ProgramFiles:~0,2%

REM # register correct MSFileReader
call pwiz_tools\reg-controls.bat %TARGETPLATFORM%
if %ERRORLEVEL% NEQ 0 exit /b 1

REM # call clean
echo ##teamcity[progressMessage 'Cleaning project...']
call clean.bat
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 set ERROR_TEXT=Error performing clean & goto error

REM # the -p1 argument overrides bjam's default behavior of merging stderr into stdout
REM # the --abbreviate-paths argument abbreviates paths like .../ftr1-value/ftr2-value/...

REM # call quickbuild to build and run tests
echo ##teamcity[progressMessage 'Running build-apps...']
echo quickbuild.bat %ALL_ARGS% -p1 --abbreviate-paths --teamcity-test-decoration --without-compassxtract
call quickbuild.bat %ALL_ARGS% -p1 --abbreviate-paths --teamcity-test-decoration --without-compassxtract
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 set ERROR_TEXT=Error running quickbuild & goto error

popd

:error
if %EXIT% NEQ 0 echo ##teamcity[message text='%ERROR_TEXT%' status='ERROR']
exit /b %EXIT%
