/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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
using System.Linq;

namespace pwiz.Osprey.FDR.ModelDiagnostics
{
    /// <summary>
    /// The fragment mass-error view of the --model-diagnostics report: per run, a histogram of the signed m/z error of the peak closest to each top library
    /// fragment of the confidently detected targets, measured in a window WIDER than the search
    /// tolerance so a tolerance that is too narrow shows as a distribution cut off at its edges.
    /// Split by observed peak intensity, because weak ions are measured less precisely.
    ///
    /// <para>Each run's histogram is a bounded, fixed-bin summary (a few hundred counts per
    /// intensity class) written beside the run by PerFileRescoring, which holds the run's
    /// calibrated spectra and its first-pass q-values. The report plots each beside the run's row
    /// of the calibration corrections table.</para>
    /// </summary>
    public sealed partial class ModelDiagnosticsData
    {
        /// <summary>The intensity class names, weakest first, then the class holding every match.</summary>
        public static readonly string[] MASS_ERROR_CLASSES = { @"low", @"mid", @"high", @"all" };

        /// <summary>Half-widths (in robust SDs) of the core the plain mean and SD are taken over.</summary>
        private const double MASS_ERROR_CORE_ROBUST_SDS = 4.0;

        /// <summary>
        /// The fragment mass-error view, or null when no run has a mass-error summary. Attached at
        /// render time from the per-run summaries, never stored in a pass product.
        /// </summary>
        public FragmentMassErrorData FragmentMassErrors { get; set; }

        /// <summary>Every run's fragment mass-error summary.</summary>
        public sealed class FragmentMassErrorData
        {
            /// <summary>Per-run summaries, in input-file order.</summary>
            public List<FragmentMassErrorRun> Files { get; set; }
        }

        /// <summary>
        /// One run's fragment mass-error histogram. Bin <c>i</c> covers
        /// <c>[-HalfWidth + i*w, -HalfWidth + (i+1)*w)</c> with <c>w = 2*HalfWidth/BinCount</c>.
        /// </summary>
        public sealed class FragmentMassErrorRun
        {
            public string File { get; set; }
            /// <summary>"ppm" or "Th", the unit of every width and error here.</summary>
            public string Unit { get; set; }
            /// <summary>Half-width of the measurement window, wider than any search tolerance.</summary>
            public double HalfWidth { get; set; }
            public int BinCount { get; set; }
            /// <summary>The fragment tolerance the user configured.</summary>
            public double ConfiguredTolerance { get; set; }
            /// <summary>The tolerance the search actually extracted with (calibrated), about the calibration mean.</summary>
            public double SearchTolerance { get; set; }
            /// <summary>The MS2 calibration's mean error, already removed from the spectra measured.</summary>
            public double CalibrationMean { get; set; }
            /// <summary>The MS2 calibration's SD, from which the search tolerance was derived.</summary>
            public double CalibrationSd { get; set; }
            /// <summary>The first-pass run q-value a target must reach to be measured.</summary>
            public double MaxQ { get; set; }
            /// <summary>Library fragments measured per precursor (the most intense).</summary>
            public int TopFragments { get; set; }
            /// <summary>Scans measured either side of the apex scan.</summary>
            public int ScanRadius { get; set; }
            /// <summary>Target precursors measured.</summary>
            public int Precursors { get; set; }
            /// <summary>
            /// The two log10 observed-intensity boundaries between the low / mid / high classes:
            /// the run's intensity terciles.
            /// </summary>
            public double[] IntensityEdges { get; set; }
            /// <summary>One entry per <see cref="MASS_ERROR_CLASSES"/> name, in that order.</summary>
            public List<FragmentMassErrorClass> Classes { get; set; }
        }

        /// <summary>One intensity class's histogram and the statistics the report draws from it.</summary>
        public sealed class FragmentMassErrorClass
        {
            public string Name { get; set; }
            public int[] Counts { get; set; }
            public int Total { get; set; }
            /// <summary>Median of the binned errors.</summary>
            public double Median { get; set; }
            /// <summary>1.4826 x the median absolute deviation from <see cref="Median"/>.</summary>
            public double RobustSd { get; set; }
            /// <summary>Mean over the core: errors within 4 robust SDs of the median.</summary>
            public double Mean { get; set; }
            /// <summary>Standard deviation over the same core.</summary>
            public double Sd { get; set; }
        }

        /// <summary>
        /// Bin one run's measured errors into its fixed-bin summary, splitting by observed
        /// intensity terciles. <paramref name="errors"/> and <paramref name="log10Intensities"/>
        /// are parallel; an error outside the window is dropped. Pure; unit-tested directly.
        /// </summary>
        public static FragmentMassErrorRun BuildFragmentMassErrorRun(FragmentMassErrorRun header,
            IReadOnlyList<double> errors, IReadOnlyList<double> log10Intensities)
        {
            var sorted = log10Intensities.OrderBy(v => v).ToArray();
            double lowEdge = sorted.Length > 0 ? sorted[sorted.Length / 3] : 0;
            double highEdge = sorted.Length > 0 ? sorted[2 * sorted.Length / 3] : 0;
            header.IntensityEdges = new[] { lowEdge, highEdge };
            var counts = MASS_ERROR_CLASSES.Select(_ => new int[header.BinCount]).ToArray();
            for (int i = 0; i < errors.Count; i++)
            {
                int bin = MassErrorBin(errors[i], header.HalfWidth, header.BinCount);
                if (bin < 0)
                    continue;
                double intensity = log10Intensities[i];
                int intensityClass = intensity < lowEdge ? 0 : intensity < highEdge ? 1 : 2;
                counts[intensityClass][bin]++;
                counts[MASS_ERROR_CLASSES.Length - 1][bin]++;
            }
            header.Classes = BuildMassErrorClasses(counts, header);
            return header;
        }

        /// <summary>
        /// The bin <paramref name="error"/> falls in, or -1 outside the window.
        /// </summary>
        public static int MassErrorBin(double error, double halfWidth, int binCount)
        {
            if (double.IsNaN(error) || error < -halfWidth || error >= halfWidth)
                return -1;
            int bin = (int)Math.Floor((error + halfWidth) / (2 * halfWidth) * binCount);
            return Math.Min(Math.Max(bin, 0), binCount - 1);
        }

        /// <summary>
        /// One class's statistics from its bins (at bin centers): median and robust SD, then
        /// the plain mean and SD over the core within 4 robust SDs of the median - the uniform
        /// floor of nearest-noise-peak matches across the wide window would otherwise dominate
        /// the plain SD.
        /// </summary>
        public static FragmentMassErrorClass ComputeMassErrorStats(string name, int[] counts, double halfWidth)
        {
            int n = counts.Length;
            double width = 2 * halfWidth / n;
            var centers = Enumerable.Range(0, n).Select(i => -halfWidth + (i + 0.5) * width).ToArray();
            int total = counts.Sum();
            var result = new FragmentMassErrorClass
            {
                Name = name, Counts = counts, Total = total,
                Median = double.NaN, RobustSd = double.NaN, Mean = double.NaN, Sd = double.NaN,
            };
            if (total == 0)
                return result;
            result.Median = WeightedMedian(centers, counts, total);
            var deviations = centers.Select(c => Math.Abs(c - result.Median)).ToArray();
            var order = Enumerable.Range(0, n).OrderBy(i => deviations[i]).ToArray();
            result.RobustSd = 1.4826 * WeightedMedian(order.Select(i => deviations[i]).ToArray(),
                order.Select(i => counts[i]).ToArray(), total);
            double coreHalf = result.RobustSd > 0 ? MASS_ERROR_CORE_ROBUST_SDS * result.RobustSd : halfWidth;
            double sum = 0, sumSq = 0;
            long coreCount = 0;
            for (int i = 0; i < n; i++)
            {
                if (counts[i] == 0 || deviations[i] > coreHalf)
                    continue;
                coreCount += counts[i];
                sum += counts[i] * centers[i];
                sumSq += counts[i] * centers[i] * centers[i];
            }
            if (coreCount == 0)
                return result;
            result.Mean = sum / coreCount;
            result.Sd = Math.Sqrt(Math.Max(0, sumSq / coreCount - result.Mean * result.Mean));
            return result;
        }

        private static List<FragmentMassErrorClass> BuildMassErrorClasses(int[][] counts, FragmentMassErrorRun header)
        {
            return MASS_ERROR_CLASSES.Select((name, c) =>
                ComputeMassErrorStats(name, counts[c], header.HalfWidth)).ToList();
        }

        /// <summary>The value at which the cumulative count first reaches half the total.</summary>
        private static double WeightedMedian(double[] values, int[] counts, int total)
        {
            double half = total / 2.0;
            long cumulative = 0;
            for (int i = 0; i < values.Length; i++)
            {
                cumulative += counts[i];
                if (cumulative >= half)
                    return values[i];
            }
            return values[values.Length - 1];
        }
    }
}
