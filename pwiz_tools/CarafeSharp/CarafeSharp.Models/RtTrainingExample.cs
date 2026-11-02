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

using pwiz.CarafeSharp.Core;

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// One observed retention time to fine-tune the RT model on, as a fraction of the gradient
    /// (<c>rt / rt_max</c>), and, when the training runs were aligned (<c>-rt_align kde</c>), as the hydrophobic index
    /// its run's map gives it.
    /// </summary>
    public sealed class RtTrainingExample
    {
        public RtTrainingExample(PeptideForm peptide, double rtNorm, double hi = double.NaN)
        {
            Peptide = peptide;
            RtNorm = rtNorm;
            Hi = hi;
        }

        public PeptideForm Peptide { get; }

        public double RtNorm { get; }

        /// <summary>The hydrophobic index from the run's alignment map, or NaN when the runs were not aligned.</summary>
        public double Hi { get; }
    }
}
