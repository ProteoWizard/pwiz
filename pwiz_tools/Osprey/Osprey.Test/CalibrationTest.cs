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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.Scoring;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Tests for RT calibration (LOESS), mass calibration, and calibration I/O.
    /// Ported from osprey-chromatography/src/calibration/ Rust tests.
    /// </summary>
    [TestClass]
    public class CalibrationTest
    {
        private const double TOLERANCE = 1e-6;

        #region RT Calibration Tests

        /// <summary>
        /// Verifies LOESS calibration fits a linear RT relationship with high R-squared
        /// and accurate prediction/extrapolation.
        /// </summary>
        [TestMethod]
        public void TestRtCalibrationLinear()
        {
            // Linear relationship: measured_rt = 2 * library_rt + 5
            double[] libraryRts = new double[50];
            double[] measuredRts = new double[50];
            for (int i = 0; i < 50; i++)
            {
                libraryRts[i] = i;
                measuredRts[i] = 2.0 * i + 5.0;
            }

            var config = new RTCalibratorConfig { Bandwidth = 0.3, OutlierRetention = 1.0 };
            var calibrator = new RTCalibrator(config);
            var calibration = calibrator.Fit(libraryRts, measuredRts);

            // Test prediction
            double pred = calibration.Predict(25.0);
            Assert.IsTrue(Math.Abs(pred - 55.0) < 0.5,
                string.Format("Prediction at 25 should be ~55, got {0}", pred));

            // Test extrapolation
            double predExtrap = calibration.Predict(60.0);
            Assert.IsTrue(Math.Abs(predExtrap - 125.0) < 5.0,
                string.Format("Extrapolation at 60 should be ~125, got {0}", predExtrap));

            // Test stats
            var stats = calibration.Stats();
            Assert.IsTrue(stats.RSquared > 0.99,
                string.Format("R-squared should be > 0.99 for linear data, got {0}", stats.RSquared));
        }

        /// <summary>
        /// Verifies LOESS calibration captures a sinusoidal nonlinear RT relationship
        /// with R-squared above 0.99.
        /// </summary>
        [TestMethod]
        public void TestRtCalibrationNonlinear()
        {
            double[] libraryRts = new double[100];
            double[] measuredRts = new double[100];
            for (int i = 0; i < 100; i++)
            {
                libraryRts[i] = i * 0.5;
                measuredRts[i] = libraryRts[i] + 10.0 * Math.Sin(libraryRts[i] / 50.0);
            }

            var config = new RTCalibratorConfig
            {
                Bandwidth = 0.2,
                MinPoints = 20,
                OutlierRetention = 1.0
            };
            var calibrator = new RTCalibrator(config);
            var calibration = calibrator.Fit(libraryRts, measuredRts);

            var stats = calibration.Stats();
            Assert.IsTrue(stats.RSquared > 0.99,
                string.Format("R-squared should be > 0.99 for smooth nonlinear data, got {0}",
                    stats.RSquared));
        }

        /// <summary>
        /// Verifies predict then inverse_predict recovers the original input RT.
        /// </summary>
        [TestMethod]
        public void TestInversePredictRoundtrip()
        {
            double[] libraryRts = new double[50];
            double[] measuredRts = new double[50];
            for (int i = 0; i < 50; i++)
            {
                libraryRts[i] = i;
                measuredRts[i] = 2.0 * i + 5.0;
            }

            var config = new RTCalibratorConfig { Bandwidth = 0.3, OutlierRetention = 1.0 };
            var calibrator = new RTCalibrator(config);
            var calibration = calibrator.Fit(libraryRts, measuredRts);

            double[] testPoints = { 5.0, 15.0, 25.0, 35.0, 45.0 };
            foreach (double libRt in testPoints)
            {
                double measured = calibration.Predict(libRt);
                double recovered = calibration.InversePredict(measured);
                Assert.IsTrue(Math.Abs(recovered - libRt) < 0.5,
                    string.Format("inverse_predict(predict({0})) = {1}, expected ~{0}",
                        libRt, recovered));
            }
        }

        /// <summary>
        /// Verifies that local RT tolerance values are computed across the RT range.
        /// </summary>
        [TestMethod]
        public void TestLocalToleranceVariesWithRt()
        {
            double[] libraryRts = new double[50];
            double[] measuredRts = new double[50];
            for (int i = 0; i < 50; i++)
            {
                libraryRts[i] = i;
                measuredRts[i] = i + 0.1 * Math.Sin(i / 10.0) * (i / 50.0 + 0.1);
            }

            var config = new RTCalibratorConfig { Bandwidth = 0.3, OutlierRetention = 1.0 };
            var calibrator = new RTCalibrator(config);
            var calibration = calibrator.Fit(libraryRts, measuredRts);

            double tolEarly = calibration.LocalTolerance(5.0, 3.0, 0.25);
            double tolLate = calibration.LocalTolerance(45.0, 3.0, 0.25);

            Assert.IsTrue(tolEarly >= 0.25,
                string.Format("Early tolerance should be at least 0.25, got {0}", tolEarly));
            Assert.IsTrue(tolLate >= 0.25,
                string.Format("Late tolerance should be at least 0.25, got {0}", tolLate));
        }

        /// <summary>
        /// Verifies that local tolerance returns the minimum floor when residuals are near zero.
        /// </summary>
        [TestMethod]
        public void TestLocalToleranceMinimumFloor()
        {
            double[] libraryRts = new double[50];
            double[] measuredRts = new double[50];
            for (int i = 0; i < 50; i++)
            {
                libraryRts[i] = i;
                measuredRts[i] = i + 0.001;
            }

            var config = new RTCalibratorConfig { Bandwidth = 0.3, OutlierRetention = 1.0 };
            var calibrator = new RTCalibrator(config);
            var calibration = calibrator.Fit(libraryRts, measuredRts);

            double tol = calibration.LocalTolerance(25.0, 3.0, 0.25);
            Assert.IsTrue(Math.Abs(tol - 0.25) < 0.01,
                string.Format("Tolerance should be at minimum floor 0.25, got {0}", tol));
        }

        #endregion

        #region LOESS Helper Tests

        /// <summary>
        /// Verifies median computation for odd-length, even-length, and empty arrays.
        /// </summary>
        [TestMethod]
        public void TestMedian()
        {
            Assert.IsTrue(Math.Abs(LoessRegression.Median(new[] { 1.0, 2.0, 3.0 }) - 2.0) < 1e-10);
            Assert.IsTrue(Math.Abs(LoessRegression.Median(new[] { 1.0, 2.0, 3.0, 4.0 }) - 2.5) < 1e-10);
            Assert.IsTrue(Math.Abs(LoessRegression.Median(new double[0]) - 0.0) < 1e-10);
        }

        /// <summary>
        /// Verifies sample standard deviation calculation against a known reference value.
        /// </summary>
        [TestMethod]
        public void TestStdDev()
        {
            double[] vals = { 2.0, 4.0, 4.0, 4.0, 5.0, 5.0, 7.0, 9.0 };
            double sd = LoessRegression.StdDev(vals);
            Assert.IsTrue(Math.Abs(sd - 2.138) < 0.01,
                string.Format("std dev should be ~2.138, got {0}", sd));
        }

        #endregion

        #region Stratified Sampler Tests

        /// <summary>
        /// Verifies the RT stratified sampler distributes samples evenly across all bins.
        /// </summary>
        [TestMethod]
        public void TestStratifiedSampler()
        {
            double[] rts = new double[1000];
            for (int i = 0; i < 1000; i++)
                rts[i] = i / 10.0;

            var sampler = new RTStratifiedSampler
            {
                NBins = 10,
                PeptidesPerBin = 50
            };

            int[] sampled = sampler.Sample(rts);

            Assert.IsTrue(sampled.Length <= 500,
                string.Format("Should sample at most 500 (10 bins x 50 each), got {0}", sampled.Length));
            Assert.IsTrue(sampled.Length >= 100,
                string.Format("Should sample at least 100 entries, got {0}", sampled.Length));

            // Check distribution across bins
            int[] binCounts = new int[10];
            foreach (int idx in sampled)
            {
                double rt = rts[idx];
                int bin = (int)(rt / 10.0);
                bin = Math.Min(bin, 9);
                binCounts[bin]++;
            }

            for (int b = 0; b < 10; b++)
            {
                Assert.IsTrue(binCounts[b] > 0,
                    string.Format("Bin {0} should have samples, got {1}", b, binCounts[b]));
            }
        }

        #endregion

        #region Mass Calibration Tests

        /// <summary>
        /// Verifies MS1/MS2 calibration statistics (mean, median, SD, count) from known data.
        /// </summary>
        [TestMethod]
        public void TestCalculateMzCalibration()
        {
            var qcData = new MzQCData(ToleranceUnit.Ppm);
            foreach (double e in new[] { -2.5, -2.3, -2.7, -2.4, -2.6 })
                qcData.AddMs1Error(e);
            foreach (double e in new[] { 1.2, 1.1, 1.3, 1.0, 1.4 })
                qcData.AddMs2Error(e);

            MzCalibrationResult ms1Cal, ms2Cal;
            MzCalibration.CalculateMzCalibration(qcData, out ms1Cal, out ms2Cal);

            // MS1 mean should be -2.5, median should be -2.5
            Assert.IsTrue(Math.Abs(ms1Cal.Mean + 2.5) < 0.01,
                string.Format("MS1 mean should be ~-2.5, got {0}", ms1Cal.Mean));
            Assert.IsTrue(Math.Abs(ms1Cal.Median + 2.5) < 0.01);
            Assert.IsTrue(ms1Cal.SD > 0.0);
            Assert.AreEqual(5, ms1Cal.Count);
            Assert.IsTrue(ms1Cal.Calibrated);
            Assert.AreEqual("ppm", ms1Cal.Unit);

            // MS2 mean should be 1.2, median should be 1.2
            Assert.IsTrue(Math.Abs(ms2Cal.Mean - 1.2) < 0.01,
                string.Format("MS2 mean should be ~1.2, got {0}", ms2Cal.Mean));
            Assert.IsTrue(Math.Abs(ms2Cal.Median - 1.2) < 0.01);
            Assert.IsTrue(ms2Cal.SD > 0.0);
            Assert.AreEqual(5, ms2Cal.Count);
            Assert.IsTrue(ms2Cal.Calibrated);
            Assert.AreEqual("ppm", ms2Cal.Unit);
        }

        /// <summary>
        /// Verifies that applying calibration shifts observed m/z toward theoretical.
        /// </summary>
        [TestMethod]
        public void TestApplyCalibration()
        {
            // Negative offset: observed m/z is systematically low, correction shifts up
            var calibration = new MzCalibrationResult
            {
                Mean = -2.5,
                Median = -2.5,
                SD = 0.1,
                Count = 100,
                Unit = "ppm",
                AdjustedTolerance = 2.5 + 3.0 * 0.1,
                Calibrated = true
            };

            double observed = 500.0;
            double corrected = MzCalibration.ApplyCalibration(observed, calibration);

            // Correction: 500.0 - (500.0 * -2.5 / 1e6) = 500.0 + 0.00125 = 500.00125
            Assert.IsTrue(Math.Abs(corrected - 500.00125) < 0.00001,
                string.Format("Expected ~500.00125, got {0}", corrected));
        }

        /// <summary>
        /// Verifies standard PPM error formula.
        /// </summary>
        [TestMethod]
        public void TestPpmErrorCalculation()
        {
            // Observed = 500.001, Theoretical = 500.0 -> 2.0 ppm
            double error = MzCalibration.CalculatePpmError(500.001, 500.0);
            Assert.IsTrue(Math.Abs(error - 2.0) < 0.01,
                string.Format("PPM error should be ~2.0, got {0}", error));
        }

        /// <summary>
        /// Verifies that uncalibrated m/z correction returns the observed value unchanged.
        /// </summary>
        [TestMethod]
        public void TestUncalibratedPassthrough()
        {
            var calibration = MzCalibrationResult.Uncalibrated();
            double observed = 500.12345;
            double corrected = MzCalibration.ApplyCalibration(observed, calibration);
            Assert.AreEqual(observed, corrected, TOLERANCE);
        }

        /// <summary>
        /// Verifies tolerance checking accepts m/z errors near the calibrated mean
        /// and rejects those far from it.
        /// </summary>
        [TestMethod]
        public void TestWithinCalibratedTolerance()
        {
            var calibration = new MzCalibrationResult
            {
                Mean = -2.5,
                Median = -2.5,
                SD = 1.0,
                Count = 100,
                Unit = "ppm",
                AdjustedTolerance = 5.5, // |mean| + 3*SD = 2.5 + 3.0
                Calibrated = true
            };

            double theoretical = 500.0;

            // Observed with -2.5 ppm error (exactly at mean) - should be within tolerance
            double observedAtMean = theoretical * (1.0 - 2.5 / 1e6);
            Assert.IsTrue(MzCalibration.IsWithinCalibratedTolerance(
                observedAtMean, theoretical, calibration, 10.0));

            // Observed with -6.0 ppm error (3.5 ppm from mean) - within tolerance
            double observedWithin = theoretical * (1.0 - 6.0 / 1e6);
            Assert.IsTrue(MzCalibration.IsWithinCalibratedTolerance(
                observedWithin, theoretical, calibration, 10.0));

            // Observed with +5.0 ppm error (7.5 ppm from mean) - outside tolerance
            double observedOutside = theoretical * (1.0 + 5.0 / 1e6);
            Assert.IsFalse(MzCalibration.IsWithinCalibratedTolerance(
                observedOutside, theoretical, calibration, 10.0));
        }

        #endregion

        #region Calibration I/O Tests

        /// <summary>
        /// Verifies that CalibrationFilename appends ".calibration.json" to the base name.
        /// </summary>
        [TestMethod]
        public void TestCalibrationFilename()
        {
            Assert.AreEqual("results.calibration.json",
                CalibrationIO.CalibrationFilename("results"));
            Assert.AreEqual("my_search.calibration.json",
                CalibrationIO.CalibrationFilename("my_search"));
            Assert.AreEqual("test.calibration.json",
                CalibrationIO.CalibrationFilename("test"));
        }

        /// <summary>
        /// Verifies calibration filename derivation from input file paths.
        /// </summary>
        [TestMethod]
        public void TestCalibrationFilenameForInput()
        {
            // Use Path.Combine for the directory-prefixed case so the test
            // runs on both Windows (\) and Linux (/). Path.GetFileNameWithoutExtension
            // only recognizes its host OS's separator, so a hard-coded
            // Windows literal would fail under Linux/WSL even though the
            // production code handles either OS correctly given an
            // OS-native input path.
            Assert.AreEqual("sample.calibration.json",
                CalibrationIO.CalibrationFilenameForInput(Path.Combine("data", "sample.mzML")));
            Assert.AreEqual("test.dia.calibration.json",
                CalibrationIO.CalibrationFilenameForInput("test.dia.mzML"));
            Assert.AreEqual("experiment.calibration.json",
                CalibrationIO.CalibrationFilenameForInput("experiment.mzML"));
        }

        /// <summary>
        /// Verifies JSON round-trip serialization preserves all calibration fields.
        /// </summary>
        [TestMethod]
        public void TestCalibrationJsonRoundtrip()
        {
            var calibration = new CalibrationParams
            {
                Metadata = new CalibrationMetadata
                {
                    NumConfidentPeptides = 100,
                    NumSampledPrecursors = 1000,
                    CalibrationSuccessful = true,
                    Timestamp = "2024-01-15T10:30:00Z"
                },
                Ms1Calibration = new MzCalibrationJson
                {
                    Mean = -2.5,
                    Median = -2.4,
                    SD = 0.8,
                    Count = 100,
                    Unit = "ppm",
                    AdjustedTolerance = 4.9,
                    WindowHalfwidthMultiplier = 3.0,
                    Calibrated = true
                },
                Ms2Calibration = new MzCalibrationJson
                {
                    Mean = 1.2,
                    Median = 1.1,
                    SD = 1.0,
                    Count = 500,
                    Unit = "ppm",
                    AdjustedTolerance = 4.2,
                    WindowHalfwidthMultiplier = 3.0,
                    Calibrated = true
                },
                RtCalibration = new RTCalibrationJson
                {
                    Method = RTCalibrationMethod.LOESS,
                    ResidualSD = 0.8,
                    NPoints = 100,
                    RSquared = 0.98,
                    ModelParams = new RTModelParamsJson
                    {
                        LibraryRts = new[] { 0.0, 10.0, 20.0, 30.0 },
                        FittedRts = new[] { 1.0, 11.0, 21.0, 31.0 },
                        AbsResiduals = new[] { 0.1, 0.2, 0.3, 0.4 }
                    },
                    P20AbsResidual = 0.15,
                    MAD = 0.12,
                    // Final RT search-window half-width persisted per issue #4364.
                    RtSearchWindowHalfWidth = 1.5
                }
            };

            // Serialize to JSON string
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(calibration,
                Newtonsoft.Json.Formatting.Indented);

            Assert.IsTrue(json.Contains("\"ms1_calibration\""));
            Assert.IsTrue(json.Contains("-2.5"));
            Assert.IsTrue(json.Contains("\"rt_search_window_halfwidth\""));

            // Deserialize back
            var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<CalibrationParams>(json);
            Assert.IsTrue(loaded.IsCalibrated);
            Assert.IsTrue(Math.Abs(loaded.Ms1Calibration.Mean + 2.5) < 0.001);
            Assert.AreEqual(RTCalibrationMethod.LOESS, loaded.RtCalibration.Method);
            Assert.IsNotNull(loaded.RtCalibration.ModelParams);
            Assert.AreEqual(4, loaded.RtCalibration.ModelParams.LibraryRts.Length);
            Assert.IsTrue(loaded.RtCalibration.RtSearchWindowHalfWidth.HasValue);
            Assert.AreEqual(1.5, loaded.RtCalibration.RtSearchWindowHalfWidth.Value, TOLERANCE);
        }

        /// <summary>
        /// The final RT search-window half-width is the single shared definition
        /// <c>clamp(3 * MAD * 1.4826, [min, max])</c> used by the scoring path, the
        /// persisted JSON, and the console summary (issue #4364). Verifies the clamp
        /// and that FromRTCalibration only persists the field when the clamps are
        /// supplied.
        /// </summary>
        [TestMethod]
        public void TestRtSearchWindowHalfWidth()
        {
            // 3 * 1.4826 * MAD, unclamped when inside [min, max].
            double mad = 0.30;
            double expected = 3.0 * mad * 1.4826; // ~1.334
            Assert.AreEqual(expected,
                RTCalibration.SearchWindowHalfWidth(mad, 0.5, 3.0), TOLERANCE);

            // Clamped up to the min floor for a tiny MAD.
            Assert.AreEqual(0.5,
                RTCalibration.SearchWindowHalfWidth(0.001, 0.5, 3.0), TOLERANCE);

            // Clamped down to the max ceiling for a large MAD.
            Assert.AreEqual(3.0,
                RTCalibration.SearchWindowHalfWidth(5.0, 0.5, 3.0), TOLERANCE);

            // Fit a small calibration and confirm FromRTCalibration persists the
            // window only when the clamps are supplied.
            double[] libraryRts = new double[50];
            double[] measuredRts = new double[50];
            for (int i = 0; i < 50; i++)
            {
                libraryRts[i] = i;
                measuredRts[i] = 2.0 * i + 5.0;
            }
            var cal = new RTCalibrator(
                new RTCalibratorConfig { Bandwidth = 0.3, OutlierRetention = 1.0 })
                .Fit(libraryRts, measuredRts);

            var withoutClamps = RTCalibrationJson.FromRTCalibration(cal);
            Assert.IsFalse(withoutClamps.RtSearchWindowHalfWidth.HasValue);

            var withClamps = RTCalibrationJson.FromRTCalibration(cal, 0.5, 3.0);
            Assert.IsTrue(withClamps.RtSearchWindowHalfWidth.HasValue);
            Assert.AreEqual(
                RTCalibration.SearchWindowHalfWidth(cal.Stats().MAD, 0.5, 3.0),
                withClamps.RtSearchWindowHalfWidth.Value, TOLERANCE);
        }

        /// <summary>
        /// <see cref="RTCalibration.SearchWindowRaw"/> is the unclamped
        /// <c>3 * MAD * 1.4826</c> the console summary reports as the computed
        /// tolerance. Verifies the formula and the small / large / in-range MAD
        /// cases that select the summary's floor / cap / in-range wording.
        /// </summary>
        [TestMethod]
        public void TestSearchWindowRaw()
        {
            // Unclamped 3 * 1.4826 * MAD.
            double mad = 0.30;
            Assert.AreEqual(3.0 * mad * 1.4826,
                RTCalibration.SearchWindowRaw(mad), TOLERANCE);
            Assert.AreEqual(0.0, RTCalibration.SearchWindowRaw(0.0), TOLERANCE);

            // The raw value vs the clamped SearchWindowHalfWidth selects which
            // branch the summary reports: below the floor, above the ceiling, or
            // in range (equal).
            Assert.IsTrue(RTCalibration.SearchWindowRaw(0.001)
                < RTCalibration.SearchWindowHalfWidth(0.001, 0.5, 3.0));
            Assert.IsTrue(RTCalibration.SearchWindowRaw(5.0)
                > RTCalibration.SearchWindowHalfWidth(5.0, 0.5, 3.0));
            Assert.AreEqual(RTCalibration.SearchWindowRaw(0.30),
                RTCalibration.SearchWindowHalfWidth(0.30, 0.5, 3.0), TOLERANCE);

            // A NaN MAD propagates, so the summary treats it as undetermined.
            Assert.IsTrue(double.IsNaN(RTCalibration.SearchWindowRaw(double.NaN)));
        }

        /// <summary>
        /// Verifies save and load of calibration file to disk.
        /// </summary>
        [TestMethod]
        public void TestCalibrationSaveLoad()
        {
            string tempPath = Path.GetTempFileName();
            try
            {
                var calibration = CalibrationParams.Uncalibrated();
                calibration.Metadata.CalibrationSuccessful = true;
                calibration.Metadata.NumConfidentPeptides = 42;
                calibration.Ms1Calibration.Mean = -1.5;
                calibration.Ms1Calibration.Calibrated = true;

                CalibrationIO.SaveCalibration(calibration, tempPath);
                var loaded = CalibrationIO.LoadCalibration(tempPath);

                Assert.IsTrue(loaded.IsCalibrated);
                Assert.AreEqual(42, loaded.Metadata.NumConfidentPeptides);
                Assert.IsTrue(Math.Abs(loaded.Ms1Calibration.Mean + 1.5) < 0.001);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        #endregion

        #region Calibration XCorr Regression Guard

        /// <summary>
        /// Regression guard for the calibration XCorr bin-width spec documented
        /// in Rust osprey docs/02-calibration.md ("Comet-style XCorr (unit
        /// resolution, BLAS sdot)"). Calibration LDA only needs target/decoy
        /// discrimination, and unit bins (~2K) are ~50x cheaper than HRAM bins
        /// (~100K) -- on .NET Framework the 100K double[] arrays land in the
        /// LOH and trigger gen-2 GC pressure during the all-windows-at-once
        /// pre-preprocessing step.
        ///
        /// If a future change makes Calibrator.s_calXcorrScorer
        /// construct with BinConfig.HRAM() (or any other non-unit bin config),
        /// these asserts fail loudly and cite the Rust design doc. Mirrors
        /// the calibration_scorer_uses_unit_bins_for_* tests in Rust osprey
        /// (osprey/crates/osprey/src/pipeline.rs).
        /// </summary>
        [TestMethod]
        public void TestCalibrationXcorrScorerUsesUnitBins()
        {
            int calBins = Calibrator.s_calXcorrScorer.BinConfig.NBins;
            int unitBins = new SpectralScorer(BinConfig.UnitResolution()).BinConfig.NBins;
            int hramBins = new SpectralScorer(BinConfig.HRAM()).BinConfig.NBins;

            Assert.AreEqual(unitBins, calBins,
                "Calibration XCorr must use unit-resolution bins " +
                "(see Rust osprey docs/02-calibration.md).");
            Assert.AreNotEqual(hramBins, calBins,
                "Calibration XCorr must NOT use HRAM bins -- if this fails, " +
                "someone changed s_calXcorrScorer to a non-unit BinConfig. " +
                "On .NET Framework this causes LOH allocation pressure from " +
                "pre-preprocessing ~200K HRAM spectra for calibration.");
        }

        #endregion

        #region Calibration retry ladder (issue #4401)

        /// <summary>
        /// The calibration retry ladder: how many sampling attempts run, how the
        /// sample grows on a shortfall, and when a short attempt is accepted,
        /// retried, or degraded to fallback tolerances.
        ///
        /// Guards issue #4401, where C# implemented only attempt 1 and any file whose
        /// confident-peptide count landed in [50, MinCalibrationPoints) silently ran
        /// with uncalibrated tolerances. The Stellar/Astral regression files never
        /// enter that band, so only a test like this one exercises it.
        /// Mirrors Rust pipeline.rs:709-714 and :1000-1048.
        /// </summary>
        [TestMethod]
        public void TestCalibrationRetryLadder()
        {
            const int minPoints = 200;      // RTCalibrationConfig.MinCalibrationPoints
            const int minFitPoints = 15;    // MIN_LINEAR_FIT_POINTS
            const int sampleSize = 100000;  // RTCalibrationConfig.CalibrationSampleSize
            const double retry = 2.0;       // RTCalibrationConfig.CalibrationRetryFactor
            const int seaAdTargets = 1580119; // SEA-AD Carafe library target count

            // --- max_attempts derivation ---
            // Sample is a strict subset and the factor grows it -> the full 3-rung ladder.
            Assert.AreEqual(3, Calibrator.ComputeMaxAttempts(sampleSize, retry, seaAdTargets));
            // Library smaller than the sample: attempt 1 already sees every target.
            Assert.AreEqual(1, Calibrator.ComputeMaxAttempts(sampleSize, retry, 50000));
            // sampleSize == 0 already means "use all".
            Assert.AreEqual(1, Calibrator.ComputeMaxAttempts(0, retry, seaAdTargets));
            // A factor that cannot grow the sample would rescore an identical set.
            Assert.AreEqual(1, Calibrator.ComputeMaxAttempts(sampleSize, 1.0, seaAdTargets));

            // --- attempt 1 short (the three SEA-AD files: 193 / 178 / 141) -> retry, doubled ---
            foreach (int nConfident in new[] { 193, 178, 141 })
            {
                var d = Calibrator.DecideLadderAction(
                    1, 3, false, nConfident, minPoints, sampleSize, retry, seaAdTargets);
                Assert.AreEqual(Calibrator.CalibrationLadderAction.Retry, d.Action,
                    string.Format("{0} confident peptides on attempt 1 must retry, not fall back", nConfident));
                Assert.AreEqual(200000, d.NextSampleSize);
            }

            // --- attempt 2 still short -> the final attempt uses ALL targets (0) ---
            var attempt2 = Calibrator.DecideLadderAction(
                2, 3, false, 193, minPoints, 200000, retry, seaAdTargets);
            Assert.AreEqual(Calibrator.CalibrationLadderAction.Retry, attempt2.Action);
            Assert.AreEqual(0, attempt2.NextSampleSize, "final attempt must sample ALL targets");

            // Growth clamps to ALL when the doubled sample would exceed the target count.
            var clamped = Calibrator.DecideLadderAction(
                1, 3, false, 193, minPoints, sampleSize, retry, 150000);
            Assert.AreEqual(0, clamped.NextSampleSize);

            // --- final attempt, at or above the absolute floor -> fit anyway ---
            var banded = Calibrator.DecideLadderAction(
                3, 3, true, 193, minPoints, 0, retry, seaAdTargets);
            Assert.AreEqual(Calibrator.CalibrationLadderAction.Fit, banded.Action);
            Assert.IsTrue(banded.BelowTarget);
            Assert.AreEqual(193, banded.EffectiveMinPoints,
                "effectiveMinPoints = min(nConfident, MinCalibrationPoints)");

            // Exactly at the fit floor still fits; one below degrades. A fit on a few
            // dozen confident peptides beats searching at the raw library RT, so the
            // floor is MIN_LINEAR_FIT_POINTS, not the old 50-point absolute minimum.
            Assert.AreEqual(Calibrator.CalibrationLadderAction.Fit, Calibrator.DecideLadderAction(
                3, 3, true, minFitPoints, minPoints, 0, retry, seaAdTargets).Action);
            Assert.AreEqual(Calibrator.CalibrationLadderAction.Fallback, Calibrator.DecideLadderAction(
                3, 3, true, minFitPoints - 1, minPoints, 0, retry, seaAdTargets).Action);
            // 50 points -- which used to be the hard floor -- now fits, linearly.
            var fifty = Calibrator.DecideLadderAction(3, 3, true, 50, minPoints, 0, retry, seaAdTargets);
            Assert.AreEqual(Calibrator.CalibrationLadderAction.Fit, fifty.Action);
            Assert.IsTrue(fifty.BelowTarget);
            Assert.IsTrue(Calibrator.SelectFitPlan(50, 0.3, minPoints).LinearFit);

            // --- a healthy file fits on attempt 1 and is NOT flagged below target ---
            var healthy = Calibrator.DecideLadderAction(
                1, 3, false, 823, minPoints, sampleSize, retry, seaAdTargets);
            Assert.AreEqual(Calibrator.CalibrationLadderAction.Fit, healthy.Action);
            Assert.IsFalse(healthy.BelowTarget);
            Assert.AreEqual(minPoints, healthy.EffectiveMinPoints,
                "a clean file keeps MinPoints == MinCalibrationPoints, so its fit is unchanged");

            // --- a one-rung ladder (small library) cannot retry: band fits, sub-floor degrades ---
            var smallLib = Calibrator.DecideLadderAction(
                1, 1, true, 193, minPoints, sampleSize, retry, 50000);
            Assert.AreEqual(Calibrator.CalibrationLadderAction.Fit, smallLib.Action);
            Assert.IsTrue(smallLib.BelowTarget);
            Assert.AreEqual(Calibrator.CalibrationLadderAction.Fallback, Calibrator.DecideLadderAction(
                1, 1, true, 10, minPoints, sampleSize, retry, 50000).Action);

            // No confident peptides at all, with rungs left, still climbs the ladder.
            Assert.AreEqual(Calibrator.CalibrationLadderAction.Retry, Calibrator.DecideLadderAction(
                1, 3, false, 0, minPoints, sampleSize, retry, seaAdTargets).Action);
        }

        /// <summary>
        /// Cross-attempt match accumulation keeps the better match per library entry,
        /// carrying that match's S/N and RTs with it, and resolves ties in favour of
        /// the incumbent.
        ///
        /// The decisive detail: the winner is chosen on CorrelationScore -- the field
        /// holding Rust's CalibrationMatch.score (batch.rs:2773 sets it to the
        /// co-elution correlation sum, despite the doc comment at batch.rs:869 calling
        /// it XCorr). It must NOT be DiscriminantScore, which LDA overwrites in place
        /// between attempts.
        /// </summary>
        [TestMethod]
        public void TestCalibrationMatchAccumulation()
        {
            var accumulated = new Dictionary<uint, Calibrator.AccumulatedMatch>();

            // Attempt 1.
            Calibrator.MergeCalibrationMatches(accumulated,
                Bag(Match(10, 1.0), Match(11, 5.0), Match(12, 3.0)),
                Snr(10, 6.0, 11, 6.1, 12, 6.2),
                Rts(10, 1.0, 1.1, 11, 2.0, 2.1, 12, 3.0, 3.1));

            // LDA runs on the accumulated set between attempts and overwrites
            // DiscriminantScore in place. Entry 11's discriminant becomes huge while
            // its CorrelationScore stays 5.0.
            accumulated[11].Match.DiscriminantScore = 999.0;

            // Attempt 2: entry 10 improves, entry 11 is challenged by a match with a
            // BETTER correlation but a lower (raw) discriminant, entry 12 ties, 13 is new.
            Calibrator.MergeCalibrationMatches(accumulated,
                Bag(Match(10, 2.0), Match(11, 7.0), Match(12, 3.0), Match(13, 4.0)),
                Snr(10, 7.0, 11, 7.1, 12, 9.9, 13, 7.3),
                Rts(10, 1.0, 1.2, 11, 2.0, 2.2, 12, 3.0, 9.9, 13, 4.0, 4.1));

            Assert.AreEqual(4, accumulated.Count);

            // Entry 10: higher correlation wins, and its S/N + measured RT travel with it.
            Assert.AreEqual(2.0, accumulated[10].Match.CorrelationScore, TOLERANCE);
            Assert.AreEqual(7.0, accumulated[10].Snr, TOLERANCE);
            Assert.AreEqual(1.2, accumulated[10].MeasuredRt, TOLERANCE);

            // Entry 11: correlation 7.0 > 5.0 wins, even though the incumbent's
            // LDA-written DiscriminantScore (999.0) dwarfs the challenger's. Comparing
            // DiscriminantScore here would wrongly keep the incumbent.
            Assert.AreEqual(7.0, accumulated[11].Match.CorrelationScore, TOLERANCE);
            Assert.AreEqual(7.1, accumulated[11].Snr, TOLERANCE);

            // Entry 12: an exact tie keeps the incumbent (strict >), so its S/N and RT
            // are attempt 1's, not the 9.9 sentinels from attempt 2.
            Assert.AreEqual(6.2, accumulated[12].Snr, TOLERANCE);
            Assert.AreEqual(3.1, accumulated[12].MeasuredRt, TOLERANCE);

            // Entry 13: seen only on attempt 2.
            Assert.AreEqual(4.0, accumulated[13].Match.CorrelationScore, TOLERANCE);
        }

        /// <summary>
        /// The graduated fit tier: a full-bandwidth LOESS when there are enough
        /// points, a progressively stiffer (wider-bandwidth) LOESS as the point set
        /// thins, and a global linear fit once even that is over-flexible.
        ///
        /// The invariant that motivates it: a LOESS local window holds
        /// <c>bandwidth * n</c> points, so a fixed bandwidth lets the window collapse
        /// as n falls. The tier holds the window near the size the default config
        /// yields at MinCalibrationPoints (0.3 * 200 = 60 points).
        /// </summary>
        [TestMethod]
        public void TestCalibrationGraduatedFitPlan()
        {
            const double bw = 0.3;      // RTCalibrationConfig.LoessBandwidth
            const int minPoints = 200;  // RTCalibrationConfig.MinCalibrationPoints

            // At or above the target, nothing changes -- this is what keeps the
            // Stellar/Astral goldens bit-identical.
            foreach (int n in new[] { 200, 633, 729 })
            {
                var plan = Calibrator.SelectFitPlan(n, bw, minPoints);
                Assert.IsFalse(plan.LinearFit);
                Assert.AreEqual(bw, plan.Bandwidth, TOLERANCE,
                    "a healthy point count must keep the configured bandwidth");
            }

            // In the band, bandwidth widens so the local window stays near 60 points.
            foreach (int n in new[] { 199, 193, 150, 120, 100 })
            {
                var plan = Calibrator.SelectFitPlan(n, bw, minPoints);
                Assert.IsFalse(plan.LinearFit, "n={0} must still use LOESS", n);
                Assert.IsTrue(plan.Bandwidth >= bw, "bandwidth must never narrow");
                Assert.IsTrue(plan.Bandwidth <= 1.0, "bandwidth must stay a valid fraction");
                Assert.AreEqual(60.0, plan.Bandwidth * n, 1.0,
                    "the local window should hold ~60 points regardless of n");
            }

            // 193 points -- the worst SEA-AD file before the retry ladder -- already
            // gives 58-point windows at the default bandwidth, so the tier barely
            // moves it. The stiffening is real only as n approaches the linear cutoff.
            Assert.AreEqual(0.311, Calibrator.SelectFitPlan(193, bw, minPoints).Bandwidth, 0.001);
            Assert.AreEqual(0.600, Calibrator.SelectFitPlan(100, bw, minPoints).Bandwidth, 0.001);

            // Below the cutoff: a global line, whatever the bandwidth would have been.
            foreach (int n in new[] { 99, 75, 50 })
                Assert.IsTrue(Calibrator.SelectFitPlan(n, bw, minPoints).LinearFit,
                    "n={0} must fall back to a linear fit", n);

            // A degenerate config whose target sits below the linear cutoff must not
            // produce a band: n >= minPoints still takes the unchanged LOESS path.
            Assert.IsFalse(Calibrator.SelectFitPlan(60, bw, 50).LinearFit);
            Assert.IsTrue(Calibrator.SelectFitPlan(40, bw, 50).LinearFit);

            // The widened bandwidth is clamped to a valid fraction even when the
            // target window exceeds the point count.
            Assert.AreEqual(1.0, Calibrator.SelectFitPlan(120, 0.9, 200).Bandwidth, TOLERANCE);
        }

        /// <summary>
        /// The linear tier fits a true global least-squares line, recovers exact
        /// slope/intercept on collinear input, reports itself as
        /// <see cref="RTCalibrationMethod.Linear"/>, and -- unlike LOESS -- does not
        /// bend toward a single outlier.
        /// </summary>
        [TestMethod]
        public void TestLinearRtCalibrationFit()
        {
            // measured = 2 * library + 5, 60 points (inside the linear tier).
            const int n = 60;
            double[] libraryRts = new double[n];
            double[] measuredRts = new double[n];
            for (int i = 0; i < n; i++)
            {
                libraryRts[i] = i;
                measuredRts[i] = 2.0 * i + 5.0;
            }

            var linear = new RTCalibrator(new RTCalibratorConfig
            {
                LinearFit = true,
                MinPoints = n,
                OutlierRetention = 1.0
            }).Fit(libraryRts, measuredRts);

            Assert.AreEqual(RTCalibrationMethod.Linear, linear.Method);
            Assert.AreEqual(25.0, linear.Predict(10.0), 1e-9);
            // Extrapolates along the line rather than flattening at the last knot.
            Assert.AreEqual(2.0 * (n - 1) + 5.0, linear.Predict(n - 1), 1e-9);
            Assert.AreEqual(1.0, linear.Stats().RSquared, 1e-9);
            Assert.AreEqual(0.0, linear.Stats().ResidualSD, 1e-9);

            // A single gross outlier perturbs the line only slightly (it is spread
            // across all points), and the fit stays linear rather than tracking it.
            measuredRts[n / 2] += 50.0;
            var perturbed = new RTCalibrator(new RTCalibratorConfig
            {
                LinearFit = true,
                MinPoints = n,
                OutlierRetention = 1.0
            }).Fit(libraryRts, measuredRts);
            Assert.AreEqual(RTCalibrationMethod.Linear, perturbed.Method);
            // The outlier's own point is NOT interpolated to its measured value.
            int outlierIndex = n / 2;
            Assert.IsTrue(
                Math.Abs(perturbed.Predict(outlierIndex) - measuredRts[outlierIndex]) > 20.0,
                "a linear fit must not chase a single outlier");

            // Degenerate x (all library RTs identical) yields a horizontal line at
            // mean(y) instead of dividing by zero.
            double[] flatX = new double[n];
            double[] y = new double[n];
            for (int i = 0; i < n; i++)
            {
                flatX[i] = 7.0;
                y[i] = i;
            }
            var flat = new RTCalibrator(new RTCalibratorConfig
            {
                LinearFit = true,
                MinPoints = n,
                OutlierRetention = 1.0
            }).Fit(flatX, y);
            Assert.AreEqual((n - 1) / 2.0, flat.Predict(7.0), 1e-9);
        }

        /// <summary>
        /// Theil-Sen must recover the true slope despite outliers that would lever an
        /// ordinary least-squares line -- the reason the linear tier can trust a few
        /// dozen confident peptides even when one or two of them are false positives.
        /// </summary>
        [TestMethod]
        public void TestLinearRtCalibrationIsRobustToOutliers()
        {
            const int n = 60;
            double[] libraryRts = new double[n];
            double[] measuredRts = new double[n];
            for (int i = 0; i < n; i++)
            {
                libraryRts[i] = i;
                measuredRts[i] = 2.0 * i + 5.0;
            }

            // Three false positives, including two at the high-leverage RT extremes,
            // which is exactly where OLS is worst.
            measuredRts[0] += 40.0;
            measuredRts[n - 1] -= 40.0;
            measuredRts[n / 3] += 25.0;

            var cal = new RTCalibrator(new RTCalibratorConfig
            {
                LinearFit = true,
                MinPoints = n,
                OutlierRetention = 1.0
            }).Fit(libraryRts, measuredRts);

            // The median of pairwise slopes is untouched by 3 bad points in 60.
            double slope = (cal.Predict(50.0) - cal.Predict(10.0)) / 40.0;
            Assert.AreEqual(2.0, slope, 1e-9, "Theil-Sen must recover the true slope");
            Assert.AreEqual(25.0, cal.Predict(10.0), 1e-9, "and the true intercept");
        }

        /// <summary>
        /// The two guards on a thin linear fit: its points must span enough of the
        /// library RT range to determine a slope, and the resulting line must agree
        /// with the library/mzML range mapping. Otherwise a mis-centred window would
        /// be worse than no calibration at all.
        /// </summary>
        [TestMethod]
        public void TestLowPointCalibrationGuards()
        {
            // --- RT span ---
            // Points covering the full library range identify a slope.
            var spread = new List<double>();
            for (int i = 0; i < 20; i++)
                spread.Add(i * 5.0); // 0..95 over a 0..100 library range
            Assert.IsTrue(Calibrator.HasSufficientRtSpan(spread, 0.0, 100.0));

            // The same 20 confident peptides bunched into one region do not.
            var bunched = new List<double>();
            for (int i = 0; i < 20; i++)
                bunched.Add(40.0 + i * 0.5); // 40..49.5
            Assert.IsFalse(Calibrator.HasSufficientRtSpan(bunched, 0.0, 100.0),
                "a tight cluster cannot determine a gradient, however confident");

            // Degenerate library range.
            Assert.IsFalse(Calibrator.HasSufficientRtSpan(spread, 5.0, 5.0));

            // --- fitted-line plausibility, against a range slope of 1.0 ---
            const double rangeSlope = 1.0;
            Assert.IsTrue(Calibrator.IsPlausibleLinearFit(
                LineCalibration(0.0, 100.0, 1.0, 0.0), rangeSlope, 0.0, 100.0));

            // A slope 3x the range mapping is not credible from a thin fit.
            Assert.IsFalse(Calibrator.IsPlausibleLinearFit(
                LineCalibration(0.0, 100.0, 3.0, 0.0), rangeSlope, 0.0, 100.0));
            // Nor is one 3x flatter.
            Assert.IsFalse(Calibrator.IsPlausibleLinearFit(
                LineCalibration(0.0, 100.0, 1.0 / 3.0, 0.0), rangeSlope, 0.0, 100.0));
            // A negative gradient does not preserve RT ordering.
            Assert.IsFalse(Calibrator.IsPlausibleLinearFit(
                LineCalibration(0.0, 100.0, -1.0, 100.0), rangeSlope, 0.0, 100.0));
            // A plausible slope that predicts RTs outside the acquisition window.
            Assert.IsFalse(Calibrator.IsPlausibleLinearFit(
                LineCalibration(0.0, 100.0, 1.0, 60.0), rangeSlope, 0.0, 100.0));
            // Exactly at the 2x ratio bound is still accepted.
            Assert.IsTrue(Calibrator.IsPlausibleLinearFit(
                LineCalibration(0.0, 50.0, 2.0, 0.0), rangeSlope, 0.0, 100.0));
        }

        /// <summary>
        /// The RT-tolerance floor widens as the calibration thins, so a MAD that came
        /// out small by luck cannot buy a window the fit does not support. It reduces
        /// exactly to the configured floor at MinCalibrationPoints, which is what keeps
        /// a healthy calibration bit-identical.
        /// </summary>
        [TestMethod]
        public void TestEffectiveMinRtTolerance()
        {
            const double minTol = 0.5;
            const double maxTol = 3.0;
            const int minCal = 200;

            // At or above the target the floor is untouched.
            foreach (int n in new[] { 200, 729, 5000 })
                Assert.AreEqual(minTol,
                    RTCalibration.EffectiveMinRtTolerance(n, minTol, maxTol, minCal), TOLERANCE);

            // Below it, the floor grows like sqrt(minCal / n).
            Assert.AreEqual(0.5 * Math.Sqrt(2.0),
                RTCalibration.EffectiveMinRtTolerance(100, minTol, maxTol, minCal), 1e-9);
            Assert.AreEqual(1.0,
                RTCalibration.EffectiveMinRtTolerance(50, minTol, maxTol, minCal), 1e-9);
            Assert.AreEqual(0.5 * Math.Sqrt(8.0),
                RTCalibration.EffectiveMinRtTolerance(25, minTol, maxTol, minCal), 1e-9);

            // Monotone: fewer points never buys a tighter floor.
            double prev = 0.0;
            for (int n = 199; n >= 15; n -= 1)
            {
                double tol = RTCalibration.EffectiveMinRtTolerance(n, minTol, maxTol, minCal);
                Assert.IsTrue(tol >= prev - 1e-12, "floor must not narrow as n falls");
                prev = tol;
            }

            // Never exceeds the configured maximum, even at absurdly small n.
            Assert.AreEqual(maxTol,
                RTCalibration.EffectiveMinRtTolerance(1, minTol, maxTol, minCal), TOLERANCE);
            // A degenerate n is treated as "unknown", not "infinitely uncertain".
            Assert.AreEqual(minTol,
                RTCalibration.EffectiveMinRtTolerance(0, minTol, maxTol, minCal), TOLERANCE);
        }

        /// <summary>
        /// The predict-only range mapping used when calibration fails: it reproduces
        /// the line exactly (including outside the library RT range), is the identity
        /// when the two RT scales agree -- so the search behaves exactly as before --
        /// and refuses a degenerate range.
        /// </summary>
        [TestMethod]
        public void TestFromLinearMapping()
        {
            // A seconds-to-minutes library, the case the identity fallback gets wrong.
            var cal = RTCalibration.FromLinearMapping(0.0, 6000.0, 1.0 / 60.0, 0.0);
            Assert.AreEqual(RTCalibrationMethod.Linear, cal.Method);
            Assert.AreEqual(50.0, cal.Predict(3000.0), 1e-9);
            Assert.AreEqual(0.0, cal.Predict(0.0), 1e-9);
            Assert.AreEqual(100.0, cal.Predict(6000.0), 1e-9);
            // Extrapolates along the line rather than flattening at the end knots.
            Assert.AreEqual(200.0, cal.Predict(12000.0), 1e-9);

            // Matching scales -> identity -> the search behaves exactly as before.
            var identity = RTCalibration.FromLinearMapping(0.0, 100.0, 1.0, 0.0);
            foreach (double rt in new[] { 0.0, 12.5, 60.0, 100.0 })
                Assert.AreEqual(rt, identity.Predict(rt), 1e-9);

            // A degenerate library RT range yields no mapping.
            Assert.IsNull(RTCalibration.FromLinearMapping(5.0, 5.0, 1.0, 0.0));
            Assert.IsNull(RTCalibration.FromLinearMapping(5.0, 1.0, 1.0, 0.0));

            // Non-finite bounds are rejected rather than producing a degenerate map:
            // `libMaxRt > libMinRt` alone would accept an infinite max. Matches Rust's
            // is_finite() guard, which a malformed library could otherwise diverge on.
            Assert.IsNull(RTCalibration.FromLinearMapping(0.0, double.PositiveInfinity, 1.0, 0.0));
            Assert.IsNull(RTCalibration.FromLinearMapping(double.NegativeInfinity, 100.0, 1.0, 0.0));
            Assert.IsNull(RTCalibration.FromLinearMapping(double.NaN, 100.0, 1.0, 0.0));
            Assert.IsNull(RTCalibration.FromLinearMapping(0.0, double.NaN, 1.0, 0.0));
        }

        /// <summary>Build a linear RTCalibration spanning [xMin, xMax] with the given line.</summary>
        private static RTCalibration LineCalibration(
            double xMin, double xMax, double slope, double intercept)
        {
            const int n = 20;
            double[] x = new double[n];
            double[] y = new double[n];
            for (int i = 0; i < n; i++)
            {
                x[i] = xMin + (xMax - xMin) * i / (n - 1);
                y[i] = intercept + slope * x[i];
            }
            return new RTCalibrator(new RTCalibratorConfig
            {
                LinearFit = true,
                MinPoints = n,
                OutlierRetention = 1.0
            }).Fit(x, y);
        }

        private static CalibrationMatch Match(uint entryId, double correlationScore)
        {
            return new CalibrationMatch
            {
                EntryId = entryId,
                CorrelationScore = correlationScore,
                // ScoreCalibrationEntry seeds DiscriminantScore from the correlation
                // sum; LDA later replaces it.
                DiscriminantScore = correlationScore,
                QValue = 1.0,
            };
        }

        private static ConcurrentBag<CalibrationMatch> Bag(params CalibrationMatch[] matches)
        {
            return new ConcurrentBag<CalibrationMatch>(matches);
        }

        /// <summary>Flat (entryId, snr) pairs.</summary>
        private static ConcurrentDictionary<uint, double> Snr(params double[] pairs)
        {
            var map = new ConcurrentDictionary<uint, double>();
            for (int i = 0; i < pairs.Length; i += 2)
                map[(uint)pairs[i]] = pairs[i + 1];
            return map;
        }

        /// <summary>Flat (entryId, libRt, measuredRt) triples.</summary>
        private static ConcurrentDictionary<uint, KeyValuePair<double, double>> Rts(params double[] triples)
        {
            var map = new ConcurrentDictionary<uint, KeyValuePair<double, double>>();
            for (int i = 0; i < triples.Length; i += 3)
                map[(uint)triples[i]] = new KeyValuePair<double, double>(triples[i + 1], triples[i + 2]);
            return map;
        }

        #endregion
    }
}
