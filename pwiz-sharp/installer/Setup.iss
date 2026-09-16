; -----------------------------------------------------------------------------
; Inno Setup script for ProteoWizard-Sharp.
;
; build.ps1 invokes ISCC twice from this one script to produce two variants:
;
;   ProteoWizard-Sharp-Setup.exe (~62 MB)
;     Bundles the .NET 10 Desktop Runtime installer. On install, checks for
;     .NET 10; if missing, silently invokes the bundled runtime installer
;     (which triggers UAC for its per-machine install).
;
;   ProteoWizard-Sharp-NoNetRuntime-Setup.exe (~5 MB)
;     Same payload minus the bundled runtime. On install, aborts with a
;     dialog + download link if .NET 10 isn't already present. For users
;     who manage their own runtime install (corp deployments, dev boxes
;     that already have it, etc.).
;
;   Both variants:
;     - Ask the user to pick "for me" (per-user, no admin) or "for everyone"
;       (per-machine, admin) at install time
;     - Ask for a standard install (replaced in place by newer versions) or a
;       version-specific one (kept beside other versions, never replaced);
;       see common\InstallType.iss
;     - Install to %LOCALAPPDATA%\Programs\ProteoWizard-Sharp\ or
;       %ProgramFiles%\ProteoWizard-Sharp\ accordingly (plus " <version>"
;       for a version-specific install)
;     - Create Start Menu shortcuts for MSConvertGUI and SeeMS
;     - Register Windows Explorer right-click verbs
;     - Standard uninstall via Programs and Features
;
; The lightweight variant is selected by passing `/DNoNetRuntime` to ISCC -
; build.ps1 does this automatically for the second compile pass. Without
; the define (= default behavior, including direct ISCC invocations), the
; bundled-runtime variant is produced.
;
; Compile:
;   pwsh -File pwiz-sharp/installer/build.ps1
;
; ISCC.exe lives at %LOCALAPPDATA%\Programs\Inno Setup 6\ (installed via
; `winget install JRSoftware.InnoSetup`).
; -----------------------------------------------------------------------------

#define MyAppName "ProteoWizard-Sharp"
; Version comes in from build.ps1 via /DMyAppVersion=4.0.YYDOY-gitsha. The
; fallback below keeps direct ISCC invocations buildable for local debugging.
#ifndef MyAppVersion
  #define MyAppVersion "4.0.0-dev"
#endif
#define MyAppPublisher "ProteoWizard"
#define MyAppURL "https://proteowizard.sourceforge.io/"
; The stable AppId. A standard install is keyed on exactly this; a version-specific
; install on "<this>_<version>" (see common\InstallType.iss). Installer.Tests matches
; on the same GUID, so a change here must be mirrored there.
#define AppBaseId "{E4F1A2B3-5C6D-7E8F-9A0B-1C2D3E4F5A6B}"

; StagingDir + OutputDir come from build.ps1 via /Dxxx command-line defines so
; the script doesn't have to hardcode paths.
#ifndef StagingDir
  #define StagingDir "build\stage"
#endif
#ifndef OutputDir
  #define OutputDir "build"
#endif
#ifndef OutputBaseFilename
  #define OutputBaseFilename "ProteoWizard-Sharp-Setup"
#endif

[Setup]
; AppId, directory, Start Menu group and uninstall entry name all depend on the
; standard / version-specific choice, so they route through common\InstallType.iss.
; Inno keys every install on AppId: the standard install has one uninstall slot per
; install mode and is upgraded in place, a version-specific install gets its own.
;
; Shared resources policy (last-installed-wins, no automatic cleanup):
;   - Explorer context-menu verbs are SHARED across installs: each install
;     overwrites them to point at its own EXEs, and uninstall LEAVES them
;     alone (no uninsdeletekey). Orphan risk if every install is removed.
;   - Start Menu group and Desktop shortcuts belong to the install that made
;     them (versioned names for a version-specific install), so uninstall
;     cleanly removes just that install's set.
AppId={code:InstallTypeAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
UninstallDisplayName={code:InstallTypeDisplayName}
; Both required by Inno whenever AppId includes constants: the language and the
; install mode are chosen before the AppId can be evaluated. Moot for the language
; (there is one), and the install-mode dialog is meant to be asked every time.
UsePreviousLanguage=no
UsePreviousPrivileges=no
; InstallType.iss decides when the directory page is skipped.
DisableDirPage=no
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseFilename}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\MSConvertGUI-sharp.exe
LicenseFile=
SetupIconFile=
MinVersion=10.0
; CloseApplications detects running instances of our app and prompts the user to
; close them so file replacement doesn't fall back to "schedule for restart".
; RestartApplications=no skips Inno's "want me to restart those apps for you?"
; flow - pwiz-sharp users prefer to relaunch manually.
CloseApplications=yes
RestartApplications=no
; AlwaysRestart=no + RestartIfNeededByRun=no: don't show the "restart your PC"
; prompt at end of setup unless a file we tried to replace is genuinely locked
; (in that case Inno schedules the replace via MoveFileEx and a restart is
; required for it to take effect). The .NET runtime EXE in [Run] uses
; /norestart so it never queues anything.
AlwaysRestart=no
RestartIfNeededByRun=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startmenu_msconvertgui"; Description: "Add a &Start Menu shortcut for MSConvertGUI"; \
    GroupDescription: "Start Menu shortcuts:"
Name: "startmenu_seems";        Description: "Add a Start Menu shortcut for See&MS"; \
    GroupDescription: "Start Menu shortcuts:"
Name: "context_msconvertgui";   Description: "Add ""Convert with MSConvertGUI"" to the Windows Explorer right-click menu (files and folders)"; \
    GroupDescription: "Windows Explorer integration:"
Name: "context_seems";          Description: "Add ""Open with SeeMS"" to the Windows Explorer right-click menu (files and folders)"; \
    GroupDescription: "Windows Explorer integration:"
Name: "desktopicon_msconvertgui"; Description: "Create a Desktop shortcut for MSConvertGUI"; \
    GroupDescription: "Desktop shortcuts:"; Flags: unchecked
Name: "desktopicon_seems";        Description: "Create a Desktop shortcut for SeeMS"; \
    GroupDescription: "Desktop shortcuts:"; Flags: unchecked

[Files]
; Bring the entire pwiz-sharp staging tree (filtered by build.ps1: no vendor
; SDKs, no debug symbols, no cross-platform native runtimes) into {app}.
Source: "{#StagingDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; {group} already carries the version for a version-specific install (the group
; name is set by InstallType.iss), so the shortcut names inside it stay plain.
Name: "{group}\MSConvertGUI"; Filename: "{app}\MSConvertGUI-sharp.exe"; \
    WorkingDir: "{app}"; Tasks: startmenu_msconvertgui; \
    Comment: "Convert vendor mass-spec data to mzML / mzXML / MGF"
Name: "{group}\SeeMS";        Filename: "{app}\seems-sharp.exe"; \
    WorkingDir: "{app}"; Tasks: startmenu_seems; \
    Comment: "Spectrum viewer for vendor mass-spec data and mzML"
; Desktop shortcuts share one folder, so a version-specific install suffixes
; them with its version and leaves other installs' icons alone.
Name: "{userdesktop}\MSConvertGUI{code:InstallTypeNameSuffix}"; Filename: "{app}\MSConvertGUI-sharp.exe"; \
    WorkingDir: "{app}"; Tasks: desktopicon_msconvertgui
Name: "{userdesktop}\SeeMS{code:InstallTypeNameSuffix}";        Filename: "{app}\seems-sharp.exe"; \
    WorkingDir: "{app}"; Tasks: desktopicon_seems

[Registry]
; Windows Explorer right-click verbs.
;
; Vendor mass-spec acquisitions aren't always plain files - Bruker .d, Agilent
; .d, and Waters .raw are FOLDERS containing the real instrument data (e.g.
; analysis.tdf inside a Bruker .d, _FUNC001.DAT inside a Waters .raw). Windows
; doesn't reliably support "extension"-based context-menu rules on directories
; (SystemFileAssociations\.d does not work the way SystemFileAssociations\.raw
; does for files), so we'd miss most of the formats msconvert can actually
; read if we kept the per-extension list.
;
; Instead, register against the two canonical class entries Microsoft documents
; for "all files" and "all real folders":
;   *         -> every file regardless of extension
;   Directory -> every real folder (excludes virtual folders like Control Panel,
;               Recycle Bin, etc. - those use the abstract "Folder" class)
;
; This is consistent with how the legacy pwiz installer registers msconvertgui
; on the parent of the .d / .raw directory. msconvert + SeeMS already do
; reader-detection on the path passed to them, so an unsupported file just
; surfaces an error from the app, the same as if the user dragged it into the
; window.
;
; Root: HKA = per-user install -> HKCU; per-machine install -> HKLM. Inno
; auto-resolves based on PrivilegesRequired at run time.

; Note on multi-install policy: no Flags: uninsdeletekey on these verb writes.
; Each install OVERWRITES the verb's command value to point at its own EXE
; (last-installed-wins), and the verb survives any individual install's
; uninstall. If you uninstall every install you have the verb is orphaned and
; will fail with "file not found" until you install something again.

; --- MSConvertGUI ---
#define ConvertVerb "OpenWithMSConvertGUI"
#define ConvertLabel "Convert with MSConvertGUI"

Root: HKA; Subkey: "Software\Classes\*\shell\{#ConvertVerb}"; \
    ValueType: string; ValueData: "{#ConvertLabel}"; Tasks: context_msconvertgui
Root: HKA; Subkey: "Software\Classes\*\shell\{#ConvertVerb}"; \
    ValueType: string; ValueName: "Icon"; ValueData: """{app}\MSConvertGUI-sharp.exe"""; Tasks: context_msconvertgui
Root: HKA; Subkey: "Software\Classes\*\shell\{#ConvertVerb}\command"; \
    ValueType: string; ValueData: """{app}\MSConvertGUI-sharp.exe"" ""%1"""; Tasks: context_msconvertgui

Root: HKA; Subkey: "Software\Classes\Directory\shell\{#ConvertVerb}"; \
    ValueType: string; ValueData: "{#ConvertLabel}"; Tasks: context_msconvertgui
Root: HKA; Subkey: "Software\Classes\Directory\shell\{#ConvertVerb}"; \
    ValueType: string; ValueName: "Icon"; ValueData: """{app}\MSConvertGUI-sharp.exe"""; Tasks: context_msconvertgui
Root: HKA; Subkey: "Software\Classes\Directory\shell\{#ConvertVerb}\command"; \
    ValueType: string; ValueData: """{app}\MSConvertGUI-sharp.exe"" ""%1"""; Tasks: context_msconvertgui

; --- SeeMS ---
#define ViewVerb "OpenWithSeeMS"
#define ViewLabel "Open with SeeMS"

Root: HKA; Subkey: "Software\Classes\*\shell\{#ViewVerb}"; \
    ValueType: string; ValueData: "{#ViewLabel}"; Tasks: context_seems
Root: HKA; Subkey: "Software\Classes\*\shell\{#ViewVerb}"; \
    ValueType: string; ValueName: "Icon"; ValueData: """{app}\seems-sharp.exe"""; Tasks: context_seems
Root: HKA; Subkey: "Software\Classes\*\shell\{#ViewVerb}\command"; \
    ValueType: string; ValueData: """{app}\seems-sharp.exe"" ""%1"""; Tasks: context_seems

Root: HKA; Subkey: "Software\Classes\Directory\shell\{#ViewVerb}"; \
    ValueType: string; ValueData: "{#ViewLabel}"; Tasks: context_seems
Root: HKA; Subkey: "Software\Classes\Directory\shell\{#ViewVerb}"; \
    ValueType: string; ValueName: "Icon"; ValueData: """{app}\seems-sharp.exe"""; Tasks: context_seems
Root: HKA; Subkey: "Software\Classes\Directory\shell\{#ViewVerb}\command"; \
    ValueType: string; ValueData: """{app}\seems-sharp.exe"" ""%1"""; Tasks: context_seems

; .NET 10 desktop runtime: bundled EXE + [Run] entry, or the NoNetRuntime abort.
; Included here so its [Run] entry precedes the launch entries below.
#define DotNetMajor "10"
#define DotNetRuntimeExePath "cache\windowsdesktop-runtime-10.0-win-x64.exe"
#define ProductDisplayName MyAppName
#include "common\DotNetDesktopRuntime.iss"

[Run]
; Optional "launch at end of install" buttons. Both unchecked by default so
; the wizard finishes silently; users can pick either.
Filename: "{app}\MSConvertGUI-sharp.exe"; Description: "Launch &MSConvertGUI"; \
    Flags: nowait postinstall skipifsilent unchecked
Filename: "{app}\seems-sharp.exe"; Description: "Launch See&MS"; \
    Flags: nowait postinstall skipifsilent unchecked

; Standard / version-specific wizard page and the AppId, directory, group and
; display-name code the [Setup] section routes through.
#include "common\InstallType.iss"
