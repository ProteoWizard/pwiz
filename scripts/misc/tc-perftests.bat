@echo off
set SKYLINE_DOWNLOAD_PATH=z:\download

REM Remove C++ build artifacts to free up space
rmdir /s /q build-nt-x86

REM Remove Skyline test artifacts
rmdir /s /q pwiz_tools\Skyline\TestResults

pwiz_tools\Skyline\bin\x64\Release\TestRunner.exe test=TestTutorial.dll,TestPerf.dll listonly > tests.txt
powershell "Get-Content tests.txt | ForEach-Object { $_.split(\"`t\")[1] }" > testNames.txt

FOR /F %%I IN (testNames.txt) DO (
dir z:\ | findstr "free"
pwiz_tools\Skyline\bin\x64\Release\TestRunner.exe test=%%I loop=1 language=en perftests=on skip=@scripts\misc\tc-perftests-skiplist.txt teamcitytestdecoration=on runsmallmoleculeversions=on showheader=off
rmdir /s /q pwiz_tools\Skyline\TestResults
rmdir /s /q %SKYLINE_DOWNLOAD_PATH%
)
