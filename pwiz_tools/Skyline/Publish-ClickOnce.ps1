<#
.SYNOPSIS
    Publish the net8 Skyline ClickOnce deployment into pwiz_tools\Skyline\publish.

.DESCRIPTION
    .NET 5+ ClickOnce is publish-profile driven and, unlike an ordinary build, it cannot be
    produced by `dotnet publish` - only MSBuild implements the ClickOnce publish protocol. So
    this script locates Visual Studio's MSBuild via vswhere and runs

        msbuild Skyline.csproj -t:Publish -p:PublishProfile=ClickOnceProfile

    Everything else - install URL, signing, framework-dependent vs self-contained - is in
    Properties\PublishProfiles\ClickOnceProfile.pubxml. Read the identity note at the top of
    that file before changing the URL or the certificate.

    The output tree (Skyline-daily.application, Launcher.exe, "Application Files\...") is left
    in pwiz_tools\Skyline\publish for manual upload; nothing here touches the web server.

.PARAMETER Configuration
    Release (default) or Debug.

.PARAMETER PfxPassword
    Password for SkylineDailyPreviewSelfSigned.pfx. Only needed the first time on a machine:
    MSBuild signs from the certificate store, so the pfx is imported into CurrentUser\My if the
    thumbprint named by the profile is not already there. Omit it once the cert is imported.

.PARAMETER SkipBuild
    Publish without rebuilding (-p:NoBuild=true). Only valid when the net8 output is already
    current; ClickOnce hashes every deployed file, so a stale build publishes a stale manifest.

.PARAMETER AgreeToVendorLicenses
    Acknowledge the vendor SDK EULAs (-p:IAgreeToVendorLicenses=true), same as build.bat's
    --i-agree-to-the-vendor-licenses. Without it the vendor readers build in their
    no-vendor-support mode, so the published deployment cannot open instrument files - fine for
    checking the deployment plumbing, not for a preview anyone will actually use.

.PARAMETER SkipHardklor
    Skip the native Hardklor.exe build. The publish then ships no Hardklor.exe and the
    Hardklor/Bullseye feature-detection pipeline fails in the installed app, so use this only
    when Hardklor is already built and current.

.EXAMPLE
    .\Publish-ClickOnce.ps1 -PfxPassword skyline

.EXAMPLE
    .\Publish-ClickOnce.ps1 -Configuration Debug
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string] $Configuration = 'Release',
    [string] $PfxPassword,
    [switch] $SkipBuild,
    [switch] $AgreeToVendorLicenses,
    [switch] $SkipHardklor
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $scriptDir 'Skyline.csproj'
$profileName = 'ClickOnceProfile'
$profilePath = Join-Path $scriptDir "Properties\PublishProfiles\$profileName.pubxml"
$publishDir = Join-Path $scriptDir 'publish'

if (-not (Test-Path $profilePath)) {
    throw "Publish profile not found: $profilePath"
}

# --- Signing certificate -------------------------------------------------------------
# MSBuild's SignFile task signs from the certificate store, by thumbprint, not from the .pfx.
# Read the thumbprint the profile asks for rather than duplicating it here, so the two cannot
# disagree.
$thumbprint = ([xml](Get-Content $profilePath)).Project.PropertyGroup.ManifestCertificateThumbprint
if ($thumbprint) {
    $thumbprint = $thumbprint.Trim()
    $installed = Get-ChildItem Cert:\CurrentUser\My |
        Where-Object { $_.Thumbprint -eq $thumbprint -and $_.HasPrivateKey }
    if (-not $installed) {
        $pfx = Join-Path $scriptDir 'SkylineDailyPreviewSelfSigned.pfx'
        if (-not $PfxPassword) {
            throw ("Signing certificate $thumbprint is not in CurrentUser\My. Re-run with " +
                   "-PfxPassword <password> to import it from $pfx, or import it by hand.")
        }
        if (-not (Test-Path $pfx)) {
            throw "Signing certificate $thumbprint is not in CurrentUser\My and $pfx does not exist."
        }
        Write-Host "Importing $pfx into CurrentUser\My ..."
        $secure = ConvertTo-SecureString -String $PfxPassword -AsPlainText -Force
        $imported = Import-PfxCertificate -FilePath $pfx -CertStoreLocation Cert:\CurrentUser\My -Password $secure
        if ($imported.Thumbprint -ne $thumbprint) {
            throw ("$pfx has thumbprint $($imported.Thumbprint) but the profile asks for " +
                   "$thumbprint. Signing with the wrong certificate changes the ClickOnce " +
                   "publicKeyToken, which breaks upgrades from already-installed clients.")
        }
    }
    Write-Host "Signing manifests with $thumbprint ($((Get-ChildItem Cert:\CurrentUser\My | Where-Object Thumbprint -eq $thumbprint).Subject))"
}

# --- MSBuild -------------------------------------------------------------------------
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) {
    throw "vswhere.exe not found at $vswhere; Visual Studio is required to publish ClickOnce."
}
$vsInstall = & $vswhere -latest -products * -property installationPath
if (-not $vsInstall) { throw 'No Visual Studio installation found.' }
$msbuild = Join-Path $vsInstall 'MSBuild\Current\Bin\amd64\MSBuild.exe'
if (-not (Test-Path $msbuild)) {
    $msbuild = Join-Path $vsInstall 'MSBuild\Current\Bin\MSBuild.exe'
}
if (-not (Test-Path $msbuild)) { throw "MSBuild.exe not found under $vsInstall." }

# --- Bootstrapper package staging -----------------------------------------------------
# setup.exe needs the .NET 8 Desktop Runtime prerequisite, and GenerateBootstrapper resolves both
# its engine and its packages from a SINGLE root ($(GenerateBootstrapperSdkPath)). No such root
# exists on disk: the engine (Engine\setup.bin) and the .NET Framework-era packages live under
# "%ProgramFiles(x86)%\Microsoft SDKs\ClickOnce Bootstrapper\", while the .NET 8 package ships
# with Visual Studio under MSBuild\Microsoft\VisualStudio\BootstrapperPackages. Point the task at
# either one alone and it fails - MSB3155 (package not found) or MSB3147 (setup.bin not found).
# So stage a combined root under obj\. Under 1 MB, and it avoids writing into Program Files.
$bootstrapperRoot = Join-Path $scriptDir 'obj\clickonce-bootstrapper'
$legacyRoot = Join-Path ${env:ProgramFiles(x86)} 'Microsoft SDKs\ClickOnce Bootstrapper'
$vsPackages = Join-Path $vsInstall 'MSBuild\Microsoft\VisualStudio\BootstrapperPackages'
if (-not (Test-Path (Join-Path $legacyRoot 'Engine\setup.bin'))) {
    throw "ClickOnce bootstrapper engine not found at $legacyRoot\Engine\setup.bin."
}
if (-not (Test-Path $vsPackages)) {
    throw "Visual Studio bootstrapper packages not found at $vsPackages (needed for the .NET 8 Desktop Runtime prerequisite)."
}
Write-Host "Staging bootstrapper root at $bootstrapperRoot ..."
if (Test-Path $bootstrapperRoot) { Remove-Item $bootstrapperRoot -Recurse -Force }
New-Item -ItemType Directory -Path (Join-Path $bootstrapperRoot 'Packages') -Force | Out-Null
Copy-Item (Join-Path $legacyRoot 'Engine')  $bootstrapperRoot -Recurse -Force
Copy-Item (Join-Path $legacyRoot 'Schemas') $bootstrapperRoot -Recurse -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $legacyRoot 'Packages\*') (Join-Path $bootstrapperRoot 'Packages') -Recurse -Force
# VS packages last so a newer copy of a same-named package wins over the legacy SDK's.
Copy-Item (Join-Path $vsPackages '*') (Join-Path $bootstrapperRoot 'Packages') -Recurse -Force

# --- Native Hardklor -----------------------------------------------------------------
# Hardklor.exe is C++ and cannot be built by the .NET SDK, so build.bat builds it in a separate
# VS MSBuild step and Skyline.csproj deploys it through an Exists()-conditioned Content include.
# That condition makes its absence silent: without this step the publish simply ships no
# Hardklor.exe and the feature-detection pipeline fails at run time in the installed app.
if (-not $SkipHardklor) {
    $hardklor = Join-Path $scriptDir 'Executables\Hardklor\Hardklor.vcxproj'
    if (Test-Path $hardklor) {
        Write-Host "Building native Hardklor.exe ($Configuration|x64) ..."
        & $msbuild $hardklor "-p:Configuration=$Configuration" -p:Platform=x64 -m -nologo -v:minimal -nodeReuse:false
        if ($LASTEXITCODE -ne 0) { throw "MSBuild Hardklor.vcxproj failed (exit $LASTEXITCODE)." }
    } else {
        Write-Warning "$hardklor not found; publishing without Hardklor.exe."
    }
}

if (Test-Path $publishDir) {
    # ClickOnce keeps one "Application Files\<name>_<version>" folder per published version and
    # never prunes; wipe so what is uploaded is exactly what was just built.
    # Clear the CONTENTS, not the folder. Removing the folder itself fails with "being used by
    # another process" whenever anything has it as its working directory - a shell or an Explorer
    # window left open there is enough - and the publish would then abort for no real reason.
    Write-Host "Clearing $publishDir ..."
    Get-ChildItem $publishDir -Force | Remove-Item -Recurse -Force
}

# TargetFramework has to be on the command line even though the profile also sets it: Skyline
# multi-targets, and the SDK's cross-targeting check (NETSDK1129) runs before the publish profile
# is imported, so it sees a project with two TFMs and no choice made.
$msbuildArgs = @(
    $project,
    '-t:Publish',
    "-p:PublishProfile=$profileName",
    '-p:TargetFramework=net8.0-windows',
    "-p:Configuration=$Configuration",
    '-nologo',
    '-v:minimal',
    '-nodeReuse:false',
    # Trailing separator matters: the task appends "Engine\" and "Packages\" to this.
    "-p:GenerateBootstrapperSdkPath=$bootstrapperRoot\"
)
if ($SkipBuild) { $msbuildArgs += '-p:NoBuild=true' }
if ($AgreeToVendorLicenses) { $msbuildArgs += '-p:IAgreeToVendorLicenses=true' }

# RID-specific restore first. ClickOnce publishes RID-specific (win-x64), and an ordinary
# `dotnet build` restore writes only a RID-agnostic target into project.assets.json, so the
# publish fails with NETSDK1047 ("doesn't have a target for net8.0-windows/win-x64"). Restoring
# here rather than adding <RuntimeIdentifiers> to Skyline.csproj keeps the normal build's
# restore untouched - note this means publishing from Visual Studio's Publish tool needs the
# same restore run once by hand.
# Deliberately no -p:TargetFramework here. Restore walks the whole ProjectReference graph, and a
# global TargetFramework reaches every project in it - including the pwiz-sharp projects, which
# target plain net8.0. Pinning it rewrites their project.assets.json with a net8.0-windows target
# only, and the build then fails with NETSDK1005 "doesn't have a target for 'net8.0'".
# IAgreeToVendorLicenses has to be set for the restore too, not just the build: it gates whole
# ProjectReferences (Waters, and Sciex's OfxLoggingStub), so a restore without it leaves those
# projects with no project.assets.json and the build fails with NETSDK1004.
$restoreArgs = @(
    $project,
    '-t:Restore',
    '-p:RuntimeIdentifier=win-x64',
    "-p:Configuration=$Configuration",
    '-nologo',
    '-v:minimal',
    '-nodeReuse:false'
)
if ($AgreeToVendorLicenses) { $restoreArgs += '-p:IAgreeToVendorLicenses=true' }
Write-Host "$msbuild $($restoreArgs -join ' ')"
& $msbuild @restoreArgs
if ($LASTEXITCODE -ne 0) { throw "Restore for win-x64 failed (exit $LASTEXITCODE)." }

Write-Host "$msbuild $($msbuildArgs -join ' ')"
& $msbuild @msbuildArgs
if ($LASTEXITCODE -ne 0) { throw "ClickOnce publish failed (exit $LASTEXITCODE)." }

# --- Report what was produced ---------------------------------------------------------
# The deployment identity is the thing most likely to be silently wrong (a changed assembly
# name, architecture or certificate all break upgrades from installed clients), so print it
# rather than making the developer go read the XML.
$application = Get-ChildItem $publishDir -Filter *.application -ErrorAction SilentlyContinue | Select-Object -First 1
if ($application) {
    $xml = [xml](Get-Content $application.FullName)
    $id = $xml.assembly.assemblyIdentity
    # A deployment with blank InstallUrl/UpdateUrl has no <deploymentProvider> element at all -
    # that absence is what lets it install from any folder it was extracted into. StrictMode
    # makes reading the missing property a terminating error, so probe for it rather than
    # dereferencing it, or every ZIP-style publish dies here AFTER doing all the work.
    $deployment = $xml.assembly.deployment
    $providerUrl = if ($deployment.PSObject.Properties['deploymentProvider']) {
        $deployment.deploymentProvider.codebase
    } else { $null }
    $provider = if ($providerUrl) { $providerUrl } else { '(none - installs from the folder it is run out of)' }
    Write-Host ''
    Write-Host "Published $($application.FullName)"
    Write-Host "  identity  $($id.name) $($id.version)"
    Write-Host "  token     $($id.publicKeyToken)   arch $($id.processorArchitecture)   culture $($id.language)"
    Write-Host "  provider  $provider"
    # Install page. Visual Studio generates one of these (publish.htm) as part of ITS publish step;
    # MSBuild does not, so a command-line publish leaves the folder as a bare directory listing and
    # there is nothing obvious to click. Written here so the URL behaves the way the existing
    # previews do. Values come from the manifest just parsed, so the page cannot drift from it.
    $product = ([xml](Get-Content $profilePath)).Project.PropertyGroup.ProductName
    $publisher = ([xml](Get-Content $profilePath)).Project.PropertyGroup.PublisherName
    $setupName = if (Test-Path (Join-Path $publishDir 'setup.exe')) { 'setup.exe' } else { $application.Name }
    $html = @"
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>$product</title>
<style>
 body { font-family: Segoe UI, sans-serif; margin: 3em auto; max-width: 40em; color: #222; }
 dt { font-weight: 600; float: left; width: 7em; clear: left; }
 dd { margin: 0 0 .4em 7em; }
 .install { display: inline-block; margin: 1.5em 0; padding: .6em 2em; background: #0a5; color: #fff;
            text-decoration: none; border-radius: 4px; font-size: 1.1em; }
 .note { color: #555; font-size: .9em; }
</style>
</head>
<body>
<h1>$product</h1>
<dl>
  <dt>Name</dt><dd>$product</dd>
  <dt>Version</dt><dd>$($id.version)</dd>
  <dt>Publisher</dt><dd>$publisher</dd>
</dl>
<p>Requires the <b>.NET Desktop Runtime 8.0 (x64)</b>. Installing through the button below will
   install it first if it is missing.</p>
<p><a class="install" href="$setupName">Install</a></p>
<p class="note">Already have the .NET 8 Desktop Runtime? You can install straight from
   <a href="$($application.Name)">$($application.Name)</a>.</p>
<p class="note">Signed with a self-signed certificate, so Windows will show an unknown publisher
   warning. This is a preview build of Skyline running on .NET 8.</p>
</body>
</html>
"@
    $indexPath = Join-Path $publishDir 'index.html'
    Set-Content -Path $indexPath -Value $html -Encoding UTF8
    Write-Host "  page      $indexPath"
    Write-Host ''
    if ($providerUrl) {
        Write-Host 'Copy the whole publish folder to the install URL above.'
    } else {
        Write-Host 'No deploymentProvider: ZIP the publish folder, extract it anywhere, run setup.exe.'
    }
} else {
    Write-Warning "No .application found in $publishDir - did the publish actually run?"
}
