@echo off
setlocal
@echo off

set VERBOSE=1
if /I "%1"=="-quiet" set VERBOSE=0
if /I "%1"=="-q" set VERBOSE=0

REM # Get the location of quickbuild.bat and drop trailing slash
set PWIZ_ROOT=%~dp0
set PWIZ_ROOT=%PWIZ_ROOT:~0,-1%
pushd %PWIZ_ROOT%

if %VERBOSE%==1 echo   Cleaning build directories...
IF EXIST build-nt-x86 rmdir /s /q build-nt-x86
IF EXIST build-nt-x86_64 rmdir /s /q build-nt-x86_64

if %VERBOSE%==1 echo   Cleaning libraries...
IF EXIST libraries\boost-build\engine\bin.nt rmdir /s /q libraries\boost-build\engine\bin.nt
IF EXIST libraries\boost-build\engine\bootstrap rmdir /s /q libraries\boost-build\engine\bootstrap
IF EXIST libraries\boost_1_43_0 rmdir /s /q libraries\boost_1_43_0
IF EXIST libraries\boost_1_54_0 rmdir /s /q libraries\boost_1_54_0
IF EXIST libraries\boost_1_56_0 rmdir /s /q libraries\boost_1_56_0
IF EXIST libraries\gd-2.0.33 rmdir /s /q libraries\gd-2.0.33
IF EXIST libraries\zlib-1.2.3 rmdir /s /q libraries\zlib-1.2.3
IF EXIST libraries\libgd-2.1.0alpha rmdir /s /q libraries\libgd-2.1.0alpha
IF EXIST libraries\libpng-1.5.6 rmdir /s /q libraries\libpng-1.5.6
IF EXIST libraries\freetype-2.4.7 rmdir /s /q libraries\freetype-2.4.7
IF EXIST libraries\hdf5-1.8.7 rmdir /s /q libraries\hdf5-1.8.7
IF EXIST libraries\fftw-3.1.2 rmdir /s /q libraries\fftw-3.1.2
IF EXIST libraries\expat-2.0.1 rmdir /s /q libraries\expat-2.0.1

del /f /q libraries\libfftw3-3.d* > nul 2>&1

del /f /q pwiz\Version.cpp > nul 2>&1
del /f /q pwiz\data\msdata\Version.cpp > nul 2>&1
del /f /q pwiz\data\identdata\Version.cpp > nul 2>&1
del /f /q pwiz\data\tradata\Version.cpp > nul 2>&1
del /f /q pwiz\data\proteome\Version.cpp > nul 2>&1
del /f /q pwiz\analysis\Version.cpp > nul 2>&1

if %VERBOSE%==1 echo   Cleaning vendor dlls...
del /f /q pwiz_aux\msrc\utility\vendor_api\ABI\*.dll > nul 2>&1
del /f /q pwiz_aux\msrc\utility\vendor_api\ABI\LicenseKey.h > nul 2>&1
rmdir /s /q pwiz_aux\msrc\utility\vendor_api\ABI\vc10 > nul 2>&1
rmdir /s /q pwiz_aux\msrc\utility\vendor_api\ABI\vc9 > nul 2>&1

del /f /q pwiz_aux\msrc\utility\vendor_api\Agilent\*.dll > nul 2>&1
rmdir /s /q pwiz_aux\msrc\utility\vendor_api\Agilent\x86 > nul 2>&1
rmdir /s /q pwiz_aux\msrc\utility\vendor_api\Agilent\x64 > nul 2>&1
del /f /q pwiz_aux\msrc\utility\vendor_api\Agilent\EULA.* > nul 2>&1
rmdir /s /q pwiz_aux\msrc\utility\vendor_api\Agilent\Documents > nul 2>&1
del /f /q pwiz_aux\msrc\utility\vendor_api\Bruker\*.manifest > nul 2>&1
del /f /q pwiz_aux\msrc\utility\vendor_api\Bruker\baf2sql_c.h > nul 2>&1
del /f /q pwiz_aux\msrc\utility\vendor_api\Bruker\baf2sql_cpp.h > nul 2>&1
del /f /q pwiz_aux\msrc\utility\vendor_api\Bruker\schema.h > nul 2>&1
rmdir /s /q pwiz_aux\msrc\utility\vendor_api\Bruker\install_pwiz_vendor_api_bruker_stub > nul 2>&1
rmdir /s /q pwiz_aux\msrc\utility\vendor_api\Bruker\x86 > nul 2>&1
rmdir /s /q pwiz_aux\msrc\utility\vendor_api\Bruker\x64 > nul 2>&1
del /f /q pwiz_aux\msrc\utility\vendor_api\Shimadzu\EULA.SFCS > nul 2>&1
rmdir /s /q pwiz_aux\msrc\utility\vendor_api\Shimadzu\x86 > nul 2>&1
rmdir /s /q pwiz_aux\msrc\utility\vendor_api\Shimadzu\x64 > nul 2>&1
del /f /q pwiz_aux\msrc\utility\vendor_api\Thermo\*.dll > nul 2>&1
del /f /q pwiz_aux\msrc\utility\vendor_api\Thermo\*.manifest > nul 2>&1
rmdir /s /q pwiz_aux\msrc\utility\vendor_api\Thermo\x86 > nul 2>&1
rmdir /s /q pwiz_aux\msrc\utility\vendor_api\Thermo\x64 > nul 2>&1
del /f /q pwiz_aux\msrc\utility\vendor_api\Thermo\EULA.* > nul 2>&1
del /f /q /s pwiz_aux\msrc\utility\vendor_api\Waters\*.dll > nul 2>&1
del /f /q /s pwiz_aux\msrc\utility\vendor_api\Waters\*.lib > nul 2>&1
rmdir /s /q pwiz_aux\msrc\utility\vendor_api\Waters\vc12_x86 > nul 2>&1
rmdir /s /q pwiz_aux\msrc\utility\vendor_api\Waters\vc12_x64 > nul 2>&1
del /f /q pwiz_aux\msrc\utility\vendor_api\Waters\*.h > nul 2>&1

if %VERBOSE%==1 echo   Cleaning vendor test data...
rmdir /s /q pwiz\data\vendor_readers\Thermo\Reader_Thermo_Test.data > nul 2>&1
rmdir /s /q pwiz\data\vendor_readers\Agilent\Reader_Agilent_Test.data > nul 2>&1
rmdir /s /q pwiz\data\vendor_readers\ABI\Reader_ABI_Test.data > nul 2>&1
rmdir /s /q pwiz\data\vendor_readers\ABI\T2D\Reader_ABI_T2D_Test.data > nul 2>&1
rmdir /s /q pwiz\data\vendor_readers\Waters\Reader_Waters_Test.data > nul 2>&1
rmdir /s /q pwiz\data\vendor_readers\Bruker\Reader_Bruker_Test.data > nul 2>&1
rmdir /s /q pwiz\data\vendor_readers\UIMF\Reader_UIMF_Test.data > nul 2>&1

IF EXIST pwiz_tools\clean-apps.bat call pwiz_tools\clean-apps.bat

popd
