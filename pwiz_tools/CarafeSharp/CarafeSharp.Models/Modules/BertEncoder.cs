/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on HuggingFace transformers 4.47.0 models/bert/modeling_bert.py
 *   (https://github.com/huggingface/transformers), Apache-2.0, as configured by
 *   AlphaPeptDeep's _Pseudo_Bert_Config (https://github.com/MannLabs/alphapeptdeep)
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
using TorchSharp.Modules;
using TorchSharp.Utils;
using static TorchSharp.torch;

namespace pwiz.CarafeSharp.Models.Modules
{
    /// <summary>
    /// HuggingFace <c>BertEncoder</c> exactly as AlphaPeptDeep configures it: exact-erf GELU,
    /// LayerNorm epsilon 1e-8, eager attention and no attention mask, so every position
    /// (including the two terminal pad tokens) attends to every other. Submodule names match
    /// the HuggingFace state_dict keys (<c>layer.0.attention.self.query.weight</c>, ...).
    /// </summary>
    internal sealed class BertEncoder : nn.Module<Tensor, Tensor>
    {
        public const double LAYER_NORM_EPS = 1e-8;

        [ComponentName(Name = @"layer")]
        private readonly ModuleList<BertLayer> _layers;

        public BertEncoder(int hidden, int intermediate, int numHeads, int numLayers, double dropout)
            : base(nameof(BertEncoder))
        {
            var layers = new BertLayer[numLayers];
            for (int i = 0; i < numLayers; i++)
                layers[i] = new BertLayer(hidden, intermediate, numHeads, dropout);
            _layers = nn.ModuleList(layers);
            RegisterComponents();
        }

        public override Tensor forward(Tensor hidden)
        {
            foreach (var layer in _layers)
                hidden = layer.call(hidden);
            return hidden;
        }
    }

    /// <summary>One transformer block: self-attention, then the feed-forward sublayer.</summary>
    internal sealed class BertLayer : nn.Module<Tensor, Tensor>
    {
        [ComponentName(Name = @"attention")]
        private readonly BertAttention _attention;
        [ComponentName(Name = @"intermediate")]
        private readonly BertIntermediate _intermediate;
        [ComponentName(Name = @"output")]
        private readonly BertResidualOutput _output;

        public BertLayer(int hidden, int intermediate, int numHeads, double dropout)
            : base(nameof(BertLayer))
        {
            _attention = new BertAttention(hidden, numHeads, dropout);
            _intermediate = new BertIntermediate(hidden, intermediate);
            _output = new BertResidualOutput(intermediate, hidden, dropout);
            RegisterComponents();
        }

        public override Tensor forward(Tensor hidden)
        {
            var attentionOutput = _attention.call(hidden);
            var intermediateOutput = _intermediate.call(attentionOutput);
            return _output.call(intermediateOutput, attentionOutput);
        }
    }

    internal sealed class BertAttention : nn.Module<Tensor, Tensor>
    {
        [ComponentName(Name = @"self")]
        private readonly BertSelfAttention _self;
        [ComponentName(Name = @"output")]
        private readonly BertResidualOutput _output;

        public BertAttention(int hidden, int numHeads, double dropout)
            : base(nameof(BertAttention))
        {
            _self = new BertSelfAttention(hidden, numHeads, dropout);
            _output = new BertResidualOutput(hidden, hidden, dropout);
            RegisterComponents();
        }

        public override Tensor forward(Tensor hidden)
        {
            return _output.call(_self.call(hidden), hidden);
        }
    }

    /// <summary>
    /// Eager multi-head scaled dot-product attention with no mask, computed in the same
    /// operation order as HuggingFace <c>BertSelfAttention</c>: <c>(q @ k^T) / sqrt(d)</c>,
    /// softmax, dropout, <c>@ v</c>.
    /// </summary>
    internal sealed class BertSelfAttention : nn.Module<Tensor, Tensor>
    {
        private readonly int _numHeads;
        private readonly int _headSize;
        private readonly double _scale;
        [ComponentName(Name = @"query")]
        private readonly Linear _query;
        [ComponentName(Name = @"key")]
        private readonly Linear _key;
        [ComponentName(Name = @"value")]
        private readonly Linear _value;
        [ComponentName(Name = @"dropout")]
        private readonly Dropout _dropout;

        public BertSelfAttention(int hidden, int numHeads, double dropout)
            : base(nameof(BertSelfAttention))
        {
            if (hidden % numHeads != 0)
                throw new ArgumentException(string.Format(@"Hidden size {0} is not a multiple of {1} heads.", hidden, numHeads));
            _numHeads = numHeads;
            _headSize = hidden / numHeads;
            _scale = Math.Sqrt(_headSize);
            _query = nn.Linear(hidden, hidden);
            _key = nn.Linear(hidden, hidden);
            _value = nn.Linear(hidden, hidden);
            _dropout = nn.Dropout(dropout);
            RegisterComponents();
        }

        public override Tensor forward(Tensor hidden)
        {
            long batch = hidden.shape[0];
            long length = hidden.shape[1];
            var query = SplitHeads(_query.call(hidden), batch, length);
            var key = SplitHeads(_key.call(hidden), batch, length);
            var value = SplitHeads(_value.call(hidden), batch, length);
            var scores = query.matmul(key.transpose(-1, -2)) / _scale;
            var probs = _dropout.call(nn.functional.softmax(scores, -1));
            var context = probs.matmul(value).permute(0, 2, 1, 3).contiguous();
            return context.view(batch, length, _numHeads * _headSize);
        }

        private Tensor SplitHeads(Tensor x, long batch, long length)
        {
            return x.view(batch, length, _numHeads, _headSize).permute(0, 2, 1, 3);
        }
    }

    /// <summary>
    /// The dense, dropout, residual-add, LayerNorm tail. HuggingFace has two classes for it,
    /// <c>BertSelfOutput</c> (after attention) and <c>BertOutput</c> (after the feed-forward
    /// expansion), which differ only in the dense layer's input width and register the same
    /// three submodule names.
    /// </summary>
    internal sealed class BertResidualOutput : nn.Module<Tensor, Tensor, Tensor>
    {
        [ComponentName(Name = @"dense")]
        private readonly Linear _dense;
        [ComponentName(Name = @"LayerNorm")]
        private readonly LayerNorm _layerNorm;
        [ComponentName(Name = @"dropout")]
        private readonly Dropout _dropout;

        public BertResidualOutput(int inFeatures, int hidden, double dropout)
            : base(nameof(BertResidualOutput))
        {
            _dense = nn.Linear(inFeatures, hidden);
            _layerNorm = nn.LayerNorm(new long[] { hidden }, BertEncoder.LAYER_NORM_EPS);
            _dropout = nn.Dropout(dropout);
            RegisterComponents();
        }

        public override Tensor forward(Tensor hidden, Tensor residual)
        {
            return _layerNorm.call(_dropout.call(_dense.call(hidden)) + residual);
        }
    }

    internal sealed class BertIntermediate : nn.Module<Tensor, Tensor>
    {
        [ComponentName(Name = @"dense")]
        private readonly Linear _dense;

        public BertIntermediate(int hidden, int intermediate)
            : base(nameof(BertIntermediate))
        {
            _dense = nn.Linear(hidden, intermediate);
            RegisterComponents();
        }

        public override Tensor forward(Tensor hidden)
        {
            // HuggingFace "gelu" is the exact erf form, TorchSharp's default.
            return nn.functional.gelu(_dense.call(hidden));
        }
    }
}
