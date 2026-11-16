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
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
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
        // A '/'-rooted directory, a 'C:\'-rooted one, and a Windows one typed with '/'.
        private static readonly string[] PATH_STYLE_DIRS = { @"/runs/plate1", @"C:\runs\plate1", @"D:/runs/plate1" };

        // Helper: run an action with ArtifactPaths overrides set, always resetting
        // the process-wide statics afterward so other tests see the default
        // (null = each input file's own directory). Shared with other tests that
        // depend on where artifacts resolve.
        internal static void WithArtifactDirs(string outputDir, string cacheDir, Action body)
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
                    // FDR / reconciliation sidecars route through the same dir.
                    Assert.AreEqual(Path.Combine(inputDir, "sample.1st-pass.fdr_scores.bin"),
                        FdrScoresSidecar.Pass1Path(input));
                    Assert.AreEqual(Path.Combine(inputDir, "sample.2nd-pass.fdr_scores.bin"),
                        FdrScoresSidecar.Pass2Path(input));
                    Assert.AreEqual(Path.Combine(inputDir, "sample.reconciliation.json"),
                        ReconciliationFile.PathForInput(input));
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
                    Assert.AreEqual(Path.Combine(outDir, "sample.1st-pass.fdr_scores.bin"),
                        FdrScoresSidecar.Pass1Path(input));
                    Assert.AreEqual(Path.Combine(outDir, "sample.reconciliation.json"),
                        ReconciliationFile.PathForInput(input));
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
            OspreyConfig work = Program.ParseArgs(ArgTokens.Split(OspreyCommandArgs.ARG_WORK_DIR + @"W"));
            Assert.AreEqual("W", work.OutputDir);
            Assert.AreEqual("W", work.CacheDir);

            OspreyConfig split = Program.ParseArgs(ArgTokens.Split(OspreyCommandArgs.ARG_WORK_DIR + @"W", OspreyCommandArgs.ARG_OUTPUT_DIR + @"O", OspreyCommandArgs.ARG_CACHE_DIR + @"C"));
            Assert.AreEqual("O", split.OutputDir);
            Assert.AreEqual("C", split.CacheDir);

            OspreyConfig outOnly = Program.ParseArgs(ArgTokens.Split(OspreyCommandArgs.ARG_WORK_DIR + @"W", OspreyCommandArgs.ARG_OUTPUT_DIR + @"O"));
            Assert.AreEqual("O", outOnly.OutputDir);
            Assert.AreEqual("W", outOnly.CacheDir);

            OspreyConfig none = Program.ParseArgs(ArgTokens.Split(OspreyCommandArgs.ARG_INPUT + @"x.mzML"));
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

        /// <summary>
        /// The paths Osprey derives from the ones it is given come out the same way from a
        /// '/'-rooted path, a 'C:\'-rooted one and a 'D:/'-rooted one, on Windows and on Linux:
        /// every per-run artifact, and every artifact named for the output blib, is the input's
        /// (or blib's) name with the artifact's suffix, in the directory the input names, with no
        /// mixed separators; the library format comes from the extension whatever the
        /// directory looks like; and a file's identity hash ignores its directory. On Linux a
        /// backslash is an ordinary file-name character, so 'C:\runs\plate1\sample.mzML' is one
        /// file name in the working directory - the assertions below hold for that reading too,
        /// and the Linux-only pins say so explicitly.
        /// </summary>
        [TestMethod]
        public void TestPathDerivationsAcrossPathStyles()
        {
            WithArtifactDirs(null, null, () =>
            {
                foreach (string dir in PATH_STYLE_DIRS)
                {
                    string input = StylePath(dir, @"sample.mzML");
                    AssertArtifactPath(input, ParquetScoreCache.GetScoresPath(input), ParquetScoreCache.EXT_SCORES);
                    AssertArtifactPath(input, ParquetScoreCache.GetReconciledScoresPath(input), ParquetScoreCache.EXT_SCORES_RECONCILED);
                    AssertArtifactPath(input, SpectraCache.GetCachePath(input), @".spectra.bin");
                    AssertArtifactPath(input, FdrScoresSidecar.Pass1Path(input), @".1st-pass.fdr_scores.bin");
                    AssertArtifactPath(input, ReconciliationFile.PathForInput(input), @".reconciliation.json");

                    // Artifacts named for the blib land in the input's directory, whichever
                    // style the blib path was typed in.
                    foreach (string blibDir in PATH_STYLE_DIRS)
                    {
                        var config = new OspreyConfig
                        {
                            InputFiles = new List<string> { input },
                            OutputBlib = StylePath(blibDir, @"out.blib")
                        };
                        AssertBlibArtifactPath(input, config, FirstPassModelIO.PathFor(config));
                        AssertBlibArtifactPath(input, config, Stage6ConsensusFile.PathFor(config));
                        AssertBlibArtifactPath(input, config, RetainedBaseIdSidecar.PathFor(config.OutputBlib, input));
                        AssertBlibArtifactPath(input, config,
                            FdrExperimentSidecar.PathFor(config.OutputBlib, input, FdrScoresSidecar.Pass.FirstPass));
                    }

                    AssertLibraryFormat(StylePath(dir, @"lib.blib"), LibraryFormat.Blib);
                    AssertLibraryFormat(StylePath(dir, @"lib.tsv"), LibraryFormat.DiannTsv);
                    AssertLibraryFormat(StylePath(dir, @"lib.sky"), LibraryFormat.SkylineDocument);
                }

                // The mixed-separator case: a Windows path typed with forward slashes gets the
                // platform separator; on Linux the forward-slash path is already native.
                if (OperatingSystem.IsWindows())
                {
                    Assert.AreEqual(@"D:\runs\plate1\sample.scores.parquet",
                        ParquetScoreCache.GetScoresPath(@"D:/runs/plate1/sample.mzML"));
                }
                else
                {
                    Assert.AreEqual(@"/runs/plate1/sample.scores.parquet",
                        ParquetScoreCache.GetScoresPath(@"/runs/plate1/sample.mzML"));
                    Assert.AreEqual(@"C:\runs\plate1\sample.scores.parquet",
                        ParquetScoreCache.GetScoresPath(@"C:\runs\plate1\sample.mzML"));
                }
            });

            // A file's identity is its name, size and mtime, never its directory, so one library
            // identifies the same from any directory a forward-slash path names, on either
            // platform. A backslash path names the same file only where backslash separates. The
            // directories are fresh names, so no file exists to add a size and mtime to one side.
            string absent = @"osprey_absent_" + Guid.NewGuid().ToString(@"N");
            string identity = SearchIdentity.FileIdentityTerm(@"/" + absent + @"/lib.tsv");
            Assert.AreEqual(identity, SearchIdentity.FileIdentityTerm(@"D:/" + absent + @"/data/lib.tsv"));
            Assert.AreEqual(identity, SearchIdentity.FileIdentityTerm(absent + @"/lib.tsv"));
            Assert.AreEqual(OperatingSystem.IsWindows(),
                identity == SearchIdentity.FileIdentityTerm(@"C:\" + absent + @"\lib.tsv"));

            // Two paths name one file as the file system compares names: case matters on Linux
            // only, and the relative parts resolve first everywhere.
            Assert.AreEqual(!OperatingSystem.IsLinux(), PathComparison.IsSamePath(@"/runs/A.mzML", @"/runs/a.mzML"));
            Assert.AreEqual(!OperatingSystem.IsLinux(), PathComparison.Comparer.Equals(@"Run1", @"run1"));
            Assert.IsTrue(PathComparison.IsSamePath(@"/runs/x/../A.mzML", @"/runs/A.mzML"));
        }

        /// <summary>
        /// <paramref name="fileName"/> in <paramref name="dir"/>, joined with the separator the
        /// directory itself uses - as a user would type it - rather than the platform's.
        /// </summary>
        private static string StylePath(string dir, string fileName)
        {
            return dir + (dir.IndexOf('\\') >= 0 ? @"\" : @"/") + fileName;
        }

        /// <summary>
        /// <paramref name="derived"/> is <paramref name="input"/>'s name without its extension
        /// plus <paramref name="suffix"/>, in the directory <paramref name="input"/> names.
        /// </summary>
        private static void AssertArtifactPath(string input, string derived, string suffix)
        {
            AssertDerivedPath(input, Path.GetFileNameWithoutExtension(input) + suffix, derived);
        }

        /// <summary>
        /// <paramref name="derived"/> is named for the configuration's output blib and lies in
        /// the directory <paramref name="input"/> names, not the blib's.
        /// </summary>
        private static void AssertBlibArtifactPath(string input, OspreyConfig config, string derived)
        {
            Assert.IsNotNull(derived, config.OutputBlib);
            StringAssert.StartsWith(Path.GetFileName(derived), Path.GetFileNameWithoutExtension(config.OutputBlib) + @".", derived);
            AssertDerivedPath(input, Path.GetFileName(derived), derived);
        }

        private static void AssertDerivedPath(string input, string expectedName, string derived)
        {
            Assert.AreEqual(expectedName, Path.GetFileName(derived), input);
            Assert.AreEqual(Path.GetDirectoryName(Path.GetFullPath(input)), Path.GetDirectoryName(Path.GetFullPath(derived)), input);
            if (Path.DirectorySeparatorChar != Path.AltDirectorySeparatorChar)
                Assert.AreEqual(-1, derived.IndexOf(Path.AltDirectorySeparatorChar), derived);
        }

        private static void AssertLibraryFormat(string path, LibraryFormat expected)
        {
            var source = LibrarySource.FromPath(path);
            Assert.AreEqual(expected, source.Format, path);
            Assert.AreEqual(path, source.Path);
        }
    }
}
