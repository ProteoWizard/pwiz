@echo off
REM Syntax used in this batch file:
REM "~dp0": directory containing the executing batch file

if not exist pwiz_tools (
	echo Error: This tool should be run from the root of the project
	exit /b 1
)

call %~dp0readResxFiles.bat
%~dp0exe\ResourcesOrganizer.exe exportLocalizationCsv --language ja zh-CHS
