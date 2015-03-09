REM Signs .application and .exe.manifest files in publish folder
REM Arguments:
REM Arg 1: Full path to signing certificate
REM Arg 2: Signing certificate password
REM Arg 3: Publish folder
REM Arg 4: Target name (e.g. Skyline-daily)
CALL "%VS120COMNTOOLS%vsvars32.bat"
pushd %3
REM Set "CURRENT_VERSION" to the folder found under "Application Files"
FOR /D %%G IN ("Application Files\*") DO SET CURRENT_VERSION=%%G
ECHO Signing .exe, .exe.manifest and .application in %3\"%CURRENT_VERSION%"
pushd "%CURRENT_VERSION%"
signtool sign /t http://timestamp.verisign.com/scripts/timstamp.dll /v /f %1 /p %2 %4.exe
mage -update %4.exe.manifest -CertFile %1 -pwd %2
popd
ECHO Signing .application in root of publish folder
mage -update %4.application -AppManifest "%CURRENT_VERSION%\%4.exe.manifest" -CertFile %1 -pwd %2
popd
