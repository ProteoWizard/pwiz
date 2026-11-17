/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/resources/py/v2/models.py
 *   (Model_CCS_LSTM), itself extracted from AlphaPeptDeep (https://github.com/MannLabs/alphapeptdeep),
 *   Apache-2.0
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
    /// AlphaPeptDeep's <c>Model_CCS_LSTM</c>: predicts a precursor's collisional cross section
    /// (square angstroms) from its sequence, modifications and charge. The charge enters the
    /// encoder at every position and again beside the encoding before the decoder. Output is
    /// <c>[batch]</c>.
    /// </summary>
    internal sealed class ModelCcsLstm : nn.Module<Tensor, Tensor, Tensor, Tensor>
    {
        public const int HIDDEN = 256;
        private const int NUM_LSTM_LAYERS = 2;

        [ComponentName(Name = @"dropout")]
        private readonly Dropout _dropout;
        [ComponentName(Name = @"ccs_encoder")]
        private readonly EncoderAaModChargeCnnLstmAttnSum _ccsEncoder;
        [ComponentName(Name = @"ccs_decoder")]
        private readonly DecoderLinear _ccsDecoder;

        public ModelCcsLstm(double dropout = 0.1)
            : base(nameof(ModelCcsLstm))
        {
            _dropout = nn.Dropout(dropout);
            _ccsEncoder = new EncoderAaModChargeCnnLstmAttnSum(HIDDEN, NUM_LSTM_LAYERS);
            _ccsDecoder = new DecoderLinear(HIDDEN + 1, 1);
            RegisterComponents();
        }

        public override Tensor forward(Tensor aaIndices, Tensor modX, Tensor charges)
        {
            var x = _dropout.call(_ccsEncoder.call(aaIndices, modX, charges));
            x = cat(new[] { x, charges }, 1);
            return _ccsDecoder.call(x).squeeze(1);
        }
    }
}
