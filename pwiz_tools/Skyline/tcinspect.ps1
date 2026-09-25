<#
.SYNOPSIS
    Run ReSharper code inspection over Skyline.sln and report it as its own GitHub status.

.DESCRIPTION
    This is the in-build replacement for the standalone "Skyline Code Inspection" TeamCity
    config. Inspection findings get their OWN GitHub commit status and never fail the build
    that runs the tests, so this script ALWAYS exits 0 and reports its verdict by posting the
    status itself.

    Posting from here rather than from a following TeamCity step is what lets the inspection
    sit anywhere in the build - including inside tcbuild.bat - and still have the status appear
    the moment it finishes, instead of whenever the enclosing step happens to end. It also
    means a 'pending' status can go up front, so the check shows on the PR while the inspection
    is still running. scripts/misc/smartBuildTrigger.py posts commit statuses the same way,
    with a token handed to it by TeamCity.

    Exiting 0 on a genuine failure would be dishonest, so a crashed or missing inspection is
    reported as state 'error' rather than quietly passing. The only outcome that reports
    'success' is a run that completed and found nothing at or above -Severity.

    The report is written outside the checkout by default. tcbuild.bat fails the build when
    'git status --porcelain' is non-empty at the end.

    The ReSharper command line tool is pinned in .config/dotnet-tools.json rather than taken
    from the TeamCity agent's bundled copy, so a developer running this locally and the CI
    agent analyze with the same version. The bundled agent tool trails by a release or two,
    which matters here: Skyline targets net10.0-windows, and only 2025.3 and later resolve
    net10.0 projects.

.EXAMPLE
    pwsh -File .\tcinspect.ps1

.EXAMPLE
    pwsh -File .\tcinspect.ps1 -Severity ERROR -UseExistingReport
#>
[CmdletBinding()]
param(
    [string] $Solution = (Join-Path $PSScriptRoot 'Skyline.sln'),
    [ValidateSet('Debug', 'Release')] [string] $Configuration = 'Release',
    [string] $Platform = 'x64',
    # Minimal severity to report, matching the -e=WARNING the standalone config has always used.
    [ValidateSet('INFO', 'HINT', 'SUGGESTION', 'WARNING', 'ERROR')] [string] $Severity = 'WARNING',
    [string] $ReportPath = (Join-Path ([System.IO.Path]::GetTempPath()) 'skyline-inspectcode/inspectcode_report.xml'),
    # Built before inspectcode loads the solution, and built with the SAME Platform the
    # inspection runs under. ReSharper resolves a project reference pointing outside the
    # solution through the referenced project's output assembly, and it evaluates that project
    # with the properties given here: under Platform=x64 it looks in bin\x64\Release, which on
    # a cold agent is empty, so every pwiz-sharp type goes unresolved and the project model
    # keeps that state for the whole run. Measured on this tree with the x64 outputs deleted:
    # 442 unresolved references, and the same 65 errors and 2347 warnings build #285 reported.
    # Building x64 first clears them. Building AnyCPU instead fixes nothing - it fills
    # bin\Release, which is not where the inspection looks (build #298).
    #
    # Skyline.csproj, not Skyline.sln, and the difference is the whole point:
    # AssignOutOfSolutionProjectReferenceConfiguration in pwiz_tools/Directory.Build.targets
    # fires only for a SOLUTION build and pins out-of-solution references to AnyCPU on
    # purpose, so building the solution leaves bin\x64\Release empty no matter what Platform
    # it is given (build #298 and a local run both measured that). A csproj build carries no
    # solution configuration, that target stays quiet, and Platform flows down the whole
    # reference graph. Skyline.csproj is the hub - ProteowizardWrapper and its fifteen,
    # Bruker.PrmScheduling, and the tool projects all hang off it - so nothing here needs a
    # list of what to build.
    [string] $PreBuild = (Join-Path $PSScriptRoot 'Skyline.csproj'),
    # Empty by default: with the references resolved there is nothing left for an exclusion of
    # the ported sandbox to hide.
    [string[]] $Exclude = @(),
    # Parse a report a previous run left behind instead of running inspectcode again. The
    # inspection takes several minutes; this is how you iterate on the reporting.
    [switch] $UseExistingReport,

    # GitHub reporting. These come from TeamCity; with no token the script just prints its
    # verdict, which is what a developer running it locally wants.
    [string] $CommitSha = $env:BUILD_VCS_NUMBER,
    [string] $StatusContext = 'Skyline code inspection',
    [string] $TargetUrl = $env:INSPECTION_TARGET_URL,
    [string] $Repository = 'ProteoWizard/pwiz'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# PowerShell 7.4 and later turn a native command's non-zero exit into a terminating error when
# $ErrorActionPreference is 'Stop'. That would take inspectcode's "I found problems" exit code
# and turn it into a failed build step, which is the one thing this script must never do.
$PSNativeCommandUseErrorActionPreference = $false

# TeamCity service message values escape these six. Counts never contain them; the description
# carries tool output on the failure paths, which can.
function Format-TcValue([string] $text) {
    if ([string]::IsNullOrEmpty($text)) { return '' }
    return $text.Replace('|', '||').Replace("'", "|'").Replace("`n", '|n').Replace("`r", '|r').Replace('[', '|[').Replace(']', '|]')
}

# Never throws and never retries: a status that fails to post is worth a warning in the log,
# not a failed build. The token is a TeamCity credentialsJSON value, so it is never echoed.
function Send-GitHubStatus([string] $state, [string] $description) {
    $missing = @()
    if ([string]::IsNullOrEmpty($env:GITHUB_STATUS_TOKEN)) { $missing += 'GITHUB_STATUS_TOKEN' }
    if ([string]::IsNullOrEmpty($CommitSha)) { $missing += 'BUILD_VCS_NUMBER' }
    if ($missing.Count -gt 0) {
        Write-Host "  (not posting '$state' to '$StatusContext': $($missing -join ' and ') $(if ($missing.Count -gt 1) { 'are' } else { 'is' }) empty)"
        return
    }
    $body = @{ state = $state; context = $StatusContext; description = $description }
    if (-not [string]::IsNullOrEmpty($TargetUrl)) { $body['target_url'] = $TargetUrl }
    try {
        Invoke-RestMethod -Method Post -ContentType 'application/json' `
            -Uri "https://api.github.com/repos/$Repository/statuses/$CommitSha" `
            -Headers @{ Authorization = "Bearer $env:GITHUB_STATUS_TOKEN" } `
            -Body ($body | ConvertTo-Json -Compress) | Out-Null
        Write-Host "  posted '$state' to GitHub status '$StatusContext' on $CommitSha"
    } catch {
        Write-Host "##teamcity[message text='Could not post the code inspection status: $(Format-TcValue $_.Exception.Message)' status='WARNING']"
    }
}

# The single exit point. Every path lands here, and every path exits 0: this step must never be
# the reason the Skyline build goes red.
function Complete-Inspection([string] $state, [string] $summary) {
    # GitHub truncates a commit status description at 140 characters; a tool's exception text
    # runs well past that, so cut it here where the tail can still be read in the build log.
    if ($summary.Length -gt 140) { $summary = $summary.Substring(0, 137) + '...' }
    Write-Host "Code inspection: $state - $summary"
    Send-GitHubStatus $state $summary
    exit 0
}

try {
    $Solution = [System.IO.Path]::GetFullPath($Solution)
    $ReportPath = [System.IO.Path]::GetFullPath($ReportPath)

    $reportDir = Split-Path -Parent $ReportPath
    if ($reportDir -and -not (Test-Path $reportDir)) {
        New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
    }

    if (-not $UseExistingReport) {
        if (Test-Path $ReportPath) { Remove-Item $ReportPath -Force }

        # Up front, so the check appears on the PR straight away rather than minutes in.
        Send-GitHubStatus 'pending' 'Running ReSharper inspection'

        Write-Host "##teamcity[progressMessage 'Restoring the ReSharper command line tool']"
        # 'dotnet jb' below resolves the tool manifest by walking up from the CURRENT directory,
        # and the manifest is under pwiz_tools/Skyline while CI runs this from the checkout root.
        # Both calls therefore run from the script's own directory.
        Push-Location $PSScriptRoot
        try {
            & dotnet tool restore
            if ($LASTEXITCODE -ne 0) {
                Complete-Inspection 'error' "dotnet tool restore failed with code $LASTEXITCODE; inspection did not run"
            }

            if (-not [string]::IsNullOrEmpty($PreBuild)) {
                Write-Host "##teamcity[progressMessage 'Building $(Split-Path -Leaf $PreBuild) for the inspection']"
                Write-Host "##teamcity[blockOpened name='pre-build']"
                & dotnet build $PreBuild -c $Configuration -p:Platform=$Platform -p:IAgreeToVendorLicenses=true
                $preBuildExit = $LASTEXITCODE
                Write-Host "##teamcity[blockClosed name='pre-build']"
                # Not fatal, and not silent: the inspection still runs, it just reports every
                # out-of-solution type as unresolved. Saying so here is what tells that apart
                # from a real finding when the error count comes back high.
                if ($preBuildExit -ne 0) {
                    Write-Host "##teamcity[message text='Pre-build of $(Format-TcValue $PreBuild) failed with code $preBuildExit; out-of-solution references will not resolve' status='WARNING']"
                }
            }

            Write-Host "##teamcity[progressMessage 'inspectcode over Skyline.sln']"
            # inspectcode prints a line per file inspected - thousands of them. Fold them so
            # the build log stays readable, but keep them: when a project fails to load, its
            # error is in there and nowhere else.
            Write-Host "##teamcity[blockOpened name='inspectcode']"
            # --no-swea matches the standalone config: solution-wide analysis roughly doubles the
            # runtime, and its findings are largely the ones Skyline.sln.DotSettings silences.
            # inspectcode builds the solution itself (--build is its default). --no-build is not
            # an option even when the tree is already built: Skyline.csproj removes
            # ProtocolBuffers\GeneratedCode\** at evaluation and re-injects it inside the
            # GenerateProtobufCode target, so skipping the build loses every generated type.
            $inspectArgs = @(
                $Solution
                "-o=$ReportPath"
                '-f=Xml'
                '--no-swea'
                "-e=$Severity"
                "--properties:Configuration=$Configuration;Platform=$Platform;IAgreeToVendorLicenses=true"
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

    if (-not (Test-Path $ReportPath)) {
        Complete-Inspection 'error' 'inspectcode produced no report'
    }

    # Keep TeamCity's Inspections tab populated exactly as the standalone config populated it,
    # and keep the raw report downloadable - the tab summarises, the XML is what you diff.
    Write-Host ("##teamcity[importData type='ReSharperInspectCode' path='{0}']" -f (Format-TcValue $ReportPath))
    Write-Host ("##teamcity[publishArtifacts '{0} =>.teamcity/skyline-inspectcode']" -f (Format-TcValue $ReportPath))

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
        if (-not $bySeverity.ContainsKey($issueSeverity)) { $bySeverity[$issueSeverity] = 0 }
        if (-not $byType.ContainsKey($typeId)) { $byType[$typeId] = 0 }
        $bySeverity[$issueSeverity]++
        $byType[$typeId]++
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
    }

    if ($total -eq 0) {
        Complete-Inspection 'success' "No inspections at $Severity or above"
    }

    $counts = ($bySeverity.GetEnumerator() | Sort-Object -Property Key |
               ForEach-Object { "$($_.Value) $($_.Key.ToLowerInvariant())" }) -join ', '
    Complete-Inspection 'failure' "$total inspections ($counts)"
} catch {
    # Anything unforeseen still has to leave a verdict behind rather than a failed build step.
    Complete-Inspection 'error' "code inspection threw: $($_.Exception.Message)"
}
