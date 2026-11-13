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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    public partial class SubsetPipelineTest
    {
        /// <summary>
        /// <c>--feature-set</c> on the Stellar subset (unit resolution). Without it the search uses the
        /// core set and its scores parquets hold the core columns alone. The same directory searched with
        /// the default set must score again, because per-file scoring keys the recorded columns, and
        /// its parquets then hold exactly the default set's columns the resolution can use - not the
        /// rest of the families it computes. Searched again, every task is cached. A first-pass FDR
        /// alone under a wider set, over scores that lack its columns, must not read them as zeros.
        /// Last, the efficient set scores again and records its own, smaller set of columns.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestSubsetFeatureSets()
        {
            string workDir = CreateDir(@"feature-sets");
            string log = RunAnalysis(workDir, DataInputs(), Verifier(false));
            AssertFeatureSetRun(workDir, log, null, OspreyFeatureSet.core);

            log = RunAnalysis(workDir, DataInputs(), Verifier(false), FeatureSetArgs(OspreyFeatureSet.@default));
            AssertTasks(log, Array.Empty<string>(), ALL_TASKS);
            AssertFeatureSetRun(workDir, log, OspreyFeatureSet.@default, OspreyFeatureSet.@default);

            log = RunAnalysis(workDir, DataInputs(), Verifier(false), FeatureSetArgs(OspreyFeatureSet.@default));
            AssertTasks(log, ALL_TASKS, Array.Empty<string>());
            AssertNoRecompute(log);

            // The first-pass FDR alone, under the all set, over the default set's scores: a single-task
            // run adopts the scores it is pointed at, so the feature load must refuse them, naming the
            // first column the set uses that they lack, rather than train on zeros.
            log = RunAnalysisExpecting(workDir, RunNames(workDir, MZML_EXTENSION), LIBRARY_FILE, Verifier(false),
                Program.EXIT_CODE_FAILURE_TO_START,
                FeatureSetArgs(OspreyFeatureSet.all).Concat(new[] { OspreyCommandArgs.ARG_TASK.ArgumentText, FirstPassFdrTask.TASK_NAME }).ToArray());
            int missing = Enumerable.Range(0, ParquetScoreCache.NUM_PIN_FEATURES)
                .First(f => PinFeatureMask.IsAvailable(f, ResolutionMode.UnitResolution) &&
                            !PinFeatureMask.FeatureSetIndices(OspreyFeatureSet.@default).Contains(f));
            string error = SplitLines(log).Single(CommandStatusWriter.IsErrorLine);
            StringAssert.Contains(error, ParquetScoreCache.PIN_FEATURE_NAMES[missing]);
            StringAssert.Contains(error, ParquetScoreCache.EXT_SCORES);

            // The efficient set, within the default: fewer columns is another file too, so it scores again.
            log = RunAnalysis(workDir, DataInputs(), Verifier(false), FeatureSetArgs(OspreyFeatureSet.efficient));
            AssertTasks(log, Array.Empty<string>(), ALL_TASKS);
            AssertFeatureSetRun(workDir, log, OspreyFeatureSet.efficient, OspreyFeatureSet.efficient);
        }

        private static string[] FeatureSetArgs(OspreyFeatureSet featureSet)
        {
            return new[] { OspreyCommandArgs.ARG_FEATURE_SET.ArgumentText, featureSet.ToString() };
        }

        // The run logged the set it used, and every scores parquet it left holds exactly the core and
        // the set's columns the resolution can use.
        private static void AssertFeatureSetRun(string workDir, string log, OspreyFeatureSet? asked, OspreyFeatureSet expected)
        {
            const ResolutionMode unit = ResolutionMode.UnitResolution;
            string[] expectedColumns;
            using (new FeatureSetScope(unit, asked))
            {
                Assert.AreEqual(expected, PinFeatureMask.FeatureSet);
                AssertHasLine(log, PinFeatureMask.DescribeFeatures());
                expectedColumns = Enumerable.Range(0, ParquetScoreCache.NUM_PIN_FEATURES).Where(PinFeatureMask.IsRecorded)
                    .Select(f => ParquetScoreCache.PIN_FEATURE_NAMES[f]).ToArray();
            }
            var inSet = PinFeatureMask.FeatureSetIndices(expected)
                .Where(f => PinFeatureMask.IsAvailable(f, unit)).Select(f => ParquetScoreCache.PIN_FEATURE_NAMES[f]);
            CollectionAssert.AreEqual(inSet.ToArray(), expectedColumns, @"the recorded columns are the set's");

            var parquets = Directory.GetFiles(workDir, @"*" + ParquetScoreCache.EXT_SCORES)
                .Concat(Directory.GetFiles(workDir, @"*" + ParquetScoreCache.EXT_SCORES_RECONCILED)).ToArray();
            Assert.AreNotEqual(0, parquets.Length, workDir);
            foreach (string parquet in parquets)
            {
                foreach (string name in ParquetScoreCache.PIN_FEATURE_NAMES)
                {
                    Assert.AreEqual(expectedColumns.Contains(name), ParquetScoreCache.HasColumn(parquet, name),
                        parquet + @" " + name);
                }
            }
        }
    }
}
