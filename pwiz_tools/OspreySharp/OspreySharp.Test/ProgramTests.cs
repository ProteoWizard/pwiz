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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.IO;

namespace pwiz.OspreySharp.Test
{
    /// <summary>
    /// Tests for OspreySharp Program-level helpers: the HPC scoring split
    /// flag validation (Program.ValidateArgs) and the --input-scores
    /// directory expansion (Program.ResolveInputScores).
    ///
    /// These are unit tests of CLI argument plumbing only. End-to-end
    /// scoring round-trip (Stages 1-4 → parquet → Stage 5+) is exercised
    /// by perf-class tests against the Stellar dataset.
    /// </summary>
    [TestClass]
    public class ProgramTests
    {
        // --- ValidateArgs --------------------------------------------------

        [TestMethod]
        public void TestValidateNoJoinAndJoinOnlyIsMutex()
        {
            var config = new OspreyConfig();
            string err = Program.ValidateArgs(config, noJoinFlag: true, joinOnlyFlag: true, joinOnlyModifier: false);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "mutually exclusive");
        }

        [TestMethod]
        public void TestValidateJoinOnlyRequiresInputScores()
        {
            var config = new OspreyConfig
            {
                LibrarySource = LibrarySource.FromPath("ref.blib"),
                OutputBlib = "out.blib"
            };
            string err = Program.ValidateArgs(config, noJoinFlag: false, joinOnlyFlag: true, joinOnlyModifier: false);
            Assert.IsNotNull(err);
            // Error refers to the canonical flag the user typed.
            StringAssert.Contains(err, "--join-at-pass=1");
            StringAssert.Contains(err, "--input-scores");
        }

        [TestMethod]
        public void TestValidateJoinOnlyRejectsInputMzml()
        {
            var config = new OspreyConfig
            {
                InputFiles = new List<string> { "a.mzML" },
                InputScores = new List<string> { "a.scores.parquet" },
                LibrarySource = LibrarySource.FromPath("ref.blib"),
                OutputBlib = "out.blib"
            };
            string err = Program.ValidateArgs(config, noJoinFlag: false, joinOnlyFlag: true, joinOnlyModifier: false);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--join-at-pass=1");
            StringAssert.Contains(err, "cannot be combined with --input");
        }

        [TestMethod]
        public void TestValidateJoinOnlyRequiresLibraryAndOutput()
        {
            var config = new OspreyConfig
            {
                InputScores = new List<string> { "a.scores.parquet" }
            };
            string err = Program.ValidateArgs(config, noJoinFlag: false, joinOnlyFlag: true, joinOnlyModifier: false);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--join-at-pass=1");
            StringAssert.Contains(err, "--library and --output");
        }

        [TestMethod]
        public void TestValidateNoJoinRequiresInput()
        {
            var config = new OspreyConfig
            {
                NoJoin = true,
                LibrarySource = LibrarySource.FromPath("ref.blib")
            };
            string err = Program.ValidateArgs(config, noJoinFlag: true, joinOnlyFlag: false, joinOnlyModifier: false);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--input <mzML");
        }

        [TestMethod]
        public void TestValidateNoJoinRejectsInputScores()
        {
            var config = new OspreyConfig
            {
                NoJoin = true,
                InputFiles = new List<string> { "a.mzML" },
                InputScores = new List<string> { "a.scores.parquet" },
                LibrarySource = LibrarySource.FromPath("ref.blib")
            };
            string err = Program.ValidateArgs(config, noJoinFlag: true, joinOnlyFlag: false, joinOnlyModifier: false);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--input-scores");
        }

        [TestMethod]
        public void TestValidateNoJoinWorkerHappyPath()
        {
            // --join-at-pass=1 --no-join (per-file rescore worker):
            // requires --input-scores, --library, and --output. No
            // --input mzML.
            var config = new OspreyConfig
            {
                NoJoin = true,
                InputScores = new List<string> { "a.scores.parquet" },
                LibrarySource = LibrarySource.FromPath("ref.blib"),
                OutputBlib = "out.blib",
            };
            Assert.IsNull(
                Program.ValidateArgs(config, noJoinFlag: true, joinOnlyFlag: false, joinOnlyModifier: false));
        }

        [TestMethod]
        public void TestValidateNoJoinWorkerRequiresLibraryAndOutput()
        {
            // Worker mode without --library or --output — like the in-process
            // --join-at-pass=1 path, both are required so the per-file
            // parquet write-back has somewhere to go.
            var config = new OspreyConfig
            {
                NoJoin = true,
                InputScores = new List<string> { "a.scores.parquet" },
                // missing LibrarySource and OutputBlib
            };
            string err = Program.ValidateArgs(config, noJoinFlag: true, joinOnlyFlag: false, joinOnlyModifier: false);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--library and --output");
        }

        [TestMethod]
        public void TestValidateNoJoinHappyPath()
        {
            var config = new OspreyConfig
            {
                NoJoin = true,
                InputFiles = new List<string> { "a.mzML" },
                LibrarySource = LibrarySource.FromPath("ref.blib")
            };
            Assert.IsNull(Program.ValidateArgs(config, noJoinFlag: true, joinOnlyFlag: false, joinOnlyModifier: false));
        }

        [TestMethod]
        public void TestValidateJoinOnlyHappyPath()
        {
            var config = new OspreyConfig
            {
                InputScores = new List<string> { "a.scores.parquet", "b.scores.parquet" },
                LibrarySource = LibrarySource.FromPath("ref.blib"),
                OutputBlib = "out.blib"
            };
            Assert.IsNull(Program.ValidateArgs(config, noJoinFlag: false, joinOnlyFlag: true, joinOnlyModifier: false));
        }

        [TestMethod]
        public void TestValidateJoinOnlyModifierRejectsSingleFile()
        {
            // --join-at-pass=1 --join-only writes the Stage 5 → Stage 6
            // boundary file pair; that's only meaningful with siblings,
            // so a single-file invocation should error fast rather than
            // running Stages 1-5 and silently producing nothing useful.
            var config = new OspreyConfig
            {
                InputScores = new List<string> { "only.scores.parquet" },
                LibrarySource = LibrarySource.FromPath("ref.blib"),
                OutputBlib = "out.blib"
            };
            string err = Program.ValidateArgs(config,
                noJoinFlag: false, joinOnlyFlag: true, joinOnlyModifier: true);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--join-at-pass=1 --join-only");
            StringAssert.Contains(err, "2+ parquet files");
        }

        [TestMethod]
        public void TestValidateJoinOnlyModifierRequiresReconciliationEnabled()
        {
            var config = new OspreyConfig
            {
                InputScores = new List<string> { "a.scores.parquet", "b.scores.parquet" },
                LibrarySource = LibrarySource.FromPath("ref.blib"),
                OutputBlib = "out.blib"
            };
            config.Reconciliation.Enabled = false;
            string err = Program.ValidateArgs(config,
                noJoinFlag: false, joinOnlyFlag: true, joinOnlyModifier: true);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "Reconciliation.Enabled");
        }

        [TestMethod]
        public void TestValidateJoinOnlyPlainAcceptsSingleFile()
        {
            // Plain --join-at-pass=1 (no modifier) runs Stages 5-8 from
            // the parquet entry point; a 1-file run is a degenerate but
            // legal case and shouldn't be rejected here.
            var config = new OspreyConfig
            {
                InputScores = new List<string> { "only.scores.parquet" },
                LibrarySource = LibrarySource.FromPath("ref.blib"),
                OutputBlib = "out.blib"
            };
            Assert.IsNull(Program.ValidateArgs(config,
                noJoinFlag: false, joinOnlyFlag: true, joinOnlyModifier: false));
        }

        [TestMethod]
        public void TestValidateDefaultModeIsUnaffected()
        {
            var config = new OspreyConfig
            {
                InputFiles = new List<string> { "a.mzML" },
                LibrarySource = LibrarySource.FromPath("ref.blib"),
                OutputBlib = "out.blib"
            };
            Assert.IsNull(Program.ValidateArgs(config, noJoinFlag: false, joinOnlyFlag: false, joinOnlyModifier: false));
        }

        [TestMethod]
        public void TestValidateDefaultRejectsMissingInput()
        {
            var config = new OspreyConfig
            {
                LibrarySource = LibrarySource.FromPath("ref.blib"),
                OutputBlib = "out.blib"
            };
            string err = Program.ValidateArgs(config, noJoinFlag: false, joinOnlyFlag: false, joinOnlyModifier: false);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "No input files");
        }

        // --- NormalizeHpcArgs (--join-at-pass) ----------------------------

        [TestMethod]
        public void TestNormalizeJoinAtPass1MapsToJoinOnly()
        {
            bool noJoin = false, joinOnly = false;
            string err = Program.NormalizeHpcArgs(joinAtPass: 1, noJoinFlag: ref noJoin, joinOnlyFlag: ref joinOnly, joinOnlyModifier: out _);
            Assert.IsNull(err);
            Assert.IsTrue(joinOnly, "joinOnly should be set to true so existing Stage 5+ path runs");
            Assert.IsFalse(noJoin);
        }

        [TestMethod]
        public void TestNormalizeJoinAtPass2ErrorsUntilImplemented()
        {
            bool noJoin = false, joinOnly = false;
            string err = Program.NormalizeHpcArgs(joinAtPass: 2, noJoinFlag: ref noJoin, joinOnlyFlag: ref joinOnly, joinOnlyModifier: out _);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "not yet implemented");
        }

        [TestMethod]
        public void TestNormalizeJoinAtPassInvalidValueErrors()
        {
            bool noJoin = false, joinOnly = false;
            string err = Program.NormalizeHpcArgs(joinAtPass: 3, noJoinFlag: ref noJoin, joinOnlyFlag: ref joinOnly, joinOnlyModifier: out _);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "must be 1 or 2");
        }

        [TestMethod]
        public void TestNormalizeJoinAtPass1WithJoinOnlyModifierSetsStopFlag()
        {
            // `--join-at-pass=1 --join-only` means "run only Stage 5 + planning,
            // write boundary files, exit." Both joinOnly (existing
            // Stage 5+ entry path) and joinOnlyModifier (post-planning
            // early exit signal) should be set.
            bool noJoin = false, joinOnly = true;
            string err = Program.NormalizeHpcArgs(
                joinAtPass: 1, noJoinFlag: ref noJoin, joinOnlyFlag: ref joinOnly,
                joinOnlyModifier: out bool joinOnlyModifier);
            Assert.IsNull(err);
            Assert.IsTrue(joinOnly);
            Assert.IsTrue(joinOnlyModifier);
        }

        [TestMethod]
        public void TestNormalizeJoinAtPass1WithNoJoinModifierKeepsBothFlags()
        {
            // --join-at-pass=1 --no-join is the per-file rescore worker
            // mode. Normalize keeps noJoinFlag true and joinOnlyFlag false
            // (they're not flipped); Main routes the combination to
            // RescoreWorker.Run.
            bool noJoin = true, joinOnly = false;
            string err = Program.NormalizeHpcArgs(joinAtPass: 1, noJoinFlag: ref noJoin, joinOnlyFlag: ref joinOnly, joinOnlyModifier: out bool joinOnlyModifier);
            Assert.IsNull(err, "got: {0}", err);
            Assert.IsTrue(noJoin, "noJoinFlag should remain true");
            Assert.IsFalse(joinOnly, "joinOnlyFlag should remain false");
            Assert.IsFalse(joinOnlyModifier, "joinOnlyModifier should be false");
        }

        [TestMethod]
        public void TestNormalizeJoinOnlyAloneErrorsNoEntryPoint()
        {
            bool noJoin = false, joinOnly = true;
            string err = Program.NormalizeHpcArgs(joinAtPass: null, noJoinFlag: ref noJoin, joinOnlyFlag: ref joinOnly, joinOnlyModifier: out _);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "modifier");
        }

        [TestMethod]
        public void TestNormalizeNoJoinAndJoinOnlyModifiersAreMutex()
        {
            bool noJoin = true, joinOnly = true;
            string err = Program.NormalizeHpcArgs(joinAtPass: 1, noJoinFlag: ref noJoin, joinOnlyFlag: ref joinOnly, joinOnlyModifier: out _);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "mutually exclusive");
        }

        [TestMethod]
        public void TestNormalizeNoJoinAloneUnchanged()
        {
            // Stage 1 entry path with `-i ...` + `--no-join` keeps its
            // existing meaning: do per-file work only = Stages 1-4.
            bool noJoin = true, joinOnly = false;
            string err = Program.NormalizeHpcArgs(joinAtPass: null, noJoinFlag: ref noJoin, joinOnlyFlag: ref joinOnly, joinOnlyModifier: out _);
            Assert.IsNull(err);
            Assert.IsTrue(noJoin);
            Assert.IsFalse(joinOnly);
        }

        [TestMethod]
        public void TestNormalizeDefaultModeIsNoop()
        {
            bool noJoin = false, joinOnly = false;
            string err = Program.NormalizeHpcArgs(joinAtPass: null, noJoinFlag: ref noJoin, joinOnlyFlag: ref joinOnly, joinOnlyModifier: out _);
            Assert.IsNull(err);
            Assert.IsFalse(noJoin);
            Assert.IsFalse(joinOnly);
        }

        // --- ResolveInputScores -------------------------------------------

        [TestMethod]
        public void TestResolveExplicitFilesPassThrough()
        {
            string dir = NewTempDir();
            try
            {
                string a = Path.Combine(dir, "a.scores.parquet");
                string b = Path.Combine(dir, "b.scores.parquet");
                File.WriteAllText(a, string.Empty);
                File.WriteAllText(b, string.Empty);
                var resolved = Program.ResolveInputScores(new List<string> { a, b });
                CollectionAssert.AreEqual(new List<string> { a, b }, resolved);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void TestResolveExplicitMissingFileErrors()
        {
            string dir = NewTempDir();
            try
            {
                string missing = Path.Combine(dir, "does-not-exist.scores.parquet");
                try
                {
                    Program.ResolveInputScores(new List<string> { missing });
                    Assert.Fail("Expected ArgumentException for missing file");
                }
                catch (ArgumentException ex)
                {
                    StringAssert.Contains(ex.Message, "not found");
                }
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void TestResolveDirectoryScansAndSorts()
        {
            string dir = NewTempDir();
            try
            {
                File.WriteAllText(Path.Combine(dir, "z.scores.parquet"), string.Empty);
                File.WriteAllText(Path.Combine(dir, "a.scores.parquet"), string.Empty);
                File.WriteAllText(Path.Combine(dir, "m.scores.parquet"), string.Empty);
                File.WriteAllText(Path.Combine(dir, "readme.txt"), string.Empty);
                var resolved = Program.ResolveInputScores(new List<string> { dir });
                Assert.AreEqual(3, resolved.Count);
                Assert.AreEqual("a.scores.parquet", Path.GetFileName(resolved[0]));
                Assert.AreEqual("m.scores.parquet", Path.GetFileName(resolved[1]));
                Assert.AreEqual("z.scores.parquet", Path.GetFileName(resolved[2]));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void TestResolveEmptyDirectoryErrors()
        {
            string dir = NewTempDir();
            try
            {
                File.WriteAllText(Path.Combine(dir, "not-a-match.txt"), string.Empty);
                try
                {
                    Program.ResolveInputScores(new List<string> { dir });
                    Assert.Fail("Expected ArgumentException for directory with no parquets");
                }
                catch (ArgumentException ex)
                {
                    StringAssert.Contains(ex.Message, "No *.scores.parquet");
                }
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [TestMethod]
        public void TestResolveEmptyListErrors()
        {
            try
            {
                Program.ResolveInputScores(new List<string>());
                Assert.Fail("Expected ArgumentException for empty list");
            }
            catch (ArgumentException ex)
            {
                StringAssert.Contains(ex.Message, "at least one path");
            }
        }

        // --- OspreyConfig defaults ----------------------------------------

        [TestMethod]
        public void TestConfigDefaultsDisableHpcMode()
        {
            var cfg = new OspreyConfig();
            Assert.IsFalse(cfg.NoJoin, "NoJoin should default to false");
            Assert.IsNull(cfg.InputScores, "InputScores should default to null");
        }

        // --- ParquetScoreCache.CheckParquetMetadata -----------------------

        private const string VALID_SEARCH = "search-hash-aaa";
        private const string VALID_LIB = "lib-hash-bbb";

        // Track Program.VERSION so happy-path and drift tests stay
        // meaningful after upstream version bumps.
        private const string CURRENT_VERSION = Program.VERSION;
        private static readonly string PATCH_DRIFT_VERSION = DriftVersion(0, 0, 5);
        private static readonly string MINOR_DRIFT_VERSION = DriftVersion(0, 1, 0);
        private static readonly string MAJOR_DRIFT_VERSION = DriftVersion(1, 0, 0);

        private static string DriftVersion(int majorDelta, int minorDelta, int patchDelta)
        {
            var parts = CURRENT_VERSION.Split('.');
            int major = int.Parse(parts[0], CultureInfo.InvariantCulture);
            int minor = int.Parse(parts[1], CultureInfo.InvariantCulture);
            int patch = int.Parse(parts[2], CultureInfo.InvariantCulture);
            // Reset lower-level components when a higher-level one drifts.
            int driftMinor = majorDelta != 0 ? 0 : minor + minorDelta;
            int driftPatch = (majorDelta != 0 || minorDelta != 0) ? 0 : patch + patchDelta;
            return string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}",
                major + majorDelta, driftMinor, driftPatch);
        }

        private static string CheckMd(
            string cachedV, string cachedS, string cachedL, out string warning)
        {
            return ParquetScoreCache.CheckParquetMetadata(
                "test.scores.parquet",
                cachedV, cachedS, cachedL,
                VALID_SEARCH, VALID_LIB, CURRENT_VERSION,
                out warning);
        }

        [TestMethod]
        public void TestParseVersionRoundTrip()
        {
            int M, m, p;
            Assert.IsTrue(ParquetScoreCache.TryParseVersion("26.3.0", out M, out m, out p));
            Assert.AreEqual(26, M); Assert.AreEqual(3, m); Assert.AreEqual(0, p);
            Assert.IsTrue(ParquetScoreCache.TryParseVersion("0.0.1", out M, out m, out p));
            Assert.AreEqual(0, M); Assert.AreEqual(0, m); Assert.AreEqual(1, p);
        }

        [TestMethod]
        public void TestParseVersionRejectsBadInput()
        {
            Assert.IsFalse(ParquetScoreCache.TryParseVersion("", out _, out _, out _));
            Assert.IsFalse(ParquetScoreCache.TryParseVersion("26.3", out _, out _, out _));
            Assert.IsFalse(ParquetScoreCache.TryParseVersion("v26.3.0", out _, out _, out _));
            Assert.IsFalse(ParquetScoreCache.TryParseVersion("26.3.x", out _, out _, out _));
        }

        [TestMethod]
        public void TestMetadataHappyPathNoWarning()
        {
            string warn;
            string err = CheckMd(CURRENT_VERSION, VALID_SEARCH, VALID_LIB, out warn);
            Assert.IsNull(err);
            Assert.IsNull(warn);
        }

        [TestMethod]
        public void TestMetadataPatchDriftWarnsButSucceeds()
        {
            string warn;
            string err = CheckMd(PATCH_DRIFT_VERSION, VALID_SEARCH, VALID_LIB, out warn);
            Assert.IsNull(err);
            Assert.IsNotNull(warn);
            StringAssert.Contains(warn, "patch-version drift");
        }

        [TestMethod]
        public void TestMetadataMinorVersionDriftAborts()
        {
            string err = CheckMd(MINOR_DRIFT_VERSION, VALID_SEARCH, VALID_LIB, out _);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "major/minor");
        }

        [TestMethod]
        public void TestMetadataMajorVersionDriftAborts()
        {
            string err = CheckMd(MAJOR_DRIFT_VERSION, VALID_SEARCH, VALID_LIB, out _);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "major/minor");
        }

        [TestMethod]
        public void TestMetadataMissingVersionAborts()
        {
            string err = CheckMd(null, VALID_SEARCH, VALID_LIB, out _);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "osprey.version");
        }

        [TestMethod]
        public void TestMetadataMissingSearchHashAborts()
        {
            string err = CheckMd(CURRENT_VERSION, null, VALID_LIB, out _);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "osprey.search_hash");
        }

        [TestMethod]
        public void TestMetadataMissingLibraryHashAborts()
        {
            string err = CheckMd(CURRENT_VERSION, VALID_SEARCH, null, out _);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "osprey.library_hash");
        }

        [TestMethod]
        public void TestMetadataSearchHashMismatchNamesFieldAndFile()
        {
            string err = CheckMd(CURRENT_VERSION, "wrong-hash", VALID_LIB, out _);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "search_hash mismatch");
            StringAssert.Contains(err, "test.scores.parquet");
            StringAssert.Contains(err, "wrong-hash");
        }

        [TestMethod]
        public void TestMetadataLibraryHashMismatchNamesFieldAndFile()
        {
            string err = CheckMd(CURRENT_VERSION, VALID_SEARCH, "wrong-lib", out _);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "library_hash mismatch");
            StringAssert.Contains(err, "test.scores.parquet");
            StringAssert.Contains(err, "wrong-lib");
        }

        [TestMethod]
        public void TestMetadataUnparseableVersionWarnsButProceeds()
        {
            string warn;
            string err = CheckMd("garbage", VALID_SEARCH, VALID_LIB, out warn);
            Assert.IsNull(err);
            Assert.IsNotNull(warn);
            StringAssert.Contains(warn, "could not parse");
        }

        // --- helpers -------------------------------------------------------

        private static string NewTempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(),
                "osprey_test_program_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
