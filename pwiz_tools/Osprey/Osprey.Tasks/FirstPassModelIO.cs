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
    /// plus the per-fold linear weights and biases, or for <c>--fdr-method gbdt</c> the per-fold
    /// tree ensembles in their <see cref="GbtModelData"/> form. Doubles route through
    /// <see cref="RoundtripDoubleConverter"/> so a reloaded model scores BIT-IDENTICALLY to
    /// the in-process original (the <see cref="FrozenModelScorer.TryCreate"/> fold-average is
    /// applied to the same per-fold values either way, and a tree's node arrays ARE the model).
    ///
    /// GBDT was not persisted until the default first pass trained trees at all: the lean path
    /// trained the SVM under the gbdt flag, so the file this wrote for a gbdt run was a linear
    /// model. A tree file writes EMPTY weight and bias arrays beside the ensembles, so a reader
    /// that predates them refuses it on its no-weights check rather than mis-scoring.
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

            /// <summary>The per-fold tree ensembles of a <c>--fdr-method gbdt</c> model, or null for
            /// the linear SVM. Omitted from the file when null, so a linear model serializes
            /// byte-for-byte as it did before this property existed; added without bumping
            /// <see cref="SchemaVersion"/> for the reason <see cref="ExperimentAgg"/> was.</summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public GbtModelData[] FoldGbtModels { get; set; }

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
        /// Load from the per-file sidecars among <paramref name="perFileParquetPaths"/>
        /// (stem -&gt; score parquet path), or null when none is readable. The copies are
        /// identical, so ONE model is parsed: the first stem that also has its stratum file, or
        /// failing that the first readable model.
        ///
        /// <para>The stratum is a SEPARATE artifact written by a later phase, so a run killed
        /// between training and protein FDR legitimately has the model and no stratum - that is
        /// not a reason to reject the model. But the two must come from the SAME stem: splitting
        /// them removed the structural guarantee that they matched, and with parquets spanning
        /// directories (-LinkFrom junctions, per-file worker dirs, a partly-rewritten directory)
        /// an independent scan for each could pair one run's model with another run's stratum
        /// and publish that as the frozen first-pass state.</para>
        ///
        /// <para>The stem is found by file EXISTENCE before anything is parsed. This used to
        /// parse every stem's model in turn while looking for one with a stratum, and under every
        /// mode but protein-compact no stem has one, so it parsed every copy - 446 of them at 446
        /// files, 3.4 MB each for a tree model - to return the first.</para>
        /// </summary>
        public static Sidecar LoadFromAny(IReadOnlyDictionary<string, string> perFileParquetPaths)
        {
            if (perFileParquetPaths == null)
                return null;
            foreach (var kvp in perFileParquetPaths)
            {
                string stratumPath = StratumPathFor(kvp.Value, kvp.Key);
                if (!File.Exists(stratumPath))
                    continue;
                var stratum = LoadStratum(stratumPath);
                var sidecar = stratum != null ? Load(PathFor(kvp.Value, kvp.Key)) : null;
                if (sidecar == null)
                    continue;
                // The dedicated file wins over anything the model file itself carried.
                sidecar.StratumBaseIds = stratum;
                return sidecar;
            }
            // No stem pairs a model with a readable stratum file. Return the first readable
            // model, with whatever stratum it carries itself - non-null only in a directory
            // written before the split, where every copy was written together - and let the
            // caller's mode gate decide whether a missing stratum is fatal.
            foreach (var kvp in perFileParquetPaths)
            {
                var sidecar = Load(PathFor(kvp.Value, kvp.Key));
                if (sidecar != null)
                    return sidecar;
            }
            return null;
        }

        /// <summary>
        /// Write the frozen-scorer slice of <paramref name="model"/> to <paramref name="path"/>:
        /// its tree ensembles when it has them, otherwise its linear weights. Returns false
        /// (writing nothing) when <see cref="Serialize"/> declines the model, so the caller does
        /// not advertise a sidecar SecondPassFDR cannot use. A caller writing the identical copy
        /// beside every input serializes once and writes each copy with <see cref="WriteText"/>.
        /// </summary>
        /// <param name="path">Sidecar path to write.</param>
        /// <param name="model">The trained 1st-pass model.</param>
        /// <param name="experimentAgg">Normalized OSPREY_EXPERIMENT_AGG of the training process;
        ///   see <see cref="Serialize"/>.</param>
        public static bool Save(string path, PercolatorResults model, string experimentAgg)
        {
            string json = Serialize(model, experimentAgg);
            if (json == null)
                return false;
            WriteText(path, json);
            return true;
        }

        /// <summary>
        /// The model file's serialized form, or null when there is nothing <see cref="Load"/>
        /// could read back: no standardizer, neither linear weights nor trees (an empty or
        /// degenerate model), or trees <see cref="Load"/> would refuse. Separate from
        /// <see cref="Save"/> so the caller writing a copy beside every input clones and renders
        /// the model ONCE: a tree model is ~3.4 MB of JSON and took 1.15 s to render per copy,
        /// measured, for byte-identical output.
        /// </summary>
        /// <param name="model">The trained 1st-pass model.</param>
        /// <param name="experimentAgg">Normalized OSPREY_EXPERIMENT_AGG of the training process,
        ///   stamped so a SecondPassFDR node reads the pass-1 arm instead of guessing it from its own
        ///   environment. Comes from the caller (which holds the byproduct) rather than being
        ///   re-read here, so this stays a pure serializer.</param>
        public static string Serialize(PercolatorResults model, string experimentAgg)
        {
            if (model?.Standardizer == null)
                return null;

            var dto = new ModelDto
            {
                SchemaVersion = 1,
                NumFeatures = model.Standardizer.NumFeatures,
                Means = model.Standardizer.Means,
                Stds = model.Standardizer.Stds,
                ExperimentAgg = experimentAgg,
            };
            if (model.IsGradientBoostedTrees)
            {
                // Declined rather than written when any fold would fail Load's own check, since
                // the marker stamped beside the file would then attest a model nobody can read.
                var foldData = new GbtModelData[model.FoldGbtModels.Count];
                for (int f = 0; f < foldData.Length; f++)
                {
                    foldData[f] = model.FoldGbtModels[f]?.ToModelData();
                    if (!IsLoadableTree(foldData[f], dto.NumFeatures))
                        return null;
                }
                // Empty, not null: the shape an in-process tree model has, and what makes a
                // reader that predates the ensembles refuse the file instead of reading it as
                // a linear model with no folds.
                dto.FoldWeights = Array.Empty<double[]>();
                dto.FoldBiases = Array.Empty<double>();
                dto.FoldGbtModels = foldData;
            }
            else
            {
                if (model.FoldWeights == null || model.FoldWeights.Count == 0 ||
                    model.FoldBiases == null || model.FoldBiases.Count != model.FoldWeights.Count)
                    return null;
                dto.FoldWeights = model.FoldWeights.ToArray();
                dto.FoldBiases = model.FoldBiases.ToArray();
            }
            return SerializeJson(dto);
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
            string json = SerializeStratum(stratumBaseIds);
            if (json == null)
                return false;
            WriteText(path, json);
            return true;
        }

        /// <summary>
        /// The stratum's serialized form, or null when there is nothing to persist. Separate
        /// from <see cref="SaveStratum"/> so a caller writing the identical copy beside every
        /// input file sorts and serializes ONCE: the set reaches ~0.9 M ids on a 446-file
        /// cohort, which is ~12 MB of indented JSON, and re-rendering it per file spends
        /// several GB of writes plus 446 sorts producing byte-identical output.
        /// </summary>
        public static string SerializeStratum(HashSet<uint> stratumBaseIds)
        {
            if (stratumBaseIds == null || stratumBaseIds.Count == 0)
                return null;

            var sortedBaseIds = new uint[stratumBaseIds.Count];
            stratumBaseIds.CopyTo(sortedBaseIds);
            Array.Sort(sortedBaseIds); // Array.Sort OK: distinct uint keys, so stability cannot change the result
            return SerializeJson(new StratumDto { SchemaVersion = 1, StratumBaseIds = sortedBaseIds });
        }

        /// <summary>Write an already-serialized artifact through <see cref="FileSaver"/>.</summary>
        public static void WriteText(string path, string json)
        {
            string parent = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);
            using (var saver = new FileSaver(path))
            {
                File.WriteAllText(saver.SafeName, json);
                saver.Commit();
            }
        }

        /// <summary>
        /// Load a persisted model, or return null when <paramref name="path"/> is absent or
        /// unreadable (the caller then fails fast exactly as it did before persistence existed).
        /// The returned <see cref="PercolatorResults"/> carries only the scorer slice
        /// (standardizer + per-fold weights/biases, or standardizer + per-fold tree ensembles);
        /// <see cref="FrozenModelScorer.TryCreate"/> needs nothing else.
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
                    dto.NumFeatures != dto.Means.Length)
                    return null;

                var model = dto.FoldGbtModels != null && dto.FoldGbtModels.Length > 0
                    ? LoadTreeModel(dto)
                    : LoadLinearModel(dto);
                if (model == null)
                    return null;

                return new Sidecar
                {
                    Model = model,
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
        /// The linear SVM a file carries, or null when its weights are missing or inconsistent.
        /// Each fold's linear weights must be present and match the feature width, or the frozen
        /// scorer would dereference null / index past the end while scoring on SecondPassFDR,
        /// an opaque crash outside <see cref="Load"/>'s documented null-on-unreadable contract.
        /// </summary>
        private static PercolatorResults LoadLinearModel(ModelDto dto)
        {
            if (dto.FoldWeights == null || dto.FoldWeights.Length == 0 ||
                dto.FoldBiases == null || dto.FoldBiases.Length != dto.FoldWeights.Length)
                return null;
            foreach (var foldWeights in dto.FoldWeights)
            {
                if (foldWeights == null || foldWeights.Length != dto.Means.Length)
                    return null;
            }
            return new PercolatorResults
            {
                Standardizer = FeatureStandardizer.FromMeansStds(dto.Means, dto.Stds),
                FoldWeights = new List<double[]>(dto.FoldWeights),
                FoldBiases = new List<double>(dto.FoldBiases),
            };
        }

        /// <summary>
        /// The tree ensembles a file carries, or null when they are inconsistent. A file holding
        /// linear weights BESIDE its trees is refused, since the scorer would have to pick one of
        /// two models silently. Each ensemble must pass <see cref="IsLoadableTree"/>; a corrupt
        /// node graph throws out of <see cref="GradientBoostedTrees.FromModelData"/>, which
        /// <see cref="Load"/> turns into null like any other unreadable file.
        /// </summary>
        private static PercolatorResults LoadTreeModel(ModelDto dto)
        {
            if ((dto.FoldWeights != null && dto.FoldWeights.Length > 0) ||
                (dto.FoldBiases != null && dto.FoldBiases.Length > 0))
                return null;
            var trees = new List<GradientBoostedTrees>(dto.FoldGbtModels.Length);
            foreach (var data in dto.FoldGbtModels)
            {
                if (!IsLoadableTree(data, dto.NumFeatures))
                    return null;
                trees.Add(GradientBoostedTrees.FromModelData(data));
            }
            return new PercolatorResults
            {
                Standardizer = FeatureStandardizer.FromMeansStds(dto.Means, dto.Stds),
                FoldGbtModels = trees,
            };
        }

        /// <summary>
        /// Whether one fold ensemble is a model the frozen scorer can use: present, exactly as
        /// wide as the standardizer, and fit under the binary log-odds objective the competition
        /// ranks on. The ONE check both directions apply - <see cref="LoadTreeModel"/> to what it
        /// reads and <see cref="Serialize"/> to what it would write - so the writer cannot stamp
        /// a file the reader turns into null.
        /// </summary>
        private static bool IsLoadableTree(GbtModelData data, int numFeatures)
        {
            return data != null && data.FeatureCount == numFeatures &&
                   data.Objective == GbtObjective.LogisticBinary;
        }

        /// <summary>
        /// Serialize <paramref name="dto"/> in the artifact's canonical form: LF + trailing
        /// newline, matching the reconciliation.json convention so the artifact is stable
        /// across platforms.
        /// </summary>
        private static string SerializeJson(object dto)
        {
            var settings = new JsonSerializerSettings { Converters = { new RoundtripDoubleConverter() } };
            string json = JsonConvert.SerializeObject(dto, Formatting.Indented, settings);
            json = json.Replace("\r\n", "\n");
            if (!json.EndsWith("\n", StringComparison.Ordinal))
                json += "\n";
            return json;
        }
    }
}
