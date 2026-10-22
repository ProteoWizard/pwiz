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
using System.Text.RegularExpressions;
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

                // Every tuned run is a saved-model run later: the model it saved, given to --model,
                // predicts the spectra the tuned run's own library holds.
                string savedDir = CreateDir(@"library-build-tuned-saved");
                RunOsprey(LibraryBuildArgs(fasta, savedDir, inputs)
                    .Concat(new[] { OspreyCommandArgs.ARG_MODEL.ArgumentText, products.Model }).ToArray(), Verifier(false));
                AssertSameSpectra(products.Library, new TunedProducts(savedDir).Library);

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

                // The model kept but its folder, which the library is predicted from, lost: the
                // tuning runs again rather than being skipped as current.
                Directory.Delete(products.ModelDirectory, true);
                string refolder = RunOsprey(buildArgs, Verifier(false));
                AssertTunedPhases(refolder, TUNING_PHASES.Take(1), TUNING_PHASES.Skip(1), true, true);
                Assert.IsTrue(Directory.Exists(products.ModelDirectory), products.ModelDirectory);

                // Nor is the tuning current when the folder holds other weights than the model file
                // packed (a later tuning that died before saving its model): it runs again.
                File.WriteAllBytes(Path.Combine(products.ModelDirectory, ModelFiles.RT_SAFETENSORS), new byte[] { 1, 2, 3 });
                string reweighted = RunOsprey(buildArgs, Verifier(false));
                AssertTunedPhases(reweighted, TUNING_PHASES.Take(1), TUNING_PHASES.Skip(1), true, true);

                // A tuning option changes the tuning and what follows it, not the training library
                // or its search.
                var seedArgs = buildArgs.Concat(new[] { CarafeCommandLine.ARG_SEED.ArgumentText, @"7" }).ToArray();
                string seed = RunOsprey(seedArgs, Verifier(false));
                AssertTunedPhases(seed, TUNING_PHASES.Take(1), TUNING_PHASES.Skip(1), true, true);

                // The inputs alone are the output's search alone: every library-build phase and the
                // tuning search are current, and in the output's search the remaining runs' scoring
                // is too, so only the experiment-wide stages run again. What runs is checked here,
                // not what passes: two windows of two runs searched with a one-epoch tuning detect
                // nothing at 1% on some machines (the CPU changes the tuned weights), and a search
                // that detects nothing fails, so every precursor is reported.
                string fewer = RunOsprey(TunedArgs(fasta, workDir, inputs.Take(2), tuningRuns)
                    .Concat(new[] { CarafeCommandLine.ARG_SEED.ArgumentText, @"7", OspreyCommandArgs.ARG_EXPERIMENT_FDR.ArgumentText, @"1" }).ToArray(),
                    Verifier(false));
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
            // A training-run RT reference that names none of the tuning runs, refused before any phase runs.
            string badReference = RunOspreyExpecting(buildArgs.Concat(new[] { CarafeCommandLine.ARG_RT_REFERENCE.ArgumentText, @"no_such_run" }).ToArray(),
                Verifier(false), Program.EXIT_CODE_FAILURE_TO_START);
            StringAssert.Contains(badReference, @"no_such_run");
            Assert.IsFalse(badReference.Contains(TaskLine(LibraryBuildWorkflow.PREDICT_TRAINING_LIBRARY_TASK, @"starting")), badReference);
            // The model this run writes cannot be the one it tunes from.
            string ownModel = RunOspreyExpecting(buildArgs.Concat(new[] { OspreyCommandArgs.ARG_MODEL.ArgumentText, products.Model }).ToArray(),
                Verifier(false), Program.EXIT_CODE_FAILURE_TO_START);
            StringAssert.Contains(ownModel, string.Format(OspreyResources.Program_LibraryBuildError__0__names_the_model_this_run_writes___1___Copy_it__or_choose_another__2__,
                OspreyCommandArgs.ARG_MODEL.ArgumentText, products.Model, OspreyCommandArgs.ARG_OUTPUT.ShortArgumentText));
            // A saved model tunes further with both of its models, never one.
            string copied = Path.Combine(_testDir, @"tuned-copy" + CarafeModelFile.EXTENSION);
            File.Copy(products.Model, copied, true);
            AssertLibraryBuildRefused(fasta, workDir, OspreyCommandArgs.ARG_TRAINING_INPUT.ArgumentText, inputs[1],
                OspreyCommandArgs.ARG_MODEL.ArgumentText, copied, CarafeCommandLine.ARG_TF.ArgumentText, @"ms2");
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

            }
            // The model this run tuned, tuned further on the same runs. At learning rate 0 the
            // further tuning keeps the saved model's weights, so the search reports what the first
            // one did on every platform: a second one-epoch tune at the first one's rate leaves a
            // model so marginal that on Linux nothing passed 1% (TeamCity 4228252).
            using (ShortFineTune(0))
            {
                string furtherDir = CreateDir(@"library-build-further");
                var further = new TunedProducts(furtherDir);
                string furtherLog = RunOsprey(TunedArgs(fasta, furtherDir, inputs, tuningRuns)
                    .Concat(new[] { OspreyCommandArgs.ARG_MODEL.ArgumentText, products.Model }).ToArray(), Verifier(false));
                AssertTunedPhases(furtherLog, Array.Empty<string>(), TUNING_PHASES, false, true);
                AssertTunedProducts(further, tuningRuns, inputs.Length, MIN_LIBRARY_BUILD_PRECURSORS);
                var baseModels = CarafeModelFile.Open(further.Model).BaseModels;
                Assert.AreEqual(1, baseModels.Count, @"the further-tuned model names the model it started from");
                Assert.AreEqual(CarafeModelFile.Open(products.Model).FileSha256, baseModels[0].Sha256);
            }
        }

        /// <summary>
        /// The tuned one command stopped after every task it runs (<c>OSPREY_STOP_AFTER_TASKS=1</c>)
        /// and run again until it completes: each run skips every task finished before it,
        /// recomputes none of their work, and runs exactly the next task, in the order a run
        /// straight through takes them; the result is the straight-through run's.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestSubsetLibraryBuildTunedStepwise()
        {
            string fasta = WriteSubsetProteinFasta();
            var inputs = DataInputs().ToArray();
            using (ShortFineTune())
            {
                string straightDir = CreateDir(@"library-build-straight");
                RunOsprey(TunedArgs(fasta, straightDir, inputs, inputs).ToArray(), Verifier(false));

                string stepwiseDir = CreateDir(@"library-build-stepwise");
                var args = TunedArgs(fasta, stepwiseDir, inputs, inputs).ToArray();
                var stopEach = new Dictionary<string, string>(Verifier(false)) { [@"OSPREY_STOP_AFTER_TASKS"] = @"1" };
                var expected = new[] { LibraryBuildWorkflow.PREDICT_TRAINING_LIBRARY_TASK }.Concat(ALL_TASKS)
                    .Concat(new[] { LibraryBuildWorkflow.TRAIN_MODEL_TASK, PredictLibraryTask.TASK_NAME }).Concat(ALL_TASKS).ToList();
                var ran = new List<string>();
                for (int step = 0; step <= expected.Count; step++)
                {
                    string log = RunOsprey(args, stopEach);
                    var started = TaskNames(log, @"starting");
                    Assert.AreEqual(step, TaskNames(log, @"skipping").Count, @"tasks skipped at step " + step + Environment.NewLine + log);
                    if (step == expected.Count)
                    {
                        // Everything was done: nothing runs and nothing stops.
                        Assert.AreEqual(0, started.Count, log);
                        Assert.IsFalse(HasStopLine(log), log);
                        break;
                    }
                    Assert.AreEqual(1, started.Count, @"tasks run at step " + step + Environment.NewLine + log);
                    Assert.IsTrue(HasStopLine(log), log);
                    ran.Add(started[0]);
                }
                CollectionAssert.AreEqual(expected, ran);
                AssertBlibsEqual(new TunedProducts(straightDir).Output, new TunedProducts(stepwiseDir).Output);
            }
        }

        /// <summary>
        /// The tuned one command as a cluster runs it: every node the same command line and its own
        /// <c>--task</c>, the library phases by name, the tuning runs' search by its stages with
        /// <c>--training-search</c>, and each per-file stage one node per run. Each node runs exactly
        /// its task; a prediction that comes before the tuning it needs is refused; the result is the
        /// straight-through run's; and each library phase's node run again finds its output current.
        /// (A whole run over the nodes' files re-runs the rescoring: a PerFileRescoring node cannot
        /// release library fragments and records that in its outputs' key, so an in-process run,
        /// which can, does not take them as its own. That is the search's rule, with or without
        /// <c>--fasta</c>.)
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestSubsetLibraryBuildTunedHpcChain()
        {
            string fasta = WriteSubsetProteinFasta();
            var inputs = DataInputs().ToArray();
            using (ShortFineTune())
            {
                string straightDir = CreateDir(@"library-build-hpc-straight");
                RunOsprey(TunedArgs(fasta, straightDir, inputs, inputs).ToArray(), Verifier(false));

                string chainDir = CreateDir(@"library-build-hpc-chain");
                var chain = new TunedProducts(chainDir);
                // One node: the command line every node takes, with its own runs and task.
                string Node(string task, IEnumerable<string> nodeInputs, IEnumerable<string> nodeTuningRuns, bool trainingSearch = false,
                    string state = @"starting")
                {
                    var args = new[] { OspreyCommandArgs.ARG_TASK.ArgumentText, task }
                        .Concat(TunedArgs(fasta, chainDir, nodeInputs, nodeTuningRuns));
                    if (trainingSearch)
                        args = args.Concat(new[] { OspreyCommandArgs.ARG_TRAINING_SEARCH.ArgumentText });
                    string log = RunOsprey(args.ToArray(), Verifier(false));
                    CollectionAssert.AreEqual(new[] { task }, TaskNames(log, state), log);
                    return log;
                }
                // A search, one node per run for the per-file stages.
                void Search(bool trainingSearch)
                {
                    foreach (string task in ALL_TASKS)
                    {
                        bool perFile = task == PerFileScoringTask.TASK_NAME || task == PerFileRescoreTask.TASK_NAME;
                        foreach (var runs in perFile ? inputs.Select(run => new[] { run }) : new[] { inputs })
                            Node(task, trainingSearch ? inputs : runs, trainingSearch ? runs : inputs, trainingSearch);
                    }
                }

                string trainingLibraryLog = Node(LibraryBuildWorkflow.PREDICT_TRAINING_LIBRARY_TASK, inputs, inputs);
                StringAssert.Contains(trainingLibraryLog, string.Format(OspreyResources.Program_Run_Output___0_, chain.TrainingLibrary));
                Search(true);
                Assert.IsTrue(File.Exists(chain.TrainingOutput), chain.TrainingOutput);
                // The final prediction before the tuning has nothing to predict with.
                string early = RunOspreyExpecting(new[] { OspreyCommandArgs.ARG_TASK.ArgumentText, LibraryBuildWorkflow.PREDICT_LIBRARY_TASK }
                    .Concat(TunedArgs(fasta, chainDir, inputs, inputs)).ToArray(), Verifier(false), Program.EXIT_CODE_FAILURE_TO_START);
                StringAssert.Contains(early, string.Format(OspreyResources.LibraryBuildWorkflow_Run__0__predicts_the_library_with_the_tuned_model__1___which_is_not_there__Run__2__first_,
                    OspreyArgNames.TaskText(LibraryBuildWorkflow.PREDICT_LIBRARY_TASK), chain.Model, OspreyArgNames.TaskText(LibraryBuildWorkflow.TRAIN_MODEL_TASK)));
                Node(LibraryBuildWorkflow.TRAIN_MODEL_TASK, inputs, inputs);
                Node(LibraryBuildWorkflow.PREDICT_LIBRARY_TASK, inputs, inputs);
                Search(false);

                var straight = new TunedProducts(straightDir);
                AssertSameSpectra(straight.TrainingLibrary, chain.TrainingLibrary);
                AssertSameSpectra(straight.Library, chain.Library);
                AssertBlibsEqual(straight.TrainingOutput, chain.TrainingOutput);
                AssertBlibsEqual(straight.Output, chain.Output);

                // Each library phase's node again: its output is current.
                foreach (string phase in TUNING_PHASES)
                    Assert.AreEqual(0, TaskNames(Node(phase, inputs, inputs, false, @"skipping"), @"starting").Count);
            }

            // The refusals, each a designed usage error.
            string refusalDir = CreateDir(@"library-build-hpc-refusals");
            // A library phase without the FASTA it builds from.
            string noFasta = RunOspreyExpecting(InputArgs(inputs).Concat(new[]
            {
                OspreyCommandArgs.ARG_TASK.ArgumentText, LibraryBuildWorkflow.PREDICT_LIBRARY_TASK,
                OspreyCommandArgs.ARG_OUTPUT.ArgumentText, Path.Combine(refusalDir, BLIB_FILE),
            }).ToArray(), Verifier(false), Program.EXIT_CODE_FAILURE_TO_START);
            StringAssert.Contains(noFasta, string.Format(OspreyResources.Program_LibraryBuildError__0__can_be_used_only_with__1__,
                OspreyArgNames.TaskText(LibraryBuildWorkflow.PREDICT_LIBRARY_TASK), OspreyCommandArgs.ARG_FASTA.ArgumentText));
            // The tuning phases without the runs to tune on.
            string noRuns = RunOspreyExpecting(new[] { OspreyCommandArgs.ARG_TASK.ArgumentText, LibraryBuildWorkflow.TRAIN_MODEL_TASK }
                .Concat(LibraryBuildArgs(fasta, refusalDir, inputs)).ToArray(), Verifier(false), Program.EXIT_CODE_FAILURE_TO_START);
            StringAssert.Contains(noRuns, string.Format(OspreyResources.Program_LibraryBuildError__0__can_be_used_only_with__1__,
                OspreyArgNames.TaskText(LibraryBuildWorkflow.TRAIN_MODEL_TASK), OspreyCommandArgs.ARG_TRAINING_INPUT.ArgumentText));
            // The tuning runs' search names a stage of it.
            foreach (string task in new[] { null, LibraryBuildWorkflow.PREDICT_LIBRARY_TASK, TrainingExportTask.TASK_NAME })
            {
                var args = TunedArgs(fasta, refusalDir, inputs, inputs).Concat(new[] { OspreyCommandArgs.ARG_TRAINING_SEARCH.ArgumentText });
                if (task != null)
                    args = new[] { OspreyCommandArgs.ARG_TASK.ArgumentText, task }.Concat(args);
                StringAssert.Contains(RunOspreyExpecting(args.ToArray(), Verifier(false), Program.EXIT_CODE_FAILURE_TO_START),
                    OspreyCommandArgs.ARG_TRAINING_SEARCH.ArgumentText);
            }
            // ... over the tuning runs, which the run must name.
            string noTuning = RunOspreyExpecting(new[] { OspreyCommandArgs.ARG_TASK.ArgumentText, PerFileScoringTask.TASK_NAME }
                .Concat(LibraryBuildArgs(fasta, refusalDir, inputs)).Concat(new[] { OspreyCommandArgs.ARG_TRAINING_SEARCH.ArgumentText }).ToArray(),
                Verifier(false), Program.EXIT_CODE_FAILURE_TO_START);
            StringAssert.Contains(noTuning, string.Format(OspreyResources.Program_LibraryBuildError__0__can_be_used_only_with__1__,
                OspreyCommandArgs.ARG_TRAINING_SEARCH.ArgumentText, OspreyCommandArgs.ARG_TRAINING_INPUT.ArgumentText));
        }

        /// <summary>The tasks a log's <c>[TASK]</c> lines name in <paramref name="state"/>, in order.</summary>
        private static List<string> TaskNames(string log, string state)
        {
            var line = new Regex(Regex.Escape(@"[" + LogTag.TASK + @"] ") + @"(\w+):" + Regex.Escape(state) + @"\b");
            return line.Matches(log).Select(m => m.Groups[1].Value).ToList();
        }

        private static bool HasStopLine(string log)
        {
            return HasLine(log, TaskLine(@"stop", string.Empty));
        }

        /// <summary>The files a tuned one-command run writes, under its output's stem in <c>workDir</c>.</summary>
        private sealed class TunedProducts
        {
            public TunedProducts(string workDir)
            {
                string stem = Path.GetFileNameWithoutExtension(BLIB_FILE);
                Output = Path.Combine(workDir, BLIB_FILE);
                Library = Path.Combine(workDir, stem + LibraryBuildWorkflow.PREDICTED_SUFFIX + LibrarySource.EXT_BLIB);
                Model = Path.Combine(workDir, stem + CarafeModelFile.EXTENSION);
                WorkDirectory = Path.Combine(workDir, stem + LibraryBuildWorkflow.PREDICTED_SUFFIX);
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

        /// <summary>
        /// The two libraries predict the same spectrum for every precursor they share - m/z,
        /// retention time and fragment m/z exactly, intensities to float32 rounding, which moves
        /// with the other peptides of a prediction batch - and share nearly all of them: the saved
        /// model's library takes its precursor window from the command line, the tuned run's from
        /// the training run's isolation window.
        /// </summary>
        private static void AssertSameSpectra(string expectedLibrary, string actualLibrary)
        {
            var tolerance = FragmentToleranceConfig.UnitResolution(0.5);
            var expected = new BlibLoader(tolerance).Load(expectedLibrary).ToDictionary(e => e.ModifiedSequence + @"/" + e.Charge);
            var actual = new BlibLoader(tolerance).Load(actualLibrary).ToDictionary(e => e.ModifiedSequence + @"/" + e.Charge);
            var shared = expected.Keys.Where(actual.ContainsKey).ToList();
            Assert.IsTrue(shared.Count >= 0.9 * Math.Min(expected.Count, actual.Count),
                string.Format(@"{0} precursors shared of {1} and {2}", shared.Count, expected.Count, actual.Count));
            foreach (string key in shared)
            {
                var a = expected[key];
                var b = actual[key];
                Assert.AreEqual(a.PrecursorMz, b.PrecursorMz, key);
                Assert.AreEqual(a.RetentionTime, b.RetentionTime, key);
                CollectionAssert.AreEqual(a.Fragments.Select(f => f.Mz).ToArray(), b.Fragments.Select(f => f.Mz).ToArray(), key);
                for (int i = 0; i < a.Fragments.Count; i++)
                    Assert.AreEqual(a.Fragments[i].RelativeIntensity, b.Fragments[i].RelativeIntensity, 1e-5, key);
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
        private static IDisposable ShortFineTune(double learningRate = 1e-3)
        {
            LibraryBuildWorkflow.ConfigureFineTuneForTests = options =>
            {
                options.Ms2 = new FineTuneSettings { Epochs = 1, WarmupEpochs = 0, BatchSize = 64, LearningRate = learningRate, AdjustBatchSize = false };
                options.Rt = new FineTuneSettings { Epochs = 2, WarmupEpochs = 0, BatchSize = 64, LearningRate = learningRate, AdjustBatchSize = false };
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
