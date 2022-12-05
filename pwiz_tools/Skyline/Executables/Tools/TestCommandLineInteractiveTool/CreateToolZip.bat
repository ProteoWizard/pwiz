del %~dp0TestCommandLineInteractiveTool.zip
%~dp0..\..\..\..\..\libraries\7za.exe a %~dp0TestCommandLineInteractiveTool.zip %~dp0bin\%1\* %~dp0tool-inf
%~dp0..\..\..\..\..\libraries\7za.exe a %~dp0..\..\..\TestFunctional\InteractiveCommandLineToolTest.zip %~dp0TestCommandLineInteractiveTool.zip