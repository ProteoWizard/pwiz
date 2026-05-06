#!/usr/bin/env pwsh
# Imports per-assembly VSTest TRX files into TeamCity, wrapping each in its own
# testSuiteStarted/Finished pair. Without this wrapper, TC's `##teamcity[importData
# type='vstest']` lumps every test into one suite called "VSTest"; per-assembly
# suites give the build's Tests tab a useful structure.
#
# Each TRX has `<UnitTest storage="...path/to/<assembly>.dll">` elements; we read
# the first one to identify the source assembly. Title-case the bare name so
# `analysis.tests.dll` shows up as `Analysis.Tests` in TC (the trx writes the
# storage path lowercased).
#
# Called from build.bat after `dotnet test` finishes, regardless of whether the
# test run was wrapped in dotCover.

[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$ResultsDir,
    # Defaults to the parent of $PSScriptRoot's directory — i.e., the pwiz-sharp
    # source root when this script lives at pwiz-sharp/scripts/. Override if the
    # caller wants to pin to a different layout.
    [string]$SourceRoot = (Split-Path -Parent $PSScriptRoot),
    # Skip emitting service messages — useful for local diagnostic runs.
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $ResultsDir)) {
    Write-Warning "Results directory not found: $ResultsDir"
    exit 0
}

# Discover the canonical (case-preserved) test project names from the source
# tree. The TRX `storage` attribute writes the dll path lowercased, so without
# this map we'd lose case info on multi-cap names like `UNIFI.Tests`,
# `MsData.Tests`, `MsConvert.Tests`. Each .Tests project lives in its own
# `test/<name>/` directory whose name matches the assembly.
$canonicalNames = @{}
$testDir = Join-Path $SourceRoot 'test'
if (Test-Path $testDir) {
    foreach ($d in Get-ChildItem -LiteralPath $testDir -Directory) {
        $canonicalNames[$d.Name.ToLowerInvariant()] = $d.Name
    }
}

$trxFiles = @(Get-ChildItem -Path $ResultsDir -Filter '*.trx' -Recurse -File)
if ($trxFiles.Count -eq 0) {
    Write-Warning "No TRX files found under $ResultsDir"
    exit 0
}

# TC service messages need certain characters escaped — see
# https://www.jetbrains.com/help/teamcity/service-messages.html#Escaped+values
function Format-TeamCityValue([string]$value) {
    return $value `
        -replace "\|", "||" `
        -replace "'", "|'" `
        -replace "\n", "|n" `
        -replace "\r", "|r" `
        -replace "\[", "|[" `
        -replace "\]", "|]"
}

$xmlNs = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'

foreach ($trx in $trxFiles) {
    # vstest disambiguates same-second filename collisions with `[N]` suffix
    # (e.g. `Matt_AEROWORK_2026-05-06_12_24_58[1].trx`). Without -LiteralPath,
    # PowerShell would treat the brackets as a wildcard character class and
    # fail to find the file.
    [xml]$doc = Get-Content -LiteralPath $trx.FullName -Raw

    $ns = New-Object System.Xml.XmlNamespaceManager $doc.NameTable
    $ns.AddNamespace('t', $xmlNs)
    $firstUnit = $doc.SelectSingleNode('//t:UnitTest', $ns)
    if (-not $firstUnit) {
        Write-Warning "$($trx.Name): no <UnitTest> elements; skipping"
        continue
    }

    $storage = $firstUnit.GetAttribute('storage')
    if ([string]::IsNullOrWhiteSpace($storage)) {
        Write-Warning "$($trx.Name): empty storage attribute; skipping"
        continue
    }
    $dllName = Split-Path $storage -Leaf
    $bare = [IO.Path]::GetFileNameWithoutExtension($dllName).ToLowerInvariant()
    # Prefer the source-tree directory name (preserves multi-cap forms like
    # `UNIFI.Tests`, `MsData.Tests`); fall back to TitleCase if no match.
    $suiteName = if ($canonicalNames.ContainsKey($bare)) {
        $canonicalNames[$bare]
    } else {
        (Get-Culture).TextInfo.ToTitleCase($bare)
    }

    $suiteEsc = Format-TeamCityValue $suiteName
    $pathEsc  = Format-TeamCityValue $trx.FullName

    if ($DryRun) {
        Write-Host "[dry-run] suite=$suiteName  trx=$($trx.Name)"
    } else {
        Write-Host "##teamcity[testSuiteStarted name='$suiteEsc']"
        Write-Host "##teamcity[importData type='vstest' path='$pathEsc']"
        Write-Host "##teamcity[testSuiteFinished name='$suiteEsc']"
    }
}
