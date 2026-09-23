; -----------------------------------------------------------------------------
; Inno Setup script for Skyline and Skyline-daily (.NET build, Windows x64).
;
; Replaces both the ClickOnce publish (per-user install) and the WiX admin .msi
; (per-machine install) of the .NET Framework build with one installer:
;
;   - The user first picks "for me" (per-user, no admin) or "for everyone"
;     (per-machine, admin).
;   - Per-user installs go to %LOCALAPPDATA%\Programs\<Skyline|Skyline-daily>
;     with no license or directory page, as ClickOnce had neither.
;   - Per-machine installs show the license and the directory page, defaulting
;     to %ProgramFiles%\<Skyline|Skyline-daily>, as the WiX .msi did.
;   - Skyline and Skyline-daily are distinct products (their own AppIds) that
;     install side by side; within a product a newer version replaces the
;     previous one in place. A private build can be given another product
;     name (/DProductName), making it a third product with its own folder,
;     shortcut, file types and installer name.
;   - Start Menu shortcut under "MacCoss Lab, UW", optional Desktop shortcut,
;     .sky / .skyd / .skyp associations, Programs and Features entry, and a
;     registry record of the install location for SkylineRunner, SkylineBatch
;     and the MCP server to find the install.
;   - The .NET Desktop Runtime prerequisite is bundled (default) or checked
;     for (NoNetRuntime variant); see pwiz-sharp\installer\common\.
;
; build.ps1 stages the Skyline build output and invokes ISCC with:
;   /DSkylineAppName=Skyline|Skyline-daily   (the channel; from the staged exe)
;   /DProductName=...                        (what is installed; the channel by default)
;   /DMyAppVersion=YY.N.B.DDD                (FileVersion of the staged exe)
;   /DMyAppInformationalVersion=...          (ProductVersion, with the git hash)
;   /DStagingDir=..., /DOutputDir=..., /DOutputBaseFilename=...
;   /DDotNetRuntimeExePath=<cached runtime EXE>   (unless /DNoNetRuntime)
; -----------------------------------------------------------------------------

#ifndef SkylineAppName
  #define SkylineAppName "Skyline-daily"
#endif
#if SkylineAppName != "Skyline" && SkylineAppName != "Skyline-daily"
  #error SkylineAppName must be Skyline or Skyline-daily
#endif
#ifndef ProductName
  #define ProductName SkylineAppName
#endif
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0.0"
#endif
#ifndef MyAppInformationalVersion
  #define MyAppInformationalVersion MyAppVersion
#endif
#define MyAppPublisher "MacCoss Lab, University of Washington"
#define MyAppURL "https://skyline.ms/"
#define MyAppGroup "MacCoss Lab, UW"
#define MyAppExe SkylineAppName + ".exe"

; One stable AppId per product. Inno keys every install on it: same AppId means
; "upgrade in place", so each product has exactly one install per install mode.
; Not the WiX UpgradeCodes - an Inno install and an MSI are unrelated records.
; The two channels keep the GUIDs they shipped with; any other product name is
; its own AppId (Inno accepts any string), and the doubled brace that escapes a
; GUID's opening brace is part of the value here so the plain name needs none.
; The ProgId prefix is the product name minus the hyphen: a ProgId is
; Vendor.Component.Version with no punctuation but the periods.
#if ProductName == "Skyline"
  #define MyAppId "{{67DE971E-A042-4EF7-A93C-3F85D2A3D241}"
  #define ProgIdPrefix "Skyline"
#elif ProductName == "Skyline-daily"
  #define MyAppId "{{C701F69C-B553-4E3E-90D0-5676DD615570}"
  #define ProgIdPrefix "SkylineDaily"
#else
  #define MyAppId ProductName
  #define ProgIdPrefix StringChange(ProductName, "-", "")
#endif

#ifndef StagingDir
  #error StagingDir must be passed (/DStagingDir=<staged Skyline output>); see build.ps1
#endif
#ifndef OutputDir
  #define OutputDir "..\..\bin\installer"
#endif
#ifndef OutputBaseFilename
  #define OutputBaseFilename ProductName + "-Setup-" + MyAppVersion
#endif

[Setup]
AppId={#MyAppId}
AppName={#ProductName}
AppVersion={#MyAppVersion}
AppVerName={#ProductName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
VersionInfoVersion={#MyAppVersion}
VersionInfoProductTextVersion={#MyAppInformationalVersion}
; {autopf} is %LocalAppData%\Programs for a per-user install and Program Files for
; a per-machine one. The directory page is only offered for the latter (see
; ShouldSkipPage).
DefaultDirName={autopf}\{#ProductName}
DisableDirPage=no
DefaultGroupName={#MyAppGroup}
DisableProgramGroupPage=yes
; Ask "for me / for everyone" every time rather than silently reusing the
; previous install's mode, like the other ProteoWizard installers.
UsePreviousPrivileges=no
UninstallDisplayName={#ProductName}
UninstallDisplayIcon={app}\{#MyAppExe}
; Shown for per-machine installs only (see ShouldSkipPage).
LicenseFile=SkylineLicense.rtf
SetupIconFile=..\..\Skyline.ico
#ifdef SignSetup
; build.ps1 -SignToolCommand defines the "skylinesign" tool on the ISCC command
; line (/Sskylinesign=...); Inno then signs the uninstaller and the Setup.exe.
SignTool=skylinesign
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
; Explorer is told to refresh its association cache after install / uninstall.
ChangesAssociations=yes
; A running Skyline holds its own files open; ask the user to close it rather
; than falling back to replace-on-restart.
CloseApplications=yes
RestartApplications=no
AlwaysRestart=no
RestartIfNeededByRun=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "associate";   Description: "&Associate Skyline document files (.sky, .skyd, .skyp) with {#ProductName}"; \
    GroupDescription: "File associations:"
Name: "desktopicon"; Description: "Create a &Desktop shortcut"; \
    GroupDescription: "Desktop shortcuts:"; Flags: unchecked

[Files]
; The whole staged build output (build.ps1 strips XML doc files and the
; non-Windows native runtimes; everything else Skyline builds next to itself
; ships, vendor readers included, as the ClickOnce payload did).
Source: "{#StagingDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Document icons for the file associations, from the Skyline project directory.
Source: "..\..\SkylineDoc.ico";        DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\SkylineData.ico";       DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\SkylineDocPointer.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#ProductName}";       Filename: "{app}\{#MyAppExe}"; WorkingDir: "{app}"; \
    Comment: "Targeted mass spectrometry environment"
Name: "{autodesktop}\{#ProductName}"; Filename: "{app}\{#MyAppExe}"; WorkingDir: "{app}"; \
    Tasks: desktopicon

[UninstallDelete]
; Skyline creates Tools\ next to itself at run time (external tools). Only an
; empty one is removed - installed tools are the user's - so that the install
; directory itself can go.
Type: dirifempty; Name: "{app}\Tools"

[Registry]
; Where this product is installed, for SkylineRunner / SkylineBatch / the MCP
; server. HKA = HKCU for a per-user install, HKLM for a per-machine one.
Root: HKA; Subkey: "Software\MacCossLabUW"; Flags: uninsdeletekeyifempty
Root: HKA; Subkey: "Software\MacCossLabUW\{#ProductName}"; \
    ValueType: string; ValueName: "InstallDir"; ValueData: "{app}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\MacCossLabUW\{#ProductName}"; \
    ValueType: string; ValueName: "Version"; ValueData: "{#MyAppVersion}"

; File associations. Each product owns its own ProgIds, and they are new names:
; every ClickOnce install, daily or release, registered Skyline.Document.0 /
; .Data.0 / .Pointer.0 per-user (the legacy csproj hard-codes them for both
; channels), and those must stay intact while a ClickOnce Skyline coexists with
; an Inno one. The extension's default value is shared - last-installed-wins -
; so on uninstall it is handed back to whatever it named before this install
; (recorded at install time), and this channel's ProgIds are deleted only while
; their open command still points into {app}; see the [Code] section. That is
; why no uninsdelete flags appear here. OpenWithProgids lists every channel, so
; Explorer's "Open with" offers whichever are installed.
#define ProgIdDoc     ProgIdPrefix + ".Document.1"
#define ProgIdData    ProgIdPrefix + ".Data.1"
#define ProgIdPointer ProgIdPrefix + ".Pointer.1"
; Spelled as the [Registry] parameter text, so the embedded quotes are doubled.
#define OpenCommand   '""{app}\' + MyAppExe + '"" --opendoc ""%1""'

Root: HKA; Subkey: "Software\Classes\{#ProgIdDoc}"; ValueType: string; ValueData: "Skyline Document"; \
    Tasks: associate
Root: HKA; Subkey: "Software\Classes\{#ProgIdDoc}\DefaultIcon"; ValueType: string; ValueData: "{app}\SkylineDoc.ico"; \
    Tasks: associate
Root: HKA; Subkey: "Software\Classes\{#ProgIdDoc}\shell\open\command"; ValueType: string; ValueData: "{#OpenCommand}"; \
    Tasks: associate
Root: HKA; Subkey: "Software\Classes\.sky"; ValueType: string; ValueData: "{#ProgIdDoc}"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\.sky"; ValueType: string; ValueName: "Content Type"; ValueData: "application/sky"; \
    Tasks: associate
Root: HKA; Subkey: "Software\Classes\.sky\OpenWithProgids"; ValueType: string; ValueName: "{#ProgIdDoc}"; ValueData: ""; \
    Flags: uninsdeletevalue; Tasks: associate

Root: HKA; Subkey: "Software\Classes\{#ProgIdData}"; ValueType: string; ValueData: "Skyline Chromatogram Data"; \
    Tasks: associate
Root: HKA; Subkey: "Software\Classes\{#ProgIdData}\DefaultIcon"; ValueType: string; ValueData: "{app}\SkylineData.ico"; \
    Tasks: associate
Root: HKA; Subkey: "Software\Classes\{#ProgIdData}\shell\open\command"; ValueType: string; ValueData: "{#OpenCommand}"; \
    Tasks: associate
Root: HKA; Subkey: "Software\Classes\.skyd"; ValueType: string; ValueData: "{#ProgIdData}"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\.skyd"; ValueType: string; ValueName: "Content Type"; ValueData: "application/skyd"; \
    Tasks: associate
Root: HKA; Subkey: "Software\Classes\.skyd\OpenWithProgids"; ValueType: string; ValueName: "{#ProgIdData}"; ValueData: ""; \
    Flags: uninsdeletevalue; Tasks: associate

Root: HKA; Subkey: "Software\Classes\{#ProgIdPointer}"; ValueType: string; ValueData: "Skyline Document Pointer"; \
    Tasks: associate
Root: HKA; Subkey: "Software\Classes\{#ProgIdPointer}\DefaultIcon"; ValueType: string; ValueData: "{app}\SkylineDocPointer.ico"; \
    Tasks: associate
Root: HKA; Subkey: "Software\Classes\{#ProgIdPointer}\shell\open\command"; ValueType: string; ValueData: "{#OpenCommand}"; \
    Tasks: associate
Root: HKA; Subkey: "Software\Classes\.skyp"; ValueType: string; ValueData: "{#ProgIdPointer}"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\.skyp"; ValueType: string; ValueName: "Content Type"; ValueData: "application/skyp"; \
    Tasks: associate
Root: HKA; Subkey: "Software\Classes\.skyp\OpenWithProgids"; ValueType: string; ValueName: "{#ProgIdPointer}"; ValueData: ""; \
    Flags: uninsdeletevalue; Tasks: associate

; .NET desktop runtime: bundled EXE + [Run] entry, or the NoNetRuntime abort.
; Included here so its [Run] entry precedes the launch entry below.
#define DotNetMajor "10"
#define ProductDisplayName ProductName
#include "..\..\..\..\pwiz-sharp\installer\common\DotNetDesktopRuntime.iss"

[Run]
; ClickOnce launched Skyline as soon as the install finished; keep that as the
; checked-by-default finish-page option.
Filename: "{app}\{#MyAppExe}"; Description: "Launch {#ProductName}"; \
    Flags: nowait postinstall skipifsilent

; The WiX admin .msi (UpgradeCodes from Product-template.wxs, 64-bit) installed
; into the same %ProgramFiles% folder; refuse a per-machine install over it.
#include "..\..\..\..\pwiz-sharp\installer\common\LegacyMsi.iss"

[Code]
#if SkylineAppName == "Skyline"
  #define LegacyMsiUpgradeCode "{AADA455D-DB0C-4245-A844-AB81329B9E4F}"
#else
  #define LegacyMsiUpgradeCode "{08F72B41-9E5D-4A20-8CFF-8DAAE033CF34}"
#endif

const
  InstallRecordKey = 'Software\MacCossLabUW\{#ProductName}';
  { The AppId as Inno writes it into the key name: a GUID keeps its braces, and the
    doubled brace that escaped it in [Setup] is not part of the value. }
  UninstallKeyOfThisProduct = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#StringChange(MyAppId, "{{", "{")}_is1';

{ Refuse to overlay the legacy .msi, and warn when the product is already installed
  in the other mode: the mode is asked every run (UsePreviousPrivileges=no), so a
  click-through over an existing per-machine install would otherwise silently add a
  per-user copy beside it. Silent runs continue; their mode was given explicitly. }
function InitializeSetup(): Boolean;
var
  OtherRoot: Integer;
  OtherDir: String;
begin
  Result := True;
  if IsAdminInstallMode then
  begin
    Result := LegacyMsiAbortIfInstalled('{#ProductName}', ['{#LegacyMsiUpgradeCode}']);
    if not Result then
      Exit;
    OtherRoot := HKCU;
  end
  else
    OtherRoot := HKLM;
  if RegQueryStringValue(OtherRoot, UninstallKeyOfThisProduct, 'InstallLocation', OtherDir) and
     (not WizardSilent) then
  begin
    if IsAdminInstallMode then
      Result := MsgBox('{#ProductName} is already installed for your user account at ' + OtherDir + '.' + #13#10#13#10 +
                       'Installing it for all users as well creates a second, separate copy. Continue?',
                       mbConfirmation, MB_YESNO) = IDYES
    else
      Result := MsgBox('{#ProductName} is already installed for all users at ' + OtherDir + '.' + #13#10#13#10 +
                       'Installing it for your user account as well creates a second, separate copy that will ' +
                       'shadow the shared one. Continue?',
                       mbConfirmation, MB_YESNO) = IDYES;
  end;
end;

{ The license and directory pages are per-machine-only, keeping the per-user
  install as quick as the ClickOnce one was. A per-machine install over an
  existing one is upgraded where it is (what DisableDirPage=auto would do). }
function ShouldSkipPage(PageID: Integer): Boolean;
begin
  case PageID of
    wpLicense:
      Result := not IsAdminInstallMode;
    wpSelectDir:
      Result := (not IsAdminInstallMode) or (WizardForm.PrevAppDir <> '');
  else
    Result := False;
  end;
end;

{ ----- File associations -----
  The extension default values (.sky -> ProgId) are shared with every other
  Skyline registration on the machine: another product, a ClickOnce install
  (Skyline.Document.0), the WiX .msi. Before the [Registry] section makes this
  product the owner, remember who owned it, so the uninstall can hand it back
  rather than leave the extension unregistered. }
procedure RememberExtensionOwner(const Ext: String);
var
  Owner: String;
begin
  if not RegQueryStringValue(HKA, 'Software\Classes\' + Ext, '', Owner) then
    Owner := '';
  if (Owner = '{#ProgIdDoc}') or (Owner = '{#ProgIdData}') or (Owner = '{#ProgIdPointer}') then
    Exit; { our own earlier registration - keep the value recorded then }
  RegWriteStringValue(HKA, InstallRecordKey, 'PreviousOwner' + Ext, Owner);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssInstall) and WizardIsTaskSelected('associate') then
  begin
    RememberExtensionOwner('.sky');
    RememberExtensionOwner('.skyd');
    RememberExtensionOwner('.skyp');
  end;
end;

{ Uninstall: give the extension back to its previous owner if it still names our
  ProgId (and the previous owner's ProgId still exists), or clear it; then drop our
  ProgId, but only while its open command still points into this install. }
procedure ReleaseExtension(const Ext, ProgId, AppDir: String);
var
  Current, Previous, Command: String;
begin
  if RegQueryStringValue(HKA, 'Software\Classes\' + Ext, '', Current) and (Current = ProgId) then
  begin
    if RegQueryStringValue(HKA, InstallRecordKey, 'PreviousOwner' + Ext, Previous) and
       (Previous <> '') and RegKeyExists(HKA, 'Software\Classes\' + Previous) then
    begin
      RegWriteStringValue(HKA, 'Software\Classes\' + Ext, '', Previous);
      Log('Returned ' + Ext + ' to ' + Previous);
    end
    else
    begin
      RegDeleteValue(HKA, 'Software\Classes\' + Ext, '');
      RegDeleteValue(HKA, 'Software\Classes\' + Ext, 'Content Type');
      RegDeleteKeyIfEmpty(HKA, 'Software\Classes\' + Ext + '\OpenWithProgids');
      RegDeleteKeyIfEmpty(HKA, 'Software\Classes\' + Ext);
      Log('Released ' + Ext + ' from ' + ProgId);
    end;
  end;
  if RegQueryStringValue(HKA, 'Software\Classes\' + ProgId + '\shell\open\command', '', Command) and
     (Pos(Uppercase(AppDir), Uppercase(Command)) > 0) then
    RegDeleteKeyIncludingSubkeys(HKA, 'Software\Classes\' + ProgId);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  AppDir: String;
begin
  { usUninstall, not usPostUninstall: the uninstaller broadcasts the association
    change between the two, and these edits must precede the broadcast. }
  if CurUninstallStep = usUninstall then
  begin
    AppDir := ExpandConstant('{app}');
    ReleaseExtension('.sky', '{#ProgIdDoc}', AppDir);
    ReleaseExtension('.skyd', '{#ProgIdData}', AppDir);
    ReleaseExtension('.skyp', '{#ProgIdPointer}', AppDir);
  end;
end;
