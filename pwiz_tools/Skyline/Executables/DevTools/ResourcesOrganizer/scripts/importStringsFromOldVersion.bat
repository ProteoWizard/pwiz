@echo off
REM creates the following files:
REM newrelease.zip: resx files to be committed to new release branch. 
REM These files contain either the localized string from the old release or the English text if anything has changed
REM <comment> tags are added to the strings in .ja and .zh-CHS resx files which were not present in the last version


REM Syntax used in this batch file:
REM "~dp0": directory containing the executing batch file

if not exist oldrelease (
	echo Error: Folder "oldrelease" does not exist
	exit /b 1
)
if not exist newrelease (
	echo Error: Folder "newrelease" does not exist
	exit /b 1
)

setlocal
set ResourcesOrganizerExe=%~dp0exe\ResourcesOrganizer.exe
set ArgumentsForAdd=pwiz_tools pwiz_tools --createnew --exclude pwiz_tools\msconvertgui pwiz_tools\seems pwiz_tools\bumbershoot pwiz_tools\shared\zedgraph pwiz_tools\shared\proteomedb\forms\proteomedbform.resx pwiz_tools\skyline\executables\autoqc pwiz_tools\skyline\executables\keepresxw pwiz_tools\skyline\executables\localizationhelper pwiz_tools\skyline\executables\multiload pwiz_tools\skyline\executables\sharedbatch pwiz_tools\skyline\executables\skylinebatch pwiz_tools\skyline\executables\skylinepeptidecolorgenerator pwiz_tools\skyline\executables\skylinerunner pwiz_tools\skyline\executables\sortresx pwiz_tools\skyline\executables\tools\exampleargcollector pwiz_tools\skyline\executables\tools\exampleinteractivetool pwiz_tools\skyline\executables\tools\toolservicetestharness pwiz_tools\skyline\executables\tools\TFExport\TFExportTool\TFExportTool\Properties\Resources.resx pwiz_tools\skyline\executables\tools\XLTCalc\c#\SkylineIntegration\Properties\Resources.resx pwiz_tools\skyline\executables\keepresxw pwiz_tools\skyline\controls\startup\tutoriallinkresources.resx pwiz_tools\skyline\skylinenightly pwiz_tools\skyline\skylinetester pwiz_tools\skyline\testutil pwiz_tools\Skyline\Controls\Startup\TutorialImageResources.resx pwiz_tools\Skyline\Controls\Startup\TutorialLinkResources.resx

pushd oldrelease
%ResourcesOrganizerExe% add --db ..\oldrelease.db %ArgumentsForAdd%
popd
pushd newrelease
%ResourcesOrganizerExe% add --db ..\newrelease.db %ArgumentsForAdd%
popd
%ResourcesOrganizerExe% importLastVersion --db newrelease.db oldrelease.db --output newstrings.db --language ja zh-CHS
%ResourcesOrganizerExe% exportResx --db newstrings.db --overrideAll newresxfiles.zip
