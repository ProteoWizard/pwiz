/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// The one-command library build (<c>--fasta</c>): what Osprey's own options say about the
    /// library to predict before the search. The library-build parameters themselves - the
    /// digest, the modifications, the fragments, the acquisition - are CarafeSharp's
    /// declarations, which the executable interprets into CarafeSharp's settings; this holds only
    /// what Osprey decides. Off (no <c>--fasta</c>), a run searches the library <c>-l</c> names,
    /// as it always has.
    /// </summary>
    public sealed class LibraryBuildConfig
    {
        /// <summary>The protein FASTA to predict the library from (<c>--fasta</c>), or null for a run that searches a given library.</summary>
        public string FastaPath { get; set; }

        /// <summary>True when the run builds its library.</summary>
        public bool Enabled => FastaPath != null;

        /// <summary>
        /// The runs the models are tuned on before the library is predicted (<c>--training-input</c>);
        /// empty for no tuning. A run may be one of the inputs or not: one that is not is searched
        /// for the tuning only and is absent from the output.
        /// </summary>
        public List<string> TrainingInputs { get; } = new List<string>();

        /// <summary>True when the run tunes the models on <see cref="TrainingInputs"/> first.</summary>
        public bool Tuning => TrainingInputs.Count > 0;

        /// <summary>A saved fine-tuned model to predict with (<c>--model</c>), or null for the pretrained models.</summary>
        public string ModelPath { get; set; }

        /// <summary>Where the library is predicted (<c>--device</c>: gpu or cpu), or null for CarafeSharp's default.</summary>
        public string Device { get; set; }

        /// <summary>Add entrapment peptides to the library (<c>--entrapment</c>).</summary>
        public bool Entrapment { get; set; }

        /// <summary>The fraction of targets that carry an entrapment peptide, or null for all of them.</summary>
        public double? EntrapmentRatio { get; set; }

        /// <summary>A foreign-species FASTA to draw entrapment peptides from (<c>--entrapment-fasta</c>), or null to shuffle.</summary>
        public string EntrapmentFastaPath { get; set; }

        /// <summary>
        /// The library-build options given on a command line with no <c>--fasta</c>, as typed, for
        /// the one refusal that names them: without the FASTA they would change nothing.
        /// </summary>
        public List<string> OptionsWithoutFasta { get; } = new List<string>();

        /// <summary>The tuning options given without <c>--training-input</c>, as typed: without the runs they would change nothing.</summary>
        public List<string> OptionsWithoutTrainingInput { get; } = new List<string>();
    }
}
