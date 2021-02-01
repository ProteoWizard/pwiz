@echo off
setlocal
@echo off

REM # Get the location of quickbuild.bat and drop trailing slash
set PWIZ_ROOT=%~dp0..\..
pushd %PWIZ_ROOT%

echo Cleaning project of vendor-related files...
IF EXIST build-nt-x86\msvc-release rmdir /s /q build-nt-x86\msvc-release
IF EXIST build-nt-x86\msvc-release-x86_64 rmdir /s /q build-nt-x86\msvc-release-x86_64
IF EXIST build-nt-x86\msvc-debug rmdir /s /q build-nt-x86\msvc-debug
IF EXIST build-nt-x86\msvc-debug-x86_64 rmdir /s /q build-nt-x86\msvc-debug-x86_64
IF EXIST build-nt-x86\gcc-release rmdir /s /q build-nt-x86\gcc-release
IF EXIST build-nt-x86\gcc-debug rmdir /s /q build-nt-x86\gcc-debug
IF EXIST build-nt-x86\pwiz\data\vendor_readers rmdir /s /q build-nt-x86\pwiz\data\vendor_readers
IF EXIST build-nt-x86\pwiz\utility\vendor_api rmdir /s /q build-nt-x86\pwiz\utility\vendor_api
IF EXIST build-nt-x86\pwiz\utility\bindings rmdir /s /q build-nt-x86\pwiz\utility\bindings
IF EXIST build-nt-x86\pwiz_aux\msrc\data\vendor_readers rmdir /s /q build-nt-x86\pwiz_aux\msrc\data\vendor_readers
IF EXIST build-nt-x86\pwiz_aux\msrc\utility\vendor_api rmdir /s /q build-nt-x86\pwiz_aux\msrc\utility\vendor_api
IF EXIST build-nt-x86\pwiz\analysis\spectrum_processing rmdir /s /q build-nt-x86\pwiz\analysis\spectrum_processing
IF EXIST build-nt-x86\pwiz\analysis\chromatogram_processing rmdir /s /q build-nt-x86\pwiz\analysis\chromatogram_processing

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
del /f /q pwiz_aux\msrc\utility\vendor_api\Shimadzu\*.dll > nul 2>&1
rmdir /s /q pwiz_aux\msrc\utility\vendor_api\Shimadzu\ja-JP > nul 2>&1
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

rmdir /s /q pwiz\data\vendor_readers\Thermo\Reader_Thermo_Test.data > nul 2>&1
rmdir /s /q pwiz\data\vendor_readers\Agilent\Reader_Agilent_Test.data > nul 2>&1
rmdir /s /q pwiz\data\vendor_readers\ABI\Reader_ABI_Test.data > nul 2>&1
rmdir /s /q pwiz\data\vendor_readers\ABI\T2D\Reader_ABI_T2D_Test.data > nul 2>&1
git clean -f -d -X pwiz\data\vendor_readers\Bruker\Reader_Bruker_Test.data
git clean -f -d -X pwiz\data\vendor_readers\Waters\Reader_Waters_Test.data
REM rmdir /s /q pwiz\data\vendor_readers\Bruker\Reader_Shimadzu_Test.data > nul 2>&1


popd
