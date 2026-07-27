@echo off
REM Scheduled TeamCity entry point: Osprey overnight end-to-end regression.
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
REM   * downloads + extracts osprey-testfiles-mzML-v2.zip (skip-if-present)
REM   * runs all four datasets straight-through, no input copies, output under
REM     pwiz_tools/Osprey/TestResults/regression-<date>: Stellar and Astral
REM     (generated decoys), StellarLibDecoy (library-supplied decoys) and
REM     StellarGenDecoyEntrap (generated decoys measured against entrapment)
REM   * asserts straight-through vs committed golden (mode 1), the FDR-calibration
REM     spot checks on the datasets carrying model diagnostics (mode 1b), the HPC
REM     4-task chain vs straight-through (mode 3) and resume vs straight-through
REM     (mode 2), all at 1e-9; buildProblem on any mismatch
REM
REM Outputs consumed by TeamCity (emitted via service messages in regression.ps1):
REM   * ##teamcity[buildProblem ...]                          (on any failure)
REM   No artifacts are published: the run deletes its own scratch as it goes so a
REM   shared agent is not starved, and a red gate's diagnosis lives in the build log.

setlocal
pwsh -NoProfile -File "%~dp0regression.ps1" -TeamCity -Dataset All
exit /b %ERRORLEVEL%
