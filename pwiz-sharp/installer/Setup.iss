; -----------------------------------------------------------------------------
; Inno Setup script for ProteoWizard-Sharp.
;
; Replaces the WiX 4 MSI + Burn bundle pair. Produces a single self-contained
; ProteoWizard-Sharp-Setup.exe (~5 MB compressed) that:
;   - Detects .NET 8 desktop runtime via registry; downloads + installs from
;     Microsoft if missing
;   - Asks user to pick "for me" (per-user, no admin) or "for everyone"
;     (per-machine, admin) at install time
;   - Installs to %LOCALAPPDATA%\Programs\ProteoWizard-Sharp\ or
;     %ProgramFiles%\ProteoWizard-Sharp\ accordingly
;   - Creates a Start Menu shortcut for MSConvertGUI-sharp
;   - Standard uninstall via Programs and Features
;
; Compile:
;   pwsh -File pwiz-sharp/installer/build.ps1
;
; ISCC.exe lives at %LOCALAPPDATA%\Programs\Inno Setup 6\ (installed via
; `winget install JRSoftware.InnoSetup`).
; -----------------------------------------------------------------------------

#define MyAppName "ProteoWizard-Sharp"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "ProteoWizard"
#define MyAppURL "https://proteowizard.sourceforge.io/"

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
; AppId and DefaultDirName both embed the version so distinct versions install
; side-by-side without colliding (e.g. a stable release and a dev/preview build).
; Inno keys every install on AppId — a versioned AppId means each version has
; its own uninstall slot in Programs and Features, its own install dir, and its
; own uninstaller log. Same-version reinstalls still upgrade in place.
;
; The base GUID stays stable so future migration code (or scripts iterating
; "all ProteoWizard-Sharp installs") can match on the prefix.
;
; Shared resources policy (last-installed-wins, no automatic cleanup):
;   - Explorer context-menu verbs are SHARED across versions: each install
;     overwrites them to point at its own EXEs, and uninstall LEAVES them
;     alone (no uninsdeletekey). Orphan risk if every version is removed.
;   - Start Menu group and Desktop shortcuts are VERSIONED (the group name +
;     shortcut filename include {#MyAppVersion}), so each install owns its
;     own shortcuts and uninstall cleanly removes just that version's set.
AppId={{E4F1A2B3-5C6D-7E8F-9A0B-1C2D3E4F5A6B}_{#MyAppVersion}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}\{#MyAppVersion}
DefaultGroupName={#MyAppName} {#MyAppVersion}
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
; flow — pwiz-sharp users prefer to relaunch manually.
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

; .NET 8 desktop runtime installer EXE. Bundled into the setup; deleted after
; install via [InstallDelete]. Skip download (DownloadTemporaryFile would work
; but offline-install support is the main reason we bundle Burn-style).
Source: "cache\windowsdesktop-runtime-win-x64.exe"; DestDir: "{tmp}"; \
    Flags: deleteafterinstall; Check: not IsDotNet8DesktopInstalled

[Icons]
Name: "{group}\MSConvertGUI"; Filename: "{app}\MSConvertGUI-sharp.exe"; \
    WorkingDir: "{app}"; Tasks: startmenu_msconvertgui; \
    Comment: "Convert vendor mass-spec data to mzML / mzXML / MGF"
Name: "{group}\SeeMS";        Filename: "{app}\seems-sharp.exe"; \
    WorkingDir: "{app}"; Tasks: startmenu_seems; \
    Comment: "Spectrum viewer for vendor mass-spec data and mzML"
; Desktop shortcuts are version-suffixed so multiple installed versions don't
; clobber each other's icon — each version writes its own "MSConvertGUI 0.1.0".
; The Start Menu group is similarly version-suffixed via DefaultGroupName.
Name: "{userdesktop}\MSConvertGUI {#MyAppVersion}"; Filename: "{app}\MSConvertGUI-sharp.exe"; \
    WorkingDir: "{app}"; Tasks: desktopicon_msconvertgui
Name: "{userdesktop}\SeeMS {#MyAppVersion}";        Filename: "{app}\seems-sharp.exe"; \
    WorkingDir: "{app}"; Tasks: desktopicon_seems

[Registry]
; Windows Explorer right-click verbs.
;
; Vendor mass-spec acquisitions aren't always plain files — Bruker .d, Agilent
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
;               Recycle Bin, etc. — those use the abstract "Folder" class)
;
; This is consistent with how the legacy pwiz installer registers msconvertgui
; on the parent of the .d / .raw directory. msconvert + SeeMS already do
; reader-detection on the path passed to them, so an unsupported file just
; surfaces an error from the app, the same as if the user dragged it into the
; window.
;
; Root: HKA = per-user install -> HKCU; per-machine install -> HKLM. Inno
; auto-resolves based on PrivilegesRequired at run time.

; Note on multi-version policy: no Flags: uninsdeletekey on these verb writes.
; Each install OVERWRITES the verb's command value to point at its own EXE
; (last-installed-wins), and the verb survives any individual version's
; uninstall. If you uninstall every installed version the verb is orphaned and
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

[Run]
; .NET 8 desktop runtime install. Runs only if not already present. /install
; /quiet /norestart matches Microsoft's documented silent-install flags. The
; runtime always installs per-machine (it goes to %ProgramFiles%\dotnet\),
; which means the .NET install step triggers UAC even if the pwiz-sharp
; install itself is per-user — Inno surfaces this cleanly via the
; "shellexec" flag + Verb=runas, which raises the UAC prompt at the right
; moment rather than at process start.
Filename: "{tmp}\windowsdesktop-runtime-win-x64.exe"; \
    Parameters: "/install /quiet /norestart"; \
    StatusMsg: "Installing .NET 8 desktop runtime..."; \
    Flags: waituntilterminated shellexec; \
    Check: not IsDotNet8DesktopInstalled

; Optional "launch at end of install" buttons. Both unchecked by default so
; the wizard finishes silently; users can pick either.
Filename: "{app}\MSConvertGUI-sharp.exe"; Description: "Launch &MSConvertGUI"; \
    Flags: nowait postinstall skipifsilent unchecked
Filename: "{app}\seems-sharp.exe"; Description: "Launch See&MS"; \
    Flags: nowait postinstall skipifsilent unchecked

[Code]
{ ----- .NET 8 desktop runtime detection -----
  The .NET runtime is "installed" iff its files live under
  C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App\<version>\ — that
  directory is what the dotnet host walks at startup, so its presence/absence
  is the authoritative signal.

  The registry-based check we tried originally
  (HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\...) wasn't
  reliable: on at least some machines the .NET installer registers under
  WOW6432Node (the 32-bit view) even for x64 runtimes, while Inno's HKLM
  default depends on its install mode. The filesystem layout is the same on
  every install. }

function IsDotNet8DesktopInstalled(): Boolean;
var
  baseDir: String;
  rec: TFindRec;
  found: Boolean;
begin
  Result := False;
  baseDir := ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App');
  if not DirExists(baseDir) then
    Exit;
  found := FindFirst(baseDir + '\*', rec);
  try
    while found do
    begin
      if ((rec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0) and
         (Copy(rec.Name, 1, 2) = '8.') then
      begin
        Result := True;
        Exit;
      end;
      found := FindNext(rec);
    end;
  finally
    FindClose(rec);
  end;
end;
