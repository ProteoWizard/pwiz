/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Text;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.FDR.ModelDiagnostics
{
    public sealed partial class ModelDiagnosticsData
    {
        /// <summary>
        /// Single-peak multiple-ID co-assignment: how often a DETECTED precursor sits on a
        /// chromatographic peak that a better-scoring precursor of the same m/z already explains
        /// (issue #4522).
        ///
        /// <para>DIA search can assign two IDs to one chromatographic feature with no
        /// sequence-specific differentiating evidence, and nothing in the pipeline prevents it -
        /// every competition Osprey runs is WITHIN an identity (best candidate peak per precursor,
        /// target against its own decoy, charge states of one modified sequence), never ACROSS
        /// identities at a peak. Normally the effect is unfalsifiable, because with two co-eluting
        /// same-m/z IDs there is no ground truth about which is real.</para>
        ///
        /// <para>Entrapment and decoys supply the missing label. Both are false by construction,
        /// so one of them sharing a peak with a better-scoring target is a DEMONSTRATED false
        /// co-assignment. The accepted-target rate is reported beside them as the base rate -
        /// targets co-assign with each other too (paralogs, isoforms, orthologs, I/L isobars) -
        /// and only the excess over that base rate is specific to the known-false classes.</para>
        ///
        /// <para><see cref="CoAssignmentClassRow.NBetter"/> is the number that answers the
        /// operational question: how much of the detected entrapment and decoy population would
        /// disappear if a best-match-wins rule were enforced on every peak claimed by two entries.
        /// See that member for the PTM caveat, which is why this is a diagnostic and not yet a
        /// filter.</para>
        ///
        /// <para>"Detected" is reported at BOTH q scopes the rest of the report uses -
        /// <see cref="Run"/> (run-level q) and <see cref="Experiment"/> (experiment-wide q) - at
        /// whichever pass this panel was built for, so it can be read against the per-file and
        /// cross-run tables without a scope mismatch.</para>
        ///
        /// <para>The test itself is pure geometry: same run, same apex RT, same precursor m/z. It
        /// needs no knowledge of the sequence relationship, so it finds co-assignment nobody
        /// thought to look for.</para>
        /// </summary>
        public sealed class CoAssignmentData
        {
            /// <summary>Precursor m/z match tolerance (Da) - the co-isolation half of the test.</summary>
            public double MzTolerance { get; set; }
            /// <summary>
            /// Apex RT tolerance (minutes) behind the headline rates. The whole
            /// <see cref="ToleranceLadder"/> is reported because the measured enrichment depends on
            /// this choice, so a single baked-in number would hide the sensitivity.
            /// </summary>
            public double RtTolerance { get; set; }
            /// <summary>1 = pre-compaction first-pass detection, 2 = final reported pool.</summary>
            public int Pass { get; set; }
            /// <summary>
            /// True when the pool this was built from is post-reconciliation (pass 2).
            ///
            /// <para>Read the two passes together; do NOT assume a direction. Stage 6 both adds
            /// co-assignment (multi-charge consensus moves disagreeing charge states onto the
            /// leader's peak, forced integration gap-fills at a consensus RT) and removes it
            /// (compaction drops the weaker of two competitors). Measured on the Stellar
            /// entrapment gate the net is REMOVAL - target 2.17% -&gt; 1.18%, entrapment 6.80%
            /// -&gt; 5.17% - while the ENRICHMENT rises, 3.14x -&gt; 4.37x, because the reported
            /// pool sheds co-assigned targets faster than co-assigned entrapment. So pass 2 is
            /// cleaner in absolute terms and worse in the ratio that matters. An earlier version
            /// of this comment asserted the rate would rise; it does not, and the enrichment
            /// delta is the quantity to watch.</para>
            /// </summary>
            public bool PostReconciliation { get; set; }

            /// <summary>Detection gated on the RUN-level q value.</summary>
            public CoAssignmentScope Run { get; set; }
            /// <summary>Detection gated on the EXPERIMENT-wide q value.</summary>
            public CoAssignmentScope Experiment { get; set; }

            /// <summary>
            /// Apex RT tolerances (minutes) the rates in
            /// <see cref="CoAssignmentClassRow.BetterByTolerance"/> are reported over, ascending.
            /// Makes the tolerance sensitivity visible instead of baking one choice in.
            /// </summary>
            public double[] ToleranceLadder { get; set; }
            /// <summary>Index into <see cref="ToleranceLadder"/> that <see cref="RtTolerance"/> sits at.</summary>
            public int HeadlineToleranceIndex { get; set; }
            /// <summary>
            /// Bin edges (minutes) of the |dRT| histograms, from 0 to the widest scanned window.
            /// The histogram separates same-feature apex jitter (a spike at 0) from chance
            /// co-elution (a flat background), which is what justifies a tolerance rather than
            /// leaving it an arbitrary knob.
            /// </summary>
            public double[] DeltaRtEdges { get; set; }
        }

        /// <summary>
        /// The co-assignment picture under ONE definition of "detected" (run-level or
        /// experiment-wide q). Mirrors the run / experiment split
        /// <see cref="CrossRunDetection"/> uses, so both cards answer at the same scope.
        /// </summary>
        public sealed class CoAssignmentScope
        {
            /// <summary>Detected real targets - the base rate every other class is read against.</summary>
            public CoAssignmentClassRow Target { get; set; }
            /// <summary>Detected entrapment (p_target), or null when the run carries no entrapment.</summary>
            public CoAssignmentClassRow Entrapment { get; set; }
            /// <summary>Detected decoys, or null when none were detected.</summary>
            public CoAssignmentClassRow Decoy { get; set; }

            /// <summary>
            /// Entrapment co-assignment rate divided by the target rate at the headline tolerance.
            /// Entrapment is absent by construction, so a ratio above 1 is co-assignment the search
            /// is demonstrably getting wrong. NaN with no entrapment or a zero base rate.
            /// </summary>
            public double Enrichment { get; set; }
            /// <summary>Decoy rate divided by the target rate - the same read with the decoy null.</summary>
            public double DecoyEnrichment { get; set; }

            /// <summary>|dRT| counts for co-assigned detected TARGET precursors.</summary>
            public int[] DeltaRtTarget { get; set; }
            /// <summary>|dRT| counts for co-assigned detected ENTRAPMENT precursors, or null with no entrapment.</summary>
            public int[] DeltaRtEntrapment { get; set; }

            /// <summary>
            /// The worst co-assigned pairs, ranked by score gap - what makes the panel actionable
            /// rather than merely informative. Deterministically ordered and capped, so the
            /// embedded JSON stays bounded at Astral scale.
            /// </summary>
            public List<CoAssignedPair> WorstOffenders { get; set; }
        }

        /// <summary>
        /// One measured class (target / entrapment / decoy) of a <see cref="CoAssignmentScope"/>.
        /// "Shared" counts a precursor whose peak another detected real target also sits on;
        /// <see cref="NBetter"/> restricts that to partners which OUTSCORE it.
        /// </summary>
        public sealed class CoAssignmentClassRow
        {
            /// <summary>Detected precursors of this class (the denominator).</summary>
            public int N { get; set; }
            /// <summary>Detected precursors sharing a peak with another detected real target.</summary>
            public int NShared { get; set; }
            /// <summary>
            /// Of those, the ones whose peak partner OUTSCORES them - i.e. this precursor is the
            /// redundant explanation of a peak a better-scoring target already accounts for.
            ///
            /// <para>On the entrapment and decoy rows this is the operational number: since both
            /// classes are false by construction, it is how much of the detected false population
            /// would disappear under a best-match-wins rule on peaks claimed by two entries. On the
            /// target row it is the same rule's cost, applied to IDs that may well be real.</para>
            ///
            /// <para><b>Caveat, and why this is a diagnostic rather than a filter.</b> Some
            /// co-assignments are genuine and must survive: isobaric PTM positional isomers - a
            /// phosphopeptide with the modification on either of two residues - really can elute at
            /// the same time at the same precursor m/z, and both are present. Best-match-wins would
            /// delete one of a real pair. Those need a distinguishing transition to quantify the two
            /// individually, not a winner. <see cref="NBetterSameBaseSequence"/> bounds that
            /// population, so the "would go away" number can be read net of it.</para>
            /// </summary>
            public int NBetter { get; set; }
            /// <summary>
            /// The subset of <see cref="NBetter"/> whose better-scoring partner has the SAME base
            /// sequence once modifications are stripped - the isobaric PTM positional-isomer
            /// population (see <see cref="NBetter"/>). These are the co-assignments most likely to
            /// be REAL, so subtracting them gives the conservative "would go away" count.
            /// </summary>
            public int NBetterSameBaseSequence { get; set; }
            /// <summary><see cref="NShared"/> / <see cref="N"/>; NaN when N is 0.</summary>
            public double SharedFraction { get; set; }
            /// <summary><see cref="NBetter"/> / <see cref="N"/>; NaN when N is 0. The headline rate.</summary>
            public double BetterFraction { get; set; }
            /// <summary>
            /// <see cref="BetterFraction"/> at each entry of
            /// <see cref="CoAssignmentData.ToleranceLadder"/>. Derived from one scan: every
            /// precursor's MINIMUM |dRT| to a better-scoring same-m/z partner is retained, so a
            /// rate at any tolerance is a threshold count over those minima.
            /// </summary>
            public double[] BetterByTolerance { get; set; }
        }

        /// <summary>
        /// One co-assigned pair for the worst-offenders listing: the detected precursor and the
        /// better-scoring detected target sitting on the same peak in the same run.
        /// </summary>
        public sealed class CoAssignedPair
        {
            /// <summary>Input file the pair was observed in.</summary>
            public string File { get; set; }
            public string ModifiedSequence { get; set; }
            public int Charge { get; set; }
            /// <summary>Target / PTarget / Decoy, as <see cref="EntrapmentClass"/>.</summary>
            public string Class { get; set; }
            public string PartnerModifiedSequence { get; set; }
            public int PartnerCharge { get; set; }
            public double PrecursorMz { get; set; }
            public double ApexRt { get; set; }
            /// <summary>Signed apex RT difference, partner minus this precursor (minutes).</summary>
            public double DeltaRt { get; set; }
            public double Score { get; set; }
            public double PartnerScore { get; set; }
            /// <summary>Partner score minus this precursor's score; always positive here.</summary>
            public double ScoreGap { get; set; }
            /// <summary>
            /// How many runs this same precursor pair is co-assigned in. One row per PAIR, not per
            /// observation: without this the listing spends its slots re-printing one pair once per
            /// file, and a pair that co-assigns in most runs (the most damning kind) looks the same
            /// as one that did it once. The other per-observation fields describe the single run
            /// with the largest score gap.
            /// </summary>
            public int Runs { get; set; }
            /// <summary>
            /// True when the two differ only in modification - same sequence once mods are
            /// stripped. Flags the isobaric PTM positional-isomer case, where BOTH IDs may be real
            /// and a best-match-wins rule would be wrong (see
            /// <see cref="CoAssignmentClassRow.NBetter"/>).
            /// </summary>
            public bool SameBaseSequence { get; set; }
        }

        /// <summary>
        /// One detected precursor observation in one run, reduced to what the co-assignment test
        /// reads. Both passes project onto this: pass 1 from the per-file
        /// <c>.1st-pass.fdr_scores.bin</c> sidecar joined to its <c>.scores.parquet</c> apex RT,
        /// pass 2 from the resident reported pool.
        /// </summary>
        public readonly struct CoAssignmentRow
        {
            /// <summary>Precursor identity, <c>modified sequence + "|" + charge</c>.</summary>
            public readonly string Key;
            public readonly string ModifiedSequence;
            public readonly byte Charge;
            /// <summary>Library precursor m/z - exact, so no residue-mass table is involved.</summary>
            public readonly double PrecursorMz;
            public readonly double ApexRt;
            public readonly double Score;
            public readonly EntrapmentClass Class;

            public CoAssignmentRow(string key, string modifiedSequence, byte charge,
                double precursorMz, double apexRt, double score, EntrapmentClass entrapmentClass)
            {
                Key = key;
                ModifiedSequence = modifiedSequence;
                Charge = charge;
                PrecursorMz = precursorMz;
                ApexRt = apexRt;
                Score = score;
                Class = entrapmentClass;
            }
        }

        /// <summary>
        /// The shared entrapment classification, for callers outside this assembly that project
        /// their own rows (the streaming pass-1 co-assignment source, which reads the FDR sidecar
        /// rather than an <see cref="FdrEntry"/> pool). Exposed rather than reimplemented so the
        /// panel cannot drift from the classification every other card on the page uses.
        /// </summary>
        public static EntrapmentClass ClassifyEntry(bool isDecoy, uint entryId,
            IReadOnlyDictionary<uint, EntrapmentClass> classByBaseId)
        {
            int nWithClass = 0, nWithoutClass = 0;
            return Classify(isDecoy, entryId & BASE_ID_MASK, classByBaseId,
                classByBaseId != null && classByBaseId.Count > 0, ref nWithClass, ref nWithoutClass);
        }

        /// <summary>
        /// Build the co-assignment panel from a RESIDENT per-file <see cref="FdrEntry"/> pool.
        /// This is the pass-2 path (<see cref="BuildPass2"/> over the reported pool, which carries
        /// a populated <see cref="FdrEntry.ApexRt"/>) and the resident pass-1 path
        /// (<see cref="Build"/>). The streaming pass-1 path cannot use this - it never holds the
        /// pool - and instead drives a <see cref="CoAssignmentPassBuilder"/> from the per-file FDR
        /// sidecar joined to its <c>.scores.parquet</c> apex RT.
        ///
        /// <para>Returns null when no precursor m/z could be resolved at all, which means the
        /// caller's library lookup is not wired up rather than that the run has no
        /// co-assignment.</para>
        /// </summary>
        /// <param name="perFileEntries">Per-file entry lists, scored and q-valued, in input order.</param>
        /// <param name="classByBaseId">Library base-id to entrapment class, as the rest of the report uses.</param>
        /// <param name="precursorMzByEntryId">
        /// Full entry id (decoy bit included) to library precursor m/z; NaN when unknown. Exact by
        /// construction - no residue-mass table is involved.
        /// </param>
        /// <param name="runFdr">The configured run-level FDR, applied at both q scopes.</param>
        /// <param name="fdrLevel">FDR control level deciding which q gates detection.</param>
        /// <param name="pass">1 = pre-compaction first-pass detection, 2 = final reported pool.</param>
        /// <param name="postReconciliation">True when the pool is post-Stage-6 (pass 2).</param>
        public static CoAssignmentData BuildCoAssignment(
            IReadOnlyList<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<uint, EntrapmentClass> classByBaseId,
            Func<uint, double> precursorMzByEntryId,
            double runFdr,
            FdrLevel fdrLevel,
            int pass,
            bool postReconciliation)
        {
            if (perFileEntries == null || precursorMzByEntryId == null)
                return null;
            bool haveManifest = classByBaseId != null && classByBaseId.Count > 0;
            var runNames = new string[perFileEntries.Count];
            for (int f = 0; f < perFileEntries.Count; f++)
                runNames[f] = perFileEntries[f].Key;

            var builder = new CoAssignmentPassBuilder(runNames, pass, postReconciliation);
            bool anyMz = false;
            for (int f = 0; f < perFileEntries.Count; f++)
            {
                foreach (var e in perFileEntries[f].Value)
                {
                    // Gate on detection BEFORE building the row: the key is a fresh string per
                    // row, and pass 1 hands this method the whole pre-compaction pool.
                    double runQ = e.EffectiveRunQvalue(fdrLevel);
                    double expQ = e.EffectiveExperimentQvalue(fdrLevel);
                    if (runQ > runFdr && expQ > runFdr)
                        continue;
                    double mz = precursorMzByEntryId(e.EntryId);
                    if (double.IsNaN(mz) || mz <= 0)
                        continue;
                    anyMz = true;
                    int wc = 0, woc = 0;
                    var cls = Classify(e.IsDecoy, e.EntryId & BASE_ID_MASK, classByBaseId,
                        haveManifest, ref wc, ref woc);
                    builder.AddRow(f,
                        new CoAssignmentRow(e.ModifiedSequence + "|" + e.Charge, e.ModifiedSequence,
                            e.Charge, mz, e.ApexRt, e.Score, cls),
                        e.EffectiveRunQvalue(fdrLevel), e.EffectiveExperimentQvalue(fdrLevel), runFdr);
                }
                builder.FlushFile();
            }
            return anyMz ? builder.Build() : null;
        }

        /// <summary>
        /// Assembles one pass's <see cref="CoAssignmentData"/> by driving a
        /// <see cref="CoAssignmentAccumulator"/> per q scope, so both definitions of "detected" -
        /// run-level and experiment-wide - come out of a single walk over the rows. That matters
        /// most on the streaming pass-1 path, where the walk is a re-read of every
        /// <c>.scores.parquet</c> and doing it twice would double the cost.
        /// </summary>
        public sealed class CoAssignmentPassBuilder
        {
            private readonly CoAssignmentAccumulator _run;
            private readonly CoAssignmentAccumulator _experiment;
            private readonly int _pass;
            private readonly bool _postReconciliation;
            private bool _any;

            public CoAssignmentPassBuilder(string[] runNames, int pass, bool postReconciliation)
            {
                _run = new CoAssignmentAccumulator(runNames);
                _experiment = new CoAssignmentAccumulator(runNames);
                _pass = pass;
                _postReconciliation = postReconciliation;
            }

            /// <summary>
            /// Offer one observation to both scopes; each keeps it only if its own q clears
            /// <paramref name="runFdr"/>. Rows must arrive file-major, each run closed with
            /// <see cref="FlushFile"/>.
            /// </summary>
            public void AddRow(int fileIdx, in CoAssignmentRow row, double runQvalue,
                double experimentQvalue, double runFdr)
            {
                if (runQvalue <= runFdr)
                {
                    _run.AddDetectedRow(fileIdx, row);
                    _any = true;
                }
                if (experimentQvalue <= runFdr)
                {
                    _experiment.AddDetectedRow(fileIdx, row);
                    _any = true;
                }
            }

            /// <summary>Close the current run in both scopes.</summary>
            public void FlushFile()
            {
                _run.FlushFile();
                _experiment.FlushFile();
            }

            /// <summary>Assemble the panel, or null when no row was ever detected at either scope.</summary>
            public CoAssignmentData Build()
            {
                if (!_any)
                    return null;
                return new CoAssignmentData
                {
                    MzTolerance = CoAssignmentAccumulator.MZ_TOLERANCE,
                    RtTolerance = CoAssignmentAccumulator.RT_TOLERANCE,
                    Pass = _pass,
                    PostReconciliation = _postReconciliation,
                    ToleranceLadder = CoAssignmentAccumulator.TOLERANCE_LADDER,
                    HeadlineToleranceIndex = CoAssignmentAccumulator.HeadlineToleranceIndex(),
                    DeltaRtEdges = CoAssignmentAccumulator.BuildDeltaRtEdges(),
                    Run = _run.Build(),
                    Experiment = _experiment.Build(),
                };
            }
        }

        /// <summary>
        /// Builds one <see cref="CoAssignmentScope"/> from detected precursor observations fed one
        /// run at a time, so neither pass ever holds more than a single file's detected rows.
        ///
        /// <para>Rows arrive file-major; <see cref="FlushFile"/> closes each run, which is where
        /// the work happens: reduce to one row per precursor, sort by m/z, and for every detected
        /// row find the same-m/z partners it shares an apex with. What survives the file is
        /// per-PRECURSOR (not per-observation): the minimum |dRT| to any qualifying partner across
        /// all runs. A precursor co-assigned in any run counts once, matching the prototype, and
        /// retaining the minimum rather than a flag is what lets
        /// <see cref="CoAssignmentClassRow.BetterByTolerance"/> report the whole tolerance ladder
        /// from a single scan.</para>
        /// </summary>
        public sealed class CoAssignmentAccumulator
        {
            /// <summary>
            /// Precursor m/z match tolerance (Da). Deliberately m/z and NOT the neutral peptide
            /// mass the prototype used: co-isolation is an m/z-window property, and two charge
            /// states of ONE peptide have identical neutral mass and identical apex RT, so a
            /// neutral-mass test matches them trivially and has to be patched with a
            /// same-sequence exclusion that then throws away real co-assignments.
            /// </summary>
            public const double MZ_TOLERANCE = 0.01;

            /// <summary>
            /// Apex RT tolerance (minutes) behind the headline rates. Physically motivated: it
            /// matches the same-feature apex jitter measured on co-assigned pairs (median 0.046
            /// min). It also maximises the measured enrichment, which is why the whole
            /// <see cref="TOLERANCE_LADDER"/> and the |dRT| histogram are reported beside it.
            /// </summary>
            public const double RT_TOLERANCE = 0.05;

            /// <summary>
            /// Widest apex RT difference scanned. Everything inside this window is binned into the
            /// histogram and folded into the per-precursor minima, so every rate on the ladder
            /// comes out of one pass. Also the histogram's upper edge.
            /// </summary>
            public const double SCAN_RT_WINDOW = 0.25;

            /// <summary>Apex RT tolerances the rates are reported over. Ascending; none exceeds <see cref="SCAN_RT_WINDOW"/>.</summary>
            public static readonly double[] TOLERANCE_LADDER = { 0.01, 0.02, 0.05, 0.10, 0.25 };

            private const int DELTA_RT_BINS = 50;
            private const int MAX_OFFENDERS = 50;

            /// <summary>
            /// Fewest detected precursors a class needs before its enrichment ratio is reported.
            /// Below this the ratio is arithmetic noise dressed as a finding: a measured Stellar
            /// run accepted 7 decoys at experiment-wide q, and 1 of 7 rendered as a 6.4x
            /// enrichment, which reads exactly like the 5.7x that took a 40-file cohort to
            /// establish. Set below the ~95-144 entrapment precursors the real entrapment arms
            /// carry, so it never suppresses a number that means something.
            /// </summary>
            public const int MIN_N_FOR_ENRICHMENT = 30;

            private readonly string[] _runNames;

            // Current run's detected rows, reduced to one per precursor (max score). Cleared at
            // every FlushFile, so only a single run is ever resident.
            private readonly List<CoAssignmentRow> _fileRows = new List<CoAssignmentRow>();
            private readonly Dictionary<string, int> _fileRowByKey =
                new Dictionary<string, int>(StringComparer.Ordinal);
            private int _fileIdx = -1;

            // Per-precursor result carried across runs: class, and the minimum |dRT| to a
            // same-m/z partner (any / better-scoring). NaN means "never matched".
            private readonly Dictionary<string, PrecursorCoAssignment> _byPrecursor =
                new Dictionary<string, PrecursorCoAssignment>(StringComparer.Ordinal);

            private readonly int[] _deltaRtTarget = new int[DELTA_RT_BINS];
            private readonly int[] _deltaRtEntrapment = new int[DELTA_RT_BINS];
            private bool _anyEntrapment;
            private bool _anyDecoy;

            // Offender candidates keyed by precursor pair, so a pair co-assigned in many runs is
            // one row carrying a run count. Bounded by the number of DISTINCT co-assigned pairs
            // (thousands at Astral scale, not the tens of thousands of observations), so it needs
            // no mid-accumulation trimming - which also keeps the run counts exact.
            private readonly Dictionary<string, CoAssignedPair> _offendersByPair =
                new Dictionary<string, CoAssignedPair>(StringComparer.Ordinal);

            /// <param name="runNames">Input-file names in input order; indexes <see cref="AddDetectedRow"/>.</param>
            public CoAssignmentAccumulator(string[] runNames)
            {
                _runNames = runNames ?? throw new ArgumentNullException(nameof(runNames));
            }

            /// <summary>
            /// Fold one DETECTED precursor observation from run <paramref name="fileIdx"/> into the
            /// current run's buffer, keeping the best-scoring observation per precursor. Callers
            /// gate detection themselves (the q scope this accumulator represents), and must feed
            /// runs in file-major order, closing each with <see cref="FlushFile"/>.
            /// </summary>
            public void AddDetectedRow(int fileIdx, in CoAssignmentRow row)
            {
                if (fileIdx != _fileIdx)
                {
                    // A new run started without its predecessor being closed would silently
                    // compare apex RTs across runs, which is not a shared peak at all.
                    if (_fileRows.Count > 0)
                    {
                        throw new InvalidOperationException(
                            $@"CoAssignmentAccumulator: run {_fileIdx} was not flushed before run {fileIdx}");
                    }
                    _fileIdx = fileIdx;
                }
                if (_fileRowByKey.TryGetValue(row.Key, out int existing))
                {
                    // Pass 1 is pre-compaction, so one precursor has several rows per run (one per
                    // candidate peak / scan). The reported peak is the best-scoring one.
                    if (row.Score > _fileRows[existing].Score)
                        _fileRows[existing] = row;
                    return;
                }
                _fileRowByKey[row.Key] = _fileRows.Count;
                _fileRows.Add(row);
            }

            /// <summary>
            /// Close the current run: find every co-assigned pair within it and fold the results
            /// into the cross-run state, then release the run's rows. Cheap on an empty run.
            /// </summary>
            public void FlushFile()
            {
                if (_fileRows.Count == 0)
                {
                    _fileIdx = -1;
                    return;
                }
                ScanCurrentFile();
                _fileRows.Clear();
                _fileRowByKey.Clear();
                _fileIdx = -1;
            }

            /// <summary>
            /// Assemble this scope. Flushes any run left open, so a caller that fed the last run
            /// without closing it still gets that run counted.
            /// </summary>
            public CoAssignmentScope Build()
            {
                FlushFile();

                var target = new CoAssignmentClassRow();
                var entrapment = new CoAssignmentClassRow();
                var decoy = new CoAssignmentClassRow();
                var targetMinima = new List<double>();
                var entrapmentMinima = new List<double>();
                var decoyMinima = new List<double>();
                foreach (var p in _byPrecursor.Values)
                {
                    switch (p.Class)
                    {
                        case EntrapmentClass.Target:
                            Tally(target, targetMinima, p);
                            break;
                        case EntrapmentClass.PTarget:
                            Tally(entrapment, entrapmentMinima, p);
                            break;
                        case EntrapmentClass.Decoy:
                        case EntrapmentClass.PDecoy:
                            Tally(decoy, decoyMinima, p);
                            break;
                    }
                }
                Finish(target, targetMinima);
                Finish(entrapment, entrapmentMinima);
                Finish(decoy, decoyMinima);

                var scope = new CoAssignmentScope
                {
                    Target = target,
                    Entrapment = _anyEntrapment ? entrapment : null,
                    Decoy = _anyDecoy ? decoy : null,
                    Enrichment = double.NaN,
                    DecoyEnrichment = double.NaN,
                    DeltaRtTarget = _deltaRtTarget,
                    DeltaRtEntrapment = _anyEntrapment ? _deltaRtEntrapment : null,
                    WorstOffenders = RankOffenders(),
                };
                // Both denominators have to carry enough precursors for a ratio to mean anything,
                // the numerator class especially - see MIN_N_FOR_ENRICHMENT.
                if (target.BetterFraction > 0 && target.N >= MIN_N_FOR_ENRICHMENT)
                {
                    if (_anyEntrapment && entrapment.N >= MIN_N_FOR_ENRICHMENT)
                        scope.Enrichment = entrapment.BetterFraction / target.BetterFraction;
                    if (_anyDecoy && decoy.N >= MIN_N_FOR_ENRICHMENT)
                        scope.DecoyEnrichment = decoy.BetterFraction / target.BetterFraction;
                }
                return scope;
            }

            /// <summary>
            /// Index of the ladder entry the headline tolerance sits at, or the nearest one below
            /// it.
            /// </summary>
            public static int HeadlineToleranceIndex()
            {
                int index = -1;
                for (int i = 0; i < TOLERANCE_LADDER.Length; i++)
                {
                    if (TOLERANCE_LADDER[i] <= RT_TOLERANCE)
                        index = i;
                }
                return index;
            }

            /// <summary>Bin edges (minutes) of the |dRT| histogram, 0 to <see cref="SCAN_RT_WINDOW"/>.</summary>
            public static double[] BuildDeltaRtEdges()
            {
                var edges = new double[DELTA_RT_BINS + 1];
                for (int i = 0; i <= DELTA_RT_BINS; i++)
                    edges[i] = SCAN_RT_WINDOW * i / DELTA_RT_BINS;
                return edges;
            }

            /// <summary>
            /// The sequence with every bracketed modification removed, e.g.
            /// <c>PEPT[+80]IDE</c> to <c>PEPTIDE</c>. Two co-assigned precursors sharing a base
            /// sequence are the isobaric PTM positional-isomer case, where both IDs may be real
            /// (see <see cref="CoAssignmentClassRow.NBetter"/>).
            /// </summary>
            public static string StripModifications(string modifiedSequence)
            {
                if (string.IsNullOrEmpty(modifiedSequence))
                    return modifiedSequence;
                if (modifiedSequence.IndexOf('[') < 0)
                    return modifiedSequence;
                var sb = new StringBuilder(modifiedSequence.Length);
                int depth = 0;
                foreach (char c in modifiedSequence)
                {
                    if (c == '[')
                        depth++;
                    else if (c == ']')
                    {
                        if (depth > 0)
                            depth--;
                    }
                    else if (depth == 0)
                        sb.Append(c);
                }
                return sb.ToString();
            }

            // Per-precursor state carried across runs. MinDeltaRt* is the smallest apex RT
            // difference to a qualifying partner seen in ANY run; retaining the minimum rather
            // than a boolean is what makes every tolerance on the ladder a threshold count.
            private struct PrecursorCoAssignment
            {
                public EntrapmentClass Class;
                public double MinDeltaRtShared;
                public double MinDeltaRtBetter;
                public bool BetterIsSameBaseSequence;
            }

            // Find the co-assigned pairs within the current run. Sorting by m/z makes the
            // co-isolation test a binary-searched window: at Astral density (~50k detected rows
            // over ~800 m/z) a +/-0.01 window holds ~1 candidate, so the RT test runs on almost
            // nothing. The partner pool is detected REAL TARGETS only - the question being asked
            // is "does a target already explain this peak", and letting entrapment or decoys be
            // partners would count deliberately-false padding as an explanation.
            private void ScanCurrentFile()
            {
                // Register EVERY detected precursor first. These are the denominators - the rates
                // are "of all detected precursors, how many are co-assigned" - so a precursor that
                // matches nothing still has to be counted. Folding only matched precursors would
                // make every rate 100% by construction.
                foreach (var row in _fileRows)
                {
                    if (_byPrecursor.ContainsKey(row.Key))
                        continue;
                    _byPrecursor.Add(row.Key, new PrecursorCoAssignment
                    {
                        Class = row.Class,
                        MinDeltaRtShared = double.NaN,
                        MinDeltaRtBetter = double.NaN,
                    });
                    if (row.Class == EntrapmentClass.PTarget)
                        _anyEntrapment = true;
                    else if (row.Class == EntrapmentClass.Decoy || row.Class == EntrapmentClass.PDecoy)
                        _anyDecoy = true;
                }

                var byMz = _fileRows.ToArray();
                Array.Sort(byMz, CompareByMz); // Array.Sort OK: CompareByMz falls through to the precursor key, which this file's rows were deduplicated on, so distinct elements never compare equal and there are no ties to reorder

                foreach (var row in _fileRows)
                {
                    int lo = LowerBoundMz(byMz, row.PrecursorMz - MZ_TOLERANCE);
                    for (int i = lo; i < byMz.Length; i++)
                    {
                        var partner = byMz[i];
                        if (partner.PrecursorMz > row.PrecursorMz + MZ_TOLERANCE)
                            break;
                        // A precursor cannot be its own peak partner. Charge states of one peptide
                        // are excluded by m/z, not by sequence, so no sequence-level exclusion is
                        // needed or wanted here.
                        if (string.Equals(partner.Key, row.Key, StringComparison.Ordinal))
                            continue;
                        if (partner.Class != EntrapmentClass.Target)
                            continue;
                        double deltaRt = partner.ApexRt - row.ApexRt;
                        double absDeltaRt = Math.Abs(deltaRt);
                        if (absDeltaRt > SCAN_RT_WINDOW)
                            continue;
                        FoldPair(row, partner, deltaRt, absDeltaRt);
                    }
                }
            }

            // Fold one co-assigned pair into the per-precursor minima, the |dRT| histogram and the
            // offender candidates.
            private void FoldPair(in CoAssignmentRow row, in CoAssignmentRow partner,
                double deltaRt, double absDeltaRt)
            {
                var state = _byPrecursor[row.Key];
                if (double.IsNaN(state.MinDeltaRtShared) || absDeltaRt < state.MinDeltaRtShared)
                    state.MinDeltaRtShared = absDeltaRt;
                bool better = partner.Score > row.Score;
                bool sameBase = better && string.Equals(
                    StripModifications(row.ModifiedSequence),
                    StripModifications(partner.ModifiedSequence), StringComparison.Ordinal);
                if (better && (double.IsNaN(state.MinDeltaRtBetter) || absDeltaRt < state.MinDeltaRtBetter))
                {
                    state.MinDeltaRtBetter = absDeltaRt;
                    // Carried from the NEAREST better partner, which is the one a best-match-wins
                    // rule would actually be adjudicating against.
                    state.BetterIsSameBaseSequence = sameBase;
                }
                _byPrecursor[row.Key] = state;

                // Histogram over the full scanned window, not the headline tolerance: truncating
                // it at the tolerance would hide the very shape (jitter spike vs flat chance
                // background) that justifies the tolerance.
                int bin = (int)(absDeltaRt / SCAN_RT_WINDOW * DELTA_RT_BINS);
                if (bin >= DELTA_RT_BINS)
                    bin = DELTA_RT_BINS - 1;
                if (row.Class == EntrapmentClass.Target)
                    _deltaRtTarget[bin]++;
                else if (row.Class == EntrapmentClass.PTarget)
                    _deltaRtEntrapment[bin]++;

                // Offender candidates are gathered at the HEADLINE tolerance: the listing is the
                // actionable "look at these" view, so it should not fill up with chance
                // co-elutions from the wide scan window.
                if (better && absDeltaRt <= RT_TOLERANCE)
                {
                    // One entry per PRECURSOR PAIR across all runs, not one per observation. The
                    // same pair co-assigning in 31 of 40 runs is one finding, and printing it 31
                    // times would crowd every other pair out of the listing.
                    string pairKey = row.Key + "" + partner.Key;
                    if (_offendersByPair.TryGetValue(pairKey, out var seen))
                    {
                        seen.Runs++;
                        // Keep the worst single observation's detail beside the run count.
                        if (partner.Score - row.Score > seen.ScoreGap)
                            FillObservation(seen, row, partner, deltaRt);
                        return;
                    }
                    var pair = new CoAssignedPair
                    {
                        ModifiedSequence = row.ModifiedSequence,
                        Charge = row.Charge,
                        Class = row.Class.ToString(),
                        PartnerModifiedSequence = partner.ModifiedSequence,
                        PartnerCharge = partner.Charge,
                        PrecursorMz = row.PrecursorMz,
                        SameBaseSequence = sameBase,
                        Runs = 1,
                    };
                    FillObservation(pair, row, partner, deltaRt);
                    _offendersByPair.Add(pairKey, pair);
                }
            }

            // Copy the per-observation half of a pair row (which run, which peak, what gap).
            private void FillObservation(CoAssignedPair pair, in CoAssignmentRow row,
                in CoAssignmentRow partner, double deltaRt)
            {
                pair.File = _fileIdx >= 0 && _fileIdx < _runNames.Length ? _runNames[_fileIdx] : null;
                pair.ApexRt = row.ApexRt;
                pair.DeltaRt = deltaRt;
                pair.Score = row.Score;
                pair.PartnerScore = partner.Score;
                pair.ScoreGap = partner.Score - row.Score;
            }

            // Count one precursor into its class row, retaining its better-partner minimum for the
            // tolerance ladder.
            private static void Tally(CoAssignmentClassRow row, List<double> betterMinima,
                in PrecursorCoAssignment p)
            {
                row.N++;
                if (!double.IsNaN(p.MinDeltaRtShared) && p.MinDeltaRtShared <= RT_TOLERANCE)
                    row.NShared++;
                if (double.IsNaN(p.MinDeltaRtBetter))
                    return;
                betterMinima.Add(p.MinDeltaRtBetter);
                if (p.MinDeltaRtBetter > RT_TOLERANCE)
                    return;
                row.NBetter++;
                if (p.BetterIsSameBaseSequence)
                    row.NBetterSameBaseSequence++;
            }

            // Turn the counts into rates, and the retained minima into the whole tolerance ladder.
            private static void Finish(CoAssignmentClassRow row, List<double> betterMinima)
            {
                row.BetterByTolerance = new double[TOLERANCE_LADDER.Length];
                if (row.N == 0)
                {
                    row.SharedFraction = double.NaN;
                    row.BetterFraction = double.NaN;
                    for (int i = 0; i < row.BetterByTolerance.Length; i++)
                        row.BetterByTolerance[i] = double.NaN;
                    return;
                }
                row.SharedFraction = (double)row.NShared / row.N;
                row.BetterFraction = (double)row.NBetter / row.N;
                betterMinima.Sort(); // Array.Sort OK: a plain double list sorted purely to binary-search it for a threshold count; equal doubles are indistinguishable so tie order cannot reach any output
                for (int i = 0; i < TOLERANCE_LADDER.Length; i++)
                    row.BetterByTolerance[i] = (double)CountAtMost(betterMinima, TOLERANCE_LADDER[i]) / row.N;
            }

            // Number of sorted minima at or below tolerance (upper bound by binary search).
            private static int CountAtMost(List<double> sortedMinima, double tolerance)
            {
                int lo = 0, hi = sortedMinima.Count;
                while (lo < hi)
                {
                    int mid = lo + (hi - lo) / 2;
                    if (sortedMinima[mid] <= tolerance)
                        lo = mid + 1;
                    else
                        hi = mid;
                }
                return lo;
            }

            // Rank the offender candidates by score gap and cap the listing. The comparison chain
            // ends on (partner charge, file), which makes it a total order over distinct pairs, so
            // the embedded JSON does not churn between otherwise identical runs.
            private List<CoAssignedPair> RankOffenders()
            {
                var ranked = new List<CoAssignedPair>(_offendersByPair.Values);
                ranked.Sort(CompareOffenders); // Array.Sort OK: CompareOffenders is a total order over distinct pairs, and the dictionary holds one entry per pair, so there are no ties to reorder
                if (ranked.Count > MAX_OFFENDERS)
                    ranked.RemoveRange(MAX_OFFENDERS, ranked.Count - MAX_OFFENDERS);
                return ranked;
            }

            private static int CompareOffenders(CoAssignedPair a, CoAssignedPair b)
            {
                int c = b.ScoreGap.CompareTo(a.ScoreGap);
                if (c != 0)
                    return c;
                c = string.CompareOrdinal(a.ModifiedSequence, b.ModifiedSequence);
                if (c != 0)
                    return c;
                c = a.Charge.CompareTo(b.Charge);
                if (c != 0)
                    return c;
                c = string.CompareOrdinal(a.PartnerModifiedSequence, b.PartnerModifiedSequence);
                if (c != 0)
                    return c;
                c = a.PartnerCharge.CompareTo(b.PartnerCharge);
                if (c != 0)
                    return c;
                return string.CompareOrdinal(a.File, b.File);
            }

            private static int CompareByMz(CoAssignmentRow a, CoAssignmentRow b)
            {
                int c = a.PrecursorMz.CompareTo(b.PrecursorMz);
                if (c != 0)
                    return c;
                // Total order so the scan is reproducible: distinct precursors can tie on m/z.
                c = string.CompareOrdinal(a.Key, b.Key);
                if (c != 0)
                    return c;
                return a.ApexRt.CompareTo(b.ApexRt);
            }

            // First index whose m/z is at or above minMz.
            private static int LowerBoundMz(CoAssignmentRow[] sorted, double minMz)
            {
                int lo = 0, hi = sorted.Length;
                while (lo < hi)
                {
                    int mid = lo + (hi - lo) / 2;
                    if (sorted[mid].PrecursorMz < minMz)
                        lo = mid + 1;
                    else
                        hi = mid;
                }
                return lo;
            }
        }
    }
}
