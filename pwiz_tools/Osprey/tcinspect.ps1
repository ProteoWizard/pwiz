<#
.SYNOPSIS
    Check the Osprey build for compiler warnings and ReSharper inspection findings, at the end of
    the Osprey Windows .NET build.

.DESCRIPTION
    The Osprey counterpart of pwiz_tools/Skyline/tcinspect.ps1, which does the same for the
    Skyline master and PRs configuration. Osprey had neither check in CI, so warnings the local
    gate (ai/scripts/Osprey/Build-Osprey.ps1 -RunInspection) would have caught reached PRs and
    master while every TeamCity check stayed green.

    Compiler warnings: nothing promotes a warning to an error, because that would stop the build
    before any test runs. Instead tcbuild.bat hands every compile - build.ps1, CarafeSharp's
    build.ps1, and the pre-build below - the same warnings-only log (-BuildWarningsPath), after
    clean.bat, so every warning is emitted once and logged. This script reads that log.

    Inspection: inspectcode over Osprey.sln at -Severity and above, with the same arguments as the
    local gate. The ReSharper command line tool is pinned in .config/dotnet-tools.json, at the
    version Skyline's inspection uses, so nothing has to be installed on the agent.

    One difference from Skyline, and it is deliberate: this script FAILS the build. Skyline's
    inspection is a step BEFORE the tests, so it must never fail and reports a separate GitHub
    status instead. Osprey's tcbuild.bat calls this script LAST, after the tests and installers
    have run and reported, and its failure is its own buildProblem, so a test failure and a
    warning failure each still read as themselves. When TeamCity also provides GITHUB_STATUS_TOKEN
    (as the Skyline configuration does), the verdict is posted as an 'Osprey code inspection'
    status too.

    Exit codes: 0 no warnings and no findings; 1 warnings or findings; 2 a check did not run.
    With -TeamCity a non-zero exit is also raised as a buildProblem that says why.

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
    # The warnings-only log the build's compiles appended to. Empty skips the warnings check,
    # for a developer who only wants the inspection.
    [string] $BuildWarningsPath,
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
    $warningsLogger = @()
    if ($BuildWarningsPath) {
        $BuildWarningsPath = [System.IO.Path]::GetFullPath($BuildWarningsPath)
        $warningsLogger = Get-BuildWarningsLoggerArgs $BuildWarningsPath
    }

    # Ahead of the verdict, which leads with it: GitHub cuts the description at 140 characters.
    $notRun = @()
    $inspected = $false
    if ($UseExistingReport) {
        $inspected = Test-Path $ReportPath
    } else {
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
                $notRun += "dotnet tool restore failed with code $LASTEXITCODE"
            } else {
                # inspectcode resolves a reference to a project outside the solution (pwiz-sharp,
                # CommonUtil, ProteowizardWrapper) through that project's output assembly under
                # the properties it runs with: bin\$Platform\$Configuration. A solution build does
                # not write there - Directory.Build.targets gives out-of-solution references
                # Platform=AnyCPU in a solution build (#4725) - so every pwiz-sharp type would go
                # unresolved. A PROJECT build of the test project, whose references reach every
                # out-of-solution project, fills that directory. The local gate does the same.
                # It compiles those projects for the first time under x64, so it logs warnings too.
                $testProj = Join-Path $PSScriptRoot 'Osprey.Test\Osprey.Test.csproj'
                Write-Host "##teamcity[blockOpened name='pre-build Osprey.Test.csproj']"
                & dotnet build $testProj @($buildProperties.GetEnumerator() | ForEach-Object { "-p:$($_.Key)=$($_.Value)" }) @warningsLogger
                $preBuildExit = $LASTEXITCODE
                Write-Host "##teamcity[blockClosed name='pre-build Osprey.Test.csproj']"
                if ($preBuildExit -ne 0) {
                    $notRun += "pre-build of Osprey.Test.csproj failed with code $preBuildExit"
                } else {
                    Write-Host "##teamcity[progressMessage 'inspectcode over Osprey.sln']"
                    Write-Host "##teamcity[blockOpened name='inspectcode']"
                    # Arguments match the local gate: --no-swea as Skyline's inspection, --no-build
                    # since everything was built above, and one target framework (net10.0) so the
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
                    if ($inspectExit -ne 0) {
                        $notRun += "inspectcode exited with code $inspectExit"
                    } elseif (-not (Test-Path $ReportPath)) {
                        $notRun += 'inspectcode produced no report'
                    } else {
                        $inspected = $true
                    }
                }
            }
        } finally {
            Pop-Location
        }
    }
    if (-not $inspected -and $notRun.Count -eq 0) { $notRun += "no inspection report at $ReportPath" }

    $findings = 0
    $counts = ''
    if ($inspected) {
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
        $findings = $issues.Count
        if ($findings -gt 0) {
            Write-Host ''
            Write-Host "$findings inspection finding(s) at $Severity or above:"
            foreach ($issue in ($issues | Select-Object -First 100)) {
                Write-Host ("  {0}({1}): {2} - {3}" -f $issue.GetAttribute('File'), $issue.GetAttribute('Line'),
                                                       $issue.GetAttribute('TypeId'), $issue.GetAttribute('Message'))
            }
            if ($findings -gt 100) { Write-Host "  ... and $($findings - 100) more; see the Inspections tab" }
            $counts = ($bySeverity.GetEnumerator() | Sort-Object -Property Key |
                       ForEach-Object { "$($_.Value) $($_.Key.ToLowerInvariant())" }) -join ', '
        }
    }

    $buildWarningCount = 0
    if ($BuildWarningsPath) {
        $buildWarningCount = Write-BuildWarnings -LogPath $BuildWarningsPath -ArtifactDir '.teamcity/osprey-inspectcode'
        # No log means no build ran under the logger: no evidence the build is warning-free.
        if ($null -eq $buildWarningCount) { $notRun += 'build warnings not checked (no log)' }
    }

    $verdict = @($notRun)
    if ($buildWarningCount -gt 0) { $verdict += "$buildWarningCount build warnings" }
    if ($findings -gt 0) { $verdict += "$findings inspections ($counts)" }
    if ($notRun.Count -gt 0) { Complete-Inspection 'error' ($verdict -join '; ') }
    if ($verdict.Count -gt 0) { Complete-Inspection 'failure' ($verdict -join '; ') }
    $checked = if ($BuildWarningsPath) { 'No build warnings and no inspections' } else { 'No inspections' }
    Complete-Inspection 'success' "$checked at $Severity or above"
} catch {
    Complete-Inspection 'error' "code inspection threw: $($_.Exception.Message)"
}
