REM creates a file called ""

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
set ResourcesOrganizerExe=%~dp0..\ResourcesOrganizer\bin\Release\net8.0\ResourcesOrganizer.exe
set VerbAddArguments=pwiz_tools --createnew --overrideall --exclude pwiz_tools\msconvertgui pwiz_tools\seems pwiz_tools\bumbershoot pwiz_tools\shared\zedgraph pwiz_tools\shared\proteomedb\forms\proteomedbform.resx pwiz_tools\skyline\executables\autoqc pwiz_tools\skyline\executables\keepresxw pwiz_tools\skyline\executables\localizationhelper pwiz_tools\skyline\executables\multiload pwiz_tools\skyline\executables\sharedbatch pwiz_tools\skyline\executables\skylinebatch pwiz_tools\skyline\executables\skylinepeptidecolorgenerator pwiz_tools\skyline\executables\skylinerunner pwiz_tools\skyline\executables\sortresx pwiz_tools\skyline\executables\tools\exampleargcollector pwiz_tools\skyline\executables\tools\exampleinteractivetool pwiz_tools\skyline\executables\tools\toolservicetestharness pwiz_tools\skyline\executables\tools\TFExport\TFExportTool\TFExportTool\Properties\Resources.resx pwiz_tools\skyline\executables\tools\XLTCalc\c#\SkylineIntegration\Properties\Resources.resx pwiz_tools\skyline\executables\keepresxw pwiz_tools\skyline\controls\startup\tutoriallinkresources.resx pwiz_tools\skyline\skylinenightly pwiz_tools\skyline\skylinetester pwiz_tools\skyline\testutil pwiz_tools\Skyline\Controls\Startup\TutorialImageResources.resx pwiz_tools\Skyline\Controls\Startup\TutorialLinkResources.resx

pushd oldrelease
%ResourcesOrganizerExe% add --db ..\oldrelease.db %VerbAddArguments%
popd
pushd newrelease
%ResourcesOrganizerExe% add --db ..\newrelease.db %VerbAddArguments%
popd
%ResourcesOrganizerExe% importtranslations --db newrelease.db oldrelease.db --output updatednewrelease.db --language ja zh-CHS
%ResourcesOrganizerExe% export --db updatednewrelease.db newrelease.zip
(
echo .mode csv
echo .header on
echo .output newjapanesestrings.csv
type %~dp0exportjapanesestrings.sql
) | sqlite3.exe updatednewrelease.db
(
echo .mode csv
echo .header on
echo .output updatednewrelease.csv
type %~dp0exportchinesestrings.sql
) | sqlite3.exe newstrings.db
