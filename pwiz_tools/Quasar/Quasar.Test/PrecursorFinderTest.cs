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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Quasar.Core.Clustering;
using pwiz.Quasar.Core.Elution;
using pwiz.Quasar.Core.Traces;

namespace pwiz.Quasar.Test
{
    /// <summary>
    /// Precursor candidates (spec Stage 3f) for one cluster in the isolation window 643.54 to
    /// 646.54: MS1 isotope series (tier 1) that peak with the template and reach into the window,
    /// and anchor members of charge 2 or more inside it (tier 2).
    /// </summary>
    [TestClass]
    public class PrecursorFinderTest
    {
        private const double CYCLE_MINUTES = 1.14 / 60.0;
        private const double LOW = 643.54;
        private const double HIGH = 646.54;
        private const double AREA = 200.0;
        private static readonly EmgShape SHAPE = new EmgShape(0.0, 0.018, 0.009).WithModeAt(20.0);

        [TestMethod]
        public void TestPrecursorCandidates()
        {
            // The MS1 scans fall half a cycle from the window's MS2 scans.
            var ms1Rt = Enumerable.Range(0, 120).Select(i => 19.0 + (i + 0.5) * CYCLE_MINUTES).ToArray();
            var settings = new WindowClusterSettings(new EmgShapePrior(SHAPE.Sigma, SHAPE.Tau), new NoiseModel(1.0, 1.0),
                CYCLE_MINUTES);
            var clusterer = new WindowClusterer(settings);
            var precursorMember = Member(Anchor(10, 645.30, 2, SHAPE, ms1Rt));
            var cluster = new Cluster(new ElutionTemplate(0, SHAPE, new[] { 10 }, TemplateSource.anchors),
                new[] { precursorMember, Member(Anchor(11, 500.25, 1, SHAPE, ms1Rt)), Member(Anchor(12, 900.40, 2, SHAPE, ms1Rt)) },
                ClusterRejection.none);

            // MS1 centroid traces: isotope series and single peaks.
            int id = 100;
            var ms1Traces = new[]
                {
                    Envelope(ref id, 645.00, 2, SHAPE, ms1Rt),                     // co-eluting, in the window
                    Envelope(ref id, 644.10, 3, SHAPE, ms1Rt),                     // co-eluting, charge 3
                    Envelope(ref id, 645.80, 2, SHAPE.WithModeAt(20.17), ms1Rt),   // in the window, 10 s later
                    Envelope(ref id, 640.00, 2, SHAPE, ms1Rt),                     // co-eluting, below the window
                    Envelope(ref id, 643.00, 2, SHAPE, ms1Rt),                     // monoisotopic peak clipped by the window edge
                    new[] { Centroid(ref id, 646.20, SHAPE, ms1Rt) }               // co-eluting, no isotope partner
                }
                .SelectMany(t => t).ToList();
            var tier1 = PrecursorFinder.FromMs1(cluster, ms1Rt, ms1Traces, LOW, HIGH, clusterer, settings);
            CollectionAssert.AreEqual(new[] { (643.00, 2), (644.10, 3), (645.00, 2) },
                tier1.Select(c => (Math.Round(c.Mz, 6), c.Charge)).ToArray());
            Assert.IsTrue(tier1.All(c => c.Tier == PrecursorFinder.TIER_MS1));

            var tier2 = PrecursorFinder.UnfragmentedInMs2(cluster, LOW, HIGH);
            Assert.AreEqual(1, tier2.Count);
            Assert.AreSame(precursorMember, tier2[0].Member);
            Assert.AreEqual(645.30, tier2[0].Candidate.Mz, 1e-6);
            Assert.AreEqual(PrecursorFinder.TIER_UNFRAGMENTED, tier2[0].Candidate.Tier);
        }

        /// <summary>
        /// An anchor trace for an envelope whose monoisotopic m/z is <paramref name="mono"/> at
        /// <paramref name="charge"/>, stored singly charged as Stage 1 stores anchors.
        /// </summary>
        private static Trace Anchor(int id, double mono, int charge, EmgShape shape, double[] rt)
        {
            double singlyCharged = (mono - TraceBuilder.PROTON) * charge + TraceBuilder.PROTON;
            return new Trace(id, true, singlyCharged, charge, rt.Select(t => AREA * shape.Density(t)).ToArray());
        }

        /// <summary>Three MS1 centroid traces: the isotope peaks of an envelope of monoisotopic m/z <paramref name="mono"/> at <paramref name="charge"/>.</summary>
        private static Trace[] Envelope(ref int id, double mono, int charge, EmgShape shape, double[] rt)
        {
            var peaks = new Trace[3];
            for (int i = 0; i < peaks.Length; i++)
                peaks[i] = Centroid(ref id, mono + i * PrecursorFinder.ISOTOPE_SPACING / charge, shape, rt);
            return peaks;
        }

        private static Trace Centroid(ref int id, double mz, EmgShape shape, double[] rt)
        {
            return new Trace(id++, false, mz, 0, rt.Select(t => AREA * shape.Density(t)).ToArray());
        }

        private static ClusterMember Member(Trace trace)
        {
            return new ClusterMember(trace, AREA, 100.0, 1.0);
        }
    }
}
