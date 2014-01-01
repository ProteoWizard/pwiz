@echo off
setlocal
@echo off

REM # Get to the pwiz root directory
set SCRIPTS_MISC_ROOT=%~dp0
set SCRIPTS_MISC_ROOT=%SCRIPTS_MISC_ROOT:~0,-1%
pushd %SCRIPTS_MISC_ROOT%\..\..

set EXIT=0

REM # call clean
echo ##teamcity[progressMessage 'Cleaning project...']
call clean.bat
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 set ERROR_TEXT=Error performing clean & goto error

REM # the -p1 argument overrides bjam's default behavior of merging stderr into stdout
REM # the --abbreviate-paths argument abbreviates paths like .../ftr1-value/ftr2-value/...

REM # call quickbuild to build and run tests
echo ##teamcity[progressMessage 'Running quickbuild...']
call quickbuild.bat -p1 --abbreviate-paths --teamcity-test-decoration --verbose-test %*
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 set ERROR_TEXT=Error running quickbuild & goto error

REM # uncomment this to test that test failures and error output are handled properly
REM call quickbuild.bat -p1 --teamcity-test-decoration pwiz/utility/misc//FailUnitTest pwiz/utility/misc//FailRunTest

popd

:error
echo "##teamcity[message text='%ERROR_TEXT%' status='ERROR']"
exit /b %EXIT%
