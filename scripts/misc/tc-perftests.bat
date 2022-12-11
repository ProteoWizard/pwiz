@setlocal ENABLEDELAYEDEXPANSION
@echo off
set SKYLINE_DOWNLOAD_PATH=z:\download

REM Remove C++ build artifacts to free up space
rmdir /s /q build-nt-x86

pwiz_tools\Skyline\bin\x64\Release\TestRunner.exe test=TestTutorial.dll,TestPerf.dll listonly > tests.txt
powershell "Get-Content tests.txt | ForEach-Object { $_.split(\"`t\")[1] }" > testNames.txt

set FailedTests=0
FOR /F %%I IN (testNames.txt) DO (
REM check if test is in skip list
findstr /b %%I scripts\misc\tc-perftests-skiplist.txt >nul
REM if test is in skiplist, ERRORLEVEL will be 0
IF ERRORLEVEL 1 (pwiz_tools\Skyline\bin\x64\Release\TestRunner.exe test=%%I loop=1 language=en perftests=on teamcitytestdecoration=on runsmallmoleculeversions=on showheader=off) ELSE echo Skipped %%I
IF ERRORLEVEL 1 set /a FailedTests += 1
IF EXIST pwiz_tools\Skyline\TestResults rmdir /s /q pwiz_tools\Skyline\TestResults
IF EXIST %SKYLINE_DOWNLOAD_PATH% rmdir /s /q %SKYLINE_DOWNLOAD_PATH%
IF EXIST z:\Temp del /f /s /q z:\Temp\*.* >nul
)

echo %FailedTests% tests failed.
exit /b %FailedTests%