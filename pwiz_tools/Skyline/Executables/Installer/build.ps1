<#
.SYNOPSIS
Build the Skyline / Skyline-daily installer (Inno Setup) from a .NET build of Skyline.

.DESCRIPTION
Packaging pipeline on top of the Skyline build (build.bat, or Skyline.csproj built
Release x64 from Visual Studio):
  1. Locate the Skyline build output (bin\x64\<Config>\net10.0-windows) and read the
     channel (Skyline.exe or Skyline-daily.exe) and version off the exe.
  2. Stage a filtered copy: everything the build put next to Skyline except the
     NuGet doc-comment XML files, the non-Windows native runtimes and any stray
     RID-named publish folder.
  3. Settle where this build installs from: the InstallUrl application setting in
     the staged <channel>.dll.config, overwritten by -InstallUrl for a private
     build. Write the update manifest there too, the small JSON file Skyline's
     startup check reads to learn the published version; it is published beside
     the installer, under the installer's name with a .json extension.
  4. Make sure the .NET 10 desktop runtime installer EXE is cached (shared with
     the pwiz-sharp installer under pwiz-sharp\installer\cache\).
  5. Compile Setup.iss twice: the default variant bundling the runtime and the
     NoNetRuntime variant that only checks for it.
  6. Report, including the two URLs to upload the manifest and the installer to.

The installer itself is described in Setup.iss. Test-Installer.ps1 exercises a built
installer end to end (silent install, SkylineCmd smoke, uninstall).

.PARAMETER Configuration
Which Skyline build to package. Release by default.

.PARAMETER SkylineBinDir
The Skyline build output directory to package. Default: the newest of
..\..\bin\x64\<Configuration>\net10.0-windows and ..\..\bin\<Configuration>\net10.0-windows.

.PARAMETER OutputDir
Where the installers land. Default ..\..\bin\installer (gitignored).

.PARAMETER InstallUrl
Where the installed Skyline was installed from and checks for a newer version: the URL of
the bundled installer as published, e.g.
  https://proteome.gs.washington.edu/~nicksh/SpecialSkylines/Skyline-daily-Setup.exe
A {0} in it stands for the channel (Skyline or Skyline-daily). Written into the staged
<channel>.dll.config, so the installed copy carries it. Default: the value app.config
compiled into the build, which is the official location.

.PARAMETER SignToolCommand
A complete signtool command line, e.g.
  'signtool sign /csp "DigiCert Signing Manager KSP" /kc <key> /f <cert> /tr http://timestamp.digicert.com /td SHA256 /fd SHA256 $f'
where $f stands for the (quoted) file to sign; a literal $ must be written $$. Inno
signs the uninstaller and the Setup.exe with it. Double quotes are translated to
Inno's $q placeholder here, since ISCC's own command line cannot carry them.
Unsigned when omitted.

.USAGE
    pwsh -File pwiz_tools/Skyline/Executables/Installer/build.ps1
    pwsh -File pwiz_tools/Skyline/Executables/Installer/build.ps1 -SkylineBinDir C:\other\bin\x64\Release\net10.0-windows
    pwsh -File pwiz_tools/Skyline/Executables/Installer/build.ps1 -InstallUrl https://example.org/skylines/Skyline-daily-Setup.exe
#>
#requires -Version 7.0
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')] [string] $Configuration = 'Release',
    [string] $SkylineBinDir,
    [string] $OutputDir,
    [string] $InstallUrl,
    [string] $SignToolCommand
)
$ErrorActionPreference = 'Stop'

$installerDir = $PSScriptRoot
$skylineDir   = (Resolve-Path (Join-Path $installerDir '..\..')).Path
$repoRoot     = (Resolve-Path (Join-Path $skylineDir '..\..')).Path
$pwizSharpInstaller = Join-Path $repoRoot 'pwiz-sharp\installer'
if (-not $OutputDir) { $OutputDir = Join-Path $skylineDir 'bin\installer' }
# Absolute, because it is handed to ISCC, which resolves relative paths against the script.
$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
$stagingDir = Join-Path $OutputDir 'stage'
$cacheDir   = Join-Path $pwizSharpInstaller 'cache'

# 1. Skyline build output. Platform-aware and newest wins, like Stage-Tests.ps1: Visual
#    Studio and build.bat write bin\x64\<Config>\<TFM>, a plain "dotnet build" bin\<Config>\<TFM>.
if (-not $SkylineBinDir) {
    $candidates = @(
        (Join-Path $skylineDir "bin\x64\$Configuration\net10.0-windows"),
        (Join-Path $skylineDir "bin\$Configuration\net10.0-windows")
    ) | Where-Object { Test-Path $_ }
    $SkylineBinDir = $candidates |
        Sort-Object { (Get-ChildItem $_ -Filter 'Skyline*.exe' | Measure-Object LastWriteTimeUtc -Maximum).Maximum } -Descending |
        Select-Object -First 1
    if (-not $SkylineBinDir) {
        throw "No Skyline build output found under $skylineDir\bin for $Configuration. Run build.bat --build-only first."
    }
}
# Trimmed: Resolve-Path keeps a trailing separator, and the relative paths staged below
# are cut at this length.
$SkylineBinDir = (Resolve-Path $SkylineBinDir).Path.TrimEnd('\', '/')

# The channel is whichever exe the build produced (MSBuildAssemblyName picks it).
$channelExe = @('Skyline-daily.exe', 'Skyline.exe') | Where-Object { Test-Path (Join-Path $SkylineBinDir $_) } | Select-Object -First 1
if (-not $channelExe) {
    throw "Neither Skyline-daily.exe nor Skyline.exe is in $SkylineBinDir; is this a Skyline build output directory?"
}
$appName = [System.IO.Path]::GetFileNameWithoutExtension($channelExe)
foreach ($required in @('SkylineCmd.exe', 'msconvert.exe', 'BlibBuild.exe')) {
    if (-not (Test-Path (Join-Path $SkylineBinDir $required))) {
        throw "$required is missing from $SkylineBinDir - the Skyline build is incomplete."
    }
}

# Version off the exe: FileVersion is the numeric YY.N.B.DDD that SkylineVersion.targets
# stamps, ProductVersion the informational one with the git hash and build kind.
$versionInfo = (Get-Item (Join-Path $SkylineBinDir $channelExe)).VersionInfo
$appVersion = $versionInfo.FileVersion.Trim()
$informationalVersion = $versionInfo.ProductVersion.Trim()
if ($appVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw "$channelExe has FileVersion '$appVersion', not the YY.N.B.DDD Skyline stamps; was it built from SkylineVersion.targets?"
}
Write-Host "==> $appName $appVersion ($informationalVersion) from $SkylineBinDir" -ForegroundColor Cyan

# 2. Stage a filtered copy. Everything Skyline builds next to itself ships - vendor
#    readers, BlibBuild, msconvert, Hardklor, the localized satellites - except:
#      <name>.xml       NuGet doc-comment file beside a <name>.dll / <name>.exe. Data XML
#                       with no such sibling (unimod.xml, modifications.xml, which BlibBuild
#                       reads from its own directory) ships.
#      runtimes\<rid>   native packages' cross-platform copies; x64 Windows keeps win-x64 + win
#      <rid>\           a self-contained publish folder at the bin root (a stray
#                       `dotnet publish -r win-x64` of a referenced project, swept in by a
#                       Content glob); never part of a framework-dependent build
function Should-Skip([System.IO.FileInfo] $file, [string] $relName) {
    if ($relName -match '\.xml$') {
        $base = [System.IO.Path]::Combine($file.DirectoryName, [System.IO.Path]::GetFileNameWithoutExtension($file.Name))
        if ((Test-Path "$base.dll") -or (Test-Path "$base.exe")) { return $true }
    }
    if ($relName -match '^runtimes[\\/](?!win-x64[\\/]|win[\\/])') { return $true }
    if ($relName -match '^(win|linux|osx)-[^\\/]+[\\/]') { return $true }
    return $false
}

Write-Host "`n==> stage payload" -ForegroundColor Cyan
if (Test-Path $stagingDir) { Remove-Item $stagingDir -Recurse -Force }
New-Item -ItemType Directory $stagingDir -Force | Out-Null
$copied = 0; $bytesCopied = 0L; $bytesSkipped = 0L
Get-ChildItem $SkylineBinDir -Recurse -File | ForEach-Object {
    $rel = $_.FullName.Substring($SkylineBinDir.Length + 1)
    if (Should-Skip $_ $rel) { $bytesSkipped += $_.Length; return }
    $dest = Join-Path $stagingDir $rel
    $destDir = Split-Path -Parent $dest
    if (-not (Test-Path $destDir)) { New-Item -ItemType Directory $destDir -Force | Out-Null }
    Copy-Item $_.FullName $dest
    $copied++; $bytesCopied += $_.Length
}
Write-Host "    $copied files ($([math]::Round($bytesCopied/1MB, 1)) MB), $([math]::Round($bytesSkipped/1MB, 1)) MB skipped"
foreach ($dataXml in @('unimod.xml', 'modifications.xml')) {
    if (-not (Test-Path (Join-Path $stagingDir $dataXml))) { throw "$dataXml did not make it into the stage; BlibBuild needs it." }
}
if (Test-Path (Join-Path $stagingDir 'coreclr.dll')) {
    throw "The stage contains coreclr.dll: $SkylineBinDir looks like a self-contained publish, not the framework-dependent build the installer expects."
}

# 3. Install URL and update manifest. Skyline reads InstallUrl from the config beside
#    its exe (the channel name replaces the {0}), downloads a newer installer from it,
#    and reads the manifest from that URL with the extension changed to .json. A
#    private build gets its own URL written into the staged config here, and the
#    manifest is named from whatever the staged config ends up saying, so the
#    installed Skyline and the published files agree by construction.
Write-Host "`n==> install URL" -ForegroundColor Cyan
$configPath = Join-Path $stagingDir "$appName.dll.config"
[xml] $config = Get-Content $configPath
$urlNode = $config.SelectSingleNode("/configuration/applicationSettings/pwiz.Skyline.Properties.Settings/setting[@name='InstallUrl']/value")
if (-not $urlNode) {
    throw "InstallUrl is not in $configPath; the update manifest cannot be named."
}
if ($InstallUrl) {
    $urlNode.InnerText = $InstallUrl
    $config.Save($configPath)
}
$installUrl = $urlNode.InnerText -f $appName
$manifestUrl = [System.IO.Path]::ChangeExtension(([uri] $installUrl).GetLeftPart([System.UriPartial]::Path), '.json')
$manifestPath = Join-Path $OutputDir ([System.IO.Path]::GetFileName($manifestUrl))
if (-not (Test-Path $OutputDir)) { New-Item -ItemType Directory $OutputDir -Force | Out-Null }
Set-Content -Path $manifestPath -Value (@{ version = $appVersion } | ConvertTo-Json)
Write-Host "    $installUrl$(if ($InstallUrl) { ' (from -InstallUrl)' })"
Write-Host "    manifest $manifestPath"

# 4. The .NET 10 desktop runtime EXE, cached beside the pwiz-sharp installer so the two
#    products share one download. The aka.ms URL redirects to the latest 10.0.x.
$dotnetRuntimeUrl = 'https://aka.ms/dotnet/10.0/windowsdesktop-runtime-win-x64.exe'
$dotnetExe = Join-Path $cacheDir 'windowsdesktop-runtime-10.0-win-x64.exe'
Write-Host "`n==> .NET 10 desktop runtime (cached)" -ForegroundColor Cyan
if (-not (Test-Path $cacheDir)) { New-Item -ItemType Directory $cacheDir -Force | Out-Null }
if (-not (Test-Path $dotnetExe)) {
    Write-Host "    downloading $dotnetRuntimeUrl"
    Invoke-WebRequest -Uri $dotnetRuntimeUrl -OutFile $dotnetExe
}
Write-Host "    $([math]::Round((Get-Item $dotnetExe).Length / 1MB, 1)) MB at $dotnetExe"

# 5. ISCC, bootstrapped by the shared Ensure-InnoSetup.ps1 when the machine lacks it.
$ensure = Join-Path $pwizSharpInstaller 'Ensure-InnoSetup.ps1'
$iscc = & pwsh -NoProfile -File $ensure -PassThru | Select-Object -Last 1
if ($LASTEXITCODE -ne 0 -or -not $iscc -or -not (Test-Path $iscc)) {
    throw "Inno Setup (ISCC.exe) is not available; cannot build the installer."
}

$iss = Join-Path $installerDir 'Setup.iss'
function Invoke-Iscc {
    param(
        [string] $OutputBaseFilename,
        [string[]] $ExtraDefines = @()
    )
    Write-Host "`n==> ISCC compile: $OutputBaseFilename" -ForegroundColor Cyan
    $isccArgs = @(
        '/Q',
        "/DSkylineAppName=$appName",
        "/DMyAppVersion=$appVersion",
        "/DMyAppInformationalVersion=$informationalVersion",
        "/DStagingDir=$stagingDir",
        "/DOutputDir=$OutputDir",
        "/DOutputBaseFilename=$OutputBaseFilename",
        "/DDotNetRuntimeExePath=$dotnetExe"
    ) + $ExtraDefines
    if ($SignToolCommand) {
        $isccArgs += @('/DSignSetup', "/Sskylinesign=$($SignToolCommand.Replace('"', '$q'))")
    }
    $isccArgs += $iss
    & $iscc @isccArgs
    if ($LASTEXITCODE -ne 0) { throw "ISCC compile failed for $OutputBaseFilename (exit $LASTEXITCODE)" }
}

$bundledName = "$appName-Setup-$appVersion"
$lightName   = "$appName-NoNetRuntime-Setup-$appVersion"
Invoke-Iscc -OutputBaseFilename $bundledName
Invoke-Iscc -OutputBaseFilename $lightName -ExtraDefines @('/DNoNetRuntime')

# 6. Report.
Write-Host ""
foreach ($base in @($bundledName, $lightName)) {
    $setupPath = Join-Path $OutputDir "$base.exe"
    if (-not (Test-Path $setupPath)) {
        Write-Host "MISSING: $setupPath" -ForegroundColor Red
        continue
    }
    $size = [math]::Round((Get-Item $setupPath).Length / 1MB, 1)
    $hash = (Get-FileHash -Path $setupPath -Algorithm SHA256).Hash
    Write-Host "Setup:   $setupPath" -ForegroundColor Green
    Write-Host "Version: $appVersion"
    Write-Host "Size:    $size MB"
    Write-Host "SHA-256: $hash"
    Write-Host ""
}
Write-Host "Update manifest: $manifestPath" -ForegroundColor Green
Write-Host "To publish this version, upload:"
Write-Host "    $manifestPath"
Write-Host "        as $manifestUrl"
Write-Host "    $(Join-Path $OutputDir "$bundledName.exe")"
Write-Host "        as $installUrl"
