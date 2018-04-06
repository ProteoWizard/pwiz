@echo off

REM This script decompiles a given .NET DLL, updates a specific version of an assembly reference to another version, and recompiles the DLL.
REM Edit the perl one-liner to change the source and target versions.

"%VS140COMNTOOLS%vsvars32.bat" || "%VS130COMNTOOLS%vsvars32.bat" || "%VS120COMNTOOLS%vsvars32.bat" || "%VS110COMNTOOLS%vsvars32.bat" || "%VS100COMNTOOLS%vsvars32.bat"

ildasm /typelist /out=%~pn1.il %1 || exit /b
perl -pi.bak -e "s/1:0:97:0/1:0:105:2/g" %~pn1.il || exit /b
move %1 %1.bak || exit /b
ilasm /DLL %~pn1.il || exit /b

del %~pn1.il %~pn1.il.bak %~pn1.res