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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.IO;
using pwiz.CarafeSharp.Models;
using static TorchSharp.torch;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// Compares CarafeSharp's predictions with the ones Carafe itself wrote for the same
    /// precursors, from Carafe library-generation output folders: the <c>0_ms2_df</c> input
    /// frame (sequence, mods, charge, NCE, instrument, fragment row range) and the
    /// <c>0_ms2_pred</c> and <c>0_rt_pred</c> outputs of Carafe's Python.
    ///
    /// Two kinds of folder (<see cref="TestData"/> finds them): libraries predicted with the
    /// generic pretrained models (Carafe's <c>osprey_initial_library</c>,
    /// <c>CARAFESHARP_CARAFE_REFERENCE</c>), and libraries predicted after fine-tuning
    /// (<c>osprey_new_library</c>, which also holds the fine-tuned <c>ms2_model.pt</c> and
    /// <c>rt_model.pt</c>, <c>CARAFESHARP_CARAFE_FINETUNED</c>).
    /// </summary>
    [TestClass]
    public class CarafeParityTest
    {
        private const string MS2_FRAME_FILE = @"0_ms2_df.parquet";
        private const string MS2_PREDICTION_FILE = @"0_ms2_pred.parquet";
        private const string RT_PREDICTION_FILE = @"0_rt_pred.parquet";
        private const string MS2_MODEL_FILE = @"ms2_model.pt";
        private const string RT_MODEL_FILE = @"rt_model.pt";

        /// <summary>Precursors compared per folder, spread evenly over the file.</summary>
        private const int SAMPLE_SIZE = 4000;

        // Measured against Carafe 2.2.0 (torch 2.5.1 on CUDA) from libtorch 2.10.0 on the CPU:
        // MS2 max 2.6e-6, 99.9th percentile 8.9e-7; RT 2.4e-7; iRT 3.5e-5. Limits allow ~5x.
        private const double MS2_MAX_DIFF = 1.5e-5;
        private const double MS2_P999_DIFF = 5e-6;
        private const double RT_MAX_DIFF = 2e-6;
        private const double IRT_MAX_DIFF = 5e-4;

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void TestPredictionsMatchCarafe()
        {
            ComparePredictions(TestData.PretrainedLibraries, TestData.FineTunedLibraries);
        }

        [TestMethod, TestCategory(TestData.ASTRAL_CATEGORY)]
        public void TestAstralPredictionsMatchCarafe()
        {
            ComparePredictions(TestData.AstralPretrainedLibraries, TestData.AstralFineTunedLibraries);
        }

        private void ComparePredictions(TestData.Item pretrainedItem, TestData.Item finetunedItem)
        {
            TestData.InconclusiveUnlessAvailable(pretrainedItem, finetunedItem);
            var pretrainedDirs = pretrainedItem.Resolve();
            var finetunedDirs = finetunedItem.Resolve();
            foreach (string folder in pretrainedDirs)
                TestData.RequireFiles(folder, MS2_FRAME_FILE, MS2_PREDICTION_FILE, RT_PREDICTION_FILE);
            foreach (string folder in finetunedDirs)
                TestData.RequireFiles(folder, MS2_FRAME_FILE, MS2_PREDICTION_FILE, RT_PREDICTION_FILE, MS2_MODEL_FILE, RT_MODEL_FILE);

            if (pretrainedDirs.Count > 0)
            {
                var pretrained = PretrainedModels.Open();
                using (var ms2 = Ms2Model.FromPretrained(pretrained, CPU))
                using (var rt = RtModel.FromPretrained(pretrained, CPU))
                {
                    foreach (string folder in pretrainedDirs)
                        CompareFolder(@"pretrained " + folder, folder, ms2, rt, true);
                }
            }
            foreach (string folder in finetunedDirs)
            {
                // Carafe uses its fine-tuned MS2 model only when it beats the pretrained one on
                // all four test metrics; these reference folders record that it did.
                using (var ms2 = Ms2Model.FromPthFile(Path.Combine(folder, MS2_MODEL_FILE), CPU))
                using (var rt = RtModel.FromPthFile(Path.Combine(folder, RT_MODEL_FILE), CPU))
                    CompareFolder(@"fine-tuned " + folder, folder, ms2, rt, false);
            }
        }

        private void CompareFolder(string label, string folder, Ms2Model ms2, RtModel rt, bool compareIrt)
        {
            var frame = ParquetColumns.Read(Path.Combine(folder, MS2_FRAME_FILE),
                @"sequence", @"charge", @"mods", @"mod_sites", @"instrument", @"nce", @"frag_start_idx");
            var predicted = ParquetColumns.Read(Path.Combine(folder, MS2_PREDICTION_FILE),
                @"b_z1", @"b_z2", @"y_z1", @"y_z2");
            CompareMs2(label, ms2, frame, predicted);
            CompareRt(label, rt, Path.Combine(folder, RT_PREDICTION_FILE), compareIrt);
        }

        private void CompareMs2(string label, Ms2Model model, ParquetColumns frame, ParquetColumns predicted)
        {
            int[] rows = SampleRows(frame.RowCount);
            string[] sequences = frame.Get<string>(@"sequence");
            int[] charges = frame.Get<int>(@"charge");
            string[] mods = frame.Get<string>(@"mods");
            string[] sites = frame.Get<string>(@"mod_sites");
            string[] instruments = frame.Get<string>(@"instrument");
            double[] nces = frame.Get<double>(@"nce");
            long[] starts = frame.Get<long>(@"frag_start_idx");
            var columns = new[] { @"b_z1", @"b_z2", @"y_z1", @"y_z2" }.Select(c => predicted.Get<float>(c)).ToArray();

            var requests = rows.Select(r => new Ms2Request(
                new PrecursorForm(PeptideForm.FromAlphabase(sequences[r], mods[r], sites[r]), charges[r]),
                nces[r], instruments[r])).ToArray();
            var ours = model.Predict(requests);

            var differences = new List<double>();
            int cutoffFlips = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                var prediction = ours[i];
                for (int row = 0; row < prediction.RowCount; row++)
                {
                    for (int col = 0; col < columns.Length; col++)
                    {
                        float theirs = columns[col][starts[rows[i]] + row];
                        float mine = prediction.Get(row, col);
                        // A value within rounding of the 1e-4 cutoff can land on either side of it.
                        if ((theirs == 0) != (mine == 0) && Math.Max(theirs, mine) < 2 * PeptdeepConstants.MIN_INTENSITY)
                        {
                            cutoffFlips++;
                            continue;
                        }
                        differences.Add(Math.Abs(theirs - mine));
                    }
                }
            }
            differences.Sort();
            double max = differences[differences.Count - 1];
            double p999 = differences[(int)(differences.Count * 0.999)];
            TestContext.WriteLine(@"{0} MS2: {1} precursors, {2} fragment values, max |diff| {3:E3}, 99.9th pct {4:E3}, cutoff flips {5}",
                label, rows.Length, differences.Count, max, p999, cutoffFlips);
            Assert.IsTrue(max < MS2_MAX_DIFF, label + @" MS2 max |diff| " + max);
            Assert.IsTrue(p999 < MS2_P999_DIFF, label + @" MS2 99.9th percentile |diff| " + p999);
            Assert.IsTrue(cutoffFlips <= differences.Count / 10000, label + @" MS2 cutoff flips " + cutoffFlips);
        }

        private void CompareRt(string label, RtModel model, string rtPath, bool compareIrt)
        {
            var reference = ParquetColumns.Read(rtPath, @"sequence", @"mods", @"mod_sites", @"rt_pred", @"irt_pred");
            int[] rows = SampleRows(reference.RowCount);
            string[] sequences = reference.Get<string>(@"sequence");
            string[] mods = reference.Get<string>(@"mods");
            string[] sites = reference.Get<string>(@"mod_sites");
            double[] rtPred = reference.Get<double>(@"rt_pred");
            double[] irtPred = reference.Get<double>(@"irt_pred");
            var peptides = rows.Select(r => PeptideForm.FromAlphabase(sequences[r], mods[r], sites[r])).ToArray();
            double[] ours = model.Predict(peptides);
            var irt = model.FitIrtCalibration();
            double maxRt = 0, maxIrt = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                maxRt = Math.Max(maxRt, Math.Abs(ours[i] - rtPred[rows[i]]));
                maxIrt = Math.Max(maxIrt, Math.Abs(ours[i] * irt.Slope + irt.Intercept - irtPred[rows[i]]));
            }
            TestContext.WriteLine(@"{0} RT: {1} peptides, max |diff| rt_norm {2:E3}, iRT {3:E3}", label, rows.Length, maxRt, maxIrt);
            Assert.IsTrue(maxRt < RT_MAX_DIFF, label + @" RT max |diff| " + maxRt);
            if (compareIrt)
                Assert.IsTrue(maxIrt < IRT_MAX_DIFF, label + @" iRT max |diff| " + maxIrt);
        }

        private static int[] SampleRows(int count)
        {
            int step = Math.Max(1, count / SAMPLE_SIZE);
            return Enumerable.Range(0, count / step).Select(i => i * step).ToArray();
        }
    }
}
