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
using System.Security.Cryptography;
using System.Text;

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Configuration settings for an Osprey analysis run.
    /// Maps to osprey-core/src/config.rs OspreyConfig.
    /// </summary>
    public class OspreyConfig
    {
        /// <summary>Input mzML file paths.</summary>
        public List<string> InputFiles { get; set; } = new List<string>();

        /// <summary>Spectral library source.</summary>
        public LibrarySource LibrarySource { get; set; }

        /// <summary>Primary output: blib for Skyline.</summary>
        public string OutputBlib { get; set; }

        /// <summary>Optional: TSV report output path.</summary>
        public string OutputReport { get; set; }

        /// <summary>Resolution mode for binning.</summary>
        public ResolutionMode ResolutionMode { get; set; } = ResolutionMode.Auto;

        /// <summary>Fragment tolerance for LibCosine scoring.</summary>
        public FragmentToleranceConfig FragmentTolerance { get; set; } = FragmentToleranceConfig.Default();

        /// <summary>Precursor tolerance for MS1 matching.</summary>
        public FragmentToleranceConfig PrecursorTolerance { get; set; } = FragmentToleranceConfig.Default();

        /// <summary>RT calibration configuration.</summary>
        public RTCalibrationConfig RtCalibration { get; set; } = new RTCalibrationConfig();

        /// <summary>Run-level FDR threshold.</summary>
        public double RunFdr { get; set; } = 0.01;

        /// <summary>Experiment-level FDR threshold.</summary>
        public double ExperimentFdr { get; set; } = 0.01;

        /// <summary>Decoy generation method.</summary>
        public DecoyMethod DecoyMethod { get; set; } = DecoyMethod.Reverse;

        /// <summary>Whether library already contains decoys.</summary>
        public bool DecoysInLibrary { get; set; }

        /// <summary>FDR method: native Percolator (default), external mokapot, or simple target-decoy.</summary>
        public FdrMethod FdrMethod { get; set; } = FdrMethod.Percolator;

        /// <summary>Write PIN files for external tools.</summary>
        public bool WritePin { get; set; }

        /// <summary>Inter-replicate peak reconciliation settings.</summary>
        public ReconciliationConfig Reconciliation { get; set; } = new ReconciliationConfig();

        /// <summary>Enable the coelution signal pre-filter.</summary>
        public bool PrefilterEnabled { get; set; } = true;

        /// <summary>Protein-level FDR threshold (enables protein parsimony and picked-protein FDR).</summary>
        public double? ProteinFdr { get; set; }

        /// <summary>How to handle shared peptides for protein inference.</summary>
        public SharedPeptideMode SharedPeptides { get; set; } = SharedPeptideMode.All;

        /// <summary>FDR filtering level for output.</summary>
        public FdrLevel FdrLevel { get; set; } = FdrLevel.Both;

        /// <summary>Number of threads to use.</summary>
        public int NThreads { get; set; } = Environment.ProcessorCount;

        /// <summary>
        /// HPC scoring split: when true, run Stages 1-4 only and exit. Each
        /// input mzML produces a {stem}.scores.parquet next to it; no FDR
        /// is run and no blib is written. Set by the --no-join CLI flag.
        /// Mutually exclusive with <see cref="InputScores"/>.
        /// </summary>
        public bool NoJoin { get; set; }

        /// <summary>
        /// HPC scoring split: when set (non-null, non-empty), skip Stages 1-4
        /// entirely and load these per-file scoring caches as the starting
        /// point for Stage 5+. Set by --join-only + --input-scores. When set,
        /// <see cref="InputFiles"/> is ignored.
        /// </summary>
        public List<string> InputScores { get; set; }

        /// <summary>
        /// How many files will actually run concurrently in the current
        /// invocation. Set by the pipeline before per-file ProcessFile()
        /// calls; used to divide the inner main-search thread budget so
        /// total thread demand stays near core count. Defaults to 1
        /// (no scaling).
        /// </summary>
        public int EffectiveFileParallelism { get; set; } = 1;

        /// <summary>
        /// Shallow clone for per-file ProcessFile() calls. The pipeline
        /// mutates a few fields (notably <see cref="FragmentTolerance"/>
        /// after MS2 calibration); cloning at the top of ProcessFile
        /// isolates each parallel file from the others. References to
        /// inner config objects (RtCalibration, Reconciliation, etc.)
        /// are shared because nothing mutates them per-file.
        /// </summary>
        public OspreyConfig ShallowClone()
        {
            return (OspreyConfig)this.MemberwiseClone();
        }

        /// <summary>
        /// Compute SHA-256 hash of parameters that affect first-pass scoring.
        /// If this hash changes, cached .scores.parquet files are invalid.
        /// </summary>
        public string SearchParameterHash()
        {
            // Cross-impl bit-equivalence with Rust requires:
            //  - Booleans: Rust prints "true"/"false" (lowercase). C# default
            //    bool.ToString() is "True"/"False". Use lowercase explicitly.
            //  - Numbers: invariant culture (no locale-dependent separators).
            using (var sha256 = SHA256.Create())
            {
                var ic = System.Globalization.CultureInfo.InvariantCulture;
                Func<bool, string> b = v => v ? "true" : "false";
                var sb = new StringBuilder();
                sb.AppendFormat(ic, "resolution_mode:{0}\n", ResolutionMode);
                sb.AppendFormat(ic, "fragment_tolerance:{0},{1}\n", FragmentTolerance.Tolerance, FragmentTolerance.Unit);
                sb.AppendFormat(ic, "precursor_tolerance:{0},{1}\n", PrecursorTolerance.Tolerance, PrecursorTolerance.Unit);
                sb.AppendFormat(ic, "prefilter_enabled:{0}\n", b(PrefilterEnabled));
                sb.AppendFormat(ic, "decoy_method:{0}\n", DecoyMethod);
                sb.AppendFormat(ic, "decoys_in_library:{0}\n", b(DecoysInLibrary));
                sb.AppendFormat(ic, "rt_cal.enabled:{0}\n", b(RtCalibration.Enabled));
                sb.AppendFormat(ic, "rt_cal.fallback_rt_tolerance:{0}\n", RtCalibration.FallbackRtTolerance);
                sb.AppendFormat(ic, "rt_cal.rt_tolerance_factor:{0}\n", RtCalibration.RtToleranceFactor);
                sb.AppendFormat(ic, "rt_cal.min_rt_tolerance:{0}\n", RtCalibration.MinRtTolerance);
                sb.AppendFormat(ic, "rt_cal.max_rt_tolerance:{0}\n", RtCalibration.MaxRtTolerance);
                sb.AppendFormat(ic, "rt_cal.loess_bandwidth:{0}\n", RtCalibration.LoessBandwidth);
                sb.AppendFormat(ic, "rt_cal.min_calibration_points:{0}\n", RtCalibration.MinCalibrationPoints);
                sb.AppendFormat(ic, "rt_cal.calibration_sample_size:{0}\n", RtCalibration.CalibrationSampleSize);
                sb.AppendFormat(ic, "rt_cal.calibration_retry_factor:{0}\n", RtCalibration.CalibrationRetryFactor);
                sb.AppendFormat(ic, "reconciliation.top_n_peaks:{0}\n", Reconciliation.TopNPeaks);

                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                var result = new StringBuilder(64);
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    result.Append(hashBytes[i].ToString("x2"));
                }
                return result.ToString();
            }
        }

        /// <summary>
        /// Compute a fast identity hash for the library file (file name + size
        /// + mtime). Filesystem metadata only -- no content hashing. The
        /// directory portion is deliberately NOT in the hash so the same
        /// library identifies identically across Rust / .NET / OS variations
        /// (drive letter case, forward vs back slash, relative vs absolute,
        /// HPC node-local vs shared paths). Mirrors the
        /// <c>reconciliation_parameter_hash</c> precedent that hashes only
        /// sorted file stems for the input set. Same recipe as Rust's
        /// <c>library_identity_hash</c>.
        /// </summary>
        public string LibraryIdentityHash()
        {
            string libPath = LibrarySource != null ? LibrarySource.Path : string.Empty;
            using (var sha256 = SHA256.Create())
            {
                var sb = new StringBuilder();
                string fileName = string.IsNullOrEmpty(libPath)
                    ? string.Empty
                    : Path.GetFileName(libPath);
                sb.AppendFormat("file_name:{0}\n", fileName);
                if (!string.IsNullOrEmpty(libPath) && File.Exists(libPath))
                {
                    var info = new FileInfo(libPath);
                    sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                        "size:{0}\n", info.Length);
                    // Unix seconds matching Rust's library_identity_hash
                    // (SystemTime::duration_since(UNIX_EPOCH).as_secs()).
                    long mtimeSecs = (long)(info.LastWriteTimeUtc
                        - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                    sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                        "mtime:{0}\n", mtimeSecs);
                }
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                var result = new StringBuilder(64);
                for (int i = 0; i < hashBytes.Length; i++)
                    result.Append(hashBytes[i].ToString("x2"));
                return result.ToString();
            }
        }
    }

    /// <summary>
    /// Method used to generate decoy sequences.
    /// Maps to osprey-core/src/types.rs DecoyMethod.
    /// </summary>
    public enum DecoyMethod
    {
        Reverse,
        Shuffle,
        FromLibrary
    }

    /// <summary>
    /// Level at which FDR is controlled.
    /// Maps to osprey-core/src/types.rs FdrLevel.
    /// </summary>
    public enum FdrLevel
    {
        Precursor,
        Peptide,
        Both
    }

    /// <summary>
    /// Statistical method for FDR estimation.
    /// Maps to osprey-core/src/types.rs FdrMethod.
    /// </summary>
    public enum FdrMethod
    {
        Percolator,
        Mokapot,
        Simple
    }

    /// <summary>
    /// Mass spectrometer resolution mode.
    /// Maps to osprey-core/src/types.rs ResolutionMode.
    /// </summary>
    public enum ResolutionMode
    {
        Auto,
        UnitResolution,
        HRAM
    }

    /// <summary>
    /// How shared peptides are handled during protein inference.
    /// Maps to osprey-core/src/types.rs SharedPeptideMode.
    /// </summary>
    public enum SharedPeptideMode
    {
        All,
        Razor,
        Unique
    }
}
