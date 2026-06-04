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
        /// OspreySharp bails with a clear error rather than running with
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

        /// <summary>Inter-replicate peak reconciliation settings.</summary>
        public ReconciliationConfig Reconciliation { get; set; } = new ReconciliationConfig();

        /// <summary>Enable the coelution signal pre-filter.</summary>
        public bool PrefilterEnabled { get; set; } = true;

        /// <summary>Protein-level FDR threshold (enables protein parsimony and picked-protein FDR).</summary>
        public double? ProteinFdr { get; set; }

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
        /// HPC: when true, exit after Stage 5 + reconciliation planning,
        /// having written the boundary files
        /// (<c>&lt;stem&gt;.&lt;phase&gt;-pass.fdr_scores.bin</c> and
        /// <c>&lt;stem&gt;.reconciliation.json</c>) for each input file.
        /// Skips Stage 6 + 7 + 8. Set by the
        /// <c>--join-at-pass=1 --join-only</c> flag combination.
        /// </summary>
        public bool StopAfterStage5 { get; set; }

        /// <summary>
        /// HPC: when true, every <c>--input-scores</c> parquet must carry
        /// <c>osprey.reconciled = "true"</c> in its footer metadata. Set
        /// by <c>--join-at-pass=2</c>; the post-Stage-6 (reconciled)
        /// entry point. Stages 1-6 are skipped: the pipeline loads
        /// reconciled scores + the <c>.{1st,2nd}-pass.fdr_scores.bin</c>
        /// sidecars, then runs Stages 7-8 (second-pass FDR overlay,
        /// protein parsimony + picked-protein FDR, blib output). Mirrors
        /// Rust's <c>config.expect_reconciled_input</c> wired from
        /// <c>main.rs</c> at the same flag.
        /// </summary>
        public bool ExpectReconciledInput { get; set; }

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
