<#
.SYNOPSIS
    Self-contained (pwiz-standalone) BiblioSpecLite (.blib) golden capture +
    comparison for the OspreySharp regression harness.

.DESCRIPTION
    Dot-source this file to get the blib-projection schema and the capture /
    compare functions used by the OspreySharp overnight regression
    (regression.ps1). NO dependency on the sibling ai/ checkout: the SQLite
    access uses the System.Data.SQLite.dll that ships in the OspreySharp build
    output, and all comparison logic lives here.

    The schema ($BlibTables) is the single source of truth for which .blib
    tables + columns are regression-significant, mirroring the cross-impl
    comparator (ai/scripts/OspreySharp/Compare/Compare-Blib-Crossimpl.ps1) but
    re-homed into pwiz so the nightly needs no ai/ checkout. Each table entry
    declares:
      Name     stable table label
      Scope    'Full'   -> every row committed to the golden (small tables)
               'Subset' -> only rows for the deterministic precursor subset are
                           committed (large per-precursor tables)
      Sql      projection that yields the precursor key columns first
      Key      stable join key (RefSpectra.id is autoincrement, so secondary
               tables join through RefSpectra to (peptideModSeq, precursorCharge))
      Numeric  columns compared at NumericTolerance (default 1e-9)
      Exact    columns compared by string equality

    Two golden artifacts per dataset capture the full-fidelity output compactly:
      tables\<Name>.tsv             full projection rows, filtered (Subset) or
                                    whole (Full), at 1e-9 fidelity
      blib_summary.tsv              full-set per-table row count + per-numeric-
                                    column aggregates (count / sum / min / max),
                                    so drift on precursors OUTSIDE the subset is
                                    still caught (coarsely)
    The Stage 7 protein-FDR dump (protein_fdr.tsv) is captured separately by the
    caller (it is produced by the run via OSPREY_DUMP_STAGE7_PROTEIN_FDR, not
    read from the blib).

    The same projection schema also powers the mode-2 self-consistency check
    (Compare-BlibFull), which compares two .blib files row+column at 1e-9 with
    no committed baseline (the resume run is its own oracle).
#>

$ErrorActionPreference = 'Stop'

# Unit-separator joiner for composite keys (won't occur in any data field).
$script:KeySep = [char]0x1F

# ----------------------------------------------------------------------
# Projection schema -- the regression-significant .blib tables.
# ----------------------------------------------------------------------
# Per-precursor SELECTs put (peptideModSeq, precursorCharge) first so the
# subset filter applies uniformly. RefSpectraPeaks is represented by a
# per-spectrum SHA-256 digest (PeakDigest) rather than the raw blobs, which
# are the binary bulk and are library-passthrough.
$script:BlibTables = @(
    @{
        Name = 'RefSpectra'; Scope = 'Subset'
        Sql = @'
SELECT peptideModSeq, precursorCharge, peptideSeq, prevAA, nextAA, copies,
       numPeaks, ionMobility, retentionTime, startTime, endTime,
       precursorMZ, score, scoreType
FROM RefSpectra
'@
        Key = @('peptideModSeq', 'precursorCharge')
        Numeric = @('precursorMZ', 'retentionTime', 'startTime', 'endTime', 'score', 'ionMobility')
        Exact = @('peptideSeq', 'prevAA', 'nextAA', 'copies', 'numPeaks', 'scoreType')
    },
    @{
        Name = 'RetentionTimes'; Scope = 'Subset'
        Sql = @'
SELECT r.peptideModSeq, r.precursorCharge, sf.fileName,
       rt.retentionTime, rt.startTime, rt.endTime, rt.score, rt.bestSpectrum
FROM RetentionTimes rt
JOIN RefSpectra r ON rt.RefSpectraID = r.id
JOIN SpectrumSourceFiles sf ON rt.SpectrumSourceID = sf.id
'@
        Key = @('peptideModSeq', 'precursorCharge', 'fileName')
        Numeric = @('retentionTime', 'startTime', 'endTime', 'score')
        Exact = @('bestSpectrum')
    },
    @{
        Name = 'OspreyRunScores'; Scope = 'Subset'
        Sql = @'
SELECT r.peptideModSeq, r.precursorCharge, s.FileName,
       s.RunQValue, s.DiscriminantScore, s.PosteriorErrorProb
FROM OspreyRunScores s
JOIN RefSpectra r ON s.RefSpectraID = r.id
'@
        Key = @('peptideModSeq', 'precursorCharge', 'FileName')
        Numeric = @('RunQValue', 'DiscriminantScore', 'PosteriorErrorProb')
        Exact = @()
    },
    @{
        Name = 'OspreyExperimentScores'; Scope = 'Subset'
        Sql = @'
SELECT r.peptideModSeq, r.precursorCharge,
       s.ExperimentQValue, s.NRunsDetected, s.NRunsSearched
FROM OspreyExperimentScores s
JOIN RefSpectra r ON s.RefSpectraID = r.id
'@
        Key = @('peptideModSeq', 'precursorCharge')
        Numeric = @('ExperimentQValue')
        Exact = @('NRunsDetected', 'NRunsSearched')
    },
    @{
        Name = 'OspreyPeakBoundaries'; Scope = 'Subset'
        Sql = @'
SELECT r.peptideModSeq, r.precursorCharge, s.FileName,
       s.StartRT, s.EndRT, s.ApexRT, s.ApexIntensity, s.IntegratedArea
FROM OspreyPeakBoundaries s
JOIN RefSpectra r ON s.RefSpectraID = r.id
'@
        Key = @('peptideModSeq', 'precursorCharge', 'FileName')
        Numeric = @('StartRT', 'EndRT', 'ApexRT', 'ApexIntensity', 'IntegratedArea')
        Exact = @()
    },
    @{
        Name = 'OspreyCoefficients'; Scope = 'Subset'
        Sql = @'
SELECT r.peptideModSeq, r.precursorCharge, s.FileName, s.ScanNumber,
       s.RT, s.Coefficient
FROM OspreyCoefficients s
JOIN RefSpectra r ON s.RefSpectraID = r.id
'@
        Key = @('peptideModSeq', 'precursorCharge', 'FileName', 'ScanNumber')
        Numeric = @('RT', 'Coefficient')
        Exact = @()
    },
    @{
        Name = 'RefSpectraProteins'; Scope = 'Subset'
        Sql = @'
SELECT r.peptideModSeq, r.precursorCharge, p.accession
FROM RefSpectraProteins x
JOIN RefSpectra r ON x.RefSpectraID = r.id
JOIN Proteins p ON x.ProteinID = p.id
'@
        Key = @('peptideModSeq', 'precursorCharge', 'accession')
        Numeric = @()
        Exact = @()
    },
    @{
        Name = 'Modifications'; Scope = 'Subset'
        Sql = @'
SELECT r.peptideModSeq, r.precursorCharge, m.position, m.mass
FROM Modifications m
JOIN RefSpectra r ON m.RefSpectraID = r.id
'@
        Key = @('peptideModSeq', 'precursorCharge', 'position')
        Numeric = @('mass')
        Exact = @()
    },
    @{
        Name = 'Proteins'; Scope = 'Full'
        Sql = 'SELECT accession FROM Proteins'
        Key = @('accession')
        Numeric = @()
        Exact = @()
    },
    @{
        Name = 'SpectrumSourceFiles'; Scope = 'Full'
        Sql = 'SELECT fileName, idFileName, cutoffScore, workflowType FROM SpectrumSourceFiles'
        Key = @('fileName')
        Numeric = @('cutoffScore')
        Exact = @('idFileName', 'workflowType')
    },
    @{
        Name = 'OspreyMetadata'; Scope = 'Full'
        Sql = 'SELECT Key, Value FROM OspreyMetadata'
        Key = @('Key')
        Numeric = @()
        Exact = @('Value')
    }
)

# Per-spectrum peak digest: one SHA-256 over (peakMZ || peakIntensity).
# Subset-scoped, keyed by precursor. Detects peak-content drift without
# committing the multi-MB blob arrays.
$script:PeakDigestSql = @'
SELECT r.peptideModSeq, r.precursorCharge, p.peakMZ, p.peakIntensity
FROM RefSpectraPeaks p
JOIN RefSpectra r ON p.RefSpectraID = r.id
'@

# ----------------------------------------------------------------------
# SQLite access (System.Data.SQLite from the OspreySharp build output)
# ----------------------------------------------------------------------
function Initialize-Sqlite {
    <#
    Load System.Data.SQLite from the OspreySharp net8.0 build output and make
    sure its native SQLite.Interop.dll sits beside the managed assembly (the
    P/Invoke probes the assembly dir directly when loaded via Add-Type). Call
    once before any Open-Blib. -OspreyBinDir points at the build's net8.0 dir.
    #>
    param([Parameter(Mandatory = $true)][string]$OspreyBinDir)

    $dll = Join-Path $OspreyBinDir 'System.Data.SQLite.dll'
    if (-not (Test-Path $dll)) {
        throw "System.Data.SQLite.dll not found at $dll -- build OspreySharp (net8.0) first."
    }
    $rid = if ($IsLinux) { 'linux-x64' } else { 'win-x64' }
    $nativeSrc = Join-Path $OspreyBinDir "runtimes/$rid/native/SQLite.Interop.dll"
    $nativeDst = Join-Path $OspreyBinDir 'SQLite.Interop.dll'
    # Always overwrite: a previous run on another OS may have left the
    # wrong-architecture binary, which P/Invoke rejects with "incorrect format".
    if (Test-Path $nativeSrc) {
        Copy-Item $nativeSrc $nativeDst -Force
    }
    Add-Type -Path $dll
}

function Invoke-BlibQuery {
    <#
    Run a read-only SQL query against a .blib and return an ordered result:
    Cols (string[]) and Rows (List[object[]]) in column order. Raw object
    values are preserved (byte[] for blobs, boxed numerics otherwise).
    #>
    param([Parameter(Mandatory = $true)][string]$Blib,
          [Parameter(Mandatory = $true)][string]$Sql)

    # Pooling=False so the file handle is released the instant the connection
    # is disposed -- otherwise a pooled handle lingers and a later Remove-Item on
    # the blib (e.g. the mode-2 resume invalidation) fails with "being used by
    # another process".
    $conn = New-Object System.Data.SQLite.SQLiteConnection "Data Source=$Blib;Read Only=True;Pooling=False"
    $conn.Open()
    $cmd = $null; $reader = $null
    try {
        $cmd = $conn.CreateCommand()
        $cmd.CommandText = $Sql
        $reader = $cmd.ExecuteReader()
        $n = $reader.FieldCount
        $cols = [string[]]::new($n)
        for ($i = 0; $i -lt $n; $i++) { $cols[$i] = $reader.GetName($i) }
        $rows = [System.Collections.Generic.List[object[]]]::new()
        while ($reader.Read()) {
            $vals = [object[]]::new($n)
            for ($i = 0; $i -lt $n; $i++) {
                $v = $reader.GetValue($i)
                if ($v -is [System.DBNull]) { $v = $null }
                $vals[$i] = $v
            }
            $rows.Add($vals)
        }
        return [pscustomobject]@{ Cols = $cols; Rows = $rows }
    } finally {
        if ($reader) { $reader.Dispose() }
        if ($cmd) { $cmd.Dispose() }
        $conn.Dispose()
    }
}

# ----------------------------------------------------------------------
# Deterministic precursor subset selection
# ----------------------------------------------------------------------
function Test-PrecursorInSubset {
    <#
    Stable, order- and machine-independent membership test for the golden
    subset. Hash the precursor key (peptideModSeq|precursorCharge) with MD5 and
    keep it when the first 4 bytes (mod Modulus) == 0. Modulus ~120 yields
    ~500 of ~60K precursors. Crypto hash -> identical selection on every
    machine/runtime, independent of row order.
    #>
    param([string]$PeptideModSeq, [string]$PrecursorCharge, [int]$Modulus = 120)

    $key = "$PeptideModSeq$($script:KeySep)$PrecursorCharge"
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($key)
    $md5 = [System.Security.Cryptography.MD5]::Create()
    try {
        $hash = $md5.ComputeHash($bytes)
    } finally {
        $md5.Dispose()
    }
    $u = [System.BitConverter]::ToUInt32($hash, 0)
    return (($u % [uint32]$Modulus) -eq 0)
}

# ----------------------------------------------------------------------
# TSV helpers (LF-free, tab-delimited; values rendered invariantly)
# ----------------------------------------------------------------------
function Format-CellValue {
    param($Value)
    if ($null -eq $Value) { return '' }
    if ($Value -is [byte[]]) { return [System.BitConverter]::ToString($Value).Replace('-', '') }
    if ($Value -is [double] -or $Value -is [float]) {
        return ([double]$Value).ToString('R', [System.Globalization.CultureInfo]::InvariantCulture)
    }
    return $Value.ToString()
}

function Write-Tsv {
    <#
    Write a header + rows (each an object[] in column order) to a TSV file.
    Rows are written in the order supplied; callers sort before calling.
    #>
    param([string]$Path, [string[]]$Header, [System.Collections.IEnumerable]$Rows)

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append(($Header -join "`t")); [void]$sb.Append("`r`n")
    foreach ($row in $Rows) {
        $cells = foreach ($v in $row) { Format-CellValue $v }
        [void]$sb.Append(($cells -join "`t")); [void]$sb.Append("`r`n")
    }
    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [System.IO.File]::WriteAllText($Path, $sb.ToString(), (New-Object System.Text.UTF8Encoding $false))
}

function Read-Tsv {
    <#
    Read a TSV written by Write-Tsv. Returns Header (string[]) and Rows
    (List[string[]]). Pure string cells; numeric comparison parses on demand.
    #>
    param([string]$Path)
    $lines = [System.IO.File]::ReadAllLines($Path)
    if ($lines.Length -lt 1) { throw "Empty TSV: $Path" }
    $header = $lines[0] -split "`t"
    $rows = [System.Collections.Generic.List[string[]]]::new()
    for ($i = 1; $i -lt $lines.Length; $i++) {
        if ($lines[$i].Length -eq 0) { continue }
        $rows.Add(($lines[$i] -split "`t"))
    }
    return [pscustomobject]@{ Header = $header; Rows = $rows }
}

# ----------------------------------------------------------------------
# Row-set comparison primitive (shared by golden-compare and full-compare)
# ----------------------------------------------------------------------
function New-RowKey {
    param([string[]]$Row, [int[]]$KeyIdx)
    $parts = foreach ($k in $KeyIdx) { $Row[$k] }
    return ($parts -join $script:KeySep)
}

# Return a new List[string[]] sorted by the composite key (precomputed once
# per row). Building the list with .Add avoids the generic-ctor-from-pipeline
# overload pitfall (an object[] from Sort-Object does not satisfy
# IEnumerable[string[]]).
function Sort-RowsByKey {
    param([System.Collections.Generic.List[string[]]]$Rows, [int[]]$KeyIdx)
    $tagged = foreach ($r in $Rows) { [pscustomobject]@{ K = (New-RowKey $r $KeyIdx); R = $r } }
    $out = [System.Collections.Generic.List[string[]]]::new($Rows.Count)
    foreach ($x in ($tagged | Sort-Object -Property K)) { $out.Add($x.R) }
    return , $out
}

function Compare-RowSets {
    <#
    Compare two sets of string rows sharing one header. Key-join on KeyCols,
    report row-set disagreement, then compare NumericCols at Tolerance and
    ExactCols by string equality. Returns a result object with Pass + a list of
    human-readable issue strings (empty when Pass). 'A' is the golden/expected
    side, 'B' is the fresh/actual side.
    #>
    param(
        [string]$Label,
        [string[]]$Header,
        [System.Collections.Generic.List[string[]]]$RowsA,
        [System.Collections.Generic.List[string[]]]$RowsB,
        [string[]]$KeyCols,
        [string[]]$NumericCols = @(),
        [string[]]$ExactCols = @(),
        [double]$Tolerance = 1e-9
    )

    $idx = @{}
    for ($i = 0; $i -lt $Header.Length; $i++) { $idx[$Header[$i]] = $i }
    $keyIdx = foreach ($k in $KeyCols) { $idx[$k] }

    $mapA = [System.Collections.Generic.Dictionary[string, string[]]]::new($RowsA.Count)
    foreach ($r in $RowsA) { $mapA[(New-RowKey $r $keyIdx)] = $r }
    $mapB = [System.Collections.Generic.Dictionary[string, string[]]]::new($RowsB.Count)
    foreach ($r in $RowsB) { $mapB[(New-RowKey $r $keyIdx)] = $r }

    $issues = [System.Collections.Generic.List[string]]::new()

    $onlyA = 0; $onlyB = 0; $sampleA = $null; $sampleB = $null
    foreach ($k in $mapA.Keys) { if (-not $mapB.ContainsKey($k)) { $onlyA++; if ($null -eq $sampleA) { $sampleA = $k } } }
    foreach ($k in $mapB.Keys) { if (-not $mapA.ContainsKey($k)) { $onlyB++; if ($null -eq $sampleB) { $sampleB = $k } } }
    if ($onlyA -gt 0) { $issues.Add(("{0}: {1} key(s) only in golden (e.g. {2})" -f $Label, $onlyA, $sampleA)) }
    if ($onlyB -gt 0) { $issues.Add(("{0}: {1} key(s) only in run (e.g. {2})" -f $Label, $onlyB, $sampleB)) }

    $common = [System.Collections.Generic.List[string]]::new()
    foreach ($k in $mapA.Keys) { if ($mapB.ContainsKey($k)) { $common.Add($k) } }

    foreach ($col in $NumericCols) {
        $ci = $idx[$col]
        $nDiverge = 0; $maxDiff = 0.0; $sampleKey = $null; $sa = $null; $sb = $null
        foreach ($k in $common) {
            $av = $mapA[$k][$ci]; $bv = $mapB[$k][$ci]
            $aEmpty = [string]::IsNullOrEmpty($av); $bEmpty = [string]::IsNullOrEmpty($bv)
            if ($aEmpty -and $bEmpty) { continue }
            if ($aEmpty -ne $bEmpty) {
                $nDiverge++; if ($null -eq $sampleKey) { $sampleKey = $k; $sa = $av; $sb = $bv }
                continue
            }
            $ad = [double]::Parse($av, [System.Globalization.CultureInfo]::InvariantCulture)
            $bd = [double]::Parse($bv, [System.Globalization.CultureInfo]::InvariantCulture)
            $d = [Math]::Abs($ad - $bd)
            if ($d -gt $maxDiff) { $maxDiff = $d; $sampleKey = $k; $sa = $ad; $sb = $bd }
            if ($d -gt $Tolerance) { $nDiverge++ }
        }
        if ($nDiverge -gt 0) {
            $issues.Add(("{0}.{1}: {2}/{3} rows > {4:e1} (max_diff={5:e3}; key={6} golden={7} run={8})" -f `
                $Label, $col, $nDiverge, $common.Count, $Tolerance, $maxDiff, $sampleKey, $sa, $sb))
        }
    }

    foreach ($col in $ExactCols) {
        $ci = $idx[$col]
        $nDiverge = 0; $sampleKey = $null; $sa = $null; $sb = $null
        foreach ($k in $common) {
            $av = $mapA[$k][$ci]; $bv = $mapB[$k][$ci]
            if ($av -cne $bv) {
                $nDiverge++; if ($null -eq $sampleKey) { $sampleKey = $k; $sa = $av; $sb = $bv }
            }
        }
        if ($nDiverge -gt 0) {
            $issues.Add(("{0}.{1}: {2}/{3} rows differ (exact; key={4} golden='{5}' run='{6}')" -f `
                $Label, $col, $nDiverge, $common.Count, $sampleKey, $sa, $sb))
        }
    }

    return [pscustomobject]@{
        Label = $Label; Pass = ($issues.Count -eq 0); Issues = $issues
        RowsA = $RowsA.Count; RowsB = $RowsB.Count; Common = $common.Count
    }
}

# Project one schema table from a blib into a header + List[string[]] of
# rendered rows, optionally filtered to the deterministic precursor subset and
# sorted by key for stable diffs. Assumes peptideModSeq/precursorCharge are the
# first two projected columns for Subset tables.
function Get-TableProjection {
    param([string]$Blib, [hashtable]$Table, [switch]$SubsetFilter, [switch]$NoSort, [int]$Modulus = 120)

    $res = Invoke-BlibQuery -Blib $Blib -Sql $Table.Sql
    $header = $res.Cols
    $rendered = [System.Collections.Generic.List[string[]]]::new($res.Rows.Count)
    $applyFilter = $SubsetFilter -and ($Table.Scope -eq 'Subset')
    foreach ($vals in $res.Rows) {
        if ($applyFilter) {
            if (-not (Test-PrecursorInSubset $vals[0].ToString() $vals[1].ToString() $Modulus)) { continue }
        }
        $cells = [string[]]::new($vals.Length)
        for ($i = 0; $i -lt $vals.Length; $i++) { $cells[$i] = Format-CellValue $vals[$i] }
        $rendered.Add($cells)
    }
    # Sorting is only needed for stable on-disk TSV diffs (golden capture); the
    # compare path keys into a dictionary and does not care about row order.
    if ($NoSort) { return [pscustomobject]@{ Header = $header; Rows = $rendered } }
    $keyIdx = @(foreach ($k in $Table.Key) { [Array]::IndexOf($header, $k) })
    $sorted = Sort-RowsByKey -Rows $rendered -KeyIdx $keyIdx
    return [pscustomobject]@{ Header = $header; Rows = $sorted }
}

# Per-spectrum peak SHA-256 digest projection (optionally subset-filtered).
function Get-PeakDigestProjection {
    param([string]$Blib, [switch]$SubsetFilter, [int]$Modulus = 120)

    $res = Invoke-BlibQuery -Blib $Blib -Sql $script:PeakDigestSql
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $rendered = [System.Collections.Generic.List[string[]]]::new($res.Rows.Count)
        foreach ($vals in $res.Rows) {
            $pep = $vals[0].ToString(); $chg = $vals[1].ToString()
            if ($SubsetFilter -and -not (Test-PrecursorInSubset $pep $chg $Modulus)) { continue }
            $buf = New-Object System.Collections.Generic.List[byte]
            if ($vals[2] -is [byte[]]) { $buf.AddRange([byte[]]$vals[2]) }
            if ($vals[3] -is [byte[]]) { $buf.AddRange([byte[]]$vals[3]) }
            $hash = [System.BitConverter]::ToString($sha.ComputeHash($buf.ToArray())).Replace('-', '')
            $rendered.Add([string[]]@($pep, $chg, $hash))
        }
    } finally {
        $sha.Dispose()
    }
    $header = @('peptideModSeq', 'precursorCharge', 'peakHash')
    $sorted = Sort-RowsByKey -Rows $rendered -KeyIdx @(0, 1)
    return [pscustomobject]@{ Header = $header; Rows = $sorted }
}

# Full-set per-table row count + per-numeric-column aggregates, computed via
# SQL so no rows are materialized. Catches drift on precursors OUTSIDE the
# committed subset (coarsely). Returns header + rows for blib_summary.tsv.
function Get-BlibSummary {
    param([string]$Blib)
    $rows = [System.Collections.Generic.List[string[]]]::new()
    foreach ($t in $script:BlibTables) {
        $cnt = (Invoke-BlibQuery -Blib $Blib -Sql ("SELECT COUNT(*) AS n FROM ({0})" -f $t.Sql)).Rows[0][0]
        $rows.Add([string[]]@($t.Name, '<rows>', (Format-CellValue $cnt), '', ''))
        foreach ($col in $t.Numeric) {
            $agg = (Invoke-BlibQuery -Blib $Blib -Sql (
                "SELECT SUM($col) AS s, MIN($col) AS lo, MAX($col) AS hi FROM ({0})" -f $t.Sql)).Rows[0]
            $rows.Add([string[]]@($t.Name, $col,
                (Format-CellValue $agg[0]), (Format-CellValue $agg[1]), (Format-CellValue $agg[2])))
        }
    }
    # Peak digest count (full set).
    $pcnt = (Invoke-BlibQuery -Blib $Blib -Sql ("SELECT COUNT(*) AS n FROM ({0})" -f $script:PeakDigestSql)).Rows[0][0]
    $rows.Add([string[]]@('PeakDigest', '<rows>', (Format-CellValue $pcnt), '', ''))
    return [pscustomobject]@{ Header = @('table', 'column', 'rows_or_sum', 'min', 'max'); Rows = $rows }
}

# Stage 7 protein-FDR dump schema (the TSV produced by the run under
# OSPREY_DUMP_STAGE7_PROTEIN_FDR). Compared at 1e-9 like the cross-impl gate.
$script:ProteinFdrKey = @('accessions')
$script:ProteinFdrNumeric = @('best_peptide_score', 'group_qvalue')
$script:ProteinFdrExact = @('n_unique', 'n_shared', 'is_target_winner')

# ----------------------------------------------------------------------
# Top-level: capture a golden
# ----------------------------------------------------------------------
function Save-BlibGolden {
    <#
    Capture the compact text golden for a .blib into GoldenDir:
      tables\<Name>.tsv     per-table projection (subset-filtered for Subset scope)
      tables\PeakDigest.tsv per-spectrum peak SHA-256 (subset-filtered)
      blib_summary.tsv      full-set row counts + per-numeric-column aggregates
      protein_fdr.tsv       copied from the run's Stage 7 dump (if provided)
    Initialize-Sqlite must have been called. Modulus controls subset size.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$Blib,
        [Parameter(Mandatory = $true)][string]$GoldenDir,
        [string]$ProteinFdrTsv,
        [int]$Modulus = 120
    )
    $tablesDir = Join-Path $GoldenDir 'tables'
    New-Item -ItemType Directory -Path $tablesDir -Force | Out-Null

    foreach ($t in $script:BlibTables) {
        $proj = Get-TableProjection -Blib $Blib -Table $t -SubsetFilter:$true -Modulus $Modulus
        Write-Tsv -Path (Join-Path $tablesDir ($t.Name + '.tsv')) -Header $proj.Header -Rows $proj.Rows
    }
    $peak = Get-PeakDigestProjection -Blib $Blib -SubsetFilter:$true -Modulus $Modulus
    Write-Tsv -Path (Join-Path $tablesDir 'PeakDigest.tsv') -Header $peak.Header -Rows $peak.Rows

    $summary = Get-BlibSummary -Blib $Blib
    Write-Tsv -Path (Join-Path $GoldenDir 'blib_summary.tsv') -Header $summary.Header -Rows $summary.Rows

    if ($ProteinFdrTsv) {
        if (-not (Test-Path $ProteinFdrTsv)) { throw "Stage 7 protein-FDR dump not found: $ProteinFdrTsv" }
        Copy-Item $ProteinFdrTsv (Join-Path $GoldenDir 'protein_fdr.tsv') -Force
    }
}

# ----------------------------------------------------------------------
# Top-level: compare a run's blib against a committed golden (mode 1)
# ----------------------------------------------------------------------
function Compare-BlibGolden {
    <#
    Compare a fresh run's .blib (+ its Stage 7 protein-FDR dump) against the
    committed golden in GoldenDir. Returns a result object: Pass + Issues (flat
    list of human-readable mismatch lines) + the per-artifact results. The
    subset projection is recomputed from the fresh blib with the SAME modulus,
    so it selects the same precursors the golden captured.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$Blib,
        [Parameter(Mandatory = $true)][string]$GoldenDir,
        [string]$ProteinFdrTsv,
        [int]$Modulus = 120,
        [double]$Tolerance = 1e-9,
        [double]$SummaryRelTolerance = 1e-6
    )
    $tablesDir = Join-Path $GoldenDir 'tables'
    $issues = [System.Collections.Generic.List[string]]::new()

    foreach ($t in $script:BlibTables) {
        $goldenPath = Join-Path $tablesDir ($t.Name + '.tsv')
        if (-not (Test-Path $goldenPath)) { $issues.Add("missing golden table: $($t.Name)"); continue }
        $golden = Read-Tsv -Path $goldenPath
        $fresh = Get-TableProjection -Blib $Blib -Table $t -SubsetFilter:$true -Modulus $Modulus
        $cmp = Compare-RowSets -Label $t.Name -Header $golden.Header `
            -RowsA $golden.Rows -RowsB $fresh.Rows `
            -KeyCols $t.Key -NumericCols $t.Numeric -ExactCols $t.Exact -Tolerance $Tolerance
        foreach ($iss in $cmp.Issues) { $issues.Add($iss) }
    }

    # Peak digest (peakHash is an exact column).
    $peakGoldenPath = Join-Path $tablesDir 'PeakDigest.tsv'
    if (Test-Path $peakGoldenPath) {
        $golden = Read-Tsv -Path $peakGoldenPath
        $fresh = Get-PeakDigestProjection -Blib $Blib -SubsetFilter:$true -Modulus $Modulus
        $cmp = Compare-RowSets -Label 'PeakDigest' -Header $golden.Header `
            -RowsA $golden.Rows -RowsB $fresh.Rows `
            -KeyCols @('peptideModSeq', 'precursorCharge') -ExactCols @('peakHash') -Tolerance $Tolerance
        foreach ($iss in $cmp.Issues) { $issues.Add($iss) }
    }

    # Protein FDR (Stage 7 dump).
    $proteinGolden = Join-Path $GoldenDir 'protein_fdr.tsv'
    if (Test-Path $proteinGolden) {
        if (-not $ProteinFdrTsv -or -not (Test-Path $ProteinFdrTsv)) {
            $issues.Add('protein_fdr: golden present but run produced no Stage 7 dump')
        } else {
            $golden = Read-Tsv -Path $proteinGolden
            $fresh = Read-Tsv -Path $ProteinFdrTsv
            $cmp = Compare-RowSets -Label 'protein_fdr' -Header $golden.Header `
                -RowsA $golden.Rows -RowsB $fresh.Rows `
                -KeyCols $script:ProteinFdrKey -NumericCols $script:ProteinFdrNumeric `
                -ExactCols $script:ProteinFdrExact -Tolerance $Tolerance
            foreach ($iss in $cmp.Issues) { $issues.Add($iss) }
        }
    }

    # Full-set summary (counts exact; aggregates at relative tolerance).
    $summaryGolden = Join-Path $GoldenDir 'blib_summary.tsv'
    if (Test-Path $summaryGolden) {
        $golden = Read-Tsv -Path $summaryGolden
        $fresh = Get-BlibSummary -Blib $Blib
        foreach ($iss in (Compare-Summary -Golden $golden -Fresh $fresh -RelTolerance $SummaryRelTolerance)) {
            $issues.Add($iss)
        }
    }

    return [pscustomobject]@{ Pass = ($issues.Count -eq 0); Issues = $issues }
}

# Compare two blib_summary projections: row counts must match exactly; numeric
# aggregates (sum/min/max) at relative tolerance to absorb float-format noise
# while still flagging out-of-subset drift.
function Compare-Summary {
    param([pscustomobject]$Golden, [pscustomobject]$Fresh, [double]$RelTolerance = 1e-6)

    function To-Map($parsed) {
        $m = @{}
        foreach ($r in $parsed.Rows) { $m["$($r[0])$($script:KeySep)$($r[1])"] = $r }
        return $m
    }
    $gMap = To-Map $Golden
    $fMap = To-Map $Fresh
    $issues = [System.Collections.Generic.List[string]]::new()

    foreach ($k in $gMap.Keys) {
        if (-not $fMap.ContainsKey($k)) { $issues.Add("summary: row missing in run ($k)"); continue }
        $g = $gMap[$k]; $f = $fMap[$k]
        $table = $g[0]; $col = $g[1]
        if ($col -eq '<rows>') {
            if ($g[2] -cne $f[2]) { $issues.Add("summary $table row count: golden=$($g[2]) run=$($f[2])") }
        } else {
            foreach ($ci in 2, 3, 4) {
                $gv = $g[$ci]; $fv = $f[$ci]
                if ([string]::IsNullOrEmpty($gv) -and [string]::IsNullOrEmpty($fv)) { continue }
                if ([string]::IsNullOrEmpty($gv) -ne [string]::IsNullOrEmpty($fv)) {
                    $issues.Add("summary $table.$col null mismatch: golden='$gv' run='$fv'"); continue
                }
                $gd = [double]::Parse($gv, [System.Globalization.CultureInfo]::InvariantCulture)
                $fd = [double]::Parse($fv, [System.Globalization.CultureInfo]::InvariantCulture)
                $scale = [Math]::Max([Math]::Abs($gd), 1.0)
                if ([Math]::Abs($gd - $fd) / $scale -gt $RelTolerance) {
                    $issues.Add(("summary {0}.{1}[{2}]: golden={3} run={4}" -f $table, $col, $ci, $gd, $fd))
                }
            }
        }
    }
    foreach ($k in $fMap.Keys) { if (-not $gMap.ContainsKey($k)) { $issues.Add("summary: extra row in run ($k)") } }
    return $issues
}

# ----------------------------------------------------------------------
# Top-level: full blib-vs-blib self-consistency (mode 2, no baseline)
# ----------------------------------------------------------------------
function Compare-BlibFull {
    <#
    Compare two .blib files row + column at Tolerance across the full schema (NO
    subset filter): the resume run is its own oracle against the straight-through
    run, so no committed golden is needed. Returns Pass + Issues.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$BlibExpected,
        [Parameter(Mandatory = $true)][string]$BlibActual,
        [double]$Tolerance = 1e-9,
        [switch]$IncludePeaks
    )
    $issues = [System.Collections.Generic.List[string]]::new()
    foreach ($t in $script:BlibTables) {
        # -NoSort: the compare keys into a dictionary, so row order is
        # irrelevant -- skip the (expensive on 180K-row tables) sort.
        $a = Get-TableProjection -Blib $BlibExpected -Table $t -NoSort
        $b = Get-TableProjection -Blib $BlibActual -Table $t -NoSort
        $cmp = Compare-RowSets -Label $t.Name -Header $a.Header `
            -RowsA $a.Rows -RowsB $b.Rows `
            -KeyCols $t.Key -NumericCols $t.Numeric -ExactCols $t.Exact -Tolerance $Tolerance
        foreach ($iss in $cmp.Issues) { $issues.Add($iss) }
    }
    # RefSpectraPeaks are copied verbatim from the spectral library and cannot
    # differ between a straight-through run and its resume (same library, same
    # reference spectra) -- the self-consistency check that motivates this
    # comparison is about computed RT/area/score columns, not peaks. Skip the
    # 60K-spectrum SHA digest by default; -IncludePeaks forces it.
    if ($IncludePeaks) {
        $ap = Get-PeakDigestProjection -Blib $BlibExpected
        $bp = Get-PeakDigestProjection -Blib $BlibActual
        $cmp = Compare-RowSets -Label 'PeakDigest' -Header $ap.Header `
            -RowsA $ap.Rows -RowsB $bp.Rows `
            -KeyCols @('peptideModSeq', 'precursorCharge') -ExactCols @('peakHash') -Tolerance $Tolerance
        foreach ($iss in $cmp.Issues) { $issues.Add($iss) }
    }

    return [pscustomobject]@{ Pass = ($issues.Count -eq 0); Issues = $issues }
}
