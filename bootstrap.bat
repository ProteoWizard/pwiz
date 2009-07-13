@echo off
setlocal
@echo off

REM # Get the location of quickbuild.bat and drop trailing slash
set PWIZ_ROOT=%~dp0
set PWIZ_ROOT=%PWIZ_ROOT:~0,-1%

REM # Clean any existing bootstrap and build artifacts
call %PWIZ_ROOT%\clean.bat

REM # Extract Boost distro
call %PWIZ_ROOT%\libraries\untar_boost.bat  %PWIZ_ROOT%


REM # Extract Boost.Build (for VC9 support)
pushd %PWIZ_ROOT%\libraries
echo Extracting boost-build tarball...
bsdtar.exe -xkjf boost-build.tar.bz2
copy /Y msvc.jam boost-build\tools

REM # Extract Libraries
call %PWIZ_ROOT%\libraries\untar_fftw.bat
call %PWIZ_ROOT%\libraries\untar_gd.bat
call %PWIZ_ROOT%\libraries\untar_zlib.bat
popd

set PWIZ_BJAM=%PWIZ_ROOT%\libraries\boost-build\jam_src\bin.ntx86\bjam.exe
REM set PWIZ_BJAM=%PWIZ_ROOT%\libraries\bjam.exe

REM # msvc.jam assumes it will find "ShowVer.exe" in %PATH%
set PATH=%PATH%;%PWIZ_ROOT%\libraries

REM # Build local copy of bjam
echo Building bjam...
pushd %PWIZ_ROOT%\libraries\boost-build\jam_src
call build.bat
popd
