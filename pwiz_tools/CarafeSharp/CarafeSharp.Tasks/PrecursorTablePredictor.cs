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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.Models;
using pwiz.CarafeSharp.Proteome;
using pwiz.Common.SystemUtil;

namespace pwiz.CarafeSharp.Tasks
{
    /// <summary>
    /// Predicts the precursors of a table - exactly those, with their modification sites and
    /// charges - and writes them as peptdeep's <c>library --output_tsv</c> wrote them, which is the
    /// file Skyline's Build Library turns into a .blib. The table is the one Skyline wrote for
    /// peptdeep (<see cref="PrecursorTable"/>: alphabase's sequence, mods, mod_sites and charge), so
    /// a document's precursors go to the models as they are, where Carafe's library prediction
    /// (<see cref="LibraryGenerator"/>) lists peptidoforms from a digest's bare sequences under
    /// global modification rules. The models, the fragment selection and the retention time are
    /// peptdeep 1.5.0's library defaults, with which this reproduced the libraries peptdeep built:
    /// the same fragments, intensities within 1e-5 and iRT within 0.02.
    /// </summary>
    public sealed class PrecursorTablePredictor
    {
        // peptdeep 1.5.0 library defaults (model_mgr and library--output_tsv in its default settings.yaml),
        // applied where the command line gives none; a saved model's training values replace them.
        public const double DEFAULT_NCE = 30;
        public const string DEFAULT_INSTRUMENT = @"Lumos";
        private const double MIN_FRAGMENT_MZ = 200;
        private const double MAX_FRAGMENT_MZ = 2000;
        private const float MIN_RELATIVE_INTENSITY = 0.001f;
        private const int MAX_FRAGMENTS = 12;

        // Precursors predicted between progress reports
        private const int PRECURSORS_PER_CHUNK = 1000;

        // The columns of the predict.speclib.tsv peptdeep wrote, without its IonMobility, the timsTOF 1/K0
        // of the CCS, which the library did not keep
        public static readonly IReadOnlyList<string> OUTPUT_COLUMNS = new[]
        {
            @"ModifiedPeptide", @"PrecursorCharge", @"RT", @"CCS", @"StrippedPeptide", @"PrecursorMz", @"Decoy",
            @"FragmentType", @"FragmentMz", @"RelativeIntensity", @"FragmentCharge", @"FragmentNumber",
            @"FragmentLossType"
        };

        private static readonly string[] FRAGMENT_TYPES = { @"b", @"b", @"y", @"y" };
        private static readonly int[] FRAGMENT_CHARGES = { 1, 2, 1, 2 };
        private const string TAB = "\t";

        private readonly LibrarySettings _settings;
        private readonly string _tablePath;
        private readonly string _outputPath;
        private readonly TextWriter _log;
        private PretrainedModels _pretrained;

        /// <param name="settings">The models to predict with (<see cref="LibrarySettings.ModelFile"/> for a
        /// saved model, else the pretrained ones at <see cref="LibrarySettings.PretrainedModels"/>), the device,
        /// and the prediction settings the command line gave; the rest are peptdeep's library defaults.</param>
        /// <param name="tablePath">The precursor table.</param>
        /// <param name="outputPath">The TSV to write.</param>
        /// <param name="log">Receives progress, or null.</param>
        public PrecursorTablePredictor(LibrarySettings settings, string tablePath, string outputPath, TextWriter log)
        {
            _settings = settings;
            _tablePath = tablePath;
            _outputPath = outputPath;
            _log = log ?? TextWriter.Null;
        }

        /// <summary>Precursors written.</summary>
        public int PrecursorCount { get; private set; }

        public void Run()
        {
            string modelFileFolder = null;
            try
            {
                Predict(ref modelFileFolder);
            }
            finally
            {
                // A saved model's unpacked copy, which the models were loaded from.
                if (modelFileFolder != null && Directory.Exists(modelFileFolder))
                    Directory.Delete(modelFileFolder, true);
            }
        }

        private void Predict(ref string modelFileFolder)
        {
            var clock = Stopwatch.StartNew();
            var precursors = PrecursorTable.Read(_tablePath);
            Log(string.Format(CultureInfo.InvariantCulture, @"Predicting {0} precursors from {1}", precursors.Count, _tablePath));
            string outputDirectory = Path.GetDirectoryName(Path.GetFullPath(_outputPath));
            if (!string.IsNullOrEmpty(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            // peptdeep's defaults where the command line gave none; a saved model then applies its training's
            if (!_settings.UserNce)
                _settings.Nce = DEFAULT_NCE;
            if (!_settings.UserInstrument)
                _settings.Instrument = DEFAULT_INSTRUMENT;
            CarafeModelDirectory modelDirectory;
            if (_settings.ModelFile != null)
            {
                modelDirectory = LibraryModels.OpenModelFile(_settings, Log, out modelFileFolder);
            }
            else
            {
                string folder = _settings.ModelDirectory ?? outputDirectory;
                modelDirectory = CarafeModelDirectory.Open(folder, _settings.PreferSafetensors);
                foreach (string warning in modelDirectory.Warnings)
                    Log(warning);
                if (_settings.ModelDirectory != null)
                {
                    // A model folder (the fine-tune's, when the table is predicted right after one):
                    // its models, of whatever kind the tuning wrote, and its training run's values
                    Log(@"Use the model in the folder: " + folder + @" for spectral library generation");
                    LibraryModels.ApplyModelDirectoryMeta(_settings, modelDirectory, Log);
                }
                // Without a fine-tuned RT model, Carafe's default, the pretrained Chronologer, whose
                // hydrophobic index the library takes as iRT; peptdeep's own RT model predicts poorly
            }

            var device = TorchDevice.Resolve(_settings.Device, out string fallback);
            if (fallback != null)
                Log(fallback);
            using (var ms2 = LibraryModels.LoadMs2Model(_settings, modelDirectory, device, OpenPretrained, Log))
            using (var rt = LibraryModels.LoadRtModel(_settings, modelDirectory, device, OpenPretrained, Log))
            using (var ccs = LibraryModels.LoadCcsModel(_settings, modelDirectory, device, OpenPretrained, Log))
            {
                // A model of a fraction of the gradient gives minutes on the training runs' gradient, as
                // CarafeSharp writes them; the pretrained models' predictions, and a Chronologer's
                // hydrophobic index, give iRT
                double libraryRtMax = rt.PredictsNormalizedRt ? _settings.RtMax : 0;
                var irt = libraryRtMax > 0 ? (Slope: 0.0, Intercept: 0.0) : rt.FitIrtCalibration();
                if (libraryRtMax > 0)
                    Log(string.Format(CultureInfo.InvariantCulture, @"Library RT: rt_pred * rt_max ({0})", libraryRtMax));
                else
                    Log(string.Format(CultureInfo.InvariantCulture, @"Library RT: iRT = {0} * rt_pred + {1}", irt.Slope, irt.Intercept));
                Log(string.Format(CultureInfo.InvariantCulture, @"NCE: {0}, instrument: {1}, activation: {2}, analyzer: {3}", _settings.Nce,
                    _settings.Instrument, _settings.Activation ?? @"(none)", _settings.Analyzer ?? @"(none)"));

                using var writer = new StreamWriter(_outputPath, false, new UTF8Encoding(false));
                writer.WriteLine(string.Join(TAB, OUTPUT_COLUMNS));
                // Predicting is the long step: a throttled percent over the precursors says the run is alive
                using (var progress = new ProgressReporter(@"Predicting spectra", precursors.Count, null, ProgressReporter.IO_INTERVAL_SECONDS))
                {
                    for (int start = 0; start < precursors.Count; start += PRECURSORS_PER_CHUNK)
                    {
                        var chunk = precursors.Skip(start).Take(PRECURSORS_PER_CHUNK).ToList();
                        var spectra = ms2.Predict(chunk.Select(p =>
                            new Ms2Request(p, _settings.Nce, _settings.Instrument, _settings.Activation, _settings.Analyzer)).ToList());
                        var rtPredictions = rt.Predict(chunk.Select(p => p.Peptide).ToList());
                        var ccsPredictions = ccs.Predict(chunk);
                        for (int i = 0; i < chunk.Count; i++)
                        {
                            WriteSpectrum(writer, spectra[i],
                                LibrarySpectrumBuilder.GetRetentionTime(rtPredictions[i], libraryRtMax, irt.Slope, irt.Intercept),
                                ccsPredictions[i]);
                        }
                        progress.Report(start + chunk.Count);
                    }
                }
            }
            Log(string.Format(CultureInfo.InvariantCulture, @"Wrote {0} precursors in {1:F1} s", PrecursorCount, clock.Elapsed.TotalSeconds));
            Log(@"The spectral library is saved to " + _outputPath);
        }

        /// <summary>
        /// Writes one precursor's rows the way peptdeep translated its predictions to TSV: b and y ions of
        /// charge 1 and 2 (not above the precursor charge) within the fragment m/z range, renormalized to the
        /// most intense of them, at least <see cref="MIN_RELATIVE_INTENSITY"/>, the
        /// <see cref="MAX_FRAGMENTS"/> most intense, in decreasing intensity.
        /// </summary>
        private void WriteSpectrum(TextWriter writer, Ms2Prediction spectrum, double rt, double ccs)
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

            PrecursorCount++;
            float maxIntensity = fragments.Max(f => f.Intensity);
            string precursorColumns = string.Join(TAB,
                GetModifiedPeptide(peptide),
                precursor.Charge.ToString(CultureInfo.InvariantCulture),
                rt.ToString(CultureInfo.InvariantCulture),
                ccs.ToString(CultureInfo.InvariantCulture),
                peptide.Sequence,
                AlphabaseFragmentMz.CalculatePrecursorMz(precursor).ToString(CultureInfo.InvariantCulture),
                @"0");
            foreach (var fragment in fragments
                         .Select(f => (f.Row, f.Column, f.Mz, Intensity: f.Intensity / maxIntensity))
                         .Where(f => f.Intensity >= MIN_RELATIVE_INTENSITY)
                         .OrderByDescending(f => f.Intensity)
                         .Take(MAX_FRAGMENTS))
            {
                string type = FRAGMENT_TYPES[fragment.Column];
                int number = type == @"b" ? fragment.Row + 1 : peptide.Length - 1 - fragment.Row;
                writer.WriteLine(string.Join(TAB,
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
        public static string GetModifiedPeptide(PeptideForm peptide)
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

        /// <summary>The pretrained models, opened (and their SHA-256 checked) once.</summary>
        private PretrainedModels OpenPretrained()
        {
            return _pretrained ??= PretrainedModels.Open(_settings.PretrainedModels);
        }

        private void Log(string message)
        {
            _log.WriteLine(message);
            _log.Flush();
        }
    }
}
