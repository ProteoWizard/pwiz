<#
Per-file FDR score sidecar comparison.

Two consumers, one decoder:
  * regression.ps1's four-task-chain leg - distributed route vs straight-through, C# only.
  * ai/scripts/Osprey/Compare/Compare-FdrSidecars-Crossimpl.ps1 - C# vs Rust.

The four-task chain leg asserted only Compare-BlibFull, and the blib carries no protein
q-value, so a route that writes a different ExperimentProteinQvalue into every
<stem>.2nd-pass.fdr_scores.bin passed green (issue #4553: 32,450 of 260,419 records differ
on StellarGenDecoyEntrap, 1.57% at 82 files). Peptide counts, protein-group counts and the
blib are all identical while it happens, so nothing the gate already reads can see it.

The cross-impl gate had the same blind spot from the other direction: it compares the
Stage 7 protein FDR dump (per-protein-GROUP columns) and the blib, neither of which carries
a per-entry SVM score or protein q. Both implementations dropped the same fields, so
they agreed on the wrong value and nothing was red.

This compares the sidecars themselves, which is where the distributed tasks' per-file
output actually lands.

Test-Pass2ProteinQvalue (below) covers the case a two-route comparison structurally CANNOT:
a value both routes copy identically from pass 1. See issue #4559.

Two artifacts since the v5 scope split (issue #4486), and this decodes both.

Per-file run-scope (Osprey.IO\FdrScoresSidecar.cs), v6: 32-byte header, 28-byte records,
  entry_id u32 @0, score f64 @4, run_precursor_q @12, run_peptide_q @20.
  NO pep: it is experiment-scope and moved to the experiment sidecar (issue #4486).
  Magic OSPRYFDR. One record per OBSERVATION, one file per input.

Analysis-wide experiment-scope (Osprey.IO\FdrExperimentSidecar.cs), v1: 32-byte header,
  v2: 44-byte records, entry_id u32 @0, experiment_precursor_q f64 @4,
  experiment_peptide_q @12, experiment_protein_q @20, experiment_aggregate_score @28,
  pep @36.
  Magic OSPRYEXP. One record per DISTINCT entry_id, ONE file per pass per analysis, named
  after the output blib.

Both headers: magic @0..8, version @8, pass @9, record count u64 @16.

Rust FUSED per-file (osprey/crates/osprey/src/pipeline.rs, write_fdr_scores_sidecar), v4:
  32-byte header, 68-byte records carrying BOTH scopes in one per-file artifact,
  entry_id u32 @0, score f64 @4, run_precursor_q @12, run_peptide_q @20,
  experiment_precursor_q @28, experiment_peptide_q @36, pep @44,
  experiment_protein_q @52, experiment_aggregate_score @60. Magic OSPRYFDR, version 4.

The v5 scope split left the two implementations with no like-for-like artifact: the same
nine values are one Rust file and two C# files. Compare-FdrSidecarsFused rebuilds Rust's
per-observation view from the C# pair - run scope from this file's record, experiment scope
joined by entry_id from the analysis-wide file - so the cross-impl gate compares MEANING
rather than bytes. That join is not a formality: it is the whole behavioural difference,
because the C# side now answers "this entry's experiment q" once per analysis where the
fused form answers it once per observation and can disagree with itself across runs.

The decode + compare runs as compiled C#, not PowerShell. A per-record PowerShell loop
took over 10 minutes on one Astral 3-file pass (6.2M records) and would be unusable at the
82-file scale these gates are meant to reach; the compiled form is seconds.

A gate that reports a false PASS is worse than no gate, so every "cannot decode this" path
returns a NAMED problem rather than a silent skip, and the comparison refuses to call a
file pair equal on the strength of records it never read.
#>

# A .NET type cannot be replaced once loaded into a PowerShell session, and the guard below
# keys on the type NAME - so a session that already dot-sourced an older copy of this file keeps
# that older type. Such a session would throw "does not contain a method named X" from deep
# inside a leg, aborting a multi-hour gate mid-dataset with an error that names neither the
# cause nor the cure. Reloading is impossible, so say so immediately instead. Reachable via a
# second regression.ps1 invocation in one session, or Compare-FdrSidecars-Crossimpl.ps1
# dot-sourcing a sibling checkout's copy first.
#
# Every method this file added AFTER the type first shipped is listed, not just the newest:
# the copies in circulation differ by which ones they have, so checking only the latest lets
# an intermediate vintage load and then fail later at the call site this guard exists to
# pre-empt.
$ospreyComparerType = ([System.Management.Automation.PSTypeName]'OspreyFdrSidecarComparer').Type
if ($ospreyComparerType) {
    foreach ($required in @('CheckPass2ProteinQ', 'LoadExperimentMap', 'CompareFused',
                            'CompareBytes')) {
        if (-not $ospreyComparerType.GetMethod($required)) {
            throw ("An older OspreyFdrSidecarComparer is already loaded in this PowerShell " +
                   "session and has no $required method. A loaded .NET type cannot be " +
                   "replaced, so this session cannot run every sidecar gate. Start a fresh " +
                   "pwsh session and re-run.")
        }
    }
}
if (-not $ospreyComparerType) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.IO;

public class FdrSidecarField
{
    public string Name;
    public int Offset;
}

/// One field of the FUSED cross-impl view: where it lives in Rust's 68-byte per-file record,
/// and where the C# side keeps the same value now that the two scopes are separate artifacts.
/// Three facts in ONE row for the same reason Fields is one array - a name, a Rust offset and
/// a C# source that can drift apart are three chances to compare the wrong pair of bytes and
/// report agreement.
public class FdrFusedField
{
    public string Name;
    public int RustOffset;
    public int CsOffset;
    /// true = read from the analysis-wide experiment sidecar (joined by entry_id),
    /// false = read from this file's own run-scope sidecar (read at the matched record).
    public bool FromExperiment;
}

/// The analysis-wide experiment-scope sidecar, decoded once and reused for every per-file
/// comparison in the run. It is ONE file per pass per analysis, so re-reading it per input
/// file would re-parse the same megabytes N times for nothing.
public class FdrExperimentMap
{
    public bool Readable;
    public string Problem;
    public byte[] Data;
    public Dictionary<uint, int> OffsetById;
    public long Count;
}

/// Result of a whole-file byte comparison: equal or not, and enough detail to act on when not.
public class FdrByteDiff
{
    public bool Readable;
    public string Problem;
    public bool Equal;
    public long LengthExpected;
    public long LengthActual;
    public long FirstDiffOffset;
    public long DiffCount;
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
    /// Fused compare only: observations whose entry_id the per-file run-scope sidecar carries
    /// but the analysis-wide experiment sidecar does not. Counted, never skipped - the C# side
    /// claims one experiment-scope record per distinct entry_id in the analysis, so a miss is
    /// that claim failing, and silently dropping those rows would hide it behind a PASS.
    public long MissingExperiment;
    public string FirstMissingExperiment;
}

public class Pass2ProteinQLiveness
{
    public bool Readable;
    public string Problem;
    public long Matched;
    public long Differing;
    public long DifferingAtDefault;
    public long AbsentFromPass1;
}

public static class OspreyFdrSidecarComparer
{
    private const int HeaderLen = 32;
    private const int RecordLen = 28;
    private const byte ExpectedVersion = 6;
    private static readonly byte[] Magic = { 0x4F, 0x53, 0x50, 0x52, 0x59, 0x46, 0x44, 0x52 }; // OSPRYFDR

    // The analysis-wide experiment-scope sidecar (format v5, issue #4486): its own magic, its
    // own version, one record per DISTINCT entry_id.
    public const int ExperimentHeaderLen = 32;
    public const int ExperimentRecordLen = 44;
    private const byte ExpectedExperimentVersion = 2;
    private static readonly byte[] ExperimentMagic =
        { 0x4F, 0x53, 0x50, 0x52, 0x59, 0x45, 0x58, 0x50 }; // OSPRYEXP

    /// Name and byte offset in ONE table. They were parallel arrays whose lengths separately
    /// bounded the compare loop and the report loop, so extending one and not the other (the
    /// v4 68-byte layout on the #4522 branch adds experiment_aggregate_score at [60..68])
    /// tallied differences into a slot the report never read - a silent PASS on a real
    /// divergence. One array cannot drift against itself.
    public static readonly FdrSidecarField[] Fields =
    {
        new FdrSidecarField { Name = "score",                Offset = 4  },
        new FdrSidecarField { Name = "run_precursor_qvalue", Offset = 12 },
        new FdrSidecarField { Name = "run_peptide_qvalue",   Offset = 20 },
    };

    /// The experiment-scope record's fields, in its own file. Kept as a separate table for the
    /// reason the note above gives: one array cannot drift against itself, and two files with
    /// different layouts must not share one.
    public static readonly FdrSidecarField[] ExperimentFields =
    {
        new FdrSidecarField { Name = "experiment_precursor_qvalue", Offset = 4  },
        new FdrSidecarField { Name = "experiment_peptide_qvalue",   Offset = 12 },
        new FdrSidecarField { Name = "experiment_protein_qvalue",   Offset = 20 },
        new FdrSidecarField { Name = "experiment_aggregate_score",  Offset = 28 },
        new FdrSidecarField { Name = "pep",                          Offset = 36 },
    };

    // Rust's FUSED per-file sidecar (write_fdr_scores_sidecar, pipeline.rs): the same nine
    // values in ONE artifact, at its own version and stride. Same magic as the C# run-scope
    // file, which is why the version byte is what tells the two apart.
    private const int FusedHeaderLen = 32;
    private const int FusedRecordLen = 68;
    private const byte ExpectedFusedVersion = 4;

    /// The fused view, field by field: Rust's offset in its 68-byte record beside the C#
    /// offset and which of the two C# artifacts holds it. This table IS the equivalence claim
    /// the cross-impl gate makes, so it is written once and read by both the compare loop and
    /// the report loop.
    ///
    /// entry_id is not a row here: it is the join key, checked before any field is read.
    public static readonly FdrFusedField[] FusedFields =
    {
        new FdrFusedField { Name = "score",                       RustOffset = 4,  CsOffset = 4,  FromExperiment = false },
        new FdrFusedField { Name = "run_precursor_qvalue",        RustOffset = 12, CsOffset = 12, FromExperiment = false },
        new FdrFusedField { Name = "run_peptide_qvalue",          RustOffset = 20, CsOffset = 20, FromExperiment = false },
        // PEP moved to the EXPERIMENT record (issue #4486), and its MEANING changed with it.
        // Rust still writes it per OBSERVATION - real on the base_id winner's row, 1.0 on every
        // other row of that entry. C# now stores one value per entry_id, which every observation
        // of that entry reports, because PEP is PosteriorError(winner score) and that is a
        // property of the precursor, not of a row.
        //
        // So the two sides agree on the winner's row and disagree everywhere else BY DESIGN, and
        // C# cannot reproduce Rust's view: doing so needs the winning RUN, which is exactly what
        // was removed and which mean-best-N cannot express at all (there the aggregate is a mean
        // of N runs, so no single run originates it). Compared through the join below rather
        // Mapped straight through for now, so this run REPORTS the divergence at full size
        // rather than hiding it: every row whose entry won somewhere else will differ. That is
        // deliberate - narrowing the comparison (e.g. to rows where Rust has a non-1.0 value,
        // which are the only rows carrying information on its side) is a semantic change to a
        // parity gate and needs explicit sign-off, not a quiet edit here.
        new FdrFusedField { Name = "pep",                         RustOffset = 44, CsOffset = 36, FromExperiment = true  },
        new FdrFusedField { Name = "experiment_precursor_qvalue", RustOffset = 28, CsOffset = 4,  FromExperiment = true  },
        new FdrFusedField { Name = "experiment_peptide_qvalue",   RustOffset = 36, CsOffset = 12, FromExperiment = true  },
        new FdrFusedField { Name = "experiment_protein_qvalue",   RustOffset = 52, CsOffset = 20, FromExperiment = true  },
        new FdrFusedField { Name = "experiment_aggregate_score",  RustOffset = 60, CsOffset = 28, FromExperiment = true  },
    };

    /// Byte offset of a field, by name, so a caller cannot hardcode an offset that the next
    /// layout change silently moves out from under it.
    public static int OffsetOf(string name)
    {
        foreach (var f in Fields)
        {
            if (f.Name == name)
                return f.Offset;
        }
        throw new ArgumentException("no such sidecar field: " + name);
    }

    /// Is the 2nd-pass sidecar's experiment_protein_qvalue a SECOND-PASS value, or a verbatim
    /// copy of the first pass? Issue #4559: no pass-2 mode wrote the column, so it reached the
    /// 2nd-pass file carrying whatever pass 1 put there - the one column in that file that was
    /// unconditionally a pass-1 value. Nothing could see it: the two-route comparison above is
    /// blind because BOTH routes copied it, and the golden reads the per-GROUP protein dump,
    /// never this per-entry column.
    ///
    /// This is a LIVENESS check, not an equivalence one. It cannot say the pass-2 value is
    /// correct; it says the pass-2 protein FDR result reached the file at all. That is exactly
    /// the failure class that hid here, and it is checkable from one run's own output with no
    /// baseline. Records absent from pass 1 (gap-fill) cannot be compared and are counted
    /// separately rather than silently skipped.
    public static Pass2ProteinQLiveness CheckPass2ProteinQ(string pass1Path, string pass2Path)
    {
        var result = new Pass2ProteinQLiveness();
        byte[] a = ReadExperimentIfValid(pass1Path, 1, out long na, out string problemA);
        byte[] b = ReadExperimentIfValid(pass2Path, 2, out long nb, out string problemB);
        if (a == null || b == null)
        {
            result.Problem = a == null ? problemA : problemB;
            return result;
        }
        result.Readable = true;

        // experiment_protein_qvalue lives in the analysis-wide experiment sidecar at format v5
        // (issue #4486), one record per distinct entry_id, so this is now ONE file per pass for
        // the whole run rather than one per input file.
        const int off = 20;   // ExperimentFields: experiment_protein_qvalue
        var pass1ById = new Dictionary<uint, double>();
        for (long i = 0; i < na; i++)
        {
            int o = ExperimentHeaderLen + (int)(i * ExperimentRecordLen);
            pass1ById[BitConverter.ToUInt32(a, o)] = BitConverter.ToDouble(a, o + off);
        }
        for (long i = 0; i < nb; i++)
        {
            int o = ExperimentHeaderLen + (int)(i * ExperimentRecordLen);
            uint id = BitConverter.ToUInt32(b, o);
            double q2 = BitConverter.ToDouble(b, o + off);
            double q1;
            if (!pass1ById.TryGetValue(id, out q1))
            {
                result.AbsentFromPass1++;
                continue;
            }
            result.Matched++;
            if (BitConverter.DoubleToInt64Bits(q1) != BitConverter.DoubleToInt64Bits(q2))
            {
                result.Differing++;
                // "Differs from pass 1" alone stopped being sufficient when #4559 also removed
                // the pass-1 seed from RestorePass1Scalars: an UNPATCHED record now reads 1.0,
                // the ResetScores default, which also differs from the pass-1 value. So a run
                // with the patch reverted would still show Differing > 0 and pass. Counting the
                // defaults separately lets the caller tell a real pass-2 value from the patch
                // never having run.
                if (q2 == 1.0)
                    result.DifferingAtDefault++;
            }
        }
        return result;
    }

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

    /// Byte-equality for two artifacts that must be identical, with the first differing offset
    /// and a bounded difference count when they are not.
    ///
    /// This exists because PowerShell's Compare-Object is catastrophically wrong for the job:
    /// it boxes every element into a PSObject and builds hash tables, so comparing two 85.8 MB
    /// experiment sidecars (Astral, 2,498,773 records) drove the harness process to a 53 GB
    /// working set and stalled a -Dataset All gate for many minutes with no output. The
    /// operation is a memcmp. Reading both files costs the sum of their sizes and the compare
    /// is a vectorized span equality, so the whole check is under a second.
    ///
    /// Counting differences is bounded: a file written by a route that diverged early differs
    /// in most of its bytes, and the count is only ever used to say "these differ, badly".
    public static FdrByteDiff CompareBytes(string pathExpected, string pathActual, long maxCount)
    {
        var result = new FdrByteDiff();
        byte[] a, b;
        try
        {
            a = File.ReadAllBytes(pathExpected);
            b = File.ReadAllBytes(pathActual);
        }
        catch (Exception ex)
        {
            result.Problem = string.Format("could not be read: {0}", ex.Message);
            return result;
        }
        result.Readable = true;
        result.LengthExpected = a.LongLength;
        result.LengthActual = b.LongLength;
        if (a.LongLength == b.LongLength &&
            new ReadOnlySpan<byte>(a).SequenceEqual(new ReadOnlySpan<byte>(b)))
        {
            result.Equal = true;
            return result;
        }
        result.FirstDiffOffset = -1;
        long common = Math.Min(a.LongLength, b.LongLength);
        for (long i = 0; i < common; i++)
        {
            if (a[i] == b[i])
                continue;
            if (result.FirstDiffOffset < 0)
                result.FirstDiffOffset = i;
            result.DiffCount++;
            if (result.DiffCount >= maxCount)
                break;
        }
        // A pure length difference has no differing byte in the common prefix, and reporting
        // "0 differing bytes" for two files that are not equal would read as a passing check.
        if (result.DiffCount == 0 && a.LongLength != b.LongLength)
            result.FirstDiffOffset = common;
        return result;
    }

    /// Decode the analysis-wide experiment-scope sidecar once, into an entry_id -> record
    /// offset index the per-file fused comparisons all share.
    ///
    /// A duplicate entry_id here is a contract violation rather than a tolerable oddity - the
    /// file is defined as one record per DISTINCT entry_id - so it is reported as a problem
    /// instead of being resolved last-wins. Silently picking one of two records would make the
    /// gate's answer depend on write order.
    public static FdrExperimentMap LoadExperimentMap(string path, int expectedPass)
    {
        var map = new FdrExperimentMap();
        long n;
        string problem;
        byte[] data = ReadExperimentIfValid(path, expectedPass, out n, out problem);
        if (data == null)
        {
            map.Problem = problem;
            return map;
        }
        var offsetById = new Dictionary<uint, int>((int)Math.Min(n, int.MaxValue));
        for (long i = 0; i < n; i++)
        {
            int off = ExperimentHeaderLen + (int)(i * ExperimentRecordLen);
            uint id = BitConverter.ToUInt32(data, off);
            if (offsetById.ContainsKey(id))
            {
                map.Problem = string.Format(
                    "duplicate entry_id {0} in the analysis-wide experiment sidecar, which is " +
                    "defined as one record per distinct entry_id: {1}", id, path);
                return map;
            }
            offsetById[id] = off;
        }
        map.Readable = true;
        map.Data = data;
        map.OffsetById = offsetById;
        map.Count = n;
        return map;
    }

    /// Compare Rust's FUSED per-file sidecar against the C# pair that replaced it: this file's
    /// run-scope record for the run-scope fields, and the analysis-wide experiment record for
    /// the same entry_id for the experiment-scope ones.
    ///
    /// The join is the comparison's whole content. Rust answers "this entry's experiment q"
    /// once per OBSERVATION and can therefore give one entry different answers in different
    /// runs; the C# side answers it once per ANALYSIS. Where the two disagree, this names the
    /// field and the entry rather than reporting that two artifacts are not byte-identical -
    /// which they cannot be, and never will be again.
    public static FdrSidecarDiff CompareFused(
        string rustPath, string csRunPath, FdrExperimentMap csExperiment,
        double tolerance, int expectedPass)
    {
        var result = new FdrSidecarDiff
        {
            FieldCounts = new long[FusedFields.Length],
            FirstExample = new string[FusedFields.Length],
        };

        long na, nb;
        string problemA, problemB;
        byte[] a = ReadFusedIfValid(rustPath, expectedPass, out na, out problemA);
        byte[] b = ReadIfValid(csRunPath, expectedPass, out nb, out problemB);
        result.ReadableExpected = a != null;
        result.ReadableActual = b != null;
        result.ProblemExpected = problemA;
        result.ProblemActual = problemB;
        result.CountExpected = na;
        result.CountActual = nb;
        if (a == null || b == null)
            return result;
        if (csExperiment == null || !csExperiment.Readable)
        {
            // Not "the C# file is unreadable" - the per-file one read fine. Naming the
            // analysis-wide artifact is what sends the next person to the right file.
            result.ReadableActual = false;
            result.ProblemActual = csExperiment == null
                ? "no analysis-wide experiment sidecar was supplied"
                : csExperiment.Problem;
            return result;
        }

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
        // Distinct ids, kept apart from the OBSERVATION tally in MissingExperiment: the set
        // arithmetic below is over distinct ids, and subtracting a record count from it would
        // under-report - or go negative - the moment one missing id appears twice.
        var missingExperimentIds = new HashSet<uint>();
        for (long i = 0; i < na; i++)
        {
            int offA = FusedHeaderLen + (int)(i * FusedRecordLen);
            uint entryId = BitConverter.ToUInt32(a, offA);
            if (!seenExpected.Add(entryId))
                result.DuplicateExpected++;
            int offB;
            if (!offsetById.TryGetValue(entryId, out offB))
                continue;
            int offExp;
            if (!csExperiment.OffsetById.TryGetValue(entryId, out offExp))
            {
                result.MissingExperiment++;
                missingExperimentIds.Add(entryId);
                if (result.FirstMissingExperiment == null)
                {
                    result.FirstMissingExperiment = string.Format(
                        "entry_id={0}", entryId);
                }
                continue;
            }
            matchedIds.Add(entryId);
            result.Compared++;
            for (int f = 0; f < FusedFields.Length; f++)
            {
                double va = BitConverter.ToDouble(a, offA + FusedFields[f].RustOffset);
                double vb = FusedFields[f].FromExperiment
                    ? BitConverter.ToDouble(csExperiment.Data, offExp + FusedFields[f].CsOffset)
                    : BitConverter.ToDouble(b, offB + FusedFields[f].CsOffset);
                // Bit-equality first, for the reason Compare gives: NaN and matching
                // infinities both fail a tolerance test against themselves.
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
        // "Only in expected" means absent from the C# PER-FILE sidecar. An id the per-file
        // file has but the analysis-wide file lacks is a different failure with a different
        // cause, so it is reported under MissingExperiment and excluded here rather than
        // folded into a set difference it would misdescribe.
        result.OnlyExpected =
            seenExpected.Count - matchedIds.Count - missingExperimentIds.Count;
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
    /// Read + validate an EXPERIMENT-scope sidecar (own magic, own version, 36-byte records).
    /// Same contract as ReadIfValid: null with a NAMED problem rather than a silent skip, because
    /// a gate that reports a false PASS is worse than no gate.
    public static byte[] ReadExperimentIfValid(string path, int expectedPass, out long count, out string problem)
    {
        count = 0;
        problem = null;
        if (!File.Exists(path))
        {
            problem = "missing experiment sidecar: " + path;
            return null;
        }
        byte[] data = File.ReadAllBytes(path);
        if (data.Length < ExperimentHeaderLen)
        {
            problem = "experiment sidecar shorter than its header: " + path;
            return null;
        }
        for (int i = 0; i < ExperimentMagic.Length; i++)
        {
            if (data[i] != ExperimentMagic[i])
            {
                problem = "experiment sidecar bad magic: " + path;
                return null;
            }
        }
        if (data[8] != ExpectedExperimentVersion)
        {
            problem = "experiment sidecar version " + data[8] + ", expected " +
                      ExpectedExperimentVersion + ": " + path;
            return null;
        }
        if (data[9] != (byte)expectedPass)
        {
            problem = "experiment sidecar pass " + data[9] + ", expected " + expectedPass + ": " + path;
            return null;
        }
        ulong headerCount = BitConverter.ToUInt64(data, 16);
        long expectedLen = (long)ExperimentHeaderLen + (long)headerCount * ExperimentRecordLen;
        if (data.LongLength != expectedLen)
        {
            problem = "experiment sidecar length " + data.LongLength + " != header count " +
                      headerCount + " implies " + expectedLen + ": " + path;
            return null;
        }
        count = (long)headerCount;
        return data;
    }

    /// Read + validate Rust's FUSED per-file sidecar. Same magic as the C# run-scope file, so
    /// the VERSION byte is the only thing that separates the two layouts - and a 36-byte file
    /// read at a 68-byte stride produces plausible garbage rather than an error, which is why
    /// the version is refused by name before the size arithmetic is trusted.
    private static byte[] ReadFusedIfValid(
        string path, int expectedPass, out long count, out string problem)
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
            problem = string.Format("could not be read: {0}", ex.Message);
            return null;
        }
        if (data.Length < FusedHeaderLen)
        {
            problem = string.Format(
                "file is {0} bytes, shorter than the {1}-byte header",
                data.Length, FusedHeaderLen);
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
        if (data[8] != ExpectedFusedVersion)
        {
            problem = string.Format(
                "sidecar format version {0}, but the fused cross-impl comparison decodes " +
                "version {1} ({2}-byte records). Version {3} is the C# scope-split layout, " +
                "which belongs on the OTHER side of this comparison.",
                data[8], ExpectedFusedVersion, FusedRecordLen, ExpectedVersion);
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
            expectedLen = checked(FusedHeaderLen + (long)n * FusedRecordLen);
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
                data.Length, FusedHeaderLen, n, FusedRecordLen, expectedLen);
            return null;
        }
        count = (long)n;
        return data;
    }

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

function Compare-FdrSidecarsFused {
    <#
    Cross-implementation comparison of the nine per-observation FDR values, against a Rust run
    whose sidecars still FUSE both scopes into one 68-byte per-file record.

    There is no byte-level comparison to make after the v5 scope split: the same values are one
    artifact on the Rust side and two on the C# side. This rebuilds Rust's per-observation view
    from the C# pair - run scope from the matched per-file record, experiment scope joined by
    entry_id from the analysis-wide file - and compares the nine values field by field.

    The join is not bookkeeping. It is where the two implementations now genuinely differ: a
    fused record answers "this entry's experiment q" once per observation and can disagree with
    itself across runs of the same analysis, which the C# side made structurally impossible.
    Expect this to name experiment-scope fields until the Rust side reads its pass-1 experiment
    values per ENTRY rather than per file (osprey/crates/osprey/src/pipeline.rs, the
    protein-compact map-back).

    Returns Pass, Issues and Compared (records actually verified - the caller should assert it
    is non-zero, because comparing nothing is not agreement).
    #>
    param(
        [Parameter(Mandatory = $true)][string]$RustDir,
        [Parameter(Mandatory = $true)][string]$CsDir,
        [ValidateSet(1, 2)][int]$Pass = 2,
        [double]$Tolerance = 1e-9
    )

    $issues = [System.Collections.Generic.List[string]]::new()
    $passLabel = if ($Pass -eq 1) { '1st' } else { '2nd' }
    $suffix = ".$passLabel-pass.fdr_scores.bin"
    $expSuffix = ".$passLabel-pass.fdr_experiment.bin"

    $rust = @{}
    foreach ($f in Get-ChildItem -File -Path $RustDir -Filter "*$suffix" -ErrorAction SilentlyContinue) {
        $rust[$f.Name.Substring(0, $f.Name.Length - $suffix.Length)] = $f.FullName
    }
    $cs = @{}
    foreach ($f in Get-ChildItem -File -Path $CsDir -Filter "*$suffix" -ErrorAction SilentlyContinue) {
        $cs[$f.Name.Substring(0, $f.Name.Length - $suffix.Length)] = $f.FullName
    }
    if ($rust.Count -eq 0) {
        $issues.Add("no $suffix files in the Rust dir $RustDir")
        return @{ Pass = $false; Issues = $issues; Compared = 0 }
    }
    foreach ($stem in $rust.Keys) {
        if (-not $cs.ContainsKey($stem)) { $issues.Add("missing $stem$suffix in $CsDir") }
    }
    foreach ($stem in $cs.Keys) {
        if (-not $rust.ContainsKey($stem)) { $issues.Add("unexpected $stem$suffix in $CsDir") }
    }

    # ONE experiment sidecar per pass per analysis, named after the output blib rather than
    # after an input file. More than one means two analyses wrote into the same directory, and
    # picking either would silently compare against the wrong run.
    $expFiles = @(Get-ChildItem -File -Path $CsDir -Filter "*$expSuffix" -ErrorAction SilentlyContinue)
    if ($expFiles.Count -eq 0) {
        $issues.Add(("no $expSuffix in $CsDir - this C# run predates the v5 scope split, or " +
            "wrote no analysis-wide experiment sidecar at all"))
        return @{ Pass = $false; Issues = $issues; Compared = 0 }
    }
    if ($expFiles.Count -gt 1) {
        $issues.Add(("{0} files match *{1} in {2} - one analysis writes exactly one, so this " +
            "directory holds more than one run" -f $expFiles.Count, $expSuffix, $CsDir))
        return @{ Pass = $false; Issues = $issues; Compared = 0 }
    }
    $expMap = [OspreyFdrSidecarComparer]::LoadExperimentMap($expFiles[0].FullName, $Pass)
    if (-not $expMap.Readable) {
        $issues.Add("analysis-wide experiment sidecar unusable: $($expMap.Problem)")
        return @{ Pass = $false; Issues = $issues; Compared = 0 }
    }

    $nCompared = 0
    foreach ($stem in ($rust.Keys | Sort-Object)) {
        if (-not $cs.ContainsKey($stem)) { continue }

        $diff = [OspreyFdrSidecarComparer]::CompareFused(
            $rust[$stem], $cs[$stem], $expMap, $Tolerance, $Pass)
        if (-not $diff.ReadableExpected -or -not $diff.ReadableActual) {
            $why = if (-not $diff.ReadableExpected) { $diff.ProblemExpected } else { $diff.ProblemActual }
            $side = if (-not $diff.ReadableExpected) { 'rust' } else { 'cs' }
            $issues.Add("$stem$suffix unreadable on the $side side: $why")
            continue
        }
        if ($diff.CountExpected -ne $diff.CountActual) {
            $issues.Add("$stem$suffix record count $($diff.CountExpected) -> $($diff.CountActual)")
        }
        if ($diff.OnlyExpected -ne 0 -or $diff.OnlyActual -ne 0) {
            $issues.Add((("{0}{1}: entry_id sets differ - {2} matched, {3} only in rust, " +
                "{4} only in cs") -f $stem, $suffix, $diff.Compared,
                $diff.OnlyExpected, $diff.OnlyActual))
        }
        if ($diff.MissingExperiment -ne 0) {
            $issues.Add((("{0}{1}: {2} observation(s) whose entry_id the per-file sidecar " +
                "carries but the analysis-wide experiment sidecar does not (first {3}); the " +
                "C# side claims one experiment record per distinct entry_id") -f
                $stem, $suffix, $diff.MissingExperiment, $diff.FirstMissingExperiment))
        }
        if ($diff.DuplicateExpected -ne 0 -or $diff.DuplicateActual -ne 0) {
            $issues.Add((("{0}{1}: duplicate entry_id(s) - {2} on the rust side, {3} on the cs " +
                "side; only the last record of each is compared") -f
                $stem, $suffix, $diff.DuplicateExpected, $diff.DuplicateActual))
        }
        if ($diff.Compared -eq 0) {
            $issues.Add("$stem$suffix compared 0 records - agreeing on nothing is not agreement")
        }
        $nCompared += $diff.Compared

        for ($f = 0; $f -lt [OspreyFdrSidecarComparer]::FusedFields.Length; $f++) {
            if ($diff.FieldCounts[$f] -gt 0) {
                $issues.Add(("{0}: {1} differs on {2} record(s); first {3}" -f
                    $stem, [OspreyFdrSidecarComparer]::FusedFields[$f].Name,
                    $diff.FieldCounts[$f], $diff.FirstExample[$f]))
            }
        }
    }

    return @{ Pass = ($issues.Count -eq 0); Issues = $issues; Compared = $nCompared }
}

function Test-Pass2ProteinQvalue {
    <#
    Assert that each <stem>.2nd-pass.fdr_scores.bin carries a SECOND-PASS
    experiment_protein_qvalue rather than a verbatim copy of the 1st-pass column.

    Single-run property, so it needs no baseline and no second route - which is the point.
    Issue #4559: no pass-2 mode wrote that column, so it reached the 2nd-pass file holding
    whatever pass 1 put there, and nothing could see it. Compare-FdrSidecars is blind because
    both routes copied the same wrong value (the shared-defect blind spot), and the golden
    reads the per-GROUP Stage 7 dump, never this per-entry column.

    LIVENESS, not equivalence: this cannot say the pass-2 value is right, only that the
    second-pass protein FDR reached the file. Records absent from pass 1 (gap-fill) cannot be
    compared and are reported separately rather than silently skipped.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$RunDir
    )

    $issues = [System.Collections.Generic.List[string]]::new()
    # ONE file per pass for the whole analysis at format v5 (issue #4486), named after the blib
    # rather than after an input file, so this globs for the pair instead of walking per-file
    # sidecars.
    $suffix = '.2nd-pass.fdr_experiment.bin'
    $files = @(Get-ChildItem -File -Path $RunDir -Filter "*$suffix" -ErrorAction SilentlyContinue)
    if ($files.Count -eq 0) {
        $issues.Add("no $suffix file in $RunDir")
        return @{ Pass = $false; Issues = $issues; Matched = 0 }
    }

    $totalMatched = 0
    $totalDiffering = 0
    $totalDefault = 0
    $totalGapFill = 0
    foreach ($f in ($files | Sort-Object Name)) {
        $stem = $f.Name.Substring(0, $f.Name.Length - $suffix.Length)
        $pass1 = Join-Path $RunDir "$stem.1st-pass.fdr_experiment.bin"
        if (-not (Test-Path $pass1)) {
            $issues.Add("$stem : no 1st-pass experiment sidecar to compare against at $pass1")
            continue
        }
        $r = [OspreyFdrSidecarComparer]::CheckPass2ProteinQ($pass1, $f.FullName)
        if (-not $r.Readable) {
            $issues.Add("$stem : $($r.Problem)")
            continue
        }
        if ($r.Matched -eq 0) {
            $issues.Add("$stem : no entry_id present in both passes - verified nothing")
            continue
        }
        $totalMatched += $r.Matched
        $totalDiffering += $r.Differing
        $totalDefault += $r.DifferingAtDefault
        $totalGapFill += $r.AbsentFromPass1
    }

    # Asserted at RUN level, not per file. The property - "the second-pass protein FDR reached
    # the sidecar" - is a property of the run, and PropagateProteinQvalues legitimately assigns
    # 1.0 to every entry whose ModifiedSequence is absent from PeptideQvalues in BOTH passes
    # (ProteinFdr.cs), so a file dominated by those produces bit-identical columns without
    # anything being wrong. Asserting per file reddened the whole dataset for that.
    if ($files.Count -gt 0 -and $totalMatched -gt 0) {
        if ($totalDiffering -eq 0) {
            $issues.Add((("experiment_protein_qvalue is identical to the 1st-pass column on all " +
                "{0} shared record(s) across {1} file(s) - the second-pass protein FDR result " +
                "never reached the 2nd-pass sidecar (issue #4559)") -f $totalMatched, $files.Count))
        }
        elseif ($totalDiffering -eq $totalDefault) {
            # Every record that moved, moved to the ResetScores default. That is what a run with
            # PatchPass2ProteinQvalues reverted or failing looks like, and it is NOT what a
            # populated pass-2 column looks like - so it must not read as liveness.
            $issues.Add((("all {0} differing record(s) hold the 1.0 reset default rather than a " +
                "computed second-pass value - the patch did not run, or wrote nothing (#4559)") `
                -f $totalDiffering))
        }
    }

    return @{
        Pass = ($issues.Count -eq 0)
        Issues = $issues
        Matched = $totalMatched
        Differing = $totalDiffering
        GapFill = $totalGapFill
    }
}
