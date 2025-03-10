set PWIZ_ROOT=%~dp0..\..\..\..\..\..
if not exist "%PWIZ_ROOT%\pwiz_tools" (
	echo "%PWIZ_ROOT%\pwiz_tools" does not exist
	SET ERRORLEVEL=1
	goto end
)

set RESORGANIZER=%~dp0..\ResourcesOrganizer\bin\Release\ResourcesOrganizer.exe
if not exist "%RESORGANIZER%" (
	echo "%RESORGANIZER%" does not exist
	SET ERRORLEVEL=1
	goto end
)

set WORKDIR=%PWIZ_ROOT%\pwiz_tools\Skyline\Translation\Scratch
if not exist "%WORKDIR%" (
	mkdir %WORKDIR%
)
:end
