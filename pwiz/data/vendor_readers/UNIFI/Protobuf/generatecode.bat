@echo off
REM Regenerates Google.Protobuf C# code from the .proto files in this directory.
REM Mirrors pwiz_tools/Skyline/ProtocolBuffers/generatecode.bat: reuse Skyline's
REM checked-in protoc.exe 3.8.0 so the generated C# stays compatible with the matching
REM Google.Protobuf.dll 3.7.0 in pwiz_tools/Shared/Lib.
REM
REM Run by hand after editing unifi.proto or waters_connect.proto. Output lands in
REM GeneratedCode/ and is committed alongside the .proto sources.

setlocal
pushd %~dp0

set PROTOC=%~dp0..\..\..\..\..\pwiz_tools\Skyline\ProtocolBuffers\protoc.exe
if not exist "%PROTOC%" goto :missing_protoc

if not exist tmp mkdir tmp
if not exist GeneratedCode mkdir GeneratedCode

for /f %%i in ('dir /B *.proto') do "%PROTOC%" %%i --csharp_out=tmp

REM Only copy files that actually changed so generated-code timestamps don't churn.
for /R tmp %%F in (*.cs) do (
    fc.exe tmp\%%~nF%%~xF GeneratedCode\%%~nF%%~xF > nul 2> nul
    if errorlevel 1 copy /y tmp\%%~nF%%~xF GeneratedCode\%%~nF%%~xF
)

rd /s /q tmp
popd
endlocal
goto :eof

:missing_protoc
echo ERROR: protoc.exe not found at %PROTOC%
exit /b 1
