@echo off
setlocal

if not exist resources.db (
	echo resources.db does not exist;
	exit /b 1;
) 
set ResourcesOrganizerExe=%~dp0exe\ResourcesOrganizer.exe
echo importing translations into resources.db
%ResourcesOrganizerExe% importLocalizationCsv --language ja zh-CHS
