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
    /// <c>-rt_model</c> (CarafeSharp only): the retention-time model library prediction and fine-tuning use.
    /// <see cref="alphapeptdeep"/> is Carafe's; <see cref="chronologer"/> is Chronologer, CarafeSharp's default,
    /// which tracks the end of the gradient where AlphaPeptDeep's generic model plateaus. Pretrained,
    /// Chronologer's library RT is iRT; fine-tuned, it predicts the run's normalized RT, as a fine-tuned
    /// AlphaPeptDeep model does.
    /// </summary>
    public enum RtModelType
    {
        alphapeptdeep,
        chronologer,
    }
}
