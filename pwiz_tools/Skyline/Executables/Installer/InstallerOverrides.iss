; Overrides for this branch's installer. Setup.iss defines the defaults and then
; includes this file, so a #define here replaces the default. On master this file is
; comments only and an official build is the channel (Skyline or Skyline-daily)
; published in the official folder. A branch that publishes its own build defines
; what it changes, for example:
;
;   #define ProductName "SkylineNet10Preview"
;   #define InstallUrl "https://proteome.gs.washington.edu/~nicksh/SpecialSkylines/SkylineNet10Preview/"
;
; ProductName: what the installation is called - its folder under Programs or Program
;   Files, its Start Menu entry, its Programs and Features row, and the installer and
;   manifest names. Any name other than the channel's is a product of its own that
;   installs beside the channels. Letters, digits, periods and hyphens only.
; InstallUrl: the folder the installer and its manifest are published in, and so
;   where the installed Skyline looks for a newer version:
;   <InstallUrl><ProductName>.json and <InstallUrl><ProductName>-Setup-<version>.exe.
;
; Both Setup.iss and build.ps1 read this file, and build.ps1 writes the resulting
; values into the config beside the installed exe, where Skyline's update check reads
; them.

#define ProductName "SkylineNet10Preview"
#define InstallUrl "https://proteome.gs.washington.edu/~nicksh/SpecialSkylines/SkylineNet10Preview/"
