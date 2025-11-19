@echo off
setlocal

REM Check if a path argument was provided
if "%~1"=="" (
    echo Usage: %0 ^<source_folder_path^>
    echo Example: %0 "C:\path\to\source"
    pause
    exit /b
)

REM Set the source folder based on the provided argument
set "sourceFolder=%~1"

REM Check if the source folder exists
if not exist "%sourceFolder%" (
    echo Error: The specified source folder "%sourceFolder%" does not exist.
    pause
    exit /b
)

REM Define the destination folder by appending "-utf8" to the source folder name
set "destinationFolder=%sourceFolder%-utf8"

REM Create the destination folder if it doesn't exist
if not exist "%destinationFolder%" mkdir "%destinationFolder%"

REM Loop through each .xml file in the source folder
for %%f in ("%sourceFolder%\*.view") do (
    "%~dp0\Utf16to8" "%%f" "%destinationFolder%\%%~nxf"
)

move "%sourceFolder%" "%sourceFolder%-utf16"
move "%destinationFolder%" "%sourceFolder%"

echo Conversion complete.
endlocal
pause
