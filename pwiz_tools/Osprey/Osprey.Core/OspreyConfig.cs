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
using System.Linq;

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

        /// <summary>At least one input was named on the command line.</summary>
        public bool HasInputFiles => InputFiles != null && InputFiles.Count > 0;

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

        /// <summary>Bit for the pre-compaction first-pass pool in <see cref="FdrBenchPass"/>.</summary>
        public const int FDRBENCH_PASS_1 = 1;
        /// <summary>Bit for the post-compaction reported set in <see cref="FdrBenchPass"/>.</summary>
        public const int FDRBENCH_PASS_2 = 2;

        /// <summary>
        /// With <see cref="OutputFdrBench"/>: which FDR pass(es) the emitted rows and q-values
        /// come from, as a bitmask of <see cref="FDRBENCH_PASS_1"/> and
        /// <see cref="FDRBENCH_PASS_2"/>. <c>2</c> (default) is the post-compaction, second-pass
        /// survivors written to the blib output -- the FDR of what Osprey actually reports.
        /// <c>1</c> is the full pre-compaction first-pass pool (every scored target, regardless
        /// of q-value) with its first-pass q-values, mirroring Rust osprey's
        /// <c>write_fdrbench_peptide_input</c> -- the assumption the second-pass output rests on.
        /// <c>3</c> (both) emits both in one run; because a single <see cref="OutputFdrBench"/>
        /// path is given, each pass is written with a <c>.pass1</c> / <c>.pass2</c> stem suffix so
        /// they do not overwrite each other (see <c>FdrBenchInputWriter.PathForPass</c>). Pass 1
        /// is emitted from the FirstPassFDR stage before compaction; pass 2 from SecondPassFDR
        /// after rescoring.
        /// </summary>
        public int FdrBenchPass { get; set; } = FDRBENCH_PASS_2;

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

        /// <summary>
        /// Peptide q-value threshold for first-pass compaction. Peptides whose
        /// first-pass peptide q-value is at or below this threshold survive
        /// compaction and remain available for reconciliation and second-pass FDR.
        /// Default 0.01 matches <see cref="RunFdr"/>; loosening it (e.g. to 0.05)
        /// broadens the reconciliation pool but risks second-pass FDR inflation
        /// (Percolator re-trains on an enriched set). Mirrors Rust
        /// config.reconciliation_compaction_fdr. Peptides whose protein group passes
        /// first-pass protein FDR are additionally rescued regardless of this threshold.
        /// </summary>
        public double ReconciliationCompactionFdr { get; set; } = 0.01;

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

        /// <summary>
        /// Write the protein-group report (<c>&lt;output&gt;.protein_groups.tsv</c>) at the
        /// end of the run: one row per target protein group with its member accessions
        /// (how the proteins were grouped), the unique (representative) and shared
        /// peptides supporting it, the group q-value, and whether it passes protein FDR.
        /// ON by default -- it is the user-facing answer to "which proteins did you
        /// detect, and on what evidence"; the former <c>cs_stage7_protein_fdr.tsv</c> is a
        /// counts-only cross-impl diagnostic, not this. There is no CLI switch to turn it
        /// off (only <c>--diagnostics-only</c> or an absent <c>-o</c> skips it). Additive
        /// (a new file), so byte-parity gates that compare the blib + Stage-7 dump are
        /// unaffected.
        /// </summary>
        public bool WriteProteinReport { get; set; } = true;

        /// <summary>
        /// Write the summary report (<c>&lt;output&gt;.stats.tsv</c>): one row per replicate
        /// with its precursors, peptides, and protein groups passing FDR, plus a final
        /// experiment-level row. Modeled on DIA-NN's per-run <c>stats.tsv</c>, with the
        /// per-replicate protein count computed by an INDEPENDENT run-level protein FDR
        /// (its own parsimony + picked-protein FDR on that replicate) so it is a true
        /// per-run number, not a slice of the experiment set. ON by default, with no CLI
        /// switch to turn it off. Additive, so byte-parity gates are unaffected.
        /// </summary>
        public bool WriteSummaryReport { get; set; } = true;

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
        /// HPC: when true, exit after Stage 5 + reconciliation planning,
        /// having written the boundary files
        /// (<c>&lt;stem&gt;.&lt;phase&gt;-pass.fdr_scores.bin</c> and
        /// <c>&lt;stem&gt;.reconciliation.json</c>) for each input file.
        /// Skips Stage 6 + 7 + 8. Set by <c>--task FirstPassFDR</c>. A behavior flag the
        /// first-pass task's own arms read (planning ends the run; no survivor loader is
        /// built) - not a membership flag: which stages run is <see cref="Includes"/>.
        /// </summary>
        public bool StopAfterStage5 { get; set; }

        /// <summary>
        /// HPC: when true, every run's reconciled parquet must carry
        /// <c>osprey.reconciled = "true"</c> in its footer metadata. Set
        /// by <c>--task SecondPassFDR</c>; the post-Stage-6 (reconciled)
        /// entry point. Stages 1-6 are skipped: the pipeline loads
        /// reconciled scores + the <c>.{1st,2nd}-pass.fdr_scores.bin</c>
        /// sidecars, then runs Stages 7-8 (second-pass FDR overlay,
        /// protein parsimony + picked-protein FDR, blib output). Mirrors
        /// Rust's <c>config.expect_reconciled_input</c>. A behavior flag (the strict
        /// reconciled-footer gate lives below the task library and reads it here) - not a
        /// membership flag: which stages run is <see cref="Includes"/>.
        /// </summary>
        public bool ExpectReconciledInput { get; set; }

        /// <summary>
        /// The single task selected by <c>--task &lt;Name&gt;</c> on the CLI, or null for
        /// the full pipeline (no <c>--task</c>). Set only through <see cref="SelectTask"/>,
        /// together with the <see cref="Pipeline"/> it runs and the flags it implies, so the
        /// CLI path cannot set one without the others or leave a previous selection's behind.
        /// The instance is the task itself - the same one the pipeline runs - so a task can
        /// ask whether it IS the selection by reference, and what it is is answered by the
        /// task through <see cref="ISelectableTask"/> rather than by a switch over its name.
        /// It has no input-KIND contract to enforce: every task takes the same data files,
        /// and the second seam that said "you handed me parquets, so Stage 1-4 is done" has
        /// retired.
        /// </summary>
        public ISelectableTask SelectedTask { get; private set; }

        /// <summary>
        /// The stages this run walks, in execution order: the pipeline the selection was
        /// resolved against, or the canonical pipeline when nothing is selected. Null until
        /// <see cref="SelectTask"/> is called, which a bare config in a unit test never does;
        /// every reader treats that as "no selection, the full pipeline". Position questions -
        /// does this run start after per-file scoring, does it run the final join - are
        /// answered from this list and the selection, never by a task describing where it
        /// sits.
        /// </summary>
        public IReadOnlyList<ISelectableTask> Pipeline { get; private set; }

        /// <summary>
        /// Select the task a run executes - or null for the full pipeline - together with the
        /// stages it runs, and let it set the flags it implies. The one place the selection,
        /// its pipeline and its flags are written together: the flags a selection derives are
        /// cleared first, so they hold exactly what this task sets and nothing a previous
        /// selection left. <see cref="ModelDiagnostics"/> is not among them -
        /// <c>--model-diagnostics</c> sets it on its own.
        /// </summary>
        public void SelectTask(ISelectableTask task, IReadOnlyList<ISelectableTask> pipeline)
        {
            if (task != null && pipeline == null)
                throw new ArgumentNullException(nameof(pipeline), @"A selected task must come with the pipeline it runs.");
            StopAfterStage5 = false;
            ExpectReconciledInput = false;
            DiagnosticsOnly = false;
            SelectedTask = task;
            Pipeline = pipeline;
            task?.ApplySelection(this);
        }

        /// <summary>
        /// Whether <paramref name="stage"/> is included in this run's driver loop - the one
        /// membership rule. Every stage is when nothing is selected. A selected task that is a
        /// stage of the pipeline it runs (an HPC node: one node = one task) is included alone,
        /// and the stages before it materialize on demand from their artifacts on disk. A
        /// selected task that is NOT a stage of the pipeline it runs is a selector that runs
        /// all of it - the diagnostics render - and every stage is included. Replaces the
        /// three membership flags (<c>NoJoin</c>, and the two above read as membership) that
        /// each stage's own predicate used to combine, which encoded one fan-out and one
        /// join over a pipeline that has two of each.
        /// </summary>
        public bool Includes(ISelectableTask stage)
        {
            if (SelectedTask == null || ReferenceEquals(SelectedTask, stage))
                return true;
            return !Pipeline.Contains(SelectedTask);
        }

        /// <summary>
        /// True under <c>--task ModelDiagnostics</c>: recompute the pass-2 view and write ONLY
        /// the report, suppressing the .blib, the protein/summary reports and the 2nd-pass FDR
        /// sidecars. The point is to be able to re-judge a diagnostics change on a completed
        /// large cohort without disturbing - or waiting for - the results it already produced.
        /// Every suppressed artifact is one this run would otherwise REWRITE with the same
        /// content it already holds, so skipping them costs nothing but the write. Set by
        /// that task's <see cref="ISelectableTask.ApplySelection"/>, like its two siblings.
        /// </summary>
        public bool DiagnosticsOnly { get; set; }

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
    /// The user-facing name of a <see cref="DecoyMethod"/>, as it reads in "Generating {0}
    /// decoys". Skyline's <c>GetLocalizedString</c> pattern.
    /// </summary>
    public static class DecoyMethodExtension
    {
        private static string[] LOCALIZED_VALUES
        {
            get { return new[] { "reverse-sequence", "shuffled-sequence", "library-supplied" }; }
        }

        public static string GetLocalizedString(this DecoyMethod val)
        {
            return LOCALIZED_VALUES[(int)val];
        }
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
        Simple,
        /// <summary>Gradient-boosted decision trees (non-linear alternative to the linear
        /// Percolator SVM); implemented by Osprey.ML GradientBoostedTrees. Selected by
        /// <c>--fdr-method gbdt</c> (the legacy alias <c>fasttree</c> still parses).</summary>
        Gbdt
    }

    public static class FdrMethodExtensions
    {
        private static string[] LOCALIZED_VALUES
        {
            get { return new[] { "Percolator", "Mokapot", "simple target-decoy", "gradient-boosted tree" }; }
        }

        /// <summary>
        /// The user-facing name of an <see cref="FdrMethod"/>, as it reads in "Running {0} FDR
        /// control". Skyline's <c>GetLocalizedString</c> pattern.
        /// </summary>
        public static string GetLocalizedString(this FdrMethod val)
        {
            return LOCALIZED_VALUES[(int)val];
        }

        /// <summary>
        /// True for the methods driven by the shared semi-supervised target-decoy
        /// framework: <see cref="FdrMethod.Percolator"/> (linear SVM) and
        /// <see cref="FdrMethod.Gbdt"/> (gradient-boosted trees). The two differ ONLY
        /// in the classifier -- identical best-per-precursor dedup, peptide-grouped CV
        /// folds, positive-set iteration, target-decoy competition, q-values, PEP, and the
        /// identical projection / streaming plumbing around all of it.
        ///
        /// Use this ANYWHERE the question is "is this the Percolator pipeline?" rather
        /// than a raw <c>== FdrMethod.Percolator</c>. Those gates are scattered across the
        /// Tasks layer -- FirstPassFDR's projection gate, the 2nd-pass projection gate,
        /// <c>NeedsResidentPool</c>, the Stage 5 log header -- and each one that compares
        /// against Percolator alone silently routes Gbdt down the resident
        /// <c>FdrEntry</c> path instead of the streaming projection. That fails quietly:
        /// same q-values, but the whole-run pool goes resident, which is exactly what
        /// OOM'd the 82-file join.
        ///
        /// Mokapot / Simple are NOT part of this framework and must stay excluded.
        /// </summary>
        public static bool UsesPercolatorFramework(this FdrMethod method)
        {
            return method == FdrMethod.Percolator || method == FdrMethod.Gbdt;
        }
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
    /// The user-facing name of a <see cref="ResolutionMode"/>, echoing the <c>--resolution</c>
    /// values rather than the enum identifier. Skyline's <c>GetLocalizedString</c> pattern, so the
    /// move to resource strings replaces only the array contents.
    /// </summary>
    public static class ResolutionModeExtension
    {
        private static string[] LOCALIZED_VALUES
        {
            get { return new[] { "auto", "unit", "HRAM" }; }
        }

        public static string GetLocalizedString(this ResolutionMode val)
        {
            return LOCALIZED_VALUES[(int)val];
        }
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
