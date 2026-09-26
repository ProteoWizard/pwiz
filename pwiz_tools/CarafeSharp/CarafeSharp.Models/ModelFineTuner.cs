/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/resources/py/v2/models.py
 *   (ModelInterface._train_one_epoch / _train_one_batch) and ai.py (train_ms2, train_rt),
 *   from AlphaPeptDeep, Apache-2.0
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
using System.Linq;
using static TorchSharp.torch;

namespace pwiz.CarafeSharp.Models
{
    /// <summary>One epoch of a fine-tuning run.</summary>
    public sealed class EpochRecord
    {
        public EpochRecord(int epoch, double learningRate, double trainLoss, double testLoss)
        {
            Epoch = epoch;
            LearningRate = learningRate;
            TrainLoss = trainLoss;
            TestLoss = testLoss;
        }

        /// <summary>1-based, as Carafe's history files number them.</summary>
        public int Epoch { get; }

        /// <summary>The learning rate after this epoch's scheduler step (Carafe's <c>lr</c> column).</summary>
        public double LearningRate { get; }

        public double TrainLoss { get; }
        public double TestLoss { get; }
    }

    /// <summary>
    /// Fine-tunes the AlphaPeptDeep models the way Carafe does: Adam at a warmup-cosine
    /// learning rate stepped per epoch, masked L1 loss for MS2 and L1 for RT, gradients clipped
    /// to norm 1, and each epoch visiting the training rows grouped by peptide length in an
    /// order drawn from numpy's global random state (<c>sample(frac=1)</c>, then a permutation
    /// of the length groups). The last epoch's weights are kept.
    ///
    /// <para>Pass the same <see cref="NumpyRandomState"/> to the RT and then the MS2 run to draw
    /// the shuffles Carafe draws (its <c>np.random.seed</c> is set once and RT trains first).
    /// Dropout comes from libtorch's generator and is not reproduced across implementations,
    /// so fine-tuned models agree with Carafe's statistically, not bit for bit.</para>
    /// </summary>
    public static class ModelFineTuner
    {
        private const double MAX_GRAD_NORM = 1.0;

        public static IReadOnlyList<EpochRecord> TrainMs2(Ms2Model model, IReadOnlyList<Ms2TrainingExample> train,
            IReadOnlyList<Ms2TrainingExample> test, FineTuneSettings settings, int batchSize,
            NumpyRandomState shuffle, Action<string> log)
        {
            var normalized = train.Select(NormalizeToApex).ToArray();
            var network = model.Network;
            return Train(network, normalized, test, settings, batchSize, shuffle, log, e => e.Precursor.Peptide.Length,
                batch => Ms2Loss(model, batch));
        }

        public static IReadOnlyList<EpochRecord> TrainRt(RtModel model, IReadOnlyList<RtTrainingExample> train,
            IReadOnlyList<RtTrainingExample> test, FineTuneSettings settings, int batchSize,
            NumpyRandomState shuffle, Action<string> log)
        {
            return Train(model.Network, train, test, settings, batchSize, shuffle, log, e => e.Peptide.Length,
                batch => RtLoss(model, batch));
        }

        private static IReadOnlyList<EpochRecord> Train<T>(nn.Module network, IReadOnlyList<T> train, IReadOnlyList<T> test,
            FineTuneSettings settings, int batchSize, NumpyRandomState shuffle, Action<string> log,
            Func<T, int> getLength, Func<IReadOnlyList<T>, Tensor> loss)
        {
            log = log ?? (_ => { });
            // Every parameter, as Carafe passes model.parameters(); the LSTM's frozen initial
            // states (requires_grad false) and the unused modloss branch get no gradient, so Adam
            // leaves them alone. StateDict.Load keeps each parameter's requires_grad.
            var parameters = network.parameters().ToArray();
            var optimizer = optim.Adam(parameters, settings.LearningRate);
            var scheduler = optim.lr_scheduler.LambdaLR(optimizer, settings.LearningRateFactor);
            var history = new List<EpochRecord>();

            network.train();
            for (int epoch = 0; epoch < settings.Epochs; epoch++)
            {
                var batchLosses = new List<double>();
                foreach (var batch in EpochBatches(train, getLength, batchSize, shuffle))
                {
                    using (NewDisposeScope())
                    {
                        optimizer.zero_grad();
                        var cost = loss(batch);
                        cost.backward();
                        nn.utils.clip_grad_norm_(parameters, MAX_GRAD_NORM);
                        optimizer.step();
                        batchLosses.Add(cost.item<float>());
                    }
                }
                scheduler.step();
                double testLoss = TestLoss(network, test, getLength, batchSize, loss);
                double lr = optimizer.ParamGroups.First().LearningRate;
                history.Add(new EpochRecord(epoch + 1, lr, batchLosses.Count > 0 ? batchLosses.Average() : 0, testLoss));
                log(string.Format(@"  epoch {0}/{1}: lr {2:E2}, train loss {3:F6}, test loss {4:F6}",
                    epoch + 1, settings.Epochs, lr, history[history.Count - 1].TrainLoss, testLoss));
            }
            network.eval();
            return history;
        }

        /// <summary>
        /// One epoch's batches in Carafe's order: all rows shuffled (<c>sample(frac=1)</c>),
        /// grouped by length in ascending order keeping the shuffled order inside a group, the
        /// groups visited in a random permutation, each group sliced into batches.
        /// </summary>
        internal static IEnumerable<IReadOnlyList<T>> EpochBatches<T>(IReadOnlyList<T> rows, Func<T, int> getLength,
            int batchSize, NumpyRandomState shuffle)
        {
            int[] order = shuffle.ChooseWithoutReplacement(rows.Count, rows.Count);
            var groups = new SortedDictionary<int, List<T>>();
            foreach (int index in order)
            {
                int length = getLength(rows[index]);
                if (!groups.TryGetValue(length, out var group))
                {
                    group = new List<T>();
                    groups.Add(length, group);
                }
                group.Add(rows[index]);
            }
            var groupList = groups.Values.ToArray();
            foreach (int g in shuffle.Permutation(groupList.Length))
            {
                var group = groupList[g];
                for (int start = 0; start < group.Count; start += batchSize)
                    yield return group.GetRange(start, Math.Min(batchSize, group.Count - start));
            }
        }

        private static double TestLoss<T>(nn.Module network, IReadOnlyList<T> test, Func<T, int> getLength, int batchSize,
            Func<IReadOnlyList<T>, Tensor> loss)
        {
            if (test.Count == 0)
                return 0;
            network.eval();
            var losses = new List<double>();
            using (no_grad())
            {
                foreach (var group in test.GroupBy(getLength))
                {
                    var rows = group.ToList();
                    for (int start = 0; start < rows.Count; start += batchSize)
                    {
                        using (NewDisposeScope())
                            losses.Add(loss(rows.GetRange(start, Math.Min(batchSize, rows.Count - start))).item<float>());
                    }
                }
            }
            network.train();
            return losses.Average();
        }

        private static Tensor Ms2Loss(Ms2Model model, IReadOnlyList<Ms2TrainingExample> batch)
        {
            var predicted = model.Forward(batch.Select(e => new Ms2Request(e.Precursor, e.Nce, e.Instrument)).ToArray());
            int rows = batch[0].Precursor.Peptide.Length - 1;
            int width = rows * PeptdeepConstants.CHARGED_FRAG_TYPES.Length;
            var observed = new float[batch.Count * width];
            var mask = new float[batch.Count * width];
            for (int i = 0; i < batch.Count; i++)
                Ms2Metrics.FillObserved(batch[i], observed, mask, i * width);
            var shape = new long[] { batch.Count, rows, PeptdeepConstants.CHARGED_FRAG_TYPES.Length };
            var target = tensor(observed, shape).to(model.Device);
            var weight = tensor(mask, shape).to(model.Device);
            // Carafe: L1Loss(reduction='sum')(mask * pred, mask * target) / mask.sum(). The four
            // zero modloss columns are unmasked, so they count in the denominator.
            return (weight * predicted - weight * target).abs().sum() / weight.sum();
        }

        private static Tensor RtLoss(RtModel model, IReadOnlyList<RtTrainingExample> batch)
        {
            var predicted = model.Forward(batch.Select(e => e.Peptide).ToArray());
            var target = tensor(batch.Select(e => (float)e.RtNorm).ToArray()).to(model.Device);
            return (predicted - target).abs().mean();
        }

        /// <summary>
        /// Carafe's <c>normalize_fragment_intensities</c>: each training spectrum divided by its
        /// most intense ion (in float64, before the float32 tensor is built).
        /// </summary>
        private static Ms2TrainingExample NormalizeToApex(Ms2TrainingExample example)
        {
            double max = example.Intensities.Max();
            if (max <= 0)
                return example;
            return new Ms2TrainingExample(example.Precursor, example.Nce, example.Instrument,
                example.Intensities.Select(v => v / max).ToArray(), example.Invalid);
        }
    }
}
