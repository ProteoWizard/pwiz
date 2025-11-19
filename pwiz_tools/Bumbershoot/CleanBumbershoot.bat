@echo off
setlocal
@echo off

REM # Get the location of this script and drop trailing slash
set SCRIPT_ROOT=%~dp0
set SCRIPT_ROOT=%SCRIPT_ROOT:~0,-1%
pushd %SCRIPT_ROOT%

FOR /r %%I IN (*Version.cpp) DO del "%%I"
IF EXIST idpicker\build-nt-x86 rmdir /s /q idpicker\build-nt-x86
IF EXIST idpicker\packages rmdir /s /q idpicker\packages

IF EXIST greazy\GUI\Properties\AssemblyInfo.cs del /q greazy\GUI\Properties\AssemblyInfo.cs
IF EXIST idpicker\Controls\Properties\AssemblyInfo.cs del /q idpicker\Controls\Properties\AssemblyInfo.cs
IF EXIST idpicker\CustomDataSourceDialog\Properties\AssemblyInfo.cs del /q idpicker\CustomDataSourceDialog\Properties\AssemblyInfo.cs
IF EXIST idpicker\Model\Properties\AssemblyInfo.cs del /q idpicker\Model\Properties\AssemblyInfo.cs
IF EXIST idpicker\Qonverter\CLI\AssemblyInfo.cpp del /q idpicker\Qonverter\CLI\AssemblyInfo.cpp
IF EXIST idpicker\Util\Properties\AssemblyInfo.cs del /q idpicker\Util\Properties\AssemblyInfo.cs
IF EXIST idpicker\Properties\AssemblyInfo.cs del /q idpicker\Properties\AssemblyInfo.cs
IF EXIST idpicker\Resources\Resources.rc del /q idpicker\Resources\Resources.rc
IF EXIST idpicker\Resources\Resources.res del /q idpicker\Resources\Resources.res

popd