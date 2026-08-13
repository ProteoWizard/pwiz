<#
Per-file FDR score sidecar comparison.

Two consumers, one decoder:
  * regression.ps1's four-task-chain leg - distributed route vs straight-through, C# only.
  * ai/scripts/Osprey/Compare/Compare-FdrSidecars-Crossimpl.ps1 - C# vs Rust.

The four-task chain leg asserted only Compare-BlibFull, and the blib carries no protein
q-value, so a route that writes a different RunProteinQvalue into every
<stem>.2nd-pass.fdr_scores.bin passed green (issue #4553: 32,450 of 260,419 records differ
on StellarGenDecoyEntrap, 1.57% at 82 files). Peptide counts, protein-group counts and the
blib are all identical while it happens, so nothing the gate already reads can see it.

The cross-impl gate had the same blind spot from the other direction: it compares the
Stage 7 protein FDR dump (per-protein-GROUP columns) and the blib, neither of which carries
a per-entry SVM score or run protein q. Both implementations dropped the same fields, so
they agreed on the wrong value and nothing was red.

This compares the sidecars themselves, which is where the distributed tasks' per-file
output actually lands.

Record layout (Osprey.IO\FdrScoresSidecar.cs), v4: 32-byte header, 68-byte records,
  entry_id u32 @0, score f64 @4, run_precursor_q @12, run_peptide_q @20,
  experiment_precursor_q @28, experiment_peptide_q @36, pep @44, run_protein_q @52,
  experiment_aggregate_score @60 (issue #4522).
Header: magic @0..8, version @8, pass @9, record count u64 @16.

The decode + compare runs as compiled C#, not PowerShell. A per-record PowerShell loop
took over 10 minutes on one Astral 3-file pass (6.2M records) and would be unusable at the
82-file scale these gates are meant to reach; the compiled form is seconds.

A gate that reports a false PASS is worse than no gate, so every "cannot decode this" path
returns a NAMED problem rather than a silent skip, and the comparison refuses to call a
file pair equal on the strength of records it never read.
#>

if (-not ([System.Management.Automation.PSTypeName]'OspreyFdrSidecarComparer').Type) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.IO;

public class FdrSidecarField
{
    public string Name;
    public int Offset;
}

public class FdrSidecarDiff
{
    public bool ReadableExpected;
    public bool ReadableActual;
    public string ProblemExpected;
    public string ProblemActual;
    public long CountExpected;
    public long CountActual;
    public long DistinctExpected;
    public long DistinctActual;
    public long OnlyExpected;
    public long OnlyActual;
    public long DuplicateExpected;
    public long DuplicateActual;
    public long Compared;
    public long[] FieldCounts;
    public string[] FirstExample;
}

public static class OspreyFdrSidecarComparer
{
    private const int HeaderLen = 32;
    private const int RecordLen = 68;
    private const byte ExpectedVersion = 4;
    private static readonly byte[] Magic = { 0x4F, 0x53, 0x50, 0x52, 0x59, 0x46, 0x44, 0x52 }; // OSPRYFDR

    /// Name and byte offset in ONE table. They were parallel arrays whose lengths separately
    /// bounded the compare loop and the report loop, so extending one and not the other (the
    /// v4 68-byte layout on the #4522 branch adds experiment_aggregate_score at [60..68])
    /// tallied differences into a slot the report never read - a silent PASS on a real
    /// divergence. One array cannot drift against itself.
    public static readonly FdrSidecarField[] Fields =
    {
        new FdrSidecarField { Name = "score",                       Offset = 4  },
        new FdrSidecarField { Name = "run_precursor_qvalue",        Offset = 12 },
        new FdrSidecarField { Name = "run_peptide_qvalue",          Offset = 20 },
        new FdrSidecarField { Name = "experiment_precursor_qvalue", Offset = 28 },
        new FdrSidecarField { Name = "experiment_peptide_qvalue",   Offset = 36 },
        new FdrSidecarField { Name = "pep",                         Offset = 44 },
        new FdrSidecarField { Name = "run_protein_qvalue",          Offset = 52 },
        new FdrSidecarField { Name = "experiment_aggregate_score",  Offset = 60 },
    };

    public static FdrSidecarDiff Compare(
        string pathExpected, string pathActual, double tolerance, int expectedPass)
    {
        var result = new FdrSidecarDiff
        {
            FieldCounts = new long[Fields.Length],
            FirstExample = new string[Fields.Length],
        };

        byte[] a = ReadIfValid(pathExpected, expectedPass, out long na, out string problemA);
        byte[] b = ReadIfValid(pathActual, expectedPass, out long nb, out string problemB);
        result.ReadableExpected = a != null;
        result.ReadableActual = b != null;
        result.ProblemExpected = problemA;
        result.ProblemActual = problemB;
        result.CountExpected = na;
        result.CountActual = nb;
        if (a == null || b == null)
            return result;

        // entry_id -> record offset for the actual side. Last-wins on a duplicate, so count
        // duplicates explicitly: with them, record-count arithmetic no longer describes the
        // set difference, and drift confined to a non-last duplicate would be unreadable.
        var offsetById = new Dictionary<uint, int>();
        for (long i = 0; i < nb; i++)
        {
            int off = HeaderLen + (int)(i * RecordLen);
            uint id = BitConverter.ToUInt32(b, off);
            if (offsetById.ContainsKey(id))
                result.DuplicateActual++;
            offsetById[id] = off;
        }
        result.DistinctActual = offsetById.Count;

        var seenExpected = new HashSet<uint>();
        var matchedIds = new HashSet<uint>();
        for (long i = 0; i < na; i++)
        {
            int offA = HeaderLen + (int)(i * RecordLen);
            uint entryId = BitConverter.ToUInt32(a, offA);
            if (!seenExpected.Add(entryId))
                result.DuplicateExpected++;
            int offB;
            if (!offsetById.TryGetValue(entryId, out offB))
                continue;
            matchedIds.Add(entryId);
            result.Compared++;
            for (int f = 0; f < Fields.Length; f++)
            {
                double va = BitConverter.ToDouble(a, offA + Fields[f].Offset);
                double vb = BitConverter.ToDouble(b, offB + Fields[f].Offset);
                // Bit-equality first: Math.Abs(NaN - NaN) is NaN and every comparison against
                // it is false, so two byte-identical records holding a NaN would otherwise be
                // reported as differing from themselves. Matching infinities fail the same way.
                if (va.Equals(vb) || Math.Abs(va - vb) <= tolerance)
                    continue;
                result.FieldCounts[f]++;
                if (result.FirstExample[f] == null)
                {
                    result.FirstExample[f] = string.Format(
                        "entry_id={0} {1:R} -> {2:R}", entryId, va, vb);
                }
            }
        }
        result.DistinctExpected = seenExpected.Count;

        // TRUE set difference over distinct ids. Subtracting record counts is wrong the
        // moment either side holds a duplicate: it both masks a genuine set difference and
        // can print a negative remainder.
        result.OnlyExpected = seenExpected.Count - matchedIds.Count;
        result.OnlyActual = offsetById.Count - matchedIds.Count;
        return result;
    }

    /// Read a sidecar whose header validates, else null with a reason.
    ///
    /// Magic, version AND the pass byte are all checked. The pass byte matters because the
    /// caller globs by filename: a file written with the wrong Pass enum, or a 1st-pass
    /// sidecar copied to the 2nd-pass path, holds values every canonical reader rejects
    /// (FdrScoresSidecar.TryRead returns false on a pass mismatch) while a filename-only
    /// comparison would happily call the two sides equal.
    ///
    /// The version check matters for the same reason: a writer whose record width differs from
    /// ExpectedVersion's must be REFUSED by name rather than decoded at the wrong stride. The
    /// size check alone rejected the v3 -> v4 growth only because 68 and 60 happen not to divide
    /// alike, which is luck, not a guard.
    ///
    /// The size arithmetic is checked: 68 divides many lengths, so a corrupt count can
    /// satisfy the size test by wrapping mod 2^64 and then walk off the end of the buffer.
    /// The canonical reader wraps the identical expression for the identical reason.
    private static byte[] ReadIfValid(string path, int expectedPass, out long count, out string problem)
    {
        count = 0;
        problem = null;
        if (!File.Exists(path))
        {
            problem = "file not found";
            return null;
        }
        byte[] data;
        try
        {
            data = File.ReadAllBytes(path);
        }
        catch (Exception ex)
        {
            // An IO fault on one of 82 sidecars must become an issue line, not an exception
            // out of a static that both callers run under $ErrorActionPreference = 'Stop'
            // (which would abandon every remaining dataset in a multi-hour gate).
            problem = string.Format("could not be read: {0}", ex.Message);
            return null;
        }
        if (data.Length < HeaderLen)
        {
            problem = string.Format(
                "file is {0} bytes, shorter than the {1}-byte header", data.Length, HeaderLen);
            return null;
        }
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
        if (data[9] != expectedPass)
        {
            problem = string.Format(
                "header says pass {0} but this comparison was asked for pass {1}; every " +
                "canonical reader rejects this file", data[9], expectedPass);
            return null;
        }
        ulong n = BitConverter.ToUInt64(data, 16);
        long expectedLen;
        try
        {
            expectedLen = checked(HeaderLen + (long)n * RecordLen);
        }
        catch (OverflowException)
        {
            problem = string.Format("header record count {0} is not credible (overflows)", n);
            return null;
        }
        if (data.Length != expectedLen)
        {
            problem = string.Format(
                "size {0} does not match header ({1} + {2} records x {3} bytes = {4}); " +
                "truncated, or written by a different record layout",
                data.Length, HeaderLen, n, RecordLen, expectedLen);
            return null;
        }
        count = (long)n;
        return data;
    }
}
'@
}

function Compare-FdrSidecars {
    <#
    Compare every <stem>.<Pass>-pass.fdr_scores.bin between two run directories, all seven
    scalar fields at Tolerance, matched by stem and then by entry_id. Returns Pass, Issues
    and Compared (the record count actually verified - the caller should assert it is
    non-zero, because comparing nothing is not the same as agreeing).

    -Pass selects which sidecar AND is validated against the header pass byte: 2 (default)
    is the one #4553 is about; 1 is the pre-reconciliation write, compared by the cross-impl
    gate so a 2nd-pass failure can be read as "pass 2 dropped it" rather than "the two runs
    diverged upstream".
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

        $diff = [OspreyFdrSidecarComparer]::Compare(
            $expected[$stem], $actual[$stem], $Tolerance, $Pass)
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

        # Only entry_ids present on BOTH sides get their fields compared, so equal counts do
        # not by themselves mean the same population - one swapped entry_id leaves the counts
        # intact and its two records simply go uncompared. This is a true set difference over
        # distinct ids, not count arithmetic, so a duplicate cannot mask it.
        if ($diff.OnlyExpected -ne 0 -or $diff.OnlyActual -ne 0) {
            $issues.Add((("{0}{1}: entry_id sets differ - {2} matched, {3} only in expected, " +
                "{4} only in actual") -f $stem, $suffix, $diff.Compared,
                $diff.OnlyExpected, $diff.OnlyActual))
        }
        if ($diff.DuplicateExpected -ne 0 -or $diff.DuplicateActual -ne 0) {
            # Duplicates make the last record win the lookup, so drift confined to an earlier
            # one is invisible. Never seen in 1.9 billion real records, but this comparison
            # cannot report agreement it did not establish.
            $issues.Add((("{0}{1}: duplicate entry_id(s) - {2} on the expected side, {3} on " +
                "the actual side; only the last record of each is compared") -f
                $stem, $suffix, $diff.DuplicateExpected, $diff.DuplicateActual))
        }
        if ($diff.Compared -eq 0) {
            $issues.Add("$stem$suffix compared 0 records - agreeing on nothing is not agreement")
        }
        $nCompared += $diff.Compared

        # Per-field tallies so the summary names WHICH field drifted, not just that
        # something did - the fields have very different failure meanings.
        for ($f = 0; $f -lt [OspreyFdrSidecarComparer]::Fields.Length; $f++) {
            if ($diff.FieldCounts[$f] -gt 0) {
                $issues.Add(("{0}: {1} differs on {2} record(s); first {3}" -f
                    $stem, [OspreyFdrSidecarComparer]::Fields[$f].Name,
                    $diff.FieldCounts[$f], $diff.FirstExample[$f]))
            }
        }
    }

    return @{ Pass = ($issues.Count -eq 0); Issues = $issues; Compared = $nCompared }
}
