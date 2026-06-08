/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
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

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.Scoring;

namespace pwiz.OspreySharp.Test
{
    /// <summary>
    /// Tests for the modular scoring SPI (<see cref="IOspreyFeatureCalculator"/> /
    /// <see cref="OspreyScoringContext"/> / <see cref="OspreyFeatureCalculators"/>),
    /// which mirrors Skyline's peak-scoring calculator model. These exercise the
    /// extracted calculators directly -- a capability the inline
    /// <c>ScoreCandidate</c> feature block did not have.
    /// </summary>
    [TestClass]
    public class OspreyFeatureCalculatorsTest
    {
        private const double TOLERANCE = 1e-6;

        /// <summary>
        /// Peak-shape family (peak_apex / peak_area / peak_sharpness): values come
        /// from the reference XIC (highest total intensity), apex is a direct
        /// lookup at the supplied apex index, area is the trapezoid over
        /// [start, end), sharpness is the mean of the left/right slopes.
        /// </summary>
        [TestMethod]
        public void TestPeakShapeCalculators()
        {
            var rts = new double[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            // frag1 has the higher total intensity (212 > 110), so it is the
            // reference XIC; its apex at scan 3 is value 90.
            var frag0 = new double[] { 0, 0, 0, 0, 5, 20, 60, 20, 5, 0 };
            var frag1 = new double[] { 0, 10, 50, 90, 50, 10, 2, 0, 0, 0 };
            var xics = new List<XicData>
            {
                new XicData(0, rts, frag0),
                new XicData(1, rts, frag1),
            };
            var bounds = new XICPeakBounds { StartIndex = 1, EndIndex = 5, ApexIndex = 3 };
            var peakData = new FakeDetailedPeakData(xics, bounds);

            var context = new OspreyScoringContext(null);
            context.ClearByproducts();

            double apex = OspreyFeatureCalculators.Get(3).Calculate(context, peakData);
            double area = OspreyFeatureCalculators.Get(4).Calculate(context, peakData);
            double sharpness = OspreyFeatureCalculators.Get(5).Calculate(context, peakData);

            // peak_apex: reference XIC (frag1) intensity at apex index 3 = 90.
            Assert.AreEqual(90.0, apex, TOLERANCE);
            // peak_area: trapezoid over [1,5) with dt = 1:
            // (10+50)/2 + (50+90)/2 + (90+50)/2 + (50+10)/2 = 30+70+70+30 = 200.
            Assert.AreEqual(200.0, area, TOLERANCE);
            // peak_sharpness: left (90-10)/(3-1)=40, right (90-10)/(5-3)=40, mean 40.
            Assert.AreEqual(40.0, sharpness, TOLERANCE);

            // The three calculators expose the parity-critical PIN names.
            Assert.AreEqual("peak_apex", OspreyFeatureCalculators.Get(3).Name);
            Assert.AreEqual("peak_area", OspreyFeatureCalculators.Get(4).Name);
            Assert.AreEqual("peak_sharpness", OspreyFeatureCalculators.Get(5).Name);

            // Degenerate peak data (no XICs) yields 0.0 for every peak-shape feature.
            var empty = new FakeDetailedPeakData(new List<XicData>(), bounds);
            context.ClearByproducts();
            Assert.AreEqual(0.0, OspreyFeatureCalculators.Get(3).Calculate(context, empty), TOLERANCE);
            Assert.AreEqual(0.0, OspreyFeatureCalculators.Get(4).Calculate(context, empty), TOLERANCE);
            Assert.AreEqual(0.0, OspreyFeatureCalculators.Get(5).Calculate(context, empty), TOLERANCE);
        }

        private sealed class FakeDetailedPeakData : IOspreyDetailedPeakData
        {
            private readonly IReadOnlyList<XicData> _xics;
            private readonly XICPeakBounds _peakBounds;

            public FakeDetailedPeakData(IReadOnlyList<XicData> xics, XICPeakBounds peakBounds)
            {
                _xics = xics;
                _peakBounds = peakBounds;
            }

            // Peak-shape calculators read only Xics and PeakBounds.
            public LibraryEntry Candidate { get { return null; } }
            public XICPeakBounds PeakBounds { get { return _peakBounds; } }
            public IReadOnlyList<XicData> Xics { get { return _xics; } }
        }
    }
}
