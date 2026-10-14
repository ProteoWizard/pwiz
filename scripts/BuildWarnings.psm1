<#
.SYNOPSIS
    The post-build warning gate shared by tcinspect.ps1 (pwiz-sharp, Core Windows) and
    pwiz_tools/Skyline/tcinspect.ps1 (Skyline).

.DESCRIPTION
    The Directory.Build.props files do not promote warnings to errors, because a promoted warning
    stops the build before any test runs - including one the commit did not cause, such as a NuGet
    advisory published overnight. Instead each tcinspect.ps1 pre-builds with the warnings-only file
    logger from Get-BuildWarningsLoggerArgs, before the build step builds on top of it
    incrementally, and then calls Write-BuildWarnings. Any warning raises a TeamCity buildProblem,
    which marks the build failed while the script still exits 0, so the steps after it - tests
    included - still run.
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

# Reports the warnings in $LogPath and returns their count. Restore warnings are logged twice -
# by the restore and again when the build replays them - and a source file shared between projects
# warns once per project, so each line is compared without the trailing ' [project]' a compiler
# warning carries.
function Write-BuildWarnings {
    param(
        [Parameter(Mandatory)] [string] $LogPath,
        # The buildProblem identity: fixed per configuration, so TeamCity can tell a new problem
        # from one the previous build also had.
        [Parameter(Mandatory)] [string] $ProblemIdentity,
        [Parameter(Mandatory)] [string] $ArtifactDir
    )
    if (-not (Test-Path $LogPath)) {
        # No log means no build ran under the logger: no evidence the build is warning-free.
        Write-Host "##teamcity[message text='No build warnings log at $(Format-TcValue $LogPath); build warnings were not checked' status='WARNING']"
        return 0
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
    Write-Host ("##teamcity[buildProblem identity='{0}' description='{1}']" -f $ProblemIdentity,
                (Format-TcValue "$($warnings.Count) build warning(s), first: $($warnings[0])"))
    return $warnings.Count
}

Export-ModuleMember -Function Get-BuildWarningsLoggerArgs, Write-BuildWarnings
