/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using JetBrains.Annotations;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib
{
    public static class EncyclopeDiaHelpers
    {
        public static string ENCYCLOPEDIA_VERSION = @"2.12.30";
        public static string ENCYCLOPEDIA_FILENAME = $@"encyclopedia-{ENCYCLOPEDIA_VERSION}-executable.jar";
        static Uri ENCYCLOPEDIA_URL = new Uri($@"https://bitbucket.org/searleb/encyclopedia/downloads/{ENCYCLOPEDIA_FILENAME}");
        public static string EncyclopeDiaDirectory => ToolDescriptionHelpers.GetToolsDirectory();
        public static string EncyclopeDiaBinary => Path.Combine(EncyclopeDiaDirectory, ENCYCLOPEDIA_FILENAME);

        public static FileDownloadInfo EncyclopeDiaDownloadInfo => new FileDownloadInfo
        {
            Filename = ENCYCLOPEDIA_FILENAME, DownloadUrl = ENCYCLOPEDIA_URL, InstallPath = EncyclopeDiaDirectory,
            OverwriteExisting = true, Unzip = false
        };
        public static FileDownloadInfo[] FilesToDownload => Java8DownloadInfo.FilesToDownload.Concat(new[] {
            EncyclopeDiaDownloadInfo
        }).ToArray();

        public static string JavaBinary => Java8DownloadInfo.JavaBinary;

        private static bool EnsureRequiredFilesDownloaded(IEnumerable<FileDownloadInfo> requiredFiles, IProgressMonitor progressMonitor)
        {
            var requiredFilesList = requiredFiles.ToList();
            var filesNotAlreadyDownloaded = SimpleFileDownloader.FilesNotAlreadyDownloaded(requiredFilesList).ToList();
            if (!filesNotAlreadyDownloaded.Any())
                return true;

            if (!SimpleFileDownloader.DownloadRequiredFiles(requiredFilesList, progressMonitor))
                return false;

            return !SimpleFileDownloader.FilesNotAlreadyDownloaded(filesNotAlreadyDownloaded).Any();
        }

        public static class EnzymeInfo
        {

            // ReSharper disable LocalizableElement
            static SortedSet<string> _supportedEnzymes = new SortedSet<string>
            {
                "Trypsin", "Trypsin/p", "Lys-C", "Lys-N", "Arg-C", "Glu-C", "Chymotrypsin", "Pepsin A", "Elastase",
                "Thermolysin", "No Enzyme"
            };
            // ReSharper restore LocalizableElement

            public static SortedSet<string> SupportedEnzymes => _supportedEnzymes;
        }

        public class FastaToKoinaInputCsvConfig
        {
            [CanBeNull] private string _enzyme;

            [CanBeNull, Track]
            public string Enzyme
            {
                get { return _enzyme; }
                set
                {
                    if (value == null)
                    {
                        _enzyme = null;
                        return;
                    }

                    if (!EnzymeInfo.SupportedEnzymes.Contains(value))
                        throw new ArgumentOutOfRangeException(string.Format(
                            LibResources.FastaToKoina_Enzyme_unsupported_enzyme___0____allowed_values_are___1_, value,
                            string.Join(@", ", EnzymeInfo.SupportedEnzymes)));
                    _enzyme = value;
                }
            }
            [Track]
            public int? DefaultNCE { get; set; }
            [Track]
            public int? DefaultCharge { get; set; }
            [Track]
            public int? MinCharge { get; set; }
            [Track]
            public int? MaxCharge { get; set; }
            [Track]
            public int? MaxMissedCleavage { get; set; }
            [Track]
            public double? MinMz { get; set; }
            [Track]
            public double? MaxMz { get; set; }

            public override string ToString()
            {
                // ReSharper disable LocalizableElement
                var sb = new StringBuilder();
                if (Enzyme != null) sb.AppendFormat(" -enzyme {0}", Enzyme);
                if (DefaultNCE.HasValue) sb.AppendFormat(" -defaultNCE {0}", DefaultNCE);
                if (DefaultCharge.HasValue) sb.AppendFormat(" -defaultCharge {0}", DefaultCharge);
                if (MinCharge.HasValue) sb.AppendFormat(" -minCharge {0}", MinCharge);
                if (MaxCharge.HasValue) sb.AppendFormat(" -maxCharge {0}", MaxCharge);
                if (MaxMissedCleavage.HasValue) sb.AppendFormat(" -maxMissedCleavage {0}", MaxMissedCleavage);
                if (MinMz.HasValue) sb.AppendFormat(CultureInfo.InvariantCulture, " -minMz {0}", MinMz);
                if (MaxMz.HasValue) sb.AppendFormat(CultureInfo.InvariantCulture, " -maxMz {0}", MaxMz);
                return sb.ToString();
                // ReSharper restore LocalizableElement
            }

            public static int MinNCE => 10;
            public static int MaxNCE => 50;
        }

        private static bool IsGoodEncyclopeDiaOutput(string stdOut, int exitCode)
        {
            return exitCode == 0 && !stdOut.Contains(@"Fatal Error") && !stdOut.Contains(@"FileSystemException");
        }

        public static void ConvertFastaToKoinaInputCsv(string fastaFilepath, string koinaCsvFilepath,
            IProgressMonitor progressMonitor, ref IProgressStatus status, FastaToKoinaInputCsvConfig config)
        {
            if (!EnsureRequiredFilesDownloaded(FilesToDownload, progressMonitor))
                throw new InvalidOperationException(Resources.EncyclopeDiaHelpers_ConvertFastaToKoinaInputCsv_could_not_find_EncyclopeDia);

            long javaMaxHeapMB = Math.Min(4 * 1024L * 1024 * 1024, MemoryInfo.TotalBytes / 2) / 1024 / 1024;
            const string csvToLibraryClasspath = "edu.washington.gs.maccoss.encyclopedia.cli.ConvertFastaToPrositCSV";

            var pr = new ProcessRunner();
            var psi = new ProcessStartInfo(JavaBinary,
                $" -Xmx{javaMaxHeapMB}M -cp {EncyclopeDiaBinary.Quote()} {csvToLibraryClasspath} {LOCALIZATION_PARAMS} -i {fastaFilepath.Quote()} -o {koinaCsvFilepath.Quote()} {config}")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            psi.EnvironmentVariables[@"TMP"] = JAVA_TMPDIR_PATH;

            status = status.ChangeMessage(LibResources.EncyclopeDiaHelpers_ConvertFastaToKoinaInputCsv_Converting_FASTA_to_Koina_input);
            if (progressMonitor.UpdateProgress(status) == UpdateProgressResponse.cancel)
                return;

            status = status.ChangeMessage(String.Format(Resources.EncyclopeDiaHelpers_GenerateLibrary_Running_command___0___1_,
                psi.FileName, psi.Arguments));
            progressMonitor.UpdateProgress(status);
            pr.Run(psi, null, progressMonitor, ref status, null, ProcessPriorityClass.BelowNormal, true, IsGoodEncyclopeDiaOutput, false);
        }

        public static void ConvertKoinaOutputToDlib(string koinaBlibFilepath, string fastaFilepath,
            string encyclopeDiaDlibFilepath, IProgressMonitor progressMonitor, ref IProgressStatus status)
        {
            if (!EnsureRequiredFilesDownloaded(FilesToDownload, progressMonitor))
                throw new InvalidOperationException(Resources.EncyclopeDiaHelpers_ConvertFastaToKoinaInputCsv_could_not_find_EncyclopeDia);

            long javaMaxHeapMB = Math.Min(12 * 1024L * 1024 * 1024, MemoryInfo.TotalBytes / 2) / 1024 / 1024;
            const string csvToLibraryClasspath = "edu.washington.gs.maccoss.encyclopedia.cli.ConvertBLIBToLibrary";

            var pr = new ProcessRunner();
            var psi = new ProcessStartInfo(JavaBinary,
                $" -Xmx{javaMaxHeapMB}M -cp {EncyclopeDiaBinary.Quote()} {csvToLibraryClasspath} {LOCALIZATION_PARAMS} -i {koinaBlibFilepath.Quote()} -f {fastaFilepath.Quote()} -o {encyclopeDiaDlibFilepath.Quote()}")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            psi.EnvironmentVariables[@"TMP"] = JAVA_TMPDIR_PATH;

            status = status.ChangeMessage(LibResources.EncyclopeDiaHelpers_ConvertKoinaOutputToDlib_Converting_Koina_output_to_EncyclopeDia_library);
            if (progressMonitor.UpdateProgress(status) == UpdateProgressResponse.cancel)
                return;

            status = status.ChangeMessage(String.Format(Resources.EncyclopeDiaHelpers_GenerateLibrary_Running_command___0___1_,
                psi.FileName, psi.Arguments));
            progressMonitor.UpdateProgress(status);
            pr.Run(psi, null, progressMonitor, ref status, null, ProcessPriorityClass.BelowNormal, true, IsGoodEncyclopeDiaOutput, false);
        }

        public static void GenerateChromatogramLibrary(string encyclopeDiaDlibInputFilepath,
            string encyclopeDiaElibOutputFilepath, string fastaFilepath, MsDataFileUri diaDataFile,
            IProgressMonitor progressMonitor, ref IProgressStatus status, EncyclopeDiaConfig config)
        {
            GenerateLibraryWithErrorHandling(encyclopeDiaDlibInputFilepath, encyclopeDiaElibOutputFilepath, fastaFilepath, diaDataFile, progressMonitor, ref status, config, false);
        }

        public static void GenerateQuantLibrary(string encyclopeDiaElibInputFilepath,
            string encyclopeDiaQuantLibOutputFilepath, string fastaFilepath, MsDataFileUri diaDataFile,
            IProgressMonitor progressMonitor, ref IProgressStatus status, EncyclopeDiaConfig config)
        {
            GenerateLibraryWithErrorHandling(encyclopeDiaElibInputFilepath, encyclopeDiaQuantLibOutputFilepath, fastaFilepath, diaDataFile, progressMonitor, ref status, config, true);
        }

        public class ParallelRunner
        {
            private readonly string _encyclopeDiaDlibInputFilepath;
            private readonly string _encyclopeDiaElibOutputFilepath;
            private readonly string _encyclopeDiaQuantElibOutputFilepath;
            private readonly string _fastaFilepath;
            private readonly EncyclopeDiaConfig _config;
            private readonly CancellationToken _cancelToken;
            private IProgressMonitor _encyclopediaProgressMonitor { get; }
            private IProgressMonitor _conversionProgressMonitor { get; }

            public IList<MsDataFileUri> NarrowWindowDiaDataFiles { get; }
            public IList<MsDataFileUri> WideWindowDiaDataFiles { get; }

            public ParallelRunner(string encyclopeDiaDlibInputFilepath, string encyclopeDiaElibOutputFilepath,
                string encyclopeDiaQuantElibOutputFilepath, string fastaFilepath, EncyclopeDiaConfig config,
                IEnumerable<MsDataFileUri> narrowWindowDiaDataFiles,
                IEnumerable<MsDataFileUri> wideWindowDiaDataFiles,
                IProgressMonitor encyclopediaProgressMonitor,
                IProgressMonitor conversionProgressMonitor,
                CancellationToken cancelToken)
            {
                _encyclopeDiaDlibInputFilepath = encyclopeDiaDlibInputFilepath;
                _encyclopeDiaElibOutputFilepath = encyclopeDiaElibOutputFilepath;
                _encyclopeDiaQuantElibOutputFilepath = encyclopeDiaQuantElibOutputFilepath;
                _fastaFilepath = fastaFilepath;
                _config = config;
                _cancelToken = cancelToken;
                NarrowWindowDiaDataFiles = narrowWindowDiaDataFiles.ToList();
                WideWindowDiaDataFiles = wideWindowDiaDataFiles.ToList();
                _encyclopediaProgressMonitor = encyclopediaProgressMonitor;
                _conversionProgressMonitor = conversionProgressMonitor;
            }

            private bool IsCanceled => _encyclopediaProgressMonitor.IsCanceled || _conversionProgressMonitor.IsCanceled;

            public void Generate(IProgressStatus status)
            {
                var progressMonitor = _encyclopediaProgressMonitor;
                var queueStatus = status;
                int totalFileCount = NarrowWindowDiaDataFiles.Count + WideWindowDiaDataFiles.Count + 2; // 2 merged elibs

                var narrowFileQueue = new ConcurrentQueue<MsDataFileUri>(NarrowWindowDiaDataFiles);
                var wideFileQueue = new ConcurrentQueue<MsDataFileUri>(WideWindowDiaDataFiles);
                var isolationSchemeByFile = new ConcurrentDictionary<MsDataFileUri, IsolationScheme>();

                // QueueWorkers convert/demultiplex input DIA result files (in parallel) to feed them to an EncyclopeDIA job (run serially)

                int convertedNarrowFiles = 0;
                var allNarrowFilesConverted = new ManualResetEventSlim(false);
                void ConsumeNarrowWindowFile(MsDataFileUri diaFile, int i)
                {
                    bool inputIsDemuxed = diaFile.GetFileName().Contains(DEMUX_SUFFIX);
                    var isolationScheme = isolationSchemeByFile[diaFile];
                    string originalFilename = diaFile.GetFileName().Replace(DEMUX_SUFFIX, string.Empty);
                    var progressMonitorForFile = new ProgressMonitorForFile(originalFilename, false, isolationScheme.PrespecifiedIsolationWindows.Count, _conversionProgressMonitor);
                    IProgressStatus statusForFile = new ProgressStatus();
                    if (inputIsDemuxed)
                        statusForFile = statusForFile.ChangeSegments(1, 2);

                    GenerateChromatogramLibrary(_encyclopeDiaDlibInputFilepath, _encyclopeDiaElibOutputFilepath, _fastaFilepath, diaFile, progressMonitorForFile, ref statusForFile, _config);

                    if (_config.LogProgressForIndividualFiles)
                        File.WriteAllText(originalFilename + ".log", progressMonitorForFile.LogText);

                    lock (narrowFileQueue)
                    {
                        ++convertedNarrowFiles;
                        queueStatus = queueStatus.ChangePercentComplete(convertedNarrowFiles * 100 / totalFileCount);
                        if (convertedNarrowFiles == NarrowWindowDiaDataFiles.Count)
                            allNarrowFilesConverted.Set();
                    }
                    progressMonitor.UpdateProgress(queueStatus);
                }

                MsDataFileUri ProduceNarrowWindowFile(int i)
                {
                    if (!narrowFileQueue.TryDequeue(out var diaFile))
                    {
                        allNarrowFilesConverted.Wait(_cancelToken);
                        return null;
                    }
                    var result = ConvertDiaDataFileAsync(diaFile, out var isolationScheme, true);
                    isolationSchemeByFile[result] = isolationScheme;
                    return result;
                }

                int demuxThreads = ParallelEx.GetThreadCount() - 1;
                int encyclopeDiaThreads = 2;

                var narrowWindowDiaConverter = new QueueWorker<MsDataFileUri>(ProduceNarrowWindowFile, ConsumeNarrowWindowFile);
                narrowWindowDiaConverter.RunAsync(encyclopeDiaThreads, @"EncyclopeDiaNarrowWindowRunner", demuxThreads, @"EncylopeDiaNarrowWindowConverterDemultiplexer");

                int convertedWideFiles = 0;
                var chromLibraryCreated = new ManualResetEventSlim(false);
                var allWideFilesConverted = new ManualResetEventSlim(false);
                void ConsumeWideWindowFile(MsDataFileUri diaFile, int i)
                {
                    bool inputIsDemuxed = diaFile.GetFileName().Contains(DEMUX_SUFFIX);
                    var isolationScheme = isolationSchemeByFile[diaFile];
                    string originalFilename = diaFile.GetFileName().Replace(DEMUX_SUFFIX, string.Empty);
                    var progressMonitorForFile = new ProgressMonitorForFile(originalFilename, false, isolationScheme.PrespecifiedIsolationWindows.Count, _conversionProgressMonitor);
                    IProgressStatus statusForFile = new ProgressStatus(LibResources.EncyclopeDiaHelpers_Generate_Waiting_for_chromatogram_library);
                    if (inputIsDemuxed)
                        statusForFile = statusForFile.ChangeSegments(1, 2);
                    progressMonitorForFile.UpdateProgress(statusForFile);
                    chromLibraryCreated.Wait(_cancelToken); // wait until the chromatogram library has been merged
                    GenerateQuantLibrary(_encyclopeDiaElibOutputFilepath, _encyclopeDiaQuantElibOutputFilepath, _fastaFilepath, diaFile, progressMonitorForFile, ref statusForFile, _config);

                    if (_config.LogProgressForIndividualFiles)
                        File.WriteAllText(originalFilename + ".log", progressMonitorForFile.LogText);

                    lock (wideFileQueue)
                    {
                        ++convertedWideFiles;
                        queueStatus = queueStatus.ChangePercentComplete((convertedNarrowFiles + convertedWideFiles + 1) * 100 / totalFileCount);
                        if (convertedWideFiles == WideWindowDiaDataFiles.Count)
                            allWideFilesConverted.Set();
                    }
                    progressMonitor.UpdateProgress(queueStatus);
                }

                MsDataFileUri ProduceWideWindowFile(int i)
                {
                    if (!wideFileQueue.TryDequeue(out var diaFile))
                    {
                        allWideFilesConverted.Wait(_cancelToken);
                        return null;
                    }
                    var result = ConvertDiaDataFileAsync(diaFile, out var isolationScheme);
                    isolationSchemeByFile[result] = isolationScheme;
                    return result;
                }

                var wideWindowDiaConverter = new QueueWorker<MsDataFileUri>(ProduceWideWindowFile, ConsumeWideWindowFile);

                // start producing (converting/demultiplexing) wide window result files without waiting for narrow window jobs to finish
                wideWindowDiaConverter.RunAsync(encyclopeDiaThreads, @"EncyclopeDiaWideWindowRunner", demuxThreads, @"EncylopeDiaWideWindowConverterDemultiplexer");

                // wait for all narrow window EncyclopeDIA jobs to finish
                while (!allNarrowFilesConverted.Wait(1000, _cancelToken) && !IsCanceled)
                {
                    lock (narrowWindowDiaConverter)
                    {
                        var exception = narrowWindowDiaConverter.Exception;
                        if (exception != null)
                            throw new OperationCanceledException(LibResources.ParallelRunner_Generate_An_EncyclopeDIA_task_failed_, exception);
                    }
                }

                if (IsCanceled)
                    return;

                status = status.ChangeMessage(string.Format(LibResources.EncyclopeDiaHelpers_GenerateLibrary_Generating_chromatogram_library_0_of_1_2,
                    convertedNarrowFiles + 1, totalFileCount, Path.GetFileName(_encyclopeDiaElibOutputFilepath)));
                if (progressMonitor.UpdateProgress(status) == UpdateProgressResponse.cancel)
                    return;

                // merge the chromatogram library for the narrow window results
                MergeLibrary(_encyclopeDiaDlibInputFilepath, _encyclopeDiaElibOutputFilepath, _fastaFilepath, NarrowWindowDiaDataFiles[0], progressMonitor, ref status, _config, false);
                
                status = status.ChangePercentComplete((convertedNarrowFiles + 1) * 100 / totalFileCount);
                progressMonitor.UpdateProgress(status);

                // allow wide window EncyclopeDIA jobs to start
                chromLibraryCreated.Set();

                // wait for all wide window EncyclopeDIA jobs to finish
                while (!allWideFilesConverted.Wait(1000, _cancelToken) && !IsCanceled)
                {
                    lock (wideWindowDiaConverter)
                    {
                        var exception = wideWindowDiaConverter.Exception;
                        if (exception != null)
                            throw new OperationCanceledException(LibResources.ParallelRunner_Generate_An_EncyclopeDIA_task_failed_, exception);
                    }
                }

                if (IsCanceled)
                    return;

                status = status.ChangeMessage(string.Format(LibResources.EncyclopeDiaHelpers_GenerateLibrary_Generating_chromatogram_library_0_of_1_2,
                    totalFileCount, totalFileCount, Path.GetFileName(_encyclopeDiaElibOutputFilepath)));
                if (progressMonitor.UpdateProgress(status) == UpdateProgressResponse.cancel)
                    return;

                // merge the quant library for the wide window results
                MergeLibrary(_encyclopeDiaElibOutputFilepath, _encyclopeDiaQuantElibOutputFilepath, _fastaFilepath, WideWindowDiaDataFiles[0], progressMonitor, ref status, _config, true);

                progressMonitor.UpdateProgress(status.ChangePercentComplete(100));
            }

            private static string MsconvertOutputExtension => @".mzML";
            private const string DEMUX_SUFFIX = "-demuxed";

            private static string GetConvertedFileOutputPath(MsDataFileUri diaDataFile, bool demultiplex)
            {
                string suffix = demultiplex ? DEMUX_SUFFIX : string.Empty;
                return Path.Combine(Path.GetDirectoryName(diaDataFile.GetFilePath()) ?? string.Empty,
                    diaDataFile.GetFileNameWithoutExtension() + suffix + MsconvertOutputExtension);
            }

            private MsDataFileUri ConvertDiaDataFileAsync(MsDataFileUri diaDataFile, out IsolationScheme isolationScheme, bool removeNonOverlappingEdges = false)
            {
                var progressMonitorForFile = new ProgressMonitorForFile(diaDataFile.GetFileName(), true, 1, _conversionProgressMonitor);
                var reader = new IsolationSchemeReader(new[] { diaDataFile });
                string isolationSchemeName = Path.GetFileNameWithoutExtension(diaDataFile.GetFileNameWithoutExtension());
                isolationScheme = reader.Import(isolationSchemeName, new SilentProgressMonitor());
                bool needsDemultiplexing = isolationScheme.SpecialHandling == IsolationScheme.SpecialHandlingType.OVERLAP ||
                                           isolationScheme.SpecialHandling == IsolationScheme.SpecialHandlingType.OVERLAP_MULTIPLEXED;
                string removeNonOverlappingEdgesOption = removeNonOverlappingEdges ? @"removeNonOverlappingEdges=true" : string.Empty;
                string outputFilepath = GetConvertedFileOutputPath(diaDataFile, needsDemultiplexing);

                IProgressStatus status = new ProgressStatus();

                if (!needsDemultiplexing && string.Compare(outputFilepath, diaDataFile.GetFilePath(), StringComparison.OrdinalIgnoreCase) == 0)
                    return diaDataFile;

                if (File.Exists(outputFilepath) &&
                    File.GetLastWriteTime(outputFilepath) > File.GetLastWriteTime(diaDataFile.GetFilePath()) &&
                    MsDataFileImpl.IsValidFile(outputFilepath))
                {
                    status = status.ChangeMessage(string.Format(
                        Resources.MsconvertDdaConverter_Run_Re_using_existing_converted__0__file_for__1__,
                        MsconvertOutputExtension, diaDataFile.GetSampleOrFileName()));
                    status = status.ChangeSegments(1, 2);
                    progressMonitorForFile.UpdateProgress(status.ChangePercentComplete(100));
                    return new MsDataFilePath(outputFilepath);
                }

                status = status.ChangeSegments(0, 2);

                const string MSCONVERT_EXE = "msconvert";

                status = status.ChangeMessage(LibResources.EncyclopeDiaHelpers_GetConvertedDiaDataFile_Converting_DIA_data_to_mzML);
                progressMonitorForFile.UpdateProgress(status);

                var pr = new ProcessRunner();
                var psi = new ProcessStartInfo(MSCONVERT_EXE)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    Arguments =
                        "-v -z --mzML " +
                        $"-o {Path.GetDirectoryName(outputFilepath).Quote()} " +
                        $"--outfile {Path.GetFileName(outputFilepath).Quote()} " +
                        " --acceptZeroLengthSpectra --simAsSpectra --combineIonMobilitySpectra" +
                        " --filter \"peakPicking true 1-\" " +
                        (needsDemultiplexing ? @$" --filter ""demultiplex {removeNonOverlappingEdgesOption}""" : "") +
                        " --runIndex " + Math.Max(0, diaDataFile.GetSampleIndex()) + " " +
                        diaDataFile.GetFilePath().Quote()
                };

                //try
                //{
                    status = status.ChangeMessage(String.Format(Resources.EncyclopeDiaHelpers_GenerateLibrary_Running_command___0___1_,
                        psi.FileName, psi.Arguments));
                    progressMonitorForFile.UpdateProgress(status);
                    pr.Run(psi, null, progressMonitorForFile, ref status, null, ProcessPriorityClass.BelowNormal, false, IsGoodEncyclopeDiaOutput, false);
                //}
                //catch (IOException e)
                //{
                //    _progressMonitor.UpdateProgress(status.ChangeMessage(e.Message));
                //}

                if (progressMonitorForFile.IsCanceled)
                {
                    FileEx.SafeDelete(outputFilepath, true);
                    return null;
                }

                var outputFile = new MsDataFilePath(outputFilepath);
                reader = new IsolationSchemeReader(new[] { outputFile });
                isolationScheme = reader.Import(isolationSchemeName, new SilentProgressMonitor());

                return outputFile;
            }

            public class ProgressMonitorForFile : IProgressMonitor
            {
                private readonly string _filename;
                private readonly bool _processAllMessages; // for msconvert show all messages; for EncyclopeDIA only show the interesting parts
                private readonly int _isolationWindowCount;
                private readonly IProgressMonitor _multiProgressMonitor;
                private int _maxPercentComplete;
                private int _processedWindows;
                private StringBuilder _logText = new StringBuilder();

                public string Filename => _filename;
                public string LogText => _logText.ToString();

                public ProgressMonitorForFile(string filename, bool processAllMessages, int isolationWindowCount, IProgressMonitor multiProgressMonitor)
                {
                    _filename = filename;
                    _processAllMessages = processAllMessages;
                    _isolationWindowCount = isolationWindowCount;
                    _multiProgressMonitor = multiProgressMonitor;
                }

                public bool IsCanceled => _multiProgressMonitor.IsCanceled;

                public UpdateProgressResponse UpdateProgress(IProgressStatus status)
                {
                    var message = status.Message;
                    _logText.AppendLine(message);

                    var match = Regex.Match(status.Message, @"writing spectra: (\d+)/(\d+)");
                    if (match.Success && match.Groups.Count == 3)
                    {
                        _maxPercentComplete = Math.Max(_maxPercentComplete,
                            Convert.ToInt32(match.Groups[1].Value) * 100 /
                            Convert.ToInt32(match.Groups[2].Value));
                        // substitute progress message for localization and to make it clear what work is being done
                        message = string.Format(LibResources.ProgressMonitorForFile_UpdateProgress_Demultiplexing_spectra___0___1_, match.Groups[1].Value, match.Groups[2].Value);
                    }
                    else if (status.Message == LibResources.EncyclopeDiaHelpers_Generate_Waiting_for_chromatogram_library ||
                             status.Message.Contains(@"Processing") ||
                             status.Message.Contains(@"Iteration") ||
                             status.Message.Contains(@"Finished analysis"))
                    {
                        message = status.Message;
                        if (status.Message.Contains(@"Processing"))
                        {
                            ++_processedWindows;
                            _maxPercentComplete = Math.Max(_maxPercentComplete,
                                _processedWindows * 100 / (_isolationWindowCount + 1));
                        }
                        else if (status.Message.Contains(@"Finished analysis"))
                            _maxPercentComplete = 100;
                    }
                    else if (status.ErrorException != null)
                    {
                        status = status.ChangePercentComplete(100);
                        _multiProgressMonitor.UpdateProgress(status.ChangeMessage(_filename + @"::" + message));
                        return UpdateProgressResponse.cancel;
                    }
                    else if (!_processAllMessages)
                    {
                        return UpdateProgressResponse.normal;
                    }

                    status = status.ChangePercentComplete(_maxPercentComplete);

                    return _multiProgressMonitor.UpdateProgress(status.ChangeMessage(_filename + @"::" + message));
                }

                public bool HasUI => _multiProgressMonitor.HasUI;
            }
        }

        private static string GetConvertedDiaDataFile(MsDataFileUri diaDataFile, string outputPath, IProgressMonitor progressMonitor, ref IProgressStatus status)
        {
            string outputFilepath = Path.Combine(outputPath, diaDataFile.GetFileName());
            if (!File.Exists(outputFilepath))
                FileEx.HardLinkOrCopyFile(diaDataFile.GetFilePath(), outputFilepath);
            return outputFilepath;
        }


        public class EncyclopeDiaConfig
        {
            public EncyclopeDiaConfig()
            {
                Parameters = new Dictionary<string, AbstractDdaSearchEngine.Setting>();
                foreach(var kvp in DefaultParameters)
                    Parameters[kvp.Key] = new AbstractDdaSearchEngine.Setting(kvp.Value);
                V2scoring = true; // EncyclopeDIA defaults to V1 but we want to default to V2
                LogProgressForIndividualFiles = false;
            }

            public IDictionary<string, AbstractDdaSearchEngine.Setting> Parameters { get; }
            public bool LogProgressForIndividualFiles { get; set; }

            // ReSharper disable LocalizableElement
            public static readonly ImmutableDictionary<string, AbstractDdaSearchEngine.Setting> DefaultParameters =
                new ImmutableDictionary<string, AbstractDdaSearchEngine.Setting>(new Dictionary<string, AbstractDdaSearchEngine.Setting>
                {
                    { "Enzyme", new AbstractDdaSearchEngine.Setting("Enzyme", "Trypsin", EnzymeInfo.SupportedEnzymes) },
                    { "Fixed", new AbstractDdaSearchEngine.Setting("Fixed") },
                    { "Frag", new AbstractDdaSearchEngine.Setting("Frag", "CID", Enum.GetNames(typeof(FragmentationType))) },
                    { "Ptol", new AbstractDdaSearchEngine.Setting("Ptol", 10, 0, 1e8) },
                    { "PtolUnits", new AbstractDdaSearchEngine.Setting("PtolUnits", "PPM", Enum.GetNames(typeof(MassErrorType))) },
                    { "Ftol", new AbstractDdaSearchEngine.Setting("Ftol", 10, 0, 1e8) },
                    { "FtolUnits", new AbstractDdaSearchEngine.Setting("FtolUnits", "PPM", Enum.GetNames(typeof(MassErrorType))) },
                    { "Lftol", new AbstractDdaSearchEngine.Setting("Lftol", 10, 0, 1e8) },
                    { "LftolUnits", new AbstractDdaSearchEngine.Setting("LftolUnits", "PPM", Enum.GetNames(typeof(MassErrorType))) },
                    { "Poffset", new AbstractDdaSearchEngine.Setting("Poffset", 0.0) },
                    { "Foffset", new AbstractDdaSearchEngine.Setting("Foffset", 0.0) },
                    { "PercolatorThreshold", new AbstractDdaSearchEngine.Setting("PercolatorThreshold", 0.01, 0, 1) },
                    //{ "PercolatorVersion", new AbstractDdaSearchEngine.Setting("PercolatorVersion", },
                    { "PercolatorTrainingSetSize", new AbstractDdaSearchEngine.Setting("PercolatorTrainingSetSize", 500000, 0) },
                    { "PercolatorTrainingFDR", new AbstractDdaSearchEngine.Setting("PercolatorTrainingFDR", 0, 0, 1.0) },
                    { "Acquisition", new AbstractDdaSearchEngine.Setting("Acquisition", "DIA", Enum.GetNames(typeof(DataAcquisitionType))) },
                    { "NumberOfThreadsUsed", new AbstractDdaSearchEngine.Setting("NumberOfThreadsUsed", 32) },
                    { "ExpectedPeakWidth", new AbstractDdaSearchEngine.Setting("ExpectedPeakWidth", 25, 0, 1000) },
                    { "PrecursorWindowSize", new AbstractDdaSearchEngine.Setting("PrecursorWindowSize", -1.0) },
                    { "NumberOfQuantitativePeaks", new AbstractDdaSearchEngine.Setting("NumberOfQuantitativePeaks", 5, 0) },
                    { "MinNumOfQuantitativePeaks", new AbstractDdaSearchEngine.Setting("MinNumOfQuantitativePeaks", 3, 0) },
                    { "TopNTargetsUsed", new AbstractDdaSearchEngine.Setting("TopNTargetsUsed", -1) },
                    { "QuantifyAcrossSamples", new AbstractDdaSearchEngine.Setting("QuantifyAcrossSamples", false) },
                    { "NumberOfExtraDecoyLibrariesSearched", new AbstractDdaSearchEngine.Setting("NumberOfExtraDecoyLibrariesSearched", 0.0, 0.0) },
                    { "VerifyModificationIons", new AbstractDdaSearchEngine.Setting("VerifyModificationIons", true) },
                    { "MinIntensity", new AbstractDdaSearchEngine.Setting("MinIntensity", -1.0) },
                    { "RtWindowInMin", new AbstractDdaSearchEngine.Setting("RtWindowInMin", -1.0) },
                    { "FilterPeaklists", new AbstractDdaSearchEngine.Setting("FilterPeaklists", false) },
                    { "V2scoring", new AbstractDdaSearchEngine.Setting("V2scoring", false) }
                });
            // ReSharper restore LocalizableElement

            public static readonly EncyclopeDiaConfig DEFAULT = new EncyclopeDiaConfig();

            public enum FragmentationType
            {
                CID,
                ETD,
                HCD
            }

            public enum MassErrorType
            {
                PPM,
                AMU,
                Resolution
            }

            public enum DataAcquisitionType
            {
                DIA,
                OverlappingDIA
            }

            [Track(ignoreName:true)]
            public IList<KeyValuePair<string, string>> AuditParameters
            {
                get
                {
                    var result = new List<KeyValuePair<string, string>>();
                    foreach (var kvp in Parameters)
                    {
                        if (kvp.Value.ValidValues?.Any() ?? false)
                            result.Add(new KeyValuePair<string, string>(kvp.Key, (string) kvp.Value.Value));
                        else if (!string.IsNullOrWhiteSpace(kvp.Value.Value.ToString()))
                            result.Add(new KeyValuePair<string, string>(kvp.Key, kvp.Value.Value.ToString()));
                    }

                    return result;
                }
            }

            #region Get/Set utility functions
            private T GetValue<T>(string name)
            {
                return (T) Parameters[name].Value;
            }
            private T GetValueEnum<T>(string name) where T : Enum
            {
                return (T) Enum.Parse(typeof(T), (string)Parameters[name].Value);
            }
            private void SetValue<T>(string name, T value)
            {
                Parameters[name].Value = value;
            }
            private void SetValueEnum<T>(string name, T value) where T : Enum
            {
                Parameters[name].Value = value == null ? DefaultParameters[name].Value : Enum.GetName(typeof(T), value);
            }
            #endregion

            #region Properties
            [CanBeNull]
            public string Enzyme
            {
                get => GetValue<string>(nameof(Enzyme));
                set => SetValue(nameof(Enzyme), value);
            }
            [CanBeNull] public string Fixed
            {
                get => GetValue<string>(nameof(Fixed));
                set => SetValue(nameof(Fixed), value);
            }
            public FragmentationType? Frag
            {
                get => GetValueEnum<FragmentationType>(nameof(Frag));
                set => SetValueEnum(nameof(Frag), value ?? FragmentationType.CID);
            }
            public double? Ptol
            {
                get => GetValue<double>(nameof(Ptol));
                set => SetValue(nameof(Ptol), value);
            }
            public MassErrorType? PtolUnits
            {
                get => GetValueEnum<MassErrorType>(nameof(PtolUnits));
                set => SetValueEnum(nameof(PtolUnits), value ?? MassErrorType.PPM);
            }
            public double? Ftol
            {
                get => GetValue<double>(nameof(Ftol));
                set => SetValue(nameof(Ftol), value);
            }
            public MassErrorType? FtolUnits
            {
                get => GetValueEnum<MassErrorType>(nameof(FtolUnits));
                set => SetValueEnum(nameof(FtolUnits), value ?? MassErrorType.PPM);
            }
            public double? Lftol
            {
                get => GetValue<double>(nameof(Lftol));
                set => SetValue(nameof(Lftol), value);
            }
            public MassErrorType? LftolUnits
            {
                get => GetValueEnum<MassErrorType>(nameof(LftolUnits));
                set => SetValueEnum(nameof(LftolUnits), value ?? MassErrorType.PPM);
            }
            public double? Poffset
            {
                get => GetValue<double>(nameof(Poffset));
                set => SetValue(nameof(Poffset), value);
            }
            public double? Foffset
            {
                get => GetValue<double>(nameof(Foffset));
                set => SetValue(nameof(Foffset), value);
            }
            public double? PercolatorThreshold
            {
                get => GetValue<double>(nameof(PercolatorThreshold));
                set => SetValue(nameof(PercolatorThreshold), value);
            }
            //[CanBeNull] public string PercolatorVersion { get; set; }
            public int? PercolatorTrainingSetSize
            {
                get => GetValue<int>(nameof(PercolatorTrainingSetSize));
                set => SetValue(nameof(PercolatorTrainingSetSize), value);
            }
            public double? PercolatorTrainingFDR
            {
                get => GetValue<double>(nameof(PercolatorTrainingFDR));
                set => SetValue(nameof(PercolatorTrainingFDR), value);
            }
            public DataAcquisitionType? Acquisition
            {
                get => GetValueEnum<DataAcquisitionType>(nameof(Acquisition));
                set => SetValueEnum(nameof(Acquisition), value ?? DataAcquisitionType.DIA);
            }
            public int? NumberOfThreadsUsed
            {
                get => GetValue<int>(nameof(NumberOfThreadsUsed));
                set => SetValue(nameof(NumberOfThreadsUsed), value);
            }
            public double? ExpectedPeakWidth
            {
                get => GetValue<double>(nameof(ExpectedPeakWidth));
                set => SetValue(nameof(ExpectedPeakWidth), value);
            }
            public double? PrecursorWindowSize
            {
                get => GetValue<double>(nameof(PrecursorWindowSize));
                set => SetValue(nameof(PrecursorWindowSize), value);
            }
            public int? NumberOfQuantitativePeaks
            {
                get => GetValue<int>(nameof(NumberOfQuantitativePeaks));
                set => SetValue(nameof(NumberOfQuantitativePeaks), value);
            }
            public int? MinNumOfQuantitativePeaks
            {
                get => GetValue<int>(nameof(MinNumOfQuantitativePeaks));
                set => SetValue(nameof(MinNumOfQuantitativePeaks), value);
            }
            public int? TopNTargetsUsed
            {
                get => GetValue<int>(nameof(TopNTargetsUsed));
                set => SetValue(nameof(TopNTargetsUsed), value);
            }
            public bool? QuantifyAcrossSamples
            {
                get => GetValue<bool>(nameof(QuantifyAcrossSamples));
                set => SetValue(nameof(QuantifyAcrossSamples), value);
            }
            public double? NumberOfExtraDecoyLibrariesSearched
            {
                get => GetValue<double>(nameof(NumberOfExtraDecoyLibrariesSearched));
                set => SetValue(nameof(NumberOfExtraDecoyLibrariesSearched), value);
            }
            public bool? VerifyModificationIons
            {
                get => GetValue<bool>(nameof(VerifyModificationIons));
                set => SetValue(nameof(VerifyModificationIons), value);
            }
            public double? MinIntensity
            {
                get => GetValue<double>(nameof(MinIntensity));
                set => SetValue(nameof(MinIntensity), value);
            }
            public double? RtWindowInMin
            {
                get => GetValue<double>(nameof(RtWindowInMin));
                set => SetValue(nameof(RtWindowInMin), value);
            }
            public bool? FilterPeaklists
            {
                get => GetValue<bool>(nameof(FilterPeaklists));
                set => SetValue(nameof(FilterPeaklists), value);
            }

            public bool? V2scoring
            {
                get => GetValue<bool>(nameof(V2scoring));
                set => SetValue(nameof(V2scoring), value);
            }

            // scoringBreadthType recal
            // localizationModification none
            // precursorIsolationRangeFile none
            // percolatorModelFile none
            #endregion

            public override string ToString()
            {
                var sb = new StringBuilder();
                foreach (var param in Parameters)
                {
                    if (DefaultParameters[param.Key].Value.Equals(param.Value.Value))
                        continue;
                    sb.AppendFormat(CultureInfo.InvariantCulture, @" -{0}{1} ""{2}""", char.ToLowerInvariant(param.Key[0]), param.Key.Substring(1), param.Value.Value);
                }
                return sb.ToString();
            }
        }

        private const string LOCALIZATION_PARAMS = "-Duser.language=en-US -Duser.region=US";

        // EncyclopeDia and its embedded Percolator do not behave well with paths with spaces and/or Unicode and/or symbols that need escaping
        // TODO(MattC/BrianS): update EncyclopeDia to fix this issue
        private static string JAVA_TMPDIR_PATH => Path.Combine(Environment.GetEnvironmentVariable("SystemRoot")!, @"Temp");

        private static string JAVA_TMPDIR => $@"-Djava.io.tmpdir={JAVA_TMPDIR_PATH}";

        private static void GenerateLibraryWithErrorHandling(string encyclopeDiaLibInputFilepath,
            string encyclopeDiaElibOutputFilepath, string fastaFilepath, MsDataFileUri diaDataFile,
            IProgressMonitor progressMonitor, ref IProgressStatus status, EncyclopeDiaConfig config, bool quantLibrary)
        {
            try
            {
                GenerateLibrary(encyclopeDiaLibInputFilepath, encyclopeDiaElibOutputFilepath, fastaFilepath, diaDataFile, progressMonitor, ref status, config, quantLibrary);
            }
            catch (Exception ex)
            {
                var fatalError = Regex.Match(ex.Message, @"Fatal Error: (.*)");
                if (fatalError.Success && fatalError.Groups[1].Success)
                    status = status.ChangeMessage(fatalError.Groups[1].Value).ChangeErrorException(ex);
                status = status.ChangePercentComplete(100);
                progressMonitor.UpdateProgress(status);
                throw;
            }
        }

        private static void GenerateLibrary(string encyclopeDiaLibInputFilepath,
            string encyclopeDiaElibOutputFilepath, string fastaFilepath, MsDataFileUri diaDataFile,
            IProgressMonitor progressMonitor, ref IProgressStatus status, EncyclopeDiaConfig config, bool quantLibrary)
        {
            if (!EnsureRequiredFilesDownloaded(FilesToDownload, progressMonitor))
                throw new InvalidOperationException(Resources.EncyclopeDiaHelpers_ConvertFastaToKoinaInputCsv_could_not_find_EncyclopeDia);

            long javaMaxHeapMB = Math.Min(12 * 1024L * 1024 * 1024, MemoryInfo.TotalBytes / 2) / 1024 / 1024;
            string extraParams = config.ToString();
            string subdir = quantLibrary ? @"elib_quant" : @"elib_chrom";

            // EncyclopeDia requires all DIA files to be in the same directory;
            // we use the first file's directory to make a subdirectory which will contain links or copies of all the files
            string diaDataPath = Path.Combine(Path.GetDirectoryName(diaDataFile.GetFilePath()) ?? string.Empty, subdir);
            string diaDataFilepath = GetConvertedDiaDataFile(diaDataFile, diaDataPath, progressMonitor, ref status);

            /*status = status.ChangeMessage(String.Format(quantLibrary
                    ? LibResources.EncyclopeDiaHelpers_GenerateLibrary_Generating_quantification_library_0_of_1_2
                    : LibResources.EncyclopeDiaHelpers_GenerateLibrary_Generating_chromatogram_library_0_of_1_2,
                0, 0, Path.GetFileName(diaDataFilepath)));*/
            if (progressMonitor.UpdateProgress(status) == UpdateProgressResponse.cancel)
                return;

            // if this function runs in parallel with the same JAVA_TMPDIR, there may be a race condition when EncyclopeDIA extracts the JAR dependencies (like SQLite)
            string threadDir = @"Thread" + Thread.CurrentThread.ManagedThreadId;
            var pr = new ProcessRunner();
            var psi = new ProcessStartInfo(JavaBinary,
                $" -Xmx{javaMaxHeapMB}M -jar {EncyclopeDiaBinary.Quote()} {LOCALIZATION_PARAMS} {extraParams} -i {diaDataFilepath.Quote()} -f {fastaFilepath.Quote()} -l {encyclopeDiaLibInputFilepath.Quote()}")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            psi.EnvironmentVariables[@"TMP"] = Path.Combine(JAVA_TMPDIR_PATH, threadDir);
            status = status.ChangeMessage(String.Format(Resources.EncyclopeDiaHelpers_GenerateLibrary_Running_command___0___1_,
                psi.FileName, psi.Arguments));
            if (progressMonitor.UpdateProgress(status) == UpdateProgressResponse.cancel)
                return;
            pr.Run(psi, null, progressMonitor, ref status, null, ProcessPriorityClass.BelowNormal, true, IsGoodEncyclopeDiaOutput, false);

            foreach (var file in Directory.EnumerateFiles(diaDataPath, @"*.pdf"))
                FileEx.SafeDelete(file);
        }

        private static void MergeLibrary(string encyclopeDiaLibInputFilepath,
            string encyclopeDiaElibOutputFilepath, string fastaFilepath, MsDataFileUri diaDataFile,
            IProgressMonitor progressMonitor, ref IProgressStatus status, EncyclopeDiaConfig config, bool quantLibrary)
        {
            if (!EnsureRequiredFilesDownloaded(FilesToDownload, progressMonitor))
                throw new InvalidOperationException(Resources.EncyclopeDiaHelpers_ConvertFastaToKoinaInputCsv_could_not_find_EncyclopeDia);

            long javaMaxHeapMB = Math.Min(12 * 1024L * 1024 * 1024, MemoryInfo.TotalBytes / 2) / 1024 / 1024;
            string extraParams = config.ToString();
            string subdir = quantLibrary ? @"elib_quant" : @"elib_chrom";

            // EncyclopeDia requires all DIA files to be in the same directory;
            // we use the first file's directory to make a subdirectory which will contain links or copies of all the files
            string diaDataPath = Path.Combine(Path.GetDirectoryName(diaDataFile.GetFilePath()) ?? string.Empty, subdir);
            string aParam = quantLibrary ? @"-a" : @"";

            var prMerge = new ProcessRunner();
            var psiMerge = new ProcessStartInfo(JavaBinary,
                $" -Xmx{javaMaxHeapMB}M -jar {EncyclopeDiaBinary.Quote()} {LOCALIZATION_PARAMS} {extraParams} -i {diaDataPath.Quote()} -libexport {aParam} -o {encyclopeDiaElibOutputFilepath.Quote()} -f {fastaFilepath.Quote()} -l {encyclopeDiaLibInputFilepath.Quote()}")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            psiMerge.EnvironmentVariables[@"TMP"] = JAVA_TMPDIR_PATH;
            status = status.ChangeMessage(String.Format(Resources.EncyclopeDiaHelpers_GenerateLibrary_Running_command___0___1_,
                psiMerge.FileName, psiMerge.Arguments));
            progressMonitor.UpdateProgress(status);
            prMerge.Run(psiMerge, null, progressMonitor, ref status, null, ProcessPriorityClass.BelowNormal, true, IsGoodEncyclopeDiaOutput, false);

        }

        public static void Generate(string encyclopeDiaDlibInputFilepath, string encyclopeDiaElibOutputFilepath,
            string encyclopeDiaQuantElibOutputFilepath, string fastaFilepath, EncyclopeDiaConfig config,
            IEnumerable<MsDataFileUri> narrowWindowResultUris, IEnumerable<MsDataFileUri> wideWindowResultUris,
            IProgressMonitor encyclopediaProgressMonitor, IProgressMonitor conversionProgressMonitor,
            CancellationToken cancelToken, IProgressStatus status)
        {
            // parallel converter that starts all files converting
            var runner = new ParallelRunner(encyclopeDiaDlibInputFilepath, encyclopeDiaElibOutputFilepath,
                encyclopeDiaQuantElibOutputFilepath, fastaFilepath, config, narrowWindowResultUris,
                wideWindowResultUris, encyclopediaProgressMonitor, conversionProgressMonitor, cancelToken);

            runner.Generate(status);
        }
    }
}
