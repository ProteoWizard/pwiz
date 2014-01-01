@echo off

echo.
echo Registering MSFileReader

REM # Bracketing causes problems, because of the quoting around cmd /c "...(x86)..."
REM # So use gotos instead

if "%1" NEQ "64" goto reg32

	IF EXIST "c:\Program Files (x86)\Thermo\MSFileReader\XRawfile2.dll" regsvr32 /s /u "c:\Program Files (x86)\Thermo\MSFileReader\XRawfile2.dll"
	REM # regsvr32 must be called through cmd /c for it to impact %ERRORLEVEL% with the /s option
	IF EXIST "c:\Program Files\Thermo\MSFileReader\XRawfile2_x64.dll" cmd /c "regsvr32 /s "c:\Program Files\Thermo\MSFileReader\XRawfile2_x64.dll""
	if %ERRORLEVEL% NEQ 0 (
	    echo     *** Couldn't register 64-bit MSFileReader
	    set ERRORLEVEL=1
	    exit /b 1
	)
	echo     *** Registered 64-bit MSFileReader

goto success

:reg32

	IF EXIST "c:\Program Files\Thermo\MSFileReader\XRawfile2_x64.dll" regsvr32 /s /u "c:\Program Files\Thermo\MSFileReader\XRawfile2_x64.dll"
	REM # regsvr32 must be called through cmd /c for it to impact %ERRORLEVEL% with the /s option
	IF EXIST "c:\Program Files (x86)\Thermo\MSFileReader\XRawfile2.dll" cmd /c "regsvr32 /s "c:\Program Files (x86)\Thermo\MSFileReader\XRawfile2.dll""
	if %ERRORLEVEL% NEQ 0 (
	    echo     *** Couldn't register 32-bit MSFileReader
	    set ERRORLEVEL=1
	    exit /b 1
	)
	echo     *** Registered 32-bit MSFileReader

:success

echo.
