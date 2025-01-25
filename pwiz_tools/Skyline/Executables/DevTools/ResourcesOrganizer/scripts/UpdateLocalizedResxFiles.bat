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
pushd %ROOT%
call %~dp0MakeResourcesDb.bat %WORKDIR%\NewRelease.db
popd
pushd %WORKDIR%
%RESORGANIZER% importLastVersion --db NewRelease.db %LASTVERSIONDB% --output newstrings.db --language ja zh-CHS
%RESORGANIZER% exportResx --db newstrings.db --overrideAll newresxfiles.zip
popd

echo SUCCESS
goto end
:end
