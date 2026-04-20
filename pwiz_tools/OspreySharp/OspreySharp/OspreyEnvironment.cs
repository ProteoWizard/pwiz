/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
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

namespace pwiz.OspreySharp
{
    /// <summary>
    /// Central access point for OSPREY_* environment variables that control
    /// production behavior (throttling, fast-iteration early exits, algorithm
    /// variants). A separate OspreyDiagnostics class (coming next) covers the
    /// diagnostic-dump env vars. Values are read once at process start and
    /// cached as readonly static fields so callers never reach for
    /// <see cref="Environment.GetEnvironmentVariable(string)"/> inline.
    /// </summary>
    public static class OspreyEnvironment
    {
        /// <summary>
        /// OSPREY_MAX_PARALLEL_FILES: cap on concurrent file processing in the
        /// Parallel.For over input files. Values:
        ///   0 / unset = use .NET default (all files at once)
        ///   1        = strictly sequential
        ///   N &gt; 1    = at most N files concurrently
        /// Useful for memory-bound datasets (Astral HRAM) where three 30 GB
        /// working sets exceed a 64 GB budget.
        /// </summary>
        public static readonly int MaxParallelFiles = ParseIntOrZero(@"OSPREY_MAX_PARALLEL_FILES");

        /// <summary>
        /// OSPREY_MAX_SCORING_WINDOWS: limits main-search isolation windows
        /// scored in Stage 4. Used for fast iteration during dotTrace
        /// profiling and parity bisection. 0 or unset means "score them all".
        /// </summary>
        public static readonly int MaxScoringWindows = ParseIntOrZero(@"OSPREY_MAX_SCORING_WINDOWS");

        /// <summary>
        /// OSPREY_LOESS_CLASSICAL_ROBUST: use classical Cleveland (1979) robust
        /// LOESS iteration (residuals recomputed from the current fit each
        /// pass) instead of the default that caches absolute residuals from
        /// the initial fit. The default matches Rust calibration_ml.rs out of
        /// the box; set this in both tools together when validating a
        /// potential upstream fix.
        /// </summary>
        public static readonly bool LoessClassicalRobust = IsOne(@"OSPREY_LOESS_CLASSICAL_ROBUST");

        /// <summary>
        /// OSPREY_EXIT_AFTER_CALIBRATION: exit after Stage 3 (calibration
        /// complete), skipping Stage 4 main search and everything downstream.
        /// Used for calibration-only benchmarking and bisection.
        /// </summary>
        public static readonly bool ExitAfterCalibration = IsSet(@"OSPREY_EXIT_AFTER_CALIBRATION");

        // Note: the OSPREY_EXIT_AFTER_SCORING env var that used to live here
        // was retired in favor of the --no-join CLI flag. See the HPC
        // scoring split work in AnalysisPipeline.Run. ExitAfterCalibration
        // (Stage 3) stays because it has no production CLI analog.

        /// <summary>
        /// OSPREY_LOAD_CALIBRATION: path to a .calibration.json produced by
        /// the Rust implementation. When set and the file exists, Stage 3 is
        /// skipped and the Rust calibration is loaded directly. Used for
        /// feature-parity bisection (isolates downstream feature divergence
        /// from calibration drift).
        /// </summary>
        public static readonly string LoadCalibrationPath = Environment.GetEnvironmentVariable(@"OSPREY_LOAD_CALIBRATION");

        private static int ParseIntOrZero(string name)
        {
            string v = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(v))
                return 0;
            int.TryParse(v, out int result);
            return result;
        }

        private static bool IsOne(string name)
        {
            return Environment.GetEnvironmentVariable(name) == @"1";
        }

        private static bool IsSet(string name)
        {
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name));
        }
    }
}
