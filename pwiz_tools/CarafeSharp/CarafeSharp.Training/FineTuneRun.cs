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
using System.IO;
using System.Linq;
using System.Text.Json;
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
    /// Fine-tunes the pretrained RT and then MS2 model on a set of identifications, the way
    /// Carafe's <c>ai.py --tf_type all</c> does: one seed for the run, RT first, each model
    /// scored before and after training on the same held-out rows, and the fine-tuned MS2 model
    /// kept only when it beats the pretrained one on all four similarity medians. Writes
    /// <c>rt.safetensors</c>, <c>ms2.safetensors</c> and <c>model.json</c>.
    /// </summary>
    public static class FineTuneRun
    {
        public const string RT_MODEL_FILE = @"rt.safetensors";
        public const string MS2_MODEL_FILE = @"ms2.safetensors";
        public const string MODEL_INFO_FILE = @"model.json";

        public static FineTuneResult Run(IReadOnlyList<RtTrainingExample> rtRows, IReadOnlyList<Ms2TrainingExample> ms2Rows,
            PretrainedModels pretrained, FineTuneOptions options, string outputDirectory, Action<string> log)
        {
            log = log ?? (_ => { });
            Directory.CreateDirectory(outputDirectory);
            manual_seed(options.Seed);
            var shuffle = new NumpyRandomState(options.Seed);
            var result = new FineTuneResult();

            if (rtRows != null && rtRows.Count > 0)
                TrainRt(rtRows, pretrained, options, outputDirectory, shuffle, result, log);
            if (ms2Rows != null && ms2Rows.Count > 0)
                TrainMs2(ms2Rows, pretrained, options, outputDirectory, shuffle, result, log);

            WriteModelInfo(Path.Combine(outputDirectory, MODEL_INFO_FILE), pretrained, options, result);
            return result;
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

            using (var model = RtModel.FromPretrained(pretrained, options.Device))
            {
                result.RtPretrained = RtMetrics.Evaluate(model, test);
                log(@"RT pretrained: " + result.RtPretrained);
                result.RtHistory = ModelFineTuner.TrainRt(model, train, test, options.Rt, batchSize, shuffle, log);
                result.RtFineTuned = RtMetrics.Evaluate(model, test);
                log(@"RT fine-tuned: " + result.RtFineTuned);
                model.Save(Path.Combine(outputDirectory, RT_MODEL_FILE));
            }
            result.RtTrainCount = train.Length;
            result.RtBatchSize = batchSize;
            result.RtElapsed = stopwatch.Elapsed;
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

            using (var model = Ms2Model.FromPretrained(pretrained, options.Device))
            {
                result.Ms2Pretrained = Ms2Metrics.Evaluate(model, test);
                log(@"MS2 pretrained: " + result.Ms2Pretrained);
                result.Ms2History = ModelFineTuner.TrainMs2(model, train, test, options.Ms2, batchSize, shuffle, log);
                result.Ms2FineTuned = Ms2Metrics.Evaluate(model, test);
                log(@"MS2 fine-tuned: " + result.Ms2FineTuned);
                result.UseFineTunedMs2 = result.Ms2FineTuned.BeatsOnAll(result.Ms2Pretrained);
                log(result.UseFineTunedMs2
                    ? @"MS2: the fine-tuned model beats the pretrained one on all four metrics and will be used."
                    : @"MS2: the fine-tuned model does not beat the pretrained one on all four metrics; predictions keep the pretrained model.");
                model.Save(Path.Combine(outputDirectory, MS2_MODEL_FILE));
            }
            result.Ms2TrainCount = train.Length;
            result.Ms2BatchSize = batchSize;
            result.Ms2Elapsed = stopwatch.Elapsed;
        }

        private static void WriteModelInfo(string path, PretrainedModels pretrained, FineTuneOptions options, FineTuneResult result)
        {
            var info = new Dictionary<string, object>
            {
                { @"format", @"carafesharp-model-1" },
                { @"pretrained_sha256", pretrained.Sha256 },
                { @"seed", options.Seed },
                { @"use_finetuned_ms2", result.UseFineTunedMs2 },
            };
            if (result.RtFineTuned != null)
            {
                info[@"rt"] = new Dictionary<string, object>
                {
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
            return new { pcc = summary.Pcc, cos = summary.Cos, sa = summary.Sa, spc = summary.Spc };
        }
    }
}
