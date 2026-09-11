<#
.SYNOPSIS
Locate the repo root via anchor discovery (sentinel: pwiz/+pwiz_tools/+Pwiz.sln).

.DESCRIPTION
Dot-source this from any pwiz PowerShell script that needs to resolve paths
against the source-tree root. It defines a single function, Get-PwizSharpRoot,
and a script-scoped $PwizSharpRoot variable for convenience.

Discovery walks parents of the dot-sourcing script's $PSScriptRoot until it finds
a directory containing all three sentinels (pwiz/, pwiz_tools/, Pwiz.sln). Future tree
restructures need only update this one function; no other script needs to change.

.EXAMPLE
    . "$PSScriptRoot/../scripts/Get-PwizSharpRoot.ps1"
    $msconvertGui = Join-Path $PwizSharpRoot "pwiz_tools/MSConvertGUI/MsConvertGUI.csproj"
#>

function Get-PwizSharpRoot {
    param(
        [string] $StartFrom = $PSScriptRoot
    )
    $dir = $StartFrom
    while ($dir) {
        if ((Test-Path (Join-Path $dir 'pwiz')) `
            -and (Test-Path (Join-Path $dir 'pwiz_tools')) `
            -and (Test-Path (Join-Path $dir 'Pwiz.sln'))) {
            return (Resolve-Path $dir).Path
        }
        $parent = Split-Path -Parent $dir
        if ($parent -eq $dir) { break }
        $dir = $parent
    }
    throw "Get-PwizSharpRoot: cannot find the repo root from '$StartFrom'. Expected an ancestor containing pwiz/, pwiz_tools/, and Pwiz.sln."
}

# Convenience: set $PwizSharpRoot in the dot-sourcing script's scope.
$script:PwizSharpRoot = Get-PwizSharpRoot
