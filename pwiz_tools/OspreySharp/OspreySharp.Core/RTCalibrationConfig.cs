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

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// RT Calibration configuration.
    /// Controls how library retention times are calibrated against measured RTs.
    /// Uses LOESS (Local Polynomial Regression) for calibration.
    /// Maps to osprey-core/src/config.rs RTCalibrationConfig.
    /// </summary>
    public class RTCalibrationConfig
    {
        /// <summary>Enable RT calibration.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>LOESS bandwidth (0.0-1.0, fraction of data to use for local fits).</summary>
        public double LoessBandwidth { get; set; } = 0.3;

        /// <summary>Minimum detections required for calibration.</summary>
        public int MinCalibrationPoints { get; set; } = 200;

        /// <summary>RT tolerance multiplier (x residual SD) for calibrated search.</summary>
        public double RtToleranceFactor { get; set; } = 3.0;

        /// <summary>Fallback RT tolerance (minutes) if calibration fails.</summary>
        public double FallbackRtTolerance { get; set; } = 2.0;

        /// <summary>Minimum RT tolerance (minutes) floor for local tolerance calculation.</summary>
        public double MinRtTolerance { get; set; } = 0.5;

        /// <summary>Number of target peptides to sample for calibration (0 = use all).</summary>
        public int CalibrationSampleSize { get; set; } = 100000;

        /// <summary>Multiplier for expanding sample on retry if too few calibration points.</summary>
        public double CalibrationRetryFactor { get; set; } = 2.0;

        /// <summary>Maximum RT tolerance (minutes). Hard cap to prevent catastrophic widening.</summary>
        public double MaxRtTolerance { get; set; } = 3.0;

        /// <summary>Create enabled calibration with default settings.</summary>
        public static RTCalibrationConfig CreateEnabled()
        {
            return new RTCalibrationConfig();
        }

        /// <summary>Create disabled calibration (uses fallback RT tolerance).</summary>
        public static RTCalibrationConfig CreateDisabled()
        {
            return new RTCalibrationConfig { Enabled = false };
        }
    }
}
