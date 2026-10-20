/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Linq;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.BiblioSpec.IO;
using pwiz.CarafeSharp.Contracts;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.Models;
using pwiz.CarafeSharp.Proteome;
using pwiz.CarafeSharp.Tasks;
using pwiz.CarafeSharp.Training;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// The one command with tuning (<c>--training-input</c>, mode 2), run in-process on the Stellar
    /// subset as the mode-1 tests are: a library is predicted to search the tuning run with, the
    /// run is searched with the training export, the models are tuned on the export, and the
    /// library the output is searched with is predicted with the tuned models. The fine-tune is cut
    /// to an epoch or two, so what is under test is the workflow, not the model: the products and
    /// where they land, which phases a changed command line runs again, a tuning run that is not
    /// an input, a saved model tuned further, and the refusals. This is the end-to-end check of
    /// Osprey's export feeding CarafeSharp's training that CarafeSharp's chained regression leg
    /// made with two executables.
    /// </summary>
    public partial class SubsetPipelineTest
    {
        // A tuning run's export keeps at least this many precursors at 1% run FDR (about 100 on
        // each subset run with the predicted library).
        private const int MIN_EXPORTED_PRECURSORS = 50;
        // Inside the subset's one isolation window: every tuning run's meta.json entry must hold it.
        private const double SUBSET_WINDOW_TARGET = 594.5201;

        // The library-build phases of a tuned run, in order, before the output's search.
        private static readonly string[] TUNING_PHASES =
        {
            LibraryBuildWorkflow.PREDICT_TRAINING_LIBRARY_TASK, LibraryBuildWorkflow.TRAIN_MODEL_TASK, PredictLibraryTask.TASK_NAME
        };

        [TestMethod, DoNotParallelize]
        public void TestSubsetLibraryBuildTuned()
        {
            string fasta = WriteSubsetProteinFasta();
            var inputs = DataInputs().ToArray();
            // Tuned on all three runs: searched alone, one seven-minute window of one run keeps no
            // precursor at 1% in the second pass, and its export is empty.
            var tuningRuns = inputs;
            string workDir = CreateDir(@"library-build-tuned");
            var buildArgs = TunedArgs(fasta, workDir, inputs, tuningRuns).ToArray();
            var products = new TunedProducts(workDir);

            using (ShortFineTune())
            {
                // Cold: every phase runs - the training library, the search of the tuning run, the
                // tuning, the library from the tuned models, and the output's search.
                string log = RunOsprey(buildArgs, Verifier(false));
                AssertTunedPhases(log, Array.Empty<string>(), TUNING_PHASES, false, true);
                AssertTunedProducts(products, tuningRuns, inputs.Length, MIN_LIBRARY_BUILD_PRECURSORS);
                AssertTrainingChain(products, tuningRuns);

                // Warm: every phase is current, both searches included.
                var before = products.Hashes();
                string warm = RunOsprey(buildArgs, Verifier(false));
                AssertTunedPhases(warm, TUNING_PHASES, Array.Empty<string>(), true, false);
                CollectionAssert.AreEqual(before, products.Hashes());

                // Entrapment is the final library's alone: the training library, the tuning search
                // and the tuning are current, and the library and the output's search run again.
                string entrapment = RunOsprey(buildArgs.Concat(new[] { OspreyCommandArgs.ARG_ENTRAPMENT.ArgumentText }).ToArray(), Verifier(false));
                AssertTunedPhases(entrapment, TUNING_PHASES.Take(2), new[] { PredictLibraryTask.TASK_NAME }, true, true);
                Assert.IsTrue(File.Exists(Path.Combine(products.WorkDirectory, LibraryBuildWorkflow.PAIRING_FILE_NAME)));
                Assert.AreEqual(1, Math.Sign(BlibComparer.CountWhere(products.Library, @"DecoyPairs", @"IsEntrapment = 1")));

                // A lost model is tuned again from the exports, with no search of the tuning run;
                // the library it predicts is predicted again, and the output searched again.
                File.Delete(products.Model);
                string retune = RunOsprey(buildArgs, Verifier(false));
                AssertTunedPhases(retune, TUNING_PHASES.Take(1), TUNING_PHASES.Skip(1), true, true);
                Assert.IsTrue(File.Exists(products.Model), products.Model);

                // A tuning option changes the tuning and what follows it, not the training library
                // or its search.
                var seedArgs = buildArgs.Concat(new[] { CarafeCommandLine.ARG_SEED.ArgumentText, @"7" }).ToArray();
                string seed = RunOsprey(seedArgs, Verifier(false));
                AssertTunedPhases(seed, TUNING_PHASES.Take(1), TUNING_PHASES.Skip(1), true, true);

                // The inputs alone are the output's search alone: every library-build phase and the
                // tuning search are current, and in the output's search the remaining runs' scoring
                // is too, so only the experiment-wide stages run again.
                string fewer = RunOsprey(TunedArgs(fasta, workDir, inputs.Take(2), tuningRuns)
                    .Concat(new[] { CarafeCommandLine.ARG_SEED.ArgumentText, @"7" }).ToArray(), Verifier(false));
                string fewerBuild = SplitAtSearch(fewer, out string fewerSearch);
                AssertTasks(fewerBuild, TUNING_PHASES.Concat(ALL_TASKS), Array.Empty<string>());
                AssertTasks(fewerSearch, new[] { PerFileScoringTask.TASK_NAME }, new[] { SecondPassFdrTask.TASK_NAME });
                Assert.AreEqual(2, BlibComparer.CountRows(products.Output, @"SpectrumSourceFiles"));
            }

            // The refusals, each a designed usage error.
            // A tuning option without the runs to tune on would change nothing.
            string withoutRuns = RunOspreyExpecting(LibraryBuildArgs(fasta, workDir)
                .Concat(new[] { CarafeCommandLine.ARG_COR.ArgumentText, @"0.8" }).ToArray(), Verifier(false), Program.EXIT_CODE_FAILURE_TO_START);
            StringAssert.Contains(withoutRuns, string.Format(OspreyResources.Program_LibraryBuildError__0__can_be_used_only_with__1__,
                CarafeCommandLine.ARG_COR.ArgumentText, OspreyCommandArgs.ARG_TRAINING_INPUT.ArgumentText));
            // The runs without the FASTA.
            string withoutFasta = RunOspreyExpecting(InputArgs(inputs).Concat(new[]
            {
                OspreyCommandArgs.ARG_LIBRARY.ArgumentText, Path.Combine(_dataDir, LIBRARY_FILE),
                OspreyCommandArgs.ARG_OUTPUT.ArgumentText, Path.Combine(workDir, BLIB_FILE),
                OspreyCommandArgs.ARG_TRAINING_INPUT.ArgumentText, inputs[1],
            }).ToArray(), Verifier(false), Program.EXIT_CODE_FAILURE_TO_START);
            StringAssert.Contains(withoutFasta, OspreyCommandArgs.ARG_TRAINING_INPUT.ArgumentText);
            // A tuning run that does not exist.
            string missing = Path.Combine(_testDir, @"missing.mzML");
            StringAssert.Contains(RunOspreyExpecting(TunedArgs(fasta, workDir, inputs, new[] { missing }).ToArray(), Verifier(false), Program.EXIT_CODE_FAILURE_TO_START),
                string.Format(OspreyResources.Program_LibraryBuildError_Training_input_file_not_found___0_, missing));
            // A saved model tunes further with both of its models, never one.
            AssertLibraryBuildRefused(fasta, workDir, OspreyCommandArgs.ARG_TRAINING_INPUT.ArgumentText, inputs[1],
                OspreyCommandArgs.ARG_MODEL.ArgumentText, products.Model, CarafeCommandLine.ARG_TF.ArgumentText, @"ms2");
        }

        /// <summary>
        /// Held-out tuning: a tuning run that is not an input is searched for the tuning only and is
        /// absent from the output, which records it in its metadata. And a saved model tuned further
        /// (<c>--model</c> with <c>--training-input</c>): the training library is predicted from the
        /// saved model and the tuning starts from it.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestSubsetLibraryBuildTunedHeldOut()
        {
            string fasta = WriteSubsetProteinFasta();
            var tuningRuns = DataInputs().ToArray();
            var inputs = tuningRuns.Take(2).ToArray();
            string workDir = CreateDir(@"library-build-held-out");
            var products = new TunedProducts(workDir);
            using (ShortFineTune())
            {
                string log = RunOsprey(TunedArgs(fasta, workDir, inputs, tuningRuns).ToArray(), Verifier(false));
                AssertTunedPhases(log, Array.Empty<string>(), TUNING_PHASES, false, true);
                AssertTunedProducts(products, tuningRuns, inputs.Length, MIN_LIBRARY_BUILD_PRECURSORS);
                // The held-out run is in the tuning search, not the output.
                Assert.AreEqual(tuningRuns.Length, BlibComparer.CountRows(products.TrainingOutput, @"SpectrumSourceFiles"));
                string heldOut = Path.GetFileNameWithoutExtension(tuningRuns[2]);
                Assert.AreEqual(0, BlibComparer.CountWhere(products.Output, @"SpectrumSourceFiles", @"FileName LIKE '%" + heldOut + @"%'"));

                // The model this run tuned, tuned further on the same runs.
                string furtherDir = CreateDir(@"library-build-further");
                var further = new TunedProducts(furtherDir);
                string furtherLog = RunOsprey(TunedArgs(fasta, furtherDir, inputs, tuningRuns)
                    .Concat(new[] { OspreyCommandArgs.ARG_MODEL.ArgumentText, products.Model }).ToArray(), Verifier(false));
                AssertTunedPhases(furtherLog, Array.Empty<string>(), TUNING_PHASES, false, true);
                // Two one-epoch tunes in a row move the RT model further than the reported count can
                // stand for: here it shows only that the output was searched.
                AssertTunedProducts(further, tuningRuns, inputs.Length, 1);
                var baseModels = CarafeModelFile.Open(further.Model).BaseModels;
                Assert.AreEqual(1, baseModels.Count, @"the further-tuned model names the model it started from");
                Assert.AreEqual(CarafeModelFile.Open(products.Model).FileSha256, baseModels[0].Sha256);
            }
        }

        /// <summary>The files a tuned one-command run writes, under its output's stem in <c>workDir</c>.</summary>
        private sealed class TunedProducts
        {
            public TunedProducts(string workDir)
            {
                string stem = Path.GetFileNameWithoutExtension(BLIB_FILE);
                Output = Path.Combine(workDir, BLIB_FILE);
                Library = Path.Combine(workDir, stem + LibraryBuildWorkflow.LIBRARY_SUFFIX + LibrarySource.EXT_BLIB);
                Model = Path.Combine(workDir, stem + CarafeModelFile.EXTENSION);
                WorkDirectory = Path.Combine(workDir, stem + LibraryBuildWorkflow.LIBRARY_SUFFIX);
                TrainingLibrary = Path.Combine(WorkDirectory, LibraryBuildWorkflow.TRAINING_LIBRARY_FILE_NAME);
                TrainingDirectory = Path.Combine(WorkDirectory, LibraryBuildWorkflow.TRAINING_FOLDER);
                TrainingOutput = Path.Combine(TrainingDirectory, LibraryBuildWorkflow.TRAINING_OUTPUT_FILE_NAME);
                ModelDirectory = Path.Combine(WorkDirectory, LibraryBuildWorkflow.MODEL_FOLDER);
            }

            public string Output { get; }
            public string Library { get; }
            public string Model { get; }
            public string WorkDirectory { get; }
            public string TrainingLibrary { get; }
            public string TrainingDirectory { get; }
            public string TrainingOutput { get; }
            public string ModelDirectory { get; }

            /// <summary>The user-facing products and the training library, hashed, to show a warm run rewrites none of them.</summary>
            public List<byte[]> Hashes()
            {
                return new[] { Output, Library, Model, TrainingLibrary }.Select(HashFile).ToList();
            }
        }

        /// <summary>
        /// The tuned run's products: both libraries stamped by their own phases, the training export
        /// of each tuning run beside its search, the model stamped by the tuning and naming the runs it
        /// was tuned on, and an output searched against the library beside it that records the tuning runs.
        /// </summary>
        private static void AssertTunedProducts(TunedProducts products, IReadOnlyList<string> tuningRuns, int inputCount, int minReported)
        {
            AssertPredictedLibrary(products.TrainingLibrary, false, LibraryBuildWorkflow.PREDICT_TRAINING_LIBRARY_TASK);
            AssertPredictedLibrary(products.Library, false, PredictLibraryTask.TASK_NAME);
            var model = CarafeModelFile.Open(products.Model);
            Assert.AreEqual(LibraryBuildWorkflow.TRAIN_MODEL_TASK, model.Validity?.Task);
            Assert.AreEqual(tuningRuns.Count, model.Training.Runs.Count);
            string trainingInput = BlibMetadata.Read(products.Output, OspreyBlibWriter.KEY_TRAINING_INPUT);
            foreach (string tuningRun in tuningRuns)
            {
                string tuningStem = Path.GetFileNameWithoutExtension(tuningRun);
                string export = Path.Combine(products.TrainingDirectory, tuningStem + TrainingExportParquet.EXT);
                Assert.IsTrue(File.Exists(export), export);
                Assert.IsTrue(model.Training.Runs.Any(r => (r.Run + @" " + r.MsFile).Contains(tuningStem)), tuningStem);
                StringAssert.Contains(trainingInput, Path.GetFileName(tuningRun));
            }

            int reported = BlibComparer.CountRows(products.Output, @"RefSpectra");
            Assert.IsTrue(reported >= minReported, string.Format(@"{0} precursors reported", reported));
            Assert.AreEqual(inputCount, BlibComparer.CountRows(products.Output, @"SpectrumSourceFiles"));
        }

        /// <summary>
        /// The chain from Osprey's training export to CarafeSharp's training, as CarafeSharp's
        /// chained regression leg checked it with the two executables: an export per tuning run with
        /// the precursors a 1% run FDR keeps and the per-run second pass's q-values; non-empty
        /// training tables built from them; both models written with finite metrics; and every
        /// tuning run's isolation window recorded in the model folder's meta.json.
        /// </summary>
        private static void AssertTrainingChain(TunedProducts products, IReadOnlyList<string> tuningRuns)
        {
            long exported = 0;
            foreach (string run in tuningRuns)
            {
                string path = Path.Combine(products.TrainingDirectory, Path.GetFileNameWithoutExtension(run) + TrainingExportParquet.EXT);
                var export = TrainingExport.Read(path);
                exported += export.Records.Count;
                Assert.IsTrue(export.Records.Count >= MIN_EXPORTED_PRECURSORS, string.Format(@"{0}: {1} precursors", path, export.Records.Count));
                Assert.AreEqual(@"2", export.RunQPass, path + @": run q from the per-run second pass");
            }
            string folder = products.ModelDirectory;
            foreach (string table in new[] { CarafeTrainingDirectory.INTENSITY_FILE, CarafeTrainingDirectory.VALID_FILE, CarafeTrainingDirectory.RT_FILE })
                Assert.IsTrue(File.ReadLines(Path.Combine(folder, table)).Count() > 1, table);
            int psms = File.ReadLines(Path.Combine(folder, CarafeTrainingDirectory.PSM_FILE)).Count() - 1;
            Assert.IsTrue(psms > 0 && psms <= exported, string.Format(@"{0} PSMs from {1} exported precursors", psms, exported));
            foreach (string model in new[] { ModelFiles.MS2_SAFETENSORS, ModelFiles.RT_SAFETENSORS })
                Assert.IsTrue(File.Exists(Path.Combine(folder, model)), model);
            using (var metrics = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, ModelFiles.METRICS))))
                AssertFiniteNumbers(metrics.RootElement, ModelFiles.METRICS);
            var meta = CarafeModelDirectory.Open(folder).Runs;
            Assert.AreEqual(tuningRuns.Count, meta.Count, CarafeModelDirectory.META_FILE);
            foreach (var run in meta)
            {
                Assert.IsTrue(run.PrecursorMzMin <= SUBSET_WINDOW_TARGET && SUBSET_WINDOW_TARGET <= run.PrecursorMzMax,
                    string.Format(@"{0}: precursor m/z {1}-{2}", run.MsFile, run.PrecursorMzMin, run.PrecursorMzMax));
            }
        }

        private static void AssertFiniteNumbers(JsonElement element, string path)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                    AssertFiniteNumbers(property.Value, path + @"." + property.Name);
            }
            else if (element.ValueKind == JsonValueKind.Number)
            {
                Assert.IsTrue(double.IsFinite(element.GetDouble()), path);
            }
        }

        /// <summary>
        /// The library-build phases skipped and run, and whether each of the two searches - the
        /// tuning runs' and the output's - skipped every stage or ran every stage. The log is split
        /// where the output's search starts, since the two searches log the same stage names.
        /// </summary>
        private static void AssertTunedPhases(string log, IEnumerable<string> skipped, IEnumerable<string> ran,
            bool trainingSearchCurrent, bool searchRuns)
        {
            string beforeSearch = SplitAtSearch(log, out string search);
            AssertTasks(beforeSearch, skipped, ran);
            AssertTasks(beforeSearch, trainingSearchCurrent ? ALL_TASKS : Array.Empty<string>(), trainingSearchCurrent ? Array.Empty<string>() : ALL_TASKS);
            AssertTasks(search, searchRuns ? Array.Empty<string>() : ALL_TASKS, searchRuns ? ALL_TASKS : Array.Empty<string>());
        }

        /// <summary>The log before the output's search starts; <paramref name="search"/> is the rest.</summary>
        private static string SplitAtSearch(string log, out string search)
        {
            string marker = string.Format(OspreyResources.LibraryBuildWorkflow_Run_Searching_the_predicted_library__0_, string.Empty);
            int split = log.IndexOf(marker, StringComparison.Ordinal);
            Assert.IsTrue(split > 0, log);
            search = log.Substring(split);
            return log.Substring(0, split);
        }

        /// <summary>The one command over <paramref name="inputs"/>, tuned on <paramref name="tuningRuns"/>.</summary>
        private IEnumerable<string> TunedArgs(string fasta, string workDir, IEnumerable<string> inputs, IEnumerable<string> tuningRuns)
        {
            return LibraryBuildArgs(fasta, workDir, inputs)
                .Concat(new[] { OspreyCommandArgs.ARG_TRAINING_INPUT.ArgumentText })
                .Concat(tuningRuns);
        }

        /// <summary>A fine-tune of an epoch or two, in seconds on the CPU, for the length of the scope.</summary>
        private static IDisposable ShortFineTune()
        {
            LibraryBuildWorkflow.ConfigureFineTuneForTests = options =>
            {
                options.Ms2 = new FineTuneSettings { Epochs = 1, WarmupEpochs = 0, BatchSize = 64, LearningRate = 1e-3, AdjustBatchSize = false };
                options.Rt = new FineTuneSettings { Epochs = 2, WarmupEpochs = 0, BatchSize = 64, LearningRate = 1e-3, AdjustBatchSize = false };
            };
            return new ScopedAction(() => LibraryBuildWorkflow.ConfigureFineTuneForTests = null);
        }

        private sealed class ScopedAction : IDisposable
        {
            private readonly Action _dispose;

            public ScopedAction(Action dispose)
            {
                _dispose = dispose;
            }

            public void Dispose()
            {
                _dispose();
            }
        }
    }
}
