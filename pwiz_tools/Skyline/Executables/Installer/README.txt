How to build the Skyline Installer
1. Install WIX Toolset 3.8
2. Build Skyline from the command line as usual (either 32 or 64 bit), but add "pwiz_tools/Skyline/Executables/Installer//setup.exe" to the commmand line
3. (Optional) Run an automatic test of the installer by adding the "pwiz_tools/Skyline/Executables/Installer//Test" target to the command line
4. Distribute the MSI built at "pwiz_tools/Skyline/bin/<platform>/Skyline[-daily]-<version>-<platform>.msi"
