@echo off
setlocal
@echo off

REM # Get the location of quickbuild.bat and drop trailing slash
set PWIZ_ROOT=%~dp0
set PWIZ_ROOT=%PWIZ_ROOT:~0,-1%
pushd %PWIZ_ROOT%

echo Cleaning project...
IF EXIST build-nt-x86 rmdir /s /q build-nt-x86
IF EXIST build-nt-x86_64 rmdir /s /q build-nt-x86_64
IF EXIST libraries\boost-build\jam_src\bin.ntx86 rmdir /s /q libraries\boost-build\jam_src\bin.ntx86
IF EXIST libraries\boost-build\jam_src\bootstrap rmdir /s /q libraries\boost-build\jam_src\bootstrap
IF EXIST libraries\boost_1_43_0 rmdir /s /q libraries\boost_1_43_0
IF EXIST libraries\gd-2.0.33 rmdir /s /q libraries\gd-2.0.33
IF EXIST libraries\zlib-1.2.3 rmdir /s /q libraries\zlib-1.2.3
IF EXIST libraries\fftw-3.1.2 rmdir /s /q libraries\fftw-3.1.2

IF EXIST libraries\libfftw3-3.def del /q libraries\libfftw3-3.def
IF EXIST libraries\libfftw3-3.dll del /q libraries\libfftw3-3.dll

IF EXIST pwiz\Version.cpp del /q pwiz\Version.cpp
IF EXIST pwiz\data\msdata\Version.cpp del /q pwiz\data\msdata\Version.cpp
IF EXIST pwiz\data\mziddata\Version.cpp del /q pwiz\data\mziddata\Version.cpp
IF EXIST pwiz\data\tradata\Version.cpp del /q pwiz\data\tradata\Version.cpp
IF EXIST pwiz\data\proteome\Version.cpp del /q pwiz\data\proteome\Version.cpp
IF EXIST pwiz\analysis\Version.cpp del /q pwiz\analysis\Version.cpp

IF EXIST pwiz_aux\msrc\utility\vendor_api\thermo\MSFileReader.XRawfile2.dll del /q pwiz_aux\msrc\utility\vendor_api\thermo\MSFileReader.XRawfile2.dll
IF EXIST pwiz_aux\msrc\utility\vendor_api\thermo\fregistry.dll del /q pwiz_aux\msrc\utility\vendor_api\thermo\fregistry.dll
IF EXIST pwiz_aux\msrc\utility\vendor_api\thermo\fileio.dll del /q pwiz_aux\msrc\utility\vendor_api\thermo\fileio.dll
IF EXIST pwiz_aux\msrc\utility\vendor_api\ABI\ABSciex.DataAccess.WiffFileDataReader.dll del /q pwiz_aux\msrc\utility\vendor_api\ABI\ABSciex.DataAccess.WiffFileDataReader.dll
IF EXIST pwiz_aux\msrc\utility\vendor_api\ABI\Clearcore.dll del /q pwiz_aux\msrc\utility\vendor_api\ABI\Clearcore.dll
IF EXIST pwiz_aux\msrc\utility\vendor_api\ABI\Clearcore.Storage.dll del /q pwiz_aux\msrc\utility\vendor_api\ABI\Clearcore.Storage.dll
IF EXIST pwiz_aux\msrc\utility\vendor_api\ABI\rscoree.dll del /q pwiz_aux\msrc\utility\vendor_api\ABI\rscoree.dll

IF EXIST pwiz\data\vendor_readers\Thermo\Reader_Thermo_Test.data rmdir /s /q pwiz\data\vendor_readers\Thermo\Reader_Thermo_Test.data
IF EXIST pwiz\data\vendor_readers\Agilent\Reader_Agilent_Test.data rmdir /s /q pwiz\data\vendor_readers\Agilent\Reader_Agilent_Test.data
IF EXIST pwiz\data\vendor_readers\ABI\Reader_ABI_Test.data rmdir /s /q pwiz\data\vendor_readers\ABI\Reader_ABI_Test.data
IF EXIST pwiz\data\vendor_readers\ABI\T2D\Reader_ABI_T2D_Test.data rmdir /s /q pwiz\data\vendor_readers\ABI\T2D\Reader_ABI_T2D_Test.data
IF EXIST pwiz\data\vendor_readers\Waters\Reader_Waters_Test.data rmdir /s /q pwiz\data\vendor_readers\Waters\Reader_Waters_Test.data
IF EXIST pwiz\data\vendor_readers\Bruker\Reader_Bruker_Test.data rmdir /s /q pwiz\data\vendor_readers\Bruker\Reader_Bruker_Test.data

IF EXIST pwiz_tools\SeeMS\CleanSeeMS.bat call pwiz_tools\SeeMS\CleanSeeMS.bat
IF EXIST pwiz_tools\Skyline\CleanSkyline.bat call pwiz_tools\Skyline\CleanSkyline.bat
IF EXIST pwiz_tools\Topograph\CleanTopograph.bat call pwiz_tools\Topograph\CleanTopograph.bat

popd
