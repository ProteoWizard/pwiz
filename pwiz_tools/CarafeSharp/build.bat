@echo off
rem Build and test CarafeSharp on Windows. Arguments pass through to build.ps1, for example:
rem   build.bat                     CPU build and tests
rem   build.bat -Torch cuda         CUDA build and the Cuda test category
rem   build.bat -NoTests            build only
pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
exit /b %ERRORLEVEL%
