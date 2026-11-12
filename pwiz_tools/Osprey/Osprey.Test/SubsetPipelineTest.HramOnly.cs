/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Test
{
    public partial class SubsetPipelineTest
    {
        private static readonly string[] HRAM_ONLY_COLUMNS =
        {
            @"lib_cosine_coel", @"lib_entropy_sim", @"lib_coel_share",
            @"spec_match_prob", @"spec_proj_cosine_xt", @"spec_hyperscore", @"spec_match_prob_max",
            @"spec_proj_cosine_xt_max",
        };

        /// <summary>
        /// With OSPREY_FEATURE_MASK=none, the scores parquets hold the HRAM-only families at HRAM and not at unit
        /// resolution, where <c>Program</c> hands the resolved mode to <see cref="PinFeatureMask.ApplySearchSettings"/>.
        /// OspreyEnvironment reads the mask once per process, so these run Osprey.exe rather than in-process
        /// like the other legs. The unit run also asks for <c>--feature-set core</c>, which the mask overrides:
        /// it records every column the resolution can use, <c>tc_clean_sum6</c> among them, which no set but
        /// <c>all</c> holds. Last, OSPREY_RECORD_ALL_FEATURES=1 at HRAM records all the PIN features while
        /// the classifier uses the core set.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestHramOnlyFamiliesFollowTheResolution()
        {
            string unitDir = CreateDir(@"hram-only-unit");
            RunOspreyProcess(InputArgs(DataInputs()).Concat(new[]
            {
                OspreyCommandArgs.ARG_LIBRARY.ArgumentText, Path.Combine(_dataDir, LIBRARY_FILE),
                OspreyCommandArgs.ARG_OUTPUT.ArgumentText, Path.Combine(unitDir, BLIB_FILE),
                OspreyCommandArgs.ARG_WORK_DIR.ArgumentText, unitDir
            }).Concat(CommonArgs()).Concat(FeatureSetArgs(OspreyFeatureSet.core)), OspreyEnvironment.FEATURE_MASK_NONE);
            AssertHramOnlyColumns(unitDir, false);
            AssertAllAvailableColumns(unitDir, ResolutionMode.UnitResolution);

            string astralDir = ExtractAstral();
            string hramDir = CreateDir(@"hram-only-hram");
            var astralInputs = ASTRAL_RUN_NAMES.Select(run => Path.Combine(astralDir, run + MZML_EXTENSION)).ToArray();
            RunOspreyProcess(AstralProcessArgs(astralDir, astralInputs, hramDir), OspreyEnvironment.FEATURE_MASK_NONE);
            AssertHramOnlyColumns(hramDir, true);
            AssertAllAvailableColumns(hramDir, ResolutionMode.HRAM);

            string recordAllDir = CreateDir(@"record-all-hram");
            RunOspreyProcess(AstralProcessArgs(astralDir, astralInputs, recordAllDir).Concat(FeatureSetArgs(OspreyFeatureSet.core)),
                string.Empty, true);
            AssertAllAvailableColumns(recordAllDir, ResolutionMode.HRAM);
        }

        private static IEnumerable<string> AstralProcessArgs(string astralDir, string[] astralInputs, string workDir)
        {
            return InputArgs(astralInputs).Concat(new[]
            {
                OspreyCommandArgs.ARG_LIBRARY.ArgumentText, Path.Combine(astralDir, ASTRAL_LIBRARY_FILE),
                OspreyCommandArgs.ARG_OUTPUT.ArgumentText, Path.Combine(workDir, BLIB_FILE),
                OspreyCommandArgs.ARG_WORK_DIR.ArgumentText, workDir
            }).Concat(CommonArgs(@"hram"));
        }

        private static void AssertHramOnlyColumns(string workDir, bool expected)
        {
            foreach (string parquet in ScoresParquets(workDir))
            {
                Assert.IsTrue(ParquetScoreCache.HasColumn(parquet, @"tc_clean_sum6"), parquet);
                foreach (string column in HRAM_ONLY_COLUMNS)
                    Assert.AreEqual(expected, ParquetScoreCache.HasColumn(parquet, column), parquet + @" " + column);
            }
        }

        // Every scores parquet holds a column for each PIN feature the resolution can use: all 109 at HRAM.
        private static void AssertAllAvailableColumns(string workDir, ResolutionMode mode)
        {
            var names = ParquetScoreCache.PIN_FEATURE_NAMES;
            Assert.AreEqual(mode == ResolutionMode.HRAM ? names.Length : names.Length - HRAM_ONLY_COLUMNS.Length,
                Enumerable.Range(0, names.Length).Count(f => PinFeatureMask.IsAvailable(f, mode)));
            foreach (string parquet in ScoresParquets(workDir))
            {
                for (int f = 0; f < names.Length; f++)
                {
                    Assert.AreEqual(PinFeatureMask.IsAvailable(f, mode), ParquetScoreCache.HasColumn(parquet, names[f]),
                        parquet + @" " + names[f]);
                }
            }
        }

        private static string[] ScoresParquets(string workDir)
        {
            var parquets = Directory.GetFiles(workDir, @"*" + ParquetScoreCache.EXT_SCORES)
                .Concat(Directory.GetFiles(workDir, @"*" + ParquetScoreCache.EXT_SCORES_RECONCILED)).ToArray();
            Assert.AreNotEqual(0, parquets.Length, workDir);
            return parquets;
        }

        // Osprey.exe in its own process, with the OSPREY_FEATURE_MASK given (empty to leave it unset),
        // OSPREY_RECORD_ALL_FEATURES=1 when asked, and the switches Verifier blanks blanked.
        private static void RunOspreyProcess(IEnumerable<string> args, string featureMask, bool recordAll = false)
        {
            var startInfo = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory,
                OperatingSystem.IsWindows() ? @"Osprey.exe" : @"Osprey"))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (string arg in args)
                startInfo.ArgumentList.Add(arg);
            foreach (var pair in Verifier(false))
                startInfo.Environment[pair.Key] = pair.Value;
            startInfo.Environment[@"OSPREY_FEATURE_MASK"] = featureMask;
            startInfo.Environment[@"OSPREY_RECORD_ALL_FEATURES"] = recordAll ? @"1" : string.Empty;

            var output = new StringBuilder();
            using (var process = new Process())
            {
                process.StartInfo = startInfo;
                process.OutputDataReceived += (_, e) => AppendLine(output, e.Data);
                process.ErrorDataReceived += (_, e) => AppendLine(output, e.Data);
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                Assert.AreEqual(Program.EXIT_CODE_SUCCESS, process.ExitCode, output.ToString());
            }
        }

        private static void AppendLine(StringBuilder output, string line)
        {
            lock (output)
            {
                output.AppendLine(line);
            }
        }
    }
}
