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
        // --- ValidateArgs: --task is authoritative over input type --------

        private static OspreyConfig TaskConfig(HpcTask task)
        {
            // Mirror Main's wiring: ResolveTask -> SelectedTask + derived flags.
            return new OspreyConfig
            {
                SelectedTask = task,
                NoJoin = task == HpcTask.PerFileScoring || task == HpcTask.PerFileRescore,
                StopAfterStage5 = task == HpcTask.FirstJoin,
                ExpectReconciledInput = task == HpcTask.MergeNode,
            };
        }

        // -- PerFileScoring (mzML in) --

        [TestMethod]
        public void TestValidatePerFileScoringHappyPath()
        {
            var config = TaskConfig(HpcTask.PerFileScoring);
            config.InputFiles = new List<string> { "a.mzML" };
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            Assert.IsNull(Program.ValidateArgs(config));
        }

        [TestMethod]
        public void TestValidatePerFileScoringRequiresInput()
        {
            var config = TaskConfig(HpcTask.PerFileScoring);
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--task PerFileScoring");
            StringAssert.Contains(err, "--input <mzML");
        }

        [TestMethod]
        public void TestValidatePerFileScoringRequiresLibrary()
        {
            var config = TaskConfig(HpcTask.PerFileScoring);
            config.InputFiles = new List<string> { "a.mzML" };
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--task PerFileScoring");
            StringAssert.Contains(err, "--library");
        }

        [TestMethod]
        public void TestValidatePerFileScoringRejectsInputScores()
        {
            // --task is authoritative: PerFileScoring + --input-scores must
            // error, not silently dispatch PerFileRescore.
            var config = TaskConfig(HpcTask.PerFileScoring);
            config.InputScores = new List<string> { "a.scores.parquet" };
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            config.OutputBlib = "out.blib";
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--task PerFileScoring");
            StringAssert.Contains(err, "not --input-scores");
        }

        // -- PerFileRescore (--input-scores in) --

        [TestMethod]
        public void TestValidatePerFileRescoreHappyPath()
        {
            var config = TaskConfig(HpcTask.PerFileRescore);
            config.InputScores = new List<string> { "a.scores.parquet" };
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            config.OutputBlib = "out.blib";
            Assert.IsNull(Program.ValidateArgs(config));
        }

        [TestMethod]
        public void TestValidatePerFileRescoreRequiresInputScores()
        {
            var config = TaskConfig(HpcTask.PerFileRescore);
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            config.OutputBlib = "out.blib";
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--task PerFileRescore");
            StringAssert.Contains(err, "--input-scores");
        }

        [TestMethod]
        public void TestValidatePerFileRescoreRequiresLibraryAndOutput()
        {
            var config = TaskConfig(HpcTask.PerFileRescore);
            config.InputScores = new List<string> { "a.scores.parquet" };
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--task PerFileRescore");
            StringAssert.Contains(err, "--library and --output");
        }

        [TestMethod]
        public void TestValidatePerFileRescoreRejectsInputMzml()
        {
            // Authoritative: PerFileRescore + -i mzML must error, not silently
            // dispatch PerFileScoring. Error must name the task the user typed.
            var config = TaskConfig(HpcTask.PerFileRescore);
            config.InputFiles = new List<string> { "a.mzML" };
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            config.OutputBlib = "out.blib";
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--task PerFileRescore");
            StringAssert.Contains(err, "not -i <mzML>");
        }

        // -- FirstJoin (--input-scores in, 2+ files, reconciliation on) --

        [TestMethod]
        public void TestValidateFirstJoinHappyPath()
        {
            var config = TaskConfig(HpcTask.FirstJoin);
            config.InputScores = new List<string> { "a.scores.parquet", "b.scores.parquet" };
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            config.OutputBlib = "out.blib";
            Assert.IsNull(Program.ValidateArgs(config));
        }

        [TestMethod]
        public void TestValidateFirstJoinRequiresInputScores()
        {
            var config = TaskConfig(HpcTask.FirstJoin);
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            config.OutputBlib = "out.blib";
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--task FirstJoin");
            StringAssert.Contains(err, "--input-scores");
        }

        [TestMethod]
        public void TestValidateFirstJoinRejectsInputMzml()
        {
            var config = TaskConfig(HpcTask.FirstJoin);
            config.InputFiles = new List<string> { "a.mzML" };
            config.InputScores = new List<string> { "a.scores.parquet", "b.scores.parquet" };
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            config.OutputBlib = "out.blib";
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--task FirstJoin");
            StringAssert.Contains(err, "cannot be combined with --input");
        }

        [TestMethod]
        public void TestValidateFirstJoinRequiresLibraryAndOutput()
        {
            var config = TaskConfig(HpcTask.FirstJoin);
            config.InputScores = new List<string> { "a.scores.parquet", "b.scores.parquet" };
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--task FirstJoin");
            StringAssert.Contains(err, "--library and --output");
        }

        [TestMethod]
        public void TestValidateFirstJoinRejectsSingleFile()
        {
            // FirstJoin writes the Stage 5 -> Stage 6 boundary pair, only
            // meaningful with siblings; a single-file run errors fast.
            var config = TaskConfig(HpcTask.FirstJoin);
            config.InputScores = new List<string> { "only.scores.parquet" };
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            config.OutputBlib = "out.blib";
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--task FirstJoin");
            StringAssert.Contains(err, "2+ parquet files");
        }

        [TestMethod]
        public void TestValidateFirstJoinRequiresReconciliationEnabled()
        {
            var config = TaskConfig(HpcTask.FirstJoin);
            config.InputScores = new List<string> { "a.scores.parquet", "b.scores.parquet" };
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            config.OutputBlib = "out.blib";
            config.Reconciliation.Enabled = false;
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "Reconciliation.Enabled");
        }

        // -- MergeNode (reconciled --input-scores in) --

        [TestMethod]
        public void TestValidateMergeNodeHappyPath()
        {
            var config = TaskConfig(HpcTask.MergeNode);
            config.InputScores = new List<string> { "a.scores-reconciled.parquet" };
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            config.OutputBlib = "out.blib";
            Assert.IsNull(Program.ValidateArgs(config));
        }

        [TestMethod]
        public void TestValidateMergeNodeRequiresInputScores()
        {
            // Uncontested gap from ultrareview: --task MergeNode without
            // --input-scores (even with -i mzML) used to pass validation and
            // silently run the full pipeline. It must now fail fast.
            var config = TaskConfig(HpcTask.MergeNode);
            config.InputFiles = new List<string> { "a.mzML" };
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            config.OutputBlib = "out.blib";
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--task MergeNode");
            // -i present -> the cross is reported first; either way it must not pass.
        }

        [TestMethod]
        public void TestValidateMergeNodeRequiresInputScoresNoMzml()
        {
            var config = TaskConfig(HpcTask.MergeNode);
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            config.OutputBlib = "out.blib";
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--task MergeNode");
            StringAssert.Contains(err, "--input-scores");
        }

        [TestMethod]
        public void TestValidateMergeNodeRequiresLibraryAndOutput()
        {
            var config = TaskConfig(HpcTask.MergeNode);
            config.InputScores = new List<string> { "a.scores-reconciled.parquet" };
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--task MergeNode");
            StringAssert.Contains(err, "--library and --output");
        }

        [TestMethod]
        public void TestValidateMergeNodeRejectsInputMzml()
        {
            var config = TaskConfig(HpcTask.MergeNode);
            config.InputFiles = new List<string> { "a.mzML" };
            config.InputScores = new List<string> { "a.scores-reconciled.parquet" };
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            config.OutputBlib = "out.blib";
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--task MergeNode");
            StringAssert.Contains(err, "cannot be combined with --input");
        }

        // -- Default (no --task): full pipeline from -i mzML or --input-scores --

        [TestMethod]
        public void TestValidateDefaultFullHappyPath()
        {
            var config = new OspreyConfig
            {
                InputFiles = new List<string> { "a.mzML" },
                LibrarySource = LibrarySource.FromPath("ref.blib"),
                OutputBlib = "out.blib"
            };
            Assert.IsNull(Program.ValidateArgs(config));
        }

        [TestMethod]
        public void TestValidateDefaultRejectsMissingInput()
        {
            var config = new OspreyConfig
            {
                LibrarySource = LibrarySource.FromPath("ref.blib"),
                OutputBlib = "out.blib"
            };
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "No input files");
        }

        [TestMethod]
        public void TestValidateFullFromScoresHappyPath()
        {
            // No --task + --input-scores: the full pipeline started from scores
            // (PerFileScoring lazy-rehydrates). A single file is a legal,
            // degenerate case.
            var config = new OspreyConfig
            {
                InputScores = new List<string> { "only.scores.parquet" },
                LibrarySource = LibrarySource.FromPath("ref.blib"),
                OutputBlib = "out.blib"
            };
            Assert.IsNull(Program.ValidateArgs(config));
        }

        [TestMethod]
        public void TestValidateFullFromScoresRequiresLibraryAndOutput()
        {
            // No --task: the error references --input-scores, not a task the
            // user never selected.
            var config = new OspreyConfig
            {
                InputScores = new List<string> { "a.scores.parquet", "b.scores.parquet" },
                LibrarySource = LibrarySource.FromPath("ref.blib"),
                // missing OutputBlib
            };
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--input-scores");
            StringAssert.Contains(err, "--library and --output");
            Assert.IsFalse(err.Contains("--task"), "full-from-scores error must not name a --task: " + err);
        }

        [TestMethod]
        public void TestValidateFullFromScoresRejectsInputMzml()
        {
            var config = new OspreyConfig
            {
                InputFiles = new List<string> { "a.mzML" },
                InputScores = new List<string> { "a.scores.parquet" },
                LibrarySource = LibrarySource.FromPath("ref.blib"),
                OutputBlib = "out.blib"
            };
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "cannot be combined with --input");
        }

        // --- ResolveTask (--task) -----------------------------------------

        [TestMethod]
        public void TestResolveTaskPerFileScoring()
        {
            Assert.IsNull(Program.ResolveTask("PerFileScoring", out HpcTask task));
            Assert.AreEqual(HpcTask.PerFileScoring, task);
        }

        [TestMethod]
        public void TestResolveTaskFirstJoin()
        {
            Assert.IsNull(Program.ResolveTask("FirstJoin", out HpcTask task));
            Assert.AreEqual(HpcTask.FirstJoin, task);
        }

        [TestMethod]
        public void TestResolveTaskPerFileRescore()
        {
            Assert.IsNull(Program.ResolveTask("PerFileRescore", out HpcTask task));
            Assert.AreEqual(HpcTask.PerFileRescore, task);
        }

        [TestMethod]
        public void TestResolveTaskMergeNode()
        {
            Assert.IsNull(Program.ResolveTask("MergeNode", out HpcTask task));
            Assert.AreEqual(HpcTask.MergeNode, task);
        }

        [TestMethod]
        public void TestResolveTaskIsCaseInsensitive()
        {
            Assert.IsNull(Program.ResolveTask("perfilerescore", out HpcTask task));
            Assert.AreEqual(HpcTask.PerFileRescore, task);
        }

        [TestMethod]
        public void TestResolveTaskUnknownErrors()
        {
            string err = Program.ResolveTask("Bogus", out _);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "unknown task");
            StringAssert.Contains(err, "Bogus");
        }

        [TestMethod]
        public void TestResolveTaskMapsToExpectedMembershipFlags()
        {
            // Each task must derive (via Main's wiring, mirrored by TaskConfig)
            // the (NoJoin, StopAfterStage5, ExpectReconciledInput) tuple the four
            // tasks' IsIncluded methods read. Mirrors PipelineMembershipTest.
            //   task             | NoJoin | StopAfterStage5 | ExpectReconciled
            //   PerFileScoring   | true   | false           | false
            //   FirstJoin        | false  | true            | false
            //   PerFileRescore   | true   | false           | false
            //   MergeNode        | false  | false           | true
            var cases = new (HpcTask Task, bool NoJoin, bool StopAfterStage5, bool ExpectReconciled)[]
            {
                (HpcTask.PerFileScoring, true,  false, false),
                (HpcTask.FirstJoin,      false, true,  false),
                (HpcTask.PerFileRescore, true,  false, false),
                (HpcTask.MergeNode,      false, false, true),
            };
            foreach (var c in cases)
            {
                var config = TaskConfig(c.Task);
                Assert.AreEqual(c.NoJoin, config.NoJoin, string.Format("{0}: NoJoin", c.Task));
                Assert.AreEqual(c.StopAfterStage5, config.StopAfterStage5,
                    string.Format("{0}: StopAfterStage5", c.Task));
                Assert.AreEqual(c.ExpectReconciled, config.ExpectReconciledInput,
                    string.Format("{0}: ExpectReconciledInput", c.Task));
            }
        }

        // --- ParseArgs: unknown / retired flags fail fast -----------------

        [TestMethod]
        public void TestParseArgsRejectsRetiredNoJoin()
        {
            // The retired HPC mode flags are now unknown options. ParseArgs
            // must throw rather than silently dropping them (which would run
            // the full pipeline in the wrong mode). Replaced by --task <Name>.
            var ex = Assert.ThrowsException<ArgumentException>(
                () => Program.ParseArgs(new[] { "--no-join", "-i", "a.mzML" }));
            StringAssert.Contains(ex.Message, "--no-join");
        }

        [TestMethod]
        public void TestParseArgsRejectsRetiredJoinOnly()
        {
            var ex = Assert.ThrowsException<ArgumentException>(
                () => Program.ParseArgs(new[] { "--join-only" }));
            StringAssert.Contains(ex.Message, "--join-only");
        }

        [TestMethod]
        public void TestParseArgsRejectsRetiredJoinAtPass()
        {
            var ex = Assert.ThrowsException<ArgumentException>(
                () => Program.ParseArgs(new[] { "--join-at-pass=2" }));
            StringAssert.Contains(ex.Message, "--join-at-pass=2");

            // Space-separated form too.
            Assert.ThrowsException<ArgumentException>(
                () => Program.ParseArgs(new[] { "--join-at-pass", "1" }));
        }

        [TestMethod]
        public void TestParseArgsRejectsUnknownFlag()
        {
            // Any unrecognized option fails fast (e.g. a typo), not just the
            // retired flags.
            var ex = Assert.ThrowsException<ArgumentException>(
                () => Program.ParseArgs(new[] { "--bogus-flag" }));
            StringAssert.Contains(ex.Message, "--bogus-flag");
        }

        [TestMethod]
        public void TestParseArgsAcceptsTaskAndValidArgs()
        {
            // --task and ordinary flags must NOT throw.
            Program.ParseArgs(new[] { "--task", "FirstJoin", "-l", "ref.blib", "-o", "out.blib" });
            Program.ParseArgs(new[] { "--task=MergeNode", "-l", "ref.blib", "-o", "out.blib" });
        }

        [TestMethod]
        public void TestParseArgsRejectsTaskWithoutValue()
        {
            // A bare --task (or --task followed by another flag) must throw,
            // like the other required-value flags, so it can't be silently
            // ignored when ParseArgs runs outside Main's pre-scan.
            var ex = Assert.ThrowsException<ArgumentException>(
                () => Program.ParseArgs(new[] { "--task" }));
            StringAssert.Contains(ex.Message, "--task");
            Assert.ThrowsException<ArgumentException>(
                () => Program.ParseArgs(new[] { "--task", "-l", "ref.blib" }));
        }

        [TestMethod]
        public void TestParseArgsRejectsValueFlagsWithoutValue()
        {
            // Single-value option flags must reject both a missing value (flag
            // is the last token) and a following option token (the next arg
            // starts with '-'), so e.g. `-o -l x` can't silently swallow `-l`
            // as the output path. Representative coverage across the path,
            // numeric, and enum flags.
            var missingOrFlagFollowed = new[]
            {
                new[] { "-l" },
                new[] { "-o", "-l", "x.blib" },
                new[] { "--output" },
                new[] { "--resolution", "--protein-fdr", "0.01" },
                new[] { "--protein-fdr" },
                new[] { "--threads", "-i", "f.mzML" },
                new[] { "--decoy-pairing-manifest" },
                new[] { "--fdr-method", "-o", "out.blib" },
                new[] { "--fdr-level" },
                new[] { "--shared-peptides", "--threads", "4" },
            };
            foreach (var args in missingOrFlagFollowed)
            {
                Assert.ThrowsException<ArgumentException>(
                    () => Program.ParseArgs(args));
            }
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
        public void TestResolveDirectoryPrefersReconciledPerStem()
        {
            // The directory holds both Stage 4 <stem>.scores.parquet and Stage 6
            // <stem>.scores-reconciled.parquet files. For any stem that has both,
            // only the reconciled file is returned; never both.
            string dir = NewTempDir();
            try
            {
                File.WriteAllText(Path.Combine(dir, "a.scores.parquet"), string.Empty);
                File.WriteAllText(Path.Combine(dir, "a.scores-reconciled.parquet"), string.Empty);
                File.WriteAllText(Path.Combine(dir, "b.scores.parquet"), string.Empty); // no reconciled sibling
                File.WriteAllText(Path.Combine(dir, "c.scores-reconciled.parquet"), string.Empty); // no original
                // An input stem ending in ".reconciled" stays an original (Copilot
                // ambiguity regression guard) -- its Stage 4 file must be returned
                // as an original, not misread as a reconciled output.
                File.WriteAllText(Path.Combine(dir, "d.reconciled.scores.parquet"), string.Empty);
                var resolved = Program.ResolveInputScores(new List<string> { dir });
                CollectionAssert.AreEqual(
                    new[] { "a.scores-reconciled.parquet", "b.scores.parquet",
                            "c.scores-reconciled.parquet", "d.reconciled.scores.parquet" },
                    resolved.ConvertAll(Path.GetFileName));
                // The superseded original must not appear.
                CollectionAssert.DoesNotContain(resolved.ConvertAll(Path.GetFileName), "a.scores.parquet");
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

        // Track OspreyVersion.Current so happy-path and drift tests stay
        // meaningful as the build version advances. The version follows the
        // Skyline scheme YEAR.ORDINAL.BRANCH.DOY: the first three components are
        // the release identity (a difference aborts cache reuse) and the
        // day-of-year is daily drift (warn but proceed).
        private static readonly string CURRENT_VERSION = OspreyVersion.Current;
        private static readonly string DAILY_DRIFT_VERSION = DriftVersion(0, 0, 0, 5);
        private static readonly string BRANCH_DRIFT_VERSION = DriftVersion(0, 0, 1, 0);
        private static readonly string ORDINAL_DRIFT_VERSION = DriftVersion(0, 1, 0, 0);
        private static readonly string YEAR_DRIFT_VERSION = DriftVersion(1, 0, 0, 0);

        private static string DriftVersion(int yearDelta, int ordinalDelta, int branchDelta, int doyDelta)
        {
            var parts = CURRENT_VERSION.Split('.');
            int year = int.Parse(parts[0], CultureInfo.InvariantCulture);
            int ordinal = int.Parse(parts[1], CultureInfo.InvariantCulture);
            int branch = int.Parse(parts[2], CultureInfo.InvariantCulture);
            int doy = int.Parse(parts[3], CultureInfo.InvariantCulture);
            return string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}.{3}",
                year + yearDelta, ordinal + ordinalDelta, branch + branchDelta, doy + doyDelta);
        }

        private static string CheckMd(string cachedV, string cachedS, string cachedL)
        {
            return ParquetScoreCache.CheckParquetMetadata(
                "test.scores.parquet",
                cachedV, cachedS, cachedL,
                VALID_SEARCH, VALID_LIB, CURRENT_VERSION);
        }

        [TestMethod]
        public void TestParseVersionRoundTrip()
        {
            int y, o, b, d;
            Assert.IsTrue(ParquetScoreCache.TryParseVersion("26.1.1.166", out y, out o, out b, out d));
            Assert.AreEqual(26, y); Assert.AreEqual(1, o); Assert.AreEqual(1, b); Assert.AreEqual(166, d);
            Assert.IsTrue(ParquetScoreCache.TryParseVersion("0.0.0.1", out y, out o, out b, out d));
            Assert.AreEqual(0, y); Assert.AreEqual(0, o); Assert.AreEqual(0, b); Assert.AreEqual(1, d);
        }

        [TestMethod]
        public void TestParseVersionRejectsBadInput()
        {
            Assert.IsFalse(ParquetScoreCache.TryParseVersion("", out _, out _, out _, out _));
            Assert.IsFalse(ParquetScoreCache.TryParseVersion("26.1.1", out _, out _, out _, out _));
            Assert.IsFalse(ParquetScoreCache.TryParseVersion("v26.1.1.0", out _, out _, out _, out _));
            Assert.IsFalse(ParquetScoreCache.TryParseVersion("26.1.1.x", out _, out _, out _, out _));
        }

        [TestMethod]
        public void TestMetadataExactVersionMatchOk()
        {
            string err = CheckMd(CURRENT_VERSION, VALID_SEARCH, VALID_LIB);
            Assert.IsNull(err);
        }

        [TestMethod]
        public void TestMetadataDailyDriftAborts()
        {
            // A different daily build may have changed scoring: hard-fail rather
            // than silently reuse a stale cache behind an easily-missed warning.
            string err = CheckMd(DAILY_DRIFT_VERSION, VALID_SEARCH, VALID_LIB);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "different daily build");
        }

        [TestMethod]
        public void TestMetadataBranchVersionDriftAborts()
        {
            string err = CheckMd(BRANCH_DRIFT_VERSION, VALID_SEARCH, VALID_LIB);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "incompatible release identity");
        }

        [TestMethod]
        public void TestMetadataOrdinalVersionDriftAborts()
        {
            string err = CheckMd(ORDINAL_DRIFT_VERSION, VALID_SEARCH, VALID_LIB);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "incompatible release identity");
        }

        [TestMethod]
        public void TestMetadataYearVersionDriftAborts()
        {
            string err = CheckMd(YEAR_DRIFT_VERSION, VALID_SEARCH, VALID_LIB);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "incompatible release identity");
        }

        [TestMethod]
        public void TestMetadataMissingVersionAborts()
        {
            string err = CheckMd(null, VALID_SEARCH, VALID_LIB);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "osprey.version");
        }

        [TestMethod]
        public void TestMetadataMissingSearchHashAborts()
        {
            string err = CheckMd(CURRENT_VERSION, null, VALID_LIB);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "osprey.search_hash");
        }

        [TestMethod]
        public void TestMetadataMissingLibraryHashAborts()
        {
            string err = CheckMd(CURRENT_VERSION, VALID_SEARCH, null);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "osprey.library_hash");
        }

        [TestMethod]
        public void TestMetadataSearchHashMismatchNamesFieldAndFile()
        {
            string err = CheckMd(CURRENT_VERSION, "wrong-hash", VALID_LIB);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "search_hash mismatch");
            StringAssert.Contains(err, "test.scores.parquet");
            StringAssert.Contains(err, "wrong-hash");
        }

        [TestMethod]
        public void TestMetadataLibraryHashMismatchNamesFieldAndFile()
        {
            string err = CheckMd(CURRENT_VERSION, VALID_SEARCH, "wrong-lib");
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "library_hash mismatch");
            StringAssert.Contains(err, "test.scores.parquet");
            StringAssert.Contains(err, "wrong-lib");
        }

        [TestMethod]
        public void TestMetadataUnparseableVersionAborts()
        {
            // An unrecognized cached version can't be validated for
            // compatibility, so refuse to reuse the cache (hard fail).
            string err = CheckMd("garbage", VALID_SEARCH, VALID_LIB);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "unrecognized osprey version");
        }

        // --- Library-decoy CLI flags ---------------------------------------

        [TestMethod]
        public void TestParseArgsDecoysInLibraryFlag()
        {
            // --decoys-in-library is a flat boolean; flips DecoysInLibrary
            // to true without consuming a value. Mirrors Rust osprey's
            // --decoys-in-library semantics.
            var args = new[]
            {
                @"-i", @"x.mzML",
                @"-l", @"lib.tsv",
                @"-o", @"out.blib",
                @"--decoys-in-library",
            };
            var config = Program.ParseArgs(args);
            Assert.IsTrue(config.DecoysInLibrary);
            Assert.IsTrue(string.IsNullOrEmpty(config.DecoyPairingManifestPath));
        }

        [TestMethod]
        public void TestParseArgsDecoyPairingManifestFlag()
        {
            // --decoy-pairing-manifest <PATH> sets the path on the config
            // WITHOUT flipping DecoysInLibrary. Pin the contract here in
            // isolation (no companion --decoys-in-library) so a regression
            // where the flag accidentally enables library-decoy mode on
            // its own would actually fail the test.
            var args = new[]
            {
                @"-i", @"x.mzML",
                @"-l", @"lib.tsv",
                @"-o", @"out.blib",
                @"--decoy-pairing-manifest", @"T:\test\manifest.tsv",
            };
            var config = Program.ParseArgs(args);
            Assert.IsFalse(config.DecoysInLibrary);
            Assert.AreEqual(@"T:\test\manifest.tsv", config.DecoyPairingManifestPath);
        }

        [TestMethod]
        public void TestParseArgsDecoyPairingManifestRequiresValue()
        {
            // A bare --decoy-pairing-manifest with no value, or a value
            // that's itself an option (--decoys-in-library), must throw
            // rather than silently consume the next token as the path.
            var argsNoValue = new[]
            {
                @"-i", @"x.mzML",
                @"-l", @"lib.tsv",
                @"-o", @"out.blib",
                @"--decoy-pairing-manifest",
            };
            try
            {
                Program.ParseArgs(argsNoValue);
                Assert.Fail(@"Expected ArgumentException for bare --decoy-pairing-manifest.");
            }
            catch (ArgumentException)
            {
                // expected
            }

            var argsFlagAsValue = new[]
            {
                @"-i", @"x.mzML",
                @"-l", @"lib.tsv",
                @"-o", @"out.blib",
                @"--decoy-pairing-manifest", @"--decoys-in-library",
            };
            try
            {
                Program.ParseArgs(argsFlagAsValue);
                Assert.Fail(@"Expected ArgumentException for next-flag-as-value.");
            }
            catch (ArgumentException)
            {
                // expected
            }
        }

        [TestMethod]
        public void TestParseArgsDecoysInLibraryDefaultsFalse()
        {
            // Without the flag, DecoysInLibrary stays at its config default
            // (false) and DecoyPairingManifestPath stays null. Pipeline runs
            // the existing reverse-decoy path. Pins the "library-decoy mode
            // is fully opt-in" contract.
            var args = new[]
            {
                @"-i", @"x.mzML",
                @"-l", @"lib.tsv",
                @"-o", @"out.blib",
            };
            var config = Program.ParseArgs(args);
            Assert.IsFalse(config.DecoysInLibrary);
            Assert.IsTrue(string.IsNullOrEmpty(config.DecoyPairingManifestPath));
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
