# DIAGNOSTIC (temporary): collect the MSAmanda Percolator input (.pin) and mzIdentML
# output the DiaUmpire test leaves on disk, into $DumpDir, so a CI-agent run and a local
# run can be diffed to localize per-machine result drift. Robust to the wrapper env var
# not reaching the test process: it searches the test downloads folder directly. Prints
# everything it does (prefix CAPTURE:) so the CI log is self-diagnosing. Never fails the build.
param(
    [Parameter(Mandatory=$true)][string]$DumpDir,
    [int]$RecentMinutes = 90   # only collect files written during this build, to scope + shrink the artifact
)

$ErrorActionPreference = 'Continue'
try {
    New-Item -ItemType Directory -Force -Path $DumpDir | Out-Null

    Write-Host "CAPTURE: DumpDir=$DumpDir"
    Write-Host "CAPTURE: SKYLINE_MSAMANDA_DUMP_DIR=[$env:SKYLINE_MSAMANDA_DUMP_DIR]"
    Write-Host "CAPTURE: SKYLINE_DOWNLOAD_PATH=[$env:SKYLINE_DOWNLOAD_PATH]"

    # Whatever the wrapper already wrote (if its env propagated) stays; list it.
    $pre = Get-ChildItem -Path $DumpDir -File -ErrorAction SilentlyContinue
    Write-Host ("CAPTURE: wrapper-dumped files already present: {0}" -f ($pre.Count))
    foreach ($f in $pre) { Write-Host ("CAPTURE:   have {0} ({1:N1} MB)" -f $f.Name, ($f.Length/1MB)) }

    # Search roots for the persistent test data (Skyline's GetDownloadsPath = this env var,
    # else the user's Downloads folder). Add common fallbacks.
    $roots = @()
    if ($env:SKYLINE_DOWNLOAD_PATH) { $roots += $env:SKYLINE_DOWNLOAD_PATH }
    $roots += "$env:USERPROFILE\Downloads"
    $roots += "$env:PUBLIC\Downloads"
    $roots += "C:\test"
    $roots = $roots | Where-Object { $_ -and (Test-Path $_) } | Select-Object -Unique
    Write-Host ("CAPTURE: search roots: {0}" -f ($roots -join ' ; '))

    $cutoff = (Get-Date).AddMinutes(-$RecentMinutes)
    Write-Host ("CAPTURE: collecting files written after {0:o} (last {1} min)" -f $cutoff, $RecentMinutes)
    $found = @()
    foreach ($r in $roots) {
        $hits = Get-ChildItem -Path $r -Recurse -File -ErrorAction SilentlyContinue -Include '*-diaumpire_pin.tsv','*-diaumpire.mzid.gz' |
                Where-Object { $_.LastWriteTime -gt $cutoff }
        if ($hits) { $found += $hits }
    }
    $found = $found | Sort-Object FullName -Unique
    Write-Host ("CAPTURE: found {0} pin/mzid file(s) under downloads" -f $found.Count)
    foreach ($f in $found) {
        Write-Host ("CAPTURE:   FOUND {0}  {1:N1} MB  {2:o}" -f $f.FullName, ($f.Length/1MB), $f.LastWriteTime)
        $dest = Join-Path $DumpDir $f.Name
        if (-not (Test-Path $dest)) {
            Copy-Item -LiteralPath $f.FullName -Destination $dest -Force -ErrorAction SilentlyContinue
        }
    }

    $final = Get-ChildItem -Path $DumpDir -File -ErrorAction SilentlyContinue
    Write-Host ("CAPTURE: DumpDir now has {0} file(s), {1:N1} MB total" -f $final.Count, (($final | Measure-Object Length -Sum).Sum/1MB))
}
catch {
    Write-Host "CAPTURE: error (ignored): $_"
}
exit 0
