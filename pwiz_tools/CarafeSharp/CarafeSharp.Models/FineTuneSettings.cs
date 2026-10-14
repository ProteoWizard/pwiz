/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/resources/py/v2/ai.py
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

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// Hyperparameters for one fine-tuning run, with Carafe's defaults (MS2: 20 epochs, batch
    /// 512; RT: 40 epochs, batch 1024; both warm up for 10 epochs at learning rate 1e-4).
    /// </summary>
    public sealed class FineTuneSettings
    {
        /// <summary>Carafe adjusts the batch so every epoch takes at least this many steps.</summary>
        public const int MIN_STEPS_PER_EPOCH = 40;
        public const int MIN_BATCH_SIZE = 32;

        public static FineTuneSettings Ms2Defaults()
        {
            return new FineTuneSettings { Epochs = 20, WarmupEpochs = 10, BatchSize = 512, LearningRate = 1e-4 };
        }

        public static FineTuneSettings RtDefaults()
        {
            return new FineTuneSettings { Epochs = 40, WarmupEpochs = 10, BatchSize = 1024, LearningRate = 1e-4 };
        }

        public int Epochs { get; set; }
        public int WarmupEpochs { get; set; }
        public int BatchSize { get; set; }
        public double LearningRate { get; set; }

        /// <summary>Carafe's <c>adjust_batch_size_for_steps</c>, on by default.</summary>
        public bool AdjustBatchSize { get; set; } = true;

        /// <summary>
        /// Carafe's <c>adjust_batch_size</c>: when <paramref name="trainCount"/> rows at the
        /// configured batch size give fewer than 40 steps, the largest power of two (at least 32)
        /// not above <c>ceil(trainCount / 40)</c>.
        /// </summary>
        public int EffectiveBatchSize(int trainCount)
        {
            if (!AdjustBatchSize || BatchSize <= 0)
                return BatchSize;
            int steps = (int)Math.Ceiling(trainCount / (double)BatchSize);
            if (steps >= MIN_STEPS_PER_EPOCH)
                return BatchSize;
            int adjusted = Math.Max(MIN_BATCH_SIZE, (int)Math.Ceiling(trainCount / (double)MIN_STEPS_PER_EPOCH));
            adjusted = 1 << (int)Math.Floor(Math.Log(adjusted, 2));
            return Math.Max(MIN_BATCH_SIZE, adjusted);
        }

        /// <summary>
        /// Carafe's learning-rate multiplier for an epoch: linear warmup from 0, then a
        /// half-cosine decay to 0 (<c>_get_cosine_schedule_with_warmup_lr_lambda</c>, stepped once
        /// per epoch, so epoch 0 trains at a learning rate of 0). A warmup longer than the run is
        /// halved, as Carafe does.
        /// </summary>
        public double LearningRateFactor(int epoch)
        {
            int warmup = WarmupEpochs > Epochs ? Epochs / 2 : WarmupEpochs;
            if (epoch < warmup)
                return epoch / (double)Math.Max(1, warmup);
            double progress = (epoch - warmup) / (double)Math.Max(1, Epochs - warmup);
            return Math.Max(0.0, 0.5 * (1.0 + Math.Cos(Math.PI * 0.5 * 2.0 * progress)));
        }
    }
}
