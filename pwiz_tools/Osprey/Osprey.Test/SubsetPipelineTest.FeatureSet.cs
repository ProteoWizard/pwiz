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
using pwiz.BiblioSpec.IO;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Test
{
    public partial class SubsetPipelineTest
    {
        /// <summary>
        /// <c>--feature-set</c> on the Stellar subset (unit resolution), with <c>--keep-scores-parquet</c>
        /// so each run keeps its features parquets beside its stub scores parquet. Without the option
        /// the search uses the default set, and its features parquets hold exactly the default set's
        /// columns the resolution can use - not the rest of the families it computes. The same
        /// directory searched with the core set must score again, because per-file scoring keys the
        /// recorded columns, and the features parquets then hold the core columns alone. Searched
        /// again, every task is cached. Last, the efficient set scores again and records its own set
        /// of columns.
        /// A first-pass FDR alone reads no features (each run's own model scored the run in
        /// PerFileScoring), so it has no columns to refuse.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestSubsetFeatureSets()
        {
            string workDir = CreateDir(@"feature-sets");
            string log = RunAnalysis(workDir, DataInputs(), Verifier(false), KeepScoresParquetArgs());
            AssertFeatureSetRun(workDir, log, null, OspreyFeatureSet.@default);
            AssertNoZeroApexUnderArea(workDir);
            AssertBlibFeatureSet(workDir, OspreyFeatureSet.@default.ToString());

            log = RunAnalysis(workDir, DataInputs(), Verifier(false), FeatureSetArgs(OspreyFeatureSet.core).Concat(KeepScoresParquetArgs()).ToArray());
            AssertTasks(log, Array.Empty<string>(), ALL_TASKS);
            AssertFeatureSetRun(workDir, log, OspreyFeatureSet.core, OspreyFeatureSet.core);
            AssertBlibFeatureSet(workDir, null);

            log = RunAnalysis(workDir, DataInputs(), Verifier(false), FeatureSetArgs(OspreyFeatureSet.core).Concat(KeepScoresParquetArgs()).ToArray());
            AssertTasks(log, ALL_TASKS, Array.Empty<string>());
            AssertNoRecompute(log);

            // The efficient set, within the default: fewer columns is another file too, so it scores again.
            log = RunAnalysis(workDir, DataInputs(), Verifier(false), FeatureSetArgs(OspreyFeatureSet.efficient).Concat(KeepScoresParquetArgs()).ToArray());
            AssertTasks(log, Array.Empty<string>(), ALL_TASKS);
            AssertFeatureSetRun(workDir, log, OspreyFeatureSet.efficient, OspreyFeatureSet.efficient);
            AssertBlibFeatureSet(workDir, OspreyFeatureSet.efficient.ToString());
        }

        /// <summary>The library records a set other than core in its LibMetadata, and nothing for core.</summary>
        private static void AssertBlibFeatureSet(string workDir, string expected)
        {
            string blibPath = Path.Combine(workDir, BLIB_FILE);
            Assert.AreEqual(expected, BlibMetadata.Read(blibPath, OspreyBlibWriter.KEY_FEATURE_SET), blibPath);
        }

        private static string[] FeatureSetArgs(OspreyFeatureSet featureSet)
        {
            return new[] { OspreyCommandArgs.ARG_FEATURE_SET.ArgumentText, featureSet.ToString() };
        }

        /// <summary>Keeps each run's features parquets, which hold the feature columns the stub
        /// scores parquets no longer do.</summary>
        private static string[] KeepScoresParquetArgs()
        {
            return new[] { OspreyCommandArgs.ARG_KEEP_SCORES_PARQUET.ArgumentText };
        }

        // The run logged the set it used, and every features parquet it left holds exactly the core and
        // the set's columns the resolution can use.
        private static void AssertFeatureSetRun(string workDir, string log, OspreyFeatureSet? asked, OspreyFeatureSet expected)
        {
            const ResolutionMode unit = ResolutionMode.UnitResolution;
            string[] expectedColumns;
            var selection = PinFeatureMask.For(unit, asked);
            Assert.AreEqual(expected, selection.FeatureSet);
            AssertHasLine(log, selection.DescribeFeatures());
            expectedColumns = Enumerable.Range(0, ParquetScoreCache.NUM_PIN_FEATURES).Where(selection.IsRecorded)
                .Select(f => ParquetScoreCache.PIN_FEATURE_NAMES[f]).ToArray();
            var inSet = PinFeatureMask.FeatureSetIndices(expected)
                .Where(f => PinFeatureMask.IsAvailable(f, unit)).Select(f => ParquetScoreCache.PIN_FEATURE_NAMES[f]);
            CollectionAssert.AreEqual(inSet.ToArray(), expectedColumns, @"the recorded columns are the set's");

            var parquets = Directory.GetFiles(workDir, @"*" + ParquetScoreCache.EXT_FEATURES)
                .Concat(Directory.GetFiles(workDir, @"*" + ParquetScoreCache.EXT_FEATURES_RECONCILED)).ToArray();
            // A first-pass and a second-pass features parquet per run.
            Assert.AreEqual(2 * RUN_NAMES.Length, parquets.Length, workDir);
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
