<#
.SYNOPSIS
    Run ReSharper code inspection over Osprey.sln inside the Osprey Windows .NET build.

.DESCRIPTION
    The Osprey counterpart of pwiz_tools/Skyline/tcinspect.ps1, which runs inspection inside the
    Skyline master and PRs configuration. Osprey has never had an inspection in CI, so warnings the
    local gate (ai/scripts/Osprey/Build-Osprey.ps1 -RunInspection) would have caught have reached
    PRs and master while every TeamCity check stayed green.

    One difference from Skyline, and it is deliberate: this script FAILS the build on a finding.
    Skyline's inspection runs as its own step BEFORE the tests, so failing it would skip every
    test; it reports a separate GitHub status and always exits 0 instead. Osprey's tcbuild.bat
    calls this script LAST, after the tests and the installers, so a failure here costs nothing
    but the red check that is wanted. When TeamCity also hands the build GITHUB_STATUS_TOKEN (as
    the Skyline configuration does), the verdict is posted as its own 'Osprey code inspection'
    status too, so it can be told apart from a test failure at a glance.

    The ReSharper command line tool is pinned in .config/dotnet-tools.json, at the version
    Skyline's inspection uses, so a developer and the CI agent analyze with the same version
    without anything installed on the agent.

    Exit codes: 0 no findings at or above -Severity; 1 findings; 2 the inspection did not run
    or produced no report. With -TeamCity a non-zero exit is also raised as a buildProblem that
    says why.

.EXAMPLE
    pwsh -File .\tcinspect.ps1 -IAgreeToVendorLicenses

.EXAMPLE
    pwsh -File .\tcinspect.ps1 -UseExistingReport
#>
[CmdletBinding()]
param(
    # Match build.ps1, which tcbuild.bat runs first, so the pre-build below is incremental.
    [ValidateSet('Debug', 'Release')] [string] $Configuration = 'Release',
    [string] $Platform = 'x64',
    [switch] $IAgreeToVendorLicenses,
    # Minimal severity to report, matching the local gate and Skyline's inspection.
    [ValidateSet('INFO', 'HINT', 'SUGGESTION', 'WARNING', 'ERROR')] [string] $Severity = 'WARNING',
    # Outside the checkout, as Skyline's is.
    [string] $ReportPath = (Join-Path ([System.IO.Path]::GetTempPath()) 'osprey-inspectcode/inspectcode_report.xml'),
    # Parse a report a previous run left behind instead of running inspectcode again.
    [switch] $UseExistingReport,
    [switch] $TeamCity,

    # GitHub reporting. These come from TeamCity; with no token the script just prints its
    # verdict, which is what a developer running it locally wants.
    [string] $CommitSha = $env:BUILD_VCS_NUMBER,
    [string] $StatusContext = 'Osprey code inspection',
    [string] $TargetUrl = $env:INSPECTION_TARGET_URL,
    [string] $Repository = 'ProteoWizard/pwiz'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# inspectcode exits non-zero when it finds problems; that is a verdict to report, not a throw.
$PSNativeCommandUseErrorActionPreference = $false

Import-Module (Join-Path $PSScriptRoot '..\..\scripts\BuildWarnings.psm1') -Force

# The single exit point.
function Complete-Inspection([string] $state, [string] $summary) {
    Write-Host "Osprey code inspection: $state - $summary"
    Publish-GitHubStatus -State $state -Description $summary -Context $StatusContext `
                         -CommitSha $CommitSha -TargetUrl $TargetUrl -Repository $Repository
    $code = switch ($state) { 'success' { 0 } 'failure' { 1 } default { 2 } }
    if ($code -ne 0 -and $TeamCity) {
        Write-Host "##teamcity[buildProblem description='$(Format-TcValue "Osprey code inspection: $summary")' identity='OspreyCodeInspection']"
    }
    exit $code
}

try {
    $solution = Join-Path $PSScriptRoot 'Osprey.sln'
    $dotSettings = Join-Path $PSScriptRoot 'Osprey.sln.DotSettings'
    $ReportPath = [System.IO.Path]::GetFullPath($ReportPath)
    $reportDir = Split-Path -Parent $ReportPath
    if (-not (Test-Path $reportDir)) { New-Item -ItemType Directory -Path $reportDir -Force | Out-Null }

    if (-not $UseExistingReport) {
        if (Test-Path $ReportPath) { Remove-Item $ReportPath -Force }
        Publish-GitHubStatus -State 'pending' -Description 'Running ReSharper inspection' -Context $StatusContext `
                             -CommitSha $CommitSha -TargetUrl $TargetUrl -Repository $Repository

        $buildProperties = [ordered]@{ Configuration = $Configuration; Platform = $Platform }
        if ($IAgreeToVendorLicenses) { $buildProperties.IAgreeToVendorLicenses = 'true' }

        # 'dotnet jb' resolves the tool manifest by walking up from the CURRENT directory, and
        # the manifest is under pwiz_tools/Osprey while CI runs this from the checkout root.
        Push-Location $PSScriptRoot
        try {
            Write-Host "##teamcity[progressMessage 'Restoring the ReSharper command line tool']"
            & dotnet tool restore
            if ($LASTEXITCODE -ne 0) {
                Complete-Inspection 'error' "dotnet tool restore failed with code $LASTEXITCODE; inspection did not run"
            }

            # inspectcode resolves a reference to a project outside the solution (pwiz-sharp,
            # CommonUtil, ProteowizardWrapper) through that project's output assembly under the
            # properties it runs with: bin\$Platform\$Configuration. A solution build does not
            # write there - Directory.Build.targets gives out-of-solution references
            # Platform=AnyCPU in a solution build (#4725) - so every pwiz-sharp type would go
            # unresolved. A PROJECT build of the test project, whose references reach every
            # out-of-solution project, fills that directory. The local gate does the same.
            $testProj = Join-Path $PSScriptRoot 'Osprey.Test\Osprey.Test.csproj'
            Write-Host "##teamcity[blockOpened name='pre-build Osprey.Test.csproj']"
            & dotnet build $testProj @($buildProperties.GetEnumerator() | ForEach-Object { "-p:$($_.Key)=$($_.Value)" })
            $preBuildExit = $LASTEXITCODE
            Write-Host "##teamcity[blockClosed name='pre-build Osprey.Test.csproj']"
            if ($preBuildExit -ne 0) {
                Complete-Inspection 'error' "pre-build of Osprey.Test.csproj failed with code $preBuildExit; inspection did not run"
            }

            Write-Host "##teamcity[progressMessage 'inspectcode over Osprey.sln']"
            Write-Host "##teamcity[blockOpened name='inspectcode']"
            # Arguments match the local gate: --no-swea as Skyline's inspection, --no-build since
            # everything was built above, and one target framework (net10.0) so the
            # nondeterministic suppression race of #4379 cannot arise.
            $inspectArgs = @(
                $solution
                "--profile=$dotSettings"
                "-o=$ReportPath"
                '-f=Xml'
                '--no-swea'
                '--no-build'
                "-e=$Severity"
                "--properties:$(($buildProperties.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ';')"
            )
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

    if (-not (Test-Path $ReportPath)) {
        Complete-Inspection 'error' 'inspectcode produced no report'
    }

    # Populate TeamCity's Inspections tab and keep the raw report downloadable.
    if ($TeamCity) {
        Write-Host ("##teamcity[importData type='ReSharperInspectCode' path='{0}']" -f (Format-TcValue $ReportPath))
        Write-Host ("##teamcity[publishArtifacts '{0} => .teamcity/osprey-inspectcode']" -f (Format-TcValue $ReportPath))
    }

    [xml] $report = Get-Content -LiteralPath $ReportPath -Raw
    $severityOfType = @{}
    foreach ($issueType in $report.SelectNodes('//IssueTypes/IssueType')) {
        $severityOfType[$issueType.GetAttribute('Id')] = $issueType.GetAttribute('Severity')
    }
    $issues = @($report.SelectNodes('//Issues/Project/Issue'))
    $bySeverity = @{}
    foreach ($issue in $issues) {
        $issueSeverity = $issue.GetAttribute('Severity')
        if ([string]::IsNullOrEmpty($issueSeverity)) { $issueSeverity = $severityOfType[$issue.GetAttribute('TypeId')] }
        if ([string]::IsNullOrEmpty($issueSeverity)) { $issueSeverity = 'UNKNOWN' }
        if (-not $bySeverity.ContainsKey($issueSeverity)) { $bySeverity[$issueSeverity] = 0 }
        $bySeverity[$issueSeverity]++
    }

    if ($issues.Count -eq 0) {
        Complete-Inspection 'success' "No inspections at $Severity or above"
    }
    Write-Host ''
    Write-Host "$($issues.Count) inspection finding(s) at $Severity or above:"
    foreach ($issue in ($issues | Select-Object -First 100)) {
        Write-Host ("  {0}({1}): {2} - {3}" -f $issue.GetAttribute('File'), $issue.GetAttribute('Line'),
                                               $issue.GetAttribute('TypeId'), $issue.GetAttribute('Message'))
    }
    if ($issues.Count -gt 100) { Write-Host "  ... and $($issues.Count - 100) more; see the Inspections tab" }
    $counts = ($bySeverity.GetEnumerator() | Sort-Object -Property Key |
               ForEach-Object { "$($_.Value) $($_.Key.ToLowerInvariant())" }) -join ', '
    Complete-Inspection 'failure' "$($issues.Count) inspections ($counts)"
} catch {
    Complete-Inspection 'error' "code inspection threw: $($_.Exception.Message)"
}
