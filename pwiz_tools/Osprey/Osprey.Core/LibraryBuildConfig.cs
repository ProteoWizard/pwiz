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

using System;
using System.Collections.Generic;
using System.Linq;

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
        /// <summary>The library phase that predicts the library the tuning runs are searched with.</summary>
        public const string PREDICT_TRAINING_LIBRARY = @"PredictTrainingLibrary";

        /// <summary>The library phase that tunes the models on the tuning runs' training exports.</summary>
        public const string TRAIN_MODEL = @"TrainModel";

        /// <summary>The library phase that predicts the library the output is searched with.</summary>
        public const string PREDICT_LIBRARY = @"PredictLibrary";

        /// <summary>
        /// The library phases <c>--task</c> can select, in the order the one command runs them. They
        /// are not stages of the search pipeline: each is a single-node task of the workflow above it.
        /// </summary>
        public static readonly IReadOnlyList<string> PHASES = new[] { PREDICT_TRAINING_LIBRARY, TRAIN_MODEL, PREDICT_LIBRARY };

        /// <summary>
        /// The library phase named <paramref name="name"/> (case-insensitive, as <c>--task</c> matches
        /// the stages), in its canonical spelling, or null when no phase has that name.
        /// </summary>
        public static string FindPhase(string name)
        {
            return PHASES.FirstOrDefault(phase => string.Equals(phase, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The protein FASTA to predict the library from (<c>--fasta</c>), or null for a run that searches a given library.</summary>
        public string FastaPath { get; set; }

        /// <summary>
        /// The precursor table to predict a library for (<c>--precursors</c>): exactly its
        /// precursors, with no digest, written as the TSV peptdeep wrote, which Skyline's Build
        /// Library runs as the <see cref="PREDICT_LIBRARY"/> phase alone; or null.
        /// </summary>
        public string PrecursorTablePath { get; set; }

        /// <summary>True when the run builds its library, or predicts one for a precursor table.</summary>
        public bool Enabled => FastaPath != null || PrecursorTablePath != null;

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

        /// <summary>
        /// Where the library is predicted (<c>--device</c>: auto, gpu or cpu), or null for auto: the GPU
        /// when this computer has one that can use the NVIDIA CUDA runtime and the runtime is installed,
        /// else the CPU. The device the phases run on is settled when the runtime is loaded.
        /// </summary>
        public string Device { get; set; }

        /// <summary>
        /// The folder of installed libtorch runtimes (<c>--torch-runtimes</c>): where the CUDA runtime
        /// is downloaded to and looked for. Null for Osprey's own folder under the user's local
        /// application data; Skyline passes its Tools folder, so one download serves both.
        /// </summary>
        public string TorchRuntimesDirectory { get; set; }

        /// <summary>Download and install the NVIDIA CUDA runtime, then exit (<c>--install-cuda-runtime</c>).</summary>
        public bool InstallCudaRuntime { get; set; }

        /// <summary>Add entrapment peptides to the library (<c>--entrapment</c>).</summary>
        public bool Entrapment { get; set; }

        /// <summary>The fraction of targets that carry an entrapment peptide, or null for all of them.</summary>
        public double? EntrapmentRatio { get; set; }

        /// <summary>A foreign-species FASTA to draw entrapment peptides from (<c>--entrapment-fasta</c>), or null to shuffle.</summary>
        public string EntrapmentFastaPath { get; set; }

        /// <summary>
        /// The library phase this run is (<c>--task</c> naming one of <see cref="PHASES"/>), which it
        /// runs alone, or null for a run that is not one of them.
        /// </summary>
        public string Phase { get; set; }

        /// <summary>
        /// The stage task selected with <c>--task</c> is a stage of the tuning runs' search
        /// (<c>--training-search</c>), not of the output's.
        /// </summary>
        public bool TrainingSearch { get; set; }

        /// <summary>
        /// The library-build options given on a command line with no <c>--fasta</c>, as typed, for
        /// the one refusal that names them: without the FASTA they would change nothing.
        /// </summary>
        public List<string> OptionsWithoutFasta { get; } = new List<string>();

        /// <summary>The tuning options given without <c>--training-input</c>, as typed: without the runs they would change nothing.</summary>
        public List<string> OptionsWithoutTrainingInput { get; } = new List<string>();

        /// <summary>The Library Digest options given with <c>--precursors</c>, as typed: a table is predicted as it is, not digested.</summary>
        public List<string> DigestOptionsWithPrecursors { get; } = new List<string>();
    }
}
