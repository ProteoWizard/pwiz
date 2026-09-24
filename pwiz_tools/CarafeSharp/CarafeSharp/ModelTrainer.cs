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
using System.Text.Json;
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

        public ModelTrainer(TrainingSettings settings, TextWriter log)
        {
            _settings = settings;
            _log = log ?? TextWriter.Null;
        }

        public FineTuneResult Result { get; private set; }

        public OspreyTrainingSetStats Stats { get; private set; }

        public void Run()
        {
            Directory.CreateDirectory(_settings.OutputDirectory);
            var paths = TrainingExportLocator.Find(_settings.Identifications, _settings.MsFiles);
            var exports = new List<OspreyTrainingExport>(paths.Count);
            foreach (string path in paths)
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

            var device = TorchDevice.Resolve(_settings.Device, out string fallback);
            if (fallback != null)
                Log(fallback);
            var fineTune = new FineTuneOptions { Seed = _settings.Seed, Device = device };
            Result = FineTuneRun.Run(_settings.TrainRt ? trainingSet.Rt : null, _settings.TrainMs2 ? trainingSet.Ms2 : null,
                PretrainedModels.Open(_settings.PretrainedModels), fineTune, _settings.OutputDirectory, Log);
            WriteMeta(exports, options);

            if (_settings.Library != null)
            {
                _settings.Library.OutputDirectory = _settings.OutputDirectory;
                new LibraryGenerator(_settings.Library, _log).Run();
            }
        }

        /// <summary>
        /// Carafe's <c>meta.json</c>, one entry per training run, which library prediction
        /// (right after training, or later through <c>-model_dir</c>) reads for the collision
        /// energy, instrument, rt_max and precursor window.
        /// </summary>
        private void WriteMeta(IReadOnlyList<OspreyTrainingExport> exports, OspreyTrainingSetOptions options)
        {
            var library = _settings.Library ?? new LibrarySettings();
            var runs = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var export in exports)
            {
                string stem = TrainingExportLocator.RunStem(export.Path);
                string msFile = _settings.MsFiles.FirstOrDefault(f =>
                    string.Equals(Path.GetFileNameWithoutExtension(f), stem, StringComparison.OrdinalIgnoreCase)) ?? stem;
                var isolation = export.IsolationRange;
                var scanWindow = export.Ms2ScanWindow ?? (library.MinFragmentMz, library.MaxFragmentMz);
                runs[msFile] = new Dictionary<string, object>
                {
                    { @"lf_frag_mz_max", library.MaxFragmentMz },
                    { @"lf_frag_mz_min", library.MinFragmentMz },
                    { @"lf_top_n_fragment_ions", library.TopFragments },
                    { @"max_fragment_ion_mz", scanWindow.Upper },
                    { @"min_fragment_ion_mz", scanWindow.Lower },
                    { @"ms_file", msFile },
                    { @"ms_instrument", options.Instrument ?? export.InstrumentModel ?? string.Empty },
                    { @"nce", options.Nce ?? export.DominantCollisionEnergy ?? library.Nce },
                    { @"precursor_ion_mz_max", isolation.Upper },
                    { @"precursor_ion_mz_min", isolation.Lower },
                    { @"rt_max", export.RtMax + options.RtMaxPadding },
                    { @"rt_min", 0.0 },
                };
            }
            File.WriteAllText(Path.Combine(_settings.OutputDirectory, ModelFiles.META),
                JsonSerializer.Serialize(runs, new JsonSerializerOptions { WriteIndented = true }));
        }

        private void Log(string message)
        {
            _log.WriteLine(message);
            _log.Flush();
        }
    }
}
