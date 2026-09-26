/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) main.java.ai.AIGear (main, the -model_dir
 *   branch, and get_ms2_matches_diann's meta handling), main.java.ai.JMeta and
 *   src/main/resources/py/v2/ai_pred.py (predict_ms2, predict_rt)
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
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using pwiz.CarafeSharp.Core;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// A Carafe model folder: the fine-tuned <c>ms2_model.pt</c> and <c>rt_model.pt</c>,
    /// <c>model_evaluation_metrics.json</c>, and the <c>meta.json</c> training run summary.
    /// Carafe's library prediction picks its models from such a folder as its Python does:
    /// <list type="bullet">
    /// <item>MS2: the fine-tuned model only when the metrics say
    /// <c>ms2.use_finetuned_for_prediction</c> and the file exists, else the pretrained one.</item>
    /// <item>RT: the fine-tuned model whenever the file exists.</item>
    /// <item><c>-tf rt</c> takes the MS2 model pretrained and <c>-tf ms2</c> the RT model;
    /// a <c>-tf</c> other than all, rt, ms2 or test takes both pretrained.</item>
    /// <item>A fine-tuned model is Carafe's checkpoint, else CarafeSharp's safetensors; the
    /// library a training run predicts takes the safetensors that run wrote first.</item>
    /// </list>
    /// </summary>
    public sealed class CarafeModelDirectory
    {
        public const string MS2_MODEL_FILE = ModelFiles.MS2_CHECKPOINT;
        public const string RT_MODEL_FILE = ModelFiles.RT_CHECKPOINT;
        public const string METRICS_FILE = ModelFiles.METRICS;
        public const string META_FILE = ModelFiles.META;

        /// <summary>The warning for a folder holding both a model's checkpoint and its safetensors: the one read, then the other.</summary>
        public const string BOTH_MODELS_WARNING_FORMAT = @"WARNING: both {0} and {1} are in the model folder; the fine-tuned model is read from {0}.";

        /// <summary>
        /// Opens a model folder. A missing or unreadable metrics file counts as no metrics, as
        /// in Carafe's Python; meta.json is read when present.
        /// </summary>
        /// <param name="directory">The folder.</param>
        /// <param name="preferSafetensors">
        /// Take CarafeSharp's safetensors models over Carafe's checkpoints beside them, as the
        /// library after a training run does (<see cref="LibrarySettings.PreferSafetensors"/>).
        /// </param>
        public static CarafeModelDirectory Open(string directory, bool preferSafetensors = false)
        {
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException(@"Model folder not found: " + directory);
            return new CarafeModelDirectory(directory, preferSafetensors);
        }

        /// <summary>
        /// Writes <paramref name="runs"/> as <paramref name="directory"/>'s meta.json, each keyed
        /// by its <see cref="CarafeRunMeta.MsFile"/>, as Carafe's training run writes it.
        /// </summary>
        public static void WriteMeta(string directory, IEnumerable<CarafeRunMeta> runs)
        {
            var json = new Dictionary<string, IReadOnlyDictionary<string, object>>(StringComparer.Ordinal);
            foreach (var run in runs)
                json[run.MsFile] = run.ToJson();
            File.WriteAllText(Path.Combine(directory, META_FILE), JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }));
        }

        private readonly bool _preferSafetensors;
        private readonly List<string> _warnings = new List<string>();

        private CarafeModelDirectory(string directory, bool preferSafetensors)
        {
            DirectoryPath = directory;
            _preferSafetensors = preferSafetensors;
            WarnIfBoth(MS2_MODEL_FILE, ModelFiles.MS2_SAFETENSORS);
            WarnIfBoth(RT_MODEL_FILE, ModelFiles.RT_SAFETENSORS);
            UseFineTunedMs2 = ReadUseFineTunedMs2(Path.Combine(directory, METRICS_FILE)) && File.Exists(Ms2ModelPath);
            string metaPath = Path.Combine(directory, META_FILE);
            Runs = File.Exists(metaPath) ? ReadMeta(metaPath) : new List<CarafeRunMeta>();
        }

        public string DirectoryPath { get; }

        /// <summary>Warnings about the folder, for the log: a model with both a checkpoint and safetensors, naming the one used.</summary>
        public IReadOnlyList<string> Warnings
        {
            get { return _warnings; }
        }

        /// <summary>
        /// The fine-tuned MS2 model: Carafe's checkpoint, else CarafeSharp's safetensors, or the
        /// other way round when opened to prefer safetensors.
        /// </summary>
        public string Ms2ModelPath
        {
            get { return ModelPath(MS2_MODEL_FILE, ModelFiles.MS2_SAFETENSORS); }
        }

        /// <summary>The fine-tuned RT model, chosen as <see cref="Ms2ModelPath"/> is.</summary>
        public string RtModelPath
        {
            get { return ModelPath(RT_MODEL_FILE, ModelFiles.RT_SAFETENSORS); }
        }

        /// <summary>True for a CarafeSharp safetensors model, false for a Carafe PyTorch checkpoint.</summary>
        public static bool IsSafetensors(string modelPath)
        {
            return string.Equals(Path.GetExtension(modelPath), @".safetensors", StringComparison.OrdinalIgnoreCase);
        }

        private string ModelPath(string checkpoint, string safetensors)
        {
            string first = Path.Combine(DirectoryPath, _preferSafetensors ? safetensors : checkpoint);
            string second = Path.Combine(DirectoryPath, _preferSafetensors ? checkpoint : safetensors);
            return !File.Exists(first) && File.Exists(second) ? second : first;
        }

        /// <summary>A warning naming the file used when a model has both a checkpoint and safetensors.</summary>
        private void WarnIfBoth(string checkpoint, string safetensors)
        {
            string checkpointPath = Path.Combine(DirectoryPath, checkpoint);
            string safetensorsPath = Path.Combine(DirectoryPath, safetensors);
            if (!File.Exists(checkpointPath) || !File.Exists(safetensorsPath))
                return;
            string used = ModelPath(checkpoint, safetensors);
            _warnings.Add(string.Format(BOTH_MODELS_WARNING_FORMAT, used, used == checkpointPath ? safetensorsPath : checkpointPath));
        }

        /// <summary>The metrics say to predict with the fine-tuned MS2 model, and it exists.</summary>
        public bool UseFineTunedMs2 { get; }

        public bool HasRtModel
        {
            get { return File.Exists(RtModelPath); }
        }

        /// <summary>The meta.json entries, one per training MS file, in Java HashMap order.</summary>
        public IReadOnlyList<CarafeRunMeta> Runs { get; }

        /// <summary>The fine-tuned MS2 model to predict with for <paramref name="trainingType"/>, or null for the pretrained one.</summary>
        public string GetMs2ModelPath(string trainingType)
        {
            return UseFineTunedMs2 && (IsType(trainingType, @"all") || IsType(trainingType, @"ms2") || IsType(trainingType, @"test"))
                ? Ms2ModelPath
                : null;
        }

        /// <summary>The fine-tuned RT model to predict with for <paramref name="trainingType"/>, or null for the pretrained one.</summary>
        public string GetRtModelPath(string trainingType)
        {
            return HasRtModel && (IsType(trainingType, @"all") || IsType(trainingType, @"rt") || IsType(trainingType, @"test"))
                ? RtModelPath
                : null;
        }

        /// <summary>
        /// What Carafe's <c>-model_dir</c> branch copies from meta.json over the command line,
        /// each training file in turn so the last one wins: the library fragment m/z range, the
        /// NCE, the instrument (unless <c>-ms_instrument</c> was given), rt_max, and a precursor
        /// m/z window of the training isolation range minus 0.5 at BOTH ends. The upper bound is
        /// Carafe's bug (its training run uses +0.5), kept so the libraries agree.
        /// </summary>
        public void ApplyModelDirectoryOverrides(LibrarySettings settings)
        {
            foreach (var run in Runs)
            {
                settings.MaxFragmentMz = run.LfFragMzMax;
                settings.MinFragmentMz = run.LfFragMzMin;
                if (!settings.UserInstrument)
                    settings.Instrument = run.MsInstrument;
                settings.Nce = run.Nce;
                settings.RtMax = run.RtMax;
                settings.MinPrecursorMz = run.PrecursorMzMin - 0.5;
                settings.MaxPrecursorMz = run.PrecursorMzMax - 0.5;
            }
        }

        /// <summary>
        /// The state Carafe's training run (<c>-ms</c>) leaves for the library it predicts right
        /// after training: the precursor window of the training isolation range widened by 0.5
        /// at each end (the union over files when there are several), the last file's NCE and
        /// non-empty instrument, and the largest rt_max. The fragment m/z range stays the
        /// command line's.
        /// </summary>
        public void ApplyTrainingRunOverrides(LibrarySettings settings)
        {
            if (Runs.Count == 0)
                return;
            double minMz = double.PositiveInfinity;
            double maxMz = 0;
            foreach (var run in Runs)
            {
                minMz = Math.Min(minMz, run.PrecursorMzMin - 0.5);
                maxMz = Math.Max(maxMz, run.PrecursorMzMax + 0.5);
                settings.Nce = run.Nce;
                if (!settings.UserInstrument && !string.IsNullOrEmpty(run.MsInstrument))
                    settings.Instrument = run.MsInstrument;
                if (run.RtMax > settings.RtMax)
                    settings.RtMax = run.RtMax;
            }
            settings.MinPrecursorMz = minMz;
            settings.MaxPrecursorMz = maxMz;
        }

        private static bool IsType(string trainingType, string value)
        {
            return string.Equals(trainingType, value, StringComparison.Ordinal);
        }

        /// <summary>Python's <c>bool(metrics.get("ms2", {}).get("use_finetuned_for_prediction", False))</c>.</summary>
        private static bool ReadUseFineTunedMs2(string path)
        {
            if (!File.Exists(path))
                return false;
            try
            {
                using (var json = JsonDocument.Parse(File.ReadAllText(path)))
                {
                    if (json.RootElement.ValueKind != JsonValueKind.Object ||
                        !json.RootElement.TryGetProperty(ModelFiles.METRICS_MS2, out var ms2) || ms2.ValueKind != JsonValueKind.Object ||
                        !ms2.TryGetProperty(ModelFiles.METRICS_USE_FINETUNED, out var use))
                    {
                        return false;
                    }
                    return IsTruthy(use);
                }
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool IsTruthy(JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.Number:
                    return value.GetDouble() != 0;
                case JsonValueKind.String:
                    return value.GetString()?.Length > 0;
                case JsonValueKind.Array:
                    return value.GetArrayLength() > 0;
                case JsonValueKind.Object:
                    return value.EnumerateObject().Any();
                default:
                    return false;
            }
        }

        /// <summary>
        /// Reads meta.json, which Carafe writes with raw Windows paths as keys: a backslash that
        /// does not start a JSON escape is read as a literal one, as Carafe's lenient parser
        /// reads it. Entries come back in the order Carafe's HashMap iterates them.
        /// </summary>
        private static List<CarafeRunMeta> ReadMeta(string path)
        {
            var runs = new Dictionary<string, CarafeRunMeta>(StringComparer.Ordinal);
            var keys = new List<string>();
            using (var json = JsonDocument.Parse(EscapeStrayBackslashes(File.ReadAllText(path))))
            {
                foreach (var property in json.RootElement.EnumerateObject())
                {
                    if (!runs.ContainsKey(property.Name))
                        keys.Add(property.Name);
                    runs[property.Name] = CarafeRunMeta.FromJson(property.Value);
                }
            }
            return JavaHashOrder.OrderStringKeys(keys).Select(k => runs[k]).ToList();
        }

        private static string EscapeStrayBackslashes(string text)
        {
            var builder = new StringBuilder(text.Length + 16);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                builder.Append(c);
                if (c != '\\')
                    continue;
                char next = i + 1 < text.Length ? text[i + 1] : '\0';
                if (@"""\/bfnrtu".IndexOf(next) >= 0)
                    builder.Append(text[++i]);
                else
                    builder.Append('\\');
            }
            return builder.ToString();
        }
    }
}
