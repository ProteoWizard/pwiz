@echo off
setlocal
@echo off

REM # Get to the pwiz root directory
set SCRIPTS_MISC_ROOT=%~dp0
set SCRIPTS_MISC_ROOT=%SCRIPTS_MISC_ROOT:~0,-1%
pushd %SCRIPTS_MISC_ROOT%\..\..

REM # call clean
echo ##teamcity[message text='Cleaning project...']
echo ##teamcity[progressMessage 'Cleaning project...']
call clean.bat
if %ERRORLEVEL% NEQ 0 set ERROR_TEXT=Error performing clean & goto error

REM # the -p1 argument overrides bjam's default behavior of merging stderr into stdout

REM # call quickbuild to generate SVN revision info
echo ##teamcity[message text='Generating revision info...']
echo ##teamcity[progressMessage 'Generating revision info...']
call quickbuild.bat -j4 -p1 pwiz//svnrev.hpp
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 set ERROR_TEXT=Error generating revision info & goto error

REM # call quickbuild to build and run tests with variant=release
echo ##teamcity[message text='Running quickbuild for release variant...']
echo ##teamcity[progressMessage 'Running quickbuild for release variant...']
call quickbuild.bat -j4 -p1 ci=teamcity release
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 echo ##teamcity[message text='Error running quickbuild for release variant! See full build log for more details.' status='ERROR']

REM # call quickbuild to build and run tests with variant=debug
echo ##teamcity[message text='Running quickbuild for debug variant...']
echo ##teamcity[progressMessage 'Running quickbuild for debug variant...']
call quickbuild.bat -j4 -p1 ci=teamcity debug
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 echo ##teamcity[message text='Error running quickbuild for debug variant! See full build log for more details.' status='ERROR']

REM # uncomment this to test that test failures and error output are handled properly
REM call quickbuild.bat -p1 ci=teamcity pwiz/utility/misc//FailUnitTest pwiz/utility/misc//FailRunTest

popd

:error
echo "##teamcity[message text='%ERROR_TEXT%' status='ERROR']"
exit /b %EXIT%
