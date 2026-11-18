<#
.SYNOPSIS
    Check the pwiz-sharp build for warnings, run ReSharper inspection over Pwiz.sln, and report
    the verdict as its own GitHub status.

.DESCRIPTION
    The pwiz-sharp counterpart of pwiz_tools/Skyline/tcinspect.ps1, reporting under the status
    'Core code inspection'. A build warning or an inspection finding at -Severity or above turns
    the status red.

    The Directory.Build.props files do not promote warnings to errors, because a promoted warning
    stops the build before any test runs - including one the commit did not cause, such as a NuGet
    advisory published overnight. Instead tcbuild.bat calls this after clean.bat and before
    build.bat, so its pre-build is the first compile of the build and build.bat builds on top of it
    incrementally. That makes it the one place every compiler warning is emitted - an up-to-date
    project re-emits none - and the pre-build logs every restore and build warning to a
    warnings-only log.

    inspectcode then inspects Pwiz.sln under Pwiz.sln.DotSettings, which keeps Skyline's rules
    for code that may be wrong and hides the style and redundancy ones. The projects Pwiz.sln
    shares with Skyline.sln are excluded: Skyline's inspection already checks them under rules
    that are a superset of these.

    Warnings and findings turn the status red and leave the build green: this script ALWAYS exits
    0 and raises no TeamCity buildProblem, either of which would fail the build. So that they can
    be read without opening the build log, each build warning is also reported as a TeamCity
    inspection, which lists it on the build's Code Inspection tab beside the ReSharper findings
    without failing anything. Exiting 0 on a genuine failure would be dishonest, so a pre-build or inspection that failed or left no output
    reports state 'error' rather than quietly passing. The only outcome that reports 'success' is
    a completed pre-build with no warnings and a completed inspection with no findings.

    tcbuild.bat runs this for vendor builds only. Without vendor licenses build.bat compiles each
    test project on its own after this pre-build, so a status from that build could not vouch for
    them, and the full no-vendor Pwiz.sln does not build for inspectcode to load.

    The warnings log and the report are written outside the checkout by default. tcbuild.bat
    fails the build when 'git status --porcelain' is non-empty at the end.

    The ReSharper command line tool is pinned in .config/dotnet-tools.json, at the same version as
    pwiz_tools/Skyline/.config/dotnet-tools.json, so a developer running this locally and the CI
    agent analyze with the same version.

.EXAMPLE
    pwsh -File .\tcinspect.ps1 -IAgreeToVendorLicenses

.EXAMPLE
    pwsh -File .\tcinspect.ps1 -IAgreeToVendorLicenses -UseExistingReport
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

    # Minimal severity to report, as in the Skyline inspection.
    [ValidateSet('INFO', 'HINT', 'SUGGESTION', 'WARNING', 'ERROR')] [string] $Severity = 'WARNING',
    [string] $ReportPath = (Join-Path ([System.IO.Path]::GetTempPath()) 'pwiz-sharp-inspect/inspectcode_report.xml'),
    # The projects Pwiz.sln shares with Skyline.sln. ProteowizardWrapper compiles its sources
    # from ProteowizardWrapper.PwizSharp as linked files, so that folder is named too.
    [string[]] $Exclude = @(
        'pwiz_tools/Shared/zedgraph/**'
        'pwiz_tools/Shared/MSGraph/**'
        'pwiz_tools/Shared/CommonUtil/**'
        'pwiz_tools/Shared/CommonMsData/**'
        'pwiz_tools/Shared/CommonBaseUI/**'
        'pwiz_tools/Shared/CommonFileDialogs/**'
        'pwiz_tools/Shared/ProteowizardWrapper/**'
        'pwiz_tools/Shared/ProteowizardWrapper.PwizSharp/**'
    ),
    # Parse the report and warnings log a previous run left behind instead of building and
    # inspecting again. The inspection takes several minutes; this is how you iterate on the
    # reporting.
    [switch] $UseExistingReport,

    # GitHub reporting. These come from TeamCity; with no token the script just prints its
    # verdict, which is what a developer running it locally wants.
    [string] $CommitSha = $env:BUILD_VCS_NUMBER,
    [string] $StatusContext = 'Core code inspection',
    [string] $TargetUrl = $env:INSPECTION_TARGET_URL,
    [string] $Repository = 'ProteoWizard/pwiz'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# PowerShell 7.4 and later turn a native command's non-zero exit into a terminating error when
# $ErrorActionPreference is 'Stop'. A failed pre-build, or inspectcode's "I found problems" exit
# code, is a verdict to report, not a reason to fail the step.
$PSNativeCommandUseErrorActionPreference = $false

# Format-TcValue, Publish-GitHubStatus and the build-warnings check, shared with the Skyline
# tcinspect.ps1. Loaded before anything that reports; if it cannot load there is no way to post a
# verdict, and the step must still not fail the build.
try {
    Import-Module (Join-Path $PSScriptRoot 'scripts\BuildWarnings.psm1') -Force
} catch {
    Write-Host "##teamcity[message text='Could not load scripts/BuildWarnings.psm1; code inspection did not run' status='WARNING']"
    exit 0
}

function Send-GitHubStatus([string] $state, [string] $description) {
    Publish-GitHubStatus -State $state -Description $description -Context $StatusContext `
                         -CommitSha $CommitSha -TargetUrl $TargetUrl -Repository $Repository
}

# Prints and publishes the build warnings log, lists each warning on the Code Inspection tab, and
# returns the warning count ($null when there is no log).
function Report-BuildWarnings {
    $count = Write-BuildWarnings -LogPath $BuildWarningsPath -ArtifactDir '.teamcity/pwiz-sharp-inspect'
    if ($count -gt 0) {
        Publish-BuildWarningInspections -LogPath $BuildWarningsPath -RepoRoot $PSScriptRoot
    }
    return $count
}

# The single exit point. Every path lands here, and every path exits 0.
function Complete-Inspection([string] $state, [string] $summary) {
    # GitHub truncates a commit status description at 140 characters; a tool's exception text
    # runs well past that, so cut it here where the tail can still be read in the build log.
    if ($summary.Length -gt 140) { $summary = $summary.Substring(0, 137) + '...' }
    Write-Host "${StatusContext}: $state - $summary"
    Send-GitHubStatus $state $summary
    exit 0
}

try {
    $BuildWarningsPath = [System.IO.Path]::GetFullPath($BuildWarningsPath)
    $ReportPath = [System.IO.Path]::GetFullPath($ReportPath)
    foreach ($outputDir in @((Split-Path -Parent $BuildWarningsPath), (Split-Path -Parent $ReportPath))) {
        if (-not (Test-Path $outputDir)) { New-Item -ItemType Directory -Path $outputDir -Force | Out-Null }
    }

    # build.bat's BUILD_TARGET for the same flags.
    $target = if ($IAgreeToVendorLicenses) { 'Pwiz.sln' } else { 'pwiz_tools\Commandline\MsConvert\src\MsConvert.csproj' }
    # The full no-vendor Pwiz.sln does not build, so there is nothing for inspectcode to load.
    $inspect = $IAgreeToVendorLicenses -or $UseExistingReport

    if (-not $UseExistingReport) {
        if (Test-Path $BuildWarningsPath) { Remove-Item $BuildWarningsPath -Force }
        if (Test-Path $ReportPath) { Remove-Item $ReportPath -Force }

        # Up front, so the check appears on the PR straight away rather than minutes in.
        Send-GitHubStatus 'pending' $(if ($inspect) { 'Checking build warnings and running ReSharper inspection' } else { 'Checking the build for warnings' })

        $properties = [ordered]@{ Configuration = $Configuration }
        if ($IAgreeToVendorLicenses) { $properties.IAgreeToVendorLicenses = 'true' }
        if ($AutomatedBuild) { $properties.AutomatedBuild = 'true' }
        if ($WithoutMascot) { $properties.MascotSupport = 'false' }

        Write-Host "##teamcity[progressMessage 'Building $target for the warnings check']"
        Write-Host "##teamcity[blockOpened name='pre-build $target']"
        & dotnet build (Join-Path $PSScriptRoot $target) -nologo @($properties.GetEnumerator() | ForEach-Object { "-p:$($_.Key)=$($_.Value)" }) @(Get-BuildWarningsLoggerArgs $BuildWarningsPath)
        $preBuildExit = $LASTEXITCODE
        Write-Host "##teamcity[blockClosed name='pre-build $target']"

        # A failed compile stops the projects that depend on the failed one, so their warnings
        # were never emitted, and inspectcode would report their unresolved types as findings;
        # build.bat reports the errors themselves.
        if ($preBuildExit -ne 0) {
            Report-BuildWarnings | Out-Null
            Complete-Inspection 'error' "Pre-build of $target failed with code $preBuildExit; build warnings not fully checked, inspection not run"
        }

        if ($inspect) {
            # 'dotnet jb' resolves the tool manifest by walking up from the CURRENT directory.
            Push-Location $PSScriptRoot
            try {
                Write-Host "##teamcity[progressMessage 'Restoring the ReSharper command line tool']"
                & dotnet tool restore
                if ($LASTEXITCODE -ne 0) {
                    Complete-Inspection 'error' "dotnet tool restore failed with code $LASTEXITCODE; inspection did not run"
                }

                Write-Host "##teamcity[progressMessage 'inspectcode over Pwiz.sln']"
                # inspectcode prints a line per file inspected - thousands of them. Fold them so
                # the build log stays readable, but keep them: when a project fails to load, its
                # error is in there and nowhere else.
                Write-Host "##teamcity[blockOpened name='inspectcode']"
                # --no-swea as in the Skyline inspection: solution-wide analysis roughly doubles
                # the runtime. inspectcode builds the solution itself; with the same properties as
                # the pre-build that build is incremental.
                $inspectArgs = @(
                    (Join-Path $PSScriptRoot 'Pwiz.sln')
                    "-o=$ReportPath"
                    '-f=Xml'
                    '--no-swea'
                    "-e=$Severity"
                    "--properties:$(($properties.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ';')"
                )
                if ($Exclude.Count -gt 0) { $inspectArgs += "--exclude=$($Exclude -join ';')" }
                & dotnet jb inspectcode @inspectArgs
                $inspectExit = $LASTEXITCODE
                Write-Host "##teamcity[blockClosed name='inspectcode']"
            } finally {
                Pop-Location
            }
            if ($inspectExit -ne 0) {
                Complete-Inspection 'error' "inspectcode exited with code $inspectExit"
            }
        }
    }

    $total = 0
    $findings = ''
    if ($inspect) {
        if (-not (Test-Path $ReportPath)) {
            Complete-Inspection 'error' 'inspectcode produced no report'
        }

        # Populate TeamCity's Inspections tab, and keep the raw report downloadable - the tab
        # summarises, the XML is what you diff.
        Write-Host ("##teamcity[importData type='ReSharperInspectCode' path='{0}']" -f (Format-TcValue $ReportPath))
        Write-Host ("##teamcity[publishArtifacts '{0} =>.teamcity/pwiz-sharp-inspect']" -f (Format-TcValue $ReportPath))

        [xml] $report = Get-Content -LiteralPath $ReportPath -Raw

        # Severity lives on the IssueType definition; an individual Issue may override it.
        $severityOfType = @{}
        foreach ($issueType in $report.SelectNodes('//IssueTypes/IssueType')) {
            $severityOfType[$issueType.GetAttribute('Id')] = $issueType.GetAttribute('Severity')
        }

        $issues = @($report.SelectNodes('//Issues/Project/Issue'))
        $bySeverity = @{}
        $byType = @{}
        foreach ($issue in $issues) {
            $typeId = $issue.GetAttribute('TypeId')
            $issueSeverity = $issue.GetAttribute('Severity')
            if ([string]::IsNullOrEmpty($issueSeverity)) { $issueSeverity = $severityOfType[$typeId] }
            if ([string]::IsNullOrEmpty($issueSeverity)) { $issueSeverity = 'UNKNOWN' }
            $bySeverity[$issueSeverity] = 1 + $(if ($bySeverity.ContainsKey($issueSeverity)) { $bySeverity[$issueSeverity] } else { 0 })
            $byType[$typeId] = 1 + $(if ($byType.ContainsKey($typeId)) { $byType[$typeId] } else { 0 })
        }

        $total = $issues.Count
        if ($total -gt 0) {
            Write-Host ''
            Write-Host "$total inspection finding(s) at $Severity or above, most frequent first:"
            foreach ($entry in ($byType.GetEnumerator() | Sort-Object -Property Value -Descending | Select-Object -First 25)) {
                Write-Host ("  {0,5}  {1} [{2}]" -f $entry.Value, $entry.Key, $severityOfType[$entry.Key])
            }
            Write-Host ''
            # Enough individual findings to act on without opening the Inspections tab.
            foreach ($issue in ($issues | Select-Object -First 100)) {
                Write-Host ("  {0}({1}): {2} - {3}" -f $issue.GetAttribute('File'), $issue.GetAttribute('Line'),
                                                       $issue.GetAttribute('TypeId'), $issue.GetAttribute('Message'))
            }
            if ($total -gt 100) { Write-Host "  ... and $($total - 100) more; see the Inspections tab" }
            $findings = ($bySeverity.GetEnumerator() | Sort-Object -Property Key |
                         ForEach-Object { "$($_.Value) $($_.Key.ToLowerInvariant())" }) -join ', '
        }
    }

    $warningCount = Report-BuildWarnings

    $verdict = @()
    # No log after a pre-build means no evidence the build is warning-free, so it cannot pass.
    # -UseExistingReport may have no log to read, which only matters to whoever is iterating.
    if ($null -eq $warningCount -and -not $UseExistingReport) {
        Complete-Inspection 'error' 'The pre-build left no warnings log; build warnings not checked'
    }
    if ($warningCount -gt 0) { $verdict += "$warningCount build warnings" }
    if ($total -gt 0) { $verdict += "$total inspections ($findings)" }
    if ($verdict.Count -gt 0) {
        Complete-Inspection 'failure' ($verdict -join '; ')
    }
    if ($inspect) {
        Complete-Inspection 'success' "No build warnings and no inspections at $Severity or above"
    }
    Complete-Inspection 'success' 'No build warnings'
} catch {
    # Anything unforeseen still has to leave a verdict behind rather than a failed build step.
    Complete-Inspection 'error' "code inspection threw: $($_.Exception.Message)"
}
