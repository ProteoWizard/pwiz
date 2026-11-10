/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using pwiz.Common.SystemUtil;

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// Central access point for OSPREY_* environment variables that control
    /// production behavior (throttling, fast-iteration early exits, algorithm
    /// variants). A separate OspreyDiagnostics class covers the diagnostic-dump
    /// env vars. Every variable is an <see cref="EnvVar"/> declared beside its read, with its
    /// group and description, and read only through <see cref="EnvVar.Value"/>; together
    /// they are the catalog <c>--help-env</c> prints (<see cref="EnvVarCatalog"/>).
    ///
    /// <para>Some values are read once at process start into readonly static fields; a value
    /// a command-line test must vary is a property that re-reads the variable on every
    /// access, so <see cref="OverrideVariables"/> can change it for one in-process
    /// <c>Program.RunCommand</c> - the same fix Skyline uses for a resource string captured
    /// in a static. Which is which is this class's business, and either may change. So a
    /// caller must not assume a read is free: read the value once, outside any loop, and pass
    /// the result down as a plain argument rather than reaching back here per row.</para>
    ///
    /// Lives in Osprey.Core so every project below the main pipeline
    /// (FDR, Chromatography, Scoring, ML, IO) can read it without
    /// depending on the main project (which would create a cycle). See
    /// "Osprey project layering" in
    /// <c>ai/docs/osprey-development-guide.md</c>.
    /// </summary>
    public static class OspreyEnvironment
    {
        private static readonly EnvVar ENV_MAX_PARALLEL_FILES = new EnvVar(@"OSPREY_MAX_PARALLEL_FILES", EnvVarGroup.performance,
            @"Legacy cap on the input files processed at once, consulted only when the parallel-files " +
            @"argument is absent. 0 or unset sets no cap here.")
            { ValueExample = @"<N>" };

        /// <summary>
        /// OSPREY_MAX_PARALLEL_FILES: legacy back-compat cap on concurrent file
        /// processing, superseded by the <c>--parallel-files</c> CLI argument
        /// (which wins when both are set). Consulted by
        /// <see cref="FileParallelismResolver"/> only when the argument is absent.
        /// Values:
        ///   0 / unset = no cap from here (the default is now strictly sequential)
        ///   1        = strictly sequential
        ///   N &gt; 1    = at most N files concurrently
        /// Note the default changed: an unset value used to mean "all files at
        /// once"; it now means "one file at a time" -- opt into concurrency with
        /// <c>--parallel-files</c>. Useful historically for memory-bound datasets
        /// (Astral HRAM) where three large working sets exceed a 64 GB budget.
        /// </summary>
        public static readonly int MaxParallelFiles = ParseIntOrZero(ENV_MAX_PARALLEL_FILES);

        private static readonly EnvVar ENV_FDR_FILE_LANES = new EnvVar(@"OSPREY_FDR_FILE_LANES", EnvVarGroup.performance,
            @"Force how many files first-pass FDR works on at once, bypassing its thread and memory " +
            @"limits, for measuring the lane curve. 0 or unset lets Osprey choose.")
            { ValueExample = @"<N>" };

        /// <summary>
        /// OSPREY_FDR_FILE_LANES: forces how many files first-pass FDR works on at once,
        /// bypassing <see cref="FdrLaneResolver"/>'s thread and memory limits (clamped to the
        /// file count only). For measuring the lane curve, and for a test that needs a
        /// one-lane oracle; 0 / unset lets the resolver choose.
        /// </summary>
        public static int FdrFileLanes => ParseIntOrZero(ENV_FDR_FILE_LANES);

        private static readonly EnvVar ENV_BLOCK_READ_MB = new EnvVar(@"OSPREY_BLOCK_READ_MB", EnvVarGroup.performance,
            @"Read score parquets and FDR score files in blocks of this many MB, at least 1; larger than " +
            @"1024 reads 1024. The 0 arm that read directly was retired.")
            { ValueExample = @"<MB>", DefaultValue = @"4", AcceptsValue = IsPositiveInt };

        /// <summary>
        /// OSPREY_BLOCK_READ_MB: score parquets are read in planned spans - the column chunks a
        /// reader touches in each row group, read together - and FDR score sidecars in blocks of
        /// this many MB, serving the reader's smaller reads from memory. Default 4. A cold parquet
        /// walk is otherwise many small column-chunk reads, one seek each on a spinning disk.
        /// Values are clamped to [1, <see cref="MAX_BLOCK_READ_MB"/>]: the <c>0</c> arm that read
        /// directly (the pre-#4765 path, kept for parity testing) was retired, along with
        /// OSPREY_BLOCK_READ_GATE=0, so every read now takes the process-wide disk gate.
        /// </summary>
        public static readonly int BlockReadMb = ClampBlockReadMb(ParseIntOrNull(ENV_BLOCK_READ_MB) ?? 4);

        /// <summary>The largest <see cref="BlockReadMb"/>: a block is one array.</summary>
        public const int MAX_BLOCK_READ_MB = 1024;

        /// <summary>
        /// A <see cref="BlockReadMb"/> setting made safe: at least 1, at most
        /// <see cref="MAX_BLOCK_READ_MB"/>.
        /// </summary>
        public static int ClampBlockReadMb(int blockMb)
        {
            return Math.Max(1, Math.Min(blockMb, MAX_BLOCK_READ_MB));
        }

        private static readonly EnvVar ENV_STUB_IDENTITY = new EnvVar(@"OSPREY_STUB_IDENTITY", EnvVarGroup.performance,
            @"Where first-pass FDR takes each score row's charge, decoy flag and peptide: 1 from the " +
            @"library, 2 from both, stopping at the first row where they differ.")
            { Values = new[] { @"1", @"2" }, DefaultValue = @"1" };

        /// <summary>
        /// OSPREY_STUB_IDENTITY: where the first-pass FDR walks take each score row's charge,
        /// decoy flag and peptide. 1 (the default): from the library entry the row's entry_id
        /// names, which skips decoding three columns (two of them strings) from every row; a row
        /// group with an id the library lacks reads them from the file. <c>2</c> reads both and
        /// stops at the first row where they differ, the proof harness for the flip. The
        /// <c>0</c> arm that always read the file (the pre-#4765 path, kept for parity testing)
        /// was retired.
        /// </summary>
        public static readonly int StubIdentity = ParseIntOrNull(ENV_STUB_IDENTITY) == 2 ? 2 : 1;

        private static readonly EnvVar ENV_KEEP_FAILED_WRITES = new EnvVar(@"OSPREY_KEEP_FAILED_WRITES", EnvVarGroup.diagnostic,
            @"Keep the temporary file of a write abandoned by an exception, to inspect what it got " +
            @"through. The real path is never touched.");

        /// <summary>
        /// OSPREY_KEEP_FAILED_WRITES: forensic opt-in for <see cref="SimpleFileSaver"/>. Every
        /// durable write, diagnostic dumps included, goes through <c>SimpleFileSaver</c>, which
        /// normally deletes its sibling temp file when an exception unwinds before
        /// <c>Commit()</c> - the real path then holds the previous content or nothing,
        /// never a partial write (see <c>SimpleFileSaver</c>'s own doc comment). Set this to
        /// inspect what a write got through before it was abandoned: on an uncommitted
        /// <c>Dispose()</c>, the temp is left in place (same directory as the real path,
        /// its normal <c>~SK</c>-prefixed name) instead of deleted. It never touches the
        /// real path, so presence-proves-completeness still holds for every consumer;
        /// only a developer who knows to look for the temp sees the partial content.
        /// </summary>
        public static bool KeepFailedWrites { get; set; } = IsSetAndNotZero(ENV_KEEP_FAILED_WRITES);

        private static readonly EnvVar ENV_MAX_SCORING_WINDOWS = new EnvVar(@"OSPREY_MAX_SCORING_WINDOWS", EnvVarGroup.harness,
            @"Score only the first N isolation windows in the main search, for profiling and bisection. 0 " +
            @"or unset scores them all.")
            { ValueExample = @"<N>" };

        /// <summary>
        /// OSPREY_MAX_SCORING_WINDOWS: limits main-search isolation windows
        /// scored in Stage 4. Used for fast iteration during dotTrace
        /// profiling and parity bisection. 0 or unset means "score them all".
        /// </summary>
        public static readonly int MaxScoringWindows = ParseIntOrZero(ENV_MAX_SCORING_WINDOWS);

        private static readonly EnvVar ENV_EXIT_AFTER_CALIBRATION = new EnvVar(@"OSPREY_EXIT_AFTER_CALIBRATION", EnvVarGroup.harness,
            @"Exit after calibration, skipping the main search and everything after it.");

        /// <summary>
        /// OSPREY_EXIT_AFTER_CALIBRATION: exit after Stage 3 (calibration
        /// complete), skipping Stage 4 main search and everything downstream.
        /// Used for calibration-only benchmarking and bisection.
        /// </summary>
        public static readonly bool ExitAfterCalibration = IsSetAndNotZero(ENV_EXIT_AFTER_CALIBRATION);

        private static readonly EnvVar ENV_LOG_COASSIGN_ALLOC = new EnvVar(@"OSPREY_LOG_COASSIGN_ALLOC", EnvVarGroup.diagnostic,
            @"Attribute the model diagnostics peak co-assignment allocation by call site and log the " +
            @"totals.");

        /// <summary>
        /// Attribute the model-diagnostics co-assignment fold's allocation by call site and
        /// report the totals when it finishes. Diagnostic only; it changes nothing the run
        /// produces.
        /// </summary>
        public static readonly bool LogCoAssignmentAllocation = IsSetAndNotZero(ENV_LOG_COASSIGN_ALLOC);

        private static readonly EnvVar ENV_MDIAG_COASSIGN_ONLY = new EnvVar(@"OSPREY_MDIAG_COASSIGN_ONLY", EnvVarGroup.harness,
            @"With the ModelDiagnostics task, skip the per-run fold and build only the peak co-assignment " +
            @"panel. The report is left unstamped, so the next real run regenerates it.");

        /// <summary>
        /// OSPREY_MDIAG_COASSIGN_ONLY=1: on <c>--task ModelDiagnostics</c>, skip the per-run fold
        /// and build ONLY the peak co-assignment panel.
        ///
        /// <para>A measurement harness, not a product. On the 446-run CHS cohort the task takes
        /// 63 minutes, of which the per-run fold is 54 and this panel is 8; skipping the fold
        /// turns a one-hour iteration into about ten minutes, which is what makes questions
        /// about the panel's memory answerable in a morning rather than a night.</para>
        ///
        /// <para>The report it leaves has every OTHER section empty, so it is written with no
        /// validity key. An unstamped diagnostics product is refused by the render rather than
        /// trusted, and the next real run regenerates it - which is what keeps a harness run
        /// from being mistaken for, or overwriting, an answer.</para>
        /// </summary>
        public static readonly bool CoAssignmentPanelOnly = IsSetAndNotZero(ENV_MDIAG_COASSIGN_ONLY);

        private static readonly EnvVar ENV_LIBRARY_LOAD_ONLY = new EnvVar(@"OSPREY_LIBRARY_LOAD_ONLY", EnvVarGroup.harness,
            @"Load the spectral library, report what that cost, and exit before decoys, scoring or " +
            @"anything else.");

        /// <summary>
        /// OSPREY_LIBRARY_LOAD_ONLY=1: load the spectral library, report what that cost, and
        /// exit 0 before decoys, scoring or anything else.
        ///
        /// <para>A measurement harness, like <see cref="CoAssignmentPanelOnly"/>. The library
        /// load is the one phase every <c>--task</c> leg performs and each performs
        /// DIFFERENTLY - <c>PerFileScoring</c> reads every fragment, <c>FirstPassFDR</c> reads
        /// none (<c>OmitFragments</c>), <c>SecondPassFDR</c> reads only the retained set
        /// (issue #4650) - so it is the one phase where the three can be compared directly.
        /// Without this the comparison means running the legs themselves, which is hours on a
        /// 446-run cohort and swamps a 10-second difference in noise.</para>
        ///
        /// <para>Exits BEFORE decoy handling deliberately. Decoy generation is its own cost
        /// (~45 s on Astral at one file) and belongs to a different question; including it
        /// would report the load as whatever the decoy arm happens to do on that leg.</para>
        ///
        /// <para>Writes NOTHING, so it cannot be mistaken for a run or overwrite one.</para>
        /// </summary>
        public static readonly bool LibraryLoadOnly = IsSetAndNotZero(ENV_LIBRARY_LOAD_ONLY);

        private static readonly EnvVar ENV_LOG_MEMORY = new EnvVar(@"OSPREY_LOG_MEMORY", EnvVarGroup.diagnostic,
            @"Log post-collection memory probes. Forces garbage collections, so it changes timings.");

        /// <summary>
        /// OSPREY_LOG_MEMORY=1: emit the post-GC <c>[MEM ...]</c> probes. Each one forces a
        /// blocking <c>GC.Collect()/WaitForPendingFinalizers()/GC.Collect()</c> so the number it
        /// reports is a true live set rather than a heap with uncollected garbage in it.
        ///
        /// <para><see cref="IsSetAndNotZero"/>, NOT <see cref="EnvVar.IsSet"/>, and the difference was
        /// not academic. The dataset runners write <c>OSPREY_LOG_MEMORY=0</c> to mean OFF
        /// (<c>OspreyDatasetRun.psm1</c>), and the previous <c>!IsNullOrEmpty</c> test read
        /// <c>"0"</c> as SET - so every run through a runner had the probes on while its banner
        /// said "memprobe : off ... no forced GCs". On the 446-run CHS cohort that is one forced
        /// gen2 collection per file in the diagnostics fold, which flattens the very allocation
        /// curve the fold is measured by: the measurement was changing what it measured, in the
        /// phase whose flatness is the claim. Timings taken through a runner include that cost.</para>
        /// </summary>
        public static readonly bool LogMemory = IsSetAndNotZero(ENV_LOG_MEMORY);

        /// <summary>
        /// The name of <see cref="Diagnostics"/>, for a test that turns it on.
        /// </summary>
        public const string DIAGNOSTICS = @"OSPREY_DIAGNOSTICS";

        private static readonly EnvVar ENV_DIAGNOSTICS = new EnvVar(DIAGNOSTICS, EnvVarGroup.diagnostic,
            @"Turn on the bundle of cross-implementation bisection dumps, each of which its own " +
            @"OSPREY_DUMP_* variable also turns on alone.");

        /// <summary>
        /// OSPREY_DIAGNOSTICS=1: the master switch that turns on the bundle of cross-implementation
        /// bisection dumps (see <c>OspreyDiagnostics.Initialize</c>), each of which can also be
        /// turned on alone with its own OSPREY_DUMP_* variable. It was the <c>-d</c> /
        /// <c>--diagnostics</c> argument, and moved here with the other developer switches so the
        /// command-line help holds only what a user can use. A runtime toggle only: it is not part
        /// of any identity hash. Read on each access, so a test can set it with
        /// <see cref="OverrideVariables"/>.
        /// </summary>
        public static bool Diagnostics => IsSetAndNotZero(ENV_DIAGNOSTICS);

        private static readonly EnvVar ENV_CAL_MEDIANPOLISH = new EnvVar(@"OSPREY_CAL_MEDIANPOLISH", EnvVarGroup.behavior,
            @"Add median-polish cosine as a fifth calibration LDA feature. Experimental.");

        /// <summary>
        /// OSPREY_CAL_MEDIANPOLISH=1: add median-polish cosine (the dominant full-search
        /// Percolator feature) as a 5th calibration-LDA feature, computed over the
        /// peak-cropped calibration XICs. Experimental lever for raising the calibration
        /// peak-selection yield; default OFF keeps the calibration output byte-identical
        /// and perf-neutral (the feature is neither computed nor scored when unset).
        /// </summary>
        public static readonly bool CalMedianPolishFeature = IsSetAndNotZero(ENV_CAL_MEDIANPOLISH);

        private static readonly EnvVar ENV_CAL_SAMPLE_SIZE = new EnvVar(@"OSPREY_CAL_SAMPLE_SIZE", EnvVarGroup.behavior,
            @"Override the calibration library sample size (targets sampled per attempt). 0 or unset uses " +
            @"the configured size.")
            { ValueExample = @"<N>" };

        /// <summary>
        /// OSPREY_CAL_SAMPLE_SIZE: override the calibration library sample size (targets
        /// sampled per attempt). Default 0 = use the configured CalibrationSampleSize
        /// (100K). Experimental lever for testing whether a larger sample surfaces
        /// proportionally more near-zero-FDR calibration anchors on rich files. Re-read on
        /// each access (once per file), so an in-process test can drive the sampling path on
        /// a library smaller than the default sample.
        /// </summary>
        public static int CalSampleSizeOverride => ParseIntOrZero(ENV_CAL_SAMPLE_SIZE);

        // Note: the OSPREY_EXIT_AFTER_SCORING env var that used to live here
        // was retired in favor of the --task PerFileScoring CLI flag. See the HPC
        // scoring split work in AnalysisPipeline.Run. ExitAfterCalibration
        // (Stage 3) stays because it has no production CLI analog.

        private static readonly EnvVar ENV_LOAD_CALIBRATION = new EnvVar(@"OSPREY_LOAD_CALIBRATION", EnvVarGroup.behavior,
            @"Load this calibration JSON, written by Rust osprey, instead of calibrating, for " +
            @"feature-parity bisection.")
            { ValueExample = @"<path>" };

        /// <summary>
        /// OSPREY_LOAD_CALIBRATION: path to a .calibration.json produced by
        /// the Rust implementation. When set and the file exists, Stage 3 is
        /// skipped and the Rust calibration is loaded directly. Used for
        /// feature-parity bisection (isolates downstream feature divergence
        /// from calibration drift).
        /// </summary>
        public static readonly string LoadCalibrationPath = ENV_LOAD_CALIBRATION.Value;

        private static readonly EnvVar ENV_CROSS_IMPL_FDR_SIDECAR_OUT = new EnvVar(@"OSPREY_CROSS_IMPL_FDR_SIDECAR_OUT", EnvVarGroup.diagnostic,
            @"Test hook: the FDR score file round-trip unit test also copies its output here, for a byte " +
            @"comparison against Rust osprey.")
            { ValueExample = @"<path>" };

        /// <summary>
        /// OSPREY_CROSS_IMPL_FDR_SIDECAR_OUT: when a unit test is run under
        /// the cross-impl harness, the round-trip test for the v2
        /// .fdr_scores.bin format also copies its output to this path, so a
        /// sibling Rust unit test (with the same hardcoded inputs) can be
        /// byte-compared against ours. Test-only hook; never set in
        /// production. The harness verifies cross-impl byte parity once both
        /// sides have written their copy.
        /// </summary>
        public static readonly string CrossImplFdrSidecarOut = ENV_CROSS_IMPL_FDR_SIDECAR_OUT.Value;

        private static readonly EnvVar ENV_CROSS_IMPL_RECONCILIATION_OUT = new EnvVar(@"OSPREY_CROSS_IMPL_RECONCILIATION_OUT", EnvVarGroup.diagnostic,
            @"Test hook: the reconciliation file round-trip unit test also copies its output here, for a " +
            @"byte comparison against Rust osprey.")
            { ValueExample = @"<path>" };

        /// <summary>
        /// OSPREY_CROSS_IMPL_RECONCILIATION_OUT: same idea as
        /// CrossImplFdrSidecarOut but for the per-file
        /// .reconciliation.json boundary file. Test-only hook; never set
        /// in production.
        /// </summary>
        public static readonly string CrossImplReconciliationOut = ENV_CROSS_IMPL_RECONCILIATION_OUT.Value;

        private static readonly EnvVar ENV_DROP_BETWEEN_TASKS = new EnvVar(@"OSPREY_DROP_BETWEEN_TASKS", EnvVarGroup.performance,
            @"Make each task release everything but the library when it finishes, so the next one reloads " +
            @"what it needs from disk, as an HPC chain does.");

        /// <summary>
        /// DIAGNOSTIC. <c>OSPREY_DROP_BETWEEN_TASKS=1</c> makes each task release every byproduct
        /// but the library when it finishes, so the next task reloads what it needs from disk -
        /// the dataflow an HPC chain gets for free from process boundaries. Default OFF; the
        /// experiment reverts as a unit. See <c>PipelineContext.DropAllButLibrary</c>.
        /// </summary>
        public static bool DropBetweenTasks { get; set; } = IsSetAndNotZero(ENV_DROP_BETWEEN_TASKS);

        private static readonly EnvVar ENV_STOP_AFTER_TASKS = new EnvVar(@"OSPREY_STOP_AFTER_TASKS", EnvVarGroup.harness,
            @"End the run cleanly, with exit code 0, once N tasks have run in it (a task skipped as " +
            @"current does not count). For resume tests. 0 or unset never stops.")
            { ValueExample = @"<N>" };

        /// <summary>
        /// DEVELOPER. <c>OSPREY_STOP_AFTER_TASKS=N</c> ends the run cleanly once N tasks have RUN in
        /// it - a task skipped because its outputs are current does not count - with exit code 0
        /// and a <c>[TASK] stop:</c> line. Run again and again, a run then advances exactly one task
        /// at a time, which is how a resume test shows that a re-run skips every task already done
        /// and recomputes none of it. Read on every access, so a command-line test can vary it;
        /// unset or 0 for no stop.
        /// </summary>
        public static int StopAfterTasks => ParseIntOrNull(ENV_STOP_AFTER_TASKS) ?? 0;

        private static readonly EnvVar ENV_SERIAL_WINDOW_READS = new EnvVar(@"OSPREY_SERIAL_WINDOW_READS", EnvVarGroup.performance,
            @"Read the spectra cache one isolation window block at a time in calibration and the " +
            @"second-pass re-score. 0 reads in parallel, the timing A/B arm.")
            { DefaultValue = @"1" };

        /// <summary>
        /// DIAGNOSTIC. Calibration and the Stage 6 re-score read a run's spectra cache one isolation
        /// window block at a time (<c>SpectraWindowIndex.LoadWindowSerialRead</c>), because both are
        /// often the first read of a cache that is no longer in the file cache. Default ON;
        /// <c>OSPREY_SERIAL_WINDOW_READS=0</c> makes those two read in parallel as they did before,
        /// the A/B arm for timing. Output is the same either way. A training export made later reads
        /// serially regardless.
        /// </summary>
        public static bool SerialWindowReads { get; set; } = IsNotZero(ENV_SERIAL_WINDOW_READS);

        private static readonly EnvVar ENV_SERIAL_READ_SCOPE = new EnvVar(@"OSPREY_SERIAL_READ_SCOPE", EnvVarGroup.performance,
            @"Whether the serial window reads take turns within each file or across every file in the " +
            @"process.")
            { Values = new[] { @"file", @"process" }, DefaultValue = @"file" };

        /// <summary>
        /// DIAGNOSTIC. <c>OSPREY_SERIAL_READ_SCOPE=process</c> makes the serial window reads take
        /// turns across every file in the process instead of within each file. Under
        /// --parallel-files the per-file default lets that many files' reads compete for one disk.
        /// It covers only those block reads, not mzML parsing, cache writes or parallel window reads.
        /// </summary>
        public static bool SerialReadsProcessWide { get; set; } =
            string.Equals(ENV_SERIAL_READ_SCOPE.Value?.Trim(), @"process", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The log line naming the window-read switches when either is set, so a timing run records
        /// which arm it was; null at the defaults, leaving a default run's log unchanged.
        /// </summary>
        public static string DescribeWindowReads()
        {
            if (SerialWindowReads && !SerialReadsProcessWide)
                return null;
            return string.Format(@"Window reads (DIAGNOSTIC): calibration and Stage 6 {0}; serial read lock {1}",
                SerialWindowReads ? @"serial" : @"parallel (OSPREY_SERIAL_WINDOW_READS=0)",
                SerialReadsProcessWide ? @"process-wide (OSPREY_SERIAL_READ_SCOPE=process)" : @"per file");
        }

        private static readonly EnvVar ENV_PICK_DUMP_CANDIDATES = new EnvVar(@"OSPREY_PICK_DUMP_CANDIDATES", EnvVarGroup.diagnostic,
            @"Write one row per candidate peak of every precursor scored in the first-pass search to a " +
            @"pick_candidates.tsv per input file, for training a pick model.");

        /// <summary>
        /// OSPREY_PICK_DUMP_CANDIDATES: when set to a non-empty / non-zero value, dump one
        /// row per CWT candidate peak of every precursor (targets AND decoys) scored in the
        /// first-pass main search to a per-input-file TSV
        /// (<c>&lt;work-dir&gt;\&lt;inputStem&gt;.pick_candidates.tsv</c>). The row carries the
        /// exact raw rank terms the picker computes (coelution, ln_intensity, rt_penalty,
        /// median_polish) plus the candidate bounds and whether it was the chosen peak, so a
        /// downstream trainer can learn a linear pick model (see <see cref="PickLdaModelPath"/>)
        /// on precisely those values. Default OFF: no per-candidate median polish is computed and
        /// no file is written, so the hot loop is byte-identical and pays nothing when unset.
        /// </summary>
        public static readonly bool PickDumpCandidates = IsSetAndNotZero(ENV_PICK_DUMP_CANDIDATES);

        private static readonly EnvVar ENV_PICK_LDA_MODEL = new EnvVar(@"OSPREY_PICK_LDA_MODEL", EnvVarGroup.behavior,
            @"Replace the built-in peak-pick model with the linear model in this JSON file.")
            { ValueExample = @"<path>" };

        /// <summary>
        /// OSPREY_PICK_LDA_MODEL: path to a JSON file with a frozen linear pick model. When set
        /// and the file exists, the CWT candidate rank score is REPLACED by
        ///   rank = w0*z(coelution) + w1*z(ln_intensity) + w2*z(rt_penalty) + w3*z(median_polish)
        /// where z(x_i) = (x_i - mean[i]) / scale[i], using the same four raw terms the dump
        /// (<see cref="PickDumpCandidates"/>) captures. The argmax selection and IEEE-754
        /// total-order tie-break are unchanged. Overrides the resolution-keyed default model.
        /// Loaded and cached once by <c>PickLdaModel</c>. JSON schema:
        ///   { "features": ["coelution","ln_intensity","rt_penalty","median_polish"],
        ///     "weights": [w0,w1,w2,w3], "means": [m0,m1,m2,m3], "scales": [s0,s1,s2,s3] }
        /// </summary>
        public static readonly string PickLdaModelPath = ENV_PICK_LDA_MODEL.Value;

        private static readonly EnvVar ENV_PICK_LDA = new EnvVar(@"OSPREY_PICK_LDA", EnvVarGroup.behavior,
            @"Pick peaks with the learned resolution-keyed linear model. 0 restores the legacy " +
            @"product-form pick.")
            { DefaultValue = @"1" };

        /// <summary>
        /// OSPREY_PICK_LDA: use the learned resolution-keyed linear peak-pick model (Stellar
        /// for unit, Astral for HRAM). DEFAULT ON; set OSPREY_PICK_LDA=0 for the legacy pure
        /// product-form pick (coelution * rt_penalty * ln_intensity, no median-polish factor).
        /// Precedence in the picker:
        ///   1. OSPREY_PICK_LDA_MODEL set -> that model (test override);
        ///   2. else OSPREY_PICK_LDA != 0 (default) -> the hardcoded resolution-keyed model;
        ///   3. else (OSPREY_PICK_LDA=0) -> the legacy product pick.
        ///
        /// The default moved to the learned model with the #4484 golden re-baseline. An
        /// additive rank over standardized terms is the same direction Skyline's
        /// LegacyScoringModel took, and it is unlikely that a three-way product of terms whose
        /// combination in log space was never established is the end state. Measured effect on
        /// the discovery set is SMALL and its sign is not stable per cohort (seven A/B cells
        /// spanning -3.5% to +1.8% at matched true FDP), so do NOT expect a sensitivity gain
        /// from this and do NOT read a single validation run as evidence either way - the pick
        /// relocates ~44% of contested peaks and moves discoveries by about 1%. The =0 opt-out
        /// is kept precisely so the two stay comparable.
        /// </summary>
        public static readonly bool PickLda = IsNotZero(ENV_PICK_LDA);

        private static readonly EnvVar ENV_SCAN_MAJOR_PREFILTER = new EnvVar(@"OSPREY_SCAN_MAJOR_PREFILTER", EnvVarGroup.performance,
            @"Evaluate the first-pass signal prefilter for all of a window's candidates scan by scan. 0 " +
            @"restores the candidate-by-candidate loop, the timing A/B arm.")
            { DefaultValue = @"1" };

        /// <summary>
        /// OSPREY_SCAN_MAJOR_PREFILTER: evaluate the first-pass signal prefilter for all of a
        /// window's candidates at once, scan by scan, so each spectrum is visited once while its
        /// arrays are in cache. DEFAULT ON; set OSPREY_SCAN_MAJOR_PREFILTER=0 for the original
        /// candidate-by-candidate loop, the A/B arm for timing. Output is the same either way:
        /// the prefilter verdict is a pure function of the candidate, its scan range and the
        /// spectra.
        /// </summary>
        public static readonly bool ScanMajorPrefilter = IsNotZero(ENV_SCAN_MAJOR_PREFILTER);

        private static readonly EnvVar ENV_SCAN_MAJOR_XIC = new EnvVar(@"OSPREY_SCAN_MAJOR_XIC", EnvVarGroup.performance,
            @"Extract the fragment XICs of prefiltered candidates scan by scan, a block at a time. 0 " +
            @"restores candidate-by-candidate extraction, the timing A/B arm.")
            { DefaultValue = @"1" };

        /// <summary>
        /// OSPREY_SCAN_MAJOR_XIC: extract the fragment XICs of the candidates that pass the
        /// scan-major prefilter scan by scan too, a block of candidates at a time, instead of
        /// one candidate's whole scan range after another. DEFAULT ON (takes effect only with
        /// <see cref="ScanMajorPrefilter"/>); set OSPREY_SCAN_MAJOR_XIC=0 for the
        /// candidate-by-candidate extraction, the A/B arm for timing. Output is the same either
        /// way: each XIC value comes from the same peak lookup in the same spectrum.
        /// </summary>
        public static readonly bool ScanMajorXic = IsNotZero(ENV_SCAN_MAJOR_XIC);

        private static readonly EnvVar ENV_SCAN_MAJOR_CAL_PREFILTER = new EnvVar(@"OSPREY_SCAN_MAJOR_CAL_PREFILTER", EnvVarGroup.performance,
            @"Evaluate the calibration top-6 fragment prefilter for all of a window's entries scan by " +
            @"scan. 0 restores the entry-by-entry loop, the timing A/B arm.")
            { DefaultValue = @"1" };

        /// <summary>
        /// OSPREY_SCAN_MAJOR_CAL_PREFILTER: evaluate the calibration top-6 fragment prefilter
        /// for all of a window's calibration entries at once, scan by scan, so each spectrum is
        /// visited once while its arrays are in cache. DEFAULT ON; set
        /// OSPREY_SCAN_MAJOR_CAL_PREFILTER=0 for the original entry-by-entry loop, the A/B arm
        /// for timing. Output is the same either way: each entry gets the same candidate spectra
        /// in the same order.
        /// </summary>
        public static readonly bool ScanMajorCalPrefilter = IsNotZero(ENV_SCAN_MAJOR_CAL_PREFILTER);

        /// <summary>
        /// The log line naming the OSPREY_SCAN_MAJOR_* switches set to 0, so a timing run records
        /// which arm it was; null at the defaults, leaving a default run's log unchanged.
        /// </summary>
        public static string DescribeScanMajorSwitches()
        {
            var off = new List<string>();
            if (!ScanMajorPrefilter)
                off.Add(@"OSPREY_SCAN_MAJOR_PREFILTER=0");
            if (!ScanMajorXic)
                off.Add(@"OSPREY_SCAN_MAJOR_XIC=0");
            if (!ScanMajorCalPrefilter)
                off.Add(@"OSPREY_SCAN_MAJOR_CAL_PREFILTER=0");
            if (off.Count == 0)
                return null;
            return string.Format(@"Scan-major passes (DIAGNOSTIC): candidate-major for {0}",
                string.Join(@", ", off));
        }

        /// <summary>The <see cref="FdrModel"/> spelling of the default linear SVM. Accepted so a
        /// sweep script can name both arms explicitly rather than unsetting the variable for
        /// one of them, as <see cref="EXPERIMENT_AGG_MAX"/> and
        /// <see cref="PASS2_QVALUE_PROTEIN_COMPACT"/> spell their defaults.</summary>
        public const string FDR_MODEL_SVM = @"svm";

        /// <summary>The <see cref="FdrModel"/> value that selects gradient-boosted trees.</summary>
        public const string FDR_MODEL_GBDT = @"gbdt";

        private static readonly EnvVar ENV_FDR_MODEL = new EnvVar(@"OSPREY_FDR_MODEL", EnvVarGroup.behavior,
            @"The first-pass classifier: the linear SVM, or gradient-boosted trees (experimental, C# " +
            @"only).")
            { Values = new[] { @"svm", @"gbdt" }, DefaultValue = @"svm" };

        /// <summary>
        /// OSPREY_FDR_MODEL: the classifier first-pass Percolator trains. Unset, empty or
        /// <see cref="FDR_MODEL_SVM"/> selects the linear SVM (the default);
        /// <see cref="FDR_MODEL_GBDT"/> selects gradient-boosted trees. Case-insensitive and
        /// trimmed, like the other OSPREY_* selectors. An unrecognized value is a startup ERROR
        /// (<see cref="FdrModelError"/>), never a fallback: a run that asked for trees and
        /// silently trained the SVM is the #4491 failure over again.
        ///
        /// <para>GBDT is EXPERIMENTAL and is not expected to improve results with the current
        /// features, which were chosen for the linear SVM. It exists so that features which do
        /// not work well with a linear SVM - some were removed for that reason - can be
        /// evaluated when they are added. It has to keep working.</para>
        ///
        /// <para>An environment variable rather than a command-line argument because it is a
        /// developer lever, not a product setting: it replaced <c>--fdr-method</c>, which was
        /// removed with no alias (#4543). Read once at process start and carried in-process as
        /// <see cref="OspreyConfig.FdrClassifier"/>, which the command-line parse sets from it; a test
        /// that runs the whole pipeline sets this instead, as it sets <see cref="MeanBestN"/>. The
        /// trees and every <c>OSPREY_GBT_*</c> setting key the FirstPassFDR, PerFileRescoring
        /// and SecondPassFDR validity keys (<c>PercolatorEngine.GbdtValidityKeySuffix</c>); the
        /// SVM adds nothing to them.</para>
        /// </summary>
        public static FdrClassifier FdrModel { get; internal set; } =
            ParseFdrModel(ENV_FDR_MODEL.Value) ?? FdrClassifier.LinearSvm;

        /// <summary>The startup error for an unrecognized OSPREY_FDR_MODEL, or null when the
        /// value is usable. Program startup ABORTS on it, like
        /// <see cref="Pass2QValueUnrecognized"/>, and at startup rather than at Stage 5 so a
        /// typo costs seconds instead of Stages 1-4.</summary>
        public static readonly string FdrModelError =
            DescribeUnrecognizedFdrModel(ENV_FDR_MODEL.Value);

        /// <summary>
        /// The classifier an OSPREY_FDR_MODEL value selects, or null when the value is not
        /// recognized. Unset, empty and whitespace select the linear SVM, as they select the
        /// default for every other OSPREY_* selector. Public so the parse can be tested; the
        /// environment itself is read once, into <see cref="FdrModel"/>.
        /// </summary>
        public static FdrClassifier? ParseFdrModel(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return FdrClassifier.LinearSvm;
            string v = raw.Trim().ToLowerInvariant();
            if (v == FDR_MODEL_SVM)
                return FdrClassifier.LinearSvm;
            if (v == FDR_MODEL_GBDT)
                return FdrClassifier.Gbdt;
            return null;
        }

        /// <summary>
        /// The operator-actionable error for an OSPREY_FDR_MODEL value
        /// <see cref="ParseFdrModel"/> does not recognize, or null when it does. Names the
        /// value as given, so a shell-quoted or misspelled value is not mistaken for an unset one.
        /// </summary>
        public static string DescribeUnrecognizedFdrModel(string raw)
        {
            if (ParseFdrModel(raw).HasValue)
                return null;
            return string.Format(
                @"OSPREY_FDR_MODEL='{0}' is not a recognized classifier. Recognized: '{1}' (the " +
                @"linear SVM, also the default when the variable is unset) and '{2}' " +
                @"(gradient-boosted trees, experimental). Unset it for the default.",
                raw, FDR_MODEL_SVM, FDR_MODEL_GBDT);
        }

        /// <summary>
        /// The run-log line naming the classifier when it is NOT the default, or null for the
        /// linear SVM, whose log is unchanged. Logged at startup rather than at Stage 5 because
        /// FirstPassFDR does not run on a resume or on <c>--task SecondPassFDR</c>, and those
        /// runs score with the model too.
        /// </summary>
        public static string DescribeFdrModel(FdrClassifier fdrClassifier)
        {
            if (fdrClassifier != FdrClassifier.Gbdt)
                return null;
            return string.Format(
                @"FDR model: {0} (EXPERIMENTAL, OSPREY_FDR_MODEL) - gradient-boosted trees " +
                @"replace the linear SVM in first-pass Percolator", FDR_MODEL_GBDT);
        }

        private static readonly EnvVar ENV_GBT_MAX_ITERATIONS = new EnvVar(@"OSPREY_GBT_MAX_ITERATIONS", EnvVarGroup.behavior,
            @"Semi-supervised training iterations for the gradient-boosted trees. 0 or unset uses the " +
            @"default.")
            { ValueExample = @"<N>", DefaultValue = @"30" };

        /// <summary>
        /// Semi-supervised training iterations for <c>OSPREY_FDR_MODEL=gbdt</c>
        /// (OSPREY_GBT_MAX_ITERATIONS); 0/unset uses <see cref="GBT_MAX_ITERATIONS_DEFAULT"/>.
        /// Tree-only: the linear SVM keeps its own fixed 10 and is untouched by this.
        ///
        /// Exists because the two classifiers converge at very different rates. On Stellar
        /// the SVM plateaus by iteration 4 and early-stops, while the trees were still
        /// improving monotonically (0.7 -> 1.2% of training targets at 1% FDR) when they
        /// hit the shared cap of 10 -- i.e. the cap was binding on the trees, so their
        /// reported discrimination may understate the model rather than measure it. Raising
        /// it costs nothing when it is not binding: the existing
        /// stop-after-2-non-improving-iterations rule still ends training on convergence.
        /// </summary>
        public static readonly int GbtMaxIterations = ResolveGbtMaxIterations();

        /// <summary>Default for <see cref="GbtMaxIterations"/>. Well above the SVM's 10 so
        /// convergence (not the cap) ends tree training, while still bounding a pathological
        /// run: each iteration retrains the full ensemble on the &lt;= MaxTrainSize subsample.</summary>
        public const int GBT_MAX_ITERATIONS_DEFAULT = 30;

        private static int ResolveGbtMaxIterations()
        {
            int v = ParseIntOrZero(ENV_GBT_MAX_ITERATIONS);
            return v > 0 ? v : GBT_MAX_ITERATIONS_DEFAULT;
        }

        private static readonly EnvVar ENV_GBT_GAMMA = new EnvVar(@"OSPREY_GBT_GAMMA", EnvVarGroup.behavior,
            @"Gradient-boosted trees: the minimum split gain to keep a split.")
            { ValueExample = @"<x>", DefaultValue = @"0" };
        private static readonly EnvVar ENV_GBT_LAMBDA = new EnvVar(@"OSPREY_GBT_LAMBDA", EnvVarGroup.behavior,
            @"Gradient-boosted trees: the L2 leaf-weight penalty.")
            { ValueExample = @"<x>", DefaultValue = @"1" };
        private static readonly EnvVar ENV_GBT_ALPHA = new EnvVar(@"OSPREY_GBT_ALPHA", EnvVarGroup.behavior,
            @"Gradient-boosted trees: the L1 leaf-weight penalty.")
            { ValueExample = @"<x>", DefaultValue = @"0" };
        private static readonly EnvVar ENV_GBT_MAX_DEPTH = new EnvVar(@"OSPREY_GBT_MAX_DEPTH", EnvVarGroup.behavior,
            @"Gradient-boosted trees: the tree depth.")
            { ValueExample = @"<N>", DefaultValue = @"6" };
        private static readonly EnvVar ENV_GBT_N_TREES = new EnvVar(@"OSPREY_GBT_N_TREES", EnvVarGroup.behavior,
            @"Gradient-boosted trees: boosting rounds per model.")
            { ValueExample = @"<N>", DefaultValue = @"200" };
        private static readonly EnvVar ENV_GBT_MIN_CHILD_WEIGHT = new EnvVar(@"OSPREY_GBT_MIN_CHILD_WEIGHT", EnvVarGroup.behavior,
            @"Gradient-boosted trees: the minimum summed child hessian.")
            { ValueExample = @"<x>", DefaultValue = @"1" };
        private static readonly EnvVar ENV_GBT_LEARNING_RATE = new EnvVar(@"OSPREY_GBT_LEARNING_RATE", EnvVarGroup.behavior,
            @"Gradient-boosted trees: the shrinkage.")
            { ValueExample = @"<x>", DefaultValue = @"0.1" };
        private static readonly EnvVar ENV_GBT_SUBSAMPLE = new EnvVar(@"OSPREY_GBT_SUBSAMPLE", EnvVarGroup.behavior,
            @"Gradient-boosted trees: the row subsample per tree.")
            { ValueExample = @"<x>", DefaultValue = @"0.8" };
        private static readonly EnvVar ENV_GBT_COLSAMPLE = new EnvVar(@"OSPREY_GBT_COLSAMPLE", EnvVarGroup.behavior,
            @"Gradient-boosted trees: the feature subsample per tree.")
            { ValueExample = @"<x>", DefaultValue = @"0.8" };

        /// <summary>Optional overrides for the gradient-boosted-trees hyper-parameters
        /// (<c>OSPREY_FDR_MODEL=gbdt</c>), so a regularization / capacity sweep runs from
        /// env vars without a recompile per setting. Each is null when its var is unset,
        /// leaving the validated <c>GbtParams</c> default in place; applied in
        /// <c>BuildProjectionPercolatorConfig</c>. Tree-only -- the linear SVM ignores them.
        ///   OSPREY_GBT_GAMMA            min split gain to keep a split   (default 0, off)
        ///   OSPREY_GBT_LAMBDA           L2 leaf-weight penalty           (default 1)
        ///   OSPREY_GBT_ALPHA            L1 leaf-weight penalty           (default 0, off)
        ///   OSPREY_GBT_MAX_DEPTH        tree depth                       (default 6)
        ///   OSPREY_GBT_N_TREES          boosting rounds per model        (default 200)
        ///   OSPREY_GBT_MIN_CHILD_WEIGHT min summed child hessian         (default 1)
        ///   OSPREY_GBT_LEARNING_RATE    shrinkage                        (default 0.1)
        ///   OSPREY_GBT_SUBSAMPLE        row subsample per tree           (default 0.8)
        ///   OSPREY_GBT_COLSAMPLE        feature subsample per tree       (default 0.8)
        /// The chosen values are echoed to the run log (the "Gradient-boosted trees: ..."
        /// line) so each sweep point records exactly what it ran with.</summary>
        public static readonly double? GbtGamma = ParseDoubleOrNull(ENV_GBT_GAMMA);
        public static readonly double? GbtRegLambda = ParseDoubleOrNull(ENV_GBT_LAMBDA);
        public static readonly double? GbtRegAlpha = ParseDoubleOrNull(ENV_GBT_ALPHA);
        public static readonly int? GbtMaxDepth = ParseIntOrNull(ENV_GBT_MAX_DEPTH);
        public static readonly int? GbtNTrees = ParseIntOrNull(ENV_GBT_N_TREES);
        public static readonly double? GbtMinChildWeight = ParseDoubleOrNull(ENV_GBT_MIN_CHILD_WEIGHT);
        public static readonly double? GbtLearningRate = ParseDoubleOrNull(ENV_GBT_LEARNING_RATE);
        public static readonly double? GbtSubsample = ParseDoubleOrNull(ENV_GBT_SUBSAMPLE);
        public static readonly double? GbtColSample = ParseDoubleOrNull(ENV_GBT_COLSAMPLE);

        private static readonly EnvVar ENV_MAX_TRAIN_SIZE = new EnvVar(@"OSPREY_MAX_TRAIN_SIZE", EnvVarGroup.behavior,
            @"The cap on first-pass training rows.")
            { ValueExample = @"<N>", DefaultValue = @"300000" };

        /// <summary>Optional override for <c>PercolatorConfig.MaxTrainSize</c> -- the
        /// Percolator-3.0 peptide-grouped training-subsample cap (default 300000). Set via
        /// OSPREY_MAX_TRAIN_SIZE. Raising it feeds the classifier more real rows (the cap is
        /// binding when the deduped population exceeds it) at the cost of memory + training
        /// time. Null when unset -- keeps the 300k default.</summary>
        public static readonly int? MaxTrainSizeOverride = ParseIntOrNull(ENV_MAX_TRAIN_SIZE);

        /// <summary>
        /// The default first-pass SVM C-selection tolerance: the grid search keeps the most
        /// regularized C whose inner-CV passing count is within this fraction of the best
        /// (see <c>PercolatorConfig.CSelectionTolerance</c>).
        /// </summary>
        public const double DEFAULT_SVM_C_SELECTION_TOLERANCE = 0.01;

        private static readonly EnvVar ENV_SVM_C_TOLERANCE = new EnvVar(@"OSPREY_SVM_C_TOLERANCE", EnvVarGroup.behavior,
            @"First-pass SVM C selection: keep the most regularized C within this fraction of the best " +
            @"inner-CV count, a number in [0, 1). 0 is the strict maximum Rust osprey used before 1%.")
            { ValueExample = @"<x>", DefaultValue = @"0.01" };

        /// <summary>OSPREY_SVM_C_TOLERANCE exactly as set, or null when unset: an override for
        /// <see cref="DEFAULT_SVM_C_SELECTION_TOLERANCE"/>, a number in [0, 1). 0 restores the
        /// strict maximum (the pre-#4703 rule), for A/B work. Rust has used the same 1% default
        /// since maccoss/osprey#69, with no opt-out.</summary>
        public static readonly string SvmCSelectionToleranceSetting =
            ENV_SVM_C_TOLERANCE.Value;

        /// <summary>True when OSPREY_SVM_C_TOLERANCE is set to anything but a number in [0, 1)
        /// (<c>0,01</c>, <c>1%</c>, a quoted <c>"0"</c> from cmd.exe's <c>set</c>). Program
        /// startup ABORTS on this rather than falling back: the fallback would train under the
        /// default rule and key its directories as the default, so a parity or A/B arm would
        /// report the default arm's numbers under its own name.</summary>
        public static readonly bool SvmCSelectionToleranceUnrecognized =
            !string.IsNullOrEmpty(SvmCSelectionToleranceSetting) &&
            !ParseSvmCSelectionTolerance(SvmCSelectionToleranceSetting).HasValue;

        /// <summary>The first-pass SVM C-selection tolerance in effect.</summary>
        public static readonly double SvmCSelectionTolerance =
            ParseSvmCSelectionTolerance(SvmCSelectionToleranceSetting) ?? DEFAULT_SVM_C_SELECTION_TOLERANCE;

        private static readonly EnvVar ENV_GBT_OOF = new EnvVar(@"OSPREY_GBT_OOF", EnvVarGroup.behavior,
            @"Experiment, gbdt first pass only: 1 scores each training row with only the fold model that held " +
            @"it out, instead of the mean of every fold model, two of which trained on it. Not in the validity " +
            @"key: run it into its own output directory. A resumed first pass retrains rather than adopt the saved model. " +
            @"Second-pass rescoring still scores training rows with the fold mean.");

        private static readonly EnvVar ENV_XIC_FRAGMENTS = new EnvVar(@"OSPREY_XIC_FRAGMENTS", EnvVarGroup.behavior,
            @"Experiment: how many of a candidate's most intense library fragments get XICs (CWT " +
            @"peak detection, coelution, peak-shape and median-polish features). Calibration and " +
            @"the 2-of-top-6 prefilter keep 6. A whole number from 2 to 20.")
            { ValueExample = @"<2-20>", DefaultValue = @"6" };

        /// <summary>
        /// OSPREY_XIC_FRAGMENTS (experiment): how many of a
        /// candidate's most intense library fragments the search extracts XICs for - the input of the
        /// CWT peak detection and of the coelution, peak-shape and median-polish features. Null when
        /// unset (the top 6, <c>TopFragmentExtractor.CAL_TOP_N_FRAGMENTS</c>). Calibration and the
        /// 2-of-top-6 prefilter keep 6 either way. A value that is not a whole number from 2 to 20
        /// throws, so an arm can never run under the default while named for another count.
        /// </summary>
        public static readonly int? XicFragments = ParseXicFragments(
            ENV_XIC_FRAGMENTS.Value);

        /// <summary>The XIC count when OSPREY_XIC_FRAGMENTS is unset - the only count before it existed.</summary>
        public const int DEFAULT_XIC_FRAGMENTS = 6;

        private const int MIN_XIC_FRAGMENTS = 2;
        private const int MAX_XIC_FRAGMENTS = 20;

        /// <summary>The XIC count in effect: <see cref="XicFragments"/>, else <see cref="DEFAULT_XIC_FRAGMENTS"/>.</summary>
        public static int SearchXicFragments => XicFragments ?? DEFAULT_XIC_FRAGMENTS;

        private static readonly EnvVar ENV_FEATURE_MASK = new EnvVar(@"OSPREY_FEATURE_MASK", EnvVarGroup.behavior,
            @"The PIN features hidden from the FDR classifier, as comma-separated feature names, " +
            @"or none to mask nothing. Unset masks the default set, the features still under " +
            @"evaluation. An unknown name stops the search at startup.")
            { ValueExample = @"none|<name>,<name>..." };

        /// <summary>
        /// OSPREY_FEATURE_MASK: the PIN features to hide from the FDR classifier (left out of the
        /// vectors the FDR stage loads, so the classifier never sees them), as comma-separated feature
        /// names, or <c>none</c> to mask nothing. Null when unset: the default set, the features still
        /// under evaluation, is masked. Scoring computes and records only the feature families the
        /// mask leaves a feature of (and the 21 core features always), so arms share their per-file
        /// scores only when they record the same families - see <see cref="RecordAllFeatures"/>.
        /// An unknown name stops the search at startup.
        /// </summary>
        public static readonly string[] FeatureMask = ParseFeatureMask(
            ENV_FEATURE_MASK.Value);

        /// <summary>The OSPREY_FEATURE_MASK value that masks nothing.</summary>
        public const string FEATURE_MASK_NONE = @"none";

        private static readonly EnvVar ENV_RECORD_ALL_FEATURES = new EnvVar(@"OSPREY_RECORD_ALL_FEATURES", EnvVarGroup.performance,
            @"Developer and research switch: scoring computes and records every PIN feature in " +
            @"the scores parquet, whatever the mask hides, so masks can be compared on the FDR " +
            @"stage alone. Unset records only what the FDR stage reads. Keyed into the scoring " +
            @"and rescoring validity keys.");

        /// <summary>
        /// OSPREY_RECORD_ALL_FEATURES=1 (developer and research switch, not for release use): Stage 4
        /// computes every PIN feature and records it in the scores parquet, whatever the mask hides
        /// from the classifier. Unset, scoring computes only the features the FDR stage will read
        /// (<c>PinFeatureMask.IsRecorded</c>), so a default search costs what it did before the
        /// features under evaluation were added. Recording everything once lets a researcher compare
        /// masks on the FDR stage alone. Keyed into the per-file scoring and rescoring validity keys
        /// through the recorded-feature term, so a run that needs a feature its scores lack rescores.
        /// </summary>
        public static readonly bool RecordAllFeatures = ParseIntOrNull(ENV_RECORD_ALL_FEATURES) == 1;

        private static readonly EnvVar ENV_FDR_SEED = new EnvVar(@"OSPREY_FDR_SEED", EnvVarGroup.behavior,
            @"Experiment: the Percolator seed in place of 42 (fold split, training subsample, " +
            @"GBDT row and column sampling), to measure how much a comparison owes to the draw.")
            { ValueExample = @"<N>", DefaultValue = @"42" };

        /// <summary>
        /// OSPREY_FDR_SEED (experiment): the Percolator seed in place of 42 (fold split, training subsample, GBDT
        /// row and column sampling), to measure how much a feature-set comparison owes to the draw. Null when unset.
        /// </summary>
        public static readonly ulong? FdrSeed = ulong.TryParse(ENV_FDR_SEED.Value,
            NumberStyles.None, CultureInfo.InvariantCulture, out ulong seed) ? seed : null;

        private static readonly EnvVar ENV_FDR_TRAIN_FDR = new EnvVar(@"OSPREY_FDR_TRAIN_FDR", EnvVarGroup.behavior,
            @"Experiment: the q-value under which Percolator takes targets as training " +
            @"positives, in place of 0.01; 1 trains on every target. A number in (0, 1]; " +
            @"anything else stops the run. Keyed into the FDR validity keys.")
            { ValueExample = @"<q>", DefaultValue = @"0.01" };

        /// <summary>OSPREY_FDR_TRAIN_FDR exactly as set, or null when unset.</summary>
        public static readonly string FdrTrainFdrSetting = ENV_FDR_TRAIN_FDR.Value;

        /// <summary>True when OSPREY_FDR_TRAIN_FDR is set to anything but a number in (0, 1]. Program startup
        /// aborts on this rather than training under the default and keying the directories as the default.</summary>
        public static readonly bool FdrTrainFdrUnrecognized =
            !string.IsNullOrEmpty(FdrTrainFdrSetting) && !ParseFdrTrainFdr(FdrTrainFdrSetting).HasValue;

        /// <summary>
        /// OSPREY_FDR_TRAIN_FDR (experiment): the q-value under which Percolator takes targets as positives, in
        /// place of 0.01. 1 trains on every target against the decoys, as DIA-NN 1.7.12's neural network does.
        /// Keyed into the FDR validity keys when set. Null when unset or not a number in (0, 1].
        /// </summary>
        public static readonly double? FdrTrainFdr = ParseFdrTrainFdr(FdrTrainFdrSetting);

        private static readonly EnvVar ENV_NO_PREFILTER = new EnvVar(@"OSPREY_NO_PREFILTER", EnvVarGroup.behavior,
            @"Experiment: 1 turns off the coelution signal pre-filter, as the command-line switch " +
            @"does, for runners that cannot pass Osprey arguments. Enters the search identity.");

        /// <summary>
        /// OSPREY_NO_PREFILTER=1 (experiment): turns off the coelution signal pre-filter, as --no-prefilter does, for
        /// runners that cannot pass Osprey arguments. It enters the search identity, so every task keys on it.
        /// </summary>
        public static readonly bool NoPrefilter = ParseIntOrNull(ENV_NO_PREFILTER) == 1;

        private static readonly EnvVar ENV_LOESS_BANDWIDTH = new EnvVar(@"OSPREY_LOESS_BANDWIDTH", EnvVarGroup.behavior,
            @"Experiment: the RT calibration LOESS bandwidth (fraction of points per local fit) " +
            @"in place of 0.3. Enters the search identity.")
            { ValueExample = @"<fraction>", DefaultValue = @"0.3" };

        /// <summary>
        /// OSPREY_LOESS_BANDWIDTH (experiment): the RT calibration LOESS bandwidth (fraction of points per local fit)
        /// in place of 0.3, to let the curve follow a library whose RTs bend at the gradient end, as DIA-NN 1.7.12's
        /// 20-segment spline does. It enters the search identity, so every task keys on it. Null when unset.
        /// </summary>
        public static readonly double? LoessBandwidth = double.TryParse(ENV_LOESS_BANDWIDTH.Value,
            NumberStyles.Float, CultureInfo.InvariantCulture, out double loessBandwidth) ? loessBandwidth : null;

        private static readonly EnvVar ENV_MIN_RT_TOLERANCE = new EnvVar(@"OSPREY_MIN_RT_TOLERANCE", EnvVarGroup.behavior,
            @"Experiment: the RT search-window floor in minutes in place of the configured 0.5. " +
            @"Enters the search identity.")
            { ValueExample = @"<minutes>", DefaultValue = @"0.5" };

        /// <summary>
        /// OSPREY_MIN_RT_TOLERANCE (experiment): the RT search-window floor in minutes in place of the configured
        /// 0.5, to price precursors whose library RT the calibration maps badly (the SEA-AD gradient tail). It
        /// enters the search identity, so every task keys on it. Null when unset.
        /// </summary>
        public static readonly double? MinRtTolerance = double.TryParse(ENV_MIN_RT_TOLERANCE.Value,
            NumberStyles.Float, CultureInfo.InvariantCulture, out double minRtTolerance) ? minRtTolerance : null;

        private static readonly EnvVar ENV_TC_HALF_WINDOW = new EnvVar(@"OSPREY_TC_HALF_WINDOW", EnvVarGroup.behavior,
            @"Experiment: half-width in minutes of the window the tc_* time-correlation features " +
            @"correlate over, in place of 0.15. Keyed into every task.")
            { ValueExample = @"<minutes>", DefaultValue = @"0.15" };

        private static readonly EnvVar ENV_MIN_FRAGMENT_TOLERANCE = new EnvVar(@"OSPREY_MIN_FRAGMENT_TOLERANCE", EnvVarGroup.behavior,
            @"Experiment: a floor in ppm on the calibrated fragment tolerance the main search extracts " +
            @"XICs with. Keyed into every task.")
            { ValueExample = @"<ppm>" };

        private static readonly EnvVar ENV_LOCAL_RT_TOLERANCE = new EnvVar(@"OSPREY_LOCAL_RT_TOLERANCE", EnvVarGroup.behavior,
            @"Experiment: widen the RT search window at each library RT from the residual spread of this " +
            @"many nearest calibration points. Keyed into every task.")
            { ValueExample = @"<N>", AcceptsValue = IsPositiveInt };

        /// <summary>
        /// OSPREY_LOCAL_RT_TOLERANCE (experiment): widen the RT search window where the calibration fits
        /// the run worst, from the robust spread of the residuals of this many calibration points nearest
        /// in library RT (3 * 1.4826 * median |residual|, clamped between the global half-width and the
        /// maximum RT tolerance). Aimed at a library whose predicted RTs saturate at the gradient end,
        /// where many precursors share a library RT but elute minutes apart. Keyed into every task through
        /// <see cref="PickValidityKeySuffix()"/>. Null when unset or not a positive integer.
        /// </summary>
        public static readonly int? LocalRtToleranceNeighbors = int.TryParse(ENV_LOCAL_RT_TOLERANCE.Value,
            NumberStyles.None, CultureInfo.InvariantCulture, out int localRtNeighbors) && localRtNeighbors > 0 ? localRtNeighbors : null;

        /// <summary>
        /// OSPREY_MIN_FRAGMENT_TOLERANCE (experiment): a floor in ppm on the calibrated fragment tolerance
        /// the main search extracts XICs with (|mean| + 3 SD of confident matches, which is too tight for
        /// weak fragment ions). Keyed into every task through <see cref="PickValidityKeySuffix()"/>.
        /// Null when unset.
        /// </summary>
        public static readonly double? MinFragmentTolerancePpm = double.TryParse(ENV_MIN_FRAGMENT_TOLERANCE.Value,
            NumberStyles.Float, CultureInfo.InvariantCulture, out double minFragTol) ? minFragTol : null;

        private static readonly EnvVar ENV_CORR_FRAGMENT_TOLERANCE = new EnvVar(@"OSPREY_CORR_FRAGMENT_TOLERANCE", EnvVarGroup.behavior,
            @"Experiment: a tolerance in ppm (at least the calibrated one) the correlation-shape features " +
            @"re-extract their fragment XICs with. Keyed into every task.")
            { ValueExample = @"<ppm>" };

        /// <summary>
        /// OSPREY_CORR_FRAGMENT_TOLERANCE (experiment): a tolerance in ppm the correlation-shape features
        /// re-extract their fragment XICs with, as max(calibrated, setting), over the same scans. Peak
        /// detection, the apex, the intensity-pattern and the mass-error features stay on the XICs at the
        /// calibrated tolerance. Keyed into every task through <see cref="PickValidityKeySuffix()"/>.
        /// Null when unset.
        /// </summary>
        public static readonly double? CorrFragmentTolerancePpm = double.TryParse(ENV_CORR_FRAGMENT_TOLERANCE.Value,
            NumberStyles.Float, CultureInfo.InvariantCulture, out double corrFragTol) ? corrFragTol : null;

        private static readonly EnvVar ENV_REF_XIC = new EnvVar(@"OSPREY_REF_XIC", EnvVarGroup.behavior,
            @"Experiment: corr chooses the reference fragment XIC (apex scan, area, SNR, sharpness, MS1 " +
            @"reference) by agreement with the other fragments inside the chosen peak. Keyed into every task.")
            { Values = new[] { @"corr" } };

        /// <summary>
        /// OSPREY_REF_XIC (experiment): <c>corr</c> re-chooses the reference fragment XIC inside the chosen
        /// peak as the fragment with the largest sum of correlations to the other fragments there, in place
        /// of the fragment with the largest total intensity over the whole extraction window. The reference
        /// sets the apex scan (and so every apex-spectrum feature), the recomputed bounds area and SNR, the
        /// MS1 correlation reference, and the apex/area of the stored CWT candidates; the peak_apex,
        /// peak_area and peak_sharpness features keep their own most-intense-fragment reference. Keyed into every task through <see cref="PickValidityKeySuffix()"/>.
        /// False when unset.
        /// </summary>
        public static readonly bool RefXicByCorrelation = string.Equals(
            ENV_REF_XIC.Value, @"corr", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// OSPREY_TC_HALF_WINDOW (experiment): half-width in minutes of the window the tc_* time-correlation
        /// features correlate over, in place of 0.15. Keyed into every task through <see cref="PickValidityKeySuffix()"/>.
        /// Null when unset.
        /// </summary>
        public static readonly double? TcHalfWindowMinutes = double.TryParse(ENV_TC_HALF_WINDOW.Value,
            NumberStyles.Float, CultureInfo.InvariantCulture, out double halfWindow) ? halfWindow : null;

        private static readonly EnvVar ENV_RECONCILIATION = new EnvVar(@"OSPREY_RECONCILIATION", EnvVarGroup.behavior,
            @"Experiment: symmetric-decoys builds a decoy's consensus RT only from its picks in " +
            @"the runs where its paired target's peptide qualified. Unset pools every pick.")
            { Values = new[] { RECONCILIATION_SYMMETRIC_DECOYS } };

        /// <summary>
        /// OSPREY_RECONCILIATION (experiment): <c>symmetric-decoys</c> builds a decoy's consensus RT
        /// only from its picks in the runs where its paired target's peptide qualified, as the
        /// target's consensus is built only from its qualifying runs. Unset, the decoy's consensus
        /// pools every pick. Any other value throws. (There is no <c>off</c>: with reconciliation
        /// disabled the first pass writes no reconciliation data and per-file rescoring cannot run.)
        /// </summary>
        public static readonly string ReconciliationMode = ParseReconciliationMode(
            ENV_RECONCILIATION.Value);

        /// <summary>True under OSPREY_RECONCILIATION=symmetric-decoys.</summary>
        public static bool ReconciliationSymmetricDecoys => ReconciliationMode == RECONCILIATION_SYMMETRIC_DECOYS;

        private const string RECONCILIATION_SYMMETRIC_DECOYS = @"symmetric-decoys";

        private static string ParseReconciliationMode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            string mode = value.Trim().ToLowerInvariant();
            if (mode == RECONCILIATION_SYMMETRIC_DECOYS)
                return mode;
            throw new InvalidOperationException(string.Format(
                OspreyCoreResources.OspreyEnvironment_ParseSingleValueSwitch__0__must_be___1___or_unset__not___2___,
                @"OSPREY_RECONCILIATION", RECONCILIATION_SYMMETRIC_DECOYS, value));
        }

        private static readonly EnvVar ENV_RECON_REFIT = new EnvVar(@"OSPREY_RECON_REFIT", EnvVarGroup.behavior,
            @"Experiment: off plans cross-run reconciliation and gap-fill with each run's first-pass RT " +
            @"calibration instead of refitting it on the consensus. Keys the reconciliation and second-pass outputs.")
            { Values = new[] { RECON_REFIT_OFF } };

        /// <summary>
        /// OSPREY_RECON_REFIT (experiment): <c>off</c> skips the Stage 6 consensus refit, so each run is
        /// planned with its own first-pass RT calibration. Unset refits as before. Any other value throws.
        /// </summary>
        public static bool ReconciliationRefitOff => ParseReconRefit(ENV_RECON_REFIT.Value);

        /// <summary>
        /// The validity-key term of <see cref="ReconciliationRefitOff"/> for the tasks whose output
        /// the refit reaches (PerFileRescoring, SecondPassFDR): EMPTY when unset, so no directory
        /// written with the refit is invalidated, and <c>;reconrefit=off</c> otherwise.
        /// </summary>
        public static string ReconRefitValidityKeySuffix()
        {
            return ReconRefitValidityKeySuffix(ReconciliationRefitOff);
        }

        /// <summary><see cref="ReconRefitValidityKeySuffix()"/> for an explicitly supplied setting.</summary>
        public static string ReconRefitValidityKeySuffix(bool refitOff)
        {
            return refitOff ? @";reconrefit=" + RECON_REFIT_OFF : string.Empty;
        }

        private const string RECON_REFIT_OFF = @"off";

        private static bool ParseReconRefit(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            if (value.Trim().ToLowerInvariant() == RECON_REFIT_OFF)
                return true;
            throw new InvalidOperationException(string.Format(
                OspreyCoreResources.OspreyEnvironment_ParseSingleValueSwitch__0__must_be___1___or_unset__not___2___,
                @"OSPREY_RECON_REFIT", RECON_REFIT_OFF, value));
        }

        private static readonly EnvVar ENV_DUMP_RUN_PEP = new EnvVar(@"OSPREY_DUMP_RUN_PEP", EnvVarGroup.diagnostic,
            @"Experiment: a folder. The transfer second pass fits each run's PEP from its " +
            @"first-pass target-vs-paired-decoy winners and writes every survivor's class, " +
            @"score, run q and run PEP to <run>.run_pep.tsv there.")
            { ValueExample = @"<folder>" };

        /// <summary>
        /// OSPREY_DUMP_RUN_PEP (experiment): a folder. When set, the transfer second pass fits each
        /// run's posterior error probability from that run's first-pass target-vs-paired-decoy
        /// winners and writes, per run, every survivor's class (unchanged, moved, gap-fill), score,
        /// run q and run PEP to <c>&lt;run&gt;.run_pep.tsv</c> there. Null when unset: nothing is fitted.
        /// </summary>
        public static readonly string RunPepDumpDir = ParseRunPepDumpDir(
            ENV_DUMP_RUN_PEP.Value);

        private static readonly EnvVar ENV_PEP_FORMULA = new EnvVar(@"OSPREY_PEP_FORMULA", EnvVarGroup.behavior,
            @"Experiment: ratio estimates PEP as (D/T) fD(s) / fT(s) instead of pi0 fD / (pi0 fD " +
            @"+ (1 - pi0) fT), which overstates PEP by about 1 / (1 - pi0). Unset keeps the " +
            @"current formula.")
            { Values = new[] { @"ratio" } };

        /// <summary>
        /// OSPREY_PEP_FORMULA=ratio (experiment): <c>PepEstimator</c> estimates PEP as
        /// (D/T) fD(s) / fT(s) - with paired competition the expected false target winners at a score
        /// equal the decoy winners there - instead of pi0 fD / (pi0 fD + (1 - pi0) fT), which treats the
        /// density of ALL target winners (false and true) as the true-match density and so overstates PEP
        /// by about 1 / (1 - pi0). Unset keeps the current formula; any other value throws.
        /// </summary>
        public static readonly bool PepRatioForm = ParsePepFormula(ENV_PEP_FORMULA.Value);

        private static bool ParsePepFormula(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            if (string.Equals(value.Trim(), @"ratio", StringComparison.OrdinalIgnoreCase))
                return true;
            throw new InvalidOperationException(string.Format(
                OspreyCoreResources.OspreyEnvironment_ParseSingleValueSwitch__0__must_be___1___or_unset__not___2___,
                @"OSPREY_PEP_FORMULA", @"ratio", value));
        }

        private static string[] ParseFeatureMask(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            if (string.Equals(value.Trim(), FEATURE_MASK_NONE, StringComparison.OrdinalIgnoreCase))
                return Array.Empty<string>();
            return value.Split(',').Select(name => name.Trim()).Where(name => name.Length > 0).ToArray();
        }

        private static string ParseRunPepDumpDir(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static int? ParseXicFragments(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;
            if (int.TryParse(value.Trim(), NumberStyles.None,
                    CultureInfo.InvariantCulture, out int n) && n >= MIN_XIC_FRAGMENTS && n <= MAX_XIC_FRAGMENTS)
                return n;
            throw new InvalidOperationException(string.Format(
                OspreyCoreResources.OspreyEnvironment_ParseXicFragments__0__must_be_a_whole_number_from__1__to__2___not___3___,
                @"OSPREY_XIC_FRAGMENTS", MIN_XIC_FRAGMENTS, MAX_XIC_FRAGMENTS, value));
        }

        /// <summary>
        /// OSPREY_GBT_OOF=1 (experiment): on the streaming score pass, scores each tree-model training row with
        /// only the fold model that held it out, as the cross-validated direct path does, instead of the average
        /// of every fold model (two of which trained on it). Not keyed: run it into its own output directory.
        /// The held-out folds are not persisted with the first-pass model, so a resumed first pass retrains
        /// rather than adopt the saved model. Known limitation: the second-pass rescoring and transfer
        /// (FrozenModelScorer, PerFileRescoreTask) still score training rows with the fold mean.
        /// </summary>
        public static readonly bool GbtOutOfFold = ParseIntOrNull(ENV_GBT_OOF) == 1;

        private static readonly EnvVar ENV_GBT_PREFILTER_SIZE = new EnvVar(@"OSPREY_GBT_PREFILTER_SIZE", EnvVarGroup.behavior,
            @"Experiment, first pass, either classifier (the GBT_ prefix is historical): two-round training. Round 1 trains on the usual random " +
            @"subsample; its fold models score every best-per-precursor candidate, and round 2 retrains on " +
            @"the N rows of the best-scoring peptide groups. Changes the validity key. Loads the features of every candidate " +
            @"during training (about double the resident features). Ignored, with a warning, by the " +
            @"projection path. 0 is off. Unset, it is " + PROTEIN_BEST_PREFILTER_SIZE.ToString(CultureInfo.InvariantCulture) + @" with " +
            OspreyArgNames.PREFIX + OspreyArgNames.PROTEIN_BEST + @" on (the default), whose column needs round 2, and off with it off.")
            { ValueExample = @"<N>" };

        /// <summary>
        /// OSPREY_GBT_PREFILTER_SIZE=N: two-round first-pass training, as set (0 when unset; the size a
        /// run trains with is <see cref="PrefilterSizeFor"/>, which defaults it for --protein-best). Round 1 trains as usual
        /// on the random MaxTrainSize subsample; its fold models then score every best-per-precursor candidate, and
        /// round 2 retrains on the N rows of the peptide groups that score best. Either classifier, the name
        /// notwithstanding; 0 when unset. Keyed through the classifier validity-key term
        /// (PercolatorEngine.GbdtValidityKeySuffix, which appends it for the SVM too), so a run with it never
        /// adopts outputs trained without it.
        /// Memory: the first pass then loads the features of every best-per-precursor candidate, not only the
        /// MaxTrainSize subsample - about candidates x features doubles resident during training. Read only
        /// by the streamed first pass; the projection path ignores it and logs a warning.
        /// </summary>
        public static int GbtPrefilterSize => Math.Max(0, ParseIntOrZero(ENV_GBT_PREFILTER_SIZE));

        private static readonly EnvVar ENV_GBT_PREFILTER_Q = new EnvVar(@"OSPREY_GBT_PREFILTER_Q", EnvVarGroup.behavior,
            @"Experiment, with OSPREY_GBT_PREFILTER_SIZE as a row cap: round 2 keeps every peptide group whose " +
            @"best round-1 score reaches the score where target-decoy competition on the round-1 scores reaches " +
            @"this q, instead of a fixed row count. A number in (0, 1]; anything else stops the run. Changes the validity key.")
            { ValueExample = @"<q>" };

        /// <summary>OSPREY_GBT_PREFILTER_Q exactly as set, or null when unset.</summary>
        public static readonly string GbtPrefilterQSetting = ENV_GBT_PREFILTER_Q.Value;

        /// <summary>
        /// OSPREY_GBT_PREFILTER_Q=q (experiment): round 2 of the two-round training
        /// (<see cref="GbtPrefilterSize"/>, which must also be set and becomes a row cap) keeps every peptide
        /// group whose best round-1 score reaches the score at which paired target-decoy competition on the
        /// round-1 scores reaches q, instead of a fixed row count - a cut that scales with the data. The
        /// selection only chooses training rows: every candidate is still scored and competed afterwards.
        /// Keyed through the gbdt validity-key term. Null when unset or not a number in (0, 1].
        /// </summary>
        public static readonly double? GbtPrefilterQ = ParseFdrTrainFdr(GbtPrefilterQSetting);

        /// <summary>True when OSPREY_GBT_PREFILTER_Q is set to anything but a number in (0, 1]; startup aborts.</summary>
        public static readonly bool GbtPrefilterQUnrecognized =
            !string.IsNullOrEmpty(GbtPrefilterQSetting) && !GbtPrefilterQ.HasValue;

        private static readonly EnvVar ENV_GBT_PREFILTER_LFDR = new EnvVar(@"OSPREY_GBT_PREFILTER_LFDR", EnvVarGroup.behavior,
            @"Experiment, with OSPREY_GBT_PREFILTER_SIZE as a row cap: round 2 keeps every peptide group down to " +
            @"the round-1 score where the local FDR (the windowed ratio of decoy to target competition winners) " +
            @"first exceeds this value. A number in (0, 1], not together with OSPREY_GBT_PREFILTER_Q; anything " +
            @"else stops the run. Changes the validity key.")
            { ValueExample = @"<f>" };

        /// <summary>OSPREY_GBT_PREFILTER_LFDR exactly as set, or null when unset.</summary>
        public static readonly string GbtPrefilterLocalFdrSetting = ENV_GBT_PREFILTER_LFDR.Value;

        /// <summary>
        /// OSPREY_GBT_PREFILTER_LFDR=f (experiment): like <see cref="GbtPrefilterQ"/>, but round 2 keeps every
        /// peptide group down to the round-1 score where the local FDR - the windowed ratio of decoy to target
        /// competition winners - first exceeds f, so the cut lands wherever targets and decoys stop separating.
        /// Keyed through the gbdt validity-key term. Null when unset or not a number in (0, 1].
        /// </summary>
        public static readonly double? GbtPrefilterLocalFdr = ParseFdrTrainFdr(GbtPrefilterLocalFdrSetting);

        /// <summary>True when OSPREY_GBT_PREFILTER_LFDR is set to anything but a number in (0, 1], or is set
        /// together with OSPREY_GBT_PREFILTER_Q; startup aborts.</summary>
        public static readonly bool GbtPrefilterLocalFdrUnrecognized =
            !string.IsNullOrEmpty(GbtPrefilterLocalFdrSetting) && (!GbtPrefilterLocalFdr.HasValue || GbtPrefilterQ.HasValue);

        private static readonly EnvVar ENV_GBT_PREFILTER_SAMPLE = new EnvVar(@"OSPREY_GBT_PREFILTER_SAMPLE", EnvVarGroup.behavior,
            @"Experiment: 1 makes round 2 take a seeded random sample of the eligible peptide groups (those at " +
            @"or above the OSPREY_GBT_PREFILTER_Q or _LFDR cut, or all of them) up to OSPREY_GBT_PREFILTER_SIZE " +
            @"rows, instead of the best-scoring ones. Changes the validity key.")
            { Values = new[] { @"0", @"1" }, DefaultValue = @"0" };

        /// <summary>
        /// OSPREY_GBT_PREFILTER_SAMPLE=1 (experiment): when the peptide groups eligible for round 2 of the
        /// two-round training (everything at or above the OSPREY_GBT_PREFILTER_Q / _LFDR cut, or every group
        /// without one) hold more rows than <see cref="GbtPrefilterSize"/>, round 2 takes a seeded random
        /// sample of those groups up to that cap instead of the best-scoring ones, so the cap bounds the
        /// training cost without dropping the low-confidence end of the eligible set. Keyed through the gbdt
        /// validity-key term.
        /// </summary>
        public static readonly bool GbtPrefilterSample = ParseIntOrNull(ENV_GBT_PREFILTER_SAMPLE) == 1;

        /// <summary>
        /// True when OSPREY_GBT_PREFILTER_Q, _LFDR or _SAMPLE is set while the run trains one round
        /// (<paramref name="size"/>, from <see cref="PrefilterSizeFor"/>, is 0); startup aborts. They only
        /// shape round 2 of the two-round training, so the run would be the one-round baseline under the
        /// baseline's validity key.
        /// </summary>
        public static bool PrefilterSwitchesWithoutSize(string qSetting, string localFdrSetting, bool sample, int size)
        {
            return size <= 0 && (!string.IsNullOrEmpty(qSetting) || !string.IsNullOrEmpty(localFdrSetting) || sample);
        }

        /// <summary>
        /// The round-2 row count of the two-round first-pass training with <c>--protein-best on</c> and
        /// OSPREY_GBT_PREFILTER_SIZE unset: the column reaches the model only through round 2, and this
        /// is the size every protein-best experiment trained with.
        /// </summary>
        public const int PROTEIN_BEST_PREFILTER_SIZE = 200000;

        /// <summary>The <see cref="ProteinBestDecoyOwn"/> spelling of the default decoy arm.</summary>
        public const string PROTEIN_BEST_DECOY_PAIRED = @"paired";

        /// <summary>The <see cref="ProteinBestDecoyOwn"/> spelling of the decoy-sibling arm.</summary>
        public const string PROTEIN_BEST_DECOY_OWN = @"own";

        /// <summary>The validity-key and log spelling of the column's reduction: the top sibling.</summary>
        private const string PROTEIN_BEST_MAX = @"max";

        /// <summary>
        /// How many top sibling peptides the column reduces. Always the maximum: the mean-of-top-N
        /// arms the experiment also tried remain in <c>ProteinBestFeature.ComputeColumn</c>, but no
        /// setting selects them.
        /// </summary>
        public const int PROTEIN_BEST_TOP_N = 1;

        private static readonly EnvVar ENV_PROTEIN_BEST_DECOY = new EnvVar(@"OSPREY_PROTEIN_BEST_DECOY", EnvVarGroup.behavior,
            @"With " + OspreyArgNames.PREFIX + OspreyArgNames.PROTEIN_BEST + @" on: paired has a decoy read the value of its " +
            @"paired target's protein, from target siblings, so it sees what a false target would; own has it read decoy " +
            @"siblings only. Changes the validity key.")
            { Values = new[] { PROTEIN_BEST_DECOY_PAIRED, PROTEIN_BEST_DECOY_OWN }, DefaultValue = PROTEIN_BEST_DECOY_PAIRED };

        private static readonly EnvVar ENV_PROTEIN_BEST_SHUFFLE = new EnvVar(@"OSPREY_PROTEIN_BEST_SHUFFLE", EnvVarGroup.behavior,
            @"With " + OspreyArgNames.PREFIX + OspreyArgNames.PROTEIN_BEST + @" on, the negative control: 1 permutes which " +
            @"peptide has which accessions, seeded, keeping every protein's peptide count. Changes the validity key.")
            { Values = new[] { @"0", @"1" }, DefaultValue = @"0" };

        /// <summary>OSPREY_PROTEIN_BEST_DECOY=own: decoys read decoy siblings, not their paired target's value.</summary>
        public static bool ProteinBestDecoyOwn => string.Equals(ENV_PROTEIN_BEST_DECOY.Value?.Trim(),
            PROTEIN_BEST_DECOY_OWN, StringComparison.OrdinalIgnoreCase);

        /// <summary>OSPREY_PROTEIN_BEST_SHUFFLE=1: the negative control, accessions permuted among peptides.</summary>
        public static bool ProteinBestShuffle => ParseIntOrNull(ENV_PROTEIN_BEST_SHUFFLE) == 1;

        /// <summary>The default bound on the runs round 1 of the protein-best training draws from.</summary>
        public const int PROTEIN_BEST_ROUND1_RUNS = 10;

        private static readonly EnvVar ENV_PROTEIN_BEST_ROUND1_RUNS = new EnvVar(@"OSPREY_PROTEIN_BEST_ROUND1_RUNS", EnvVarGroup.behavior,
            @"With " + OspreyArgNames.PREFIX + OspreyArgNames.PROTEIN_BEST + @" on: round 1 of the two-round training draws its " +
            @"rows from at most this many runs, chosen by the training seed, so its feature read is a few files rather than " +
            @"all of them; 0 draws from every run. No effect on a cohort of this many runs or fewer. Changes the validity key.")
            { ValueExample = @"<N>", DefaultValue = @"10" };

        /// <summary>
        /// OSPREY_PROTEIN_BEST_ROUND1_RUNS: the bound on the runs round 1 trains from
        /// (<see cref="PROTEIN_BEST_ROUND1_RUNS"/> unless set; 0 for every run).
        /// </summary>
        public static int ProteinBestRound1Runs => ParseIntOrNull(ENV_PROTEIN_BEST_ROUND1_RUNS) ?? PROTEIN_BEST_ROUND1_RUNS;

        /// <summary>
        /// The round-2 row count of the two-round first-pass training: OSPREY_GBT_PREFILTER_SIZE when it
        /// is set (0 then trains one round), else <see cref="PROTEIN_BEST_PREFILTER_SIZE"/> with the
        /// protein-best column on, which needs round 2, and 0 - one round, the default before the
        /// column - with it off. Re-read on each access, like the size itself.
        /// </summary>
        public static int PrefilterSizeFor(bool proteinBest)
        {
            if (ENV_GBT_PREFILTER_SIZE.IsSet)
                return GbtPrefilterSize;
            return proteinBest ? PROTEIN_BEST_PREFILTER_SIZE : 0;
        }

        /// <summary>
        /// The FDR validity-key term for the protein-aware column: EMPTY when off, so no directory
        /// written without it is invalidated. The model it trains has another feature and different
        /// scores, so a directory written with one arm must never be adopted by another. The terms
        /// are those the OSPREY_PROTEIN_BEST=max experiment wrote, so its directories stay current.
        /// </summary>
        public static string ProteinBestValidityKeySuffix(OspreyConfig config)
        {
            return ProteinBestValidityKeySuffix(config.ProteinBest ? PROTEIN_BEST_TOP_N : 0,
                ProteinBestDecoyOwn, ProteinBestShuffle, ProteinBestRound1Runs);
        }

        /// <summary>
        /// <see cref="ProteinBestValidityKeySuffix(OspreyConfig)"/> for explicitly supplied settings.
        /// The round-1 bound is keyed in every arm, its default included: a cohort larger than the
        /// bound trains a different round 1 than every directory written before the bound existed.
        /// </summary>
        public static string ProteinBestValidityKeySuffix(int topN, bool decoyOwn, bool shuffle, int round1Runs)
        {
            if (topN <= 0)
                return string.Empty;
            return string.Format(CultureInfo.InvariantCulture, @";proteinbest={0};proteinbestdecoy={1}{2};proteinbestround1runs={3}",
                DescribeProteinBestTopN(topN),
                decoyOwn ? PROTEIN_BEST_DECOY_OWN : PROTEIN_BEST_DECOY_PAIRED,
                shuffle ? @";proteinbestshuffle=1" : string.Empty, round1Runs);
        }

        /// <summary>
        /// The run-log line for the protein-best setting, on or off, with the developer switches that
        /// shape it when they are set, and the round-2 size it trains with.
        /// </summary>
        public static string DescribeProteinBest(OspreyConfig config)
        {
            if (!config.ProteinBest)
                return string.Format(@"Protein-best training column: {0}", OspreyArgNames.VALUE_OFF);
            return string.Format(CultureInfo.InvariantCulture,
                @"Protein-best training column: {0} (two-round first pass, round 1 from {4}, round 2 at {1} rows{2}{3})",
                OspreyArgNames.VALUE_ON, PrefilterSizeFor(true),
                ProteinBestDecoyOwn ? @", OSPREY_PROTEIN_BEST_DECOY=" + PROTEIN_BEST_DECOY_OWN : string.Empty,
                ProteinBestShuffle ? @", OSPREY_PROTEIN_BEST_SHUFFLE=1 (negative control)" : string.Empty,
                ProteinBestRound1Runs > 0
                    ? string.Format(CultureInfo.InvariantCulture, @"at most {0} runs", ProteinBestRound1Runs)
                    : @"every run");
        }

        /// <summary>
        /// The startup refusal for this run's protein-best settings, or null when they hold. Either
        /// classifier can train with the column, through the two-round training. Off, the developer
        /// switches must be unset too: they would do nothing while the run kept the baseline's key.
        /// </summary>
        public static string ValidateProteinBestSettings(OspreyConfig config)
        {
            string argText = OspreyArgNames.Text(OspreyArgNames.PROTEIN_BEST);
            if (!config.ProteinBest)
            {
                return ENV_PROTEIN_BEST_DECOY.IsSet || ENV_PROTEIN_BEST_SHUFFLE.IsSet || ENV_PROTEIN_BEST_ROUND1_RUNS.IsSet
                    ? string.Format(@"OSPREY_PROTEIN_BEST_DECOY, OSPREY_PROTEIN_BEST_SHUFFLE and OSPREY_PROTEIN_BEST_ROUND1_RUNS " +
                                    @"shape the column {0} adds and cannot be set with {1}.", argText,
                        OspreyArgNames.Text(OspreyArgNames.PROTEIN_BEST, OspreyArgNames.VALUE_OFF))
                    : null;
            }
            if (ProteinBestRound1Runs < 0)
                return @"OSPREY_PROTEIN_BEST_ROUND1_RUNS must be 0 (every run) or a positive number of runs.";
            if (PrefilterSizeFor(true) <= 0)
            {
                return string.Format(@"{0} reaches the model through round 2 of the two-round training: " +
                                     @"OSPREY_GBT_PREFILTER_SIZE=0 turns that off. Unset it, or use {1}.",
                    argText, OspreyArgNames.Text(OspreyArgNames.PROTEIN_BEST, OspreyArgNames.VALUE_OFF));
            }
            return null;
        }

        private static string DescribeProteinBestTopN(int topN)
        {
            return topN == 1
                ? PROTEIN_BEST_MAX
                : @"mean-" + topN.ToString(CultureInfo.InvariantCulture);
        }

        private static readonly EnvVar ENV_DETERMINISTIC_MATH = new EnvVar(@"OSPREY_DETERMINISTIC_MATH", EnvVarGroup.behavior,
            @"Exp and Log in gradient-boosted tree training: 1 the managed fdlibm port, bit-identical on " +
            @"every OS; 0 System.Math, which can differ in the last bit between operating systems.")
            { Values = new[] { @"0", @"1" }, DefaultValue = @"1" };

        /// <summary>OSPREY_DETERMINISTIC_MATH exactly as set, or null when unset. Unset (or
        /// <c>1</c>) selects the managed fdlibm Exp and Log in <see cref="DeterministicMath"/>,
        /// which return the same bits on every OS; <c>0</c> selects <see cref="Math.Exp"/> and
        /// <see cref="Math.Log(double)"/>, which come from the OS C library and differ in the
        /// last bit between Windows and Linux. An A/B switch, for measuring what determinism
        /// costs and for reproducing a model trained before it.</summary>
        public static readonly string DeterministicMathSetting =
            ENV_DETERMINISTIC_MATH.Value;

        /// <summary>True when OSPREY_DETERMINISTIC_MATH is set to anything but <c>0</c> or
        /// <c>1</c>. Program startup ABORTS on this, as it does for
        /// <see cref="SvmCSelectionToleranceUnrecognized"/>: falling back to the default would
        /// file the default arm's numbers under the name of the arm the caller asked for.</summary>
        public static readonly bool DeterministicMathUnrecognized =
            !string.IsNullOrEmpty(DeterministicMathSetting) &&
            !ParseDeterministicMath(DeterministicMathSetting).HasValue;

        /// <summary>Whether <see cref="DeterministicMath"/> computes Exp and Log with its
        /// platform-independent fdlibm arm (the default) rather than <see cref="Math"/>. Read
        /// once into a static, so the JIT folds the dispatch to a constant; code that must
        /// exercise both arms in one process passes the arm explicitly instead.</summary>
        public static readonly bool UseDeterministicMath = ParseDeterministicMath(DeterministicMathSetting) ?? true;

        private static readonly EnvVar ENV_TRAIN_PICK_RUN = new EnvVar(@"OSPREY_TRAIN_PICK_RUN", EnvVarGroup.behavior,
            @"Represent each precursor in first-pass training by one uniformly drawn run's best candidate " +
            @"peak. 0 restores the cross-run maximum.")
            { DefaultValue = @"1" };

        /// <summary>OSPREY_TRAIN_PICK_RUN: represent each precursor in the first-pass training
        /// subset by ONE uniformly sampled RUN -- contributing that run's best candidate peak --
        /// instead of by its best observation across all runs. ON by default; setting the variable
        /// to 0 restores the historical cross-run maximum for A/B work.
        ///
        /// The two halves are both load-carrying. Pass 1 is PRE-COMPACTION, so a precursor has
        /// several candidate-peak rows per run; drawing per ROW rather than per RUN would weight a
        /// run by how many candidates it produced AND leave a random candidate as the training row,
        /// which measured -17.4% identifications on 3-file Stellar.
        ///
        /// Why the default moved: the cross-run maximum makes every training row an extreme value
        /// over however many files were batched together, so the training population drifts away
        /// from the per-run population the model is then applied to, and it drifts further as the
        /// batch grows (mean coelution_sum 1.19 at one file vs 5.75 at 82, against 1.18 in the
        /// scored population). Sampling one run trains on ordinary per-run observations at the
        /// same row count and the same memory, and at 82 SEA-AD files it is worth +24.7%
        /// discoveries at matched true FDP -- enough that it removed a library deficit previously
        /// read as a property of the library.
        ///
        /// The escape hatch is INVERTED deliberately. Every script written while this was an
        /// experiment sets the variable to 1, and <see cref="IsNotZero"/> reads that as the
        /// shipped behaviour rather than silently reverting those scripts to the old sampling.
        ///
        /// Honored by EVERY selection path. That is a requirement of being the default rather
        /// than a lever: while it was opt-in the paths that could not honor it aborted, which is
        /// a defensible experiment guard and an indefensible shipped behaviour.</summary>
        public static readonly bool TrainPickRun = IsNotZero(ENV_TRAIN_PICK_RUN);

        private static readonly EnvVar ENV_GBT_INNER_FOLDS = new EnvVar(@"OSPREY_GBT_INNER_FOLDS", EnvVarGroup.behavior,
            @"Gradient-boosted trees: inner folds for held-out iteration selection. 1 or less selects " +
            @"in-sample.")
            { ValueExample = @"<N>", DefaultValue = @"5" };

        /// <summary>Inner-fold count for the GBDT's held-out iteration selection
        /// (OSPREY_GBT_INNER_FOLDS, default 5 -> hold out 20% of each training fold to pick
        /// the boosting iteration honestly). A value &lt;= 1 turns held-out selection OFF and
        /// reverts to IN-SAMPLE selection (fit = validate = all training rows) -- the
        /// pre-held-out, validated behavior. Exposed so a regularization sweep or an
        /// in-sample-vs-held-out A/B runs without a code revert. Tree-only.</summary>
        public static readonly int GbtInnerFolds = ParseIntOrNull(ENV_GBT_INNER_FOLDS) ?? 5;

        private static readonly EnvVar ENV_MZML_DECODE_THREADS = new EnvVar(@"OSPREY_MZML_DECODE_THREADS", EnvVarGroup.performance,
            @"Threads that decode mzML binary arrays. 1 is the serial decode, the A/B arm.")
            { ValueExample = @"<N>", DefaultValue = @"8" };

        /// <summary>Threads pwiz-sharp uses to decode mzML binary arrays
        /// (OSPREY_MZML_DECODE_THREADS, default 8). The library ships this OFF (1, decode
        /// inline on the read thread) because a host processing several FILES at once should
        /// not also go wide underneath its own parallelism. Osprey is the other shape: it
        /// funnels reads through a one-permit gate, so the read phase has the machine to
        /// itself. Measured on a 5.99 GB Astral mzML, 1 thread takes 83.4s and 8 takes 54.6s,
        /// after which the curve is flat -- 16, 24 and 32 all land within a second of 8,
        /// because the floor is the XML parse, which stays serial. Past the knee extra threads
        /// only add memory pressure, so this does NOT scale off core count or --threads.
        /// Set to 1 to A/B against the serial decode without a rebuild.</summary>
        public static readonly int MzmlDecodeThreads = ParseIntOrNull(ENV_MZML_DECODE_THREADS) ?? 8;

        /// <summary>The <see cref="Pass2QValue"/> confidence-transfer mode: do NOT retrain
        /// or re-estimate a null; score each reconciled peak with the frozen 1st-pass model
        /// and map it to a q via the full pre-compaction 1st-pass score-&gt;q table.</summary>
        public const string PASS2_QVALUE_TRANSFER = @"transfer";

        /// <summary>The <see cref="Pass2QValue"/> protein-anchored constrained mode: the FROZEN
        /// 1st-pass model (no retrain), with the target-decoy competition CONSTRAINED to the
        /// peptides of proteins
        /// detected in the 1st pass -- included as target+decoy PAIRS so the stratum's null
        /// stays fair. Removing off-stratum decoys from the null lowers q for stratum
        /// members (reduced multiple testing / independent filtering; Bourgon 2010), which
        /// recovers marginal peptides of already-detected proteins. The stratum's COMPLEMENT
        /// is competed the same way, against its own paired decoys, rather than keeping its
        /// 1st-pass q: the stratum takes most of the high-scoring targets and few of the
        /// high-scoring decoys, so the decoys it excludes must still count against the
        /// complement's targets (issue #4581). The frozen model avoids the two-pass
        /// retrain's over-separation.</summary>
        public const string PASS2_QVALUE_PROTEIN_COMPACT = @"protein-compact";

        /// <summary>The <see cref="Pass2StrataValidityKeySuffix"/> term under protein-compact.</summary>
        public const string PASS2_STRATA_TERM = @";pass2strata=2";

        /// <summary>The <see cref="ExperimentAgg"/> default: each precursor/peptide keeps its
        /// single BEST (max) observation across runs before the experiment-wide target/decoy
        /// competition. The shipped behavior; the committed golden is byte-identical here.</summary>
        public const string EXPERIMENT_AGG_MAX = @"max";

        /// <summary>Prefix of the <see cref="ExperimentAgg"/> reproducibility mode value
        /// OSPREY_EXPERIMENT_AGG=mean-best-&lt;N&gt; (e.g. mean-best-2, mean-best-3, mean-best-4): the
        /// experiment-wide PRECURSOR score becomes the mean of its best-N per-run scores (runs beyond
        /// the detected count are filled with the decoy-median floor; see the OSPREY_MEANBEST2_FLOOR_*
        /// A/B toggles), rolled up by MAX to PEPTIDE. Not to protein: protein FDR ranks groups by the
        /// max RAW per-peptide SVM score and never reads this aggregate, so mean(best-N) reaches
        /// protein results only through which peptides clear the experiment-q gate. Larger N rewards
        /// detection in more runs (drives toward the &gt;=N-run reproducibility frontier). Symmetric
        /// for decoys, so the null stays honest - the honest sensitivity lever for #4484 vs. the
        /// target-conditioned transfer-compete/protein-compact. N is read from the flag value and
        /// bounded by <see cref="MEAN_BEST_N_MAX"/>; a future command argument may pick N
        /// intelligently from the run count.</summary>
        public const string EXPERIMENT_AGG_MEAN_BEST_PREFIX = @"mean-best-";

        /// <summary>Largest accepted N in OSPREY_EXPERIMENT_AGG=mean-best-&lt;N&gt;. Two unbounded
        /// failures motivate a cap rather than trust: <c>mean-best-1000000</c> allocates an
        /// N-wide accumulator per (base_id, side) - gigabytes, an out-of-memory abort seconds
        /// into Stage 5 - and any N above the run count leaves EVERY unit floor-filled, which
        /// saturates the statistic (for N &gt;= the largest observation count the aggregate is
        /// <c>(S - L*floor)/N + floor</c>, whose ranking no longer depends on N at all) while
        /// still being recorded by the operator as a distinct A/B arm. 64 is far above any
        /// plausible reproducibility frontier for a DIA experiment and well inside both
        /// failures. Values above it are rejected as unrecognized, so
        /// <see cref="ExperimentAggUnrecognized"/> stops the run at startup instead of it silently
        /// proceeding on the max default.</summary>
        public const int MEAN_BEST_N_MAX = 64;

        /// <summary>
        /// OSPREY_PASS2_QVALUE: selects how the SecondPassFDR 2nd pass assigns the reported
        /// precursor/peptide q-values AFTER Stage 6 reconciliation. The 2nd-pass peak
        /// RE-SCORING (better peak choices against the consensus) is kept in ALL modes;
        /// only the q-value step changes.
        ///   <see cref="PASS2_QVALUE_PROTEIN_COMPACT"/> (default): frozen 1st-pass model, with
        ///     the target-decoy competition stratified - the protein stratum and its complement
        ///     each compete against their own paired decoys.
        ///   <see cref="PASS2_QVALUE_TRANSFER"/>: carry the pass-1 q through and recompute ONLY
        ///     the per-run q of the peaks reconciliation MOVED, scoring each with the FROZEN
        ///     1st-pass model and mapping it through THAT FILE'S OWN on-disk
        ///     <c>.1st-pass.fdr_scores.bin</c> score-&gt;run-q table, one file at a time, in
        ///     PerFileRescoring like the default (#4665). No retrain, no reduced-pool null.
        ///     Restores calibration while keeping the re-scoring ID gain.
        ///     NOTE: the per-run-only redesign (#4438) REPLACED the earlier full pre-compaction
        ///     score-&gt;q table, which is why transfer no longer needs the O(files) resident
        ///     pool. Re-adding a resident pool for it is the #4446 regression.
        /// Unset normalizes to the default; an unrecognized value is a startup ERROR (see
        /// <see cref="Pass2QValueUnrecognized"/>). Re-read on each access, so a command-line
        /// test can set it (<see cref="OverrideVariables"/>).
        ///
        /// SECOND-PASS RETRAINING IS GONE, and these two modes are what remain. The former
        /// default <c>percolator</c> - retrain the 2nd-pass Percolator SVM and recompute a
        /// target/decoy null on the reconciled + COMPACTED pool - was REMOVED, not merely
        /// demoted. Compaction strips most decoys from that pool, so the retrained null is thin
        /// and the reported q anti-conservative: 1.57% true FDP at a nominal 1% on Stellar
        /// libdecoy entrapment (vs 0.92% for the 1st-pass q), and ~9% on an 82-file SEA-AD set.
        /// The <c>OSPREY_PROTEIN_COMPACT_RETRAIN</c> A/B toggle that reached it followed. So did
        /// <c>transfer-compete</c>, for a related but distinct reason: its competition ran over a
        /// TARGET-CONDITIONED subset - survivors chosen by target per-run q, decoys admitted only
        /// by base_id pairing - which strips decoys that WON the 1st-pass competition and so
        /// improves pass-2 q with no added evidence. Measured 1.96% true FDP at a nominal 1% on
        /// 82-file SEA-AD against 1.53% for the default, with fewer ids: dominated on both axes.
        /// See issues #4484 (closed) and #4581 (open, the same bias in the surviving default), and
        /// docs/12-second-pass-fdr.md, "Why a second-pass null is a problem".
        ///
        /// The consequence worth naming, because it simplifies everything downstream: THERE IS
        /// NO SECOND-PASS MODEL. The linear model the 1st-pass SVM trained is the model for
        /// pass 2, unchanged - only the score DISTRIBUTIONS differ, because pass 2 runs on a
        /// subset. Anything that used to ask a retrained pass-2 model for its weights can read
        /// the frozen ones instead.
        ///
        /// Switching modes within one output directory is now SAFE: the mode participates in
        /// the resume validity key through <see cref="Pass2QValueValidityKeySuffix()"/>, so a
        /// re-run under a different mode invalidates the previous mode's outputs instead of
        /// adopting its cached q-values. That retires the standing "use a FRESH --output-dir
        /// per mode" limitation, which became far more than an experimental-mode caveat once
        /// this variable acquired a non-percolator default.
        /// </summary>
        public static string Pass2QValue => NormalizePass2QValue(Pass2QValueSetting);

        private static readonly EnvVar ENV_PASS2_QVALUE = new EnvVar(@"OSPREY_PASS2_QVALUE", EnvVarGroup.behavior,
            @"How the second pass assigns reported q-values: protein-compact constrains the competition to " +
            @"detected proteins; transfer carries the first-pass q through and re-scores only moved peaks.")
            { Values = new[] { @"protein-compact", @"transfer" }, DefaultValue = @"protein-compact" };

        /// <summary>OSPREY_PASS2_QVALUE exactly as set, for a message that must quote what the
        /// user typed. Null when unset.</summary>
        public static string Pass2QValueSetting => ENV_PASS2_QVALUE.Value;

        /// <summary>True when OSPREY_PASS2_QVALUE was set to a value that is not one of the
        /// recognized modes. Program startup ABORTS on this rather than falling back: silently
        /// substituting the default would report numbers the caller did not ask for, and the
        /// removed <c>percolator</c> token in particular is one that existing sweep scripts
        /// still pass. Checked at startup, not at SecondPassFDR, so the run fails in seconds
        /// instead of after Stage 1-5.</summary>
        public static bool Pass2QValueUnrecognized => IsUnrecognizedPass2QValue(Pass2QValueSetting);

        /// <summary>True when <see cref="Pass2QValue"/> selects the frozen-model
        /// confidence-transfer path (OSPREY_PASS2_QVALUE=transfer).</summary>
        public static bool Pass2TransferQ =>
            string.Equals(Pass2QValue, PASS2_QVALUE_TRANSFER, StringComparison.Ordinal);

        /// <summary>True when <see cref="Pass2QValue"/> selects the protein-anchored
        /// constrained competition (OSPREY_PASS2_QVALUE=protein-compact).</summary>
        public static bool Pass2ProteinCompact =>
            string.Equals(Pass2QValue, PASS2_QVALUE_PROTEIN_COMPACT, StringComparison.Ordinal);

        private static readonly EnvVar ENV_PASS2_VERIFY_WORKER = new EnvVar(@"OSPREY_PASS2_VERIFY_WORKER", EnvVarGroup.diagnostic,
            @"Re-run the per-file second-pass competition in SecondPassFDR and assert it against the " +
            @"rescore worker's answer. A test instrument: it re-reads every first-pass file.");

        /// <summary>
        /// OSPREY_PASS2_VERIFY_WORKER: re-run the per-file second-pass competition inside Stage 7
        /// and assert it against the answer the rescore worker wrote (issue #4486). A TEST
        /// INSTRUMENT, off by default.
        ///
        /// <para><b>Why it is not always on.</b> The recompute re-reads each file's 1st-pass
        /// sidecar and re-runs the frozen rescore - measured at 82 files as 9.2 GB of re-reads
        /// against the 2.8 GB the join actually needs, inside an 860.8 s Stage 7. That is
        /// precisely the cost #4486 exists to remove, so leaving it on would hide the whole gain
        /// behind a check of the code that produces it.</para>
        ///
        /// <para><b>What it is and is not.</b> Both sides call the SAME
        /// <c>StreamingFdr.CompeteOneFile</c>, so this verifies that the worker's inputs and the
        /// transmission of its answer agree - it cannot see a defect INSIDE that function, where
        /// both sides would be wrong together. It is also blind to the artifact's POPULATION: a
        /// gap-fill never competes, so dropping one changes no competition map, which is why 594
        /// dropped records once passed this check and surfaced only in a diagnostics golden. The
        /// population and order are covered instead by
        /// <c>Pass2FdrSidecar.AssertSidecarDescribesPool</c>, which is cheap and always on.</para>
        ///
        /// <para>Deliberately NOT in any validity key: it changes no output, so including it
        /// would invalidate every cached artifact the moment it was flipped, turning a
        /// diagnostic into a re-run.</para>
        ///
        /// <para>Re-read on each access rather than fixed at class load, so an in-process test
        /// can switch it per command line with <see cref="OverrideVariables"/>.</para>
        /// </summary>
        public static bool Pass2VerifyWorker =>
            IsSetAndNotZero(ENV_PASS2_VERIFY_WORKER);

        private static readonly EnvVar ENV_DIANN_REPORTS = new EnvVar(@"OSPREY_DIANN_REPORTS", EnvVarGroup.diagnostic,
            @"Also write protein_groups.tsv and stats.tsv beside the output, the results in the shape of " +
            @"DIA-NN's reports, for comparing the two tools.");

        /// <summary>
        /// OSPREY_DIANN_REPORTS: also write <c>&lt;output&gt;.protein_groups.tsv</c> and
        /// <c>&lt;output&gt;.stats.tsv</c>, the search's results in the shape of DIA-NN's
        /// <c>pg_matrix</c> and <c>stats.tsv</c>, for comparing the two tools. Off by default:
        /// they hold nothing the output .blib does not, and nothing in Osprey reads them.
        /// Re-read on every access, so a test can switch it per command line with
        /// <see cref="OverrideVariables"/>.
        /// </summary>
        public static bool DiannReports =>
            IsSetAndNotZero(ENV_DIANN_REPORTS);

        private static readonly EnvVar ENV_EXPERIMENT_AGG = new EnvVar(@"OSPREY_EXPERIMENT_AGG", EnvVarGroup.behavior,
            @"How first-pass experiment-wide scores aggregate a precursor's runs: max, or the mean of the " +
            @"best N runs (N from 2 to 64), missing runs filled with a decoy floor. Experimental.")
            { Values = new[] { @"max", @"mean-best-<N>" }, DefaultValue = @"max", AcceptsValue = v => !IsUnrecognizedExperimentAgg(v) };

        /// <summary>The N in OSPREY_EXPERIMENT_AGG=mean-best-&lt;N&gt;: how many top per-run scores are
        /// averaged (runs beyond the detected count filled with the decoy floor). 0 in the default
        /// max mode; otherwise in [2, <see cref="MEAN_BEST_N_MAX"/>]. A value outside that range,
        /// or unrecognized, falls back to max (and sets <see cref="ExperimentAggUnrecognized"/>).
        ///
        /// Initialized from the environment at process start, exactly as before; a settable
        /// property only so unit tests can exercise the mean(best-N) arm at all. It is the SINGLE
        /// source of truth - <see cref="ExperimentAgg"/> and <see cref="ExperimentAggMeanBest"/>
        /// are computed from it rather than snapshotted, so a test that pins N cannot leave the
        /// three disagreeing (the failure mode that made <c>MeanBestFloorOverspecified</c> a
        /// computed property too). Nothing in the pipeline writes it.</summary>
        public static int MeanBestN { get; set; } = ParseMeanBestN(
            ENV_EXPERIMENT_AGG.Value);

        /// <summary>OSPREY_EXPERIMENT_AGG: how the 1st-pass EXPERIMENT-wide precursor/peptide
        /// score aggregates a unit's per-run observations before the target/decoy competition.
        /// <see cref="EXPERIMENT_AGG_MAX"/> (default, byte-identical golden) or
        /// <see cref="EXPERIMENT_AGG_MEAN_BEST_PREFIX"/>&lt;N&gt;. The normalized spelling of
        /// <see cref="MeanBestN"/> - this is the form persisted as pass-1 provenance and appended
        /// to the cache validity key, so it must never disagree with the N actually used.</summary>
        public static string ExperimentAgg =>
            MeanBestN >= 2
                ? EXPERIMENT_AGG_MEAN_BEST_PREFIX +
                  MeanBestN.ToString(CultureInfo.InvariantCulture)
                : EXPERIMENT_AGG_MAX;

        /// <summary>True when <see cref="MeanBestN"/> selects a mean(best-N) reproducibility score
        /// (OSPREY_EXPERIMENT_AGG=mean-best-N, N&gt;=2).</summary>
        public static bool ExperimentAggMeanBest => MeanBestN >= 2;

        /// <summary>True when OSPREY_EXPERIMENT_AGG was set to something that is neither
        /// <see cref="EXPERIMENT_AGG_MAX"/> nor a well-formed
        /// <see cref="EXPERIMENT_AGG_MEAN_BEST_PREFIX"/>&lt;N&gt; with N&gt;=2, and was therefore
        /// normalized to the max default. Program startup ABORTS on it, mirroring
        /// <see cref="Pass2QValueUnrecognized"/>. This matters more here than for most flags: the
        /// whole point of this one is A/B measurement, so a typo (mean-best-1, meanbest2) that
        /// silently ran the DEFAULT would be recorded by the operator as a mean(best-N) result and
        /// would corrupt the comparison rather than fail it.</summary>
        public static readonly bool ExperimentAggUnrecognized = IsUnrecognizedExperimentAgg(
            ENV_EXPERIMENT_AGG.Value);

        private static readonly EnvVar ENV_MEANBEST2_FLOOR_MEAN = new EnvVar(@"OSPREY_MEANBEST2_FLOOR_MEAN", EnvVarGroup.behavior,
            @"With mean-best-<N>, fill missing runs with the decoy mean instead of the decoy median.");

        /// <summary>OSPREY_MEANBEST2_FLOOR_MEAN: A/B toggle to use the decoy MEAN instead of the
        /// default decoy MEDIAN as the missing-run floor. Off by default. Applies at every N
        /// despite the MEANBEST2 name, which predates the best-2 -> best-N generalization.
        /// TAKES PRECEDENCE over <see cref="MeanBest2FloorPercentile"/>; setting both is a
        /// configuration error and is refused rather than silently resolved.
        /// A settable property (not a readonly field) so unit tests can pin the floor instead of
        /// inheriting whatever the operator exported for a floor sweep - the aggregation tests
        /// assert exact floor-dependent values, so an ambient variable would fail them.</summary>
        public static bool MeanBest2FloorMean { get; set; } =
            IsSetAndNotZero(ENV_MEANBEST2_FLOOR_MEAN);

        private static readonly EnvVar ENV_MEANBEST2_FLOOR_PCT = new EnvVar(@"OSPREY_MEANBEST2_FLOOR_PCT", EnvVarGroup.behavior,
            @"With mean-best-<N>, fill missing runs with this percentile (0 to 100) of the decoy scores " +
            @"instead of the median.")
            { ValueExample = @"<percent>" };

        /// <summary>OSPREY_MEANBEST2_FLOOR_PCT: A/B override to use a low PERCENTILE (0-100) of the
        /// decoy score distribution as the missing-run floor instead of the median center - a
        /// harder reproducibility cut. Null (unset) selects the median default. Applies at every N
        /// (see the MEANBEST2 naming note above). Settable for the same test reason.</summary>
        public static double? MeanBest2FloorPercentile { get; set; } =
            ParseDoubleOrNull(ENV_MEANBEST2_FLOOR_PCT);

        /// <summary>True when BOTH floor overrides are set. They are not composable -
        /// <see cref="MeanBest2FloorMean"/> would silently win and the percentile would never be
        /// consulted, so an operator sweeping OSPREY_MEANBEST2_FLOOR_PCT with a stale
        /// OSPREY_MEANBEST2_FLOOR_MEAN=1 still exported would log a percentile arm while measuring
        /// the mean. The consuming site refuses the combination.</summary>
        /// Computed, not snapshotted: the two toggles above are settable properties (so tests can
        /// pin the floor), and a readonly field capturing the raw environment at type load would
        /// disagree with them in both directions.
        public static bool MeanBestFloorOverspecified =>
            MeanBest2FloorMean && MeanBest2FloorPercentile.HasValue;

        /// <summary>
        /// One line naming the experiment-wide aggregation this process will actually use,
        /// including the missing-run floor arm. Logged unconditionally in the startup settings
        /// block - a POSITIVE statement, not only a warning - because the dominant real failure
        /// of an environment-variable flag is that the variable never reached the process
        /// (Start-Process without the parent environment, an HPC job spec, a scheduled launch,
        /// a service account). Unset is indistinguishable from a typo'd-then-normalized value in
        /// the output itself, so without this line an operator records a DEFAULT run as a
        /// mean(best-N) result and the A/B is silently corrupted rather than failed. Mirrors the
        /// positive "mode active" line OSPREY_PASS2_QVALUE prints.
        /// </summary>
        public static string DescribeExperimentAgg()
        {
            if (!ExperimentAggMeanBest)
            {
                return string.Format(OspreyCoreResources.OspreyEnvironment_DescribeExperimentAgg_Experiment_aggregation___0___default___best_observation_per_unit_,
                    EXPERIMENT_AGG_MAX);
            }
            return string.Format(
                @"Experiment aggregation: {0} ACTIVE - experiment-wide precursor score is the mean " +
                @"of its best {1} per-run scores; missing-run floor = {2}",
                ExperimentAgg, MeanBestN, DescribeMeanBestFloor());
        }

        /// <summary>
        /// Validate the OSPREY_EXPERIMENT_AGG family against each other and against the run's
        /// file count. Returns null when the settings are usable, or an operator-actionable
        /// message when they are not. Shared by the startup check
        /// (<c>Program.ValidateArgs</c>, before any I/O) and the Stage-5 consuming site, so a
        /// resumed or single-task run that skips one still hits the other and both say the same
        /// thing. Every check is gated on the aggregation being ENGAGED: with
        /// OSPREY_EXPERIMENT_AGG unset none of these variables is read, so refusing an ordinary
        /// run over a floor sweep the operator merely left exported would break the default path.
        /// </summary>
        /// <param name="fileCount">Number of runs (input files) this analysis will aggregate
        /// across, or 0 when the caller does not know it yet.</param>
        public static string ValidateExperimentAggSettings(int fileCount)
        {
            if (!ExperimentAggMeanBest)
                return null;
            if (MeanBestFloorOverspecified)
            {
                return @"OSPREY_MEANBEST2_FLOOR_MEAN and OSPREY_MEANBEST2_FLOOR_PCT are both set. " +
                       @"They are not composable: FLOOR_MEAN wins and the percentile is never " +
                       @"consulted, so a sweep would record a percentile arm while measuring the " +
                       @"decoy mean. Set exactly one.";
            }
            // Validated ONCE here rather than where the floor is computed: the streaming
            // estimator reaches its percentile only at the END of Stage 5, hours into a large
            // run, and the resident twin silently clamps the same value - so a mid-run refusal
            // would both come too late to be actionable and disagree with the other path.
            double? pct = MeanBest2FloorPercentile;
            if (pct.HasValue && (double.IsNaN(pct.Value) || pct.Value < 0.0 || pct.Value > 100.0))
            {
                return string.Format(
                    @"OSPREY_MEANBEST2_FLOOR_PCT={0} is outside the valid range [0, 100]. It names a " +
                    @"percentile of the decoy score distribution to use as the missing-run floor.",
                    pct.Value.ToString(CultureInfo.InvariantCulture));
            }
            if (fileCount > 0 && MeanBestN > fileCount)
            {
                return string.Format(
                    @"OSPREY_EXPERIMENT_AGG={0} averages the best {1} per-run scores, but this " +
                    @"analysis has only {2} run(s). Every unit would be floor-filled for the " +
                    @"missing {3}, which saturates the statistic - for any N at or above the " +
                    @"largest observation count the ranking, and therefore every q-value, is " +
                    @"identical - so the arm would be recorded as distinct while measuring the " +
                    @"same thing. Use N <= {2}.",
                    ExperimentAgg, MeanBestN, fileCount, MeanBestN - fileCount);
            }
            return null;
        }

        /// <summary>
        /// The cache-invalidation suffix for any task whose output depends on the experiment-wide
        /// aggregation, or the EMPTY string when the aggregation is not engaged.
        ///
        /// Empty by default and not merely "the default arm's value": <see cref="ExperimentAgg"/>
        /// normalizes an unset variable to the constant "max", so appending unconditionally would
        /// change the key of every DEFAULT run and invalidate every output directory produced
        /// before this suffix existed - re-running Stage 5 FDR, protein FDR and compaction (hours
        /// at 82 files) to reproduce byte-identical output. That exact regression shipped once and
        /// the byte-identity gate could not see it, because the gate always uses fresh directories.
        ///
        /// One helper rather than three copies: the suffix must be IDENTICAL across
        /// <c>FirstPassFdrTask</c>, <c>PerFileRescoreTask</c> and <c>SecondPassFdrTask</c>. When only the
        /// first had it, a flipped arm re-ran Stage 5 while the downstream reconciled parquets,
        /// 2nd-pass sidecars and .blib were reused from the OTHER arm - a self-inconsistent output
        /// set that no single task's key could have caught.
        /// </summary>
        public static string ExperimentAggValidityKeySuffix()
        {
            if (!ExperimentAggMeanBest)
                return string.Empty;
            return @";expagg=" + ExperimentAgg
                   + @";floormean=" + MeanBest2FloorMean
                   + @";floorpct=" + (MeanBest2FloorPercentile?.ToString(
                       CultureInfo.InvariantCulture) ?? @"none");
        }

        /// <summary>
        /// The cache-invalidation suffix for any task whose output depends on the peak-pick
        /// model - which is every task from per-file scoring onward, because the pick decides
        /// WHICH peak each precursor's row describes.
        ///
        /// UNCONDITIONAL, unlike <see cref="ExperimentAggValidityKeySuffix"/>, and that
        /// difference is the point. The aggregation suffix is empty for its default arm because
        /// that arm's output never changed; this arm's DID. The default moved from the
        /// product-form pick to the learned model (<see cref="PickLda"/>), so "emits nothing"
        /// already describes every output directory written before the flip. Emitting nothing
        /// for the new default too would make a post-flip key EQUAL a pre-flip key, and the
        /// resume driver would adopt product-form parquets as though the learned model had
        /// produced them.
        ///
        /// The one-time cost is real: every output directory written before this shipped is
        /// invalidated, so a warm re-run or a <c>-LinkFrom</c> adoption re-runs Stage 1-4. That
        /// is the correct outcome, not a regression - those artifacts were picked by a
        /// different model, and reusing them reports one model's peaks under the other's name.
        ///
        /// The arm can also be passed explicitly, because the environment is read once into a
        /// static and cannot be flipped at run time - a test would otherwise be able to assert
        /// only whichever arm the test process happens to be running under.
        /// </summary>
        public static string PickValidityKeySuffix()
        {
            return PickValidityKeySuffix(PickLda, PickLdaModelPath)
                   + (TcHalfWindowMinutes.HasValue ? @";tcwin=" + TcHalfWindowMinutes.Value.ToString(@"R", CultureInfo.InvariantCulture) : string.Empty)
                   + (MinFragmentTolerancePpm.HasValue ? @";ftolmin=" + MinFragmentTolerancePpm.Value.ToString(@"R", CultureInfo.InvariantCulture) : string.Empty)
                   + (CorrFragmentTolerancePpm.HasValue ? @";fcorr=" + CorrFragmentTolerancePpm.Value.ToString(@"R", CultureInfo.InvariantCulture) : string.Empty)
                   + (RefXicByCorrelation ? @";refxic=corr" : string.Empty)
                   + (LocalRtToleranceNeighbors.HasValue ? @";rtlocal=" + LocalRtToleranceNeighbors.Value.ToString(CultureInfo.InvariantCulture) : string.Empty);
        }

        /// <summary>
        /// <see cref="PickValidityKeySuffix()"/> for an explicitly supplied arm. The model PATH
        /// participates as well as the on/off flag: <see cref="PickLdaModelPath"/> overrides the
        /// resolution-keyed default outright, so two runs can differ in nothing else.
        /// </summary>
        public static string PickValidityKeySuffix(bool pickLda, string pickModelPath)
        {
            return @";pick=" + (pickLda ? @"lda" : @"product")
                   + @";pickmodel=" + (string.IsNullOrEmpty(pickModelPath) ? @"none" : pickModelPath);
        }

        /// <summary>
        /// The cache-invalidation suffix for any task whose output depends on the 2nd-pass
        /// q-value mode. UNCONDITIONAL for the same reason as
        /// <see cref="PickValidityKeySuffix()"/>: the default moved from the removed
        /// <c>percolator</c> retrain to <see cref="PASS2_QVALUE_PROTEIN_COMPACT"/>, so an empty
        /// suffix would let a run adopt the other mode's cached q-values.
        ///
        /// This is the tagging the <see cref="Pass2QValue"/> remarks used to defer, and it
        /// retires the "use a FRESH --output-dir per mode" limitation they carried.
        /// </summary>
        public static string Pass2QValueValidityKeySuffix()
        {
            return Pass2QValueValidityKeySuffix(Pass2QValue);
        }

        /// <summary>
        /// <see cref="Pass2QValueValidityKeySuffix()"/> for an explicitly supplied mode, in the
        /// normalized form <see cref="Pass2QValue"/> holds.
        /// </summary>
        public static string Pass2QValueValidityKeySuffix(string normalizedPass2QValue)
        {
            return @";pass2=" + normalizedPass2QValue;
        }

        /// <summary>
        /// Validity-key suffix for what cross-run reconciliation moves under
        /// <see cref="PASS2_QVALUE_TRANSFER"/>, which carries first-pass q-values forward and
        /// re-competes nothing: only the precursors the blib reports (the Stage 6 consensus
        /// FirstPassFDR writes), and none of their paired decoys (what PerFileRescoring
        /// re-scores). Both differ from the builds that reconciled every precursor passing at any
        /// level in any run, decoys included, so FirstPassFDR, PerFileRescoring and SecondPassFDR
        /// carry it. Empty under protein-compact, whose outputs are unchanged.
        /// </summary>
        public static string TransferReconciliationValidityKeySuffix()
        {
            return Pass2TransferQ ? @";reconset=output;recondecoys=0;pepfloor=reported" : string.Empty;
        }

        /// <summary>
        /// Validity-key suffix for the FDR experiment switches <see cref="ReconciliationMode"/>
        /// and <see cref="PepRatioForm"/>: each changes the first-pass consensus or PEP and so
        /// every FDR output after it. Empty when both are unset, so default outputs keep their key.
        /// The FDR classifier switches follow (<see cref="FdrClassifierValidityKeySuffix(ulong?, double?)"/>).
        /// </summary>
        public static string FdrExperimentValidityKeySuffix()
        {
            return FdrExperimentValidityKeySuffix(ReconciliationMode, PepRatioForm)
                   + FdrClassifierValidityKeySuffix(FdrSeed, FdrTrainFdr);
        }

        /// <summary>
        /// <see cref="FdrExperimentValidityKeySuffix()"/> for explicitly supplied settings, in the
        /// normalized form <see cref="ReconciliationMode"/> holds.
        /// </summary>
        public static string FdrExperimentValidityKeySuffix(string reconciliationMode, bool pepRatioForm)
        {
            string suffix = string.Empty;
            if (reconciliationMode != null)
                suffix += @";recon=" + reconciliationMode;
            if (pepRatioForm)
                suffix += @";pep=ratio";
            return suffix;
        }

        /// <summary>
        /// Validity-key suffix for the FDR classifier switches <see cref="FdrSeed"/> and
        /// <see cref="FdrTrainFdr"/>: each changes the draw or the positives the classifier trains
        /// on, and so every FDR output. A switch emits nothing when unset, so default outputs keep
        /// their key.
        /// </summary>
        public static string FdrClassifierValidityKeySuffix(ulong? seed, double? trainFdr)
        {
            string suffix = string.Empty;
            if (seed.HasValue)
                suffix += @";fdrseed=" + seed.Value.ToString(CultureInfo.InvariantCulture);
            if (trainFdr.HasValue)
                suffix += @";fdrtrain=" + trainFdr.Value.ToString(@"R", CultureInfo.InvariantCulture);
            return suffix;
        }

        /// <summary>
        /// Validity-key term for the MEANING of protein-compact's 2nd-pass q (issue #4581): the
        /// stratum's complement now competes against its own decoys instead of keeping its
        /// 1st-pass q (an unchanged off-stratum survivor used to take run q 1.0). No byte layout
        /// and no mode string changed, so without this term a resume or <c>-LinkFrom</c> into an
        /// output directory written before the change finds the per-run 2nd-pass sidecars and
        /// the .blib valid and adopts the old answers. Carried by the two tasks that write them,
        /// PerFileRescoring and SecondPassFDR - not FirstPassFDR, whose outputs did not change.
        /// Empty under <see cref="PASS2_QVALUE_TRANSFER"/>, which the change does not reach.
        /// </summary>
        public static string Pass2StrataValidityKeySuffix()
        {
            return Pass2ProteinCompact ? PASS2_STRATA_TERM : string.Empty;
        }

        /// <summary>
        /// Validity-key suffix for the first-pass TRAINING levers
        /// (<see cref="TrainPickRun"/>, <see cref="MaxTrainSizeOverride"/>,
        /// <see cref="SvmCSelectionTolerance"/>). Each changes the model trained and therefore
        /// every score, q and count downstream, so a directory written under one setting must not
        /// be adopted under another.
        ///
        /// Without this a re-run under a changed setting reports "FirstPassFDR:skipping (outputs
        /// valid)" in seconds and hands back the PREVIOUS setting's numbers - which reads exactly
        /// like a change that had no effect, the most expensive possible failure for an A/B.
        ///
        /// The levers are keyed differently ON PURPOSE, and the difference is the same one
        /// <see cref="PickValidityKeySuffix()"/> draws. <see cref="TrainPickRun"/> is a FLIPPED
        /// DEFAULT, so it is emitted for every arm including the new default: "emits nothing"
        /// already describes every directory written before the flip, and an empty new default
        /// would make a post-flip key EQUAL a pre-flip one, letting a resume or a
        /// <c>-LinkFrom</c> adopt maximum-trained scores as though the reservoir had produced
        /// them. <see cref="MaxTrainSizeOverride"/> is a plain knob whose default never moved,
        /// so it stays silent for the default and keys only when set. The C-selection tolerance
        /// is a flipped default too (the grid search used to keep the strict maximum), so it is
        /// emitted for every arm.
        ///
        /// The one-time cost of the unconditional terms is real and is the correct outcome: every
        /// FirstPassFDR-and-later directory written before each of them shipped is invalidated.
        /// PerFileScoring (Stages 1-4) carries no training suffix, so a <c>-LinkFrom</c> still
        /// adopts the expensive extraction artifacts and only the FDR tail re-runs.
        /// </summary>
        public static string TrainSampleValidityKeySuffix()
        {
            return TrainSampleValidityKeySuffix(TrainPickRun, MaxTrainSizeOverride, SvmCSelectionTolerance);
        }

        /// <summary>
        /// <see cref="TrainSampleValidityKeySuffix()"/> for explicitly supplied settings, so the
        /// arms can be compared without mutating process-wide state the environment reads once.
        /// </summary>
        public static string TrainSampleValidityKeySuffix(bool trainPickRun, int? maxTrainSizeOverride, double cSelectionTolerance)
        {
            string suffix = @";trainpick=" + (trainPickRun ? @"run" : @"max");
            if (maxTrainSizeOverride.HasValue)
            {
                suffix += @";maxtrain=" +
                          maxTrainSizeOverride.Value.ToString(CultureInfo.InvariantCulture);
            }
            suffix += @";csel=" + cSelectionTolerance.ToString(@"R", CultureInfo.InvariantCulture);
            return suffix;
        }

        /// <summary>
        /// True when <paramref name="normalizedAgg"/> names a mean(best-N) arm. Takes the arm as
        /// an ARGUMENT rather than reading <see cref="ExperimentAggMeanBest"/> so a consumer can
        /// ask about an arm RECORDED BY ANOTHER PROCESS - a distributed <c>--task SecondPassFDR</c>
        /// SecondPassFDR node reloads the 1st-pass model from disk and must gate on the arm that trained
        /// it, not on its own environment. Expects the normalized form
        /// (<see cref="ExperimentAgg"/>) as persisted alongside the model.
        /// </summary>
        public static bool IsMeanBestArm(string normalizedAgg)
        {
            return !string.IsNullOrEmpty(normalizedAgg) &&
                   normalizedAgg.StartsWith(EXPERIMENT_AGG_MEAN_BEST_PREFIX, StringComparison.Ordinal);
        }

        /// <summary>The missing-run floor arm in words, for
        /// <see cref="DescribeExperimentAgg"/>.</summary>
        private static string DescribeMeanBestFloor()
        {
            if (MeanBest2FloorMean)
                return @"decoy MEAN (OSPREY_MEANBEST2_FLOOR_MEAN)";
            double? pct = MeanBest2FloorPercentile;
            if (pct.HasValue)
            {
                return string.Format(@"decoy {0} percentile (OSPREY_MEANBEST2_FLOOR_PCT)",
                    pct.Value.ToString(@"0.###", CultureInfo.InvariantCulture));
            }
            return @"decoy MEDIAN (default)";
        }

        // Parse N from OSPREY_EXPERIMENT_AGG=mean-best-<N>. Returns 0 (the max default) when unset,
        // not a mean-best-<N> value, or N outside [2, MEAN_BEST_N_MAX] - and 0 makes
        // IsUnrecognizedExperimentAgg true, so an out-of-range N warns rather than silently
        // running the default.
        private static int ParseMeanBestN(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return 0;
            string v = raw.Trim().ToLowerInvariant();
            if (!v.StartsWith(EXPERIMENT_AGG_MEAN_BEST_PREFIX, StringComparison.Ordinal))
                return 0;
            string tail = v.Substring(EXPERIMENT_AGG_MEAN_BEST_PREFIX.Length);
            // NumberStyles.None, not Integer: Integer also accepts a leading sign and surrounding
            // whitespace, so "mean-best-+3" and "mean-best- 3" parsed as 3 - two spellings of the
            // same arm that an A/B log could not tell apart from the canonical one.
            return int.TryParse(tail, NumberStyles.None,
                       CultureInfo.InvariantCulture, out int n) &&
                   n >= 2 && n <= MEAN_BEST_N_MAX
                ? n
                : 0;
        }

        // A set-but-unusable OSPREY_EXPERIMENT_AGG: anything that is neither the max default nor a
        // well-formed mean-best-<N> with N >= 2. Unset / whitespace is NOT unrecognized - that is
        // simply the default. Mirrors IsUnrecognizedPass2QValue.
        private static bool IsUnrecognizedExperimentAgg(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return false;
            return !string.Equals(raw.Trim(), EXPERIMENT_AGG_MAX, StringComparison.OrdinalIgnoreCase) &&
                   ParseMeanBestN(raw) == 0;
        }

        private static string NormalizePass2QValue(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return PASS2_QVALUE_PROTEIN_COMPACT;
            string v = raw.Trim().ToLowerInvariant();
            if (v == PASS2_QVALUE_TRANSFER)
                return PASS2_QVALUE_TRANSFER;
            if (v == PASS2_QVALUE_PROTEIN_COMPACT)
                return PASS2_QVALUE_PROTEIN_COMPACT;
            // An unrecognized token normalizes to the default only so the other statics are
            // well-formed; the run does not get here, because Program aborts at startup on
            // IsUnrecognizedPass2QValue.
            return PASS2_QVALUE_PROTEIN_COMPACT;
        }

        private static bool IsUnrecognizedPass2QValue(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return false;
            string v = raw.Trim().ToLowerInvariant();
            return v != PASS2_QVALUE_TRANSFER && v != PASS2_QVALUE_PROTEIN_COMPACT;
        }

        private static readonly EnvVar ENV_VERSION_OVERRIDE = new EnvVar(OspreyVersion.ENV_OVERRIDE, EnvVarGroup.allowance,
            @"Run as this build version, so intermediate files written by another day's build are used " +
            @"instead of refused.")
            { ValueExample = @"<version>" };

        /// <summary>
        /// OSPREY_VERSION_OVERRIDE, as <see cref="OspreyVersion.Current"/> reads it: through the
        /// test seam, on each access, so a test can run one in-process command as another build.
        /// </summary>
        internal static string VersionOverride => ENV_VERSION_OVERRIDE.Value;

        /// <summary>
        /// Replace the named variables with <paramref name="values"/> until the returned object
        /// is disposed (<see cref="EnvVar.OverrideVariables"/>).
        /// </summary>
        internal static IDisposable OverrideVariables(IReadOnlyDictionary<string, string> values)
        {
            return EnvVar.OverrideVariables(values);
        }

        private static int ParseIntOrZero(EnvVar envVar)
        {
            string v = envVar.Value;
            if (string.IsNullOrEmpty(v))
                return 0;
            int.TryParse(v, out int result);
            return result;
        }

        /// <summary>Env int override, or null when unset/unparseable -- lets a consumer keep
        /// its own default rather than collapsing an unset var to 0 (as ParseIntOrZero does).</summary>
        private static int? ParseIntOrNull(EnvVar envVar)
        {
            string v = envVar.Value;
            if (string.IsNullOrEmpty(v))
                return null;
            return int.TryParse(v, out int result) ? result : null;
        }

        // The accepted values of a size with no zero arm (OSPREY_BLOCK_READ_MB).
        private static bool IsPositiveInt(string value)
        {
            return int.TryParse(value, out int result) && result > 0;
        }

        /// <summary>Env double override (invariant culture, so "0.5" parses regardless of
        /// locale), or null when unset/unparseable.</summary>
        private static double? ParseDoubleOrNull(EnvVar envVar)
        {
            string v = envVar.Value;
            if (string.IsNullOrEmpty(v))
                return null;
            return double.TryParse(v, NumberStyles.Float,
                CultureInfo.InvariantCulture, out double result)
                ? result : null;
        }

        /// <summary>
        /// An OSPREY_SVM_C_TOLERANCE value as a number in [0, 1) (invariant culture), or null when
        /// it is unset, unparseable or out of range - the null that
        /// <see cref="SvmCSelectionToleranceUnrecognized"/> reports for a set variable. Internal so
        /// a test can pin what is accepted.
        /// </summary>
        internal static double? ParseSvmCSelectionTolerance(string raw)
        {
            if (string.IsNullOrEmpty(raw) ||
                !double.TryParse(raw, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out double tolerance) ||
                !(tolerance >= 0 && tolerance < 1))
            {
                return null;
            }
            // -0 passes the range check but formats as "-0" in the validity key on .NET Core, so
            // it would key apart from the 0 it selects.
            return tolerance == 0 ? 0 : tolerance;
        }

        /// <summary>
        /// An OSPREY_FDR_TRAIN_FDR value as a number in (0, 1] (invariant culture), or null when it is
        /// unset, unparseable or out of range - the null that <see cref="FdrTrainFdrUnrecognized"/>
        /// reports for a set variable. Internal so a test can pin what is accepted.
        /// </summary>
        internal static double? ParseFdrTrainFdr(string raw)
        {
            if (string.IsNullOrEmpty(raw) ||
                !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double trainFdr) ||
                !(trainFdr > 0 && trainFdr <= 1))
            {
                return null;
            }
            return trainFdr;
        }

        /// <summary>
        /// An OSPREY_DETERMINISTIC_MATH value as the arm it selects: true for <c>1</c>, false
        /// for <c>0</c>, null for anything else including unset - the null that
        /// <see cref="DeterministicMathUnrecognized"/> reports for a set variable. Surrounding
        /// whitespace is ignored. Internal so a test can pin what is accepted.
        /// </summary>
        internal static bool? ParseDeterministicMath(string raw)
        {
            switch (raw?.Trim())
            {
                case @"1":
                    return true;
                case @"0":
                    return false;
                default:
                    return null;
            }
        }

        private static bool IsNotZero(EnvVar envVar)
        {
            return envVar.Value != @"0";
        }

        /// <summary>
        /// Set to anything but <c>0</c>. Internal rather than private so a test can pin the
        /// distinction from <see cref="EnvVar.IsSet"/>: the runners write <c>=0</c> to mean off,
        /// and a flag that reaches for <see cref="EnvVar.IsSet"/> turns ON for it (issue #4673).
        /// Every on/off switch that is off by default reads this way; the four that once read
        /// <see cref="EnvVar.IsSet"/> (OSPREY_EXIT_AFTER_CALIBRATION and the like) moved here when
        /// the catalog documented them as 0|1.
        /// </summary>
        internal static bool IsSetAndNotZero(EnvVar envVar)
        {
            string v = envVar.Value;
            return !string.IsNullOrEmpty(v) && v != @"0";
        }
    }
}
