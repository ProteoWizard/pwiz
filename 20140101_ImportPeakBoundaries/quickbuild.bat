@echo off
setlocal
@echo off

set start=%time%
echo Build started at %start%

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

REM # need to check if no arguments were passed, or else batch will complain
REM # about a comparison with an empty string
if "%ALL_ARGS%"=="" GOTO SKIP_ADDRESS_CHECK
if "%ALL_ARGS:address-model 64=%" neq "%ALL_ARGS%" set ADDRESS_MODEL=64

:SKIP_ADDRESS_CHECK
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


set end=%time%
echo Build finished at %end%

REM # Calculate elapsed time
set options="tokens=1-4 delims=:."
for /f %options% %%a in ("%start%") do set start_h=%%a&set /a start_m=100%%b %% 100&set /a start_s=100%%c %% 100&set /a start_ms=100%%d %% 100
for /f %options% %%a in ("%end%") do set end_h=%%a&set /a end_m=100%%b %% 100&set /a end_s=100%%c %% 100&set /a end_ms=100%%d %% 100
set /a hours=%end_h%-%start_h%
set /a mins=%end_m%-%start_m%
set /a secs=%end_s%-%start_s%
set /a ms=%end_ms%-%start_ms%
if %hours% lss 0 set /a hours = 24%hours%
if %mins% lss 0 set /a hours = %hours% - 1 & set /a mins = 60%mins%
if %secs% lss 0 set /a mins = %mins% - 1 & set /a secs = 60%secs%
if %ms% lss 0 set /a secs = %secs% - 1 & set /a ms = 100%ms%
if 1%ms% lss 100 set ms=0%ms%
set /a totalsecs = %hours%*3600 + %mins%*60 + %secs% 
echo Elapsed time: %hours%:%mins%:%secs%
