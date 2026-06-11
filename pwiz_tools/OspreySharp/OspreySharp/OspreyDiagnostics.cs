/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
using System.Globalization;
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.FDR;
using pwiz.OspreySharp.FDR.Reconciliation;
using pwiz.OspreySharp.Scoring;

namespace pwiz.OspreySharp
{
    /// <summary>
    /// Call-site facade for the cross-implementation bisection diagnostics.
    /// All production code reaches the dumps through this static surface
    /// (<c>OspreyDiagnostics.WriteX(...)</c>, <c>OspreyDiagnostics.DumpX</c>),
    /// which delegates to a single swappable <see cref="OspreyFileDiagnostics"/>
    /// sink. The sink is created only when diagnostics are enabled (a
    /// OSPREY_DUMP_* / OSPREY_DIAG_* env var is set, or <c>-d</c> was passed);
    /// otherwise <see cref="s_sink"/> is <c>null</c> and every call here is a
    /// no-op, so a production run carries no diagnostic state or writers. This
    /// is the "no-op default, replaced by dependency injection" posture: the
    /// driver calls <see cref="Initialize"/> once at pipeline entry to select
    /// the live sink.
    ///
    /// Pure helpers that are not part of the dump surface stay here: the
    /// <see cref="F10"/> formatter, the <see cref="LogAction"/> logging hook,
    /// and <see cref="ExitAfterDump"/>.
    /// </summary>
    public static class OspreyDiagnostics
    {
        // The live sink, or null when diagnostics are off (the no-op default).
        private static OspreyFileDiagnostics s_sink;
        private static bool s_initialized;

        // Contract: Initialize() runs once, single-threaded, at pipeline entry
        // (Program.Main) before any diagnostics call. We deliberately do NOT
        // lazily self-init here -- a lazy init first touched from the parallel
        // scoring loop would be a data race. Accessing the sink before
        // Initialize is a programming error (an entry point that forgot to call
        // it), so it is surfaced loudly rather than racily self-initialized.
        // Env-only bisection workflows still work: Initialize self-enables the
        // sink from the OSPREY_DUMP_* / OSPREY_DIAG_* env vars.
        private static OspreyFileDiagnostics Sink
        {
            get
            {
                if (!s_initialized)
                    throw new InvalidOperationException(
                        @"OspreyDiagnostics.Initialize must be called once at pipeline entry before any diagnostics call.");
                return s_sink;
            }
        }

        /// <summary>
        /// OSPREY_DUMP_* env vars turned on by the <c>-d</c> master switch.
        /// Excludes the per-call firehose (OSPREY_DUMP_MP_INPUTS), the disabled
        /// predict-rt dump, the *_ONLY early-exit gates, and the OSPREY_DIAG_*
        /// per-entry selectors (which need specific IDs). Fine-grained control
        /// is still available by setting individual env vars.
        /// </summary>
        private static readonly string[] s_forcedDumpBundle =
        {
            @"OSPREY_DUMP_CAL_SAMPLE",
            @"OSPREY_DUMP_CAL_WINDOWS",
            @"OSPREY_DUMP_CAL_MATCH",
            @"OSPREY_DUMP_MS2_CAL_ERRORS",
            @"OSPREY_DUMP_LDA_SCORES",
            @"OSPREY_DUMP_LOESS_INPUT",
            @"OSPREY_DUMP_PERCOLATOR",
            @"OSPREY_DUMP_RESCORED",
            @"OSPREY_DUMP_CWT_PATH",
            @"OSPREY_DUMP_CONSENSUS",
            @"OSPREY_DUMP_MULTICHARGE",
            @"OSPREY_DUMP_REFIT",
            @"OSPREY_DUMP_RECONCILIATION",
            @"OSPREY_DUMP_CALIBRATION",
            @"OSPREY_DUMP_INV_PREDICT",
            @"OSPREY_DUMP_PROTEIN_FDR",
            @"OSPREY_DUMP_STAGE7_PROTEIN_FDR",
            @"OSPREY_DUMP_DETECTED_PEPTIDES",
            @"OSPREY_DUMP_LOESS_FIT",
        };

        /// <summary>
        /// Select the live diagnostics sink. Call once at pipeline entry.
        /// When <paramref name="forceDumps"/> is <c>true</c> (the <c>-d</c>
        /// flag) the structured-dump env vars are turned on in-process first,
        /// so the sink picks them up the same way an external env var would.
        /// Otherwise the sink is created only if a diagnostic env var is already
        /// set, preserving existing env-var-driven bisection workflows. When
        /// nothing is enabled the sink stays <c>null</c> and the facade no-ops.
        /// </summary>
        public static void Initialize(bool forceDumps)
        {
            if (forceDumps)
            {
                foreach (string envVar in s_forcedDumpBundle)
                {
                    // Force-on: override "0" / unset / anything except already-"1".
                    if (Environment.GetEnvironmentVariable(envVar) != @"1")
                        Environment.SetEnvironmentVariable(envVar, @"1");
                }
            }
            var sink = new OspreyFileDiagnostics();
            s_sink = sink.AnyEnabled ? sink : null;
            s_initialized = true;   // publish s_sink before marking initialized
        }

        /// <summary>
        /// Delegate for logging. The pipeline hooks this to its LogInfo so dump
        /// messages flow through the standard logging channel.
        /// </summary>
        public static Action<string> LogAction { get; set; } = Console.WriteLine;

        /// <summary>
        /// Format a double with 10 decimal places using round-half-to-even
        /// (banker's) to match Rust's {:.10} formatter. .NET Framework's F10
        /// default is round-half-away-from-zero, which flips the last digit on
        /// exact .5 values. Pure helper, available regardless of whether
        /// diagnostics are enabled.
        /// </summary>
        public static string F10(double v)
        {
            return Math.Round(v, 10, MidpointRounding.ToEven)
                .ToString(@"F10", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Log the "aborting after dump" message for the given env var and call
        /// Environment.Exit(0). Used after *_ONLY dumps where the bisection diff
        /// is the only output we care about.
        /// </summary>
        public static void ExitAfterDump(string varName)
        {
            LogAction(string.Format(@"[BISECT] {0} set - aborting after dump", varName));
            Environment.Exit(0);
        }

        // ----- Env-var gate flags (delegated to the live sink; false/default when off) -----

        public static bool DumpCalSample => Sink?.DumpCalSample ?? false;
        public static bool CalSampleOnly => Sink?.CalSampleOnly ?? false;
        public static bool DumpCalWindows => Sink?.DumpCalWindows ?? false;
        public static bool CalWindowsOnly => Sink?.CalWindowsOnly ?? false;
        public static bool DumpCalMatch => Sink?.DumpCalMatch ?? false;
        public static bool CalMatchOnly => Sink?.CalMatchOnly ?? false;
        public static bool DumpMs2CalErrors => Sink?.DumpMs2CalErrors ?? false;
        public static bool Ms2CalErrorsOnly => Sink?.Ms2CalErrorsOnly ?? false;
        public static bool DumpLdaScores => Sink?.DumpLdaScores ?? false;
        public static bool LdaScoresOnly => Sink?.LdaScoresOnly ?? false;
        public static bool DumpLoessInput => Sink?.DumpLoessInput ?? false;
        public static bool LoessInputOnly => Sink?.LoessInputOnly ?? false;
        public static bool DumpPercolator => Sink?.DumpPercolator ?? false;
        public static bool PercolatorOnly => Sink?.PercolatorOnly ?? false;
        public static bool DumpRescored => Sink?.DumpRescored ?? false;
        public static bool RescoredOnly => Sink?.RescoredOnly ?? false;
        public static bool DumpMpInputs => Sink?.DumpMpInputs ?? false;
        public static bool DumpPredictRt => Sink?.DumpPredictRt ?? false;
        public static bool DumpCwtPath => Sink?.DumpCwtPath ?? false;
        public static bool DumpConsensus => Sink?.DumpConsensus ?? false;
        public static bool ConsensusOnly => Sink?.ConsensusOnly ?? false;
        public static bool DumpMulticharge => Sink?.DumpMulticharge ?? false;
        public static bool MultichargeOnly => Sink?.MultichargeOnly ?? false;
        public static bool DumpRefit => Sink?.DumpRefit ?? false;
        public static bool RefitOnly => Sink?.RefitOnly ?? false;
        public static bool DumpReconciliation => Sink?.DumpReconciliation ?? false;
        public static bool ReconciliationOnly => Sink?.ReconciliationOnly ?? false;
        public static bool DumpCalibration => Sink?.DumpCalibration ?? false;
        public static bool CalibrationOnly => Sink?.CalibrationOnly ?? false;
        public static bool DumpInvPredict => Sink?.DumpInvPredict ?? false;
        public static bool InvPredictOnly => Sink?.InvPredictOnly ?? false;
        public static bool DumpProteinFdr => Sink?.DumpProteinFdr ?? false;
        public static bool ProteinFdrOnly => Sink?.ProteinFdrOnly ?? false;
        public static bool DumpStage7ProteinFdr => Sink?.DumpStage7ProteinFdr ?? false;
        public static bool Stage7ProteinFdrOnly => Sink?.Stage7ProteinFdrOnly ?? false;
        public static bool DumpDetectedPeptides => Sink?.DumpDetectedPeptides ?? false;
        public static bool DumpLoessFit => Sink?.DumpLoessFit ?? false;
        public static bool LoessFitOnly => Sink?.LoessFitOnly ?? false;

        public static uint? DiagXicEntryId => Sink?.DiagXicEntryId;
        public static int DiagXicPass => Sink?.DiagXicPass ?? 1;
        public static HashSet<uint> DiagSearchEntryIds => Sink?.DiagSearchEntryIds;
        public static int? DiagMpScan => Sink?.DiagMpScan;

        public static bool CalWindowsCollecting => Sink?.CalWindowsCollecting ?? false;

        // ----- Dump methods (no-op when no sink) -----

        public static void WriteCalSampleDump(string fileName, IEnumerable<LibraryEntry> sampledEntries)
        {
            Sink?.WriteCalSampleDump(fileName, sampledEntries);
        }

        public static void WriteCalScalarsAndGridDump(
            IReadOnlyList<LibraryEntry> targets,
            IReadOnlyList<LibraryEntry> decoys,
            int binsPerAxis,
            double rtMin, double rtMax, double mzMin, double mzMax,
            double rtRange, double mzRange, double rtBinWidth, double mzBinWidth,
            int nOccupied, int perCell, ulong seed,
            List<int>[,] grid)
        {
            Sink?.WriteCalScalarsAndGridDump(targets, decoys, binsPerAxis,
                rtMin, rtMax, mzMin, mzMax, rtRange, mzRange, rtBinWidth, mzBinWidth,
                nOccupied, perCell, seed, grid);
        }

        public static void StartCalWindowCollection()
        {
            Sink?.StartCalWindowCollection();
        }

        public static void AddCalWindowRow(LibraryEntry entry, IsolationWindow iso,
            double expectedRt, double rtLo, double rtHi)
        {
            Sink?.AddCalWindowRow(entry, iso, expectedRt, rtLo, rtHi);
        }

        public static void WriteCalWindowsDump(int passNumber)
        {
            Sink?.WriteCalWindowsDump(passNumber);
        }

        public static void WriteCalMatchDump(int passNumber,
            IEnumerable<CalibrationMatch> matches,
            IEnumerable<LibraryEntry> sampledEntries,
            IDictionary<uint, KeyValuePair<double, double>> matchRts,
            IDictionary<uint, double> snrByEntryId)
        {
            Sink?.WriteCalMatchDump(passNumber, matches, sampledEntries, matchRts, snrByEntryId);
        }

        public static void WriteMs2CalErrorsDump(IEnumerable<CalibrationMatch> contributingMatches)
        {
            Sink?.WriteMs2CalErrorsDump(contributingMatches);
        }

        public static void WriteLdaScoresDump(int passNumber, IEnumerable<CalibrationMatch> matchArray)
        {
            Sink?.WriteLdaScoresDump(passNumber, matchArray);
        }

        public static void WriteLoessInputDump(int passNumber, double[] libRts, double[] measuredRts)
        {
            Sink?.WriteLoessInputDump(passNumber, libRts, measuredRts);
        }

        public static void WriteCalibrationSummary(
            RTCalibration rtCal,
            MzCalibrationResult ms1Cal,
            MzCalibrationResult ms2Cal)
        {
            Sink?.WriteCalibrationSummary(rtCal, ms1Cal, ms2Cal);
        }

        public static bool ShouldDumpCalXicFor(uint entryId, int currentPass)
        {
            return Sink?.ShouldDumpCalXicFor(entryId, currentPass) ?? false;
        }

        public static void WriteCalXicEntryDumpAndExit(LibraryEntry entry, int currentPass,
            RTCalibration calibrationModel, double expectedRt, double initialTolerance,
            double rtSlope, double rtIntercept,
            IReadOnlyList<Spectrum> candidateSpectra,
            IReadOnlyList<XicData> xics)
        {
            Sink?.WriteCalXicEntryDumpAndExit(entry, currentPass, calibrationModel,
                expectedRt, initialTolerance, rtSlope, rtIntercept, candidateSpectra, xics);
        }

        public static bool ShouldDumpSearchXicFor(uint entryId)
        {
            return Sink?.ShouldDumpSearchXicFor(entryId) ?? false;
        }

        public static void WriteSearchXicDump(LibraryEntry candidate,
            double expectedRt, double rtTolerance,
            int startScan, int endScan, int rangeLen,
            IReadOnlyList<Spectrum> windowSpectra,
            IReadOnlyList<XicData> xics)
        {
            Sink?.WriteSearchXicDump(candidate, expectedRt, rtTolerance,
                startScan, endScan, rangeLen, windowSpectra, xics);
        }

        public static bool ShouldDumpMpFor(uint apexScanNumber, string candidateModifiedSequence)
        {
            return Sink?.ShouldDumpMpFor(apexScanNumber, candidateModifiedSequence) ?? false;
        }

        public static void WriteMpDump(LibraryEntry candidate, uint apexScanNumber,
            XICPeakBounds bestPeak, int peakLen,
            double mpCosine, double mpResidualRatio, double mpMinFragmentR2, double mpResidualCorr,
            TukeyMedianPolishResult polish,
            IReadOnlyList<KeyValuePair<int, double[]>> peakXics)
        {
            Sink?.WriteMpDump(candidate, apexScanNumber, bestPeak, peakLen,
                mpCosine, mpResidualRatio, mpMinFragmentR2, mpResidualCorr, polish, peakXics);
        }

        public static void WriteMpInputsRow(uint entryId, uint apexScan,
            IList<KeyValuePair<int, double[]>> peakXics, double[] peakRts)
        {
            Sink?.WriteMpInputsRow(entryId, apexScan, peakXics, peakRts);
        }

        public static void CloseMpInputsDump()
        {
            Sink?.CloseMpInputsDump();
        }

        public static void WritePredictRtArrays(string fileName, double[] libraryRts, double[] fittedValues)
        {
            Sink?.WritePredictRtArrays(fileName, libraryRts, fittedValues);
        }

        public static void WritePredictRtCall(uint entryId, double libraryRt, double expectedRt)
        {
            Sink?.WritePredictRtCall(entryId, libraryRt, expectedRt);
        }

        public static void WriteCwtPathRow(string fileName, uint entryId,
            int nCwtPeaks, int nFinalPeaks, int nScored, bool scored,
            List<XicData> xics)
        {
            Sink?.WriteCwtPathRow(fileName, entryId, nCwtPeaks, nFinalPeaks, nScored, scored, xics);
        }

        public static void CloseCwtPathDump()
        {
            Sink?.CloseCwtPathDump();
        }

        public static void ClosePredictRtDump()
        {
            Sink?.ClosePredictRtDump();
        }

        public static void WriteStage5PercolatorDump(List<KeyValuePair<string, List<FdrEntry>>> perFileEntries)
        {
            Sink?.WriteStage5PercolatorDump(perFileEntries);
        }

        public static void WriteStage6RescoredDump(List<KeyValuePair<string, List<FdrEntry>>> perFileEntries)
        {
            Sink?.WriteStage6RescoredDump(perFileEntries);
        }

        public static void WriteStage6ConsensusDump(IReadOnlyList<PeptideConsensusRT> consensus)
        {
            Sink?.WriteStage6ConsensusDump(consensus);
        }

        public static void WriteStage6MultichargeDump(
            IReadOnlyList<KeyValuePair<string, IReadOnlyList<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, IReadOnlyList<(int Index, double Apex, double Start, double End)>> perFileTargets)
        {
            Sink?.WriteStage6MultichargeDump(perFileEntries, perFileTargets);
        }

        public static void WriteStage6RefitDump(
            IReadOnlyDictionary<string, RTCalibration> refinedCalibrations)
        {
            Sink?.WriteStage6RefitDump(refinedCalibrations);
        }

        public static void WriteStage6ReconciliationDump(
            IReadOnlyDictionary<(string File, int Index), ReconcileAction> actions,
            IReadOnlyList<KeyValuePair<string, IReadOnlyList<FdrEntry>>> perFileEntries)
        {
            Sink?.WriteStage6ReconciliationDump(actions, perFileEntries);
        }

        public static void WriteStage6CalibrationDump(
            string fileName, double[] libraryRts, double[] fittedValues)
        {
            Sink?.WriteStage6CalibrationDump(fileName, libraryRts, fittedValues);
        }

        public static void WriteStage6InvPredictDump(IList<InvPredictRecord> records)
        {
            Sink?.WriteStage6InvPredictDump(records);
        }

        public static void WriteStage6ProteinFdrDump(
            IDictionary<string, PeptideScore> bestScores,
            IDictionary<string, double> peptideQvalues)
        {
            Sink?.WriteStage6ProteinFdrDump(bestScores, peptideQvalues);
        }

        public static void WriteStage7ProteinFdrDump(
            ProteinParsimonyResult parsimony,
            ProteinFdrResult fdrResult)
        {
            Sink?.WriteStage7ProteinFdrDump(parsimony, fdrResult);
        }

        public static void WriteStage6LoessFitDump(
            IReadOnlyDictionary<string, RTCalibration> refinedCalibrations)
        {
            Sink?.WriteStage6LoessFitDump(refinedCalibrations);
        }

        public static void WriteStage7DetectedPeptidesDump(HashSet<string> detectedPeptides)
        {
            Sink?.WriteStage7DetectedPeptidesDump(detectedPeptides);
        }
    }
}
