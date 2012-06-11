@echo off
setlocal
@echo off

REM # Get the location of quickbuild.bat and drop trailing slash
set PWIZ_ROOT=%~dp0
set PWIZ_ROOT=%PWIZ_ROOT:~0,-1%
set BOOST_BUILD_PATH=%PWIZ_ROOT%\libraries\boost-build

set PWIZ_BJAM=%BOOST_BUILD_PATH%\engine\bin.ntx86\bjam.exe

REM # msvc.jam assumes it will find "ShowVer.exe" in %PATH%
set PATH=%PWIZ_ROOT%\libraries;%PATH%

REM # determine address-model (default 32)
set ADDRESS_MODEL=32
set ALL_ARGS= %*
REM # remove pesky '=' character for subsequent string substitution
for /f "usebackq tokens=*" %%a in ('%ALL_ARGS%') do set ALL_ARGS=%%~a
if "%ALL_ARGS: address-model 64=%" neq "%ALL_ARGS%" set ADDRESS_MODEL=64

REM # Build local copy of bjam
IF EXIST %PWIZ_BJAM% GOTO SKIP_BJAM
echo Building bjam x86 for %ADDRESS_MODEL%-bit build...
pushd %BOOST_BUILD_PATH%\engine
call build.bat
@echo off
setlocal
@echo off
popd
:SKIP_BJAM

REM # Do full build of ProteoWizard, passing quickbuild's arguments to bjam
echo Building pwiz (%ADDRESS_MODEL%-bit)...
pushd %PWIZ_ROOT%
%PWIZ_BJAM% %*
popd
