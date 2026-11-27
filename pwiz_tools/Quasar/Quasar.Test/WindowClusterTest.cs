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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Quasar.Core.Clustering;
using pwiz.Quasar.Core.Elution;

namespace pwiz.Quasar.Test
{
    /// <summary>
    /// Template-first clustering of a synthetic Astral-like window (spec Stages 2c to 3d): three
    /// peptides, two of them 6 s apart, a fragment shared by those two, an ion common to all three,
    /// and traces with no peptide signal. Each window is sampled every 1.1 s, peaks are about
    /// 4 s wide, and every point has Poisson counting noise on a small baseline.
    /// </summary>
    [TestClass]
    public class WindowClusterTest
    {
        private const double CYCLE_MINUTES = 1.1 / 60.0;
        private const double BASELINE_IONS = 2.0;
        private static readonly EmgShape TYPICAL = new EmgShape(0.0, 0.028, 0.014);

        /// <summary>Areas (ions x minutes) of each peptide's three anchors, then five centroid fragments.</summary>
        private static readonly double[] ANCHOR_AREAS = { 30.0, 22.0, 15.0 };
        private static readonly double[] CENTROID_AREAS = { 25.0, 18.0, 12.0, 9.0, 7.0 };

        [TestMethod]
        public void TestWindowClustering()
        {
            var window = new SyntheticWindow(new Random(3));
            var clusterer = new WindowClusterer(Settings());

            // Every peptide gets one accepted cluster with all of its traces, and no noise trace
            // joins any of them.
            var result = clusterer.Run(window.Rt, window.Traces, Array.Empty<EmgShape>());
            Assert.AreEqual(window.Peptides.Length, result.Templates.Count, string.Join(@"; ", result.Templates));
            var accepted = result.Clusters.Where(c => c.IsAccepted).ToList();
            Assert.AreEqual(3, accepted.Count, string.Join(@"; ", result.Clusters));
            var byPeptide = window.Peptides.Select(p => ClusterAt(accepted, p.Mode)).ToArray();
            for (int p = 0; p < window.Peptides.Length; p++)
            {
                var memberIds = byPeptide[p].Members.Select(m => m.Trace.Id).ToHashSet();
                foreach (int id in window.Peptides[p].TraceIds)
                    Assert.IsTrue(memberIds.Contains(id), @"peptide " + p + @" missing trace " + id);
                Assert.IsFalse(memberIds.Overlaps(window.NoiseTraceIds), @"noise trace in peptide " + p);
                Assert.AreEqual(ANCHOR_AREAS.Length + (p < 2 ? 1 : 0), byPeptide[p].AnchorCount);
            }

            // The shared fragment is split between peptides 0 and 1 at about its true areas.
            var sharedIn0 = Member(byPeptide[0], window.SharedTraceId);
            var sharedIn1 = Member(byPeptide[1], window.SharedTraceId);
            Assert.AreEqual(SyntheticWindow.SHARED_AREAS[0], sharedIn0.Amplitude, SyntheticWindow.SHARED_AREAS[0] * 0.25);
            Assert.AreEqual(SyntheticWindow.SHARED_AREAS[1], sharedIn1.Amplitude, SyntheticWindow.SHARED_AREAS[1] * 0.25);
            Assert.IsTrue(sharedIn0.IsShared && sharedIn1.IsShared);

            // The ion common to all three peptides is a member of each, without joining them.
            foreach (var cluster in byPeptide)
                Assert.IsNotNull(Member(cluster, window.CommonTraceId));

            // With peptide 0 known (as Osprey's precursors are in --features unassigned mode), only
            // the other two become clusters, and peptide 0's traces seed nothing new.
            var known = new[] { window.Peptides[0].Shape };
            var unassigned = clusterer.Run(window.Rt, window.Traces, known);
            Assert.IsTrue(unassigned.Templates.Any(t => t.Source == TemplateSource.known));
            var newAccepted = unassigned.Clusters.Where(c => c.IsAccepted).ToList();
            Assert.AreEqual(2, newAccepted.Count, string.Join(@"; ", unassigned.Clusters));
            Assert.IsNotNull(ClusterAt(newAccepted, window.Peptides[1].Mode));
            Assert.IsNotNull(ClusterAt(newAccepted, window.Peptides[2].Mode));
            Assert.IsFalse(unassigned.Clusters.Any(c => Math.Abs(c.Template.Shape.Mode - window.Peptides[0].Mode) <
                                                         0.5 * CYCLE_MINUTES));
        }

        private static WindowClusterSettings Settings()
        {
            return new WindowClusterSettings(new EmgShapePrior(TYPICAL.Sigma, TYPICAL.Tau),
                new NoiseModel(1.0, 1.0), CYCLE_MINUTES);
        }

        /// <summary>The cluster whose template apex is within half a peak width of <paramref name="mode"/>.</summary>
        private static Cluster ClusterAt(IEnumerable<Cluster> clusters, double mode)
        {
            var match = clusters.Where(c => Math.Abs(c.Template.Shape.Mode - mode) < 0.03)
                .OrderBy(c => Math.Abs(c.Template.Shape.Mode - mode))
                .FirstOrDefault();
            Assert.IsNotNull(match, @"no cluster at " + mode);
            return match;
        }

        private static ClusterMember Member(Cluster cluster, int traceId)
        {
            var member = cluster.Members.FirstOrDefault(m => m.Trace.Id == traceId);
            Assert.IsNotNull(member, @"trace " + traceId + @" not in " + cluster);
            return member;
        }

        /// <summary>A peptide in the synthetic window: its elution shape and its own traces.</summary>
        private sealed class SyntheticPeptide
        {
            public SyntheticPeptide(EmgShape shape)
            {
                Shape = shape;
                Mode = shape.Mode;
            }

            public EmgShape Shape { get; }
            public double Mode { get; }
            public List<int> TraceIds { get; } = new List<int>();
        }

        /// <summary>
        /// 200 scans of one window. Peptide apexes at 19.80, 19.90 (6 s later, about 1.4 peak
        /// widths) and 20.60 minutes, with slightly different widths.
        /// </summary>
        private sealed class SyntheticWindow
        {
            public static readonly double[] SHARED_AREAS = { 12.0, 10.0 };
            private static readonly double[] COMMON_AREAS = { 8.0, 6.0, 10.0 };

            private readonly Random _random;
            private readonly List<Trace> _traces = new List<Trace>();

            public SyntheticWindow(Random random)
            {
                _random = random;
                Rt = Enumerable.Range(0, 200).Select(i => 19.0 + i * CYCLE_MINUTES).ToArray();
                Peptides = new[]
                {
                    new SyntheticPeptide(new EmgShape(0.0, 0.027, 0.013).WithModeAt(19.80)),
                    new SyntheticPeptide(new EmgShape(0.0, 0.030, 0.016).WithModeAt(19.90)),
                    new SyntheticPeptide(new EmgShape(0.0, 0.026, 0.012).WithModeAt(20.60))
                };

                double mz = 300.0;
                foreach (var peptide in Peptides)
                {
                    foreach (double area in ANCHOR_AREAS)
                        peptide.TraceIds.Add(Add(true, mz += 7.1, (peptide.Shape, area)));
                    foreach (double area in CENTROID_AREAS)
                        peptide.TraceIds.Add(Add(false, mz += 7.1, (peptide.Shape, area)));
                }

                SharedTraceId = Add(true, mz += 7.1, (Peptides[0].Shape, SHARED_AREAS[0]), (Peptides[1].Shape, SHARED_AREAS[1]));
                Peptides[0].TraceIds.Add(SharedTraceId);
                Peptides[1].TraceIds.Add(SharedTraceId);
                CommonTraceId = Add(false, 147.113,
                    (Peptides[0].Shape, COMMON_AREAS[0]), (Peptides[1].Shape, COMMON_AREAS[1]), (Peptides[2].Shape, COMMON_AREAS[2]));
                NoiseTraceIds = Enumerable.Range(0, 8).Select(i => Add(i < 2, mz += 7.1)).ToHashSet();
            }

            public double[] Rt { get; }
            public SyntheticPeptide[] Peptides { get; }
            public IReadOnlyList<Trace> Traces => _traces;
            public int SharedTraceId { get; }
            public int CommonTraceId { get; }
            public HashSet<int> NoiseTraceIds { get; }

            private int Add(bool isAnchor, double mz, params (EmgShape Shape, double Area)[] contributions)
            {
                var intensities = Rt.Select(t => (double)SyntheticCounts.Poisson(_random,
                    contributions.Sum(c => c.Area * c.Shape.Density(t)) + BASELINE_IONS)).ToArray();
                int id = _traces.Count;
                _traces.Add(new Trace(id, isAnchor, mz, isAnchor ? 1 : 0, intensities));
                return id;
            }
        }
    }
}
