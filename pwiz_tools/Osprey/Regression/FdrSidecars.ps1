<#
Per-file FDR score sidecar comparison.

Two consumers, one decoder:
  * regression.ps1's four-task-chain leg -- distributed route vs straight-through, C# only.
  * ai/scripts/Osprey/Compare/Compare-FdrSidecars-Crossimpl.ps1 -- C# vs Rust.

The four-task chain leg asserted only Compare-BlibFull, and the blib carries no protein
q-value, so a route that writes a different RunProteinQvalue into every
<stem>.2nd-pass.fdr_scores.bin passed green (issue #4553: 32,450 of 260,419 records differ
on StellarGenDecoyEntrap, 1.57% at 82 files). Peptide counts, protein-group counts and the
blib are all identical while it happens, so nothing the gate already reads can see it.

The cross-impl gate had the same blind spot from the other direction: it compares the
Stage 7 protein FDR dump (per-protein-GROUP columns) and the blib, neither of which carries
a per-entry SVM score or run protein q. Both implementations dropped the same two fields,
so they agreed on the wrong value and nothing was red.

This compares the sidecars themselves, which is where the distributed tasks' per-file
output actually lands.

Record layout (Osprey.IO\FdrScoresSidecar.cs): 32-byte header, 60-byte records,
  entry_id u32 @0, score f64 @4, run_precursor_q @12, run_peptide_q @20,
  experiment_precursor_q @28, experiment_peptide_q @36, pep @44, run_protein_q @52.

The decode + compare runs as compiled C#, not PowerShell. A per-record PowerShell loop
took over 10 minutes on one Astral 3-file pass (6.2M records) and would be unusable at the
82-file scale these gates are meant to reach; the compiled form is seconds.
#>

if (-not ([System.Management.Automation.PSTypeName]'OspreyFdrSidecarComparer').Type) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.IO;

public class FdrSidecarDiff
{
    public bool ReadableExpected;
    public bool ReadableActual;
    public string ProblemExpected;
    public string ProblemActual;
    public long CountExpected;
    public long CountActual;
    public long Compared;
    public long[] FieldCounts;
    public string[] FirstExample;
}

public static class OspreyFdrSidecarComparer
{
    // Byte offsets within the 60-byte record, in report order.
    private static readonly int[] Offsets = { 4, 12, 20, 28, 36, 44, 52 };
    private const int HeaderLen = 32;
    private const int RecordLen = 60;
    private const byte ExpectedVersion = 3;
    private static readonly byte[] Magic = { 0x4F, 0x53, 0x50, 0x52, 0x59, 0x46, 0x44, 0x52 }; // OSPRYFDR

    public static string[] FieldNames = {
        "score", "run_precursor_qvalue", "run_peptide_qvalue",
        "experiment_precursor_qvalue", "experiment_peptide_qvalue",
        "pep", "run_protein_qvalue"
    };

    public static FdrSidecarDiff Compare(string pathExpected, string pathActual, double tolerance)
    {
        var result = new FdrSidecarDiff
        {
            FieldCounts = new long[Offsets.Length],
            FirstExample = new string[Offsets.Length],
        };

        byte[] a = ReadIfValid(pathExpected, out long na, out string problemA);
        byte[] b = ReadIfValid(pathActual, out long nb, out string problemB);
        result.ReadableExpected = a != null;
        result.ReadableActual = b != null;
        result.ProblemExpected = problemA;
        result.ProblemActual = problemB;
        result.CountExpected = na;
        result.CountActual = nb;
        if (a == null || b == null)
            return result;

        // entry_id -> record offset for the actual side, then walk the expected side.
        var offsetById = new Dictionary<uint, int>((int)nb);
        for (long i = 0; i < nb; i++)
        {
            int off = HeaderLen + (int)(i * RecordLen);
            offsetById[BitConverter.ToUInt32(b, off)] = off;
        }

        for (long i = 0; i < na; i++)
        {
            int offA = HeaderLen + (int)(i * RecordLen);
            uint entryId = BitConverter.ToUInt32(a, offA);
            int offB;
            if (!offsetById.TryGetValue(entryId, out offB))
                continue;
            result.Compared++;
            for (int f = 0; f < Offsets.Length; f++)
            {
                double va = BitConverter.ToDouble(a, offA + Offsets[f]);
                double vb = BitConverter.ToDouble(b, offB + Offsets[f]);
                if (Math.Abs(va - vb) <= tolerance)
                    continue;
                result.FieldCounts[f]++;
                if (result.FirstExample[f] == null)
                {
                    result.FirstExample[f] = string.Format(
                        "entry_id={0} {1:R} -> {2:R}", entryId, va, vb);
                }
            }
        }
        return result;
    }

    /// Read a sidecar whose header validates, else null with a reason.
    ///
    /// Magic and version are checked, not just the size invariant. A newer writer that grows
    /// the record (the v4 68-byte layout on the #4522 branch adds experiment_aggregate_score
    /// at [60..68]) must be REFUSED by name here rather than decoded at the old stride: the
    /// size check alone rejects it only because 68 and 60 happen not to divide alike, which
    /// is luck, not a guard. Silently misreading a field offset is the failure mode this
    /// comparison exists to catch, so it must not be one this comparison can commit.
    private static byte[] ReadIfValid(string path, out long count, out string problem)
    {
        count = 0;
        problem = null;
        if (!File.Exists(path))
        {
            problem = "file not found";
            return null;
        }
        byte[] data = File.ReadAllBytes(path);
        if (data.Length >= 9)
        {
            for (int i = 0; i < Magic.Length; i++)
            {
                if (data[i] != Magic[i])
                {
                    problem = "wrong magic bytes (not an Osprey FDR sidecar)";
                    return null;
                }
            }
            if (data[8] != ExpectedVersion)
            {
                problem = string.Format(
                    "sidecar format version {0}, but this comparison decodes version {1} " +
                    "({2}-byte records). Update FdrSidecars.ps1 for the newer layout.",
                    data[8], ExpectedVersion, RecordLen);
                return null;
            }
        }
        if (data.Length < HeaderLen)
            return null;
        ulong n = BitConverter.ToUInt64(data, 16);
        if ((ulong)data.Length != (ulong)HeaderLen + n * RecordLen)
            return null;
        count = (long)n;
        return data;
    }
}
'@
}

function Compare-FdrSidecars {
    <#
    Compare every <stem>.<Pass>-pass.fdr_scores.bin between two run directories, all seven
    scalar fields at Tolerance, matched by stem and then by entry_id. Returns Pass + Issues.

    -Pass selects which sidecar: 2 (default) is the one #4553 is about; 1 is the
    pre-reconciliation write, compared by the cross-impl gate so a 2nd-pass failure can be
    read as "pass 2 dropped it" rather than "the two runs diverged upstream".
    #>
    param(
        [Parameter(Mandatory = $true)][string]$ExpectedDir,
        [Parameter(Mandatory = $true)][string]$ActualDir,
        [ValidateSet(1, 2)][int]$Pass = 2,
        [double]$Tolerance = 1e-9
    )

    $issues = [System.Collections.Generic.List[string]]::new()
    $suffix = if ($Pass -eq 1) { '.1st-pass.fdr_scores.bin' } else { '.2nd-pass.fdr_scores.bin' }
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
        return @{ Pass = $false; Issues = $issues; Compared = 0 }
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

        $diff = [OspreyFdrSidecarComparer]::Compare($expected[$stem], $actual[$stem], $Tolerance)
        if (-not $diff.ReadableExpected -or -not $diff.ReadableActual) {
            # Name the reason. "Not readable" on a version bump reads as corruption and sends
            # the next person looking at the wrong thing.
            $why = if (-not $diff.ReadableExpected) { $diff.ProblemExpected } else { $diff.ProblemActual }
            $side = if (-not $diff.ReadableExpected) { 'expected' } else { 'actual' }
            $issues.Add("$stem$suffix unreadable on the $side side: $why")
            continue
        }
        if ($diff.CountExpected -ne $diff.CountActual) {
            $issues.Add("$stem$suffix record count $($diff.CountExpected) -> $($diff.CountActual)")
        }
        $nCompared += $diff.Compared

        # Per-field tallies so the summary names WHICH field drifted, not just that
        # something did -- the fields have very different failure meanings.
        for ($f = 0; $f -lt [OspreyFdrSidecarComparer]::FieldNames.Length; $f++) {
            if ($diff.FieldCounts[$f] -gt 0) {
                $issues.Add(("{0}: {1} differs on {2} record(s); first {3}" -f
                    $stem, [OspreyFdrSidecarComparer]::FieldNames[$f],
                    $diff.FieldCounts[$f], $diff.FirstExample[$f]))
            }
        }
    }

    return @{ Pass = ($issues.Count -eq 0); Issues = $issues; Compared = $nCompared }
}
