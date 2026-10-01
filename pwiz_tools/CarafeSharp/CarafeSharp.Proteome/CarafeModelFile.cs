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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using pwiz.CarafeSharp.Core;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// A fine-tuned model saved as one file (<c>.carafemodel</c>), to predict libraries of any
    /// peptides from later with <c>-model</c>, without training again. It is a zip holding:
    /// <list type="bullet">
    /// <item><c>manifest.json</c>: the format, what wrote it and when, which models were
    /// fine-tuned and which are used, the pretrained archive the training started from, what the
    /// models were trained on (<see cref="CarafeModelTraining"/>, for a user choosing a model), the
    /// NCE, instrument and rt_max a library takes unless its command line gives its own, and every
    /// other entry's SHA-256.</item>
    /// <item>The fine-tuned models the training chose to predict with: <c>ms2.safetensors</c>
    /// when the fine-tuned MS2 model beat the pretrained one, <c>rt.safetensors</c> when the RT
    /// model was fine-tuned. A model not in the file is the pretrained one.</item>
    /// <item><c>model_evaluation_metrics.json</c> and <c>meta.json</c>, as the training run wrote them.</item>
    /// </list>
    /// The format is described for other readers (Skyline) in docs/06-saved-models.md.
    /// </summary>
    public sealed class CarafeModelFile
    {
        public const string EXTENSION = @".carafemodel";
        public const string DEFAULT_FILE_NAME = @"carafe_fine_tuned_model" + EXTENSION;
        public const string MANIFEST_ENTRY = @"manifest.json";
        public const string FORMAT = @"carafemodel-1";

        /// <summary>
        /// Saves the models <paramref name="trained"/> predicts with for <paramref name="trainingType"/>
        /// into <paramref name="path"/>, with its metrics and meta.json. The file is written under a
        /// temporary name and renamed, so a failed write leaves no partial model file.
        /// </summary>
        /// <param name="path">The file to write.</param>
        /// <param name="trained">The training run's output folder, opened to prefer the safetensors it wrote.</param>
        /// <param name="trainingType">The training run's <c>-tf</c>.</param>
        /// <param name="pretrainedSha256">The SHA-256 of the pretrained archive the training started from, or null.</param>
        /// <param name="ms2StartModel">The <c>-ms2_model</c> the MS2 fine-tune started from instead, or null.</param>
        /// <param name="training">What the models were trained on, or null; its held-out metrics are read from the folder here.</param>
        /// <param name="acquisition">The activations and analyzers the MS2 model has columns for, or null when unknown.</param>
        public static CarafeModelFile Write(string path, CarafeModelDirectory trained, string trainingType, string pretrainedSha256,
            string ms2StartModel, CarafeModelTraining training, AcquisitionVocabulary acquisition)
        {
            // Only the safetensors this run wrote: a Carafe checkpoint left in the folder is not its model.
            string ms2 = SafetensorsOrNull(trained.GetMs2ModelPath(trainingType));
            string rt = SafetensorsOrNull(trained.GetRtModelPath(trainingType));
            var sources = new List<(string Entry, string Path)>();
            if (ms2 != null)
                sources.Add((ModelFiles.MS2_SAFETENSORS, ms2));
            if (rt != null)
                sources.Add((ModelFiles.RT_SAFETENSORS, rt));
            foreach (string name in new[] { ModelFiles.METRICS, ModelFiles.META })
            {
                string file = System.IO.Path.Combine(trained.DirectoryPath, name);
                if (File.Exists(file))
                    sources.Add((name, file));
            }

            string metrics = System.IO.Path.Combine(trained.DirectoryPath, ModelFiles.METRICS);
            if (training != null && File.Exists(metrics))
                training.HeldOutMetrics = ReadMetrics(metrics);

            var runs = trained.Runs;
            var model = new CarafeModelFile
            {
                Path = path,
                Format = FORMAT,
                Creator = @"CarafeSharp " + GetVersion(),
                Created = DateTime.UtcNow.ToString(@"o", CultureInfo.InvariantCulture),
                TrainingType = trainingType,
                Ms2FineTuned = IsTrained(trainingType, @"ms2"),
                Ms2Used = ms2 != null,
                RtFineTuned = IsTrained(trainingType, @"rt"),
                RtUsed = rt != null,
                PretrainedSha256 = pretrainedSha256,
                Ms2StartModel = ms2StartModel == null ? null : System.IO.Path.GetFileName(ms2StartModel),
                Runs = runs,
                Training = training,
                Acquisition = acquisition,
                Activation = runs.Select(r => r.Activation).LastOrDefault(a => a != null),
                Analyzer = runs.Select(r => r.Analyzer).LastOrDefault(a => a != null),
                // As the library right after training takes them (CarafeModelDirectory.ApplyTrainingRunOverrides):
                // the last run's NCE and non-empty instrument, the largest rt_max.
                Nce = runs.Count > 0 ? runs[runs.Count - 1].Nce : LibrarySettings.DEFAULT_NCE,
                Instrument = runs.Select(r => r.MsInstrument).LastOrDefault(i => !string.IsNullOrEmpty(i)),
                RtMax = runs.Count > 0 ? runs.Max(r => r.RtMax) : 0,
                Entries = sources.ToDictionary(s => s.Entry, s => Sha256(File.ReadAllBytes(s.Path)), StringComparer.Ordinal),
            };

            string temp = path + @".tmp";
            if (File.Exists(temp))
                File.Delete(temp);
            using (var zip = ZipFile.Open(temp, ZipArchiveMode.Create))
            {
                using (var stream = zip.CreateEntry(MANIFEST_ENTRY).Open())
                    model.WriteManifest(stream);
                foreach (var source in sources)
                    zip.CreateEntryFromFile(source.Path, source.Entry, CompressionLevel.Optimal);
            }
            File.Move(temp, path, true);
            return model;
        }

        /// <summary>
        /// Opens a saved model, checking its format and every entry's SHA-256, so a damaged or
        /// foreign file fails here, before anything is predicted.
        /// </summary>
        public static CarafeModelFile Open(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(@"Model file (-model) not found: " + path, path);
            try
            {
                using (var zip = ZipFile.OpenRead(path))
                {
                    var manifestEntry = zip.GetEntry(MANIFEST_ENTRY) ??
                                        throw new InvalidDataException(string.Format(@"{0} is not a CarafeSharp model file: it has no {1}.", path, MANIFEST_ENTRY));
                    CarafeModelFile model;
                    using (var stream = manifestEntry.Open())
                        model = ReadManifest(path, stream);
                    foreach (var pair in model.Entries)
                    {
                        var entry = zip.GetEntry(pair.Key) ??
                                    throw new InvalidDataException(string.Format(@"{0} lists {1}, which it does not hold.", path, pair.Key));
                        if (!string.Equals(Sha256(ReadAll(entry)), pair.Value, StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException(string.Format(@"{0} is damaged: {1} does not match its SHA-256.", path, pair.Key));
                    }
                    var metaEntry = model.Entries.ContainsKey(ModelFiles.META) ? zip.GetEntry(ModelFiles.META) : null;
                    if (metaEntry != null)
                    {
                        using (var reader = new StreamReader(metaEntry.Open()))
                            model.Runs = CarafeModelDirectory.ReadMetaText(reader.ReadToEnd());
                    }
                    return model;
                }
            }
            catch (InvalidDataException e) when (!e.Message.StartsWith(path, StringComparison.Ordinal))
            {
                // Not a zip at all: the compression library's message names no file.
                throw new InvalidDataException(string.Format(@"{0} is not a CarafeSharp model file: {1}", path, e.Message), e);
            }
            catch (Exception e) when (e is JsonException || e is KeyNotFoundException || e is InvalidOperationException)
            {
                // A manifest that is not JSON, lacks a field, or has one of the wrong kind.
                throw new InvalidDataException(string.Format(@"{0} has an unreadable {1}: {2}", path, MANIFEST_ENTRY, e.Message), e);
            }
        }

        private CarafeModelFile()
        {
        }

        public string Path { get; private set; }
        public string Format { get; private set; }
        /// <summary>What wrote the file: CarafeSharp and its version.</summary>
        public string Creator { get; private set; }
        /// <summary>When the file was written, UTC, ISO 8601.</summary>
        public string Created { get; private set; }
        /// <summary>The training run's <c>-tf</c>.</summary>
        public string TrainingType { get; private set; }
        public bool Ms2FineTuned { get; private set; }
        /// <summary>The fine-tuned MS2 model beat the pretrained one, and the file holds it.</summary>
        public bool Ms2Used { get; private set; }
        public bool RtFineTuned { get; private set; }
        public bool RtUsed { get; private set; }
        /// <summary>The SHA-256 of the pretrained archive the training started from, when known.</summary>
        public string PretrainedSha256 { get; private set; }
        /// <summary>The file name of the <c>-ms2_model</c> the MS2 fine-tune started from, if any.</summary>
        public string Ms2StartModel { get; private set; }
        /// <summary>The training runs, as the file's meta.json records them for prediction.</summary>
        public IReadOnlyList<CarafeRunMeta> Runs { get; private set; } = new List<CarafeRunMeta>();
        /// <summary>What the models were trained on, or null for a file written without it.</summary>
        public CarafeModelTraining Training { get; private set; }
        /// <summary>The activations and analyzers the MS2 model has columns for, or null when the file does not say.</summary>
        public AcquisitionVocabulary Acquisition { get; private set; }
        /// <summary>The activation a library takes unless <c>-activation</c> is given, or null.</summary>
        public string Activation { get; private set; }
        /// <summary>The MS2 analyzer a library takes unless <c>-analyzer</c> is given, or null.</summary>
        public string Analyzer { get; private set; }
        /// <summary>The NCE a library takes unless <c>-nce</c> is given.</summary>
        public double Nce { get; private set; }
        /// <summary>The instrument a library takes unless <c>-ms_instrument</c> is given, or null for the command line's.</summary>
        public string Instrument { get; private set; }
        /// <summary>
        /// The rt_max a library takes unless <c>-rt_max</c> is given: the fine-tuned RT model
        /// predicts retention times scaled to the training run's gradient.
        /// </summary>
        public double RtMax { get; private set; }
        /// <summary>Every entry but the manifest, with its SHA-256.</summary>
        public IReadOnlyDictionary<string, string> Entries { get; private set; }

        /// <summary>
        /// Unpacks the models, metrics and meta.json into <paramref name="folder"/>, a new folder
        /// the caller deletes, and opens it as a model folder.
        /// </summary>
        public CarafeModelDirectory Extract(string folder)
        {
            Directory.CreateDirectory(folder);
            using (var zip = ZipFile.OpenRead(Path))
            {
                foreach (string name in Entries.Keys)
                {
                    var entry = zip.GetEntry(name) ?? throw new InvalidDataException(string.Format(@"{0} lists {1}, which it does not hold.", Path, name));
                    entry.ExtractToFile(System.IO.Path.Combine(folder, name), true);
                }
            }
            return CarafeModelDirectory.Open(folder, true);
        }

        /// <summary>
        /// The training run's NCE, instrument and rt_max, for each the command line did not give.
        /// The precursor and fragment m/z ranges stay the command line's, so the model predicts
        /// any peptides, unlike <c>-model_dir</c>, which takes the training run's window.
        /// </summary>
        public void ApplyPredictionDefaults(LibrarySettings settings)
        {
            if (!settings.UserNce)
                settings.Nce = Nce;
            if (!settings.UserInstrument && !string.IsNullOrEmpty(Instrument))
                settings.Instrument = Instrument;
            if (!settings.UserRtMax)
                settings.RtMax = RtMax;
            if (!settings.UserActivation && Activation != null)
                settings.Activation = Activation;
            if (!settings.UserAnalyzer && Analyzer != null)
                settings.Analyzer = Analyzer;
        }

        /// <summary>A one-line description for the log: which models are fine-tuned, and what they were trained on.</summary>
        public string Describe()
        {
            return string.Format(CultureInfo.InvariantCulture, @"MS2 {0}, RT {1}; trained on {2}; written by {3} at {4}",
                Ms2Used ? @"fine-tuned" : Ms2FineTuned ? @"pretrained (the fine-tuned model did not beat it)" : @"pretrained",
                RtUsed ? @"fine-tuned" : @"pretrained",
                Runs.Count == 0 ? @"unknown runs" : string.Join(@", ", Runs.Select(r => System.IO.Path.GetFileName(r.MsFile))),
                Creator, Created);
        }

        /// <summary>
        /// What <c>-model_info</c> prints: the models, the settings a library takes from the file,
        /// and what the models were trained on, run by run, for a user choosing a model.
        /// </summary>
        public string FormatInfo()
        {
            var text = new System.Text.StringBuilder();
            void Line(string format, params object[] args) => text.AppendLine(string.Format(CultureInfo.InvariantCulture, format, args));
            Line(@"{0}", Path);
            Line(@"  Format {0}, written by {1} at {2}", Format, Creator, Created);
            Line(@"  MS2 model: {0}", Ms2Used ? @"fine-tuned" : Ms2FineTuned ? @"pretrained (the fine-tuned model did not beat it)" : @"pretrained");
            Line(@"  RT model: {0}", RtUsed ? @"fine-tuned" : @"pretrained");
            if (Ms2StartModel != null)
                Line(@"  MS2 fine-tune started from {0}", Ms2StartModel);
            Line(@"  A library takes: NCE {0}, instrument {1}, activation {2}, analyzer {3}, rt_max {4}" +
                 @" (unless -nce, -ms_instrument, -activation, -analyzer, -rt_max are given)",
                Nce, Instrument ?? @"(the command line's)", Activation ?? @"(none)", Analyzer ?? @"(none)", RtMax);
            if (Acquisition != null)
                Line(@"  MS2 acquisition columns: {0}", Acquisition);
            if (Training == null)
                return text.ToString();

            var training = Training;
            Line(@"  Training: run q <= {0}, correlation >= {1}, masking {2}, seed {3}", training.Fdr, training.MinCorrelation,
                training.Masking ? @"on" : @"off", training.Seed);
            Line(@"  Trained on {0} MS2 spectra ({1}) and {2} RT peptide forms; peptide length {3}-{4}", training.Ms2Spectra,
                FormatCharges(training.Ms2Charges), training.RtPeptideForms, training.MinPeptideLength, training.MaxPeptideLength);
            if (training.Modifications.Count > 0)
                Line(@"  Modifications: {0}", string.Join(@", ", training.Modifications.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + @" " + p.Value)));
            foreach (var run in training.Runs)
            {
                Line(@"  Run {0}: {1}, {2}", run.Run, run.Activation ?? @"activation unknown", run.Analyzer ?? @"analyzer unknown");
                Line(@"    Instrument: {0} {1}{2}", run.InstrumentVendor ?? string.Empty, run.InstrumentModel ?? @"(unknown)",
                    string.IsNullOrEmpty(run.Instrument) ? string.Empty : @" (trained as " + run.Instrument + @")");
                Line(@"    Fragmentation: {0}; collision energies {1}; NCE {2}; MS2 read out in {3}", FormatCounts(run.DissociationMethods),
                    FormatCounts(run.CollisionEnergies), run.Nce, FormatCounts(run.Ms2MassAnalyzers));
                Line(@"    RT {0}-{1} min; isolation m/z {2}-{3}; MS2 m/z {4}-{5}; fragment tolerance {6} {7}",
                    FormatNumber(run.RtMin), FormatNumber(run.RtMax), FormatNumber(run.IsolationMzMin), FormatNumber(run.IsolationMzMax),
                    FormatNumber(run.Ms2MzMin), FormatNumber(run.Ms2MzMax), FormatNumber(run.FragmentTolerance), run.FragmentToleranceUnit ?? string.Empty);
                Line(@"    {0} precursors ({1}) at run q <= {2}, pass {3}; Osprey {4}", run.Precursors, FormatCharges(run.PrecursorCharges),
                    FormatNumber(run.MaxQ), run.RunQPass ?? @"?", run.OspreyVersion ?? @"(unknown)");
            }
            if (training.HeldOutMetrics.Count > 0)
            {
                Line(@"  Held-out metrics (pretrained -> fine-tuned):");
                foreach (string model in new[] { @"ms2", @"rt" })
                {
                    var names = training.HeldOutMetrics.Keys.Where(k => k.StartsWith(model + @".pretrained.", StringComparison.Ordinal))
                        .Select(k => k.Substring(model.Length + @".pretrained.".Length));
                    var parts = names.Select(n => string.Format(CultureInfo.InvariantCulture, @"{0} {1:0.####} -> {2}", n,
                        training.HeldOutMetrics[model + @".pretrained." + n],
                        training.HeldOutMetrics.TryGetValue(model + @".finetuned." + n, out double tuned) ? tuned.ToString(@"0.####", CultureInfo.InvariantCulture) : @"?"));
                    Line(@"    {0}: {1}", model.ToUpperInvariant(), string.Join(@", ", parts));
                }
            }
            return text.ToString();
        }

        private static string FormatCharges(IReadOnlyDictionary<int, int> charges)
        {
            return charges.Count == 0 ? @"no charges" : string.Join(@", ", charges.OrderBy(p => p.Key).Select(p => string.Format(CultureInfo.InvariantCulture, @"{0} at {1}+", p.Value, p.Key)));
        }

        private static string FormatCounts(IReadOnlyDictionary<string, long> counts)
        {
            return counts.Count == 0 ? @"unknown" : string.Join(@", ", counts.OrderByDescending(p => p.Value).Select(p => p.Key + @" x" + p.Value.ToString(CultureInfo.InvariantCulture)));
        }

        private static string FormatNumber(double? value)
        {
            return value.HasValue ? value.Value.ToString(@"0.###", CultureInfo.InvariantCulture) : @"?";
        }

        private void WriteManifest(Stream stream)
        {
            using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                json.WriteStartObject();
                json.WriteString(@"format", Format);
                json.WriteString(@"creator", Creator);
                json.WriteString(@"created", Created);
                json.WriteString(@"training_type", TrainingType);
                json.WriteStartObject(@"models");
                WriteModel(json, @"ms2", Ms2FineTuned, Ms2Used, ModelFiles.MS2_SAFETENSORS);
                WriteModel(json, @"rt", RtFineTuned, RtUsed, ModelFiles.RT_SAFETENSORS);
                json.WriteEndObject();
                WriteStringOrNull(json, @"pretrained_sha256", PretrainedSha256);
                WriteStringOrNull(json, @"ms2_start_model", Ms2StartModel);
                json.WritePropertyName(@"acquisition");
                if (Acquisition == null)
                {
                    json.WriteNullValue();
                }
                else
                {
                    json.WriteStartObject();
                    json.WriteStartArray(@"activations");
                    foreach (string activation in Acquisition.Activations)
                        json.WriteStringValue(activation);
                    json.WriteEndArray();
                    json.WriteStartArray(@"analyzers");
                    foreach (string analyzer in Acquisition.Analyzers)
                        json.WriteStringValue(analyzer);
                    json.WriteEndArray();
                    json.WriteEndObject();
                }
                json.WritePropertyName(@"training");
                if (Training == null)
                    json.WriteNullValue();
                else
                    Training.WriteJson(json);
                json.WriteStartObject(@"prediction_defaults");
                json.WriteNumber(@"nce", Nce);
                WriteStringOrNull(json, @"instrument", Instrument);
                WriteStringOrNull(json, @"activation", Activation);
                WriteStringOrNull(json, @"analyzer", Analyzer);
                json.WriteNumber(@"rt_max", RtMax);
                json.WriteEndObject();
                json.WriteStartObject(@"entries");
                foreach (var pair in Entries)
                    json.WriteString(pair.Key, pair.Value);
                json.WriteEndObject();
                json.WriteEndObject();
            }
        }

        private static CarafeModelFile ReadManifest(string path, Stream stream)
        {
            using (var json = JsonDocument.Parse(stream))
            {
                var root = json.RootElement;
                string format = root.TryGetProperty(@"format", out var formatValue) ? formatValue.GetString() : null;
                if (format != FORMAT)
                {
                    throw new InvalidDataException(string.Format(@"{0} has model file format {1}; this CarafeSharp reads {2}.",
                        path, format ?? @"(none)", FORMAT));
                }
                var models = root.GetProperty(@"models");
                var defaults = root.GetProperty(@"prediction_defaults");
                var training = root.TryGetProperty(@"training", out var trainingValue) && trainingValue.ValueKind == JsonValueKind.Object
                    ? CarafeModelTraining.ReadJson(trainingValue)
                    : null;
                var acquisition = root.TryGetProperty(@"acquisition", out var acquisitionValue) && acquisitionValue.ValueKind == JsonValueKind.Object
                    ? new AcquisitionVocabulary(acquisitionValue.GetProperty(@"activations").EnumerateArray().Select(v => v.GetString()),
                        acquisitionValue.GetProperty(@"analyzers").EnumerateArray().Select(v => v.GetString()))
                    : null;
                return new CarafeModelFile
                {
                    Path = path,
                    Format = format,
                    Creator = root.GetProperty(@"creator").GetString(),
                    Created = root.GetProperty(@"created").GetString(),
                    TrainingType = root.GetProperty(@"training_type").GetString(),
                    Ms2FineTuned = models.GetProperty(@"ms2").GetProperty(@"fine_tuned").GetBoolean(),
                    Ms2Used = models.GetProperty(@"ms2").GetProperty(@"used").GetBoolean(),
                    RtFineTuned = models.GetProperty(@"rt").GetProperty(@"fine_tuned").GetBoolean(),
                    RtUsed = models.GetProperty(@"rt").GetProperty(@"used").GetBoolean(),
                    PretrainedSha256 = GetStringOrNull(root, @"pretrained_sha256"),
                    Ms2StartModel = GetStringOrNull(root, @"ms2_start_model"),
                    Training = training,
                    Acquisition = acquisition,
                    Activation = GetStringOrNull(defaults, @"activation"),
                    Analyzer = GetStringOrNull(defaults, @"analyzer"),
                    Nce = defaults.GetProperty(@"nce").GetDouble(),
                    Instrument = GetStringOrNull(defaults, @"instrument"),
                    RtMax = defaults.GetProperty(@"rt_max").GetDouble(),
                    Entries = root.GetProperty(@"entries").EnumerateObject()
                        .ToDictionary(p => p.Name, p => p.Value.GetString(), StringComparer.Ordinal),
                };
            }
        }

        private static void WriteModel(Utf8JsonWriter json, string name, bool fineTuned, bool used, string entry)
        {
            json.WriteStartObject(name);
            json.WriteBoolean(@"fine_tuned", fineTuned);
            json.WriteBoolean(@"used", used);
            WriteStringOrNull(json, @"entry", used ? entry : null);
            json.WriteEndObject();
        }

        private static void WriteStringOrNull(Utf8JsonWriter json, string name, string value)
        {
            if (value == null)
                json.WriteNull(name);
            else
                json.WriteString(name, value);
        }

        private static string GetStringOrNull(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }

        /// <summary>Whether <c>-tf</c> <paramref name="trainingType"/> fine-tunes the model <paramref name="model"/> (ms2 or rt).</summary>
        private static bool IsTrained(string trainingType, string model)
        {
            return string.Equals(trainingType, @"all", StringComparison.Ordinal) || string.Equals(trainingType, model, StringComparison.Ordinal);
        }

        private static string SafetensorsOrNull(string modelPath)
        {
            return modelPath != null && CarafeModelDirectory.IsSafetensors(modelPath) ? modelPath : null;
        }

        /// <summary>model_evaluation_metrics.json's scores, as "ms2.pretrained.cos" and the like.</summary>
        private static IReadOnlyDictionary<string, double> ReadMetrics(string path)
        {
            var metrics = new Dictionary<string, double>(StringComparer.Ordinal);
            using (var json = JsonDocument.Parse(File.ReadAllText(path)))
            {
                foreach (var model in json.RootElement.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Object))
                {
                    foreach (var kind in model.Value.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Object))
                    {
                        foreach (var metric in kind.Value.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.Number))
                            metrics[model.Name + @"." + kind.Name + @"." + metric.Name] = metric.Value.GetDouble();
                    }
                }
            }
            return metrics;
        }

        private static byte[] ReadAll(ZipArchiveEntry entry)
        {
            using (var stream = entry.Open())
            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                return memory.ToArray();
            }
        }

        private static string Sha256(byte[] bytes)
        {
            return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }

        private static string GetVersion()
        {
            return typeof(CarafeModelFile).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? @"unknown";
        }
    }
}
