@echo off
REM Signs .exe, .exe.manifest and .application in publish folder with a self-signed certificate.
REM This signature only exists to give the ClickOnce deployment a distinct application identity.
REM It provides no security and is not trusted by anything.
REM Arguments:
REM Arg 1: Full path to the self-signed .pfx file
REM Arg 2: Password for the .pfx file
REM Arg 3: Publish folder
REM Arg 4: Target name (e.g. Skyline-daily)
REM Arg 5: Full path to the built .exe.config
setlocal enabledelayedexpansion
set PFX_FILE=%~1
set PFX_PASSWORD=%~2
set PUBLISH_FOLDER=%~3
set TARGET_NAME=%~4
set CONFIG_FILE=%~5

REM Find signtool.exe: prefer the PATH (developer command prompt), otherwise the Windows SDK
set SIGNTOOL=
for /f "delims=" %%A in ('where signtool.exe 2^>nul') do if not defined SIGNTOOL set SIGNTOOL=%%A
if not defined SIGNTOOL for /f "delims=" %%A in ('dir /s /b "%ProgramFiles(x86)%\Windows Kits\10\bin\signtool.exe" 2^>nul ^| findstr /i "\\x64\\"') do set SIGNTOOL=%%A
if not defined SIGNTOOL echo Unable to find signtool.exe & exit /b 1

REM Find mage.exe: prefer the PATH (developer command prompt), otherwise the .NET Framework tools
set MAGE=
for /f "delims=" %%A in ('where mage.exe 2^>nul') do if not defined MAGE set MAGE=%%A
if not defined MAGE for /f "delims=" %%A in ('dir /s /b "%ProgramFiles(x86)%\Microsoft SDKs\Windows\v10.0A\bin\mage.exe" 2^>nul') do set MAGE=%%A
if not defined MAGE echo Unable to find mage.exe & exit /b 1

echo Using signtool: %SIGNTOOL%
echo Using mage: %MAGE%

pushd "%PUBLISH_FOLDER%"

REM Set "CURRENT_VERSION" to the folder found under "Application Files"
FOR /D %%G IN ("Application Files\*") DO SET CURRENT_VERSION=%%G
ECHO Signing .exe, .exe.manifest and .application in %PUBLISH_FOLDER%\"%CURRENT_VERSION%"
pushd "%CURRENT_VERSION%"

REM The ClickOnce publish lists %TARGET_NAME%.exe.config in the application manifest but does not
REM copy it here. Without it mage fails to hash the manifest and the deployed app is missing its
REM binding redirects, so copy it in from the build output.
if not exist "%CONFIG_FILE%" echo Unable to find "%CONFIG_FILE%" & exit /b 1
echo Copying "%CONFIG_FILE%" to "%TARGET_NAME%.exe.config"
copy /y "%CONFIG_FILE%" "%TARGET_NAME%.exe.config" >nul
if %errorlevel% neq 0 exit /b %errorlevel%

echo "%SIGNTOOL%" sign /f "%PFX_FILE%" /p **** /fd SHA256 "%TARGET_NAME%.exe"
"%SIGNTOOL%" sign /f "%PFX_FILE%" /p %PFX_PASSWORD% /fd SHA256 "%TARGET_NAME%.exe"
if %errorlevel% neq 0 exit /b %errorlevel%

echo "%MAGE%" -update "%TARGET_NAME%.exe.manifest" -CertFile "%PFX_FILE%" -Password **** -a sha256RSA
"%MAGE%" -update "%TARGET_NAME%.exe.manifest" -CertFile "%PFX_FILE%" -Password %PFX_PASSWORD% -a sha256RSA
if %errorlevel% neq 0 exit /b %errorlevel%
popd

echo "%MAGE%" -update "%TARGET_NAME%.application" -AppManifest "%CURRENT_VERSION%\%TARGET_NAME%.exe.manifest" -CertFile "%PFX_FILE%" -Password **** -a sha256RSA
"%MAGE%" -update "%TARGET_NAME%.application" -AppManifest "%CURRENT_VERSION%\%TARGET_NAME%.exe.manifest" -CertFile "%PFX_FILE%" -Password %PFX_PASSWORD% -a sha256RSA
if %errorlevel% neq 0 exit /b %errorlevel%
popd
