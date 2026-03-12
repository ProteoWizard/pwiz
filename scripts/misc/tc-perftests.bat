@setlocal ENABLEDELAYEDEXPANSION
@echo off
IF DEFINED TEAMCITY_VERSION (
  IF "%USERNAME%" neq "maccoss-teamcity" set SKYLINE_DOWNLOAD_PATH=z:\download
  IF "%USERNAME%" equ "maccoss-teamcity" set SKYLINE_DOWNLOAD_FROM_S3=0
  set SKYLINE_TEMP_PATH=z:\Temp
)

REM Remove C++ build artifacts to free up space
rmdir /s /q build-nt-x86

pwiz_tools\Skyline\bin\x64\Release\TestRunner.exe test=TestTutorial.dll listonly > tests.txt
powershell "Get-Content tests.txt | ForEach-Object { $_.split(\"`t\")[1] }" > tutorialTestNames.txt
pwiz_tools\Skyline\bin\x64\Release\TestRunner.exe test=TestPerf.dll listonly > tests.txt
powershell "Get-Content tests.txt | ForEach-Object { $_.split(\"`t\")[1] }" > perfTestNames.txt

set FailedTests=0
set Aborted=0
FOR /F %%I IN (tutorialTestNames.txt) DO (
IF !Aborted! equ 1 goto :doneTutorials
REM check if test is in skip list
findstr /b /e %%I scripts\misc\tc-perftests-skiplist.txt >nul
REM if test is in skiplist, ERRORLEVEL will be 0
IF ERRORLEVEL 1 (pwiz_tools\Skyline\bin\x64\Release\TestRunner.exe test=%%I pass0=on teamcitytestsuite=TestTutorial loop=1 language=en perftests=on teamcitytestdecoration=on runsmallmoleculeversions=on showheader=off pause=-4) ELSE echo Skipped %%I
IF !ERRORLEVEL! equ -1073741510 set Aborted=1
IF !Aborted! equ 1 goto :doneTutorials
IF ERRORLEVEL 1 set /a FailedTests += 1
call :cleanup
)
:doneTutorials

IF !Aborted! equ 1 goto :done
FOR /F %%I IN (perfTestNames.txt) DO (
IF !Aborted! equ 1 goto :donePerf
REM check if test is in skip list
findstr /b /e %%I scripts\misc\tc-perftests-skiplist.txt >nul
REM if test is in skiplist, ERRORLEVEL will be 0
IF ERRORLEVEL 1 (pwiz_tools\Skyline\bin\x64\Release\TestRunner.exe test=%%I teamcitytestsuite=TestPerf loop=1 language=en perftests=on teamcitytestdecoration=on runsmallmoleculeversions=on showheader=off pause=-4) ELSE echo Skipped %%I
IF !ERRORLEVEL! equ -1073741510 set Aborted=1
IF !Aborted! equ 1 goto :donePerf
IF ERRORLEVEL 1 set /a FailedTests += 1
call :cleanup
)
:donePerf

:done
IF !Aborted! equ 1 (
  echo Aborted by user.
  exit /b 1
)
echo %FailedTests% tests failed.
exit /b %FailedTests%

:cleanup
IF "%USERNAME%" neq "maccoss-teamcity" (
  IF DEFINED SKYLINE_DOWNLOAD_PATH (
    echo Cleaning downloads
    IF EXIST %SKYLINE_DOWNLOAD_PATH% rmdir /s /q %SKYLINE_DOWNLOAD_PATH%
  )
)
IF DEFINED SKYLINE_TEMP_PATH (
  echo Cleaning temp
  IF EXIST %SKYLINE_TEMP_PATH% del /f /s /q %SKYLINE_TEMP_PATH%\*.* >nul 2>&1
)
goto :eof
