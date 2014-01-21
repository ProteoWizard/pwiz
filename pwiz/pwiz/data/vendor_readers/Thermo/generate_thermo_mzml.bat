@echo off
setlocal
@echo off

REM # Find the location of Jamroot.jam relative to this script.
set PWIZ_ROOT=%~dp0..\..\..\..
call %PWIZ_ROOT%\scripts\test\generate_vendor_mzml.bat Thermo %~dp0

