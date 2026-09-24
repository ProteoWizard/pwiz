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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Models;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// The pieces of Carafe's fine-tuning that are deterministic: numpy's legacy random state,
    /// pandas sampling, the train/test split, batch-size adjustment and the learning-rate
    /// schedule. Expected values were produced by numpy 2.5 / pandas 3.0 (the legacy
    /// RandomState stream is frozen, so they equal Carafe's numpy 1.26 / pandas 2.2).
    /// </summary>
    [TestClass]
    public class FineTuneSamplingTest
    {
        [TestMethod]
        public void TestNumpyRandomState()
        {
            // np.random.RandomState(1337).permutation(12)
            CollectionAssert.AreEqual(new[] { 5, 3, 1, 0, 4, 6, 10, 2, 9, 11, 8, 7 }, new NumpyRandomState(1337).Permutation(12));
            // Two draws from one RandomState(2024) continue the same stream.
            var random = new NumpyRandomState(2024);
            CollectionAssert.AreEqual(new[] { 1, 5, 3, 4, 6, 2, 0 }, random.Permutation(7));
            CollectionAssert.AreEqual(new[] { 2, 0, 4, 3, 1 }, random.Permutation(5));
            // pandas df.sample(6, random_state=1337) over 20 rows.
            CollectionAssert.AreEqual(new[] { 5, 3, 4, 0, 15, 11 }, new NumpyRandomState(1337).ChooseWithoutReplacement(20, 6));
            // np.random.seed(2024); df.sample(frac=1) over 20 rows.
            CollectionAssert.AreEqual(new[] { 11, 15, 19, 6, 13, 12, 16, 5, 17, 2, 10, 3, 1, 9, 7, 14, 4, 18, 0, 8 },
                new NumpyRandomState(2024).ChooseWithoutReplacement(20, 20));
            // A long stream: RandomState(99).permutation(100000)
            var large = new NumpyRandomState(99).Permutation(100000);
            CollectionAssert.AreEqual(new[] { 86155, 29633, 67710, 62348, 98787 }, large.Take(5).ToArray());
            Assert.AreEqual(51894556L, large.Take(1000).Sum(v => (long)v));
        }

        [TestMethod]
        public void TestTrainingSplit()
        {
            // 200 distinct sequences: n_test 10, n_train 190, plus up to 50 rows per modification.
            var sequences = Enumerable.Range(0, 200).Select(i => string.Format(@"SEQ{0:000}K", i)).ToArray();
            var mods = Enumerable.Range(0, 200).Select(i => i % 4 == 0 ? @"Carbamidomethyl@C" : i % 7 == 0 ? @"Oxidation@M" : string.Empty).ToArray();
            int testCount = TrainingSplit.TestCount(200);
            Assert.AreEqual(10, testCount);
            var (train, test) = TrainingSplit.Split(sequences, mods, 200 - testCount, testCount);
            Assert.AreEqual(261, train.Length);
            CollectionAssert.AreEqual(new[] { 29, 117, 69, 174, 97, 133, 14, 101, 86, 172, 124, 25 }, train.Take(12).ToArray());
            CollectionAssert.AreEqual(new[] { 147, 154, 161, 175, 182, 189 }, train.Skip(train.Length - 6).ToArray());
            Assert.AreEqual(25621, train.Sum());
            // Only 5 sequences are left untrained, fewer than n_test, so all 5 are the test set.
            CollectionAssert.AreEqual(new[] { 82, 90, 114, 151, 167 }, test);

            // When every sequence is trained on, Carafe tests on every row.
            var repeated = Enumerable.Range(0, 30).Select(i => @"SEQ" + (i % 20)).ToArray();
            var none = Enumerable.Repeat(string.Empty, 30).ToArray();
            var (_, allRows) = TrainingSplit.Split(repeated, none, 29, TrainingSplit.TestCount(30));
            CollectionAssert.AreEqual(Enumerable.Range(0, 30).ToArray(), allRows);
        }

        [TestMethod]
        public void TestScheduleAndBatchSize()
        {
            var ms2 = FineTuneSettings.Ms2Defaults();
            // Carafe on 13,806 MS2 training rows: 27 steps at 512 is under 40, so 256.
            Assert.AreEqual(256, ms2.EffectiveBatchSize(13806));
            Assert.AreEqual(512, ms2.EffectiveBatchSize(40 * 512));
            Assert.AreEqual(32, ms2.EffectiveBatchSize(100));
            Assert.AreEqual(256, FineTuneSettings.RtDefaults().EffectiveBatchSize(18881));
            // Warmup from 0: epoch 0 trains at lr 0, epoch 1 at a tenth; cosine to 0 after epoch 10.
            Assert.AreEqual(0.0, ms2.LearningRateFactor(0));
            Assert.AreEqual(0.1, ms2.LearningRateFactor(1), 1e-12);
            Assert.AreEqual(1.0, ms2.LearningRateFactor(10), 1e-12);
            Assert.AreEqual(0.5, ms2.LearningRateFactor(15), 1e-12);
            Assert.AreEqual(0.0, ms2.LearningRateFactor(20), 1e-12);
            // A warmup longer than the run is halved.
            var shortRun = new FineTuneSettings { Epochs = 6, WarmupEpochs = 10, BatchSize = 64, LearningRate = 1e-4 };
            Assert.AreEqual(1.0 / 3, shortRun.LearningRateFactor(1), 1e-12);
        }
    }
}
