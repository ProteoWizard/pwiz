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
    /// Known templates (spec Stage 3a): a known precursor's template is the pooled fit to its
    /// fragments' traces inside its boundaries, not its reported apex; one with too few matching
    /// traces gets the run's typical shape at its apex.
    /// </summary>
    [TestClass]
    public class KnownTemplatesTest
    {
        private const double CYCLE_MINUTES = 1.14 / 60.0;
        private static readonly EmgShape PRIOR_SHAPE = new EmgShape(0.0, 0.018, 0.009);

        [TestMethod]
        public void TestKnownTemplates()
        {
            var rt = Enumerable.Range(0, 120).Select(i => 19.0 + i * CYCLE_MINUTES).ToArray();
            var truth = new EmgShape(0.0, 0.020, 0.010).WithModeAt(20.0);
            double[] fragments = { 300.1600, 401.2077, 514.2918, 627.3758, 742.4028 };
            var traces = fragments.Select((mz, i) => Trace(i, false, mz, 0, truth, 50.0 + 10.0 * i, rt)).ToList();

            // A doubly charged fragment found as an anchor, stored singly charged.
            double doubly = 450.7500;
            traces.Add(Trace(traces.Count, true, (doubly - TraceBuilder.PROTON) * 2 + TraceBuilder.PROTON, 2, truth, 80.0, rt));

            // Signal at a fragment's m/z but outside the boundaries must not pull the fit.
            traces.Add(Trace(traces.Count, false, 855.4500, 0, truth.WithModeAt(20.5), 500.0, rt));

            var settings = new WindowClusterSettings(new EmgShapePrior(PRIOR_SHAPE.Sigma, PRIOR_SHAPE.Tau),
                new NoiseModel(1.0, 1.0), CYCLE_MINUTES);
            var clusterer = new WindowClusterer(settings);
            var found = new KnownPrecursor(@"PEPTIDEK", 645.0, 2, 19.95, 20.07, 20.01,
                fragments.Concat(new[] { doubly, 855.4500 }).ToArray());
            var missing = new KnownPrecursor(@"MISSINGK", 645.5, 2, 20.30, 20.40, 20.35, new[] { 999.9 });

            var shapes = KnownTemplates.Fit(rt, traces, new[] { found, missing }, clusterer, settings);
            Assert.AreEqual(2, shapes.Count);
            Assert.AreEqual(truth.Mode, shapes[0].Mode, 0.1 * CYCLE_MINUTES, @"fitted mode, not the reported apex");
            Assert.AreEqual(truth.Sigma, shapes[0].Sigma, 0.1 * truth.Sigma);
            Assert.AreEqual(missing.Apex, shapes[1].Mode, 1e-9);
            Assert.AreEqual(PRIOR_SHAPE.Sigma, shapes[1].Sigma, 1e-12);
        }

        private static Trace Trace(int id, bool isAnchor, double mz, int charge, EmgShape shape, double area, double[] rt)
        {
            return new Trace(id, isAnchor, mz, charge, rt.Select(t => Math.Round(area * shape.Density(t))).ToArray());
        }
    }
}
