#!/usr/bin/env pwsh
# Head-to-head parity + bench for the UNIFI / waters_connect harness URLs:
# convert each URL with cpp msconvert and pwiz-sharp's msconvert-sharp, time
# both wall-clock, msdiff the outputs. Mirrors how the local-file vendor benches
# work, just with HTTP URLs instead of .raw / .d directories.
#
# `--filter "index 0-1"` matches what the cpp Reader_UNIFI_Test fixtures were
# generated with (cpp Reader_UNIFI_Test.cpp:99-125 — the last `IsUnifi()` pass
# overwrites earlier ranges with (0,1)). The reference mzMLs the harness diffs
# against carry just two spectra; we mirror that here so the timing is
# spectra-bound rather than network-bound.

[CmdletBinding()]
param(
    [string]$CppMsConvert      = 'C:\dev\pwiz\build-nt-x86\msvc-release-x86_64\msconvert.exe',
    [string]$SharpMsConvert    = 'C:\dev\pwiz-msconvert-pr\pwiz-sharp\Tools\MsConvert\Tools\MsConvert\Tools\MsConvert\Tools\Commandline\MsConvert\Tools\Commandline\MsConvert\src\bin\Release\net8.0\msconvert-sharp.exe',
    [string]$MsDiff            = 'C:\dev\pwiz\build-nt-x86\msvc-release-x86_64\msdiff.exe',
    [string]$UrlsFile          = 'C:\dev\pwiz-msconvert-pr\pwiz\data\vendor_readers\UNIFI\Reader_UNIFI_Test.data\urls.txt',
    [string]$OutDir            = (Join-Path $env:TEMP "unifi-bench-$([DateTime]::Now.ToString('yyyyMMdd-HHmmss'))"),
    [string]$Filter            = 'index 0-1'
)

$ErrorActionPreference = 'Stop'

foreach ($p in @($CppMsConvert, $SharpMsConvert, $MsDiff, $UrlsFile)) {
    if (-not (Test-Path $p)) { throw "missing required path: $p" }
}
foreach ($v in @('UNIFI_PASSWORD', 'WC_PASSWORD')) {
    if (-not [Environment]::GetEnvironmentVariable($v)) {
        Write-Warning "$v not set; live URLs may fail OAuth"
    }
}

$cppOut   = Join-Path $OutDir 'cpp'
$sharpOut = Join-Path $OutDir 'sharp'
New-Item -ItemType Directory -Force -Path $cppOut, $sharpOut | Out-Null

$urls = Get-Content $UrlsFile | ForEach-Object { $_.Trim() } |
        Where-Object { $_ -and -not $_.StartsWith('#') }

# cpp msconvert (current build) requires credentials embedded in the URL — it doesn't
# read the *_PASSWORD env vars the way pwiz-sharp does. Splice in `<user>:<pwd>@` so
# both binaries see authenticated URLs. Skyline's convention (pwiz_tools/Skyline/
# TestUtil/{UnifiTestUtil,WatersConnectTestUtil}.cs): hardcoded usernames msconvert /
# skyline, password from UNIFI_PASSWORD / WC_PASSWORD.
function Add-Credentials([string]$url) {
    $isWc = $url -match 'sampleSetId='
    $user = if ($isWc) { 'skyline' } else { 'msconvert' }
    $pass = if ($isWc) { $env:WC_PASSWORD } else { $env:UNIFI_PASSWORD }
    if (-not $pass) { throw "missing password env var for $url" }
    $escapedPass = [Uri]::EscapeDataString($pass)
    return ($url -replace '^https://', "https://${user}:${escapedPass}@")
}

Write-Host "Bench output: $OutDir"
Write-Host "Filter:       $Filter"
Write-Host "URL count:    $($urls.Count)"
Write-Host ''

$results = @()
foreach ($url in $urls) {
    $tag = if ($url -match 'sampleSetId=([0-9a-f-]+)') { "wc-$($Matches[1].Substring(0,8))" }
           elseif ($url -match 'sampleresults\(([0-9a-f-]+)\)') { "unifi-$($Matches[1].Substring(0,8))" }
           else { 'unknown' }
    Write-Host "[$tag]"

    $authUrl = Add-Credentials $url
    $cppSw = [Diagnostics.Stopwatch]::StartNew()
    & $CppMsConvert $authUrl --filter $Filter -o $cppOut --outfile "$tag.mzML" --64 --zlib *>&1 | Out-Null
    $cppSw.Stop()
    $cppMs = [int]$cppSw.Elapsed.TotalMilliseconds
    $cppFile = Join-Path $cppOut "$tag.mzML"
    $cppOk = Test-Path $cppFile
    Write-Host ("  cpp:    {0,6} ms   {1}" -f $cppMs, ($(if ($cppOk) {"ok ($([Math]::Round((Get-Item $cppFile).Length/1KB))kB)"} else {'FAIL'})))

    # pwiz-sharp accepts the bare URL via the *_PASSWORD env vars; pass the same
    # credentialed URL anyway so we time identical inputs and rule out any auth
    # path skew between the two binaries.
    $sharpSw = [Diagnostics.Stopwatch]::StartNew()
    & $SharpMsConvert $authUrl --filter $Filter -o $sharpOut --outfile "$tag.mzML" --64 --zlib *>&1 | Out-Null
    $sharpSw.Stop()
    $sharpMs = [int]$sharpSw.Elapsed.TotalMilliseconds
    $sharpFile = Join-Path $sharpOut "$tag.mzML"
    $sharpOk = Test-Path $sharpFile
    Write-Host ("  sharp:  {0,6} ms   {1}" -f $sharpMs, ($(if ($sharpOk) {"ok ($([Math]::Round((Get-Item $sharpFile).Length/1KB))kB)"} else {'FAIL'})))

    # msdiff exits 0 on parity, 1 when there are differences. Capture stderr+stdout
    # so we can quote the first few lines on a mismatch.
    $diffStatus = 'skip'
    $diffOutput = ''
    if ($cppOk -and $sharpOk) {
        # `-i` enables ignore-metadata (skips source-file SHA, timestamps, software versions)
        # — that's the closest equivalent to the diff config the harness test uses.
        $diffOutput = & $MsDiff -i $cppFile $sharpFile 2>&1 | Out-String
        $diffStatus = if ($LASTEXITCODE -eq 0) { 'PARITY' } else { 'DIFF' }
    }
    Write-Host "  diff:   $diffStatus"
    if ($diffStatus -eq 'DIFF') {
        $diffOutput.Split("`n") | Select-Object -First 40 | ForEach-Object { Write-Host "    $_" }
    }

    $results += [pscustomobject]@{
        Tag       = $tag
        CppMs     = $cppMs
        SharpMs   = $sharpMs
        CppOk     = $cppOk
        SharpOk   = $sharpOk
        Diff      = $diffStatus
    }
    Write-Host ''
}

Write-Host '===== Summary ====='
$results | Format-Table -AutoSize Tag, CppMs, SharpMs, CppOk, SharpOk, Diff
