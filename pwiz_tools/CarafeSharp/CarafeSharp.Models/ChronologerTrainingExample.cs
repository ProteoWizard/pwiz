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
    /// <summary>An RT training example with its Chronologer tokens, for fine-tuning Chronologer.</summary>
    public sealed class ChronologerTrainingExample
    {
        /// <summary>The examples the model's encoding accepts, each with its tokens, in input order.</summary>
        public static IReadOnlyList<ChronologerTrainingExample> Encode(ChronologerModel model, IEnumerable<RtTrainingExample> examples)
        {
            return examples.Select(e => (Example: e, Tokens: model.Encoding.Encode(e.Peptide)))
                .Where(e => e.Tokens != null)
                .Select(e => new ChronologerTrainingExample(e.Example, e.Tokens))
                .ToArray();
        }

        private ChronologerTrainingExample(RtTrainingExample example, long[] tokens)
        {
            Example = example;
            Tokens = tokens;
        }

        public RtTrainingExample Example { get; }

        public long[] Tokens { get; }

        public double RtNorm
        {
            get { return Example.RtNorm; }
        }

        public int Length
        {
            get { return Example.Peptide.Length; }
        }
    }
}
