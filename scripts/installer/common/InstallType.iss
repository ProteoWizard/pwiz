[Code]
(* -----------------------------------------------------------------------------
  "Standard" vs "version-specific" installation, shared by the installers whose
  users may want several versions side by side (ProteoWizard-Sharp, Osprey).

    Standard          One install per install mode (per-user / per-machine). A newer
                      version replaces it in place: same AppId, same directory, same
                      unversioned Start Menu group and shortcuts.
    Version-specific  AppId, directory, Start Menu group and shortcut names all carry
                      the version, so the install is never touched by a later version
                      and uninstalls independently.

  The choice is a wizard page shown after the license page. Silent installs (and the
  page's initial selection) take it from the command line: /INSTALLTYPE=versioned or
  /INSTALLTYPE=standard (the default).

  Known limit: Setup looks a previous install up once, at startup, under whichever
  AppId the command line implies (the standard one unless /INSTALLTYPE=versioned), so
  the previous-install conveniences Inno offers - remembered directory, group and
  task selections, the automatic directory-page skip - are native only for that one.
  The include recovers the important part for the other case (the directory of an
  existing version-specific install of the same version) itself.

  The product script must define, before the #include:
    MyAppName      product name, also the directory and Start Menu group base name
    MyAppVersion   version string appended to the directory / group / AppId
    AppBaseId      the product's stable AppId GUID, in braces: {XXXXXXXX-XXXX-...}

  and wire the [Setup] section like this (the include supplies only [Code]):
    AppId={code:InstallTypeAppId}
    DefaultDirName={autopf}\{#MyAppName}
    DefaultGroupName={#MyAppName}
    UninstallDisplayName={code:InstallTypeDisplayName}
    DisableDirPage=no
    UsePreviousLanguage=no
    UsePreviousPrivileges=no

  DisableDirPage=no because the include decides itself when to skip the directory
  page: a standard install over an existing one is upgraded where it is (what
  DisableDirPage=auto would do), while a version-specific install always shows the
  page, since Setup looked up the previous install under the standard AppId before
  the user could choose. The two UsePrevious* directives are required by Inno for
  any AppId that includes constants.

  Shortcut names outside {group} (e.g. on the desktop) should end in
  {code:InstallTypeNameSuffix} so each version-specific install owns its own.
  Everything that is shared across versions (Explorer verbs, PATH) is the product
  script's business; the convention there is last-installed-wins.
  ----------------------------------------------------------------------------- *)

#ifndef MyAppName
  #error MyAppName must be defined before including InstallType.iss
#endif
#ifndef MyAppVersion
  #error MyAppVersion must be defined before including InstallType.iss
#endif
#ifndef AppBaseId
  #error AppBaseId must be defined before including InstallType.iss
#endif

var
  InstallTypePage: TWizardPage;
  InstallTypeStandardRadio: TNewRadioButton;
  InstallTypeVersionedRadio: TNewRadioButton;
  { DirEdit / GroupEdit as the wizard initialized them: DefaultDirName / DefaultGroupName,
    or the previous standard install's values, or /DIR. Restored when the user comes
    back to "standard" after having looked at "version-specific". }
  InstallTypeInitialDir: String;
  InstallTypeInitialGroup: String;
  { The choice the edits currently reflect, so re-visiting the page without changing
    it keeps a directory the user edited by hand. }
  InstallTypeApplied: Boolean;
  InstallTypeAppliedVersioned: Boolean;

function InstallTypeParamIsVersioned(): Boolean;
begin
  Result := CompareText(ExpandConstant('{param:INSTALLTYPE|standard}'), 'versioned') = 0;
end;

{ True for a version-specific install. Before the wizard exists (Setup evaluates AppId
  once at startup to look up a previous install) only the command line can answer,
  and a standard answer there is what makes the previous standard install's directory,
  group and task selections come back as the wizard's defaults. }
function IsVersionedInstall(): Boolean;
begin
  if InstallTypeVersionedRadio <> nil then
    Result := InstallTypeVersionedRadio.Checked
  else
    Result := InstallTypeParamIsVersioned();
end;

function InstallTypeNameSuffix(Param: String): String;
begin
  if IsVersionedInstall() then
    Result := ' {#MyAppVersion}'
  else
    Result := '';
end;

function InstallTypeAppId(Param: String): String;
begin
  if IsVersionedInstall() then
    Result := '{#AppBaseId}_{#MyAppVersion}'
  else
    Result := '{#AppBaseId}';
end;

function InstallTypeDisplayName(Param: String): String;
begin
  Result := '{#MyAppName}' + InstallTypeNameSuffix('');
end;

{ Where the version-specific install of this very version already is, if anywhere.
  Setup only looked up the previous install under the standard AppId, so this is the
  lookup it could not do for the versioned one; without it a re-run of the same
  installer would install a second copy into the default folder and orphan the first. }
function InstallTypePreviousVersionedDir(): String;
begin
  if not RegQueryStringValue(HKA,
       'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#AppBaseId}_{#MyAppVersion}_is1',
       'Inno Setup: App Path', Result) then
    Result := '';
end;

{ Push the current choice into the directory and group edits the later pages show.
  /DIR on the command line wins over the version-specific default (/GROUP does not
  apply: Inno ignores it while DisableProgramGroupPage=yes). }
procedure InstallTypeApply();
var
  Versioned: Boolean;
  PrevDir: String;
begin
  Versioned := IsVersionedInstall();
  if InstallTypeApplied and (Versioned = InstallTypeAppliedVersioned) then
    Exit;
  InstallTypeApplied := True;
  InstallTypeAppliedVersioned := Versioned;
  if Versioned then
  begin
    if ExpandConstant('{param:DIR|}') = '' then
    begin
      PrevDir := InstallTypePreviousVersionedDir();
      if PrevDir <> '' then
        WizardForm.DirEdit.Text := PrevDir
      else
        WizardForm.DirEdit.Text := ExpandConstant('{autopf}\{#MyAppName} {#MyAppVersion}');
    end;
    WizardForm.GroupEdit.Text := '{#MyAppName} {#MyAppVersion}';
  end
  else
  begin
    WizardForm.DirEdit.Text := InstallTypeInitialDir;
    WizardForm.GroupEdit.Text := InstallTypeInitialGroup;
  end;
end;

function InstallTypeAddOption(const Caption, Description: String; Top: Integer;
  var Radio: TNewRadioButton): Integer;
var
  Text: TNewStaticText;
begin
  Radio := TNewRadioButton.Create(InstallTypePage);
  Radio.Parent := InstallTypePage.Surface;
  Radio.Left := 0;
  Radio.Top := Top;
  Radio.Width := InstallTypePage.SurfaceWidth;
  Radio.Caption := Caption;

  Text := TNewStaticText.Create(InstallTypePage);
  Text.Parent := InstallTypePage.Surface;
  Text.Left := ScaleX(20);
  Text.Top := Radio.Top + Radio.Height + ScaleY(2);
  Text.Width := InstallTypePage.SurfaceWidth - Text.Left;
  Text.WordWrap := True;
  Text.AutoSize := True;
  Text.Caption := Description;

  Result := Text.Top + Text.Height + ScaleY(16);
end;

<event('InitializeWizard')>
procedure InstallTypeInitializeWizard();
var
  Top: Integer;
begin
  InstallTypeInitialDir := WizardForm.DirEdit.Text;
  InstallTypeInitialGroup := WizardForm.GroupEdit.Text;

  InstallTypePage := CreateCustomPage(wpLicense, 'Installation Type',
    'How should {#MyAppName} {#MyAppVersion} be installed?');

  Top := InstallTypeAddOption('&Standard installation (recommended)',
    'Installs into the {#MyAppName} folder and replaces any previously installed version. ' +
    'Start Menu shortcuts are not versioned, so they always open the most recently installed version.',
    0, InstallTypeStandardRadio);
  InstallTypeAddOption('&Version-specific installation',
    'Installs into a "{#MyAppName} {#MyAppVersion}" folder with its own versioned Start Menu ' +
    'shortcuts. A newer version never replaces it, so several versions can be kept side by side ' +
    'and each one uninstalls separately.',
    Top, InstallTypeVersionedRadio);

  InstallTypeVersionedRadio.Checked := InstallTypeParamIsVersioned();
  InstallTypeStandardRadio.Checked := not InstallTypeVersionedRadio.Checked;
  { Silent installs never visit the page, so apply the command-line choice now. }
  InstallTypeApply();
end;

<event('NextButtonClick')>
function InstallTypeNextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if (InstallTypePage <> nil) and (CurPageID = InstallTypePage.ID) then
    InstallTypeApply();
end;

{ Requires DisableDirPage=no in [Setup]; see the header. }
<event('ShouldSkipPage')>
function InstallTypeShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (PageID = wpSelectDir) and (not IsVersionedInstall()) and
            (WizardForm.PrevAppDir <> '');
end;
