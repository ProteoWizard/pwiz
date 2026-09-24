/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/resources/py/v2/models.py,
 *   itself extracted from AlphaPeptDeep peptdeep/model/ms2.py
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

using TorchSharp.Modules;
using TorchSharp.Utils;
using static TorchSharp.torch;

namespace pwiz.CarafeSharp.Models.Modules
{
    /// <summary>
    /// AlphaPeptDeep's <c>ModelMS2Bert</c>: the BERT fragment-intensity model. Output is
    /// <c>[batch, nAA - 1, 8]</c> raw (unnormalized, unclipped) intensities over
    /// b_z1, b_z2, y_z1, y_z2 and their four modloss counterparts; row r is b(r+1) and
    /// y(nAA-1-r).
    ///
    /// The modloss branch (<c>modloss_nn</c>) is always built so the checkpoint's keys load,
    /// but with <see cref="MaskModLoss"/> (general and ubiquitin modes) its columns are exact
    /// zeros and it takes no part in the forward pass or in training.
    /// </summary>
    internal sealed class ModelMs2Bert : nn.Module<Tensor, Tensor, Tensor, Tensor, Tensor, Tensor>
    {
        public const int HIDDEN = 256;
        public const int NUM_LAYERS = 4;
        private const int META_DIM = 8;
        private const double RESIDUAL_SCALE = 0.2;

        /// <summary>Positions dropped from the front: N pad, first residue, and the offset to b1.</summary>
        private const int OUTPUT_OFFSET = 3;

        [ComponentName(Name = @"dropout")]
        private readonly Dropout _dropout;
        [ComponentName(Name = @"input_nn")]
        private readonly InputAaModPositionalEncoding _inputNn;
        [ComponentName(Name = @"meta_nn")]
        private readonly MetaEmbedding _metaNn;
        [ComponentName(Name = @"hidden_nn")]
        private readonly HiddenHFaceTransformer _hiddenNn;
        [ComponentName(Name = @"output_nn")]
        private readonly DecoderLinear _outputNn;
        // peptdeep's modloss_nn = ModuleList([Hidden_HFace_Transformer(1 layer), Decoder_Linear]).
        [ComponentName(Name = @"modloss_nn")]
        private readonly ModuleList<nn.Module<Tensor, Tensor>> _modlossNn;

        public ModelMs2Bert(bool maskModLoss, double dropout = 0.1)
            : base(nameof(ModelMs2Bert))
        {
            MaskModLoss = maskModLoss;
            _dropout = nn.Dropout(dropout);
            _inputNn = new InputAaModPositionalEncoding(HIDDEN - META_DIM);
            _metaNn = new MetaEmbedding(META_DIM);
            _hiddenNn = new HiddenHFaceTransformer(HIDDEN, NUM_LAYERS, dropout);
            _outputNn = new DecoderLinear(HIDDEN, PeptdeepConstants.NUM_NON_MODLOSS_FRAG_TYPES);
            _modlossNn = nn.ModuleList<nn.Module<Tensor, Tensor>>(
                new HiddenHFaceTransformer(HIDDEN, 1, dropout),
                new DecoderLinear(HIDDEN, PeptdeepConstants.NUM_MODLOSS_FRAG_TYPES));
            RegisterComponents();
        }

        /// <summary>True in general mode: the modloss columns are zeros.</summary>
        public bool MaskModLoss { get; }

        public override Tensor forward(Tensor aaIndices, Tensor modX, Tensor charges, Tensor nces, Tensor instrumentIndices)
        {
            var inX = _dropout.call(_inputNn.call(aaIndices, modX));
            var meta = _metaNn.call(charges, nces, instrumentIndices).unsqueeze(1).repeat(1, inX.shape[1], 1);
            inX = cat(new[] { inX, meta }, 2);

            var hiddenX = _dropout.call(_hiddenNn.call(inX) + inX * RESIDUAL_SCALE);
            var outX = _outputNn.call(hiddenX);

            Tensor modloss;
            if (MaskModLoss)
            {
                modloss = zeros(outX.shape[0], outX.shape[1], PeptdeepConstants.NUM_MODLOSS_FRAG_TYPES,
                    dtype: outX.dtype, device: outX.device);
            }
            else
            {
                modloss = _modlossNn[1].call(_modlossNn[0].call(inX) + hiddenX);
            }
            outX = cat(new[] { outX, modloss }, 2);
            return outX[TensorIndex.Colon, TensorIndex.Slice(OUTPUT_OFFSET), TensorIndex.Colon];
        }
    }
}
