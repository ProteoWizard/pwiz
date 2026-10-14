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

using pwiz.CarafeSharp.Models;
using pwiz.CarafeSharp.Proteome;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>Saved-model pieces the tests write models with.</summary>
    internal static class TestModels
    {
        /// <summary>The origin of a model fine-tuned from AlphaPeptDeep's pretrained model, for a saved model written in a test.</summary>
        public static CarafeModelOrigin PretrainedOrigin()
        {
            return new CarafeModelOrigin
            {
                Model = CarafeModelOrigin.ALPHAPEPTDEEP, Version = PretrainedModels.VERSION, Start = CarafeModelOrigin.START_PRETRAINED,
            };
        }
    }
}
