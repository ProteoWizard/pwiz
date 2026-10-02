; -----------------------------------------------------------------------------
; Inno Setup script for Osprey (Windows x64).
;
; Built by ..\package.ps1 -Setup, which dotnet-publishes a self-contained win-x64
; Osprey into a staging folder and then invokes ISCC with:
;   /DStagingDir=<self-contained win-x64 publish folder>
;   /DMyAppVersion=<YEAR.ORDINAL.BRANCH.DOY>       (Get-OspreyVersion)
;   /DOutputDir=..., /DOutputBaseFilename=Osprey-Setup-<version>
;
; The payload is self-contained (its own .NET runtime), so unlike the
; ProteoWizard-Sharp and Skyline installers there is no runtime prerequisite.
;
; What the installer does, mirroring ProteoWizard-Sharp minus its Explorer
; context-menu verbs (Osprey is a command-line tool):
;   - Ask "for me" (per-user, no admin) or "for everyone" (per-machine, admin)
;   - Ask for a standard install (replaced in place by newer versions) or a
;     version-specific one (kept beside other versions, never replaced); see
;     pwiz-sharp\installer\common\InstallType.iss
;   - Install to %LOCALAPPDATA%\Programs\Osprey\ or %ProgramFiles%\Osprey\
;     (plus " <version>" for a version-specific install)
;   - Optionally put the install directory on the user's or the system PATH so
;     `osprey` resolves in any shell (what the retired WiX .msi always did)
;   - Optionally add Start Menu shortcuts: a command prompt with Osprey on the
;     PATH and the command-line documentation
;   - Standard uninstall via Programs and Features, which also takes the
;     directory back off the PATH
; -----------------------------------------------------------------------------

#define MyAppName "Osprey"
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0.0"
#endif
#define MyAppPublisher "MacCoss Lab, University of Washington"
#define MyAppURL "https://github.com/ProteoWizard/pwiz/tree/master/pwiz_tools/Osprey"
; The stable AppId (not the retired .msi's UpgradeCode: an Inno install and an MSI
; are unrelated records). A standard install is keyed on exactly this, a
; version-specific install on "<this>_<version>".
#define AppBaseId "{8C03D3FA-EA86-4678-B6C4-E356E7EF1D68}"

#ifndef StagingDir
  #error StagingDir must be passed (/DStagingDir=<publish folder>); see package.ps1
#endif
#ifndef OutputDir
  #define OutputDir "..\dist"
#endif
#ifndef OutputBaseFilename
  #define OutputBaseFilename "Osprey-Setup-" + MyAppVersion
#endif

[Setup]
AppId={code:InstallTypeAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
VersionInfoVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
UninstallDisplayName={code:InstallTypeDisplayName}
UninstallDisplayIcon={app}\Osprey.exe
; See InstallType.iss for why these three are what they are.
DisableDirPage=no
UsePreviousLanguage=no
UsePreviousPrivileges=no
DisableProgramGroupPage=yes
#ifdef SignSetup
; package.ps1 -Sign defines the "ospreysign" tool on the ISCC command line
; (/Sospreysign=...); Inno then signs the uninstaller and the Setup.exe with it.
SignTool=ospreysign
SignedUninstaller=yes
#endif
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseFilename}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
; Setup broadcasts WM_SETTINGCHANGE after the PATH edits in [Code], so open
; shells that re-read the environment (and new ones) see the change.
ChangesEnvironment=yes
CloseApplications=yes
RestartApplications=no
AlwaysRestart=no
RestartIfNeededByRun=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "addtopath";  Description: "Add Osprey to the PATH (so ""osprey"" works in any command prompt)"; \
    GroupDescription: "Command line:"
Name: "startmenu";  Description: "Add &Start Menu shortcuts (Osprey command prompt, documentation)"; \
    GroupDescription: "Start Menu shortcuts:"

[Files]
; The whole self-contained publish tree, as staged by package.ps1 (Osprey.exe,
; the runtime, Documentation\, README.md, LICENSE - the license ships but is not
; a wizard page, matching the ProteoWizard-Sharp installer).
Source: "{#StagingDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; {group} already carries the version for a version-specific install.
; The quoted `set ""PATH=...""` form keeps the trailing space before && out of the
; last PATH entry. WorkingDir stays a literal %USERPROFILE% (Setup only expands
; {...} constants) so the shell resolves it for whoever launches the shortcut,
; not the account that ran a per-machine install.
Name: "{group}\Osprey Command Prompt"; Filename: "{cmd}"; \
    Parameters: "/k set ""PATH={app};%PATH%""&& osprey --help"; \
    WorkingDir: "%USERPROFILE%"; IconFilename: "{app}\Osprey.exe"; Tasks: startmenu; \
    Comment: "Command prompt with Osprey {#MyAppVersion} on the PATH"
Name: "{group}\Osprey Documentation"; Filename: "{app}\Documentation\CommandLine.html"; \
    Tasks: startmenu; Comment: "Osprey command-line reference"

[Registry]
; Where the standard install lives, for tools that want to find Osprey without
; walking the PATH (the retired .msi wrote the same value under HKLM).
; Version-specific installs deliberately do not claim it.
Root: HKA; Subkey: "Software\University of Washington\Osprey"; \
    ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; \
    Flags: uninsdeletevalue uninsdeletekeyifempty; Check: not IsVersionedInstall

[Code]
{ ----- PATH -----
  Per-user installs edit HKCU\Environment, per-machine installs the system
  environment key. Values are REG_EXPAND_SZ, read unexpanded and written back
  unexpanded so existing %VAR% entries survive. Entries are compared without a
  trailing backslash and case-insensitively. }

function EnvRootKey(): Integer;
begin
  if IsAdminInstallMode then
    Result := HKEY_LOCAL_MACHINE
  else
    Result := HKEY_CURRENT_USER;
end;

function EnvSubKey(): String;
begin
  if IsAdminInstallMode then
    Result := 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment'
  else
    Result := 'Environment';
end;

function EnvPathWithout(const Paths, Dir: String): String;
var
  Rest, Entry: String;
  P: Integer;
begin
  Result := '';
  Rest := Paths;
  while Rest <> '' do
  begin
    P := Pos(';', Rest);
    if P = 0 then
    begin
      Entry := Rest;
      Rest := '';
    end
    else
    begin
      Entry := Copy(Rest, 1, P - 1);
      Delete(Rest, 1, P);
    end;
    if (Entry <> '') and (CompareText(RemoveBackslash(Entry), RemoveBackslash(Dir)) <> 0) then
    begin
      if Result <> '' then
        Result := Result + ';';
      Result := Result + Entry;
    end;
  end;
end;

procedure EnvAddPath(const Dir: String);
var
  Paths: String;
begin
  if not RegQueryStringValue(EnvRootKey(), EnvSubKey(), 'Path', Paths) then
    Paths := '';
  Paths := EnvPathWithout(Paths, Dir);
  if Paths <> '' then
    Paths := Paths + ';';
  Paths := Paths + Dir;
  if RegWriteExpandStringValue(EnvRootKey(), EnvSubKey(), 'Path', Paths) then
    Log('Added to PATH: ' + Dir)
  else
    Log('Failed to add to PATH: ' + Dir);
end;

procedure EnvRemovePath(const Dir: String);
var
  Paths, Trimmed: String;
begin
  if not RegQueryStringValue(EnvRootKey(), EnvSubKey(), 'Path', Paths) then
    Exit;
  Trimmed := EnvPathWithout(Paths, Dir);
  if Trimmed = Paths then
    Exit;
  if RegWriteExpandStringValue(EnvRootKey(), EnvSubKey(), 'Path', Trimmed) then
    Log('Removed from PATH: ' + Dir)
  else
    Log('Failed to remove from PATH: ' + Dir);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and WizardIsTaskSelected('addtopath') then
    EnvAddPath(ExpandConstant('{app}'));
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  { Unconditional: removing an entry that was never added is a no-op, and the
    uninstaller has no record of which tasks the install selected. usUninstall
    rather than usPostUninstall, so the edit precedes the uninstaller's
    WM_SETTINGCHANGE broadcast. }
  if CurUninstallStep = usUninstall then
    EnvRemovePath(ExpandConstant('{app}'));
end;

#include "..\..\..\pwiz-sharp\installer\common\LegacyMsi.iss"
{ The first Osprey packaging was a per-machine WiX .msi into the same
  %ProgramFiles%\Osprey folder (UpgradeCode from the retired Osprey.wxs); refuse
  a per-machine install over it. }
<event('InitializeSetup')>
function LegacyMsiInitializeSetup(): Boolean;
begin
  Result := True;
  if IsAdminInstallMode then
    Result := LegacyMsiAbortIfInstalled('Osprey', ['{7B0D9C2A-4E6F-4A1B-8C3D-2F5E6A7B8C9D}']);
end;

{ Standard / version-specific wizard page and the AppId, directory, group and
  display-name code the [Setup] section routes through. }
#include "..\..\..\pwiz-sharp\installer\common\InstallType.iss"
