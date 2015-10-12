@echo off
setlocal
@echo off

REM ##
REM # This Windows script downloads the latest version of the PSI-MS and unit
REM # ontologies in OBO format and runs the bjam target to parse them into
REM # ProteoWizard enumerations in cv.hpp (for MSData and its CLI binding).
REM # Much of this script is shared with quickbuild.bat in the pwiz root.
REM ##

REM # Get the location of update_cv.bat and drop trailing slash
set PWIZ_ROOT=%~dp0..\..
REM set PWIZ_ROOT=%PWIZ_ROOT:~0,-1%
set BOOST_BUILD_PATH=%PWIZ_ROOT%\libraries\boost-build

REM # download latest OBO files
pushd %~dp0
echo Downloading latest controlled vocabularies...
call download_cv.vbs
popd

@echo off

set PWIZ_BJAM=%BOOST_BUILD_PATH%\engine\bin.ntx86\bjam.exe

REM # msvc.jam assumes it will find "ShowVer.exe" in %PATH%
set PATH=%PATH%;%PWIZ_ROOT%\libraries

REM # Build local copy of bjam
IF EXIST %PWIZ_BJAM% GOTO SKIP_BJAM
echo Building bjam...
pushd %BOOST_BUILD_PATH%\engine
call build.bat
@echo off
setlocal
@echo off
popd
:SKIP_BJAM

set BOOST_BUILD_PATH=%PWIZ_ROOT%\libraries\boost-build

REM # build and run cvgen targets
echo Building and running cvgen and cvgen_cli...
%PWIZ_BJAM% %PWIZ_ROOT%\pwiz\data\common//cv.hpp %PWIZ_ROOT%\pwiz\utility\bindings\CLI\common//cv.hpp %*
