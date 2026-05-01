/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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
using Newtonsoft.Json;

namespace pwiz.OspreySharp.IO
{
    /// <summary>
    /// Per-file <c>&lt;stem&gt;.reconciliation.json</c> Stage 5 → Stage 6
    /// boundary file. Carries everything a Stage 6 worker needs to do
    /// per-file rescore + gap-fill + reconciled parquet write-back without
    /// re-running any of the joined Stage 5 work: the non-Keep
    /// reconciliation actions for the file (split into two homogeneous
    /// arrays so the JSON has no discriminator field gymnastics), the
    /// gap-fill targets for the file, and the refined RT calibration.
    ///
    /// Mirrors <c>osprey/crates/osprey/src/reconciliation_io.rs</c>.
    /// Field declaration order is alphabetical at every nesting level
    /// (matches Rust). All <see cref="double"/> values are routed through
    /// <see cref="RoundtripDoubleConverter"/> on this side and through a
    /// matching custom <c>serde_json</c> formatter on the Rust side, so
    /// every f64 is emitted as the same canonical fixed-point decimal
    /// form on both runtimes — sidestepping the
    /// Newtonsoft-<c>R</c>/Grisu vs. Rust-<c>ryu</c> threshold
    /// disagreement on small values like <c>4.58e-5</c>. Cross-impl byte
    /// parity is verified by a sibling test in each language.
    /// </summary>
    public class ReconciliationFile
    {
        /// <summary>Current schema version. Bump on incompatible changes.</summary>
        public const int CurrentFormatVersion = 1;

        [JsonProperty("forced_integration_actions", Order = 0)]
        public List<ForcedIntegrationEntry> ForcedIntegrationActions { get; set; }

        [JsonProperty("format_version", Order = 1)]
        public int FormatVersion { get; set; }

        [JsonProperty("gap_fill_targets", Order = 2)]
        public List<GapFillEntry> GapFillTargets { get; set; }

        [JsonProperty("library_hash", Order = 3)]
        public string LibraryHash { get; set; }

        [JsonProperty("refined_rt_calibration", Order = 4, NullValueHandling = NullValueHandling.Include)]
        public RefinedRtCalibrationJson RefinedRtCalibration { get; set; }

        [JsonProperty("search_hash", Order = 5)]
        public string SearchHash { get; set; }

        [JsonProperty("use_cwt_peak_actions", Order = 6)]
        public List<UseCwtPeakEntry> UseCwtPeakActions { get; set; }

        /// <summary>
        /// Read a reconciliation file and validate its
        /// <c>format_version</c>. Throws on missing file, malformed JSON,
        /// or unsupported version.
        /// </summary>
        public static ReconciliationFile Load(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path must not be null or empty", nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException("Reconciliation file not found: " + path, path);

            string json = File.ReadAllText(path);
            var parsed = JsonConvert.DeserializeObject<ReconciliationFile>(json);
            if (parsed == null)
                throw new InvalidDataException("Reconciliation file parsed as null: " + path);
            if (parsed.FormatVersion != CurrentFormatVersion)
            {
                throw new InvalidDataException(string.Format(
                    "Reconciliation file {0} has unsupported format_version {1} (expected {2})",
                    path, parsed.FormatVersion, CurrentFormatVersion));
            }
            return parsed;
        }

        /// <summary>
        /// Write the reconciliation file as pretty 2-space-indented JSON
        /// with LF line endings. Stages through a sibling .tmp file then
        /// atomically renames so partial writes don't corrupt the output.
        /// </summary>
        public static void Save(string path, ReconciliationFile file)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path must not be null or empty", nameof(path));
            if (file == null)
                throw new ArgumentNullException(nameof(file));

            string parent = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            var settings = new JsonSerializerSettings
            {
                Converters = { new RoundtripDoubleConverter() },
            };
            string json = JsonConvert.SerializeObject(file, Formatting.Indented, settings);
            // Newtonsoft's Formatting.Indented emits CRLF on Windows by
            // default; normalize to LF so cross-impl byte parity with the
            // Rust side (which always emits LF via serde_json) holds. Also
            // emit a trailing newline so the file ends with `}\n`,
            // matching the explicit newline Rust appends after the
            // serializer.
            json = json.Replace("\r\n", "\n");
            if (!json.EndsWith("\n", StringComparison.Ordinal))
                json += "\n";

            string tmpPath = path + ".tmp";
            try
            {
                File.WriteAllText(tmpPath, json);
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(tmpPath, path);
            }
            finally
            {
                if (File.Exists(tmpPath))
                    File.Delete(tmpPath);
            }
        }

        /// <summary>
        /// Compute the per-file reconciliation JSON path: sibling to the
        /// input mzML at <c>&lt;dir&gt;/&lt;stem&gt;.reconciliation.json</c>.
        /// Mirrors the existing pattern used for
        /// <c>&lt;stem&gt;.calibration.json</c>, etc.
        /// </summary>
        public static string PathForInput(string inputPath)
        {
            string stem = Path.GetFileNameWithoutExtension(inputPath) ?? "unknown";
            string parent = Path.GetDirectoryName(inputPath);
            string filename = stem + ".reconciliation.json";
            return string.IsNullOrEmpty(parent) ? filename : Path.Combine(parent, filename);
        }
    }

    /// <summary>
    /// Wire form of an <c>UseCwtPeak</c> reconciliation action for a
    /// single entry. Field order alphabetical.
    /// </summary>
    public class UseCwtPeakEntry
    {
        [JsonProperty("apex_rt", Order = 0)]
        public double ApexRt { get; set; }

        [JsonProperty("candidate_idx", Order = 1)]
        public uint CandidateIdx { get; set; }

        [JsonProperty("end_rt", Order = 2)]
        public double EndRt { get; set; }

        [JsonProperty("entry_id", Order = 3)]
        public uint EntryId { get; set; }

        [JsonProperty("start_rt", Order = 4)]
        public double StartRt { get; set; }
    }

    /// <summary>
    /// Wire form of a <c>ForcedIntegration</c> reconciliation action.
    /// Field order alphabetical.
    /// </summary>
    public class ForcedIntegrationEntry
    {
        [JsonProperty("entry_id", Order = 0)]
        public uint EntryId { get; set; }

        [JsonProperty("expected_rt", Order = 1)]
        public double ExpectedRt { get; set; }

        [JsonProperty("half_width", Order = 2)]
        public double HalfWidth { get; set; }
    }

    /// <summary>
    /// Wire form of a <c>GapFillTarget</c>. Field order alphabetical.
    /// </summary>
    public class GapFillEntry
    {
        [JsonProperty("charge", Order = 0)]
        public byte Charge { get; set; }

        [JsonProperty("decoy_entry_id", Order = 1)]
        public uint DecoyEntryId { get; set; }

        [JsonProperty("expected_rt", Order = 2)]
        public double ExpectedRt { get; set; }

        [JsonProperty("half_width", Order = 3)]
        public double HalfWidth { get; set; }

        [JsonProperty("modified_sequence", Order = 4)]
        public string ModifiedSequence { get; set; }

        [JsonProperty("target_entry_id", Order = 5)]
        public uint TargetEntryId { get; set; }
    }

    /// <summary>
    /// Wire form of the refined per-file RT calibration. Carries the
    /// LOESS model parameters; Stage 6 workers reconstruct an in-memory
    /// calibration via <c>RTCalibration.FromModelParams</c>.
    /// </summary>
    public class RefinedRtCalibrationJson
    {
        [JsonProperty("abs_residuals", Order = 0)]
        public double[] AbsResiduals { get; set; }

        [JsonProperty("fitted_rts", Order = 1)]
        public double[] FittedRts { get; set; }

        [JsonProperty("library_rts", Order = 2)]
        public double[] LibraryRts { get; set; }

        [JsonProperty("residual_sd", Order = 3)]
        public double ResidualSd { get; set; }
    }
}
