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

using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.Models;
using static TorchSharp.torch;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// Loads the real AlphaPeptDeep pretrained weights and checks that the port predicts
    /// sensibly. Inconclusive when the pinned <c>pretrained_models.zip</c> is not on the
    /// machine; exact numeric parity with Python is covered by the reference-dump test.
    /// </summary>
    [TestClass]
    public class PretrainedModelTest
    {
        [TestMethod]
        public void TestPretrainedModelsPredict()
        {
            if (!File.Exists(PretrainedModels.DefaultPath))
                Assert.Inconclusive(@"No pretrained_models.zip at " + PretrainedModels.DefaultPath);
            var pretrained = PretrainedModels.Open();

            using (var rt = RtModel.FromPretrained(pretrained, CPU))
            {
                // The iRT kit peptides are ordered by iRT, so a working model ranks them in order.
                var (slope, _) = rt.FitIrtCalibration();
                Assert.IsTrue(slope > 0, @"iRT slope " + slope);
                double[] predicted = rt.Predict(new[]
                {
                    new PeptideForm(@"LGGNEQVTR"), new PeptideForm(@"YILAGVENSK"),
                    new PeptideForm(@"ADVTPADFSEWSK"), new PeptideForm(@"LFLQFGAQGSPFLK"),
                });
                for (int i = 1; i < predicted.Length; i++)
                    Assert.IsTrue(predicted[i] > predicted[i - 1], @"RT order at " + i);
            }

            using (var ms2 = Ms2Model.FromPretrained(pretrained, CPU))
            {
                var precursor = new PrecursorForm(new PeptideForm(@"LGGNEQVTR"), 2);
                var prediction = ms2.Predict(new[] { new Ms2Request(precursor, 30, @"Lumos") }).Single();
                Assert.AreEqual(8, prediction.RowCount);
                Assert.AreEqual(1f, prediction.Intensities.Max());
                Assert.IsTrue(prediction.Intensities.All(v => v >= 0));
                // General mode: the four modloss columns are exactly zero.
                for (int row = 0; row < prediction.RowCount; row++)
                {
                    for (int col = 4; col < 8; col++)
                        Assert.AreEqual(0f, prediction.Get(row, col));
                }
                // A tryptic peptide fragments mostly to singly charged y ions.
                float ySum = Enumerable.Range(0, prediction.RowCount).Sum(r => prediction.Get(r, 2));
                float bSum = Enumerable.Range(0, prediction.RowCount).Sum(r => prediction.Get(r, 0));
                Assert.IsTrue(ySum > bSum, string.Format(@"y {0} vs b {1}", ySum, bSum));
            }
        }
    }
}
