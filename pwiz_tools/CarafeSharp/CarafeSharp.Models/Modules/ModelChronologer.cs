/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Chronologer (https://github.com/searlelab/chronologer) src/chronologer/model.py and
 *   src/chronologer/chronologer_utils/core_layers.py, Apache-2.0
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
using System.Globalization;
using System.Linq;
using TorchSharp.Modules;
using TorchSharp.Utils;
using static TorchSharp.torch;

namespace pwiz.CarafeSharp.Models.Modules
{
    /// <summary>
    /// Chronologer's <c>chronologer_model</c>: predicts a hydrophobic index from a peptide encoded as
    /// <see cref="VECTOR_LENGTH"/> tokens (<see cref="ChronologerEncoding"/>). Output is <c>[batch]</c>.
    /// </summary>
    internal sealed class ModelChronologer : nn.Module<Tensor, Tensor>
    {
        /// <summary>Up to 50 residues plus the N- and C-terminal tokens.</summary>
        public const int VECTOR_LENGTH = 52;
        /// <summary>The 54 tokens of the vocabulary plus padding (0).</summary>
        public const int TOKEN_COUNT = 55;
        public const int EMBED_DIMENSION = 64;
        private const int RESNET_BLOCKS = 3;
        private const int KERNEL_SIZE = 7;

        [ComponentName(Name = @"seq_embed")]
        private readonly Embedding _seqEmbed;
        [ComponentName(Name = @"resnet_blocks")]
        private readonly Sequential _resnetBlocks;
        [ComponentName(Name = @"dropout")]
        private readonly Dropout _dropout;
        [ComponentName(Name = @"output")]
        private readonly Linear _output;

        public ModelChronologer(double dropout = 0.1)
            : base(nameof(ModelChronologer))
        {
            _seqEmbed = nn.Embedding(TOKEN_COUNT, EMBED_DIMENSION, padding_idx: 0);
            // Block d has dilation d + 1, as Chronologer builds them.
            _resnetBlocks = nn.Sequential(Enumerable.Range(0, RESNET_BLOCKS)
                .Select(d => (d.ToString(CultureInfo.InvariantCulture),
                    (nn.Module<Tensor, Tensor>)new ChronologerResnetBlock(EMBED_DIMENSION, KERNEL_SIZE, d + 1)))
                .ToArray());
            _dropout = nn.Dropout(dropout);
            _output = nn.Linear(VECTOR_LENGTH * EMBED_DIMENSION, 1);
            RegisterComponents();
        }

        /// <summary>
        /// Keep the BatchNorm layers on their running statistics while the rest trains. Fine-tuning batches hold
        /// one peptide length each and can be small, so their own statistics would be noisy, and a few thousand
        /// peptides of one run should not move statistics learned from a large multi-source database.
        /// </summary>
        public bool FreezeBatchNorm { get; set; }

        public override void train(bool train = true)
        {
            base.train(train);
            if (train && FreezeBatchNorm)
            {
                foreach (var batchNorm in modules().OfType<BatchNorm1d>())
                    batchNorm.eval();
            }
        }

        public override Tensor forward(Tensor tokens)
        {
            // [batch, positions, channels] -> [batch, channels, positions] for the convolutions; the
            // flatten is then channel-major, as in Python.
            var x = _seqEmbed.call(tokens).transpose(1, -1);
            x = _dropout.call(_resnetBlocks.call(x));
            return _output.call(x.flatten(1)).squeeze(1);
        }

        /// <summary>Folds <c>y = slope * x + intercept</c> into the output layer, so the network predicts y.</summary>
        public void ScaleOutput(double slope, double intercept)
        {
            var bias = _output.bias ?? throw new InvalidOperationException(@"Chronologer's output layer has no bias.");
            using (no_grad())
            {
                _output.weight.mul_(slope);
                bias.mul_(slope).add_(intercept);
            }
        }
    }

    /// <summary>
    /// Chronologer's <c>resnet_block</c> at equal input and output widths: a 1x1 and a kernel-k
    /// convolution unit (each Conv1d, BatchNorm1d, ReLU), the input added back, then ReLU. The shortcut
    /// convolution exists only because the checkpoint carries its weights: Python uses it only when the
    /// widths differ, which they never do here.
    /// </summary>
    internal sealed class ChronologerResnetBlock : nn.Module<Tensor, Tensor>
    {
        [ComponentName(Name = @"process_blocks")]
        private readonly Sequential _processBlocks;
        [ComponentName(Name = @"activate")]
        private readonly ReLU _activate;
        // Registered only so the checkpoint's shortcut keys load.
        // ReSharper disable once NotAccessedField.Local
        [ComponentName(Name = @"shortcut")]
        private readonly Sequential _shortcut;

        public ChronologerResnetBlock(int channels, int kernel, int dilation)
            : base(nameof(ChronologerResnetBlock))
        {
            _processBlocks = nn.Sequential(
                (@"0", ResnetUnit(channels, 1, dilation)),
                (@"1", ResnetUnit(channels, kernel, dilation)));
            _activate = nn.ReLU();
            _shortcut = ConvolutionUnit(channels, 1, dilation);
            RegisterComponents();
        }

        public override Tensor forward(Tensor x)
        {
            return _activate.call(_processBlocks.call(x) + x);
        }

        private static Sequential ResnetUnit(int channels, int kernel, int dilation)
        {
            return nn.Sequential(
                (@"0", ConvolutionUnit(channels, kernel, dilation)),
                (@"1", nn.ReLU()));
        }

        /// <summary>
        /// Conv1d and BatchNorm1d. Python's <c>padding='same'</c> on an odd kernel pads each side by
        /// <c>dilation * (kernel - 1) / 2</c>.
        /// </summary>
        private static Sequential ConvolutionUnit(int channels, int kernel, int dilation)
        {
            return nn.Sequential(
                (@"0", nn.Conv1d(channels, channels, kernel, padding: dilation * (kernel - 1) / 2, dilation: dilation)),
                (@"1", nn.BatchNorm1d(channels)));
        }
    }
}
