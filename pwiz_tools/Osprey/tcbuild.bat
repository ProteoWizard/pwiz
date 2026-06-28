@echo off
REM TeamCity entry point: build + test Osprey with dotCover
REM and TeamCity service messages.  TeamCity invokes this batch
REM directly as the build step; smart-build trigger wiring for
REM pwiz_tools/Osprey/** is in scripts/misc/vcs_trigger_and_paths_config.py.
REM
REM Pre-requisites on the build agent:
REM   * pwsh (PowerShell 7+) on PATH (project standard; no powershell.exe fallback)
REM   * Visual Studio Build Tools (MSBuild + vstest.console.exe)
REM   * .NET 8 SDK
REM   * JetBrains.dotCover.GlobalTools (dotnet tool install -g)
REM
REM Outputs consumed by TeamCity (all emitted via service messages in build.ps1):
REM   * pwiz_tools/Osprey/TestResults/*.trx                  (vstest importData)
REM   * pwiz_tools/Osprey/TestResults/*.dcvr                 (dotCover importData)
REM   * pwiz_tools/Osprey/Osprey/bin/x64/Release/net8.0 (publishArtifacts)
REM   * pwiz_tools/Osprey/Osprey/bin/x64/Release/net472 (publishArtifacts)
REM   * pwiz_tools/Osprey/TestResults                        (publishArtifacts)

setlocal
pwsh -NoProfile -File "%~dp0build.ps1" -TeamCity -Coverage -Configuration Release -Framework net8.0
exit /b %ERRORLEVEL%
