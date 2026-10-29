/*
 * Author: David Shteynberg <dshteyn .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.IO;
using pwiz.CarafeSharp.Proteome;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib.AlphaPeptDeep
{
    /// <summary>
    /// Builds a library of Carafe's MS2, RT and CCS predictions (the pretrained AlphaPeptDeep models,
    /// or a fine-tuned model) for the document's precursors. The precursors go to Osprey as a table
    /// (the one Skyline once wrote for peptdeep), and Osprey's PredictLibrary phase (CarafeSharp's
    /// TorchSharp port of peptdeep, in Osprey's own process) predicts them, on an NVIDIA GPU when the
    /// CUDA runtime is installed (see <see cref="TorchRuntimes"/>) and otherwise on the CPU, and
    /// writes the library itself, as it writes the libraries of its own searches: the .blib asked
    /// for, each precursor with its CCS, which Skyline converts to ion mobility with each data
    /// file's calibration.
    /// </summary>
    public class CarafeLibraryBuilder : AbstractDeepLibraryBuilder, IiRTCapableLibraryBuilder
    {
        public const string ALPHAPEPTDEEP = @"AlphaPeptDeep";

        // Processing folders
        private const string PREFIX_WORKDIR = "APD";
        // Osprey's working folder for a fine-tune, beside the APD folders and kept between builds
        private const string OSPREY_WORKDIR = @"osprey";

        // The precursor table Osprey predicts, and the fixed name a fine-tune's products are keyed on
        private const string INPUT_FILE_NAME = @"input.tsv";
        private const string OSPREY_OUTPUT_FILE_NAME = @"predict.blib";

        // Column names for AlphaPeptDeep
        private const string SEQUENCE = @"sequence";
        private const string MODS = @"mods";
        private const string MOD_SITES = @"mod_sites";
        private const string CHARGE = @"charge";

        private static readonly IEnumerable<string> PrecursorTableColumnNames = new[] { SEQUENCE, MODS, MOD_SITES, CHARGE };

        protected override string ToolName => ALPHAPEPTDEEP;

        private LibraryBuilderModificationSupport _modificationSupport;
        private string _carafeModelPath;
        private readonly string _rootProcessingDir;

        /// <summary>
        /// The pretrained models' support, extended by what the fine-tuned model, if one is given, was
        /// trained on.
        /// </summary>
        protected override LibraryBuilderModificationSupport LibraryBuilderModificationSupport
        {
            get { return _modificationSupport ??= GetModificationSupport(CarafeModelPath); }
        }

        /// <summary>
        /// The modifications the models support when predicting with the fine-tuned model file at
        /// <paramref name="carafeModelPath"/>, or the pretrained models' when it is null: the
        /// pretrained set plus the modifications the file says it was trained on. A file that cannot
        /// be read adds nothing; the build reports it.
        /// </summary>
        public static LibraryBuilderModificationSupport GetModificationSupport(string carafeModelPath)
        {
            if (string.IsNullOrEmpty(carafeModelPath))
                return MODEL_MODIFICATION_SUPPORT;
            try
            {
                var training = CarafeModelFile.ReadDescription(carafeModelPath).Training;
                if (training?.Modifications == null)
                    return MODEL_MODIFICATION_SUPPORT;
                return MODEL_MODIFICATION_SUPPORT.WithTrainingModifications(training.Modifications.Keys);
            }
            catch (Exception x)
            {
                if (ExceptionUtil.IsProgrammingDefect(x))
                    throw;
                return MODEL_MODIFICATION_SUPPORT;
            }
        }

        internal static List<ModificationType> MODEL_SUPPORTED_UNIMODS = new List<ModificationType>
        {
            GetUniModType(4, PredictionSupport.all), // Carbamidomethyl (C)
            GetUniModType(21, PredictionSupport.fragmentation), // Phospho
            GetUniModType(35, PredictionSupport.all), // Oxidation
            GetUniModType(121, PredictionSupport.fragmentation) // GlyGly (a.k.a. GG)
        };

        /// <summary>
        /// The pretrained models' modification support, for a form to ask about a document's
        /// modifications before a builder exists.
        /// </summary>
        public static readonly LibraryBuilderModificationSupport MODEL_MODIFICATION_SUPPORT =
            new LibraryBuilderModificationSupport(MODEL_SUPPORTED_UNIMODS);

        public LibrarySpec LibrarySpec { get; private set; }

        /// <summary>
        /// A model CarafeSharp fine-tuned on a set of runs and saved as a .model.zip file, to predict
        /// with in place of the pretrained models, or null for the pretrained models. It predicts for
        /// the collision energy, instrument, activation and analyzer it was trained on, with the kind of
        /// RT model it names.
        /// </summary>
        public string CarafeModelPath
        {
            get { return _carafeModelPath; }
            set
            {
                _carafeModelPath = value;
                _modificationSupport = null;
            }
        }

        /// <summary>
        /// The name the model list gives <see cref="CarafeModelPath"/>'s model, for the progress
        /// text; the pretrained models' name when none is given.
        /// </summary>
        public string CarafeModelName { get; set; }

        /// <summary>True when <paramref name="modelPath"/> names the model beside this library, <see cref="TunedModelPath"/>.</summary>
        private bool IsTunedModelPath(string modelPath)
        {
            return modelPath != null && string.Equals(Path.GetFullPath(modelPath), Path.GetFullPath(TunedModelPath),
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The libtorch runtimes Osprey predicts with: where the CUDA runtime is installed, passed to Osprey.
        /// </summary>
        public TorchRuntimes TorchRuntimes { get; set; } = new TorchRuntimes();

        /// <summary>
        /// Whether to predict on the GPU, which needs the CUDA runtime installed and is then a requirement
        /// of the run; otherwise prediction runs on the CPU.
        /// </summary>
        public bool UseGpu { get; set; }

        /// <summary>
        /// A fine-tune before the prediction: the FASTA the tuning runs are searched against, the
        /// runs, and the digest, modification and search settings of that search, as Osprey's one
        /// command takes them. Osprey tunes the pretrained models on the runs' results, predicts the
        /// precursor table with the tuned models, and the model is saved beside the library
        /// (<see cref="TunedModelPath"/>). Null predicts with the pretrained models or
        /// <see cref="CarafeModelPath"/> as they are.
        /// </summary>
        public OspreyConfig TuningConfig { get; set; }

        /// <summary>Whether this build fine-tunes a model first.</summary>
        public bool IsTuning => TuningConfig != null && TuningConfig.TrainingInputs.Count > 0;

        /// <summary>
        /// How the document's decoys were made (<see cref="LibraryDecoyPairs"/>'s Method), when the
        /// library is to carry its decoys paired with their targets in a DecoyPairs table; null
        /// writes no table unless there are <see cref="EntrapmentTargets"/>.
        /// </summary>
        public string DecoyMethod { get; set; }

        /// <summary>The entrapment peptides among the document's, flagged as such in the DecoyPairs table; null for none.</summary>
        public ICollection<Target> EntrapmentTargets { get; set; }

        /// <summary>Whether the built library gets a DecoyPairs table marking its decoys and entrapment peptides.</summary>
        public bool WritesDecoyPairs => DecoyMethod != null || (EntrapmentTargets != null && EntrapmentTargets.Count > 0);

        /// <summary>
        /// Osprey's working folder for the fine-tune: the tuning runs' spectra caches, the training
        /// library and its search, the model folder and the model file. Beside the per-build APD
        /// folders and kept between builds, so a library built again on the same runs finds the
        /// tuning current and does not tune again.
        /// </summary>
        public string OspreyWorkDirectory => Path.Combine(_rootProcessingDir, OSPREY_WORKDIR);

        /// <summary>The fine-tuned model the build saves beside the library (&lt;library&gt;.model.zip).</summary>
        public string TunedModelPath => OspreyHelpers.GetLibraryModelPath(LibrarySpec.FilePath);

        protected override IEnumerable<string> GetHeaderColumnNames(bool training)
        {
            return PrecursorTableColumnNames;
        }

        protected override string GetTableRow(PeptideDocNode peptide, ModifiedSequence modifiedSequence,
            int charge, bool training, string modsBuilder, string modSitesBuilder)
        {
            return new[] { modifiedSequence.GetUnmodifiedSequence(), modsBuilder, modSitesBuilder, charge.ToString() }
                .ToDsvLine(TextUtil.SEPARATOR_TSV);
        }

        public override string InputFilePath => Path.Combine(WorkDir, INPUT_FILE_NAME);
        public override string TrainingFilePath => null;

        /// <summary>
        /// The library Osprey writes: the library itself, or for a fine-tune a fixed name in Osprey's
        /// working folder, which the tuning's products (the model, the training library) are keyed
        /// on, so they stay current for the next build whatever the library is called.
        /// </summary>
        private string OspreyOutputPath => IsTuning
            ? Path.Combine(OspreyWorkDirectory, OSPREY_OUTPUT_FILE_NAME)
            : LibrarySpec.FilePath;

        /// <summary>
        /// Constructor for the Carafe library builder.
        /// </summary>
        /// <param name="libName">Name of the library to build.</param>
        /// <param name="libOutPath">Path to the blib final product.</param>
        /// <param name="document">Input document for building the library.</param>
        /// <param name="irtStandard">iRT peptide standard to include in the library.</param>
        public CarafeLibraryBuilder(string libName, string libOutPath,
            SrmDocument document, IrtStandard irtStandard) : base(document, irtStandard)
        {
            LibrarySpec = new BiblioSpecLiteSpec(libName, libOutPath);
            string rootProcessingDir = Path.GetDirectoryName(libOutPath);
            if (string.IsNullOrEmpty(rootProcessingDir))
                throw new ArgumentException($@"CarafeLibraryBuilder libOutputPath {libOutPath} must be a full path.");

            rootProcessingDir = Path.Combine(rootProcessingDir, Path.GetFileNameWithoutExtension(libOutPath));
            _rootProcessingDir = rootProcessingDir;
            EnsureWorkDir(rootProcessingDir, PREFIX_WORKDIR);
        }

        public bool BuildLibrary(IProgressMonitor progress)
        {
            IProgressStatus progressStatus = new ProgressStatus();

            try
            {
                RunPrediction(progress, ref progressStatus);
                if (progressStatus.IsCanceled)
                    return false;
                progress.UpdateProgress(progressStatus = progressStatus.Complete());
                return true;
            }
            catch (Exception exception)
            {
                progress.UpdateProgress(progressStatus.ChangeErrorException(exception));
                return false;
            }
        }

        private void RunPrediction(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            // Note: Segments are distributed to balance the expected work of each task
            // One end per step, including the last: NextSegment() never advances past the final end
            var segmentEndPercentages = new[] { 5, 95, 100 };
            progressStatus = progressStatus.ChangeSegments(0, ImmutableList<int>.ValueOf(segmentEndPercentages));
            PreparePrecursorInputFile(progress, ref progressStatus);
            progressStatus = progressStatus.NextSegment();
            ExecuteOsprey(progress, ref progressStatus);
            // ProcessRunner advanced the segment itself on Osprey's final 100% line (or at its exit),
            // and reported the cancel, if any, as the status
            if (progressStatus.IsCanceled)
                return;
            ImportSpectralLibrary(progress, ref progressStatus);
            if (WritesDecoyPairs)
                LibraryDecoyPairs.Write(LibrarySpec.FilePath, Document, EntrapmentTargets, DecoyMethod);
        }

        /// <summary>
        /// Predicts fragment intensities and retention times (iRT, or minutes with a fine-tuned RT model)
        /// for every precursor in the input file by running Osprey's PredictLibrary phase over it
        /// (<see cref="OspreyHelpers.BuildPredictLibraryCommandLine"/>), which writes the library to
        /// <see cref="OspreyOutputPath"/>. With <see cref="TuningConfig"/> the one command runs
        /// first: the training library from the FASTA, the tuning runs' search, the fine-tune, and then
        /// the table's prediction with the tuned models, whose task lines drive the bar through
        /// <see cref="OspreyProgressMonitor"/>. Osprey chooses the libtorch runtime in its own process,
        /// so a build on the GPU needs no restart of Skyline; its console lines are the status messages,
        /// and its output is teed to a log in the working folder.
        /// </summary>
        private void ExecuteOsprey(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progress.UpdateProgress(progressStatus = progressStatus.ChangeMessage(IsTuning
                ? ModelResources.CarafeLibraryBuilder_ExecuteOsprey_Fine_tuning_a_model_on_the_DIA_runs_and_predicting_the_library
                : string.Format(ModelResources.CarafeLibraryBuilder_ExecuteOsprey_Predicting_the_library_with_the__0__model,
                    CarafeModelName ?? CarafeModelSpec.DEFAULT.Name)));
            if (!File.Exists(OspreyHelpers.OspreyBinary))
            {
                throw new IOException(string.Format(ModelResources.CarafeLibraryBuilder_ExecuteOsprey_Osprey__which_predicts_the_library__is_not_installed_at__0_,
                    OspreyHelpers.OspreyBinary));
            }
            var timer = Stopwatch.StartNew();
            string args;
            var monitor = progress;
            if (IsTuning)
            {
                Directory.CreateDirectory(OspreyWorkDirectory);
                var config = GetTuningCommand();
                args = OspreyHelpers.BuildCommandLine(config);
                // The bar follows Osprey's [TASK] lines, not its NN% lines, which restart at every task
                monitor = new OspreyProgressMonitor(progress, config);
            }
            else
            {
                args = OspreyHelpers.BuildPredictLibraryCommandLine(InputFilePath, OspreyOutputPath, CarafeModelPath,
                    UseGpu, TorchRuntimes.RuntimesDirectory, LocalizationHelper.CurrentUICulture.Name);
            }
            try
            {
                OspreyHelpers.RunOsprey(args, WorkDir, monitor, ref progressStatus, !IsTuning);
            }
            catch (Exception ex)
            {
                throw new IOException(ModelResources.CarafeLibraryBuilder_PredictSpectralLibrary_Failed_to_predict_the_library_, ex);
            }
            if (progressStatus.IsCanceled)
                return;
            if (!File.Exists(OspreyOutputPath))
                throw new FileNotFoundException(ModelResources.CarafeLibraryBuilder_PredictSpectralLibrary_Failed_to_predict_the_library_, OspreyOutputPath);
            if (IsTuning)
            {
                // ProcessRunner advanced the segment itself only when it read the NN% lines
                progressStatus = progressStatus.NextSegment();
                CopyTunedModel();
            }
            else if (File.Exists(TunedModelPath) && !IsTunedModelPath(CarafeModelPath))
            {
                // The library is rebuilt without a tuning: the model an earlier build left beside
                // it did not predict this one, so it goes, as the library it belonged to does -
                // unless it is the saved model this build predicted with, which stays in the list
                FileEx.SafeDelete(TunedModelPath);
            }
            timer.Stop();
            Messages.WriteAsyncUserMessage(string.Format(ModelResources.CarafeLibraryBuilder_ExecuteOsprey_Predicted_the_library_in__0__minutes__1__seconds_,
                timer.Elapsed.Minutes, timer.Elapsed.Seconds));
        }

        /// <summary>
        /// The one command of the fine-tune: <see cref="TuningConfig"/>'s FASTA, runs and search
        /// settings, with what this build decides - the precursor table to predict, the output and
        /// working folder, the device and runtimes, and the language of Osprey's messages.
        /// </summary>
        private OspreyConfig GetTuningCommand()
        {
            // A copy: what the form handed in stays as it was
            var config = TuningConfig.Clone();
            config.PrecursorTablePath = InputFilePath;
            config.Task = null;
            config.OutputPath = OspreyOutputPath;
            config.WorkDirectory = OspreyWorkDirectory;
            config.ModelPath = CarafeModelPath;
            config.Device = UseGpu ? OspreyHelpers.DEVICE_GPU : OspreyHelpers.DEVICE_CPU;
            config.TorchRuntimesDirectory = TorchRuntimes.RuntimesDirectory;
            config.CultureName = LocalizationHelper.CurrentUICulture.Name;
            return config;
        }

        /// <summary>
        /// The model Osprey tuned, saved in its working folder as the output's stem, is copied beside
        /// the library as the library's model (<see cref="TunedModelPath"/>), where the user finds it
        /// and can add it to the model list by name.
        /// </summary>
        private void CopyTunedModel()
        {
            string modelPath = OspreyHelpers.GetTunedModelPath(OspreyOutputPath, OspreyWorkDirectory);
            if (!File.Exists(modelPath))
                throw new FileNotFoundException(string.Format(ModelResources.CarafeLibraryBuilder_CopyTunedModel_The_fine_tuned_model__0__was_not_written_, modelPath), modelPath);
            File.Copy(modelPath, TunedModelPath, true);
        }

        /// <summary>
        /// The library of a prediction alone was written to its path directly; a fine-tune's,
        /// predicted into Osprey's working folder beside the tuning's products, is copied there.
        /// </summary>
        private void ImportSpectralLibrary(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(ModelResources.CarafeLibraryBuilder_Importing_spectral_library));
            if (IsTuning)
                File.Copy(OspreyOutputPath, LibrarySpec.FilePath, true);
        }
    }
}
