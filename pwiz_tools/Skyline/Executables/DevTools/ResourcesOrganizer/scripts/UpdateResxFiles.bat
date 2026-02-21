@setlocal
echo on
if "%1" == "final" (
    set EXPORTARGS=--overrideAll
) else if "%1" == "incremental" (
    set EXPORTARGS=
) else (
    echo Usage %~nx0 final^|incremental
    set ERRORLEVEL=1
    goto end
)

call %~dp0SetVariables.bat
if %ERRORLEVEL% neq 0 (
    goto end
)

set LASTVERSIONDB=%PWIZ_ROOT%\pwiz_tools\Skyline\Translation\LastReleaseResources.db
if not exist %LASTVERSIONDB% (
    echo %LASTVERSIONDB% does not exist
    set ERRORLEVEL=1
    goto end
)
pushd %PWIZ_ROOT%
call %~dp0MakeResourcesDb.bat %WORKDIR%\CurrentRelease.db
popd
pushd %WORKDIR%
%RESORGANIZER% importLastVersion --db CurrentRelease.db %LASTVERSIONDB% --output MergedResources.db --language ja zh-CHS
if %ERRORLEVEL% neq 0 (
    goto error
)
%RESORGANIZER% exportResx %EXPORTARGS% --db MergedResources.db MergedResxFiles.zip
if %ERRORLEVEL% neq 0 (
    goto error
)
popd
pushd %PWIZ_ROOT%
libraries\7za.exe x -y %WORKDIR%\MergedResxFiles.zip
if %ERRORLEVEL% neq 0 (
    goto error
)
popd

echo SUCCESS
goto end
:error
echo ERROR
:end
