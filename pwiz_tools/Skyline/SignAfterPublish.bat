REM Signs .application and .exe.manifest files in publish folder
REM Arguments:
REM Arg 1: KeyLocker key name for signing certificate
set KEY_NAME=%1
REM Arg 2: Full path to signing certificate
set CRT_FILE_PATH=%2
REM Arg 3: Publish folder
set PUBLISH_FOLDER=%3
REM Arg 4: Target name (e.g. Skyline)
set TARGET_NAME=%4

pushd %PUBLISH_FOLDER%

REM Set "CURRENT_VERSION" to the folder found under "Application Files"
FOR /D %%G IN ("Application Files\*") DO SET CURRENT_VERSION=%%G
ECHO Signing .exe, .exe.manifest and .application in %PUBLISH_FOLDER%\"%CURRENT_VERSION%"
pushd "%CURRENT_VERSION%"

rem signtool sign %TARGET_NAME%.exe
echo signtool sign /csp "DigiCert Signing Manager KSP" /kc %KEY_NAME% /f %CRT_FILE_PATH% /tr http://timestamp.digicert.com /td SHA256 /fd SHA256 %TARGET_NAME%.exe
signtool sign /csp "DigiCert Signing Manager KSP" /kc %KEY_NAME% /f %CRT_FILE_PATH% /tr http://timestamp.digicert.com /td SHA256 /fd SHA256 %TARGET_NAME%.exe
if %errorlevel% neq 0 exit /b %errorlevel%

rem mage -update %TARGET_NAME%.exe.manifest
echo mage -update %TARGET_NAME%.exe.manifest -CertFile %CRT_FILE_PATH% -KeyContainer %KEY_NAME% -CryptoProvider "DigiCert Signing Manager KSP" -a sha256RSA -TimestampUri http://timestamp.digicert.com
mage -update %TARGET_NAME%.exe.manifest -CertFile %CRT_FILE_PATH% -KeyContainer %KEY_NAME% -CryptoProvider "DigiCert Signing Manager KSP" -a sha256RSA -TimestampUri http://timestamp.digicert.com
if %errorlevel% neq 0 exit /b %errorlevel%
popd

rem mage -update %TARGET_NAME%.application
echo mage -update %TARGET_NAME%.application -AppManifest "%CURRENT_VERSION%\%TARGET_NAME%.exe.manifest" -CertFile %CRT_FILE_PATH% -KeyContainer %KEY_NAME% -CryptoProvider "DigiCert Signing Manager KSP" -a sha256RSA -TimestampUri http://timestamp.digicert.com
mage -update %TARGET_NAME%.application -AppManifest "%CURRENT_VERSION%\%TARGET_NAME%.exe.manifest" -CertFile %CRT_FILE_PATH% -KeyContainer %KEY_NAME% -CryptoProvider "DigiCert Signing Manager KSP" -a sha256RSA -TimestampUri http://timestamp.digicert.com
if %errorlevel% neq 0 exit /b %errorlevel%
popd
