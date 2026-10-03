; -----------------------------------------------------------------------------
; .NET Desktop Runtime prerequisite, shared by the framework-dependent installers
; (ProteoWizard-Sharp, Skyline). Include it from the product script AFTER the
; product's own [Files] section and BEFORE its [Run] section, so the runtime is
; installed before any "launch the app" [Run] entry executes.
;
; The product script must define, before the #include:
;   DotNetMajor           "10" - the major version the product targets
;   DotNetRuntimeExePath  path of the cached windowsdesktop-runtime-<major>.0-win-x64.exe
;                         (absolute, or relative to the product script's directory)
;   ProductDisplayName    name used in the "runtime is missing" message
;   NoNetRuntime          (optional) build the lightweight variant that bundles no runtime
;
; Two variants come out of one script:
;   default        Bundles the runtime installer. If .NET <major> is missing at install
;                  time the bundled EXE runs silently (its per-machine install raises UAC).
;   NoNetRuntime   Bundles nothing. InitializeSetup aborts with a download link if
;                  .NET <major> is missing. For users who manage their own runtime.
; -----------------------------------------------------------------------------

#ifndef DotNetMajor
  #error DotNetMajor must be defined before including DotNetDesktopRuntime.iss
#endif
#ifndef ProductDisplayName
  #error ProductDisplayName must be defined before including DotNetDesktopRuntime.iss
#endif
#ifndef NoNetRuntime
  #ifndef DotNetRuntimeExePath
    #error DotNetRuntimeExePath must be defined before including DotNetDesktopRuntime.iss
  #endif
  #define DotNetRuntimeExeName ExtractFileName(DotNetRuntimeExePath)
#endif

[Files]
#ifndef NoNetRuntime
; Extracted to {tmp} only when the runtime is missing, deleted after install.
; Bundling (rather than DownloadTemporaryFile) keeps offline installs working.
; nocompression: the EXE is a compressed bundle already; LZMA over it costs seconds
; per compile for nothing.
Source: "{#DotNetRuntimeExePath}"; DestDir: "{tmp}"; \
    Flags: deleteafterinstall nocompression; Check: not IsDotNetDesktopInstalled
#endif

[Run]
#ifndef NoNetRuntime
; /install /quiet /norestart are Microsoft's documented silent-install flags. The
; runtime always installs per-machine (%ProgramFiles%\dotnet\), so this step raises
; UAC even for a per-user product install; shellexec + the runas verb the EXE's
; manifest requests surfaces that prompt at the right moment rather than at start.
Filename: "{tmp}\{#DotNetRuntimeExeName}"; \
    Parameters: "/install /quiet /norestart"; \
    StatusMsg: "Installing .NET {#DotNetMajor} desktop runtime..."; \
    Flags: waituntilterminated shellexec; \
    Check: not IsDotNetDesktopInstalled
#endif

[Code]
{ The x64 runtime is installed iff a <major>.* directory exists under
  <root>\shared\Microsoft.WindowsDesktop.App\ - that is what the x64 apphost walks
  at startup, so it is the authoritative signal regardless of how the runtime got
  there (runtime EXE, dotnet-install.ps1, enterprise MSI). <root> is resolved the
  way the apphost resolves it: DOTNET_ROOT_X64, then DOTNET_ROOT, then the default
  install location, which on an ARM64 host is dotnet\x64\ (dotnet\ holds the ARM64
  runtime there, which cannot run this x64 product). }

function DotNetDesktopRuntimeRoot(): String;
begin
  Result := GetEnv('DOTNET_ROOT_X64');
  if Result = '' then
    Result := GetEnv('DOTNET_ROOT');
  if Result = '' then
  begin
    if IsArm64 then
      Result := ExpandConstant('{commonpf64}\dotnet\x64')
    else
      Result := ExpandConstant('{commonpf64}\dotnet');
  end;
end;

function IsDotNetDesktopInstalled(): Boolean;
var
  baseDir: String;
  rec: TFindRec;
  found: Boolean;
begin
  Result := False;
  baseDir := AddBackslash(DotNetDesktopRuntimeRoot()) + 'shared\Microsoft.WindowsDesktop.App';
  if not DirExists(baseDir) then
    Exit;
  found := FindFirst(baseDir + '\*', rec);
  try
    while found do
    begin
      if ((rec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0) and
         (Copy(rec.Name, 1, Length('{#DotNetMajor}.')) = '{#DotNetMajor}.') then
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

#ifdef NoNetRuntime
{ Lightweight variant: nothing can install the runtime for the user, so stop with
  a clear message and a download link instead of failing at first launch. A silent
  run (/SUPPRESSMSGBOXES) gets the Cancel default and exits non-zero without a
  browser window. }
<event('InitializeSetup')>
function DotNetRuntimeInitializeSetup(): Boolean;
var
  rc: Integer;
begin
  Result := True;
  if not IsDotNetDesktopInstalled() then
  begin
    rc := SuppressibleMsgBox(
      '{#ProductDisplayName} requires the .NET {#DotNetMajor} Desktop Runtime (x64), which is not installed on this machine.' + #13#10#13#10 +
      'Click OK to open the Microsoft download page in your browser, then re-run this installer after installing the runtime.' + #13#10#13#10 +
      'If you prefer an installer that bundles the runtime, use the full {#ProductDisplayName} installer instead.',
      mbError, MB_OKCANCEL, IDCANCEL);
    if (rc = IDOK) and not WizardSilent then
      ShellExec('', 'https://dotnet.microsoft.com/download/dotnet/{#DotNetMajor}.0/runtime',
                '', '', SW_SHOW, ewNoWait, rc);
    Result := False;
  end;
end;
#endif
