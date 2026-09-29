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
using System.Text;
using pwiz.BiblioSpec;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.Models;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Util.Extensions;
using TorchSharp;

namespace pwiz.Skyline.Model.Lib.AlphaPeptDeep
{
    public class ArgumentAndValue
    {
        private const string DEFAULT_DASH = @"--";

        public ArgumentAndValue(string name, string value, bool quoteValue)
            : this(name, value, DEFAULT_DASH, quoteValue)
        {}

        public ArgumentAndValue(string name, string value, string dash = DEFAULT_DASH, bool quoteValue = false)
        {
            Name = name;
            Value = value;
            if (quoteValue)
                Value = '"' + Value + '"';
            Dash = dash;
        }
        public string Name { get; private set; }
        public string Value { get; private set; }
        public string Dash { get; set; }

        public override string ToString() { return TextUtil.SpaceSeparate(Dash + Name, Value); }
    }

    /// <summary>
    /// Builds a library of AlphaPeptDeep MS2 and RT predictions for the document's precursors. The
    /// models run in process through CarafeSharp's TorchSharp port of peptdeep, on an NVIDIA GPU when
    /// one is usable and otherwise on the CPU. The predictions are written as the predict.speclib.tsv
    /// that peptdeep 1.5.0's "cmd-flow --task_workflow library" wrote when Skyline ran it in Python,
    /// using that workflow's defaults, and are then imported through BlibBuild as before. CCS is not
    /// predicted: CarafeSharp has no port of the CCS model.
    /// </summary>
    public class AlphapeptdeepLibraryBuilder : AbstractDeepLibraryBuilder, IiRTCapableLibraryBuilder
    {
        public const string ALPHAPEPTDEEP = @"AlphaPeptDeep";

        // peptdeep 1.5.0 library defaults (model_mgr and library--output_tsv in its default settings.yaml).
        // With them CarafeSharp reproduces the libraries peptdeep built for TestAlphaPeptDeepBuildLibrary:
        // the same fragments, intensities within 1e-5 and iRT within 0.02.
        private const double NCE = 30;
        private const string INSTRUMENT = @"Lumos";
        private const double MIN_FRAGMENT_MZ = 200;
        private const double MAX_FRAGMENT_MZ = 2000;
        private const float MIN_RELATIVE_INTENSITY = 0.001f;
        private const int MAX_FRAGMENTS = 12;

        // Precursors predicted between progress updates and cancellation checks
        private const int PRECURSORS_PER_CHUNK = 1000;

        // Processing folders
        private const string PREFIX_WORKDIR = "APD";
        private const string OUTPUT_SPECTRAL_LIBS = @"output_libs";

        // Processing intermediate file names
        private const string INPUT_FILE_NAME = @"input.tsv";
        private const string OUTPUT_SPECTRAL_LIB_FILE_NAME = @"predict.speclib.tsv";
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
        private const string ION_MOBILITY = "IonMobility";
        private const string CCS = "CCS";
        private const string COLLISIONAL_CROSS_SECTION = "CollisionalCrossSection";

        // Columns of the predict.speclib.tsv peptdeep wrote, without its IonMobility and CCS
        private static readonly string[] SpectralLibraryColumnNames =
        {
            MODIFIED_PEPTIDE, @"PrecursorCharge", NORMALIZED_RT, @"StrippedPeptide", @"PrecursorMz", @"Decoy",
            @"FragmentType", @"FragmentMz", @"RelativeIntensity", @"FragmentCharge", @"FragmentNumber",
            @"FragmentLossType"
        };

        private static readonly string[] FRAGMENT_TYPES = { @"b", @"b", @"y", @"y" };
        private static readonly int[] FRAGMENT_CHARGES = { 1, 2, 1, 2 };

        protected override string ToolName => ALPHAPEPTDEEP;

        protected override LibraryBuilderModificationSupport LibraryBuilderModificationSupport { get; }

        internal static List<ModificationType> MODEL_SUPPORTED_UNIMODS = new List<ModificationType>
        {
            GetUniModType(4, PredictionSupport.all), // Carbamidomethyl (C)
            GetUniModType(21, PredictionSupport.fragmentation), // Phospho
            GetUniModType(35, PredictionSupport.all), // Oxidation
            GetUniModType(121, PredictionSupport.fragmentation) // GlyGly (a.k.a. GG)
        };

        /// <summary>
        /// The pinned AlphaPeptDeep pretrained models, which the build places beside the Skyline assembly.
        /// </summary>
        public static string PretrainedModelsPath =>
            Path.Combine(Path.GetDirectoryName(typeof(AlphapeptdeepLibraryBuilder).Assembly.Location) ?? string.Empty,
                PretrainedModels.BUNDLED_RELATIVE_PATH);

        public LibrarySpec LibrarySpec { get; private set; }

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

        public string OutputSpectraLibFilepath => Path.Combine(OutputSpectralLibsDir, OUTPUT_SPECTRAL_LIB_FILE_NAME);
        public string TransformedOutputSpectraLibFilepath => Path.Combine(OutputSpectralLibsDir, TRANSFORMED_OUTPUT_SPECTRAL_LIB_FILE_NAME);

        private Dictionary<string, string> OpenSwathAssayLikeColName =>
            new Dictionary<string, string>()
            {
                { @"RT", @"NormalizedRetentionTimeAPD" },
                { @"ModifiedPeptide", @"ModifiedPeptideSequence" },
                { @"FragmentMz", @"ProductMz" },
                { @"RelativeIntensity", @"LibraryIntensity" },
                { @"FragmentNumber", @"FragmentSeriesNumber" },
                { CCS, COLLISIONAL_CROSS_SECTION},
                { ION_MOBILITY, null}
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
            LibraryBuilderModificationSupport = new LibraryBuilderModificationSupport(MODEL_SUPPORTED_UNIMODS);
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
            PredictSpectralLibrary(progress, ref progressStatus);
            progressStatus = progressStatus.NextSegment();
            TransformPeptdeepOutput(progress, ref progressStatus);
            progressStatus = progressStatus.NextSegment();
            ImportSpectralLibrary(progress, ref progressStatus);
        }

        /// <summary>
        /// Predicts fragment intensities and iRT for every precursor in the input file and writes them
        /// to <see cref="OutputSpectraLibFilepath"/>.
        /// </summary>
        private void PredictSpectralLibrary(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(ModelResources.AlphapeptdeepLibraryBuilder_Running_AlphaPeptDeep));
            var timer = Stopwatch.StartNew();
            var precursors = ReadPrecursorInputFile();
            try
            {
                var device = TorchDevice.Resolve(@"gpu", out _);
                Messages.WriteAsyncUserMessage(device.type == DeviceType.CUDA
                    ? ModelResources.AlphapeptdeepLibraryBuilder_PredictSpectralLibrary_Predicting_on_the_GPU
                    : ModelResources.AlphapeptdeepLibraryBuilder_PredictSpectralLibrary_Predicting_on_the_CPU);
                var pretrained = PretrainedModels.Open(PretrainedModelsPath);
                Directory.CreateDirectory(OutputSpectralLibsDir);
                using var ms2 = Ms2Model.FromPretrained(pretrained, device);
                using var rt = RtModel.FromPretrained(pretrained, device);
                var irt = rt.FitIrtCalibration();
                using var writer = new StreamWriter(OutputSpectraLibFilepath, false, new UTF8Encoding(false));
                writer.WriteLine(string.Join(TextUtil.SEPARATOR_TSV_STR, SpectralLibraryColumnNames));
                for (int start = 0; start < precursors.Count; start += PRECURSORS_PER_CHUNK)
                {
                    if (progress.IsCanceled)
                        throw new OperationCanceledException();

                    var chunk = precursors.Skip(start).Take(PRECURSORS_PER_CHUNK).ToList();
                    var spectra = ms2.Predict(chunk.Select(p => new Ms2Request(p, NCE, INSTRUMENT)).ToList());
                    var normalizedRts = rt.Predict(chunk.Select(p => p.Peptide).ToList());
                    for (int i = 0; i < chunk.Count; i++)
                        WriteSpectrum(writer, spectra[i], irt.Slope * normalizedRts[i] + irt.Intercept);

                    progress.UpdateProgress(progressStatus = progressStatus
                        .ChangePercentComplete((start + chunk.Count) * 100 / precursors.Count));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new IOException(ModelResources.AlphapeptdeepLibraryBuilder_PredictSpectralLibrary_Failed_to_predict_the_library_with_AlphaPeptDeep_, ex);
            }
            timer.Stop();
            Messages.WriteAsyncUserMessage(string.Format(ModelResources.AlphapeptdeepLibraryBuilder_ExecutePeptdeep_AlphaPeptDeep_finished_in__0__minutes__1__seconds_,
                timer.Elapsed.Minutes, timer.Elapsed.Seconds));
        }

        private List<PrecursorForm> ReadPrecursorInputFile()
        {
            var precursors = new List<PrecursorForm>();
            using var reader = new DsvFileReader(InputFilePath, TextUtil.SEPARATOR_TSV);
            while (null != reader.ReadLine())
            {
                var peptide = PeptideForm.FromAlphabase(reader.GetFieldByName(SEQUENCE),
                    reader.GetFieldByName(MODS), reader.GetFieldByName(MOD_SITES));
                precursors.Add(new PrecursorForm(peptide,
                    int.Parse(reader.GetFieldByName(CHARGE), CultureInfo.InvariantCulture)));
            }
            return precursors;
        }

        /// <summary>
        /// Writes one precursor's rows the way peptdeep translated its predictions to TSV: b and y ions of
        /// charge 1 and 2 (not above the precursor charge) within the fragment m/z range, renormalized to the
        /// most intense of them, at least <see cref="MIN_RELATIVE_INTENSITY"/>, the
        /// <see cref="MAX_FRAGMENTS"/> most intense, in decreasing intensity.
        /// </summary>
        private static void WriteSpectrum(TextWriter writer, Ms2Prediction spectrum, double irt)
        {
            var precursor = spectrum.Precursor;
            var peptide = precursor.Peptide;
            var fragmentMzs = AlphabaseFragmentMz.Calculate(precursor);
            var fragments = new List<(int Row, int Column, float Mz, float Intensity)>();
            for (int row = 0; row < spectrum.RowCount; row++)
            {
                for (int column = 0; column < AlphabaseFragmentMz.COLUMN_COUNT; column++)
                {
                    float intensity = spectrum.Get(row, column);
                    double mz = fragmentMzs[row * AlphabaseFragmentMz.COLUMN_COUNT + column];
                    if (FRAGMENT_CHARGES[column] > precursor.Charge || intensity <= 0 ||
                        mz < MIN_FRAGMENT_MZ || mz > MAX_FRAGMENT_MZ)
                    {
                        continue;
                    }
                    fragments.Add((row, column, (float)mz, intensity));
                }
            }
            if (fragments.Count == 0)
                return;

            float maxIntensity = fragments.Max(f => f.Intensity);
            string precursorColumns = string.Join(TextUtil.SEPARATOR_TSV_STR,
                GetModifiedPeptide(peptide),
                precursor.Charge.ToString(CultureInfo.InvariantCulture),
                irt.ToString(CultureInfo.InvariantCulture),
                peptide.Sequence,
                GetPrecursorMz(precursor).ToString(CultureInfo.InvariantCulture),
                @"0");
            foreach (var fragment in fragments
                         .Select(f => (f.Row, f.Column, f.Mz, Intensity: f.Intensity / maxIntensity))
                         .Where(f => f.Intensity >= MIN_RELATIVE_INTENSITY)
                         .OrderByDescending(f => f.Intensity)
                         .Take(MAX_FRAGMENTS))
            {
                string type = FRAGMENT_TYPES[fragment.Column];
                int number = type == @"b" ? fragment.Row + 1 : peptide.Length - 1 - fragment.Row;
                writer.WriteLine(string.Join(TextUtil.SEPARATOR_TSV_STR,
                    precursorColumns,
                    type,
                    fragment.Mz.ToString(CultureInfo.InvariantCulture),
                    fragment.Intensity.ToString(CultureInfo.InvariantCulture),
                    FRAGMENT_CHARGES[fragment.Column].ToString(CultureInfo.InvariantCulture),
                    number.ToString(CultureInfo.InvariantCulture),
                    @"noloss"));
            }
        }

        /// <summary>
        /// The peptide in peptdeep's DIA-NN notation with UniMod ids, e.g. _S[UniMod:21]KYLINE_. A terminal
        /// modification follows the terminal residue, as peptdeep writes it.
        /// </summary>
        private static string GetModifiedPeptide(PeptideForm peptide)
        {
            var modsAfterResidue = new List<string>[peptide.Length];
            for (int i = 0; i < peptide.ModNames.Count; i++)
            {
                int site = peptide.ModSites[i];
                int index = site == 0 ? 0 : site == -1 ? peptide.Length - 1 : site - 1;
                modsAfterResidue[index] ??= new List<string>();
                modsAfterResidue[index].Add(string.Format(CultureInfo.InvariantCulture, @"[UniMod:{0}]",
                    ModificationTable.Get(peptide.ModNames[i]).UnimodId));
            }
            var result = new StringBuilder(@"_");
            for (int i = 0; i < peptide.Length; i++)
            {
                result.Append(peptide.Sequence[i]);
                if (modsAfterResidue[i] != null)
                    result.Append(string.Concat(modsAfterResidue[i]));
            }
            return result.Append(@"_").ToString();
        }

        /// <summary>
        /// Monoisotopic precursor m/z from alphabase's masses, which peptdeep reported.
        /// </summary>
        private static double GetPrecursorMz(PrecursorForm precursor)
        {
            var peptide = precursor.Peptide;
            double mass = peptide.Sequence.Sum(AlphabaseMasses.GetResidueMass) + AlphabaseMasses.H2O +
                          peptide.ModNames.Sum(name => ModificationTable.Get(name).Mass);
            return mass / precursor.Charge + AlphabaseMasses.PROTON;
        }

        public void TransformPeptdeepOutput(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(ModelResources.AlphapeptdeepLibraryBuilder_Importing_spectral_library));

            var result = new List<string>();

            using var reader = new DsvFileReader(OutputSpectraLibFilepath, TextUtil.SEPARATOR_TSV);

            // Transform table header
            var colNames = reader.FieldNames;
            var newColNames = new List<string>();
            foreach (var colName in colNames)
            {
                string newColName;
                if (!OpenSwathAssayLikeColName.TryGetValue(colName, out newColName))
                {
                    newColName = colName;
                }
                if (newColName != null)
                {
                    newColNames.Add(newColName);
                }
            }
            var header = string.Join(TextUtil.SEPARATOR_TSV_STR, newColNames);
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
                    else if (colName != ION_MOBILITY)
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
