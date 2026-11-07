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
        /// resolution, where <c>Program</c> hands the resolved mode to <see cref="PinFeatureMask.ApplyResolutionMode"/>.
        /// OspreyEnvironment reads the mask once per process, so these run Osprey.exe rather than in-process
        /// like the other legs. Another family's column shows the mask reached the run.
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
            }).Concat(CommonArgs()));
            AssertHramOnlyColumns(unitDir, false);

            string astralDir = ExtractAstral();
            string hramDir = CreateDir(@"hram-only-hram");
            var astralInputs = ASTRAL_RUN_NAMES.Select(run => Path.Combine(astralDir, run + MZML_EXTENSION));
            RunOspreyProcess(InputArgs(astralInputs).Concat(new[]
            {
                OspreyCommandArgs.ARG_LIBRARY.ArgumentText, Path.Combine(astralDir, ASTRAL_LIBRARY_FILE),
                OspreyCommandArgs.ARG_OUTPUT.ArgumentText, Path.Combine(hramDir, BLIB_FILE),
                OspreyCommandArgs.ARG_WORK_DIR.ArgumentText, hramDir
            }).Concat(CommonArgs(@"hram")));
            AssertHramOnlyColumns(hramDir, true);
        }

        private static void AssertHramOnlyColumns(string workDir, bool expected)
        {
            var parquets = Directory.GetFiles(workDir, @"*.scores.parquet")
                .Concat(Directory.GetFiles(workDir, @"*" + ParquetScoreCache.EXT_SCORES_RECONCILED)).ToArray();
            Assert.AreNotEqual(0, parquets.Length, workDir);
            foreach (string parquet in parquets)
            {
                Assert.IsTrue(ParquetScoreCache.HasColumn(parquet, @"tc_clean_sum6"), parquet);
                foreach (string column in HRAM_ONLY_COLUMNS)
                    Assert.AreEqual(expected, ParquetScoreCache.HasColumn(parquet, column), parquet + @" " + column);
            }
        }

        // Osprey.exe in its own process, with OSPREY_FEATURE_MASK=none and the switches Verifier blanks blanked.
        private static void RunOspreyProcess(IEnumerable<string> args)
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
            startInfo.Environment[@"OSPREY_FEATURE_MASK"] = @"none";
            startInfo.Environment[@"OSPREY_RECORD_ALL_FEATURES"] = string.Empty;

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
