CALL "%~dp0boost-build\src\engine\vswhere_usability_wrapper.cmd"
CALL "%VS150COMNTOOLS%VsDevCmd.bat" -arch=%1
CALL "%VS160COMNTOOLS%VsDevCmd.bat" -arch=%1