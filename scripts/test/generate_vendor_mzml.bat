@echo off
setlocal
@echo off

REM # Each vendor reader has a script that calls this script.
REM # This script regenerates the vendor unit test data tarballs iff they are out of date.

REM # Find the location of Jamroot.jam relative to this script.
set PWIZ_ROOT=%~dp0..\..
pushd %PWIZ_ROOT%

REM # clean the vendor readers
call scripts\misc\clean_vendor_targets.bat

set name=%1
set readerpath=%2

echo Building Reader_%name% and running its unit test...
call quickbuild.bat -q toolset=msvc --i-agree-to-the-vendor-licenses --abbreviate-paths %readerpath% -j4 > nul
if %ERRORLEVEL% EQU 0 GOTO SKIP
echo The mzML for Reader_%name% is out of date. Regenerating mzML...
call quickbuild.bat -q toolset=msvc --i-agree-to-the-vendor-licenses --abbreviate-paths --incremental --generate-mzML -j4 %readerpath%
if %ERRORLEVEL% NEQ 0 GOTO ERROR
echo Tarballing Reader_%name% test data...
call scripts\test\tar_test_data.bat %readerpath% %PWIZ_ROOT% Reader_%name%_Test.data > nul
:SKIP
echo Reader_%name% test data is up to date.
GOTO end
:ERROR
echo Error occured while generating the mzML. Use the regular build commands to fix it.
GOTO end

:end
REM # pop PWIZ_ROOT
popd
