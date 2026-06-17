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
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Chromatography
{
    /// <summary>
    /// QC data for m/z calibration. Collects error measurements for MS1 and MS2.
    /// Maps to MzQCData in osprey-chromatography/src/calibration/mass.rs.
    /// </summary>
    public class MzQCData
    {
        private readonly System.Collections.Generic.List<double> _ms1Errors;
        private readonly System.Collections.Generic.List<double> _ms2Errors;
        private readonly ToleranceUnit _unit;

        /// <summary>Create new empty QC data with specified unit.</summary>
        public MzQCData(ToleranceUnit unit)
        {
            _ms1Errors = new System.Collections.Generic.List<double>();
            _ms2Errors = new System.Collections.Generic.List<double>();
            _unit = unit;
        }

        /// <summary>Create new empty QC data with PPM unit (default).</summary>
        public MzQCData() : this(ToleranceUnit.Ppm) { }

        /// <summary>Unit for mass errors.</summary>
        public ToleranceUnit Unit { get { return _unit; } }

        /// <summary>MS1 m/z errors.</summary>
        public double[] Ms1Errors { get { return _ms1Errors.ToArray(); } }

        /// <summary>MS2 m/z errors.</summary>
        public double[] Ms2Errors { get { return _ms2Errors.ToArray(); } }

        /// <summary>Number of MS1 observations.</summary>
        public int NMs1 { get { return _ms1Errors.Count; } }

        /// <summary>Number of MS2 observations.</summary>
        public int NMs2 { get { return _ms2Errors.Count; } }

        /// <summary>Add MS1 error measurement.</summary>
        public void AddMs1Error(double error) { _ms1Errors.Add(error); }

        /// <summary>Add MS2 error measurement.</summary>
        public void AddMs2Error(double error) { _ms2Errors.Add(error); }
    }

    /// <summary>
    /// m/z calibration parameters for a single MS level.
    /// Maps to MzCalibration in osprey-chromatography/src/calibration/mod.rs.
    /// </summary>
    public class MzCalibrationResult
    {
        /// <summary>Mean error (systematic offset).</summary>
        public double Mean { get; set; }

        /// <summary>Median error.</summary>
        public double Median { get; set; }

        /// <summary>Standard deviation of errors.</summary>
        public double SD { get; set; }

        /// <summary>Number of observations.</summary>
        public int Count { get; set; }

        /// <summary>Unit string ("ppm" or "Th").</summary>
        public string Unit { get; set; }

        /// <summary>Adjusted tolerance: |mean| + 3*SD.</summary>
        public double? AdjustedTolerance { get; set; }

        /// <summary>Whether calibration was successfully performed.</summary>
        public bool Calibrated { get; set; }

        /// <summary>Create uncalibrated result.</summary>
        public static MzCalibrationResult Uncalibrated()
        {
            return new MzCalibrationResult
            {
                Mean = 0.0,
                Median = 0.0,
                SD = 0.0,
                Count = 0,
                Unit = "ppm",
                AdjustedTolerance = null,
                Calibrated = false
            };
        }

        /// <summary>Get effective tolerance for matching.</summary>
        public double EffectiveTolerance(double baseTolerance)
        {
            if (Calibrated && AdjustedTolerance.HasValue)
                return AdjustedTolerance.Value;
            return baseTolerance;
        }
    }

    /// <summary>
    /// m/z calibration functions.
    /// Maps to functions in osprey-chromatography/src/calibration/mass.rs.
    /// </summary>
    public static class MzCalibration
    {
        /// <summary>
        /// Calculate m/z calibration parameters from QC data.
        /// </summary>
        /// <param name="qcData">QC data with error measurements.</param>
        /// <param name="ms1Calibration">Output MS1 calibration.</param>
        /// <param name="ms2Calibration">Output MS2 calibration.</param>
        public static void CalculateMzCalibration(MzQCData qcData,
            out MzCalibrationResult ms1Calibration, out MzCalibrationResult ms2Calibration)
        {
            ms1Calibration = CalculateSingleCalibration(qcData.Ms1Errors, qcData.Unit);
            ms2Calibration = CalculateSingleCalibration(qcData.Ms2Errors, qcData.Unit);
        }

        /// <summary>
        /// Apply m/z calibration to correct an observed m/z value.
        /// For PPM: corrected = observed - observed * mean / 1e6.
        /// For Th: corrected = observed - mean.
        /// </summary>
        public static double ApplyCalibration(double observedMz, MzCalibrationResult calibration)
        {
            if (!calibration.Calibrated)
                return observedMz;

            if (calibration.Unit == "Th")
                return observedMz - calibration.Mean;

            // PPM correction
            double correctionDa = observedMz * calibration.Mean / 1e6;
            return observedMz - correctionDa;
        }

        /// <summary>
        /// Calculate m/z error in PPM.
        /// Positive = observed > theoretical.
        /// </summary>
        public static double CalculatePpmError(double observed, double theoretical)
        {
            return ((observed - theoretical) / theoretical) * 1e6;
        }

        /// <summary>
        /// Get calibrated tolerance (PPM-based, for HRAM data).
        /// Returns 3*SD if calibrated, otherwise base tolerance. Minimum 1.0 ppm.
        /// </summary>
        public static double CalibratedTolerancePpm(MzCalibrationResult calibration,
            double baseTolerancePpm)
        {
            if (calibration.Calibrated)
            {
                double tolerance3SD = 3.0 * calibration.SD;
                return Math.Max(tolerance3SD, 1.0);
            }
            return baseTolerancePpm;
        }

        /// <summary>
        /// Get calibrated tolerance (unit-aware).
        /// Returns 3*SD in the appropriate unit (ppm or Th).
        /// </summary>
        /// <param name="calibration">Calibration parameters.</param>
        /// <param name="baseTolerance">Default tolerance if not calibrated.</param>
        /// <param name="baseUnit">Unit for the base tolerance.</param>
        /// <param name="toleranceValue">Output tolerance value.</param>
        /// <param name="toleranceUnit">Output tolerance unit.</param>
        public static void CalibratedTolerance(MzCalibrationResult calibration,
            double baseTolerance, ToleranceUnit baseUnit,
            out double toleranceValue, out ToleranceUnit toleranceUnit)
        {
            if (calibration.Calibrated)
            {
                double tolerance3SD = 3.0 * calibration.SD;
                ToleranceUnit unit = calibration.Unit == "Th" ? ToleranceUnit.Mz : ToleranceUnit.Ppm;
                double minTolerance = unit == ToleranceUnit.Mz ? 0.05 : 1.0;

                toleranceValue = Math.Max(tolerance3SD, minTolerance);
                toleranceUnit = unit;
            }
            else
            {
                toleranceValue = baseTolerance;
                toleranceUnit = baseUnit;
            }
        }

        /// <summary>
        /// Check if an m/z match is within calibrated tolerance.
        /// </summary>
        public static bool IsWithinCalibratedTolerance(double observedMz, double theoreticalMz,
            MzCalibrationResult calibration, double baseTolerancePpm)
        {
            double errorPpm = CalculatePpmError(observedMz, theoreticalMz);

            if (calibration.Calibrated)
            {
                double tolerance = calibration.AdjustedTolerance ?? baseTolerancePpm;
                return Math.Abs(errorPpm - calibration.Mean) <= tolerance;
            }

            return Math.Abs(errorPpm) <= baseTolerancePpm;
        }

        /// <summary>
        /// Calculate calibration from raw error values with string unit.
        /// </summary>
        public static MzCalibrationResult CalculateSingleLevel(double[] errors, string unitStr)
        {
            ToleranceUnit unit = unitStr == "Th" ? ToleranceUnit.Mz : ToleranceUnit.Ppm;
            return CalculateSingleCalibration(errors, unit);
        }

        private static MzCalibrationResult CalculateSingleCalibration(double[] errors,
            ToleranceUnit unit)
        {
            string unitStr = unit == ToleranceUnit.Ppm ? "ppm" : "Th";

            if (errors == null || errors.Length == 0)
            {
                return new MzCalibrationResult
                {
                    Mean = 0.0,
                    Median = 0.0,
                    SD = 0.0,
                    Count = 0,
                    Unit = unitStr,
                    AdjustedTolerance = null,
                    Calibrated = false
                };
            }

            int n = errors.Length;

            // Naive sum/n with consistent left-to-right accumulation.
            // Mirror of osprey-chromatography/src/calibration/mass.rs.
            //
            // For ppm mass-error inputs the running sum stays well below
            // the f64 precision wall (worst case ~18 k errors × ~0.22 ppm
            // ≈ 4 000, f64 ULP ~5e-13, ~9 digits headroom for the mean —
            // well clear of the 1e-9 cross-impl gate).
            //
            // The caller sorts the error list by (base_id, entry_id)
            // before this method runs, matching the Rust caller's
            // deterministic ordering. With deterministic order and .NET's
            // default IEEE 754 strict mode (no `-ffast-math`, no
            // associative re-ordering), the JIT cannot vectorise the
            // dependent reduction `sum += errors[i]`, so the loop stays
            // serial-scalar and bit-equal to the Rust mirror which is
            // similarly constrained by Rust's default IEEE 754 mode.
            //
            // The earlier Welford-Knuth recurrence was bit-equal to Rust
            // but cost ~21 s extra on Stellar Rust stage1to4 (4 ops per
            // element vs 1) for stability we did not need at ppm scale.
            double sum = 0.0;
            for (int i = 0; i < n; i++)
            {
                sum += errors[i];
            }
            double mean = sum / n;

            // Sample variance via a second left-to-right walk over
            // (x - mean)^2 contributions.  Two passes total still beat
            // Welford's single-pass 4-op recurrence on per-element cost.
            double sumSqDev = 0.0;
            for (int i = 0; i < n; i++)
            {
                double d = errors[i] - mean;
                sumSqDev += d * d;
            }
            double m2 = sumSqDev;

            // Calculate median (sort-based, unaffected by the Welford change)
            double median = LoessRegression.Median(errors);

            // Sample standard deviation: sqrt(M2 / (n-1)).
            double variance = 0;
            if (n > 1)
                variance = m2 / (n - 1);
            double sd = Math.Sqrt(variance);

            return new MzCalibrationResult
            {
                Mean = mean,
                Median = median,
                SD = sd,
                Count = n,
                Unit = unitStr,
                AdjustedTolerance = Math.Abs(mean) + 3.0 * sd,
                Calibrated = true
            };
        }
    }
}
