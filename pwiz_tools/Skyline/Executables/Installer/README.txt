How to build the Skyline Installer
1. Install WIX Toolset (3.8 or higher but less than 4.0)
2. For official signed builds, make sure the PFX file is in the Skyline folder ("University of Washington (MacCoss Lab).pfx")
3. Build Skyline from the command line as usual, but add "pwiz_tools/Skyline/Executables/Installer//setup.exe" to the commmand line (and for official builds, --pfx-password=<password>)
4. (Optional) Run an automatic test of the installer by adding the "pwiz_tools/Skyline/Executables/Installer//Test" target to the command line
5. Distribute the MSI built at "pwiz_tools/Skyline/bin/<platform>/Skyline[-daily]-<version>-<platform>.msi"
