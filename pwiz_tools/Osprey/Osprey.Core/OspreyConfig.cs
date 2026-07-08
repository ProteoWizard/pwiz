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

namespace pwiz.Osprey.Core
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

        /// <summary>
        /// Optional: write an FDRBench-compatible input TSV to this path. Includes every reported
        /// (compaction-surviving) target, i.e. the peptides actually written to the output, regardless
        /// of q-value, with the raw SVM discriminant as <c>score</c>. The level
        /// is taken from <see cref="FdrLevel"/> (peptide, or precursor for precursor/both).
        /// </summary>
        public string OutputFdrBench { get; set; }

        /// <summary>
        /// With <see cref="OutputFdrBench"/>: emit one row per (precursor, run) using run-level
        /// q-values (adds a <c>run</c> column). Default is one row per precursor using
        /// experiment-level q-values.
        /// </summary>
        public bool FdrBenchPerRun { get; set; }

        /// <summary>
        /// With <see cref="OutputFdrBench"/>: which FDR pass the emitted rows and q-values
        /// come from. <c>2</c> (default) is the post-compaction, second-pass survivors written
        /// to the blib output -- the FDR of what Osprey actually reports. <c>1</c> is the
        /// full pre-compaction first-pass pool (every scored target, regardless of q-value)
        /// with its first-pass q-values, mirroring Rust osprey's
        /// <c>write_fdrbench_peptide_input</c> -- the assumption the second-pass output rests
        /// on. Pass 1 is emitted from the first-join stage before compaction; pass 2 from the
        /// merge node after rescoring.
        /// </summary>
        public int FdrBenchPass { get; set; } = 2;

        /// <summary>
        /// Optional base directory for all per-file <em>derived</em> artifacts
        /// (<c>.scores.parquet</c>, <c>.calibration.json</c>,
        /// <c>.scores-reconciled.parquet</c>, the FDR sidecars, and
        /// <c>.reconciliation.json</c>). Null = write each artifact in its input
        /// file's own directory (the historical behavior). Set by
        /// <c>--output-dir</c> (or <c>--work-dir</c>), it lets an analysis read
        /// read-only input data while writing only derived output elsewhere.
        /// Maps to Rust <c>OspreyConfig::output_dir</c> (Track B).
        /// </summary>
        public string OutputDir { get; set; }

        /// <summary>
        /// Optional directory for the <c>.spectra.bin</c> cache only. Null =
        /// resolve at write time: beside the data file if that directory is
        /// writable, else <see cref="OutputDir"/> (or the input file's own
        /// directory when <see cref="OutputDir"/> is also null). Set by
        /// <c>--cache-dir</c> (or <c>--work-dir</c>). The cache is
        /// settings-independent, so a shared CacheDir lets many analyses reuse
        /// a single parse of the spectra.
        /// Maps to Rust <c>OspreyConfig::cache_dir</c> (Track B).
        /// </summary>
        public string CacheDir { get; set; }

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

        /// <summary>
        /// Whether library already contains decoys. When true (or when
        /// <see cref="DecoyMethod"/> = <see cref="DecoyMethod.FromLibrary"/>),
        /// <c>DecoyGenerator</c> is skipped and existing entries are
        /// scanned for <see cref="DecoyPrefixes"/> matches on their
        /// protein accessions; matching entries get
        /// <see cref="LibraryEntry.IsDecoy"/> = true and the high bit of
        /// their <see cref="LibraryEntry.Id"/> set.
        /// </summary>
        public bool DecoysInLibrary { get; set; }

        /// <summary>
        /// Protein-accession prefixes that identify decoys when the
        /// library already contains them (case-insensitive). Default
        /// covers the three common conventions: Osprey's own
        /// <c>DECOY_</c>, plus <c>rev_</c> / <c>decoy_</c> used by tools
        /// like DIA-NN, EncyclopeDIA, and Carafe.
        /// Maps to Rust <c>OspreyConfig::decoy_prefixes</c>.
        /// </summary>
        public List<string> DecoyPrefixes { get; set; } = new List<string>
        {
            @"DECOY_",
            @"rev_",
            @"decoy_",
        };

        /// <summary>
        /// Minimum fraction of decoys that must pair successfully with a
        /// target when <see cref="DecoysInLibrary"/> is set. Below this,
        /// Osprey bails with a clear error rather than running with
        /// broken target-decoy competition (FDR would be optimistic).
        /// Maps to Rust <c>OspreyConfig::decoy_pair_min_fraction</c>.
        /// </summary>
        public double DecoyPairMinFraction { get; set; } = 0.80;

        /// <summary>
        /// Optional path to a FDRBench-style pairing manifest (5-column
        /// TSV: <c>sequence, decoy, proteins, peptide_type,
        /// peptide_pair_index</c>). When set together with
        /// <see cref="DecoysInLibrary"/>, the pipeline runs manifest-based
        /// pairing first and then falls back to composition-based pairing
        /// for decoys the manifest didn't cover. Recommended for
        /// FDRBench-generated entrapment libraries.
        /// Maps to Rust <c>OspreyConfig::decoy_pairing_manifest</c>.
        /// </summary>
        public string DecoyPairingManifestPath { get; set; }

        /// <summary>FDR method: native Percolator (default), external mokapot, or simple target-decoy.</summary>
        public FdrMethod FdrMethod { get; set; } = FdrMethod.Percolator;

        /// <summary>Write PIN files for external tools.</summary>
        public bool WritePin { get; set; }

        /// <summary>
        /// -d / --diagnostics: master switch that turns on the cross-impl
        /// bisection dump bundle (see <c>OspreyDiagnostics.Initialize</c>).
        /// Runtime toggle only -- intentionally NOT part of any identity hash.
        /// </summary>
        public bool Diagnostics { get; set; }

        /// <summary>
        /// --model-diagnostics: emit a single self-contained interactive HTML
        /// report of the trained scoring model + FDR calibration (feature
        /// contributions, target/decoy/entrapment score densities, q-value to
        /// FDP calibration, paired decoy-win fraction) when first-pass FDR
        /// completes. A user-facing deliverable, distinct from the -d bisection
        /// dumps; opt-in and off the default output path (writes only its own
        /// HTML file), so it does not affect any other output. Runtime toggle
        /// only -- intentionally NOT part of any identity hash.
        /// </summary>
        public bool ModelDiagnostics { get; set; }

        /// <summary>
        /// --timestamp: prefix each output line with [yyyy/MM/dd HH:mm:ss]. Runtime
        /// output decoration only -- not part of any identity hash.
        /// </summary>
        public bool IsTimeStamped { get; set; }

        /// <summary>
        /// --memstamp: prefix each output line with managed and private memory (MB).
        /// Pairs with <see cref="IsTimeStamped"/> for perfviz. Runtime-only.
        /// </summary>
        public bool IsMemStamped { get; set; }

        /// <summary>
        /// --log-file: redirect all output to this file instead of stderr. Null leaves
        /// output on stderr. Runtime-only.
        /// </summary>
        public string LogFilePath { get; set; }

        /// <summary>
        /// --perf-stats: emit the machine-parseable [COUNT]/[TIMING]/[STAGE-WALL] lines for
        /// the perf tools (Test-PerfGate.ps1, Measure-Pipeline.ps1). Off by default so the
        /// human log stays clean. Runtime-only.
        /// </summary>
        public bool PerfStats { get; set; }

        /// <summary>
        /// --verbose: show implementer-grade detail that is hidden by default (e.g. the
        /// per-fold Percolator training iterations, which the default log collapses to one
        /// line per round). Runtime-only.
        /// </summary>
        public bool Verbose { get; set; }

        /// <summary>Inter-replicate peak reconciliation settings.</summary>
        public ReconciliationConfig Reconciliation { get; set; } = new ReconciliationConfig();

        /// <summary>Enable the coelution signal pre-filter.</summary>
        public bool PrefilterEnabled { get; set; } = true;

        /// <summary>
        /// Protein-level FDR threshold. Optional on the command line
        /// (<c>--protein-fdr</c>); when unset, <see cref="EffectiveProteinFdr"/>
        /// falls back to <see cref="DefaultProteinFdr"/>. To match Rust osprey
        /// (where <c>config.protein_fdr</c> is a plain f64, default 0.01, and the
        /// protein-FDR machinery runs unconditionally), the presence of this value
        /// no longer gates whether protein parsimony / picked-protein FDR / the
        /// second Percolator pass run -- those always run. It only sets the
        /// threshold used for the passing-group count and <c>--fdr-level protein</c>
        /// output filtering.
        /// </summary>
        public double? ProteinFdr { get; set; }

        /// <summary>Default protein-FDR threshold applied when <c>--protein-fdr</c>
        /// is not supplied, matching Rust <c>config.protein_fdr</c> (default 0.01).</summary>
        public const double DefaultProteinFdr = 0.01;

        /// <summary>Protein-FDR threshold actually applied: <see cref="ProteinFdr"/>
        /// when supplied, else <see cref="DefaultProteinFdr"/>. Always defined so the
        /// protein-FDR machinery can run without a null check, matching Rust's
        /// always-present <c>config.protein_fdr</c>.</summary>
        public double EffectiveProteinFdr => ProteinFdr ?? DefaultProteinFdr;

        /// <summary>How to handle shared peptides for protein inference.</summary>
        public SharedPeptideMode SharedPeptides { get; set; } = SharedPeptideMode.All;

        /// <summary>
        /// FDR filtering level. Default <see cref="FdrLevel.Precursor"/> matches
        /// Rust osprey-core/src/config.rs (FdrLevel::default() = Precursor).
        /// Cross-impl bisection requires identical defaults; the previous
        /// <c>Both</c> default silently shifted every downstream q-value-gated
        /// step (compaction, Stage 7 detected-peptides filter, blib output)
        /// toward a stricter pool than Rust uses.
        /// </summary>
        public FdrLevel FdrLevel { get; set; } = FdrLevel.Precursor;

        /// <summary>
        /// INNER per-file main-search thread budget. Set by <c>--threads</c>.
        /// Divided across concurrent files (see <see cref="FileParallelism"/>)
        /// so total thread demand stays near the core count.
        /// </summary>
        public int NThreads { get; set; } = Environment.ProcessorCount;

        /// <summary>
        /// OUTER across-files parallelism request, set by <c>--parallel-files</c>.
        /// Default <see cref="Core.FileParallelism.Sequential"/> (one file at a
        /// time) is safe on any machine; <c>--parallel-files</c> opts into
        /// RAM/CPU-aware auto or an explicit count. Resolved to a concrete
        /// concurrent-file count at run time by <see cref="FileParallelismResolver"/>;
        /// not part of any identity / cache hash (a per-run scheduling decision).
        /// </summary>
        public FileParallelism FileParallelism { get; set; } = FileParallelism.Sequential;

        /// <summary>
        /// Pipeline-membership flag (read by each task's <c>IsIncluded</c>):
        /// include only the per-file fan-out, not the join. Set by both
        /// <c>--task PerFileScoring</c> and <c>--task PerFileRescoring</c>; the
        /// concrete behavior depends on the input type. With <c>-i</c> mzML it
        /// is the Stage 1-4 worker — each input produces a
        /// <c>{stem}.scores.parquet</c> next to it, no FDR, no blib. With
        /// <see cref="InputScores"/> it is the Stage 6 rescore worker. The two
        /// are told apart by input type (see <see cref="SelectedTask"/>).
        /// </summary>
        public bool NoJoin { get; set; }

        /// <summary>
        /// HPC scoring split: when set (non-null, non-empty), skip Stages 1-4
        /// entirely and load these per-file scoring caches as the starting
        /// point for Stage 5+. Set by <c>--input-scores</c>. When set,
        /// <see cref="InputFiles"/> is ignored.
        /// </summary>
        public List<string> InputScores { get; set; }

        /// <summary>
        /// HPC: when true, exit after Stage 5 + reconciliation planning,
        /// having written the boundary files
        /// (<c>&lt;stem&gt;.&lt;phase&gt;-pass.fdr_scores.bin</c> and
        /// <c>&lt;stem&gt;.reconciliation.json</c>) for each input file.
        /// Skips Stage 6 + 7 + 8. Set by <c>--task FirstPassFDR</c>.
        /// </summary>
        public bool StopAfterStage5 { get; set; }

        /// <summary>
        /// HPC: when true, every <c>--input-scores</c> parquet must carry
        /// <c>osprey.reconciled = "true"</c> in its footer metadata. Set
        /// by <c>--task SecondPassFDR</c>; the post-Stage-6 (reconciled)
        /// entry point. Stages 1-6 are skipped: the pipeline loads
        /// reconciled scores + the <c>.{1st,2nd}-pass.fdr_scores.bin</c>
        /// sidecars, then runs Stages 7-8 (second-pass FDR overlay,
        /// protein parsimony + picked-protein FDR, blib output). Mirrors
        /// Rust's <c>config.expect_reconciled_input</c>.
        /// </summary>
        public bool ExpectReconciledInput { get; set; }

        /// <summary>
        /// The single pipeline task selected by <c>--task &lt;Name&gt;</c> on the
        /// CLI, or null for the full pipeline (no <c>--task</c>). The three
        /// membership flags above (<see cref="NoJoin"/>,
        /// <see cref="StopAfterStage5"/>, <see cref="ExpectReconciledInput"/>)
        /// are derived from this and drive each task's <c>IsIncluded</c>; this
        /// property additionally lets argument validation enforce the
        /// task&#8596;input-type contract (e.g. PerFileScoring takes mzML,
        /// PerFileRescore takes <see cref="InputScores"/>) and name the task the
        /// user actually typed in error messages.
        /// </summary>
        public HpcTask? SelectedTask { get; set; }

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
        /// The bit-parity-critical identity hashing for this run. Split out
        /// of <see cref="OspreyConfig"/> into <see cref="SearchIdentity"/>
        /// so this type is only the configuration bag and the SHA hashing is
        /// its own single-responsibility unit. A fresh instance is returned
        /// per access; it reads this config's hash-affecting fields at call
        /// time, preserving the historical behavior of the former instance
        /// methods. The hash recipes live on <see cref="SearchIdentity"/>
        /// and MUST stay byte-identical with Rust.
        /// </summary>
        public SearchIdentity Identity => new SearchIdentity(this);
    }

    /// <summary>
    /// A single HPC pipeline task selectable via <c>--task &lt;Name&gt;</c>
    /// (one HPC node = one task). The names are the stable CLI contract and
    /// match each task's <c>OspreyTask.Name</c>.
    /// </summary>
    public enum HpcTask
    {
        PerFileScoring,
        FirstJoin,
        PerFileRescore,
        MergeNode
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
