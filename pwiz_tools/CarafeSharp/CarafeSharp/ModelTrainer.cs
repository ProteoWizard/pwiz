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

        public ModelTrainer(TrainingSettings settings, TextWriter log)
        {
            _settings = settings;
            _log = log ?? TextWriter.Null;
        }

        public FineTuneResult Result { get; private set; }

        public OspreyTrainingSetStats Stats { get; private set; }

        public void Run()
        {
            // What the run needs before hours of work: the library FASTA, any -ms2_model, the
            // device, and the pretrained models the fine-tuning starts from.
            if (_settings.Library != null && !File.Exists(_settings.Library.Database))
                throw new FileNotFoundException(@"Library FASTA (-db) not found: " + _settings.Library.Database, _settings.Library.Database);
            if (_settings.Ms2Model != null && !File.Exists(_settings.Ms2Model))
                throw new FileNotFoundException(@"MS2 model (-ms2_model) not found: " + _settings.Ms2Model, _settings.Ms2Model);
            var device = TorchDevice.Resolve(_settings.Device, out string fallback);
            if (fallback != null)
                Log(fallback);
            var pretrained = PretrainedModels.Open(_settings.PretrainedModels);
            Directory.CreateDirectory(_settings.OutputDirectory);

            var selection = TrainingExportLocator.Find(_settings.Identifications, _settings.MsFiles);
            foreach (string warning in selection.Warnings)
                Log(warning);
            var exports = new List<OspreyTrainingExport>(selection.Exports.Count);
            foreach (string path in selection.Exports)
            {
                var export = OspreyTrainingExport.Read(path);
                Log(string.Format(CultureInfo.InvariantCulture, @"Training export {0}: {1} precursors, rt_max {2}, NCE {3}, instrument {4}",
                    path, export.Records.Count, export.RtMax, export.DominantCollisionEnergy?.ToString(CultureInfo.InvariantCulture) ?? @"unknown",
                    export.InstrumentModel ?? @"unknown"));
                exports.Add(export);
            }

            var options = new OspreyTrainingSetOptions
            {
                MaxRunQ = _settings.Fdr,
                Nce = _settings.Nce,
                Instrument = _settings.Instrument,
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
            var trainingSet = OspreyTrainingSet.Build(exports, options);
            Stats = trainingSet.Stats;
            Log(@"Training data: " + trainingSet.Stats);
            Log(@"Ions masked by rule: " + string.Join(@", ", trainingSet.Stats.MaskedBy
                .OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + @" " + p.Value)));
            if (_settings.NoMasking)
                Log(@"-no_masking: training on every ion of the kept spectra");
            CarafeTrainingDirectory.Write(_settings.OutputDirectory, trainingSet.Rt, trainingSet.Ms2);

            var fineTune = new FineTuneOptions { Seed = _settings.Seed, Device = device, Ms2Model = _settings.Ms2Model };
            Result = FineTuneRun.Run(_settings.TrainRt ? trainingSet.Rt : null, _settings.TrainMs2 ? trainingSet.Ms2 : null,
                pretrained, fineTune, _settings.OutputDirectory, Log);
            CarafeModelDirectory.WriteMeta(_settings.OutputDirectory, BuildRunMeta(exports, selection.Runs, options));

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
        /// rt_max (at least <c>-rt_max</c>), MS2 scan window and isolation range. The library
        /// fragment range and count, which Carafe never sets, keep JMeta's defaults.
        /// </summary>
        internal static IReadOnlyList<CarafeRunMeta> BuildRunMeta(IReadOnlyList<OspreyTrainingExport> exports,
            IReadOnlyDictionary<string, string> runPaths, OspreyTrainingSetOptions options)
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
                    Nce = OspreyTrainingSet.GetNce(export, options.Nce),
                    MinFragmentIonMz = scanWindow.Lower,
                    MaxFragmentIonMz = scanWindow.Upper,
                    RtMax = OspreyTrainingSet.GetRtMax(export, options),
                    PrecursorMzMin = isolation.Lower,
                    PrecursorMzMax = isolation.Upper,
                });
            }
            return runs;
        }

        private void Log(string message)
        {
            _log.WriteLine(message);
            _log.Flush();
        }
    }
}
