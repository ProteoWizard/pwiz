@echo off
REM TeamCity entry point: build + test OspreySharp with dotCover
REM and TeamCity service messages.  TeamCity invokes this batch
REM directly as the build step; no other configuration needed in the
REM build config except a working directory and the file trigger
REM (pwiz_tools/OspreySharp/**).
REM
REM Pre-requisites on the build agent:
REM   * Visual Studio Build Tools (MSBuild + vstest.console.exe)
REM   * .NET 8 SDK
REM   * JetBrains.dotCover.GlobalTools (dotnet tool install -g)
REM
REM Outputs consumed by TeamCity:
REM   * pwiz_tools/OspreySharp/TestResults/*.trx         (vstest results, imported via service message in build.ps1)
REM   * pwiz_tools/OspreySharp/TestResults/*.dcvr        (dotCover data, imported via service message in build.ps1)
REM   * pwiz_tools/OspreySharp/OspreySharp/bin/x64/Release/net8.0  (runnable build, published as artifact below)
REM   * pwiz_tools/OspreySharp/OspreySharp/bin/x64/Release/net472  (net472 build, published as artifact below)

setlocal
pwsh -NoProfile -File "%~dp0build.ps1" -TeamCity -Coverage -Configuration Release -Framework net8.0
set BUILD_EXIT=%ERRORLEVEL%

REM Tell TeamCity to publish artifacts. Emitted regardless of build exit so partial
REM outputs are inspectable on a failed build; missing paths are skipped by the agent.
echo ##teamcity[publishArtifacts 'pwiz_tools/OspreySharp/OspreySharp/bin/x64/Release/net8.0 => OspreySharp-net8.0.zip']
echo ##teamcity[publishArtifacts 'pwiz_tools/OspreySharp/OspreySharp/bin/x64/Release/net472 => OspreySharp-net472.zip']
echo ##teamcity[publishArtifacts 'pwiz_tools/OspreySharp/TestResults/*.trx => test-results']
echo ##teamcity[publishArtifacts 'pwiz_tools/OspreySharp/TestResults/*.dcvr => test-results']

exit /b %BUILD_EXIT%
