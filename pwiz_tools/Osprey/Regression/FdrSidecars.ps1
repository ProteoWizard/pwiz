<#
Per-file FDR score sidecar comparison for regression.ps1.

The four-task chain leg asserted only Compare-BlibFull, and the blib carries no protein
q-value, so a route that writes a different RunProteinQvalue into every
<stem>.2nd-pass.fdr_scores.bin passed green (issue #4553: 32,450 of 260,419 records differ
on StellarGenDecoyEntrap, 1.57% at 82 files). Peptide counts, protein-group counts and the
blib are all identical while it happens, so nothing the gate already reads can see it.

This compares the sidecars themselves, which is where the distributed tasks' per-file
output actually lands.

Record layout (Osprey.IO\FdrScoresSidecar.cs): 32-byte header, 60-byte records,
  entry_id u32 @0, score f64 @4, run_precursor_q @12, run_peptide_q @20,
  experiment_precursor_q @28, experiment_peptide_q @36, pep @44, run_protein_q @52.
#>

$script:FdrSidecarFields = @(
    @{ Name = 'score';                  Offset = 4  },
    @{ Name = 'run_precursor_qvalue';   Offset = 12 },
    @{ Name = 'run_peptide_qvalue';     Offset = 20 },
    @{ Name = 'experiment_precursor_qvalue'; Offset = 28 },
    @{ Name = 'experiment_peptide_qvalue';   Offset = 36 },
    @{ Name = 'pep';                    Offset = 44 },
    @{ Name = 'run_protein_qvalue';     Offset = 52 }
)

function Read-FdrSidecarRecords {
    <#
    entry_id -> double[] of the seven scalar fields, in $script:FdrSidecarFields order.
    Returns $null when the file is not a readable v3 sidecar, so the caller can report a
    structural problem rather than a value mismatch.
    #>
    param([Parameter(Mandatory = $true)][string]$Path)

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 32) { return $null }
    $count = [BitConverter]::ToUInt64($bytes, 16)
    if ($bytes.Length -ne (32 + $count * 60)) { return $null }

    $map = @{}
    for ($i = 0; $i -lt $count; $i++) {
        $off = 32 + $i * 60
        $vals = New-Object double[] $script:FdrSidecarFields.Count
        for ($f = 0; $f -lt $script:FdrSidecarFields.Count; $f++) {
            $vals[$f] = [BitConverter]::ToDouble($bytes, $off + $script:FdrSidecarFields[$f].Offset)
        }
        $map[[BitConverter]::ToUInt32($bytes, $off)] = $vals
    }
    return $map
}

function Compare-Pass2Sidecars {
    <#
    Compare every <stem>.2nd-pass.fdr_scores.bin between two run directories, all seven
    scalar fields at Tolerance, matched by stem and then by entry_id. Returns Pass + Issues.

    Byte-equality fast path first: when the files are identical there is nothing to decode,
    which keeps the common (green) case free.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$ExpectedDir,
        [Parameter(Mandatory = $true)][string]$ActualDir,
        [double]$Tolerance = 1e-9,
        [int]$MaxIssues = 12
    )

    $issues = [System.Collections.Generic.List[string]]::new()
    $suffix = '.2nd-pass.fdr_scores.bin'
    $expected = @{}
    foreach ($f in Get-ChildItem -File -Path $ExpectedDir -Filter "*$suffix" -ErrorAction SilentlyContinue) {
        $expected[$f.Name.Substring(0, $f.Name.Length - $suffix.Length)] = $f.FullName
    }
    $actual = @{}
    foreach ($f in Get-ChildItem -File -Path $ActualDir -Filter "*$suffix" -ErrorAction SilentlyContinue) {
        $actual[$f.Name.Substring(0, $f.Name.Length - $suffix.Length)] = $f.FullName
    }

    if ($expected.Count -eq 0) {
        $issues.Add("no $suffix files in expected dir $ExpectedDir")
        return @{ Pass = $false; Issues = $issues }
    }
    foreach ($stem in $expected.Keys) {
        if (-not $actual.ContainsKey($stem)) { $issues.Add("missing $stem$suffix in $ActualDir") }
    }
    foreach ($stem in $actual.Keys) {
        if (-not $expected.ContainsKey($stem)) { $issues.Add("unexpected $stem$suffix in $ActualDir") }
    }

    $nCompared = 0
    foreach ($stem in ($expected.Keys | Sort-Object)) {
        if (-not $actual.ContainsKey($stem)) { continue }
        $pa = $expected[$stem]
        $pb = $actual[$stem]

        $ba = [System.IO.File]::ReadAllBytes($pa)
        $bb = [System.IO.File]::ReadAllBytes($pb)
        if ($ba.Length -eq $bb.Length) {
            $same = $true
            for ($i = 0; $i -lt $ba.Length; $i++) {
                if ($ba[$i] -ne $bb[$i]) { $same = $false; break }
            }
            if ($same) { continue }
        }

        $ra = Read-FdrSidecarRecords -Path $pa
        $rb = Read-FdrSidecarRecords -Path $pb
        if ($null -eq $ra -or $null -eq $rb) {
            $issues.Add("$stem$suffix is not a readable v3 sidecar (expected=$($null -ne $ra) actual=$($null -ne $rb))")
            continue
        }
        if ($ra.Count -ne $rb.Count) {
            $issues.Add("$stem$suffix record count $($ra.Count) -> $($rb.Count)")
        }

        # Per-field tallies so the summary names WHICH field drifted, not just that
        # something did -- the fields have very different failure meanings.
        $fieldCounts = New-Object int[] $script:FdrSidecarFields.Count
        $firstExample = New-Object string[] $script:FdrSidecarFields.Count
        foreach ($eid in $ra.Keys) {
            if (-not $rb.ContainsKey($eid)) { continue }
            $va = $ra[$eid]; $vb = $rb[$eid]
            $nCompared++
            for ($f = 0; $f -lt $script:FdrSidecarFields.Count; $f++) {
                if ([math]::Abs($va[$f] - $vb[$f]) -gt $Tolerance) {
                    $fieldCounts[$f]++
                    if ($null -eq $firstExample[$f]) {
                        $firstExample[$f] = ("entry_id={0} {1} -> {2}" -f $eid, $va[$f], $vb[$f])
                    }
                }
            }
        }
        for ($f = 0; $f -lt $script:FdrSidecarFields.Count; $f++) {
            if ($fieldCounts[$f] -gt 0) {
                $issues.Add(("{0}: {1} differs on {2} record(s); first {3}" -f
                    $stem, $script:FdrSidecarFields[$f].Name, $fieldCounts[$f], $firstExample[$f]))
            }
        }
    }

    return @{ Pass = ($issues.Count -eq 0); Issues = $issues; Compared = $nCompared }
}
