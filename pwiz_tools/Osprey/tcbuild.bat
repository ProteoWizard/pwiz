@echo off
REM TeamCity entry point: build + test Osprey with dotCover and TeamCity service
REM messages, then package the unsigned redistributable installers so they are
REM published as artifacts of this per-commit config (the Tier-C "testing"
REM installers a collaborator can grab before an official release). TeamCity
REM invokes this batch directly as the build step; smart-build trigger wiring for
REM pwiz_tools/Osprey/** is in scripts/misc/vcs_trigger_and_paths_config.py.
REM
REM Pre-requisites on the build agent:
REM   * pwsh (PowerShell 7+) on PATH (project standard; no powershell.exe fallback)
REM   * Visual Studio Build Tools (MSBuild + vstest.console.exe)
REM   * .NET 10 SDK
REM   (dotCover is restored from .config\dotnet-tools.json by the build, so it
REM   needs no agent provisioning.)
REM   Inno Setup 6 (for the Setup.exe) is self-provisioned by package.ps1 through
REM   pwiz-sharp/installer/Ensure-InnoSetup.ps1 if absent, so no manual agent step
REM   is needed.
REM
REM Outputs consumed by TeamCity:
REM   * pwiz_tools/Osprey/TestResults/*.trx                  (vstest importData)
REM   * pwiz_tools/Osprey/TestResults/*.dcvr                 (dotCover importData)
REM   * pwiz_tools/Osprey/dist/Osprey-<ver>-win-x64.zip      (publishArtifacts)
REM   * pwiz_tools/Osprey/dist/Osprey-Setup-<ver>.exe         (publishArtifacts)
REM   The installers are unsigned here; signing is a later CI step. The
REM   publishArtifacts service messages are emitted by package.ps1 -TeamCity, so
REM   no server-side artifact-path configuration is required.

setlocal

pwsh -NoProfile -File "%~dp0build.ps1" -TeamCity -Coverage -Configuration Release
if %ERRORLEVEL% NEQ 0 exit /b %ERRORLEVEL%

pwsh -NoProfile -File "%~dp0package.ps1" -TeamCity -Rid win-x64 -Setup
exit /b %ERRORLEVEL%
