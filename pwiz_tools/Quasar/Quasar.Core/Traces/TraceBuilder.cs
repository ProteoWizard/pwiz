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
using pwiz.Quasar.Core.Clustering;
using pwiz.Quasar.Features;

namespace pwiz.Quasar.Core.Traces
{
    /// <summary>The settings of Stage 1 (spec, Stage 1: Traces).</summary>
    public sealed class TraceBuilderSettings
    {
        public double PpmTolerance { get; set; } = 10.0;

        /// <summary>A centroid trace may skip this many scans in a row and continue.</summary>
        public int GapTolerance { get; set; } = 1;

        /// <summary>Traces seen in fewer scans are dropped.</summary>
        public int MinScans { get; set; } = 3;

        /// <summary>
        /// Centroid traces whose highest point is below this quantile of the window's centroid
        /// intensities (in ions) are dropped. A setting, because the low-abundance tail is where the
        /// unassigned peptides are likely to be.
        /// </summary>
        public double IntensityFloorQuantile { get; set; } = 0.2;

        public KronikSettings Kronik { get; set; } = KronikSettings.Default;
    }

    /// <summary>
    /// Stage 1 for one isolation window: turns its scans into <see cref="Trace"/>s on the
    /// window's scan axis, in ion counts.
    /// <list type="number">
    /// <item>Anchor traces: Hardklor envelopes linked across scans by Kronik, one trace per
    /// persistent feature, at the singly charged monoisotopic m/z. The envelope's intensity
    /// already sums its isotopes; this is the deconvolution.</item>
    /// <item>Centroid traces: every peak no kept envelope claims, linked across scans at
    /// <see cref="TraceBuilderSettings.PpmTolerance"/> with up to
    /// <see cref="TraceBuilderSettings.GapTolerance"/> missed scans, as Kronik links envelopes.</item>
    /// </list>
    /// </summary>
    public static class TraceBuilder
    {
        /// <summary>Proton mass, for the singly charged m/z of a neutral monoisotopic mass.</summary>
        public const double PROTON = 1.007276467;

        /// <summary>Builds the traces of one window from its scans in time order.</summary>
        public static List<Trace> Build(IReadOnlyList<WindowScan> scans, TraceBuilderSettings settings)
        {
            var traces = new List<Trace>();
            var claimed = AddAnchorTraces(scans, settings, traces);
            AddCentroidTraces(scans, settings, claimed, traces);
            return traces;
        }

        /// <summary>
        /// Adds one anchor trace per Kronik feature and returns, per scan, the peaks its envelopes
        /// claim, so the centroid traces do not count them a second time.
        /// </summary>
        private static HashSet<int>[] AddAnchorTraces(IReadOnlyList<WindowScan> scans, TraceBuilderSettings settings,
            List<Trace> traces)
        {
            var keys = scans.Select(s => (IReadOnlyList<EnvelopeKey>)s.Envelopes
                .Select(e => new EnvelopeKey(e.MonoMass, e.Charge, e.Intensity)).ToList()).ToList();
            var linked = Kronik.Link(keys, settings.Kronik);
            var claimed = scans.Select(_ => new HashSet<int>()).ToArray();
            var anchorIntensities = linked.Features.Select(_ => new double[scans.Count]).ToArray();
            for (int s = 0; s < scans.Count; s++)
            {
                var featureOf = linked.FeatureOf[s];
                for (int e = 0; e < featureOf.Length; e++)
                {
                    int feature = featureOf[e];
                    if (feature < 0)
                        continue;
                    var envelope = scans[s].Envelopes[e];
                    anchorIntensities[feature][s] += envelope.Intensity * scans[s].IonsPerIntensity;
                    claimed[s].UnionWith(envelope.PeakIndices);
                }
            }

            for (int f = 0; f < linked.Features.Count; f++)
            {
                var feature = linked.Features[f];
                traces.Add(new Trace(traces.Count, true, feature.MonoMass + PROTON, feature.Charge, anchorIntensities[f]));
            }

            return claimed;
        }

        /// <summary>Links the unclaimed peaks of every scan into centroid traces and adds those that pass the filters.</summary>
        private static void AddCentroidTraces(IReadOnlyList<WindowScan> scans, TraceBuilderSettings settings,
            HashSet<int>[] claimed, List<Trace> traces)
        {
            var finished = new List<OpenTrace>();
            var open = new List<OpenTrace>();
            var allIntensities = new List<double>();
            for (int s = 0; s < scans.Count; s++)
            {
                var scan = scans[s];
                var extended = new HashSet<OpenTrace>();
                var started = new List<OpenTrace>();
                var openMzs = open.Select(t => t.LastMz).ToArray();
                for (int p = 0; p < scan.Mzs.Length; p++)
                {
                    if (claimed[s].Contains(p) || scan.Intensities[p] <= 0.0)
                        continue;
                    double mz = scan.Mzs[p];
                    double ions = scan.Intensities[p] * scan.IonsPerIntensity;
                    allIntensities.Add(ions);
                    var match = NearestOpen(open, openMzs, mz, settings.PpmTolerance, extended);
                    if (match != null)
                    {
                        match.Add(s, mz, ions);
                        extended.Add(match);
                    }
                    else
                    {
                        var trace = new OpenTrace();
                        trace.Add(s, mz, ions);
                        started.Add(trace);
                    }
                }

                // Traces not extended for more than the gap tolerance are closed.
                var stillOpen = new List<OpenTrace>();
                foreach (var trace in open)
                {
                    if (s - trace.LastScan > settings.GapTolerance)
                        finished.Add(trace);
                    else
                        stillOpen.Add(trace);
                }

                stillOpen.AddRange(started);
                open = stillOpen.OrderBy(t => t.LastMz).ToList();
            }

            finished.AddRange(open);
            double floor = Quantile(allIntensities, settings.IntensityFloorQuantile);
            foreach (var trace in finished)
            {
                if (trace.ScanCount < settings.MinScans || trace.MaxIons < floor)
                    continue;
                traces.Add(trace.ToTrace(traces.Count, scans.Count));
            }
        }

        /// <summary>
        /// The open trace nearest <paramref name="mz"/> within the tolerance that has not taken a
        /// peak from this scan yet, or null.
        /// </summary>
        private static OpenTrace NearestOpen(List<OpenTrace> open, double[] openMzs, double mz, double ppm,
            HashSet<OpenTrace> extended)
        {
            double tolerance = mz * ppm * 1e-6;
            int i = Array.BinarySearch(openMzs, mz);
            if (i < 0)
                i = ~i;
            OpenTrace best = null;
            double bestDistance = tolerance;
            for (int j = Math.Max(0, i - 2); j < Math.Min(openMzs.Length, i + 2); j++)
            {
                double distance = Math.Abs(openMzs[j] - mz);
                if (distance <= bestDistance && !extended.Contains(open[j]))
                {
                    best = open[j];
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static double Quantile(List<double> values, double quantile)
        {
            if (values.Count == 0)
                return 0.0;
            var sorted = values.ToArray();
            Array.Sort(sorted);
            int index = (int)Math.Floor(quantile * (sorted.Length - 1));
            return sorted[Math.Max(0, Math.Min(sorted.Length - 1, index))];
        }

        /// <summary>A centroid trace being linked: its points so far.</summary>
        private sealed class OpenTrace
        {
            private readonly List<int> _scans = new List<int>();
            private readonly List<double> _ions = new List<double>();
            private double _weightedMz;
            private double _weight;

            public int LastScan => _scans[_scans.Count - 1];
            public double LastMz { get; private set; }
            public int ScanCount => _scans.Count;
            public double MaxIons => _ions.Max();

            public void Add(int scan, double mz, double ions)
            {
                _scans.Add(scan);
                _ions.Add(ions);
                LastMz = mz;
                _weightedMz += mz * ions;
                _weight += ions;
            }

            /// <summary>The trace on the full scan axis, at its intensity-weighted mean m/z.</summary>
            public Trace ToTrace(int id, int scanCount)
            {
                var intensities = new double[scanCount];
                for (int i = 0; i < _scans.Count; i++)
                    intensities[_scans[i]] += _ions[i];
                return new Trace(id, false, _weight > 0.0 ? _weightedMz / _weight : LastMz, 0, intensities);
            }
        }
    }
}
