/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using static TorchSharp.torch;

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// Loads the RT model a library predicts with: the fine-tuned one a model folder holds, else the pretrained one, of
    /// the kind asked for (AlphaPeptDeep's or Chronologer), with a Chronologer taken to minutes through the training
    /// runs' maps when the folder holds them. CarafeSharp's library prediction and Skyline's AlphaPeptDeep library
    /// builder both load through it, so a saved model predicts the same retention times in each.
    /// </summary>
    public sealed class RtPredictorLoader
    {
        /// <summary>
        /// The kind of RT model a fine-tuned RT model file is: a saved Chronologer, else AlphaPeptDeep's (a safetensors,
        /// or a Carafe checkpoint).
        /// </summary>
        public static RtModelType GetRtModelType(string rtModelPath)
        {
            return ModelFiles.IsSafetensors(rtModelPath) && ChronologerModel.IsChronologerFile(rtModelPath)
                ? Core.RtModelType.chronologer
                : Core.RtModelType.alphapeptdeep;
        }

        private readonly Func<PretrainedModels> _openPretrained;
        private readonly Device _device;

        /// <param name="openPretrained">Opens the pretrained AlphaPeptDeep models, which are needed for a pretrained
        /// AlphaPeptDeep RT model and for the peptides a Chronologer cannot encode.</param>
        /// <param name="device">Where the model runs.</param>
        public RtPredictorLoader(Func<PretrainedModels> openPretrained, Device device)
        {
            _openPretrained = openPretrained;
            _device = device;
        }

        /// <summary>The fine-tuned RT model file, or null for the pretrained model.</summary>
        public string FineTunedPath { get; set; }

        /// <summary>
        /// The kind of RT model asked for, or null for the kind <see cref="FineTunedPath"/> is, else
        /// <see cref="DefaultRtModelType"/>. A fine-tuned RT model of another kind is replaced by that kind's
        /// pretrained model.
        /// </summary>
        public RtModelType? RtModelType { get; set; }

        /// <summary>The kind of RT model when neither <see cref="RtModelType"/> nor a fine-tuned model names one.</summary>
        public RtModelType DefaultRtModelType { get; set; }

        /// <summary>The folder that may hold the training runs' maps (<see cref="ModelFiles.RT_MAPS"/>), or null.</summary>
        public string ModelDirectory { get; set; }

        /// <summary>The library's rt_max: its retention times are minutes on a gradient of this length when positive.</summary>
        public double RtMax { get; set; }

        /// <summary>Whether <see cref="RtMax"/> was given by the user (<c>-rt_max</c>), not taken from the training runs.</summary>
        public bool UserRtMax { get; set; }

        /// <summary>The training run whose minutes the library takes (<c>-rt_reference</c>), or null.</summary>
        public string RtReference { get; set; }

        /// <summary>The folder holding the Chronologer files, or null for <see cref="ChronologerFiles.DefaultDirectory"/>.</summary>
        public string ChronologerDirectory { get; set; }

        /// <summary>Receives what was loaded, or null.</summary>
        public Action<string> Log { get; set; }

        public IRtPredictor Load()
        {
            string path = FineTunedPath;
            var fineTunedType = path != null ? GetRtModelType(path) : (RtModelType?)null;
            var type = RtModelType ?? fineTunedType ?? DefaultRtModelType;
            if (fineTunedType != null && fineTunedType != type)
            {
                WriteLog(string.Format(@"-rt_model {0}: using the pretrained {0} RT model instead of the fine-tuned {1} RT model {2}",
                    type, fineTunedType, path));
                path = null;
            }
            if (type == Core.RtModelType.chronologer)
                return WithAlignment(LoadChronologer(path));
            if (RtReference != null)
                throw new InvalidOperationException(string.Format(@"--rt-reference {0} needs the Chronologer RT model, not {1}.", RtReference, type));
            if (path != null)
            {
                WriteLog(@"Using fine-tuned RT model " + path);
                return ModelFiles.IsSafetensors(path) ? RtModel.FromSafetensors(path, _device) : RtModel.FromPthFile(path, _device);
            }
            WriteLog(@"Using the pretrained RT model");
            return RtModel.FromPretrained(_openPretrained(), _device);
        }

        /// <summary>
        /// A Chronologer and the training runs' maps (<see cref="ModelFiles.RT_MAPS"/>, from <c>-rt_align kde</c>), when
        /// the model folder holds them and the library has an rt_max:
        /// <list type="bullet">
        /// <item>fine-tuned on runs of one gradient, it predicts their aligned minutes, which the library takes as they
        /// are, or with <c>-rt_reference</c> through the maps to one run's;</item>
        /// <item>predicting the hydrophobic index (pretrained, or fine-tuned on runs of different gradients), its library
        /// is in the minutes of the run <c>-rt_reference</c> names, else the runs' median, unless the runs are on
        /// different gradients (<see cref="RtAlignment.IsWide"/>), whose median is none of them: then iRT.</item>
        /// </list>
        /// Without the maps the predictor is as it is. <c>-rt_reference</c> needs them.
        /// </summary>
        private IRtPredictor WithAlignment(IRtPredictor rt)
        {
            string path = ModelDirectory != null ? Path.Combine(ModelDirectory, ModelFiles.RT_MAPS) : null;
            try
            {
                if (path == null || !File.Exists(path))
                {
                    if (RtReference != null)
                    {
                        throw new InvalidOperationException(string.Format(
                            @"--rt-reference {0} needs a Chronologer trained with --rt-align kde, whose runs' maps ({1}) its model folder holds.",
                            RtReference, ModelFiles.RT_MAPS));
                    }
                    return rt;
                }
                if (!(RtMax > 0))
                {
                    if (RtReference != null)
                    {
                        throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture,
                            @"--rt-reference {0} needs a library in minutes, which --rt-max {1} is not.", RtReference, RtMax));
                    }
                    return rt;
                }
                var alignment = RtAlignment.Read(path);
                string run = RtReference != null ? alignment.FindRun(RtReference) : null;
                if (rt.PredictsNormalizedRt && alignment.IsWide)
                {
                    WriteLog(string.Format(@"WARNING: this model predicts the median minutes of training runs on different gradients ({0}), " +
                        @"which are none of theirs.", alignment));
                }
                if (run != null)
                {
                    // A minutes model predicts the median minutes over the training normalizer, whatever this library's rt_max.
                    double trainingRtMax = double.IsNaN(alignment.RtMax) ? RtMax : alignment.RtMax;
                    var medianToRun = rt.PredictsNormalizedRt ? alignment.MedianToRunMinutes(run) : null;
                    Func<double, double> toMinutes = medianToRun != null ? rtNorm => medianToRun(rtNorm * trainingRtMax) : alignment.ToRunMinutes(run);
                    LogMapsTakeMinutes();
                    WriteLog(string.Format(@"Library RT: minutes of training run {0} (--rt-reference, {1}): {2}", run, path, alignment));
                    return new AlignedRtPredictor(rt, toMinutes, RtMax);
                }
                if (rt.PredictsNormalizedRt)
                    return rt;
                if (alignment.IsWide)
                {
                    WriteLog(string.Format(CultureInfo.InvariantCulture,
                        @"WARNING: the training runs are on different gradients ({0}), so their median minutes are none of theirs; " +
                        @"the library RT is iRT. Name a run with --rt-reference for its minutes: {1}.",
                        alignment, string.Join(@", ", alignment.Runs.Select(r => r.Run))));
                    return rt;
                }
                LogMapsTakeMinutes();
                WriteLog(@"Library RT: minutes of the training runs' median map (" + path + @"): " + alignment);
                return new AlignedRtPredictor(rt, alignment.ToMinutes, RtMax);
            }
            catch
            {
                rt.Dispose();
                throw;
            }
        }

        /// <summary>A given <c>-rt_max</c> does not scale minutes the training runs' maps give.</summary>
        private void LogMapsTakeMinutes()
        {
            if (UserRtMax)
            {
                WriteLog(string.Format(CultureInfo.InvariantCulture, @"Ignored --rt-max {0}: the library's minutes come from the training runs' maps",
                    RtMax));
            }
        }

        /// <summary>
        /// The fine-tuned Chronologer at <paramref name="path"/>, or the pretrained one when it is null, with AlphaPeptDeep's
        /// pretrained RT model for the peptides Chronologer cannot encode; the pretrained archive is needed for it.
        /// </summary>
        private IRtPredictor LoadChronologer(string path)
        {
            var files = ChronologerFiles.Open(ChronologerDirectory);
            ChronologerModel chronologer = null;
            RtModel fallback = null;
            try
            {
                if (path != null)
                {
                    WriteLog(@"Using the fine-tuned Chronologer RT model " + path);
                    chronologer = ChronologerModel.FromSafetensors(path, files, _device);
                }
                else
                {
                    WriteLog(@"Using the pretrained Chronologer RT model " + files.WeightsPath);
                    chronologer = ChronologerModel.FromFiles(files, _device);
                }
                WriteLog(@"AlphaPeptDeep's pretrained RT model predicts the peptides Chronologer cannot encode");
                fallback = RtModel.FromPretrained(_openPretrained(), _device);
                return new ChronologerRtPredictor(chronologer, fallback);
            }
            catch
            {
                chronologer?.Dispose();
                fallback?.Dispose();
                throw;
            }
        }

        private void WriteLog(string message)
        {
            Log?.Invoke(message);
        }
    }
}
