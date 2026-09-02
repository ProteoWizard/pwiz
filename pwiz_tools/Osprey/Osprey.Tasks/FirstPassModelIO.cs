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
using System.IO;
using Newtonsoft.Json;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;
using pwiz.Osprey.IO;
using pwiz.Osprey.ML;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// Persists the trained 1st-pass Percolator model to a small join-wide JSON sidecar
    /// so a distributed <c>--task SecondPassFDR</c> node -- or any resume that
    /// skips 1st-pass training -- can run the frozen 2nd-pass modes
    /// (OSPREY_PASS2_QVALUE=transfer / transfer-compete / protein-compact) without the
    /// in-process <see cref="FirstPassPercolatorModel"/>. Without this SecondPassFDR hits
    /// the <see cref="Pass2FdrSidecar"/> fail-fast ("a distributed SecondPassFDR node
    /// that never trained pass 1").
    ///
    /// Only the slice <see cref="FrozenModelScorer"/> consumes is stored: the feature
    /// standardizer (<see cref="FeatureStandardizer.Means"/>/<see cref="FeatureStandardizer.Stds"/>)
    /// plus the per-fold linear weights and biases. Doubles route through
    /// <see cref="RoundtripDoubleConverter"/> so a reloaded model scores BIT-IDENTICALLY to
    /// the in-process original (the <see cref="FrozenModelScorer.TryCreate"/> fold-average is
    /// applied to the same per-fold values either way).
    ///
    /// GBDT (<c>--fdr-method gbdt</c>) is NOT persisted here: the tree ensembles carry no
    /// linear weights, so <see cref="Save"/> declines and a GBDT SecondPassFDR node keeps the prior
    /// fail-fast behavior (unchanged) until tree serialization is added.
    ///
    /// The <see cref="ProteinCompactStratum"/> base ids live in a SECOND file
    /// (<c>.1st-pass.stratum.json</c>) rather than this one, because a different phase
    /// computes them: the model exists when training ends, the stratum only after first-pass
    /// protein FDR has resolved which proteins carry two detected peptides. Writing one file
    /// would mean holding the model in memory for the whole first pass and persisting it at
    /// the end - which is what made a run killed in the score passes unrecoverable, since the
    /// model it had already trained was nowhere on disk. Each artifact is written when the
    /// phase that produces it ends, and <see cref="LoadFromAny"/> merges the two on read, so
    /// a consumer still sees one <see cref="Sidecar"/>. The stratum file is absent under every
    /// mode but protein-compact, which is what the mode gate below expects.
    /// </summary>
    internal static class FirstPassModelIO
    {
        private const string ModelSuffix = @".1st-pass.model.json";
        private const string StratumSuffix = @".1st-pass.stratum.json";

        /// <summary>Serializable slice of <see cref="PercolatorResults"/> the frozen scorer needs,
        /// plus the pass-1 provenance a SecondPassFDR node cannot otherwise know.</summary>
        private sealed class ModelDto
        {
            public int SchemaVersion { get; set; }
            public int NumFeatures { get; set; }
            public double[] Means { get; set; }
            public double[] Stds { get; set; }
            public double[][] FoldWeights { get; set; }
            public double[] FoldBiases { get; set; }

            /// <summary>Normalized OSPREY_EXPERIMENT_AGG of the process that TRAINED this model.
            /// Deliberately added WITHOUT bumping <see cref="SchemaVersion"/>: it is an optional
            /// additive property, so an older reader ignores it and this reader sees null on an
            /// older file. Bumping the version would instead make every pre-existing sidecar
            /// unreadable, which on a SecondPassFDR node is the hard fail-fast, not a graceful
            /// degradation.</summary>
            public string ExperimentAgg { get; set; }

            /// <summary>The <see cref="ProteinCompactStratum"/> base ids. NO LONGER WRITTEN
            /// here - the stratum moved to its own <c>.1st-pass.stratum.json</c> when the phase
            /// artifacts were split, because protein FDR computes it and training does not.
            /// Still READ, so a directory written before the split keeps its fast-path resume
            /// instead of needing a fresh multi-hour run; <see cref="LoadFromAny"/> prefers the
            /// dedicated file and falls back to this. Removable at the next format bump.</summary>
            public uint[] StratumBaseIds { get; set; }
        }

        /// <summary>Serializable form of the protein-compact stratum: the base ids of every
        /// library precursor belonging to a protein with at least two detected peptides.</summary>
        private sealed class StratumDto
        {
            public int SchemaVersion { get; set; }

            /// <summary>Sorted ascending on write: the in-memory source is a
            /// <see cref="HashSet{T}"/>, whose enumeration order is an implementation detail,
            /// and an artifact that reorders between runs is neither diffable nor safe to
            /// compare byte-wise in the regression golden.</summary>
            public uint[] StratumBaseIds { get; set; }
        }

        /// <summary>What one persisted sidecar carries. A result type rather than a stack of
        /// out parameters because the three values are read together and travel together: the
        /// frozen scorer's model slice, the pass-1 provenance, and the protein-compact
        /// stratum.</summary>
        public sealed class Sidecar
        {
            public PercolatorResults Model { get; set; }
            public string ExperimentAgg { get; set; }
            public HashSet<uint> StratumBaseIds { get; set; }
        }

        /// <summary>
        /// Per-file model sidecar path <c>&lt;parquetDir&gt;/&lt;fileStem&gt;.1st-pass.model.json</c>,
        /// sitting beside the file's other Stage-5 sidecars (<c>.calibration.json</c>,
        /// <c>.1st-pass.fdr_scores.bin</c>, <c>.reconciliation.json</c>). Per-file (not
        /// join-wide) so a distributed <c>--task SecondPassFDR</c> node finds it by the
        /// SAME input-file-stem derivation it uses for every other reconciled sidecar -- the
        /// model is identical across files, so any one copy serves. <paramref name="parquetPath"/>
        /// is the file's score parquet; only its directory is used.
        /// </summary>
        public static string PathFor(string parquetPath, string fileStem)
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(parquetPath)) ?? string.Empty;
            return Path.Combine(dir, fileStem + ModelSuffix);
        }

        /// <summary>
        /// Per-file protein-compact stratum path
        /// <c>&lt;parquetDir&gt;/&lt;fileStem&gt;.1st-pass.stratum.json</c>, resolved exactly as
        /// <see cref="PathFor"/> resolves the model so the two land together and every phase
        /// agrees on where.
        /// </summary>
        public static string StratumPathFor(string parquetPath, string fileStem)
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(parquetPath)) ?? string.Empty;
            return Path.Combine(dir, fileStem + StratumSuffix);
        }

        /// <summary>
        /// Load from the first per-file sidecar that exists among
        /// <paramref name="perFileParquetPaths"/> (stem -&gt; score parquet path), or null when
        /// none is present. The copies are identical, so the first hit is authoritative.
        /// </summary>
        public static Sidecar LoadFromAny(IReadOnlyDictionary<string, string> perFileParquetPaths)
        {
            if (perFileParquetPaths == null)
                return null;
            Sidecar sidecar = null;
            foreach (var kvp in perFileParquetPaths)
            {
                sidecar = Load(PathFor(kvp.Value, kvp.Key));
                if (sidecar != null)
                    break;
            }
            if (sidecar == null)
                return null;

            // The stratum is a SEPARATE artifact written by a later phase, so it is scanned for
            // separately: a run killed between training and protein FDR has the model and no
            // stratum, which is a legitimate state and not a reason to reject the model. The
            // dedicated file wins where it exists; sidecar.StratumBaseIds is whatever the model
            // file carried, which is non-null only for a directory written before the split.
            foreach (var kvp in perFileParquetPaths)
            {
                var stratum = LoadStratum(StratumPathFor(kvp.Value, kvp.Key));
                if (stratum == null)
                    continue;
                sidecar.StratumBaseIds = stratum;
                break;
            }
            return sidecar;
        }

        /// <summary>
        /// Write the frozen-scorer slice of <paramref name="model"/> to <paramref name="path"/>.
        /// Returns false (writing nothing) when the model has no linear weights or standardizer
        /// -- the GBDT path, or an empty/degenerate model -- so the caller does not advertise a
        /// sidecar SecondPassFDR cannot use.
        /// </summary>
        /// <param name="path">Sidecar path to write.</param>
        /// <param name="model">The trained 1st-pass model.</param>
        /// <param name="experimentAgg">Normalized OSPREY_EXPERIMENT_AGG of the training process,
        ///   stamped so a SecondPassFDR node reads the pass-1 arm instead of guessing it from its own
        ///   environment. Comes from the caller (which holds the byproduct) rather than being
        ///   re-read here, so this stays a pure serializer.</param>
        public static bool Save(string path, PercolatorResults model, string experimentAgg)
        {
            if (model?.Standardizer == null ||
                model.FoldWeights == null || model.FoldWeights.Count == 0 ||
                model.FoldBiases == null || model.FoldBiases.Count != model.FoldWeights.Count)
                return false;

            var dto = new ModelDto
            {
                SchemaVersion = 1,
                NumFeatures = model.Standardizer.NumFeatures,
                Means = model.Standardizer.Means,
                Stds = model.Standardizer.Stds,
                FoldWeights = model.FoldWeights.ToArray(),
                FoldBiases = model.FoldBiases.ToArray(),
                ExperimentAgg = experimentAgg,
            };
            WriteJson(path, dto);
            return true;
        }

        /// <summary>
        /// Write the protein-compact stratum to <paramref name="path"/>. Returns false (writing
        /// nothing) when there is no stratum to persist, so the caller does not advertise an
        /// artifact a consumer cannot use.
        ///
        /// <para>Its own file, written when first-pass protein FDR ends, because that is the
        /// phase that computes it - the rule that keeps every artifact write-once. Bundling it
        /// with the model would force both to wait for the later phase.</para>
        /// </summary>
        /// <param name="path">Stratum sidecar path to write.</param>
        /// <param name="stratumBaseIds">The protein-compact stratum base ids. Sorted here so
        ///   the artifact is stable across runs.</param>
        public static bool SaveStratum(string path, HashSet<uint> stratumBaseIds)
        {
            if (stratumBaseIds == null || stratumBaseIds.Count == 0)
                return false;

            var sortedBaseIds = new uint[stratumBaseIds.Count];
            stratumBaseIds.CopyTo(sortedBaseIds);
            Array.Sort(sortedBaseIds); // Array.Sort OK: distinct uint keys, so stability cannot change the result
            WriteJson(path, new StratumDto { SchemaVersion = 1, StratumBaseIds = sortedBaseIds });
            return true;
        }

        /// <summary>
        /// Load a persisted model, or return null when <paramref name="path"/> is absent or
        /// unreadable (the caller then fails fast exactly as it did before persistence existed).
        /// The returned <see cref="PercolatorResults"/> carries only the scorer slice
        /// (standardizer + per-fold weights/biases); <see cref="FrozenModelScorer.TryCreate"/>
        /// needs nothing else.
        ///
        /// <see cref="Sidecar.ExperimentAgg"/> is null when the sidecar predates that field, so
        /// null there means "unknown", never "max" - a caller that gates on the arm must treat it
        /// as unknown rather than assuming the default. <see cref="Sidecar.StratumBaseIds"/> is
        /// null under every mode but protein-compact, and likewise on an older sidecar.
        /// </summary>
        /// <param name="path">Sidecar path to read.</param>
        public static Sidecar Load(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;

            // A corrupt or truncated sidecar must load as null (the documented "unreadable"
            // contract) rather than throw and crash SecondPassFDR: a bad read/parse and a
            // shape-invariant violation both fall back to the pre-persistence fail-fast.
            try
            {
                var settings = new JsonSerializerSettings { Converters = { new RoundtripDoubleConverter() } };
                var dto = JsonConvert.DeserializeObject<ModelDto>(File.ReadAllText(path), settings);
                if (dto == null || dto.SchemaVersion != 1 ||
                    dto.Means == null || dto.Stds == null || dto.Means.Length != dto.Stds.Length ||
                    dto.NumFeatures != dto.Means.Length ||
                    dto.FoldWeights == null || dto.FoldWeights.Length == 0 ||
                    dto.FoldBiases == null || dto.FoldBiases.Length != dto.FoldWeights.Length)
                    return null;

                // Each fold's linear weights must be present and match the feature width,
                // or the frozen scorer would dereference null / index past the end while
                // scoring on SecondPassFDR -- an opaque crash outside this method's
                // documented null-on-unreadable contract.
                foreach (var foldWeights in dto.FoldWeights)
                {
                    if (foldWeights == null || foldWeights.Length != dto.Means.Length)
                        return null;
                }

                return new Sidecar
                {
                    Model = new PercolatorResults
                    {
                        Standardizer = FeatureStandardizer.FromMeansStds(dto.Means, dto.Stds),
                        FoldWeights = new List<double[]>(dto.FoldWeights),
                        FoldBiases = new List<double>(dto.FoldBiases),
                    },
                    ExperimentAgg = dto.ExperimentAgg,
                    StratumBaseIds = dto.StratumBaseIds == null || dto.StratumBaseIds.Length == 0
                        ? null
                        : new HashSet<uint>(dto.StratumBaseIds),
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Load a persisted protein-compact stratum, or null when <paramref name="path"/> is
        /// absent, unreadable or carries no ids. Null-on-unreadable for the same reason
        /// <see cref="Load"/> is: a caller that cannot get the stratum must fall back to
        /// recomputing it, not crash.
        /// </summary>
        /// <param name="path">Stratum sidecar path to read.</param>
        public static HashSet<uint> LoadStratum(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;
            try
            {
                var dto = JsonConvert.DeserializeObject<StratumDto>(File.ReadAllText(path));
                if (dto == null || dto.SchemaVersion != 1 ||
                    dto.StratumBaseIds == null || dto.StratumBaseIds.Length == 0)
                    return null;
                return new HashSet<uint>(dto.StratumBaseIds);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Serialize <paramref name="dto"/> to <paramref name="path"/> through
        /// <see cref="FileSaver"/>, so the artifact is committed by an atomic rename and is
        /// therefore absent or complete - never half-written. LF + trailing newline, matching
        /// the reconciliation.json convention so the artifact is stable across platforms.
        /// </summary>
        private static void WriteJson(string path, object dto)
        {
            string parent = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            var settings = new JsonSerializerSettings { Converters = { new RoundtripDoubleConverter() } };
            string json = JsonConvert.SerializeObject(dto, Formatting.Indented, settings);
            json = json.Replace("\r\n", "\n");
            if (!json.EndsWith("\n", StringComparison.Ordinal))
                json += "\n";

            using (var saver = new FileSaver(path))
            {
                File.WriteAllText(saver.SafeName, json);
                saver.Commit();
            }
        }
    }
}
