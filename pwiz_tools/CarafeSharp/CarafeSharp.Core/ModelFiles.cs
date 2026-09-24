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

namespace pwiz.CarafeSharp.Core
{
    /// <summary>
    /// The files of a fine-tuned model folder. Carafe writes the models as PyTorch
    /// checkpoints and CarafeSharp as safetensors; both write Carafe's metrics and training-run
    /// summary, so either folder predicts a library through <c>-model_dir</c>.
    /// </summary>
    public static class ModelFiles
    {
        /// <summary>Carafe's fine-tuned MS2 model (PyTorch state_dict).</summary>
        public const string MS2_CHECKPOINT = @"ms2_model.pt";

        /// <summary>Carafe's fine-tuned RT model (PyTorch state_dict).</summary>
        public const string RT_CHECKPOINT = @"rt_model.pt";

        /// <summary>CarafeSharp's fine-tuned MS2 model.</summary>
        public const string MS2_SAFETENSORS = @"ms2.safetensors";

        /// <summary>CarafeSharp's fine-tuned RT model.</summary>
        public const string RT_SAFETENSORS = @"rt.safetensors";

        /// <summary>Held-out scores before and after fine-tuning, and whether to predict with the fine-tuned MS2 model.</summary>
        public const string METRICS = @"model_evaluation_metrics.json";

        /// <summary>One entry per training run: NCE, instrument, rt_max, isolation and fragment m/z ranges.</summary>
        public const string META = @"meta.json";

        /// <summary>CarafeSharp's own record of a fine-tuning run (seed, pretrained model, row counts, epochs).</summary>
        public const string INFO = @"model.json";
    }
}
