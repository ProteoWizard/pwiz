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
if exist localization.zh-Hans.csv (
    echo Importing Chinese translations from localization.zh-Hans.csv
    %RESORGANIZER% importLocalizationCsv --db ForImportLocalizationCsv.db --input localization.zh-Hans.csv --language zh-Hans
    if %ERRORLEVEL% neq 0 (
        goto error
    )
) else (
    echo localization.zh-Hans.csv not found, skipping Chinese
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
