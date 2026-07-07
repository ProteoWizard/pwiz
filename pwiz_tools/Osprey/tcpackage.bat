@echo off
REM TeamCity entry point: build the redistributable Osprey artifacts
REM (per-RID self-contained ZIPs + the win-x64 .msi) and publish them.
REM
REM Intended for a dedicated packaging build config (trigger on master and/or
REM release tags), SEPARATE from the per-commit ProteoWizard_OspreyWindowsNet
REM build so day-to-day commits are not slowed by a full self-contained publish.
REM package.ps1 emits ##teamcity[publishArtifacts ...] for each artifact, so no
REM server-side artifact-path configuration is required -- only a build config
REM that runs this batch on an agent with the prerequisites below.
REM
REM Pre-requisites on the build agent (in addition to tcbuild.bat's):
REM   * .NET 8 runtime packs (restored automatically by `dotnet publish`;
REM     needs outbound NuGet on first run)
REM   * The wix v5 dotnet tool + the matching v5 UI extension for the .msi:
REM       dotnet tool install --global wix --version 5.0.2
REM       wix extension add -g WixToolset.UI.wixext/5.0.2
REM     WiX v5 is the last release under the free MS-RL license; v6+ require the
REM     paid Open Source Maintenance Fee EULA.
REM
REM Artifacts (published via service messages from package.ps1):
REM   * pwiz_tools/Osprey/dist/Osprey-<version>-win-x64.zip
REM   * pwiz_tools/Osprey/dist/Osprey-<version>-linux-x64.zip
REM   * pwiz_tools/Osprey/dist/Osprey-<version>-win-x64.msi

setlocal
pwsh -NoProfile -File "%~dp0package.ps1" -TeamCity -Msi
exit /b %ERRORLEVEL%
