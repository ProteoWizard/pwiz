@echo off
REM TeamCity entry point: build + test OspreySharp with dotCover
REM and TeamCity service messages.  TeamCity invokes this batch
REM directly as the build step; no other configuration needed in the
REM build config except a working directory and the file trigger
REM (pwiz_tools/OspreySharp/**).
REM
REM Pre-requisites on the build agent:
REM   * Visual Studio Build Tools (MSBuild + vstest.console.exe)
REM   * .NET 8 SDK
REM   * JetBrains.dotCover.GlobalTools (dotnet tool install -g)
REM
REM Outputs consumed by TeamCity:
REM   * pwiz_tools/OspreySharp/TestResults/*.trx         (vstest results)
REM   * pwiz_tools/OspreySharp/TestResults/*.dcvr        (dotCover data)
REM Both are imported via service messages emitted by build.ps1.

setlocal
pwsh -NoProfile -File "%~dp0build.ps1" -TeamCity -Coverage -Configuration Release -Framework net8.0
exit /b %ERRORLEVEL%
