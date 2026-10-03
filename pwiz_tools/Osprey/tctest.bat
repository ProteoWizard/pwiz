@echo off
REM Scheduled TeamCity entry point: Osprey overnight end-to-end regression.
REM TeamCity invokes this batch directly as the build step of the
REM "Osprey Windows .NET Regression" config (schedule-triggered, separate from
REM the per-commit ProteoWizard_OspreyWindowsNet smart-trigger config).
REM
REM Pre-requisites on the build agent (same as tcbuild.bat, plus internet):
REM   * pwsh (PowerShell 7+) on PATH (project standard; no powershell.exe fallback)
REM   * Visual Studio Build Tools (MSBuild + vstest.console.exe)
REM   * .NET 10 SDK
REM   * Outbound HTTPS to panoramaweb.org (downloads the mzML test-data zip into
REM     the shared <Downloads>\Perftests folder on first run; skipped if present)
REM
REM What it does (see regression.ps1 for the full design):
REM   * downloads + extracts osprey-testfiles-mzML-v2.zip (skip-if-present)
REM   * runs all four datasets straight-through, no input copies, output under
REM     pwiz_tools/Osprey/TestResults/regression-<date>: Stellar and Astral
REM     (generated decoys), StellarLibDecoy (library-supplied decoys) and
REM     StellarGenDecoyEntrap (generated decoys measured against entrapment)
REM   * passes no -Skip* switch, so this config runs whatever regression.ps1
REM     defines for each dataset - which is NOT every mode on every dataset: each
REM     dataset's SkipModes cuts the legs whose property another dataset already
REM     covers. Do NOT maintain that list here, and do not read a missing leg as a
REM     defect; pwiz_tools/Osprey/regression.html is the generated map of which
REM     assertion runs where. This comment named only 1/1b/2/3 for months after
REM     modes 4-6 were added, which is how "TeamCity runs mode1/2/3" became fact.
REM
REM Outputs consumed by TeamCity (emitted via service messages in regression.ps1):
REM   * ##teamcity[buildProblem ...]                          (on any failure)
REM   No artifacts are published: the run deletes its own scratch as it goes so a
REM   shared agent is not starved, and a red gate's diagnosis lives in the build log.

REM Runs the suite as two concurrent lanes (Astral + StellarGenDecoyEntrap in one,
REM Stellar + StellarLibDecoy in the other), balanced on measured per-leg seconds.
REM regression-parallel.ps1 sizes threads per lane from the agent's logical
REM processor count, so this is correct on a 16-logical agent and on a 32-logical
REM dev box without a per-machine setting here.
setlocal
pwsh -NoProfile -File "%~dp0regression-parallel.ps1" -TeamCity -Dataset All
exit /b %ERRORLEVEL%
