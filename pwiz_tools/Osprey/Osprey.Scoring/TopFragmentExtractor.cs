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

using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Scoring
{
    /// <summary>
    /// Stateless helpers that select a library entry's top-N fragments (by
    /// relative intensity, with a stable tie-break that matches Rust
    /// slice::sort_by) and probe spectra for them: extract their XICs or count
    /// how many match. Relocated verbatim out of AbstractScoringTask, which
    /// carried these as tangled instance/static members; the arithmetic and the
    /// stable-sort tie-break are unchanged, so cross-impl parity is unaffected.
    /// </summary>
    public static class TopFragmentExtractor
    {
        // Number of top-intensity library fragments used for calibration
        // scoring + dedup top-6 overlap. Public so the Tasks layer
        // (Calibrator) can reuse the same width.
        public const int CAL_TOP_N_FRAGMENTS = 6;

        /// <summary>
        /// Extract XICs for the top N most intense library fragments across the
        /// supplied (pre-filtered) spectra list. Includes an XIC for every selected
        /// fragment, even all-zero ones -- dropping all-zero fragments biases decoys
        /// to higher R^2 (matches Rust).
        /// </summary>
        public static List<XicData> ExtractTopNFragmentXics(
            LibraryEntry entry,
            List<Spectrum> candidateSpectra,
            double[] rts,
            int maxFragments,
            OspreyConfig config)
        {
            var xics = new List<XicData>();
            if (entry.Fragments == null || entry.Fragments.Count == 0)
                return xics;

            int[] topIndices = SelectTopFragmentIndices(entry.Fragments, maxFragments);

            int nScans = candidateSpectra.Count;
            foreach (int fragIdx in topIndices)
            {
                var fragment = entry.Fragments[fragIdx];
                double tolDa = config.FragmentTolerance.ToleranceDa(fragment.Mz);
                double lower = fragment.Mz - tolDa;
                double upper = fragment.Mz + tolDa;

                double[] intensities = new double[nScans];

                for (int scanIdx = 0; scanIdx < nScans; scanIdx++)
                {
                    var spectrum = candidateSpectra[scanIdx];
                    int best = FindClosestPeakInWindow(spectrum.Mzs, fragment.Mz, lower, upper);
                    if (best >= 0)
                        intensities[scanIdx] = spectrum.Intensities[best];
                }

                // Always include the fragment XIC, even all-zero. Rust:
                // "Dropping all-zero fragments biases decoys to higher R^2".
                xics.Add(new XicData(fragIdx, rts, intensities));
            }

            return xics;
        }

        /// <summary>
        /// Extract fragment XICs for a candidate across the scan range.
        /// </summary>
        public static List<XicData> ExtractFragmentXics(
            LibraryEntry candidate,
            List<Spectrum> windowSpectra,
            double[] windowRts,
            int startScan, int endScan,
            OspreyConfig config)
        {
            // Port of Rust extract_fragment_xics (osprey-scoring/src/lib.rs:505).
            // Differences from the previous C# implementation:
            //   1. Use top-6 fragments by relative intensity (not all fragments)
            //   2. Pick the closest peak by m/z within tolerance (not most intense)
            //   3. Always include all selected fragments, even all-zero XICs
            //      (dropping all-zero fragments biases decoys to higher R^2)
            int rangeLen = endScan - startScan + 1;
            var xics = new List<XicData>();
            if (candidate.Fragments == null || candidate.Fragments.Count == 0)
                return xics;

            int[] topIndices = SelectTopFragmentIndices(candidate.Fragments, CAL_TOP_N_FRAGMENTS);

            // Build shared RT array for this range
            double[] rangeRts = new double[rangeLen];
            for (int i = 0; i < rangeLen; i++)
                rangeRts[i] = windowRts[startScan + i];

            foreach (int fragIdx in topIndices)
            {
                var fragment = candidate.Fragments[fragIdx];
                double tolDa = config.FragmentTolerance.ToleranceDa(fragment.Mz);
                double lower = fragment.Mz - tolDa;
                double upper = fragment.Mz + tolDa;

                double[] intensities = new double[rangeLen];

                for (int scanIdx = 0; scanIdx < rangeLen; scanIdx++)
                {
                    var spectrum = windowSpectra[startScan + scanIdx];
                    int best = FindClosestPeakInWindow(spectrum.Mzs, fragment.Mz, lower, upper);
                    if (best >= 0)
                        intensities[scanIdx] = spectrum.Intensities[best];
                }

                // Always include the fragment XIC, even if all zero. Zero intensities
                // are valid data (no centroided peak found) and dropping all-zero
                // fragments biases decoys to higher R^2. Matches Rust behavior.
                xics.Add(new XicData(fragIdx, rangeRts, intensities));
            }

            return xics;
        }

        /// <summary>
        /// Count how many of the top 6 library fragments (by intensity) have
        /// matching peaks in the spectrum. Used for the top6_matched feature
        /// value (called once per scored entry, not in the hot prefilter loop).
        /// The prefilter uses HasTopNFragmentMatch instead for speed.
        /// </summary>
        public static byte CountTop6Matches(
            LibraryEntry entry, Spectrum spectrum, OspreyConfig config)
        {
            if (entry.Fragments == null || entry.Fragments.Count == 0)
                return 0;

            int[] topIndices = SelectTopFragmentIndices(entry.Fragments, 6);
            byte matched = 0;
            foreach (int fragIdx in topIndices)
            {
                if (SpectralScorer.HasMatch(entry.Fragments[fragIdx].Mz,
                    spectrum.Mzs, config.FragmentTolerance))
                    matched++;
            }
            return matched;
        }

        /// <summary>
        /// Select the indices of the top <paramref name="maxFragments"/> library
        /// fragments by descending <see cref="LibraryFragment.RelativeIntensity"/>.
        /// When there are at most <paramref name="maxFragments"/> fragments, every
        /// index is returned in library order.
        /// </summary>
        /// <remarks>
        /// The sort is a STABLE <c>OrderByDescending</c> (stable per the .NET
        /// contract) so ties on RelativeIntensity preserve the library's fragment
        /// order, matching Rust's stable <c>slice::sort_by</c>
        /// (osprey-scoring/src/lib.rs:528). <c>List&lt;T&gt;.Sort</c> /
        /// <c>Array.Sort</c> with a <c>Comparison&lt;T&gt;</c> is introsort and
        /// unstable, which would pick different fragments on ties and cascade
        /// through XIC extraction -> peak detection -> rankScore -> bestPeak.
        /// </remarks>
        public static int[] SelectTopFragmentIndices(
            IReadOnlyList<LibraryFragment> fragments, int maxFragments)
        {
            int nFrags = fragments.Count;
            if (nFrags <= maxFragments)
            {
                var allIndices = new int[nFrags];
                for (int i = 0; i < nFrags; i++)
                    allIndices[i] = i;
                return allIndices;
            }
            return Enumerable.Range(0, nFrags)
                .OrderByDescending(i => fragments[i].RelativeIntensity)
                .Take(maxFragments)
                .ToArray();
        }

        /// <summary>
        /// Index of the peak whose m/z is closest to <paramref name="targetMz"/>
        /// within the inclusive window [<paramref name="lower"/>,
        /// <paramref name="upper"/>], or -1 when no peak falls in the window.
        /// </summary>
        /// <remarks>
        /// Picks CLOSEST by m/z (not most intense). The ascending scan with a
        /// strict-less-than replacement keeps the first (lowest-index) peak on
        /// exact ties. Matches Rust extract_fragment_xics in
        /// osprey-scoring/src/batch.rs. Callers read the intensity or the m/z at
        /// the returned index as needed.
        /// </remarks>
        public static int FindClosestPeakInWindow(
            double[] mzs, double targetMz, double lower, double upper)
        {
            if (mzs == null || mzs.Length == 0)
                return -1;
            int lo = ScoringMath.BinarySearchLowerBound(mzs, lower);
            if (lo >= mzs.Length || mzs[lo] > upper)
                return -1;
            int best = lo;
            double bestDiff = Math.Abs(mzs[lo] - targetMz);
            for (int k = lo + 1; k < mzs.Length && mzs[k] <= upper; k++)
            {
                double diff = Math.Abs(mzs[k] - targetMz);
                if (diff < bestDiff)
                {
                    bestDiff = diff;
                    best = k;
                }
            }
            return best;
        }
    }
}
