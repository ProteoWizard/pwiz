<#
.SYNOPSIS
    Check the pwiz-sharp build for warnings, failing the TeamCity build without stopping it.

.DESCRIPTION
    The pwiz-sharp counterpart of pwiz_tools/Skyline/tcinspect.ps1. For now it does only that
    script's build-warning half; ReSharper inspection of Pwiz.sln can be added here later.

    tcbuild.bat calls this after clean.bat and before build.bat, so its pre-build is the first
    compile of the build and build.bat builds on top of it incrementally. That makes it the one
    place every compiler warning is emitted - an up-to-date project re-emits none - and the
    pre-build logs every restore and build warning to a warnings-only log. Any warning raises a
    TeamCity buildProblem, which marks the build failed while this script still exits 0, so
    build.bat and its tests still run. The Directory.Build.props files do not promote warnings
    to errors, because a promoted warning stops the build before any test runs - including one
    the commit did not cause, such as a NuGet advisory published overnight.

    Always exits 0. A pre-build that fails to compile is left for build.bat to report.

    The warnings log is written outside the checkout by default. tcbuild.bat fails the build
    when 'git status --porcelain' is non-empty at the end.

.EXAMPLE
    pwsh -File .\tcinspect.ps1 -IAgreeToVendorLicenses
#>
[CmdletBinding()]
param(
    # These mirror build.bat's flags, which turn into its MSBUILD_PROPS. Keep the two in step:
    # build.bat builds this same tree afterwards, and any property that differs turns that
    # incremental build into a full one.
    [ValidateSet('Debug', 'Release')] [string] $Configuration = 'Release',
    [switch] $IAgreeToVendorLicenses,
    [switch] $AutomatedBuild,
    [switch] $WithoutMascot,
    [string] $BuildWarningsPath = (Join-Path ([System.IO.Path]::GetTempPath()) 'pwiz-sharp-inspect/build-warnings.log')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# PowerShell 7.4 and later turn a native command's non-zero exit into a terminating error when
# $ErrorActionPreference is 'Stop'. A failed compile is build.bat's to report, not a reason to
# skip the steps after this one.
$PSNativeCommandUseErrorActionPreference = $false

try {
    Import-Module (Join-Path $PSScriptRoot 'scripts\BuildWarnings.psm1') -Force

    $BuildWarningsPath = [System.IO.Path]::GetFullPath($BuildWarningsPath)
    $logDir = Split-Path -Parent $BuildWarningsPath
    if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }
    if (Test-Path $BuildWarningsPath) { Remove-Item $BuildWarningsPath -Force }

    # build.bat's BUILD_TARGET. Without vendor licenses it builds msconvert here and then each
    # test project on its own, which this pre-build does not cover; CI always passes the licenses.
    $target = if ($IAgreeToVendorLicenses) { 'Pwiz.sln' } else { 'pwiz_tools\Commandline\MsConvert\src\MsConvert.csproj' }
    $properties = @("-p:Configuration=$Configuration")
    if ($IAgreeToVendorLicenses) { $properties += '-p:IAgreeToVendorLicenses=true' }
    if ($AutomatedBuild) { $properties += '-p:AutomatedBuild=true' }
    if ($WithoutMascot) { $properties += '-p:MascotSupport=false' }

    Write-Host "##teamcity[progressMessage 'Building $target for the warnings check']"
    Write-Host "##teamcity[blockOpened name='pre-build $target']"
    & dotnet build (Join-Path $PSScriptRoot $target) -nologo @properties @(Get-BuildWarningsLoggerArgs $BuildWarningsPath)
    $preBuildExit = $LASTEXITCODE
    Write-Host "##teamcity[blockClosed name='pre-build $target']"
    if ($preBuildExit -ne 0) {
        Write-Host "##teamcity[message text='Pre-build of $target failed with code $preBuildExit; build.bat will report the errors' status='WARNING']"
    }

    Write-BuildWarnings -LogPath $BuildWarningsPath -ProblemIdentity 'pwiz-sharp-build-warnings' -ArtifactDir '.teamcity/pwiz-sharp-inspect' | Out-Null
} catch {
    # Not silent: no verdict means the build's warnings went unchecked. Escaped by hand, since
    # the module that has Format-TcValue may be what failed.
    $message = $_.Exception.Message.Replace('|', '||').Replace("'", "|'").Replace("`n", '|n').Replace("`r", '|r').Replace('[', '|[').Replace(']', '|]')
    Write-Host "##teamcity[message text='Build warnings check threw: $message' status='WARNING']"
}
exit 0
