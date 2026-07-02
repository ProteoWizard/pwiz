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

function Get-OspreyInformationalVersion {
    <#
    .SYNOPSIS
        The numeric version plus the git short hash (and a -dirty marker for a
        modified tree): YEAR.ORDINAL.BRANCH.DOY-<shorthash>[-dirty].
    .DESCRIPTION
        Mirrors the Skyline AssemblyInformationalVersion scheme
        (pwiz_tools/Skyline/Util/Install.cs parses the "<numeric>-<hash>" form).
        OspreyVersion.DisplayVersion renders it Skyline-style as
        "26.1.1.182 (b2373f9f9c)". This is the single source of the hash stamp:
        build.ps1 (binary), package.ps1 (redistributable), and the
        Directory.Build.targets stamping target for every other build path all
        derive from it, so the hash can never drift across build paths. Falls
        back to the bare numeric version outside a git checkout.
    #>
    param(
        [Parameter(Mandatory = $true)] [string]$RepoPath
    )

    $numeric = Get-OspreyVersion -RepoPath $RepoPath

    # --short=10 is a MINIMUM width: git lengthens the abbreviation when 10 chars
    # would be ambiguous in a given clone, so the hash LENGTH (not identity) can
    # differ across machines. It still resolves to the same commit; the numeric
    # version (the parquet-cache key) is fully deterministic regardless.
    $shortHash = & git -C $RepoPath rev-parse --short=10 HEAD 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($shortHash)) {
        return $numeric   # not a git checkout (e.g. an extracted source tree)
    }
    $shortHash = $shortHash.Trim()

    # A -dirty marker so a modified build can never masquerade as a clean commit.
    # Untracked files (build artifacts, .tmp scratch) are ignored -- only tracked
    # modifications change what compiles.
    $dirty = & git -C $RepoPath status --porcelain --untracked-files=no 2>$null
    $suffix = if ([string]::IsNullOrWhiteSpace($dirty)) { '' } else { '-dirty' }

    return "$numeric-$shortHash$suffix"
}
