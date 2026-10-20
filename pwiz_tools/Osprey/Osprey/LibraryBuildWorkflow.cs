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
using System.IO.Compression;
using System.Linq;
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
    /// <c>&lt;out&gt;.library.blib</c>, where <c>-l</c> takes it next time, and the tuned model as
    /// <c>&lt;out&gt;.carafemodel</c>, where <c>--model</c> takes it; the working files go under
    /// <c>&lt;out&gt;.library/</c>. All follow <c>--output-dir</c> as the run's other artifacts do.
    /// Every phase checkpoints in the file it writes, stamped, so a re-run skips each phase whose
    /// inputs and settings are unchanged and resumes a run that died at the first one that is not.
    /// </para>
    /// <para>
    /// TorchSharp and its native runtime load when a prediction or the tuning runs and never for a
    /// run that searches a given library: nothing on the <c>-l</c> path references this class.
    /// </para>
    /// </summary>
    internal sealed class LibraryBuildWorkflow
    {
        /// <summary>The suffix of the library and of its working folder, after the output's stem.</summary>
        public const string LIBRARY_SUFFIX = @".library";

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
        public const string PREDICT_TRAINING_LIBRARY_TASK = @"PredictTrainingLibrary";

        /// <summary>The phase that tunes the models on the training exports.</summary>
        public const string TRAIN_MODEL_TASK = @"TrainModel";

        /// <summary>The library as the help spells it, after the output's stem.</summary>
        public const string LIBRARY_EXAMPLE = @"<output>" + LIBRARY_SUFFIX + LibrarySource.EXT_BLIB;

        /// <summary>The working folder as the help spells it, after the output's stem.</summary>
        public const string LIBRARY_SUFFIX_EXAMPLE = @"<output>" + LIBRARY_SUFFIX + @"/";

        /// <summary>The pairing manifest as the help spells it.</summary>
        public const string PAIRING_FILE_EXAMPLE = LIBRARY_SUFFIX_EXAMPLE + PAIRING_FILE_NAME;

        private readonly OspreyConfig _config;
        private readonly LibraryBuildSettings _settings;

        /// <param name="config">The run's configuration, whose <see cref="OspreyConfig.LibraryBuild"/> is on.</param>
        /// <param name="settings">What to predict and tune, as the command line interpreted it.</param>
        public LibraryBuildWorkflow(OspreyConfig config, LibraryBuildSettings settings)
        {
            _config = config;
            _settings = settings;
        }

        /// <summary>
        /// Shortens the tuning's fine-tune; for the subset tests, which run the whole command in
        /// seconds and cannot afford one. Not a setting: it is not in the tuning's validity key.
        /// </summary>
        internal static Action<FineTuneOptions> ConfigureFineTuneForTests { get; set; }

        /// <summary>The library the run predicts and then searches: <c>&lt;out&gt;.library.blib</c>.</summary>
        public static string LibraryPath(OspreyConfig config)
        {
            return Path.Combine(BaseDirectory(config), OutputStem(config) + LIBRARY_SUFFIX + LibrarySource.EXT_BLIB);
        }

        /// <summary>The model the tuning writes: <c>&lt;out&gt;.carafemodel</c>.</summary>
        public static string ModelPath(OspreyConfig config)
        {
            return Path.Combine(BaseDirectory(config), OutputStem(config) + CarafeModelFile.EXTENSION);
        }

        /// <summary>The library build's working folder: <c>&lt;out&gt;.library/</c>.</summary>
        public static string WorkDirectory(OspreyConfig config)
        {
            return Path.Combine(BaseDirectory(config), OutputStem(config) + LIBRARY_SUFFIX);
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

        /// <summary>Runs the phases in order; the exit code of the first that fails, else the search's.</summary>
        public int Run(IReadOnlyList<OspreyTask> pipeline)
        {
            string libraryPath = LibraryPath(_config);
            string workDirectory = WorkDirectory(_config);
            var training = _settings.Training;
            // Without tuning the library is predicted with the pretrained models or --model; with it,
            // from the models the tuning writes.
            var library = training?.Library ?? _settings.Library;
            PrepareLibrary(library, libraryPath, workDirectory);
            try
            {
                if (training != null)
                {
                    CheckRtReference(library, training);
                    int tuningExit = RunTuning(training, workDirectory);
                    if (tuningExit != 0)
                        return tuningExit;
                    // Predicted as CarafeSharp's training predicts it right after the fine-tune: from
                    // the folder the tuning wrote, which is the prediction's input, so a new tuning
                    // predicts the library again.
                    library.ModelDirectory = Path.Combine(workDirectory, MODEL_FOLDER);
                    library.PreferSafetensors = true;
                }

                var predict = new PredictLibraryTask(library, DigestSettings(library, _config.LibraryBuild.Entrapment));
                int exitCode = RunPhase(predict);
                if (exitCode != 0)
                    return exitCode;
                // The pairing manifest for FDRBench, which reads the entrapment pairing from the
                // file; Osprey itself reads it from the library's DecoyPairs table. Written when the
                // prediction ran, and when a skipped prediction (the library is current) finds no
                // manifest beside it - a write that failed after the library was committed is made
                // good here from the same seeded digest, in seconds, rather than by deleting the
                // library.
                if (_config.LibraryBuild.Entrapment)
                {
                    string pairingPath = Path.Combine(workDirectory, PAIRING_FILE_NAME);
                    if (predict.Peptides != null || !File.Exists(pairingPath))
                    {
                        var peptides = predict.Peptides ?? LibraryPeptides.Build(DigestSettings(library, true), TextWriter.Null);
                        peptides.WriteManifest(pairingPath);
                        Program.LogInfo(string.Format(OspreyResources.LibraryBuildWorkflow_Run_Wrote_the_pairing_manifest__0_, pairingPath));
                    }
                }
            }
            catch (Exception ex)
            {
                Program.LogError(DescribeFailure(ex));
                return Program.EXIT_CODE_FAILURE_TO_START;
            }

            Program.LogInfo(string.Format(OspreyResources.LibraryBuildWorkflow_Run_Searching_the_predicted_library__0_, libraryPath));
            Program.LogInfo(string.Empty);
            // The search reads the library it was given like any other: with its decoys and the
            // pairing its DecoyPairs table carries.
            _config.LibrarySource = LibrarySource.FromPath(libraryPath);
            _config.DecoysInLibrary = true;
            return new AnalysisPipeline().Run(_config, pipeline);
        }

        /// <summary>
        /// The three phases of <c>--training-input</c> before the final prediction: the training
        /// library (never with entrapment), the search of the tuning runs with the training export,
        /// and the tuning on the exports, which writes the model folder and <c>&lt;out&gt;.carafemodel</c>.
        /// </summary>
        private int RunTuning(TrainingSettings training, string workDirectory)
        {
            var trainingLibrary = _settings.Library;
            string trainingLibraryPath = Path.Combine(workDirectory, TRAINING_LIBRARY_FILE_NAME);
            PrepareLibrary(trainingLibrary, trainingLibraryPath, workDirectory);
            // The training-run RT reference names a run whose minutes the tuned models map to; the
            // models this library is predicted with have no such map.
            trainingLibrary.RtReference = null;
            int exitCode = RunPhase(new PredictLibraryTask(trainingLibrary, DigestSettings(trainingLibrary, false), PREDICT_TRAINING_LIBRARY_TASK));
            if (exitCode != 0)
                return exitCode;

            string trainingDirectory = Path.Combine(workDirectory, TRAINING_FOLDER);
            Program.LogInfo(string.Format(OspreyResources.LibraryBuildWorkflow_Run_Searching_the_tuning_runs_with__0_, trainingLibraryPath));
            Program.LogInfo(string.Empty);
            exitCode = RunTrainingSearch(trainingLibraryPath, trainingDirectory);
            if (exitCode != 0)
                return exitCode;
            Program.LogInfo(string.Empty);

            // The tuning predicts no library of its own: the final prediction is a phase of its own,
            // so a change to the entrapment or the inputs never tunes again.
            training.Library = null;
            training.Identifications = trainingDirectory;
            training.MsFiles = _config.LibraryBuild.TrainingInputs.Select(Path.GetFullPath).ToArray();
            training.OutputDirectory = Path.Combine(workDirectory, MODEL_FOLDER);
            training.ModelFile = ModelPath(_config);
            DiscardModelWithoutFolder(training.ModelFile, training.OutputDirectory);
            return RunPhase(new TrainTask(training, TRAIN_MODEL_TASK) { ConfigureFineTune = ConfigureFineTuneForTests });
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
        /// The library is predicted from the model folder, so a current <c>&lt;out&gt;.carafemodel</c>
        /// whose folder lost any of the files it packed (the working files deleted, the model kept)
        /// is discarded, and the tuning runs again from the exports, rather than skipping the tuning
        /// and predicting from what is left.
        /// </summary>
        private static void DiscardModelWithoutFolder(string modelFile, string modelFolder)
        {
            if (!File.Exists(modelFile))
                return;
            bool complete;
            using (var archive = ZipFile.OpenRead(modelFile))
            {
                complete = archive.Entries.Where(e => e.Name.Length > 0 && e.FullName != CarafeModelFile.MANIFEST_ENTRY)
                    .All(e => File.Exists(Path.Combine(modelFolder, e.FullName)));
            }
            if (!complete)
                File.Delete(modelFile);
        }

        /// <summary>
        /// The four stages over the tuning runs, with the training library, into a folder of their
        /// own, writing each run's training export. Its artifacts are per run and named by the run,
        /// so a tuning run that is also an input keeps two sets, one per library; the spectra cache
        /// is the one both analyses read.
        /// <para>
        /// The stages are a task set of their own: a task holds what one analysis hands from one of
        /// its stages to the next (PerFileRescoring's survivor plan, which SecondPassFDR rebuilds
        /// against), so the output's search must not inherit this one's.
        /// </para>
        /// </summary>
        private int RunTrainingSearch(string trainingLibraryPath, string trainingDirectory)
        {
            var config = _config.ShallowClone();
            var pipeline = OspreyTasks.Create().PipelineFor(null);
            config.SelectTask(null, pipeline);
            config.InputFiles = new List<string>(_config.LibraryBuild.TrainingInputs);
            config.LibrarySource = LibrarySource.FromPath(trainingLibraryPath);
            config.DecoysInLibrary = true;
            config.OutputBlib = Path.Combine(trainingDirectory, TRAINING_OUTPUT_FILE_NAME);
            config.OutputDir = trainingDirectory;
            // What the user asked of the run is the final search's; this one writes what the tuning reads.
            config.OutputReport = null;
            config.OutputFdrBench = null;
            config.ModelDiagnostics = false;
            // Not the one command's analysis: its result was not searched with tuned models.
            config.LibraryBuild = new LibraryBuildConfig();
            config.TrainingExport = new TrainingExportConfig
            {
                Enabled = true,
                MaxQ = _config.TrainingExport.MaxQ,
                ClaimantQ = _config.TrainingExport.ClaimantQ,
                WriteXics = _config.TrainingExport.WriteXics,
            };
            Directory.CreateDirectory(trainingDirectory);

            string outputDir = ArtifactPaths.OutputDir;
            // The cache resolves as the output's search resolves it, so a run both analyses read is
            // parsed once and a run without --output-dir never probes the data directory.
            using (ArtifactPaths.ResolveCacheAs(outputDir))
            {
                ArtifactPaths.OutputDir = trainingDirectory;
                try
                {
                    return new AnalysisPipeline().Run(config, pipeline);
                }
                finally
                {
                    ArtifactPaths.OutputDir = outputDir;
                }
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
        private static int RunPhase(PipelineTask task)
        {
            var tasks = new[] { task };
            return new LibraryPhaseDriver().Run(tasks, new CarafeContext(tasks, CommandOutput.Out, CommandOutput.Out));
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
            }
        }
    }
}
