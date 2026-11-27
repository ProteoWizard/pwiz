@echo off
REM Local dev wrapper: build + test Quasar.  Arguments pass through to build.ps1:
REM   build.bat                          -- Release, with tests
REM   build.bat -NoTests                 -- build only
REM   build.bat -Configuration Debug
REM Requires pwsh (PowerShell 7+) on PATH.
pwsh -NoProfile -File "%~dp0build.ps1" %*
exit /b %ERRORLEVEL%
