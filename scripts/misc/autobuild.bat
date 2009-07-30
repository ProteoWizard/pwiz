@echo off
call "C:\Program Files (x86)\Microsoft Visual Studio 9.0\VC\vcvarsall.bat"
if %ERRORLEVEL% NEQ 0 set ERROR_TEXT="Error setting up Visual C++ environment variables" & goto error
setlocal
@echo off

REM # Get to the pwiz root directory
set SCRIPTS_MISC_ROOT=%~dp0
set SCRIPTS_MISC_ROOT=%SCRIPTS_MISC_ROOT:~0,-1%
pushd %SCRIPTS_MISC_ROOT%\..\..

REM # call bootstrap
echo ##teamcity[message text='Running bootstrap...']
echo ##teamcity[progressMessage text='Running bootstrap...']
call bootstrap.bat
if %ERRORLEVEL% NEQ 0 set ERROR_TEXT="Error performing bootstrap" & goto error

REM # call quickbuild to build only the bindings first
echo ##teamcity[message text='Building bindings...']
echo ##teamcity[progressMessage text='Building bindings...']
call quickbuild.bat -j4 pwiz\utility\bindings\CLI//pwiz_bindings_cli
if %ERRORLEVEL% NEQ 0 set ERROR_TEXT="Error building bindings" & goto error

REM # call quickbuild again to build the rest and run tests
echo ##teamcity[message text='Running quickbuild...']
echo ##teamcity[progressMessage text='Running quickbuild...']
call quickbuild.bat -j4
if %ERRORLEVEL% NEQ 0 set ERROR_TEXT="Error performing quickbuild" & goto error

popd
goto :EOF

:error
echo "##teamcity[message text='%ERROR_TEXT%' status='ERROR']"
exit /b 1
