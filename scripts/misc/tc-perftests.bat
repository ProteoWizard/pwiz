@setlocal ENABLEDELAYEDEXPANSION
@echo off
set SKYLINE_DOWNLOAD_PATH=z:\download

REM Remove C++ build artifacts to free up space
rmdir /s /q build-nt-x86

pwiz_tools\Skyline\bin\x64\Release\TestRunner.exe test=TestTutorial.dll listonly > tests.txt
powershell "Get-Content tests.txt | ForEach-Object { $_.split(\"`t\")[1] }" > tutorialTestNames.txt
pwiz_tools\Skyline\bin\x64\Release\TestRunner.exe test=TestPerf.dll listonly > tests.txt
powershell "Get-Content tests.txt | ForEach-Object { $_.split(\"`t\")[1] }" > perfTestNames.txt

set FailedTests=0
FOR /F %%I IN (tutorialTestNames.txt) DO (
REM check if test is in skip list
findstr /b /e %%I scripts\misc\tc-perftests-skiplist.txt >nul
REM if test is in skiplist, ERRORLEVEL will be 0
IF ERRORLEVEL 1 (pwiz_tools\Skyline\bin\x64\Release\TestRunner.exe test=%%I pass0=on loop=1 language=en perftests=on teamcitytestdecoration=on runsmallmoleculeversions=on showheader=off) ELSE echo Skipped %%I
IF ERRORLEVEL 1 set /a FailedTests += 1
echo Cleaning TestResults
IF EXIST pwiz_tools\Skyline\TestResults rmdir /s /q pwiz_tools\Skyline\TestResults
echo Cleaning downloads
IF EXIST %SKYLINE_DOWNLOAD_PATH% rmdir /s /q %SKYLINE_DOWNLOAD_PATH%
echo Cleaning temp
IF EXIST z:\Temp del /f /s /q z:\Temp\*.* >nul 2>&1
)

FOR /F %%I IN (perfTestNames.txt) DO (
REM check if test is in skip list
findstr /b /e %%I scripts\misc\tc-perftests-skiplist.txt >nul
REM if test is in skiplist, ERRORLEVEL will be 0
IF ERRORLEVEL 1 (pwiz_tools\Skyline\bin\x64\Release\TestRunner.exe test=%%I loop=1 language=en perftests=on teamcitytestdecoration=on runsmallmoleculeversions=on showheader=off) ELSE echo Skipped %%I
IF ERRORLEVEL 1 set /a FailedTests += 1
echo Cleaning TestResults
IF EXIST pwiz_tools\Skyline\TestResults rmdir /s /q pwiz_tools\Skyline\TestResults
echo Cleaning downloads
IF EXIST %SKYLINE_DOWNLOAD_PATH% rmdir /s /q %SKYLINE_DOWNLOAD_PATH%
echo Cleaning temp
IF EXIST z:\Temp del /f /s /q z:\Temp\*.* >nul 2>&1
)

echo %FailedTests% tests failed.
exit /b %FailedTests%