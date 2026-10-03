<#
.SYNOPSIS
Locate the pwiz-sharp/ root via anchor discovery (sentinel: pwiz/+Tools/+Pwiz.sln).

.DESCRIPTION
Dot-source this from any pwiz-sharp PowerShell script that needs to resolve paths
against the source-tree root. It defines a single function, Get-PwizSharpRoot,
and a script-scoped $PwizSharpRoot variable for convenience.

Discovery walks parents of the dot-sourcing script's $PSScriptRoot until it finds
a directory containing all three sentinels (pwiz/, Tools/, Pwiz.sln). Future tree
restructures need only update this one function; no other script needs to change.

.EXAMPLE
    . "$PSScriptRoot/../scripts/Get-PwizSharpRoot.ps1"
    $msconvertGui = Join-Path $PwizSharpRoot "Tools/MsConvertGUI/src/MsConvertGUI.csproj"
#>

function Get-PwizSharpRoot {
    param(
        [string] $StartFrom = $PSScriptRoot
    )
    $dir = $StartFrom
    while ($dir) {
        if ((Test-Path (Join-Path $dir 'pwiz')) `
            -and (Test-Path (Join-Path $dir 'Tools')) `
            -and (Test-Path (Join-Path $dir 'Pwiz.sln'))) {
            return (Resolve-Path $dir).Path
        }
        # Also try sibling (handle case where caller is alongside pwiz-sharp).
        $sibling = Join-Path $dir 'pwiz-sharp'
        if ((Test-Path $sibling) `
            -and (Test-Path (Join-Path $sibling 'pwiz')) `
            -and (Test-Path (Join-Path $sibling 'Tools')) `
            -and (Test-Path (Join-Path $sibling 'Pwiz.sln'))) {
            return (Resolve-Path $sibling).Path
        }
        $parent = Split-Path -Parent $dir
        if ($parent -eq $dir) { break }
        $dir = $parent
    }
    throw "Get-PwizSharpRoot: cannot find pwiz-sharp root from '$StartFrom'. Expected an ancestor containing pwiz/, Tools/, and Pwiz.sln."
}

# Convenience: set $PwizSharpRoot in the dot-sourcing script's scope.
$script:PwizSharpRoot = Get-PwizSharpRoot
