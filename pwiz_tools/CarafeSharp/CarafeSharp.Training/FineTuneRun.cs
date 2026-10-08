/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) src/main/resources/py/v2/ai.py
 *   (the --tf_type all path: train_rt, then train_ms2)
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
using System.IO;
using System.Linq;
using System.Text.Json;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.Models;
using static TorchSharp.torch;

namespace pwiz.CarafeSharp.Training
{
    /// <summary>Options for <see cref="FineTuneRun"/>, with Carafe's defaults.</summary>
    public sealed class FineTuneOptions
    {
        /// <summary>Carafe's <c>--seed</c>: numpy's global state and libtorch's generator.</summary>
        public uint Seed { get; set; } = 2024;

        public FineTuneSettings Ms2 { get; set; } = FineTuneSettings.Ms2Defaults();

        public FineTuneSettings Rt { get; set; } = FineTuneSettings.RtDefaults();

        public int Ms2MaxTest { get; set; } = TrainingSplit.DEFAULT_MAX_TEST;

        /// <summary>
        /// Carafe's <c>--ms2_model</c>: the MS2 model to fine-tune (a checkpoint or safetensors),
        /// which is also the baseline the fine-tuned model must beat; null for the pretrained one.
        /// </summary>
        public string Ms2Model { get; set; }

        /// <summary>
        /// When the fine-tuned MS2 model does not beat <see cref="Ms2Model"/> (a safetensors
        /// file), keep that start model as <see cref="ModelFiles.MS2_BASE_SAFETENSORS"/>, which
        /// prediction then uses instead of the pretrained model: for a saved model fine-tuned
        /// further. Carafe's <c>--ms2_model</c> keeps the pretrained model instead.
        /// </summary>
        public bool KeepMs2Start { get; set; }

        /// <summary>A safetensors RT model to fine-tune instead of the pretrained one: a saved model's, or a test's.</summary>
        public string RtModel { get; set; }

        /// <summary>
        /// The RT model to fine-tune. Chronologer starts from <see cref="RtModel"/> when that is a saved Chronologer,
        /// else from the pretrained Chronologer.
        /// </summary>
        public RtModelType RtModelType { get; set; }

        /// <summary>
        /// Set for runs on different gradients: Chronologer fine-tunes on the RT rows' hydrophobic index
        /// (<see cref="RtTrainingExample.Hi"/>), from a start model that predicts it, and this map from the index to the
        /// runs' median normalized RT scores it. Null trains on the rows' normalized RT: unaligned, or aligned runs of one
        /// gradient, whose median minutes the model then predicts.
        /// </summary>
        public Func<double, double> HiToRtNorm { get; set; }

        public Device Device { get; set; } = CPU;
    }

    /// <summary>What a fine-tuning run measured, and whether it keeps the fine-tuned MS2 model.</summary>
    public sealed class FineTuneResult
    {
        public RtMetricSummary RtPretrained { get; set; }
        public RtMetricSummary RtFineTuned { get; set; }
        public Ms2MetricSummary Ms2Pretrained { get; set; }
        public Ms2MetricSummary Ms2FineTuned { get; set; }

        /// <summary>Carafe's rule: the fine-tuned MS2 model beats the pretrained one on all four medians.</summary>
        public bool UseFineTunedMs2 { get; set; }

        public int RtTrainCount { get; set; }
        public int Ms2TrainCount { get; set; }
        public int RtBatchSize { get; set; }
        public int Ms2BatchSize { get; set; }
        public IReadOnlyList<EpochRecord> RtHistory { get; set; } = Array.Empty<EpochRecord>();
        public IReadOnlyList<EpochRecord> Ms2History { get; set; } = Array.Empty<EpochRecord>();
        public TimeSpan RtElapsed { get; set; }
        public TimeSpan Ms2Elapsed { get; set; }
    }

    /// <summary>
    /// Fine-tunes the pretrained RT and then MS2 model (or the MS2 model of
    /// <see cref="FineTuneOptions.Ms2Model"/>) on a set of identifications, the way Carafe's
    /// <c>ai.py --tf_type all</c> does: one seed for the run, RT first, each model scored before
    /// and after training on the same held-out rows, and the fine-tuned MS2 model kept only
    /// when it beats the starting one on all four similarity medians. Writes
    /// <c>rt.safetensors</c>, <c>ms2.safetensors</c>, Carafe's
    /// <c>model_evaluation_metrics.json</c> and CarafeSharp's <c>model.json</c>.
    /// </summary>
    public static class FineTuneRun
    {
        public const string RT_MODEL_FILE = ModelFiles.RT_SAFETENSORS;
        public const string MS2_MODEL_FILE = ModelFiles.MS2_SAFETENSORS;
        public const string MODEL_INFO_FILE = ModelFiles.INFO;

        /// <summary>
        /// Fine-tunes the models given rows: <paramref name="rtRows"/> or <paramref name="ms2Rows"/>
        /// null skips that model, and an empty list is an error, since the library would then
        /// be predicted from whatever models an earlier run left in the folder.
        /// </summary>
        public static FineTuneResult Run(IReadOnlyList<RtTrainingExample> rtRows, IReadOnlyList<Ms2TrainingExample> ms2Rows,
            PretrainedModels pretrained, FineTuneOptions options, string outputDirectory, Action<string> log)
        {
            RequireRows(rtRows, @"RT");
            RequireRows(ms2Rows, @"MS2");
            log = log ?? (_ => { });
            Directory.CreateDirectory(outputDirectory);
            // An earlier run's base model would otherwise predict in place of this run's choice.
            File.Delete(Path.Combine(outputDirectory, ModelFiles.MS2_BASE_SAFETENSORS));
            manual_seed(options.Seed);
            var shuffle = new NumpyRandomState(options.Seed);
            var result = new FineTuneResult();

            if (rtRows != null)
                TrainRt(rtRows, pretrained, options, outputDirectory, shuffle, result, log);
            if (ms2Rows != null)
                TrainMs2(ms2Rows, pretrained, options, outputDirectory, shuffle, result, log);

            WriteMetrics(Path.Combine(outputDirectory, ModelFiles.METRICS), result);
            WriteModelInfo(Path.Combine(outputDirectory, MODEL_INFO_FILE), pretrained, options, result);
            return result;
        }

        private static void RequireRows<T>(IReadOnlyList<T> rows, string model)
        {
            if (rows != null && rows.Count == 0)
            {
                throw new InvalidOperationException(string.Format(
                    @"No {0} training rows: no identification passed the filters for the {0} model -tf asks to fine-tune.", model));
            }
        }

        private static void TrainRt(IReadOnlyList<RtTrainingExample> rows, PretrainedModels pretrained, FineTuneOptions options,
            string outputDirectory, NumpyRandomState shuffle, FineTuneResult result, Action<string> log)
        {
            var stopwatch = Stopwatch.StartNew();
            // Carafe sizes the split from the uncollapsed rows, then samples the collapsed ones.
            int testCount = TrainingSplit.TestCount(rows.Count);
            int trainCount = rows.Count - testCount;
            var forms = RtMetrics.CollapseByForm(rows);
            var (trainIndexes, testIndexes) = TrainingSplit.Split(
                forms.Select(f => f.Peptide.Sequence).ToArray(), forms.Select(f => f.Peptide.ModsText).ToArray(),
                trainCount, testCount);
            var train = trainIndexes.Select(i => forms[i]).ToArray();
            var test = testIndexes.Select(i => forms[i]).ToArray();
            int batchSize = options.Rt.EffectiveBatchSize(trainCount);
            log(string.Format(@"RT: {0} rows, {1} peptide forms, {2} training rows, {3} test rows, batch {4}",
                rows.Count, forms.Count, train.Length, test.Length, batchSize));
            if (options.RtModelType == RtModelType.chronologer)
            {
                TrainChronologer(train, test, options, outputDirectory, batchSize, shuffle, result, log);
            }
            else
            {
                if (options.RtModel != null && ChronologerModel.IsChronologerFile(options.RtModel))
                    throw new InvalidOperationException(options.RtModel + @" is a saved Chronologer model, not an AlphaPeptDeep RT model.");
                if (options.RtModel != null)
                    log(@"RT: fine-tuning " + options.RtModel);
                using (var model = options.RtModel != null
                           ? RtModel.FromSafetensors(options.RtModel, options.Device)
                           : RtModel.FromPretrained(pretrained, options.Device))
                {
                    result.RtPretrained = RtMetrics.Evaluate(model, test);
                    log(@"RT pretrained: " + result.RtPretrained);
                    result.RtHistory = ModelFineTuner.TrainRt(model, train, test, options.Rt, batchSize, shuffle, log);
                    result.RtFineTuned = RtMetrics.Evaluate(model, test);
                    log(@"RT fine-tuned: " + result.RtFineTuned);
                    model.Save(Path.Combine(outputDirectory, RT_MODEL_FILE));
                }
                result.RtTrainCount = train.Length;
            }
            result.RtBatchSize = batchSize;
            result.RtElapsed = stopwatch.Elapsed;
        }

        /// <summary>
        /// Fine-tunes Chronologer on the forms its encoding accepts. Starting from the pretrained model, the line
        /// from its hydrophobic index to the training peptides' normalized RT is folded into its output layer
        /// first, so that fine-tuning starts from the best linear calibration rather than from another scale.
        /// When it can encode none of the test forms, it is tested on its training forms, as the split tests on
        /// every row when none are left for testing.
        /// </summary>
        private static void TrainChronologer(RtTrainingExample[] train, RtTrainingExample[] test, FineTuneOptions options,
            string outputDirectory, int batchSize, NumpyRandomState shuffle, FineTuneResult result, Action<string> log)
        {
            if (options.RtModel != null && !ChronologerModel.IsChronologerFile(options.RtModel))
                throw new InvalidOperationException(options.RtModel + @" is not a saved Chronologer model, so Chronologer cannot be fine-tuned from it.");
            var files = ChronologerFiles.Open();
            log(options.RtModel != null ? @"RT: fine-tuning the Chronologer model " + options.RtModel : @"RT: fine-tuning Chronologer " + files.WeightsPath);
            using (var model = options.RtModel != null
                       ? ChronologerModel.FromSafetensors(options.RtModel, files, options.Device)
                       : ChronologerModel.FromFiles(files, options.Device))
            {
                var encodedTrain = ChronologerTrainingExample.Encode(model, train);
                var encodedTest = ChronologerTrainingExample.Encode(model, test);
                if (encodedTrain.Count < train.Length || encodedTest.Count < test.Length)
                {
                    log(string.Format(@"RT: Chronologer cannot encode {0} of {1} training and {2} of {3} test peptide forms, which are left out",
                        train.Length - encodedTrain.Count, train.Length, test.Length - encodedTest.Count, test.Length));
                }
                if (encodedTrain.Count == 0)
                    throw new InvalidOperationException(@"No RT training rows Chronologer can encode.");
                if (encodedTest.Count == 0)
                {
                    log(@"RT: Chronologer can encode none of the test peptide forms; testing on the training forms");
                    encodedTest = encodedTrain;
                }
                result.RtTrainCount = encodedTrain.Count;
                if (options.HiToRtNorm != null && train.All(e => !double.IsNaN(e.Hi)))
                {
                    TrainChronologerOnHi(model, encodedTrain, encodedTest, options, outputDirectory, batchSize, shuffle, result, log);
                    return;
                }
                if (!model.PredictsNormalizedRt)
                {
                    var (slope, intercept) = FitLine(model.Predict(encodedTrain.Select(e => e.Example.Peptide).ToArray()),
                        encodedTrain.Select(e => e.RtNorm).ToArray());
                    model.RescaleToNormalizedRt(slope, intercept);
                    log(string.Format(CultureInfo.InvariantCulture,
                        @"RT: Chronologer's hydrophobic index to normalized RT: {0} * hi + {1}", slope, intercept));
                }
                var testExamples = encodedTest.Select(e => e.Example).ToArray();
                result.RtPretrained = RtMetrics.Evaluate(peptides => model.Predict(peptides), testExamples);
                log(@"RT pretrained (Chronologer, linear calibration): " + result.RtPretrained);
                result.RtHistory = ModelFineTuner.TrainChronologer(model, encodedTrain, encodedTest, options.Rt, batchSize, shuffle, log);
                result.RtFineTuned = RtMetrics.Evaluate(peptides => model.Predict(peptides), testExamples);
                log(@"RT fine-tuned: " + result.RtFineTuned);
                model.Save(Path.Combine(outputDirectory, RT_MODEL_FILE));
            }
        }

        /// <summary>
        /// Fine-tunes Chronologer on the hydrophobic index of aligned runs (<c>-rt_align kde</c>), which holds no
        /// gradient: each run's map put its minutes there, so the network learns the peptides and the maps keep the
        /// gradients. It trains in the units of one fixed line of the index, the least-squares line to the median
        /// gradient's normalized RT, folded into the output layer, so the optimizer sees the scale the normalized-RT
        /// fine-tune does; the inverse line is folded back before saving, and the saved model predicts the hydrophobic
        /// index. It is scored, before and after, at the median map's normalized RT of what it predicts.
        /// </summary>
        private static void TrainChronologerOnHi(ChronologerModel model, IReadOnlyList<ChronologerTrainingExample> encodedTrain,
            IReadOnlyList<ChronologerTrainingExample> encodedTest, FineTuneOptions options, string outputDirectory, int batchSize,
            NumpyRandomState shuffle, FineTuneResult result, Action<string> log)
        {
            if (model.PredictsNormalizedRt)
                throw new InvalidOperationException(@"A Chronologer that predicts normalized RT cannot be fine-tuned on the hydrophobic index of runs on different gradients.");
            var (slope, intercept) = FitLine(encodedTrain.Select(e => e.Example.Hi).ToArray(), encodedTrain.Select(e => e.RtNorm).ToArray());
            if (!(slope > 0))
                throw new InvalidOperationException(string.Format(@"The aligned hydrophobic index does not rise with retention time (slope {0}).", slope));
            log(string.Format(CultureInfo.InvariantCulture,
                @"RT: training Chronologer on the aligned runs' hydrophobic index, in units of {0} * hi + {1}", slope, intercept));
            var train = encodedTrain.Select(e => e.WithTarget(slope * e.Example.Hi + intercept)).ToArray();
            var test = encodedTest.Select(e => e.WithTarget(slope * e.Example.Hi + intercept)).ToArray();
            model.FoldOutputLine(slope, intercept);
            double[] Scored(IReadOnlyList<PeptideForm> peptides) =>
                model.Predict(peptides).Select(y => options.HiToRtNorm((y - intercept) / slope)).ToArray();
            var testExamples = encodedTest.Select(e => e.Example).ToArray();
            result.RtPretrained = RtMetrics.Evaluate(Scored, testExamples);
            log(@"RT pretrained (Chronologer, aligned runs): " + result.RtPretrained);
            result.RtHistory = ModelFineTuner.TrainChronologer(model, train, test, options.Rt, batchSize, shuffle, log);
            result.RtFineTuned = RtMetrics.Evaluate(Scored, testExamples);
            log(@"RT fine-tuned: " + result.RtFineTuned);
            model.FoldOutputLine(1 / slope, -intercept / slope);
            model.Save(Path.Combine(outputDirectory, RT_MODEL_FILE));
        }

        /// <summary>The least-squares line y = slope * x + intercept.</summary>
        private static (double Slope, double Intercept) FitLine(IReadOnlyList<double> x, IReadOnlyList<double> y)
        {
            double meanX = x.Average(), meanY = y.Average();
            double sxy = 0, sxx = 0;
            for (int i = 0; i < x.Count; i++)
            {
                sxy += (x[i] - meanX) * (y[i] - meanY);
                sxx += (x[i] - meanX) * (x[i] - meanX);
            }
            double slope = sxx > 0 ? sxy / sxx : 0;
            return (slope, meanY - slope * meanX);
        }

        private static void TrainMs2(IReadOnlyList<Ms2TrainingExample> rows, PretrainedModels pretrained, FineTuneOptions options,
            string outputDirectory, NumpyRandomState shuffle, FineTuneResult result, Action<string> log)
        {
            var stopwatch = Stopwatch.StartNew();
            int testCount = TrainingSplit.TestCount(rows.Count, options.Ms2MaxTest);
            int trainCount = rows.Count - testCount;
            var (trainIndexes, testIndexes) = TrainingSplit.Split(
                rows.Select(r => r.Sequence).ToArray(), rows.Select(r => r.Precursor.Peptide.ModsText).ToArray(),
                trainCount, testCount);
            var train = trainIndexes.Select(i => rows[i]).ToArray();
            var test = testIndexes.Select(i => rows[i]).ToArray();
            int batchSize = options.Ms2.EffectiveBatchSize(trainCount);
            log(string.Format(@"MS2: {0} spectra, {1} training rows, {2} test rows, batch {3}",
                rows.Count, train.Length, test.Length, batchSize));

            // Carafe's --ms2_model replaces the pretrained model, as the start and the baseline.
            if (options.Ms2Model != null)
                log(@"MS2: fine-tuning " + options.Ms2Model);
            using (var model = options.Ms2Model != null
                       ? Ms2Model.FromFile(options.Ms2Model, options.Device)
                       : Ms2Model.FromPretrained(pretrained, options.Device))
            {
                result.Ms2Pretrained = Ms2Metrics.Evaluate(model, test);
                log(@"MS2 pretrained: " + result.Ms2Pretrained);
                result.Ms2History = ModelFineTuner.TrainMs2(model, train, test, options.Ms2, batchSize, shuffle, log);
                result.Ms2FineTuned = Ms2Metrics.Evaluate(model, test);
                log(@"MS2 fine-tuned: " + result.Ms2FineTuned);
                result.UseFineTunedMs2 = result.Ms2FineTuned.BeatsOnAll(result.Ms2Pretrained);
                bool keepStart = !result.UseFineTunedMs2 && options.KeepMs2Start && options.Ms2Model != null;
                log(result.UseFineTunedMs2
                    ? @"MS2: the fine-tuned model beats the pretrained one on all four metrics and will be used."
                    : keepStart
                        ? @"MS2: the fine-tuned model does not beat its start model on all four metrics; predictions keep the start model."
                        : @"MS2: the fine-tuned model does not beat the pretrained one on all four metrics; predictions keep the pretrained model.");
                model.Save(Path.Combine(outputDirectory, MS2_MODEL_FILE));
                if (keepStart)
                    File.Copy(options.Ms2Model, Path.Combine(outputDirectory, ModelFiles.MS2_BASE_SAFETENSORS));
            }
            result.Ms2TrainCount = train.Length;
            result.Ms2BatchSize = batchSize;
            result.Ms2Elapsed = stopwatch.Elapsed;
        }

        /// <summary>
        /// Carafe's <c>model_evaluation_metrics.json</c>: the held-out scores of each trained
        /// model before and after, and <c>ms2.use_finetuned_for_prediction</c>, which library
        /// prediction reads to choose the MS2 model.
        /// </summary>
        private static void WriteMetrics(string path, FineTuneResult result)
        {
            var metrics = new Dictionary<string, object>();
            if (result.Ms2FineTuned != null)
            {
                metrics[ModelFiles.METRICS_MS2] = new Dictionary<string, object>
                {
                    { ModelFiles.METRICS_FINETUNED, Metrics(result.Ms2FineTuned) },
                    { ModelFiles.METRICS_PRETRAINED, Metrics(result.Ms2Pretrained) },
                    { ModelFiles.METRICS_USE_FINETUNED, result.UseFineTunedMs2 },
                };
            }
            if (result.RtFineTuned != null)
            {
                metrics[ModelFiles.METRICS_RT] = new Dictionary<string, object>
                {
                    { ModelFiles.METRICS_FINETUNED, new { mae_normalized = result.RtFineTuned.MedianAbsoluteError, r2 = result.RtFineTuned.R2 } },
                    { ModelFiles.METRICS_PRETRAINED, new { mae_normalized = result.RtPretrained.MedianAbsoluteError, r2 = result.RtPretrained.R2 } },
                };
            }
            File.WriteAllText(path, JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true }));
        }

        private static void WriteModelInfo(string path, PretrainedModels pretrained, FineTuneOptions options, FineTuneResult result)
        {
            var info = new Dictionary<string, object>
            {
                { @"format", @"carafesharp-model-1" },
                { @"pretrained_sha256", pretrained?.Sha256 },
                { @"seed", options.Seed },
                { @"use_finetuned_ms2", result.UseFineTunedMs2 },
            };
            if (options.Ms2Model != null)
                info[@"ms2_start_model"] = options.Ms2Model;
            if (result.RtFineTuned != null)
            {
                info[@"rt"] = new Dictionary<string, object>
                {
                    { @"model", options.RtModelType.ToString() },
                    { @"pretrained", new { r2 = result.RtPretrained.R2, mae_normalized = result.RtPretrained.MedianAbsoluteError } },
                    { @"finetuned", new { r2 = result.RtFineTuned.R2, mae_normalized = result.RtFineTuned.MedianAbsoluteError } },
                    { @"train_rows", result.RtTrainCount },
                    { @"test_rows", result.RtFineTuned.Count },
                    { @"batch_size", result.RtBatchSize },
                    { @"epochs", options.Rt.Epochs },
                };
            }
            if (result.Ms2FineTuned != null)
            {
                info[@"ms2"] = new Dictionary<string, object>
                {
                    { @"pretrained", Metrics(result.Ms2Pretrained) },
                    { @"finetuned", Metrics(result.Ms2FineTuned) },
                    { @"use_finetuned_for_prediction", result.UseFineTunedMs2 },
                    { @"train_rows", result.Ms2TrainCount },
                    { @"test_rows", result.Ms2FineTuned.Count },
                    { @"batch_size", result.Ms2BatchSize },
                    { @"epochs", options.Ms2.Epochs },
                };
            }
            File.WriteAllText(path, JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));
        }

        private static object Metrics(Ms2MetricSummary summary)
        {
            return new { cos = summary.Cos, pcc = summary.Pcc, sa = summary.Sa, spc = summary.Spc };
        }
    }
}
