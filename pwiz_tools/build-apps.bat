@echo off
setlocal
@echo off

REM # argument of 32 or 64 sets target platform (default 32)
set TARGETPLATFORM=32
set ARGS=
set TARGETS=
set REGISTER=

:setArgs
if "%1"=="" goto doneSetArgs
set A=%1
if "%A%"=="32" (
    set TARGETPLATFORM=32
) else if "%A%"=="64" (
    set TARGETPLATFORM=64
) else if "%A:~0,1%"=="-" (
    set ARGS=%ARGS% %A%
) else if /I "%A%"=="register" (
    set REGISTER=1
) else (
    set TARGETS=%TARGETS% %A%
)
shift
goto setArgs
:doneSetArgs

REM # set target (default all-tests)
IF "%TARGETS%"=="" set TARGETS=all-tests

REM # quickbuild.bat should be in the current directory or parent directory
IF EXIST "%CD%\quickbuild.bat" (
    set QUICKBUILD="%CD%\quickbuild.bat"
) else IF EXIST "%CD%\..\quickbuild.bat" (
    set QUICKBUILD="%CD%\..\quickbuild.bat"
) else (
    echo Can't find quickbuild.bat.  Call build-apps.bat from pwiz or pwiz_tools directory.
    exit /b
)

REM # register correct MSFileReader
if "%REGISTER%"=="1" (
    echo.
    echo Registering MSFileReader
    echo.
    if "%TARGETPLATFORM%"=="64" (
        IF EXIST "c:\Program Files (x86)\Thermo\MSFileReader\XRawfile2.dll" regsvr32 /s /u "c:\Program Files (x86)\Thermo\MSFileReader\XRawfile2.dll"
        REM # regsvr32 must be called through cmd /c for it to impact %ERRORLEVEL% with the /s option
        IF EXIST "c:\Program Files\Thermo\MSFileReader\XRawfile2_x64.dll" cmd /c "regsvr32 /s "c:\Program Files\Thermo\MSFileReader\XRawfile2_x64.dll""
        if %ERRORLEVEL% GTR 0 (
            echo *** Couldn't register 64-bit MSFileReader
            exit /b
        )
        echo *** Registered 64-bit MSFileReader

    ) else (
        IF EXIST "c:\Program Files\Thermo\MSFileReader\XRawfile2_x64.dll" regsvr32 /s /u "c:\Program Files\Thermo\MSFileReader\XRawfile2_x64.dll"
        REM # regsvr32 must be called through cmd /c for it to impact %ERRORLEVEL% with the /s option
        IF EXIST "c:\Program Files (x86)\Thermo\MSFileReader\XRawfile2.dll" cmd /c "regsvr32 /s "c:\Program Files (x86)\Thermo\MSFileReader\XRawfile2.dll""
        if %ERRORLEVEL% GTR 0 (
            echo *** Couldn't register 32-bit MSFileReader
            exit /b
        )
        echo *** Registered 32-bit MSFileReader
    )
) else (
    echo.
    echo Skipping MSFileReader registration
    echo.
)

if "%ARGS"=="" (
    echo Building %TARGETPLATFORM%-bit %TARGETS%
) else (
    echo Building %TARGETPLATFORM%-bit %TARGETS%: %ARGS%
)

REM # Do full build of ProteoWizard, passing quickbuild's arguments to bjam
set QUICKBUILDLOG=%CD%\build%TARGETPLATFORM%.log
echo Build output: %QUICKBUILDLOG%

REM # build!
echo.
echo %QUICKBUILD% %TARGETS% %ARGS% -j%NUMBER_OF_PROCESSORS% --hash optimization=space secure-scl=off address-model=%TARGETPLATFORM%
call %QUICKBUILD% %TARGETS% %ARGS% -j%NUMBER_OF_PROCESSORS% --hash optimization=space secure-scl=off address-model=%TARGETPLATFORM% >%QUICKBUILDLOG% 2>&1
echo.
echo Build done.
echo.

REM # look for problems
findstr /c:"...updated" %QUICKBUILDLOG%
findstr /c:"...skipped" %QUICKBUILDLOG%
findstr /c:"...failed" %QUICKBUILDLOG%
echo.
findstr /c:"Could not resolve reference" %QUICKBUILDLOG%
findstr /b /c:"Unable to load" %QUICKBUILDLOG%
findstr /b /c:"error:" %QUICKBUILDLOG%
findstr /c:"test(s) Passed" %QUICKBUILDLOG%
