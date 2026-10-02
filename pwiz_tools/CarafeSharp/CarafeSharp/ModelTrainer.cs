/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/java/ai/AIGear.java
 *   (the -ms training branch: training data, then ai.py, then the library)
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
using System.Globalization;
using System.IO;
using System.Linq;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.IO;
using pwiz.CarafeSharp.Models;
using pwiz.CarafeSharp.Proteome;
using pwiz.CarafeSharp.Training;

namespace pwiz.CarafeSharp
{
    /// <summary>
    /// A Carafe training run on Osprey's results: reads each run's training export, builds the
    /// RT and MS2 training rows with Carafe's rules (<see cref="OspreyTrainingSet"/>), writes
    /// them as Carafe's training tables, fine-tunes (<see cref="FineTuneRun"/>), writes Carafe's
    /// <c>meta.json</c>, and predicts the library from <c>-db</c> with the fine-tuned models.
    /// </summary>
    internal sealed class ModelTrainer
    {
        private readonly TrainingSettings _settings;
        private readonly TextWriter _log;
        private readonly Func<string, PretrainedModels> _openPretrained;

        public ModelTrainer(TrainingSettings settings, TextWriter log)
            : this(settings, log, PretrainedModels.Open)
        {
        }

        /// <summary>A trainer whose pretrained models come from <paramref name="openPretrained"/>, given <c>-pretrained</c>; for tests.</summary>
        internal ModelTrainer(TrainingSettings settings, TextWriter log, Func<string, PretrainedModels> openPretrained)
        {
            _settings = settings;
            _log = log ?? TextWriter.Null;
            _openPretrained = openPretrained;
        }

        public FineTuneResult Result { get; private set; }

        /// <summary>Changes the fine-tuning options before the run; for tests.</summary>
        internal Action<FineTuneOptions> ConfigureFineTune { get; set; }

        public OspreyTrainingSetStats Stats { get; private set; }

        public void Run()
        {
            // What the run needs before hours of work: the library FASTA, any -ms2_model or
            // saved model (-model, its entries checked), the device, and the pretrained models.
            if (_settings.Library != null && !File.Exists(_settings.Library.Database))
                throw new FileNotFoundException(@"Library FASTA (-db) not found: " + _settings.Library.Database, _settings.Library.Database);
            if (_settings.Ms2Model != null && !File.Exists(_settings.Ms2Model))
                throw new FileNotFoundException(@"MS2 model (-ms2_model) not found: " + _settings.Ms2Model, _settings.Ms2Model);
            var baseModel = _settings.BaseModel != null ? CarafeModelFile.Open(_settings.BaseModel) : null;
            var device = TorchDevice.Resolve(_settings.Device, out string fallback);
            if (fallback != null)
                Log(fallback);
            var pretrained = _openPretrained(_settings.PretrainedModels);
            Directory.CreateDirectory(_settings.OutputDirectory);

            var selection = TrainingExportLocator.Find(_settings.Identifications, _settings.MsFiles);
            foreach (string warning in selection.Warnings)
                Log(warning);
            var exports = new List<OspreyTrainingExport>(selection.Exports.Count);
            foreach (string path in selection.Exports)
            {
                var export = OspreyTrainingExport.Read(path);
                Log(string.Format(CultureInfo.InvariantCulture, @"Training export {0}: {1} precursors, rt_max {2}, NCE {3}, instrument {4}, run q from pass {5}",
                    path, export.Records.Count, export.RtMax, export.DominantCollisionEnergy?.ToString(CultureInfo.InvariantCulture) ?? @"unknown",
                    export.InstrumentModel ?? @"unknown", export.RunQPass ?? @"unknown"));
                if (export.RunQPass == @"1")
                    Log(string.Format(@"WARNING: {0} took its run q-values from Osprey's first pass; the run had no second pass.", path));
                exports.Add(export);
            }

            var options = new OspreyTrainingSetOptions
            {
                MaxRunQ = _settings.Fdr,
                Nce = _settings.Nce,
                Instrument = _settings.Instrument,
                Activation = _settings.Activation,
                Analyzer = _settings.Analyzer,
                RtMax = _settings.RtMax,
                UseMasking = !_settings.NoMasking,
                Masking = new OspreyMaskingSettings
                {
                    MinCorrelation = _settings.MinCorrelation,
                    LowOrdinalB = _settings.LowOrdinalB,
                    LowOrdinalY = _settings.LowOrdinalY,
                    MinFragmentOrdinal = _settings.MinFragmentOrdinal,
                    MinMatchedIons = _settings.MinMatchedIons,
                    MinValidIons = _settings.MinValidIons,
                    RequireTopIonValid = _settings.RequireTopIonValid,
                },
            };
            var fineTune = new FineTuneOptions
            {
                Seed = _settings.Seed, Device = device, Ms2Model = _settings.Ms2Model, RtModelType = _settings.RtModelType,
            };
            ConfigureFineTune?.Invoke(fineTune);
            OspreyTrainingSet trainingSet;
            string baseFolder = null;
            try
            {
                if (baseModel != null)
                {
                    // The models a library from the saved model predicts with: those it holds, else the pretrained ones.
                    baseFolder = Path.Combine(Path.GetTempPath(), @"CarafeSharp_base_" + Guid.NewGuid().ToString(@"N"));
                    var baseDirectory = baseModel.Extract(baseFolder);
                    fineTune.Ms2Model = baseDirectory.GetMs2ModelPath(@"all");
                    fineTune.RtModel = baseDirectory.GetRtModelPath(@"all");
                    // A saved Chronologer is fine-tuned further as a Chronologer.
                    if (fineTune.RtModel != null && CarafeModelDirectory.IsSafetensors(fineTune.RtModel) &&
                        ChronologerModel.IsChronologerFile(fineTune.RtModel))
                    {
                        fineTune.RtModelType = RtModelType.chronologer;
                    }
                    fineTune.KeepMs2Start = true;
                    Log(@"Fine-tune the saved model " + _settings.BaseModel + @" further: " + baseModel.Describe());
                }
                trainingSet = BuildTrainingSet(exports, options, fineTune, pretrained);
                CarafeTrainingDirectory.Write(_settings.OutputDirectory, trainingSet.Rt, trainingSet.Ms2);
                Result = FineTuneRun.Run(_settings.TrainRt ? trainingSet.Rt : null, _settings.TrainMs2 ? trainingSet.Ms2 : null,
                    pretrained, fineTune, _settings.OutputDirectory, Log);
            }
            finally
            {
                if (baseFolder != null && Directory.Exists(baseFolder))
                    Directory.Delete(baseFolder, true);
            }
            CarafeModelDirectory.WriteMeta(_settings.OutputDirectory, BuildRunMeta(exports, selection.Runs, options, trainingSet));

            // The fine-tuned model as one file, to predict later libraries from with -model.
            // The acquisition columns of the MS2 model it holds; the pretrained model has the default ones.
            string modelFile = Path.Combine(_settings.OutputDirectory, CarafeModelFile.DEFAULT_FILE_NAME);
            var trained = CarafeModelDirectory.Open(_settings.OutputDirectory, true);
            string ms2File = trained.GetMs2ModelPath(_settings.TrainingType);
            var acquisition = ms2File != null && CarafeModelDirectory.IsSafetensors(ms2File)
                ? AcquisitionVocabulary.FromMetadata(StateDict.ReadSafetensorsMetadata(ms2File)) ?? AcquisitionVocabulary.DEFAULT
                : AcquisitionVocabulary.DEFAULT;
            var saved = CarafeModelFile.Write(modelFile, trained, _settings.TrainingType, pretrained?.Sha256, _settings.Ms2Model,
                BuildTrainingDescription(exports, selection.Runs, options, trainingSet, _settings), acquisition, baseModel);
            Log(@"Saved the fine-tuned model " + modelFile + @": " + saved.Describe());

            if (_settings.Library != null)
            {
                // The models this run wrote, not checkpoints an earlier Carafe run left in -o.
                _settings.Library.OutputDirectory = _settings.OutputDirectory;
                _settings.Library.PreferSafetensors = true;
                new LibraryGenerator(_settings.Library, _log, pretrained).Run();
            }
        }

        /// <summary>
        /// Carafe's <c>meta.json</c> entries, one per training run, which library prediction
        /// (right after training, or later through <c>-model_dir</c>) reads for the collision
        /// energy, instrument, rt_max and precursor window. Each holds what Carafe's training run
        /// records (AIGear, the training data loop): the run's path as <paramref name="runPaths"/>
        /// keys it (its stem without <c>-ms</c>), the instrument detected in the run (empty when
        /// Carafe recognizes none; not <c>-ms_instrument</c>), the run's collision energy, its
        /// rt_max (at least <c>-rt_max</c>), MS2 scan window and isolation range. The scan window is
        /// the export's measured m/z range, so it can differ from the declared one Carafe records
        /// by a fraction of a Th; prediction does not read it. The library fragment range and
        /// count, which Carafe never sets, keep JMeta's defaults.
        /// </summary>
        internal static IReadOnlyList<CarafeRunMeta> BuildRunMeta(IReadOnlyList<OspreyTrainingExport> exports,
            IReadOnlyDictionary<string, string> runPaths, OspreyTrainingSetOptions options, OspreyTrainingSet trainingSet)
        {
            var runs = new List<CarafeRunMeta>();
            foreach (var export in exports)
            {
                string stem = TrainingExportLocator.RunStem(export.Path);
                var isolation = export.IsolationRange;
                var scanWindow = export.Ms2ScanWindow ?? (CarafeRunMeta.DEFAULT_MIN_FRAGMENT_ION_MZ, CarafeRunMeta.DEFAULT_MAX_FRAGMENT_ION_MZ);
                runs.Add(new CarafeRunMeta
                {
                    MsFile = runPaths.TryGetValue(stem, out string path) ? path : stem,
                    MsInstrument = OspreyTrainingSet.GetCarafeInstrument(export.InstrumentModel) ?? string.Empty,
                    Activation = OspreyTrainingSet.GetActivation(export, options.Activation),
                    Analyzer = OspreyTrainingSet.GetAnalyzer(export, options.Analyzer),
                    Nce = trainingSet.GetCollisionEnergy(export).Nce,
                    MinFragmentIonMz = scanWindow.Lower,
                    MaxFragmentIonMz = scanWindow.Upper,
                    RtMax = OspreyTrainingSet.GetRtMax(export, options),
                    PrecursorMzMin = isolation.Lower,
                    PrecursorMzMax = isolation.Upper,
                });
            }
            return runs;
        }

        /// <summary>
        /// What the models were trained on, saved in the model file for a user choosing among
        /// models: the settings, the training data's size, charges, peptide lengths and
        /// modifications, and each run's acquisition from its export footer.
        /// </summary>
        internal static CarafeModelTraining BuildTrainingDescription(IReadOnlyList<OspreyTrainingExport> exports,
            IReadOnlyDictionary<string, string> runPaths, OspreyTrainingSetOptions options, OspreyTrainingSet trainingSet,
            TrainingSettings settings)
        {
            var forms = new Dictionary<string, PeptideForm>(StringComparer.Ordinal);
            foreach (var form in trainingSet.Rt.Select(r => r.Peptide).Concat(trainingSet.Ms2.Select(m => m.Precursor.Peptide)))
                forms[form.Sequence + @"|" + string.Join(@";", form.ModNames) + @"|" + string.Join(@";", form.ModSites)] = form;
            var runs = new List<CarafeModelTrainingRun>();
            foreach (var export in exports)
            {
                string stem = TrainingExportLocator.RunStem(export.Path);
                var scanWindow = export.Ms2ScanWindow;
                runs.Add(new CarafeModelTrainingRun
                {
                    Run = Footer(export, @"osprey.file_name") ?? stem,
                    MsFile = runPaths.TryGetValue(stem, out string path) ? path : stem,
                    InstrumentVendor = Footer(export, @"osprey.instrument_vendor"),
                    InstrumentModel = export.InstrumentModel,
                    Instrument = settings.Instrument ?? OspreyTrainingSet.GetCarafeInstrument(export.InstrumentModel) ?? string.Empty,
                    Activation = OspreyTrainingSet.GetActivation(export, options.Activation),
                    Analyzer = OspreyTrainingSet.GetAnalyzer(export, options.Analyzer),
                    Nce = trainingSet.GetCollisionEnergy(export).Nce,
                    NceSource = trainingSet.GetCollisionEnergy(export).Source,
                    DissociationMethods = export.DissociationMethods,
                    CollisionEnergies = export.CollisionEnergies,
                    CollisionEnergyUnit = trainingSet.GetCollisionEnergy(export).Unit,
                    Ms2MassAnalyzers = export.Ms2MassAnalyzers,
                    RtMin = FooterNumber(export, @"osprey.rt_min"),
                    RtMax = FooterNumber(export, @"osprey.rt_max"),
                    IsolationMzMin = FooterNumber(export, @"osprey.isolation_mz_min"),
                    IsolationMzMax = FooterNumber(export, @"osprey.isolation_mz_max"),
                    Ms2MzMin = scanWindow?.Lower,
                    Ms2MzMax = scanWindow?.Upper,
                    FragmentTolerance = FooterNumber(export, @"osprey.fragment_tolerance"),
                    FragmentToleranceUnit = Footer(export, @"osprey.fragment_tolerance_unit"),
                    Precursors = export.Records.Count,
                    PrecursorCharges = export.Records.GroupBy(r => r.Charge).ToDictionary(g => g.Key, g => g.Count()),
                    RunQPass = export.RunQPass,
                    MaxQ = FooterNumber(export, @"osprey.training_export.max_q"),
                    OspreyVersion = Footer(export, @"osprey.version"),
                    SearchHash = Footer(export, OspreyTrainingExport.SEARCH_HASH_KEY),
                    LibraryHash = Footer(export, OspreyTrainingExport.LIBRARY_HASH_KEY),
                });
            }
            return new CarafeModelTraining
            {
                Fdr = settings.Fdr,
                MinCorrelation = settings.MinCorrelation,
                Masking = !settings.NoMasking,
                Seed = settings.Seed,
                Ms2Spectra = trainingSet.Ms2.Count,
                RtPeptideForms = trainingSet.Rt.Count,
                Ms2Charges = trainingSet.Ms2.GroupBy(m => m.Precursor.Charge).ToDictionary(g => g.Key, g => g.Count()),
                MinPeptideLength = forms.Count > 0 ? forms.Values.Min(f => f.Sequence.Length) : 0,
                MaxPeptideLength = forms.Count > 0 ? forms.Values.Max(f => f.Sequence.Length) : 0,
                Modifications = forms.Values.SelectMany(f => f.ModNames.Distinct())
                    .GroupBy(name => name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal),
                Runs = runs,
            };
        }

        /// <summary>
        /// The training set, logged with what it kept and why. The NCE of a run whose collision
        /// energy is in eV is calibrated on its spectra by the MS2 model the fine-tune starts
        /// from, loaded only when such a run needs it.
        /// </summary>
        private OspreyTrainingSet BuildTrainingSet(IReadOnlyList<OspreyTrainingExport> exports, OspreyTrainingSetOptions options,
            FineTuneOptions fineTune, PretrainedModels pretrained)
        {
            Ms2Model startModel = null;
            try
            {
                options.CalibrateNce = rows =>
                {
                    startModel ??= fineTune.Ms2Model != null
                        ? Ms2Model.FromFile(fineTune.Ms2Model, fineTune.Device)
                        : Ms2Model.FromPretrained(pretrained, fineTune.Device);
                    return NceCalibration.Calibrate(startModel, rows);
                };
                var trainingSet = OspreyTrainingSet.Build(exports, options);
                Stats = trainingSet.Stats;
                Log(@"Training data: " + trainingSet.Stats);
                Log(@"Ions masked by rule: " + string.Join(@", ", trainingSet.Stats.MaskedBy
                    .OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + @" " + p.Value)));
                if (_settings.NoMasking)
                    Log(@"-no_masking: training on every ion of the kept spectra");
                foreach (var export in exports)
                {
                    var energy = trainingSet.GetCollisionEnergy(export);
                    Log(@"Collision energy of " + export.Path + @": " + energy);
                    if (energy.Calibration != null && energy.Calibration.AtLimit)
                    {
                        Log(string.Format(CultureInfo.InvariantCulture,
                            @"WARNING: {0} calibrated at NCE {1}, an end of the range scored ({2}-{3}); give its NCE with -nce if another fits better.",
                            export.Path, energy.Nce, NceCalibration.MIN_NCE, NceCalibration.MAX_NCE));
                    }
                }
                return trainingSet;
            }
            finally
            {
                startModel?.Dispose();
            }
        }

        private static string Footer(OspreyTrainingExport export, string key)
        {
            return export.Metadata.TryGetValue(key, out string value) && !string.IsNullOrEmpty(value) ? value : null;
        }

        private static double? FooterNumber(OspreyTrainingExport export, string key)
        {
            string text = Footer(export, key);
            return text != null && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : null;
        }

        private void Log(string message)
        {
            _log.WriteLine(message);
            _log.Flush();
        }
    }
}
