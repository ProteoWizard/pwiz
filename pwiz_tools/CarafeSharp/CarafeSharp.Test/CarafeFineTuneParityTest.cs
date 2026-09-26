/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Models;
using pwiz.CarafeSharp.Training;
using static TorchSharp.torch;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// Fine-tunes on the training data of a Carafe fine-tuning folder (Carafe's
    /// <c>osprey_new_library</c>: <c>psm_pdv.txt</c>, the fragment tables, <c>rt_train_data.tsv</c>,
    /// and its results <c>model_evaluation_metrics.json</c>, <c>rt_test.tsv</c>,
    /// <c>test_true_intensity.tsv</c>) and compares with Carafe:
    /// <list type="bullet">
    /// <item>the held-out RT and MS2 rows are exactly Carafe's;</item>
    /// <item>the pretrained models score exactly as Carafe reports on them;</item>
    /// <item>with <c>CARAFESHARP_FINETUNE_FULL=1</c>, a full fine-tune reaches Carafe's
    /// fine-tuned scores within a small tolerance (dropout noise differs between runs, so this
    /// is statistical, not exact). That takes tens of minutes on a CPU.</item>
    /// </list>
    /// Inconclusive unless <c>CARAFESHARP_CARAFE_FINETUNED</c> names such a folder.
    /// </summary>
    [TestClass]
    public class CarafeFineTuneParityTest
    {
        public const string FULL_RUN_VARIABLE = @"CARAFESHARP_FINETUNE_FULL";

        // Carafe's ai.py arguments for this data (carafe_log.txt): --nce 30 --instrument Eclipse.
        private const double NCE = 30;
        private const string INSTRUMENT = @"Eclipse";

        private const double PRETRAINED_METRIC_TOLERANCE = 2e-4;
        private const double FINETUNED_MS2_TOLERANCE = 0.01;
        private const double FINETUNED_RT_R2_TOLERANCE = 0.005;

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void TestFineTuneMatchesCarafe()
        {
            string folder = Environment.GetEnvironmentVariable(CarafeParityTest.FINETUNED_REFERENCE_VARIABLE);
            if (string.IsNullOrEmpty(folder) || !File.Exists(Path.Combine(folder, CarafeTrainingDirectory.PSM_FILE)))
                Assert.Inconclusive(CarafeParityTest.FINETUNED_REFERENCE_VARIABLE + @" does not name a Carafe fine-tuning folder.");
            if (!File.Exists(PretrainedModels.DefaultPath))
                Assert.Inconclusive(@"No pretrained_models.zip at " + PretrainedModels.DefaultPath);
            var pretrained = PretrainedModels.Open();
            using (var carafeMetrics = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, @"model_evaluation_metrics.json"))))
            {
                var ms2Rows = CarafeTrainingDirectory.ReadMs2(folder, NCE, INSTRUMENT);
                var rtRows = CarafeTrainingDirectory.ReadRt(folder);
                var rtTest = CheckRtSplit(folder, rtRows);
                var ms2Test = CheckMs2Split(folder, ms2Rows);
                CheckPretrainedScores(pretrained, carafeMetrics.RootElement, rtTest, ms2Test);

                if (Environment.GetEnvironmentVariable(FULL_RUN_VARIABLE) == @"1")
                    CheckFullFineTune(pretrained, carafeMetrics.RootElement, rtRows, ms2Rows);
            }
        }

        private IReadOnlyList<RtTrainingExample> CheckRtSplit(string folder, IReadOnlyList<RtTrainingExample> rows)
        {
            int testCount = TrainingSplit.TestCount(rows.Count);
            var forms = RtMetrics.CollapseByForm(rows);
            var (_, test) = TrainingSplit.Split(forms.Select(f => f.Peptide.Sequence).ToArray(),
                forms.Select(f => f.Peptide.ModsText).ToArray(), rows.Count - testCount, testCount);
            var ours = test.Select(i => Key(forms[i])).ToArray();
            var theirs = ReadRtTestKeys(Path.Combine(folder, @"rt_test.tsv"));
            TestContext.WriteLine(@"RT test rows: ours {0}, Carafe {1}", ours.Length, theirs.Length);
            CollectionAssert.AreEquivalent(theirs, ours, @"RT held-out rows differ from Carafe's");
            return test.Select(i => forms[i]).ToArray();
        }

        private IReadOnlyList<Ms2TrainingExample> CheckMs2Split(string folder, IReadOnlyList<Ms2TrainingExample> rows)
        {
            int testCount = TrainingSplit.TestCount(rows.Count);
            var (_, test) = TrainingSplit.Split(rows.Select(r => r.Sequence).ToArray(),
                rows.Select(r => r.Precursor.Peptide.ModsText).ToArray(), rows.Count - testCount, testCount);
            var testRows = test.Select(i => rows[i]).ToArray();
            // Carafe writes the observed intensities of its test spectra in test-set order.
            var theirs = ReadFragmentValues(Path.Combine(folder, @"test_true_intensity.tsv"));
            var ours = testRows.SelectMany(r => r.Intensities).ToArray();
            TestContext.WriteLine(@"MS2 test spectra: {0}, fragment values ours {1}, Carafe {2}", testRows.Length, ours.Length, theirs.Length);
            Assert.AreEqual(theirs.Length, ours.Length, @"MS2 held-out spectra differ from Carafe's");
            for (int i = 0; i < ours.Length; i++)
                Assert.AreEqual(theirs[i], ours[i], 1e-12, @"MS2 held-out spectra differ from Carafe's at value " + i);
            return testRows;
        }

        private void CheckPretrainedScores(PretrainedModels pretrained, JsonElement carafe,
            IReadOnlyList<RtTrainingExample> rtTest, IReadOnlyList<Ms2TrainingExample> ms2Test)
        {
            using (var rt = RtModel.FromPretrained(pretrained, CPU))
            {
                var score = RtMetrics.Evaluate(rt, rtTest);
                var expected = carafe.GetProperty(@"rt").GetProperty(@"pretrained");
                TestContext.WriteLine(@"RT pretrained: ours {0}; Carafe R2 {1:F6}, median |error| {2:F6}", score,
                    expected.GetProperty(@"r2").GetDouble(), expected.GetProperty(@"mae_normalized").GetDouble());
                Assert.AreEqual(expected.GetProperty(@"r2").GetDouble(), score.R2, PRETRAINED_METRIC_TOLERANCE);
                Assert.AreEqual(expected.GetProperty(@"mae_normalized").GetDouble(), score.MedianAbsoluteError, PRETRAINED_METRIC_TOLERANCE);
            }
            using (var ms2 = Ms2Model.FromPretrained(pretrained, CPU))
            {
                var score = Ms2Metrics.Evaluate(ms2, ms2Test);
                var expected = carafe.GetProperty(@"ms2").GetProperty(@"pretrained");
                TestContext.WriteLine(@"MS2 pretrained: ours {0}; Carafe {1}", score, Describe(expected));
                AssertMs2(expected, score, PRETRAINED_METRIC_TOLERANCE);
            }
        }

        private void CheckFullFineTune(PretrainedModels pretrained, JsonElement carafe,
            IReadOnlyList<RtTrainingExample> rtRows, IReadOnlyList<Ms2TrainingExample> ms2Rows)
        {
            string output = Path.Combine(Path.GetTempPath(), @"CarafeSharpFineTune_" + Guid.NewGuid().ToString(@"N"));
            try
            {
                var result = FineTuneRun.Run(rtRows, ms2Rows, pretrained, new FineTuneOptions(), output, TestContext.WriteLine);
                var expectedRt = carafe.GetProperty(@"rt").GetProperty(@"finetuned");
                var expectedMs2 = carafe.GetProperty(@"ms2").GetProperty(@"finetuned");
                TestContext.WriteLine(@"RT fine-tuned: ours {0} in {1:F0} s; Carafe R2 {2:F6}", result.RtFineTuned,
                    result.RtElapsed.TotalSeconds, expectedRt.GetProperty(@"r2").GetDouble());
                TestContext.WriteLine(@"MS2 fine-tuned: ours {0} in {1:F0} s; Carafe {2}", result.Ms2FineTuned,
                    result.Ms2Elapsed.TotalSeconds, Describe(expectedMs2));
                Assert.AreEqual(expectedRt.GetProperty(@"r2").GetDouble(), result.RtFineTuned.R2, FINETUNED_RT_R2_TOLERANCE);
                AssertMs2(expectedMs2, result.Ms2FineTuned, FINETUNED_MS2_TOLERANCE);
                Assert.IsTrue(result.UseFineTunedMs2);
                Assert.IsTrue(File.Exists(Path.Combine(output, FineTuneRun.MS2_MODEL_FILE)));
                // The saved weights reload and predict as the trained model did.
                using (var reloaded = Ms2Model.FromSafetensors(Path.Combine(output, FineTuneRun.MS2_MODEL_FILE), CPU))
                    Assert.AreEqual(result.Ms2FineTuned.Cos, Ms2Metrics.Evaluate(reloaded, CheckMs2SplitQuiet(ms2Rows)).Cos, 1e-6);
            }
            finally
            {
                if (Directory.Exists(output))
                    Directory.Delete(output, true);
            }
        }

        private static IReadOnlyList<Ms2TrainingExample> CheckMs2SplitQuiet(IReadOnlyList<Ms2TrainingExample> rows)
        {
            int testCount = TrainingSplit.TestCount(rows.Count);
            var (_, test) = TrainingSplit.Split(rows.Select(r => r.Sequence).ToArray(),
                rows.Select(r => r.Precursor.Peptide.ModsText).ToArray(), rows.Count - testCount, testCount);
            return test.Select(i => rows[i]).ToArray();
        }

        private static void AssertMs2(JsonElement expected, Ms2MetricSummary actual, double tolerance)
        {
            Assert.AreEqual(expected.GetProperty(@"pcc").GetDouble(), actual.Pcc, tolerance, @"PCC");
            Assert.AreEqual(expected.GetProperty(@"cos").GetDouble(), actual.Cos, tolerance, @"COS");
            Assert.AreEqual(expected.GetProperty(@"sa").GetDouble(), actual.Sa, tolerance, @"SA");
            Assert.AreEqual(expected.GetProperty(@"spc").GetDouble(), actual.Spc, tolerance, @"SPC");
        }

        private static string Describe(JsonElement metrics)
        {
            return string.Format(CultureInfo.InvariantCulture, @"PCC {0:F4}, COS {1:F4}, SA {2:F4}, SPC {3:F4}",
                metrics.GetProperty(@"pcc").GetDouble(), metrics.GetProperty(@"cos").GetDouble(),
                metrics.GetProperty(@"sa").GetDouble(), metrics.GetProperty(@"spc").GetDouble());
        }

        private static string Key(RtTrainingExample example)
        {
            return example.Peptide.Sequence + @"|" + example.Peptide.ModsText + @"|" + example.Peptide.ModSitesText;
        }

        private static string[] ReadRtTestKeys(string path)
        {
            var lines = File.ReadAllLines(path);
            var header = lines[0].Split('\t').ToList();
            int sequence = header.IndexOf(@"sequence"), mods = header.IndexOf(@"mods"), sites = header.IndexOf(@"mod_sites");
            return lines.Skip(1).Where(l => l.Length > 0).Select(l => l.Split('\t'))
                .Select(f => f[sequence] + @"|" + (mods < f.Length ? f[mods] : string.Empty) + @"|" + (sites < f.Length ? f[sites] : string.Empty))
                .ToArray();
        }

        private static double[] ReadFragmentValues(string path)
        {
            return File.ReadAllLines(path).Skip(1).Where(l => l.Length > 0)
                .SelectMany(l => l.Split('\t').Take(Ms2TrainingExample.FRAGMENT_TYPES))
                .Select(v => double.Parse(v, NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
        }
    }
}
