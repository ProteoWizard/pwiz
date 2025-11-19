@setlocal
echo on
call %~dp0SetVariables.bat
if %ERRORLEVEL% neq 0 (
    goto end
)

pushd %PWIZ_ROOT%
call %~dp0MakeResourcesDb.bat %WORKDIR%\ForExportLocalizationCsv.db
popd
pushd %WORKDIR%
%RESORGANIZER% exportLocalizationCsv --db ForExportLocalizationCsv.db --language ja zh-CHS
if %ERRORLEVEL% neq 0 (
    goto error
)
popd
echo SUCCESS
goto end
:error
echo ERROR
:end

