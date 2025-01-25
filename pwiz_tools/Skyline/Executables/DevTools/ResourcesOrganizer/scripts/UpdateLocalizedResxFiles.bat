@setlocal
call %~dp0SetVariables.bat
if %ERRORLEVEL% neq 0 (
	goto end
)

set LASTVERSIONDB=%~dp0..\LastReleaseResources.db
if not exist %LASTVERSIONDB% (
	echo %LASTVERSIONDB% does not exist
	set ERRORLEVEL=1
	goto end
)
pushd %PWIZ_ROOT%
call %~dp0MakeResourcesDb.bat %WORKDIR%\NewRelease.db
popd
pushd %WORKDIR%
%RESORGANIZER% importLastVersion --db NewRelease.db %LASTVERSIONDB% --output newstrings.db --language ja zh-CHS
if %ERRORLEVEL% neq 0 (
	goto error
)
%RESORGANIZER% exportResx --db newstrings.db mergedresxfiles.zip
if %ERRORLEVEL% neq 0 (
	goto error
)
popd
pushd %PWIZ_ROOT%
libraries\7za.exe x -y %WORKDIR%\mergedresxfiles.zip
if %ERRORLEVEL% neq 0 (
	goto error
)
popd

echo SUCCESS
goto end
:error
echo ERROR
:end
