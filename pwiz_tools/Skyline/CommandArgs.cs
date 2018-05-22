/*
 * Original author: John Chilton <jchilton .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011-2015 University of Washington - Seattle, WA
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
using System.Text.RegularExpressions;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Model.Results.RemoteApi.Chorus;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline
{
    public class CommandArgs
    {
        private const string ARG_PREFIX = "--";  // Not L10N

        public string LogFile { get; private set; }
        public string SkylineFile { get; private set; }
        public List<MsDataFileUri> ReplicateFile { get; private set; }
        public string ReplicateName { get; private set; }
        public int ImportThreads { get; private set; }
        public bool ImportAppend { get; private set; }
        public bool ImportDisableJoining { get; private set; }
        public string ImportSourceDirectory { get; private set; }
        public Regex ImportNamingPattern { get; private set; }
        public DateTime? RemoveBeforeDate { get; private set; }
        public DateTime? ImportBeforeDate { get; private set; }
        public DateTime? ImportOnOrAfterDate { get; private set; }
        public string FastaPath { get; private set; }
        public bool KeepEmptyProteins { get; private set; }
        public string LibraryName { get; private set; }
        public string LibraryPath { get; private set; }
        public bool HideAllChromatogramsGraph { get; private set; }
        public bool NoAllChromatogramsGraph { get; private set; }

        // Transition list and assay library import
        public const string ARG_IMPORT_TRANSITION_LIST = "import-transition-list"; // Not L10N
        public const string ARG_IMPORT_ASSAY_LIBRARY = "import-assay-library"; // Not L10N
        public const string ARG_IGNORE_TRANSITION_ERRORS = "ignore-transition-errors"; // Not L10N
        public const string ARG_IRT_STANDARDS_GROUP_NAME = "irt-standards-group-name"; // Not L10N
        public const string ARG_IRT_STANDARDS_FILE = "irt-standards-file"; // Not L10N
        public const string ARG_IRT_DATABASE_PATH = "irt-database-path"; // Not L10N
        public const string ARG_IRT_CALC_NAME = "irt-calc-name"; // Not L10N

        public string TransitionListPath { get; private set; }
        public bool IsTransitionListAssayLibrary { get; private set; }
        public bool IsIgnoreTransitionErrors { get; private set; }
        public string IrtGroupName { get; private set; }
        public string IrtStandardsPath { get; private set; }
        public string IrtDatabasePath { get; private set; }
        public string IrtCalcName { get; private set; }

        // Waters lockmass correction
        private const string ARG_IMPORT_LOCKMASS_POSITIVE = "import-lockmass-positive"; // Not L10N
        private const string ARG_IMPORT_LOCKMASS_NEGATIVE = "import-lockmass-negative"; // Not L10N
        private const string ARG_IMPORT_LOCKMASS_TOLERANCE = "import-lockmass-tolerance"; // Not L10N
        public double? LockmassPositive { get; private set; }
        public double? LockmassNegative { get; private set; }
        public double? LockmassTolerance { get; private set; }
        public LockMassParameters LockMassParameters { get { return new LockMassParameters(LockmassPositive, LockmassNegative, LockmassTolerance); } }

        // Decoys
        public const string ARG_DECOYS_ADD = "decoys-add"; // Not L10N
        public const string ARG_DECOYS_ADD_COUNT = "decoys-add-count"; // Not L10N
        public const string ARG_DECOYS_DISCARD = "decoys-discard"; // Not L10N
        public const string ARG_DECOYS_ADD_VALUE_SHUFFLE = "shuffle"; // Not L10N
        public const string ARG_DECOYS_ADD_VALUE_REVERSE = "reverse"; // Not L10N

        // Annotations
        public const string ARG_IMPORT_ANNOTATIONS = "import-annotations"; // Not L10N
        public string ImportAnnotations { get; private set; }
        
        public string AddDecoysType { get; private set; }
        public int? AddDecoysCount { get; private set; }
        public bool DiscardDecoys { get; private set; }

        public bool AddDecoys
        {
            get { return !string.IsNullOrEmpty(AddDecoysType); }
        }
        public bool ImportingResults
        {
            get { return ImportingReplicateFile || ImportingSourceDirectory; }
        }
        public bool ImportingReplicateFile
        {
            get { return ReplicateFile.Count > 0; }
        }
        public bool ImportingSourceDirectory
        {
            get { return !string.IsNullOrEmpty(ImportSourceDirectory); }
        }

        public bool ImportWarnOnFailure { get; private set; }

        public bool RemovingResults { get; private set; }

        public bool ImportingFasta
        {
            get { return !string.IsNullOrWhiteSpace(FastaPath); }
        }

        public bool ImportingTransitionList
        {
            get { return !string.IsNullOrWhiteSpace(TransitionListPath); }
        }

        public bool SettingLibraryPath
        {
            get { return !string.IsNullOrWhiteSpace(LibraryName) || !string.IsNullOrWhiteSpace(LibraryPath); }
        }

        public string SaveFile { get; private set; }
        private bool _saving;
        public bool Saving
        {
            get { return !String.IsNullOrEmpty(SaveFile) || _saving; }
            set { _saving = value; }
        }

        // For reintegration
        private const string ARG_REINTEGRATE_MODEL_NAME = "reintegrate-model-name"; // Not L10N
        private const string ARG_REINTEGRATE_CREATE_MODEL = "reintegrate-create-model"; // Not L10N
        private const string ARG_REINTEGRATE_MODEL_ITERATION_COUNT = "reintegrate-model-iteration-count"; // Not L10N
        private const string ARG_REINTEGRATE_MODEL_SECOND_BEST = "reintegrate-model-second-best"; // Not L10N
        private const string ARG_REINTEGRATE_MODEL_BOTH = "reintegrate-model-both"; // Not L10N
        private const string ARG_REINTEGRATE_OVERWRITE_PEAKS = "reintegrate-overwrite-peaks"; // Not L10N
        private const string ARG_REINTEGRATE_LOG_TRAINING = "reintegrate-log-training"; // Not L10N
        private const string ARG_REINTEGRATE_EXCLUDE_FEATURE = "reintegrate-exclude-feature"; // Not L10N

        public string ReintegratModelName { get; private set; }
        public int? ReintegrateModelIterationCount { get; private set; }
        public bool IsOverwritePeaks { get; private set; }
        public bool IsCreateScoringModel { get; private set; }
        public bool IsSecondBestModel { get; private set; }
        public bool IsDecoyModel { get; private set; }
        public bool IsLogTraining { get; private set; }
        public List<IPeakFeatureCalculator> ExcludeFeatures { get; private set; }

        public bool Reintegrating { get { return !string.IsNullOrEmpty(ReintegratModelName); } }

        // For exporting reports
        public string ReportName { get; private set; }
        public char ReportColumnSeparator { get; private set; }
        public string ReportFile { get; private set; }
        public bool IsReportInvariant { get; private set; }
        public bool ExportingReport
        {
            get { return !string.IsNullOrEmpty(ReportName); }
        }

        // For exporting chromatograms
        public string ChromatogramsFile { get; private set; }
        public bool ChromatogramsPrecursors { get; private set; }
        public bool ChromatogramsProducts { get; private set; }
        public bool ChromatogramsBasePeaks { get; private set; }
        public bool ChromatogramsTics { get; private set; }
        public bool ExportingChromatograms { get { return !string.IsNullOrEmpty(ChromatogramsFile); } }

        // For publishing the document to Panorama
        private const string PANORAMA_SERVER_URI = "panorama-server"; // Not L10N
        private const string PANORAMA_USERNAME = "panorama-username"; // Not L10N
        private const string PANORAMA_PASSWD = "panorama-password"; // Not L10N
        private const string PANORAMA_FOLDER = "panorama-folder"; // Not L10N
        private string PanoramaServerUri { get; set; }
        private string PanoramaUserName { get; set; }
        private string PanoramaPassword { get; set; }
        public string PanoramaFolder { get; private set; }
        public bool PublishingToPanorama { get; private set; }
        public Server PanoramaServer { get; private set; }

        // For sharing zip file
        public bool SharingZipFile { get; private set; }
        public string SharedFile { get; private set; }

        // For importing a tool.
        public string ToolName { get; private set; }
        public string ToolCommand { get; private set; }
        public string ToolArguments { get; private set; }
        public string ToolInitialDirectory { get; private set; }
        public string ToolReportTitle { get; private set; }
        public bool ToolOutputToImmediateWindow { get; private set; }
        private bool _importingTool;
        public bool ImportingTool
        {
            get { return !string.IsNullOrEmpty(ToolName) || _importingTool; }
            set { _importingTool = value; }
        }
        public bool? ResolveToolConflictsBySkipping { get; private set; }

        // For importing a peptide search
        public const string ARG_IMPORT_PEPTIDE_SEARCH_FILE = "import-search-file"; // Not L10N
        public const string ARG_IMPORT_PEPTIDE_SEARCH_CUTOFF = "import-search-cutoff-score"; // Not L10N
        public const string ARG_IMPORT_PEPTIDE_SEARCH_MODS = "import-search-add-mods"; // Not L10N
        public const string ARG_IMPORT_PEPTIDE_SEARCH_AMBIGUOUS = "import-search-include-ambiguous"; // Not L10N

        public List<string> SearchResultsFiles { get; private set; }
        public double? CutoffScore { get; private set; }
        public bool AcceptAllModifications { get; private set; }
        public bool IncludeAmbiguousMatches { get; private set; }
        public bool ImportingSearch
        {
            get { return SearchResultsFiles.Count > 0; }
        }

        // For adjusting full-scan settings
        private const string ARG_FULL_SCAN_PRECURSOR_RES = "full-scan-precursor-res"; // Not L10N
        private const string ARG_FULL_SCAN_PRECURSOR_RES_MZ = "full-scan-precursor-res-mz"; // Not L10N
        private const string ARG_FULL_SCAN_PRODUCT_RES = "full-scan-product-res"; // Not L10N
        private const string ARG_FULL_SCAN_PRODUCT_RES_MZ = "full-scan-product-res-mz"; // Not L10N
        private const string ARG_FULL_SCAN_RT_FILTER_TOLERANCE = "full-scan-rt-filter-tolerance"; // Not L10N

        public double? FullScanPrecursorRes { get; private set; }
        public double? FullScanPrecursorResMz { get; private set; }
        public double? FullScanProductRes { get; private set; }
        public double? FullScanProductResMz { get; private set; }
        public double? FullScanRetentionTimeFilterLength { get; private set; }

        public bool FullScanSetting
        {
            get
            {
                return (FullScanPrecursorRes
                        ?? FullScanPrecursorResMz
                        ?? FullScanProductRes
                        ?? FullScanProductResMz
                        ?? FullScanRetentionTimeFilterLength).HasValue;
            }
        }

        // For importing a tool from a zip file.
        public bool InstallingToolsFromZip { get; private set; }
        public string ZippedToolsPath { get; private set; }
        public CommandLine.ResolveZipToolConflicts? ResolveZipToolConflictsBySkipping { get; private set; }
        public bool? ResolveZipToolAnotationConflictsBySkipping { get; private set; }
        public ProgramPathContainer ZippedToolsProgramPathContainer { get; private set; }
        public string ZippedToolsProgramPathValue { get; private set; }
        public bool ZippedToolsPackagesHandled { get; set; }
        
        // For keeping track of when an in command is required.
        public bool RequiresSkylineDocument { get; private set; }

        // For --batch-commands parameter
        public string BatchCommandsPath { get; private set; }
        private bool _runningBatchCommands;
        public bool RunningBatchCommands
        {
            get { return !string.IsNullOrEmpty(BatchCommandsPath) || _runningBatchCommands; }
            set { _runningBatchCommands = value; }
        }

        // For adding a skyr file to user.config
        public string SkyrPath { get; private set; }
        private bool _importingSkyr;
        public bool ImportingSkyr
        {
            get { return !string.IsNullOrEmpty(SkyrPath) || _importingSkyr; }
            set { _importingSkyr = value; }
        }
        public bool? ResolveSkyrConflictsBySkipping { get; private set; }

        private string _isolationListInstrumentType;
        public string IsolationListInstrumentType
        {
            get { return _isolationListInstrumentType; }
            set
            {
                if (ExportInstrumentType.ISOLATION_LIST_TYPES.Any(inst => inst.Equals(value)))
                {
                    _isolationListInstrumentType = value;
                }
                else
                {
                    throw new ArgumentException(string.Format(Resources.CommandArgs_IsolationListInstrumentType_The_instrument_type__0__is_not_valid_for_isolation_list_export, value));
                }
            }
        }

        public bool ExportingIsolationList
        {
            get { return !string.IsNullOrEmpty(IsolationListInstrumentType); }
        }

        private string _transListInstrumentType;
        public string TransListInstrumentType
        {
            get { return _transListInstrumentType; }
            set
            {
                if (ExportInstrumentType.TRANSITION_LIST_TYPES.Any(inst => inst.Equals(value)))
                {
                    _transListInstrumentType = value;
                }
                else
                {
                    throw new ArgumentException(string.Format(Resources.CommandArgs_TransListInstrumentType_The_instrument_type__0__is_not_valid_for_transition_list_export, value));
                }
            }
        }

        public bool ExportingTransitionList
        {
            get { return !string.IsNullOrEmpty(TransListInstrumentType);  }
        }

        private string _methodInstrumentType;
        public string MethodInstrumentType
        {
            get { return _methodInstrumentType; }
            set
            {
                if (ExportInstrumentType.METHOD_TYPES.Any(inst => inst.Equals(value)))
                {
                    _methodInstrumentType = value;
                }
                else
                {
                    throw new ArgumentException(string.Format(Resources.CommandArgs_MethodInstrumentType_The_instrument_type__0__is_not_valid_for_method_export, value));
                }
            }
        }

        public bool ExportingMethod
        {
            get { return !string.IsNullOrEmpty(MethodInstrumentType); }
        }

        public bool ExportStrategySet { get; private set; }
        public ExportStrategy ExportStrategy { get; set; }

        // The min value for this field comes from either MassListExporter.MAX_TRANS_PER_INJ_MIN
        // or MethodExporter.MAX_TRANS_PER_INJ_MIN_TLTQ depending on the instrument. The max value
        // comes from the document. Point being, there is no way to check the value in the accessor.
        public int MaxTransitionsPerInjection { get; set; }

        private string _importOptimizeType;
        public string ImportOptimizeType
        {
            get { return _importOptimizeType; }
            set { _importOptimizeType = ToOptimizeString(value); }
        }

        private string _exportOptimizeType;
        public string ExportOptimizeType
        {
            get { return _exportOptimizeType; }
            set { _exportOptimizeType = ToOptimizeString(value); }
        }

        public const string OPT_NONE = "NONE"; // Not L10N
        public const string OPT_CE = "CE"; // Not L10N
        public const string OPT_DP = "DP"; // Not L10N

        private static string ToOptimizeString(string value)
        {
            if (value == null)
                return null;

            switch (value.ToUpperInvariant())
            {
                case OPT_NONE:
                    return null;
                case OPT_CE:
                    return ExportOptimize.CE;
                case OPT_DP:
                    return ExportOptimize.DP;
                default:
                    throw new ArgumentException(string.Format(Resources.CommandArgs_ToOptimizeString_The_instrument_parameter__0__is_not_valid_for_optimization_, value));
            }
        }

        public ExportMethodType ExportMethodType { get; private set; }

        public string TemplateFile { get; private set; }
        
        public ExportSchedulingAlgorithm ExportSchedulingAlgorithm
        {
            get
            {
                return String.IsNullOrEmpty(SchedulingReplicate)
                    ? ExportSchedulingAlgorithm.Average
                    : ExportSchedulingAlgorithm.Single;
            }
        }
        
        public string SchedulingReplicate { get; private set; }

        public bool IgnoreProteins { get; private set; }

        private int _primaryTransitionCount;
        public int PrimaryTransitionCount
        {
            get { return _primaryTransitionCount; }
            set
            {
                if (value < AbstractMassListExporter.PRIMARY_COUNT_MIN || value > AbstractMassListExporter.PRIMARY_COUNT_MAX)
                {
                    throw new ArgumentException(string.Format(Resources.CommandArgs_PrimaryTransitionCount_The_primary_transition_count__0__must_be_between__1__and__2__, value, AbstractMassListExporter.PRIMARY_COUNT_MIN, AbstractMassListExporter.PRIMARY_COUNT_MAX));
                }
                _primaryTransitionCount = value;
            }
        }

        private int _dwellTime;
        public int DwellTime
        {
            get { return _dwellTime; }
            set
            {
                if (value < AbstractMassListExporter.DWELL_TIME_MIN || value > AbstractMassListExporter.DWELL_TIME_MAX)
                {
                    throw new ArgumentException(string.Format(Resources.CommandArgs_DwellTime_The_dwell_time__0__must_be_between__1__and__2__, value, AbstractMassListExporter.DWELL_TIME_MIN, AbstractMassListExporter.DWELL_TIME_MAX));
                }
                _dwellTime = value;
            }
        }

        public bool AddEnergyRamp { get; private set; }
        public bool UseSlens { get; private set; }
        private int _runLength;
        public int RunLength
        {
            get { return _runLength; }
            set
            {
                // Not inclusive of minimum, because it was made zero
                if (value <= AbstractMassListExporter.RUN_LENGTH_MIN || value > AbstractMassListExporter.RUN_LENGTH_MAX)
                {
                    throw new ArgumentException(string.Format(Resources.CommandArgs_RunLength_The_run_length__0__must_be_between__1__and__2__, value, AbstractMassListExporter.RUN_LENGTH_MIN, AbstractMassListExporter.RUN_LENGTH_MAX));
                }
                _runLength = value;
            }
        }

        public string ExportPath { get; private set; }

        private const string ARG_EXP_POLARITY = "exp-polarity"; // Not L10N

        public ExportPolarity ExportPolarityFilter { get; private set; }

        public ExportCommandProperties ExportCommandProperties
        {
            get
            {
                return new ExportCommandProperties(_out)
                {

                    AddEnergyRamp = AddEnergyRamp,
                    UseSlens = UseSlens,
                    DwellTime = DwellTime,
                    ExportStrategy = ExportStrategy,
                    IgnoreProteins = IgnoreProteins,
                    MaxTransitions = MaxTransitionsPerInjection,
                    MethodType = ExportMethodType,
                    OptimizeType = ExportOptimizeType,
                    PolarityFilter = ExportPolarityFilter,
                    RunLength = RunLength,
                    SchedulingAlgorithm = ExportSchedulingAlgorithm
                };
            }
        }

        private readonly CommandStatusWriter _out;
        private readonly bool _isDocumentLoaded;

        public CommandArgs(CommandStatusWriter output, bool isDocumentLoaded)
        {
            ResolveToolConflictsBySkipping = null;
            ResolveSkyrConflictsBySkipping = null;
            _out = output;
            _isDocumentLoaded = isDocumentLoaded;

            ReportColumnSeparator = TextUtil.CsvSeparator;
            MaxTransitionsPerInjection = AbstractMassListExporter.MAX_TRANS_PER_INJ_DEFAULT;
            ImportOptimizeType = OPT_NONE;
            ExportOptimizeType = OPT_NONE;
            ExportStrategy = ExportStrategy.Single;
            ExportMethodType = ExportMethodType.Standard;
            PrimaryTransitionCount = AbstractMassListExporter.PRIMARY_COUNT_DEFAULT;
            DwellTime = AbstractMassListExporter.DWELL_TIME_DEFAULT;
            RunLength = AbstractMassListExporter.RUN_LENGTH_DEFAULT;

            ReplicateFile = new List<MsDataFileUri>();
            SearchResultsFiles = new List<string>();
            ExcludeFeatures = new List<IPeakFeatureCalculator>();

            ImportBeforeDate = null;
            ImportOnOrAfterDate = null;
        }

        public struct NameValuePair
        {
            public NameValuePair(string arg)
                : this()
            {
                if (arg.StartsWith(ARG_PREFIX)) // Not L10N
                {
                    arg = arg.Substring(2);
                    int indexEqualsSign = arg.IndexOf('=');
                    if (indexEqualsSign >= 0)
                    {
                        Name = arg.Substring(0, indexEqualsSign);
                        Value = arg.Substring(indexEqualsSign + 1);
                    }
                    else
                    {
                        Name = arg;
                    }
                }
            }

            public string Name { get; private set; }
            public string Value { get; private set; }
            public int ValueInt { get { return int.Parse(Value); } }
            public double ValueDouble { get { return double.Parse(Value); } }
        }

        public bool ParseArgs(string[] args)
        {
            try
            {
                return ParseArgsInternal(args);
            }
            catch (UsageException x)
            {
                _out.WriteLine(Resources.CommandLine_GeneralException_Error___0_, x.Message);
                return false;
            }
            catch (Exception x)
            {
                // Unexpected behavior, but better to output the error then appear to crash, and
                // have Windows write it to the application event log.
                _out.WriteLine(Resources.CommandLine_GeneralException_Error___0_, x.Message);
                _out.WriteLine(x.StackTrace);
                return false;
            }
        }

        private bool ParseArgsInternal(IEnumerable<string> args)
        {
            ImportThreads = 1;

            foreach (string s in args)
            {
                var pair = new NameValuePair(s);
                if (string.IsNullOrEmpty(pair.Name))
                    continue;

                if (IsNameOnly(pair, "ui")) // Not L10N
                {
                    // Handled by Program
                }
                else if (IsNameOnly(pair, "hideacg")) // Not L10N
                {
                    HideAllChromatogramsGraph = true;
                }
                else if (IsNameOnly(pair, "noacg")) // Not L10N
                {
                    NoAllChromatogramsGraph = true;
                }
                else if (IsNameValue(pair, "log-file")) // Not L10N
                {
                    LogFile = pair.Value;
                }
                else if (IsNameValue(pair, "in")) // Not L10N
                {
                    SkylineFile = GetFullPath(pair.Value);
                    // Set requiresInCommand to be true so if SkylineFile is null or empty it still complains.
                    RequiresSkylineDocument = true;
                }
                else if (IsNameValue(pair, "dir")) // Not L10N
                {
                    if (!Directory.Exists(pair.Value))
                    {
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Error__The_specified_working_directory__0__does_not_exist_, pair.Value);
                        return false;
                    }
                    Directory.SetCurrentDirectory(pair.Value);
                }
                else if (IsNameValue(pair, "import-threads")) // Not L10N
                {
                    ImportThreads = int.Parse(pair.Value);
                }
                else if (IsNameValue(pair, "import-process-count")) // Not L10N
                {
                    ImportThreads = int.Parse(pair.Value);
                    if (ImportThreads > 0)
                        Program.MultiProcImport = true;
                }
                else if (IsNameOnly(pair, "timestamp")) // Not L10N
                {
                    _out.IsTimeStamped = true;
                }
                else if (IsNameOnly(pair, "memstamp")) // Not L10N
                {
                    _out.IsMemStamped = true;
                }

                // A command that exports all the tools to a text file in a SkylineRunner form for --batch-commands
                // Not advertised.
                // ReSharper disable NonLocalizedString
                else if (IsNameValue(pair, "tool-list-export"))
                {
                    string pathToOutputFile = pair.Value;
                    using (StreamWriter sw = new StreamWriter(pathToOutputFile))
                    {
                        foreach (var tool in Settings.Default.ToolList)
                        {
                            string command = "--tool-add=" + "\"" + tool.Title + "\"" +
                                             " --tool-command=" + "\"" + tool.Command + "\"" +
                                             " --tool-arguments=" + "\"" + tool.Arguments + "\"" +
                                             " --tool-initial-dir=" + "\"" + tool.InitialDirectory + "\"" +
                                             " --tool-conflict-resolution=skip" +
                                             " --tool-report=" + "\"" + tool.ReportTitle + "\"";

                            if (tool.OutputToImmediateWindow)
                                command += " --tool-output-to-immediate-window";

                            sw.WriteLine(command);
                        }
                    }
                }
                // ReSharper restore NonLocalizedString

                // Import a skyr file.
                else if (IsNameValue(pair, "report-add")) // Not L10N
                {
                    ImportingSkyr = true;
                    SkyrPath = pair.Value;
                }

                else if (IsNameValue(pair, "report-conflict-resolution")) // Not L10N
                {
                    string input = pair.Value.ToLowerInvariant();
                    if (input == "overwrite") // Not L10N
                    {
                        ResolveSkyrConflictsBySkipping = false;
                    }
                    if (input == "skip") // Not L10N
                    {
                        ResolveSkyrConflictsBySkipping = true;
                    }
                }

                else if (IsNameValue(pair, ARG_FULL_SCAN_PRECURSOR_RES))
                {
                    RequiresSkylineDocument = true;
                    FullScanPrecursorRes = ParseDouble(pair.Value, ARG_FULL_SCAN_PRECURSOR_RES);
                    if (!FullScanPrecursorRes.HasValue)
                        return false;
                }
                else if (IsNameValue(pair, ARG_FULL_SCAN_PRECURSOR_RES_MZ))
                {
                    RequiresSkylineDocument = true;
                    FullScanPrecursorResMz = ParseDouble(pair.Value, ARG_FULL_SCAN_PRECURSOR_RES_MZ);
                    if (!FullScanPrecursorResMz.HasValue)
                        return false;
                }
                else if (IsNameValue(pair, ARG_FULL_SCAN_PRODUCT_RES))
                {
                    RequiresSkylineDocument = true;
                    FullScanProductRes = ParseDouble(pair.Value, ARG_FULL_SCAN_PRODUCT_RES);
                    if (!FullScanProductRes.HasValue)
                        return false;
                }
                else if (IsNameValue(pair, ARG_FULL_SCAN_PRODUCT_RES_MZ))
                {
                    RequiresSkylineDocument = true;
                    FullScanProductResMz = ParseDouble(pair.Value, ARG_FULL_SCAN_PRODUCT_RES_MZ);
                    if (!FullScanProductResMz.HasValue)
                        return false;
                }
                else if (IsNameValue(pair, ARG_FULL_SCAN_RT_FILTER_TOLERANCE))
                {
                    RequiresSkylineDocument = true;
                    FullScanRetentionTimeFilterLength = ParseDouble(pair.Value, ARG_FULL_SCAN_RT_FILTER_TOLERANCE);
                    if (!FullScanRetentionTimeFilterLength.HasValue)
                        return false;
                }

                else if (IsNameValue(pair, "tool-add-zip")) // Not L10N
                {
                    InstallingToolsFromZip = true;
                    ZippedToolsPath = pair.Value;
                }
                else if (IsNameValue(pair, "tool-zip-conflict-resolution")) // Not L10N
                {
                    string input = pair.Value.ToLowerInvariant();
                    if (input == "overwrite") // Not L10N
                    {
                        ResolveZipToolConflictsBySkipping = CommandLine.ResolveZipToolConflicts.overwrite;
                    }
                    if (input == "parallel") // Not L10N
                    {
                        ResolveZipToolConflictsBySkipping = CommandLine.ResolveZipToolConflicts.in_parallel;
                    }
                }
                else if (IsNameValue(pair, "tool-zip-overwrite-annotations")) // Not L10N
                {
                    string input = pair.Value.ToLowerInvariant();
                    if (input == "true") // Not L10N
                    {
                        ResolveZipToolAnotationConflictsBySkipping = true;
                    }
                    if (input == "false") // Not L10N
                    {
                        ResolveZipToolAnotationConflictsBySkipping = false;
                    }
                }
                else if (IsNameValue(pair, "tool-program-macro")) // example --tool-program-macro=R,2.15.2  // Not L10N
                {
                    string [] spliced = pair.Value.Split(',');
                    if (spliced.Count() > 2)
                    {
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Warning__Incorrect_Usage_of_the___tool_program_macro_command_);
                    }
                    else
                    {
                        string programName = spliced[0];
                        string programVersion = null;
                        if (spliced.Count() > 1)
                        {
                            // Extract the version if specified.
                            programVersion = spliced[1];
                        }
                        ZippedToolsProgramPathContainer = new ProgramPathContainer(programName, programVersion);
                    }
                }
// ReSharper disable NonLocalizedString
                else if (IsNameValue(pair, "tool-program-path"))
                {
                    ZippedToolsProgramPathValue = pair.Value;
                }
                else if (IsNameOnly(pair, "tool-ignore-required-packages"))
                {
                    ZippedToolsPackagesHandled = true;
                }

                else if (IsNameValue(pair, "tool-add"))
                {
                    ImportingTool = true;
                    ToolName = pair.Value;
                }

                else if (IsNameValue(pair, "tool-command"))
                {
                    ImportingTool = true;
                    ToolCommand = pair.Value;
                }

                else if (IsNameValue(pair, "tool-arguments"))
                {
                    ImportingTool = true;
                    ToolArguments = pair.Value;
                }

                else if (IsNameValue(pair, "tool-initial-dir"))
                {
                    ImportingTool = true;
                    ToolInitialDirectory = pair.Value;
                }
                else if (IsNameValue(pair, "tool-report"))
                {
                    ImportingTool = true;
                    ToolReportTitle = pair.Value;
                }
                else if (IsNameOnly(pair, "tool-output-to-immediate-window"))
                {
                    ImportingTool = true;
                    ToolOutputToImmediateWindow = true;
                }

                else if (IsNameValue(pair, "tool-conflict-resolution"))
                {
                    string input = pair.Value.ToLowerInvariant();
                    if (input == "overwrite")
                    {
                        ResolveToolConflictsBySkipping = false;
                    }
                    if (input == "skip")
                    {
                        ResolveToolConflictsBySkipping = true;
                    }
                }
                else if (IsNameValue(pair, ARG_IMPORT_PEPTIDE_SEARCH_FILE))
                {
                    RequiresSkylineDocument = true;
                    SearchResultsFiles.Add(GetFullPath(pair.Value));
                    CutoffScore = CutoffScore ?? Settings.Default.LibraryResultCutOff;
                }
                else if (IsNameValue(pair, ARG_IMPORT_PEPTIDE_SEARCH_CUTOFF))
                {
                    double? cutoff;
                    try
                    {
                        cutoff = pair.ValueDouble;
                        if (cutoff < 0 || cutoff > 1)
                        {
                            cutoff = null;
                        }
                    }
                    catch
                    {
                        cutoff = null;
                    }
                    if (cutoff.HasValue)
                    {
                        CutoffScore = cutoff.Value;
                    }
                    else
                    {
                        var defaultScore = Settings.Default.LibraryResultCutOff;
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Warning__The_cutoff_score__0__is_invalid__It_must_be_a_value_between_0_and_1_, pair.Value);
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Defaulting_to__0__, defaultScore);
                        CutoffScore = defaultScore;
                    }
                }
                else if (IsNameOnly(pair, ARG_IMPORT_PEPTIDE_SEARCH_MODS))
                {
                    AcceptAllModifications = true;
                }
                else if (IsNameOnly(pair, ARG_IMPORT_PEPTIDE_SEARCH_AMBIGUOUS))
                {
                    IncludeAmbiguousMatches = true;
                }

                // Run each line of a text file like a SkylineRunner command
                else if (IsNameValue(pair, "batch-commands"))
                {
                    BatchCommandsPath = GetFullPath(pair.Value);
                    RunningBatchCommands = true;
                }

                else if (IsNameOnly(pair, "save"))
                {
                    Saving = true;
                    RequiresSkylineDocument = true;
                }

                else if (IsNameValue(pair, "out"))
                {
                    SaveFile = GetFullPath(pair.Value);
                    RequiresSkylineDocument = true;
                }

                else if (IsNameValue(pair, "add-library-name"))
                {
                    LibraryName = pair.Value;
                    RequiresSkylineDocument = true;
                }

                else if (IsNameValue(pair, "add-library-path"))
                {
                    LibraryPath = GetFullPath(pair.Value);
                    RequiresSkylineDocument = true;
                }

                else if (IsNameValue(pair, "import-fasta"))
                {
                    FastaPath = GetFullPath(pair.Value);
                    RequiresSkylineDocument = true;
                }

                else if (IsNameValue(pair, ARG_IMPORT_TRANSITION_LIST))
                {
                    TransitionListPath = GetFullPath(pair.Value);
                    IsTransitionListAssayLibrary = false;
                    RequiresSkylineDocument = true;
                }

                else if (IsNameValue(pair, ARG_IMPORT_ASSAY_LIBRARY))
                {
                    TransitionListPath = GetFullPath(pair.Value);
                    IsTransitionListAssayLibrary = true;
                    RequiresSkylineDocument = true;
                }

                else if (IsNameOnly(pair, ARG_IGNORE_TRANSITION_ERRORS))
                {
                    IsIgnoreTransitionErrors = true;
                }

                else if (IsNameValue(pair, ARG_IRT_STANDARDS_GROUP_NAME))
                {
                    IrtGroupName = pair.Value;
                }

                else if (IsNameValue(pair, ARG_IRT_STANDARDS_FILE))
                {
                    IrtStandardsPath = pair.Value;
                }

                else if (IsNameValue(pair, ARG_IRT_DATABASE_PATH))
                {
                    IrtDatabasePath = pair.Value;
                }

                else if (IsNameValue(pair, ARG_IRT_CALC_NAME))
                {
                    IrtCalcName = pair.Value;
                }

                else if (IsName(pair, ARG_DECOYS_ADD))
                {
                    if (string.IsNullOrEmpty(pair.Value) || pair.Value == ARG_DECOYS_ADD_VALUE_REVERSE)
                        AddDecoysType = DecoyGeneration.REVERSE_SEQUENCE;
                    else if (pair.Value == ARG_DECOYS_ADD_VALUE_SHUFFLE)
                        AddDecoysType = DecoyGeneration.SHUFFLE_SEQUENCE;
                    else
                    {
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Error__Invalid_value___0___for__1___use___2___or___3___,
                            pair.Value, ArgText(ARG_DECOYS_ADD), ARG_DECOYS_ADD_VALUE_REVERSE, ARG_DECOYS_ADD_VALUE_SHUFFLE);
                        return false;
                    }
                    RequiresSkylineDocument = true;
                }

                else if (IsNameValue(pair, ARG_DECOYS_ADD_COUNT))
                {
                    int count;
                    if (!int.TryParse(pair.Value, out count))
                    {
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Error__The_value___0___for__1__must_be_an_integer_, pair.Value, ARG_DECOYS_ADD_COUNT);
                        return false;
                    }
                    AddDecoysCount = count;
                }

                else if (IsNameOnly(pair, ARG_DECOYS_DISCARD))
                {
                    DiscardDecoys = true;
                    RequiresSkylineDocument = true;
                }

                else if (IsNameOnly(pair, "keep-empty-proteins"))
                {
                    KeepEmptyProteins = true;
                }

                else if (IsNameValue(pair, "import-file"))
                {
                    if (pair.Value.StartsWith(ChorusUrl.ChorusUrlPrefix))
                    {
                        ReplicateFile.Add(MsDataFileUri.Parse(pair.Value));
                    }
                    else
                    {
                        ReplicateFile.Add(new MsDataFilePath(GetFullPath(pair.Value)));
                    }
                    RequiresSkylineDocument = true;
                }

                else if (IsNameValue(pair, "import-file-cache"))
                {
                    if (Program.ReplicateCachePath != null)
                    {
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal___import_cache_file_can_only_be_specified_once);
                        return false;
                    }
                    Program.ReplicateCachePath = pair.Value;
                    RequiresSkylineDocument = true;
                }

                else if (IsNameValue(pair, "import-progress-pipe"))
                {
                    if (Program.ImportProgressPipe != null)
                    {
                        _out.WriteLine("--import-progress-pipe can only be specified once");
                        return false;
                    }
                    Program.ImportProgressPipe = pair.Value;
                    RequiresSkylineDocument = true;
                }

                else if (IsNameValue(pair, "import-replicate-name"))
                {
                    ReplicateName = pair.Value;
                    RequiresSkylineDocument = true;
                }

                else if (IsNameValue(pair, ARG_IMPORT_LOCKMASS_POSITIVE))
                {
                    LockmassPositive = ParseDouble(pair.Value, ARG_IMPORT_LOCKMASS_POSITIVE);
                    RequiresSkylineDocument = true;
                }

                else if (IsNameValue(pair, ARG_IMPORT_LOCKMASS_NEGATIVE))
                {
                    LockmassNegative = ParseDouble(pair.Value, ARG_IMPORT_LOCKMASS_NEGATIVE);
                    RequiresSkylineDocument = true;
                }

                else if (IsNameValue(pair, ARG_IMPORT_LOCKMASS_TOLERANCE))
                {
                    LockmassTolerance = ParseDouble(pair.Value, ARG_IMPORT_LOCKMASS_TOLERANCE);
                    RequiresSkylineDocument = true;
                }

                else if (IsNameValue(pair, ARG_IMPORT_ANNOTATIONS))
                {
                    ImportAnnotations = pair.Value;
                    RequiresSkylineDocument = true;
                }

                else if (IsNameOnly(pair, "import-append"))
                {
                    ImportAppend = true;
                    RequiresSkylineDocument = true;
                }

                else if (IsNameValue(pair, "import-all"))
                {
                    ImportSourceDirectory = GetFullPath(pair.Value);
                    RequiresSkylineDocument = true;
                }

                else if (IsNameOnly(pair, "import-no-join"))
                {
                    ImportDisableJoining = true;
                    RequiresSkylineDocument = true;
                }
                // ReSharper restore NonLocalizedString

                else if (IsNameValue(pair, "import-naming-pattern")) // Not L10N
                {
                    var importNamingPatternVal = pair.Value;
                    RequiresSkylineDocument = true;
                    if (importNamingPatternVal != null)
                    {
                        try
                        {
                            ImportNamingPattern = new Regex(importNamingPatternVal);
                        }
                        catch (Exception e)
                        {
                            _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Error__Regular_expression__0__cannot_be_parsed_, importNamingPatternVal);
                            _out.WriteLine(e.Message);
                            return false;
                        }

                        Match match = Regex.Match(importNamingPatternVal, @".*\(.+\).*"); // Not L10N
                        if (!match.Success)
                        {
                            _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Error__Regular_expression___0___does_not_have_any_groups___String,
                                importNamingPatternVal);
                            return false;
                        }
                    }
                }

                else if (IsNameValue(pair, "import-optimizing")) // Not L10N
                {
                    try
                    {
                        ImportOptimizeType = pair.Value;
                    }
                    catch (ArgumentException)
                    {
                        _out.WriteLine(
                            Resources.CommandArgs_ParseArgsInternal_Warning__Invalid_optimization_parameter___0____Use__ce____dp___or__none____Defaulting_to_none_,
                            pair.Value);
                    }
                }

                else if (IsNameValue(pair, "import-before")) // Not L10N
                {
                    var importBeforeDate = pair.Value;
                    if (importBeforeDate != null)
                    {
                        try
                        {
                            ImportBeforeDate = Convert.ToDateTime(importBeforeDate);
                        }
                        catch (Exception e)
                        {
                            _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Error__Date__0__cannot_be_parsed_, importBeforeDate);
                            _out.WriteLine(e.Message);
                            return false;
                        }
                    }
                }

                else if (IsNameValue(pair, "import-on-or-after")) // Not L10N
                {
                    var importAfterDate = pair.Value;
                    if (importAfterDate != null)
                    {
                        try
                        {
                            ImportOnOrAfterDate = Convert.ToDateTime(importAfterDate);
                        }
                        catch (Exception e)
                        {
                            _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Error__Date__0__cannot_be_parsed_, importAfterDate);
                            _out.WriteLine(e.Message);
                            return false;
                        }
                    }
                }

                else if (IsNameOnly(pair, "import-warn-on-failure"))    // Not L10N
                {
                    ImportWarnOnFailure = true;
                }

                else if (IsNameOnly(pair, "remove-all")) // Not L10N
                {
                    RemovingResults = true;
                    RequiresSkylineDocument = true;
                    RemoveBeforeDate = null;
                }
                else if (IsNameValue(pair, "remove-before")) // Not L10N
                {
                    var removeBeforeDate = pair.Value;
                    RemovingResults = true;
                    RequiresSkylineDocument = true;
                    if (removeBeforeDate != null)
                    {
                        try
                        {
                            RemoveBeforeDate = Convert.ToDateTime(removeBeforeDate);
                        }
                        catch (Exception e)
                        {
                            _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Error__Date__0__cannot_be_parsed_, removeBeforeDate);
                            _out.WriteLine(e.Message);
                            return false;
                        }
                    }
                }
                else if (IsNameValue(pair, ARG_REINTEGRATE_MODEL_NAME))
                {
                    ReintegratModelName = pair.Value;
                }
                else if (IsNameOnly(pair, ARG_REINTEGRATE_CREATE_MODEL))
                {
                    IsCreateScoringModel = true;
                    if (!IsSecondBestModel)
                        IsDecoyModel = true;
                }
                else if (IsNameValue(pair, ARG_REINTEGRATE_MODEL_ITERATION_COUNT))
                {
                    ReintegrateModelIterationCount = pair.ValueInt;
                }
                else if (IsNameOnly(pair, ARG_REINTEGRATE_OVERWRITE_PEAKS))
                {
                    IsOverwritePeaks = true;
                }
                else if (IsNameOnly(pair, ARG_REINTEGRATE_MODEL_SECOND_BEST))
                {
                    IsSecondBestModel = true;
                    IsDecoyModel = false;
                }
                else if (IsNameOnly(pair, ARG_REINTEGRATE_MODEL_BOTH))
                {
                    IsSecondBestModel = IsDecoyModel = true;
                }
                else if (IsNameOnly(pair, ARG_REINTEGRATE_LOG_TRAINING))
                {
                    IsLogTraining = true;
                }
                else if (IsNameValue(pair, ARG_REINTEGRATE_EXCLUDE_FEATURE))
                {
                    string featureName = pair.Value;
                    var calc = PeakFeatureCalculator.Calculators.FirstOrDefault(c =>
                        Equals(featureName, c.HeaderName) || Equals(featureName, c.Name));
                    if (calc == null)
                    {
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Error__Attempting_to_exclude_an_unknown_feature_name___0____Try_one_of_the_following_, featureName);
                        foreach (var featureCalculator in PeakFeatureCalculator.Calculators)
                        {
                            if (Equals(featureCalculator.HeaderName, featureCalculator.Name))
                                _out.WriteLine("    {0}", featureCalculator.HeaderName);    // Not L10N
                            else
                                _out.WriteLine(Resources.CommandArgs_ParseArgsInternal______0__or___1__, featureCalculator.HeaderName, featureCalculator.Name);
                        }
                        return false;
                    }
                    ExcludeFeatures.Add(calc);
                }
                else if (IsNameValue(pair, "report-name")) // Not L10N
                {
                    ReportName = pair.Value;
                    RequiresSkylineDocument = true;
                }

                else if (IsNameValue(pair, "report-file")) // Not L10N
                {
                    ReportFile = GetFullPath(pair.Value);
                    RequiresSkylineDocument = true;
                }

                else if (IsNameValue(pair, "report-format")) // Not L10N
                {
                    if (pair.Value.Equals("TSV", StringComparison.CurrentCultureIgnoreCase)) // Not L10N
                        ReportColumnSeparator = TextUtil.SEPARATOR_TSV;
                    else if (pair.Value.Equals("CSV", StringComparison.CurrentCultureIgnoreCase)) // Not L10N
                        ReportColumnSeparator = TextUtil.CsvSeparator;
                    else
                    {
                        _out.WriteLine(
                            Resources.CommandArgs_ParseArgsInternal_Warning__The_report_format__0__is_invalid__It_must_be_either__CSV__or__TSV__,
                            pair.Value);
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Defaulting_to_CSV_);
                        ReportColumnSeparator = TextUtil.CsvSeparator;
                    }
                }

                else if (IsName(pair, "report-invariant")) // Not L10N
                {
                    IsReportInvariant = true;
                }

                else if (IsNameValue(pair, "chromatogram-file")) // Not L10N
                {
                    ChromatogramsFile = GetFullPath(pair.Value);
                    RequiresSkylineDocument = true;
                }

                else if (IsNameOnly(pair, "chromatogram-precursors")) // Not L10N
                {
                    ChromatogramsPrecursors = true;
                }

                else if (IsNameOnly(pair, "chromatogram-products")) // Not L10N
                {
                    ChromatogramsProducts = true;
                }

                else if (IsNameOnly(pair, "chromatogram-base-peaks")) // Not L10N
                {
                    ChromatogramsBasePeaks = true;
                }

                else if (IsNameOnly(pair, "chromatogram-tics")) // Not L10N
                {
                    ChromatogramsTics = true;
                }
                else if (IsNameValue(pair, "exp-isolationlist-instrument")) // Not L10N
                {
                    try
                    {
                        IsolationListInstrumentType = pair.Value;
                        RequiresSkylineDocument = true;
                    }
                    catch (ArgumentException)
                    {
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Warning__The_instrument_type__0__is_not_valid__Please_choose_from_,
                            pair.Value);
                        foreach (string str in ExportInstrumentType.ISOLATION_LIST_TYPES)
                        {
                            _out.WriteLine(str);
                        }
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_No_isolation_list_will_be_exported_);
                    }
                }
                else if (IsNameValue(pair, "exp-translist-instrument")) // Not L10N
                {
                    try
                    {
                        TransListInstrumentType = pair.Value;
                        RequiresSkylineDocument = true;
                    }
                    catch (ArgumentException)
                    {
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Warning__The_instrument_type__0__is_not_valid__Please_choose_from_,
                            pair.Value);
                        foreach (string str in ExportInstrumentType.TRANSITION_LIST_TYPES)
                        {
                            _out.WriteLine(str);
                        }
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_No_transition_list_will_be_exported_);
                    }
                }
                else if (IsNameValue(pair, "exp-method-instrument")) // Not L10N
                {
                    try
                    {
                        MethodInstrumentType = pair.Value;
                        RequiresSkylineDocument = true;
                    }
                    catch (ArgumentException)
                    {
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Warning__The_instrument_type__0__is_not_valid__Please_choose_from_,
                            pair.Value);
                        foreach (string str in ExportInstrumentType.METHOD_TYPES)
                        {
                            _out.WriteLine(str);
                        }
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_No_method_will_be_exported_);
                    }
                }
                else if (IsNameValue(pair, "exp-file")) // Not L10N
                {
                    ExportPath = GetFullPath(pair.Value);
                    RequiresSkylineDocument = true;
                }
                else if (IsNameValue(pair, ARG_EXP_POLARITY))
                {
                    try
                    {
                        ExportPolarityFilter = (ExportPolarity)Enum.Parse(typeof(ExportPolarity), pair.Value, true);
                        RequiresSkylineDocument = true;
                    }
                    catch (Exception)
                    {
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Error____0___is_not_a_valid_value_for__1___It_must_be_one_of_the_following___2_,
                            pair.Value, ArgText(ARG_EXP_POLARITY), string.Join(", ", Helpers.GetEnumValues<ExportPolarity>().Select(p => p.ToString()))); // Not L10N
                    }    
                }
                else if (IsNameValue(pair, "exp-strategy")) // Not L10N
                {
                    ExportStrategySet = true;
                    RequiresSkylineDocument = true;

                    string strategy = pair.Value;

                    if (strategy.Equals("single", StringComparison.CurrentCultureIgnoreCase)) // Not L10N
                    {
                        //default
                    }
                    else if (strategy.Equals("protein", StringComparison.CurrentCultureIgnoreCase)) // Not L10N
                        ExportStrategy = ExportStrategy.Protein;
                    else if (strategy.Equals("buckets", StringComparison.CurrentCultureIgnoreCase)) // Not L10N
                        ExportStrategy = ExportStrategy.Buckets;
                    else
                    {
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Warning__The_export_strategy__0__is_not_valid__It_must_be_one_of_the_following___string,
                            pair.Value);
                        //already set to Single
                    }
                }

                else if (IsNameValue(pair, "exp-method-type")) // Not L10N
                {
                    var type = pair.Value;
                    RequiresSkylineDocument = true;
                    if (type.Equals("scheduled", StringComparison.CurrentCultureIgnoreCase)) // Not L10N
                    {
                        ExportMethodType = ExportMethodType.Scheduled;
                    }
                    else if (type.Equals("triggered", StringComparison.CurrentCultureIgnoreCase)) // Not L10N
                    {
                        ExportMethodType = ExportMethodType.Triggered;
                    }
                    else if (type.Equals("standard", StringComparison.CurrentCultureIgnoreCase)) // Not L10N
                    {
                        //default
                    }
                    else
                    {
                        _out.WriteLine(
                            Resources.CommandArgs_ParseArgsInternal_Warning__The_method_type__0__is_invalid__It_must_be_one_of_the_following___standard____scheduled__or__triggered__,
                            pair.Value);
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Defaulting_to_standard_);
                    }
                }

                else if (IsNameValue(pair, "exp-max-trans")) // Not L10N
                {
                    //This one can't be kept within bounds because the bounds depend on the instrument
                    //and the document. 
                    try
                    {
                        MaxTransitionsPerInjection = pair.ValueInt;
                    }
                    catch
                    {
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Warning__Invalid_max_transitions_per_injection_parameter___0___, pair.Value);
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_It_must_be_a_number__Defaulting_to__0__, AbstractMassListExporter.MAX_TRANS_PER_INJ_DEFAULT);
                        MaxTransitionsPerInjection = AbstractMassListExporter.MAX_TRANS_PER_INJ_DEFAULT;
                    }
                    RequiresSkylineDocument = true;
                }

                else if (IsNameValue(pair, "exp-optimizing")) // Not L10N
                {
                    try
                    {
                        ExportOptimizeType = pair.Value;
                    }
                    catch (ArgumentException)
                    {
                        _out.WriteLine(
                            Resources.CommandArgs_ParseArgsInternal_Warning__Invalid_optimization_parameter___0____Use__ce____dp___or__none__,
                            pair.Value);
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Defaulting_to_none_);
                    }
                    RequiresSkylineDocument = true;
                }
                else if (IsNameValue(pair, "exp-scheduling-replicate")) // Not L10N
                {
                    SchedulingReplicate = pair.Value;
                    RequiresSkylineDocument = true;
                }
                else if (IsNameValue(pair, "exp-template")) // Not L10N
                {
                    TemplateFile = GetFullPath(pair.Value);
                    RequiresSkylineDocument = true;
                }
                else if (IsNameOnly(pair, "exp-ignore-proteins")) // Not L10N
                {
                    IgnoreProteins = true;
                    RequiresSkylineDocument = true;
                }
                else if (IsNameValue(pair, "exp-primary-count")) // Not L10N
                {
                    try
                    {
                        PrimaryTransitionCount = pair.ValueInt;
                    }
                    catch
                    {
                        _out.WriteLine(
                            Resources.CommandArgs_ParseArgsInternal_Warning__The_primary_transition_count__0__is_invalid__it_must_be_a_number_between__1__and__2__,
                            pair.Value,
                            AbstractMassListExporter.PRIMARY_COUNT_MIN, AbstractMassListExporter.PRIMARY_COUNT_MAX);
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Defaulting_to__0__, AbstractMassListExporter.PRIMARY_COUNT_DEFAULT);
                    }
                    RequiresSkylineDocument = true;
                }
                else if (IsNameValue(pair, "exp-dwell-time")) // Not L10N
                {
                    try
                    {
                        DwellTime = pair.ValueInt;
                    }
                    catch
                    {
                        _out.WriteLine(
                            Resources.CommandArgs_ParseArgsInternal_Warning__The_dwell_time__0__is_invalid__it_must_be_a_number_between__1__and__2__,
                            pair.Value,
                            AbstractMassListExporter.DWELL_TIME_MIN, AbstractMassListExporter.DWELL_TIME_MAX);
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Defaulting_to__0__, AbstractMassListExporter.DWELL_TIME_DEFAULT);
                    }
                    RequiresSkylineDocument = true;
                }
                else if (IsNameOnly(pair, "exp-add-energy-ramp")) // Not L10N
                {
                    AddEnergyRamp = true;
                    RequiresSkylineDocument = true;
                }
                else if (IsNameOnly(pair, "exp-use-s-lens")) // Not L10N
                {
                    UseSlens = true;
                    RequiresSkylineDocument = true;
                }
                else if (IsNameValue(pair, "exp-run-length")) // Not L10N
                {
                    try
                    {
                        RunLength = pair.ValueInt;
                    }
                    catch
                    {
                        _out.WriteLine(
                            Resources
                                .CommandArgs_ParseArgsInternal_Warning__The_run_length__0__is_invalid__It_must_be_a_number_between__1__and__2__,
                            pair.Value,
                            AbstractMassListExporter.RUN_LENGTH_MIN, AbstractMassListExporter.RUN_LENGTH_MAX);
                        _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Defaulting_to__0__,
                            AbstractMassListExporter.RUN_LENGTH_DEFAULT);
                    }
                    RequiresSkylineDocument = true;
                }
                else if (IsNameValue(pair, PANORAMA_SERVER_URI))
                {
                    PanoramaServerUri = pair.Value;
                }
                else if (IsNameValue(pair, PANORAMA_USERNAME))
                {
                    PanoramaUserName = pair.Value;
                }
                else if (IsNameValue(pair, PANORAMA_PASSWD))
                {
                    PanoramaPassword = pair.Value;
                }
                else if (IsNameValue(pair, PANORAMA_FOLDER))
                {
                    PanoramaFolder = pair.Value;
                }
                else if (IsName(pair, "share-zip")) // Not L10N
                {
                    SharingZipFile = true;
                    RequiresSkylineDocument = true;
                    if (!string.IsNullOrEmpty(pair.Value))
                    {
                        SharedFile = pair.Value;
                    }
                }
                else
                {
                    _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Error__Unexpected_argument____0_, pair.Name);
                    return false;
                }
            }

            if (Reintegrating)
                RequiresSkylineDocument = true;
            else
            {
                if (IsCreateScoringModel)
                    WarnArgRequirment(ARG_REINTEGRATE_MODEL_NAME, ARG_REINTEGRATE_CREATE_MODEL);
                if (IsOverwritePeaks)
                    WarnArgRequirment(ARG_REINTEGRATE_MODEL_NAME, ARG_REINTEGRATE_OVERWRITE_PEAKS);
            }
            if (FullScanPrecursorResMz.HasValue && !FullScanPrecursorRes.HasValue)
                WarnArgRequirment(ARG_FULL_SCAN_PRECURSOR_RES, ARG_FULL_SCAN_PRECURSOR_RES_MZ);
            if (FullScanProductResMz.HasValue && !FullScanProductRes.HasValue)
                WarnArgRequirment(ARG_FULL_SCAN_PRODUCT_RES, ARG_FULL_SCAN_PRODUCT_RES_MZ);
            if (!IsCreateScoringModel && IsSecondBestModel)
            {
                if (IsDecoyModel)
                    WarnArgRequirment(ARG_REINTEGRATE_CREATE_MODEL, ARG_REINTEGRATE_MODEL_BOTH);
                else
                    WarnArgRequirment(ARG_REINTEGRATE_CREATE_MODEL, ARG_REINTEGRATE_MODEL_SECOND_BEST);
            }
            if (!IsCreateScoringModel && ExcludeFeatures.Count > 0)
            {
                WarnArgRequirment(ARG_REINTEGRATE_CREATE_MODEL, ARG_REINTEGRATE_EXCLUDE_FEATURE);
            }
            if (!AddDecoys && AddDecoysCount.HasValue)
            {
                WarnArgRequirment(ARG_DECOYS_ADD, ARG_DECOYS_ADD_COUNT);
            }
            if (!ImportingTransitionList)
            {
                if (IsIgnoreTransitionErrors)
                    WarnArgRequirment(ARG_IMPORT_TRANSITION_LIST, ARG_IGNORE_TRANSITION_ERRORS);
            }
            if (!ImportingTransitionList || !IsTransitionListAssayLibrary)
            {
                if (!string.IsNullOrEmpty(IrtGroupName))
                    WarnArgRequirment(ARG_IMPORT_ASSAY_LIBRARY, ARG_IRT_STANDARDS_GROUP_NAME);
                if (!string.IsNullOrEmpty(IrtStandardsPath))
                    WarnArgRequirment(ARG_IMPORT_ASSAY_LIBRARY, ARG_IRT_STANDARDS_FILE);
            }
            if (!string.IsNullOrEmpty(PanoramaServerUri) || !string.IsNullOrEmpty(PanoramaFolder))
            {
                if (!PanoramaArgsComplete())
                {
                    return false;
                }
                
                var serverUri = PanoramaUtil.ServerNameToUri(PanoramaServerUri);
                if (serverUri == null)
                {
                    _out.WriteLine(Resources.EditServerDlg_OkDialog_The_text__0__is_not_a_valid_server_name_,
                        PanoramaServerUri);
                    return false;
                }

                var panoramaClient = new WebPanoramaClient(serverUri);
                var panoramaHelper = new PanoramaHelper(_out);
                PanoramaServer = panoramaHelper.ValidateServer(panoramaClient, PanoramaUserName, PanoramaPassword);
                if (PanoramaServer == null)
                {
                    return false;
                }

                if (!panoramaHelper.ValidateFolder(panoramaClient, PanoramaServer, PanoramaFolder))
                {
                    return false;
                }

                RequiresSkylineDocument = true;
                PublishingToPanorama = true;
            }
            if (!ImportingSearch)
            {
                if (CutoffScore.HasValue)
                    WarnArgRequirment(ARG_IMPORT_PEPTIDE_SEARCH_FILE, ARG_IMPORT_PEPTIDE_SEARCH_CUTOFF);
                if (AcceptAllModifications)
                    WarnArgRequirment(ARG_IMPORT_PEPTIDE_SEARCH_FILE, ARG_IMPORT_PEPTIDE_SEARCH_MODS);
                if (IncludeAmbiguousMatches)
                    WarnArgRequirment(ARG_IMPORT_PEPTIDE_SEARCH_FILE, ARG_IMPORT_PEPTIDE_SEARCH_AMBIGUOUS);
            }
           
            // If skylineFile isn't set and one of the commands that requires --in is called, complain.
            if (String.IsNullOrEmpty(SkylineFile) && RequiresSkylineDocument && !_isDocumentLoaded)
            {
                _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Error__Use___in_to_specify_a_Skyline_document_to_open_);
                return false;
            }

            if(ImportingReplicateFile && ImportingSourceDirectory)
            {
                _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Error____import_file_and___import_all_options_cannot_be_used_simultaneously_);
                return false;
            }
            if(ImportingReplicateFile && ImportNamingPattern != null)
            {       
                _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Error____import_naming_pattern_cannot_be_used_with_the___import_file_option_);
                return false;
            }
            if(ImportingSourceDirectory && !string.IsNullOrEmpty(ReplicateName))
            {
                _out.WriteLine(Resources.CommandArgs_ParseArgsInternal_Error____import_replicate_name_cannot_be_used_with_the___import_all_option_);
                return false;
            }
            
            // Use the original file as the output file, if not told otherwise.
            if (Saving && String.IsNullOrEmpty(SaveFile))
            {
                SaveFile = SkylineFile;
            }
            return true;
        }

        public static string ArgText(string argName)
        {
            Assume.IsFalse(argName.StartsWith(ARG_PREFIX));  // Avoid duplicating
            return ARG_PREFIX + argName;
        }

        public static string WarnArgRequirementText(string requiredArg, string usedArg)
        {
            return string.Format(Resources.CommandArgs_WarnArgRequirment_Warning__Use_of_the_argument__0__requires_the_argument__1_,
                ArgText(usedArg), ArgText(requiredArg));
        }

        private void WarnArgRequirment(string requiredArg, string usedArg)
        {
            _out.WriteLine(WarnArgRequirementText(requiredArg, usedArg));
        }

        private double? ParseDouble(string value, string argName)
        {
            double valueDecimal;
            if (!double.TryParse(value, out valueDecimal))
            {
                _out.WriteLine(Resources.CommandArgs_ParseDouble_Error__The_argument__0__requires_a_decimal_number_value_,
                    ArgText(argName));
                return null;
            }
            return valueDecimal;
        }

        private static string GetFullPath(string path)
        {
            try
            {
            return Path.GetFullPath(path);
        }
            catch (Exception)
            {
                throw new IOException(string.Format(Resources.CommandArgs_GetFullPath_Failed_attempting_to_get_full_path_for__0_, path));
            }
        }

        private bool IsNameOnly(NameValuePair pair, string name)
        {
            if (!pair.Name.Equals(name))
                return false;
            if (!string.IsNullOrEmpty(pair.Value))
                throw new ValueUnexpectedException(name);
            return true;
        }

        private static bool IsNameValue(NameValuePair pair, string name)
        {
            if (!pair.Name.Equals(name))
                return false;
            if (string.IsNullOrEmpty(pair.Value))
                throw new ValueMissingException(name);
            return true;
        }

        private static bool IsName(NameValuePair pair, string name)
        {
            return pair.Name.Equals(name);
        }

        private bool PanoramaArgsComplete()
        {
            var missingArgs = new List<string>();
            const string prefix = ARG_PREFIX;
            if (string.IsNullOrWhiteSpace(PanoramaServerUri))
            {
                missingArgs.Add(prefix + PANORAMA_SERVER_URI); 
            }
            if (string.IsNullOrWhiteSpace(PanoramaUserName))
            {
                missingArgs.Add(prefix + PANORAMA_USERNAME);
            }
            if (string.IsNullOrWhiteSpace(PanoramaPassword))
            {
                missingArgs.Add(prefix + PANORAMA_PASSWD);
            }
            if (string.IsNullOrWhiteSpace(PanoramaFolder))
            {
                missingArgs.Add(prefix + PANORAMA_FOLDER);
            }

            if (missingArgs.Count > 0)
            {
                _out.WriteLine(missingArgs.Count > 1       
                    ? Resources.CommandArgs_PanoramaArgsComplete_plural_
                    : Resources.CommandArgs_PanoramaArgsComplete_,
                    TextUtil.LineSeparate(missingArgs)); 
                return false;
            }

            return true;
        }

        private class ValueMissingException : UsageException
        {
            public ValueMissingException(string name)
                : base(string.Format(Resources.ValueMissingException_ValueMissingException_,  ArgText(name)))
            {
            }
        }

        private class ValueUnexpectedException : UsageException
        {
            public ValueUnexpectedException(string name)
                : base(string.Format(Resources.ValueUnexpectedException_ValueUnexpectedException_The_argument__0__should_not_have_a_value_specified, ArgText(name)))
            {
            }
        }

        private class UsageException : ArgumentException
        {
            protected UsageException(string message) : base(message)
            {
            }
        }

        public class PanoramaHelper
        {
            private readonly TextWriter _statusWriter;
           
            public PanoramaHelper(TextWriter statusWriter)
            {
                _statusWriter = statusWriter;
            }

            public Uri ValidateServerUri(string panoramaServer)
            {
                // Make sure that the given server URL is valid.
                var serverUri = PanoramaUtil.ServerNameToUri(panoramaServer);
                if (serverUri == null)
                {
                    _statusWriter.WriteLine(Resources.EditServerDlg_OkDialog_The_text__0__is_not_a_valid_server_name_,
                        panoramaServer);
                    return null;
                }
                return serverUri;
            }

            public Server ValidateServer(IPanoramaClient panoramaClient, string panoramaUsername, string panoramaPassword)
            {
                try
                {
                    PanoramaUtil.VerifyServerInformation(panoramaClient, panoramaUsername, panoramaPassword);
                    return new Server(panoramaClient.ServerUri, panoramaUsername, panoramaPassword);
                }
                catch (PanoramaServerException x)
                {
                    _statusWriter.WriteLine(x.Message); 
                }
                catch (Exception x)
                {
                    _statusWriter.WriteLine(Resources.PanoramaHelper_ValidateServer_, x.Message);
                }

                return null;
            }

            public bool ValidateFolder(IPanoramaClient panoramaClient, Server server, string panoramaFolder)
            {
                try
                {
                    PanoramaUtil.VerifyFolder(panoramaClient, server, panoramaFolder);
                    return true;
                }
                catch (PanoramaServerException x)
                {
                    _statusWriter.WriteLine(x.Message);
                }
                catch (Exception x)
                {
                    _statusWriter.WriteLine(
                        Resources.PanoramaHelper_ValidateFolder_,
                        panoramaFolder, panoramaClient.ServerUri,
                        x.Message);
                }
                return false;
            }
        }
    }
}