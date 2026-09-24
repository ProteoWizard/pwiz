/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/resources/py/v2/models.py,
 *   itself extracted from AlphaPeptDeep peptdeep/model/building_block.py
 *   (https://github.com/MannLabs/alphapeptdeep), Apache-2.0
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

// The AlphaPeptDeep building blocks, one C# class per Python class, kept together as they are
// in peptdeep's building_block.py. Every submodule is registered under the attribute name the
// Python class uses, because those names are the pretrained state_dict keys.
namespace pwiz.CarafeSharp.Models.Modules
{
    /// <summary>
    /// Sinusoidal positional encoding with base <c>max_len</c> (200), not the usual 10000. The
    /// table is a persistent buffer named <c>pe</c> and is overwritten by the checkpoint.
    /// </summary>
    internal sealed class PositionalEncoding : nn.Module<Tensor, Tensor>
    {
        [ComponentName(Name = @"pe")]
        private readonly Tensor _pe;

        public PositionalEncoding(int outFeatures, int maxLength)
            : base(nameof(PositionalEncoding))
        {
            using (no_grad())
            {
                var position = arange(maxLength).unsqueeze(1);
                var divTerm = exp(arange(0, outFeatures, 2) * (-Math.Log(maxLength) / outFeatures));
                _pe = zeros(1, maxLength, outFeatures);
                _pe[0, TensorIndex.Colon, TensorIndex.Slice(0, null, 2)] = sin(position * divTerm);
                _pe[0, TensorIndex.Colon, TensorIndex.Slice(1, null, 2)] = cos(position * divTerm);
            }
            RegisterComponents();
        }

        public override Tensor forward(Tensor x)
        {
            return x + _pe[TensorIndex.Colon, TensorIndex.Slice(0, x.shape[1]), TensorIndex.Colon];
        }
    }

    /// <summary>
    /// Modification embedding: the first six element counts (C, H, N, O, P, S) pass through,
    /// the remaining 103 are projected to two dimensions by a bias-free linear layer.
    /// </summary>
    internal sealed class ModEmbeddingFixFirstK : nn.Module<Tensor, Tensor>
    {
        [ComponentName(Name = @"nn")]
        private readonly Linear _nn;

        public ModEmbeddingFixFirstK(int outFeatures)
            : base(nameof(ModEmbeddingFixFirstK))
        {
            _nn = nn.Linear(PeptdeepConstants.MOD_FEATURE_SIZE - PeptdeepConstants.MOD_FIX_FIRST_K,
                outFeatures - PeptdeepConstants.MOD_FIX_FIRST_K, hasBias: false);
            RegisterComponents();
        }

        public override Tensor forward(Tensor modX)
        {
            const int k = PeptdeepConstants.MOD_FIX_FIRST_K;
            var fixedPart = modX[TensorIndex.Colon, TensorIndex.Colon, TensorIndex.Slice(0, k)];
            var projected = _nn.call(modX[TensorIndex.Colon, TensorIndex.Colon, TensorIndex.Slice(k)]);
            return cat(new[] { fixedPart, projected }, 2);
        }
    }

    /// <summary>
    /// Precursor meta features: a linear map of the one-hot instrument and the scaled NCE,
    /// with the scaled charge appended.
    /// </summary>
    internal sealed class MetaEmbedding : nn.Module<Tensor, Tensor, Tensor, Tensor>
    {
        [ComponentName(Name = @"nn")]
        private readonly Linear _nn;

        public MetaEmbedding(int outFeatures)
            : base(nameof(MetaEmbedding))
        {
            _nn = nn.Linear(PeptdeepConstants.MAX_INSTRUMENT_NUM + 1, outFeatures - 1);
            RegisterComponents();
        }

        public override Tensor forward(Tensor charges, Tensor nces, Tensor instrumentIndices)
        {
            var instrument = nn.functional.one_hot(instrumentIndices, PeptdeepConstants.MAX_INSTRUMENT_NUM).to_type(ScalarType.Float32);
            var meta = _nn.call(cat(new[] { instrument, nces }, 1));
            return cat(new[] { meta, charges }, 1);
        }
    }

    /// <summary>Amino acid embedding plus modification embedding, then positional encoding.</summary>
    internal sealed class InputAaModPositionalEncoding : nn.Module<Tensor, Tensor, Tensor>
    {
        [ComponentName(Name = @"mod_nn")]
        private readonly ModEmbeddingFixFirstK _modNn;
        [ComponentName(Name = @"aa_emb")]
        private readonly Embedding _aaEmb;
        [ComponentName(Name = @"pos_encoder")]
        private readonly PositionalEncoding _posEncoder;

        public InputAaModPositionalEncoding(int outFeatures)
            : base(nameof(InputAaModPositionalEncoding))
        {
            _modNn = new ModEmbeddingFixFirstK(PeptdeepConstants.MOD_HIDDEN);
            _aaEmb = nn.Embedding(PeptdeepConstants.AA_EMBEDDING_SIZE, outFeatures - PeptdeepConstants.MOD_HIDDEN, padding_idx: 0);
            _posEncoder = new PositionalEncoding(outFeatures, PeptdeepConstants.MAX_SEQUENCE_LENGTH);
            RegisterComponents();
        }

        public override Tensor forward(Tensor aaIndices, Tensor modX)
        {
            var mod = _modNn.call(modX);
            var aa = _aaEmb.call(aaIndices);
            return _posEncoder.call(cat(new[] { aa, mod }, 2));
        }
    }

    /// <summary>AlphaPeptDeep's <c>Hidden_HFace_Transformer</c>: a BERT encoder under <c>bert</c>.</summary>
    internal sealed class HiddenHFaceTransformer : nn.Module<Tensor, Tensor>
    {
        private const int HIDDEN_EXPAND = 4;
        private const int NUM_HEADS = 8;

        [ComponentName(Name = @"bert")]
        private readonly BertEncoder _bert;

        public HiddenHFaceTransformer(int hidden, int numLayers, double dropout)
            : base(nameof(HiddenHFaceTransformer))
        {
            _bert = new BertEncoder(hidden, hidden * HIDDEN_EXPAND, NUM_HEADS, numLayers, dropout);
            RegisterComponents();
        }

        public override Tensor forward(Tensor x)
        {
            return _bert.call(x);
        }
    }

    /// <summary>Linear(in, 64), PReLU, Linear(64, out), registered as <c>nn.0/1/2</c>.</summary>
    internal sealed class DecoderLinear : nn.Module<Tensor, Tensor>
    {
        private const int HIDDEN = 64;

        [ComponentName(Name = @"nn")]
        private readonly Sequential _nn;

        public DecoderLinear(int inFeatures, int outFeatures)
            : base(nameof(DecoderLinear))
        {
            _nn = nn.Sequential(
                (@"0", nn.Linear(inFeatures, HIDDEN)),
                (@"1", nn.PReLU(1)),
                (@"2", nn.Linear(HIDDEN, outFeatures)));
            RegisterComponents();
        }

        public override Tensor forward(Tensor x)
        {
            return _nn.call(x);
        }
    }

    /// <summary>
    /// Three same-width 1-D convolutions (kernels 3, 5, 7) concatenated with their input, so
    /// the channel count quadruples.
    /// </summary>
    internal sealed class SeqCnn : nn.Module<Tensor, Tensor>
    {
        [ComponentName(Name = @"cnn_short")]
        private readonly Conv1d _cnnShort;
        [ComponentName(Name = @"cnn_medium")]
        private readonly Conv1d _cnnMedium;
        [ComponentName(Name = @"cnn_long")]
        private readonly Conv1d _cnnLong;

        public SeqCnn(int channels)
            : base(nameof(SeqCnn))
        {
            _cnnShort = nn.Conv1d(channels, channels, 3, padding: 1);
            _cnnMedium = nn.Conv1d(channels, channels, 5, padding: 2);
            _cnnLong = nn.Conv1d(channels, channels, 7, padding: 3);
            RegisterComponents();
        }

        public override Tensor forward(Tensor x)
        {
            x = x.transpose(1, 2);
            var shortX = _cnnShort.call(x);
            var mediumX = _cnnMedium.call(x);
            var longX = _cnnLong.call(x);
            return cat(new[] { x, shortX, mediumX, longX }, 1).transpose(1, 2);
        }
    }

    /// <summary>
    /// Bidirectional batch-first LSTM whose initial hidden and cell states are frozen
    /// parameters (<c>rnn_h0</c>, <c>rnn_c0</c>). They are not zero in the pretrained weights,
    /// so they must be loaded.
    /// </summary>
    internal sealed class SeqLstm : nn.Module<Tensor, Tensor>
    {
        [ComponentName(Name = @"rnn_h0")]
        private readonly Parameter _rnnH0;
        [ComponentName(Name = @"rnn_c0")]
        private readonly Parameter _rnnC0;
        [ComponentName(Name = @"rnn")]
        private readonly LSTM _rnn;

        public SeqLstm(int inFeatures, int outFeatures, int numLayers)
            : base(nameof(SeqLstm))
        {
            if (outFeatures % 2 != 0)
                throw new ArgumentException(string.Format(@"Bidirectional LSTM width {0} is odd.", outFeatures));
            int hidden = outFeatures / 2;
            _rnnH0 = new Parameter(zeros(numLayers * 2, 1, hidden), requires_grad: false);
            _rnnC0 = new Parameter(zeros(numLayers * 2, 1, hidden), requires_grad: false);
            _rnn = nn.LSTM(inFeatures, hidden, numLayers, batchFirst: true, bidirectional: true);
            RegisterComponents();
        }

        public override Tensor forward(Tensor x)
        {
            long batch = x.shape[0];
            var h0 = _rnnH0.expand(-1, batch, -1).contiguous();
            var c0 = _rnnC0.expand(-1, batch, -1).contiguous();
            _rnn.flatten_parameters();
            var (output, _, _) = _rnn.call(x, (h0, c0));
            return output;
        }
    }

    /// <summary>Softmax-weighted sum over sequence positions.</summary>
    internal sealed class SeqAttentionSum : nn.Module<Tensor, Tensor>
    {
        [ComponentName(Name = @"attn")]
        private readonly Sequential _attn;

        public SeqAttentionSum(int inFeatures)
            : base(nameof(SeqAttentionSum))
        {
            _attn = nn.Sequential(
                (@"0", nn.Linear(inFeatures, 1, hasBias: false)),
                (@"1", nn.Softmax(1)));
            RegisterComponents();
        }

        public override Tensor forward(Tensor x)
        {
            var weights = _attn.call(x);
            return sum(mul(x, weights), 1);
        }
    }

    /// <summary>
    /// The RT encoder: one-hot residues plus the modification embedding, through
    /// <see cref="SeqCnn"/>, a 2-layer <see cref="SeqLstm"/> and <see cref="SeqAttentionSum"/>.
    /// </summary>
    internal sealed class EncoderAaModCnnLstmAttnSum : nn.Module<Tensor, Tensor, Tensor>
    {
        [ComponentName(Name = @"mod_nn")]
        private readonly ModEmbeddingFixFirstK _modNn;
        [ComponentName(Name = @"input_cnn")]
        private readonly SeqCnn _inputCnn;
        [ComponentName(Name = @"hidden_nn")]
        private readonly SeqLstm _hiddenNn;
        [ComponentName(Name = @"attn_sum")]
        private readonly SeqAttentionSum _attnSum;

        public EncoderAaModCnnLstmAttnSum(int outFeatures, int numLstmLayers)
            : base(nameof(EncoderAaModCnnLstmAttnSum))
        {
            const int inputDim = PeptdeepConstants.AA_EMBEDDING_SIZE + PeptdeepConstants.MOD_HIDDEN;
            _modNn = new ModEmbeddingFixFirstK(PeptdeepConstants.MOD_HIDDEN);
            _inputCnn = new SeqCnn(inputDim);
            _hiddenNn = new SeqLstm(inputDim * 4, outFeatures, numLstmLayers);
            _attnSum = new SeqAttentionSum(outFeatures);
            RegisterComponents();
        }

        public override Tensor forward(Tensor aaIndices, Tensor modX)
        {
            var mod = _modNn.call(modX);
            // One-hot is int64; peptdeep's torch.cat promotes it to float32, so cast here.
            var aa = nn.functional.one_hot(aaIndices, PeptdeepConstants.AA_EMBEDDING_SIZE).to_type(ScalarType.Float32);
            var x = _inputCnn.call(cat(new[] { aa, mod }, 2));
            x = _hiddenNn.call(x);
            return _attnSum.call(x);
        }
    }
}
