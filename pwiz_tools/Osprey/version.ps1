<#
.SYNOPSIS
    Shared Osprey version computation (Skyline scheme YEAR.ORDINAL.BRANCH.DOY).

.DESCRIPTION
    Dot-sourced by build.ps1 (stamps the assembly via /p:Version) and by
    package.ps1 (names the redistributable artifacts). Keeping the formula in
    one place means the version baked into Osprey.exe and the version in the
    ZIP/.msi file names can never drift.

    Mirrors pwiz_tools/Skyline/Jamfile.jam: YEAR/ORDINAL/BRANCH are the
    release-line constants; DOY is the day-of-year of the git commit date
    (reproducible across rebuilds of the same commit), offset by 365 per year
    past YEAR. Falls back to the current date outside a git checkout.
#>

function Get-OspreyVersion {
    param(
        # Repo path used to read the committed date. Mandatory by design: a
        # dot-sourced function cannot reliably derive its own script directory
        # at call time ($PSScriptRoot / $PSCommandPath resolve to the CALLER's
        # scope, not version.ps1), so each caller (build.ps1 / package.ps1)
        # passes its own $scriptRoot explicitly.
        [Parameter(Mandatory = $true)] [string]$RepoPath
    )

    $OSPREY_YEAR = 26
    $OSPREY_ORDINAL = 1
    $OSPREY_BRANCH = 1

    $gitDate = & git -C $RepoPath log -1 --format=%cs HEAD 2>$null
    if ($LASTEXITCODE -eq 0 -and $gitDate -match '^\d{4}-\d{2}-\d{2}$') {
        $verDate = [datetime]::ParseExact($gitDate.Trim(), 'yyyy-MM-dd', [cultureinfo]::InvariantCulture)
    } else {
        $verDate = Get-Date
    }

    $doy = (([int]$verDate.ToString('yy')) - $OSPREY_YEAR) * 365 + $verDate.DayOfYear
    return "$OSPREY_YEAR.$OSPREY_ORDINAL.$OSPREY_BRANCH.$doy"
}
