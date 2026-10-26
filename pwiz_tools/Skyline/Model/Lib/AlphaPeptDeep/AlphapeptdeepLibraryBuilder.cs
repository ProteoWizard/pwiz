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
using System.Globalization;
using System.IO;
using System.Linq;
using pwiz.BiblioSpec;
using pwiz.CarafeSharp.Proteome;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib.AlphaPeptDeep
{
    /// <summary>
    /// Builds a library of AlphaPeptDeep MS2 and RT predictions for the document's precursors. The
    /// precursors go to Osprey as the table Skyline once wrote for peptdeep, and Osprey's PredictLibrary
    /// phase (CarafeSharp's TorchSharp port of peptdeep, in Osprey's own process) predicts them, on an
    /// NVIDIA GPU when the CUDA runtime is installed (see <see cref="TorchRuntimes"/>) and otherwise on
    /// the CPU. The predictions come back as the predict.speclib.tsv that peptdeep 1.5.0's
    /// "cmd-flow --task_workflow library" wrote when Skyline ran it in Python, using that workflow's
    /// defaults, and are then imported through BlibBuild as before. The library keeps the predicted
    /// CCS, which Skyline converts to ion mobility with each data file's calibration.
    /// </summary>
    public class AlphapeptdeepLibraryBuilder : AbstractDeepLibraryBuilder, IiRTCapableLibraryBuilder
    {
        public const string ALPHAPEPTDEEP = @"AlphaPeptDeep";

        // Processing folders
        private const string PREFIX_WORKDIR = "APD";
        private const string OUTPUT_SPECTRAL_LIBS = @"output_libs";

        // Processing intermediate file names. Osprey takes an output .blib path and writes the TSV
        // beside it as <stem>.predicted.speclib.tsv (OspreyHelpers.GetPredictedTablePath).
        private const string INPUT_FILE_NAME = @"input.tsv";
        private const string OSPREY_OUTPUT_FILE_NAME = @"predict.blib";
        private const string TRANSFORMED_OUTPUT_SPECTRAL_LIB_FILE_NAME = @"predict_sky.speclib.tsv";

        // Column names for AlphaPeptDeep
        private const string SEQUENCE = @"sequence";
        private const string MODS = @"mods";
        private const string MOD_SITES = @"mod_sites";
        private const string CHARGE = @"charge";

        private static readonly IEnumerable<string> PrecursorTableColumnNames = new[] { SEQUENCE, MODS, MOD_SITES, CHARGE };

        // Column names for BlibBuild
        private const string MODIFIED_PEPTIDE = "ModifiedPeptide";
        private const string NORMALIZED_RT = "RT";
        private const string CCS = "CCS";
        private const string COLLISIONAL_CROSS_SECTION = "CollisionalCrossSection";

        protected override string ToolName => ALPHAPEPTDEEP;

        private LibraryBuilderModificationSupport _modificationSupport;
        private string _carafeModelPath;

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
        /// The libtorch runtimes Osprey predicts with: where the CUDA runtime is installed, passed to Osprey.
        /// </summary>
        public TorchRuntimes TorchRuntimes { get; set; } = new TorchRuntimes();

        /// <summary>
        /// Whether to predict on the GPU, which needs the CUDA runtime installed and is then a requirement
        /// of the run; otherwise prediction runs on the CPU.
        /// </summary>
        public bool UseGpu { get; set; }

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

        private string OutputSpectralLibsDir => Path.Combine(WorkDir, OUTPUT_SPECTRAL_LIBS);

        /// <summary>The output Osprey is given, whose stem names the TSV it writes.</summary>
        private string OspreyOutputPath => Path.Combine(OutputSpectralLibsDir, OSPREY_OUTPUT_FILE_NAME);

        public string OutputSpectraLibFilepath => OspreyHelpers.GetPredictedTablePath(OspreyOutputPath);
        public string TransformedOutputSpectraLibFilepath => Path.Combine(OutputSpectralLibsDir, TRANSFORMED_OUTPUT_SPECTRAL_LIB_FILE_NAME);

        private Dictionary<string, string> OpenSwathAssayLikeColName =>
            new Dictionary<string, string>()
            {
                { @"RT", @"NormalizedRetentionTimeAPD" },
                { @"ModifiedPeptide", @"ModifiedPeptideSequence" },
                { @"FragmentMz", @"ProductMz" },
                { @"RelativeIntensity", @"LibraryIntensity" },
                { @"FragmentNumber", @"FragmentSeriesNumber" },
                { CCS, COLLISIONAL_CROSS_SECTION}
            };

        /// <summary>
        /// Constructor for AlphaPeptDeep Library Builder.
        /// </summary>
        /// <param name="libName">Name of the library to build.</param>
        /// <param name="libOutPath">Path to the blib final product.</param>
        /// <param name="document">Input document for building the library.</param>
        /// <param name="irtStandard">iRT peptide standard to include in the library.</param>
        public AlphapeptdeepLibraryBuilder(string libName, string libOutPath,
            SrmDocument document, IrtStandard irtStandard) : base(document, irtStandard)
        {
            LibrarySpec = new BiblioSpecLiteSpec(libName, libOutPath);
            string rootProcessingDir = Path.GetDirectoryName(libOutPath);
            if (string.IsNullOrEmpty(rootProcessingDir))
                throw new ArgumentException($@"AlphapeptdeepLibraryBuilder libOutputPath {libOutPath} must be a full path.");

            rootProcessingDir = Path.Combine(rootProcessingDir, Path.GetFileNameWithoutExtension(libOutPath));
            EnsureWorkDir(rootProcessingDir, PREFIX_WORKDIR);
        }

        public bool BuildLibrary(IProgressMonitor progress)
        {
            IProgressStatus progressStatus = new ProgressStatus();

            try
            {
                RunAlphapeptdeep(progress, ref progressStatus);
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

        private void RunAlphapeptdeep(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            // Note: Segments are distributed to balance the expected work of each task
            // One end per step, including the last: NextSegment() never advances past the final end
            var segmentEndPercentages = new[] { 5, 85, 90, 100 };
            progressStatus = progressStatus.ChangeSegments(0, ImmutableList<int>.ValueOf(segmentEndPercentages));
            PreparePrecursorInputFile(progress, ref progressStatus);
            progressStatus = progressStatus.NextSegment();
            ExecuteOsprey(progress, ref progressStatus);
            // ProcessRunner advanced the segment itself on Osprey's final 100% line (or at its exit),
            // and reported the cancel, if any, as the status
            if (progressStatus.IsCanceled)
                return;
            TransformPeptdeepOutput(progress, ref progressStatus);
            progressStatus = progressStatus.NextSegment();
            ImportSpectralLibrary(progress, ref progressStatus);
        }

        /// <summary>
        /// Predicts fragment intensities and retention times (iRT, or minutes with a fine-tuned RT model)
        /// for every precursor in the input file by running Osprey's PredictLibrary phase over it
        /// (<see cref="OspreyHelpers.BuildPredictLibraryCommandLine"/>), which writes them to
        /// <see cref="OutputSpectraLibFilepath"/>. Osprey chooses the libtorch runtime in its own process,
        /// so a build on the GPU needs no restart of Skyline; its console lines are the status messages,
        /// its NN% lines the bar, and its output is teed to a log in the working folder.
        /// </summary>
        private void ExecuteOsprey(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(ModelResources.AlphapeptdeepLibraryBuilder_Running_AlphaPeptDeep));
            if (!File.Exists(OspreyHelpers.OspreyBinary))
            {
                throw new IOException(string.Format(ModelResources.AlphapeptdeepLibraryBuilder_ExecuteOsprey_Osprey__which_predicts_the_library__is_not_installed_at__0_,
                    OspreyHelpers.OspreyBinary));
            }
            var timer = Stopwatch.StartNew();
            Directory.CreateDirectory(OutputSpectralLibsDir);
            string args = OspreyHelpers.BuildPredictLibraryCommandLine(InputFilePath, OspreyOutputPath, CarafeModelPath,
                UseGpu, TorchRuntimes.RuntimesDirectory, LocalizationHelper.CurrentUICulture.Name);
            try
            {
                OspreyHelpers.RunOsprey(args, WorkDir, progress, ref progressStatus, true);
            }
            catch (Exception ex)
            {
                throw new IOException(ModelResources.AlphapeptdeepLibraryBuilder_PredictSpectralLibrary_Failed_to_predict_the_library_with_AlphaPeptDeep_, ex);
            }
            if (progressStatus.IsCanceled)
                return;
            if (!File.Exists(OutputSpectraLibFilepath))
                throw new FileNotFoundException(ModelResources.AlphapeptdeepLibraryBuilder_PredictSpectralLibrary_Failed_to_predict_the_library_with_AlphaPeptDeep_, OutputSpectraLibFilepath);
            timer.Stop();
            Messages.WriteAsyncUserMessage(string.Format(ModelResources.AlphapeptdeepLibraryBuilder_ExecutePeptdeep_AlphaPeptDeep_finished_in__0__minutes__1__seconds_,
                timer.Elapsed.Minutes, timer.Elapsed.Seconds));
        }

        public void TransformPeptdeepOutput(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(ModelResources.AlphapeptdeepLibraryBuilder_Importing_spectral_library));

            var result = new List<string>();

            using var reader = new DsvFileReader(OutputSpectraLibFilepath, TextUtil.SEPARATOR_TSV);

            // Transform table header
            var colNames = reader.FieldNames;
            var header = string.Join(TextUtil.SEPARATOR_TSV_STR,
                colNames.Select(colName => OpenSwathAssayLikeColName.TryGetValue(colName, out var newColName) ? newColName : colName));
            result.Add(header);

            // Transform table body line by line
            while (null != reader.ReadLine())
            {
                var line = new List<string>();
                string peptideWithMods = string.Empty;
                bool modifiedPeptide = false;
                foreach (var colName in colNames)
                {
                    var cell = reader.GetFieldByName(colName);
                    if (colName == MODIFIED_PEPTIDE)
                    {
                        var transformedCell = cell.Replace(TextUtil.UNDERSCORE, string.Empty)
                            .Replace(TextUtil.LEFT_SQUARE_BRACKET, TextUtil.LEFT_PARENTHESIS)
                            .Replace(TextUtil.RIGHT_SQUARE_BRACKET, TextUtil.RIGHT_PARENTHESIS);
                        line.Add(transformedCell);
                        if (transformedCell.Contains('('))
                        {
                            modifiedPeptide = true;
                        }
                        peptideWithMods = cell;
                    }
                    else if (colName == CCS)
                    {
                        AddDoubleCell(line, cell,
                            !modifiedPeptide || LibraryBuilderModificationSupport.PeptideHasOnlyCcsSupportedMod(peptideWithMods));
                    }
                    else if (colName == NORMALIZED_RT)
                    {
                        AddDoubleCell(line, cell,
                            !modifiedPeptide || LibraryBuilderModificationSupport.PeptideHasOnlyRtSupportedMod(peptideWithMods));
                    }
                    else
                    {
                        line.Add(cell);
                    }
                }
                // Only add a row, if there are no modifications or all modifications at least support spectrum prediction
                if (!modifiedPeptide || LibraryBuilderModificationSupport.PeptideHasOnlyMs2SupportedMod(peptideWithMods))
                    result.Add(string.Join(TextUtil.SEPARATOR_TSV_STR, line));
            }

            // Write to new file
            File.WriteAllLines(TransformedOutputSpectraLibFilepath, result);
        }

        private void AddDoubleCell(List<string> line, string cell, bool allowedValue)
        {
            double valueToAdd;
            if (!allowedValue || !double.TryParse(cell.ToString(CultureInfo.InvariantCulture), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out valueToAdd))
            {
                valueToAdd = 0; // CONSIDER: Zero is a valid value for a normalized-RT
            }
            // Pin to G15 so net8's shortest-round-trip double formatting matches
            // net472's implicit G15 default (keeps the transformed .tsv byte-identical).
            line.Add(valueToAdd.ToString(@"G15", CultureInfo.InvariantCulture));
        }

        public void ImportSpectralLibrary(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            string[] inputFile = { TransformedOutputSpectraLibFilepath.ToLongPath() };
            string output = LibrarySpec.FilePath.ToLongPath();
            string incompleteBlibPath = BiblioSpecLiteSpec.GetRedundantName(output).ToLongPath();
            var build = new BlibBuild(incompleteBlibPath, inputFile);

            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(ModelResources.AlphapeptdeepLibraryBuilder_Importing_spectral_library));

            string[] ambiguous;
            bool completed = build.BuildLibrary(LibraryBuildAction.Create, progress, ref progressStatus, out ambiguous);

            if (ambiguous.Length > 0)
            {
                foreach (string msg in ambiguous)
                {
                    Messages.WriteAsyncUserMessage(msg);
                }
            }

            try
            {
                var blibFilter = new BlibFilter();
                // Build the final filtered library
                completed = completed && blibFilter.Filter(incompleteBlibPath, output, progress, ref progressStatus);
            }
            finally
            {
                File.Delete(incompleteBlibPath);
            }

            // Throw rather than return, so the caller does not add a library that was never written
            if (progress.IsCanceled)
                throw new OperationCanceledException();
            if (!completed)
                throw new IOException(ModelResources.AlphapeptdeepLibraryBuilder_ImportSpectralLibrary_BlibBuild_failed_to_complete_);

            Messages.WriteAsyncUserMessage(ModelResources.AlphapeptdeepLibraryBuilder_ImportSpectralLibrary_BlibBuild_completed_successfully_);
        }
    }
}
