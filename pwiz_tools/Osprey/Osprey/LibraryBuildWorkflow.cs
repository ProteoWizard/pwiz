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
using pwiz.CarafeSharp.Proteome;
using pwiz.CarafeSharp.Tasks;
using pwiz.Common.Pipeline;
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Core;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey
{
    /// <summary>
    /// The one command from a protein FASTA to a <c>.blib</c> (<c>--fasta</c>): a workflow of
    /// phases above the four-stage pipeline, each phase a CarafeSharp task run in-process through
    /// the shared driver or an Osprey analysis. With the pretrained models or a saved one
    /// (<c>--model</c>) the phases are two: predict the library, then search it. The pipeline
    /// itself stays the fixed four-element list the architecture document is built on; this class
    /// sequences what runs before it and derives the search's library from what it wrote.
    /// <para>
    /// The library is predicted over a digest made in memory from the FASTA (its decoys and,
    /// with <c>--entrapment</c>, its entrapment peptides included), so no peptide FASTA, no
    /// pairing manifest and no TSV sit between the two phases. It lands beside the output, as
    /// <c>&lt;out&gt;.library.blib</c>, where <c>-l</c> takes it next time; its working files go
    /// under <c>&lt;out&gt;.library/</c>. Both follow <c>--output-dir</c> as the run's other
    /// artifacts do. The prediction task carries its stamp inside the library, so a re-run with
    /// the same FASTA, settings and models skips it and goes straight to the search, which keys on
    /// the library file as it keys on any other.
    /// </para>
    /// <para>
    /// TorchSharp and its native runtime load when the prediction task runs and never for a run
    /// that searches a given library: nothing on the <c>-l</c> path references this class.
    /// </para>
    /// </summary>
    internal sealed class LibraryBuildWorkflow
    {
        /// <summary>The suffix of the library and of its working folder, after the output's stem.</summary>
        public const string LIBRARY_SUFFIX = @".library";

        /// <summary>The FDRBench pairing manifest written into the working folder with <c>--entrapment</c>.</summary>
        public const string PAIRING_FILE_NAME = @"pairing.tsv";

        /// <summary>The working folder as the help spells it, after the output's stem.</summary>
        public const string LIBRARY_SUFFIX_EXAMPLE = @"<output>" + LIBRARY_SUFFIX + @"/";

        /// <summary>The pairing manifest as the help spells it.</summary>
        public const string PAIRING_FILE_EXAMPLE = LIBRARY_SUFFIX_EXAMPLE + PAIRING_FILE_NAME;

        private readonly OspreyConfig _config;
        private readonly LibrarySettings _library;

        /// <param name="config">The run's configuration, whose <see cref="OspreyConfig.LibraryBuild"/> is on.</param>
        /// <param name="library">The library to predict, as the command line interpreted it.</param>
        public LibraryBuildWorkflow(OspreyConfig config, LibrarySettings library)
        {
            _config = config;
            _library = library;
        }

        /// <summary>The library the run predicts and then searches: <c>&lt;out&gt;.library.blib</c>.</summary>
        public static string LibraryPath(OspreyConfig config)
        {
            return Path.Combine(BaseDirectory(config), OutputStem(config) + LIBRARY_SUFFIX + LibrarySource.EXT_BLIB);
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
            _library.OutputFile = libraryPath;
            _library.OutputDirectory = workDirectory;
            // The digest marks its decoys the way the entrapment FASTA build does, and the
            // library's own decoy flag follows the same prefix.
            _library.DecoyPrefix = EntrapmentFastaSettings.DEFAULT_DECOY_PREFIX;

            var predict = new PredictLibraryTask(_library, DigestSettings());
            var tasks = new PipelineTask[] { predict };
            var ctx = new CarafeContext(tasks, CommandOutput.Out, CommandOutput.Out);
            try
            {
                int exitCode = new LibraryPhaseDriver().Run(tasks, ctx);
                if (exitCode != 0)
                    return exitCode;
                // The pairing manifest for FDRBench, which reads the entrapment pairing from the
                // file; Osprey itself reads it from the library's DecoyPairs table. Written when the
                // prediction ran; a skipped prediction left the earlier one in place.
                if (predict.Peptides != null && predict.Peptides.HasEntrapment)
                {
                    string pairingPath = Path.Combine(workDirectory, PAIRING_FILE_NAME);
                    predict.Peptides.WriteManifest(pairingPath);
                    Program.LogInfo(string.Format(OspreyResources.LibraryBuildWorkflow_Run_Wrote_the_pairing_manifest__0_, pairingPath));
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
        /// The digest the library is predicted over: the FASTA's targets with their decoys, as the
        /// entrapment FASTA build makes them, and with <c>--entrapment</c> their entrapment peptides
        /// and the decoys of those. The digest, the modifications, the charges and the precursor m/z
        /// window are the library's own, so one setting decides both what is digested and what is
        /// predicted. No m/z filter here: the prediction applies the window per charge.
        /// </summary>
        private EntrapmentFastaSettings DigestSettings()
        {
            var build = _config.LibraryBuild;
            return new EntrapmentFastaSettings
            {
                InputFasta = build.FastaPath,
                AddEntrapment = build.Entrapment,
                AddDecoys = true,
                ApplyMzFilter = false,
                Charges = _library.Charges,
                MinMz = _library.MinPrecursorMz,
                MaxMz = _library.MaxPrecursorMz,
                EntrapmentRatio = build.EntrapmentRatio ?? 1.0,
                EntrapmentSourceFasta = build.EntrapmentFastaPath,
                DecoyPrefix = _library.DecoyPrefix,
                Digest = _library.Digest,
                Modifications = _library.Modifications,
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
