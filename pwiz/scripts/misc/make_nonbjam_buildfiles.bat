@echo off
setlocal
@echo off

REM # Get to the pwiz root directory
set SCRIPTS_MISC_ROOT=%~dp0
set SCRIPTS_MISC_ROOT=%SCRIPTS_MISC_ROOT:~0,-1%
pushd %SCRIPTS_MISC_ROOT%\..\..
set SCRIPTS_PWIZ_ROOT=%CD%
popd
pushd %SCRIPTS_MISC_ROOT%\..\autotools

set EXIT=0

REM # call make_nonbjam_build
echo ##teamcity[progressMessage 'Creating native MSVC build tools for libpwiz...']
call make_nonbjam_build.bat  %SCRIPTS_PWIZ_ROOT% %*
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 set ERROR_TEXT=Error running make_nonbjam_build & goto error
popd
exit /b %EXIT%

:error
echo "##teamcity[message text='%ERROR_TEXT%' status='ERROR']"
exit /b %EXIT%
