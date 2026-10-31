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
using System.Globalization;
using System.IO;
using System.Linq;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.Models;
using pwiz.CarafeSharp.Proteome;
using static TorchSharp.torch;

namespace pwiz.CarafeSharp.Tasks
{
    /// <summary>
    /// The models a library is predicted with (<see cref="LibraryGenerator"/>, from a digest or a
    /// precursor table). A saved model file is unpacked into a folder of its
    /// own, which the caller deletes; the MS2, RT and CCS models come from the model folder's
    /// fine-tuned files where it has them, else from the pretrained archive.
    /// </summary>
    internal static class LibraryModels
    {
        /// <summary>
        /// A saved model (<c>-model</c>), checked and unpacked into <paramref name="folder"/>, a
        /// temporary folder the caller deletes when the prediction is done. The training run's
        /// NCE, instrument and rt_max apply where the command line gives none; the m/z ranges stay
        /// the command line's.
        /// </summary>
        public static CarafeModelDirectory OpenModelFile(LibrarySettings settings, Action<string> log, out string folder)
        {
            var modelFile = CarafeModelFile.Open(settings.ModelFile);
            folder = Path.Combine(Path.GetTempPath(), @"CarafeSharp_model_" + Guid.NewGuid().ToString(@"N"));
            var modelDirectory = modelFile.Extract(folder);
            log(@"Use the saved model " + settings.ModelFile + @" for spectral library generation: " + modelFile.Describe());
            modelFile.ApplyPredictionDefaults(settings);
            log(string.Format(CultureInfo.InvariantCulture,
                @"Precursor m/z {0}-{1}, fragment m/z {2}-{3} from the command line; NCE {4}, instrument {5}, activation {6}, analyzer {7}, rt_max {8}",
                settings.MinPrecursorMz, settings.MaxPrecursorMz, settings.MinFragmentMz, settings.MaxFragmentMz, settings.Nce,
                settings.Instrument, settings.Activation ?? @"(none)", settings.Analyzer ?? @"(none)", settings.RtMax));
            return modelDirectory;
        }

        /// <summary>
        /// What a model folder's metadata overrides on the command line, as Carafe's
        /// <c>-model_dir</c> branch copies meta.json over it, or as a training run's values
        /// (NCE, instrument, rt_max) apply to the library predicted right after the fine-tune;
        /// nothing when <paramref name="settings"/> asks for neither.
        /// </summary>
        public static void ApplyModelDirectoryMeta(LibrarySettings settings, CarafeModelDirectory modelDirectory, Action<string> log)
        {
            if (settings.ApplyModelDirectoryMeta)
            {
                modelDirectory.ApplyModelDirectoryOverrides(settings);
                log(string.Format(CultureInfo.InvariantCulture,
                    @"From {0}: precursor m/z {1}-{2}, fragment m/z {3}-{4}, NCE {5}, rt_max {6}",
                    CarafeModelDirectory.META_FILE, settings.MinPrecursorMz, settings.MaxPrecursorMz, settings.MinFragmentMz,
                    settings.MaxFragmentMz, settings.Nce, settings.RtMax));
                var rtMaxes = modelDirectory.Runs.Select(r => r.RtMax).Distinct().ToList();
                if (rtMaxes.Count > 1)
                {
                    log(string.Format(CultureInfo.InvariantCulture,
                        @"The training runs in {0} have different rt_max ({1}): as Carafe does, the library takes the last, {2}, " +
                        @"but training normalized RT by the largest, {3}, so library RTs scale by {2}/{3}.",
                        CarafeModelDirectory.META_FILE, string.Join(@", ", rtMaxes.Select(v => v.ToString(CultureInfo.InvariantCulture))),
                        settings.RtMax, rtMaxes.Max()));
                }
            }
            else if (settings.ApplyTrainingRunMeta)
            {
                modelDirectory.ApplyTrainingRunOverrides(settings);
                log(string.Format(CultureInfo.InvariantCulture,
                    @"From the training run: precursor m/z {0}-{1}, NCE {2}, instrument {3}, rt_max {4}",
                    settings.MinPrecursorMz, settings.MaxPrecursorMz, settings.Nce, settings.Instrument, settings.RtMax));
            }
        }

        /// <summary>
        /// The MS2 model: the model folder's fine-tuned one for <paramref name="settings"/>'
        /// training type, else the pretrained one.
        /// </summary>
        public static Ms2Model LoadMs2Model(LibrarySettings settings, CarafeModelDirectory modelDirectory, Device device,
            Func<PretrainedModels> pretrained, Action<string> log)
        {
            string path = modelDirectory.GetMs2ModelPath(settings.TrainingType);
            if (path != null)
            {
                log((Path.GetFileName(path) == ModelFiles.MS2_BASE_SAFETENSORS
                        ? @"Using the base model's MS2 model, which the fine-tuned one did not beat, "
                        : @"Using fine-tuned MS2 model ") + path);
                return Ms2Model.FromFile(path, device);
            }
            log(@"Using the pretrained MS2 model");
            return Ms2Model.FromPretrained(pretrained(), device);
        }

        /// <summary>
        /// The RT model the library predicts with: <c>-rt_model</c>'s, else the one the folder's fine-tuned RT model is
        /// (a saved model's <see cref="CarafeModelFile.RtModel"/> came in as <c>-rt_model</c>'s default), else
        /// <see cref="LibrarySettings.DEFAULT_RT_MODEL"/>. A fine-tuned RT model of another kind is replaced by that
        /// kind's pretrained model.
        /// </summary>
        public static IRtPredictor LoadRtModel(LibrarySettings settings, CarafeModelDirectory modelDirectory, Device device,
            Func<PretrainedModels> pretrained, Action<string> log)
        {
            return new RtPredictorLoader(pretrained, device)
            {
                FineTunedPath = modelDirectory.GetRtModelPath(settings.TrainingType),
                RtModelType = settings.RtModelType,
                DefaultRtModelType = LibrarySettings.DEFAULT_RT_MODEL,
                ModelDirectory = modelDirectory.DirectoryPath,
                RtMax = settings.RtMax,
                UserRtMax = settings.UserRtMax,
                RtReference = settings.RtReference,
                Log = log,
            }.Load();
        }

        /// <summary>
        /// The CCS model for <c>-ccs</c>: the model folder's Carafe <c>ccs_model.pt</c> for
        /// <c>-tf all</c>, else the pretrained one (a saved CarafeSharp model holds none).
        /// </summary>
        public static CcsModel LoadCcsModel(LibrarySettings settings, CarafeModelDirectory modelDirectory, Device device,
            Func<PretrainedModels> pretrained, Action<string> log)
        {
            string path = modelDirectory.GetCcsModelPath(settings.TrainingType);
            if (path != null)
            {
                log(@"Using fine-tuned CCS model " + path + @" for the ion mobility (1/K0)");
                return CcsModel.FromPthFile(path, device);
            }
            log(@"Using the pretrained CCS model for the ion mobility (1/K0)");
            return CcsModel.FromPretrained(pretrained(), device);
        }
    }
}
