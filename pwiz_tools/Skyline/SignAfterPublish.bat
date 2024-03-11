REM Signs .application and .exe.manifest files in publish folder
REM Arguments:
REM Arg 1: Full path to signing certificate
REM Arg 2: Publish folder
REM Arg 3: Target name (e.g. Skyline)
pushd %2
REM Set "CURRENT_VERSION" to the folder found under "Application Files"
FOR /D %%G IN ("Application Files\*") DO SET CURRENT_VERSION=%%G
ECHO Signing .exe, .exe.manifest and .application in %2\"%CURRENT_VERSION%"
pushd "%CURRENT_VERSION%"
rem signtool sign /tr http://timestamp.digicert.com /v /f %1 /p <pwd> %3.exe
echo signtool sign /csp "DigiCert Signing Manager KSP" /kc key_637015839 /f %1 /tr http://timestamp.digicert.com /td SHA256 /fd SHA256 %3.exe
signtool sign /csp "DigiCert Signing Manager KSP" /kc key_637015839 /f %1 /tr http://timestamp.digicert.com /td SHA256 /fd SHA256 %3.exe
if %errorlevel% neq 0 exit /b %errorlevel%
rem mage -update %3.exe.manifest -CertFile %1 -pwd <pwd>
echo mage -update %3.exe.manifest -CertFile %1 -KeyContainer key_637015839 -CryptoProvider "DigiCert Signing Manager KSP" -a sha256RSA -TimestampUri http://timestamp.digicert.com
mage -update %3.exe.manifest -CertFile %1 -KeyContainer key_637015839 -CryptoProvider "DigiCert Signing Manager KSP" -a sha256RSA -TimestampUri http://timestamp.digicert.com
if %errorlevel% neq 0 exit /b %errorlevel%
popd
ECHO Signing .application in root of publish folder
rem mage -update %3.application -AppManifest "%CURRENT_VERSION%\%3.exe.manifest" -CertFile %2 -pwd %1
echo mage -update %3.application -AppManifest "%CURRENT_VERSION%\%3.exe.manifest" -CertFile %1 -KeyContainer key_637015839 -CryptoProvider "DigiCert Signing Manager KSP" -a sha256RSA -TimestampUri http://timestamp.digicert.com
mage -update %3.application -AppManifest "%CURRENT_VERSION%\%3.exe.manifest" -CertFile %1 -KeyContainer key_637015839 -CryptoProvider "DigiCert Signing Manager KSP" -a sha256RSA -TimestampUri http://timestamp.digicert.com
if %errorlevel% neq 0 exit /b %errorlevel%
popd
