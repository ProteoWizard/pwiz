/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Demux
{
    /// <summary>
    /// Infers the demultiplexing scheme of a run from the isolation windows its MS2
    /// spectra report.
    /// </summary>
    /// <remarks>
    /// The narrow bins are the intervals between adjacent window boundaries (the sorted
    /// union of every lower and upper bound), so no uniform ladder is assumed and
    /// variable-width schedules work unchanged. Boundaries closer than a minimum bin width
    /// are merged first, because instruments report the same nominal edge with small
    /// floating-point differences. Unlike the pwiz codec, which infers the scheme from the
    /// first few cycles, every spectrum is examined, so a window that first appears late
    /// in the run is still a window.
    /// </remarks>
    public static class DemuxSchemeDetector
    {
        /// <summary>Boundaries closer than this (Th) are one boundary; msconvert's minWindowSize.</summary>
        public const double DEFAULT_MINIMUM_BIN_WIDTH = 0.2;

        /// <summary>Fewer MS2 spectra a cycle than this is not a sweep.</summary>
        public const int MIN_SCANNING_BINS = 64;

        /// <summary>Encoded bins wider than this (Th) are not a scanning quadrupole's.</summary>
        public const double MAX_SCANNING_BIN_WIDTH = 3.0;

        /// <summary>A strong peak kept in at least this many following bins, at the median, is a scanning acquisition.</summary>
        public const int MIN_SCANNING_PERSISTENCE = 3;

        // Boundaries are compared as integers at this resolution (0.1 mTh), which keeps the
        // clustering exact and independent of floating-point summation order.
        private const double BOUNDARY_SCALE = 1e4;

        /// <summary>
        /// Detects the scheme from each MS2 spectrum's isolation window, given in
        /// acquisition order.
        /// </summary>
        public static DemuxScheme Detect(IReadOnlyList<IsolationWindow> spectrumWindows,
            double minimumBinWidth = DEFAULT_MINIMUM_BIN_WIDTH)
        {
            if (spectrumWindows == null)
                throw new ArgumentNullException(nameof(spectrumWindows));

            int n = spectrumWindows.Count;
            var boundaryKeys = new long[2 * n];
            for (int i = 0; i < n; i++)
            {
                boundaryKeys[2 * i] = ToKey(spectrumWindows[i].LowerBound);
                boundaryKeys[2 * i + 1] = ToKey(spectrumWindows[i].UpperBound);
            }

            var boundaries = MergeBoundaries(boundaryKeys, minimumBinWidth,
                out long[] uniqueKeys, out int[] clusterOfUniqueKey);

            // Snap every spectrum's window onto the merged boundaries. Two windows that snap to
            // the same pair of boundaries are the same window, whatever their raw digits.
            var windowOfPair = new Dictionary<(int, int), int>();
            var pairs = new List<(int Lower, int Upper)>();
            var rawWindowOfSpectrum = new int[n];
            for (int i = 0; i < n; i++)
            {
                int lower = clusterOfUniqueKey[Array.BinarySearch(uniqueKeys, boundaryKeys[2 * i])];
                int upper = clusterOfUniqueKey[Array.BinarySearch(uniqueKeys, boundaryKeys[2 * i + 1])];
                if (upper <= lower)
                {
                    throw new InvalidOperationException(string.Format(
                        @"Isolation window [{0}, {1}] is narrower than the minimum bin width {2}.",
                        spectrumWindows[i].LowerBound, spectrumWindows[i].UpperBound, minimumBinWidth));
                }
                if (!windowOfPair.TryGetValue((lower, upper), out int w))
                {
                    w = pairs.Count;
                    windowOfPair.Add((lower, upper), w);
                    pairs.Add((lower, upper));
                }
                rawWindowOfSpectrum[i] = w;
            }

            // Order windows by m/z so window indices mean the same thing in every run.
            var order = new int[pairs.Count];
            for (int w = 0; w < order.Length; w++)
                order[w] = w;
            Array.Sort(order, (a, b) => // Array.Sort OK: the pair key is unique per window, so no ties
            {
                int c = pairs[a].Lower.CompareTo(pairs[b].Lower);
                return c != 0 ? c : pairs[a].Upper.CompareTo(pairs[b].Upper);
            });
            var rank = new int[order.Length];
            for (int r = 0; r < order.Length; r++)
                rank[order[r]] = r;

            // A boundary interval is a bin only if some window covers it; the gaps between
            // non-contiguous windows are not bins.
            var coverage = new int[boundaries.Length];
            foreach (var pair in pairs)
            {
                for (int b = pair.Lower; b < pair.Upper; b++)
                    coverage[b]++;
            }
            var bins = new List<DemuxBin>();
            var binOfInterval = new int[boundaries.Length];
            int maxCoverage = 0;
            for (int b = 0; b + 1 < boundaries.Length; b++)
            {
                binOfInterval[b] = -1;
                if (coverage[b] == 0)
                    continue;
                binOfInterval[b] = bins.Count;
                bins.Add(new DemuxBin(boundaries[b], boundaries[b + 1]));
                maxCoverage = Math.Max(maxCoverage, coverage[b]);
            }

            // The overlap factor is the coverage that spans the most m/z, not the largest
            // coverage anywhere. Ordinary DIA whose adjacent windows overlap by a margin of
            // 0.5 to 1 Th has thin slivers covered twice but is covered once almost everywhere,
            // and it is not a staggered design to demultiplex. Ties go to the lower coverage.
            var widthAtCoverage = new double[maxCoverage + 1];
            for (int b = 0; b + 1 < boundaries.Length; b++)
                widthAtCoverage[coverage[b]] += boundaries[b + 1] - boundaries[b];
            int overlapFactor = 1;
            for (int k = 2; k <= maxCoverage; k++)
            {
                if (widthAtCoverage[k] > widthAtCoverage[overlapFactor])
                    overlapFactor = k;
            }

            var windows = new AcquisitionWindow[pairs.Count];
            for (int r = 0; r < order.Length; r++)
            {
                var pair = pairs[order[r]];
                windows[r] = new AcquisitionWindow(r, boundaries[pair.Lower], boundaries[pair.Upper],
                    binOfInterval[pair.Lower], binOfInterval[pair.Upper - 1]);
            }

            var windowOfSpectrum = new int[n];
            for (int i = 0; i < n; i++)
                windowOfSpectrum[i] = rank[rawWindowOfSpectrum[i]];

            var kind = overlapFactor > 1 ? DemuxSchemeKind.overlapping : DemuxSchemeKind.non_overlapping;
            return new DemuxScheme(kind, bins, windows, overlapFactor, maxCoverage, windowOfSpectrum);
        }

        /// <summary>
        /// Groups a scanning acquisition's spectra into cycles: a survey scan and the sweep of MS2 spectra after it.
        /// SCIEX native ids name the experiment, experiment 1 being the survey scan; for any other id form, each
        /// spectrum's MS level is read. Spectra before the first survey scan belong to no cycle.
        /// </summary>
        public static void FindCycles(IDemuxSource source, List<int> surveyOfCycle, List<int[]> sweepOfCycle)
        {
            var isSurvey = new bool[source.Count];
            if (!TryReadSurveyScansFromIds(source, isSurvey))
            {
                for (int k = 0; k < source.Count; k++)
                    isSurvey[k] = source.Describe(k).MsLevel == 1;
            }
            var sweep = new List<int>();
            for (int k = 0; k < source.Count; k++)
            {
                if (isSurvey[k])
                {
                    if (surveyOfCycle.Count > 0)
                        sweepOfCycle.Add(sweep.ToArray());
                    sweep.Clear();
                    surveyOfCycle.Add(k);
                }
                else if (surveyOfCycle.Count > 0)
                {
                    sweep.Add(k);
                }
            }
            if (surveyOfCycle.Count > 0)
                sweepOfCycle.Add(sweep.ToArray());
        }

        /// <summary>
        /// Tells a scanning-quadrupole acquisition (SCIEX ZT Scan) from ordinary narrow-window DIA. The windows alone
        /// cannot: a ZT Scan file reports each spectrum's encoded bin, and the bins tile the range once a cycle, as
        /// narrow windows do. The data can: the quadrupole transmits about ten bins' width, so a precursor's
        /// fragments are in many consecutive bins of one cycle, where with narrow windows they are in one or two.
        /// </summary>
        public static ScanningDetection DetectScanning(IDemuxSource source)
        {
            var surveys = new List<int>();
            var sweeps = new List<int[]>();
            FindCycles(source, surveys, sweeps);
            int[] geometry = null;
            foreach (var sweep in sweeps)
            {
                if (sweep.Length >= MIN_SCANNING_BINS)
                {
                    geometry = sweep;
                    break;
                }
            }
            if (geometry == null)
                return new ScanningDetection(false, sweeps.Count, 0, 0, 0);

            // Geometry: one sweep of narrow bins, stepping up in m/z.
            var steps = new double[geometry.Length - 1];
            double previous = source.Describe(geometry[0]).Target;
            for (int b = 1; b < geometry.Length; b++)
            {
                double target = source.Describe(geometry[b]).Target;
                steps[b - 1] = target - previous;
                previous = target;
            }
            Array.Sort(steps); // Array.Sort OK: primitive doubles, so tied values are indistinguishable
            double step = steps[steps.Length / 2];
            if (steps[0] <= 0 || step > MAX_SCANNING_BIN_WIDTH)
                return new ScanningDetection(false, sweeps.Count, geometry.Length, step, 0);

            // Data: in three cycles from the middle of the run, how many following bins keep each strong peak.
            var runs = new List<int>();
            int middle = sweeps.Count / 2;
            for (int c = Math.Max(0, middle - 1); c <= Math.Min(sweeps.Count - 1, middle + 1); c++)
                ProbePersistence(source, sweeps[c], runs);
            runs.Sort(); // Array.Sort OK: ints, so tied values are indistinguishable
            double persistence = runs.Count > 0 ? runs[runs.Count / 2] : 0;
            return new ScanningDetection(persistence >= MIN_SCANNING_PERSISTENCE, sweeps.Count, geometry.Length, step,
                persistence);
        }

        /// <summary>
        /// For every sixteenth bin of the middle half of a sweep, the 20 most intense points, and for each how many of
        /// the following bins (up to 24) hold a point within 10 ppm of it, without a gap.
        /// </summary>
        private static void ProbePersistence(IDemuxSource source, int[] sweep, List<int> runs)
        {
            const int probeEvery = 16, probePeaks = 20, maxRun = 24;
            const double tolerance = 10e-6;
            var sorted = new Dictionary<int, double[]>();
            for (int b = sweep.Length / 4; b < sweep.Length * 3 / 4; b += probeEvery)
            {
                source.Read(sweep[b], out var mz, out var intensity);
                var top = StrongestPoints(mz, intensity, probePeaks);
                source.Release(sweep[b]);
                foreach (double m in top)
                {
                    int run = 0;
                    while (run < maxRun && b + run + 1 < sweep.Length &&
                           HasPointNear(PositiveMz(source, sweep[b + run + 1], sorted), m, m * tolerance))
                    {
                        run++;
                    }
                    runs.Add(run);
                }
            }
        }

        /// <summary>The m/z of the most intense points, strongest first.</summary>
        private static List<double> StrongestPoints(IReadOnlyList<double> mz, IReadOnlyList<double> intensity, int count)
        {
            var top = new List<double>();
            if (mz == null || intensity == null)
                return top;
            var order = new List<int>();
            for (int i = 0; i < Math.Min(mz.Count, intensity.Count); i++)
            {
                if (intensity[i] > 0)
                    order.Add(i);
            }
            order.Sort((a, b) => intensity[b] != intensity[a] ? intensity[b].CompareTo(intensity[a]) : a.CompareTo(b)); // Array.Sort OK: ties broken by index
            for (int i = 0; i < Math.Min(count, order.Count); i++)
                top.Add(mz[order[i]]);
            return top;
        }

        /// <summary>A spectrum's m/z with a positive intensity, sorted, read once per probe.</summary>
        private static double[] PositiveMz(IDemuxSource source, int index, Dictionary<int, double[]> sorted)
        {
            if (sorted.TryGetValue(index, out var cached))
                return cached;
            source.Read(index, out var mz, out var intensity);
            var kept = new List<double>();
            if (mz != null && intensity != null)
            {
                for (int i = 0; i < Math.Min(mz.Count, intensity.Count); i++)
                {
                    if (intensity[i] > 0)
                        kept.Add(mz[i]);
                }
            }
            source.Release(index);
            var result = kept.ToArray();
            Array.Sort(result); // Array.Sort OK: primitive doubles, so tied values are indistinguishable
            sorted[index] = result;
            return result;
        }

        private static bool HasPointNear(double[] sortedMz, double mz, double tolerance)
        {
            int at = Array.BinarySearch(sortedMz, mz - tolerance);
            if (at < 0)
                at = ~at;
            return at < sortedMz.Length && sortedMz[at] <= mz + tolerance;
        }

        /// <summary>
        /// Marks the survey scans from SCIEX native ids ("sample=1 period=1 cycle=N experiment=E"),
        /// experiment 1 being the survey scan. False if any id is in another form.
        /// </summary>
        private static bool TryReadSurveyScansFromIds(IDemuxSource source, bool[] isSurvey)
        {
            const string token = @"experiment=";
            for (int k = 0; k < source.Count; k++)
            {
                string id = source.NativeId(k);
                int at = id.IndexOf(token, StringComparison.Ordinal);
                if (at < 0 || id.IndexOf(@"cycle=", StringComparison.Ordinal) < 0)
                    return false;
                int start = at + token.Length, end = start;
                while (end < id.Length && char.IsDigit(id[end]))
                    end++;
                if (end == start)
                    return false;
                isSurvey[k] = id.Substring(start, end - start) == @"1";
            }
            return true;
        }

        private static long ToKey(double mz)
        {
            return (long)Math.Round(mz * BOUNDARY_SCALE);
        }

        /// <summary>
        /// Clusters the distinct boundary keys: a boundary within the minimum bin width of the
        /// first member of the current cluster joins it. Returns each cluster's m/z (the mean of
        /// its distinct members), and maps every distinct key to its cluster.
        /// </summary>
        private static double[] MergeBoundaries(long[] keys, double minimumBinWidth,
            out long[] uniqueKeys, out int[] clusterOfUniqueKey)
        {
            var sorted = (long[])keys.Clone();
            Array.Sort(sorted); // Array.Sort OK: primitive longs, so tied values are indistinguishable
            var unique = new List<long>();
            foreach (long key in sorted)
            {
                if (unique.Count == 0 || unique[unique.Count - 1] != key)
                    unique.Add(key);
            }
            uniqueKeys = unique.ToArray();
            clusterOfUniqueKey = new int[uniqueKeys.Length];

            long mergeDistance = (long)Math.Round(minimumBinWidth * BOUNDARY_SCALE);
            var clusters = new List<double>();
            int start = 0;
            while (start < uniqueKeys.Length)
            {
                int end = start + 1;
                while (end < uniqueKeys.Length && uniqueKeys[end] - uniqueKeys[start] <= mergeDistance)
                    end++;
                long sum = 0;
                for (int i = start; i < end; i++)
                {
                    sum += uniqueKeys[i];
                    clusterOfUniqueKey[i] = clusters.Count;
                }
                clusters.Add(sum / (double)(end - start) / BOUNDARY_SCALE);
                start = end;
            }
            return clusters.ToArray();
        }
    }
}
