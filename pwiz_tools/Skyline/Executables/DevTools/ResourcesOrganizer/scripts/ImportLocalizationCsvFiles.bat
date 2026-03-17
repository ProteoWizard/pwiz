@setlocal
echo on
call %~dp0SetVariables.bat
if %ERRORLEVEL% neq 0 (
    goto end
)

pushd %PWIZ_ROOT%
call %~dp0MakeResourcesDb.bat %WORKDIR%\ForImportLocalizationCsv.db
popd
pushd %WORKDIR%

REM Import Japanese translations
if exist localization.ja.csv (
    echo Importing Japanese translations from localization.ja.csv
    %RESORGANIZER% importLocalizationCsv --db ForImportLocalizationCsv.db --input localization.ja.csv --language ja
    if %ERRORLEVEL% neq 0 (
        goto error
    )
) else (
    echo localization.ja.csv not found, skipping Japanese
)

REM Import Chinese translations
if exist localization.zh-CHS.csv (
    echo Importing Chinese translations from localization.zh-CHS.csv
    %RESORGANIZER% importLocalizationCsv --db ForImportLocalizationCsv.db --input localization.zh-CHS.csv --language zh-CHS
    if %ERRORLEVEL% neq 0 (
        goto error
    )
) else (
    echo localization.zh-CHS.csv not found, skipping Chinese
)

REM Export updated resx files
echo Exporting updated resx files
%RESORGANIZER% exportResx --db ForImportLocalizationCsv.db ImportedResxFiles.zip
if %ERRORLEVEL% neq 0 (
    goto error
)
popd

REM Extract the updated resx files
pushd %PWIZ_ROOT%
echo Extracting updated resx files
libraries\7za.exe x -y %WORKDIR%\ImportedResxFiles.zip
if %ERRORLEVEL% neq 0 (
    goto error
)
popd

echo SUCCESS
goto end
:error
echo ERROR
:end
