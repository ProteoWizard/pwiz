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
REM   * .NET 8 SDK
REM   * JetBrains.dotCover.GlobalTools (dotnet tool install -g)
REM   The wix v5 tool (for the .msi) is self-provisioned below if absent, so no
REM   manual agent step is needed; this can be replaced by explicit provisioning.
REM
REM Outputs consumed by TeamCity:
REM   * pwiz_tools/Osprey/TestResults/*.trx                  (vstest importData)
REM   * pwiz_tools/Osprey/TestResults/*.dcvr                 (dotCover importData)
REM   * pwiz_tools/Osprey/dist/Osprey-<ver>-win-x64.zip      (publishArtifacts)
REM   * pwiz_tools/Osprey/dist/Osprey-<ver>-win-x64.msi      (publishArtifacts)
REM   The installers are unsigned here; signing is a later CI step. The
REM   publishArtifacts service messages are emitted by package.ps1 -TeamCity, so
REM   no server-side artifact-path configuration is required.

setlocal
REM Make dotnet global tools (wix, etc.) resolvable in this build's environment.
set "PATH=%PATH%;%USERPROFILE%\.dotnet\tools"

pwsh -NoProfile -File "%~dp0build.ps1" -TeamCity -Coverage -Configuration Release -Framework net8.0
if %ERRORLEVEL% NEQ 0 exit /b %ERRORLEVEL%

REM Bootstrap the wix v5 tool + matching v5 UI extension if the agent lacks them
REM (idempotent: first build on a fresh agent installs, later builds skip). WiX v5
REM is the last MS-RL-licensed release; v6+ require the paid OSMF EULA.
where wix >nul 2>nul || dotnet tool install --global wix --version 5.0.2
if %ERRORLEVEL% NEQ 0 exit /b %ERRORLEVEL%
wix extension list -g 2>nul | findstr /i "WixToolset.UI.wixext" >nul || wix extension add -g WixToolset.UI.wixext/5.0.2
if %ERRORLEVEL% NEQ 0 exit /b %ERRORLEVEL%

pwsh -NoProfile -File "%~dp0package.ps1" -TeamCity -Rid win-x64 -Msi
exit /b %ERRORLEVEL%
