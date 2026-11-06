<#
.SYNOPSIS
    The post-build warning check shared by tcinspect.ps1 (pwiz-sharp, Core Windows) and
    pwiz_tools/Skyline/tcinspect.ps1 (Skyline).

.DESCRIPTION
    The Directory.Build.props files do not promote warnings to errors, because a promoted warning
    stops the build before any test runs - including one the commit did not cause, such as a NuGet
    advisory published overnight. Instead each tcinspect.ps1 pre-builds with the warnings-only file
    logger from Get-BuildWarningsLoggerArgs, before the build step builds on top of it
    incrementally, reads the log back with Write-BuildWarnings, and reports the verdict in its own
    GitHub commit status through Publish-GitHubStatus. Warnings turn that one check red; the
    TeamCity build and its own status are unaffected. Publish-BuildWarningInspections also lists
    each warning on the TeamCity build's Code Inspection tab, which does not fail the build either.
#>

Set-StrictMode -Version Latest

# TeamCity service message values escape these six.
function Format-TcValue([string] $text) {
    if ([string]::IsNullOrEmpty($text)) { return '' }
    return $text.Replace('|', '||').Replace("'", "|'").Replace("`n", '|n').Replace("`r", '|r').Replace('[', '|[').Replace(']', '|]')
}

# A second, warnings-only file logger next to the console one, for every restore and build that
# writes to the same log - hence 'append'. The logger creates the file on its first build,
# warnings or not.
function Get-BuildWarningsLoggerArgs([string] $LogPath) {
    return @('-fl9', "-flp9:warningsonly;logfile=$LogPath;append;verbosity=minimal")
}

# Prints the warnings in $LogPath and returns their count, or $null when there is no log - no
# build ran under the logger, so there is no evidence either way. Restore warnings are logged
# twice - by the restore and again when the build replays them - and a source file shared between
# projects warns once per project, so each line is compared without the trailing ' [project]' a
# compiler warning carries.
function Write-BuildWarnings {
    param(
        [Parameter(Mandatory)] [string] $LogPath,
        [Parameter(Mandatory)] [string] $ArtifactDir
    )
    if (-not (Test-Path $LogPath)) {
        Write-Host "##teamcity[message text='No build warnings log at $(Format-TcValue $LogPath); build warnings were not checked' status='WARNING']"
        return $null
    }
    $warnings = @(Get-Content -LiteralPath $LogPath |
                  Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
                  ForEach-Object { ($_ -replace '\s+\[[^\]]+\]\s*$', '').Trim() } |
                  Sort-Object -Unique)
    Write-Host ("##teamcity[publishArtifacts '{0} => {1}']" -f (Format-TcValue $LogPath), $ArtifactDir)
    if ($warnings.Count -eq 0) {
        Write-Host 'No build warnings.'
        return 0
    }
    Write-Host ''
    Write-Host "$($warnings.Count) build warning(s); fix them, or demote one with NoWarn and a comment saying why:"
    foreach ($warning in $warnings) { Write-Host "  $warning" }
    return $warnings.Count
}

# Reports each warning in $LogPath as a TeamCity inspection, so the build's Code Inspection tab
# lists them; otherwise they can be read only in the build log. An inspection does not fail the
# build, which a buildProblem would. TeamCity files an inspection under a path relative to the
# checkout, so a warning raised in a file outside it - MSB3270 points at the SDK's
# Microsoft.Common.CurrentVersion.targets - is filed under the project that raised it, with the
# original location kept in the message. Not for a build that also imports a ReSharper report,
# whose inspections already fill that tab.
function Publish-BuildWarningInspections {
    param(
        [Parameter(Mandatory)] [string] $LogPath,
        [string] $RepoRoot = (Split-Path -Parent $PSScriptRoot)
    )
    if (-not (Test-Path $LogPath)) { return }
    $root = [System.IO.Path]::GetFullPath($RepoRoot).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar

    # A checkout-relative path with forward slashes, or $null for a path outside the checkout.
    function Get-CheckoutPath([string] $path) {
        if ([string]::IsNullOrWhiteSpace($path)) { return $null }
        try { $full = [System.IO.Path]::GetFullPath($path.Trim()) } catch { return $null }
        if (-not $full.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) { return $null }
        return $full.Substring($root.Length).Replace('\', '/')
    }

    # file(line,column): warning CODE: message [project] - MSBuild's form for every warning, with
    # the location, the line and the project each optional.
    $pattern = '^\s*(?<file>.+?)(?:\((?<line>\d+)(?:,\d+)*\))?\s*:\s*warning\s+(?<code>[A-Za-z]+\d+)\s*:\s*(?<message>.*?)(?:\s+\[(?<project>[^\[\]]+)\])?\s*$'
    $declaredTypes = @{}
    $reported = @{}
    foreach ($entry in Get-Content -LiteralPath $LogPath) {
        if ($entry -notmatch $pattern) { continue }
        $code = $Matches['code'].ToUpperInvariant()
        $message = $Matches['message']
        $line = $Matches['line']
        $file = Get-CheckoutPath $Matches['file']
        if ($null -eq $file) {
            $file = Get-CheckoutPath $Matches['project']
            $location = $Matches['file'].Trim()
            if ($line) { $location += "($line)" }
            $message = "$message ($location)"
            $line = $null
        }
        if ($null -eq $file) { $file = $Matches['file'].Trim() }

        # The same warning arrives once per project that compiles a shared file, and a restore
        # warning twice.
        $key = "$code|$file|$line|$message"
        if ($reported.ContainsKey($key)) { continue }
        $reported[$key] = $true

        if (-not $declaredTypes.ContainsKey($code)) {
            $declaredTypes[$code] = $true
            Write-Host ("##teamcity[inspectionType id='{0}' name='{0}' category='Build warnings' description='Compiler, restore or MSBuild warning {0}']" -f (Format-TcValue $code))
        }
        $lineAttribute = if ($line) { " line='$line'" } else { '' }
        Write-Host ("##teamcity[inspection typeId='{0}' message='{1}' file='{2}'{3} SEVERITY='WARNING']" -f
            (Format-TcValue $code), (Format-TcValue $message), (Format-TcValue $file), $lineAttribute)
    }
}

# Posts a GitHub commit status. Never throws and never retries: a status that fails to post is
# worth a warning in the log, not a failed build. The token is a TeamCity credentialsJSON value,
# so it is never echoed. Without a token or commit - a developer running a tcinspect.ps1 locally -
# it says so and posts nothing.
function Publish-GitHubStatus {
    param(
        [Parameter(Mandatory)] [ValidateSet('pending', 'success', 'failure', 'error')] [string] $State,
        [Parameter(Mandatory)] [string] $Description,
        [Parameter(Mandatory)] [string] $Context,
        [string] $CommitSha,
        [string] $TargetUrl,
        [string] $Repository = 'ProteoWizard/pwiz'
    )
    # GitHub truncates a commit status description at 140 characters; cut it here, where the
    # tail can still be read in the build log.
    if ($Description.Length -gt 140) { $Description = $Description.Substring(0, 137) + '...' }
    $missing = @()
    if ([string]::IsNullOrEmpty($env:GITHUB_STATUS_TOKEN)) { $missing += 'GITHUB_STATUS_TOKEN' }
    if ([string]::IsNullOrEmpty($CommitSha)) { $missing += 'BUILD_VCS_NUMBER' }
    if ($missing.Count -gt 0) {
        Write-Host "  (not posting '$State' to '$Context': $($missing -join ' and ') $(if ($missing.Count -gt 1) { 'are' } else { 'is' }) empty)"
        return
    }
    $body = @{ state = $State; context = $Context; description = $Description }
    if (-not [string]::IsNullOrEmpty($TargetUrl)) { $body['target_url'] = $TargetUrl }
    try {
        Invoke-RestMethod -Method Post -ContentType 'application/json' `
            -Uri "https://api.github.com/repos/$Repository/statuses/$CommitSha" `
            -Headers @{ Authorization = "Bearer $env:GITHUB_STATUS_TOKEN" } `
            -Body ($body | ConvertTo-Json -Compress) | Out-Null
        Write-Host "  posted '$State' to GitHub status '$Context' on $CommitSha"
    } catch {
        Write-Host "##teamcity[message text='Could not post the $(Format-TcValue $Context) status: $(Format-TcValue $_.Exception.Message)' status='WARNING']"
    }
}

Export-ModuleMember -Function Format-TcValue, Get-BuildWarningsLoggerArgs, Write-BuildWarnings, Publish-BuildWarningInspections, Publish-GitHubStatus
