/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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

namespace pwiz.OspreySharp.Scoring
{
    /// <summary>
    /// The coelution-scoring diagnostic dump seam. The main-search scorer
    /// (ScoreWindow / ScoreCandidate) emits its per-candidate dumps through this
    /// interface rather than calling the exe-only <c>OspreyDiagnostics</c> static
    /// facade, so the scorer can live in OspreySharp.Scoring without referencing
    /// the top-level exe project (OOP-review rec #3, "diagnostics-bleed").
    ///
    /// The exe's file-backed sink implements this. When diagnostics are off (no
    /// <c>-d</c> flag) the injected reference is <c>null</c>, and the scorer
    /// invokes it with the null-conditional operator (<c>diag?.ShouldDump...() ??
    /// false</c>, <c>diag?.Write...(...)</c>). That mirrors the existing
    /// <c>Sink?.X()</c> idiom exactly -- zero hot-path overhead, argument
    /// evaluation short-circuited, and byte-identical to diagnostics-off today.
    /// </summary>
    public interface IScoringDiagnostics
    {
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

        void WriteCwtPathRow(string fileName, uint entryId,
            int nCwtPeaks, int nFinalPeaks, int nScored, bool scored,
            IReadOnlyList<XicData> xics);
    }
}
