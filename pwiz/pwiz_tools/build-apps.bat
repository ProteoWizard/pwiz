@echo off
setlocal
@echo off

REM # argument of 32 or 64 sets target platform (default 32)
set TARGETPLATFORM=32
set ARGS=
set TARGETS=
set REGISTER=
set OPTIMIZATION=space
set NOLOG=

set ALL_ARGS= %*

if "%ALL_ARGS: 32=%" neq "%ALL_ARGS%" (
    set TARGETPLATFORM=32
    set ALL_ARGS=%ALL_ARGS: 32=%
)
if "%ALL_ARGS: 64=%" neq "%ALL_ARGS%" (
    set TARGETPLATFORM=64
    set ALL_ARGS=%ALL_ARGS: 64=%
)
if "%ALL_ARGS: register=%" neq "%ALL_ARGS%" (
    set REGISTER=1
    set ALL_ARGS=%ALL_ARGS: register=%
)
if "%ALL_ARGS: REGISTER=%" neq "%ALL_ARGS%" (
    set REGISTER=1
    set ALL_ARGS=%ALL_ARGS: REGISTER=%
)
if "%ALL_ARGS: debug=%" neq "%ALL_ARGS%" (
    set OPTIMIZATION=off
)
if "%ALL_ARGS: DEBUG=%" neq "%ALL_ARGS%" (
    set OPTIMIZATION=off
)
if "%ALL_ARGS: nolog=%" neq "%ALL_ARGS%" (
    set NOLOG=1
    set ALL_ARGS=%ALL_ARGS: nolog=%
)
if "%ALL_ARGS: NOLOG=%" neq "%ALL_ARGS%" (
    set NOLOG=1
    set ALL_ARGS=%ALL_ARGS: NOLOG=%
)

REM # quickbuild.bat should be in the current directory or parent directory
IF EXIST "%CD%\quickbuild.bat" (
    set QUICKBUILD="%CD%\quickbuild.bat"
    set REG_CONTROLS="%CD%\pwiz_tools\reg-controls.bat"
) else IF EXIST "%CD%\..\quickbuild.bat" (
    set QUICKBUILD="%CD%\..\quickbuild.bat"
    set REG_CONTROLS="%CD%\regcontrols.bat"
) else (
    echo Can't find quickbuild.bat.  Call build-apps.bat from pwiz or pwiz_tools directory.
    exit /b 1
)

REM # register correct MSFileReader
if "%REGISTER%"=="1" (
    call %REG_CONTROLS% %TARGETPLATFORM%
) else (
    echo.
    echo Skipping MSFileReader registration
    echo.
)

if %ERRORLEVEL% NEQ 0 exit /b 1

echo Building %TARGETPLATFORM%-bit %ALL_ARGS%
echo.
echo %QUICKBUILD% -j%NUMBER_OF_PROCESSORS% --hash --without-compassxtract optimization=%OPTIMIZATION% secure-scl=off address-model=%TARGETPLATFORM% %ALL_ARGS%

if "%NOLOG%"=="1" (
    call %QUICKBUILD% -j%NUMBER_OF_PROCESSORS% --hash --without-compassxtract optimization=%OPTIMIZATION% secure-scl=off address-model=%TARGETPLATFORM% %ALL_ARGS%
    GOTO BUILD_DONE
)

REM # Do full build of ProteoWizard, passing quickbuild's arguments to bjam
set QUICKBUILDLOG=%CD%\build%TARGETPLATFORM%.log
echo Build output: %QUICKBUILDLOG%

REM # build!
call %QUICKBUILD% -j%NUMBER_OF_PROCESSORS% --hash --without-compassxtract optimization=%OPTIMIZATION% secure-scl=off address-model=%TARGETPLATFORM% %ALL_ARGS% >%QUICKBUILDLOG% 2>&1

REM # look for problems
findstr /c:"...updated" %QUICKBUILDLOG%
findstr /c:"...skipped" %QUICKBUILDLOG%
findstr /c:"...failed" %QUICKBUILDLOG%
findstr /e "FAILED:" %QUICKBUILDLOG%
echo.
findstr /c:"Could not resolve reference" %QUICKBUILDLOG%
findstr /b /c:"Unable to load" %QUICKBUILDLOG%
findstr /b /c:"error:" %QUICKBUILDLOG%
findstr /c:"test(s) Passed" %QUICKBUILDLOG%

:BUILD_DONE
echo.
echo Build done.
echo.
