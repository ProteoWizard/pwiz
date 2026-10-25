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

namespace pwiz.Osprey.IO
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

        /// <summary>
        /// A name for the processing that made a run's spectra, inserted into the spectra cache's
        /// file name (<c>{stem}.{tag}.spectra.bin</c>, and likewise the demultiplexed cache), or null
        /// for the vendor's centroids and the historical name. Each cache stays a function of its
        /// source file alone: spectra Osprey centroided itself never overwrite, or stand in for, the
        /// vendor-centroided ones, and a change to the centroiding settings changes the tag.
        /// </summary>
        public static string SpectraCacheTag { get; set; }
        // The output directory the cache resolves against, when a scope from
        // ResolveCacheAs has pinned it apart from OutputDir.
        private static bool _cacheOutputDirPinned;
        private static string _cacheOutputDir;

        /// <summary>
        /// Until disposed, resolve the cache as an analysis with <paramref name="outputDir"/> as its
        /// <see cref="OutputDir"/> would, whatever <see cref="OutputDir"/> is set to. A second
        /// analysis in one process (the one command's search of its tuning runs) writes its other
        /// artifacts to a folder of its own and resolves the cache as the first analysis does, so
        /// a run both analyses read is parsed once, and a default run still never probes the data
        /// directory.
        /// </summary>
        public static IDisposable ResolveCacheAs(string outputDir)
        {
            bool pinned = _cacheOutputDirPinned;
            string previous = _cacheOutputDir;
            _cacheOutputDirPinned = true;
            _cacheOutputDir = outputDir;
            return new CacheResolutionScope(() =>
            {
                _cacheOutputDirPinned = pinned;
                _cacheOutputDir = previous;
            });
        }

        private sealed class CacheResolutionScope : IDisposable
        {
            private readonly Action _restore;

            public CacheResolutionScope(Action restore)
            {
                _restore = restore;
            }

            public void Dispose()
            {
                _restore();
            }
        }

        // Writability is probed (an ACL check is unreliable cross-platform) and
        // memoized per directory so resolution stays cheap when called for every
        // input file.
        // Ordinal (case-sensitive) keys: on a case-sensitive file system
        // (Linux/net8.0) two directories differing only by case are distinct, so
        // a case-insensitive key could reuse the wrong writability result. The
        // worst case on Windows is a couple of extra probes.
        private static readonly ConcurrentDictionary<string, bool> _writable =
            new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);

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
        /// file if that directory is writable -> <see cref="OutputDir"/> (or the
        /// directory a <see cref="ResolveCacheAs"/> scope names). The
        /// cache is settings-independent, so a shared CacheDir lets many analyses
        /// (and the straight-through vs. resume passes) reuse one parse.
        /// </summary>
        public static string ResolveCacheDir(string inputPath)
        {
            if (!string.IsNullOrEmpty(CacheDir))
                return CacheDir;
            string inputDir = InputDir(inputPath);
            string outputDir = _cacheOutputDirPinned ? _cacheOutputDir : OutputDir;
            // No redirection configured at all: this is exactly the historical
            // location, so skip the writability probe entirely (a default run
            // must not touch the data directory with a temp file).
            if (string.IsNullOrEmpty(outputDir))
                return inputDir;
            // OutputDir is set but no explicit CacheDir: prefer beside-data for
            // cross-analysis reuse, falling back to OutputDir when the data
            // directory is read-only.
            return IsDirectoryWritable(inputDir) ? inputDir : outputDir;
        }

        /// <summary>
        /// The input's directory, absolute and with the platform separator, so an input typed
        /// as D:/data/run.mzML yields D:\data\run.spectra.bin rather than D:/data\run.spectra.bin.
        /// Empty for a bare file name, which keeps the artifact relative to the working
        /// directory exactly as before.
        /// </summary>
        private static string InputDir(string inputPath)
        {
            string dir = Path.GetDirectoryName(inputPath);
            return string.IsNullOrEmpty(dir) ? string.Empty : Path.GetFullPath(dir);
        }

        private static bool IsDirectoryWritable(string dir)
        {
            string key = string.IsNullOrEmpty(dir) ? @"." : dir;
            return _writable.GetOrAdd(key, ProbeWritable);
        }

        private static bool ProbeWritable(string dir)
        {
            try
            {
                if (!Directory.Exists(dir))
                    return false;
                string probe = Path.Combine(dir, @"." + Guid.NewGuid().ToString(@"N") + @".osprey-wtest");
                // DeleteOnClose keeps the probe self-cleaning even if disposal
                // races with AV / permission edge cases -- no littered temp file.
                using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write,
                           FileShare.None, 1, FileOptions.DeleteOnClose))
                {
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
