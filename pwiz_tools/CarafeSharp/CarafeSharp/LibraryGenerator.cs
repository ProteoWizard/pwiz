/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) main.java.ai.AIGear
 *   (generate_spectral_library, generate_spectral_library_parquet,
 *   generate_spectral_library_parquet_skyline) and src/main/resources/py/v2/ai_pred.py
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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.IO;
using pwiz.CarafeSharp.Models;
using pwiz.CarafeSharp.Proteome;
using static TorchSharp.torch;

namespace pwiz.CarafeSharp
{
    /// <summary>
    /// Carafe's library prediction (<c>-db</c> without <c>-ms</c>): digests the FASTA, lists
    /// every peptidoform and precursor, predicts MS2 and RT, and writes the library as Carafe's
    /// TSV and/or a BiblioSpec .blib. Work proceeds in Carafe's batches of 200,000 peptidoforms,
    /// each predicted and written in chunks, so memory is bounded by a chunk plus the peptide
    /// list; spectra are written in peptidoform mass order, charges ascending.
    /// </summary>
    public sealed class LibraryGenerator
    {
        /// <summary>Peptidoforms predicted and written together within a batch.</summary>
        public const int FORMS_PER_CHUNK = 10000;

        /// <summary>Values per fragment row of an <see cref="Ms2Prediction"/>.</summary>
        private static readonly int INTENSITY_STRIDE = PeptdeepConstants.CHARGED_FRAG_TYPES.Length;

        private readonly LibrarySettings _settings;
        private readonly TextWriter _log;
        private readonly Stopwatch _clock = new Stopwatch();
        private readonly Stopwatch _ms2Clock = new Stopwatch();
        private readonly Stopwatch _rtClock = new Stopwatch();
        private readonly Stopwatch _buildClock = new Stopwatch();
        private readonly Stopwatch _writeClock = new Stopwatch();

        public LibraryGenerator(LibrarySettings settings, TextWriter log)
        {
            _settings = settings;
            _log = log ?? TextWriter.Null;
        }

        /// <summary>The .blib written, or null.</summary>
        public string BlibPath { get; private set; }

        /// <summary>The TSV written, or null.</summary>
        public string TsvPath { get; private set; }

        /// <summary>Precursors written.</summary>
        public int SpectrumCount { get; private set; }

        public void Run()
        {
            _clock.Restart();
            Directory.CreateDirectory(_settings.OutputDirectory);
            var modelDirectory = OpenModelDirectory();
            var outputs = LibraryOutputs.FromFormat(_settings.LibraryFormat, _settings.Fast);
            if (outputs.Warning != null)
                Log(outputs.Warning);

            var digester = new Digester(_settings.Digest);
            var peptides = LibraryDatabase.DigestPeptides(_settings.Database, digester, Log);
            var generator = new PeptideIsoformGenerator(_settings.Modifications, digester.ProteinNTermPeptides);
            var forms = LibraryPeptideForms.Enumerate(peptides, generator);
            Log(string.Format(CultureInfo.InvariantCulture, @"Generating peptide forms: {0}", forms.Count));
            var peptideToProteins = LibraryDatabase.MapPeptidesToProteins(_settings.Database, _settings.Digest);
            Log(string.Format(CultureInfo.InvariantCulture, @"Mapped {0} peptides to proteins", peptideToProteins.Count));

            var device = TorchDevice.Resolve(_settings.Device, out string fallback);
            if (fallback != null)
                Log(fallback);
            using (var ms2 = LoadMs2Model(modelDirectory, device))
            using (var rt = LoadRtModel(modelDirectory, device))
            {
                var irt = _settings.RtMax > 0 ? (Slope: 0.0, Intercept: 0.0) : rt.FitIrtCalibration();
                if (_settings.RtMax > 0)
                    Log(string.Format(CultureInfo.InvariantCulture, @"Library RT: rt_pred * rt_max ({0})", _settings.RtMax));
                else
                    Log(string.Format(CultureInfo.InvariantCulture, @"Library RT: iRT = {0} * rt_pred + {1}", irt.Slope, irt.Intercept));
                Log(string.Format(CultureInfo.InvariantCulture, @"NCE: {0}, instrument: {1}", _settings.Nce, _settings.Instrument));
                var builder = new LibrarySpectrumBuilder(_settings, outputs, peptideToProteins);
                WriteLibrary(forms, outputs, builder, ms2, rt, irt);
            }
            Log(string.Format(CultureInfo.InvariantCulture, @"Wrote {0} precursors in {1:F1} s", SpectrumCount, _clock.Elapsed.TotalSeconds));
        }

        private void WriteLibrary(List<PeptideIsoform> forms, LibraryOutputs outputs, LibrarySpectrumBuilder builder,
            Ms2Model ms2, RtModel rt, (double Slope, double Intercept) irt)
        {
            TsvPath = outputs.WritesTsv ? Path.Combine(_settings.OutputDirectory, CarafeLibraryTsvWriter.FILE_NAME) : null;
            BlibPath = outputs.WritesBlib ? Path.Combine(_settings.OutputDirectory, BlibLibraryWriter.FILE_NAME) : null;
            var pairingPrecursors = BlibPath != null && !string.IsNullOrEmpty(_settings.PairingManifest)
                ? new List<DecoyPairPlanner.Precursor>()
                : null;
            using (var tsv = TsvPath != null ? new CarafeLibraryTsvWriter(TsvPath) : null)
            using (var blib = BlibPath != null ? new BlibLibraryWriter(BlibPath, Path.GetFileNameWithoutExtension(BlibPath)) : null)
            {
                int batchCount = (forms.Count + _settings.PeptidesPerBatch - 1) / Math.Max(1, _settings.PeptidesPerBatch);
                for (int batch = 0; batch < batchCount; batch++)
                {
                    int batchStart = batch * _settings.PeptidesPerBatch;
                    int batchEnd = Math.Min(forms.Count, batchStart + _settings.PeptidesPerBatch);
                    int written = 0;
                    var batchClock = Stopwatch.StartNew();
                    for (int start = batchStart; start < batchEnd; start += FORMS_PER_CHUNK)
                    {
                        var spectra = PredictChunk(forms, start, Math.Min(batchEnd, start + FORMS_PER_CHUNK), builder, ms2, rt, irt);
                        _writeClock.Start();
                        if (tsv != null)
                        {
                            var rows = new string[spectra.Count];
                            Parallel.For(0, spectra.Count, i => rows[i] = CarafeLibraryTsvWriter.FormatRows(spectra[i]));
                            foreach (string text in rows)
                                tsv.WriteRows(text);
                        }
                        if (blib != null)
                        {
                            int firstId = blib.WriteBatch(spectra);
                            if (pairingPrecursors != null)
                            {
                                for (int i = 0; i < spectra.Count; i++)
                                {
                                    var peptide = spectra[i].Precursor.Peptide;
                                    pairingPrecursors.Add(new DecoyPairPlanner.Precursor(firstId + i, peptide.Sequence,
                                        spectra[i].Charge, DecoyPairPlanner.ModKey(peptide.ModNames)));
                                }
                            }
                        }
                        written += spectra.Count;
                        _writeClock.Stop();
                    }
                    SpectrumCount += written;
                    Log(string.Format(CultureInfo.InvariantCulture,
                        @"Batch {0}/{1}: peptide forms {2}-{3}, {4} precursors written in {5:F1} s ({6} total, {7:F1} s elapsed; " +
                        @"MS2 {8:F1} s, RT {9:F1} s, assembly {10:F1} s, writing {11:F1} s)",
                        batch + 1, batchCount, batchStart + 1, batchEnd, written, batchClock.Elapsed.TotalSeconds, SpectrumCount,
                        _clock.Elapsed.TotalSeconds, _ms2Clock.Elapsed.TotalSeconds, _rtClock.Elapsed.TotalSeconds,
                        _buildClock.Elapsed.TotalSeconds, _writeClock.Elapsed.TotalSeconds));
                }
                if (blib != null)
                {
                    if (pairingPrecursors != null)
                        WriteDecoyPairs(blib, pairingPrecursors);
                    blib.Complete();
                    Log(@"The spectral library is saved to " + BlibPath);
                }
                if (tsv != null)
                {
                    tsv.Complete();
                    Log(@"The spectral library is saved to " + TsvPath);
                }
            }
        }

        /// <summary>
        /// Predicts and builds the spectra of peptidoforms [start, end): MS2 for every precursor
        /// in the m/z window, RT once per peptidoform.
        /// </summary>
        private List<LibrarySpectrum> PredictChunk(List<PeptideIsoform> forms, int start, int end, LibrarySpectrumBuilder builder,
            Ms2Model ms2, RtModel rt, (double Slope, double Intercept) irt)
        {
            var isoforms = new List<PeptideIsoform>();
            var peptides = new List<PeptideForm>();
            var requests = new List<Ms2Request>();
            var formIndex = new List<int>();
            for (int i = start; i < end; i++)
            {
                var charges = LibraryPeptideForms.GetCharges(forms[i], _settings.Charges, _settings.MinPrecursorMz, _settings.MaxPrecursorMz);
                if (charges.Count == 0)
                    continue;
                var peptide = forms[i].ToAlphabase();
                foreach (int charge in charges)
                {
                    requests.Add(new Ms2Request(new PrecursorForm(peptide, charge), _settings.Nce, _settings.Instrument));
                    formIndex.Add(isoforms.Count);
                }
                isoforms.Add(forms[i]);
                peptides.Add(peptide);
            }
            if (requests.Count == 0)
                return new List<LibrarySpectrum>();
            _ms2Clock.Start();
            var predictions = ms2.Predict(requests);
            _ms2Clock.Stop();
            _rtClock.Start();
            double[] rtPredictions = rt.Predict(peptides);
            _rtClock.Stop();
            _buildClock.Start();
            var spectra = new LibrarySpectrum[requests.Count];
            Parallel.For(0, requests.Count, i =>
            {
                int form = formIndex[i];
                double retentionTime = LibrarySpectrumBuilder.GetRetentionTime(rtPredictions[form], _settings.RtMax, irt.Slope, irt.Intercept);
                spectra[i] = builder.Build(isoforms[form], requests[i].Precursor, predictions[i].Intensities, INTENSITY_STRIDE, retentionTime);
            });
            var built = spectra.Where(s => s != null).ToList();
            _buildClock.Stop();
            return built;
        }

        private void WriteDecoyPairs(BlibLibraryWriter blib, List<DecoyPairPlanner.Precursor> precursors)
        {
            try
            {
                var manifest = DecoyPairPlanner.ReadManifest(_settings.PairingManifest);
                var rows = DecoyPairPlanner.Plan(precursors, manifest, out int skipped);
                blib.WriteDecoyPairs(rows);
                Log(string.Format(CultureInfo.InvariantCulture, @"DecoyPairs: wrote {0} rows ({1} target/decoy pairs) from manifest {2}",
                    rows.Count, rows.Count / 2, _settings.PairingManifest));
                if (skipped > 0)
                {
                    Log(string.Format(CultureInfo.InvariantCulture,
                        @"DecoyPairs: skipped {0} pairs reusing a precursor already paired (peptides differing only by I/L), " +
                        @"where Carafe stops on a primary-key error", skipped));
                }
            }
            catch (Exception e)
            {
                // Carafe catches any DecoyPairs failure, logs it and keeps the library.
                Log(@"Failed to write DecoyPairs table: " + e.Message);
            }
        }

        /// <summary>
        /// The model folder Carafe predicts from: <c>-model_dir</c>, else the output folder.
        /// With <c>-model_dir</c> its meta.json overrides the command line.
        /// </summary>
        private CarafeModelDirectory OpenModelDirectory()
        {
            string folder = _settings.ModelDirectory ?? _settings.OutputDirectory;
            var modelDirectory = CarafeModelDirectory.Open(folder);
            if (_settings.ModelDirectory != null)
                Log(@"Use the model in the folder: " + folder + @" for spectral library generation");
            if (_settings.ApplyModelDirectoryMeta)
            {
                modelDirectory.ApplyModelDirectoryOverrides(_settings);
                Log(string.Format(CultureInfo.InvariantCulture,
                    @"From {0}: precursor m/z {1}-{2}, fragment m/z {3}-{4}, NCE {5}, rt_max {6}",
                    CarafeModelDirectory.META_FILE, _settings.MinPrecursorMz, _settings.MaxPrecursorMz, _settings.MinFragmentMz,
                    _settings.MaxFragmentMz, _settings.Nce, _settings.RtMax));
            }
            else if (_settings.ApplyTrainingRunMeta)
            {
                modelDirectory.ApplyTrainingRunOverrides(_settings);
                Log(string.Format(CultureInfo.InvariantCulture,
                    @"From the training run: precursor m/z {0}-{1}, NCE {2}, instrument {3}, rt_max {4}",
                    _settings.MinPrecursorMz, _settings.MaxPrecursorMz, _settings.Nce, _settings.Instrument, _settings.RtMax));
            }
            return modelDirectory;
        }

        private Ms2Model LoadMs2Model(CarafeModelDirectory modelDirectory, Device device)
        {
            string path = modelDirectory.GetMs2ModelPath(_settings.TrainingType);
            if (path != null)
            {
                Log(@"Using fine-tuned MS2 model " + path);
                return CarafeModelDirectory.IsSafetensors(path) ? Ms2Model.FromSafetensors(path, device) : Ms2Model.FromPthFile(path, device);
            }
            Log(@"Using the pretrained MS2 model");
            return Ms2Model.FromPretrained(OpenPretrained(), device);
        }

        private RtModel LoadRtModel(CarafeModelDirectory modelDirectory, Device device)
        {
            string path = modelDirectory.GetRtModelPath(_settings.TrainingType);
            if (path != null)
            {
                Log(@"Using fine-tuned RT model " + path);
                return CarafeModelDirectory.IsSafetensors(path) ? RtModel.FromSafetensors(path, device) : RtModel.FromPthFile(path, device);
            }
            Log(@"Using the pretrained RT model");
            return RtModel.FromPretrained(OpenPretrained(), device);
        }

        private PretrainedModels OpenPretrained()
        {
            return PretrainedModels.Open(_settings.PretrainedModels);
        }

        private void Log(string message)
        {
            _log.WriteLine(message);
            _log.Flush();
        }
    }
}
