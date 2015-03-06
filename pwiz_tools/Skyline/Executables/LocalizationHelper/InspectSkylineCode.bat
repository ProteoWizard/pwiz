:: Invokes ReSharper commandline tools to perform code inspection, including 
:: checks for strings that should be localized or declared "Not L10N".
::
:: Original author: Yuval Boss <yuval .at. u.washington.edu>,
::                  MacCoss Lab, Department of Genome Sciences, UW
::
:: Copyright 2014 University of Washington - Seattle, WA
:: 
:: Licensed under the Apache License, Version 2.0 (the "License");
:: you may not use this file except in compliance with the License.
:: You may obtain a copy of the License at
::
::     http://www.apache.org/licenses/LICENSE-2.0
::
:: Unless required by applicable law or agreed to in writing, software
:: distributed under the License is distributed on an "AS IS" BASIS,
:: WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
:: See the License for the specific language governing permissions and
:: limitations under the License.

cd
echo %1

IF "%JETBRAINS_HOME%" == "" (
    SET JETBRAINS_HOME=%LOCALAPPDATA%\JetBrains
	ECHO "Using LOCALAPPDATA environment variable to set JETBRAINS_HOME to locate ReSharper commandlines"
) ELSE (
	ECHO "Using JETBRAINS_HOME environment variable to locate ReSharper commandlines"
)

ECHO "dir JETBRAINS_HOME"
dir /a "%JETBRAINS_HOME%"

if exist "%JETBRAINS_HOME%\commandline8.2\" (
    Echo "InspectCode command line package v8.2 already installed, good."
 ) ELSE (
 	Echo "Installing ReSharper Command Line Tools v8.2."
    %1\scripts\misc\wget http://skyline.gs.washington.edu/downloads/jb-commandline-8.2.0.2151.zip
    move %1\jb-commandline-8.2.0.2151.zip "%JETBRAINS_HOME%\"
	%1\libraries\7za.exe x  "%JETBRAINS_HOME%/jb-commandline-8.2.0.2151.zip" -o"%JETBRAINS_HOME%\commandline8.2\"
)
if exist "%JETBRAINS_HOME%\commandline9.0\" (
    Echo "InspectCode command line package v9.0 already installed, good."
 ) ELSE (
 	Echo "Installing ReSharper Command Line Tools v9.0."
    %1\scripts\misc\wget http://skyline.gs.washington.edu/downloads/ReSharperCommandLineTools01Update1.zip
    move %1\ReSharperCommandLineTools01Update1.zip "%JETBRAINS_HOME%\"
	%1\libraries\7za.exe x  "%JETBRAINS_HOME%\ReSharperCommandLineTools01Update1.zip" -o"%JETBRAINS_HOME%\commandline9.0\"
)

if not exist "%JETBRAINS_HOME%\commandline8.2\InspectCode.exe" (
  Echo "There was an issue with your InspectCode 8.2 directory"
  Echo "Delete %JETBRAINS_HOME%\commandline8.2\ and rebuild."
)
if not exist "%JETBRAINS_HOME%\commandline9.0\InspectCode.exe" (
  Echo "There was an issue with your InspectCode 9.0 directory"
  Echo "Delete %JETBRAINS_HOME%\commandline9.0\ and rebuild."
)

"%JETBRAINS_HOME%\commandline8.2\inspectcode.exe" /p=%1\pwiz_tools\Skyline\Executables\LocalizationHelper\empty.sln.DotSettings /plugin=%1\pwiz_tools\Skyline\Executables\LocalizationHelper\Localizer\plugins\LocalizationHelper.dll /o="%2\InspectCodeOutput1.xml" /no-buildin-settings /properties=%3 %1\pwiz_tools\Skyline\Skyline.sln

"%JETBRAINS_HOME%\commandline9.0\inspectcode.exe" /profile=%1\pwiz_tools\Skyline\Skyline.sln.DotSettings /no-swea /o="%2\InspectCodeOutput2.xml" /no-buildin-settings /properties=%3 %1\pwiz_tools\Skyline\Skyline.sln

%1\pwiz_tools\Skyline\Executables\LocalizationHelper\OutputParser.exe %2\InspectCodeOutput1.xml %2\InspectCodeOutput2.xml 
if ERRORLEVEL 1 exit /b 1
exit /b 0
