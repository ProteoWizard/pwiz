@echo off
REM Scheduled TeamCity entry point: OspreySharp overnight end-to-end regression.
REM TeamCity invokes this batch directly as the build step of the
REM "Osprey Windows .NET Regression" config (schedule-triggered, separate from
REM the per-commit ProteoWizard_OspreyWindowsNet smart-trigger config).
REM
REM Pre-requisites on the build agent (same as tcbuild.bat, plus internet):
REM   * pwsh (PowerShell 7+) on PATH (project standard; no powershell.exe fallback)
REM   * Visual Studio Build Tools (MSBuild + vstest.console.exe)
REM   * .NET 8 SDK
REM   * Outbound HTTPS to panoramaweb.org (downloads the mzML test-data zip into
REM     the shared <Downloads>\Perftests folder on first run; skipped if present)
REM
REM What it does (see regression.ps1 for the full design):
REM   * downloads + extracts osprey-testfiles-mzML.zip (skip-if-present)
REM   * runs Stellar + Astral straight-through, no input copies, output under
REM     pwiz_tools/OspreySharp/TestResults/regression-<date>
REM   * asserts straight-through vs committed golden (mode 1) and resume vs
REM     straight-through (mode 2), both at 1e-9; buildProblem on any mismatch
REM
REM Outputs consumed by TeamCity (emitted via service messages in regression.ps1):
REM   * pwiz_tools/OspreySharp/TestResults/regression-<date>  (publishArtifacts)
REM   * ##teamcity[buildProblem ...]                          (on any failure)

setlocal
pwsh -NoProfile -File "%~dp0regression.ps1" -TeamCity -Dataset All
exit /b %ERRORLEVEL%
