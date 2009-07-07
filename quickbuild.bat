@echo off
setlocal
@echo off

REM # Get the location of quickbuild.bat and drop trailing slash
set PWIZ_ROOT=%~dp0
set PWIZ_ROOT=%PWIZ_ROOT:~0,-1%

REM # Extract Boost distro
call %PWIZ_ROOT%\libraries\untar_boost.bat  %PWIZ_ROOT%

REM # Extract Boost.Build (for VC9 support)
pushd %PWIZ_ROOT%\libraries
IF EXIST boost-build\jam_src\build.bat GOTO SKIP_BB
bsdtar.exe -xkjvf boost-build.tar.bz2
:SKIP_BB
copy /Y msvc.jam boost-build\tools
popd

set PWIZ_BJAM=%PWIZ_ROOT%\libraries\boost-build\jam_src\bin.ntx86\bjam.exe
REM set PWIZ_BJAM=%PWIZ_ROOT%\libraries\bjam.exe

REM # msvc.jam assumes it will find "ShowVer.exe" in %PATH%
set PATH=%PATH%;%PWIZ_ROOT%\libraries

REM # Build local copy of bjam
IF EXIST %PWIZ_BJAM% GOTO SKIP_BJAM
echo Building bjam...
pushd %PWIZ_ROOT%\libraries\boost-build\jam_src
call build.bat
popd
:SKIP_BJAM

set BOOST_BUILD_PATH=%PWIZ_ROOT%\libraries\boost-build

REM # Do full build of ProteoWizard, passing quickbuild's arguments to bjam
echo Building pwiz...
%PWIZ_BJAM% %PWIZ_ROOT% %*
