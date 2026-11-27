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
using pwiz.Quasar.Core.Output;
using pwiz.Quasar.Core.Traces;

namespace pwiz.Quasar.Core.Clustering
{
    /// <summary>
    /// Precursor candidates for a cluster (spec Stage 3f), each an isotope envelope of charge 2 or
    /// more that reaches into the isolation window:
    /// <list type="bullet">
    /// <item>Tier 1, an MS1 envelope whose isotope peaks peak with the template and whose profiles
    /// it explains (the Stage 3b test).</item>
    /// <item>Tier 2, an unfragmented precursor in MS2: an anchor member of the cluster, which the
    /// template already explains.</item>
    /// </list>
    /// </summary>
    public static class PrecursorFinder
    {
        public const int TIER_MS1 = 1;
        public const int TIER_UNFRAGMENTED = 2;
        public const int MIN_CHARGE = 2;

        /// <summary>
        /// Isotope peaks above the monoisotopic one that may be the part of the envelope inside the
        /// window: the window edge can clip the monoisotopic peak.
        /// </summary>
        public const int CLIPPED_ISOTOPES = 2;

        public const double ISOTOPE_SPACING = 1.0033548;

        public const int MAX_CHARGE = 4;

        /// <summary>Tolerance for an isotope peak's m/z, in ppm.</summary>
        public const double ISOTOPE_PPM = 10.0;

        /// <summary>
        /// The tier 1 candidates of <paramref name="cluster"/>, found with its template: the MS1
        /// centroid traces (<paramref name="ms1Traces"/>, on the MS1 scan axis
        /// <paramref name="ms1Rt"/>) that peak with the template and that it explains (the Stage 3b
        /// test), grouped into isotope series by their spacing, which gives the charge. A series
        /// whose envelope reaches into the isolation window [<paramref name="low"/>,
        /// <paramref name="high"/>] is a candidate at its lowest peak. Finding the envelopes
        /// first, with Hardklor and Kronik on the MS1 scans, found the envelope of only 91 of
        /// Osprey's 205 precursors in an Astral window, and a Kronik feature peaking within the
        /// apex tolerance for 9.
        /// </summary>
        public static IReadOnlyList<PrecursorCandidate> FromMs1(Cluster cluster, double[] ms1Rt,
            IEnumerable<Trace> ms1Traces, double low, double high, WindowClusterer clusterer,
            WindowClusterSettings settings)
        {
            var shape = cluster.Template.Shape;
            var (start, end) = clusterer.Segment(shape, ms1Rt);
            var explained = new List<double>();
            foreach (var trace in ms1Traces)
            {
                int apex = -1;
                for (int i = start; i <= end; i++)
                {
                    if (trace.Intensities[i] > 0.0 && (apex < 0 || trace.Intensities[i] > trace.Intensities[apex]))
                        apex = i;
                }
                if (apex < 0 || Math.Abs(ms1Rt[apex] - shape.Mode) > settings.ApexTolerance)
                    continue;

                var fit = TemplateFit.Fit(ms1Rt, trace.Intensities, start, end, new[] { shape }, settings.Noise);
                if (fit.IsMember(0, settings.MembershipThreshold))
                    explained.Add(trace.Mz);
            }

            explained.Sort();
            var candidates = new List<PrecursorCandidate>();
            foreach (double mz in explained)
            {
                for (int z = MIN_CHARGE; z <= MAX_CHARGE; z++)
                {
                    // The lowest explained peak of a series of at least two is its monoisotopic peak.
                    if (Contains(explained, mz + ISOTOPE_SPACING / z) && !Contains(explained, mz - ISOTOPE_SPACING / z) &&
                        InWindow(mz, z, low, high))
                        candidates.Add(new PrecursorCandidate(mz, z, TIER_MS1));
                }
            }

            return candidates;
        }

        /// <summary>The m/z range of MS1 peaks that can belong to a candidate for the isolation window [<paramref name="low"/>, <paramref name="high"/>].</summary>
        public static (double Low, double High) Ms1Range(double low, double high)
        {
            return (low - CLIPPED_ISOTOPES * ISOTOPE_SPACING / MIN_CHARGE - 0.01,
                high + ISOTOPE_SPACING / MIN_CHARGE + 0.01);
        }

        /// <summary>
        /// The tier 2 candidates of <paramref name="cluster"/> for the isolation window
        /// [<paramref name="low"/>, <paramref name="high"/>], each with the member it came from, so
        /// that member can be left out of the candidate's spectrum.
        /// </summary>
        public static IReadOnlyList<(ClusterMember Member, PrecursorCandidate Candidate)> UnfragmentedInMs2(
            Cluster cluster, double low, double high)
        {
            var candidates = new List<(ClusterMember Member, PrecursorCandidate Candidate)>();
            foreach (var member in cluster.Members.Where(m => m.Trace.IsAnchor && m.Trace.Charge >= MIN_CHARGE))
            {
                double mono = MonoMz(member.Trace);
                if (InWindow(mono, member.Trace.Charge, low, high))
                    candidates.Add((member, new PrecursorCandidate(mono, member.Trace.Charge, TIER_UNFRAGMENTED)));
            }

            return candidates;
        }

        /// <summary>The monoisotopic m/z at its own charge of an anchor trace, which is stored singly charged.</summary>
        private static double MonoMz(Trace anchor)
        {
            return (anchor.Mz - TraceBuilder.PROTON) / anchor.Charge + TraceBuilder.PROTON;
        }

        /// <summary>Whether sorted <paramref name="mzs"/> has one within <see cref="ISOTOPE_PPM"/> of <paramref name="mz"/>.</summary>
        private static bool Contains(List<double> mzs, double mz)
        {
            double tolerance = mz * ISOTOPE_PPM * 1e-6;
            int i = mzs.BinarySearch(mz - tolerance);
            if (i < 0)
                i = ~i;
            return i < mzs.Count && mzs[i] <= mz + tolerance;
        }

        private static bool InWindow(double mono, int charge, double low, double high)
        {
            for (int i = 0; i <= CLIPPED_ISOTOPES; i++)
            {
                double mz = mono + i * ISOTOPE_SPACING / charge;
                if (low <= mz && mz <= high)
                    return true;
            }

            return false;
        }
    }
}
