@echo off
setlocal
@echo off

REM # Use this script to regenerate the vendor unit test data tarballs iff they are out of date.

REM # Find the location of Jamroot.jam relative to this script.
set PWIZ_ROOT=%~dp0..\..
pushd %PWIZ_ROOT%

REM # clean the vendor readers
call clean_vendor_targets.bat

:Agilent
set name=Agilent
set readerpath=pwiz\data\vendor_readers\Agilent
set next=Thermo
GOTO READER_TEST

:Thermo
set name=Thermo
set readerpath=pwiz\data\vendor_readers\Thermo
set next=ABI
GOTO READER_TEST

:ABI
set name=ABI
set readerpath=pwiz_aux\msrc\data\vendor_readers\ABI
set next=Bruker
GOTO READER_TEST

:Bruker
set name=Bruker
set readerpath=pwiz_aux\msrc\data\vendor_readers\Bruker
set next=Waters
GOTO READER_TEST

:Waters
set name=Waters
set readerpath=pwiz_aux\msrc\data\vendor_readers\Waters
set next=end
GOTO READER_TEST


:READER_TEST
echo Building Reader_%name% and running its unit test...
call quickbuild.bat -q %* toolset=msvc %readerpath% > nul
if %ERRORLEVEL% EQU 0 GOTO SKIP
echo The mzML for Reader_%name% is out of date. Regenerating mzML...
call quickbuild.bat -q %* toolset=msvc --generate-mzML %readerpath% > nul
if %ERRORLEVEL% NEQ 0 GOTO ERROR
echo Tarballing Reader_%name% test data...
call scripts\test\tar_test_data.bat %readerpath% %PWIZ_ROOT% Reader_%name%_Test.data > nul
:SKIP
echo Reader_%name% test data is up to date.
GOTO %next%
:ERROR
echo Error occured while generating the mzML. Use the regular build commands to fix it.
GOTO %next%


:end
REM # pop PWIZ_ROOT
popd