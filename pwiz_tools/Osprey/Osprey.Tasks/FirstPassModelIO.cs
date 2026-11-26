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
using Newtonsoft.Json.Linq;
using pwiz.Common.Pipeline;
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;
using pwiz.Osprey.IO;
using pwiz.Osprey.ML;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// Persists a trained 1st-pass Percolator model as a small JSON file, so a later
    /// process can score with it without the training: each run's own model
    /// (<see cref="SaveRunModel"/>), which PerFileRescoring scores the run's moved rows with.
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
    /// </summary>
    internal static class FirstPassModelIO
    {
        public const string EXT_MODEL = @"." + FdrScoresSidecar.LABEL_FIRST_PASS + @".model.json";

        /// <summary>The value every model file records for <see cref="ModelDto.ExperimentAgg"/>:
        /// the default of the retired OSPREY_EXPERIMENT_AGG, each precursor's best run, which
        /// is now the only experiment-wide aggregation.</summary>
        private const string EXPERIMENT_AGG_MAX = @"max";

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

            /// <summary>The experiment-wide aggregation of the process that TRAINED this model,
            /// from when the retired OSPREY_EXPERIMENT_AGG could choose one. Always written as
            /// <see cref="EXPERIMENT_AGG_MAX"/>, the only aggregation now, so the file is the
            /// same bytes it was; read back only for the log. Deliberately added WITHOUT bumping
            /// <see cref="SchemaVersion"/>: it is an optional additive property, so an older
            /// reader ignores it and this reader sees null on an older file. Bumping the version
            /// would instead make every pre-existing sidecar unreadable, which on a SecondPassFDR
            /// node is the hard fail-fast, not a graceful degradation.</summary>
            public string ExperimentAgg { get; set; }

            /// <summary>The name of each feature the model was trained on, in vector order
            /// (<see cref="PercolatorResults.FeatureNames"/>), so the file says which columns its
            /// weights belong to. Additive like <see cref="ExperimentAgg"/>, and omitted when
            /// null.</summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string[] FeatureNames { get; set; }

            /// <summary>The <see cref="TrainingSetAnalysis"/> of the trained folds, which a report
            /// written by a later process reads back because the fold training sets exist nowhere
            /// else (doc 00, P16). Optional and additive like <see cref="ExperimentAgg"/>: omitted
            /// when null, ignored by an older reader, null from an older file. Held as a token
            /// because its undefined values (a constant feature's correlation) are NaN, which the
            /// round-trip converter refuses; they are written as the string "NaN".</summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public JToken TrainingAnalysis { get; set; }

            /// <summary>A run's own model only (<see cref="SaveRunModel"/>): the raw score its
            /// calibration maps to 0, and the distance it maps to 1. Omitted from the
            /// analysis-wide model, which is not calibrated.</summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public double? CalibrationThreshold { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public double? CalibrationDenominator { get; set; }

            /// <summary>A run's own model trained with the protein-best column: the value of an
            /// entry the run had no row for. The values of the run's rows are a column of its stub
            /// scores parquet.</summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public double? ProteinBestFloor { get; set; }

            /// <summary>A run's own model only, when its safeguard scored the run with its best
            /// single feature in place of the model (<see cref="RunFirstPassModel.FallbackFeature"/>):
            /// that feature's index in the FDR feature vector. Omitted for a run its model scores.</summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public int? FallbackFeature { get; set; }

            /// <summary>The name of <see cref="FallbackFeature"/>, which a load checks against the
            /// model's feature names, so the index cannot name another column.</summary>
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string FallbackFeatureName { get; set; }
        }

        /// <summary>What one persisted sidecar carries. A result type rather than a stack of
        /// out parameters because the values are read together and travel together: the
        /// frozen scorer's model slice and the pass-1 provenance.</summary>
        public sealed class Sidecar
        {
            public PercolatorResults Model { get; set; }
            public string ExperimentAgg { get; set; }
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
            return ScoringTaskShared.AnalysisArtifactPath(config, EXT_MODEL);
        }

        /// <summary>
        /// One run's own first-pass model file, <c>&lt;stem&gt;.1st-pass.model.json</c>, beside the
        /// run's other first-pass artifacts: written by PerFileScoring, which trains each run's own
        /// model, read by PerFileRescoring.
        /// </summary>
        public static string RunModelPathFor(string inputFile)
        {
            string pass1 = FdrScoresSidecar.Pass1Path(inputFile);
            return pass1.Substring(0, pass1.Length - FdrScoresSidecar.EXT_FIRST_PASS.Length) + EXT_MODEL;
        }

        /// <summary>
        /// Write one run's own model with its calibration map and protein-best floor. Returns false,
        /// writing nothing, when <see cref="Serialize"/> declines the model.
        /// </summary>
        public static bool SaveRunModel(string path, RunFirstPassModel runModel, ArtifactStamp stamp)
        {
            if (runModel == null)
                throw new ArgumentNullException(nameof(runModel));
            var dto = BuildModelDto(runModel.Model, stamp);
            if (dto == null)
                return false;
            dto.CalibrationThreshold = runModel.CalibrationThreshold;
            dto.CalibrationDenominator = runModel.CalibrationDenominator;
            if (runModel.ScoresProteinBest)
                dto.ProteinBestFloor = runModel.ProteinBestFloor;
            if (runModel.UsesFallbackFeature)
            {
                dto.FallbackFeature = runModel.FallbackFeature;
                dto.FallbackFeatureName = FeatureNameAt(runModel.Model.FeatureNames, runModel.FallbackFeature);
            }
            WriteText(path, SerializeJson(dto));
            return true;
        }

        /// <summary>
        /// Load one run's own model, or null when the file is absent, unreadable, carries no
        /// calibration map (an analysis-wide model), or was trained on other features. The
        /// protein-best values of the run's rows are not in the file: they are a column of the
        /// run's stub scores parquet.
        /// </summary>
        public static RunFirstPassModel LoadRunModel(string path, PinFeatureSelection features)
        {
            var dto = ReadModelDto(path, features, out var model);
            if (dto?.CalibrationThreshold == null || !(dto.CalibrationDenominator > 0))
                return null;
            if (ProteinBestFeature.IsAppendedTo(model.FeatureNames) && !dto.ProteinBestFloor.HasValue)
                return null;
            int fallbackFeature = RunFirstPassModel.NO_FALLBACK;
            if (dto.FallbackFeature.HasValue)
            {
                fallbackFeature = dto.FallbackFeature.Value;
                // The index must name an FDR feature, and the same one the run was scored with.
                if (fallbackFeature < 0 || fallbackFeature >= features.FdrFeatureIndices.Length ||
                    !string.Equals(dto.FallbackFeatureName, FeatureNameAt(model.FeatureNames, fallbackFeature), StringComparison.Ordinal))
                {
                    return null;
                }
            }
            return new RunFirstPassModel(model, dto.CalibrationThreshold.Value, dto.CalibrationDenominator.Value,
                null, dto.ProteinBestFloor ?? 0.0, fallbackFeature);
        }

        /// <summary>
        /// Load the analysis's model with its stamp, or null when the model is absent or
        /// unreadable.
        /// </summary>
        public static Sidecar LoadForAnalysis(OspreyConfig config)
        {
            string modelPath = PathFor(config);
            var sidecar = Load(modelPath, PinFeatureMask.For(config));
            if (sidecar == null)
                return null;
            sidecar.Stamp = ArtifactStamp.TryReadJsonHead(modelPath);
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
        /// <param name="stamp">The writing task's validity stamp, embedded as the first property.</param>
        public static bool Save(string path, PercolatorResults model, ArtifactStamp stamp)
        {
            string json = Serialize(model, stamp);
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
        /// <param name="stamp">The writing task's validity stamp, embedded as the first property.</param>
        public static string Serialize(PercolatorResults model, ArtifactStamp stamp)
        {
            var dto = BuildModelDto(model, stamp);
            return dto == null ? null : SerializeJson(dto);
        }

        /// <summary>The serializable form of <paramref name="model"/>, or null when <see cref="Serialize"/> declines it.</summary>
        private static ModelDto BuildModelDto(PercolatorResults model, ArtifactStamp stamp)
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
                ExperimentAgg = EXPERIMENT_AGG_MAX,
                FeatureNames = model.FeatureNames,
                TrainingAnalysis = model.TrainingAnalysis == null ? null
                    : JToken.Parse(JsonConvert.SerializeObject(model.TrainingAnalysis,
                        new JsonSerializerSettings { FloatFormatHandling = FloatFormatHandling.String })),
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
            return dto;
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
        /// <see cref="Sidecar.ExperimentAgg"/> is null when the sidecar predates that field.
        ///
        /// A model whose recorded feature names are not this run's FDR features
        /// (<see cref="PinFeatureSelection.FdrFeatureNames"/>) is unusable too, and loads as null: the
        /// frozen scorer would weigh each column of a vector as some other feature. The validity
        /// key already keeps out a model trained under another mask; this checks the file itself.
        /// </summary>
        /// <param name="path">Sidecar path to read.</param>
        /// <param name="features">The search's features, which a model must have been trained on.</param>
        public static Sidecar Load(string path, PinFeatureSelection features)
        {
            var dto = ReadModelDto(path, features, out var model);
            if (dto == null)
                return null;
            return new Sidecar
            {
                Model = model,
                ExperimentAgg = dto.ExperimentAgg,
            };
        }

        /// <summary>
        /// The model file at <paramref name="path"/> and the model it carries, or null (and a null
        /// model) on the terms <see cref="Load"/> documents.
        /// </summary>
        private static ModelDto ReadModelDto(string path, PinFeatureSelection features, out PercolatorResults model)
        {
            model = null;
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
                    (dto.FeatureNames != null && dto.FeatureNames.Length != dto.NumFeatures))
                    return null;

                var loaded = dto.FoldGbtModels != null && dto.FoldGbtModels.Length > 0
                    ? LoadTreeModel(dto)
                    : LoadLinearModel(dto);
                if (loaded == null)
                    return null;
                loaded.FeatureNames = dto.FeatureNames;
                loaded.TrainingAnalysis = ReadTrainingAnalysis(dto.TrainingAnalysis);
                if (!HasFdrFeatureLayout(loaded, features))
                    return null;
                model = loaded;
                return dto;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The name of feature <paramref name="index"/>, or null when the model records no names.</summary>
        private static string FeatureNameAt(string[] featureNames, int index)
        {
            return featureNames != null && index < featureNames.Length ? featureNames[index] : null;
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
        /// Whether <paramref name="model"/> was trained on this run's FDR feature vectors: its
        /// names, when it records them, are <see cref="PinFeatureSelection.FdrFeatureNames"/> in order.
        /// A model without names (written before they were recorded) is judged by its stamp alone.
        /// A model of <c>--protein-boost best</c> has those names and then its column
        /// (<see cref="ProteinBestFeature.FEATURE_NAME"/>), which the second pass supplies from the
        /// first pass's per-run column files.
        /// </summary>
        private static bool HasFdrFeatureLayout(PercolatorResults model, PinFeatureSelection features)
        {
            var names = model.FeatureNames;
            if (names == null)
                return true;
            if (ProteinBestFeature.IsAppendedTo(names))
                names = names[..^1];
            return names.SequenceEqual(features.FdrFeatureNames, StringComparer.Ordinal);
        }

        /// <summary>
        /// The persisted training-set analysis, or null when absent or unreadable. A report view
        /// must not make the model it rides with unloadable, so a bad analysis is dropped rather
        /// than failing <see cref="Load"/>.
        /// </summary>
        private static TrainingSetAnalysis ReadTrainingAnalysis(JToken token)
        {
            if (token == null)
                return null;
            try
            {
                return token.ToObject<TrainingSetAnalysis>();
            }
            catch (JsonException)
            {
                return null;
            }
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
    }
}
