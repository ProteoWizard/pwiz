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
using pwiz.Common.Pipeline;
using pwiz.Common.SystemUtil;
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
    /// plus the per-fold linear weights and biases, or for <c>OSPREY_FDR_MODEL=gbdt</c> the per-fold
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
    /// phase that produces it ends, and <see cref="LoadForAnalysis"/> merges the two on read, so
    /// a consumer still sees one <see cref="Sidecar"/>. The stratum file is absent under every
    /// mode but protein-compact, which is what the mode gate below expects.
    ///
    /// <para>Both are experiment-wide, so each is written ONCE under the analysis name
    /// (<c>&lt;blib-stem&gt;.1st-pass.model.json</c>, <c>&lt;blib-stem&gt;.1st-pass.stratum.json</c>),
    /// never copied beside every run. They used to be: 82 identical copies of each on an 82-run
    /// cohort, 790 MB of them stratum. An HPC orchestrator ships one file to every node, as it
    /// already does for the experiment sidecar and the retained summary.</para>
    /// </summary>
    internal static class FirstPassModelIO
    {
        public const string EXT_MODEL = @"." + FdrScoresSidecar.LABEL_FIRST_PASS + @".model.json";
        public const string EXT_STRATUM = @"." + FdrScoresSidecar.LABEL_FIRST_PASS + @".stratum.json";

        /// <summary>Serializable slice of <see cref="PercolatorResults"/> the frozen scorer needs,
        /// plus the pass-1 provenance a SecondPassFDR node cannot otherwise know.</summary>
        private sealed class ModelDto
        {
            /// <summary>The writing task's <see cref="ArtifactStamp"/>, serialized first.</summary>
            [JsonProperty(ArtifactStamp.JSON_PROPERTY, Order = -100)]
            public string OspreyValidity { get; set; }

            public int SchemaVersion { get; set; }
            public int NumFeatures { get; set; }
            public double[] Means { get; set; }
            public double[] Stds { get; set; }
            public double[][] FoldWeights { get; set; }
            public double[] FoldBiases { get; set; }

            /// <summary>The per-fold tree ensembles of a <c>OSPREY_FDR_MODEL=gbdt</c> model, or null for
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
        }

        /// <summary>Serializable form of the protein-compact stratum: the base ids of every
        /// library precursor belonging to a protein with at least two detected peptides.</summary>
        private sealed class StratumDto
        {
            /// <summary>The writing task's <see cref="ArtifactStamp"/>, serialized first.</summary>
            [JsonProperty(ArtifactStamp.JSON_PROPERTY, Order = -100)]
            public string OspreyValidity { get; set; }

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
            /// <summary>The model file's own stamp, when <see cref="LoadForAnalysis"/> loaded it.</summary>
            public ArtifactStamp Stamp { get; set; }
        }

        /// <summary>
        /// The analysis's model path, <c>&lt;blib-stem&gt;.1st-pass.model.json</c>: named for the
        /// output blib, in the directory the per-run artifacts resolve to - the same resolution
        /// as <see cref="RetainedBaseIdSidecar.PathFor"/>, for the reasons doc 00 gives under
        /// "Where blib-named experiment-wide artifacts live". Null when the configuration has no
        /// output blib or no input to resolve the directory from.
        /// </summary>
        public static string PathFor(OspreyConfig config)
        {
            return AnalysisPath(config, EXT_MODEL);
        }

        /// <summary>
        /// The analysis's protein-compact stratum path, <c>&lt;blib-stem&gt;.1st-pass.stratum.json</c>,
        /// resolved exactly as <see cref="PathFor"/> resolves the model so every phase agrees on
        /// where.
        /// </summary>
        public static string StratumPathFor(OspreyConfig config)
        {
            return AnalysisPath(config, EXT_STRATUM);
        }

        /// <summary>
        /// Load the analysis's model, with its stratum when the stratum file pairs with it, or null
        /// when the model is absent or unreadable.
        ///
        /// <para>The stratum is a SEPARATE artifact written by a later phase, so a run killed
        /// between training and protein FDR legitimately has the model and no stratum - that is
        /// not a reason to reject the model. The two pair only when their stamps match: the same
        /// FirstPassFDR run stamps both alike, and a stratum left by an earlier run under another
        /// key - this run computed an empty one and wrote none, or its write failed - would
        /// otherwise be published with this run's model. An unpaired model loads with no stratum,
        /// and the caller's mode gate decides whether that is fatal.</para>
        /// </summary>
        public static Sidecar LoadForAnalysis(OspreyConfig config)
        {
            string modelPath = PathFor(config);
            var sidecar = Load(modelPath);
            if (sidecar == null)
                return null;
            sidecar.Stamp = ArtifactStamp.TryReadJsonHead(modelPath);
            string stratumPath = StratumPathFor(config);
            if (sidecar.Stamp != null &&
                string.Equals(sidecar.Stamp.ToString(), ArtifactStamp.TryReadJsonHead(stratumPath)?.ToString(), StringComparison.Ordinal))
            {
                sidecar.StratumBaseIds = LoadStratum(stratumPath);
            }
            return sidecar;
        }

        /// <summary>
        /// Write the frozen-scorer slice of <paramref name="model"/> to <paramref name="path"/>:
        /// its tree ensembles when it has them, otherwise its linear weights. Returns false
        /// (writing nothing) when <see cref="Serialize"/> declines the model, so the caller does
        /// not advertise a sidecar SecondPassFDR cannot use.
        /// </summary>
        /// <param name="path">Sidecar path to write.</param>
        /// <param name="model">The trained 1st-pass model.</param>
        /// <param name="experimentAgg">Normalized OSPREY_EXPERIMENT_AGG of the training process;
        ///   see <see cref="Serialize"/>.</param>
        /// <param name="stamp">The writing task's validity stamp, embedded as the first property.</param>
        public static bool Save(string path, PercolatorResults model, string experimentAgg, ArtifactStamp stamp)
        {
            string json = Serialize(model, experimentAgg, stamp);
            if (json == null)
                return false;
            WriteText(path, json);
            return true;
        }

        /// <summary>
        /// The model file's serialized form, or null when there is nothing <see cref="Load"/>
        /// could read back: no standardizer, neither linear weights nor trees (an empty or
        /// degenerate model), or trees <see cref="Load"/> would refuse. Separate from
        /// <see cref="Save"/> so a caller can report a model that cannot be rendered apart from a
        /// file that cannot be written.
        /// </summary>
        /// <param name="model">The trained 1st-pass model.</param>
        /// <param name="experimentAgg">Normalized OSPREY_EXPERIMENT_AGG of the training process,
        ///   stamped so a SecondPassFDR node reads the pass-1 arm instead of guessing it from its own
        ///   environment. Comes from the caller (which holds the byproduct) rather than being
        ///   re-read here, so this stays a pure serializer.</param>
        /// <param name="stamp">The writing task's validity stamp, embedded as the first property.</param>
        public static string Serialize(PercolatorResults model, string experimentAgg, ArtifactStamp stamp)
        {
            if (stamp == null)
                throw new ArgumentNullException(nameof(stamp));
            if (model?.Standardizer == null)
                return null;

            var dto = new ModelDto
            {
                OspreyValidity = stamp.ToString(),
                SchemaVersion = 1,
                NumFeatures = model.Standardizer.NumFeatures,
                Means = model.Standardizer.Means,
                Stds = model.Standardizer.Stds,
                ExperimentAgg = experimentAgg,
            };
            if (model.IsGradientBoostedTrees)
            {
                // Declined rather than written when any fold would fail Load's own check, since
                // the stamp embedded in the file would then attest a model nobody can read.
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
        /// <param name="stamp">The writing task's validity stamp, embedded as the first property.</param>
        public static bool SaveStratum(string path, HashSet<uint> stratumBaseIds, ArtifactStamp stamp)
        {
            if (stamp == null)
                throw new ArgumentNullException(nameof(stamp));
            if (stratumBaseIds == null || stratumBaseIds.Count == 0)
                return false;

            var sortedBaseIds = new uint[stratumBaseIds.Count];
            stratumBaseIds.CopyTo(sortedBaseIds);
            Array.Sort(sortedBaseIds); // Array.Sort OK: distinct uint keys, so stability cannot change the result
            WriteText(path, SerializeJson(new StratumDto
            {
                OspreyValidity = stamp.ToString(),
                SchemaVersion = 1,
                StratumBaseIds = sortedBaseIds,
            }));
            return true;
        }

        /// <summary>Write an already-serialized artifact through <see cref="SimpleFileSaver"/>.</summary>
        public static void WriteText(string path, string json)
        {
            string parent = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);
            using (var saver = new SimpleFileSaver(path))
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
            return RoundtripDoubleConverter.SerializeIndented(dto);
        }

        /// <summary>
        /// <c>&lt;blib-stem&gt;&lt;ext&gt;</c> in the directory the analysis's per-run artifacts
        /// resolve to, or null when there is no output blib or input to resolve it from.
        /// </summary>
        private static string AnalysisPath(OspreyConfig config, string ext)
        {
            string outputBlib = config?.OutputBlib;
            string siblingPath = ScoringTaskShared.ArtifactSiblingPath(config);
            if (string.IsNullOrEmpty(outputBlib) || string.IsNullOrEmpty(siblingPath))
                return null;
            string stem = Path.GetFileNameWithoutExtension(outputBlib);
            if (string.IsNullOrEmpty(stem))
                return null;
            string parent = ArtifactPaths.ResolveOutputDir(siblingPath);
            return string.IsNullOrEmpty(parent) ? stem + ext : Path.Combine(parent, stem + ext);
        }
    }
}
