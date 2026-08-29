@echo off
REM Local dev wrapper: build + test Osprey.  Pass-through args:
REM   build.bat                  -- Release, net10.0, with tests
REM   build.bat -NoTests         -- build only
REM   build.bat --no-tests       -- build only (normalized spelling, same thing)
REM   build.bat -Configuration Debug
REM See build.ps1 for the full parameter list.
REM
REM Requires pwsh (PowerShell 7+) on PATH (project standard, see CLAUDE.md);
REM no fallback to Windows PowerShell 5.1.

setlocal EnableDelayedExpansion

REM # Accept the same two normalized flags Skyline's build.bat takes, so the
REM # top-level b.bat can route to either app without translating per-app:
REM #   --no-tests                        -> -NoTests
REM #   --i-agree-to-the-vendor-licenses  -> -IAgreeToVendorLicenses
REM # Anything else is passed through to build.ps1 untouched.
REM # Capture this before the loop: `shift` shifts %0 as well, so %~dp0 afterwards
REM # points at whatever argument ended up in slot 0, not at this script.
set SCRIPT_DIR=%~dp0
set PSARGS=
:parseargs
if "%~1"=="" goto endparse
if /i "%~1"=="--no-tests" (
    set PSARGS=!PSARGS! -NoTests
) else if /i "%~1"=="--i-agree-to-the-vendor-licenses" (
    REM # Real on this branch: Osprey reads vendor formats through pwiz-sharp, which
    REM # gates them on IAgreeToVendorLicenses. Without it the vendor readers build in
    REM # their no-vendor-support mode and raw files fail at run time.
    set PSARGS=!PSARGS! -IAgreeToVendorLicenses
) else (
    set PSARGS=!PSARGS! %1
)
shift
goto parseargs
:endparse

pwsh -NoProfile -File "!SCRIPT_DIR!build.ps1" !PSARGS!
exit /b %ERRORLEVEL%
