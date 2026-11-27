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
using System.Linq;
using pwiz.Quasar.Core.Elution;
using pwiz.Quasar.Core.Traces;

namespace pwiz.Quasar.Core.Clustering
{
    /// <summary>A precursor the peptide-centric search (Osprey) found in this run, with its library fragments and boundaries.</summary>
    public sealed class KnownPrecursor
    {
        public KnownPrecursor(string peptide, double mz, int charge, double start, double end, double apex,
            double[] fragmentMzs)
        {
            Peptide = peptide;
            Mz = mz;
            Charge = charge;
            Start = start;
            End = end;
            Apex = apex;
            FragmentMzs = fragmentMzs;
        }

        public string Peptide { get; }
        public double Mz { get; }
        public int Charge { get; }

        /// <summary>The boundaries and apex in this run, in minutes.</summary>
        public double Start { get; }
        public double End { get; }
        public double Apex { get; }

        public double[] FragmentMzs { get; }

        public override string ToString()
        {
            return $@"{Peptide} {Mz:F4}/{Charge} at {Apex:F3}";
        }
    }

    /// <summary>
    /// Known templates (spec Stage 3a, <c>--features unassigned</c>): one EMG per known
    /// precursor, fitted by the pooled fit to the traces of its library fragments that have
    /// signal inside its boundaries. A precursor with fewer than two such traces, or whose fit
    /// fails, gets the run's typical shape at its apex, so the signal it explains is still taken
    /// out before new clusters form.
    /// </summary>
    public static class KnownTemplates
    {
        /// <summary>Tolerance for matching a library fragment to a trace, in ppm.</summary>
        public const double FRAGMENT_PPM = 10.0;

        /// <summary>The highest fragment charge an anchor, stored singly charged, is matched at.</summary>
        public const int MAX_ANCHOR_FRAGMENT_CHARGE = 2;

        public static List<EmgShape> Fit(double[] rt, IReadOnlyList<Trace> traces, IEnumerable<KnownPrecursor> precursors,
            WindowClusterer clusterer, WindowClusterSettings settings)
        {
            var typical = new EmgShape(0.0, settings.Prior.Sigma, settings.Prior.Tau);
            var byMz = Enumerable.Range(0, traces.Count).OrderBy(i => traces[i].Mz).ToArray();
            var sortedMzs = byMz.Select(i => traces[i].Mz).ToArray();
            var shapes = new List<EmgShape>();
            foreach (var precursor in precursors)
            {
                var initial = typical.WithModeAt(precursor.Apex);
                int first = LowerBound(rt, precursor.Start);
                int last = LowerBound(rt, precursor.End) - 1;
                var matched = new HashSet<int>();
                foreach (double fragment in precursor.FragmentMzs)
                {
                    foreach (int t in Near(sortedMzs, byMz, fragment))
                        matched.Add(t);
                    for (int z = 2; z <= MAX_ANCHOR_FRAGMENT_CHARGE; z++)
                    {
                        double singlyCharged = (fragment - TraceBuilder.PROTON) * z + TraceBuilder.PROTON;
                        foreach (int t in Near(sortedMzs, byMz, singlyCharged).Where(t => traces[t].IsAnchor))
                            matched.Add(t);
                    }
                }

                var signal = matched.Where(t => PositiveCount(traces[t].Intensities, first, last) >= settings.MinMemberPoints)
                    .Select(t => traces[t].Intensities)
                    .ToList();
                EmgShape shape = null;
                if (signal.Count >= 2)
                {
                    var (start, end) = clusterer.Segment(initial, rt);
                    shape = PooledEmgFit.Fit(rt, signal, start, end, settings.Noise, settings.Prior, initial)?.Shape;
                }

                shapes.Add(shape ?? initial);
            }

            return shapes;
        }

        /// <summary>The traces (indices) whose m/z is within <see cref="FRAGMENT_PPM"/> of <paramref name="mz"/>.</summary>
        private static IEnumerable<int> Near(double[] sortedMzs, int[] byMz, double mz)
        {
            double tolerance = mz * FRAGMENT_PPM * 1e-6;
            for (int i = LowerBound(sortedMzs, mz - tolerance); i < sortedMzs.Length && sortedMzs[i] <= mz + tolerance; i++)
                yield return byMz[i];
        }

        private static int PositiveCount(double[] values, int first, int last)
        {
            int count = 0;
            for (int i = Math.Max(0, first); i <= last && i < values.Length; i++)
            {
                if (values[i] > 0.0)
                    count++;
            }

            return count;
        }

        private static int LowerBound(double[] values, double x)
        {
            int lo = 0, hi = values.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (values[mid] < x)
                    lo = mid + 1;
                else
                    hi = mid;
            }

            return lo;
        }
    }
}
