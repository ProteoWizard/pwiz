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

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Test
{
    /// <summary>
    /// Tests for CWT peak detection and PeakDetector,
    /// ported from osprey-chromatography/src/cwt.rs and lib.rs.
    /// </summary>
    [TestClass]
    public class ChromatographyTest
    {
        // =====================================================================
        // Helpers
        // =====================================================================

        /// <summary>
        /// Create a Gaussian XIC centered at the given position.
        /// Returns parallel RT and intensity arrays.
        /// </summary>
        private static XicData MakeGaussianXic(
            int fragmentIndex,
            double center,
            double sigma,
            double amplitude,
            int n,
            double spacing)
        {
            double[] rts = new double[n];
            double[] intensities = new double[n];
            for (int i = 0; i < n; i++)
            {
                double rt = i * spacing;
                rts[i] = rt;
                double diff = rt - center;
                intensities[i] = amplitude * Math.Exp(-(diff * diff) / (2.0 * sigma * sigma));
            }
            return new XicData(fragmentIndex, rts, intensities);
        }

        // =====================================================================
        // Kernel tests
        // =====================================================================

        [TestMethod]
        public void TestKernelZeroMean()
        {
            double[] kernel = CwtPeakDetector.MexicanHatKernel(5.0, 25);
            double sum = 0.0;
            for (int i = 0; i < kernel.Length; i++)
                sum += kernel[i];
            Assert.IsTrue(
                Math.Abs(sum) < 1e-10,
                string.Format("Kernel should be zero-mean, got sum = {0:E2}", sum));
        }

        [TestMethod]
        public void TestKernelSymmetric()
        {
            double[] kernel = CwtPeakDetector.MexicanHatKernel(5.0, 25);
            int len = kernel.Length;
            for (int i = 0; i < len / 2; i++)
            {
                Assert.IsTrue(
                    Math.Abs(kernel[i] - kernel[len - 1 - i]) < 1e-12,
                    string.Format(
                        "Kernel should be symmetric: kernel[{0}]={1:F6} != kernel[{2}]={3:F6}",
                        i, kernel[i], len - 1 - i, kernel[len - 1 - i]));
            }
        }

        [TestMethod]
        public void TestKernelPositiveCenterNegativeTails()
        {
            double[] kernel = CwtPeakDetector.MexicanHatKernel(5.0, 25);
            int center = kernel.Length / 2;
            Assert.IsTrue(
                kernel[center] > 0.0,
                string.Format("Kernel center should be positive: {0}", kernel[center]));
            Assert.IsTrue(
                kernel[0] < 0.0,
                string.Format("Kernel tail should be negative: {0}", kernel[0]));
            Assert.IsTrue(
                kernel[kernel.Length - 1] < 0.0,
                string.Format("Kernel tail should be negative: {0}", kernel[kernel.Length - 1]));
        }

        // =====================================================================
        // Convolution tests
        // =====================================================================

        [TestMethod]
        public void TestConvolveSameLength()
        {
            double[] signal = { 1.0, 2.0, 3.0, 4.0, 5.0 };
            double[] kernel = { 0.25, 0.5, 0.25 };
            double[] result = CwtPeakDetector.Convolve(signal, kernel);
            Assert.AreEqual(signal.Length, result.Length);
        }

        [TestMethod]
        public void TestConvolveDeltaFunction()
        {
            // Delta function convolved with kernel should reproduce the kernel
            int n = 21;
            int center = n / 2;
            double[] signal = new double[n];
            signal[center] = 1.0;
            double[] kernel = CwtPeakDetector.MexicanHatKernel(3.0, 5);

            double[] result = CwtPeakDetector.Convolve(signal, kernel);

            // Result at center should match kernel center
            int kCenter = kernel.Length / 2;
            Assert.IsTrue(
                Math.Abs(result[center] - kernel[kCenter]) < 1e-10,
                string.Format(
                    "Delta response at center: got {0:F6}, expected {1:F6}",
                    result[center], kernel[kCenter]));
        }

        // =====================================================================
        // Scale estimation tests
        // =====================================================================

        [TestMethod]
        public void TestEstimateScaleKnownPeak()
        {
            // Create XICs with known FWHM. Gaussian FWHM = 2.355 * sigma.
            // Use sigma=5 scans -> FWHM ~ 11.775 scans -> estimated CWT sigma ~ 5
            double sigma = 5.0;
            double sigmaRt = sigma * 0.01; // sigma in RT units (0.01 min spacing)
            List<XicData> xics = new List<XicData>();
            for (int i = 0; i < 6; i++)
            {
                double amplitude = 1000.0 * (i + 1);
                double[] rts = new double[100];
                double[] intensities = new double[100];
                for (int s = 0; s < 100; s++)
                {
                    double rt = s * 0.01;
                    rts[s] = rt;
                    double diff = rt - 0.5;
                    intensities[s] = amplitude * Math.Exp(-(diff * diff) / (2.0 * sigmaRt * sigmaRt));
                }
                xics.Add(new XicData(i, rts, intensities));
            }

            double estSigma = CwtPeakDetector.EstimateScale(xics);
            Assert.IsTrue(
                estSigma > 3.0 && estSigma < 8.0,
                string.Format("Estimated sigma should be near 5.0, got {0:F2}", estSigma));
        }

        [TestMethod]
        public void TestEstimateScaleFallback()
        {
            // All-zero XICs -> should return fallback (4.0)
            List<XicData> xics = new List<XicData>();
            for (int i = 0; i < 6; i++)
            {
                double[] rts = new double[50];
                double[] intensities = new double[50];
                for (int s = 0; s < 50; s++)
                {
                    rts[s] = s * 0.1;
                    intensities[s] = 0.0;
                }
                xics.Add(new XicData(i, rts, intensities));
            }

            double est = CwtPeakDetector.EstimateScale(xics);
            Assert.IsTrue(
                Math.Abs(est - 4.0) < 1e-10,
                string.Format("Should fall back to 4.0, got {0}", est));
        }

        // =====================================================================
        // Consensus peak detection tests
        // =====================================================================

        [TestMethod]
        public void TestConsensusGaussianPeak()
        {
            // 6 coeluting Gaussian peaks at RT=5.0, different amplitudes
            List<XicData> xics = new List<XicData>();
            for (int i = 0; i < 6; i++)
            {
                double amplitude = 1000.0 * (i + 1);
                xics.Add(MakeGaussianXic(i, 5.0, 0.3, amplitude, 100, 0.1));
            }

            List<XICPeakBounds> peaks = CwtPeakDetector.DetectConsensusPeaks(xics, 0.0);

            Assert.IsTrue(peaks.Count > 0, "Should find at least 1 peak");
            Assert.IsTrue(
                Math.Abs(peaks[0].ApexRt - 5.0) < 0.3,
                string.Format("Peak apex should be near 5.0, got {0:F2}", peaks[0].ApexRt));
            Assert.IsTrue(
                peaks[0].StartRt < 5.0 && peaks[0].EndRt > 5.0,
                string.Format(
                    "Peak boundaries should bracket apex: [{0:F2}, {1:F2}]",
                    peaks[0].StartRt, peaks[0].EndRt));
        }

        [TestMethod]
        public void TestConsensusTwoSeparatedPeaks()
        {
            // 6 fragments each with two peaks at RT=3.0 and RT=7.0
            List<XicData> xics = new List<XicData>();
            for (int i = 0; i < 6; i++)
            {
                double amplitude = 1000.0 * (i + 1);
                double[] rts = new double[100];
                double[] intensities = new double[100];
                for (int s = 0; s < 100; s++)
                {
                    double rt = s * 0.1;
                    rts[s] = rt;
                    double diff1 = rt - 3.0;
                    double p1 = amplitude * Math.Exp(-(diff1 * diff1) / (2.0 * 0.3 * 0.3));
                    double diff2 = rt - 7.0;
                    double p2 = amplitude * 0.5 * Math.Exp(-(diff2 * diff2) / (2.0 * 0.3 * 0.3));
                    intensities[s] = p1 + p2;
                }
                xics.Add(new XicData(i, rts, intensities));
            }

            List<XICPeakBounds> peaks = CwtPeakDetector.DetectConsensusPeaks(xics, 0.0);

            Assert.IsTrue(
                peaks.Count >= 2,
                string.Format("Should find at least 2 peaks, found {0}", peaks.Count));

            // Peaks should be near RT=3.0 and RT=7.0
            double[] apexRts = peaks.Select(p => p.ApexRt).ToArray();
            Assert.IsTrue(
                apexRts.Any(rt => Math.Abs(rt - 3.0) < 0.5),
                string.Format("Should find peak near 3.0, got [{0}]",
                    string.Join(", ", apexRts.Select(r => r.ToString("F2")))));
            Assert.IsTrue(
                apexRts.Any(rt => Math.Abs(rt - 7.0) < 0.5),
                string.Format("Should find peak near 7.0, got [{0}]",
                    string.Join(", ", apexRts.Select(r => r.ToString("F2")))));

            // First peak should have highest intensity (sorted by consensus descending)
            Assert.IsTrue(
                peaks[0].ApexIntensity >= peaks[1].ApexIntensity,
                "Peaks should be sorted by intensity descending");
        }

        [TestMethod]
        public void TestConsensusInterferenceRejection()
        {
            // 5 fragments with real peak at RT=5.0, 1 fragment with interference at RT=2.0
            List<XicData> xics = new List<XicData>();
            for (int i = 0; i < 5; i++)
                xics.Add(MakeGaussianXic(i, 5.0, 0.3, 1000.0, 100, 0.1));

            // Interference fragment: peak at RT=2.0, no signal at RT=5.0
            xics.Add(MakeGaussianXic(5, 2.0, 0.3, 5000.0, 100, 0.1));

            List<XICPeakBounds> peaks = CwtPeakDetector.DetectConsensusPeaks(xics, 0.0);

            Assert.IsTrue(peaks.Count > 0, "Should find at least 1 peak");
            // The first (strongest) peak should be the real one at RT=5.0
            Assert.IsTrue(
                Math.Abs(peaks[0].ApexRt - 5.0) < 0.5,
                string.Format(
                    "Best peak should be near real peak at 5.0, got {0:F2}",
                    peaks[0].ApexRt));
        }

        // =====================================================================
        // Trapezoidal area tests
        // =====================================================================

        [TestMethod]
        public void TestTrapezoidalArea()
        {
            // Rectangle: height 2.0, width 3.0 -> area = 6.0
            var series = new List<(double rt, double value)>
            {
                (0.0, 2.0),
                (1.0, 2.0),
                (2.0, 2.0),
                (3.0, 2.0)
            };
            double area = PeakDetector.TrapezoidalArea(series);
            Assert.AreEqual(6.0, area, 1e-10, "Rectangle area should be 6.0");

            // Triangle: base 2.0, height 4.0 -> area = 4.0
            var triangle = new List<(double rt, double value)>
            {
                (0.0, 0.0),
                (1.0, 4.0),
                (2.0, 0.0)
            };
            double triArea = PeakDetector.TrapezoidalArea(triangle);
            Assert.AreEqual(4.0, triArea, 1e-10, "Triangle area should be 4.0");

            // Single point -> returns the value itself
            var single = new List<(double rt, double value)>
            {
                (1.0, 5.0)
            };
            double singleArea = PeakDetector.TrapezoidalArea(single);
            Assert.AreEqual(5.0, singleArea, 1e-10, "Single point area should be the value");

            // Empty -> 0
            var empty = new List<(double rt, double value)>();
            double emptyArea = PeakDetector.TrapezoidalArea(empty);
            Assert.AreEqual(0.0, emptyArea, 1e-10, "Empty series area should be 0");
        }

        [TestMethod]
        public void TestTrapezoidalAreaParallelArrays()
        {
            // Same rectangle test but with parallel arrays
            double[] rts = { 0.0, 1.0, 2.0, 3.0 };
            double[] values = { 2.0, 2.0, 2.0, 2.0 };
            double area = PeakDetector.TrapezoidalArea(rts, values, 0, 3);
            Assert.AreEqual(6.0, area, 1e-10, "Rectangle area should be 6.0");
        }

        // =====================================================================
        // PeakDetector tests
        // =====================================================================

        [TestMethod]
        public void TestPeakDetector()
        {
            PeakDetector detector = new PeakDetector();
            detector.MinHeight = 0.1;

            // Create a simple Gaussian-like peak
            List<(double rt, double coef)> series = new List<(double, double)>();
            for (int i = 0; i < 20; i++)
            {
                double rt = i;
                double diff = rt - 10.0;
                double coef = Math.Exp(-(diff * diff / 8.0));
                series.Add((rt, coef));
            }

            List<PeakBoundaries> peaks = detector.Detect(series);
            Assert.AreEqual(1, peaks.Count, "Should find exactly 1 peak");
            Assert.IsTrue(
                Math.Abs(peaks[0].ApexRt - 10.0) < 1.0,
                string.Format("Apex should be near 10.0, got {0:F2}", peaks[0].ApexRt));
        }
    }
}
