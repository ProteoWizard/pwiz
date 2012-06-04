@echo off
setlocal
@echo off

REM # Get the location of quickbuild.bat and drop trailing slash
set ROOT=%~dp0
set ROOT=%ROOT:~0,-1%

set BJAM=%ROOT%\freicore\libraries\boost-build\jam_src\bin.ntx86\bjam.exe

REM # msvc.jam assumes it will find "ShowVer.exe" in %PATH%
set PATH=%PATH%;%ROOT%\freicore\libraries

REM # Build local copy of bjam
IF EXIST %BJAM% GOTO SKIP_BJAM
echo Building bjam...
pushd %ROOT%\freicore\libraries\boost-build\jam_src
call build.bat
@echo off
setlocal
@echo off
popd
:SKIP_BJAM

set BOOST_BUILD_PATH=%ROOT%\freicore\libraries\boost-build

REM # Do full build of ProteoWizard, passing quickbuild's arguments to bjam
pushd %ROOT%
%BJAM% %*
popd
