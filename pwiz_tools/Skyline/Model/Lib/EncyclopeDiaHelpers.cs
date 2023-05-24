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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
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

        public class FastaToPrositInputCsvConfig
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
                            Resources.FastaToProsit_Enzyme_unsupported_enzyme___0____allowed_values_are___1_, value,
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
                sb.Append(" -percolatorVersion v3-01 -enableAdvancedOptions -v2scoring");
                return sb.ToString();
                // ReSharper restore LocalizableElement
            }

            public static int MinNCE => 10;
            public static int MaxNCE => 50;
        }

        private static bool IsGoodEncyclopeDiaOutput(string stdOut, int exitCode)
        {
            return !stdOut.Contains(@"Fatal Error");
        }

        public static void ConvertFastaToPrositInputCsv(string fastaFilepath, string prositCsvFilepath,
            IProgressMonitor progressMonitor, ref IProgressStatus status, FastaToPrositInputCsvConfig config)
        {
            if (!EnsureRequiredFilesDownloaded(FilesToDownload, progressMonitor))
                throw new InvalidOperationException(Resources.EncyclopeDiaHelpers_ConvertFastaToPrositInputCsv_could_not_find_EncyclopeDia);

            using var tmpTmp = new TemporaryEnvironmentVariable(@"TMP", JAVA_TMPDIR_PATH);

            long javaMaxHeapMB = Math.Min(4 * 1024L * 1024 * 1024, MemoryInfo.TotalBytes / 2) / 1024 / 1024;
            const string csvToLibraryClasspath = "edu.washington.gs.maccoss.encyclopedia.cli.ConvertFastaToPrositCSV";

            var pr = new ProcessRunner();
            var psi = new ProcessStartInfo(Java8DownloadInfo.JavaBinary,
                $" -Xmx{javaMaxHeapMB}M -cp {EncyclopeDiaBinary.Quote()} {csvToLibraryClasspath} {LOCALIZATION_PARAMS} {JAVA_TMPDIR} -i {fastaFilepath.Quote()} -o {prositCsvFilepath.Quote()} {config}")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };

            status = status.ChangeMessage(Resources.EncyclopeDiaHelpers_ConvertFastaToPrositInputCsv_Converting_FASTA_to_Prosit_input);
            if (progressMonitor.UpdateProgress(status) == UpdateProgressResponse.cancel)
                return;

            pr.Run(psi, null, progressMonitor, ref status, null, ProcessPriorityClass.BelowNormal, true, IsGoodEncyclopeDiaOutput, false);
        }

        public static void ConvertPrositOutputToDlib(string prositBlibFilepath, string fastaFilepath,
            string encyclopeDiaDlibFilepath, IProgressMonitor progressMonitor, ref IProgressStatus status)
        {
            if (!EnsureRequiredFilesDownloaded(FilesToDownload, progressMonitor))
                throw new InvalidOperationException(Resources.EncyclopeDiaHelpers_ConvertFastaToPrositInputCsv_could_not_find_EncyclopeDia);

            using var tmpTmp = new TemporaryEnvironmentVariable(@"TMP", JAVA_TMPDIR_PATH);

            long javaMaxHeapMB = Math.Min(12 * 1024L * 1024 * 1024, MemoryInfo.TotalBytes / 2) / 1024 / 1024;
            const string csvToLibraryClasspath = "edu.washington.gs.maccoss.encyclopedia.cli.ConvertBLIBToLibrary";

            var pr = new ProcessRunner();
            var psi = new ProcessStartInfo(Java8DownloadInfo.JavaBinary,
                $" -Xmx{javaMaxHeapMB}M -cp {EncyclopeDiaBinary.Quote()} {csvToLibraryClasspath} {LOCALIZATION_PARAMS} {JAVA_TMPDIR} -i {prositBlibFilepath.Quote()} -f {fastaFilepath.Quote()} -o {encyclopeDiaDlibFilepath.Quote()}")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };

            status = status.ChangeMessage(Resources.EncyclopeDiaHelpers_ConvertPrositOutputToDlib_Converting_Prosit_output_to_EncyclopeDia_library);
            if (progressMonitor.UpdateProgress(status) == UpdateProgressResponse.cancel)
                return;

            pr.Run(psi, null, progressMonitor, ref status, null, ProcessPriorityClass.BelowNormal, true, IsGoodEncyclopeDiaOutput, false);
        }

        public static void GenerateChromatogramLibrary(string encyclopeDiaDlibInputFilepath,
            string encyclopeDiaElibOutputFilepath, string fastaFilepath, IEnumerable<MsDataFileUri> diaDataFiles,
            IProgressMonitor progressMonitor, ref IProgressStatus status, EncyclopeDiaConfig config)
        {
            GenerateLibrary(encyclopeDiaDlibInputFilepath, encyclopeDiaElibOutputFilepath, fastaFilepath, diaDataFiles, progressMonitor, ref status, config, false);
        }

        public static void GenerateQuantLibrary(string encyclopeDiaElibInputFilepath,
            string encyclopeDiaQuantLibOutputFilepath, string fastaFilepath, IEnumerable<MsDataFileUri> diaDataFiles,
            IProgressMonitor progressMonitor, ref IProgressStatus status, EncyclopeDiaConfig config)
        {
            GenerateLibrary(encyclopeDiaElibInputFilepath, encyclopeDiaQuantLibOutputFilepath, fastaFilepath, diaDataFiles, progressMonitor, ref status, config, true);
        }

        private const string DEMUX_SUBDIRECTORY = "demux";
        private static string GetConvertedDiaDataFile(MsDataFileUri diaDataFile, string outputPath, IProgressMonitor progressMonitor, ref IProgressStatus status)
        {
            string outputFilepath = Path.Combine(outputPath, diaDataFile.GetFileNameWithoutExtension() + DataSourceUtil.EXT_MZML);

            /*var reader = new IsolationSchemeReader(new [] { diaDataFile });
            string isolationSchemeName = Path.GetFileNameWithoutExtension(diaDataFile.GetFileNameWithoutExtension());
            var isolationScheme = reader.Import(isolationSchemeName, progressMonitor);
            bool needsDemultiplexing = isolationScheme.SpecialHandling == IsolationScheme.SpecialHandlingType.OVERLAP ||
                                       isolationScheme.SpecialHandling == IsolationScheme.SpecialHandlingType.OVERLAP_MULTIPLEXED;*/
            
            if (diaDataFile.GetExtension().ToLowerInvariant() == DataSourceUtil.EXT_MZML)
            {
                outputFilepath = Path.Combine(outputPath, diaDataFile.GetFileName());
                if (!File.Exists(outputFilepath))
                    FileEx.HardLinkOrCopyFile(diaDataFile.GetFilePath(), outputFilepath);
                return outputFilepath;
            }

            const string MSCONVERT_EXE = "msconvert";
            
            status = status.ChangeMessage(Resources.EncyclopeDiaHelpers_GetConvertedDiaDataFile_Converting_DIA_data_to_mzML);
            progressMonitor.UpdateProgress(status);

            var pr = new ProcessRunner();
            var psi = new ProcessStartInfo(MSCONVERT_EXE)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                Arguments =
                    "-v -z --mzML " +
                    $"-o {outputPath.Quote()} " +
                    $"--outfile {Path.GetFileName(outputFilepath).Quote()} " +
                    " --acceptZeroLengthSpectra --simAsSpectra --combineIonMobilitySpectra" +
                    " --filter \"peakPicking true 1-\" " +
                    //(needsDemultiplexing ? @" --filter ""demultiplex""" : "") +
                    " --runIndex " + Math.Max(0, diaDataFile.GetSampleIndex()) + " " +
                    diaDataFile.GetFilePath().Quote()
            };

            try
            {
                pr.Run(psi, null, progressMonitor, ref status, null, ProcessPriorityClass.BelowNormal, false, IsGoodEncyclopeDiaOutput, false);
            }
            catch (IOException e)
            {
                progressMonitor.UpdateProgress(status.ChangeMessage(e.Message));
            }

            if (progressMonitor.IsCanceled)
            {
                FileEx.SafeDelete(outputFilepath, true);
                return null;
            }

            return outputFilepath;
        }

        public class EncyclopeDiaConfig
        {
            public EncyclopeDiaConfig()
            {
                Parameters = new Dictionary<string, AbstractDdaSearchEngine.Setting>();
                foreach(var kvp in DefaultParameters)
                    Parameters[kvp.Key] = new AbstractDdaSearchEngine.Setting(kvp.Value);
            }

            public IDictionary<string, AbstractDdaSearchEngine.Setting> Parameters { get; }

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
                    { "FilterPeaklists", new AbstractDdaSearchEngine.Setting("FilterPeaklists", false) }
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

            // scoringBreadthType recal
            // localizationModification none
            // precursorIsolationRangeFile none
            // percolatorModelFile none

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

        private static void GenerateLibrary(string encyclopeDiaLibInputFilepath,
            string encyclopeDiaElibOutputFilepath, string fastaFilepath, IEnumerable<MsDataFileUri> diaDataFiles,
            IProgressMonitor progressMonitor, ref IProgressStatus status, EncyclopeDiaConfig config, bool quantLibrary)
        {
            if (!EnsureRequiredFilesDownloaded(FilesToDownload, progressMonitor))
                throw new InvalidOperationException(Resources.EncyclopeDiaHelpers_ConvertFastaToPrositInputCsv_could_not_find_EncyclopeDia);

            using var tmpTmp = new TemporaryEnvironmentVariable(@"TMP", JAVA_TMPDIR_PATH);

            long javaMaxHeapMB = Math.Min(12 * 1024L * 1024 * 1024, MemoryInfo.TotalBytes / 2) / 1024 / 1024;
            string diaDataPath = null;// = Path.GetDirectoryName(encyclopeDiaElibOutputFilepath);
            string extraParams = config.ToString();
            string subdir = quantLibrary ? @"elib_quant" : @"elib_chrom";

            var diaFiles = diaDataFiles.ToList();
            int stepCount = diaFiles.Count + 1;
            int step = 0;
            foreach (var diaDataFile in diaFiles)
            {
                status = status.ChangePercentComplete(100 * step / stepCount);

                // EncyclopeDia requires all DIA files to be in the same directory;
                // we use the first file's directory to make a subdirectory which will contain links or copies of all the files
                diaDataPath ??= Path.Combine(Path.GetDirectoryName(diaDataFile.GetFilePath()) ?? string.Empty, subdir);
                string diaDataFilepath = GetConvertedDiaDataFile(diaDataFile, diaDataPath, progressMonitor, ref status);

                status = status.ChangeMessage(String.Format(quantLibrary
                        ? Resources.EncyclopeDiaHelpers_GenerateLibrary_Generating_quantification_library_0_of_1_2
                        : Resources.EncyclopeDiaHelpers_GenerateLibrary_Generating_chromatogram_library_0_of_1_2,
                    step + 1, stepCount, Path.GetFileName(diaDataFilepath)));
                status = status.ChangeWarningMessage(status.Message);
                if (progressMonitor.UpdateProgress(status) == UpdateProgressResponse.cancel)
                    return;
                ++step;
                var pr = new ProcessRunner();
                var psi = new ProcessStartInfo(Java8DownloadInfo.JavaBinary,
                    $" -Xmx{javaMaxHeapMB}M -jar {EncyclopeDiaBinary.Quote()} {LOCALIZATION_PARAMS} {JAVA_TMPDIR} {extraParams} -i {diaDataFilepath.Quote()} -f {fastaFilepath.Quote()} -l {encyclopeDiaLibInputFilepath.Quote()}")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                pr.Run(psi, null, progressMonitor, ref status, null, ProcessPriorityClass.BelowNormal, true, IsGoodEncyclopeDiaOutput, false);
            }

            // move the .mzML.*.txt files to .dia.*.txt because it won't merge with them named as mzML?
            foreach (var txtFile in Directory.EnumerateFiles(diaDataPath!))
                if (txtFile.EndsWith(@".mzML"))
                    File.Delete(txtFile);
                else if (txtFile.Contains(@".mzML."))
                {
                    if (!File.Exists(txtFile.Replace(@".mzML.", @".dia.")))
                        File.Move(txtFile, txtFile.Replace(@".mzML.", @".dia."));
                }

            status = status.ChangePercentComplete(100 * step / stepCount);
            status = status.ChangeMessage(String.Format(quantLibrary
                    ? Resources.EncyclopeDiaHelpers_GenerateLibrary_Generating_quantification_library_0_of_1_2
                    : Resources.EncyclopeDiaHelpers_GenerateLibrary_Generating_chromatogram_library_0_of_1_2,
                step + 1, stepCount, Path.GetFileName(encyclopeDiaElibOutputFilepath)));
            status = status.ChangeWarningMessage(status.Message);
            if (progressMonitor.UpdateProgress(status) == UpdateProgressResponse.cancel)
                return;
            ++step;

            string aParam = quantLibrary ? @"-a" : @"";

            var prMerge = new ProcessRunner();
            var psiMerge = new ProcessStartInfo(Java8DownloadInfo.JavaBinary,
                $" -Xmx{javaMaxHeapMB}M -jar {EncyclopeDiaBinary.Quote()} {LOCALIZATION_PARAMS} {JAVA_TMPDIR} {extraParams} -i {diaDataPath.Quote()} -libexport {aParam} -o {encyclopeDiaElibOutputFilepath.Quote()} -f {fastaFilepath.Quote()} -l {encyclopeDiaLibInputFilepath.Quote()}")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };
            prMerge.Run(psiMerge, null, progressMonitor, ref status, null, ProcessPriorityClass.BelowNormal, true, IsGoodEncyclopeDiaOutput, false);

            status = status.ChangePercentComplete(100);
            progressMonitor.UpdateProgress(status);
        }
    }
}
