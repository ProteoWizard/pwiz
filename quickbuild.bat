@echo off
setlocal
@echo off

REM # Get the location of quickbuild.bat and drop trailing slash
set PWIZ_ROOT=%~dp0
set PWIZ_ROOT=%PWIZ_ROOT:~0,-1%

set PWIZ_BJAM=%PWIZ_ROOT%\libraries\boost-build\jam_src\bin.ntx86\bjam.exe

REM # msvc.jam assumes it will find "ShowVer.exe" in %PATH%
set PATH=%PATH%;%PWIZ_ROOT%\libraries

REM # Build local copy of bjam
IF EXIST %PWIZ_BJAM% GOTO SKIP_BJAM
echo Building bjam...
pushd %PWIZ_ROOT%\libraries\boost-build\jam_src
call build.bat
@echo off
setlocal
@echo off
popd
:SKIP_BJAM

set BOOST_BUILD_PATH=%PWIZ_ROOT%\libraries\boost-build

REM # Do full build of ProteoWizard, passing quickbuild's arguments to bjam
echo Building pwiz...
pushd %PWIZ_ROOT%
%PWIZ_BJAM% %*
popd
