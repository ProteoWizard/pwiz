@echo off
setlocal
@echo off

REM # Find the location of Jamroot.jam relative to this script.
set PWIZ_ROOT=%~dp0..\..\..\..
call %PWIZ_ROOT%\scripts\test\generate_vendor_mzml.bat Shimadzu %~dp0

REM Our bsdtar does not handle Unicode filenames properly, so they are committed directly to SVN
del %~dp0\Reader_Shimadzu_Test.data.tar.bz2