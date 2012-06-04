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
IF EXIST freicore\libraries\boost-build\jam_src\bin.ntx86 rmdir /s /q freicore\libraries\boost-build\jam_src\bin.ntx86
IF EXIST freicore\libraries\boost-build\jam_src\bootstrap rmdir /s /q freicore\libraries\boost-build\jam_src\bootstrap
IF EXIST freicore\libraries\boost_1_36_0 rmdir /s /q freicore\libraries\boost_1_36_0
IF EXIST freicore\libraries\boost_1_39_0 rmdir /s /q freicore\libraries\boost_1_39_0
IF EXIST freicore\libraries\gd-2.0.33 rmdir /s /q freicore\libraries\gd-2.0.33
IF EXIST freicore\libraries\zlib-1.2.3 rmdir /s /q freicore\libraries\zlib-1.2.3
IF EXIST freicore\libraries\fftw-3.1.2 rmdir /s /q freicore\libraries\fftw-3.1.2

IF EXIST freicore\libraries\libfftw3-3.def del /q freicore\libraries\libfftw3-3.def
IF EXIST freicore\libraries\libfftw3-3.dll del /q freicore\libraries\libfftw3-3.dll

IF EXIST freicore\pwiz_src rmdir /s /q freicore\pwiz_src

popd
