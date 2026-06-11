@echo off
REM Local dev wrapper: build + test OspreySharp.  Pass-through args:
REM   build.bat                  -- Release, net8.0, with tests
REM   build.bat -Framework both  -- run tests on net472 AND net8.0
REM   build.bat -NoTests         -- build only
REM   build.bat -Configuration Debug
REM See build.ps1 for the full parameter list.
REM
REM Requires pwsh (PowerShell 7+) on PATH (project standard, see CLAUDE.md);
REM no fallback to Windows PowerShell 5.1.

setlocal
pwsh -NoProfile -File "%~dp0build.ps1" %*
exit /b %ERRORLEVEL%
