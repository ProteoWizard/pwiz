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
        /// Build and write the report. Called from the Stage 5 boundary where the
        /// per-file <see cref="FdrEntry"/> lists are scored and q-valued (pre
        /// compaction, so decoys and entrapment are still present).
        /// </summary>
        public static void Write(
            IReadOnlyList<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            FeatureContributions contributions,
            IReadOnlyDictionary<uint, LibraryEntry> libraryById,
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
                    entrapmentRatio, config.RunFdr, config.FdrLevel.ToString());
                data.GeneratedUtc = DateTime.UtcNow.ToString(
                    @"yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
                data.OspreyVersion = OspreyVersion.DisplayVersion;
                data.OutputName = OutputStem(config);

                if (data.NUnclassified > 0)
                {
                    logInfo(string.Format(
                        @"[MODEL-DIAGNOSTICS] {0} non-decoy precursors could not be classified as target vs entrapment and are excluded from the FDP (expected 0 with library classification)",
                        data.NUnclassified));
                }

                string html = ModelDiagnosticsHtml.Render(data);
                string outPath = ResolveReportPath(config);
                string dir = Path.GetDirectoryName(outPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(outPath, html);

                logInfo(string.Format(@"[MODEL-DIAGNOSTICS] wrote model diagnostics report: {0}", outPath));
            }
            catch (Exception ex)
            {
                // Never let a diagnostics-only artifact take down a real run.
                logInfo(string.Format(@"[MODEL-DIAGNOSTICS] report generation failed: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Build the entrapment classification keyed by library base-id, from the
        /// library Osprey actually searched -- the single source of truth. Each
        /// target-side entry is classed target / p_target from its own protein
        /// accessions (a <c>_p_target</c> marker means entrapment); decoys share
        /// their target's base-id and are resolved downstream from the entry's
        /// is_decoy flag. Because every searched peptide is classified, nothing is
        /// dropped -- unlike keying off an external manifest that may not cover the
        /// whole library.
        ///
        /// The entrapment-to-target PAIRING (for the paired FDP estimator) still
        /// comes from the optional external manifest, because the per-peptide
        /// pairing token is stripped from library accessions during protein
        /// parsimony; peptides the manifest does not cover simply go unpaired
        /// (they still count in the combined / lower-bound estimators). When a
        /// manifest is supplied, its per-sequence class is cross-checked against
        /// the library and any disagreement or shortfall is logged -- the
        /// consistency assertion the two-tool generation pipeline lacks.
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

            int nTarget = 0, nPTarget = 0;
            classByBaseId = new Dictionary<uint, EntrapmentClass>();
            pairByBaseId = new Dictionary<uint, uint>();
            foreach (var kv in libraryById)
            {
                var lib = kv.Value;
                if (lib == null)
                    continue;
                // Skip decoy-side entries: their base-id equals their target's,
                // which the target-side entry below already classifies. Detect
                // from the accession (authoritative) rather than the is_decoy flag.
                if (EntrapmentLibraryClassifier.IsDecoySide(lib.ProteinIds))
                    continue;
                uint baseId = kv.Key & BASE_ID_MASK;
                bool entrap = EntrapmentLibraryClassifier.IsEntrapment(lib.ProteinIds);
                classByBaseId[baseId] = entrap ? EntrapmentClass.PTarget : EntrapmentClass.Target;
                if (entrap) nPTarget++; else nTarget++;
            }
            if (classByBaseId.Count == 0)
            {
                classByBaseId = null;
                pairByBaseId = null;
                return;
            }
            if (nTarget > 0)
                entrapmentRatio = (double)nPTarget / nTarget;

            // Pairing + consistency cross-check from the optional external manifest.
            string path = config.DecoyPairingManifestPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;
            try
            {
                var manifest = DecoyPairingManifest.FromTsv(path);
                var seqKind = new Dictionary<string, EntrapmentClass>(StringComparer.Ordinal);
                foreach (var kv in manifest.Kinds())
                {
                    if (kv.Value == PeptideKind.Target || kv.Value == PeptideKind.PTarget)
                        seqKind[kv.Key] = MapKind(kv.Value);
                }
                var seqPair = new Dictionary<string, uint>(StringComparer.Ordinal);
                foreach (var kv in manifest.PairIndices())
                    seqPair[kv.Key] = kv.Value;

                int notInManifest = 0, disagree = 0;
                foreach (var kv in libraryById)
                {
                    var lib = kv.Value;
                    if (lib == null || lib.Sequence == null ||
                        EntrapmentLibraryClassifier.IsDecoySide(lib.ProteinIds))
                        continue;
                    uint baseId = kv.Key & BASE_ID_MASK;
                    if (seqPair.TryGetValue(lib.Sequence, out uint pairIdx))
                        pairByBaseId[baseId] = pairIdx;
                    if (seqKind.TryGetValue(lib.Sequence, out var manCls))
                    {
                        if (classByBaseId.TryGetValue(baseId, out var libCls) && libCls != manCls)
                            disagree++;
                    }
                    else
                    {
                        notInManifest++;
                    }
                }
                if (notInManifest > 0 || disagree > 0)
                {
                    logInfo(string.Format(
                        @"[MODEL-DIAGNOSTICS] pairing manifest is not consistent with the searched library: " +
                        @"{0} target/entrapment peptides absent from the manifest, {1} classified differently. " +
                        @"Classifying from the library (authoritative); the manifest supplies pairing only.",
                        notInManifest, disagree));
                }
            }
            catch (Exception ex)
            {
                logInfo(string.Format(
                    @"[MODEL-DIAGNOSTICS] could not read pairing manifest for pairing/cross-check ({0}); " +
                    @"proceeding with library classification, no paired estimator",
                    ex.Message));
                pairByBaseId = new Dictionary<uint, uint>();
            }
        }

        /// <summary>Mask clearing the decoy high bit to get the shared target/decoy base-id.</summary>
        private const uint BASE_ID_MASK = 0x7FFFFFFF;

        private static EntrapmentClass MapKind(PeptideKind kind)
        {
            switch (kind)
            {
                case PeptideKind.Target: return EntrapmentClass.Target;
                case PeptideKind.Decoy: return EntrapmentClass.Decoy;
                case PeptideKind.PTarget: return EntrapmentClass.PTarget;
                case PeptideKind.PDecoy: return EntrapmentClass.PDecoy;
                default: return EntrapmentClass.Unknown;
            }
        }

        private static string OutputStem(OspreyConfig config)
        {
            if (!string.IsNullOrEmpty(config.OutputBlib))
                return Path.GetFileNameWithoutExtension(config.OutputBlib);
            return @"osprey";
        }

        private static string ResolveReportPath(OspreyConfig config)
        {
            string dir = config.OutputDir;
            if (string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(config.OutputBlib))
                dir = Path.GetDirectoryName(Path.GetFullPath(config.OutputBlib));
            if (string.IsNullOrEmpty(dir))
                dir = Directory.GetCurrentDirectory();
            return Path.Combine(dir, OutputStem(config) + HtmlSuffix);
        }
    }
}
