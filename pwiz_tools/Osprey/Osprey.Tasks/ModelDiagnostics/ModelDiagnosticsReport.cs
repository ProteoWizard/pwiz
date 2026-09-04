/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;
using pwiz.Osprey.FDR.ModelDiagnostics;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Tasks.ModelDiagnostics
{
    /// <summary>
    /// Orchestrates the <c>--model-diagnostics</c> HTML report: loads the
    /// optional pairing manifest, builds the pure
    /// <see cref="ModelDiagnosticsData"/> model from first-pass results, renders
    /// the self-contained HTML page, and writes it beside the run's output. A
    /// user-facing deliverable that lives off the default output path -- any
    /// failure is logged and swallowed so it can never abort a real run.
    /// </summary>
    public static class ModelDiagnosticsReport
    {
        public const string HtmlSuffix = ".model-diagnostics.html";

        /// <summary>
        /// The pass-1 <see cref="ModelDiagnosticsData"/>, written by FirstPassFdrTask when its
        /// score pass ends. An experiment-wide PRODUCT in the doc-00 sense: committed through
        /// <see cref="FileSaver"/> so presence proves completeness (P8), written once and never
        /// revisited (P11), and stamped with FirstPassFDR's own validity key (P9).
        ///
        /// <para>It used to be a hand-off that SecondPassFdrTask DELETED once consumed, which is
        /// the single line that made a finished run un-re-renderable: nothing was left to render
        /// from, so <c>--task ModelDiagnostics</c> had to re-run the pipeline to rebuild what it
        /// had just discarded. It is now retained, which is most of what makes that task a pure
        /// render.</para>
        ///
        /// <para>A JSON round-trip (Newtonsoft, camelCase, NaN/Infinity as literals) so it
        /// reloads into the same object graph the HTML embeds.</para>
        /// </summary>
        private const string Pass1SidecarSuffix = ".1st-pass.model-diagnostics.json";

        /// <summary>
        /// The pass-2 (final reported pool) bundle alone - <see cref="ModelDiagnosticsData.Pass2"/>
        /// and nothing else - written by SecondPassFdrTask when pass 2 ends. A separate file
        /// rather than a revisit of <see cref="Pass1SidecarSuffix"/>, which is P12 for this
        /// feature: a column lives in the file written by the phase that computes it, and the
        /// pass-2 views are computed by pass 2. Absence therefore means "pass 2 has not run",
        /// never "pass 2 had nothing to say" (P13).
        /// </summary>
        private const string Pass2SidecarSuffix = ".2nd-pass.model-diagnostics.json";

        private static readonly JsonSerializerSettings SidecarSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Include,
            Formatting = Formatting.None,
            // Symbol writes NaN / Infinity as bare literals that the reader parses
            // back to double (empty win-fraction bins are NaN); a round-trip the
            // HTML's String float handling would not survive.
            FloatFormatHandling = FloatFormatHandling.Symbol,
            FloatParseHandling = FloatParseHandling.Double,
        };

        /// <summary>
        /// Build and write the report. Called from the Stage 5 boundary where the
        /// per-file <see cref="FdrEntry"/> lists are scored and q-valued (pre
        /// compaction, so decoys and entrapment are still present).
        /// </summary>
        public static void Write(
            IReadOnlyList<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            FeatureContributions contributions,
            IReadOnlyDictionary<uint, LibraryEntry> libraryById,
            ModelDiagnosticsData.CalibrationData cal,
            OspreyConfig config,
            Action<string> logInfo,
            string validityKey = null)
        {
            try
            {
                Dictionary<uint, EntrapmentClass> classByBaseId;
                Dictionary<uint, uint> pairByBaseId;
                double entrapmentRatio;
                BuildClassificationFromLibrary(config, libraryById, logInfo,
                    out classByBaseId, out pairByBaseId, out entrapmentRatio);

                var data = ModelDiagnosticsData.Build(
                    perFileEntries, contributions, classByBaseId, pairByBaseId,
                    entrapmentRatio, config.RunFdr, config.FdrLevel,
                    BuildPrecursorMzLookup(libraryById));
                // The CAL view: per-file calibration diagnostics captured at Stage 3
                // (null when none were captured -- a resumed run, or no files calibrated).
                // Serialized into the pass-1 data sidecar below, so it round-trips into
                // WritePass2AndFinalize's reloaded object graph and survives to the final page.
                data.Cal = cal;
                // On a resumed / rehydrated run the first-pass SVM is not retrained
                // (q-values come from sidecars), so there is no trained model to show.
                // Surface it rather than silently emitting a blank Model tab.
                if (contributions == null)
                    logInfo(@"[MODEL-DIAGNOSTICS] first-pass model not retrained on this run " +
                            @"(resumed/rehydrated); the Model tab's feature table and per-feature " +
                            @"distributions are unavailable. Clear the 1st-pass FDR sidecars to force a retrain.");
                data.GeneratedUtc = DateTime.UtcNow.ToString(
                    @"yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
                data.OspreyVersion = OspreyVersion.DisplayVersion;
                data.OutputName = OutputStem(config);

                // Unmatched entrapment excluded from the FDP is reported by
                // EntrapmentPairing.LogSummary during classification; no separate
                // NUnclassified warning needed (it counts the same intended drops).

                // The pass-1 product first, then the page: the page is a view of it, so an
                // interruption between the two leaves the artifact that can rebuild the view
                // rather than a view with nothing behind it.
                WritePass1Sidecar(data, config, validityKey, logInfo);
                string outPath = RenderAndWrite(data, config);

                logInfo(string.Format(@"[MODEL-DIAGNOSTICS] wrote model diagnostics report: {0}", outPath));
            }
            catch (Exception ex)
            {
                // Never let a diagnostics-only artifact take down a real run.
                logInfo(string.Format(@"[MODEL-DIAGNOSTICS] report generation failed: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Projection-path counterpart of <see cref="Write"/>: build and write the pass-1 report
        /// from the streamed <see cref="ModelDiagnosticsData.Accumulator"/> (fed per-row by the
        /// first-pass score sink) instead of the resident pre-compaction pool that OOM'd an
        /// 82-file run at FirstPassFDR. The accumulator already holds the entrapment classification
        /// (built by <see cref="BuildClassificationFromLibrary"/> when it was constructed, and
        /// logged once there), so this only assembles the data model, attaches the CAL view + run
        /// metadata, renders the HTML, and stashes the pass-1 data sidecar for SecondPassFdrTask's
        /// pass-2 enrichment. Byte-identical to <see cref="Write"/> on the same input: the
        /// accumulator's streamed reductions reproduce the resident reductions (they are
        /// order-independent). Any failure is logged and swallowed; a diagnostics artifact never
        /// aborts a real run.
        ///
        /// <c>coAssignment</c> is the pass-1 peak co-assignment panel (issue #4522), built by
        /// <see cref="PeakCoAssignmentSource"/> off the per-file FDR sidecars rather than the
        /// streamed fold, which carries no apex RT. Null leaves the panel out.
        /// </summary>
        public static void WriteFromAccumulator(
            ModelDiagnosticsData.Accumulator accumulator,
            FeatureContributions contributions,
            ModelDiagnosticsData.CalibrationData cal,
            OspreyConfig config,
            Action<string> logInfo,
            ModelDiagnosticsData.CoAssignmentData coAssignment = null,
            string validityKey = null)
        {
            try
            {
                var data = accumulator.Build(contributions);
                data.CoAssignment = coAssignment;
                // The CAL view: per-file calibration diagnostics captured at Stage 3 (null when
                // none were captured -- a resumed run, or no files calibrated). Serialized into the
                // pass-1 data sidecar below so it round-trips into WritePass2AndFinalize's reloaded
                // object graph and survives to the final page (same as Write).
                data.Cal = cal;
                if (contributions == null)
                    logInfo(@"[MODEL-DIAGNOSTICS] first-pass model not retrained on this run " +
                            @"(resumed/rehydrated); the Model tab's feature table and per-feature " +
                            @"distributions are unavailable. Clear the 1st-pass FDR sidecars to force a retrain.");
                data.GeneratedUtc = DateTime.UtcNow.ToString(
                    @"yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
                data.OspreyVersion = OspreyVersion.DisplayVersion;
                data.OutputName = OutputStem(config);

                // The pass-1 product first, then the page: the page is a view of it, so an
                // interruption between the two leaves the artifact that can rebuild the view
                // rather than a view with nothing behind it.
                WritePass1Sidecar(data, config, validityKey, logInfo);
                string outPath = RenderAndWrite(data, config);

                logInfo(string.Format(@"[MODEL-DIAGNOSTICS] wrote model diagnostics report: {0}", outPath));
            }
            catch (Exception ex)
            {
                // Never let a diagnostics-only artifact take down a real run.
                logInfo(string.Format(@"[MODEL-DIAGNOSTICS] report generation failed: {0}", ex.Message));
            }
        }

        /// <summary>
        /// End-of-run enrichment: reload the pass-1 data sidecar, compute the
        /// pass-2 (final reported pool) FDP calibration views from the
        /// post-compaction, second-pass-q-valued <paramref name="perFileEntries"/>
        /// (SecondPassFdrTask's <c>RescoredEntries</c>), append them, and re-render the
        /// page so its FDR-calibration view selector offers both passes. Uses the
        /// same library-derived classification / pairing as pass 1, so the HTML
        /// pass-2 curve matches stock FDRBench (<c>--fdrbench-pass 2</c>) by
        /// construction. A no-op (with a log line) if the sidecar is absent -- the
        /// pass-1 page FirstPassFDR already wrote then stands unchanged. Any failure is
        /// logged and swallowed; a diagnostics artifact never aborts a real run.
        /// </summary>
        public static void WritePass2AndFinalize(
            IReadOnlyList<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            FeatureContributions pass2Contributions,
            IReadOnlyDictionary<uint, LibraryEntry> libraryById,
            OspreyConfig config,
            Action<string> logInfo,
            HashSet<uint> stratumBaseIds = null,
            string validityKey = null)
        {
            try
            {
                var data = ReadJson<ModelDiagnosticsData>(ResolvePass1SidecarPath(config));
                if (data == null)
                {
                    logInfo(@"[MODEL-DIAGNOSTICS] pass-1 data sidecar not found; pass-2 enrichment skipped (pass-1 page stands).");
                    return;
                }

                Dictionary<uint, EntrapmentClass> classByBaseId;
                Dictionary<uint, uint> pairByBaseId;
                double entrapmentRatio;
                BuildClassificationFromLibrary(config, libraryById, logInfo,
                    out classByBaseId, out pairByBaseId, out entrapmentRatio);

                // Build the complete pass-2 (final reported pool) bundle -- every
                // pass-dependent card recomputed on this post-compaction, second-pass
                // q-valued pool -- so the page's top-level Pass 1 / Pass 2 switch can
                // re-source the whole page. The structural half is null under
                // confidence-transfer mode (pass2Contributions == null); the q-driven
                // half is always built (FdpViews empty without an entrapment pool).
                // These two steps shared a 71 s silence on the 82-file SEA-AD run of 2026-08-14,
                // between the classification's [ENTRAPMENT] line and "finalized report" (#4571).
                // BuildPass2 owns essentially all of it and carries its own per-card
                // ProgressReporter; no heading is added here, because a reporter already prints
                // one and then only as many percent lines as the elapsed time needs, while a
                // heading at this call site would print unconditionally on every run forever.
                // Measured 2026-08-15: the render that follows completes inside the same second
                // it starts, so it gets no line at all.
                data.Pass2 = ModelDiagnosticsData.BuildPass2(
                    perFileEntries, pass2Contributions, classByBaseId, pairByBaseId,
                    entrapmentRatio, config.RunFdr, config.FdrLevel,
                    BuildPrecursorMzLookup(libraryById), stratumBaseIds);

                // Pass 2's own product, written before the page for the same reason pass 1's is.
                // NOTHING is deleted here and pass1.json is not rewritten: the two files are
                // immutable products of different phases, and the page below is the only thing
                // this method overwrites.
                string pass2Path = ResolvePass2SidecarPath(config);
                WriteJson(pass2Path, data.Pass2);
                StampProduct(pass2Path, SecondPassTaskName, validityKey, logInfo);
                string outPath = RenderAndWrite(data, config);
                int pass2ViewCount = data.Pass2?.FdpViews?.Count ?? 0;
                logInfo(string.Format(
                    @"[MODEL-DIAGNOSTICS] finalized report ({0} pass-2 FDR view(s); pass-2 model {1}); re-wrote: {2}",
                    pass2ViewCount, data.Pass2?.Model != null ? @"included" : @"n/a", outPath));
            }
            catch (Exception ex)
            {
                logInfo(string.Format(@"[MODEL-DIAGNOSTICS] pass-2 enrichment failed: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Re-render the page from the diagnostics PRODUCTS already on disk, doing no analysis
        /// of any kind: read <c>1st-pass.model-diagnostics.json</c>, attach <c>2nd-pass.model-diagnostics.json</c> if it is there, render.
        /// Returns false when there is no pass-1 product, which is the caller's signal that one
        /// has to be folded before there is anything to render.
        ///
        /// <para>This is the whole of <c>--task ModelDiagnostics</c> on a run that has already
        /// produced its diagnostics. It was impossible before the pass-1 product stopped being
        /// deleted once consumed: with nothing left on disk, the only way to rebuild the page was
        /// to re-run the pipeline that had produced the data - which is why the task materialised
        /// the survivor pool and met Stage 7's wall at 446 runs.</para>
        ///
        /// <para>A page rendered from pass 1 alone is a COMPLETE statement of the first pass, not
        /// a truncated report: the two passes are separate products and pass 1's is whole as soon
        /// as FirstPassFDR ends. What the reader must not be left to infer is that pass 2 is
        /// missing, which is what <see cref="ModelDiagnosticsData.Completeness"/> puts in the page
        /// itself rather than only in a console line.</para>
        /// </summary>
        public static bool TryRenderFromProducts(OspreyConfig config, Action<string> logInfo)
        {
            string pass1Path = ResolvePass1SidecarPath(config);
            if (!File.Exists(pass1Path))
                return false;
            if (!DescribesTheFirstPassOnDisk(config, pass1Path))
            {
                // Present but not OURS. Rendering it would answer a question about a different
                // library, a different parameter set or a different pass-2 arm while looking
                // exactly like an answer about this one, so refuse rather than mislead - and
                // fall through to the fold, which rebuilds it correctly.
                logInfo(@"[MODEL-DIAGNOSTICS] the pass-1 diagnostics product on disk was written " +
                        @"for a different analysis (validity key mismatch); rebuilding it.");
                return false;
            }
            var data = ReadJson<ModelDiagnosticsData>(pass1Path);
            if (data == null)
                return false;
            data.Pass2 = ReadJson<ModelDiagnosticsData.Pass2Data>(ResolvePass2SidecarPath(config));
            string outPath = RenderAndWrite(data, config);
            if (data.Pass2 == null)
            {
                logInfo(@"[MODEL-DIAGNOSTICS] second-pass results are not on disk, so this report " +
                        @"covers the FIRST PASS ONLY and no pass-2 diagnostics can be generated. " +
                        @"Re-run this task once the analysis completes to add them.");
            }
            logInfo(string.Format(
                @"[MODEL-DIAGNOSTICS] re-rendered from completed analysis state: {0}", outPath));
            return true;
        }

        /// <summary>
        /// Whether a COMPLETED first pass is on disk for this configuration, which is the
        /// precondition for producing any diagnostics at all. Asks for the analysis-wide 1st-pass
        /// experiment sidecar: FirstPassFDR declares it as an output and writes it at the end of
        /// protein FDR, so its presence is the single artifact that says the first pass finished
        /// rather than was interrupted part way.
        ///
        /// <para><c>--task ModelDiagnostics</c> ABORTS when this is false, which is the doc-00
        /// precedent for a missing relay input - the <c>.1st-pass.model.json</c> one that fails
        /// with a ConfigError - and not the <c>fdr_experiment.bin</c> one that logs and continues
        /// into a wrong answer that looks like a right one.</para>
        /// </summary>
        public static bool HasCompletedFirstPass(OspreyConfig config)
        {
            string path = FirstPassExperimentSidecarPath(config);
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }

        /// <summary>
        /// Whether the SECOND pass completed for this configuration, asked of its own
        /// analysis-wide experiment sidecar rather than of the diagnostics product.
        ///
        /// <para>The two questions are different and conflating them states a falsehood in the
        /// page. A run that finished completely BEFORE the diagnostics products were retained
        /// has a second pass and no <c>2nd-pass.model-diagnostics.json</c>; reporting that as
        /// "the second pass has not completed" would be a confident wrong answer about a
        /// finished analysis. The honest statement there is that the pass-2 VIEWS are missing
        /// and how to get them, which is what the banner says when this returns true.</para>
        /// </summary>
        public static bool HasCompletedSecondPass(OspreyConfig config)
        {
            string path = FdrExperimentSidecar.PathFor(
                config.OutputBlib, ScoringTaskShared.ArtifactSiblingPath(config),
                FdrScoresSidecar.Pass.SecondPass);
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }

        /// <summary>Task name stamped on both first-pass products; see <see cref="FirstPassFdrTask"/>.</summary>
        private const string FirstPassTaskName = @"FirstPassFDR";

        /// <summary>Task name stamped on the pass-2 product; see <see cref="SecondPassFdrTask"/>.</summary>
        private const string SecondPassTaskName = @"SecondPassFDR";

        private static string FirstPassExperimentSidecarPath(OspreyConfig config)
        {
            return FdrExperimentSidecar.PathFor(
                config.OutputBlib, ScoringTaskShared.ArtifactSiblingPath(config),
                FdrScoresSidecar.Pass.FirstPass);
        }

        /// <summary>
        /// Whether the pass-1 diagnostics product describes the first pass that is on disk NOW.
        /// Both were stamped by FirstPassFDR with the same key, so equal keys is the whole test -
        /// and it is answerable without a pipeline context, which the render path does not have.
        ///
        /// <para>Refusing when the stamp is absent is deliberate. A product written before this
        /// stamping existed cannot be shown to belong to this analysis, and "cannot tell" has to
        /// resolve to "rebuild it" - the same conservative direction
        /// <see cref="TaskValiditySidecar.IsValid"/> takes for a missing sidecar.</para>
        /// </summary>
        private static bool DescribesTheFirstPassOnDisk(OspreyConfig config, string pass1Path)
        {
            string experimentPath = FirstPassExperimentSidecarPath(config);
            if (!TaskValiditySidecar.TryReadValidityKey(experimentPath, FirstPassTaskName, out string key))
                return false;
            return TaskValiditySidecar.IsValid(pass1Path, FirstPassTaskName, key);
        }

        /// <summary>
        /// Render the data model to the self-contained HTML page and write it; returns the path.
        ///
        /// <para>The page is the one thing in this design that is OVERWRITTEN, and it is safe to
        /// overwrite for the reason the JSON products are not: it is a pure projection of them
        /// (a doc-00 CACHE, derivable again from a source that still exists), so re-rendering
        /// cannot lose information.</para>
        ///
        /// <para><paramref name="runsContributed"/> is how many runs actually reached the pass-1
        /// fold, which a caller folding from on-disk artifacts knows and the data model does not:
        /// <see cref="ModelDiagnosticsData.FileCount"/> is the cohort the fold was HANDED. They
        /// differ exactly when the analysis is unfinished, which is the case the banner exists
        /// for. Negative leaves it at <c>FileCount</c>.</para>
        /// </summary>
        private static string RenderAndWrite(ModelDiagnosticsData data, OspreyConfig config,
            int runsContributed = -1)
        {
            data.Completeness = BuildCompleteness(data, runsContributed,
                HasCompletedSecondPass(config));
            string html = ModelDiagnosticsHtml.Render(data);
            string outPath = ResolveReportPath(config);
            string dir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            // Atomic write (temp + rename) so an interrupted run can't leave a truncated report.
            using (var saver = new FileSaver(outPath))
            {
                File.WriteAllText(saver.SafeName, html);
                saver.Commit();
            }
            return outPath;
        }

        /// <summary>
        /// State the page's own scope. Built at render time from what is actually on disk, so a
        /// page that covers only the first pass says so in the artifact rather than only in a
        /// console line the reader no longer has.
        /// </summary>
        internal static ModelDiagnosticsData.CompletenessInfo BuildCompleteness(
            ModelDiagnosticsData data, int runsContributed, bool secondPassCompleted)
        {
            int contributed = runsContributed >= 0 ? runsContributed : data.FileCount;
            bool pass2 = data.Pass2 != null;
            var info = new ModelDiagnosticsData.CompletenessInfo
            {
                Pass2Present = pass2,
                RunsContributed = contributed,
                RunsExpected = data.FileCount,
            };
            if (pass2 && contributed >= data.FileCount)
                return info;
            // Two independent ways to be partial, and a page can be both. Name the one that
            // costs the reader the most first: a missing second pass changes every reported
            // q-value, where a missing run changes only coverage.
            var reasons = new List<string>();
            if (!pass2 && !secondPassCompleted)
            {
                reasons.Add(@"The second pass has not completed, so no pass-2 diagnostics can be " +
                            @"generated and every number on this page is first-pass");
            }
            else if (!pass2)
            {
                // The analysis IS finished - only the pass-2 views are missing, which is what a
                // run completed before the diagnostics products were retained looks like. Saying
                // "the second pass has not completed" here would be a confident wrong answer
                // about a finished analysis.
                reasons.Add(@"The second pass completed but left no diagnostics product, so this " +
                            @"page shows first-pass views only; re-run SecondPassFDR with " +
                            @"--model-diagnostics to add the pass-2 views");
            }
            if (contributed < data.FileCount)
            {
                reasons.Add(string.Format(CultureInfo.InvariantCulture,
                    @"{0} of {1} runs contributed to the first pass", contributed, data.FileCount));
            }
            info.Reason = string.Join(@"; ", reasons);
            return info;
        }

        private static void WritePass1Sidecar(ModelDiagnosticsData data, OspreyConfig config,
            string validityKey, Action<string> logWarning)
        {
            // A FAN-OUT worker must not write this. It is an EXPERIMENT-wide product and a
            // worker holds ONE run, so what it would write is a page-worth of data describing
            // that run under a name claiming to describe the cohort - and it would overwrite the
            // real one the join produced, which is P11 (write-once) broken by a later stage
            // reaching back into an earlier stage's output.
            //
            // Found by regression mode 3 the moment the artifact was added to the HPC relay:
            // "Osprey --task modified 1 file(s) it was given, which no task may do". Before the
            // relay existed each worker quietly wrote its own single-run copy into its own
            // directory, where nothing read it - present, complete, wrong, and invisible.
            // Relaying it is what turned a harmless stray file into a corrupt report, so the
            // guard earned its place here rather than in the relay.
            if (config.NoJoin)
            {
                logWarning(@"[MODEL-DIAGNOSTICS] fan-out worker: not writing the experiment-wide " +
                           @"pass-1 diagnostics product (this node holds one run; FirstPassFDR owns it).");
                return;
            }
            // Serialized without the pass-2 bundle even if one is attached to the in-memory
            // graph. pass1.json is pass 1's product; a caller that has just rendered a two-pass
            // page must not be able to write pass 2's answers into pass 1's file.
            var pass2 = data.Pass2;
            data.Pass2 = null;
            string path = ResolvePass1SidecarPath(config);
            try
            {
                WriteJson(path, data);
            }
            finally
            {
                data.Pass2 = pass2;
            }
            StampProduct(path, FirstPassTaskName, validityKey, logWarning);
        }

        /// <summary>
        /// Stamp a diagnostics product with its producing task's validity key, so a later render
        /// can tell "this describes the analysis on disk" from "this is left over from another
        /// one". No key means no stamp, and an unstamped product is refused by the render rather
        /// than trusted.
        /// </summary>
        private static void StampProduct(string path, string taskName, string validityKey,
            Action<string> logWarning)
        {
            if (string.IsNullOrEmpty(validityKey))
                return;
            PerFileResumeDriver.Stamp(path, taskName, OspreyVersion.Current, validityKey,
                Array.Empty<string>(), logWarning);
        }

        /// <summary>
        /// Atomic write of one diagnostics product. Every consumer relies on presence proving
        /// completeness (P8), so a partial write must never surface as a corrupt data model.
        /// </summary>
        private static void WriteJson(string path, object value)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            using (var saver = new FileSaver(path))
            {
                File.WriteAllText(saver.SafeName, JsonConvert.SerializeObject(value, SidecarSettings));
                saver.Commit();
            }
        }

        private static T ReadJson<T>(string path) where T : class
        {
            if (!File.Exists(path))
                return null;
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(path), SidecarSettings);
        }

        /// <summary>
        /// Build the entrapment classification keyed by library base-id, from the
        /// library Osprey actually searched -- the single source of truth, reconciled
        /// against the external manifest by <see cref="EntrapmentPairing"/>. Each
        /// target-side entry is classed target / p_target from its own protein
        /// accessions (a <c>_p_target</c> marker means entrapment); decoys share their
        /// target's base-id and are resolved downstream from the is_decoy flag.
        ///
        /// Unmatched entrapment (N-terminal-Met-clip artifacts with no target twin)
        /// are excluded here exactly as they are from the emitted FDRBench manifest and
        /// input TSV, so the HTML FDP and FDRBench see the same peptides. Pairing (for
        /// the paired estimator) is the reconciled pairing -- the external manifest for
        /// covered peptides, reconstructed from the library accessions for the extras.
        ///
        /// <paramref name="entrapmentRatio"/> is the library p_target/target count
        /// ratio r (1.0 for a balanced 1-fold entrapment library).
        /// </summary>
        internal static void BuildClassificationFromLibrary(
            OspreyConfig config,
            IReadOnlyDictionary<uint, LibraryEntry> libraryById,
            Action<string> logInfo,
            out Dictionary<uint, EntrapmentClass> classByBaseId,
            out Dictionary<uint, uint> pairByBaseId,
            out double entrapmentRatio)
        {
            classByBaseId = null;
            pairByBaseId = null;
            entrapmentRatio = 1.0;
            if (libraryById == null)
                return;

            // Label this phase so it is not silent: classifying the searched library
            // (6.3M entries on the 82-file Astral run) for model diagnostics ran for
            // minutes at the top of first-pass FDR. Console-only, never affects the
            // classification.
            logInfo(string.Format(@"Classifying {0} library entries for model diagnostics...",
                libraryById.Count));
            var pairing = EntrapmentPairing.Build(libraryById, config.DecoyPairingManifestPath);

            int nTarget = 0, nPTarget = 0;
            classByBaseId = new Dictionary<uint, EntrapmentClass>();
            pairByBaseId = new Dictionary<uint, uint>();
            foreach (var kv in libraryById)
            {
                var lib = kv.Value;
                if (lib == null || lib.Sequence == null)
                    continue;
                // Skip decoy-side entries: their base-id equals their target's, which
                // the target-side entry below classifies (accession is authoritative).
                if (EntrapmentLibraryClassifier.IsDecoySide(lib.ProteinIds))
                    continue;
                // Exclude unmatched entrapment so the HTML FDP counts the same peptides
                // the emitted manifest gives FDRBench.
                if (pairing.ExcludedEntrapment.Contains(lib.Sequence))
                    continue;
                uint baseId = kv.Key & BASE_ID_MASK;
                bool entrap = EntrapmentLibraryClassifier.IsEntrapment(lib.ProteinIds);
                classByBaseId[baseId] = entrap ? EntrapmentClass.PTarget : EntrapmentClass.Target;
                if (entrap) nPTarget++; else nTarget++;
                if (pairing.PairIndexBySeq.TryGetValue(lib.Sequence, out uint pairIdx))
                    pairByBaseId[baseId] = pairIdx;
            }
            if (classByBaseId.Count == 0)
            {
                classByBaseId = null;
                pairByBaseId = null;
                return;
            }
            if (nTarget > 0)
                entrapmentRatio = (double)nPTarget / nTarget;

            pairing.LogSummary(logInfo);
        }

        /// <summary>
        /// Library precursor m/z by FULL entry id (decoy bit included) for the co-assignment
        /// panel, or null when there is no library to read.
        ///
        /// <para>A decoy is NOT always present in this task's library index, while its target twin
        /// always is, and a decoy carries its target's precursor m/z by construction (same
        /// composition), so an unresolved decoy falls back to its base id. Without that fallback
        /// the caller's <c>double.IsNaN(mz)</c> guard drops the row, and the streaming path
        /// measured what that costs: ~97% of detected decoys silently gone (19 counted against
        /// 598) and a decoy rate 30x too low. This lookup feeds the PASS 2 panel - the numbers the
        /// user actually receives - which has no admitted-vs-tallied log check to expose it, so
        /// the two paths must resolve identically. See
        /// <c>PeakCoAssignmentSource.ScanCurrentFile</c>, which does the same thing.</para>
        ///
        /// <para>A lookup rather than a materialized map on purpose: the panel needs m/z only for
        /// DETECTED rows, a small fraction of a library that reaches 6.3M entries on the Astral
        /// runs, and the library is already resident wherever this is called.</para>
        ///
        /// </summary>
        internal static Func<uint, double> BuildPrecursorMzLookup(
            IReadOnlyDictionary<uint, LibraryEntry> libraryById)
        {
            if (libraryById == null)
                return null;
            return entryId =>
            {
                if ((!libraryById.TryGetValue(entryId, out var lib) || lib == null) &&
                    (entryId & LibraryEntry.DECOY_ID_BIT) != 0)
                    libraryById.TryGetValue(entryId & ~LibraryEntry.DECOY_ID_BIT, out lib);
                return lib != null ? lib.PrecursorMz : double.NaN;
            };
        }

        /// <summary>Mask clearing the decoy high bit to get the shared target/decoy base-id.</summary>
        private const uint BASE_ID_MASK = 0x7FFFFFFF;

        private static string OutputStem(OspreyConfig config)
        {
            if (!string.IsNullOrEmpty(config.OutputBlib))
                return Path.GetFileNameWithoutExtension(config.OutputBlib);
            return @"osprey";
        }

        /// <summary>
        /// The report path this run would write, for callers that must reason about the file
        /// before it exists. <see cref="SecondPassFdrTask"/> declares it as an OUTPUT when
        /// <c>--model-diagnostics</c> is on, which is what lets a completed run regenerate a
        /// deleted report: task validity requires every declared output to exist, so a missing
        /// HTML invalidates that one task and nothing else. Without this the flag was inert on
        /// a cached directory - every task reported "outputs valid", and no report appeared.
        /// </summary>
        public static string ReportPath(OspreyConfig config)
        {
            return ResolveReportPath(config);
        }

        private static string ResolveReportPath(OspreyConfig config)
        {
            return Path.Combine(ResolveOutputDir(config), OutputStem(config) + HtmlSuffix);
        }

        /// <summary>
        /// The pass-1 product's path. Public because <see cref="FirstPassFdrTask"/> declares it
        /// as an output when <c>--model-diagnostics</c> is on, which is what puts the diagnostics
        /// inside the resume driver's forward scan rather than beside it.
        /// </summary>
        public static string Pass1SidecarPath(OspreyConfig config)
        {
            return ResolvePass1SidecarPath(config);
        }

        /// <summary>The pass-2 product's path; <see cref="SecondPassFdrTask"/> declares it.</summary>
        public static string Pass2SidecarPath(OspreyConfig config)
        {
            return ResolvePass2SidecarPath(config);
        }

        private static string ResolvePass1SidecarPath(OspreyConfig config)
        {
            return Path.Combine(ResolveOutputDir(config), OutputStem(config) + Pass1SidecarSuffix);
        }

        private static string ResolvePass2SidecarPath(OspreyConfig config)
        {
            return Path.Combine(ResolveOutputDir(config), OutputStem(config) + Pass2SidecarSuffix);
        }

        private static string ResolveOutputDir(OspreyConfig config)
        {
            string dir = config.OutputDir;
            if (string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(config.OutputBlib))
                dir = Path.GetDirectoryName(Path.GetFullPath(config.OutputBlib));
            if (string.IsNullOrEmpty(dir))
                dir = Directory.GetCurrentDirectory();
            return dir;
        }
    }
}
