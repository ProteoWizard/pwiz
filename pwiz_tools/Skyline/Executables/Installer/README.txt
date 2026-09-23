Skyline installers
==================

.NET build (Skyline.csproj net10.0-windows): Inno Setup, Setup.iss
-------------------------------------------------------------------
One Setup.exe per channel replaces both the ClickOnce publish (per-user) and the
WiX admin .msi (per-machine) of the .NET Framework build:

  * "for me" (per-user, no admin, %LocalAppData%\Programs\<Skyline|Skyline-daily>,
    no license or directory page) or "for everyone" (admin, license and directory
    page, defaults to %ProgramFiles%\<Skyline|Skyline-daily>), asked at the start
    of every run
  * Skyline and Skyline-daily are distinct products that install side by side;
    a newer version of a channel replaces the previous one in place
  * Start Menu shortcut under "MacCoss Lab, UW", optional Desktop shortcut,
    .sky/.skyd/.skyp associations, Programs and Features entry, and
    HKCU|HKLM\Software\MacCossLabUW\<channel>\InstallDir for tools that need
    to find the install
  * Two variants: <channel>-Setup-<version>.exe bundles the .NET 10 Desktop
    Runtime installer (run only when the runtime is missing);
    <channel>-NoNetRuntime-Setup-<version>.exe only checks for it

Build:
  1. Build Skyline Release x64 (pwiz_tools\Skyline\build.bat --build-only, or
     Skyline.csproj from Visual Studio). The channel comes from the exe the build
     produced (Skyline-daily.exe by default; MSBuildAssemblyName=Skyline for the
     release channel) and the version from its FileVersion.
  2. pwsh -File pwiz_tools\Skyline\Executables\Installer\build.ps1
     Inno Setup 6 is fetched by pwiz-sharp\installer\Ensure-InnoSetup.ps1 if the
     machine lacks it; the runtime EXE is cached under pwiz-sharp\installer\cache.
     Pass -SignToolCommand '<signtool command with $f>' for a signed build.
  3. Installers land in pwiz_tools\Skyline\bin\installer\, with the update
     manifest <product>.json beside them. Skyline's startup check reads the
     manifest to learn the published version. The InstallUrl application
     setting (app.config) is a folder, and in it <product>.json is the
     manifest and <product>-Setup-<version>.exe is that version's installer,
     which is the name the build gives it. To publish, upload the manifest and
     the bundled installer as they are into that folder; the build prints both
     URLs.
     Setup.iss holds the defaults: the product is the channel and InstallUrl
     is the official folder. A branch that publishes its own build defines
     either in InstallerOverrides.iss beside it (comments only on master).
     Another ProductName is what Programs and Features, the Start Menu, the
     install folder, the installer and the manifest are called, so a private
     build such as SkylineNet10Preview installs beside the channels as a
     product of its own. The build writes the resulting values into the
     staged <channel>.dll.config, so the installed Skyline knows what to look
     for and where.
  4. pwsh -File pwiz_tools\Skyline\Executables\Installer\Test-Installer.ps1
     installs the newest one silently, checks the deployment, runs
     SkylineCmd --version, uninstalls, and checks the cleanup (-AllUsers from
     an elevated shell for the per-machine path).

The shared pieces (.NET runtime prerequisite, ISCC bootstrap) live under
pwiz-sharp\installer\; see pwiz-sharp\installer\NOTES.md.

.NET Framework build (Jam): WiX, Product-template.wxs
-----------------------------------------------------
1. Install WIX Toolset (3.8 or higher but less than 4.0)
2. For official signed builds, make sure the PFX file is in the Skyline folder ("University of Washington (MacCoss Lab).pfx")
3. Build Skyline from the command line as usual, but add "pwiz_tools/Skyline/Executables/Installer//setup.exe" to the commmand line (and for official builds, --pfx-password=<password>)
4. (Optional) Run an automatic test of the installer by adding the "pwiz_tools/Skyline/Executables/Installer//Test" target to the command line
5. Distribute the MSI built at "pwiz_tools/Skyline/bin/<platform>/Skyline[-daily]-<version>-<platform>.msi"
