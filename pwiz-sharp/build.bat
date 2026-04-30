@echo off
setlocal

REM # ------------------------------------------------------------------------
REM # pwiz-sharp build entry point. TeamCity calls this from the
REM # ProteoWizard_CoreWindowsNet config; runs locally too.
REM # ------------------------------------------------------------------------

REM # Resolve to the directory this script lives in so we work from pwiz-sharp/
set SCRIPT_DIR=%~dp0
set SCRIPT_DIR=%SCRIPT_DIR:~0,-1%
pushd "%SCRIPT_DIR%"

set EXIT=0
set ALL_ARGS=%*
set CONFIG=Release

REM # Allow `build.bat Debug` (or any other config) as an override.
if not "%~1"=="" set CONFIG=%~1

set ERROR_TEXT=

echo ##teamcity[progressMessage 'dotnet --version']
dotnet --version
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 set ERROR_TEXT=dotnet not on PATH & goto error

echo ##teamcity[progressMessage 'dotnet restore']
dotnet restore Pwiz.slnx
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 set ERROR_TEXT=dotnet restore failed & goto error

echo ##teamcity[progressMessage 'dotnet build (%CONFIG%)']
dotnet build Pwiz.slnx --no-restore -c %CONFIG% -nologo
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 set ERROR_TEXT=dotnet build failed & goto error

echo ##teamcity[progressMessage 'dotnet test (%CONFIG%)']
REM # --logger trx writes test results files that TeamCity's test-results step picks up;
REM # the dotnet test runner auto-emits TeamCity service-message decoration when
REM # TEAMCITY_PROJECT_NAME is set in the environment.
dotnet test Pwiz.slnx --no-build -c %CONFIG% --logger:"trx" --logger:"console;verbosity=normal"
set EXIT=%ERRORLEVEL%
if %EXIT% NEQ 0 set ERROR_TEXT=dotnet test failed & goto error

popd
exit /b 0

:error
echo ##teamcity[message text='%ERROR_TEXT%' status='ERROR']
popd
exit /b %EXIT%
