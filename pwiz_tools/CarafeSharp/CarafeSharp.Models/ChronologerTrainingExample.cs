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

using System.Collections.Generic;
using System.Linq;

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// An RT training example with its Chronologer tokens, for fine-tuning Chronologer, and the value it trains toward
    /// (<see cref="Target"/>): its normalized RT, unless set on another scale.
    /// </summary>
    public sealed class ChronologerTrainingExample
    {
        /// <summary>The examples the model's encoding accepts, each with its tokens, in input order.</summary>
        public static IReadOnlyList<ChronologerTrainingExample> Encode(ChronologerModel model, IEnumerable<RtTrainingExample> examples)
        {
            return examples.Select(e => (Example: e, Tokens: model.Encoding.Encode(e.Peptide)))
                .Where(e => e.Tokens != null)
                .Select(e => new ChronologerTrainingExample(e.Example, e.Tokens, e.Example.RtNorm))
                .ToArray();
        }

        private ChronologerTrainingExample(RtTrainingExample example, long[] tokens, double target)
        {
            Example = example;
            Tokens = tokens;
            Target = target;
        }

        public RtTrainingExample Example { get; }

        public long[] Tokens { get; }

        public double RtNorm
        {
            get { return Example.RtNorm; }
        }

        /// <summary>What the model's output trains toward: <see cref="RtNorm"/>, or the value <see cref="WithTarget"/> set.</summary>
        public double Target { get; }

        /// <summary>The same example training toward <paramref name="target"/>, on the scale the model's output is in.</summary>
        public ChronologerTrainingExample WithTarget(double target)
        {
            return new ChronologerTrainingExample(Example, Tokens, target);
        }
    }
}
