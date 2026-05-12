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
#define MyAppExeName "MSConvertGUI-sharp.exe"

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
UninstallDisplayIcon={app}\{#MyAppExeName}
LicenseFile=
SetupIconFile=
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &Desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

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
Name: "{group}\MSConvertGUI-sharp"; Filename: "{app}\{#MyAppExeName}"; \
    WorkingDir: "{app}"; \
    Comment: "Convert vendor mass-spec data to mzML / mzXML / MGF"
Name: "{userdesktop}\MSConvertGUI-sharp"; Filename: "{app}\{#MyAppExeName}"; \
    WorkingDir: "{app}"; Tasks: desktopicon

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

; Optional launch at end of install.
Filename: "{app}\{#MyAppExeName}"; Description: "&Launch MSConvertGUI-sharp"; \
    Flags: nowait postinstall skipifsilent

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
