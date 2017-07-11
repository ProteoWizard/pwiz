@echo off
::
:: setpwizenv.bat
::
:: This script sets up the shell environment to use the local pwiz copy of the
:: Boost Build system, including the bjam executable.  In particular, the script
::
:: 1) Modifies the PATH environment variable so that the local pwiz bjam is
::    found first.
::
:: 2) Sets the BOOST_BUILD_PATH environment variable to point to the local pwiz
::    Boost Build installation
::

set PWIZ_ROOT=%~dp0
set PWIZ_ROOT=%PWIZ_ROOT:~0,-1%\..
set PWIZ_BOOST_BUILD_PATH=%PWIZ_ROOT%\libraries\boost-build
set PWIZ_BJAM_PATH=%PWIZ_BOOST_BUILD_PATH%\engine\bin.nt

if exist %PWIZ_BJAM_PATH%\bjam.exe (
    echo Found pwiz bjam.exe:
    echo %PWIZ_BJAM_PATH%\bjam.exe
    echo.
) else (
    echo pwiz bjam.exe not found!
    echo %PWIZ_BJAM_PATH%\bjam.exe
    echo.
)

:: pwiz build system needs the following in PATH:
::     %PWIZ_BJAM_PATH%\bjam.exe
::     %PWIZ_ROOT%\libraries\bsdtar.exe

echo Setting PATH to include pwiz bjam.exe and bsdtar.exe:
set PATH=%PWIZ_BJAM_PATH%;%PWIZ_ROOT%\libraries;%PATH%
echo %PATH%
echo.

echo Setting BOOST_BUILD_PATH to pwiz Boost Build directory:
set BOOST_BUILD_PATH=%PWIZ_BOOST_BUILD_PATH%
echo %BOOST_BUILD_PATH%
echo.


