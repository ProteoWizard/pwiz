cd
echo %1
if exist %LocalAPPDATA%/JetBrains/commandline8.2/ (
    Echo "InspectCode command line package already installed."
 ) ELSE (
 	Echo "Installing ReSharper Command Line Tools."
    %1\scripts\misc\wget http://skyline.gs.washington.edu/downloads/jb-commandline-8.2.0.2151.zip
    move %1\jb-commandline-8.2.0.2151.zip %LocalAPPDATA%/JetBrains/
	%1\libraries\7za.exe x  %LocalAPPDATA%/JetBrains/jb-commandline-8.2.0.2151.zip -o%LocalAPPDATA%/JetBrains/commandline8.2/
)
if exist %LocalAPPDATA%/JetBrains/commandline9.0/ (
    Echo "InspectCode command line package already installed."
 ) ELSE (
 	Echo "Installing ReSharper Command Line Tools."
    %1\scripts\misc\wget http://skyline.gs.washington.edu/downloads/ReSharperCommandLineTools01Update1.zip
    move %1\ReSharperCommandLineTools01Update1.zip %LocalAPPDATA%/JetBrains/
	%1\libraries\7za.exe x  %LocalAPPDATA%/JetBrains/ReSharperCommandLineTools01Update1.zip -o%LocalAPPDATA%/JetBrains/commandline9.0/
)

if not exist %LocalAPPDATA%/JetBrains/commandline8.2/InspectCode.exe (
  Echo "There was an issue with your InspectCode 8.2 directory"
  Echo "Delete %LocalAPPDATA%/JetBrains/commandline8.2/ and rebuild."
)
if not exist %LocalAPPDATA%/JetBrains/commandline9.0/InspectCode.exe (
  Echo "There was an issue with your InspectCode 9.0 directory"
  Echo "Delete %LocalAPPDATA%/JetBrains/commandline9.0/ and rebuild."
)

%LocalAPPDATA%\JetBrains\commandline8.2\inspectcode.exe /p=%1\pwiz_tools\Skyline\Executables\LocalizationHelper\empty.sln.DotSettings /plugin=%1\pwiz_tools\Skyline\Executables\LocalizationHelper\Localizer\plugins\LocalizationHelper.dll /o="%2\InspectCodeOutput1.xml" /no-buildin-settings /properties=%3 %1\pwiz_tools\Skyline\Skyline.sln

%LocalAPPDATA%\JetBrains\commandline9.0\inspectcode.exe /profile=%1\pwiz_tools\Skyline\Skyline.sln.DotSettings /no-swea /o="%2\InspectCodeOutput2.xml" /no-buildin-settings /properties=%3 %1\pwiz_tools\Skyline\Skyline.sln

%1\pwiz_tools\Skyline\Executables\LocalizationHelper\OutputParser.exe %2\InspectCodeOutput1.xml %2\InspectCodeOutput2.xml 
if ERRORLEVEL 1 exit /b 1
exit /b 0
