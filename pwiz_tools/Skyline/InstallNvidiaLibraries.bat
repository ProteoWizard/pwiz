@echo off
setlocal enabledelayedexpansion

:: Set paths and URIs (you need to define these)
set "CudaVersion=12.6.3"
set "CudaInstaller=cuda_%CudaVersion%_windows_network.exe"
set "CudaDownloadUri=https://developer.download.nvidia.com/compute/cuda/%CudaVersion%/network_installers/%CudaInstaller%"
set "CudaDownloadPath=%USERPROFILE%\Downloads\%CudaInstaller%"

set "CuDNNVersion=9.6.0.74_cuda12"
set "CuDNNArchive=cudnn-windows-x86_64-%CuDNNVersion%-archive"
set "CuDNNDownloadUri=https://developer.download.nvidia.com/compute/cudnn/redist/cudnn/windows-x86_64/%CuDNNArchive%.zip"
set "CuDNNDownloadPath=%USERPROFILE%\Downloads\%CuDNNArchive%.zip"
set "CuDNNVersionDir=%USERPROFILE%\Downloads\cudnn\%CuDNNVersion%"
set "CuDNNInstallDir=C:\Program Files\NVIDIA\CUDNN\v9.x"

:: Download CUDA Library
echo Downloading CUDA Library...
powershell -Command "Invoke-WebRequest -Uri '%CudaDownloadUri%' -OutFile '%CudaDownloadPath%'"

:: Install CUDA Library
echo Installing CUDA Library...
"%CudaDownloadPath%"

:: Download cuDNN Library
echo Downloading cuDNN Library...
powershell -Command "Invoke-WebRequest -Uri '%CuDNNDownloadUri%' -OutFile '%CuDNNDownloadPath%'"

:: Install cuDNN Library
echo Installing cuDNN Library...

:: Extract cuDNN zip
powershell Expand-Archive -Path "%CuDNNDownloadPath%" -DestinationPath "%CuDNNVersionDir%" -Force

:: Create necessary directories (this might require admin rights)
mkdir "%CuDNNInstallDir%\bin" 2>NUL
mkdir "%CuDNNInstallDir%\include" 2>NUL
mkdir "%CuDNNInstallDir%\lib" 2>NUL

:: Copy files (you'll need to adjust permissions)
xcopy "%CuDNNVersionDir%\%CuDNNArchive%\bin\cudnn*.dll" "%CuDNNInstallDir%\bin" /Y /I
xcopy "%CuDNNVersionDir%\%CuDNNArchive%\include\cudnn*.h" "%CuDNNInstallDir%\include" /Y /I
xcopy "%CuDNNVersionDir%\%CuDNNArchive%\lib\x64\cudnn*.lib" "%CuDNNInstallDir%\lib" /Y /I

REM echo Installation complete!

endlocal
pause
