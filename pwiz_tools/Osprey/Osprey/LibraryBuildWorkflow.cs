/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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
using System.Security.Cryptography;
using pwiz.CarafeSharp.Contracts;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.Proteome;
using pwiz.CarafeSharp.Tasks;
using pwiz.CarafeSharp.Training;
using pwiz.Common.Pipeline;
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey
{
    /// <summary>
    /// The one command from a protein FASTA to a <c>.blib</c> (<c>--fasta</c>): a workflow of
    /// phases above the four-stage pipeline, each phase a CarafeSharp task run in-process through
    /// the shared driver or an Osprey analysis. With the pretrained models or a saved one
    /// (<c>--model</c>) the phases are two: predict the library, then search it. With
    /// <c>--training-input</c> three come first: predict a library to search the tuning runs with,
    /// search them with the training export, and tune the models on the exports; the library the
    /// output is searched with is then predicted with the tuned models. The pipeline itself stays
    /// the fixed four-element list the architecture document is built on; this class sequences
    /// what runs before it and derives each analysis's library from what the phase before wrote.
    /// <para>
    /// Each library is predicted over a digest made in memory from the FASTA (its decoys and,
    /// with <c>--entrapment</c>, its entrapment peptides included), so no peptide FASTA, no
    /// pairing manifest and no TSV sit between the phases. The library lands beside the output, as
    /// <c>&lt;out&gt;.predicted.blib</c>, where <c>-l</c> takes it next time, and the tuned model as
    /// <c>&lt;out&gt;.model.zip</c>, where <c>--model</c> takes it; the working files go under
    /// <c>&lt;out&gt;.predicted/</c>. All follow <c>--output-dir</c> as the run's other artifacts do.
    /// Every phase checkpoints in the file it writes, stamped, so a re-run skips each phase whose
    /// inputs and settings are unchanged and resumes a run that died at the first one that is not.
    /// </para>
    /// <para>
    /// TorchSharp and its native runtime load when a prediction or the tuning runs and never for a
    /// run that searches a given library: nothing on the <c>-l</c> path references this class.
    /// </para>
    /// <para>
    /// On a cluster each phase is a task of its own (<c>--task</c>): the three library phases by
    /// name (<see cref="LibraryBuildConfig.PHASES"/>), each run alone, and the two analyses by their
    /// stages, the tuning runs' with <c>--training-search</c>. Every node takes the same command
    /// line, so a stage node searches the analysis this class derives from it
    /// (<see cref="StageConfig"/>), exactly as the phases in one process do.
    /// </para>
    /// </summary>
    internal sealed class LibraryBuildWorkflow
    {
        /// <summary>The suffix of the library and of its working folder, after the output's stem.</summary>
        public const string PREDICTED_SUFFIX = @".predicted";

        /// <summary>The FDRBench pairing manifest written into the working folder with <c>--entrapment</c>.</summary>
        public const string PAIRING_FILE_NAME = @"pairing.tsv";

        /// <summary>The library the tuning runs are searched with, in the working folder.</summary>
        public const string TRAINING_LIBRARY_FILE_NAME = @"initial.blib";

        /// <summary>The folder of the tuning runs' search, in the working folder: its artifacts and training exports.</summary>
        public const string TRAINING_FOLDER = @"training";

        /// <summary>The tuning search's result, in <see cref="TRAINING_FOLDER"/>.</summary>
        public const string TRAINING_OUTPUT_FILE_NAME = @"training.blib";

        /// <summary>Carafe's model folder the tuning writes, in the working folder.</summary>
        public const string MODEL_FOLDER = @"model";

        /// <summary>The phase that predicts the library the tuning runs are searched with.</summary>
        public const string PREDICT_TRAINING_LIBRARY_TASK = LibraryBuildConfig.PREDICT_TRAINING_LIBRARY;

        /// <summary>The phase that tunes the models on the training exports.</summary>
        public const string TRAIN_MODEL_TASK = LibraryBuildConfig.TRAIN_MODEL;

        /// <summary>The phase that predicts the library the output is searched with.</summary>
        public const string PREDICT_LIBRARY_TASK = LibraryBuildConfig.PREDICT_LIBRARY;

        /// <summary>The library as the help spells it, after the output's stem.</summary>
        public const string LIBRARY_EXAMPLE = @"<output>" + PREDICTED_SUFFIX + LibrarySource.EXT_BLIB;

        /// <summary>The working folder as the help spells it, after the output's stem.</summary>
        public const string PREDICTED_SUFFIX_EXAMPLE = @"<output>" + PREDICTED_SUFFIX + @"/";

        /// <summary>The pairing manifest as the help spells it.</summary>
        public const string PAIRING_FILE_EXAMPLE = PREDICTED_SUFFIX_EXAMPLE + PAIRING_FILE_NAME;

        /// <summary>The tuning runs' search folder as the help spells it.</summary>
        public const string TRAINING_FOLDER_EXAMPLE = PREDICTED_SUFFIX_EXAMPLE + TRAINING_FOLDER + @"/";

        private readonly OspreyConfig _config;
        private readonly LibraryBuildSettings _settings;
        // The library the output is searched with: the tuning's (its settings carry it until the
        // tuning phase takes it off, since the tuning predicts no library of its own), else the one
        // predicted with the pretrained models or --model.
        private readonly LibrarySettings _outputLibrary;
        private bool _torchLoaded;

        /// <param name="config">The run's configuration, whose <see cref="OspreyConfig.LibraryBuild"/> is on.</param>
        /// <param name="settings">What to predict and tune, as the command line interpreted it.</param>
        public LibraryBuildWorkflow(OspreyConfig config, LibraryBuildSettings settings)
        {
            _config = config;
            _settings = settings;
            _outputLibrary = settings.Training?.Library ?? settings.Library;
        }

        /// <summary>
        /// Shortens the tuning's fine-tune; for the subset tests, which run the whole command in
        /// seconds and cannot afford one. Not a setting: it is not in the tuning's validity key.
        /// </summary>
        internal static Action<FineTuneOptions> ConfigureFineTuneForTests { get; set; }

        /// <summary>The library the run predicts and then searches: <c>&lt;out&gt;.predicted.blib</c>.</summary>
        public static string LibraryPath(OspreyConfig config)
        {
            return Path.Combine(BaseDirectory(config), OutputStem(config) + PREDICTED_SUFFIX + LibrarySource.EXT_BLIB);
        }

        /// <summary>The model the tuning writes: <c>&lt;out&gt;.model.zip</c>.</summary>
        public static string ModelPath(OspreyConfig config)
        {
            return Path.Combine(BaseDirectory(config), OutputStem(config) + CarafeModelFile.EXTENSION);
        }

        /// <summary>The library build's working folder: <c>&lt;out&gt;.predicted/</c>.</summary>
        public static string WorkDirectory(OspreyConfig config)
        {
            return Path.Combine(BaseDirectory(config), OutputStem(config) + PREDICTED_SUFFIX);
        }

        /// <summary>The library the tuning runs are searched with: <c>&lt;out&gt;.predicted/initial.blib</c>.</summary>
        public static string TrainingLibraryPath(OspreyConfig config)
        {
            return Path.Combine(WorkDirectory(config), TRAINING_LIBRARY_FILE_NAME);
        }

        /// <summary>The folder of the tuning runs' search: <c>&lt;out&gt;.predicted/training/</c>.</summary>
        public static string TrainingDirectory(OspreyConfig config)
        {
            return Path.Combine(WorkDirectory(config), TRAINING_FOLDER);
        }

        /// <summary>What a library phase selected with <c>--task</c> writes, for the startup line that names the output.</summary>
        public static string PhaseOutput(OspreyConfig config)
        {
            switch (config.LibraryBuild.Phase)
            {
                case PREDICT_TRAINING_LIBRARY_TASK:
                    return TrainingLibraryPath(config);
                case TRAIN_MODEL_TASK:
                    return ModelPath(config);
                default:
                    return LibraryPath(config);
            }
        }

        /// <summary>
        /// The analysis a stage task selected with <c>--task</c> runs in a <c>--fasta</c> run: the
        /// tuning runs' search with <c>--training-search</c>, else the search of the output, each
        /// derived from the run's configuration as the phases in one process derive it.
        /// </summary>
        public static OspreyConfig StageConfig(OspreyConfig config)
        {
            return config.LibraryBuild.TrainingSearch ? TrainingSearchConfig(config) : OutputSearchConfig(config);
        }

        /// <summary>The search of the output: the inputs, with the library the run predicts and the pairing its <c>DecoyPairs</c> table carries.</summary>
        private static OspreyConfig OutputSearchConfig(OspreyConfig config)
        {
            var search = config.ShallowClone();
            search.LibrarySource = LibrarySource.FromPath(LibraryPath(config));
            search.DecoysInLibrary = true;
            return search;
        }

        /// <summary>
        /// The search of the tuning runs: the four stages over <c>--training-input</c>, with the
        /// training library, into a folder of their own, writing each run's training export. Not
        /// the one command's analysis, whose result was not searched with tuned models, so its
        /// library build is off; and what the user asked of the run's result (FDRBench input,
        /// diagnostics) is the output's search's.
        /// </summary>
        private static OspreyConfig TrainingSearchConfig(OspreyConfig config)
        {
            string trainingDirectory = TrainingDirectory(config);
            var search = config.ShallowClone();
            search.InputFiles = new List<string>(config.LibraryBuild.TrainingInputs);
            search.LibrarySource = LibrarySource.FromPath(TrainingLibraryPath(config));
            search.DecoysInLibrary = true;
            search.OutputBlib = Path.Combine(trainingDirectory, TRAINING_OUTPUT_FILE_NAME);
            search.OutputDir = trainingDirectory;
            search.OutputFdrBench = null;
            search.ModelDiagnostics = false;
            search.LibraryBuild = new LibraryBuildConfig();
            search.TrainingExport = new TrainingExportConfig
            {
                Enabled = true,
                MaxQ = config.TrainingExport.MaxQ,
                ClaimantQ = config.TrainingExport.ClaimantQ,
                WriteXics = config.TrainingExport.WriteXics,
            };
            return search;
        }

        /// <summary>
        /// Where the products land: <c>--output-dir</c> when given, as the run's other artifacts,
        /// else beside the output blib, which is where a user reuses them from.
        /// </summary>
        private static string BaseDirectory(OspreyConfig config)
        {
            if (!string.IsNullOrEmpty(config.OutputDir))
                return config.OutputDir;
            return Path.GetDirectoryName(Path.GetFullPath(config.OutputBlib)) ?? string.Empty;
        }

        private static string OutputStem(OspreyConfig config)
        {
            return Path.GetFileNameWithoutExtension(config.OutputBlib);
        }

        /// <summary>
        /// Runs the phases in order; the exit code of the first that fails, else the search's. A
        /// library phase selected with <c>--task</c> (<see cref="LibraryBuildConfig.Phase"/>) runs
        /// alone, reading what the phases before it wrote.
        /// </summary>
        public int Run(IReadOnlyList<OspreyTask> pipeline)
        {
            string phase = _config.LibraryBuild.Phase;
            string libraryPath = LibraryPath(_config);
            var training = _settings.Training;
            try
            {
                if (training != null)
                {
                    CheckRtReference(_outputLibrary, training);
                    if (phase == null || phase == PREDICT_TRAINING_LIBRARY_TASK)
                    {
                        int exitCode = RunPhase(PhaseTask(PREDICT_TRAINING_LIBRARY_TASK));
                        if (exitCode != 0 || phase != null)
                            return exitCode;

                        Program.LogInfo(string.Format(OspreyResources.LibraryBuildWorkflow_Run_Searching_the_tuning_runs_with__0_, TrainingLibraryPath(_config)));
                        Program.LogInfo(string.Empty);
                        exitCode = RunTrainingSearch();
                        if (exitCode != 0)
                            return exitCode;
                        Program.LogInfo(string.Empty);
                    }
                    if (phase == null || phase == TRAIN_MODEL_TASK)
                    {
                        var tune = PhaseTask(TRAIN_MODEL_TASK);
                        DiscardModelWithoutFolder(training.ModelFile, training.OutputDirectory);
                        int exitCode = RunPhase(tune);
                        if (exitCode != 0 || phase != null)
                            return exitCode;
                    }
                    // On its own the prediction needs the tuning's checkpoint, the model it saves last.
                    if (phase != null && !File.Exists(ModelPath(_config)))
                    {
                        Program.LogError(string.Format(OspreyResources.LibraryBuildWorkflow_Run__0__predicts_the_library_with_the_tuned_model__1___which_is_not_there__Run__2__first_,
                            OspreyArgNames.TaskText(phase), ModelPath(_config), OspreyArgNames.TaskText(TRAIN_MODEL_TASK)));
                        return Program.EXIT_CODE_FAILURE_TO_START;
                    }
                }

                var predict = (PredictLibraryTask)PhaseTask(PREDICT_LIBRARY_TASK);
                int predictExit = RunPhase(predict);
                if (predictExit != 0)
                    return predictExit;
                // The pairing manifest for FDRBench, which reads the entrapment pairing from the
                // file; Osprey itself reads it from the library's DecoyPairs table. Written when the
                // prediction ran, and when a skipped prediction (the library is current) finds no
                // manifest beside it - a write that failed after the library was committed is made
                // good here from the same seeded digest, in seconds, rather than by deleting the
                // library.
                if (_config.LibraryBuild.Entrapment)
                {
                    string pairingPath = Path.Combine(WorkDirectory(_config), PAIRING_FILE_NAME);
                    if (predict.Peptides != null || !File.Exists(pairingPath))
                    {
                        var peptides = predict.Peptides ?? LibraryPeptides.Build(DigestSettings(_outputLibrary, true), TextWriter.Null);
                        peptides.WriteManifest(pairingPath);
                        Program.LogInfo(string.Format(OspreyResources.LibraryBuildWorkflow_Run_Wrote_the_pairing_manifest__0_, pairingPath));
                    }
                }
                if (phase != null)
                    return predictExit;
            }
            catch (Exception ex) when (!(ex is TaskBudgetSpentException))
            {
                Program.LogError(DescribeFailure(ex));
                return Program.EXIT_CODE_FAILURE_TO_START;
            }

            Program.LogInfo(string.Format(OspreyResources.LibraryBuildWorkflow_Run_Searching_the_predicted_library__0_, libraryPath));
            Program.LogInfo(string.Empty);
            // The search reads the library it was given like any other: with its decoys and the
            // pairing its DecoyPairs table carries.
            return new AnalysisPipeline().Run(OutputSearchConfig(_config), pipeline);
        }

        /// <summary>
        /// The CarafeSharp task that runs the library phase <paramref name="phase"/>, with the
        /// settings it reads and writes derived from the run's configuration: the one place both a
        /// run and the file list of a <c>--task</c> node (<see cref="PhaseFiles"/>) get them from.
        /// <list type="bullet">
        /// <item><see cref="PREDICT_TRAINING_LIBRARY_TASK"/>: the library the tuning runs are searched
        /// with, never with entrapment, predicted with the pretrained models or <c>--model</c>.</item>
        /// <item><see cref="TRAIN_MODEL_TASK"/>: the tuning on the tuning runs' exports, which writes
        /// the model folder and <c>&lt;out&gt;.model.zip</c> and predicts no library of its own: the
        /// final prediction is a phase of its own, so a change to the entrapment or the inputs never
        /// tunes again.</item>
        /// <item><see cref="PREDICT_LIBRARY_TASK"/>: the library the output is searched with, from the
        /// tuned model folder after a tuning, as CarafeSharp's training predicts it right after the
        /// fine-tune (so a new tuning predicts the library again), else from the pretrained models or
        /// <c>--model</c>.</item>
        /// </list>
        /// </summary>
        private PipelineTask PhaseTask(string phase)
        {
            string workDirectory = WorkDirectory(_config);
            var training = _settings.Training;
            switch (phase)
            {
                case PREDICT_TRAINING_LIBRARY_TASK:
                {
                    var trainingLibrary = _settings.Library;
                    PrepareLibrary(trainingLibrary, TrainingLibraryPath(_config), workDirectory);
                    // The training-run RT reference names a run whose minutes the tuned models map
                    // to; the models this library is predicted with have no such map.
                    trainingLibrary.RtReference = null;
                    return new PredictLibraryTask(trainingLibrary, DigestSettings(trainingLibrary, false), PREDICT_TRAINING_LIBRARY_TASK);
                }
                case TRAIN_MODEL_TASK:
                    training.Library = null;
                    training.Identifications = TrainingDirectory(_config);
                    training.MsFiles = _config.LibraryBuild.TrainingInputs.Select(Path.GetFullPath).ToArray();
                    training.OutputDirectory = Path.Combine(workDirectory, MODEL_FOLDER);
                    training.ModelFile = ModelPath(_config);
                    return new TrainTask(training, TRAIN_MODEL_TASK) { ConfigureFineTune = ConfigureFineTuneForTests };
                default:
                {
                    var library = _outputLibrary;
                    PrepareLibrary(library, LibraryPath(_config), workDirectory);
                    if (training != null)
                    {
                        library.ModelDirectory = Path.Combine(workDirectory, MODEL_FOLDER);
                        library.PreferSafetensors = true;
                    }
                    return new PredictLibraryTask(library, DigestSettings(library, _config.LibraryBuild.Entrapment));
                }
            }
        }

        /// <summary>
        /// The files the library phase <paramref name="phase"/> reads and writes, as its task
        /// declares them, for <c>--task-files</c>: what a cluster stages on the node and collects
        /// from it.
        /// </summary>
        public void PhaseFiles(string phase, out IList<string> inputs, out IList<string> outputs)
        {
            var task = PhaseTask(phase);
            var ctx = new CarafeContext(new[] { task }, TextWriter.Null, TextWriter.Null);
            if (phase == TRAIN_MODEL_TASK)
            {
                // The tuning finds its exports by scanning the search's folder, which a node asking
                // what to stage does not have yet: each tuning run's export, where the search wrote it.
                var training = _settings.Training;
                inputs = _config.LibraryBuild.TrainingInputs
                    .Select(run => Path.Combine(TrainingDirectory(_config), Path.GetFileNameWithoutExtension(run) + TrainingExportParquet.EXT))
                    .Concat(new[] { training.BaseModel, training.Ms2Model, training.PretrainedModels })
                    .Where(path => path != null).ToList();
                outputs = task.Outputs(ctx).Where(path => path != null).Concat(new[] { training.OutputDirectory }).ToList();
                return;
            }
            inputs = task.Inputs(ctx).Where(path => path != null).ToList();
            outputs = task.Outputs(ctx).Where(path => path != null).ToList();
            // After a tuning the prediction reads the model folder, and on its own it requires the
            // tuning's checkpoint, the model file, beside it (see Run).
            if (phase == PREDICT_LIBRARY_TASK && _settings.Training != null)
                inputs.Add(ModelPath(_config));
            // The pairing manifest is written beside the library it pairs, after the prediction.
            if (phase == PREDICT_LIBRARY_TASK && _config.LibraryBuild.Entrapment)
                outputs.Add(Path.Combine(WorkDirectory(_config), PAIRING_FILE_NAME));
        }

        /// <summary>
        /// The training-run RT reference (<c>--rt-reference</c>), checked before any phase runs, as
        /// CarafeSharp's training checks it before the fine-tune: it needs the tuning runs aligned and
        /// the Chronologer RT model, and must name one of the tuning runs. The tuning predicts no
        /// library of its own here, so its own check, which reads the library's reference, never runs.
        /// </summary>
        private void CheckRtReference(LibrarySettings library, TrainingSettings training)
        {
            string rtReference = library.RtReference;
            if (rtReference == null)
                return;
            if (training.RtAlignment != RtAlignmentType.kde)
            {
                throw new ArgumentException(string.Format(@"{0} {1} needs the training runs aligned ({2} kde), not {2} {3}.",
                    CarafeCommandLine.ARG_RT_REFERENCE.ArgumentText, rtReference, CarafeCommandLine.ARG_RT_ALIGN.ArgumentText, training.RtAlignment));
            }
            var rtModel = training.RtModelType ?? LibrarySettings.DEFAULT_RT_MODEL;
            if (rtModel != RtModelType.chronologer)
            {
                throw new ArgumentException(string.Format(@"{0} {1} needs the Chronologer RT model, not {2}.",
                    CarafeCommandLine.ARG_RT_REFERENCE.ArgumentText, rtReference, rtModel));
            }
            // The tuning runs' names are their exports' stems, as the tuning names them.
            RtAlignment.FindRun(_config.LibraryBuild.TrainingInputs.Select(Path.GetFileNameWithoutExtension).ToArray(), rtReference);
        }

        /// <summary>
        /// The library is predicted from the model folder, so a current <c>&lt;out&gt;.model.zip</c>
        /// whose folder no longer holds the files it packed, byte for byte (the working files deleted
        /// with the model kept, or rewritten by a later tuning that died before it saved its model),
        /// is discarded, and the tuning runs again from the exports, rather than skipping the tuning
        /// and predicting from other weights than the saved model holds.
        /// </summary>
        private static void DiscardModelWithoutFolder(string modelFile, string modelFolder)
        {
            if (!File.Exists(modelFile))
                return;
            bool complete;
            try
            {
                complete = CarafeModelFile.Open(modelFile).Entries.All(entry => IsSameFile(Path.Combine(modelFolder, entry.Key), entry.Value));
            }
            catch (InvalidDataException)
            {
                // A model file that does not open is no checkpoint either.
                complete = false;
            }
            if (!complete)
                File.Delete(modelFile);
        }

        private static bool IsSameFile(string path, string sha256)
        {
            if (!File.Exists(path))
                return false;
            using (var stream = File.OpenRead(path))
                return string.Equals(Convert.ToHexString(SHA256.HashData(stream)), sha256, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The four stages over the tuning runs, with the training library, into a folder of their
        /// own, writing each run's training export (<see cref="TrainingSearchConfig"/>). Its artifacts
        /// are per run and named by the run, so a tuning run that is also an input keeps two sets,
        /// one per library; the spectra cache is the one both analyses read.
        /// <para>
        /// The stages are a task set of their own: a task holds what one analysis hands from one of
        /// its stages to the next (PerFileRescoring's survivor plan, which SecondPassFDR rebuilds
        /// against), so the output's search must not inherit this one's.
        /// </para>
        /// </summary>
        private int RunTrainingSearch()
        {
            var config = TrainingSearchConfig(_config);
            var pipeline = OspreyTasks.Create().PipelineFor(null);
            config.SelectTask(null, pipeline);
            using (EnterTrainingSearch(config))
                return new AnalysisPipeline().Run(config, pipeline);
        }

        /// <summary>
        /// Until disposed, writes the artifacts into the tuning runs' search folder
        /// (<paramref name="trainingSearch"/>'s output directory) and resolves the spectra cache as
        /// the output's search resolves it, so a run both analyses read is parsed once and a run
        /// without <c>--output-dir</c> never probes the data directory. For the search in one
        /// process and for a stage node of it alike; <paramref name="createDirectory"/> false for a
        /// <c>--task-files</c> query, which creates nothing.
        /// </summary>
        public static IDisposable EnterTrainingSearch(OspreyConfig trainingSearch, bool createDirectory = true)
        {
            if (createDirectory)
                Directory.CreateDirectory(trainingSearch.OutputDir);
            string outputDir = ArtifactPaths.OutputDir;
            var cacheScope = ArtifactPaths.ResolveCacheAs(outputDir);
            ArtifactPaths.OutputDir = trainingSearch.OutputDir;
            return new ScopeExit(() =>
            {
                ArtifactPaths.OutputDir = outputDir;
                cacheScope.Dispose();
            });
        }

        private sealed class ScopeExit : IDisposable
        {
            private readonly Action _exit;

            public ScopeExit(Action exit)
            {
                _exit = exit;
            }

            public void Dispose()
            {
                _exit();
            }
        }

        /// <summary>
        /// Points <paramref name="library"/> at <paramref name="path"/>, with its working files in
        /// <paramref name="workDirectory"/>. The digest marks its decoys the way the entrapment
        /// FASTA build does, and the library's own decoy flag follows the same prefix.
        /// </summary>
        private static void PrepareLibrary(LibrarySettings library, string path, string workDirectory)
        {
            library.OutputFile = path;
            library.OutputDirectory = workDirectory;
            library.DecoyPrefix = EntrapmentFastaSettings.DEFAULT_DECOY_PREFIX;
        }

        /// <summary>One library phase through the shared driver, with Osprey's <c>[TASK]</c> lines.</summary>
        private int RunPhase(PipelineTask task)
        {
            LoadTorchRuntime();
            var tasks = new[] { task };
            return new LibraryPhaseDriver().Run(tasks, new CarafeContext(tasks, CommandOutput.Out, CommandOutput.Out));
        }

        /// <summary>
        /// Loads the libtorch runtime the library phases predict with, before the first of them, and
        /// settles the device they run on from the runtime loaded: a process keeps the first libtorch
        /// it loads, so the choice is made once, here, and logged.
        /// </summary>
        private void LoadTorchRuntime()
        {
            if (_torchLoaded)
                return;
            string device = new OspreyTorchRuntimes(_config.LibraryBuild).Load(_config.LibraryBuild.Device);
            _settings.Library.Device = device;
            if (_settings.Training != null)
                _settings.Training.Device = device;
            _torchLoaded = true;
        }

        /// <summary>
        /// The digest <paramref name="library"/> is predicted over: the FASTA's targets with their
        /// decoys, as the entrapment FASTA build makes them, and with <paramref name="entrapment"/>
        /// their entrapment peptides and the decoys of those. The digest, the modifications, the
        /// charges and the precursor m/z window are the library's own, so one setting decides both
        /// what is digested and what is predicted. No m/z filter here: the prediction applies the
        /// window per charge.
        /// </summary>
        private EntrapmentFastaSettings DigestSettings(LibrarySettings library, bool entrapment)
        {
            var build = _config.LibraryBuild;
            return new EntrapmentFastaSettings
            {
                InputFasta = build.FastaPath,
                AddEntrapment = entrapment,
                AddDecoys = true,
                ApplyMzFilter = false,
                Charges = library.Charges,
                MinMz = library.MinPrecursorMz,
                MaxMz = library.MaxPrecursorMz,
                EntrapmentRatio = build.EntrapmentRatio ?? 1.0,
                EntrapmentSourceFasta = entrapment ? build.EntrapmentFastaPath : null,
                DecoyPrefix = library.DecoyPrefix,
                Digest = library.Digest,
                Modifications = library.Modifications,
                // The per-protein peptide counter keeps a predictor that deduplicates FASTA entries
                // by accession from dropping them; nothing here reads a FASTA, so the library's
                // accessions are the proteome's.
                UniqueAccessions = false,
            };
        }

        /// <summary>
        /// The text for a failure of the library build, following CarafeSharp's own command line:
        /// its settings refuse bad input as an <see cref="ArgumentException"/> (the exact type) and
        /// an unported Carafe option as a <see cref="NotSupportedException"/>, which are messages
        /// for the user; everything else follows Osprey's rule (<see cref="Program.DescribeFailure"/>).
        /// </summary>
        private static string DescribeFailure(Exception ex)
        {
            var reported = CommonExceptionUtil.UnwrapUserException(ex);
            if ((reported.GetType() == typeof(ArgumentException) || reported is NotSupportedException) && !string.IsNullOrEmpty(reported.Message))
                return reported.Message;
            return Program.DescribeFailure(ex, OspreyResources.Program_Run_Fatal_error___0_);
        }

        /// <summary>
        /// The shared driver with Osprey's <c>[TASK]</c> lines around the library phases, so the
        /// log reads as one run and the regression gate's reader sees the prediction start, skip
        /// and finish as it sees the four search stages.
        /// </summary>
        private sealed class LibraryPhaseDriver : PipelineDriver
        {
            protected override void OnSkipped(PipelineTask task, TaskContext ctx)
            {
                Program.LogInfo(LogTag.TASK, @"{0}:skipping (outputs valid)", task.Name);
            }

            protected override void OnStarting(PipelineTask task, TaskContext ctx)
            {
                Program.LogInfo(LogTag.TASK, @"{0}:starting", task.Name);
            }

            protected override void OnDone(PipelineTask task, TaskContext ctx, TimeSpan elapsed)
            {
                Program.LogInfo(LogTag.TASK, @"{0}:done ({1:F1}s)", task.Name, elapsed.TotalSeconds);
                TaskBudget.TaskDone(task.Name, ctx.ExitCode);
            }
        }
    }
}
