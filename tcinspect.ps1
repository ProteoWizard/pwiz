<#
.SYNOPSIS
    Check the pwiz-sharp build for warnings and report the verdict as its own GitHub status.

.DESCRIPTION
    The pwiz-sharp counterpart of pwiz_tools/Skyline/tcinspect.ps1. For now it does only that
    script's build-warning half, under the status 'pwiz code inspection'; ReSharper inspection
    of Pwiz.sln can be added here later and reported under the same status.

    The Directory.Build.props files do not promote warnings to errors, because a promoted warning
    stops the build before any test runs - including one the commit did not cause, such as a NuGet
    advisory published overnight. Instead tcbuild.bat calls this after clean.bat and before
    build.bat, so its pre-build is the first compile of the build and build.bat builds on top of it
    incrementally. That makes it the one place every compiler warning is emitted - an up-to-date
    project re-emits none - and the pre-build logs every restore and build warning to a
    warnings-only log.

    Warnings turn the status red and leave the build green: this script ALWAYS exits 0 and raises
    no TeamCity buildProblem, either of which would fail the build. Exiting 0 on a genuine failure
    would be dishonest, so a pre-build that failed or left no log reports state 'error' rather than
    quietly passing. The only outcome that reports 'success' is a completed pre-build with no
    warnings.

    tcbuild.bat runs this for vendor builds only. Without vendor licenses build.bat compiles each
    test project on its own after this pre-build, so a status from that build could not vouch for
    them.

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
    [string] $BuildWarningsPath = (Join-Path ([System.IO.Path]::GetTempPath()) 'pwiz-sharp-inspect/build-warnings.log'),

    # GitHub reporting. These come from TeamCity; with no token the script just prints its
    # verdict, which is what a developer running it locally wants.
    [string] $CommitSha = $env:BUILD_VCS_NUMBER,
    [string] $StatusContext = 'pwiz code inspection',
    [string] $TargetUrl = $env:INSPECTION_TARGET_URL,
    [string] $Repository = 'ProteoWizard/pwiz'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# PowerShell 7.4 and later turn a native command's non-zero exit into a terminating error when
# $ErrorActionPreference is 'Stop'. A failed pre-build is a verdict to report, not a reason to
# fail the step.
$PSNativeCommandUseErrorActionPreference = $false

# Publish-GitHubStatus and the build-warnings check, shared with the Skyline tcinspect.ps1.
# Loaded before anything that reports; if it cannot load there is no way to post a verdict, and
# the step must still not fail the build.
try {
    Import-Module (Join-Path $PSScriptRoot 'scripts\BuildWarnings.psm1') -Force
} catch {
    Write-Host "##teamcity[message text='Could not load scripts/BuildWarnings.psm1; build warnings were not checked' status='WARNING']"
    exit 0
}

# The single exit point. Every path lands here, and every path exits 0.
function Complete-Inspection([string] $state, [string] $summary) {
    Write-Host "pwiz code inspection: $state - $summary"
    Publish-GitHubStatus -State $state -Description $summary -Context $StatusContext `
                         -CommitSha $CommitSha -TargetUrl $TargetUrl -Repository $Repository
    exit 0
}

try {
    $BuildWarningsPath = [System.IO.Path]::GetFullPath($BuildWarningsPath)
    $logDir = Split-Path -Parent $BuildWarningsPath
    if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir -Force | Out-Null }
    if (Test-Path $BuildWarningsPath) { Remove-Item $BuildWarningsPath -Force }

    # Up front, so the check appears on the PR straight away rather than minutes in.
    Publish-GitHubStatus -State 'pending' -Description 'Checking the build for warnings' -Context $StatusContext `
                         -CommitSha $CommitSha -TargetUrl $TargetUrl -Repository $Repository

    # build.bat's BUILD_TARGET for the same flags.
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

    $warningCount = Write-BuildWarnings -LogPath $BuildWarningsPath -ArtifactDir '.teamcity/pwiz-sharp-inspect'

    # A failed compile stops the projects that depend on the failed one, so their warnings were
    # never emitted; build.bat reports the errors themselves.
    if ($preBuildExit -ne 0) {
        Complete-Inspection 'error' "Pre-build of $target failed with code $preBuildExit; build warnings not fully checked"
    }
    if ($null -eq $warningCount) {
        Complete-Inspection 'error' 'The pre-build left no warnings log; build warnings not checked'
    }
    if ($warningCount -gt 0) {
        Complete-Inspection 'failure' "$warningCount build warnings"
    }
    Complete-Inspection 'success' 'No build warnings'
} catch {
    # Anything unforeseen still has to leave a verdict behind rather than a failed build step.
    Complete-Inspection 'error' "build warnings check threw: $($_.Exception.Message)"
}
