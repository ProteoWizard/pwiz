cd
echo %1
if exist %LocalAPPDATA%/JetBrains/commandline/ (
    Echo "InspectCode command line package already installed."
 ) ELSE (
 	Echo "Installing ReSharper Command Line Tools."
    %1\scripts\misc\wget http://skyline.gs.washington.edu/downloads/jb-commandline-8.2.0.2151.zip
    move %1\jb-commandline-8.2.0.2151.zip %LocalAPPDATA%/JetBrains/
	%1\libraries\7za.exe x  %LocalAPPDATA%/JetBrains/jb-commandline-8.2.0.2151.zip -o%LocalAPPDATA%/JetBrains/commandline/
)
if not exist %LocalAPPDATA%/JetBrains/commandline/InspectCode.exe (
  Echo "There was an issue with your InspectCode directory"
  Echo "Delete %LocalAPPDATA%/JetBrains/commandline/ and rebuild."
)
echo %LocalAPPDATA%\JetBrains\commandline\inspectcode.exe /profile=%1\pwiz_tools\Skyline\Skyline.sln.DotSettings /plugin=%1\pwiz_tools\Skyline\Executables\LocalizationHelper\Localizer\plugins\LocalizationHelper.dll /o="%2\InspectCodeOutput.xml" /properties=%3 %1\pwiz_tools\Skyline\Skyline.sln

%LocalAPPDATA%\JetBrains\commandline\inspectcode.exe /profile=%1\pwiz_tools\Skyline\Skyline.sln.DotSettings /plugin=%1\pwiz_tools\Skyline\Executables\LocalizationHelper\Localizer\plugins\LocalizationHelper.dll /o="%2\InspectCodeOutput.xml" /properties=%3 %1\pwiz_tools\Skyline\Skyline.sln
%1\pwiz_tools\Skyline\Executables\LocalizationHelper\OutputParser.exe %2\InspectCodeOutput.xml
if ERRORLEVEL 1 exit /b 1
exit /b 0
