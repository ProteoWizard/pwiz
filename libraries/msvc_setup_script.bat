CALL "%~dp0boost-build\src\engine\vswhere_usability_wrapper.cmd"
IF "%2"=="" echo Must specify platform toolset version without period (e.g. 141, 142, 143)
IF "%2"=="141" IF EXIST "%VS150COMNTOOLS%VsDevCmd.bat" (CALL "%VS150COMNTOOLS%VsDevCmd.bat" -arch=%1 && exit /b 0) ELSE echo VS2017 not found. && exit /b 1
IF "%2"=="142" IF EXIST "%VS160COMNTOOLS%VsDevCmd.bat" (CALL "%VS160COMNTOOLS%VsDevCmd.bat" -arch=%1 && exit /b 0) ELSE echo VS2019 not found. && exit /b 1
IF "%2"=="143" IF EXIST "%VS170COMNTOOLS%VsDevCmd.bat" (CALL "%VS170COMNTOOLS%VsDevCmd.bat" -arch=%1 && exit /b 0) ELSE echo VS2022 not found. && exit /b 1

echo Unsupported requested platform toolset version %2; current supported versions: 141, 142, 143
exit /b 1
