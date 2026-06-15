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

using System.Collections.Generic;
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.FDR;
using pwiz.OspreySharp.FDR.Reconciliation;
using pwiz.OspreySharp.Scoring;

namespace pwiz.OspreySharp
{
    /// <summary>
    /// The cross-implementation bisection diagnostics seam for the whole
    /// pipeline -- the full counterpart of the scoring-only
    /// <see cref="IScoringDiagnostics"/>. Pipeline tasks read the OSPREY_DUMP_* /
    /// OSPREY_DIAG_* gate flags and emit their byte-stable dumps through this
    /// instance (carried on the pipeline context) rather than reaching the
    /// exe-only <c>OspreyDiagnostics</c> static facade, so the task layer can
    /// live in a library that does not reference the top-level exe project
    /// (4th OOP review: "diagnostics-bleed is the keystone constraint").
    ///
    /// The exe's file-backed sink (<c>OspreyFileDiagnostics</c>) implements this.
    /// When diagnostics are off the injected reference is <c>null</c> and call
    /// sites invoke it through the null-conditional operator
    /// (<c>diag?.ShouldDump() ?? false</c>, <c>diag?.Write(...)</c>), exactly like
    /// <see cref="IScoringDiagnostics"/>: a single null check, no virtual
    /// dispatch, and arguments short-circuited -- byte-identical to, and as fast
    /// as, diagnostics-off today.
    ///
    /// Dump format must stay byte-for-byte stable; the contract for each method
    /// lives on the concrete sink implementation.
    /// </summary>
    public interface IOspreyDiagnostics
    {
        // ----- Env-var gate flags -----

        bool DumpCalSample { get; }
        bool CalSampleOnly { get; }
        bool DumpCalWindows { get; }
        bool CalWindowsOnly { get; }
        bool DumpCalMatch { get; }
        bool CalMatchOnly { get; }
        bool DumpMs2CalErrors { get; }
        bool Ms2CalErrorsOnly { get; }
        bool DumpLdaScores { get; }
        bool LdaScoresOnly { get; }
        bool DumpLoessInput { get; }
        bool LoessInputOnly { get; }
        bool DumpPercolator { get; }
        bool PercolatorOnly { get; }
        bool DumpRescored { get; }
        bool RescoredOnly { get; }
        bool DumpMpInputs { get; }
        bool DumpPredictRt { get; }
        bool DumpCwtPath { get; }
        bool DumpConsensus { get; }
        bool ConsensusOnly { get; }
        bool DumpMulticharge { get; }
        bool MultichargeOnly { get; }
        bool DumpRefit { get; }
        bool RefitOnly { get; }
        bool DumpReconciliation { get; }
        bool ReconciliationOnly { get; }
        bool DumpCalibration { get; }
        bool CalibrationOnly { get; }
        bool DumpInvPredict { get; }
        bool InvPredictOnly { get; }
        bool DumpProteinFdr { get; }
        bool ProteinFdrOnly { get; }
        bool DumpStage7ProteinFdr { get; }
        bool Stage7ProteinFdrOnly { get; }
        bool DumpDetectedPeptides { get; }
        bool DumpLoessFit { get; }
        bool LoessFitOnly { get; }
        bool CalWindowsCollecting { get; }

        uint? DiagXicEntryId { get; }
        // Defaults to 1 (not 0) when unset; the default lives in the sink's
        // parser (OspreyFileDiagnostics), so a consumer reading this through a
        // null ctx.Diagnostics must use "?? 1" to preserve historical behavior.
        int DiagXicPass { get; }
        HashSet<uint> DiagSearchEntryIds { get; }
        int? DiagMpScan { get; }

        // ----- Stages 1-4: calibration + per-candidate search dumps -----

        void WriteCalSampleDump(string fileName, IEnumerable<LibraryEntry> sampledEntries);

        void WriteCalScalarsAndGridDump(
            IReadOnlyList<LibraryEntry> targets,
            IReadOnlyList<LibraryEntry> decoys,
            int binsPerAxis,
            double rtMin, double rtMax, double mzMin, double mzMax,
            double rtRange, double mzRange, double rtBinWidth, double mzBinWidth,
            int nOccupied, int perCell, ulong seed,
            List<int>[,] grid);

        void StartCalWindowCollection();

        void AddCalWindowRow(LibraryEntry entry, IsolationWindow iso,
            double expectedRt, double rtLo, double rtHi);

        void WriteCalWindowsDump(int passNumber);

        void WriteCalMatchDump(int passNumber,
            IEnumerable<CalibrationMatch> matches,
            IEnumerable<LibraryEntry> sampledEntries,
            IDictionary<uint, KeyValuePair<double, double>> matchRts,
            IDictionary<uint, double> snrByEntryId);

        void WriteMs2CalErrorsDump(IEnumerable<CalibrationMatch> contributingMatches);

        void WriteLdaScoresDump(int passNumber, IEnumerable<CalibrationMatch> matchArray);

        void WriteLoessInputDump(int passNumber, double[] libRts, double[] measuredRts);

        void WriteCalibrationSummary(
            RTCalibration rtCal,
            MzCalibrationResult ms1Cal,
            MzCalibrationResult ms2Cal);

        bool ShouldDumpCalXicFor(uint entryId, int currentPass);

        void WriteCalXicEntryDumpAndExit(LibraryEntry entry, int currentPass,
            RTCalibration calibrationModel, double expectedRt, double initialTolerance,
            double rtSlope, double rtIntercept,
            IReadOnlyList<Spectrum> candidateSpectra,
            IReadOnlyList<XicData> xics);

        bool ShouldDumpSearchXicFor(uint entryId);

        void WriteSearchXicDump(LibraryEntry candidate,
            double expectedRt, double rtTolerance,
            int startScan, int endScan, int rangeLen,
            IReadOnlyList<Spectrum> windowSpectra,
            IReadOnlyList<XicData> xics);

        bool ShouldDumpMpFor(uint apexScanNumber, string candidateModifiedSequence);

        void WriteMpDump(LibraryEntry candidate, uint apexScanNumber,
            XICPeakBounds bestPeak, int peakLen,
            double mpCosine, double mpResidualRatio, double mpMinFragmentR2, double mpResidualCorr,
            TukeyMedianPolishResult polish,
            IReadOnlyList<KeyValuePair<int, double[]>> peakXics);

        void WriteMpInputsRow(uint entryId, uint apexScan,
            IList<KeyValuePair<int, double[]>> peakXics, double[] peakRts);

        void CloseMpInputsDump();

        void WritePredictRtArrays(string fileName, double[] libraryRts, double[] fittedValues);

        void WritePredictRtCall(uint entryId, double libraryRt, double expectedRt);

        void WriteCwtPathRow(string fileName, uint entryId,
            int nCwtPeaks, int nFinalPeaks, int nScored, bool scored,
            IReadOnlyList<XicData> xics);

        void CloseCwtPathDump();

        void ClosePredictRtDump();

        // ----- Stages 5-7: join, rescore, reconciliation, protein FDR dumps -----

        void WriteStage5PercolatorDump(List<KeyValuePair<string, List<FdrEntry>>> perFileEntries);

        void WriteStage6RescoredDump(List<KeyValuePair<string, List<FdrEntry>>> perFileEntries);

        void WriteStage6ConsensusDump(IReadOnlyList<PeptideConsensusRT> consensus);

        void WriteStage6MultichargeDump(
            IReadOnlyList<KeyValuePair<string, IReadOnlyList<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, IReadOnlyList<(int Index, double Apex, double Start, double End)>> perFileTargets);

        void WriteStage6RefitDump(
            IReadOnlyDictionary<string, RTCalibration> refinedCalibrations);

        void WriteStage6ReconciliationDump(
            IReadOnlyDictionary<(string File, int Index), ReconcileAction> actions,
            IReadOnlyList<KeyValuePair<string, IReadOnlyList<FdrEntry>>> perFileEntries);

        void WriteStage6CalibrationDump(
            string fileName, double[] libraryRts, double[] fittedValues);

        void WriteStage6InvPredictDump(IList<InvPredictRecord> records);

        void WriteStage6ProteinFdrDump(
            IDictionary<string, PeptideScore> bestScores,
            IDictionary<string, double> peptideQvalues);

        void WriteStage7ProteinFdrDump(
            ProteinParsimonyResult parsimony,
            ProteinFdrResult fdrResult);

        void WriteStage6LoessFitDump(
            IReadOnlyDictionary<string, RTCalibration> refinedCalibrations);

        void WriteStage7DetectedPeptidesDump(HashSet<string> detectedPeptides);
    }
}
