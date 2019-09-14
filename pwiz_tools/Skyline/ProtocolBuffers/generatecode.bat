@echo off
REM This batch file executes the Protocol Compiler on all of the .proto files in this directory
REM The output is first sent to the directory "tmp"
REM Then, only the files in "tmp" which are different than what is already in "GeneratedCode" get
REM copied over.  This is to prevent Visual Studio from seeing that those files have changed and
REM forcing Visual Studio to build more files.

REM Syntax used in this batch file:
REM "~dp0": directory containing the executing batch file
REM "%%~nF%%~xF": filename and extension for the variable "F".
REM "fc.exe": compares two files. Error level will be greater than 0 if files are different or missing 

pushd %~dp0
if not exist tmp mkdir tmp
for /f %%i in ('dir /B *.proto') do protoc %%i --csharp_out=tmp --plugin=protoc-gen-grpc=grpc_csharp_plugin.exe --grpc_out=tmp
FOR /R tmp %%F in (*.cs) DO (
	fc.exe tmp\%%~nF%%~xF GeneratedCode\%%~nF%%~xF > nul 2> nul
	if ERRORLEVEL 1 COPY tmp\%%~nF%%~xF GeneratedCode\%%~nF%%~xF
)
popd