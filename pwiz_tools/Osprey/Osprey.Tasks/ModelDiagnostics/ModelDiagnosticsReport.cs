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
                LoadManifestMaps(config, libraryById, logInfo,
                    out classByBaseId, out pairByBaseId, out entrapmentRatio);

                var data = ModelDiagnosticsData.Build(
                    perFileEntries, contributions, classByBaseId, pairByBaseId,
                    entrapmentRatio, config.RunFdr, config.FdrLevel.ToString());
                data.GeneratedUtc = DateTime.UtcNow.ToString(
                    @"yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
                data.OspreyVersion = OspreyVersion.DisplayVersion;
                data.OutputName = OutputStem(config);

                if (data.NUnclassified > 0 && data.NClassifiedFromManifest > 0)
                {
                    logInfo(string.Format(
                        @"[MODEL-DIAGNOSTICS] {0} entries classified from the manifest, {1} fell back to is_decoy (sequence-key mismatch if large)",
                        data.NClassifiedFromManifest, data.NUnclassified));
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
        /// Build the FDRBench-equivalent classification keyed by library base-id.
        /// FDRBench identifies each precursor by the library sequence its base-id
        /// resolves to, then looks that sequence up in the pairing manifest
        /// (dropping the row when it is absent). We reproduce that key exactly:
        /// for every target-side library entry (decoys share their target's
        /// base-id and are handled downstream) whose sequence the manifest
        /// classifies as target / p_target, map its base-id to that class and its
        /// pair index. A base-id absent from the result is FDRBench-"invalid".
        /// <paramref name="entrapmentRatio"/> is the manifest p_target/target
        /// count ratio r (1.0 for a balanced library / no manifest).
        /// </summary>
        private static void LoadManifestMaps(
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
            string path = config.DecoyPairingManifestPath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || libraryById == null)
                return;
            try
            {
                var manifest = DecoyPairingManifest.FromTsv(path);

                // sequence -> (kind, pairIndex) for the target-side kinds only.
                // FDRBench's get_peptide_type skips decoy rows, so decoy /
                // p_decoy sequences never enter the classification map.
                var seqKind = new Dictionary<string, EntrapmentClass>(StringComparer.Ordinal);
                foreach (var kv in manifest.Kinds())
                {
                    if (kv.Value == PeptideKind.Target || kv.Value == PeptideKind.PTarget)
                        seqKind[kv.Key] = MapKind(kv.Value);
                }
                var seqPair = new Dictionary<string, uint>(StringComparer.Ordinal);
                foreach (var kv in manifest.PairIndices())
                    seqPair[kv.Key] = kv.Value;

                int nTarget = 0, nPTarget = 0;
                classByBaseId = new Dictionary<uint, EntrapmentClass>();
                pairByBaseId = new Dictionary<uint, uint>();
                foreach (var kv in libraryById)
                {
                    // Target-side entries only (high decoy bit clear). A decoy's
                    // base-id already equals its target's, which is covered here.
                    if ((kv.Key & BASE_ID_MASK) != kv.Key)
                        continue;
                    var lib = kv.Value;
                    if (lib == null || lib.Sequence == null)
                        continue;
                    if (!seqKind.TryGetValue(lib.Sequence, out var cls))
                        continue;
                    classByBaseId[kv.Key] = cls;
                    if (cls == EntrapmentClass.PTarget) nPTarget++; else nTarget++;
                    if (seqPair.TryGetValue(lib.Sequence, out uint pairIdx))
                        pairByBaseId[kv.Key] = pairIdx;
                }
                if (nTarget > 0)
                    entrapmentRatio = (double)nPTarget / nTarget;
            }
            catch (Exception ex)
            {
                logInfo(string.Format(
                    @"[MODEL-DIAGNOSTICS] could not read pairing manifest ({0}); degrading to target/decoy only",
                    ex.Message));
                classByBaseId = null;
                pairByBaseId = null;
                entrapmentRatio = 1.0;
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
