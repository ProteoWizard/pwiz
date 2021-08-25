REM Syntax used in this batch file:
REM "~dp0": directory containing the executing batch file
pushd %~dp0
..\..\..\..\..\libraries\7za.exe a MSstats4_external.zip *.r tool-inf  MSStatArgsCollector.dll
popd