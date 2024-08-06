@echo off
setlocal
if not exist pwiz_tools (
	echo Error: This tool should be run from the root of the project
	exit /b 1
)

set ResourcesOrganizerExe=%~dp0exe\ResourcesOrganizer.exe
set ArgumentsForAdd=pwiz_tools pwiz_tools --createnew --exclude pwiz_tools\msconvertgui pwiz_tools\seems pwiz_tools\bumbershoot pwiz_tools\shared\zedgraph pwiz_tools\shared\proteomedb\forms\proteomedbform.resx pwiz_tools\skyline\executables\autoqc pwiz_tools\skyline\executables\keepresxw pwiz_tools\skyline\executables\localizationhelper pwiz_tools\skyline\executables\multiload pwiz_tools\skyline\executables\sharedbatch pwiz_tools\skyline\executables\skylinebatch pwiz_tools\skyline\executables\skylinepeptidecolorgenerator pwiz_tools\skyline\executables\skylinerunner pwiz_tools\skyline\executables\sortresx pwiz_tools\skyline\executables\tools\exampleargcollector pwiz_tools\skyline\executables\tools\exampleinteractivetool pwiz_tools\skyline\executables\tools\toolservicetestharness pwiz_tools\skyline\executables\tools\TFExport\TFExportTool\TFExportTool\Properties\Resources.resx pwiz_tools\skyline\executables\tools\XLTCalc\c#\SkylineIntegration\Properties\Resources.resx pwiz_tools\skyline\executables\keepresxw pwiz_tools\skyline\controls\startup\tutoriallinkresources.resx pwiz_tools\skyline\skylinenightly pwiz_tools\skyline\skylinetester pwiz_tools\skyline\testutil pwiz_tools\Skyline\TestResults pwiz_tools\Skyline\Executables\DevTools pwiz_tools\Skyline\Controls\Startup\TutorialImageResources.resx pwiz_tools\Skyline\Controls\Startup\TutorialLinkResources.resx

echo Reading resx files
%ResourcesOrganizerExe% add %ArgumentsForAdd% %*
