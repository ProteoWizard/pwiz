@echo off
setlocal
if not exist pwiz_tools (
	echo Error: This tool should be run from the root of the project
	exit /b 1
)

set ResourcesOrganizerExe=%~dp0exe\ResourcesOrganizer.exe
if exist resources.db (
	echo Using existing resources.db
) else (
	echo Reading resx files into resources.db
	call %~dp0readResxFiles.bat
)


%ResourcesOrganizerExe% exportLocalizationCsv --language ja zh-CHS
