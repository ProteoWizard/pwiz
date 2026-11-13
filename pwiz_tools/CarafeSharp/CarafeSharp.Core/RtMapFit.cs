/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Osprey's RT calibration (pwiz_tools/Osprey: Osprey.Tasks Calibrator.SelectFitPlan,
 *   Osprey.Chromatography LoessRegression and RTCalibration's Theil-Sen line)
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
using System.Linq;

namespace pwiz.CarafeSharp.Core
{
    /// <summary>
    /// A run's monotone map from its minutes to the hydrophobic index, as firm as its peptides allow, in tiers as Osprey
    /// calibrates RT (<c>Calibrator.SelectFitPlan</c>):
    /// <list type="bullet">
    /// <item>enough peptide forms for a density (<see cref="DEFAULT_KDE_MIN_POINTS"/>): Chronologer's KDE ridge
    /// (<see cref="KdeRidgeAlignment"/>);</item>
    /// <item>fewer: a robust LOESS, its window <see cref="LOESS_BANDWIDTH"/> of the forms down to
    /// <see cref="LOESS_FULL_POINTS"/> of them, and from there the same number of forms, a wider fraction, so the curve
    /// stiffens toward a line as they thin;</item>
    /// <item>below <see cref="LINEAR_FIT_MAX_POINTS"/>: a Theil-Sen line, when the forms span enough of the gradient
    /// (<see cref="MIN_LINEAR_FIT_SPAN_FRACTION"/>) to fix its slope.</item>
    /// </list>
    /// A LOESS curve is made monotone by pooling adjacent violators.
    /// </summary>
    public static class RtMapFit
    {
        /// <summary>
        /// The fewest peptide forms the KDE ridge is fitted on. Across gradients it puts a peptide at one HI better than a
        /// LOESS at every size (Astral 24 min and ZT Scan 11.4 min: 95% of shared forms within 0.27 HI on all forms, 0.30
        /// on 1000, a LOESS 0.47 and 0.45), whose wide window bends differently on gradients of different shape; on
        /// replicates of one gradient the two agree on all forms, but the density grows noisier as the forms thin (three
        /// Astral runs: 95% within 0.115 min on 1000 forms, a LOESS 0.094; on 100, 0.26, worse than unaligned at 0.185).
        /// </summary>
        public const int DEFAULT_KDE_MIN_POINTS = 1000;

        /// <summary>Osprey's LOESS bandwidth: the fraction of the points each local line is fitted on.</summary>
        public const double LOESS_BANDWIDTH = 0.3;

        /// <summary>Osprey's MinCalibrationPoints: below it, the LOESS window keeps its 60 points, a wider fraction.</summary>
        public const int LOESS_FULL_POINTS = 200;

        /// <summary>Osprey's LINEAR_FIT_MAX_POINTS: a LOESS window on fewer holds too few points to bend on.</summary>
        public const int LINEAR_FIT_MAX_POINTS = 100;

        /// <summary>Osprey's MIN_LINEAR_FIT_POINTS: the fewest forms a line's median slope means anything on.</summary>
        public const int MIN_LINEAR_FIT_POINTS = 15;

        /// <summary>Osprey's MIN_LINEAR_FIT_RT_SPAN_FRACTION: the part of the gradient a line's forms must span.</summary>
        public const double MIN_LINEAR_FIT_SPAN_FRACTION = 0.5;

        /// <summary>Osprey's bisquare robustness iterations.</summary>
        public const int ROBUSTNESS_ITERATIONS = 2;

        /// <summary>The correlation of minutes and HI below which the KDE tier takes the HI for not rising.</summary>
        public const double MIN_KDE_CORRELATION = 0.5;

        /// <summary>
        /// The map of a run's points, each a peptide form's minutes and HI, and how it was fitted. Throws
        /// <see cref="InvalidOperationException"/> when the points cannot fix one: minutes or HI that do not vary, a
        /// line's span less than <see cref="MIN_LINEAR_FIT_SPAN_FRACTION"/> of <paramref name="gradientMinutes"/>, or HI
        /// that does not rise.
        /// </summary>
        /// <param name="minutes">Each point's retention time in the run.</param>
        /// <param name="hi">Each point's hydrophobic index.</param>
        /// <param name="gradientMinutes">The run's length, which a line's points must span half of.</param>
        /// <param name="kdeMinPoints">The fewest points the KDE ridge is fitted on.</param>
        public static (MonotoneMap Map, string Fit) Fit(IReadOnlyList<double> minutes, IReadOnlyList<double> hi, double gradientMinutes,
            int kdeMinPoints = DEFAULT_KDE_MIN_POINTS)
        {
            if (minutes.Count != hi.Count)
                throw new ArgumentException(string.Format(@"{0} minutes for {1} HI.", minutes.Count, hi.Count));
            int n = minutes.Count;
            if (n < 2 || !(minutes.Max() > minutes.Min()) || !(hi.Max() > hi.Min()))
                throw new InvalidOperationException(string.Format(@"the minutes or HI of its {0} peptide forms do not vary", n));
            if (n >= kdeMinPoints)
            {
                // The ridge walk only steps up in minutes and HI, so it rises a little even through HI that falls; the
                // forms themselves must follow one another (real runs: r about 0.98).
                double r = Correlation(minutes, hi);
                if (!(r > MIN_KDE_CORRELATION))
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                        @"the HI of its {0} peptide forms does not rise with their minutes (r {1:F2})", n, r));
                }
                return (KdeRidgeAlignment.Fit(minutes, hi), string.Format(CultureInfo.InvariantCulture, @"KDE ridge on {0} peptide forms", n));
            }

            // By minutes, then HI, so equal minutes fit in one order whatever order the points came in.
            int[] order = Enumerable.Range(0, n).OrderBy(i => minutes[i]).ThenBy(i => hi[i]).ToArray();
            double[] x = order.Select(i => minutes[i]).ToArray();
            double[] y = order.Select(i => hi[i]).ToArray();
            if (n < LINEAR_FIT_MAX_POINTS)
            {
                double span = x[n - 1] - x[0];
                if (!(span >= MIN_LINEAR_FIT_SPAN_FRACTION * gradientMinutes))
                {
                    throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                        @"its {0} peptide forms span {1:F1} of its {2:F1} minutes, too little to fix a line's slope", n, span, gradientMinutes));
                }
                var (slope, intercept) = TheilSen(x, y);
                if (!(slope > 0))
                    throw new InvalidOperationException(string.Format(@"the HI of its {0} peptide forms does not rise with their minutes", n));
                return (new MonotoneMap(new[] { x[0], x[n - 1] }, new[] { intercept + slope * x[0], intercept + slope * x[n - 1] }),
                    string.Format(CultureInfo.InvariantCulture, @"Theil-Sen line on {0} peptide forms", n));
            }
            double bandwidth = Math.Min(1.0, Math.Max(LOESS_BANDWIDTH, LOESS_BANDWIDTH * LOESS_FULL_POINTS / n));
            var map = Monotone(x, Loess(x, y, bandwidth));
            if (map == null)
                throw new InvalidOperationException(string.Format(@"the HI of its {0} peptide forms does not rise with their minutes", n));
            return (map, string.Format(CultureInfo.InvariantCulture, @"LOESS (bandwidth {0:F2}) on {1} peptide forms", bandwidth, n));
        }

        /// <summary>
        /// A robust local-line fit at each of the sorted <paramref name="x"/>: tricube weights over the nearest
        /// <paramref name="bandwidth"/> of the points, then bisquare weights of each point's residual (Cleveland's, refit
        /// <see cref="ROBUSTNESS_ITERATIONS"/> times), as Osprey's LoessRegression fits RT.
        /// </summary>
        public static double[] Loess(IReadOnlyList<double> x, IReadOnlyList<double> y, double bandwidth)
        {
            int n = x.Count;
            int k = Math.Min(n, Math.Max(3, (int)Math.Ceiling(bandwidth * n)));
            var fitted = LocalLines(x, y, k, null);
            var weights = new double[n];
            for (int iteration = 0; iteration < ROBUSTNESS_ITERATIONS; iteration++)
            {
                double[] residuals = Enumerable.Range(0, n).Select(i => Math.Abs(y[i] - fitted[i])).ToArray();
                double scale = 6 * Statistics.Median(residuals);
                if (!(scale > 1e-10))
                    break;
                for (int i = 0; i < n; i++)
                {
                    double u = residuals[i] / scale;
                    double t = 1 - u * u;
                    weights[i] = u < 1 ? t * t : 0;
                }
                fitted = LocalLines(x, y, k, weights);
            }
            return fitted;
        }

        /// <summary>The Theil-Sen line: the median slope of every pair of distinct x, and the median intercept at it.</summary>
        public static (double Slope, double Intercept) TheilSen(IReadOnlyList<double> x, IReadOnlyList<double> y)
        {
            var slopes = new List<double>();
            for (int i = 0; i < x.Count; i++)
            {
                for (int j = i + 1; j < x.Count; j++)
                {
                    if (x[j] != x[i])
                        slopes.Add((y[j] - y[i]) / (x[j] - x[i]));
                }
            }
            double slope = slopes.Count > 0 ? Statistics.Median(slopes) : 0;
            return (slope, Statistics.Median(Enumerable.Range(0, x.Count).Select(i => y[i] - slope * x[i]).ToArray()));
        }

        /// <summary>
        /// The map through a curve's points, sorted by x, made monotone by pooling adjacent violators: points of equal x
        /// are first one point at their mean y, then adjacent blocks whose mean y does not rise are pooled until every
        /// block's is above the one before, and each block is one knot at its mean x and mean y. The map rises strictly,
        /// so it has no flat stretch for <see cref="MonotoneMap.Invert"/> to take for padding. Null when the y does not
        /// rise at all.
        /// </summary>
        public static MonotoneMap Monotone(IReadOnlyList<double> x, IReadOnlyList<double> y)
        {
            var blocks = new List<(double SumX, double SumY, int Count)>();
            for (int i = 0; i < x.Count; )
            {
                // Equal x are one point, at their mean y.
                int end = i;
                double sumY = 0;
                while (end < x.Count && x[end] == x[i])
                    sumY += y[end++];
                blocks.Add((x[i], sumY / (end - i), 1));
                i = end;
                while (blocks.Count > 1 && !(MeanY(blocks[blocks.Count - 1]) > MeanY(blocks[blocks.Count - 2])))
                {
                    var upper = blocks[blocks.Count - 1];
                    var lower = blocks[blocks.Count - 2];
                    blocks.RemoveAt(blocks.Count - 1);
                    blocks[blocks.Count - 1] = (lower.SumX + upper.SumX, lower.SumY + upper.SumY, lower.Count + upper.Count);
                }
            }
            if (blocks.Count < 2)
                return null;
            return new MonotoneMap(blocks.Select(b => b.SumX / b.Count).ToArray(), blocks.Select(MeanY).ToArray());
        }

        private static double MeanY((double SumX, double SumY, int Count) block)
        {
            return block.SumY / block.Count;
        }

        /// <summary>Pearson's correlation of the two series.</summary>
        private static double Correlation(IReadOnlyList<double> x, IReadOnlyList<double> y)
        {
            double meanX = x.Average(), meanY = y.Average();
            double sxy = 0, sxx = 0, syy = 0;
            for (int i = 0; i < x.Count; i++)
            {
                sxy += (x[i] - meanX) * (y[i] - meanY);
                sxx += (x[i] - meanX) * (x[i] - meanX);
                syy += (y[i] - meanY) * (y[i] - meanY);
            }
            return sxy / Math.Sqrt(sxx * syy);
        }

        /// <summary>At each x, the weighted least-squares line through its <paramref name="k"/> nearest points.</summary>
        private static double[] LocalLines(IReadOnlyList<double> x, IReadOnlyList<double> y, int k, double[] robustness)
        {
            int n = x.Count;
            var fitted = new double[n];
            int lo = 0;
            for (int i = 0; i < n; i++)
            {
                // The window of the k nearest slides right while its right neighbour is nearer than its left end.
                while (lo + k < n && x[lo + k] - x[i] < x[i] - x[lo])
                    lo++;
                int hi = lo + k;
                double reach = Math.Max(x[i] - x[lo], x[hi - 1] - x[i]);
                double sw = 0, swx = 0, swy = 0, swxx = 0, swxy = 0;
                for (int j = lo; j < hi; j++)
                {
                    double u = reach > 1e-10 ? Math.Abs(x[j] - x[i]) / reach : 0;
                    double t = u < 1 ? 1 - u * u * u : 0;
                    double w = t * t * t * (robustness?[j] ?? 1);
                    sw += w;
                    swx += w * x[j];
                    swy += w * y[j];
                    swxx += w * x[j] * x[j];
                    swxy += w * x[j] * y[j];
                }
                double det = sw * swxx - swx * swx;
                if (Math.Abs(det) > 1e-10)
                    fitted[i] = ((swxx * swy - swx * swxy) + (sw * swxy - swx * swy) * x[i]) / det;
                else
                    fitted[i] = sw > 1e-10 ? swy / sw : y[i];
            }
            return fitted;
        }

    }
}
