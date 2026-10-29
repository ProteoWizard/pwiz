<#
.SYNOPSIS
    Exits 0 when this TeamCity build is on the configuration's default branch
    (master), 1 when it is on any other branch (a pull/<N> build).

.DESCRIPTION
    tcbuild.bat collects dotCover coverage only on master. Running the Osprey
    subset-pipeline tests under dotCover is roughly 8x slower than running them
    plain (TestSubsetOptionVariants: 12s plain vs 96s covered, 2026-10-28), and
    coverage on every PR build cost about 9 of the config's 14-20 test minutes.
    Master builds still record coverage after every merge, so the running view
    of coverage that catches a slip over time is kept.

    The branch comes from teamcity.build.branch.is_default, a configuration
    parameter. The agent writes those to the file named by the
    teamcity.configuration.properties.file entry of the build properties file
    ($env:TEAMCITY_BUILD_PROPERTIES_FILE), both in Java .properties format.

    Outside TeamCity, or when the parameter cannot be read, this exits 0 so the
    build keeps coverage - the behavior before this check existed.
#>

function Read-JavaProperties([string]$path) {
    $props = @{}
    foreach ($line in Get-Content -LiteralPath $path) {
        if ($line -match '^\s*[#!]' -or $line -notmatch '^\s*([^=:\s]+)\s*[=:]\s*(.*)$') {
            continue
        }
        # Unescape \x -> x, which covers the C\:\\teamcity\\... form of agent paths
        $props[$matches[1]] = $matches[2] -replace '\\(.)', '$1'
    }
    return $props
}

$buildPropsFile = $env:TEAMCITY_BUILD_PROPERTIES_FILE
if (-not $buildPropsFile) {
    exit 0
}
$configFile = $null
if (Test-Path -LiteralPath $buildPropsFile) {
    $configFile = (Read-JavaProperties $buildPropsFile)['teamcity.configuration.properties.file']
}
$isDefault = $null
if ($configFile -and (Test-Path -LiteralPath $configFile)) {
    $isDefault = (Read-JavaProperties $configFile)['teamcity.build.branch.is_default']
}
if ($null -eq $isDefault) {
    Write-Host 'teamcity.build.branch.is_default not found; collecting coverage.'
    exit 0
}
if ($isDefault -eq 'true') {
    exit 0
}
exit 1
