#!/usr/bin/env pwsh
# Head-to-head parity + bench for the Mobilion reader: convert each .mbi fixture
# with cpp msconvert and pwiz-sharp's msconvert-sharp, time both wall-clock,
# msdiff the outputs. Modeled on Bench-UNIFI.ps1; the .mbi fixtures are local
# files (no auth) so the script is simpler.
#
# `--filter "index 0-100"` matches what the cpp Reader_Mobilion_Test fixtures
# were generated with (cpp Reader_Mobilion_Test.cpp:59 `subsetConfig.indexRange
# = make_pair(0, 100)` for the default + ignoreZeros configs); ExampleTuneMix
# also runs without indexRange to cover the larger combine-IMS variant.

[CmdletBinding()]
param(
    [string]$CppMsConvert      = 'C:\dev\pwiz\build-nt-x86\msvc-release-x86_64\msconvert.exe',
    [string]$SharpMsConvert    = 'C:\dev\pwiz-msconvert-pr\pwiz-sharp\src\MsConvert\bin\Release\net8.0\msconvert-sharp.exe',
    [string]$MsDiff            = 'C:\dev\pwiz\build-nt-x86\msvc-release-x86_64\msdiff.exe',
    [string]$DataDir           = 'C:\dev\pwiz-msconvert-pr\pwiz\data\vendor_readers\Mobilion\Reader_Mobilion_Test.data',
    [string]$OutDir            = (Join-Path $env:TEMP "mobilion-bench-$([DateTime]::Now.ToString('yyyyMMdd-HHmmss'))")
)

$ErrorActionPreference = 'Stop'

foreach ($p in @($CppMsConvert, $SharpMsConvert, $MsDiff, $DataDir)) {
    if (-not (Test-Path $p)) { throw "missing required path: $p" }
}

$cppOut   = Join-Path $OutDir 'cpp'
$sharpOut = Join-Path $OutDir 'sharp'
New-Item -ItemType Directory -Force -Path $cppOut, $sharpOut | Out-Null

# (fixture-name, [extra-msconvert-args]) pairs. The arg list excludes -o/--outfile
# (added per-run below) and excludes --64/--zlib (left at each binary's defaults).
$cases = @(
    @{ File = 'ExampleTuneMix_binned5.mbi';                Tag = 'tunemix-default';        Args = @('--filter', 'index 0-100') }
    @{ File = 'ExampleTuneMix_binned5.mbi';                Tag = 'tunemix-combineIMS';     Args = @('--combineIonMobilitySpectra') }
    @{ File = '2024-02-16-16.02.20-CCS Calibration_02.mbi'; Tag = 'ccs-default';            Args = @('--filter', 'index 0-100') }
    @{ File = '2024-02-16-16.02.20-CCS Calibration_02.mbi'; Tag = 'ccs-combineIMS';         Args = @('--combineIonMobilitySpectra') }
)

Write-Host "Bench output: $OutDir"
Write-Host "Fixtures:     $($cases.Count)"
Write-Host ''

$results = @()
foreach ($c in $cases) {
    $tag      = $c.Tag
    $fixture  = Join-Path $DataDir $c.File
    $args     = $c.Args
    Write-Host "[$tag]  $($c.File)  $($args -join ' ')"

    if (-not (Test-Path $fixture)) {
        Write-Host "  MISSING: $fixture"
        continue
    }

    # cpp msconvert: pass --64 --zlib explicitly so output binary encoding matches
    # what msconvert-sharp emits by default (msconvert-cpp's default is --32 --no-zlib).
    $cppFile = Join-Path $cppOut "$tag.mzML"
    $cppSw = [Diagnostics.Stopwatch]::StartNew()
    & $CppMsConvert $fixture @args --64 --zlib -o $cppOut --outfile "$tag.mzML" *>&1 | Out-Null
    $cppSw.Stop()
    $cppMs = [int]$cppSw.Elapsed.TotalMilliseconds
    $cppOk = Test-Path $cppFile
    Write-Host ("  cpp:    {0,6} ms   {1}" -f $cppMs, ($(if ($cppOk) {"ok ($([Math]::Round((Get-Item $cppFile).Length/1KB))kB)"} else {'FAIL'})))

    $sharpFile = Join-Path $sharpOut "$tag.mzML"
    $sharpSw = [Diagnostics.Stopwatch]::StartNew()
    & $SharpMsConvert $fixture @args --64 --zlib -o $sharpOut --outfile "$tag.mzML" *>&1 | Out-Null
    $sharpSw.Stop()
    $sharpMs = [int]$sharpSw.Elapsed.TotalMilliseconds
    $sharpOk = Test-Path $sharpFile
    Write-Host ("  sharp:  {0,6} ms   {1}" -f $sharpMs, ($(if ($sharpOk) {"ok ($([Math]::Round((Get-Item $sharpFile).Length/1KB))kB)"} else {'FAIL'})))

    # `-i` enables ignore-metadata (skips source-file SHA, timestamps, software versions).
    $diffStatus = 'skip'
    $diffOutput = ''
    if ($cppOk -and $sharpOk) {
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
