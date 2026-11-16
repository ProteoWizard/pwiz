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
    TeamCity build and its own status are unaffected.
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

Export-ModuleMember -Function Format-TcValue, Get-BuildWarningsLoggerArgs, Write-BuildWarnings, Publish-GitHubStatus
