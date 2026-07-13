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
        /// Sidecar carrying the fully-built pass-1 <see cref="ModelDiagnosticsData"/>
        /// from FirstJoinTask (pre-compaction) forward to MergeNodeTask, where the
        /// pass-2 (final reported pool) FDP views are appended and the page is
        /// re-rendered. The pass-1 pool and trained model are gone by MergeNode, so
        /// the data model is stashed on disk rather than recomputed; deleted once
        /// consumed. A JSON round-trip (Newtonsoft, camelCase, NaN/Infinity as
        /// literals) so it reloads into the same object graph the HTML embeds.
        /// </summary>
        private const string DataSidecarSuffix = ".model-diagnostics.data.json";

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
            Action<string> logInfo)
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
                    entrapmentRatio, config.RunFdr, config.FdrLevel);
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

                string outPath = RenderAndWrite(data, config);
                // Stash the pass-1 data so MergeNodeTask can append the pass-2
                // (final reported pool) FDP views and re-render one page with both.
                WriteDataSidecar(data, config);

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
        /// (MergeNodeTask's <c>RescoredEntries</c>), append them, and re-render the
        /// page so its FDR-calibration view selector offers both passes. Uses the
        /// same library-derived classification / pairing as pass 1, so the HTML
        /// pass-2 curve matches stock FDRBench (<c>--fdrbench-pass 2</c>) by
        /// construction. A no-op (with a log line) if the sidecar is absent -- the
        /// pass-1 page FirstJoin already wrote then stands unchanged. Any failure is
        /// logged and swallowed; a diagnostics artifact never aborts a real run.
        /// </summary>
        public static void WritePass2AndFinalize(
            IReadOnlyList<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            FeatureContributions pass2Contributions,
            IReadOnlyDictionary<uint, LibraryEntry> libraryById,
            OspreyConfig config,
            Action<string> logInfo)
        {
            try
            {
                string sidecarPath = ResolveSidecarPath(config);
                var data = ReadDataSidecar(sidecarPath);
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
                data.Pass2 = ModelDiagnosticsData.BuildPass2(
                    perFileEntries, pass2Contributions, classByBaseId, pairByBaseId,
                    entrapmentRatio, config.RunFdr, config.FdrLevel);

                string outPath = RenderAndWrite(data, config);
                // Consume the FirstJoin -> MergeNode hand-off sidecar unconditionally
                // once the report is finalized (deleting it in every path avoids
                // leaving a stray .data.json in the output directory).
                TryDelete(sidecarPath);
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

        /// <summary>Render the data model to the self-contained HTML page and write it; returns the path.</summary>
        private static string RenderAndWrite(ModelDiagnosticsData data, OspreyConfig config)
        {
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

        private static void WriteDataSidecar(ModelDiagnosticsData data, OspreyConfig config)
        {
            string path = ResolveSidecarPath(config);
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            // Atomic write: MergeNode consumes this sidecar, so a partial write must
            // never surface as a corrupt pass-1 data model.
            using (var saver = new FileSaver(path))
            {
                File.WriteAllText(saver.SafeName, JsonConvert.SerializeObject(data, SidecarSettings));
                saver.Commit();
            }
        }

        private static ModelDiagnosticsData ReadDataSidecar(string path)
        {
            if (!File.Exists(path))
                return null;
            return JsonConvert.DeserializeObject<ModelDiagnosticsData>(
                File.ReadAllText(path), SidecarSettings);
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* leftover sidecar is harmless; FirstJoin rewrites it each run */ }
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
        private static void BuildClassificationFromLibrary(
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

        /// <summary>Mask clearing the decoy high bit to get the shared target/decoy base-id.</summary>
        private const uint BASE_ID_MASK = 0x7FFFFFFF;

        private static string OutputStem(OspreyConfig config)
        {
            if (!string.IsNullOrEmpty(config.OutputBlib))
                return Path.GetFileNameWithoutExtension(config.OutputBlib);
            return @"osprey";
        }

        private static string ResolveReportPath(OspreyConfig config)
        {
            return Path.Combine(ResolveOutputDir(config), OutputStem(config) + HtmlSuffix);
        }

        private static string ResolveSidecarPath(OspreyConfig config)
        {
            return Path.Combine(ResolveOutputDir(config), OutputStem(config) + DataSidecarSuffix);
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
