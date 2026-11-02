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
using pwiz.Common.Pipeline;
using pwiz.Common.SystemUtil;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Per-run <c>&lt;stem&gt;.reconciliation.json</c>: the run's Stage 6 plan. Written by
    /// <c>PerFileRescoring</c> as it plans the run, just before rescoring it, and read by
    /// <c>SecondPassFDR</c> for the run's gap-fill entry ids. Carries the non-Keep
    /// reconciliation actions for the run (split into two homogeneous arrays so the JSON has
    /// no discriminator field gymnastics), the run's gap-fill targets, and its refined RT
    /// calibration - only content specific to the run it is named for.
    ///
    /// Descends from <c>osprey/crates/osprey/src/reconciliation_io.rs</c>. Field declaration
    /// order is alphabetical at every nesting level (as in Rust). All <see cref="double"/>
    /// values are routed through <see cref="RoundtripDoubleConverter"/>, so every f64 is
    /// emitted in the same canonical fixed-point decimal form as Rust's custom formatter. The
    /// Rust file also carries fields this one dropped at v4; a cross-impl comparison strips
    /// them, along with the validity stamp.
    /// </summary>
    public class ReconciliationFile
    {
        public const string EXT = @".reconciliation.json";

        /// <summary>
        /// Current schema version. Bump on incompatible changes.
        ///
        /// v1: initial format.
        /// v2: added <c>file_stems</c>, the join's file set.
        /// v3: added <c>first_pass_base_ids</c>, the join-wide first-pass passing base_ids.
        /// v4: removed <c>file_stems</c>, <c>first_pass_base_ids</c>, <c>library_hash</c> and
        ///     <c>search_hash</c>. Each was the same in every run's file and nothing read it:
        ///     the base_ids moved to the analysis-wide retained summary, and the embedded
        ///     validity stamp covers search and library identity. On an 82-run cohort the base
        ///     ids alone were ~466 MB of identical copies.
        /// </summary>
        public const int CurrentFormatVersion = 4;

        // JSON key a load error names, so a user can find it in the file.
        private const string KEY_FORMAT_VERSION = @"format_version";

        /// <summary>
        /// The writing task's <see cref="ArtifactStamp"/>, serialized FIRST so a resume check
        /// reads it without parsing the rest of a multi-megabyte document
        /// (<see cref="ArtifactStamp.TryReadJsonHead"/>). Set by <see cref="Save"/>.
        /// </summary>
        [JsonProperty(ArtifactStamp.JSON_PROPERTY, Order = -100)]
        public string OspreyValidity { get; set; }

        [JsonProperty(@"forced_integration_actions", Order = 0)]
        public List<ForcedIntegrationEntry> ForcedIntegrationActions { get; set; }

        [JsonProperty(KEY_FORMAT_VERSION, Order = 1)]
        public int FormatVersion { get; set; }

        [JsonProperty(@"gap_fill_targets", Order = 2)]
        public List<GapFillEntry> GapFillTargets { get; set; }

        [JsonProperty(@"refined_rt_calibration", Order = 3, NullValueHandling = NullValueHandling.Include)]
        public RefinedRtCalibrationJson RefinedRtCalibration { get; set; }

        [JsonProperty(@"use_cwt_peak_actions", Order = 4)]
        public List<UseCwtPeakEntry> UseCwtPeakActions { get; set; }

        /// <summary>
        /// Read a reconciliation file and validate its
        /// <c>format_version</c>. Throws on missing file, malformed JSON,
        /// or unsupported version.
        /// </summary>
        public static ReconciliationFile Load(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException(@"path must not be null or empty", nameof(path));
            if (!File.Exists(path))
                throw new FileNotFoundException(string.Format(OspreyIOResources.ReconciliationFile_Load_Reconciliation_file_not_found__, path), path);

            string json = File.ReadAllText(path);
            var parsed = JsonConvert.DeserializeObject<ReconciliationFile>(json);
            if (parsed == null)
                throw new InvalidDataException(string.Format(OspreyIOResources.ReconciliationFile_Load_Reconciliation_file_parsed_as_null__, path));
            if (parsed.FormatVersion != CurrentFormatVersion)
            {
                throw new InvalidDataException(string.Format(
                    OspreyIOResources.ReconciliationFile_Load_Reconciliation_file__0__has_unsupported_format_version__1___expected__2____Delete_this_,
                    path, parsed.FormatVersion, CurrentFormatVersion, KEY_FORMAT_VERSION,
                    @"*" + EXT));
            }
            return parsed;
        }

        /// <summary>
        /// Write the reconciliation file as pretty 2-space-indented JSON
        /// with LF line endings. Stages through a sibling <c>.tmp</c>
        /// file in the same directory and renames into place; this
        /// avoids leaving a partially-written destination on writer
        /// failure, but the rename is not strictly atomic when the
        /// destination already exists (the existing file is removed
        /// first). A crash between the remove and the rename leaves
        /// the <c>.tmp</c> next to the missing destination, recoverable
        /// by hand or by re-running.
        /// </summary>
        public static void Save(string path, ReconciliationFile file, ArtifactStamp stamp)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException(@"path must not be null or empty", nameof(path));
            if (file == null)
                throw new ArgumentNullException(nameof(file));
            file.OspreyValidity = stamp?.ToString() ?? throw new ArgumentNullException(nameof(stamp));

            string parent = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            // LF line endings and a trailing newline, for byte parity with the Rust side.
            string json = RoundtripDoubleConverter.SerializeIndented(file);

            // Atomic write via SimpleFileSaver: a sibling temp file is
            // promoted to the destination on Commit; on exception, the
            // using-block disposes SimpleFileSaver which deletes the temp
            // without touching the destination. See
            // pwiz.Common.SystemUtil.SimpleFileSaver for details.
            using (var saver = new SimpleFileSaver(path))
            {
                File.WriteAllText(saver.SafeName, json);
                saver.Commit();
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
            string stem = Path.GetFileNameWithoutExtension(inputPath) ?? @"unknown";
            // Route through ArtifactPaths so the reconciliation JSON follows the
            // scores parquet into --output-dir (default = the input's own dir),
            // shared by the straight-through writer and the resume reader.
            string parent = ArtifactPaths.ResolveOutputDir(inputPath);
            string filename = stem + EXT;
            return string.IsNullOrEmpty(parent) ? filename : Path.Combine(parent, filename);
        }
    }

    /// <summary>
    /// Wire form of an <c>UseCwtPeak</c> reconciliation action for a
    /// single entry. Field order alphabetical.
    /// </summary>
    public class UseCwtPeakEntry
    {
        [JsonProperty(@"apex_rt", Order = 0)]
        public double ApexRt { get; set; }

        [JsonProperty(@"candidate_idx", Order = 1)]
        public uint CandidateIdx { get; set; }

        [JsonProperty(@"end_rt", Order = 2)]
        public double EndRt { get; set; }

        [JsonProperty(@"entry_id", Order = 3)]
        public uint EntryId { get; set; }

        [JsonProperty(@"start_rt", Order = 4)]
        public double StartRt { get; set; }
    }

    /// <summary>
    /// Wire form of a <c>ForcedIntegration</c> reconciliation action.
    /// Field order alphabetical.
    /// </summary>
    public class ForcedIntegrationEntry
    {
        [JsonProperty(@"entry_id", Order = 0)]
        public uint EntryId { get; set; }

        [JsonProperty(@"expected_rt", Order = 1)]
        public double ExpectedRt { get; set; }

        [JsonProperty(@"half_width", Order = 2)]
        public double HalfWidth { get; set; }
    }

    /// <summary>
    /// Wire form of a <c>GapFillTarget</c>. Field order alphabetical.
    /// </summary>
    public class GapFillEntry
    {
        [JsonProperty(@"charge", Order = 0)]
        public byte Charge { get; set; }

        [JsonProperty(@"decoy_entry_id", Order = 1)]
        public uint DecoyEntryId { get; set; }

        [JsonProperty(@"expected_rt", Order = 2)]
        public double ExpectedRt { get; set; }

        [JsonProperty(@"half_width", Order = 3)]
        public double HalfWidth { get; set; }

        [JsonProperty(@"modified_sequence", Order = 4)]
        public string ModifiedSequence { get; set; }

        [JsonProperty(@"target_entry_id", Order = 5)]
        public uint TargetEntryId { get; set; }
    }

    /// <summary>
    /// Wire form of the refined per-file RT calibration. Carries the
    /// LOESS model parameters; Stage 6 workers reconstruct an in-memory
    /// calibration via <c>RTCalibration.FromModelParams</c>.
    /// </summary>
    public class RefinedRtCalibrationJson
    {
        [JsonProperty(@"abs_residuals", Order = 0)]
        public double[] AbsResiduals { get; set; }

        [JsonProperty(@"fitted_rts", Order = 1)]
        public double[] FittedRts { get; set; }

        [JsonProperty(@"library_rts", Order = 2)]
        public double[] LibraryRts { get; set; }

        [JsonProperty(@"residual_sd", Order = 3)]
        public double ResidualSd { get; set; }
    }
}
