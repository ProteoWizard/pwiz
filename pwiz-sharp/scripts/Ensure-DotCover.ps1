<#
.SYNOPSIS
    Makes the dotCover console runner available from a local tool manifest.

.DESCRIPTION
    Restores the JetBrains dotCover command line tools from the .config/dotnet-tools.json
    of the given directory, so a build can run coverage without the tool being installed
    globally on the machine. Shared by Core (pwiz-sharp), Skyline and Osprey, each of which
    keeps its own manifest.

    The version is deliberately per-app rather than shared: Skyline's TestRunner resolves
    ...\jetbrains.dotcover.commandlinetools\2023.3.3\tools\dotCover.exe by path and passes the
    old /Filters= syntax, while Osprey's build.ps1 passes the kebab-case flags that replaced it
    (--exclude-assemblies and friends). One version cannot satisfy both without rewriting one
    caller's arguments, so each manifest pins what its caller was written against.

    A local manifest is used rather than `dotnet tool install -g` so the version is pinned in
    source and the build leaves nothing behind on the agent. Note the global package name that
    was previously suggested in error messages here, JetBrains.dotCover.GlobalTools, does not
    exist on nuget.org at all - the package is JetBrains.dotCover.CommandLineTools.

.PARAMETER ManifestDir
    Directory holding .config/dotnet-tools.json.

.PARAMETER TeamCity
    Emit TeamCity service messages for progress and failures.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $ManifestDir,
    [switch] $TeamCity
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Info([string] $text) {
    if ($TeamCity) { Write-Host "##teamcity[progressMessage '$text']" } else { Write-Host $text }
}

function Write-Failure([string] $text) {
    if ($TeamCity) { Write-Host "##teamcity[message text='$text' status='ERROR']" } else { Write-Host $text }
}

$manifest = Join-Path $ManifestDir '.config/dotnet-tools.json'
if (-not (Test-Path $manifest)) {
    Write-Failure "No dotnet tool manifest at $manifest; cannot restore dotCover."
    exit 2
}

Write-Info "dotnet tool restore - $manifest"
Push-Location $ManifestDir
try {
    # Idempotent and fast once the package is in the NuGet cache, so callers can invoke this
    # unconditionally rather than probing first.
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) {
        Write-Failure "``dotnet tool restore`` failed in $ManifestDir; coverage cannot run."
        exit 2
    }

    # Prove the tool actually runs. A restore can succeed while the tool is unusable (wrong
    # RID, partial cache), and finding that out here beats failing inside the test step.
    # `help` rather than `--version`: 2023.3.3 exits 127 on --version while 2026.1.1 exits 0,
    # and `help` is 0 on both, so the probe does not assume either CLI generation.
    $null = dotnet dotcover help 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Failure "dotCover restored from $manifest but ``dotnet dotcover help`` failed."
        exit 2
    }
}
finally {
    Pop-Location
}

Write-Info 'dotCover is available as: dotnet dotcover'
exit 0
