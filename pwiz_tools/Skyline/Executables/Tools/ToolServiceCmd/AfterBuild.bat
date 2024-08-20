setlocal
if "%1"=="" {
set Configuration=Release
} else (
set Configuration=%1
)
set SevenZipExe=%~dp0..\..\..\..\..\libraries\7za.exe
set outputZip=%~dp0ExampleCmdTool.zip

del %outputZip%
pushd "%~dp0ExampleTool"
%SevenZipExe% a %outputZip% * tool-inf\*
popd
pushd %~dp0ToolServiceCmd\bin\%Configuration%\net8.0
%SevenZipExe% a %outputZip% *.dll *.exe
if %Configuration%==Debug %SevenZipExe% a %outputZip% *.pdb
popd
