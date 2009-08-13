@echo off
setlocal
@echo off

REM # Get the location of quickbuild.bat and drop trailing slash
set PWIZ_ROOT=%~dp0..\..
pushd %PWIZ_ROOT%

echo Cleaning project...
IF EXIST build rmdir /s /q build
IF EXIST libraries\boost_1_36_0 rmdir /s /q libraries\boost_1_36_0
IF EXIST libraries\boost_1_39_0 rmdir /s /q libraries\boost_1_39_0
IF EXIST libraries\boost-build rmdir /s /q libraries\boost-build
IF EXIST libraries\gd-2.0.33 rmdir /s /q libraries\gd-2.0.33
IF EXIST libraries\zlib-1.2.3 rmdir /s /q libraries\zlib-1.2.3
IF EXIST libraries\fftw-3.1.2 rmdir /s /q libraries\fftw-3.1.2
IF EXIST libraries\libfftw3-3.def del /q libraries\libfftw3-3.def
IF EXIST libraries\libfftw3-3.dll del /q libraries\libfftw3-3.dll
IF EXIST pwiz\data\vendor_readers\Thermo\Reader_Thermo_Test.data rmdir /s /q pwiz\data\vendor_readers\Thermo\Reader_Thermo_Test.data
IF EXIST pwiz\data\vendor_readers\Agilent\Reader_Agilent_Test.data rmdir /s /q pwiz\data\vendor_readers\Agilent\Reader_Agilent_Test.data
IF EXIST pwiz_aux\msrc\data\vendor_readers\ABI\Reader_ABI_Test.data rmdir /s /q pwiz_aux\msrc\data\vendor_readers\ABI\Reader_ABI_Test.data
IF EXIST pwiz_aux\msrc\data\vendor_readers\Waters\Reader_Waters_Test.data rmdir /s /q pwiz_aux\msrc\data\vendor_readers\Waters\Reader_Waters_Test.data
IF EXIST pwiz_aux\msrc\data\vendor_readers\Bruker\Reader_Bruker_Test.data rmdir /s /q pwiz_aux\msrc\data\vendor_readers\Bruker\Reader_Bruker_Test.data

popd
