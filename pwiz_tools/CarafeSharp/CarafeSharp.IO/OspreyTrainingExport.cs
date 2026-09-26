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
using System.Runtime.InteropServices;

namespace pwiz.CarafeSharp.IO
{
    /// <summary>
    /// Reads one run's Osprey training export, <c>&lt;stem&gt;.training.parquet</c>
    /// (<c>osprey --training-export</c>; contract in pwiz_tools/Osprey/docs/22-training-export.md).
    /// Per-ion arrays are little-endian typed blobs with no length prefix; a NULL cell is an
    /// empty array.
    /// </summary>
    public sealed class OspreyTrainingExport
    {
        public const string FILE_SUFFIX = @".training.parquet";
        public const string FORMAT_VERSION = @"1";

        /// <summary>Footer keys identifying the Osprey search an export came from.</summary>
        public const string SEARCH_HASH_KEY = @"osprey.search_hash";
        public const string LIBRARY_HASH_KEY = @"osprey.library_hash";

        private static readonly string[] COLUMNS =
        {
            @"entry_id", @"is_entrapment", @"sequence", @"modified_sequence", @"mod_positions", @"mod_masses",
            @"mod_unimod_ids", @"charge", @"precursor_mz", @"protein_ids", @"file_name", @"apex_rt", @"start_rt",
            @"end_rt", @"n_peak_scans", @"score", @"run_precursor_q", @"experiment_precursor_q", @"pep",
            @"mp_fitted", @"mp_residual_mad", @"n_same_apex_claimants", @"n_coeluting_claimants",
            @"ion_mz", @"ion_flags", @"apex_intensity", @"apex_mz_error", @"library_rel_intensity", @"n_finite_scans",
            @"xic_start", @"xic_end", @"xic_max", @"corr_polish", @"corr_reference", @"polish_row_effect", @"polish_r2",
            @"polish_pos_resid_max", @"polish_apex_residual", @"polish_outlier_z", @"polish_apex_ratio",
            @"polish_rel_intensity", @"shared_apex_n", @"shared_coelute_n", @"min_claimant_q",
        };

        public static OspreyTrainingExport Read(string path)
        {
            var columns = ParquetColumns.Read(path, COLUMNS);
            string version = columns.Metadata.TryGetValue(@"osprey.training_export.format_version", out string v) ? v : null;
            if (version != FORMAT_VERSION)
            {
                throw new InvalidDataException(string.Format(@"{0} is training export format {1}; CarafeSharp reads format {2}.",
                    path, version ?? @"(none)", FORMAT_VERSION));
            }
            var records = new List<OspreyTrainingRecord>(columns.RowCount);
            var entryIds = columns.Get<uint>(@"entry_id");
            var isEntrapment = columns.Get<bool>(@"is_entrapment");
            var sequences = columns.Get<string>(@"sequence");
            var modifiedSequences = columns.Get<string>(@"modified_sequence");
            var modPositions = columns.Get<byte[]>(@"mod_positions");
            var modMasses = columns.Get<byte[]>(@"mod_masses");
            var modUnimods = columns.Get<byte[]>(@"mod_unimod_ids");
            var charges = columns.Get<byte>(@"charge");
            var precursorMz = columns.Get<double>(@"precursor_mz");
            var proteins = columns.Get<string>(@"protein_ids");
            var files = columns.Get<string>(@"file_name");
            var apexRt = columns.Get<double>(@"apex_rt");
            var startRt = columns.Get<double>(@"start_rt");
            var endRt = columns.Get<double>(@"end_rt");
            var peakScans = columns.Get<int>(@"n_peak_scans");
            var scores = columns.Get<double>(@"score");
            var runQ = columns.Get<double>(@"run_precursor_q");
            var experimentQ = columns.Get<double>(@"experiment_precursor_q");
            var pep = columns.Get<double>(@"pep");
            var fitted = columns.Get<bool>(@"mp_fitted");
            var residualMad = columns.Get<double>(@"mp_residual_mad");
            var sameApexClaimants = columns.Get<int>(@"n_same_apex_claimants");
            var coelutingClaimants = columns.Get<int>(@"n_coeluting_claimants");
            var ionMz = columns.Get<byte[]>(@"ion_mz");
            var flags = columns.Get<byte[]>(@"ion_flags");
            var apex = columns.Get<byte[]>(@"apex_intensity");
            var apexError = columns.Get<byte[]>(@"apex_mz_error");
            var library = columns.Get<byte[]>(@"library_rel_intensity");
            var finite = columns.Get<byte[]>(@"n_finite_scans");
            var xicStart = columns.Get<byte[]>(@"xic_start");
            var xicEnd = columns.Get<byte[]>(@"xic_end");
            var xicMax = columns.Get<byte[]>(@"xic_max");
            var corrPolish = columns.Get<byte[]>(@"corr_polish");
            var corrReference = columns.Get<byte[]>(@"corr_reference");
            var rowEffect = columns.Get<byte[]>(@"polish_row_effect");
            var r2 = columns.Get<byte[]>(@"polish_r2");
            var positiveResidualMax = columns.Get<byte[]>(@"polish_pos_resid_max");
            var apexResidual = columns.Get<byte[]>(@"polish_apex_residual");
            var outlierZ = columns.Get<byte[]>(@"polish_outlier_z");
            var apexRatio = columns.Get<byte[]>(@"polish_apex_ratio");
            var relIntensity = columns.Get<byte[]>(@"polish_rel_intensity");
            var sharedApex = columns.Get<byte[]>(@"shared_apex_n");
            var sharedCoelute = columns.Get<byte[]>(@"shared_coelute_n");
            var minClaimantQ = columns.Get<byte[]>(@"min_claimant_q");
            for (int i = 0; i < columns.RowCount; i++)
            {
                var record = new OspreyTrainingRecord
                {
                    FileName = files[i],
                    EntryId = entryIds[i],
                    IsEntrapment = isEntrapment[i],
                    Sequence = sequences[i],
                    ModifiedSequence = modifiedSequences[i],
                    ModPositions = Blob<int>(modPositions[i]),
                    ModMasses = Blob<double>(modMasses[i]),
                    ModUnimodIds = Blob<int>(modUnimods[i]),
                    Charge = charges[i],
                    PrecursorMz = precursorMz[i],
                    ProteinIds = proteins[i],
                    ApexRt = apexRt[i],
                    StartRt = startRt[i],
                    EndRt = endRt[i],
                    PeakScanCount = peakScans[i],
                    Score = scores[i],
                    RunPrecursorQ = runQ[i],
                    ExperimentPrecursorQ = experimentQ[i],
                    Pep = pep[i],
                    MedianPolishFitted = fitted[i],
                    MedianPolishResidualMad = residualMad[i],
                    SameApexClaimantCount = sameApexClaimants[i],
                    CoelutingClaimantCount = coelutingClaimants[i],
                    IonMz = Blob<double>(ionMz[i]),
                    IonFlags = flags[i] ?? Array.Empty<byte>(),
                    ApexIntensity = Blob<float>(apex[i]),
                    ApexMzError = Blob<float>(apexError[i]),
                    LibraryRelIntensity = Blob<float>(library[i]),
                    FiniteScanCount = Blob<ushort>(finite[i]),
                    XicStart = Blob<float>(xicStart[i]),
                    XicEnd = Blob<float>(xicEnd[i]),
                    XicMax = Blob<float>(xicMax[i]),
                    CorrPolish = Blob<float>(corrPolish[i]),
                    CorrReference = Blob<float>(corrReference[i]),
                    PolishRowEffect = Blob<float>(rowEffect[i]),
                    PolishR2 = Blob<float>(r2[i]),
                    PolishPositiveResidualMax = Blob<float>(positiveResidualMax[i]),
                    PolishApexResidual = Blob<float>(apexResidual[i]),
                    PolishOutlierZ = Blob<float>(outlierZ[i]),
                    PolishApexRatio = Blob<float>(apexRatio[i]),
                    PolishRelIntensity = Blob<float>(relIntensity[i]),
                    SharedApexCount = sharedApex[i] ?? Array.Empty<byte>(),
                    SharedCoeluteCount = sharedCoelute[i] ?? Array.Empty<byte>(),
                    MinClaimantQ = Blob<float>(minClaimantQ[i]),
                };
                int expected = 4 * (record.Sequence.Length - 1);
                if (record.IonFlags.Length != expected || record.ApexIntensity.Length != expected)
                {
                    throw new InvalidDataException(string.Format(@"{0}: {1} has {2} ion slots, expected {3}.",
                        path, record.ModifiedSequence, record.IonFlags.Length, expected));
                }
                records.Add(record);
            }
            return new OspreyTrainingExport(path, records, columns.Metadata);
        }

        /// <summary>An export's key-value footer, read without its rows.</summary>
        public static IReadOnlyDictionary<string, string> ReadFooter(string path)
        {
            return ParquetColumns.ReadMetadata(path);
        }

        private OspreyTrainingExport(string path, IReadOnlyList<OspreyTrainingRecord> records, IReadOnlyDictionary<string, string> metadata)
        {
            Path = path;
            Records = records;
            Metadata = metadata;
        }

        public string Path { get; }

        public IReadOnlyList<OspreyTrainingRecord> Records { get; }

        public IReadOnlyDictionary<string, string> Metadata { get; }

        /// <summary>The run's last MS2 retention time, minutes (<c>osprey.rt_max</c>).</summary>
        public double RtMax
        {
            get { return GetDouble(@"osprey.rt_max"); }
        }

        /// <summary>The instrument model the run info reports, or null.</summary>
        public string InstrumentModel
        {
            get { return Metadata.TryGetValue(@"osprey.instrument_model", out string model) && model.Length > 0 ? model : null; }
        }

        /// <summary>The range of the run's isolation windows (<c>osprey.isolation_mz_min/max</c>).</summary>
        public (double Lower, double Upper) IsolationRange
        {
            get { return (GetDouble(@"osprey.isolation_mz_min"), GetDouble(@"osprey.isolation_mz_max")); }
        }

        /// <summary>The run's MS2 scan window (<c>osprey.ms2_scan_window</c>), or null when unknown.</summary>
        public (double Lower, double Upper)? Ms2ScanWindow
        {
            get
            {
                if (!Metadata.TryGetValue(@"osprey.ms2_scan_window", out string text) || string.IsNullOrEmpty(text))
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

        /// <summary>
        /// The collision energy most MS2 spectra were acquired at (<c>osprey.collision_energies</c>),
        /// or null when the run info is missing.
        /// </summary>
        public double? DominantCollisionEnergy
        {
            get
            {
                if (!Metadata.TryGetValue(@"osprey.collision_energies", out string json) || string.IsNullOrEmpty(json))
                    return null;
                using (var document = System.Text.Json.JsonDocument.Parse(json))
                {
                    double? best = null;
                    long bestCount = -1;
                    foreach (var property in document.RootElement.EnumerateObject())
                    {
                        long count = property.Value.GetInt64();
                        if (count > bestCount &&
                            double.TryParse(property.Name, NumberStyles.Float, CultureInfo.InvariantCulture, out double energy))
                        {
                            best = energy;
                            bestCount = count;
                        }
                    }
                    return best;
                }
            }
        }

        private double GetDouble(string key)
        {
            if (!Metadata.TryGetValue(key, out string text))
                throw new InvalidDataException(string.Format(@"{0} has no {1} footer key.", Path, key));
            return double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static T[] Blob<T>(byte[] bytes) where T : struct
        {
            if (bytes == null || bytes.Length == 0)
                return Array.Empty<T>();
            if (!BitConverter.IsLittleEndian)
                throw new PlatformNotSupportedException(@"Training export blobs are little-endian.");
            return MemoryMarshal.Cast<byte, T>(bytes).ToArray();
        }
    }
}
