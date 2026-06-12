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
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Scoring
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
        /// supplied (pre-filtered) spectra list. Returns only fragments that have
        /// at least one non-zero intensity point.
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

            // Select top N fragment indices by descending relative intensity.
            int nFrags = entry.Fragments.Count;
            int nTop = Math.Min(nFrags, maxFragments);
            int[] topIndices;
            if (nFrags <= maxFragments)
            {
                topIndices = new int[nFrags];
                for (int i = 0; i < nFrags; i++)
                    topIndices[i] = i;
            }
            else
            {
                // Stable sort matching Rust slice::sort_by on
                // RelativeIntensity ties; List<T>.Sort with
                // Comparison<T> is introsort and unstable.
                topIndices = Enumerable.Range(0, nFrags)
                    .OrderByDescending(i => entry.Fragments[i].RelativeIntensity)
                    .Take(nTop)
                    .ToArray();
            }

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
                    if (spectrum.Mzs == null || spectrum.Mzs.Length == 0)
                        continue;

                    int lo = ScoringMath.BinarySearchLowerBound(spectrum.Mzs, lower);
                    if (lo >= spectrum.Mzs.Length || spectrum.Mzs[lo] > upper)
                        continue;

                    // Pick CLOSEST peak by m/z (not most intense). Matches
                    // Rust extract_fragment_xics in osprey-scoring/src/batch.rs.
                    double bestDiff = Math.Abs(spectrum.Mzs[lo] - fragment.Mz);
                    double bestIntensity = spectrum.Intensities[lo];
                    for (int k = lo + 1; k < spectrum.Mzs.Length && spectrum.Mzs[k] <= upper; k++)
                    {
                        double diff = Math.Abs(spectrum.Mzs[k] - fragment.Mz);
                        if (diff < bestDiff)
                        {
                            bestDiff = diff;
                            bestIntensity = spectrum.Intensities[k];
                        }
                    }
                    intensities[scanIdx] = bestIntensity;
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

            int nFrags = candidate.Fragments.Count;
            int nTop = Math.Min(nFrags, CAL_TOP_N_FRAGMENTS);
            int[] topIndices;
            if (nFrags <= CAL_TOP_N_FRAGMENTS)
            {
                topIndices = new int[nFrags];
                for (int i = 0; i < nFrags; i++)
                    topIndices[i] = i;
            }
            else
            {
                // Rust's `indexed.sort_by(|a, b| b.1.total_cmp(&a.1))` at
                // osprey-scoring/src/lib.rs:528 is STABLE (slice::sort_by
                // is stable). Switch from `List<T>.Sort` (introsort,
                // unstable) to LINQ `OrderByDescending` (stable per .NET
                // contract) so that ties on RelativeIntensity preserve
                // the library's fragment order, matching Rust. Without
                // this, a peptide with two fragments at equal relative
                // intensity can land different fragments in its top-N on
                // the C# side, which cascades through XIC extraction →
                // peak detection → rankScore → bestPeak selection.
                topIndices = Enumerable.Range(0, nFrags)
                    .OrderByDescending(i => candidate.Fragments[i].RelativeIntensity)
                    .Take(nTop)
                    .ToArray();
            }

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
                    if (spectrum.Mzs == null || spectrum.Mzs.Length == 0)
                        continue;

                    int lo = ScoringMath.BinarySearchLowerBound(spectrum.Mzs, lower);
                    if (lo >= spectrum.Mzs.Length || spectrum.Mzs[lo] > upper)
                        continue;

                    // Find closest peak by m/z within tolerance (matches Rust).
                    double bestDiff = Math.Abs(spectrum.Mzs[lo] - fragment.Mz);
                    double bestIntensity = spectrum.Intensities[lo];
                    for (int k = lo + 1; k < spectrum.Mzs.Length && spectrum.Mzs[k] <= upper; k++)
                    {
                        double diff = Math.Abs(spectrum.Mzs[k] - fragment.Mz);
                        if (diff < bestDiff)
                        {
                            bestDiff = diff;
                            bestIntensity = spectrum.Intensities[k];
                        }
                    }
                    intensities[scanIdx] = bestIntensity;
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

            int nTop = Math.Min(entry.Fragments.Count, 6);
            byte matched = 0;

            if (entry.Fragments.Count <= 6)
            {
                for (int i = 0; i < entry.Fragments.Count; i++)
                {
                    if (SpectralScorer.HasMatch(entry.Fragments[i].Mz,
                        spectrum.Mzs, config.FragmentTolerance))
                        matched++;
                }
            }
            else
            {
                // Stable top-6 by RelativeIntensity, matching Rust
                // slice::sort_by ties (Array.Sort with Comparison<T>
                // is introsort and unstable).
                var indices = Enumerable.Range(0, entry.Fragments.Count)
                    .OrderByDescending(i => entry.Fragments[i].RelativeIntensity)
                    .Take(nTop)
                    .ToArray();

                for (int t = 0; t < indices.Length; t++)
                {
                    if (SpectralScorer.HasMatch(entry.Fragments[indices[t]].Mz,
                        spectrum.Mzs, config.FragmentTolerance))
                        matched++;
                }
            }
            return matched;
        }
    }
}
