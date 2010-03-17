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

REM # call quickbuild to build and run tests with variant=release
echo ##teamcity[progressMessage 'Running quickbuild for release variant...']
call quickbuild.bat -j4 -p1 --teamcity-test-decoration release
set RELEASE_ERRORLEVEL=%ERRORLEVEL%
if %EXIT% EQU 0 set EXIT=%RELEASE_ERRORLEVEL%
if %RELEASE_ERRORLEVEL% NEQ 0 echo ##teamcity[message text='Error running quickbuild for release variant! See full build log for more details.' status='ERROR']

REM # call quickbuild to build and run tests with variant=debug
echo ##teamcity[progressMessage 'Running quickbuild for debug variant...']
call quickbuild.bat -j4 -p1 --teamcity-test-decoration debug
set DEBUG_ERRORLEVEL=%ERRORLEVEL%
if %EXIT% EQU 0 set EXIT=%DEBUG_ERRORLEVEL%
if %DEBUG_ERRORLEVEL% NEQ 0 echo ##teamcity[message text='Error running quickbuild for debug variant! See full build log for more details.' status='ERROR']

REM # HACK: clean the bindings because it builds to the same place in any linkage configuration
call quickbuild.bat --clean pwiz\utility\bindings\CLI

REM # call quickbuild to build and run tests with variant=release link=shared
echo ##teamcity[progressMessage 'Running quickbuild for shared release variant...']
call quickbuild.bat -j4 -p1 --teamcity-test-decoration release link=shared
set SHARED_RELEASE_ERRORLEVEL=%ERRORLEVEL%
if %EXIT% EQU 0 set EXIT=%SHARED_RELEASE_ERRORLEVEL%
if %SHARED_RELEASE_ERRORLEVEL% NEQ 0 echo ##teamcity[message text='Error running quickbuild for shared release variant! See full build log for more details.' status='ERROR']

REM # call quickbuild to build and run tests with variant=debug link=shared
echo ##teamcity[progressMessage 'Running quickbuild for shared debug variant...']
call quickbuild.bat -j4 -p1 --teamcity-test-decoration debug link=shared
set SHARED_DEBUG_ERRORLEVEL=%ERRORLEVEL%
if %EXIT% EQU 0 set EXIT=%SHARED_DEBUG_ERRORLEVEL%
if %SHARED_DEBUG_ERRORLEVEL% NEQ 0 echo ##teamcity[message text='Error running quickbuild for shared debug variant! See full build log for more details.' status='ERROR']

REM # uncomment this to test that test failures and error output are handled properly
REM call quickbuild.bat -p1 --teamcity-test-decoration pwiz/utility/misc//FailUnitTest pwiz/utility/misc//FailRunTest

popd

:error
echo "##teamcity[message text='%ERROR_TEXT%' status='ERROR']"
exit /b %EXIT%
