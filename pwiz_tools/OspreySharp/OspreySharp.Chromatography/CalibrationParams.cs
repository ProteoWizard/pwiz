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
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace pwiz.OspreySharp.Chromatography
{
    /// <summary>
    /// RT calibration method.
    /// Maps to RTCalibrationMethod in osprey-chromatography/src/calibration/mod.rs.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum RTCalibrationMethod
    {
        /// <summary>No calibration performed.</summary>
        None,
        /// <summary>LOESS (locally weighted regression).</summary>
        LOESS,
        /// <summary>Linear regression (fallback).</summary>
        Linear
    }

    /// <summary>
    /// Complete calibration parameters, bundling MS1/MS2 m/z calibration and RT calibration.
    /// Maps to CalibrationParams in osprey-chromatography/src/calibration/mod.rs.
    /// </summary>
    public class CalibrationParams
    {
        /// <summary>Metadata about the calibration process.</summary>
        [JsonProperty("metadata")]
        public CalibrationMetadata Metadata { get; set; }

        /// <summary>MS1 (precursor) m/z calibration.</summary>
        [JsonProperty("ms1_calibration")]
        public MzCalibrationJson Ms1Calibration { get; set; }

        /// <summary>MS2 (fragment) m/z calibration.</summary>
        [JsonProperty("ms2_calibration")]
        public MzCalibrationJson Ms2Calibration { get; set; }

        /// <summary>Retention time calibration.</summary>
        [JsonProperty("rt_calibration")]
        public RTCalibrationJson RtCalibration { get; set; }

        /// <summary>Second-pass RT calibration (optional).</summary>
        [JsonProperty("second_pass_rt", NullValueHandling = NullValueHandling.Ignore)]
        public RTCalibrationJson SecondPassRt { get; set; }

        /// <summary>Create default uncalibrated parameters.</summary>
        public static CalibrationParams Uncalibrated()
        {
            return new CalibrationParams
            {
                Metadata = new CalibrationMetadata
                {
                    NumConfidentPeptides = 0,
                    NumSampledPrecursors = 0,
                    CalibrationSuccessful = false,
                    Timestamp = DateTime.UtcNow.ToString("o")
                },
                Ms1Calibration = MzCalibrationJson.Uncalibrated(),
                Ms2Calibration = MzCalibrationJson.Uncalibrated(),
                RtCalibration = RTCalibrationJson.Uncalibrated(),
                SecondPassRt = null
            };
        }

        /// <summary>Check if calibration was successful.</summary>
        [JsonIgnore]
        public bool IsCalibrated
        {
            get { return Metadata != null && Metadata.CalibrationSuccessful; }
        }
    }

    /// <summary>
    /// Calibration metadata.
    /// Maps to CalibrationMetadata in osprey-chromatography/src/calibration/mod.rs.
    /// </summary>
    public class CalibrationMetadata
    {
        /// <summary>Number of confident peptides used for calibration.</summary>
        [JsonProperty("num_confident_peptides")]
        public int NumConfidentPeptides { get; set; }

        /// <summary>Number of precursors sampled for calibration discovery.</summary>
        [JsonProperty("num_sampled_precursors")]
        public int NumSampledPrecursors { get; set; }

        /// <summary>Whether calibration was successful.</summary>
        [JsonProperty("calibration_successful")]
        public bool CalibrationSuccessful { get; set; }

        /// <summary>Timestamp when calibration was performed.</summary>
        [JsonProperty("timestamp")]
        public string Timestamp { get; set; }

        /// <summary>DIA isolation window scheme (optional).</summary>
        [JsonProperty("isolation_scheme", NullValueHandling = NullValueHandling.Ignore)]
        public IsolationSchemeJson IsolationScheme { get; set; }

        /// <summary>SHA-256 hash of search parameters (optional).</summary>
        [JsonProperty("search_hash", NullValueHandling = NullValueHandling.Ignore)]
        public string SearchHash { get; set; }
    }

    /// <summary>
    /// DIA isolation window scheme.
    /// Maps to IsolationScheme in osprey-chromatography/src/calibration/mod.rs.
    /// </summary>
    public class IsolationSchemeJson
    {
        /// <summary>Number of isolation windows per cycle.</summary>
        [JsonProperty("num_windows")]
        public int NumWindows { get; set; }

        /// <summary>Minimum isolation window center m/z.</summary>
        [JsonProperty("mz_min")]
        public double MzMin { get; set; }

        /// <summary>Maximum isolation window center m/z.</summary>
        [JsonProperty("mz_max")]
        public double MzMax { get; set; }

        /// <summary>Typical isolation window width (Da).</summary>
        [JsonProperty("typical_width")]
        public double TypicalWidth { get; set; }

        /// <summary>Whether all windows have the same width.</summary>
        [JsonProperty("uniform_width")]
        public bool UniformWidth { get; set; }
    }

    /// <summary>
    /// m/z calibration parameters for JSON serialization.
    /// Maps to MzCalibration in osprey-chromatography/src/calibration/mod.rs.
    /// </summary>
    public class MzCalibrationJson
    {
        /// <summary>Mean error (systematic offset).</summary>
        [JsonProperty("mean")]
        public double Mean { get; set; }

        /// <summary>Median error.</summary>
        [JsonProperty("median")]
        public double Median { get; set; }

        /// <summary>Standard deviation of errors.</summary>
        [JsonProperty("sd")]
        public double SD { get; set; }

        /// <summary>Number of observations.</summary>
        [JsonProperty("count")]
        public int Count { get; set; }

        /// <summary>Unit ("ppm" or "Th").</summary>
        [JsonProperty("unit")]
        public string Unit { get; set; }

        /// <summary>Adjusted tolerance: |mean| + 3*SD.</summary>
        [JsonProperty("adjusted_tolerance", NullValueHandling = NullValueHandling.Ignore)]
        public double? AdjustedTolerance { get; set; }

        /// <summary>Window halfwidth multiplier.</summary>
        [JsonProperty("window_halfwidth_multiplier", NullValueHandling = NullValueHandling.Ignore)]
        public double? WindowHalfwidthMultiplier { get; set; }

        /// <summary>Histogram of errors (optional).</summary>
        [JsonProperty("histogram", NullValueHandling = NullValueHandling.Ignore)]
        public MzHistogramJson Histogram { get; set; }

        /// <summary>Whether calibration was successfully performed.</summary>
        [JsonProperty("calibrated")]
        public bool Calibrated { get; set; }

        /// <summary>Create uncalibrated parameters.</summary>
        public static MzCalibrationJson Uncalibrated()
        {
            return new MzCalibrationJson
            {
                Mean = 0.0,
                Median = 0.0,
                SD = 0.0,
                Count = 0,
                Unit = "ppm",
                AdjustedTolerance = null,
                WindowHalfwidthMultiplier = null,
                Histogram = null,
                Calibrated = false
            };
        }

        /// <summary>
        /// Build a JSON DTO from an in-memory MzCalibrationResult.
        /// Returns Uncalibrated() when <paramref name="r"/> is null or
        /// not calibrated.
        /// </summary>
        public static MzCalibrationJson FromResult(MzCalibrationResult r)
        {
            if (r == null || !r.Calibrated)
                return Uncalibrated();
            return new MzCalibrationJson
            {
                Mean = r.Mean,
                Median = r.Median,
                SD = r.SD,
                Count = r.Count,
                Unit = r.Unit,
                AdjustedTolerance = r.AdjustedTolerance,
                WindowHalfwidthMultiplier = 3.0,
                Histogram = null,
                Calibrated = true
            };
        }
    }

    /// <summary>
    /// Histogram data for m/z error distribution.
    /// Maps to MzHistogram in osprey-chromatography/src/calibration/mod.rs.
    /// </summary>
    public class MzHistogramJson
    {
        /// <summary>Bin edges (N+1 edges for N bins).</summary>
        [JsonProperty("bin_edges")]
        public double[] BinEdges { get; set; }

        /// <summary>Counts in each bin.</summary>
        [JsonProperty("counts")]
        public int[] Counts { get; set; }

        /// <summary>Bin width.</summary>
        [JsonProperty("bin_width")]
        public double BinWidth { get; set; }
    }

    /// <summary>
    /// RT calibration parameters for JSON serialization.
    /// Maps to RTCalibrationParams in osprey-chromatography/src/calibration/mod.rs.
    /// </summary>
    public class RTCalibrationJson
    {
        /// <summary>Calibration method used.</summary>
        [JsonProperty("method")]
        public RTCalibrationMethod Method { get; set; }

        /// <summary>Residual standard deviation.</summary>
        [JsonProperty("residual_sd")]
        public double ResidualSD { get; set; }

        /// <summary>Number of calibration points.</summary>
        [JsonProperty("n_points")]
        public int NPoints { get; set; }

        /// <summary>R-squared (coefficient of determination).</summary>
        [JsonProperty("r_squared")]
        public double RSquared { get; set; }

        /// <summary>LOESS model parameters for reconstruction (optional).</summary>
        [JsonProperty("model_params", NullValueHandling = NullValueHandling.Ignore)]
        public RTModelParamsJson ModelParams { get; set; }

        /// <summary>20th percentile of absolute residuals.</summary>
        [JsonProperty("p20_abs_residual", NullValueHandling = NullValueHandling.Ignore)]
        public double? P20AbsResidual { get; set; }

        /// <summary>Median absolute deviation of residuals.</summary>
        [JsonProperty("mad", NullValueHandling = NullValueHandling.Ignore)]
        public double? MAD { get; set; }

        /// <summary>Check if RT was calibrated.</summary>
        [JsonIgnore]
        public bool IsCalibrated
        {
            get { return Method != RTCalibrationMethod.None && NPoints > 0; }
        }

        /// <summary>Create uncalibrated parameters.</summary>
        public static RTCalibrationJson Uncalibrated()
        {
            return new RTCalibrationJson
            {
                Method = RTCalibrationMethod.None,
                ResidualSD = 0.0,
                NPoints = 0,
                RSquared = 0.0,
                ModelParams = null,
                P20AbsResidual = null,
                MAD = null
            };
        }

        /// <summary>
        /// Build a JSON DTO from an in-memory RTCalibration.
        /// Returns Uncalibrated() when <paramref name="rt"/> is null.
        /// ModelParams is populated so SaveCalibration+LoadCalibration
        /// round-trips the LOESS fit exactly.
        /// </summary>
        public static RTCalibrationJson FromRTCalibration(RTCalibration rt)
        {
            if (rt == null)
                return Uncalibrated();
            var stats = rt.Stats();
            return new RTCalibrationJson
            {
                Method = RTCalibrationMethod.LOESS,
                ResidualSD = stats.ResidualSD,
                NPoints = stats.NPoints,
                RSquared = stats.RSquared,
                P20AbsResidual = stats.P20AbsResidual,
                MAD = stats.MAD,
                ModelParams = new RTModelParamsJson
                {
                    LibraryRts = rt.LibraryRts,
                    FittedRts = rt.FittedValues,
                    AbsResiduals = rt.AbsResiduals
                }
            };
        }
    }

    /// <summary>
    /// LOESS model parameters for serialization.
    /// Maps to RTModelParams in osprey-chromatography/src/calibration/mod.rs.
    /// </summary>
    public class RTModelParamsJson
    {
        /// <summary>Library retention times (sorted).</summary>
        [JsonProperty("library_rts")]
        public double[] LibraryRts { get; set; }

        /// <summary>Fitted measured retention times.</summary>
        [JsonProperty("fitted_rts")]
        public double[] FittedRts { get; set; }

        /// <summary>Absolute residuals at each calibration point.</summary>
        [JsonProperty("abs_residuals")]
        public double[] AbsResiduals { get; set; }
    }
}
