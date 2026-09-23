; What this branch's installer builds and where it is published. Both Setup.iss and
; build.ps1 read this file, and build.ps1 writes the values into the config beside the
; installed exe, where Skyline's update check reads them.
;
; ProductName: what the installation is called - its folder under Programs or Program
;   Files, its Start Menu entry, its Programs and Features row, and the installer and
;   manifest names. Empty means the channel the exe was built as, Skyline or
;   Skyline-daily. Any other name is a product of its own that installs beside the
;   channels. Letters, digits, periods and hyphens only.
; InstallUrl: the folder the installer and its manifest are published in, and so
;   where the installed Skyline looks for a newer version:
;   <InstallUrl><ProductName>.json and <InstallUrl><ProductName>-Setup-<version>.exe.
#define ProductName "SkylineNet10Preview"
#define InstallUrl "https://proteome.gs.washington.edu/~nicksh/SpecialSkylines/SkylineNet10Preview/"
