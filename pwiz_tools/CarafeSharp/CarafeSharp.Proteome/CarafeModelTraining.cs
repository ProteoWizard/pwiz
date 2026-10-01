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
using System.Linq;
using System.Text.Json;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// What a saved model (<see cref="CarafeModelFile"/>) was trained on, for a user choosing
    /// among models (Skyline shows it): the training settings, the data the models learned from,
    /// each training run's acquisition as Osprey's training export describes it, and the held-out
    /// metrics of the pretrained and fine-tuned models. It is the manifest's <c>training</c>
    /// object (docs/06-saved-models.md).
    /// </summary>
    public sealed class CarafeModelTraining
    {
        /// <summary>The run q-value the training precursors were selected at (<c>-fdr</c>).</summary>
        public double Fdr { get; set; }
        /// <summary>The fragment correlation masking kept ions at (<c>-cor</c>).</summary>
        public double MinCorrelation { get; set; }
        /// <summary>False with <c>-no_masking</c>.</summary>
        public bool Masking { get; set; }
        public uint Seed { get; set; }

        /// <summary>MS2 spectra the MS2 model trained on.</summary>
        public int Ms2Spectra { get; set; }
        /// <summary>Peptide forms the RT model trained on.</summary>
        public int RtPeptideForms { get; set; }
        /// <summary>The MS2 spectra by precursor charge.</summary>
        public IReadOnlyDictionary<int, int> Ms2Charges { get; set; } = new Dictionary<int, int>();
        public int MinPeptideLength { get; set; }
        public int MaxPeptideLength { get; set; }
        /// <summary>The training peptide forms carrying each modification (alphabase names), over both models' forms.</summary>
        public IReadOnlyDictionary<string, int> Modifications { get; set; } = new Dictionary<string, int>();

        public IReadOnlyList<CarafeModelTrainingRun> Runs { get; set; } = new List<CarafeModelTrainingRun>();

        /// <summary>model_evaluation_metrics.json's scores, as "ms2.pretrained.cos" and the like.</summary>
        public IReadOnlyDictionary<string, double> HeldOutMetrics { get; set; } = new Dictionary<string, double>();

        internal void WriteJson(Utf8JsonWriter json)
        {
            json.WriteStartObject();
            json.WriteStartObject(@"settings");
            json.WriteNumber(@"fdr", Fdr);
            json.WriteNumber(@"min_correlation", MinCorrelation);
            json.WriteBoolean(@"masking", Masking);
            json.WriteNumber(@"seed", Seed);
            json.WriteEndObject();

            json.WriteStartObject(@"data");
            json.WriteNumber(@"ms2_spectra", Ms2Spectra);
            json.WriteNumber(@"rt_peptide_forms", RtPeptideForms);
            WriteCounts(json, @"ms2_charges", Ms2Charges.OrderBy(p => p.Key).Select(p => (p.Key.ToString(CultureInfo.InvariantCulture), (long)p.Value)));
            json.WriteNumber(@"peptide_length_min", MinPeptideLength);
            json.WriteNumber(@"peptide_length_max", MaxPeptideLength);
            WriteCounts(json, @"modifications", Modifications.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => (p.Key, (long)p.Value)));
            json.WriteEndObject();

            json.WriteStartArray(@"runs");
            foreach (var run in Runs)
                run.WriteJson(json);
            json.WriteEndArray();

            json.WriteStartObject(@"held_out_metrics");
            foreach (var pair in HeldOutMetrics)
                json.WriteNumber(pair.Key, pair.Value);
            json.WriteEndObject();
            json.WriteEndObject();
        }

        internal static CarafeModelTraining ReadJson(JsonElement element)
        {
            var settings = element.GetProperty(@"settings");
            var data = element.GetProperty(@"data");
            return new CarafeModelTraining
            {
                Fdr = settings.GetProperty(@"fdr").GetDouble(),
                MinCorrelation = settings.GetProperty(@"min_correlation").GetDouble(),
                Masking = settings.GetProperty(@"masking").GetBoolean(),
                Seed = settings.GetProperty(@"seed").GetUInt32(),
                Ms2Spectra = data.GetProperty(@"ms2_spectra").GetInt32(),
                RtPeptideForms = data.GetProperty(@"rt_peptide_forms").GetInt32(),
                Ms2Charges = ReadCounts(data.GetProperty(@"ms2_charges"))
                    .ToDictionary(p => int.Parse(p.Key, CultureInfo.InvariantCulture), p => (int)p.Value),
                MinPeptideLength = data.GetProperty(@"peptide_length_min").GetInt32(),
                MaxPeptideLength = data.GetProperty(@"peptide_length_max").GetInt32(),
                Modifications = ReadCounts(data.GetProperty(@"modifications")).ToDictionary(p => p.Key, p => (int)p.Value, StringComparer.Ordinal),
                Runs = element.GetProperty(@"runs").EnumerateArray().Select(CarafeModelTrainingRun.ReadJson).ToList(),
                HeldOutMetrics = element.GetProperty(@"held_out_metrics").EnumerateObject()
                    .ToDictionary(p => p.Name, p => p.Value.GetDouble(), StringComparer.Ordinal),
            };
        }

        internal static void WriteCounts(Utf8JsonWriter json, string name, IEnumerable<(string Key, long Count)> counts)
        {
            json.WriteStartObject(name);
            foreach (var (key, count) in counts)
                json.WriteNumber(key, count);
            json.WriteEndObject();
        }

        internal static IReadOnlyDictionary<string, long> ReadCounts(JsonElement element)
        {
            return element.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetInt64(), StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// One training run of a saved model, as Osprey's training export footer describes it
    /// (Osprey's docs/22-training-export.md): the instrument, how it fragmented, the gradient and
    /// windows, and how many precursors of which charges it gave the training. A value the export
    /// does not carry (a run searched without its data file) is null.
    /// </summary>
    public sealed class CarafeModelTrainingRun
    {
        /// <summary>The run's name (<c>osprey.file_name</c>).</summary>
        public string Run { get; set; }
        /// <summary>The run as <c>-ms</c> named it, or its name.</summary>
        public string MsFile { get; set; }
        public string InstrumentVendor { get; set; }
        public string InstrumentModel { get; set; }
        /// <summary>The instrument name the run trained as (Carafe's: Eclipse, Lumos, Astral, QE, Stellar, ...), or empty when Carafe names none.</summary>
        public string Instrument { get; set; }
        /// <summary>The activation the run trained as (beam-CID, reCID), or null when unknown.</summary>
        public string Activation { get; set; }
        /// <summary>The MS2 analyzer the run trained as (Orbitrap, LIT, ToF), or null when unknown.</summary>
        public string Analyzer { get; set; }
        /// <summary>The NCE the models were trained with.</summary>
        public double Nce { get; set; }
        /// <summary>MS2 spectra by dissociation method, over the spectra Osprey sampled.</summary>
        public IReadOnlyDictionary<string, long> DissociationMethods { get; set; } = new Dictionary<string, long>();
        /// <summary>MS2 spectra by collision energy as the file reports it, over the spectra Osprey sampled.</summary>
        public IReadOnlyDictionary<string, long> CollisionEnergies { get; set; } = new Dictionary<string, long>();
        /// <summary>MS2 spectra by the mass analyzer that read them out, over the spectra Osprey sampled.</summary>
        public IReadOnlyDictionary<string, long> Ms2MassAnalyzers { get; set; } = new Dictionary<string, long>();
        /// <summary>The first and last MS2 retention time, minutes.</summary>
        public double? RtMin { get; set; }
        public double? RtMax { get; set; }
        public double? IsolationMzMin { get; set; }
        public double? IsolationMzMax { get; set; }
        /// <summary>The MS2 m/z range the run measured.</summary>
        public double? Ms2MzMin { get; set; }
        public double? Ms2MzMax { get; set; }
        public double? FragmentTolerance { get; set; }
        public string FragmentToleranceUnit { get; set; }
        /// <summary>Target precursors the export gave the training.</summary>
        public int Precursors { get; set; }
        /// <summary>Those precursors by charge.</summary>
        public IReadOnlyDictionary<int, int> PrecursorCharges { get; set; } = new Dictionary<int, int>();
        /// <summary>The Osprey pass whose run q-values selected them (2, or 1 for a run with no second pass).</summary>
        public string RunQPass { get; set; }
        /// <summary>The run q-value the export kept precursors at.</summary>
        public double? MaxQ { get; set; }
        public string OspreyVersion { get; set; }
        public string SearchHash { get; set; }
        public string LibraryHash { get; set; }

        internal void WriteJson(Utf8JsonWriter json)
        {
            json.WriteStartObject();
            WriteString(json, @"run", Run);
            WriteString(json, @"ms_file", MsFile);
            WriteString(json, @"instrument_vendor", InstrumentVendor);
            WriteString(json, @"instrument_model", InstrumentModel);
            WriteString(json, @"instrument", Instrument);
            WriteString(json, @"activation", Activation);
            WriteString(json, @"analyzer", Analyzer);
            json.WriteNumber(@"nce", Nce);
            CarafeModelTraining.WriteCounts(json, @"dissociation_methods", DissociationMethods.Select(p => (p.Key, p.Value)));
            CarafeModelTraining.WriteCounts(json, @"collision_energies", CollisionEnergies.Select(p => (p.Key, p.Value)));
            CarafeModelTraining.WriteCounts(json, @"ms2_mass_analyzers", Ms2MassAnalyzers.Select(p => (p.Key, p.Value)));
            WriteNumber(json, @"rt_min", RtMin);
            WriteNumber(json, @"rt_max", RtMax);
            WriteNumber(json, @"isolation_mz_min", IsolationMzMin);
            WriteNumber(json, @"isolation_mz_max", IsolationMzMax);
            WriteNumber(json, @"ms2_mz_min", Ms2MzMin);
            WriteNumber(json, @"ms2_mz_max", Ms2MzMax);
            WriteNumber(json, @"fragment_tolerance", FragmentTolerance);
            WriteString(json, @"fragment_tolerance_unit", FragmentToleranceUnit);
            json.WriteNumber(@"precursors", Precursors);
            CarafeModelTraining.WriteCounts(json, @"precursor_charges",
                PrecursorCharges.OrderBy(p => p.Key).Select(p => (p.Key.ToString(CultureInfo.InvariantCulture), (long)p.Value)));
            WriteString(json, @"run_q_pass", RunQPass);
            WriteNumber(json, @"max_q", MaxQ);
            WriteString(json, @"osprey_version", OspreyVersion);
            WriteString(json, @"search_hash", SearchHash);
            WriteString(json, @"library_hash", LibraryHash);
            json.WriteEndObject();
        }

        internal static CarafeModelTrainingRun ReadJson(JsonElement element)
        {
            return new CarafeModelTrainingRun
            {
                Run = ReadString(element, @"run"),
                MsFile = ReadString(element, @"ms_file"),
                InstrumentVendor = ReadString(element, @"instrument_vendor"),
                InstrumentModel = ReadString(element, @"instrument_model"),
                Instrument = ReadString(element, @"instrument"),
                Activation = ReadString(element, @"activation"),
                Analyzer = ReadString(element, @"analyzer"),
                Nce = element.GetProperty(@"nce").GetDouble(),
                DissociationMethods = CarafeModelTraining.ReadCounts(element.GetProperty(@"dissociation_methods")),
                CollisionEnergies = CarafeModelTraining.ReadCounts(element.GetProperty(@"collision_energies")),
                Ms2MassAnalyzers = element.TryGetProperty(@"ms2_mass_analyzers", out var analyzers)
                    ? CarafeModelTraining.ReadCounts(analyzers)
                    : new Dictionary<string, long>(),
                RtMin = ReadNumber(element, @"rt_min"),
                RtMax = ReadNumber(element, @"rt_max"),
                IsolationMzMin = ReadNumber(element, @"isolation_mz_min"),
                IsolationMzMax = ReadNumber(element, @"isolation_mz_max"),
                Ms2MzMin = ReadNumber(element, @"ms2_mz_min"),
                Ms2MzMax = ReadNumber(element, @"ms2_mz_max"),
                FragmentTolerance = ReadNumber(element, @"fragment_tolerance"),
                FragmentToleranceUnit = ReadString(element, @"fragment_tolerance_unit"),
                Precursors = element.GetProperty(@"precursors").GetInt32(),
                PrecursorCharges = CarafeModelTraining.ReadCounts(element.GetProperty(@"precursor_charges"))
                    .ToDictionary(p => int.Parse(p.Key, CultureInfo.InvariantCulture), p => (int)p.Value),
                RunQPass = ReadString(element, @"run_q_pass"),
                MaxQ = ReadNumber(element, @"max_q"),
                OspreyVersion = ReadString(element, @"osprey_version"),
                SearchHash = ReadString(element, @"search_hash"),
                LibraryHash = ReadString(element, @"library_hash"),
            };
        }

        private static void WriteString(Utf8JsonWriter json, string name, string value)
        {
            if (value == null)
                json.WriteNull(name);
            else
                json.WriteString(name, value);
        }

        private static void WriteNumber(Utf8JsonWriter json, string name, double? value)
        {
            if (value.HasValue && double.IsFinite(value.Value))
                json.WriteNumber(name, value.Value);
            else
                json.WriteNull(name);
        }

        private static string ReadString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }

        private static double? ReadNumber(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;
        }
    }
}
