/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace pwiz.CarafeSharp.Contracts
{
    /// <summary>
    /// One run's training export as a consumer holds it: its path, its records
    /// (<see cref="TrainingExportParquet.Read"/>) and its footer, with the footer's keys
    /// (<see cref="TrainingExportKeys"/>) read as the values they encode.
    /// </summary>
    public sealed class TrainingExport
    {
        public static TrainingExport Read(string path)
        {
            var records = TrainingExportParquet.Read(path, out var metadata);
            return new TrainingExport(path, records, metadata);
        }

        /// <summary>An export's key-value footer, read without its rows.</summary>
        public static IReadOnlyDictionary<string, string> ReadFooter(string path)
        {
            return TrainingExportParquet.ReadFooter(path);
        }

        /// <summary>An export built in memory, for tests.</summary>
        internal static TrainingExport Create(string path, IReadOnlyList<TrainingRecord> records, IReadOnlyDictionary<string, string> metadata)
        {
            return new TrainingExport(path, records, metadata);
        }

        private TrainingExport(string path, IReadOnlyList<TrainingRecord> records, IReadOnlyDictionary<string, string> metadata)
        {
            Path = path;
            Records = records;
            Metadata = metadata;
        }

        public string Path { get; }

        public IReadOnlyList<TrainingRecord> Records { get; }

        public IReadOnlyDictionary<string, string> Metadata { get; }

        /// <summary>The run's last MS2 retention time, minutes (<see cref="TrainingExportKeys.RT_MAX"/>).</summary>
        public double RtMax
        {
            get { return GetDouble(TrainingExportKeys.RT_MAX); }
        }

        /// <summary>
        /// Which Osprey pass the run q-values came from (<see cref="TrainingExportKeys.RUN_Q_PASS"/>,
        /// format 2): <c>2</c> normally, <c>1</c> when the run had no second pass; null in format 1.
        /// </summary>
        public string RunQPass
        {
            get { return GetText(TrainingExportKeys.RUN_Q_PASS); }
        }

        /// <summary>The instrument model the run info reports, or null.</summary>
        public string InstrumentModel
        {
            get { return GetText(TrainingExportKeys.INSTRUMENT_MODEL); }
        }

        /// <summary>The range of the run's isolation windows (<see cref="TrainingExportKeys.ISOLATION_MZ_MIN"/>, <see cref="TrainingExportKeys.ISOLATION_MZ_MAX"/>).</summary>
        public (double Lower, double Upper) IsolationRange
        {
            get { return (GetDouble(TrainingExportKeys.ISOLATION_MZ_MIN), GetDouble(TrainingExportKeys.ISOLATION_MZ_MAX)); }
        }

        /// <summary>
        /// The run's MS2 m/z range (<see cref="TrainingExportKeys.MS2_SCAN_WINDOW"/>), or null when
        /// unknown. Format 1 carried the declared scan window (200-1500 on Stellar); format 2
        /// carries the lowest and highest calibrated peak m/z the run's spectra measured
        /// (200.17-1500.17 on the same run).
        /// </summary>
        public (double Lower, double Upper)? Ms2ScanWindow
        {
            get
            {
                string text = GetText(TrainingExportKeys.MS2_SCAN_WINDOW);
                if (text == null)
                    return null;
                var parts = text.Split(',');
                if (parts.Length != 2 ||
                    !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double lower) ||
                    !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double upper))
                {
                    return null;
                }
                return (lower, upper);
            }
        }

        /// <summary>MS2 spectra by dissociation method, pwiz's short names (<c>HCD</c>, <c>CID</c>, ...); empty without the run info.</summary>
        public IReadOnlyDictionary<string, long> DissociationMethods
        {
            get { return GetCounts(TrainingExportKeys.DISSOCIATION_METHODS); }
        }

        /// <summary>MS2 spectra by collision energy as the file reports it; empty without the run info.</summary>
        public IReadOnlyDictionary<string, long> CollisionEnergies
        {
            get { return GetCounts(TrainingExportKeys.COLLISION_ENERGIES); }
        }

        /// <summary>
        /// MS2 spectra by mass analyzer, pwiz's names (<c>orbitrap</c>, <c>radial ejection linear
        /// ion trap</c>, ...); empty without the run info or from an Osprey that did not record it.
        /// </summary>
        public IReadOnlyDictionary<string, long> Ms2MassAnalyzers
        {
            get { return GetCounts(TrainingExportKeys.MS2_MASS_ANALYZERS); }
        }

        /// <summary>
        /// The collision energy most MS2 spectra were acquired at
        /// (<see cref="TrainingExportKeys.COLLISION_ENERGIES"/>), or null when the run info is
        /// missing. A tie keeps the first; a key that is not a number is skipped.
        /// </summary>
        public double? DominantCollisionEnergy
        {
            get
            {
                double? best = null;
                long bestCount = -1;
                foreach (var pair in CollisionEnergies)
                {
                    if (pair.Value > bestCount &&
                        double.TryParse(pair.Key, NumberStyles.Float, CultureInfo.InvariantCulture, out double energy))
                    {
                        best = energy;
                        bestCount = pair.Value;
                    }
                }
                return best;
            }
        }

        /// <summary>A footer value, or null when the key is absent or empty.</summary>
        private string GetText(string key)
        {
            return Metadata.TryGetValue(key, out string text) && text.Length > 0 ? text : null;
        }

        /// <summary>A footer histogram (JSON, key to count), empty when the export has none.</summary>
        private IReadOnlyDictionary<string, long> GetCounts(string key)
        {
            var counts = new Dictionary<string, long>(StringComparer.Ordinal);
            string json = GetText(key);
            if (json == null)
                return counts;
            using (var document = JsonDocument.Parse(json))
            {
                foreach (var property in document.RootElement.EnumerateObject())
                    counts[property.Name] = property.Value.GetInt64();
            }
            return counts;
        }

        private double GetDouble(string key)
        {
            if (!Metadata.TryGetValue(key, out string text))
            {
                throw new InvalidDataException(string.Format(
                    ContractsResources.TrainingExport_GetDouble__0__has_no__1__footer_key_, Path, key));
            }
            return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
