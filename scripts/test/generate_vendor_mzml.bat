@echo off
setlocal
@echo off

REM # Use this script to regenerate the vendor unit test data tarballs.

REM # Find the location of Jamroot.jam relative to this script.
set PWIZ_ROOT=%~dp0..\..
pushd %PWIZ_ROOT%

REM # clean the vendor readers
call clean_vendor_targets.bat

REM # build and run vendor unit tests with the --generate-mzML flag
echo Generating mzML for vendor test data...
call quickbuild.bat toolset=msvc --generate-mzML pwiz\data\vendor_readers pwiz_aux\msrc\data\vendor_readers


REM # tarball the output
echo Tarballing vendor test data with regenerated mzML...

call scripts\test\tar_test_data.bat pwiz\data\vendor_readers\Agilent %PWIZ_ROOT% Reader_Agilent_Test.data
call scripts\test\tar_test_data.bat pwiz\data\vendor_readers\Thermo %PWIZ_ROOT% Reader_Thermo_Test.data
call scripts\test\tar_test_data.bat pwiz_aux\msrc\data\vendor_readers\ABI %PWIZ_ROOT% Reader_ABI_Test.data
call scripts\test\tar_test_data.bat pwiz_aux\msrc\data\vendor_readers\Bruker %PWIZ_ROOT% Reader_Bruker_Test.data
call scripts\test\tar_test_data.bat pwiz_aux\msrc\data\vendor_readers\Waters %PWIZ_ROOT% Reader_Waters_Test.data

REM # pop PWIZ_ROOT
popd