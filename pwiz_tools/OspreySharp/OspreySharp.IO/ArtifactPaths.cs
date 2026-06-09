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
using System.Collections.Concurrent;
using System.IO;

namespace pwiz.OspreySharp.IO
{
    /// <summary>
    /// Process-wide overrides for where per-file <em>derived</em> artifacts are
    /// written, set once from the parsed <c>OspreyConfig</c> at startup (see
    /// <c>Program.Main</c>). Every per-file artifact path helper -- the scores
    /// parquet (<see cref="ParquetScoreCache.GetScoresPath"/>) and its reconciled
    /// sibling, the spectra cache (<see cref="SpectraCache.GetCachePath"/>), the
    /// calibration JSON, and the FDR / reconciliation sidecars (which hang off the
    /// scores-parquet path) -- resolves its directory through here, so the
    /// output-dir / cache-dir redirection is applied to <em>all</em> artifacts
    /// atomically and can never be applied to some while missed on others.
    ///
    /// Both properties default to null, which preserves the historical behavior
    /// of writing each artifact in its input file's own directory.
    /// <c>--work-dir</c> sets both; <c>--output-dir</c> / <c>--cache-dir</c> set
    /// them individually. Maps to Rust <c>OspreyConfig::output_dir</c> /
    /// <c>cache_dir</c> (Track B).
    /// </summary>
    public static class ArtifactPaths
    {
        /// <summary>
        /// Base directory for non-cache derived artifacts, or null to use each
        /// input file's own directory. Maps to <c>OspreyConfig.OutputDir</c>.
        /// </summary>
        public static string OutputDir { get; set; }

        /// <summary>
        /// Directory for the <c>.spectra.bin</c> cache, or null to resolve at
        /// write time (beside the data file if writable, else
        /// <see cref="OutputDir"/>). Maps to <c>OspreyConfig.CacheDir</c>.
        /// </summary>
        public static string CacheDir { get; set; }

        // Writability is probed (an ACL check is unreliable cross-platform) and
        // memoized per directory so resolution stays cheap when called for every
        // input file.
        private static readonly ConcurrentDictionary<string, bool> _writable =
            new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Directory for a non-cache derived artifact of
        /// <paramref name="inputPath"/>: <see cref="OutputDir"/> when set, else
        /// the input file's own directory (the historical default).
        /// </summary>
        public static string ResolveOutputDir(string inputPath)
        {
            if (!string.IsNullOrEmpty(OutputDir))
                return OutputDir;
            return InputDir(inputPath);
        }

        /// <summary>
        /// Directory for the spectra cache of <paramref name="inputPath"/>.
        /// Resolution order: explicit <see cref="CacheDir"/> -> beside the data
        /// file if that directory is writable -> <see cref="OutputDir"/>. The
        /// cache is settings-independent, so a shared CacheDir lets many analyses
        /// (and the straight-through vs. resume passes) reuse one parse.
        /// </summary>
        public static string ResolveCacheDir(string inputPath)
        {
            if (!string.IsNullOrEmpty(CacheDir))
                return CacheDir;
            string inputDir = InputDir(inputPath);
            // No redirection configured at all: this is exactly the historical
            // location, so skip the writability probe entirely (a default run
            // must not touch the data directory with a temp file).
            if (string.IsNullOrEmpty(OutputDir))
                return inputDir;
            // OutputDir is set but no explicit CacheDir: prefer beside-data for
            // cross-analysis reuse, falling back to OutputDir when the data
            // directory is read-only.
            return IsDirectoryWritable(inputDir) ? inputDir : OutputDir;
        }

        private static string InputDir(string inputPath)
        {
            return Path.GetDirectoryName(inputPath) ?? string.Empty;
        }

        private static bool IsDirectoryWritable(string dir)
        {
            string key = string.IsNullOrEmpty(dir) ? "." : dir;
            return _writable.GetOrAdd(key, ProbeWritable);
        }

        private static bool ProbeWritable(string dir)
        {
            try
            {
                if (!Directory.Exists(dir))
                    return false;
                string probe = Path.Combine(dir, "." + Guid.NewGuid().ToString("N") + ".osprey-wtest");
                using (File.Create(probe))
                {
                }
                File.Delete(probe);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
