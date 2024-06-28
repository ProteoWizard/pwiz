REM Syntax used in this batch file:
REM "~dp0": directory containing the executing batch file
setlocal
set ResourcesOrganizerExe=%~dp0..\ResourcesOrganizer\bin\Release\net8.0\ResourcesOrganizer.exe
set OperationAddArguments=pwiz_tools --createnew --exclude pwiz_tools\msconvertgui pwiz_tools\seems pwiz_tools\bumbershoot pwiz_tools\shared\zedgraph pwiz_tools\shared\proteomedb\forms\proteomedbform.resx pwiz_tools\skyline\executables\autoqc pwiz_tools\skyline\executables\keepresxw pwiz_tools\skyline\executables\localizationhelper pwiz_tools\skyline\executables\multiload pwiz_tools\skyline\executables\sharedbatch pwiz_tools\skyline\executables\skylinebatch pwiz_tools\skyline\executables\skylinepeptidecolorgenerator pwiz_tools\skyline\executables\skylinerunner pwiz_tools\skyline\executables\sortresx pwiz_tools\skyline\executables\tools\exampleargcollector pwiz_tools\skyline\executables\tools\exampleinteractivetool pwiz_tools\skyline\executables\tools\toolservicetestharness pwiz_tools\skyline\executables\tools\TFExport\TFExportTool\TFExportTool\Properties\Resources.resx pwiz_tools\skyline\executables\tools\XLTCalc\c#\SkylineIntegration\Properties\Resources.resx pwiz_tools\skyline\executables\keepresxw pwiz_tools\skyline\controls\startup\tutoriallinkresources.resx pwiz_tools\skyline\skylinenightly pwiz_tools\skyline\skylinetester pwiz_tools\skyline\testutil

pushd v23_1
%ResourcesOrganizerExe% add --db ..\v23_1.db %OperationAddArguments%
popd
pushd v24_1
%ResourcesOrganizerExe% add --db ..\v24_1.db %OperationAddArguments%
popd
%ResourcesOrganizerExe% importtranslations v23_1.db --db v24_1.db --output newstrings.db --language ja zh-CHS
%ResourcesOrganizerExe% export --db newstrings.db newresxfiles.zip
(
echo .mode csv
echo .header on
echo .output newjapanesestrings.csv
type %~dp0exportjapanesestrings.sql
) | sqlite3.exe newstrings.db
(
echo .mode csv
echo .header on
echo .output newchinesestrings.csv
type %~dp0exportchinesestrings.sql
) | sqlite3.exe newstrings.db
