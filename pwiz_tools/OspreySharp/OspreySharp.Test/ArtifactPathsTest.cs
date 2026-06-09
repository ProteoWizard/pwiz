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

using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.IO;

namespace pwiz.OspreySharp.Test
{
    /// <summary>
    /// Tests for the output / cache directory decoupling: the
    /// <see cref="ArtifactPaths"/> resolver, the artifact path helpers that route
    /// through it, the <c>--work-dir</c>/<c>--output-dir</c>/<c>--cache-dir</c>
    /// CLI precedence, and the spectra-cache source fingerprint.
    /// </summary>
    [TestClass]
    public class ArtifactPathsTest
    {
        // Helper: run an action with ArtifactPaths overrides set, always resetting
        // the process-wide statics afterward so other tests see the default
        // (null = each input file's own directory).
        private static void WithArtifactDirs(string outputDir, string cacheDir, System.Action body)
        {
            string savedOutput = ArtifactPaths.OutputDir;
            string savedCache = ArtifactPaths.CacheDir;
            try
            {
                ArtifactPaths.OutputDir = outputDir;
                ArtifactPaths.CacheDir = cacheDir;
                body();
            }
            finally
            {
                ArtifactPaths.OutputDir = savedOutput;
                ArtifactPaths.CacheDir = savedCache;
            }
        }

        /// <summary>
        /// ResolveOutputDir / ResolveCacheDir and the path helpers honor the
        /// configured directories, and the default (no overrides) is byte-identical
        /// to writing in the input file's own directory.
        /// </summary>
        [TestMethod]
        public void TestArtifactPathsResolution()
        {
            string inputDir = Path.Combine(Path.GetTempPath(), "osprey_ap_" + Path.GetRandomFileName());
            Directory.CreateDirectory(inputDir);
            try
            {
                string input = Path.Combine(inputDir, "sample.mzML");

                // Default: no overrides -> input file's own directory, identical to
                // the historical paths.
                WithArtifactDirs(null, null, () =>
                {
                    Assert.AreEqual(inputDir, ArtifactPaths.ResolveOutputDir(input));
                    Assert.AreEqual(inputDir, ArtifactPaths.ResolveCacheDir(input));
                    Assert.AreEqual(Path.Combine(inputDir, "sample.scores.parquet"),
                        ParquetScoreCache.GetScoresPath(input));
                    Assert.AreEqual(Path.Combine(inputDir, "sample.scores-reconciled.parquet"),
                        ParquetScoreCache.GetReconciledScoresPath(input));
                    Assert.AreEqual(Path.Combine(inputDir, "sample.spectra.bin"),
                        SpectraCache.GetCachePath(input));
                });

                // OutputDir set redirects the non-cache artifacts; CacheDir explicit
                // redirects the spectra cache.
                string outDir = Path.Combine(Path.GetTempPath(), "osprey_out_" + Path.GetRandomFileName());
                string cacheDir = Path.Combine(Path.GetTempPath(), "osprey_cache_" + Path.GetRandomFileName());
                WithArtifactDirs(outDir, cacheDir, () =>
                {
                    Assert.AreEqual(outDir, ArtifactPaths.ResolveOutputDir(input));
                    Assert.AreEqual(cacheDir, ArtifactPaths.ResolveCacheDir(input));
                    Assert.AreEqual(Path.Combine(outDir, "sample.scores.parquet"),
                        ParquetScoreCache.GetScoresPath(input));
                    Assert.AreEqual(Path.Combine(cacheDir, "sample.spectra.bin"),
                        SpectraCache.GetCachePath(input));
                });

                // CacheDir unset + OutputDir set: prefer beside-data when writable
                // (inputDir exists here), so the cache stays beside the data.
                WithArtifactDirs(outDir, null, () =>
                    Assert.AreEqual(inputDir, ArtifactPaths.ResolveCacheDir(input)));

                // CacheDir unset + OutputDir set + a non-writable (here:
                // non-existent) data directory -> fall back to OutputDir.
                string missingDir = Path.Combine(inputDir, "does_not_exist");
                string missingInput = Path.Combine(missingDir, "sample.mzML");
                WithArtifactDirs(outDir, null, () =>
                    Assert.AreEqual(outDir, ArtifactPaths.ResolveCacheDir(missingInput)));
            }
            finally
            {
                Directory.Delete(inputDir, true);
            }
        }

        /// <summary>
        /// --work-dir sets both output and cache directories; an explicit
        /// --output-dir / --cache-dir overrides the matching component.
        /// </summary>
        [TestMethod]
        public void TestWorkOutputCacheDirPrecedence()
        {
            OspreyConfig work = Program.ParseArgs(new[] { "--work-dir", "W" });
            Assert.AreEqual("W", work.OutputDir);
            Assert.AreEqual("W", work.CacheDir);

            OspreyConfig split = Program.ParseArgs(new[] { "--work-dir", "W", "--output-dir", "O", "--cache-dir", "C" });
            Assert.AreEqual("O", split.OutputDir);
            Assert.AreEqual("C", split.CacheDir);

            OspreyConfig outOnly = Program.ParseArgs(new[] { "--work-dir", "W", "--output-dir", "O" });
            Assert.AreEqual("O", outOnly.OutputDir);
            Assert.AreEqual("W", outOnly.CacheDir);

            OspreyConfig none = Program.ParseArgs(new[] { "-i", "x.mzML" });
            Assert.IsNull(none.OutputDir);
            Assert.IsNull(none.CacheDir);
        }

        /// <summary>
        /// The spectra cache rejects a cache whose source file changed (size /
        /// mtime), reloads when the source matches, and skips the check when no
        /// fingerprint was recorded or the source is unavailable.
        /// </summary>
        [TestMethod]
        public void TestSpectraCacheFingerprint()
        {
            string dir = Path.Combine(Path.GetTempPath(), "osprey_fp_" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            try
            {
                string source = Path.Combine(dir, "sample.mzML");
                File.WriteAllText(source, "fake mzML contents");
                string cachePath = Path.Combine(dir, "sample.spectra.bin");

                var ms2 = new List<Spectrum>
                {
                    new Spectrum
                    {
                        ScanNumber = 1, RetentionTime = 10.0, PrecursorMz = 500.0,
                        IsolationWindow = new IsolationWindow(500.0, 1.0, 1.0),
                        Mzs = new[] { 100.0, 200.0 }, Intensities = new[] { 1f, 2f }
                    }
                };
                var ms1 = new List<MS1Spectrum>
                {
                    new MS1Spectrum
                    {
                        ScanNumber = 2, RetentionTime = 10.5,
                        Mzs = new[] { 300.0 }, Intensities = new[] { 3f }
                    }
                };

                // Fingerprinted cache loads when the source is unchanged.
                SpectraCache.SaveSpectraCache(cachePath, ms2, ms1, source);
                var loaded = SpectraCache.LoadSpectraCache(cachePath, source);
                Assert.IsNotNull(loaded);
                Assert.AreEqual(1, loaded.Ms2Spectra.Count);
                Assert.AreEqual(1, loaded.Ms1Spectra.Count);

                // Loading without a source path skips the check (resume path where
                // the mzML is not beside the cache).
                Assert.IsNotNull(SpectraCache.LoadSpectraCache(cachePath));

                // A changed source (size + mtime) invalidates the cache.
                File.WriteAllText(source, "fake mzML contents that are now longer");
                Assert.IsNull(SpectraCache.LoadSpectraCache(cachePath, source));

                // A cache written without a fingerprint is accepted even with a
                // source path supplied (storedSize == 0 -> no check).
                SpectraCache.SaveSpectraCache(cachePath, ms2, ms1);
                Assert.IsNotNull(SpectraCache.LoadSpectraCache(cachePath, source));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
