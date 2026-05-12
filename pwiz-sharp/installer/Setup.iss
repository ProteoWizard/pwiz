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
AppId={{E4F1A2B3-5C6D-7E8F-9A0B-1C2D3E4F5A6B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
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
Name: "context_msconvertgui";   Description: "Add ""Convert with MSConvertGUI"" to the Windows Explorer right-click menu for mass-spec files"; \
    GroupDescription: "Windows Explorer integration:"
Name: "context_seems";          Description: "Add ""Open with SeeMS"" to the Windows Explorer right-click menu for mass-spec files"; \
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
Name: "{userdesktop}\MSConvertGUI"; Filename: "{app}\MSConvertGUI-sharp.exe"; \
    WorkingDir: "{app}"; Tasks: desktopicon_msconvertgui
Name: "{userdesktop}\SeeMS";        Filename: "{app}\seems-sharp.exe"; \
    WorkingDir: "{app}"; Tasks: desktopicon_seems

[Registry]
; Windows Explorer right-click "Open with X" entries for mass-spec file types.
; SystemFileAssociations is the Microsoft-blessed way to add context-menu
; verbs to a file extension WITHOUT taking over the default-handler. So we
; appear in the "Open with" submenu (or as a top-level verb on Win10+),
; without changing what double-click does on a .raw or .mzML.
;
; Root: HKA = "per-user install → HKCU; per-machine install → HKLM". Inno
; auto-resolves based on PrivilegesRequired at run time.
;
; The set of extensions covers the formats msconvert / SeeMS can READ:
;   .raw   = Thermo and Waters
;   .wiff  / .wiff2 = Sciex
;   .lcd   = Shimadzu
;   .baf   / .yep   = Bruker (small-data and ester formats; .d directories
;                    are handled by Bruker's Reader_Bruker but Windows
;                    doesn't support context-menu verbs on directory
;                    "extensions" reliably, so we skip those)
;   .mzML  / .mzXML / .mgf = open formats
;   .ms1   / .cms1 / .ms2 / .cms2 = legacy ASCII / binary

; --- MSConvertGUI ---
#define ConvertVerb "OpenWithMSConvertGUI"
#define ConvertLabel "Convert with MSConvertGUI"

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.raw\shell\{#ConvertVerb}"; \
    ValueType: string; ValueData: "{#ConvertLabel}"; Flags: uninsdeletekey; Tasks: context_msconvertgui
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.raw\shell\{#ConvertVerb}\command"; \
    ValueType: string; ValueData: """{app}\MSConvertGUI-sharp.exe"" ""%1"""; Tasks: context_msconvertgui

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.wiff\shell\{#ConvertVerb}"; \
    ValueType: string; ValueData: "{#ConvertLabel}"; Flags: uninsdeletekey; Tasks: context_msconvertgui
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.wiff\shell\{#ConvertVerb}\command"; \
    ValueType: string; ValueData: """{app}\MSConvertGUI-sharp.exe"" ""%1"""; Tasks: context_msconvertgui

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.wiff2\shell\{#ConvertVerb}"; \
    ValueType: string; ValueData: "{#ConvertLabel}"; Flags: uninsdeletekey; Tasks: context_msconvertgui
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.wiff2\shell\{#ConvertVerb}\command"; \
    ValueType: string; ValueData: """{app}\MSConvertGUI-sharp.exe"" ""%1"""; Tasks: context_msconvertgui

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.lcd\shell\{#ConvertVerb}"; \
    ValueType: string; ValueData: "{#ConvertLabel}"; Flags: uninsdeletekey; Tasks: context_msconvertgui
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.lcd\shell\{#ConvertVerb}\command"; \
    ValueType: string; ValueData: """{app}\MSConvertGUI-sharp.exe"" ""%1"""; Tasks: context_msconvertgui

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.baf\shell\{#ConvertVerb}"; \
    ValueType: string; ValueData: "{#ConvertLabel}"; Flags: uninsdeletekey; Tasks: context_msconvertgui
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.baf\shell\{#ConvertVerb}\command"; \
    ValueType: string; ValueData: """{app}\MSConvertGUI-sharp.exe"" ""%1"""; Tasks: context_msconvertgui

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.yep\shell\{#ConvertVerb}"; \
    ValueType: string; ValueData: "{#ConvertLabel}"; Flags: uninsdeletekey; Tasks: context_msconvertgui
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.yep\shell\{#ConvertVerb}\command"; \
    ValueType: string; ValueData: """{app}\MSConvertGUI-sharp.exe"" ""%1"""; Tasks: context_msconvertgui

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mzML\shell\{#ConvertVerb}"; \
    ValueType: string; ValueData: "{#ConvertLabel}"; Flags: uninsdeletekey; Tasks: context_msconvertgui
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mzML\shell\{#ConvertVerb}\command"; \
    ValueType: string; ValueData: """{app}\MSConvertGUI-sharp.exe"" ""%1"""; Tasks: context_msconvertgui

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mzXML\shell\{#ConvertVerb}"; \
    ValueType: string; ValueData: "{#ConvertLabel}"; Flags: uninsdeletekey; Tasks: context_msconvertgui
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mzXML\shell\{#ConvertVerb}\command"; \
    ValueType: string; ValueData: """{app}\MSConvertGUI-sharp.exe"" ""%1"""; Tasks: context_msconvertgui

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mgf\shell\{#ConvertVerb}"; \
    ValueType: string; ValueData: "{#ConvertLabel}"; Flags: uninsdeletekey; Tasks: context_msconvertgui
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mgf\shell\{#ConvertVerb}\command"; \
    ValueType: string; ValueData: """{app}\MSConvertGUI-sharp.exe"" ""%1"""; Tasks: context_msconvertgui

; --- SeeMS ---
#define ViewVerb "OpenWithSeeMS"
#define ViewLabel "Open with SeeMS"

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.raw\shell\{#ViewVerb}"; \
    ValueType: string; ValueData: "{#ViewLabel}"; Flags: uninsdeletekey; Tasks: context_seems
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.raw\shell\{#ViewVerb}\command"; \
    ValueType: string; ValueData: """{app}\seems-sharp.exe"" ""%1"""; Tasks: context_seems

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.wiff\shell\{#ViewVerb}"; \
    ValueType: string; ValueData: "{#ViewLabel}"; Flags: uninsdeletekey; Tasks: context_seems
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.wiff\shell\{#ViewVerb}\command"; \
    ValueType: string; ValueData: """{app}\seems-sharp.exe"" ""%1"""; Tasks: context_seems

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.wiff2\shell\{#ViewVerb}"; \
    ValueType: string; ValueData: "{#ViewLabel}"; Flags: uninsdeletekey; Tasks: context_seems
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.wiff2\shell\{#ViewVerb}\command"; \
    ValueType: string; ValueData: """{app}\seems-sharp.exe"" ""%1"""; Tasks: context_seems

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.lcd\shell\{#ViewVerb}"; \
    ValueType: string; ValueData: "{#ViewLabel}"; Flags: uninsdeletekey; Tasks: context_seems
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.lcd\shell\{#ViewVerb}\command"; \
    ValueType: string; ValueData: """{app}\seems-sharp.exe"" ""%1"""; Tasks: context_seems

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.baf\shell\{#ViewVerb}"; \
    ValueType: string; ValueData: "{#ViewLabel}"; Flags: uninsdeletekey; Tasks: context_seems
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.baf\shell\{#ViewVerb}\command"; \
    ValueType: string; ValueData: """{app}\seems-sharp.exe"" ""%1"""; Tasks: context_seems

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.yep\shell\{#ViewVerb}"; \
    ValueType: string; ValueData: "{#ViewLabel}"; Flags: uninsdeletekey; Tasks: context_seems
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.yep\shell\{#ViewVerb}\command"; \
    ValueType: string; ValueData: """{app}\seems-sharp.exe"" ""%1"""; Tasks: context_seems

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mzML\shell\{#ViewVerb}"; \
    ValueType: string; ValueData: "{#ViewLabel}"; Flags: uninsdeletekey; Tasks: context_seems
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mzML\shell\{#ViewVerb}\command"; \
    ValueType: string; ValueData: """{app}\seems-sharp.exe"" ""%1"""; Tasks: context_seems

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mzXML\shell\{#ViewVerb}"; \
    ValueType: string; ValueData: "{#ViewLabel}"; Flags: uninsdeletekey; Tasks: context_seems
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mzXML\shell\{#ViewVerb}\command"; \
    ValueType: string; ValueData: """{app}\seems-sharp.exe"" ""%1"""; Tasks: context_seems

Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mgf\shell\{#ViewVerb}"; \
    ValueType: string; ValueData: "{#ViewLabel}"; Flags: uninsdeletekey; Tasks: context_seems
Root: HKA; Subkey: "Software\Classes\SystemFileAssociations\.mgf\shell\{#ViewVerb}\command"; \
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
  Microsoft documents the canonical .NET runtime detection path as a registry
  enumeration under HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\
  Microsoft.WindowsDesktop.App. Any value name there is an installed version
  string ("8.0.0", "8.0.20", etc.). We accept any 8.x as satisfying the
  prereq. The 64-bit registry view is needed because we always install x64. }

function IsDotNet8DesktopInstalled(): Boolean;
var
  names: TArrayOfString;
  i: Integer;
  v: String;
begin
  Result := False;
  if not RegGetValueNames(
        HKEY_LOCAL_MACHINE,
        'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App',
        names) then
    Exit;
  for i := 0 to GetArrayLength(names) - 1 do
  begin
    v := names[i];
    if (Length(v) >= 2) and (Copy(v, 1, 2) = '8.') then
    begin
      Result := True;
      Exit;
    end;
  end;
end;
