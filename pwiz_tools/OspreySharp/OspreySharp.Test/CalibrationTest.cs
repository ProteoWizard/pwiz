using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Test
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
            Assert.AreEqual("sample.calibration.json",
                CalibrationIO.CalibrationFilenameForInput(@"C:\data\sample.mzML"));
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
                    MAD = 0.12
                }
            };

            // Serialize to JSON string
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(calibration,
                Newtonsoft.Json.Formatting.Indented);

            Assert.IsTrue(json.Contains("\"ms1_calibration\""));
            Assert.IsTrue(json.Contains("-2.5"));

            // Deserialize back
            var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<CalibrationParams>(json);
            Assert.IsTrue(loaded.IsCalibrated);
            Assert.IsTrue(Math.Abs(loaded.Ms1Calibration.Mean + 2.5) < 0.001);
            Assert.AreEqual(RTCalibrationMethod.LOESS, loaded.RtCalibration.Method);
            Assert.IsNotNull(loaded.RtCalibration.ModelParams);
            Assert.AreEqual(4, loaded.RtCalibration.ModelParams.LibraryRts.Length);
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
    }
}
