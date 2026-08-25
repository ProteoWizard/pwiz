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
    Write-Host "Clearing $publishDir ..."
    Remove-Item $publishDir -Recurse -Force
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
    '-nodeReuse:false'
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
    $provider = $xml.assembly.deployment.deploymentProvider.codebase
    Write-Host ''
    Write-Host "Published $($application.FullName)"
    Write-Host "  identity  $($id.name) $($id.version)"
    Write-Host "  token     $($id.publicKeyToken)   arch $($id.processorArchitecture)   culture $($id.language)"
    Write-Host "  provider  $provider"
    Write-Host ''
    Write-Host 'Copy the whole publish folder to the install URL above.'
} else {
    Write-Warning "No .application found in $publishDir - did the publish actually run?"
}
