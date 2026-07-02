<#
.SYNOPSIS
    Prints the Osprey informational version (YEAR.ORDINAL.BRANCH.DOY-<hash>[-dirty])
    to stdout for the Directory.Build.targets stamping target.

.DESCRIPTION
    Directory.Build.targets invokes this on every build path (a plain
    `dotnet build`, a Debug build in VS, a Carafe/redistribution `dotnet
    publish`) so those binaries carry the same commit-derived version + git hash
    that build.ps1 / package.ps1 stamp. Dot-sources version.ps1 -- the single
    source of the version formula -- so the stamp can never drift from the
    binary the release scripts produce.
#>
. (Join-Path $PSScriptRoot 'version.ps1')
Write-Output (Get-OspreyInformationalVersion -RepoPath $PSScriptRoot)
