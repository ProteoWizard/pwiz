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
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.OspreySharp.Core;

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
            string err = Program.ValidateArgs(config, noJoinFlag: true, joinOnlyFlag: true);
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
            string err = Program.ValidateArgs(config, noJoinFlag: false, joinOnlyFlag: true);
            Assert.IsNotNull(err);
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
            string err = Program.ValidateArgs(config, noJoinFlag: false, joinOnlyFlag: true);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "cannot be combined with --input");
        }

        [TestMethod]
        public void TestValidateJoinOnlyRequiresLibraryAndOutput()
        {
            var config = new OspreyConfig
            {
                InputScores = new List<string> { "a.scores.parquet" }
            };
            string err = Program.ValidateArgs(config, noJoinFlag: false, joinOnlyFlag: true);
            Assert.IsNotNull(err);
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
            string err = Program.ValidateArgs(config, noJoinFlag: true, joinOnlyFlag: false);
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
            string err = Program.ValidateArgs(config, noJoinFlag: true, joinOnlyFlag: false);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--input-scores");
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
            Assert.IsNull(Program.ValidateArgs(config, noJoinFlag: true, joinOnlyFlag: false));
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
            Assert.IsNull(Program.ValidateArgs(config, noJoinFlag: false, joinOnlyFlag: true));
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
            Assert.IsNull(Program.ValidateArgs(config, noJoinFlag: false, joinOnlyFlag: false));
        }

        [TestMethod]
        public void TestValidateDefaultRejectsMissingInput()
        {
            var config = new OspreyConfig
            {
                LibrarySource = LibrarySource.FromPath("ref.blib"),
                OutputBlib = "out.blib"
            };
            string err = Program.ValidateArgs(config, noJoinFlag: false, joinOnlyFlag: false);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "No input files");
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
