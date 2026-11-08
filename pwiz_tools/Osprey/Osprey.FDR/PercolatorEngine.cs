/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Core;
using pwiz.Osprey.ML;

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// Owns Percolator-based run-level FDR orchestration over the
    /// <see cref="FdrProjectionSet"/> buffer (or the flat streamed first pass):
    /// train on the best-per-precursor subsample, score the full population, and
    /// hand the scores / q-values to the pass's output sink. Moved out of the Tasks layer
    /// (the former <c>FirstPassFdrTask.RunPercolatorFdr</c>) so FDR orchestration
    /// physically lives in the FDR project; the Tasks layer calls this through
    /// a thin facade, passing <c>ctx.LogInfo</c> as the log sink and the PIN
    /// feature names. Pure: takes data + a log delegate, never the pipeline
    /// context -- no diagnostics dump, no process-exit, no byproduct registry
    /// (those stay in the Tasks facade).
    ///
    /// <para>Also holds the streaming first pass, <see cref="RunStreamingFirstPass"/>, and the
    /// private row, sidecar and dedup helpers only it uses. It trains and then scores, so it sits
    /// here with the other train-then-score drivers rather than in
    /// <see cref="PercolatorScorer"/>, which the trainer itself calls (issue #4494).</para>
    /// </summary>
    public static class PercolatorEngine
    {
        /// <summary>The <c>passLabel</c> value that marks the FIRST pass. Named rather than
        /// repeated as a literal because OSPREY_EXPERIMENT_AGG now gates on it: a drift between
        /// the default and the comparison would silently re-enable the aggregation on the 2nd
        /// pass, which is the defect this constant exists to stop recurring.</summary>
        public const string FIRST_PASS_LABEL = @"First-pass";

        // [PATH] cost buckets of RunStreamingFirstPass's two score passes. LANE buckets are timed
        // on the file lanes and summed over them; ORDER buckets on the in-order consumer.
        private const int LANE1_WALK = 0;
        private const int LANE1_SIDECAR = 1;
        private const int LANE1_FEATURES = 2;
        private const int LANE1_SCORE = 3;
        private const int LANE1_COMPETITION = 4;
        private const int LANE1_RUN_Q = 5;
        private const int LANE1_FLOORS = 6;
        private const int LANE1_FLUSH = 7;
        private const int LANE1_SINK = 8;
        private const int PASS1_LANE_BUCKETS = 9;
        private const int ORDER1_COMPETITION = 0;
        private const int PASS1_ORDER_BUCKETS = 1;
        // The --model-diagnostics row walk, the one caller left that needs every row's
        // experiment-scope values.
        private const int FOLD_WALK = 0;
        private const int FOLD_SIDECAR = 1;
        private const int FOLD_RESCORE = 2;
        private const int FOLD_LOOKUPS = 3;
        private const int FOLD_PREPARE = 4;
        private const int FOLD_LANE_BUCKETS = 5;
        private const int FOLD_ORDER_MERGE = 0;
        private const int FOLD_ORDER_BUCKETS = 1;

        /// <summary>
        /// A pass label as a person reads it. <see cref="FIRST_PASS_LABEL"/> is also a token the
        /// code compares and a tagged line carries, so it stays English there; prose shows this.
        /// </summary>
        public static string PassDisplayName(string passLabel)
        {
            return passLabel == FIRST_PASS_LABEL ? OspreyFDRResources.PercolatorEngine_PassDisplayName_First_pass : passLabel;
        }

        /// <summary>
        /// Run Percolator FDR over the thin <see cref="FdrProjectionSet"/> peak buffer
        /// (issue #4355 step (b) increment ii) instead of the full <see cref="FdrEntry"/>
        /// stub buffer. The projection is scored through the parity-locked
        /// <see cref="PercolatorEntry"/> SVM core (strings materialized from the interned
        /// peptide table) and the results go to <paramref name="sink"/> by position. The
        /// <see cref="FdrEntry"/>-buffer overload that served as its byte-identity oracle was
        /// removed with OSPREY_FDR_PROJECTION (2026-10-10). Returns <c>true</c> when a
        /// diagnostic-only (<c>*Only</c>) dump fired and the run should stop early - the
        /// Tasks-layer caller owns the process exit.
        ///
        /// <para>A TEST ORACLE, internal so nothing outside this assembly and its tests can reach
        /// it. It holds every run's projection rows at once, which no production route does any
        /// more: the first pass streams each row from its parquet, and the tests hold that
        /// streaming pass to this resident one.</para>
        /// </summary>
        internal static bool RunPercolatorFdr(
            FdrProjectionSet projections,
            OspreyConfig config,
            OspreyFeatureInfo[] featureInfos,
            IOspreyLog log,
            IFdrOutputSink sink,
            PercolatorDiagnosticsConfig diagnostics = null,
            string passLabel = FIRST_PASS_LABEL,
            Func<string, IReadOnlyList<double[]>> loadFileFeatures = null,
            Func<string, double[]> loadFileApexRts = null,
            Action<FeatureContributions> captureContributions = null,
            Action<PercolatorResults> captureModel = null)
        {
            // The lean FdrProjection no longer stores the q-value outputs (issue #4355
            // struct-shrink S0): the score pass hands them to this per-pass sink (the
            // 1st pass parks them in a parallel array; the 2nd pass streams them to the
            // sidecar), and the sink owns the tail [COUNT] tally. A null sink is a bug.
            if (sink == null)
                throw new ArgumentNullException(nameof(sink));
            int numFeatures = featureInfos.Length;
            var peptideById = projections.PeptideById;

            // Canonical sort: on the first pass each file's parquet is already written in
            // (entry_id, charge, scan_number) order, so ParquetIndex increases with
            // scan_number within a (entry_id, charge) group. Sorting by
            // (EntryId, Charge, ParquetIndex) is therefore a total order that yields
            // the identical sequence an (EntryId, Charge, ScanNumber, ParquetIndex) sort
            // produces (risk #3) - and a stable no-op on the already-sorted first-pass buffer.
            foreach (var kvp in projections.PerFile)
            {
                kvp.Value.Sort((a, b) => // Array.Sort OK: terminal key ParquetIndex is unique per distinct (entry_id,charge,scan) observation (1st pass: original-parquet row; 2nd pass: reconciled row); the only possible tie is two rows collapsed to one reconciled row, which are IDENTICAL (same features, Score, entry_id, sidecar record), so their order is irrelevant to the output.
                {
                    int c = a.EntryId.CompareTo(b.EntryId);
                    if (c != 0) return c;
                    c = a.Charge.CompareTo(b.Charge);
                    if (c != 0) return c;
                    return FdrEntry.CompareParquetIndex(a.ParquetIndex, b.ParquetIndex);
                });
            }

            // The projection path always STREAMS its features per file (for both the
            // 1st and 2nd pass), so a per-file feature loader is mandatory -- the
            // 1st-pass caller (FirstPassFdrTask) and the 2nd-pass caller (Pass2FdrSidecar)
            // both supply one. A null loader reaching here is a bug; there is no resident
            // build to fall back to.
            if (loadFileFeatures == null)
                throw new InvalidOperationException(
                    @"The projection RunPercolatorFdr overload always streams features " +
                    @"per file; a per-file feature loader is required. A null loader here " +
                    @"is a bug.");

            var percConfig = BuildProjectionPercolatorConfig(config, featureInfos, diagnostics);
            long n = projections.TotalRows;

            // Streaming-only (cross-impl parity with the Rust streaming-only change):
            // ALWAYS run the projection-native streaming score + compete pass, regardless
            // of population size. The former sub-threshold direct branch built a full
            // PercolatorEntry list, dispatched the SVM over ALL entries, and zipped a
            // PercolatorResult list back; unifying on streaming removes that transient
            // full-population stack and the divergent standardizer-fit population (the
            // direct path fit on all entries, streaming fits on the best-per-precursor
            // subsample). One path, lower memory, and matched to Rust.
            LogProjectionInputCounts(
                projections, numFeatures, loadFileFeatures, log, passLabel);
            log.LogInfo(string.Format(OspreyFDRResources.PercolatorEngine_RunPercolatorFdr_Running__0__Percolator_on__1__precursor_candidate_peaks___,
                PassDisplayName(passLabel), n));
            bool streamingAbort = RunStreamingIntoProjection(
                projections.PerFile, peptideById, percConfig, log, passLabel,
                loadFileFeatures, loadFileApexRts, sink, captureContributions, captureModel);
            if (streamingAbort)
                return true;

            // Tail [COUNT] logging (per-file pass counts, total, unique precursors)
            // moves into sink.Finish (issue #4355 struct-shrink S0, correction §0a):
            // the q-values are no longer on the struct, so the tally must read the
            // LIVE values the sink accumulated during the score pass (the 1st-pass
            // parallel array or the 2nd-pass streamed values) -- reading them off the
            // projection here would read the now-absent fields. The sink also flushes
            // any deferred per-file output (2nd-pass sidecars). Same values, same
            // FdrLevel selection, so identical [COUNT] lines.
            sink.Finish(log);
            return false;
        }

        /// <summary>
        /// The feature report <see cref="RunFirstPassStreaming"/> hands its
        /// <c>captureContributions</c>, rebuilt by a later process from the first-pass
        /// <paramref name="model"/> and every run's <see cref="FeatureSums"/> share in run order
        /// (each run's first-pass sidecar carries one). Null when there is no run or a run has
        /// no share.
        /// </summary>
        public static FeatureContributions FirstPassContributionsFromShares(PercolatorResults model,
            IEnumerable<FeatureSums> shares, OspreyFeatureInfo[] featureInfos)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            return PercolatorScorer.ContributionsFromShares(model, shares, featureInfos);
        }

        /// <summary>
        /// Projection-free 1st-pass Percolator (issue #4355 struct-shrink S3, Stage B): the
        /// FLAT-memory entry point that holds NO resident row buffer. The counterpart of the
        /// <see cref="RunPercolatorFdr(FdrProjectionSet,OspreyConfig,OspreyFeatureInfo[],IOspreyLog,IFdrOutputSink,PercolatorDiagnosticsConfig,string,System.Func{string,System.Collections.Generic.IReadOnlyList{double[]}},System.Func{string,double[]},System.Action{FeatureContributions},System.Action{PercolatorResults})"/>
        /// projection overload for the lean 1st-pass case, it builds the same parity-locked
        /// <see cref="PercolatorConfig"/> and delegates to
        /// <see cref="RunStreamingFirstPass"/>, which streams every row's identity +
        /// features from the caller-supplied row source instead of a resident
        /// <see cref="FdrProjectionSet"/>. The caller (Tasks layer) wires
        /// <paramref name="streamFileRows"/> to the per-file parquet scalar reader and
        /// <paramref name="loadFileFeatures"/> to the per-file feature loader, keeping this project
        /// free of an Osprey.IO dependency. Returns <c>true</c> on a diagnostic-only train abort.
        /// </summary>
        public static bool RunFirstPassStreaming(
            IReadOnlyList<string> fileNames,
            Action<string, StubColumns, Action<uint, byte, bool, double, string, double>> streamFileRows,
            Func<string, IReadOnlyList<double[]>> loadFileFeatures,
            OspreyConfig config,
            OspreyFeatureInfo[] featureInfos,
            IOspreyLog log,
            IFdrFirstPassSink sink,
            PercolatorDiagnosticsConfig diagnostics = null,
            string passLabel = FIRST_PASS_LABEL,
            Action<FeatureContributions> captureContributions = null,
            Action<PercolatorResults> captureModel = null,
            CompletedScoreStreamer tryStreamCompletedScores = null,
            PercolatorResults pretrainedModel = null,
            FileRunScopeSink flushFileRunScope = null,
            int fileLanes = 1,
            Func<string, FeatureSums> tryLoadFeatureSums = null,
            ProteinBestRequest proteinBest = null)
        {
            if (sink == null)
                throw new ArgumentNullException(nameof(sink));
            if (loadFileFeatures == null)
                throw new InvalidOperationException(
                    @"RunFirstPassStreaming always streams features per file; a per-file feature " +
                    @"loader is required.");
            var percConfig = BuildProjectionPercolatorConfig(config, featureInfos, diagnostics);
            percConfig.ProteinBest = proteinBest;
            return RunStreamingFirstPass(
                fileNames, streamFileRows, loadFileFeatures, percConfig, log, passLabel, sink,
                captureContributions, captureModel, tryStreamCompletedScores, pretrainedModel,
                flushFileRunScope, fileLanes, tryLoadFeatureSums);
        }

        /// <summary>
        /// The validity-key term for the classifier that trains and applies the first-pass
        /// model: EMPTY unless <c>OSPREY_FDR_MODEL=gbdt</c>, and under gbdt the model plus every tree
        /// setting that changes the trained model - the <see cref="GbtParams"/> this process
        /// trains with, <see cref="OspreyEnvironment.GbtMaxIterations"/> and
        /// <see cref="OspreyEnvironment.GbtInnerFolds"/>. FirstPassFDR, PerFileRescoring and
        /// SecondPassFDR append it, because each writes output that model determines.
        ///
        /// <para>Empty for the linear SVM on purpose, in the shape of
        /// <see cref="OspreyEnvironment.ExperimentAggValidityKeySuffix"/>: the SVM's outputs never
        /// changed, so an unconditional term would invalidate every percolator output directory to
        /// record nothing. Keyed for gbdt because without it one arm's directory was adopted by
        /// the other - a percolator directory re-run as gbdt, a gbdt directory written before the
        /// lean first pass trained trees (so holding SVM-scored results), or one OSPREY_GBT_*
        /// sweep point re-run as the next - each reporting "skipping (outputs valid)" and handing
        /// back the other arm's numbers.</para>
        ///
        /// <para>Kept out of the base task key and out of <c>SearchParameterHash</c>, which must
        /// match the Rust implementation; gbdt has no Rust counterpart. The term is spelled
        /// <c>fdrmodel=</c> after the variable that selects it; it read <c>fdrmethod=</c> while
        /// <c>--fdr-method</c> did, and no release carried that spelling.</para>
        /// </summary>
        public static string GbdtValidityKeySuffix(OspreyConfig config)
        {
            string suffix = GbdtValidityKeySuffix(config?.FdrClassifier ?? FdrClassifier.LinearSvm, BuildGbtParams(),
                OspreyEnvironment.GbtMaxIterations, OspreyEnvironment.GbtInnerFolds);
            // The two-round training (OSPREY_GBT_PREFILTER_SIZE) trains a different model, so it
            // is keyed when set - and only then, so every existing key is unchanged without it.
            if (suffix.Length > 0 && OspreyEnvironment.GbtPrefilterSize > 0)
            {
                suffix += string.Format(CultureInfo.InvariantCulture, @";gbtprefilter={0}",
                    OspreyEnvironment.GbtPrefilterSize);
                if (OspreyEnvironment.GbtPrefilterQ.HasValue)
                {
                    suffix += string.Format(CultureInfo.InvariantCulture, @";gbtprefilterq={0:R}",
                        OspreyEnvironment.GbtPrefilterQ.Value);
                }
                if (OspreyEnvironment.GbtPrefilterLocalFdr.HasValue)
                {
                    suffix += string.Format(CultureInfo.InvariantCulture, @";gbtprefilterlfdr={0:R}",
                        OspreyEnvironment.GbtPrefilterLocalFdr.Value);
                }
                if (OspreyEnvironment.GbtPrefilterSample)
                    suffix += @";gbtprefiltersample=1";
                suffix += OspreyEnvironment.ProteinBestValidityKeySuffix();
            }
            return suffix;
        }

        /// <summary>
        /// <see cref="GbdtValidityKeySuffix(OspreyConfig)"/> for explicitly supplied settings, so
        /// the arms can be compared without the process-wide environment, which is read once.
        /// <see cref="GbtParams.MaxDegreeOfParallelism"/> is the one field left out: the trained
        /// model is bit-identical at any value, so keying it would invalidate a directory for a
        /// difference that cannot exist. <see cref="GbtParams.UseDeterministicMath"/> keys only
        /// when false (OSPREY_DETERMINISTIC_MATH=0), so the default term, and every gbdt
        /// directory written under it, is unchanged.
        /// </summary>
        public static string GbdtValidityKeySuffix(FdrClassifier fdrClassifier, GbtParams gbtParams,
            int maxIterations, int innerFolds)
        {
            if (fdrClassifier != FdrClassifier.Gbdt)
                return string.Empty;
            return string.Format(CultureInfo.InvariantCulture,
                @";fdrmodel=gbdt;gbtobjective={0};gbttrees={1};gbtdepth={2};gbtlr={3:R}" +
                @";gbtminchild={4:R};gbtsubsample={5:R};gbtcolsample={6:R};gbtgamma={7:R}" +
                @";gbtlambda={8:R};gbtalpha={9:R};gbtbins={10};gbtseed={11}" +
                @";gbtiterations={12};gbtinnerfolds={13}{14}",
                gbtParams.Objective, gbtParams.NTrees, gbtParams.MaxDepth, gbtParams.LearningRate,
                gbtParams.MinChildWeight, gbtParams.Subsample, gbtParams.ColSample, gbtParams.Gamma,
                gbtParams.RegLambda, gbtParams.RegAlpha, gbtParams.MaxBins, gbtParams.Seed,
                maxIterations, innerFolds, gbtParams.UseDeterministicMath ? string.Empty : @";gbtmath=system");
        }

        /// <summary>
        /// Build the first-pass <see cref="PercolatorConfig"/> shared by the projection and
        /// flat streamed first-pass paths. Centralized (issue
        /// #4355 step (b) increment iii) so every SVM knob is IDENTICAL whether the
        /// SVM runs off a <see cref="PercolatorEntry"/> list or the projection row
        /// count -- the two buffer shapes cannot select a different SVM path for the
        /// same population. <c>MaxTrainSize</c> (the streaming training-subsample size)
        /// is the <see cref="PercolatorConfig"/> default (300000) unless
        /// OSPREY_MAX_TRAIN_SIZE overrides it.
        /// </summary>
        internal static PercolatorConfig BuildProjectionPercolatorConfig(
            OspreyConfig config,
            OspreyFeatureInfo[] featureInfos,
            PercolatorDiagnosticsConfig diagnostics)
        {
            // Tree-only: this object is ignored on the SVM path. The chosen values are echoed to
            // the run log.
            var gbtParams = BuildGbtParams();

            return new PercolatorConfig
            {
                TrainFdr = OspreyEnvironment.FdrTrainFdr ?? config.RunFdr,
                TestFdr = config.RunFdr,
                // The SVM's 10 is unchanged. Trees converge far more slowly and were
                // still improving when they hit 10, so they get their own (higher) cap --
                // both loops still early-stop on convergence, so this only binds when the
                // model genuinely has further to go. See OspreyEnvironment.GbtMaxIterations.
                MaxIterations = config.FdrClassifier == FdrClassifier.Gbdt
                    ? OspreyEnvironment.GbtMaxIterations
                    : 10,
                NFolds = 3,
                FeatureInfos = featureInfos,
                // OSPREY_FDR_MODEL=gbdt: swap the linear SVM for gradient-boosted trees.
                // This is the ONLY knob that differs between the two methods -- the
                // dedup, fold assignment, semi-supervised iteration, competition, and
                // q-value/PEP math are the same shared code either way, which is what
                // makes them comparable at matched entrapment FDP.
                UseGradientBoostedTrees = config.FdrClassifier == FdrClassifier.Gbdt,
                GbtParams = gbtParams,
                // Percolator-3.0 training-subsample cap (mirrors the PercolatorConfig ctor
                // default); OSPREY_MAX_TRAIN_SIZE raises it to feed the model more rows.
                MaxTrainSize = OspreyEnvironment.MaxTrainSizeOverride ?? 300000,
                // OSPREY_GBT_PREFILTER_SIZE: two-round tree training on the streamed first pass
                // (0, the default, trains one round). The tree model only.
                PrefilterTrainSize = config.FdrClassifier == FdrClassifier.Gbdt
                    ? OspreyEnvironment.GbtPrefilterSize
                    : 0,
                // OSPREY_GBT_PREFILTER_Q: round 2 by a round-1 q cut, the size above as its cap.
                PrefilterTrainQ = config.FdrClassifier == FdrClassifier.Gbdt
                    ? OspreyEnvironment.GbtPrefilterQ ?? 0
                    : 0,
                // OSPREY_GBT_PREFILTER_LFDR: round 2 by a round-1 local-FDR cut instead.
                PrefilterTrainLocalFdr = config.FdrClassifier == FdrClassifier.Gbdt
                    ? OspreyEnvironment.GbtPrefilterLocalFdr ?? 0
                    : 0,
                // OSPREY_GBT_PREFILTER_SAMPLE: round 2 samples the eligible groups up to the cap.
                PrefilterTrainSample = config.FdrClassifier == FdrClassifier.Gbdt &&
                                       OspreyEnvironment.GbtPrefilterSample,
                // OSPREY_GBT_OOF: the trainer reports each training row's held-out fold, so the
                // score pass scores that row out of fold. The tree model only.
                ReportTrainEntryFolds = config.FdrClassifier == FdrClassifier.Gbdt &&
                                        OspreyEnvironment.GbtOutOfFold,
                // The most regularized C within this fraction of the best inner-CV count
                // (OSPREY_SVM_C_TOLERANCE, range-checked at startup; 0 is the strict maximum,
                // the pre-#4703 rule).
                CSelectionTolerance = OspreyEnvironment.SvmCSelectionTolerance,
                // Honors --threads; drives tree training and the tree score pass (see NThreads).
                NThreads = config.NThreads,
                // The training-time feature report's histograms, for --model-diagnostics
                // (#4377). The score passes collect theirs on every run: each run's share is
                // persisted in its first-pass sidecar (FeatureSums).
                CollectFeatureHistograms = config.ModelDiagnostics,
                // The training-set analysis (#4550, #4467) on every run: it is persisted with the
                // model, since the fold training sets it reads cannot be rebuilt later (doc 00, P16).
                CollectTrainingAnalysis = true,
                Diagnostics = diagnostics
            };
        }

        /// <summary>
        /// The tree hyper-parameters this process trains with: the validated
        /// <see cref="GbtParams"/> defaults with any OSPREY_GBT_* override applied, so a
        /// regularization / capacity sweep runs without a recompile per setting. Unset variables
        /// leave the default in place. The one source of them, shared by the training config and
        /// <see cref="GbdtValidityKeySuffix(OspreyConfig)"/>, so the key cannot describe a model
        /// other than the one trained.
        /// </summary>
        private static GbtParams BuildGbtParams()
        {
            var gbtParams = new GbtParams();
            if (OspreyEnvironment.FdrSeed.HasValue)
                gbtParams.Seed = OspreyEnvironment.FdrSeed.Value;
            if (OspreyEnvironment.GbtGamma.HasValue)
                gbtParams.Gamma = OspreyEnvironment.GbtGamma.Value;
            if (OspreyEnvironment.GbtRegLambda.HasValue)
                gbtParams.RegLambda = OspreyEnvironment.GbtRegLambda.Value;
            if (OspreyEnvironment.GbtRegAlpha.HasValue)
                gbtParams.RegAlpha = OspreyEnvironment.GbtRegAlpha.Value;
            if (OspreyEnvironment.GbtMaxDepth.HasValue)
                gbtParams.MaxDepth = OspreyEnvironment.GbtMaxDepth.Value;
            if (OspreyEnvironment.GbtNTrees.HasValue)
                gbtParams.NTrees = OspreyEnvironment.GbtNTrees.Value;
            if (OspreyEnvironment.GbtMinChildWeight.HasValue)
                gbtParams.MinChildWeight = OspreyEnvironment.GbtMinChildWeight.Value;
            if (OspreyEnvironment.GbtLearningRate.HasValue)
                gbtParams.LearningRate = OspreyEnvironment.GbtLearningRate.Value;
            if (OspreyEnvironment.GbtSubsample.HasValue)
                gbtParams.Subsample = OspreyEnvironment.GbtSubsample.Value;
            if (OspreyEnvironment.GbtColSample.HasValue)
                gbtParams.ColSample = OspreyEnvironment.GbtColSample.Value;
            return gbtParams;
        }

        /// <summary>
        /// Emit the two "[COUNT] ... Percolator input / features computed" lines for
        /// the streaming projection path directly from the projection rows (issue
        /// #4355 step (b) increment iii), reproducing the same per-file input counts
        /// without building a full-population <see cref="PercolatorEntry"/> list -- so
        /// the streaming path logs identical counts while never materializing that
        /// list. <c>nWithFeatures</c> counts rows with a real parquet row
        /// (<c>ParquetIndex != uint.MaxValue</c>) on the streaming build, matching the
        /// feature-resolver's fallback bookkeeping.
        /// </summary>
        private static void LogProjectionInputCounts(
            FdrProjectionSet projections, int numFeatures,
            Func<string, IReadOnlyList<double[]>> loadFileFeatures,
            IOspreyLog log, string passLabel)
        {
            bool streamFeatures = loadFileFeatures != null;
            int n = 0, nInputTargets = 0, nInputDecoys = 0;
            int nWithFeatures = 0, nWithoutFeatures = 0;
            foreach (var kvp in projections.PerFile)
            {
                foreach (var proj in kvp.Value)
                {
                    n++;
                    if (proj.IsDecoy)
                        nInputDecoys++;
                    else nInputTargets++;

                    if (streamFeatures)
                    {
                        if (proj.ParquetIndex != uint.MaxValue)
                            nWithFeatures++;
                        else
                            nWithoutFeatures++;
                    }
                    else nWithoutFeatures++;
                }
            }

            log.LogInfo(LogTag.COUNT, @"{0} Percolator input: {1} peaks ({2} targets, {3} decoys, {4} features)",
                passLabel, n, nInputTargets, nInputDecoys, numFeatures);
            log.LogInfo(LogTag.COUNT, @"{0} Percolator features computed: {1} peaks with PIN features, {2} fallback",
                passLabel, nWithFeatures, nWithoutFeatures);
        }

        /// <summary>
        /// Projection-native streaming first-pass Percolator (issue #4355 step (b)
        /// increment iii). It runs the four-phase streaming
        /// flow (best-per-precursor dedup -> peptide-grouped subsample -> train fold
        /// models + standardizer on the subset -> apply the averaged model to the full
        /// population and compute q-values) but WITHOUT ever materializing a
        /// full-population <see cref="PercolatorEntry"/> list or a
        /// <see cref="PercolatorResult"/> list:
        /// <list type="bullet">
        /// <item>the training-subset selection reads flat <c>labels</c> /
        /// <c>entryIds</c> / <c>peptides</c> / <c>bestScores</c> (= CoelutionSum)
        /// arrays built once from the projection -- <see cref="PercolatorEntry"/>
        /// objects are built ONLY for the &lt;= MaxTrainSize subset;</item>
        /// <item>the score + competition pass runs over the projection rows and writes
        /// the Score + five q-values straight back onto them via
        /// <see cref="PercolatorScorer.ScoreProjectionAndComputeFdrInPlace"/>, reusing the
        /// same flat identity arrays.</item>
        /// </list>
        /// Every parity-locked primitive (<see cref="PercolatorSampling.BuildTrainingSubset"/>,
        /// <see cref="PercolatorTrainer.RunPercolator"/> on the subset, and the shared
        /// competition/q-value math) is called UNCHANGED. Returns <c>true</c> on a
        /// diagnostic-only train abort (same contract as the <c>RunPercolatorFdr</c>
        /// projection overload).
        /// </summary>
        internal static bool RunStreamingIntoProjection(
            List<KeyValuePair<string, List<FdrProjection>>> perFile,
            string[] peptideById,
            PercolatorConfig percConfig,
            IOspreyLog log,
            string passLabel,
            Func<string, IReadOnlyList<double[]>> loadFileFeatures,
            Func<string, double[]> loadFileApexRts,
            IFdrOutputSink sink,
            Action<FeatureContributions> captureContributions = null,
            Action<PercolatorResults> captureModel = null)
        {
            if (loadFileFeatures == null)
                throw new InvalidOperationException(
                    @"RunStreamingIntoProjection requires a per-file feature " +
                    @"loader: the projection carries no resident feature vectors.");

            // Per-file start offsets in nested (file, row) order, so a global index
            // maps back to one projection row (used to build the subset entries below).
            int nFiles = perFile.Count;
            var fileStart = new int[nFiles + 1];
            int n = 0;
            for (int f = 0; f < nFiles; f++)
            {
                fileStart[f] = n;
                n += perFile[f].Value.Count;
            }
            fileStart[nFiles] = n;

            // Distinctive path marker (issue #4374): unambiguous proof this population
            // was ingested through the projection-native streaming path -- neither the
            // removed direct fork nor the resident FdrEntry path emits it. Under the
            // projection flag BOTH the 1st and 2nd pass must show this line, at every
            // scale (the direct-vs-streaming dispatch is gone).
            log.LogInfo(LogTag.PATH, @"{0} projection streaming ingest (RunStreamingIntoProjection): {1} rows",
                passLabel, n);
            // OSPREY_GBT_PREFILTER_SIZE is keyed for every task, but only the streamed first pass
            // (RunStreamingFirstPass) trains two rounds; say so where it is ignored.
            if (percConfig.UseGradientBoostedTrees && percConfig.PrefilterTrainSize > 0)
            {
                log.LogInfo(string.Format(CultureInfo.InvariantCulture,
                    @"Warning: OSPREY_GBT_PREFILTER_SIZE={0} is ignored by the {1} projection path, which trains one round.",
                    percConfig.PrefilterTrainSize, passLabel));
            }

            int maxTrain = percConfig.MaxTrainSize;

            // Flat identity + best-score arrays from the projection, built ONCE:
            // labels/entryIds/peptides drive the subset selection here (the score/compete
            // pass below reads the projection's own PerFile keys, so no flat fileNames array
            // is built -- issue #4355 Part B). bestScores (= CoelutionSum) ranks
            // best-per-precursor before any Score exists (risk #2), byte-identical to
            // Features[0] on the first pass.
            var labels = new bool[n];
            var entryIds = new uint[n];
            var peptides = new string[n];
            var bestScores = new double[n];
            int gi = 0;
            for (int f = 0; f < nFiles; f++)
            {
                var rows = perFile[f].Value;
                for (int r = 0; r < rows.Count; r++)
                {
                    var proj = rows[r];
                    labels[gi] = proj.IsDecoy;
                    entryIds[gi] = proj.EntryId;
                    peptides[gi] = peptideById[proj.PeptideId];
                    bestScores[gi] = proj.CoelutionSum;
                    gi++;
                }
            }

            // 1. best-per-precursor dedup + 2. peptide-grouped subsample. bestScores is
            //    supplied, so SelectBestPerPrecursor never dereferences the entries arg
            //    -- passing an empty list is exactly what lets this path avoid the
            //    full-N PercolatorEntry buffer the FdrEntry streaming path allocates.
            // Phase marker: the dedup + subsample over all N rows is a multi-minute
            // silent span on an 82-file join; announce it so the console is not blank.
            log.LogInfo(string.Format(OspreyFDRResources.PercolatorEngine_RunStreamingIntoProjection_Selecting_training_peaks_from__0__precursor_candidate_peaks___, n));
            int[] bestIdx;
            // fileStart is how this path supplies run identity: it hands an EMPTY entries list to
            // avoid the full-N PercolatorEntry buffer, so there are no FileName strings to read a
            // run off. Without it the selection would fall back to treating every row as its own
            // run and sample candidate PEAKS rather than runs.
            int[] trainSubsetGlobalIdx = PercolatorSampling.BuildTrainingSubset(
                labels, entryIds, peptides, Array.Empty<PercolatorEntry>(), maxTrain,
                percConfig.Seed, out bestIdx, bestScores, fileStart);

            int dedupTargets = 0, dedupDecoys = 0;
            for (int i = 0; i < bestIdx.Length; i++)
            {
                if (labels[bestIdx[i]]) dedupDecoys++;
                else dedupTargets++;
            }
            log.LogInfo(LogTag.COUNT, @"{0} Percolator streaming best-per-precursor: {1} precursors ({2} targets, {3} decoys) from {4} peaks",
                passLabel, bestIdx.Length, dedupTargets, dedupDecoys, n);

            int subTargets = 0, subDecoys = 0;
            for (int i = 0; i < trainSubsetGlobalIdx.Length; i++)
            {
                if (labels[trainSubsetGlobalIdx[i]]) subDecoys++;
                else subTargets++;
            }
            log.LogInfo(LogTag.COUNT, @"{0} Percolator streaming subsample: {1} precursors ({2} targets, {3} decoys)",
                passLabel, trainSubsetGlobalIdx.Length, subTargets, subDecoys);

            // 3. Build the subset PercolatorEntry list from the projection rows at the
            //    (ascending) subset indices, then load ONLY the subset's feature
            //    vectors one file at a time -- bounded by MaxTrainSize, so the full
            //    population's vectors are never resident.
            var subsetEntries = new List<PercolatorEntry>(trainSubsetGlobalIdx.Length);
            int fcur = 0;
            foreach (int si in trainSubsetGlobalIdx)
            {
                while (fcur + 1 < fileStart.Length && si >= fileStart[fcur + 1])
                    fcur++;
                var proj = perFile[fcur].Value[si - fileStart[fcur]];
                subsetEntries.Add(new PercolatorEntry
                {
                    FileName = perFile[fcur].Key,
                    Peptide = peptideById[proj.PeptideId],
                    Charge = proj.Charge,
                    IsDecoy = proj.IsDecoy,
                    EntryId = proj.EntryId,
                    ParquetIndex = proj.ParquetIndex,
                    CoelutionSum = proj.CoelutionSum,
                    Features = null
                });
            }

            var subsetByFile = PercolatorSampling.GroupIndicesByFileName(subsetEntries);
            LoadTrainingSubsetFeatures(subsetByFile, subsetEntries, loadFileFeatures, percConfig.FeatureInfos.Length);

            var trainConfig = percConfig.CloneForTrainOnly();
            PercolatorResults trainResults = PercolatorTrainer.RunPercolator(subsetEntries, trainConfig);

            if (trainResults.DiagnosticAbort)
                return true;

            // Publish the trained 1st-pass model (the frozen fold weights + biases +
            // standardizer) for the OSPREY_PASS2_QVALUE=transfer / transfer-compete / protein-
            // compact pass-2 steps, which re-score reconciled features with this FROZEN model
            // (transfer-compete / protein-compact then run a fresh target-decoy competition). A
            // no-op (null) on every default run, so this projection first pass stays byte-identical.
            captureModel?.Invoke(trainResults);

            Dictionary<int, int> heldOutFoldByRow = null;
            if (trainResults.TrainEntryFolds != null)
            {
                heldOutFoldByRow = new Dictionary<int, int>(trainSubsetGlobalIdx.Length);
                for (int k = 0; k < trainSubsetGlobalIdx.Length; k++)
                {
                    if (trainResults.TrainEntryFolds[k] >= 0)
                        heldOutFoldByRow[trainSubsetGlobalIdx[k]] = trainResults.TrainEntryFolds[k];
                }
                log.LogInfo(LogTag.COUNT, @"{0} out-of-fold tree scoring for {1} training rows",
                    passLabel, heldOutFoldByRow.Count);
            }

            // Release the subset-only working sets before the score pass so only the
            // flat per-observation arrays + the projection remain resident across the
            // peak (bestIdx can be O(N/2) on a 1:1 target/decoy population).
            bestScores = null;
            bestIdx = null;
            trainSubsetGlobalIdx = null;
            subsetEntries = null;
            subsetByFile = null;

            // 4. Score ALL rows + run the competition/q-values, writing the Score
            //    straight onto the projection rows and streaming the q-value outputs
            //    to the sink (no PercolatorResult list). Reuses the flat identity
            //    arrays already built above.
            // OSPREY_EXPERIMENT_AGG applies to the FIRST pass only. The 2nd pass re-runs this same
            // projection scorer over the post-reconciliation survivor pool, where the aggregation's
            // premises no longer hold (appended gap-fill rows inflate a group's run count with
            // fabricated detections; the decoy floor would come from the compaction-depleted
            // survivor decoys rather than the full null). Before this gate the shared QMap
            // primitives re-aggregated there silently.
            PercolatorScorer.ScoreProjectionAndComputeFdrInPlace(
                perFile, labels, entryIds, peptides, trainResults, percConfig,
                loadFileFeatures, loadFileApexRts, sink, captureContributions,
                applyExperimentAgg: passLabel == FIRST_PASS_LABEL,
                heldOutFoldByRow: heldOutFoldByRow);
            return false;
        }

        /// <summary>
        /// Load the training subset's feature vectors one file at a time, under per-file progress:
        /// this step ran ~5 min silent before cross-validation. Console-only, never touches the
        /// loaded features, so training is byte-identical. A method of its own so the progress
        /// body captures these parameters rather than the caller's locals, which the caller
        /// drops before its score pass to release them.
        /// </summary>
        private static void LoadTrainingSubsetFeatures(Dictionary<string, List<int>> subsetByFile,
            List<PercolatorEntry> subsetEntries, Func<string, IReadOnlyList<double[]>> loadFileFeatures, int nFeatures)
        {
            ProgressReporter.Run(
                CountText.Format(subsetByFile.Count,
                    OspreyFDRResources.PercolatorEngine_RunStreamingIntoProjection_Loading_Percolator_training_features_from_1_file,
                    OspreyFDRResources.PercolatorEngine_RunStreamingIntoProjection_Loading_Percolator_training_features_from__0__files),
                subsetByFile.Count, loadProgress =>
            {
                int loadDone = 0;
                foreach (var kvp in subsetByFile)
                {
                    loadProgress.Report(++loadDone);
                    IReadOnlyList<double[]> rows = loadFileFeatures(kvp.Key);
                    foreach (int k in kvp.Value)
                    {
                        var entry = subsetEntries[k];
                        entry.Features = (double[])PercolatorScorer.ResolveFeatureRow(
                            rows, entry.ParquetIndex, entry.CoelutionSum, nFeatures).Clone();
                    }
                }
            });
        }

        /// <summary>
        /// Fold ONE file's entries into the experiment-q floors - the min run q per entry_id and
        /// per (peptide, isDecoy). Both maps are O(distinct), so a caller can walk the run one
        /// file at a time and drop each as it goes; nothing here needs a whole-run view.
        ///
        /// <para>The floors, applied by <see cref="ApplyExperimentQFloors"/>, are the
        /// experiment-q clamp.</para>
        ///
        /// They raise each entry's experiment-level q-value up to its own best (min) run-level
        /// q-value, enforcing that a best-of-runs aggregate can never be more confident than
        /// its best single run. Experiment-level FDR competes each precursor's single best
        /// observation against a de-duplicated (thinner) decoy null, so the raw experiment q
        /// can fall BELOW every per-run q -- letting a precursor pass experiment-level FDR with
        /// no run passing run-level FDR. Downstream that produces reported peptides with no
        /// run-level ID (the blib ID-line artifact Mike observed) and an anti-conservative
        /// experiment-wide FDP calibration.
        ///
        /// The run floor is the entry's best (min) <em>combined</em> run q-value
        /// (<see cref="FdrLevel.Both"/> = max(precursor, peptide)), matching the blib ID line
        /// and <c>RetentionTimes.score</c> (both <c>EffectiveRunQvalue(Both)</c>): so
        /// "reported =&gt; some run passes at BOTH the precursor and peptide granularity",
        /// which is the exact invariant a Skyline ID line represents. Both floors key on the
        /// target/decoy-specific identity (never the shared base_id / bare sequence -- a target
        /// must not inherit its paired decoy's good run):
        ///   ExperimentPrecursorQvalue &lt;- max(ExperimentPrecursorQvalue, min-over-runs runBoth)   [by EntryId]
        ///   ExperimentPeptideQvalue   &lt;- max(ExperimentPeptideQvalue,   min-over-runs runBoth)   [by (ModifiedSequence, IsDecoy)]
        /// Run-level q is winner-only per file, so a losing run contributes 1.0 and the min
        /// naturally picks the entry's best genuine run.
        ///
        /// The floor is run-Both at BOTH experiment levels even when <c>--fdr-level</c> is
        /// <see cref="FdrLevel.Precursor"/> (the default): the reported gate and the blib ID
        /// line are always Both, so flooring by run-Both is what makes "reported =&gt; a run
        /// has an ID line" hold. A precursor can therefore be raised out of the reported set
        /// because no run cleared run-<em>peptide</em> FDR, even under precursor-level control
        /// -- intended, matching blib fidelity rather than the run-precursor gate alone.
        ///
        /// <para>The in-pass clamp (first/second-pass Percolator) runs in the memory-bounded
        /// FLAT form, <see cref="PercolatorQValues.ClampExperimentQToBestRunFlat"/>, over the
        /// score-pass scalar arrays; this per-file pair serves the post-Stage-6 pre-blib
        /// re-clamp. Both produce identical floors (same min/max over the same values).</para>
        /// </summary>
        public static void AccumulateExperimentQFloors(
            IReadOnlyList<FdrEntry> entries,
            Dictionary<uint, double> minRunBothByEntryId,
            Dictionary<(string ModifiedSequence, bool IsDecoy), double> minRunBothByPeptide)
        {
            {
                foreach (var e in entries)
                {
                    double runBoth = e.EffectiveRunQvalue(FdrLevel.Both);
                    double curPrec;
                    if (!minRunBothByEntryId.TryGetValue(e.EntryId, out curPrec) || runBoth < curPrec)
                        minRunBothByEntryId[e.EntryId] = runBoth;

                    // Treat a null/empty ModifiedSequence as missing (a Parquet stub loaded
                    // without the modified_sequence column can be string.Empty): it has no
                    // peptide identity, so it must not bucket unrelated entries under an empty key.
                    if (string.IsNullOrEmpty(e.ModifiedSequence))
                        continue;
                    // Peptide identity is (ModifiedSequence, IsDecoy): a decoy can share its
                    // paired target's ModifiedSequence, so keying on the sequence alone would
                    // let a decoy's good run lower the target's peptide floor (anti-conservative).
                    var pkey = (e.ModifiedSequence, e.IsDecoy);
                    double curPept;
                    if (!minRunBothByPeptide.TryGetValue(pkey, out curPept) || runBoth < curPept)
                        minRunBothByPeptide[pkey] = runBoth;
                }
            }
        }

        /// <summary>
        /// Raise ONE file's experiment q-values to the floors folded by
        /// <see cref="AccumulateExperimentQFloors"/>. The apply half of the same operation,
        /// separated so a streamed consumer can fold over every file first and then apply as it
        /// revisits them - the floors are whole-run, the application is per row.
        /// </summary>
        /// <summary>
        /// Returns how many q-values this call actually RAISED. Zero is the interesting answer:
        /// the score pass applies the same floor before it writes (see the clamp in
        /// <c>PercolatorScorer.ScoreProjectionAndComputeFdrInPlace</c>), so a pool rebuilt from
        /// those persisted values should already satisfy the invariant and a re-apply should
        /// change nothing. A caller that folds every run purely to re-derive floors that raise
        /// no value is doing a whole traversal for a no-op, and the count is what tells it so
        /// rather than leaving the question to argument.
        /// </summary>
        public static int ApplyExperimentQFloors(
            IReadOnlyList<FdrEntry> entries,
            IReadOnlyDictionary<uint, double> minRunBothByEntryId,
            IReadOnlyDictionary<(string ModifiedSequence, bool IsDecoy), double> minRunBothByPeptide)
        {
            int raised = 0;
            {
                foreach (var e in entries)
                {
                    double floorPrec;
                    if (minRunBothByEntryId.TryGetValue(e.EntryId, out floorPrec) &&
                        floorPrec > e.ExperimentPrecursorQvalue)
                    {
                        e.ExperimentPrecursorQvalue = floorPrec;
                        raised++;
                    }

                    if (!string.IsNullOrEmpty(e.ModifiedSequence))
                    {
                        double floorPept;
                        if (minRunBothByPeptide.TryGetValue((e.ModifiedSequence, e.IsDecoy), out floorPept) &&
                            floorPept > e.ExperimentPeptideQvalue)
                        {
                            e.ExperimentPeptideQvalue = floorPept;
                            raised++;
                        }
                    }
                }
            }
            return raised;
        }

        /// <summary>
        /// One best-per-precursor winner captured while streaming identity in flat (file,row)
        /// order (issue #4355 struct-shrink S3, Stage B): enough to reproduce
        /// <see cref="PercolatorSampling.SelectBestPerPrecursor"/>'s output AND build the training-subset
        /// <see cref="PercolatorEntry"/> without a resident projection. <see cref="G"/> is the
        /// row's global (file,row) ordinal -- sorting the captured winners by it ascending
        /// reproduces <c>SelectBestPerPrecursor</c>'s <c>Array.Sort(globalIndex)</c> exactly.
        /// </summary>
        private readonly struct FirstPassDedupRow
        {
            public readonly int G;
            public readonly string FileName;
            public readonly uint EntryId;
            public readonly byte Charge;
            public readonly bool IsDecoy;
            public readonly uint ParquetIndex;
            public readonly double CoelutionSum;
            public readonly string Peptide;
            public FirstPassDedupRow(int g, string fileName, uint entryId, byte charge, bool isDecoy,
                uint parquetIndex, double coelutionSum, string peptide)
            {
                G = g;
                FileName = fileName;
                EntryId = entryId;
                Charge = charge;
                IsDecoy = isDecoy;
                ParquetIndex = parquetIndex;
                CoelutionSum = coelutionSum;
                Peptide = peptide;
            }
        }

        /// <summary>
        /// A file's already-computed scores, in parquet row order, or null when the caller has
        /// nothing on disk for it. The two run q-values pass 1 stored with each score come back
        /// through <paramref name="runPrecursorQvalues"/> and <paramref name="runPeptideQvalues"/>
        /// in the same row order, and are null exactly when the return is.
        ///
        /// <para>Taking the q-values from disk rather than recomputing them is byte-identical,
        /// not a second opinion: whichever path wrote the sidecar produced all three together
        /// from these same scores (see <see cref="CompletedScoreStreamer"/>), and nothing revises
        /// them afterwards. The count and entry_id checks below are what bind them to this file's
        /// rows. The recompute is a sort per file, and the reason it
        /// reads as free is that it is normally weighed against loading the file's feature
        /// vectors and re-running the dot product. On this path neither happens - that is what
        /// having the scores on disk means - so the sort is measured against nothing and becomes
        /// the pass's dominant cost.</para>
        ///
        /// <para>A caller that must WRITE the q-values still has to compute them; the sort is
        /// what produces them. Only a pure reader can take them from here.</para>
        /// </summary>
        private static double[] TryLoadCompletedScores(
            CompletedScoreStreamer tryStream, string fileName, int expectedCount,
            IReadOnlyList<uint> expectedEntryIds, out double[] runPrecursorQvalues,
            out double[] runPeptideQvalues)
        {
            runPrecursorQvalues = null;
            runPeptideQvalues = null;
            if (tryStream == null)
                return null;
            var read = new CompletedScoreRecords(expectedCount);
            if (!tryStream(fileName, read.Add))
                return null;
            // A count mismatch means the sidecar and the parquet disagree about how many rows
            // this file has, which no validity key can catch - so refuse the shortcut and score
            // it rather than emit a silently misaligned file.
            if (read.Count != expectedCount)
                return null;
            // A zero-row file the streamer accepted never called back. It is still a file whose
            // (empty) output is on disk, and returning null would have the caller score it and
            // write its attested sidecar a second time.
            double[] scores = read.Scores ?? Array.Empty<double>();
            uint[] entryIds = read.EntryIds ?? Array.Empty<uint>();
            double[] runPrec = read.RunPrecursorQvalues ?? Array.Empty<double>();
            double[] runPept = read.RunPeptideQvalues ?? Array.Empty<double>();
            // And the ROWS must line up, not just the count. This binds the sidecar's records
            // to parquet rows by POSITION, while every other reader of the file matches by
            // entry_id - so a sidecar that is complete but ordered differently (the resident
            // write path sorts by FdrEntry, not by parquet row) would hand every row its
            // neighbour's score, and the run would finish clean with wrong identifications.
            // The identity is already in hand at both call sites, so checking is free.
            for (int r = 0; r < expectedCount; r++)
            {
                if (entryIds[r] != expectedEntryIds[r])
                    return null;
            }
            // Published only now that count and row identity have both checked out, so a
            // rejected sidecar hands back three nulls together rather than q-values a caller
            // could pair with scores it was told not to use.
            runPrecursorQvalues = runPrec;
            runPeptideQvalues = runPept;
            return scores;
        }

        /// <summary>
        /// Collects a file's records from a <see cref="CompletedScoreStreamer"/> in arrival order.
        /// Sized exactly, because the row count is known before the first record arrives, and
        /// allocated only when that record does. A streamer with nothing on disk for the file
        /// refuses without calling back - on a cold run that is every file, in both passes - and
        /// allocating and zeroing four row-sized arrays (28 bytes a row, ~120 MB for a cohort
        /// file) only to drop them would put that cost back on exactly the run with no sidecars
        /// to read. The arrays stay null until the first record.
        /// </summary>
        private sealed class CompletedScoreRecords
        {
            private readonly int _expectedCount;
            private double[] _scores;
            private uint[] _entryIds;
            private double[] _runPrec;
            private double[] _runPept;

            public CompletedScoreRecords(int expectedCount)
            {
                _expectedCount = expectedCount;
            }

            /// <summary>Records received, including any past <c>expectedCount</c>.</summary>
            public int Count { get; private set; }

            public double[] Scores => _scores;
            public uint[] EntryIds => _entryIds;
            public double[] RunPrecursorQvalues => _runPrec;
            public double[] RunPeptideQvalues => _runPept;

            public void Add(uint entryId, double score, double runPrecQ, double runPeptQ)
            {
                if (_scores == null)
                {
                    _scores = new double[_expectedCount];
                    _entryIds = new uint[_expectedCount];
                    _runPrec = new double[_expectedCount];
                    _runPept = new double[_expectedCount];
                }
                // A sidecar holding MORE records than the parquet has rows would run off the
                // end of these arrays. Keep counting past it and let the caller's length check
                // refuse the file, without an exception on the way.
                if (Count < _expectedCount)
                {
                    _entryIds[Count] = entryId;
                    _scores[Count] = score;
                    _runPrec[Count] = runPrecQ;
                    _runPept[Count] = runPeptQ;
                }
                Count++;
            }
        }

        /// <summary>
        /// 1st-pass-ONLY streaming Percolator that holds NO resident row buffer at all (issue
        /// #4355 struct-shrink S3, Stage B -- the FLAT-memory win): the memory-collapsing fork of
        /// <see cref="RunStreamingIntoProjection"/> +
        /// <see cref="PercolatorScorer.ScoreProjectionAndComputeFdrInPlace"/>. Where those hold the resident
        /// <see cref="FdrProjection"/>[] + the flat <c>labels/entryIds/peptides/finalScores[n]</c>
        /// arrays (O(pre-compaction rows), the 82->500 file blocker), this streams every row's
        /// identity + features straight from parquet THREE times -- once to select the training
        /// subset, once to score + build the bounded q-value maps + clamp floors, once to score +
        /// emit, recomputing the score per row instead of parking an O(n) score array. Only
        /// the SUBSET (&lt;= MaxTrainSize) and the intrinsically-bounded lookups (O(base_ids) /
        /// O(peptides), via <see cref="StreamingFdr.StreamingFirstPassQ"/> + per-file run-q) are ever resident,
        /// so the peak is FLAT in file count.
        ///
        /// Byte-identical to the resident projection path on the same rows in the same (file,row)
        /// order, for either classifier (verified by <c>FdrTest.TestStreamingFirstPassMatchesProjection</c>
        /// for the linear SVM and <c>FdrTest.TestStreamingFirstPassTrainsGbdt</c> for gradient-boosted
        /// trees): the training-subset selection reproduces <see cref="PercolatorSampling.SelectBestPerPrecursor"/> +
        /// <see cref="PercolatorSampling.BuildTrainingSubset"/> (strict-<c>&gt;</c> first-seen dedup ranked on
        /// CoelutionSum, ascending-global-ordinal order, identical
        /// <see cref="PercolatorSampling.SubsampleByPeptideGroup"/>), the training runs the SAME
        /// <see cref="PercolatorTrainer.RunPercolator"/> with the SAME train-only config
        /// (<see cref="PercolatorConfig.CloneForTrainOnly"/>) on the SAME subset, the trained model
        /// scores each row through the same per-classifier code, and the q-value math reuses the
        /// SAME primitives (<see cref="StreamingFdr.StreamingFirstPassQ"/>, <see cref="PercolatorQValues.ComputePerFileRunQvalues"/>,
        /// <see cref="PercolatorQValues.UpdateExperimentQClampFloor"/>). This is 1st-pass-only: the 2nd pass keeps its
        /// O(survivors) resident projection (Stage 7/8 needs it) via the unchanged
        /// <see cref="PercolatorScorer.ScoreProjectionAndComputeFdrInPlace"/>.
        ///
        /// The caller supplies the row source as delegates (kept free of an Osprey.IO dependency):
        /// <paramref name="fileNames"/> in the join's file order; <paramref name="streamFileRows"/>
        /// invokes its callback once per parquet row of a file in row order (== the resident sort's
        /// order on the 1st pass, since the parquet is written (entry_id,charge,scan)-sorted);
        /// <paramref name="loadFileFeatures"/> loads one file's feature vectors, indexed by the
        /// running parquet row ordinal. Returns <c>true</c> on a diagnostic-only train abort.
        /// </summary>
        internal static bool RunStreamingFirstPass(
            IReadOnlyList<string> fileNames,
            Action<string, StubColumns, Action<uint, byte, bool, double, string, double>> streamFileRows,
            Func<string, IReadOnlyList<double[]>> loadFileFeatures,
            PercolatorConfig percConfig,
            IOspreyLog log,
            string passLabel,
            IFdrFirstPassSink sink,
            Action<FeatureContributions> captureContributions = null,
            Action<PercolatorResults> captureModel = null,
            CompletedScoreStreamer tryStreamCompletedScores = null,
            PercolatorResults pretrainedModel = null,
            FileRunScopeSink flushFileRunScope = null,
            int fileLanes = 1,
            Func<string, FeatureSums> tryLoadFeatureSums = null)
        {
            if (streamFileRows == null)
                throw new ArgumentNullException(nameof(streamFileRows));
            if (loadFileFeatures == null)
                throw new ArgumentNullException(nameof(loadFileFeatures));
            if (sink == null)
                throw new ArgumentNullException(nameof(sink));
            // A persisted model of the OTHER classifier is refused - neither adopted, which would
            // score this run with the wrong classifier, nor retrained around, which would leave
            // any file already scored off the persisted model on a different discriminant from
            // the rest. The validity key records the FDR method, so a current model can only
            // mismatch through a defect; this is the backstop. Checked before the Pass 0 ingest,
            // which streams every file of the join and cannot change the answer.
            if (pretrainedModel != null &&
                pretrainedModel.IsGradientBoostedTrees != percConfig.UseGradientBoostedTrees)
            {
                throw new InvalidOperationException(ClassifierMismatchMessage(
                    pretrainedModel.IsGradientBoostedTrees, percConfig.UseGradientBoostedTrees));
            }

            // The column reaches a model only through round 2, and startup refuses the settings that
            // have none; nothing else may quietly train without the column it was asked for.
            if (percConfig.ProteinBest != null &&
                (!percConfig.UseGradientBoostedTrees || percConfig.PrefilterTrainSize <= 0))
            {
                throw new InvalidOperationException(
                    @"The protein-best column needs the two-round tree training.");
            }

            int nFiles = fileNames.Count;
            int nFeatures = percConfig.FeatureInfos.Length;
            int maxTrain = percConfig.MaxTrainSize;
            // Each pass below walks the files through OrderedFileLanes: the file-local work - the
            // parquet decode, the feature load, the dot products, the per-file sort - runs on up to
            // fileLanes threads, and everything that crosses files (the training dedup, the
            // competition, the contribution sums, the sink) is applied on this thread in file
            // order, as the plain loop applied it. A file's raw rows live in that file's own
            // RowBuffer, so a bounded number of files (twice the lane count) is resident at once
            // rather than one; with one lane it is the plain loop exactly.

            // ---- Pass 0: stream identity, build the training subset, train the model ----
            // Best-per-precursor dedup captured WITH identity, in flat (file,row) order. Strict
            // '>' on CoelutionSum with first-seen (lowest g) winning ties -- exactly
            // SelectBestPerPrecursor iterating ascending global index. bestScores == CoelutionSum
            // (byte-identical to Features[0] on the 1st pass), so no feature load is needed here.
            var bestTarget = new Dictionary<uint, FirstPassDedupRow>();
            var bestDecoy = new Dictionary<uint, FirstPassDedupRow>();
            // Run bookkeeping for the reservoir. Only written when it is on; an empty dictionary
            // allocates no buckets, so the off arm keeps the memory it has always had.
            var runPick = new Dictionary<uint, PercolatorSampling.RunPickState>();
            // Which observation represents a precursor. Logged when it is NOT the default,
            // because nothing else in the output would say which population trained the model.
            // Plain prose in the default log, not a gated tag: the user set the variable, and it
            // changes the results.
            bool pickRun = OspreyEnvironment.TrainPickRun;
            if (!pickRun)
            {
                log.LogInfo(@"OSPREY_TRAIN_PICK_RUN=0 is set: each precursor is trained on its best " +
                            @"observation across runs, not a uniform sample of them (the behavior before 26.1).");
            }
            int g = 0;
            int nInputTargets = 0, nInputDecoys = 0;
            // The decode is this pass's cost and is file-local, so the lanes read files ahead. The
            // dedup is order-dependent - a tie goes to the first-seen ordinal, and the reservoir
            // draws per run in arrival order - so it stays on this thread, in file order.
            // Each file's row count, recorded here so the later passes can size a file's buffers
            // exactly instead of growing them.
            var rowCounts = new int[nFiles];
            void DedupFile(int f, RowBuffer buffer, ProgressReporter ingestProgress)
            {
                string file = fileNames[f];
                int count = buffer.Count;
                rowCounts[f] = count;
                for (int r = 0; r < count; r++)
                {
                    uint entryId = buffer.EntryIds[r];
                    bool isDecoy = buffer.IsDecoys[r];
                    double coelutionSum = buffer.CoelutionSums[r];
                    uint baseId = entryId & PercolatorEntry.BASE_ID_MASK;
                    var map = isDecoy ? bestDecoy : bestTarget;
                    if (map.TryGetValue(baseId, out FirstPassDedupRow existing))
                    {
                        bool replace;
                        if (pickRun)
                        {
                            // Reservoir of size one over RUNS - not over rows. Pass 1 is
                            // PRE-COMPACTION, so a precursor carries several candidate-peak rows
                            // within one file; drawing per row would both weight a run by how many
                            // candidates it produced AND leave a RANDOM candidate as the training
                            // row instead of that run's best peak.
                            uint seenKey = baseId | (isDecoy ? 0x80000000u : 0u);
                            PercolatorSampling.RunPickState state = runPick[seenKey];
                            if (f == state.LastRun)
                            {
                                // Another candidate peak from the run already drawn for: resolve it
                                // by score, so the surviving row is this run's BEST peak.
                                replace = state.HolderIsCurrentRun && coelutionSum > existing.CoelutionSum;
                            }
                            else
                            {
                                uint runs = state.RunsSeen + 1u;
                                // The DRAW takes the decoy bit too. BASE_ID_MASK clears the high
                                // bit, so drawing on the masked base_id would make a target and its
                                // paired decoy decide identically at every k and land on the same
                                // run for essentially every precursor.
                                replace = PercolatorScorer.ReservoirTakesSlot(seenKey, runs, percConfig.Seed);
                                state.RunsSeen = runs;
                                state.LastRun = f;
                                state.HolderIsCurrentRun = replace;
                                runPick[seenKey] = state;
                            }
                        }
                        else
                        {
                            replace = coelutionSum > existing.CoelutionSum;
                        }
                        if (replace)
                        {
                            map[baseId] = new FirstPassDedupRow(
                                g, file, entryId, buffer.Charges[r], isDecoy, (uint)r, coelutionSum,
                                buffer.Peptides[r]);
                        }
                    }
                    else
                    {
                        map[baseId] = new FirstPassDedupRow(
                            g, file, entryId, buffer.Charges[r], isDecoy, (uint)r, coelutionSum, buffer.Peptides[r]);
                        if (pickRun)
                        {
                            uint seenKey = baseId | (isDecoy ? 0x80000000u : 0u);
                            runPick[seenKey] = new PercolatorSampling.RunPickState
                            {
                                RunsSeen = 1u, LastRun = f, HolderIsCurrentRun = true
                            };
                        }
                    }
                    if (isDecoy) nInputDecoys++; else nInputTargets++;
                    g++;
                }
                ingestProgress.Report(f + 1);
            }
            // This pass streams every file's parquet rows before the [PATH] line below, so it is a
            // determinate O(files) I/O step (43s at 82 files, minutes at 500). Report per-file
            // progress through the standard throttled reporter so a large join never goes silent.
            ProgressReporter.Run(
                CountText.Format(nFiles, OspreyFDRResources.PercolatorScorer_RunStreamingFirstPass_Reading_precursor_candidate_peaks_for_Percolator_from_1_file,
                    OspreyFDRResources.PercolatorScorer_RunStreamingFirstPass_Reading_precursor_candidate_peaks_for_Percolator_from__0__files), nFiles,
                ingestProgress => OrderedFileLanes.Run(nFiles, fileLanes,
                    // Core only: this pass reduces to a best-per-precursor training subset and never
                    // looks at RowBuffer.ApexRts, so decoding apex_rt here is pure waste.
                    f => ReadFileRows(streamFileRows, fileNames[f], StubColumns.Core),
                    (f, buffer) => DedupFile(f, buffer, ingestProgress)),
                intervalSeconds: ProgressReporter.IO_INTERVAL_SECONDS);
            int n = g;
            log.LogInfo(LogTag.PATH, @"{0} streaming ingest (RunStreamingFirstPass): {1} rows", passLabel, n);
            LogBlockReads(log, passLabel, @"pass 0");
            log.LogInfo(LogTag.COUNT, @"{0} Percolator input: {1} peaks ({2} targets, {3} decoys, {4} features)",
                passLabel, n, nInputTargets, nInputDecoys, nFeatures);

            // Dedup rows in ascending global ordinal == SelectBestPerPrecursor's Array.Sort of the
            // winning global indices (each base_id's best is one unique row, so g never ties).
            var dedup = new List<FirstPassDedupRow>(bestTarget.Count + bestDecoy.Count);
            dedup.AddRange(bestTarget.Values);
            dedup.AddRange(bestDecoy.Values);
            dedup.Sort((a, b) => a.G.CompareTo(b.G)); // Array.Sort OK: G is the unique global row ordinal of each base_id's best row, so the comparator never ties -- reproduces SelectBestPerPrecursor's Array.Sort(result).
            int m = dedup.Count;
            int dedupTargets = 0;
            foreach (var d in dedup)
                if (!d.IsDecoy) dedupTargets++;
            log.LogInfo(LogTag.COUNT, @"{0} Percolator streaming best-per-precursor: {1} precursors ({2} targets, {3} decoys) from {4} peaks",
                passLabel, m, dedupTargets, m - dedupTargets, n);

            // Peptide-grouped subsample when the dedup count exceeds MaxTrainSize (mirrors
            // BuildTrainingSubset: SelectBestPerPrecursor already ran above via the streaming dedup,
            // so only the SubsampleByPeptideGroup step remains). localSelected indexes `dedup`.
            int[] localSelected;
            if (maxTrain <= 0 || m <= maxTrain)
            {
                localSelected = new int[m];
                for (int i = 0; i < m; i++)
                    localSelected[i] = i;
            }
            else
            {
                var dedupLabels = new bool[m];
                var dedupEntryIds = new uint[m];
                var dedupPeptides = new string[m];
                for (int i = 0; i < m; i++)
                {
                    dedupLabels[i] = dedup[i].IsDecoy;
                    dedupEntryIds[i] = dedup[i].EntryId;
                    dedupPeptides[i] = dedup[i].Peptide;
                }
                localSelected = PercolatorSampling.SubsampleByPeptideGroup(
                    dedupLabels, dedupEntryIds, dedupPeptides, maxTrain, percConfig.Seed);
            }

            // A persisted model is only usable if it was trained on THIS run's feature set, and
            // nothing upstream can establish that: the validity key the caller checks carries no
            // feature-set or build term, so a model from a build with a different feature list
            // matches it exactly. Adopting one would either index past the end of its weight
            // vector mid-score-pass or, when it is wider, silently truncate it and apply the
            // standardizer at the wrong width - scores that look plausible and are wrong.
            //
            // Checked HERE, above the training-entry build and the subset load, because both
            // depend on whether a model is trained: rejecting it after them would leave the
            // subset unloaded and then train on entries with no features, or train a single round
            // where the two-round training was asked for. (The classifier, which needs no ingest
            // to judge, is checked on entry.)
            //
            // The protein-best column comes first: scoring a file needs the column, the column
            // needs round 1's models, and only round 2's model is persisted. Whatever its width, a
            // saved model cannot score here; retraining is deterministic, so it reproduces the
            // model the saved scores came from.
            if (percConfig.ProteinBest != null && pretrainedModel != null)
            {
                log.LogInfo(@"OSPREY_PROTEIN_BEST is set: retraining the first-pass model, because " +
                            @"the column needs round 1's models, which are not saved.");
                pretrainedModel = null;
            }
            if (pretrainedModel != null)
            {
                var pretrainedTrees = PercolatorScorer.ResolveGbtModels(pretrainedModel);
                int modelFeatures = pretrainedTrees != null
                    ? pretrainedTrees[0].FeatureCount
                    : pretrainedModel.FoldWeights != null && pretrainedModel.FoldWeights.Count > 0
                        ? pretrainedModel.FoldWeights[0].Length
                        : -1;
                if (modelFeatures != nFeatures ||
                    pretrainedModel.Standardizer == null ||
                    pretrainedModel.Standardizer.NumFeatures != nFeatures)
                {
                    log.LogInfo(string.Format(
                        OspreyFDRResources.PercolatorScorer_RunStreamingFirstPass_Ignoring_the_saved_first_pass_model__it_has__0__features_and_this_run_scores__1___, modelFeatures, nFeatures));
                    pretrainedModel = null;
                }
                else if (percConfig.ReportTrainEntryFolds && percConfig.UseGradientBoostedTrees &&
                         (pretrainedModel.TrainEntryFolds == null || percConfig.PrefilterTrainSize > 0))
                {
                    // OSPREY_GBT_OOF needs each training row's held-out fold. A saved model does
                    // not carry them (they are not persisted), and under the two-round training
                    // they index round 2's rows, which this pass cannot rebuild without training.
                    // Adopted, the model would score the training rows of the files left to score
                    // with the fold mean. Retraining is deterministic, so it reproduces the model
                    // the saved scores came from, folds included.
                    log.LogInfo(@"OSPREY_GBT_OOF=1 is set: retraining the first-pass model, because " +
                                @"the saved one cannot supply its training rows' held-out folds.");
                    pretrainedModel = null;
                }
            }

            // OSPREY_GBT_PREFILTER_SIZE: two-round tree training scores EVERY deduped candidate with
            // the round-1 models, so every candidate becomes an entry and has its features loaded;
            // the round-1 subset is the same entry objects. Off (the default), only the subset is
            // built and loaded, as always. A resume trains nothing, so it never needs the candidates.
            bool prefilter = percConfig.UseGradientBoostedTrees && percConfig.PrefilterTrainSize > 0 &&
                             pretrainedModel == null;
            List<PercolatorEntry> candidateEntries = null;
            if (prefilter)
            {
                candidateEntries = new List<PercolatorEntry>(m);
                foreach (var d in dedup)
                    candidateEntries.Add(NewStreamedEntry(d));
                log.LogInfo(LogTag.COUNT, @"{0} Percolator prefilter candidates: loading features for all {1} best-per-precursor precursors",
                    passLabel, candidateEntries.Count);
            }
            int subTargets = 0;
            var subsetEntries = new List<PercolatorEntry>(localSelected.Length);
            foreach (int li in localSelected)
            {
                var d = dedup[li];
                if (!d.IsDecoy) subTargets++;
                subsetEntries.Add(candidateEntries != null ? candidateEntries[li] : NewStreamedEntry(d));
            }
            log.LogInfo(LogTag.COUNT, @"{0} Percolator streaming subsample: {1} precursors ({2} targets, {3} decoys)",
                passLabel, subsetEntries.Count, subTargets, subsetEntries.Count - subTargets);

            // Load the training entries' feature vectors, one file at a time, cloning each row so
            // the entry owns it -- mirrors RunStreamingIntoProjection. By default that is ONLY the
            // subset, bounded by MaxTrainSize. With the two-round training it is every deduped
            // candidate, so the resident features grow from MaxTrainSize rows to the candidate
            // count (about candidates x features doubles).
            var loadEntries = candidateEntries ?? subsetEntries;
            var subsetByFile = PercolatorSampling.GroupIndicesByFileName(loadEntries);
            // Both skipped when the caller supplied the model this pass would have trained.
            // Training exists to score entries; when every entry's score is already on disk
            // there is nothing for a model to do, and loading the training subset's feature
            // vectors is the single most expensive thing left - 21 minutes at 446 files, spent
            // to reproduce a model that was already persisted as the analysis's .1st-pass.model.json.
            if (pretrainedModel == null)
            {
                // Each file fills only its own subset entries, so the files need no ordering. The
                // lanes read a copy of the list reference, because the method drops its own below.
                var subsetFiles = new List<KeyValuePair<string, List<int>>>(subsetByFile);
                var laneEntries = loadEntries;
                int subsetFilesLoaded = 0;
                ProgressReporter.Run(CountText.Format(subsetFiles.Count,
                           OspreyFDRResources.PercolatorScorer_RunStreamingFirstPass_Loading_Percolator_training_features_from_1_file,
                           OspreyFDRResources.PercolatorScorer_RunStreamingFirstPass_Loading_Percolator_training_features_from__0__files), subsetFiles.Count, loadProgress =>
                {
                    OrderedFileLanes.For(subsetFiles.Count, fileLanes, i =>
                    {
                        IReadOnlyList<double[]> rows = loadFileFeatures(subsetFiles[i].Key);
                        foreach (int k in subsetFiles[i].Value)
                        {
                            var entry = laneEntries[k];
                            entry.Features = (double[])PercolatorScorer.ResolveFeatureRow(
                                rows, entry.ParquetIndex, entry.CoelutionSum, nFeatures).Clone();
                        }
                        loadProgress.Report(Interlocked.Increment(ref subsetFilesLoaded));
                    });
                });
            }
            else
            {
                // No training subset is loaded and no model is trained.
                log.LogInfo(OspreyFDRResources.PercolatorScorer_RunStreamingFirstPass_Reusing_the_saved_first_pass_model_);
            }

            // The same train-only copy the two resident streaming paths hand the trainer. This
            // path used to build its own field list, which left out the classifier choice: under
            // gbdt it trained the linear SVM, at the tree iteration cap, and the
            // default gbdt run never trained a tree.
            var trainConfig = percConfig.CloneForTrainOnly();
            PercolatorResults trainResults;
            // OSPREY_PROTEIN_BEST: set once round 1 has trained. Null on every other path, which
            // scores the rows as loaded.
            ProteinBestColumn proteinBestColumn = null;
            if (pretrainedModel != null)
                trainResults = pretrainedModel;
            else if (candidateEntries != null)
            {
                Action<PercolatorResults, IReadOnlyDictionary<uint, int>, List<PercolatorEntry>> appendColumn = null;
                if (percConfig.ProteinBest != null)
                {
                    appendColumn = (round1, heldOutFolds, round2Rows) =>
                    {
                        proteinBestColumn = new ProteinBestColumn(percConfig.ProteinBest, round1, heldOutFolds,
                            nFeatures, percConfig.NThreads);
                        AppendProteinBestToRows(proteinBestColumn, round2Rows, streamFileRows, loadFileFeatures,
                            fileNames, rowCounts, fileLanes, log, passLabel);
                    };
                }
                // Round 2's subset replaces the random one as this pass's training rows.
                trainResults = PercolatorTrainer.TrainWithScorePrefilter(candidateEntries, localSelected,
                    trainConfig, log, passLabel, out subsetEntries, appendColumn);
            }
            else
                trainResults = PercolatorTrainer.RunPercolator(subsetEntries, trainConfig);
            if (trainResults.DiagnosticAbort)
                return true;
            // Every step from here reads the model's width and the names it trained on.
            int scoreFeatures = proteinBestColumn != null ? nFeatures + 1 : nFeatures;
            var reportConfig = proteinBestColumn != null
                ? new PercolatorConfig { FeatureInfos = ProteinBestFeature.WithColumnInfo(percConfig.FeatureInfos) }
                : percConfig;

            // Publish the frozen first-pass model (fold weights + biases + standardizer) for the
            // OSPREY_PASS2_QVALUE=transfer pass-2 step. No-op (null) on the default path, so this
            // streaming first pass stays byte-identical. See TODO-osprey_pass2_per_run_only_qvalue.
            captureModel?.Invoke(trainResults);
            LogBlockReads(log, passLabel, @"training load");

            // OSPREY_GBT_OOF: each training row's held-out fold, by run and parquet row index.
            Dictionary<string, Dictionary<int, int>> heldOutFolds = null;
            if (trainResults.TrainEntryFolds != null)
            {
                heldOutFolds = new Dictionary<string, Dictionary<int, int>>();
                int heldOutRows = 0;
                for (int k = 0; k < subsetEntries.Count; k++)
                {
                    int fold = trainResults.TrainEntryFolds[k];
                    if (fold < 0)
                        continue;
                    var entry = subsetEntries[k];
                    if (!heldOutFolds.TryGetValue(entry.FileName, out var byRow))
                        heldOutFolds[entry.FileName] = byRow = new Dictionary<int, int>();
                    byRow[(int)entry.ParquetIndex] = fold;
                    heldOutRows++;
                }
                log.LogInfo(LogTag.COUNT, @"{0} out-of-fold tree scoring for {1} training rows",
                    passLabel, heldOutRows);
            }

            // Release the pass-0 working sets before the score passes so only the bounded lookups
            // remain resident across the peak. The dedup maps are emptied, not dropped: pass 0's
            // file walk captured them, so they live as long as this method does.
            bestTarget.Clear();
            bestTarget.TrimExcess();
            bestDecoy.Clear();
            bestDecoy.TrimExcess();
            runPick.Clear();
            runPick.TrimExcess();
            dedup = null;
            localSelected = null;
            subsetEntries = null;
            candidateEntries = null;
            loadEntries = null;
            subsetByFile = null;

            // Trees or linear weights - whichever this run trained, exactly as on the projection
            // score pass. Null gbtModels selects the linear path, which is unchanged.
            var gbtModels = PercolatorScorer.ResolveGbtModels(trainResults);
            int nModels = gbtModels != null ? gbtModels.Count : trainResults.FoldWeights.Count;
            if (nModels == 0)
                throw new InvalidOperationException(
                    @"RunStreamingFirstPass: trainResults contains no fold models");
            // Average fold weights + biases (identical to PercolatorScorer.ScoreProjectionAndComputeFdrInPlace).
            // Trees are averaged per score instead (see PercolatorScorer.AverageGbtScore).
            double[] avgWeights = null;
            double avgBias = 0.0;
            if (gbtModels == null)
            {
                avgWeights = new double[nFeatures];
                for (int fm = 0; fm < nModels; fm++)
                {
                    double[] foldW = trainResults.FoldWeights[fm];
                    for (int j = 0; j < nFeatures; j++)
                        avgWeights[j] += foldW[j];
                    avgBias += trainResults.FoldBiases[fm];
                }
                double nModelsD = nModels;
                for (int j = 0; j < nFeatures; j++)
                    avgWeights[j] /= nModelsD;
                avgBias /= nModelsD;
            }
            var standardizer = trainResults.Standardizer;

            // ---- Pass 1: score + build the 3 bounded q maps + reduce the clamp floors ----
            // Reuses the verified StreamingFdr.StreamingFirstPassQ kernel; per-file run-q from a bounded one-file
            // buffer, reduced into the best-of-runs clamp floors (issue #4390). The score is
            // recomputed per row (bias first, then the averaged-weight dot product in feature order;
            // a tree ensemble scores the file up front instead), byte-for-byte the resident score
            // loop, only without the O(n) finalScores array.
            // Gate the aggregation on the pass label, exactly as the resident and projection score
            // passes do. This method has one caller and it passes FIRST_PASS_LABEL, so today the
            // gate is a no-op - but an ungated read of MeanBestN here is the identical shape of the
            // defect that let the 2nd pass re-aggregate on the other two paths, and a future
            // second-pass caller would reintroduce it silently.
            var streamingQ = new StreamingFdr.StreamingFirstPassQ(
                passLabel == FIRST_PASS_LABEL ? OspreyEnvironment.MeanBestN : 0);
            var clampFloors = new PercolatorQValues.ExperimentQClampFloors();
            // Each file's first global row ordinal - the g its first row takes in the plain loop -
            // from pass 0's counts, so a lane can stamp a file's ordinals before the files ahead of
            // it have been consumed.
            var firstOrdinal = new int[nFiles];
            for (int f = 1; f < nFiles; f++)
                firstOrdinal[f] = firstOrdinal[f - 1] + rowCounts[f - 1];
            // The feature report: each file's FeatureSums share, computed on its lane in row order
            // (or read back from its sidecar when the file resumed) and added here in file order,
            // so the result does not depend on the lanes and a resumed file contributes exactly as
            // a scored one. False when a resumed file had no share to read, and then there is no
            // honest report to give.
            var contribAcc = new FeatureContributions.Accumulator(scoreFeatures, true);
            bool sharesComplete = true;
            // Every distinct entry_id pass 1 sees, with what the experiment scope needs to know
            // about it. O(distinct entries) - 6.2 M on the 446-run cohort - and the peptide
            // strings are the ones the rows already share.
            var entries = new EntryFactsByEntryId();
            int nonEmptyFiles = 0;
            int g1 = 0;
            // Pass-1 cost attribution on the [PATH] channel. The lane buckets are SUMMED over the
            // lanes - thread time, not wall time - so they say where the work is, and they add up to
            // more than the pass when lanes overlap. The in-order buckets and the wall clock are
            // single-threaded and say what the pass actually waits on. Every timer reads the clock
            // once per FILE, never per row.
            var lane1 = new LaneCost(PASS1_LANE_BUCKETS);
            var order1 = new LaneCost(PASS1_ORDER_BUCKETS);
            var wall1 = Stopwatch.StartNew();
            // No "Running Percolator on N" heading here: it would print AFTER the training lines
            // above, reading as a second Percolator pass. The score heading below marks the step.
            // Fill the previously-silent multi-minute streaming score pass with throttled percent,
            // mirroring the resident PercolatorScorer.ScoreProjectionAndComputeFdrInPlace "Scoring N entries" line.
            // Progress is log-only (CommandOutput.Out), so the FDR output stays byte-identical.
            ProgressReporter.Run(string.Format(OspreyFDRResources.PercolatorScorer_RunStreamingFirstPass_Scoring__0__precursor_candidate_peaks, n), n, scoreProgress =>
            {
                // On a lane: everything about one file that no other file can see.
                Pass1File ScoreFile(int f)
                {
                    string fileName = fileNames[f];
                    long t = Stopwatch.GetTimestamp();
                    // The ONE walk that needs apex RT: this pass hands the file's finished run-scope
                    // output to flushFileRunScope, which writes the v7 sidecar.
                    RowBuffer rows = ReadFileRows(streamFileRows, fileName, StubColumns.ApexRt, rowCounts[f]);
                    t = lane1.Stop(LANE1_WALK, t);
                    int count = rows.Count;
                    // Discards the stored q-values deliberately. This pass is the WRITER, and for
                    // any file it actually scores the sort is the step that produces them. A resumed
                    // file could read them back, but here they feed only the clamp-floor reduction,
                    // so the saving would be confined to a resume while the blast radius would be
                    // global state rather than one row's output.
                    double[] scores = TryLoadCompletedScores(
                        tryStreamCompletedScores, fileName, count, rows.EntryIds, out _, out _);
                    var file = new Pass1File { Rows = rows, Scores = scores };
                    bool scoredHere = scores == null;
                    if (!scoredHere)
                        file.Sums = tryLoadFeatureSums?.Invoke(fileName);
                    t = lane1.Stop(LANE1_SIDECAR, t);
                    if (scoredHere)
                    {
                        IReadOnlyList<double[]> featureRows = loadFileFeatures(fileName);
                        // Persisted for the second pass only where the file is scored: a resumed
                        // file's run-scope sidecar is current only beside a current column.
                        if (proteinBestColumn != null)
                            featureRows = proteinBestColumn.PersistAndAppend(fileName, rows, featureRows);
                        t = lane1.Stop(LANE1_FEATURES, t);
                        scores = ScoreRows(file, featureRows, gbtModels, avgWeights, avgBias, standardizer, scoreFeatures,
                            percConfig.NThreads, HeldOutFoldsOf(heldOutFolds, fileName));
                        t = lane1.Stop(LANE1_SCORE, t);
                    }
                    // The competition's share of this file, reduced here when it is a first-seen
                    // maximum (the default max mode) and the file holds the rows pass 0 counted, so
                    // its ordinals are right. Otherwise the consumer adds the rows one at a time.
                    if (streamingQ.ReducesByFile && count == rowCounts[f])
                    {
                        var competition = streamingQ.BeginFile();
                        int g0 = firstOrdinal[f];
                        for (int r = 0; r < count; r++)
                            competition.Add(g0 + r, scores[r], rows.EntryIds[r], rows.IsDecoys[r], rows.Peptides[r]);
                        file.Competition = competition;
                    }
                    t = lane1.Stop(LANE1_COMPETITION, t);
                    var labels = rows.IsDecoys.ToArray();
                    var entryIds = rows.EntryIds.ToArray();
                    var peptides = rows.Peptides.ToArray();
                    PercolatorQValues.ComputePerFileRunQvalues(
                        scores, labels, entryIds, peptides, 0, count,
                        out double[] runPrecFile, out double[] runPeptFile);
                    t = lane1.Stop(LANE1_RUN_Q, t);
                    // The clamp floors are minimums: this file's rows reduce to one value per key
                    // here, and fold into the shared floors from this lane, in whatever order the
                    // files finish - see ExperimentQClampFloors for why no order is needed.
                    var fileFloorByEntryId = new Dictionary<uint, double>();
                    var fileFloorByPeptide = new Dictionary<(string, bool), double>();
                    for (int r = 0; r < count; r++)
                    {
                        double runBoth = Math.Max(runPrecFile[r], runPeptFile[r]);
                        PercolatorQValues.UpdateExperimentQClampFloor(fileFloorByEntryId,
                            fileFloorByPeptide, entryIds[r], peptides[r], labels[r], runBoth);
                    }
                    clampFloors.Merge(fileFloorByEntryId, fileFloorByPeptide);
                    // Merged from the lane, like the floors, and for the same reason: one map of
                    // millions of random keys, filled from every row of the cohort.
                    entries.MergeFile(f, entryIds, peptides, labels, runPrecFile, runPeptFile);
                    t = lane1.Stop(LANE1_FLOORS, t);

                    // This file's run-scope output is COMPLETE here - score, run precursor q and run
                    // peptide q are all final, and none of them depends on another file. Hand it to
                    // the caller so it lands on disk now rather than one phase later, which is what
                    // makes an interrupted run lose one file instead of every file (see
                    // FileRunScopeSink). Skipped when the scores came off an existing sidecar: that
                    // file is already written, and rewriting an artifact a validity marker attests
                    // would replace it with a copy the marker no longer describes.
                    double[] apexRts = rows.ApexRts.ToArray();
                    if (scoredHere)
                    {
                        flushFileRunScope?.Invoke(fileName, f, count, entryIds, file.Scores,
                            runPrecFile, runPeptFile, apexRts, file.Sums);
                    }
                    t = lane1.Stop(LANE1_FLUSH, t);
                    // The sink's run-scope share - the passing tallies - reads only these values,
                    // so it is done here rather than after the experiment barrier.
                    file.RunScope = sink.PrepareRunScope(f, new FdrRunScopeRows(entryIds, labels,
                        rows.Charges, peptides, file.Scores, runPrecFile, runPeptFile, apexRts));
                    lane1.Stop(LANE1_SINK, t);
                    return file;
                }

                // On this thread, in file order: merging the competition (a tie goes to the first-seen
                // ordinal) and adding the files' feature-report shares (floating-point, so the order
                // is part of the sum).
                void ApplyScoredFile(int f, Pass1File file)
                {
                    long t = Stopwatch.GetTimestamp();
                    RowBuffer rows = file.Rows;
                    int count = rows.Count;
                    if (count > 0)
                        nonEmptyFiles++;
                    // The lane's reduction is used only when its ordinals are the ones this walk has
                    // reached. A file earlier in the cohort whose row count disagreed with pass 0 would
                    // shift every later file's, and then those files are added row by row instead.
                    bool merge = file.Competition != null && g1 == firstOrdinal[f];
                    if (merge)
                        streamingQ.MergeFile(file.Competition);
                    sink.AcceptRunScope(f, file.RunScope);
                    for (int r = 0; r < count; r++)
                    {
                        if (!merge)
                            streamingQ.Add(g1, file.Scores[r], rows.EntryIds[r], rows.IsDecoys[r], rows.Peptides[r]);
                        g1++;
                    }
                    scoreProgress.Report(g1);
                    if (file.Sums != null)
                        contribAcc.Add(file.Sums);
                    else
                        sharesComplete = false;
                    order1.Stop(ORDER1_COMPETITION, t);
                }

                OrderedFileLanes.Run(nFiles, fileLanes, ScoreFile, ApplyScoredFile);
            });
            wall1.Stop();
            log.LogInfo(LogTag.PATH,
                @"{0} pass 1 cost over {1} rows, {2} lane(s): wall {3:F1}s; lanes (summed) parquet walk {4:F1}s, sidecar load {5:F1}s, feature load {6:F1}s, score {7:F1}s, competition {8:F1}s, run-q sort {9:F1}s, clamp floors + entry facts {10:F1}s, sidecar write {11:F1}s, run-scope tallies {12:F1}s; in order competition merge + contribution sums {13:F1}s",
                passLabel, n, LanesUsed(fileLanes, nFiles), wall1.Elapsed.TotalSeconds,
                lane1.Seconds(LANE1_WALK), lane1.Seconds(LANE1_SIDECAR), lane1.Seconds(LANE1_FEATURES),
                lane1.Seconds(LANE1_SCORE), lane1.Seconds(LANE1_COMPETITION), lane1.Seconds(LANE1_RUN_Q), lane1.Seconds(LANE1_FLOORS),
                lane1.Seconds(LANE1_FLUSH), lane1.Seconds(LANE1_SINK), order1.Seconds(ORDER1_COMPETITION));
            LogBlockReads(log, passLabel, @"pass 1");

            // A report over only the files this process scored would describe a different
            // population than the run's, with nothing on the page to say so.
            var contributions = sharesComplete
                ? PercolatorScorer.BuildContributions(contribAcc, gbtModels, trainResults, reportConfig)
                : null;
            captureContributions?.Invoke(contributions);

            // Finalize the bounded lookups. PEP is global (built always); the experiment maps use
            // the single-file shortcut (exp == per-run) so they are built only when multi-file --
            // matching PercolatorScorer.ScoreProjectionAndComputeFdrInPlace exactly.
            var pepByEntryId = streamingQ.BuildPepWinnerMap();
            bool isSingleFile = nonEmptyFiles <= 1;
            Dictionary<uint, double> expPrecByWinnerId = isSingleFile
                ? null : streamingQ.BuildExperimentPrecursorQMap();
            Dictionary<string, double> expPeptByPeptide = isSingleFile
                ? null : streamingQ.BuildExperimentPeptideQMap();

            // The score those competitions ranked each entry on (sidecar v4, issue #4522).
            // Built unconditionally -- see PercolatorScorer.ScoreProjectionAndComputeFdrInPlace for why the
            // single-file shortcut does NOT apply to the aggregate.
            var expAggByEntryId = streamingQ.BuildExperimentAggregateScoreMap();
            streamingQ.Release();

            var experimentScope = new ExperimentScope(isSingleFile, expPrecByWinnerId, expPeptByPeptide,
                clampFloors, pepByEntryId, expAggByEntryId);

            // ---- Experiment scope: one set of values per distinct entry ----
            // Every input to an entry's four experiment-scope values is a property of the entry,
            // so they are computed once per entry from the facts pass 1 kept, not once per row from
            // a second walk of every row. That walk re-read every parquet and sidecar of the
            // cohort to hand the sink each value again for every run the entry appears in, and its
            // other products - the passing tallies and the sidecar records - were pass 1's by
            // then. Ascending entry_id, the order the experiment sidecar is written in.
            var wallEntries = Stopwatch.StartNew();
            uint[] entryIdsSeen = entries.SortedEntryIds();
            foreach (uint entryId in entryIdsSeen)
            {
                var facts = entries[entryId];
                // On a single-file run the experiment q IS the run q, and this entry's rows have
                // several - the disagreement FdrExperimentAccumulator used to refuse.
                if (isSingleFile && facts.RunQDisagrees)
                {
                    throw new InvalidOperationException(string.Format(
                        @"entry_id {0} has more than one row in its run, with different run q-values, " +
                        @"so it has no single experiment q-value.", entryId));
                }
                experimentScope.Resolve(entryId, facts.Peptide, facts.IsDecoy, facts.RunPrecursorQ, facts.RunPeptideQ,
                    out double ep, out double epe, out double pep, out double ea);
                sink.AcceptExperimentEntry(entryId, facts.Peptide, ea, ep, epe, pep);
            }
            entries.Release();
            wallEntries.Stop();
            log.LogInfo(LogTag.PATH, @"{0} experiment scope over {1} distinct entries: {2:F1}s",
                passLabel, entryIdsSeen.Length, wallEntries.Elapsed.TotalSeconds);

            if (sink.FoldsRowExperimentValues)
            {
                FoldRowExperimentValues(fileNames, streamFileRows, loadFileFeatures, rowCounts, sink,
                    experimentScope, tryStreamCompletedScores, gbtModels, avgWeights, avgBias, standardizer,
                    scoreFeatures, percConfig.NThreads, fileLanes, log, passLabel, n, heldOutFolds, proteinBestColumn);
            }
            sink.Finish(log);
            return false;
        }

        /// <summary>
        /// The <c>--model-diagnostics</c> row walk: every row again, with both scopes' values
        /// joined, for the sink's per-row fold. The report needs each row's experiment q-values,
        /// so this is the one consumer that still pays a second walk of every row - and only when
        /// the report was asked for.
        ///
        /// <para>Each file's scores and run q-values come back off the sidecar pass 1 wrote. A file
        /// with no sidecar - no <paramref name="tryStreamCompletedScores"/>, or one that refuses
        /// it - is re-read and re-scored here exactly as pass 1 scored it.</para>
        /// </summary>
        private static void FoldRowExperimentValues(IReadOnlyList<string> fileNames,
            Action<string, StubColumns, Action<uint, byte, bool, double, string, double>> streamFileRows,
            Func<string, IReadOnlyList<double[]>> loadFileFeatures, int[] rowCounts, IFdrFirstPassSink sink,
            ExperimentScope experimentScope, CompletedScoreStreamer tryStreamCompletedScores,
            IReadOnlyList<GradientBoostedTrees> gbtModels, double[] avgWeights, double avgBias,
            FeatureStandardizer standardizer, int nFeatures, int nThreads, int fileLanes, IOspreyLog log,
            string passLabel, int n, Dictionary<string, Dictionary<int, int>> heldOutFolds,
            ProteinBestColumn proteinBestColumn)
        {
            int nFiles = fileNames.Count;
            var lane = new LaneCost(FOLD_LANE_BUCKETS);
            var order = new LaneCost(FOLD_ORDER_BUCKETS);
            var wall = Stopwatch.StartNew();
            int gFolded = 0;
            ProgressReporter.Run(string.Format(OspreyFDRResources.PercolatorScorer_RunStreamingFirstPass_Assigning_q_values_to__0__precursor_candidate_peaks, n), n, foldProgress =>
            {
                // On a lane: the file's rows, its five q-values and the sink's order-free fold.
                FoldFile PrepareFile(int f)
                {
                    string fileName = fileNames[f];
                    long t = Stopwatch.GetTimestamp();
                    // Without coelution_sum: only the rescore below uses it, and only for a file
                    // with no sidecar, which re-reads its rows with the column. It is the one
                    // walk column among the features, so leaving it out saves a disk read per
                    // row group.
                    RowBuffer rows = ReadFileRows(streamFileRows, fileName, StubColumns.SkipCoelutionSum, rowCounts[f]);
                    t = lane.Stop(FOLD_WALK, t);
                    int count = rows.Count;
                    double[] scores = TryLoadCompletedScores(
                        tryStreamCompletedScores, fileName, count, rows.EntryIds,
                        out double[] runPrecFile, out double[] runPeptFile);
                    t = lane.Stop(FOLD_SIDECAR, t);
                    // Keyed on the scores, which TryLoadCompletedScores returns null exactly when
                    // it returns no q-values.
                    if (scores == null)
                    {
                        rows = ReadFileRows(streamFileRows, fileName, StubColumns.Core, rowCounts[f]);
                        IReadOnlyList<double[]> featureRows = loadFileFeatures(fileName);
                        if (proteinBestColumn != null)
                            featureRows = proteinBestColumn.AppendTo(rows, featureRows);
                        scores = new double[count];
                        if (ScoresBeforeRowLoop(null, featureRows, rows.CoelutionSums, gbtModels,
                                standardizer, nFeatures, nThreads, scores, HeldOutFoldsOf(heldOutFolds, fileName)) == null)
                        {
                            var featureBuf = new double[nFeatures];
                            for (int r = 0; r < count; r++)
                            {
                                scores[r] = ComputeStreamedScore(avgWeights, avgBias, standardizer, featureBuf,
                                    featureRows, r, rows.CoelutionSums[r], nFeatures);
                            }
                        }
                        PercolatorQValues.ComputePerFileRunQvalues(
                            scores, rows.IsDecoys.ToArray(), rows.EntryIds.ToArray(), rows.Peptides.ToArray(),
                            0, count, out runPrecFile, out runPeptFile);
                        t = lane.Stop(FOLD_RESCORE, t);
                    }
                    var expPrec = new double[count];
                    var expPept = new double[count];
                    var peps = new double[count];
                    for (int r = 0; r < count; r++)
                    {
                        experimentScope.Resolve(rows.EntryIds[r], rows.Peptides[r], rows.IsDecoys[r],
                            runPrecFile[r], runPeptFile[r], out expPrec[r], out expPept[r], out peps[r], out _);
                    }
                    t = lane.Stop(FOLD_LOOKUPS, t);
                    var fileRows = new FdrFileRows(rows.EntryIds, rows.IsDecoys, rows.Charges, rows.Peptides, scores,
                        runPrecFile, runPeptFile, expPrec, expPept, peps);
                    var file = new FoldFile(count, sink.PrepareRowFold(f, fileRows));
                    lane.Stop(FOLD_PREPARE, t);
                    return file;
                }

                // On this thread, in file order: the fold's order-dependent merge.
                void MergeFile(int f, FoldFile file)
                {
                    long t = Stopwatch.GetTimestamp();
                    sink.AcceptRowFold(f, file.Prepared);
                    // Once per file: a report per row is a lock taken 4 million times a file on
                    // the one thread every other lane is waiting on.
                    gFolded += file.Count;
                    foldProgress.Report(gFolded);
                    order.Stop(FOLD_ORDER_MERGE, t);
                }

                OrderedFileLanes.Run(nFiles, fileLanes, PrepareFile, MergeFile);
            });
            wall.Stop();
            log.LogInfo(LogTag.PATH,
                @"{0} diagnostics row walk over {1} rows, {2} lane(s): wall {3:F1}s; lanes (summed) parquet walk {4:F1}s, sidecar load {5:F1}s, rescore + run-q recompute {6:F1}s, q-value lookups {7:F1}s, fold preparation {8:F1}s; in order fold merge {9:F1}s",
                passLabel, n, LanesUsed(fileLanes, nFiles), wall.Elapsed.TotalSeconds,
                lane.Seconds(FOLD_WALK), lane.Seconds(FOLD_SIDECAR), lane.Seconds(FOLD_RESCORE),
                lane.Seconds(FOLD_LOOKUPS), lane.Seconds(FOLD_PREPARE), order.Seconds(FOLD_ORDER_MERGE));
            LogBlockReads(log, passLabel, @"diagnostics row walk");
        }

        /// <summary>
        /// Recompute one row's SVM discriminant from its streamed features (issue #4355
        /// struct-shrink S3, Stage B): resolve the parquet feature row by its running ordinal,
        /// standardize it in <paramref name="featureBuf"/>, then the averaged-model score = bias
        /// first + the weight dot product in feature order -- byte-for-byte the resident score
        /// loop's per-entry math (<see cref="PercolatorScorer.ScoreProjectionAndComputeFdrInPlace"/>). Leaves the
        /// standardized values in <paramref name="featureBuf"/> so the caller can bin them into
        /// the feature-contribution accumulator without recomputing. Linear only: a tree
        /// ensemble scores its file before the row loop, in <see cref="ScoresBeforeRowLoop"/>.
        /// </summary>
        private static double ComputeStreamedScore(
            double[] avgWeights, double avgBias, FeatureStandardizer standardizer, double[] featureBuf,
            IReadOnlyList<double[]> rows, int parquetIndex, double coelutionSum, int nFeatures)
        {
            PercolatorScorer.StandardizeFeatureRow(standardizer, featureBuf, rows, (uint)parquetIndex, coelutionSum, nFeatures);
            double score = avgBias;
            for (int j = 0; j < nFeatures; j++)
                score += avgWeights[j] * featureBuf[j];
            return score;
        }

        /// <summary>
        /// One file's scores when they are known before its row loop runs in
        /// <see cref="RunStreamingFirstPass"/>, or <c>null</c> when that loop computes them row
        /// by row. Known when the caller read them back off the file's own sidecar
        /// (<paramref name="completedScores"/>, returned as is), or when the model is a tree
        /// ensemble: a tree score is a pure function of its row, so the whole file is scored here
        /// in parallel (<see cref="PercolatorScorer.ScoreRowsGbt"/>), which is what the projection score pass does
        /// too, straight into <paramref name="destination"/> - the loop's own score array, so no
        /// second per-file array is allocated - which is returned. The linear model returns
        /// <c>null</c> and keeps the serial per-row dot product: the parity-locked form, and the
        /// one that leaves each row's standardized vector behind for the feature-contribution
        /// report.
        /// </summary>
        private static double[] ScoresBeforeRowLoop(
            double[] completedScores, IReadOnlyList<double[]> rows, IReadOnlyList<double> coelutionSums,
            IReadOnlyList<GradientBoostedTrees> gbtModels, FeatureStandardizer standardizer,
            int nFeatures, int nThreads, double[] destination, IReadOnlyDictionary<int, int> heldOutFoldByRow = null)
        {
            if (completedScores != null || gbtModels == null)
                return completedScores;
            // The running row ordinal IS the parquet row index here, exactly as it is for
            // ComputeStreamedScore: the rows arrive in parquet order.
            PercolatorScorer.ScoreRowsGbt(destination.Length, r => PercolatorScorer.ResolveFeatureRow(rows, (uint)r, coelutionSums[r], nFeatures),
                gbtModels, standardizer, nFeatures, destination, 0, nThreads, heldOutFoldByRow);
            return destination;
        }

        /// <summary>One run's held-out folds by parquet row index, or <c>null</c> when there are none.</summary>
        private static IReadOnlyDictionary<int, int> HeldOutFoldsOf(
            Dictionary<string, Dictionary<int, int>> heldOutFolds, string fileName)
        {
            return heldOutFolds != null && heldOutFolds.TryGetValue(fileName, out var byRow) ? byRow : null;
        }

        /// <summary>
        /// The refusal <see cref="RunStreamingFirstPass"/> throws for a pretrained model of the
        /// other classifier. Names the two classifiers by what they are, and claims neither which
        /// flag trained the persisted model nor what it scored: the scorer knows neither.
        /// </summary>
        internal static string ClassifierMismatchMessage(bool modelIsTrees, bool runUsesTrees)
        {
            return string.Format(
                @"The persisted first-pass model offered for reuse is {0}, but this run uses {1}, " +
                @"and a model of one classifier cannot score a run of the other. The task validity " +
                @"key records the classifier, so a current model should always match; this points " +
                @"to an inconsistency in the intermediate files. Re-run with a clean output directory.",
                PercolatorResults.ClassifierName(modelIsTrees), PercolatorResults.ClassifierName(runUsesTrees));
        }

        /// <summary>
        /// One file's raw parquet stub rows, buffered by the 1st-pass streaming score path
        /// (<see cref="RunStreamingFirstPass"/>) so the stream callback only appends to
        /// reference-type lists (never a captured-and-mutated counter) and the row/global ordinals
        /// advance in plain indexed loops. Bounded to one file at a time -- the same one-file
        /// resident set the per-file run-q competition already requires. Five of the six
        /// parallel lists mirror the <see cref="FdrProjection"/> scalar slice the resident path
        /// holds; <see cref="ApexRts"/> is the sixth, and it is scoring INPUT to nothing - it is
        /// carried so this file's <c>.1st-pass.fdr_scores.bin</c> can persist it (format v7,
        /// issue #4522).
        /// </summary>
        private sealed class RowBuffer
        {
            public readonly List<uint> EntryIds;
            public readonly List<byte> Charges;
            public readonly List<bool> IsDecoys;
            public readonly List<double> CoelutionSums;
            public readonly List<string> Peptides;
            public readonly List<double> ApexRts;

            public RowBuffer() : this(0)
            {
            }

            /// <param name="capacity">The file's row count when already known, so the lists are
            /// sized once rather than grown by doubling through ~4M rows.</param>
            public RowBuffer(int capacity)
            {
                EntryIds = new List<uint>(capacity);
                Charges = new List<byte>(capacity);
                IsDecoys = new List<bool>(capacity);
                CoelutionSums = new List<double>(capacity);
                Peptides = new List<string>(capacity);
                ApexRts = new List<double>(capacity);
            }

            public int Count => EntryIds.Count;

            public void Clear()
            {
                EntryIds.Clear();
                Charges.Clear();
                IsDecoys.Clear();
                CoelutionSums.Clear();
                Peptides.Clear();
                ApexRts.Clear();
            }

            public void Add(uint entryId, byte charge, bool isDecoy, double coelutionSum,
                string peptide, double apexRt)
            {
                EntryIds.Add(entryId);
                Charges.Add(charge);
                IsDecoys.Add(isDecoy);
                CoelutionSums.Add(coelutionSum);
                ApexRts.Add(apexRt);
                // Normalize a null modseq to string.Empty exactly as the resident FdrProjectionSet
                // .Builder.AddRow does (a present-but-null modified_sequence element survives
                // ReadFdrStubScalars' column-level guard): a null peptide would otherwise throw as a
                // Dictionary<string,...> key in StreamingFdr.StreamingFirstPassQ / SubsampleByPeptideGroup and
                // would group differently from the resident path's ""-normalized peptides.
                Peptides.Add(peptide ?? string.Empty);
            }
        }

        /// <summary>A training entry for one streamed dedup row, its features not yet loaded.</summary>
        private static PercolatorEntry NewStreamedEntry(FirstPassDedupRow d)
        {
            return new PercolatorEntry
            {
                FileName = d.FileName,
                Peptide = d.Peptide,
                Charge = d.Charge,
                IsDecoy = d.IsDecoy,
                EntryId = d.EntryId,
                ParquetIndex = d.ParquetIndex,
                CoelutionSum = d.CoelutionSum,
                Features = null
            };
        }

        /// <summary>
        /// One file's rows, read into a buffer of its own so several files can be read at once.
        /// </summary>
        private static RowBuffer ReadFileRows(
            Action<string, StubColumns, Action<uint, byte, bool, double, string, double>> streamFileRows,
            string fileName, StubColumns columns, int capacity = 0)
        {
            var rows = new RowBuffer(capacity);
            streamFileRows(fileName, columns, rows.Add);
            return rows;
        }

        /// <summary>
        /// Scores every row of a pass-1 file on its lane and reduces the file's share of the
        /// feature report (<see cref="FeatureSums"/>) from each row's standardized vector, in row
        /// order, as it goes - so no vector outlives its row. A tree ensemble scores the whole file
        /// first (<see cref="ScoresBeforeRowLoop"/>) and standardizes each row here for the share.
        /// Returns the scores it also stores on <paramref name="file"/>.
        /// </summary>
        private static double[] ScoreRows(Pass1File file, IReadOnlyList<double[]> featureRows,
            IReadOnlyList<GradientBoostedTrees> gbtModels, double[] avgWeights, double avgBias,
            FeatureStandardizer standardizer, int nFeatures, int nThreads,
            IReadOnlyDictionary<int, int> heldOutFoldByRow)
        {
            RowBuffer rows = file.Rows;
            int count = rows.Count;
            var scores = new double[count];
            bool linear = ScoresBeforeRowLoop(null, featureRows, rows.CoelutionSums, gbtModels,
                standardizer, nFeatures, nThreads, scores, heldOutFoldByRow) == null;
            file.Scores = scores;
            var share = new FeatureContributions.Accumulator(nFeatures, true);
            var featureBuf = new double[nFeatures];
            for (int r = 0; r < count; r++)
            {
                // Both leave featureBuf standardized - the vector the contribution report sums and
                // bins. The trees already scored the row; the linear model scores it here.
                if (linear)
                {
                    scores[r] = ComputeStreamedScore(
                        avgWeights, avgBias, standardizer, featureBuf, featureRows, r, rows.CoelutionSums[r], nFeatures);
                }
                else
                {
                    PercolatorScorer.StandardizeFeatureRow(standardizer, featureBuf, featureRows, (uint)r, rows.CoelutionSums[r], nFeatures);
                }
                share.Add(featureBuf, rows.IsDecoys[r]);
            }
            file.Sums = share.ToFeatureSums();
            return scores;
        }

        /// <summary>
        /// Extends the round-2 training rows' feature vectors by the protein-best column of their
        /// run. Each file writes only its own rows, so the files need no ordering; a file with no
        /// round-2 row is not read.
        /// </summary>
        private static void AppendProteinBestToRows(ProteinBestColumn column, List<PercolatorEntry> round2Rows,
            Action<string, StubColumns, Action<uint, byte, bool, double, string, double>> streamFileRows,
            Func<string, IReadOnlyList<double[]>> loadFileFeatures, IReadOnlyList<string> fileNames,
            int[] rowCounts, int fileLanes, IOspreyLog log, string passLabel)
        {
            var byFile = PercolatorSampling.GroupIndicesByFileName(round2Rows);
            var files = new List<KeyValuePair<string, List<int>>>(byFile);
            var rowCountByFile = new Dictionary<string, int>(fileNames.Count, StringComparer.Ordinal);
            for (int f = 0; f < fileNames.Count; f++)
                rowCountByFile[fileNames[f]] = rowCounts[f];
            var wall = Stopwatch.StartNew();
            OrderedFileLanes.For(files.Count, fileLanes, i =>
            {
                string fileName = files[i].Key;
                RowBuffer rows = ReadFileRows(streamFileRows, fileName, StubColumns.Core, rowCountByFile[fileName]);
                double[] values = column.ValuesFor(rows, loadFileFeatures(fileName));
                foreach (int k in files[i].Value)
                {
                    var entry = round2Rows[k];
                    var extended = new double[entry.Features.Length + 1];
                    Array.Copy(entry.Features, extended, entry.Features.Length);
                    extended[entry.Features.Length] = values[entry.ParquetIndex];
                    entry.Features = extended;
                }
            });
            log.LogInfo(LogTag.PATH, @"{0} protein-best column for {1} round-2 rows from {2} file(s): {3:F1}s",
                passLabel, round2Rows.Count, files.Count, wall.Elapsed.TotalSeconds);
        }

        /// <summary>
        /// What the protein-best column of a run is computed from, kept for the score pass: round
        /// 1's models, and which fold held each of its training precursors out. A file's column is
        /// a pure function of the file and these, recomputed on its lane where it is needed, so no
        /// O(rows) column stays resident between the training and the score pass.
        ///
        /// <para>The scores are out of fold whenever they can be: a precursor round 1 trained on is
        /// scored in every run by the one fold model that held its peptide group out. Any other
        /// row is scored by the mean of the fold models, none of which saw its peptide. So no prior
        /// score comes from a model that trained on the peptide it scores, with or without
        /// <c>OSPREY_GBT_OOF</c>, which governs only the final model's score pass.</para>
        /// </summary>
        private sealed class ProteinBestColumn
        {
            private readonly ProteinBestRequest _request;
            private readonly IReadOnlyList<GradientBoostedTrees> _models;
            private readonly FeatureStandardizer _standardizer;
            private readonly IReadOnlyDictionary<uint, int> _heldOutFoldByBaseId;
            private readonly int _rawFeatures;
            private readonly int _threads;

            public ProteinBestColumn(ProteinBestRequest request, PercolatorResults round1,
                IReadOnlyDictionary<uint, int> heldOutFoldByBaseId, int rawFeatures, int threads)
            {
                _request = request;
                _models = round1.FoldGbtModels;
                _standardizer = round1.Standardizer;
                _heldOutFoldByBaseId = heldOutFoldByBaseId;
                _rawFeatures = rawFeatures;
                _threads = threads;
            }

            /// <summary>The column of one file, a value per row, from its raw feature rows.</summary>
            public double[] ValuesFor(RowBuffer rows, IReadOnlyList<double[]> rawRows)
            {
                return ValuesFor(rows, rawRows, out _);
            }

            /// <summary>
            /// The score pass's form: one file's raw feature rows extended by the column, after
            /// handing the file's column to the request's persister when it has one, so the second
            /// pass reads the values this pass scored with.
            /// </summary>
            public IReadOnlyList<double[]> PersistAndAppend(string fileName, RowBuffer rows, IReadOnlyList<double[]> rawRows)
            {
                double[] values = ValuesFor(rows, rawRows, out double floor);
                _request.PersistRun?.Invoke(fileName, ProteinBestRunValues.FromRows(rows.EntryIds, values, floor));
                return Append(rows, rawRows, values);
            }

            private double[] ValuesFor(RowBuffer rows, IReadOnlyList<double[]> rawRows, out double floor)
            {
                int count = rows.Count;
                var baseIds = new uint[count];
                var heldOut = new Dictionary<int, int>();
                for (int r = 0; r < count; r++)
                {
                    baseIds[r] = rows.EntryIds[r] & PercolatorEntry.BASE_ID_MASK;
                    if (_heldOutFoldByBaseId.TryGetValue(baseIds[r], out int fold))
                        heldOut[r] = fold;
                }
                var priorScores = new double[count];
                PercolatorScorer.ScoreRowsGbt(count,
                    r => PercolatorScorer.ResolveFeatureRow(rawRows, (uint)r, rows.CoelutionSums[r], _rawFeatures),
                    _models, _standardizer, _rawFeatures, priorScores, 0, _threads, heldOut);
                return ProteinBestFeature.ComputeColumn(_request.Membership, _request.TopN, _request.DecoyOwn,
                    baseIds, rows.IsDecoys, priorScores, out floor);
            }

            /// <summary>One file's raw feature rows, each extended by its column value.</summary>
            public IReadOnlyList<double[]> AppendTo(RowBuffer rows, IReadOnlyList<double[]> rawRows)
            {
                return Append(rows, rawRows, ValuesFor(rows, rawRows));
            }

            private IReadOnlyList<double[]> Append(RowBuffer rows, IReadOnlyList<double[]> rawRows, double[] values)
            {
                var extended = new double[rows.Count][];
                for (int r = 0; r < extended.Length; r++)
                {
                    double[] raw = PercolatorScorer.ResolveFeatureRow(rawRows, (uint)r, rows.CoelutionSums[r], _rawFeatures);
                    var row = new double[_rawFeatures + 1];
                    Array.Copy(raw, row, _rawFeatures);
                    row[_rawFeatures] = values[r];
                    extended[r] = row;
                }
                return extended;
            }
        }

        /// <summary>The lanes a pass actually runs: never more than the files it has.</summary>
        private static int LanesUsed(int fileLanes, int nFiles)
        {
            return Math.Max(1, Math.Min(fileLanes, nFiles));
        }

        // Experimental block-read totals (OSPREY_BLOCK_READ_MB) at a phase boundary; silent when off.
        private static void LogBlockReads(IOspreyLog log, string passLabel, string phase)
        {
            string text = BlockReadStats.Text();
            if (text != null)
                log.LogInfo(LogTag.PATH, @"{0} after {1}: {2}", passLabel, phase, text);
        }

        /// <summary>
        /// What a pass-1 lane hands the in-order consumer for one file: the rows and their scores,
        /// with the standardized vectors whose sums must be added in row order and the
        /// histogram counts that need not be.
        /// </summary>
        private sealed class Pass1File
        {
            public RowBuffer Rows;
            public double[] Scores;
            // The file's share of the feature report: reduced on the lane when the file was scored
            // here, read back from its sidecar when it resumed; null when it resumed without one.
            public FeatureSums Sums;
            // This file's share of the competition, or null when the consumer adds its rows.
            public StreamingFdr.StreamingFirstPassQ.FileReduction Competition;
            // What IFdrFirstPassSink.PrepareRunScope returned for this file.
            public object RunScope;
        }

        /// <summary>
        /// What the experiment scope needs to know about one distinct entry_id: its peptide and
        /// side, for the peptide q-value and its floor, and its run q-values in the file it was
        /// taken from, which ARE the experiment q-values on a single-file run.
        /// </summary>
        private readonly struct EntryFacts
        {
            public EntryFacts(int fileIdx, string peptide, bool isDecoy, double runPrecursorQ, double runPeptideQ,
                bool runQDisagrees = false)
            {
                FileIdx = fileIdx;
                Peptide = peptide;
                IsDecoy = isDecoy;
                RunPrecursorQ = runPrecursorQ;
                RunPeptideQ = runPeptideQ;
                RunQDisagrees = runQDisagrees;
            }

            public int FileIdx { get; }
            public string Peptide { get; }
            public bool IsDecoy { get; }
            public double RunPrecursorQ { get; }
            public double RunPeptideQ { get; }

            /// <summary>
            /// The entry has more than one row in <see cref="FileIdx"/> and their run q-values
            /// differ, so on a single-file run it has no one experiment q-value. A cohort parquet
            /// holds one row per entry per run; a multi-candidate file does not.
            /// </summary>
            public bool RunQDisagrees { get; }

            public EntryFacts WithRunQDisagreement()
            {
                return new EntryFacts(FileIdx, Peptide, IsDecoy, RunPrecursorQ, RunPeptideQ, true);
            }
        }

        /// <summary>
        /// The distinct entry_ids of the streaming first pass, each with its
        /// <see cref="EntryFacts"/>, merged from several file lanes at once. Every value used off
        /// the facts on a multi-file run is a property of the entry, so any file's facts would do;
        /// the LOWEST file index is kept so the result does not depend on which lane finished
        /// first. Sharded and batch-locked exactly as
        /// <see cref="PercolatorQValues.ExperimentQClampFloors"/> is, for the reason given there.
        ///
        /// <para>The per-row walk this replaced fed every row to <c>FdrExperimentAccumulator</c>,
        /// which throws when two observations of an entry disagree. The merge keeps that check
        /// where it still means something: an entry_id whose rows carry a different peptide or
        /// side is refused here, in the file that shows it, and rows of one file that disagree on
        /// run q are marked, because that is only an error on a single-file run.</para>
        /// </summary>
        private sealed class EntryFactsByEntryId
        {
            private readonly Dictionary<uint, EntryFacts>[] _shards =
                new Dictionary<uint, EntryFacts>[PercolatorQValues.ExperimentQClampFloors.SHARD_COUNT];

            public EntryFactsByEntryId()
            {
                for (int s = 0; s < _shards.Length; s++)
                    _shards[s] = new Dictionary<uint, EntryFacts>();
            }

            /// <summary>Folds in one file's rows; safe to call from several lanes at once.</summary>
            public void MergeFile(int fileIdx, uint[] entryIds, string[] peptides, bool[] isDecoys,
                double[] runPrecursorQ, double[] runPeptideQ)
            {
                var batches = new List<int>[_shards.Length];
                for (int r = 0; r < entryIds.Length; r++)
                {
                    // Every experiment-scope map keys an entry by its base_id and side, and the
                    // side IS the entry_id's decoy bit, so a row whose flag disagrees would find no
                    // aggregate score after the barrier. Refused here, in the file that has it,
                    // rather than after the whole pass.
                    if (((entryIds[r] & LibraryEntry.DECOY_ID_BIT) != 0) != isDecoys[r])
                    {
                        throw new InvalidOperationException(string.Format(
                            @"entry_id {0} has decoy flag {1}, which disagrees with its decoy bit.",
                            entryIds[r], isDecoys[r]));
                    }
                    int s = PercolatorQValues.ExperimentQClampFloors.ShardIndex(entryIds[r]);
                    (batches[s] ?? (batches[s] = new List<int>())).Add(r);
                }
                for (int s = 0; s < _shards.Length; s++)
                {
                    var batch = batches[s];
                    if (batch == null)
                        continue;
                    var shard = _shards[s];
                    lock (shard)
                    {
                        foreach (int r in batch)
                        {
                            uint entryId = entryIds[r];
                            if (shard.TryGetValue(entryId, out var cur))
                            {
                                if (cur.IsDecoy != isDecoys[r] || !string.Equals(cur.Peptide, peptides[r], StringComparison.Ordinal))
                                {
                                    throw new InvalidOperationException(string.Format(
                                        @"entry_id {0} is [{1}] (decoy {2}) in one run and [{3}] (decoy {4}) in another; " +
                                        @"an entry_id names one precursor in every run.",
                                        entryId, cur.Peptide, cur.IsDecoy, peptides[r], isDecoys[r]));
                                }
                                if (cur.FileIdx == fileIdx)
                                {
                                    if (!cur.RunQDisagrees &&
                                        (!cur.RunPrecursorQ.Equals(runPrecursorQ[r]) || !cur.RunPeptideQ.Equals(runPeptideQ[r])))
                                    {
                                        shard[entryId] = cur.WithRunQDisagreement();
                                    }
                                    continue;
                                }
                                if (cur.FileIdx < fileIdx)
                                    continue;
                            }
                            shard[entryId] = new EntryFacts(fileIdx, peptides[r], isDecoys[r],
                                runPrecursorQ[r], runPeptideQ[r]);
                        }
                    }
                }
            }

            /// <summary>Every entry_id merged, ascending - the order the experiment sidecar is
            /// written in, and the order a resumed run reads its records back in.</summary>
            public uint[] SortedEntryIds()
            {
                int count = 0;
                foreach (var shard in _shards)
                    count += shard.Count;
                var ids = new uint[count];
                int i = 0;
                foreach (var shard in _shards)
                {
                    shard.Keys.CopyTo(ids, i);
                    i += shard.Count;
                }
                Array.Sort(ids); // Array.Sort OK: distinct uint keys (each shard is a dictionary, and an id hashes to one shard), so no two elements tie
                return ids;
            }

            public EntryFacts this[uint entryId] =>
                _shards[PercolatorQValues.ExperimentQClampFloors.ShardIndex(entryId)][entryId];

            /// <summary>Drops every entry once the experiment scope has been resolved.</summary>
            public void Release()
            {
                for (int s = 0; s < _shards.Length; s++)
                    _shards[s] = new Dictionary<uint, EntryFacts>();
            }
        }

        /// <summary>
        /// The finished experiment-scope maps, and the one rule that turns them into an entry's
        /// four experiment-scope values. Shared by the per-entry loop and the diagnostics row walk
        /// so the two cannot disagree. Every input is a property of the entry - its id, its
        /// peptide and side - except the run q-values, which are read only on a single-file run,
        /// where an entry has exactly one row.
        /// </summary>
        private sealed class ExperimentScope
        {
            private readonly bool _isSingleFile;
            private readonly Dictionary<uint, double> _precursorQByWinnerId;
            private readonly Dictionary<string, double> _peptideQByPeptide;
            private readonly PercolatorQValues.ExperimentQClampFloors _clampFloors;
            private readonly Dictionary<uint, double> _pepByWinnerId;
            private readonly Dictionary<uint, double> _aggregateByEntryId;

            public ExperimentScope(bool isSingleFile, Dictionary<uint, double> precursorQByWinnerId,
                Dictionary<string, double> peptideQByPeptide, PercolatorQValues.ExperimentQClampFloors clampFloors,
                Dictionary<uint, double> pepByWinnerId, Dictionary<uint, double> aggregateByEntryId)
            {
                _isSingleFile = isSingleFile;
                _precursorQByWinnerId = precursorQByWinnerId;
                _peptideQByPeptide = peptideQByPeptide;
                _clampFloors = clampFloors;
                _pepByWinnerId = pepByWinnerId;
                _aggregateByEntryId = aggregateByEntryId;
            }

            public void Resolve(uint entryId, string peptide, bool isDecoy, double runPrecursorQ, double runPeptideQ,
                out double precursorQ, out double peptideQ, out double pep, out double aggregateScore)
            {
                precursorQ = _isSingleFile
                    ? runPrecursorQ
                    : (_precursorQByWinnerId.TryGetValue(entryId, out double epv) ? epv : 1.0);
                if (_clampFloors.TryGetByEntryId(entryId, out double floorPrec) && floorPrec > precursorQ)
                    precursorQ = floorPrec;

                peptideQ = _isSingleFile
                    ? runPeptideQ
                    : (_peptideQByPeptide.TryGetValue(peptide, out double epev) ? epev : 1.0);
                if (!string.IsNullOrEmpty(peptide) &&
                    _clampFloors.TryGetByPeptide((peptide, isDecoy), out double floorPept) && floorPept > peptideQ)
                    peptideQ = floorPept;

                pep = _pepByWinnerId.TryGetValue(entryId, out double pv) ? pv : 1.0;

                // Every row pass 1 adds keys its (base_id, side) best, and a base_id plus its side
                // IS the entry_id, so a miss means a row whose decoy flag disagrees with its
                // entry_id's decoy bit. The per-row walk this replaced fell back to that row's own
                // score, which is not a property of the entry; refuse instead.
                if (!_aggregateByEntryId.TryGetValue(entryId, out aggregateScore))
                {
                    throw new InvalidOperationException(string.Format(
                        @"Experiment aggregate score missing for entry_id {0} (decoy flag {1}): " +
                        @"its decoy flag disagrees with its entry_id.", entryId, isDecoy));
                }
            }
        }

        /// <summary>
        /// What a diagnostics-walk lane hands the in-order merge for one file: its row count and
        /// what <see cref="IFdrFirstPassSink.PrepareRowFold"/> returned for it.
        /// </summary>
        private sealed class FoldFile
        {
            public FoldFile(int count, object prepared)
            {
                Count = count;
                Prepared = prepared;
            }

            public int Count { get; }
            public object Prepared { get; }
        }

        /// <summary>
        /// Elapsed time per named bucket, safe to add to from several lanes at once. Callers read
        /// the clock once per file and chain the timestamp, so a bucket costs two counter reads per
        /// file however many rows it covers.
        /// </summary>
        private sealed class LaneCost
        {
            private readonly long[] _ticks;

            public LaneCost(int buckets)
            {
                _ticks = new long[buckets];
            }

            /// <summary>Adds the time since <paramref name="start"/> to the bucket and returns
            /// now, to start the next one.</summary>
            public long Stop(int bucket, long start)
            {
                long now = Stopwatch.GetTimestamp();
                Interlocked.Add(ref _ticks[bucket], now - start);
                return now;
            }

            public double Seconds(int bucket)
            {
                return Interlocked.Read(ref _ticks[bucket]) / (double)Stopwatch.Frequency;
            }
        }
    }
}
