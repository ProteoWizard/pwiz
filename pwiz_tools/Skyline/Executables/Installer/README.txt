How to build the Skyline Installer
1. Install WIX Toolset 3.8
2. Build Skyline from the command line as usual (either 32 or 64 bit)
3. Open pwiz_tools/Skyline/Executables/Installer/Installer.sln
4. Modify the ProductVersion <?define?> in Installer/ProductVersion.wxi
5. Build Solution
6. You can distribute either SkylineInstaller/bin/Release/SkylineInstaller.msi  Bootstrapper/bin/Release/SkylineAndPrerequisites.exe
	SkylineAndPrerequisites installs .Net Framework 3.5 before attempting to install Skyline
