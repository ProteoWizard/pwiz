/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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

using System.IO;
using System.Linq;
using System.Threading;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Turns decoded peak arrays plus per-spectrum metadata into the
    /// <see cref="Spectrum"/> / <see cref="MS1Spectrum"/> objects the rest of
    /// Osprey consumes, independent of where the peaks came from.
    ///
    /// This is the single definition of what a spectrum IS: peak sort order,
    /// the isolation-window fail-fast, the precursor-m/z fallback. Both
    /// <see cref="MzmlReader"/> and (on net472) the vendor-raw reader build
    /// through it, so a cache written from a <c>.raw</c> and one written from the
    /// mzML msconvert produced from that same <c>.raw</c> can differ only where
    /// the two PARSERS genuinely disagree - never because two code paths
    /// assembled equivalent data differently. That distinction is what makes a
    /// raw-vs-mzML byte comparison a meaningful test (issue #4496).
    /// </summary>
    internal static class SpectrumBuilder
    {
        // Unsorted-centroid notices are per-process, not per-file: the cap keeps
        // a pathological file from flooding the log across parallel ProcessFile
        // calls. After the cap, we suppress further lines (the first ones
        // already prove the case is happening).
        private static int s_unsortedLogCount;
        private const int MaxUnsortedLogLines = 10;

        /// <summary>
        /// Sort a spectrum's peaks by m/z if they are not already sorted,
        /// permuting the intensity array with them. Returns true when a sort was
        /// performed, which the callers accumulate into
        /// <see cref="MzmlResult.UnsortedSpectrumCount"/>.
        ///
        /// Some producers emit peaks that are not strictly ascending in m/z
        /// (observed in a HeLa Astral 3 mz DIA file: ~0.07% of spectra have a
        /// single inverted pair of consecutive centroids). Downstream fragment
        /// matching binary-searches the spectrum; the Rust partition_point and
        /// BinarySearchLowerBound use procedurally different step patterns, so an
        /// unsorted region produces UB-style divergence between the two impls.
        /// Sort once at load time so every downstream consumer sees a
        /// well-defined ordering. The leading O(n) sortedness check is the
        /// common-case fast path; the OrderBy permutation only runs on
        /// inversions. LINQ OrderBy is stable (matches Rust slice::sort_by);
        /// Array.Sort on parallel arrays is unstable introsort and would reorder
        /// ties differently.
        ///
        /// Throws on NaN m/z: NaN comparison semantics differ between
        /// Comparer&lt;double&gt;.Default and Rust's total_cmp, so we cannot sort
        /// consistently across impls; better to refuse the spectrum than
        /// silently diverge.
        /// </summary>
        internal static bool EnsureSorted(uint spectrumIndex,
            ref double[] mzArray, ref float[] intensityArray)
        {
            if (mzArray == null || mzArray.Length < 2)
                return false;
            // Defensive guard: a malformed source where the m/z and intensity
            // arrays are not the same length would IndexOutOfRange on the
            // permutation step. Skip sorting in that case (downstream code
            // already has its own length checks).
            if (intensityArray == null || intensityArray.Length != mzArray.Length)
                return false;
            // Walk the array once: detect NaN m/z (fail loudly so a user report
            // surfaces) and check sortedness in the same pass.
            bool sorted = true;
            for (int i = 0; i < mzArray.Length; i++)
            {
                if (double.IsNaN(mzArray[i]))
                {
                    throw new InvalidDataException(string.Format(
                        "NaN m/z at index {0} of spectrum_index={1} (n_peaks={2}); " +
                        "cannot sort or fragment-match a malformed centroid array.",
                        i, spectrumIndex, mzArray.Length));
                }
                if (i > 0 && mzArray[i] < mzArray[i - 1])
                    sorted = false;
            }
            if (sorted)
                return false;
            // Re-sorting below is unconditional (correctness); the per-spectrum
            // notice is implementer detail - some instrument data carries a tiny
            // fraction of unsorted peak arrays (e.g. Astral ~0.07%) that Osprey
            // silently corrects - so it stays behind --verbose.
            if (OspreyOutput.Verbose)
            {
                int logCount = Interlocked.Increment(ref s_unsortedLogCount);
                if (logCount <= MaxUnsortedLogLines)
                {
                    OspreyOutput.Out.WriteLine(
                        $@"[unsorted-spectrum] spectrum_index={spectrumIndex} n_peaks={mzArray.Length}");
                    if (logCount == MaxUnsortedLogLines)
                    {
                        OspreyOutput.Out.WriteLine(
                            $@"[unsorted-spectrum] suppressing further lines (>{MaxUnsortedLogLines} per process)");
                    }
                }
            }
            int n = mzArray.Length;
            double[] keyMz = mzArray;
            int[] order = Enumerable.Range(0, n)
                .OrderBy(i => keyMz[i]).ToArray();
            double[] sortedMzs = new double[n];
            float[] sortedInts = new float[n];
            for (int i = 0; i < n; i++)
            {
                sortedMzs[i] = mzArray[order[i]];
                sortedInts[i] = intensityArray[order[i]];
            }
            mzArray = sortedMzs;
            intensityArray = sortedInts;
            return true;
        }

        /// <summary>
        /// Build an MS1 spectrum record. <paramref name="spectrumIndex"/> is the
        /// 0-based position in the source file, which is what
        /// <see cref="MS1Spectrum.ScanNumber"/> carries (NOT a vendor scan number).
        /// </summary>
        internal static MS1Spectrum CreateMs1Spectrum(uint spectrumIndex, double retentionTime,
            double[] mzArray, float[] intensityArray)
        {
            return new MS1Spectrum
            {
                ScanNumber = spectrumIndex,
                RetentionTime = retentionTime,
                Mzs = mzArray,
                Intensities = intensityArray,
            };
        }

        /// <summary>
        /// Build an MS2 spectrum record, or null when the spectrum carries no
        /// usable precursor center (the caller skips it).
        ///
        /// <paramref name="isoLower"/> / <paramref name="isoUpper"/> are OFFSETS
        /// from the center, not absolute bounds and not a width. Missing offsets
        /// fail fast rather than substituting a hardcoded 12.5 default: DIA
        /// processing cannot proceed without true isolation windows, and a silent
        /// default produces bogus results that are very hard to diagnose
        /// downstream. Mirrors the equivalent fail-fast in
        /// osprey/crates/osprey-io/src/mzml/parser.rs (PR #39 on maccoss/osprey).
        /// </summary>
        internal static Spectrum CreateMs2Spectrum(uint spectrumIndex, double retentionTime,
            double precursorMz, bool hasIsolationWindow, double isoTarget, double isoLower,
            double isoUpper, double[] mzArray, float[] intensityArray)
        {
            // Use selected ion m/z as center if no isolation window target.
            double center = hasIsolationWindow && isoTarget > 0 ? isoTarget : precursorMz;
            if (center <= 0)
                return null;

            if (isoLower <= 0)
                throw new InvalidDataException(string.Format(
                    "spectrum index {0}: no valid isolation-window lower offset " +
                    "(cvParam MS:1000828 missing or non-positive); cannot process DIA data " +
                    "without true isolation windows.",
                    spectrumIndex));
            if (isoUpper <= 0)
                throw new InvalidDataException(string.Format(
                    "spectrum index {0}: no valid isolation-window upper offset " +
                    "(cvParam MS:1000829 missing or non-positive); cannot process DIA data " +
                    "without true isolation windows.",
                    spectrumIndex));

            return new Spectrum
            {
                ScanNumber = spectrumIndex,
                RetentionTime = retentionTime,
                PrecursorMz = precursorMz > 0 ? precursorMz : center,
                IsolationWindow = new IsolationWindow(center, isoLower, isoUpper),
                Mzs = mzArray,
                Intensities = intensityArray,
            };
        }
    }
}
