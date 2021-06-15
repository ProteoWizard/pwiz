CALL "%~dp0boost-build\src\engine\vswhere_usability_wrapper.cmd"
IF EXIST "%VS160COMNTOOLS%VsDevCmd.bat" CALL "%VS160COMNTOOLS%VsDevCmd.bat" -arch=%1 && exit /b
IF EXIST "%VS150COMNTOOLS%VsDevCmd.bat" CALL "%VS150COMNTOOLS%VsDevCmd.bat" -arch=%1 && exit /b
