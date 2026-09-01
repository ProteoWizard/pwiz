@echo off
REM Local dev wrapper: build + test Osprey.  Pass-through args:
REM   build.bat                  -- Release, net10.0, with tests
REM   build.bat -Framework both  -- run tests on net472 AND net10.0
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
REM #   --i-agree-to-the-vendor-licenses  -> see the warning below
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
    REM # Deliberately a no-op ON THIS BRANCH, and it says so rather than pretending.
    REM # Osprey has no managed vendor path here: Osprey.csproj references neither
    REM # ProteowizardWrapper nor any pwiz-sharp vendor project, so its vendor raw
    REM # reading comes only from the bjam route (Jamfile.jam:101 builds the net472
    REM # wrapper and stages pwiz_data_cli next to Osprey.exe).
    REM #
    REM # The #4497 branch replaces that outright: Osprey gets full pwiz support
    REM # through the pwiz-sharp .NET 8 implementation and drops bjam entirely. That
    REM # became possible when ProteowizardWrapper went plain net8.0 in the
    REM # CommonUtil/CommonBaseUI split. Once it lands, this flag starts meaning
    REM # what it says and this branch goes away.
    echo WARNING: --i-agree-to-the-vendor-licenses has no effect on this build.
    echo          Osprey has no managed vendor readers on this branch. Use the bjam
    echo          route if you need them, or the #4497 branch once it lands.
) else (
    set PSARGS=!PSARGS! %1
)
shift
goto parseargs
:endparse

pwsh -NoProfile -File "!SCRIPT_DIR!build.ps1" !PSARGS!
exit /b %ERRORLEVEL%
