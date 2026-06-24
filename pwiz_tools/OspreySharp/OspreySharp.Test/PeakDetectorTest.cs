/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Test
{
    /// <summary>
    /// Tests for <see cref="PeakDetector"/> methods not already covered by
    /// <see cref="ChromatographyTest"/>: end-of-series + min-width handling in
    /// Detect, FindBestPeak selection, the Savitzky-Golay smoother, and the
    /// full DetectAllXicPeaks valley/FWHM path. Ported from
    /// osprey-chromatography/src/lib.rs.
    /// </summary>
    [TestClass]
    public class PeakDetectorTest
    {
        #region Detect

        [TestMethod]
        public void TestDetectPeakRunningToEndOfSeries()
        {
            // Signal rises and stays above threshold through the last sample, so
            // the trailing-peak branch (inPeak still true after the loop) closes it.
            var detector = new PeakDetector { MinHeight = 0.1, MinWidth = 3 };
            var series = new List<(double rt, double coef)>
            {
                (0.0, 0.0), (1.0, 0.05),
                (2.0, 0.5), (3.0, 0.8), (4.0, 1.0)
            };

            List<PeakBoundaries> peaks = detector.Detect(series);
            Assert.AreEqual(1, peaks.Count);
            Assert.AreEqual(4.0, peaks[0].ApexRt, 1e-10);
            Assert.AreEqual(4.0, peaks[0].EndRt, 1e-10);
            Assert.AreEqual(2.0, peaks[0].StartRt, 1e-10);
        }

        [TestMethod]
        public void TestDetectRejectsTooNarrowPeak()
        {
            // A single above-threshold sample (width 1) is below MinWidth=3.
            var detector = new PeakDetector { MinHeight = 0.1, MinWidth = 3 };
            var series = new List<(double rt, double coef)>
            {
                (0.0, 0.0), (1.0, 0.0), (2.0, 0.9), (3.0, 0.0), (4.0, 0.0)
            };

            Assert.AreEqual(0, detector.Detect(series).Count);
        }

        [TestMethod]
        public void TestDetectEmptySeries()
        {
            var detector = new PeakDetector();
            Assert.AreEqual(0, detector.Detect(new List<(double rt, double coef)>()).Count);
        }

        #endregion

        #region FindBestPeak

        [TestMethod]
        public void TestFindBestPeak()
        {
            var peaks = new List<PeakBoundaries>
            {
                new PeakBoundaries { ApexRt = 2.0, ApexCoefficient = 5.0 },
                new PeakBoundaries { ApexRt = 5.0, ApexCoefficient = 8.0 },
                new PeakBoundaries { ApexRt = 5.2, ApexCoefficient = 9.0 },
            };

            // Within tolerance of 5.0 there are two peaks; the higher apex wins.
            PeakBoundaries best = new PeakDetector().FindBestPeak(peaks, 5.0, 0.5);
            Assert.IsNotNull(best);
            Assert.AreEqual(9.0, best.ApexCoefficient, 1e-10);

            // Nothing within tolerance of a far-away expected RT -> null.
            Assert.IsNull(new PeakDetector().FindBestPeak(peaks, 50.0, 0.5));
        }

        #endregion

        #region SmoothSavitzkyGolay

        [TestMethod]
        public void TestSmoothSavitzkyGolayPreservesConstant()
        {
            // The [-3,12,17,12,-3]/35 kernel sums to 1, so a constant series is
            // unchanged (interior) and the endpoints are copied verbatim.
            double[] values = { 5.0, 5.0, 5.0, 5.0, 5.0, 5.0, 5.0 };
            double[] smoothed = PeakDetector.SmoothSavitzkyGolay(values);
            Assert.AreEqual(values.Length, smoothed.Length);
            foreach (double v in smoothed)
                Assert.AreEqual(5.0, v, 1e-10);
        }

        [TestMethod]
        public void TestSmoothSavitzkyGolayClampsNegative()
        {
            // A lone spike drives the i=4 window negative (-300/35); it must clamp
            // to 0, while the i=2 window equals 1700/35.
            double[] values = { 0.0, 0.0, 100.0, 0.0, 0.0, 0.0, 0.0 };
            double[] smoothed = PeakDetector.SmoothSavitzkyGolay(values);
            Assert.AreEqual(1700.0 / 35.0, smoothed[2], 1e-9);
            Assert.AreEqual(1200.0 / 35.0, smoothed[3], 1e-9);
            Assert.AreEqual(0.0, smoothed[4], 1e-10);  // clamped from -300/35
        }

        [TestMethod]
        public void TestSmoothSavitzkyGolayShortSeriesUnchanged()
        {
            // Fewer than 5 points: returned as a copy, values untouched.
            double[] values = { 1.0, 9.0, 2.0 };
            double[] smoothed = PeakDetector.SmoothSavitzkyGolay(values);
            CollectionAssert.AreEqual(values, smoothed);
            Assert.AreNotSame(values, smoothed);
        }

        #endregion

        #region DetectAllXicPeaks

        [TestMethod]
        public void TestDetectAllXicPeaksGaussian()
        {
            // Single Gaussian centered at RT 2.0; expect one peak whose apex and
            // boundaries bracket the center, with a positive integrated area.
            int n = 41;
            double[] rts = new double[n];
            double[] intensities = new double[n];
            for (int i = 0; i < n; i++)
            {
                double rt = i * 0.1;
                rts[i] = rt;
                double diff = rt - 2.0;
                intensities[i] = 1000.0 * Math.Exp(-(diff * diff) / (2.0 * 0.3 * 0.3));
            }

            List<XICPeakBounds> peaks = PeakDetector.DetectAllXicPeaks(rts, intensities, 10.0, 5.0);

            Assert.IsTrue(peaks.Count >= 1, "Should detect at least one peak");
            XICPeakBounds top = peaks[0];
            Assert.AreEqual(2.0, top.ApexRt, 0.15);
            Assert.IsTrue(top.StartRt < 2.0 && top.EndRt > 2.0,
                string.Format("Boundaries should bracket apex: [{0:F2}, {1:F2}]",
                    top.StartRt, top.EndRt));
            Assert.IsTrue(top.Area > 0.0, "Integrated area should be positive");
        }

        [TestMethod]
        public void TestDetectAllXicPeaksTooShort()
        {
            // Fewer than 3 points cannot form a peak.
            double[] rts = { 0.0, 0.1 };
            double[] intensities = { 100.0, 200.0 };
            Assert.AreEqual(0, PeakDetector.DetectAllXicPeaks(rts, intensities, 1.0, 5.0).Count);
        }

        #endregion
    }
}
