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

REM # download latest OBO files
pushd %~dp0
echo Downloading latest controlled vocabularies...
call download_cv.vbs
popd

@echo off

REM # Extract Boost.Build (for VC9 support)
pushd %PWIZ_ROOT%\libraries
IF EXIST boost-build\jam_src\build.bat GOTO SKIP_BB
bsdtar.exe -xkjvf boost-build.tar.bz2
copy /Y msvc.jam boost-build\tools
:SKIP_BB
popd

@echo off

set PWIZ_BJAM=%PWIZ_ROOT%\libraries\boost-build\jam_src\bin.ntx86\bjam.exe

@echo off

REM # Build local copy of bjam
IF EXIST %PWIZ_BJAM% GOTO SKIP_BJAM
echo Building bjam...
pushd %PWIZ_ROOT%\libraries\boost-build\jam_src
call build.bat
popd
:SKIP_BJAM

@echo off

set BOOST_BUILD_PATH=%PWIZ_ROOT%\libraries\boost-build

@echo off

REM # build and run cvgen targets
echo Building and running cvgen and cvgen_cli...
%PWIZ_BJAM% toolset=msvc %PWIZ_ROOT%\pwiz\data\msdata//cv.hpp %PWIZ_ROOT%\pwiz\utility\bindings\CLI//cv.hpp %*
