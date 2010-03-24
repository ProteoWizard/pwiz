@echo off
setlocal
@echo off

REM # Get to the pwiz root directory
set FREICORE_ROOT=%~dp0
set FREICORE_ROOT=%FREICORE_ROOT:~0,-1%
pushd %FREICORE_ROOT%

REM # call clean
echo ##teamcity[message text='Cleaning project...']
echo ##teamcity[progressMessage 'Cleaning project...']
call clean.bat
if %ERRORLEVEL% NEQ 0 set ERROR_TEXT=Error performing clean & goto error

REM # the -p1 argument overrides bjam's default behavior of merging stderr into stdout

REM # call quickbuild to build and run tests with variant=release
echo ##teamcity[message text='Running quickbuild for release variant...']
echo ##teamcity[progressMessage 'Running quickbuild for release variant...']
call quickbuild.bat %* -p1 --teamcity-test-decoration release
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 echo ##teamcity[message text='Error running quickbuild for release variant! See full build log for more details.' status='ERROR']

REM # call quickbuild to build and run tests with variant=debug
echo ##teamcity[message text='Running quickbuild for debug variant...']
echo ##teamcity[progressMessage 'Running quickbuild for debug variant...']
call quickbuild.bat %* -p1 --teamcity-test-decoration debug
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 echo ##teamcity[message text='Error running quickbuild for debug variant! See full build log for more details.' status='ERROR']

REM # uncomment this to test that test failures and error output are handled properly
REM call quickbuild.bat -p1 ci=teamcity pwiz/utility/misc//FailUnitTest pwiz/utility/misc//FailRunTest

REM # delete everything but the tarballs and installer (to save space)
move DirecTag\build-nt-x86\*.tar.bz2 .
move DirecTag\build-nt-x86\*.msi .
rmdir /s /q DirecTag\build-nt-x86
rmdir /s /q DirecTag\freicore\pwiz_src
mkdir DirecTag\build-nt-x86
move *.tar.bz2 DirecTag\build-nt-x86
move *.msi DirecTag\build-nt-x86

popd

:error
echo "##teamcity[message text='%ERROR_TEXT%' status='ERROR']"
exit /b %EXIT%
