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
REM   * runs EVERY mode: no -Skip* switch is passed here, so this config runs
REM     whatever regression.ps1 defines. Today that is straight-through vs the
REM     committed golden (mode 1), the FDR-calibration spot checks on the datasets
REM     carrying model diagnostics (mode 1b), resume vs straight-through (mode 2),
REM     the HPC 4-task chain vs straight-through (mode 3), the warm all-cached
REM     re-run (mode 4), the Stage-5 rehydrate (mode 5) and library-fragment
REM     release engagement (mode 6) - all at 1e-9; buildProblem on any mismatch.
REM     Do NOT maintain that list here. It belongs to regression.ps1, and this
REM     comment named only 1/1b/2/3 for months after modes 4-6 were added, which
REM     is how "TeamCity runs mode1/2/3" got repeated as fact.
REM
REM Outputs consumed by TeamCity (emitted via service messages in regression.ps1):
REM   * ##teamcity[buildProblem ...]                          (on any failure)
REM   No artifacts are published: the run deletes its own scratch as it goes so a
REM   shared agent is not starved, and a red gate's diagnosis lives in the build log.

REM Runs the suite as two concurrent lanes (Astral in one, the three Stellar
REM variants in the other). Same datasets, same modes, nothing skipped - only the
REM scheduling changes. regression-parallel.ps1 sizes threads per lane from the
REM agent's logical processor count, so this is correct on a 16-logical agent and
REM on a 32-logical dev box without a per-machine setting here.
setlocal
pwsh -NoProfile -File "%~dp0regression-parallel.ps1" -TeamCity -Dataset All
exit /b %ERRORLEVEL%
