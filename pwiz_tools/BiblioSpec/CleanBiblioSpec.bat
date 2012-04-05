@echo off
setlocal
@echo off

REM # Get the location of quickbuild.bat and drop trailing slash
set PWIZ_ROOT=%~dp0
set PWIZ_ROOT=%PWIZ_ROOT:~0,-1%
pushd %PWIZ_ROOT%

IF EXIST tests\output del /q tests\output\*
IF EXIST tests\inputs rmdir /s /q tests\inputs

IF EXIST %PWIZ_ROOT%..\..\build-nt-x86 rmdir /s /q %PWIZ_ROOT%..\..\build-nt-x86\pwiz_tools\BiblioSpec


popd
