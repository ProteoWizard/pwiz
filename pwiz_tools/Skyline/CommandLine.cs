/*
 * Original author: John Chilton <jchilton .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Serialization;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Optimization;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline
{
    public class CommandArgs
    {
        public string SkylineFile { get; private set; }
        public string ReplicateFile { get; private set; }
        public string ReplicateName { get; private set; }
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


        public bool ImportingResults
        {
            get { return ImportingReplicateFile || ImportingSourceDirectory; }
        }
        public bool ImportingReplicateFile
        {
            get { return !string.IsNullOrEmpty(ReplicateFile); }
        }
        public bool ImportingSourceDirectory
        {
            get { return !string.IsNullOrEmpty(ImportSourceDirectory); }
        }

        public bool RemovingResults { get; private set; }

        public bool ImportingFasta
        {
            get { return !string.IsNullOrWhiteSpace(FastaPath); }
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
        private const string ARG_REINTEGRATE_MODEL_SECOND_BEST = "reintegrate-model-second-best"; // Not L10N
        private const string ARG_REINTEGRATE_MODEL_BOTH = "reintegrate-model-both"; // Not L10N
        private const string ARG_REINTEGRATE_ANNOTATE_SCORING = "reintegrate-annotate-scoring"; // Not L10N
        private const string ARG_REINTEGRATE_OVERWRITE_PEAKS = "reintegrate-overwrite-peaks"; // Not L10N

        public string ReintegratModelName { get; private set; }
        public bool IsOverwritePeaks { get; private set; }
        public bool IsAnnotateScoring { get; private set; }
        public bool IsCreateScoringModel { get; private set; }
        public bool IsSecondBestModel { get; private set; }
        public bool IsDecoyModel { get; private set; }

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
        private int _runLength;
        public int RunLength
        {
            get { return _runLength; }
            set
            {
                if (value < AbstractMassListExporter.RUN_LENGTH_MIN || value > AbstractMassListExporter.RUN_LENGTH_MAX)
                {
                    throw new ArgumentException(string.Format(Resources.CommandArgs_RunLength_The_run_length__0__must_be_between__1__and__2__, value, AbstractMassListExporter.RUN_LENGTH_MIN, AbstractMassListExporter.RUN_LENGTH_MAX));
                }
                _runLength = value;
            }
        }

        public string ExportPath { get; private set; }

        public ExportCommandProperties ExportCommandProperties
        {
            get
            {
                return new ExportCommandProperties(_out)
                {

                    AddEnergyRamp = AddEnergyRamp,
                    DwellTime = DwellTime,
                    ExportStrategy = ExportStrategy,
                    IgnoreProteins = IgnoreProteins,
                    MaxTransitions = MaxTransitionsPerInjection,
                    MethodType = ExportMethodType,
                    OptimizeType = ExportOptimizeType,
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

            ImportBeforeDate = null;
            ImportOnOrAfterDate = null;
        }

        public struct NameValuePair
        {
            public NameValuePair(string arg)
                : this()
            {
                if (arg.StartsWith("--")) // Not L10N
                {
                    var parts = arg.Substring(2).Split('=');
                    Name = parts[0];
                    if (parts.Length > 1)
                        Value = parts[1];
                }
            }

            public string Name { get; private set; }
            public string Value { get; private set; }
            public int ValueInt { get { return int.Parse(Value); } }
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
            foreach (string s in args)
            {
                var pair = new NameValuePair(s);
                if (string.IsNullOrEmpty(pair.Name))
                    continue;

                if (IsNameValue(pair, "in")) // Not L10N
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
                else if (IsNameOnly(pair, "timestamp")) // Not L10N
                {
                    _out.IsTimeStamped = true;
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

                else if (IsNameOnly(pair, "keep-empty-proteins"))
                {
                    KeepEmptyProteins = true;
                }

                else if (IsNameValue(pair, "import-file"))
                {
                    ReplicateFile = GetFullPath(pair.Value);
                    RequiresSkylineDocument = true;
                }

                else if (IsNameValue(pair, "import-replicate-name"))
                {
                    ReplicateName = pair.Value;
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

                else if (IsNameValue(pair, "remove-all")) // Not L10N
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
                else if (IsNameOnly(pair, ARG_REINTEGRATE_ANNOTATE_SCORING))
                {
                    IsAnnotateScoring = true;
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
                if (IsAnnotateScoring)
                    WarnArgRequirment(ARG_REINTEGRATE_MODEL_NAME, ARG_REINTEGRATE_ANNOTATE_SCORING);
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

        private void WarnArgRequirment(string requiredArg, string usedArg)
        {
            _out.WriteLine(Resources.CommandArgs_ReportArgRequirment_Warning__Use_of_the_argument__0__requires_the_argument__1_, usedArg, requiredArg);
        }

        private double? ParseDouble(string value, string argName)
        {
            double valueDecimal;
            if (!double.TryParse(value, out valueDecimal))
            {
                _out.WriteLine(Resources.CommandArgs_ParseDouble_Error__The_argument__0__requires_a_decimal_number_value_, argName);
                return null;
            }
            return valueDecimal;
        }

        private static string GetFullPath(string path)
        {
            return Path.GetFullPath(path);
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
            const string prefix = "--"; // Not L10N
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
                : base(string.Format(Resources.ValueMissingException_ValueMissingException_,  "--" + name)) // Not L10N
            {
            }
        }

        private class ValueUnexpectedException : UsageException
        {
            public ValueUnexpectedException(string name)
                : base(string.Format(Resources.ValueUnexpectedException_ValueUnexpectedException_The_argument____0__should_not_have_a_value_specified, "--" + name)) // Not L10N
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

    public class CommandLine : IDisposable
    {
        private readonly CommandStatusWriter _out;

        private SrmDocument _doc;
        private string _skylineFile;

        private ExportCommandProperties _exportProperties;

        // Number of results files imported
        private int _importCount;

        public CommandLine(CommandStatusWriter output)
        {
            _out = output;
        }

        public SrmDocument Document { get { return _doc; } }

        public void Run(string[] args)
        {
            var commandArgs = new CommandArgs(_out, _doc != null);

            if(!commandArgs.ParseArgs(args))
            {
                _out.WriteLine(Resources.CommandLine_Run_Exiting___);
                return;
            }

            // First come the commands that do not depend on an --in command to run.
            // These commands modify Settings.Default instead of working with an open skyline document.
            if (commandArgs.InstallingToolsFromZip)
            {
                ImportToolsFromZip(commandArgs.ZippedToolsPath, commandArgs.ResolveZipToolConflictsBySkipping, commandArgs.ResolveZipToolAnotationConflictsBySkipping,
                                   commandArgs.ZippedToolsProgramPathContainer, commandArgs.ZippedToolsProgramPathValue, commandArgs.ZippedToolsPackagesHandled );
            }
            if (commandArgs.ImportingTool)
            {
                ImportTool(commandArgs.ToolName, commandArgs.ToolCommand, commandArgs.ToolArguments,
                    commandArgs.ToolInitialDirectory, commandArgs.ToolReportTitle, commandArgs.ToolOutputToImmediateWindow, commandArgs.ResolveToolConflictsBySkipping);
            }
            if (commandArgs.RunningBatchCommands)
            {
                RunBatchCommands(commandArgs.BatchCommandsPath);
            }
            if (commandArgs.ImportingSkyr)
            {
                ImportSkyr(commandArgs.SkyrPath, commandArgs.ResolveSkyrConflictsBySkipping);
            }
            if (!commandArgs.RequiresSkylineDocument)
            {
                // Exit quietly because Run(args[]) ran sucessfully. No work with a skyline document was called for.
                return;
            }

            string skylineFile = commandArgs.SkylineFile;
            if ((skylineFile != null && !OpenSkyFile(skylineFile)) ||
                (skylineFile == null && _doc == null))
            {
                _out.WriteLine(Resources.CommandLine_Run_Exiting___);
                return;
            }

            if (commandArgs.FullScanSetting)
            {
                if (!SetFullScanSettings(commandArgs))
                    return;
            }

            if (commandArgs.SettingLibraryPath)
            {
                if (!SetLibrary(commandArgs.LibraryName, commandArgs.LibraryPath))
                    _out.WriteLine(Resources.CommandLine_Run_Not_setting_library_);
            }

            if (commandArgs.ImportingFasta)
            {
                try
                {
                    ImportFasta(commandArgs.FastaPath, commandArgs.KeepEmptyProteins);
                }
                catch (Exception x)
                {
                    _out.WriteLine(Resources.CommandLine_Run_Error__Failed_importing_the_file__0____1_, commandArgs.FastaPath, x.Message);
                }
            }

            if (commandArgs.RemovingResults && !commandArgs.RemoveBeforeDate.HasValue)
            {
                RemoveResults(null);
            }

            if (commandArgs.ImportingResults)
            {
                OptimizableRegression optimize = null;
                try
                {
                    if (_doc != null)
                        optimize = _doc.Settings.TransitionSettings.Prediction.GetOptimizeFunction(commandArgs.ImportOptimizeType);
                }
                catch (Exception x)
                {
                    _out.WriteLine(Resources.CommandLine_Run_Error__Failed_to_get_optimization_function__0____1_, commandArgs.ImportOptimizeType, x.Message);
                }

                if (commandArgs.ImportingReplicateFile)
                {
                    // If expected results are not imported successfully, terminate
                    if (!ImportResultsFile(MsDataFileUri.Parse(commandArgs.ReplicateFile),
                                           commandArgs.ReplicateName,
                                           commandArgs.ImportBeforeDate,
                                           commandArgs.ImportOnOrAfterDate,
                                           optimize,
                                           commandArgs.ImportAppend,
                                           commandArgs.ImportDisableJoining))
                        return;
                }
                else if(commandArgs.ImportingSourceDirectory)
                {
                    // If expected results are not imported successfully, terminate
                    if(!ImportResultsInDir(commandArgs.ImportSourceDirectory,
                                           commandArgs.ImportNamingPattern,
                                           commandArgs.ImportBeforeDate,
                                           commandArgs.ImportOnOrAfterDate,
                                           optimize,
                                           commandArgs.ImportDisableJoining))
                        return;
                }
            }

            if (_doc != null && !_doc.IsLoaded)
            {
                IProgressMonitor progressMonitor = new CommandProgressMonitor(_out, new ProgressStatus(string.Empty));
                var docContainer = new ResultsMemoryDocumentContainer(null, _skylineFile) { ProgressMonitor = progressMonitor };
                docContainer.SetDocument(_doc, null, true);
                _doc = docContainer.Document;
            }

            if (commandArgs.RemovingResults && commandArgs.RemoveBeforeDate.HasValue)
            {
                RemoveResults(commandArgs.RemoveBeforeDate);
            }

            if (commandArgs.Reintegrating)
            {
                if (!ReintegratePeaks(commandArgs))
                    return;
            }

            if (commandArgs.Saving)
            {
                SaveFile(commandArgs.SaveFile ?? _skylineFile);
            }

            if (commandArgs.ExportingReport)
            {
                ExportReport(commandArgs.ReportName, commandArgs.ReportFile,
                    commandArgs.ReportColumnSeparator, commandArgs.IsReportInvariant);
            }

            if (!string.IsNullOrEmpty(commandArgs.TransListInstrumentType) &&
                !string.IsNullOrEmpty(commandArgs.MethodInstrumentType))
            {
                _out.WriteLine(Resources.CommandLine_Run_Error__You_cannot_simultaneously_export_a_transition_list_and_a_method___Neither_will_be_exported__);
            }
            else
            {
                if (commandArgs.ExportingTransitionList)
                {
                    ExportInstrumentFile(ExportFileType.List, commandArgs);
                }

                if (commandArgs.ExportingMethod)
                {
                    ExportInstrumentFile(ExportFileType.Method, commandArgs);
                }
            }
            if (commandArgs.SharingZipFile)
            {
                string sharedFileName;
                var sharedFileDir = Path.GetDirectoryName(_skylineFile) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(commandArgs.SharedFile))
                {
                    sharedFileName = Path.GetFileName(commandArgs.SharedFile.Trim());
                    if (!PathEx.HasExtension(sharedFileName, SrmDocumentSharing.EXT_SKY_ZIP))
                    {
                        sharedFileName = Path.GetFileNameWithoutExtension(sharedFileName) + SrmDocumentSharing.EXT_SKY_ZIP;
                    }
                    var dir = Path.GetDirectoryName(commandArgs.SharedFile);
                    sharedFileDir = string.IsNullOrEmpty(dir) ? sharedFileDir : dir;
                }
                else
                {
                    sharedFileName = FileEx.GetTimeStampedFileName(_skylineFile);
                }
                var sharedFilePath = Path.Combine(sharedFileDir, sharedFileName);
                ShareDocument(_doc, _skylineFile, sharedFilePath, _out);
            }
            if (commandArgs.PublishingToPanorama)
            {
                if (_importCount > 0)
                {
                    // Publish document to the given folder on the Panorama Server
                    var panoramaHelper = new PanoramaPublishHelper(_out);
                    panoramaHelper.PublishToPanorama(commandArgs.PanoramaServer, _doc, _skylineFile,
                        commandArgs.PanoramaFolder);
                }
                else
                {
                    _out.WriteLine(Resources.CommandLine_Run_No_new_results_added__Skipping_Panorama_import_);  
                }
            }
        }

        private bool SetFullScanSettings(CommandArgs commandArgs)
        {
            try
            {
                if (commandArgs.FullScanPrecursorRes.HasValue)
                {
                    double res = commandArgs.FullScanPrecursorRes.Value;
                    double? resMz = commandArgs.FullScanPrecursorResMz;
                    if (!_doc.Settings.TransitionSettings.FullScan.IsHighResPrecursor)
                        _out.WriteLine(Resources.CommandLine_SetFullScanSettings_Changing_full_scan_precursor_resolution_to__0__, res);
                    else if (resMz.HasValue)
                        _out.WriteLine(Resources.CommandLine_SetFullScanSettings_Changing_full_scan_precursor_resolving_power_to__0__at__1__, res, resMz);
                    else
                        _out.WriteLine(Resources.CommandLine_SetFullScanSettings_Changing_full_scan_precursor_resolving_power_to__0__, res);
                    _doc = _doc.ChangeSettings(_doc.Settings.ChangeTransitionFullScan(f =>
                        f.ChangePrecursorResolution(f.PrecursorMassAnalyzer, res, resMz ?? f.PrecursorResMz)));
                }
                if (commandArgs.FullScanProductRes.HasValue)
                {
                    double res = commandArgs.FullScanProductRes.Value;
                    double? resMz = commandArgs.FullScanProductResMz;
                    if (!_doc.Settings.TransitionSettings.FullScan.IsHighResProduct)
                        _out.WriteLine(Resources.CommandLine_SetFullScanSettings_Changing_full_scan_product_resolution_to__0__, res);
                    else if (resMz.HasValue)
                        _out.WriteLine(Resources.CommandLine_SetFullScanSettings_Changing_full_scan_product_resolving_power_to__0__at__1__, res, resMz);
                    else
                        _out.WriteLine(Resources.CommandLine_SetFullScanSettings_Changing_full_scan_product_resolving_power_to__0__, res);
                    _doc = _doc.ChangeSettings(_doc.Settings.ChangeTransitionFullScan(f =>
                        f.ChangeProductResolution(f.ProductMassAnalyzer, res, resMz ?? f.ProductResMz)));
                }
                if (commandArgs.FullScanRetentionTimeFilterLength.HasValue)
                {
                    double rtLen = commandArgs.FullScanRetentionTimeFilterLength.Value;
                    if (_doc.Settings.TransitionSettings.FullScan.RetentionTimeFilterType == RetentionTimeFilterType.scheduling_windows)
                        _out.WriteLine(Resources.CommandLine_SetFullScanSettings_Changing_full_scan_extraction_to______0__minutes_from_predicted_value_, rtLen);
                    else if (_doc.Settings.TransitionSettings.FullScan.RetentionTimeFilterType == RetentionTimeFilterType.ms2_ids)
                        _out.WriteLine(Resources.CommandLine_SetFullScanSettings_Changing_full_scan_extraction_to______0__minutes_from_MS_MS_IDs_, rtLen);
                    _doc = _doc.ChangeSettings(_doc.Settings.ChangeTransitionFullScan(f =>
                        f.ChangeRetentionTimeFilter(f.RetentionTimeFilterType, rtLen)));
                }
                return true;
            }
            catch (Exception x)
            {
                _out.WriteLine(Resources.CommandLine_SetFullScanSettings_Error__Failed_attempting_to_change_the_transiton_full_scan_settings_);
                _out.WriteLine(x.Message);
                return false;
            }
        }

        public bool OpenSkyFile(string skylineFile)
        {
            try
            {
                var progressMonitor = new CommandProgressMonitor(_out, new ProgressStatus(string.Empty));
                using (var stream = new StreamReaderWithProgress(skylineFile, progressMonitor))
                {
                    XmlSerializer xmlSerializer = new XmlSerializer(typeof(SrmDocument));
                    _out.WriteLine(Resources.CommandLine_OpenSkyFile_Opening_file___);

                    _doc = ConnectDocument((SrmDocument)xmlSerializer.Deserialize(stream), skylineFile);
                    if (_doc == null)
                        return false;

                    _out.WriteLine(Resources.CommandLine_OpenSkyFile_File__0__opened_, Path.GetFileName(skylineFile));
                }
            }
            catch (FileNotFoundException)
            {
                _out.WriteLine(Resources.CommandLine_OpenSkyFile_Error__The_Skyline_file__0__does_not_exist_, skylineFile);
                return false;
            }
            catch (Exception x)
            {
                _out.WriteLine(Resources.CommandLine_OpenSkyFile_Error__There_was_an_error_opening_the_file__0_, skylineFile);
                _out.WriteLine(XmlUtil.GetInvalidDataMessage(skylineFile, x));
                return false;
            }
            _skylineFile = skylineFile;
            return true;
        }

        private SrmDocument ConnectDocument(SrmDocument document, string path)
        {
            document = ConnectLibrarySpecs(document, path);
            if (document != null)
                document = ConnectBackgroundProteome(document, path);
            if (document != null)
                document = ConnectIrtDatabase(document, path);
            if (document != null)
                document = ConnectOptimizationDatabase(document, path);
            if (document != null)
                document = ConnectIonMobilityDatabase(document, path);
            return document;
        }

        private SrmDocument ConnectLibrarySpecs(SrmDocument document, string documentPath)
        {
            string docLibFile = null;
            if (!string.IsNullOrEmpty(documentPath) && document.Settings.PeptideSettings.Libraries.HasDocumentLibrary)
            {
                docLibFile = BiblioSpecLiteSpec.GetLibraryFileName(documentPath);
                if (!File.Exists(docLibFile))
                {
                    _out.WriteLine(Resources.CommandLine_ConnectLibrarySpecs_Error__Could_not_find_the_spectral_library__0__for_this_document_, docLibFile);
                    return null;
                }
            }

            var settings = document.Settings.ConnectLibrarySpecs(library =>
            {
                LibrarySpec spec;
                if (Settings.Default.SpectralLibraryList.TryGetValue(library.Name, out spec))
                {
                    if (File.Exists(spec.FilePath))
                        return spec;
                }

                string fileName = library.FileNameHint;
                if (fileName != null)
                {
                    // First look for the file name in the document directory
                    string pathLibrary = Path.Combine(Path.GetDirectoryName(documentPath) ?? string.Empty, fileName);
                    if (File.Exists(pathLibrary))
                        return library.CreateSpec(pathLibrary).ChangeDocumentLocal(true);
                    // In the user's default library directory
                    pathLibrary = Path.Combine(Settings.Default.LibraryDirectory, fileName);
                    if (File.Exists(pathLibrary))
                        return library.CreateSpec(pathLibrary);
                }
                _out.WriteLine(Resources.CommandLine_ConnectLibrarySpecs_Warning__Could_not_find_the_spectral_library__0_, library.Name);
                return library.CreateSpec(null);
            }, docLibFile);

            if (ReferenceEquals(settings, document.Settings))
                return document;

            // If the libraries were moved to disconnected state, then avoid updating
            // the document tree for this change, or it will strip all the library
            // information off the document nodes.
            if (settings.PeptideSettings.Libraries.DisconnectedLibraries != null)
                return document.ChangeSettingsNoDiff(settings);

            return document.ChangeSettings(settings);
        }

        private SrmDocument ConnectIrtDatabase(SrmDocument document, string documentPath)
        {
            var settings = document.Settings.ConnectIrtDatabase(calc => FindIrtDatabase(documentPath, calc));
            if (settings == null)
                return null;
            if (ReferenceEquals(settings, document.Settings))
                return document;
            return document.ChangeSettings(settings);
        }


        private RCalcIrt FindIrtDatabase(string documentPath, RCalcIrt irtCalc)
        {
            RetentionScoreCalculatorSpec result;
            if (Settings.Default.RTScoreCalculatorList.TryGetValue(irtCalc.Name, out result))
            {
                var irtCalcSettings = result as RCalcIrt;
                if (irtCalcSettings != null && (irtCalcSettings.IsUsable || File.Exists(irtCalcSettings.DatabasePath)))
                {
                    return irtCalcSettings;
                }
            }

            // First look for the file name in the document directory
            string fileName = Path.GetFileName(irtCalc.DatabasePath);
            string filePath = Path.Combine(Path.GetDirectoryName(documentPath) ?? string.Empty, fileName ?? string.Empty);

            if (File.Exists(filePath))
            {
                try
                {
                    return irtCalc.ChangeDatabasePath(filePath);
                }
                catch (CalculatorException)
                {
                    //Todo: should this fail silenty or report an error
                }
            }

            _out.WriteLine(Resources.CommandLine_FindIrtDatabase_Error__Could_not_find_the_iRT_database__0__, Path.GetFileName(irtCalc.DatabasePath));
            return null;
        }

        private SrmDocument ConnectOptimizationDatabase(SrmDocument document, string documentPath)
        {
            var settings = document.Settings.ConnectOptimizationDatabase(lib => FindOptimizationDatabase(documentPath, lib));
            if (settings == null)
                return null;
            if (ReferenceEquals(settings, document.Settings))
                return document;
            return document.ChangeSettings(settings);
        }

        private OptimizationLibrary FindOptimizationDatabase(string documentPath, OptimizationLibrary optLib)
        {
            OptimizationLibrary result;
            if (Settings.Default.OptimizationLibraryList.TryGetValue(optLib.Name, out result))
            {
                if (result.IsNone || File.Exists(result.DatabasePath))
                    return result;
            }

            // First look for the file name in the document directory
            string fileName = Path.GetFileName(optLib.DatabasePath);
            string filePath = Path.Combine(Path.GetDirectoryName(documentPath) ?? string.Empty, fileName ?? string.Empty);

            if (File.Exists(filePath))
            {
                try
                {
                    return optLib.ChangeDatabasePath(filePath);
                }
                // ReSharper disable once EmptyGeneralCatchClause
                catch (Exception)
                {
                    //Todo: should this fail silenty or report an error
                }
            }

            _out.WriteLine(Resources.CommandLine_FindOptimizationDatabase_Could_not_find_the_optimization_library__0__, Path.GetFileName(optLib.DatabasePath));
            return null;
        }

        private SrmDocument ConnectIonMobilityDatabase(SrmDocument document, string documentPath)
        {
            var settings = document.Settings.ConnectIonMobilityLibrary(imdb => FindIonMobilityDatabase(documentPath, imdb));
            if (settings == null)
                return null;
            if (ReferenceEquals(settings, document.Settings))
                return document;
            return document.ChangeSettings(settings);
        }

        private IonMobilityLibrarySpec FindIonMobilityDatabase(string documentPath, IonMobilityLibrarySpec ionMobilityLibSpec)
        {

            IonMobilityLibrarySpec result;
            if (Settings.Default.IonMobilityLibraryList.TryGetValue(ionMobilityLibSpec.Name, out result))
            {
                if (result.IsNone || File.Exists(result.PersistencePath))
                    return result;                
            }

            // First look for the file name in the document directory
            string fileName = Path.GetFileName(ionMobilityLibSpec.PersistencePath);
            string filePath = Path.Combine(Path.GetDirectoryName(documentPath) ?? string.Empty, fileName ?? string.Empty);

            if (File.Exists(filePath))
            {
                try
                {
                    var lib = ionMobilityLibSpec as IonMobilityLibrary;
                    if (lib != null)
                        return lib.ChangeDatabasePath(filePath);
                }
// ReSharper disable once EmptyGeneralCatchClause
                catch
                {
                    //Todo: should this fail silenty or report an error
                }
            }

            _out.WriteLine(Resources.CommandLine_FindIonMobilityDatabase_Error__Could_not_find_the_ion_mobility_library__0__, Path.GetFileName(ionMobilityLibSpec.PersistencePath));
            return null;
        }

        private SrmDocument ConnectBackgroundProteome(SrmDocument document, string documentPath)
        {
            var settings = document.Settings.ConnectBackgroundProteome(backgroundProteomeSpec =>
                FindBackgroundProteome(documentPath, backgroundProteomeSpec));
            if (settings == null)
                return null;
            if (ReferenceEquals(settings, document.Settings))
                return document;
            return document.ChangeSettings(settings);
        }

        private BackgroundProteomeSpec FindBackgroundProteome(string documentPath, BackgroundProteomeSpec backgroundProteomeSpec)
        {
            var result = Settings.Default.BackgroundProteomeList.GetBackgroundProteomeSpec(backgroundProteomeSpec.Name);
            if (result != null)
            {
                if (result.IsNone || File.Exists(result.DatabasePath))
                    return result;                
            }

            string fileName = Path.GetFileName(backgroundProteomeSpec.DatabasePath);
            // First look for the file name in the document directory
            string pathBackgroundProteome = Path.Combine(Path.GetDirectoryName(documentPath) ?? string.Empty, fileName ?? string.Empty);
            if (File.Exists(pathBackgroundProteome))
                return new BackgroundProteomeSpec(backgroundProteomeSpec.Name, pathBackgroundProteome);
            // In the user's default library directory
            pathBackgroundProteome = Path.Combine(Settings.Default.ProteomeDbDirectory, fileName ?? string.Empty);
            if (File.Exists(pathBackgroundProteome))
                return new BackgroundProteomeSpec(backgroundProteomeSpec.Name, pathBackgroundProteome);
            _out.WriteLine(Resources.CommandLine_FindBackgroundProteome_Warning__Could_not_find_the_background_proteome_file__0__, Path.GetFileName(fileName));
            return BackgroundProteomeList.GetDefault();
        }

        public bool ImportResultsInDir(string sourceDir, Regex namingPattern, DateTime? importBefore, DateTime? importOnOrAfter,
            OptimizableRegression optimize, bool disableJoining)
        {
            var listNamedPaths = GetDataSources(sourceDir, namingPattern);
            if (listNamedPaths == null)
            {
                return false;
            }

            bool hasMultiple = listNamedPaths.SelectMany(pair => pair.Key).Count() > 1;
            if (hasMultiple || disableJoining)
            {
                // Join at the end
                _doc = _doc.ChangeSettingsNoDiff(_doc.Settings.ChangeIsResultsJoiningDisabled(true));
            }

            // Import files one at a time
            foreach (var namedPaths in listNamedPaths)
            {
                string replicateName = namedPaths.Key;
                var files = namedPaths.Value;
                foreach (var file in files)
                {
                    if (!ImportResultsFile(file, replicateName, importBefore, importOnOrAfter, optimize))
                        return false;
                }
            }

            if (hasMultiple && !disableJoining)
            {
                // Allow joining to happen
                var progressMonitor = new CommandProgressMonitor(_out, new ProgressStatus(string.Empty));
                var docContainer = new ResultsMemoryDocumentContainer(null, _skylineFile) { ProgressMonitor = progressMonitor };
                _doc = _doc.ChangeSettingsNoDiff(_doc.Settings.ChangeIsResultsJoiningDisabled(false));
                if (!_doc.IsLoaded)
                {
                    docContainer.SetDocument(_doc, null, true);
                    _doc = docContainer.Document;
                    // If not fully loaded now, there must have been an error.
                    if (!_doc.IsLoaded)
                        return false;
                }
            }

            return true;
        }

        private IList<KeyValuePair<string, MsDataFileUri[]>> GetDataSources(string sourceDir, Regex namingPattern)
        {   
            // get all the valid data sources (files and sub directories) in this directory.
            IList<KeyValuePair<string, MsDataFileUri[]>> listNamedPaths;
            try
            {
                listNamedPaths = DataSourceUtil.GetDataSources(sourceDir).ToArray();
            }
            catch(IOException e)
            {
                _out.WriteLine(Resources.CommandLine_GetDataSources_Error__Failure_reading_file_information_from_directory__0__, sourceDir);
                _out.WriteLine(e.Message);
                return null;
            }
            if (!listNamedPaths.Any())
            {
                _out.WriteLine(Resources.CommandLine_GetDataSources_Error__No_data_sources_found_in_directory__0__, sourceDir);
                return null;
            }

            // If we were given a regular expression apply it to the replicate names
            if(namingPattern != null)
            {
                List<KeyValuePair<string, MsDataFileUri[]>> listRenamedPaths;
                if(!ApplyNamingPattern(listNamedPaths, namingPattern, out listRenamedPaths))
                {
                    return null;
                }
                listNamedPaths = listRenamedPaths;
            }

            // Make sure the existing replicate does not have any "unexpected" files.
            if(!CheckReplicateFiles(listNamedPaths))
            {
                return null;
            }

            // remove replicates and/or files that have already been imported into the document
            List<KeyValuePair<string, MsDataFileUri[]>> listNewPaths;
            if(!RemoveImportedFiles(listNamedPaths, out listNewPaths))
            {
                return null;
            }
            return listNewPaths;
        }

        private bool ApplyNamingPattern(IEnumerable<KeyValuePair<string, MsDataFileUri[]>> listNamedPaths, Regex namingPattern, 
                                        out List<KeyValuePair<string, MsDataFileUri[]>> listRenamedPaths)
        {
            listRenamedPaths = new List<KeyValuePair<string, MsDataFileUri[]>>();

            var uniqNames = new HashSet<string>();

            foreach (var namedPaths in listNamedPaths)
            {
                var replName = namedPaths.Key;
                Match match = namingPattern.Match(replName);
                if (match.Success)
                {
                    // Get the value of the first group
                    var replNameNew = match.Groups[1].Value;
                    if (string.IsNullOrEmpty(replNameNew))
                    {
                        _out.WriteLine(Resources.CommandLine_ApplyNamingPattern_Error__Match_to_regular_expression_is_empty_for__0__, replName);
                        return false;
                    }                    
                    if (uniqNames.Contains(replNameNew))
                    {
                        _out.WriteLine(Resources.CommandLine_ApplyNamingPattern_Error__Duplicate_replicate_name___0___after_applying_regular_expression_, replNameNew);
                        return false;
                    }
                    uniqNames.Add(replNameNew);
                    listRenamedPaths.Add(new KeyValuePair<string, MsDataFileUri[]>(replNameNew, namedPaths.Value));
                }
                else
                {
                    _out.WriteLine(Resources.CommandLine_ApplyNamingPattern_Error___0__does_not_match_the_regular_expression_, replName);
                    return false;
                }
            }

            return true;
        }

        private bool CheckReplicateFiles(IEnumerable<KeyValuePair<string, MsDataFileUri[]>> listNamedPaths)
        {
            if (!_doc.Settings.HasResults)
            {
                return true;
            }

            // Make sure the existing replicate does not have any "unexpected" files.
            // All existing files must be present in the current 
            // list of files that we are trying to import to this replicate.
            
            foreach (var namedPaths in listNamedPaths)
            {
                var replicateName = namedPaths.Key;

                // check if the document already has a replicate with this name
                int indexChrom;
                ChromatogramSet chromatogram;
                if (_doc.Settings.MeasuredResults.TryGetChromatogramSet(replicateName, out chromatogram, out indexChrom))
                {
                    // and whether the files it contains match what is expected
                    // compare case-insensitive on Windows
                    var filePaths = new HashSet<MsDataFileUri>(namedPaths.Value.Select(path => path.ToLower()));
                    foreach (var dataFilePath in chromatogram.MSDataFilePaths)
                    {
                        if (!filePaths.Contains(dataFilePath.ToLower()))
                        {
                            _out.WriteLine(
                                Resources.CommandLine_CheckReplicateFiles_Error__Replicate__0__in_the_document_has_an_unexpected_file__1__,
                                replicateName,
                                dataFilePath);
                            return false;
                        }
                    }
                }
            }
            return true;
        }
        
        private bool RemoveImportedFiles(IEnumerable<KeyValuePair<string, MsDataFileUri[]>> listNamedPaths,
                                         out List<KeyValuePair<string, MsDataFileUri[]>> listNewNamedPaths)
        {
            listNewNamedPaths = new List<KeyValuePair<string, MsDataFileUri[]>>();

            if(!_doc.Settings.HasResults)
            {
                listNewNamedPaths.AddRange(listNamedPaths);
                return true;
            }

            foreach (var namedPaths in listNamedPaths)
            {
                var replicateName = namedPaths.Key;

                // check if the document already has a replicate with this name
                int indexChrom;
                ChromatogramSet chromatogram;
                if (!_doc.Settings.MeasuredResults.TryGetChromatogramSet(replicateName,
                                                                         out chromatogram, out indexChrom))
                {
                    listNewNamedPaths.Add(namedPaths);
                }
                else
                {   
                    // We are appending to an existing replicate in the document.
                    // Remove files that are already associated with the replicate
                    var chromatFilePaths = new HashSet<MsDataFileUri>(chromatogram.MSDataFilePaths.Select(path => path.ToLower()));

                    var filePaths = namedPaths.Value;
                    var filePathsNotInRepl = new List<MsDataFileUri>(filePaths.Length);
                    foreach (var fpath in filePaths)
                    {
                        if (chromatFilePaths.Contains(fpath.ToLower()))
                        {
                            _out.WriteLine(Resources.CommandLine_RemoveImportedFiles__0______1___Note__The_file_has_already_been_imported__Ignoring___, replicateName, fpath);
                        }
                        else
                        {
                            filePathsNotInRepl.Add(fpath);
                        }
                    }

                    if (filePathsNotInRepl.Count > 0)
                    {
                        listNewNamedPaths.Add(new KeyValuePair<string, MsDataFileUri[]>(replicateName,
                                                                                 filePathsNotInRepl.ToArray()));
                    }
                }
            }
            return true;
        }

        public bool ImportResultsFile(MsDataFileUri replicateFile, string replicateName, DateTime? importBefore, DateTime? importOnOrAfter,
            OptimizableRegression optimize, bool append, bool disableJoining)
        {
            if (string.IsNullOrEmpty(replicateName))
                replicateName = replicateFile.GetFileNameWithoutExtension();

            if(_doc.Settings.HasResults && _doc.Settings.MeasuredResults.ContainsChromatogram(replicateName))
            {
                if (!append)
                {
                    // CONSIDER: Error? Check if the replicate contains the file?
                    //           It does not seem right to just continue on to export a report
                    //           or new method without the results added.
                    _out.WriteLine(Resources.CommandLine_ImportResultsFile_Warning__The_replicate__0__already_exists_in_the_given_document_and_the___import_append_option_is_not_specified___The_replicate_will_not_be_added_to_the_document_, replicateName);
                    return true;
                }
                
                // If we are appending to an existing replicate in the document
                // make sure this file is not already in the replicate.
                ChromatogramSet chromatogram;
                int index;
                _doc.Settings.MeasuredResults.TryGetChromatogramSet(replicateName, out chromatogram, out index);

                string replicateFileString = replicateFile.ToString();
                if (chromatogram.MSDataFilePaths.Any(filePath=>StringComparer.OrdinalIgnoreCase.Equals(filePath, replicateFileString)))
                {
                    _out.WriteLine(Resources.CommandLine_ImportResultsFile__0______1___Note__The_file_has_already_been_imported__Ignoring___, replicateName, replicateFile);
                    return true;
                }
            }

            return ImportResultsFile(replicateFile, replicateName, importBefore, importOnOrAfter, optimize, disableJoining);
        }

        public bool ImportResultsFile(MsDataFileUri replicateFile, string replicateName, DateTime? importBefore, DateTime? importOnOrAfter,
            OptimizableRegression optimize, bool disableJoining = false)
        {
            // Skip if file write time is after importBefore or before importAfter
            try
            {
                var fileLastWriteTime = replicateFile.GetFileLastWriteTime();
                if (importBefore != null && importBefore < fileLastWriteTime)
                {
                    _out.WriteLine(Resources.CommandLine_ImportResultsFile_File_write_date__0__is_after___import_before_date__1___Ignoring___,
                        fileLastWriteTime, importBefore);
                    return true;
                }
                else if (importOnOrAfter != null && importOnOrAfter >= fileLastWriteTime)
                {
                    _out.WriteLine(Resources.CommandLine_ImportResultsFile_File_write_date__0__is_before___import_on_or_after_date__1___Ignoring___, fileLastWriteTime, importOnOrAfter);
                    return true;
                }
            }
            catch (Exception e)
            {
                _out.WriteLine(Resources.CommandLine_ImportResultsInDir_Error__Could_not_get_last_write_time_for_file__0__, replicateFile);
                _out.WriteLine(e);
                return false;
            }

            _out.WriteLine(Resources.CommandLine_ImportResultsFile_Adding_results___);

            // Hack for un-readable RAW files from Thermo instruments.
            if(!CanReadFile(replicateFile))
            {
                _out.WriteLine(Resources.CommandLine_ImportResultsFile_Warning__Cannot_read_file__0____Ignoring___, replicateFile);
                return true;
            }

            //This function will also detect whether the replicate exists in the document
            ProgressStatus status;
            SrmDocument newDoc;
            IProgressMonitor progressMonitor = new CommandProgressMonitor(_out, new ProgressStatus(string.Empty));

            try
            {
                if (disableJoining)
                    _doc = _doc.ChangeSettingsNoDiff(_doc.Settings.ChangeIsResultsJoiningDisabled(true));

                newDoc = ImportResults(_doc, _skylineFile, replicateName, replicateFile, optimize, progressMonitor, out status);
            }
            catch (Exception x)
            {
                _out.WriteLine(Resources.CommandLine_ImportResultsFile_Error__Failed_importing_the_results_file__0__, replicateFile);
                _out.WriteLine(x.Message);
                return false;
            }

            status = status ?? new ProgressStatus(string.Empty).Complete();
            if (status.IsError && status.ErrorException != null)
            {
                if (status.ErrorException is MissingDataException)
                {
                    _out.WriteLine(Resources.CommandLine_ImportResultsFile_Warning__Failed_importing_the_results_file__0____Ignoring___, replicateFile);
                    _out.WriteLine(status.ErrorException.Message);
                    return true;
                }
                _out.WriteLine(Resources.CommandLine_ImportResultsFile_Error__Failed_importing_the_results_file__0__, replicateFile);
                _out.WriteLine(status.ErrorException.Message);
                return false;
            }
            if (!status.IsComplete || ReferenceEquals(_doc, newDoc))
            {
                _out.WriteLine(Resources.CommandLine_ImportResultsFile_Error__Failed_importing_the_results_file__0__, replicateFile);
                return false;
            }

            _doc = newDoc;

            _out.WriteLine(Resources.CommandLine_ImportResultsFile_Results_added_from__0__to_replicate__1__, replicateFile.GetFileName(), replicateName);
            //the file was imported successfully
            _importCount++;
            return true;
        }

        public void RemoveResults(DateTime? removeBefore)
        {
            if (removeBefore.HasValue)
                _out.WriteLine(Resources.CommandLine_RemoveResults_Removing_results_before_ + removeBefore.Value.ToShortDateString() + "..."); // Not L10N
            else
                _out.WriteLine(Resources.CommandLine_RemoveResults_Removing_all_results);
            var filteredChroms = new List<ChromatogramSet>();
            if (_doc.Settings.MeasuredResults == null)
            {
                // No imported results in the document.
                return;
            }
            foreach (var chromSet in _doc.Settings.MeasuredResults.Chromatograms)
            {
                var listFileInfosRemaining = new ChromFileInfo[0];
                if (removeBefore.HasValue)
                {
                    listFileInfosRemaining = chromSet.MSDataFileInfos.Where(fileInfo =>
                        fileInfo.RunStartTime == null || fileInfo.RunStartTime >= removeBefore).ToArray();
                }
                if (ArrayUtil.ReferencesEqual(listFileInfosRemaining, chromSet.MSDataFileInfos))
                    filteredChroms.Add(chromSet);
                else
                {
                    foreach (var fileInfo in chromSet.MSDataFileInfos.Except(listFileInfosRemaining))
                        _out.WriteLine(Resources.CommandLine_RemoveResults_Removed__0__, fileInfo.FilePath);
                    if (listFileInfosRemaining.Any())
                        filteredChroms.Add(chromSet.ChangeMSDataFileInfos(listFileInfosRemaining));
                }
            }
            if (!ArrayUtil.ReferencesEqual(filteredChroms, _doc.Settings.MeasuredResults.Chromatograms))
            {
                MeasuredResults newMeasuredResults = filteredChroms.Any() ?
                    _doc.Settings.MeasuredResults.ChangeChromatograms(filteredChroms) : null;

                _doc = _doc.ChangeMeasuredResults(newMeasuredResults);
            }
        }

        private bool ReintegratePeaks(CommandArgs commandArgs)
        {
            if (!_doc.Settings.HasResults)
            {
                _out.WriteLine(Resources.CommandLine_ReintegratePeaks_Error__You_must_first_import_results_into_the_document_before_reintegrating_);
                return false;
            }
            else
            {
                ModelAndFeatures modelAndFeatures;
                if (commandArgs.IsCreateScoringModel)
                {
                    modelAndFeatures = CreateScoringModel(commandArgs.ReintegratModelName,
                        commandArgs.IsDecoyModel, commandArgs.IsSecondBestModel);

                    if (modelAndFeatures == null)
                        return false;
                }
                else
                {
                    PeakScoringModelSpec scoringModel;
                    if (!Settings.Default.PeakScoringModelList.TryGetValue(commandArgs.ReintegratModelName, out scoringModel))
                    {
                        _out.WriteLine(Resources.CommandLine_ReintegratePeaks_Error__Unknown_peak_scoring_model___0__);
                        return false;
                    }
                    modelAndFeatures = new ModelAndFeatures(scoringModel, null);
                }

                if (!Reintegrate(modelAndFeatures, commandArgs.IsAnnotateScoring, commandArgs.IsOverwritePeaks))
                    return false;
            }
            return true;
        }

        private class ModelAndFeatures
        {
            public ModelAndFeatures(PeakScoringModelSpec scoringModel, IList<PeakTransitionGroupFeatures> features)
            {
                ScoringModel = scoringModel;
                Features = features;
            }

            public PeakScoringModelSpec ScoringModel { get; private set; }
            public IList<PeakTransitionGroupFeatures> Features { get; private set; }
        }

        private ModelAndFeatures CreateScoringModel(string modelName, bool decoys, bool secondBest)
        {
            _out.WriteLine(Resources.CommandLine_CreateScoringModel_Creating_scoring_model__0_, modelName);

            try
            {
                // Create new scoring model using the default calculators.
                var scoringModel = new MProphetPeakScoringModel(modelName, null as LinearModelParams, null, decoys, secondBest);
                var progressMonitor = new CommandProgressMonitor(_out, new ProgressStatus(String.Empty));
                var targetDecoyGenerator = new TargetDecoyGenerator(_doc, scoringModel, progressMonitor);

                // Get scores for target and decoy groups.
                List<IList<double[]>> targetTransitionGroups;
                List<IList<double[]>> decoyTransitionGroups;
                targetDecoyGenerator.GetTransitionGroups(out targetTransitionGroups, out decoyTransitionGroups);
                // If decoy box is checked and no decoys, throw an error
                if (decoys && decoyTransitionGroups.Count == 0)
                {
                    _out.WriteLine(Resources.CommandLine_CreateScoringModel_Error__There_are_no_decoy_peptides_in_the_document__Failed_to_create_scoring_model_);
                    return null;
                }
                // Use decoys for training only if decoy box is checked
                if (!decoys)
                    decoyTransitionGroups = new List<IList<double[]>>();

                // Set intial weights based on previous model (with NaN's reset to 0)
                var initialWeights = new double[scoringModel.PeakFeatureCalculators.Count];
                // But then set to NaN the weights that have unknown values for this dataset
                for (int i = 0; i < initialWeights.Length; ++i)
                {
                    if (!targetDecoyGenerator.EligibleScores[i])
                        initialWeights[i] = double.NaN;
                }
                var initialParams = new LinearModelParams(initialWeights);

                // Train the model.
                scoringModel = (MProphetPeakScoringModel)scoringModel.Train(targetTransitionGroups,
                    decoyTransitionGroups, initialParams, secondBest, true, progressMonitor);

                Settings.Default.PeakScoringModelList.SetValue(scoringModel);

                return new ModelAndFeatures(scoringModel, targetDecoyGenerator.PeakGroupFeatures);
            }
            catch (Exception x)
            {
                _out.WriteLine(Resources.CommandLine_CreateScoringModel_Error__Failed_to_create_scoring_model_);
                _out.WriteLine(x.Message);
                return null;
            }
        }

        private bool Reintegrate(ModelAndFeatures modelAndFeatures, bool isAnnotateScoring, bool isOverwritePeaks)
        {
            try
            {
                var resultsHandler = new MProphetResultsHandler(_doc, modelAndFeatures.ScoringModel, modelAndFeatures.Features)
                {
                    OverrideManual = isOverwritePeaks,
                    AddAnnotation = isAnnotateScoring,
                    AddMAnnotation = isAnnotateScoring
                };

                var progressMonitor = new CommandProgressMonitor(_out, new ProgressStatus(string.Empty));

                resultsHandler.ScoreFeatures(progressMonitor);
                if (resultsHandler.IsMissingScores())
                {
                    _out.WriteLine(Resources.CommandLine_Reintegrate_Error__The_current_peak_scoring_model_is_incompatible_with_one_or_more_peptides_in_the_document__Please_train_a_new_model_);
                    return false;
                }
                _doc = resultsHandler.ChangePeaks(progressMonitor);

                return true;
            }
            catch (Exception x)
            {
                _out.WriteLine(Resources.CommandLine_Reintegrate_Error__Failed_to_reintegrate_peaks_successfully_);
                _out.WriteLine(x.Message);
                return false;
            }
        }

        public void ImportFasta(string path, bool keepEmptyProteins)
        {
            _out.WriteLine(Resources.CommandLine_ImportFasta_Importing_FASTA_file__0____, Path.GetFileName(path));
            using (var readerFasta = new StreamReader(path))
            {
                var progressMonitor = new CommandProgressMonitor(_out, new ProgressStatus(string.Empty));
                IdentityPath selectPath;
                long lines = Helpers.CountLinesInFile(path);
                int emptiesIgnored;
                _doc = _doc.ImportFasta(readerFasta, progressMonitor, lines, false, null, out selectPath, out emptiesIgnored);
            }
            
            // Remove all empty proteins unless otherwise specified
            if (!keepEmptyProteins)
                _doc = new RefinementSettings { MinPeptidesPerProtein = 1 }.Refine(_doc);
        }

        public bool SetLibrary(string name, string path, bool append = true)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                _out.WriteLine(Resources.CommandLine_SetLibrary_Error__Cannot_set_library_name_without_path_);
                return false;
            }
            else if (!File.Exists(path))
            {
                _out.WriteLine(Resources.CommandLine_SetLibrary_Error__The_file__0__does_not_exist_, path);
                return false;
            }
            else if (path.EndsWith(BiblioSpecLiteSpec.EXT_REDUNDANT))
            {
                _out.WriteLine(Resources.CommandLine_SetLibrary_Error__The_file__0__appears_to_be_a_redundant_library_);
                return false;
            }

            if (string.IsNullOrWhiteSpace(name))
                name = Path.GetFileNameWithoutExtension(path);

            LibrarySpec librarySpec;

            string ext = Path.GetExtension(path);
            if (Equals(ext, BiblioSpecLiteSpec.EXT))
                librarySpec = new BiblioSpecLiteSpec(name, path);
            else if (Equals(ext, BiblioSpecLibSpec.EXT))
                librarySpec = new BiblioSpecLibSpec(name, path);
            else if (Equals(ext, XHunterLibSpec.EXT))
                librarySpec = new XHunterLibSpec(name, path);
            else if (Equals(ext, NistLibSpec.EXT))
                librarySpec = new NistLibSpec(name, path);
            else if (Equals(ext, SpectrastSpec.EXT))
                librarySpec = new SpectrastSpec(name, path);
            else
            {
                _out.WriteLine(Resources.CommandLine_SetLibrary_Error__The_file__0__is_not_a_supported_spectral_library_file_format_, path);
                return false;
            }

            // Check for conflicting names
            foreach (var docLibrarySpec in _doc.Settings.PeptideSettings.Libraries.LibrarySpecs)
            {
                if (docLibrarySpec.Name == librarySpec.Name || docLibrarySpec.FilePath == librarySpec.FilePath)
                {
                    _out.WriteLine(Resources.CommandLine_SetLibrary_Error__The_library_you_are_trying_to_add_conflicts_with_a_library_already_in_the_file_);
                    return false;
                }
            }

            var librarySpecs = append ?
                new List<LibrarySpec>(_doc.Settings.PeptideSettings.Libraries.LibrarySpecs) { librarySpec } :
                new List<LibrarySpec>{ librarySpec };

            SrmSettings newSettings = _doc.Settings.ChangePeptideLibraries(l => l.ChangeLibrarySpecs(librarySpecs));
            _doc = _doc.ChangeSettings(newSettings);

            return true;
        }

        
		// This is a hack for un-readable RAW files from Thermo instruments.
        // These files are usually 78KB.  Presumably they are
        // temporary files that, for some reason, do not get deleted.
        private bool CanReadFile(MsDataFileUri msDataFileUri)
        {
            MsDataFilePath msDataFilePath = msDataFileUri as MsDataFilePath;
            if (null == msDataFilePath)
            {
                return true;
            }
            string replicatePath = msDataFilePath.FilePath;
            if (!File.Exists(replicatePath) && !Directory.Exists(replicatePath))
            {
                _out.WriteLine(Resources.CommandLine_CanReadFile_Error__File_does_not_exist___0__,replicatePath);
                return false;
            }

            // Make sure this is a Thermo RAW file
            FileInfo fileInfo = new FileInfo(replicatePath);
            // We will not do this check for a directory source
            if(!fileInfo.Exists)
            {
                return true;
            }
            if(DataSourceUtil.GetSourceType(fileInfo) != DataSourceUtil.TYPE_THERMO_RAW)
            {
                return true;
            }

            // We will not do this chech for files over 100KB
            if(fileInfo.Length > (100 * 1024))
            {
                return true;
            }

            // Try to read the file
            try
            {
                using (new MsDataFileImpl(replicatePath))
                {
                }
            }
            catch(Exception e)
            {
                _out.WriteLine(e.Message);
                return false;
            }
            
            return true;
        }

        public void SaveFile(string saveFile)
        {
            _out.WriteLine(Resources.CommandLine_SaveFile_Saving_file___);
            try
            {
                SaveDocument(_doc, saveFile, _out);
            }
            catch
            {
                _out.WriteLine(Resources.CommandLine_SaveFile_Error__The_file_could_not_be_saved_to__0____Check_that_the_directory_exists_and_is_not_read_only_, saveFile);
                return;
            }
            _out.WriteLine(Resources.CommandLine_SaveFile_File__0__saved_, Path.GetFileName(saveFile));
        }

        public void ExportReport(string reportName, string reportFile, char reportColSeparator, bool reportInvariant)
        {

            if (String.IsNullOrEmpty(reportFile))
            {
                _out.WriteLine(Resources.CommandLine_ExportReport_);
                return;
            }

            ExportLiveReport(reportName, reportFile, reportColSeparator, reportInvariant);
        }

        private void ExportLiveReport(string reportName, string reportFile, char reportColSeparator, bool reportInvariant)
        {
            var viewContext = DocumentGridViewContext.CreateDocumentGridViewContext(_doc, reportInvariant
                ? DataSchemaLocalizer.INVARIANT
                : SkylineDataSchema.GetLocalizedSchemaLocalizer());
            var viewInfo = viewContext.GetViewInfo(PersistedViews.MainGroup.Id.ViewName(reportName));
            if (null == viewInfo)
            {
                _out.WriteLine(Resources.CommandLine_ExportLiveReport_Error__The_report__0__does_not_exist__If_it_has_spaces_in_its_name__use__double_quotes__around_the_entire_list_of_command_parameters_, reportName);
                return;
            }
            _out.WriteLine(Resources.CommandLine_ExportLiveReport_Exporting_report__0____, reportName);

            try
            {
                using (var saver = new FileSaver(reportFile))
                {
                    if (!saver.CanSave())
                    {
                        _out.WriteLine(Resources.CommandLine_ExportLiveReport_Error__The_report__0__could_not_be_saved_to__1__, reportName, reportFile);
                        _out.WriteLine(Resources.CommandLine_ExportLiveReport_Check_to_make_sure_it_is_not_read_only_);
                    }

                    var status = new ProgressStatus(string.Empty);
                    IProgressMonitor broker = new CommandProgressMonitor(_out, status);

                    using (var writer = new StreamWriter(saver.SafeName))
                    {
                        viewContext.Export(broker, ref status, viewInfo, writer,
                            new DsvWriter(reportInvariant ? CultureInfo.InvariantCulture : LocalizationHelper.CurrentCulture, reportColSeparator));
                    }

                    broker.UpdateProgress(status.Complete());
                    saver.Commit();
                    _out.WriteLine(Resources.CommandLine_ExportLiveReport_Report__0__exported_successfully_to__1__, reportName, reportFile);
                }
            }
            catch (Exception x)
            {
                _out.WriteLine(Resources.CommandLine_ExportLiveReport_Error__Failure_attempting_to_save__0__report_to__1__, reportName, reportFile);
                _out.WriteLine(x.Message);
            }
        }

        public enum ResolveZipToolConflicts
        {
            terminate,
            overwrite,
            in_parallel
        }

        public void ImportToolsFromZip(string path, ResolveZipToolConflicts? resolveConflicts, bool? overwriteAnnotations, ProgramPathContainer ppc, string programPath, bool arePackagesHandled)
        {
            if (string.IsNullOrEmpty(path))
            {
                _out.WriteLine(Resources.CommandLine_ImportToolsFromZip_Error__to_import_tools_from_a_zip_you_must_specify_a_path___tool_add_zip_must_be_followed_by_an_existing_path_);
                return;
            }
            if (!File.Exists(path))
            {
                _out.WriteLine(Resources.CommandLine_ImportToolsFromZip_Error__the_file_specified_with_the___tool_add_zip_command_does_not_exist__Please_verify_the_file_location_and_try_again_);
                return;
            }
            if (Path.GetExtension(path) != ".zip") // Not L10N
            {
                _out.WriteLine(Resources.CommandLine_ImportToolsFromZip_Error__the_file_specified_with_the___tool_add_zip_command_is_not_a__zip_file__Please_specify_a_valid__zip_file_);
                return;
            }
            string filename = Path.GetFileName(path);
            _out.WriteLine(Resources.CommandLine_ImportToolsFromZip_Installing_tools_from__0_, filename);
            ToolInstaller.UnzipToolReturnAccumulator result = null;
            try
            {
                result = ToolInstaller.UnpackZipTool(path, new AddZipToolHelper(resolveConflicts, overwriteAnnotations, _out, filename, ppc,
                                                                               programPath, arePackagesHandled));
            }
            catch (ToolExecutionException x)
            {
                _out.WriteLine(x.Message);
            }
            if (result != null)
            {
                foreach (var message in result.MessagesThrown)
                {
                    _out.WriteLine(message);
                }
                foreach (var tool in result.ValidToolsFound)
                {
                    _out.WriteLine(Resources.CommandLine_ImportToolsFromZip_Installed_tool__0_, tool.Title);
                }
                Settings.Default.Save();
            }
            else
            {
                _out.WriteLine(Resources.CommandLine_ImportToolsFromZip_Canceled_installing_tools_from__0__, filename);
            }
        }

        // A function for adding tools to the Tools Menu.
        public void ImportTool (string title, string command, string arguments, string initialDirectory, string reportTitle, bool outputToImmediateWindow, bool? resolveToolConflictsBySkipping)
        {
            if (title == null | command == null)
            {
                _out.WriteLine(Resources.CommandLine_ImportTool_Error__to_import_a_tool_it_must_have_a_name_and_a_command___Use___tool_add_to_specify_a_name_and_use___tool_command_to_specify_a_command___The_tool_was_not_imported___);
                return;
            }
            // Check if the command is of a supported type and not a URL
            else if (!ConfigureToolsDlg.CheckExtension(command) && !ToolDescription.IsWebPageCommand(command))
            {
                string supportedTypes = String.Join("; ", ConfigureToolsDlg.EXTENSIONS); // Not L10N
                supportedTypes = supportedTypes.Replace(".", "*."); // Not L10N
                _out.WriteLine(Resources.CommandLine_ImportTool_Error__the_provided_command_for_the_tool__0__is_not_of_a_supported_type___Supported_Types_are___1_, title, supportedTypes);
                _out.WriteLine(Resources.CommandLine_ImportTool_The_tool_was_not_imported___);
                return;
            }
            if (arguments != null && arguments.Contains(ToolMacros.INPUT_REPORT_TEMP_PATH))
            {
                if (string.IsNullOrEmpty(reportTitle))
                {
                    _out.WriteLine(Resources.CommandLine_ImportTool_Error__If__0__is_and_argument_the_tool_must_have_a_Report_Title__Use_the___tool_report_parameter_to_specify_a_report_, ToolMacros.INPUT_REPORT_TEMP_PATH);
                    _out.WriteLine(Resources.CommandLine_ImportTool_The_tool_was_not_imported___);
                    return;
                }

                if (!ReportSharing.GetExistingReports().ContainsKey(PersistedViews.ExternalToolsGroup.Id.ViewName(reportTitle))) 
                {
                    _out.WriteLine(Resources.CommandLine_ImportTool_Error__Please_import_the_report_format_for__0____Use_the___report_add_parameter_to_add_the_missing_custom_report_, reportTitle);
                    _out.WriteLine(Resources.CommandLine_ImportTool_The_tool_was_not_imported___);
                    return;                    
                }
            }            

            // Check for a name conflict. 
            ToolDescription toolToRemove = null;
            foreach (var tool  in Settings.Default.ToolList)
            {                
                if (tool.Title == title)
                {
                    // Conflict. 
                    if (resolveToolConflictsBySkipping == null)
                    {
                        // Complain. No resolution specified.
                        _out.WriteLine(Resources.CommandLine_ImportTool_, tool.Title);
                        return; // Dont add.
                    }
                    // Skip conflicts
                    if (resolveToolConflictsBySkipping == true)
                    {
                        _out.WriteLine(Resources.CommandLine_ImportTool_Warning__skipping_tool__0__due_to_a_name_conflict_, tool.Title);
//                        _out.WriteLine("         tool {0} was not modified.", tool.Title);
                        return;
                    }
                    // Ovewrite conflicts
                    if (resolveToolConflictsBySkipping == false)
                    {
                        _out.WriteLine(Resources.CommandLine_ImportTool_Warning__the_tool__0__was_overwritten, tool.Title);
//                      _out.WriteLine("         tool {0} was modified.", tool.Title);
                        if (toolToRemove == null) // If there are multiple tools with the same name this makes sure the first one with a naming conflict is overwritten.
                            toolToRemove = tool;
                    }
                }
            }
            // Remove the tool to be overwritten.
            if (toolToRemove !=null)
                Settings.Default.ToolList.Remove(toolToRemove);          
            // If no tool was overwritten then its a new tool. Show this message. 
            if (toolToRemove == null)
            {
                _out.WriteLine(Resources.CommandLine_ImportTool__0__was_added_to_the_Tools_Menu_, title);
            }
            // Conflicts have been dealt with now add the tool.                       
            // Adding the tool. ToolArguments and ToolInitialDirectory are optional. 
            // If arguments or initialDirectory is null set it to be an empty string.
            arguments = arguments ?? string.Empty; 
            initialDirectory = initialDirectory ?? string.Empty; 
            Settings.Default.ToolList.Add(new ToolDescription(title, command, arguments, initialDirectory, outputToImmediateWindow, reportTitle));
            Settings.Default.Save();        
        }

        // A function for running each line of a text file like a SkylineRunner command
        public void RunBatchCommands(string path)
        {
            if (!File.Exists(path))
            {
                _out.WriteLine(Resources.CommandLine_RunBatchCommands_Error___0__does_not_exist____batch_commands_failed_, path);
            }
            else
            {
                try
                {
                    using (StreamReader sr = File.OpenText(path))
                    {
                        string input;
                        // Run each line like its own command.
                        while ((input = sr.ReadLine()) != null)
                        {
                            // Parse the line and run it.
                            string[] args = ParseArgs(input);
                            Run(args);
                        }
                    }
                }
                catch (Exception)
                {
                    _out.WriteLine(Resources.CommandLine_RunBatchCommands_Error__failed_to_open_file__0____batch_commands_command_failed_, path);
                }
            }            
        }
        
        /// <summary>
        ///  A method for parsing command line inputs to accept quotes arround strings and double quotes within those strings.
        ///  See CommandLineTest.cs ConsoleParserTest() for specific examples of its behavior. 
        /// </summary>
        /// <param name="inputs"> string on inputs </param>
        /// <returns> string[] of parsed commands </returns>
        public static string[] ParseArgs(string inputs)
        {            
            List<string> output = new List<string>();
            bool foundSingle = false;
            string current = null; 
            // Loop char by char through inputs building an ouput 
            for (int i = 0; i < inputs.Length; i++)
            {
                char c = inputs[i];
                if (c == '"')
                {
                    // If you have not yet encountered a quote, its an open quote
                    if (!foundSingle)
                        foundSingle = true;
                    // If you have already encountered a quote, it could be a close quote or escaped quote
                    else
                    {
                        // In this case its an escaped quote
                        if ((i < inputs.Length - 1) && inputs[i + 1] == '"')
                        {
                            current += c;
                            i++;
                        }
                        // Its a close quote
                        else
                            foundSingle = false;
                    }                   
                }
                // If not within a quote and the current string being built isn't blank, a space is a place to break.
                else if (c == ' ' && !foundSingle && (!String.IsNullOrEmpty(current)))
                {
                    output.Add(current);
                    current = null;
                }   
                else if (c != ' ' || (c == ' ' && foundSingle))
                    current += c;

                // Catch the corner case at the end of the string, make sure the last chunk is added to output array.
                if (i == inputs.Length - 1 && (!String.IsNullOrEmpty(current)))
                {
                    output.Add(current);
                }
            }                
            return output.ToArray();
        }

        /// <summary>
        /// A method for joining an array of individual arguments to be passed to the command line to generate
        /// the string that will ultimately be passed. If a argument has white space, it is surrounded by quotes.
        /// If an empty (size 0) array is given, it returns string.Empty 
        /// </summary>
        /// <param name="arguments">The arguments to join</param>
        /// <returns>The appropriately formatted command line argument string</returns>
        public static string JoinArgs(string[] arguments)
        {
            if (!arguments.Any())
            {
                return string.Empty;
            }
            
            StringBuilder commandLineArguments = new StringBuilder();
            foreach (string argument in arguments)
            {
                if (argument == null)
                {
                    commandLineArguments.Append(" \"\""); // Not L10N
                }
                else if (argument.Contains(" ") || argument.Contains("\t") || argument.Equals(string.Empty)) // Not L10N
                {
                    commandLineArguments.Append(" \"" + argument + "\""); // Not L10N
                }
                else
                {
                    commandLineArguments.Append(TextUtil.SEPARATOR_SPACE + argument);
                }
            }
            commandLineArguments.Remove(0, 1);
            return commandLineArguments.ToString();
        }

        public void ImportSkyr(string path, bool? resolveSkyrConflictsBySkipping)
        {          
            if (!File.Exists(path))
            {
                _out.WriteLine(Resources.CommandLine_ImportSkyr_Error___0__does_not_exist____report_add_command_failed_, path);
            }
            else
            {           
                ImportSkyrHelper helper = new ImportSkyrHelper(_out, resolveSkyrConflictsBySkipping);
                bool imported;
                try
                {
                    imported = ReportSharing.ImportSkyrFile(path, helper.ResolveImportConflicts);
                }
                catch (Exception e)
                {
                    _out.WriteLine(Resources.CommandLine_ImportSkyr_, path, e);
                    return;
                }
                if (imported)
                {
                    Settings.Default.Save();
                    _out.WriteLine(Resources.CommandLine_ImportSkyr_Success__Imported_Reports_from__0_, Path.GetFileNameWithoutExtension(path));
                }
            }
        }

        private class ImportSkyrHelper
        {
            private bool? resolveSkyrConflictsBySkipping { get; set; }
            private readonly TextWriter _outWriter;

            public ImportSkyrHelper(TextWriter outWriter, bool? resolveSkyrConflictsBySkipping)
            {
                _outWriter = outWriter;
                this.resolveSkyrConflictsBySkipping = resolveSkyrConflictsBySkipping;
            }

            internal IList<string> ResolveImportConflicts(IList<string> existing)
            {
                string messageFormat = existing.Count == 1
                                           ? Resources.ImportSkyrHelper_ResolveImportConflicts_The_name___0___already_exists_
                                           : Resources.ImportSkyrHelper_ResolveImportConflicts_;
                _outWriter.WriteLine(messageFormat, string.Join("\n", existing.ToArray())); // Not L10N
                if (resolveSkyrConflictsBySkipping == null)
                {
                    _outWriter.WriteLine(Resources.ImportSkyrHelper_ResolveImportConflicts_Use_command);
                    return null;
                }
                if (resolveSkyrConflictsBySkipping == true)
                {
                    _outWriter.WriteLine(Resources.ImportSkyrHelper_ResolveImportConflicts_Resolving_conflicts_by_skipping_);
                    // The objects are skipped below for being in the list called existing
                }
                if (resolveSkyrConflictsBySkipping == false)
                {
                    _outWriter.WriteLine(Resources.ImportSkyrHelper_ResolveImportConflicts_Resolving_conflicts_by_overwriting_);
                    existing.Clear();
                    // All conflicts are overwritten because existing is empty. 
                }
                return existing;
            }
        }

        // This function needs so many variables, we might as well just pass the whole CommandArgs object
        private void ExportInstrumentFile(ExportFileType type, CommandArgs args)
        {
            if (string.IsNullOrEmpty(args.ExportPath))
            {
                _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_);
                return;
            }

            if (Equals(type, ExportFileType.Method))
            {
                if (string.IsNullOrEmpty(args.TemplateFile))
                {
                    _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__A_template_file_is_required_to_export_a_method_);
                    return;
                }
                if (Equals(args.MethodInstrumentType, ExportInstrumentType.AGILENT6400)
                        ? !Directory.Exists(args.TemplateFile)
                        : !File.Exists(args.TemplateFile))
                {
                    _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__The_template_file__0__does_not_exist_, args.TemplateFile);
                    return;
                }
                if (Equals(args.MethodInstrumentType, ExportInstrumentType.AGILENT6400) &&
                    !AgilentMethodExporter.IsAgilentMethodPath(args.TemplateFile))
                {
                    _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__The_folder__0__does_not_appear_to_contain_an_Agilent_QQQ_method_template___The_folder_is_expected_to_have_a__m_extension__and_contain_the_file_qqqacqmethod_xsd_, args.TemplateFile);
                    return;
                }
            }

            if (!args.ExportStrategySet)
            {
                _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Warning__No_export_strategy_specified__from__single____protein__or__buckets____Defaulting_to__single__);
                args.ExportStrategy = ExportStrategy.Single;
            }

            if (args.AddEnergyRamp && !Equals(args.TransListInstrumentType, ExportInstrumentType.THERMO))
            {
                _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Warning__The_add_energy_ramp_parameter_is_only_applicable_for_Thermo_transition_lists__This_parameter_will_be_ignored_);
            }

            string instrument = Equals(type, ExportFileType.List)
                                    ? args.TransListInstrumentType
                                    : args.MethodInstrumentType;
            if (!CheckInstrument(instrument, _doc))
            {
                _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Warning__The_vendor__0__does_not_match_the_vendor_in_either_the_CE_or_DP_prediction_setting___Continuing_exporting_a_transition_list_anyway___, instrument);
            }


            int maxInstrumentTrans = _doc.Settings.TransitionSettings.Instrument.MaxTransitions ??
                                     TransitionInstrument.MAX_TRANSITION_MAX;

            if ((args.MaxTransitionsPerInjection < AbstractMassListExporter.MAX_TRANS_PER_INJ_MIN ||
                 args.MaxTransitionsPerInjection > maxInstrumentTrans) &&
                (Equals(args.ExportStrategy, ExportStrategy.Buckets) ||
                 Equals(args.ExportStrategy, ExportStrategy.Protein)))
            {
                _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Warning__Max_transitions_per_injection_must_be_set_to_some_value_between__0__and__1__for_export_strategies__protein__and__buckets__and_for_scheduled_methods__You_specified__3___Defaulting_to__2__, AbstractMassListExporter.MAX_TRANS_PER_INJ_MIN, maxInstrumentTrans,AbstractMassListExporter.MAX_TRANS_PER_INJ_DEFAULT, args.MaxTransitionsPerInjection);

                args.MaxTransitionsPerInjection = AbstractMassListExporter.MAX_TRANS_PER_INJ_DEFAULT;
            }

            /*
             * Consider: for transition lists, AB Sciex and Agilent require the 
             * dwell time parameter, and Waters requires the run length parameter.
             * These are guaranteed to be set and within-bounds at this point, but
             * not necessarily by the user because there is a default.
             * 
             * Should we warn the user that they didn't set these parameters?
             * Should we warn the user if they set parameters that will not be used
             * with the given instrument?
             * 
             * This would require a pretty big matrix of conditionals, and there is
             * documentation after all...
             */

            if(Equals(type, ExportFileType.Method))
            {
                string extension = Path.GetExtension(args.TemplateFile);
                if(!Equals(ExportInstrumentType.MethodExtension(args.MethodInstrumentType),extension))
                {
                    _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__The_template_extension__0__does_not_match_the_expected_extension_for_the_instrument__1___No_method_will_be_exported_, extension,args.MethodInstrumentType);
                    return;
                }
            }

            var prediction = _doc.Settings.TransitionSettings.Prediction;
            double optimizeStepSize = 0;
            int optimizeStepCount = 0;

            if (Equals(args.ExportOptimizeType, ExportOptimize.CE))
            {
                var regression = prediction.CollisionEnergy;
                optimizeStepSize = regression.StepSize;
                optimizeStepCount = regression.StepCount;
            }
            else if (Equals(args.ExportOptimizeType, ExportOptimize.DP))
            {
                var regression = prediction.DeclusteringPotential;
                optimizeStepSize = regression.StepSize;
                optimizeStepCount = regression.StepCount;
            }

            //Now is a good time to make this conversion
            _exportProperties = args.ExportCommandProperties;
            _exportProperties.OptimizeStepSize = optimizeStepSize;
            _exportProperties.OptimizeStepCount = optimizeStepCount;

            _exportProperties.FullScans = _doc.Settings.TransitionSettings.FullScan.IsEnabledMsMs;

            _exportProperties.Ms1Scan = _doc.Settings.TransitionSettings.FullScan.IsEnabledMs &&
                            _doc.Settings.TransitionSettings.FullScan.IsEnabledMsMs;

            _exportProperties.InclusionList = _doc.Settings.TransitionSettings.FullScan.IsEnabledMs &&
                                              !_doc.Settings.TransitionSettings.FullScan.IsEnabledMsMs;

            _exportProperties.MsAnalyzer =
                TransitionFullScan.MassAnalyzerToString(
                    _doc.Settings.TransitionSettings.FullScan.PrecursorMassAnalyzer);
            _exportProperties.MsMsAnalyzer =
                TransitionFullScan.MassAnalyzerToString(
                    _doc.Settings.TransitionSettings.FullScan.ProductMassAnalyzer);


            _exportProperties.MsAnalyzer =
                TransitionFullScan.MassAnalyzerToString(
                    _doc.Settings.TransitionSettings.FullScan.PrecursorMassAnalyzer);
            _exportProperties.MsMsAnalyzer =
                TransitionFullScan.MassAnalyzerToString(
                    _doc.Settings.TransitionSettings.FullScan.ProductMassAnalyzer);

            if(!Equals(args.ExportMethodType, ExportMethodType.Standard))
            {
                if (Equals(args.ExportMethodType, ExportMethodType.Triggered))
                {
                    bool canTrigger = true;
                    if (!ExportInstrumentType.CanTriggerInstrumentType(instrument))
                    {
                        canTrigger = false;
                        if (Equals(args.MethodInstrumentType, ExportInstrumentType.THERMO_TSQ))
                        {
                            _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__the__0__instrument_lacks_support_for_direct_method_export_for_triggered_acquisition_, instrument);
                            _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_You_must_export_a__0__transition_list_and_manually_import_it_into_a_method_file_using_vendor_software_, ExportInstrumentType.THERMO);
                        }
                        else
                        {
                            _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__the_instrument_type__0__does_not_support_triggered_acquisition_, instrument);
                        }
                    }
                    else if (!_doc.Settings.HasResults && !_doc.Settings.HasLibraries)
                    {
                        canTrigger = false;
                        _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__triggered_acquistion_requires_a_spectral_library_or_imported_results_in_order_to_rank_transitions_);
                    }
                    else if (!ExportInstrumentType.CanTrigger(instrument, _doc, _exportProperties.SchedulingReplicateNum))
                    {
                        canTrigger = false;
                        _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__The_current_document_contains_peptides_without_enough_information_to_rank_transitions_for_triggered_acquisition_);
                    }
                    if (!canTrigger)
                    {
                        _out.WriteLine(Equals(type, ExportFileType.List)
                                               ? Resources.CommandLine_ExportInstrumentFile_No_list_will_be_exported_
                                               : Resources.CommandLine_ExportInstrumentFile_No_method_will_be_exported_);
                        return;
                    }
                    _exportProperties.PrimaryTransitionCount = args.PrimaryTransitionCount;
                }

                if (!ExportInstrumentType.CanSchedule(instrument, _doc))
                {
                    var predictionPep = _doc.Settings.PeptideSettings.Prediction;
                    if (!ExportInstrumentType.CanScheduleInstrumentType(instrument, _doc))
                    {
                        _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__the_specified_instrument__0__is_not_compatible_with_scheduled_methods_,
                                       instrument);
                    }
                    else if (predictionPep.RetentionTime == null)
                    {
                        if (predictionPep.UseMeasuredRTs)
                        {
                            _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__to_export_a_scheduled_method__you_must_first_choose_a_retention_time_predictor_in_Peptide_Settings___Prediction__or_import_results_for_all_peptides_in_the_document_);
                        }
                        else
                        {
                            _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__to_export_a_scheduled_method__you_must_first_choose_a_retention_time_predictor_in_Peptide_Settings___Prediction_);
                        }
                    }
                    else if (!predictionPep.RetentionTime.Calculator.IsUsable)
                    {
                        _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__the_retention_time_prediction_calculator_is_unable_to_score___Check_the_calculator_settings_);
                    }
                    else if (!predictionPep.RetentionTime.IsUsable)
                    {
                        _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__the_retention_time_predictor_is_unable_to_auto_calculate_a_regression___Check_to_make_sure_the_document_contains_times_for_all_of_the_required_standard_peptides_);
                    }
                    else
                    {
                        _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__To_export_a_scheduled_method__you_must_first_import_results_for_all_peptides_in_the_document_);
                    }
                    _out.WriteLine(Equals(type, ExportFileType.List)
                                           ? Resources.CommandLine_ExportInstrumentFile_No_list_will_be_exported_
                                           : Resources.CommandLine_ExportInstrumentFile_No_method_will_be_exported_);
                    return;
                }

                if (Equals(args.ExportSchedulingAlgorithm, ExportSchedulingAlgorithm.Average))
                {
                    _exportProperties.SchedulingReplicateNum = null;
                }
                else
                {
                    if(args.SchedulingReplicate.Equals("LAST")) // Not L10N
                    {
                        _exportProperties.SchedulingReplicateNum = _doc.Settings.MeasuredResults.Chromatograms.Count - 1;
                    }
                    else
                    {
                        //check whether the given replicate exists
                        if (!_doc.Settings.MeasuredResults.ContainsChromatogram(args.SchedulingReplicate))
                        {
                            _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__the_specified_replicate__0__does_not_exist_in_the_document_,
                                           args.SchedulingReplicate);
                            _out.WriteLine(Equals(type, ExportFileType.List)
                                                   ? Resources.CommandLine_ExportInstrumentFile_No_list_will_be_exported_
                                                   : Resources.CommandLine_ExportInstrumentFile_No_method_will_be_exported_);
                            return;
                        }

                        _exportProperties.SchedulingReplicateNum =
                            _doc.Settings.MeasuredResults.Chromatograms.IndexOf(
                                rep => rep.Name.Equals(args.SchedulingReplicate));
                    }
                }
            }

            try
            {
                _exportProperties.ExportFile(instrument, type, args.ExportPath, _doc, args.TemplateFile);
            }
            catch (IOException x)
            {
                _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__The_file__0__could_not_be_saved___Check_that_the_specified_file_directory_exists_and_is_writeable_, args.ExportPath);
                _out.WriteLine(x.Message);
                return;
            }

            _out.WriteLine(Equals(type, ExportFileType.List)
                               ? Resources.CommandLine_ExportInstrumentFile_List__0__exported_successfully_
                               : Resources.CommandLine_ExportInstrumentFile_Method__0__exported_successfully_,
                           Path.GetFileName(args.ExportPath));
        }

        public static void SaveDocument(SrmDocument doc, string outFile, TextWriter outText)
        {
            // Make sure the containing directory is created
            string dirPath = Path.GetDirectoryName(outFile);
            if (dirPath != null)
                Directory.CreateDirectory(dirPath);

            var progressMonitor = new CommandProgressMonitor(outText, new ProgressStatus(string.Empty));
            using (var writer = new XmlWriterWithProgress(outFile, Encoding.UTF8, doc.MoleculeTransitionCount, progressMonitor))
            {
                XmlSerializer ser = new XmlSerializer(typeof (SrmDocument));
                ser.Serialize(writer, doc);

                writer.Flush();
                writer.Close();

                var settings = doc.Settings;
                if (settings.HasResults)
                {
                    if (settings.MeasuredResults.IsLoaded)
                    {
                        FileStreamManager fsm = FileStreamManager.Default;
                        settings.MeasuredResults.OptimizeCache(outFile, fsm);

                        //don't worry about updating the document with the results of optimization
                        //as is done in SkylineFiles
                    }
                }
                else
                {
                    string cachePath = ChromatogramCache.FinalPathForName(outFile, null);
                    FileEx.SafeDelete(cachePath, true);
                }
            }
        }


        /// <summary>
        /// This function will add the given replicate, from dataFile, to the given document. If the replicate
        /// does not exist, it will be added. If it does exist, it will be appended to.
        /// </summary>
        public static SrmDocument ImportResults(SrmDocument doc, string docPath, string replicate, MsDataFileUri dataFile,
                                                OptimizableRegression optimize, IProgressMonitor progressMonitor, out ProgressStatus status)
        {
            var docContainer = new ResultsMemoryDocumentContainer(null, docPath) {ProgressMonitor = progressMonitor};

            // Make sure library loading happens, which may not happen, if the doc
            // parameter is used as the baseline document.
            docContainer.SetDocument(doc, null);

            SrmDocument docAdded;
            do
            {
                doc = docContainer.Document;

                var listChromatograms = new List<ChromatogramSet>();

                if (doc.Settings.HasResults)
                    listChromatograms.AddRange(doc.Settings.MeasuredResults.Chromatograms);

                int indexChrom = listChromatograms.IndexOf(chrom => chrom.Name.Equals(replicate));
                if (indexChrom != -1)
                {
                    var chromatogram = listChromatograms[indexChrom];
                    var paths = chromatogram.MSDataFilePaths;
                    var listFilePaths = paths.ToList();
                    listFilePaths.Add(dataFile);
                    listChromatograms[indexChrom] = chromatogram.ChangeMSDataFilePaths(listFilePaths);
                }
                else
                {
                    listChromatograms.Add(new ChromatogramSet(replicate, new[] { dataFile.Normalize()}, Annotations.EMPTY, optimize));
                }

                var results = doc.Settings.HasResults
                                  ? doc.Settings.MeasuredResults.ChangeChromatograms(listChromatograms)
                                  : new MeasuredResults(listChromatograms, doc.Settings.IsResultsJoiningDisabled);

                docAdded = doc.ChangeMeasuredResults(results);
            }
            while (!docContainer.SetDocument(docAdded, doc, true));

            status = docContainer.LastProgress;

            return docContainer.Document;
        }

        /// <summary>
        /// This method returns true/false whether or not there is any discrepancy
        /// between the specified instrument and the instrument in the document settings.
        /// </summary>
        /// <param name="instrument">specified instrument</param>
        /// <param name="doc">document to check against</param>
        /// <returns></returns>
        public static bool CheckInstrument(string instrument, SrmDocument doc)
        {
            // Thermo LTQ method building ignores CE and DP regression values
            if (!Equals(instrument, ExportInstrumentType.THERMO_LTQ))
            {
                // Check to make sure CE and DP match chosen instrument, and offer to use
                // the correct version for the instrument, if not.
                var predict = doc.Settings.TransitionSettings.Prediction;
                var ce = predict.CollisionEnergy;
                string ceName = (ce != null ? ce.Name : null);
                string ceNameDefault = instrument;
                if (ceNameDefault.IndexOf(' ') != -1)
                    ceNameDefault = ceNameDefault.Substring(0, ceNameDefault.IndexOf(' '));
                bool ceInSynch = ceName != null && ceName.StartsWith(ceNameDefault);

                var dp = predict.DeclusteringPotential;
                string dpName = (dp != null ? dp.Name : null);
                string dpNameDefault = instrument;
                if (dpNameDefault.IndexOf(' ') != -1)
                    dpNameDefault = dpNameDefault.Substring(0, dpNameDefault.IndexOf(' '));
                bool dpInSynch = true;
                if (instrument == ExportInstrumentType.ABI)
                    dpInSynch = dpName != null && dpName.StartsWith(dpNameDefault);
                //else
                    //dpNameDefault = null; // Ignored for all other types

                return (ceInSynch && dpInSynch);
            }

            return true;
        }

        private static bool ShareDocument(SrmDocument document, string documentPath, string fileDest, CommandStatusWriter statusWriter)
        {
            var waitBroker = new CommandProgressMonitor(statusWriter,
                new ProgressStatus(Resources.SkylineWindow_ShareDocument_Compressing_Files));
            var sharing = new SrmDocumentSharing(document, documentPath, fileDest, false);
            try
            {
                sharing.Share(waitBroker);
                return true;
            }
            catch (Exception x)
            {
                statusWriter.WriteLine(Resources.SkylineWindow_ShareDocument_Failed_attempting_to_create_sharing_file__0__, fileDest);
                statusWriter.WriteLine(x.Message);
            }
            return false;
        }

        public void Dispose()
        {
            _out.Close();
        }

        private class PanoramaPublishHelper
        {
            private readonly CommandStatusWriter _statusWriter;

            public PanoramaPublishHelper(CommandStatusWriter statusWriter)
            {
                _statusWriter = statusWriter;
            }

            public void PublishToPanorama(Server panoramaServer, SrmDocument document, string documentPath, string panoramaFolder)
            {
                var zipFilePath = FileEx.GetTimeStampedFileName(documentPath);
                if (ShareDocument(document, documentPath, zipFilePath, _statusWriter))
                {
                    PublishDocToPanorama(panoramaServer, zipFilePath, panoramaFolder);
                }
                // Delete the zip file after it has been published to Panorama.
                FileEx.SafeDelete(zipFilePath, true);
            }

            private void PublishDocToPanorama(Server panoramaServer, string zipFilePath, string panoramaFolder)
            {
                var waitBroker = new CommandProgressMonitor(_statusWriter,
                    new ProgressStatus(Resources.PanoramaPublishHelper_PublishDocToPanorama_Publishing_document_to_Panorama));
                IPanoramaPublishClient publishClient = new WebPanoramaPublishClient();
                try
                {
                    publishClient.SendZipFile(panoramaServer, panoramaFolder, zipFilePath, waitBroker);
                }
                catch (Exception x)
                {
                    var panoramaEx = x.InnerException as PanoramaImportErrorException;
                    if (panoramaEx == null)
                    {
                        _statusWriter.WriteLine(Resources.PanoramaPublishHelper_PublishDocToPanorama_, x.Message);
                    }
                    else
                    {
                        _statusWriter.WriteLine(
							Resources.PanoramaPublishHelper_PublishDocToPanorama_An_error_occurred_on_the_Panorama_server___0___importing_the_file_, 
                            panoramaEx.ServerUrl);
                        _statusWriter.WriteLine(
                            Resources.PanoramaPublishHelper_PublishDocToPanorama_Error_details_can_be_found_at__0_,
                            new Uri(panoramaEx.ServerUrl, panoramaEx.JobUrlPart));
                    }
                }
            }
        }

    }

    public class CommandStatusWriter : TextWriter
    {
        private TextWriter _writer;

        public CommandStatusWriter(TextWriter writer)
            : base(writer.FormatProvider)
        {
            _writer = writer;
        }

        public bool IsTimeStamped { get; set; }

        public override Encoding Encoding
        {
            get { return _writer.Encoding; }
        }

        protected override void Dispose(bool disposing)
        {
            if (_writer != null)
            {
                _writer.Dispose();
                _writer = null;
            }
        }

        public override void Flush()
        {
            _writer.Flush();
        }

        public override void Write(char value)
        {
            _writer.Write(value);
        }

        public override void WriteLine()
        {
            WriteLine(string.Empty);
        }

        public override void WriteLine(string value)
        {
            if (IsTimeStamped)
                value = DateTime.Now.ToString("[yyyy/MM/dd HH:mm:ss]\t") + value; // Not L10N
            _writer.WriteLine(value);
            Flush();
        }
    }

    public class ExportCommandProperties : ExportProperties
    {
        private readonly TextWriter _out;

        public ExportCommandProperties(TextWriter output)
        {
            _out = output;
        }

        public override void PerformLongExport(Action<IProgressMonitor> performExport)
        {
            var waitBroker = new CommandProgressMonitor(_out, new ProgressStatus(string.Empty));
            performExport(waitBroker);
        }
    }

    internal class AddZipToolHelper : IUnpackZipToolSupport
    {
        private string zipFileName { get; set; }
        private CommandStatusWriter _out { get; set; }
        private CommandLine.ResolveZipToolConflicts? resolveToolsAndReports { get; set; }
        private bool? overwriteAnnotations { get; set; }
        private ProgramPathContainer programPathContainer { get; set; }
        private string programPath { get; set; }
        private bool packagesHandled { get; set; }

        public AddZipToolHelper(CommandLine.ResolveZipToolConflicts? howToResolve, bool? howToResolveAnnotations,
                                CommandStatusWriter output,
                                string fileName, ProgramPathContainer ppc, string inputProgramPath,
                                bool arePackagesHandled)
        {
            resolveToolsAndReports = howToResolve;
            overwriteAnnotations = howToResolveAnnotations;
            _out = output;
            zipFileName = fileName;
            programPathContainer = ppc;
            programPath = inputProgramPath;
            packagesHandled = arePackagesHandled;
        }

        public bool? ShouldOverwrite(string toolCollectionName, string toolCollectionVersion, List<ReportOrViewSpec> reports,
                                     string foundVersion,
                                     string newCollectionName)
        {
            if (resolveToolsAndReports == CommandLine.ResolveZipToolConflicts.in_parallel)
                return false;
            if (resolveToolsAndReports == CommandLine.ResolveZipToolConflicts.overwrite)
            {
                string singularToolMessage = Resources.AddZipToolHelper_ShouldOverwrite_Overwriting_tool___0_;
                string singularReportMessage = Resources.AddZipToolHelper_ShouldOverwrite_Overwriting_report___0_;
                string plualReportMessage = Resources.AddZipToolHelper_ShouldOverwrite_Overwriting_reports___0_;
                if (reports.Count == 1)
                {
                    _out.WriteLine(singularReportMessage, reports[0].GetKey());
                }
                else if (reports.Count > 1)
                {
                    List<string> reportTitles = reports.Select(sp => sp.GetKey()).ToList();
                    string reportTitlesJoined = string.Join(", ", reportTitles); // Not L10N
                    _out.WriteLine(plualReportMessage, reportTitlesJoined);
                }
                if (toolCollectionName != null)
                {
                    _out.WriteLine(singularToolMessage, toolCollectionName);
                }           
                return true;
            }
                
            else //Conflicts and no way to handle them. Display Message.
            {
                string firstpart = string.Empty;
                string secondpart = string.Empty;
                if (reports.Count == 0)
                {
                    if (toolCollectionName != null)
                    {
                        secondpart = Resources.AddZipToolHelper_ShouldOverwrite_Error__There_is_a_conflicting_tool;   
                    }
                }
                else
                {
                    if (reports.Count == 1)
                    {
                        firstpart = Resources.AddZipToolHelper_ShouldOverwrite_Error__There_is_a_conflicting_report;
                    }
                    if (reports.Count > 1)
                    {
                        firstpart = string.Format(Resources.AddZipToolHelper_ShouldOverwrite_Error__There_are__0__conflicting_reports, reports.Count);
                    }
                    if (toolCollectionName != null)
                    {                     
                        secondpart = Resources.AddZipToolHelper_ShouldOverwrite__and_a_conflicting_tool;
                    }
                }
                string message = string.Format(string.Concat(firstpart, secondpart, Resources.AddZipToolHelper_ShouldOverwrite__in_the_file__0_), zipFileName); 

                _out.WriteLine(message);
                _out.WriteLine(Resources.AddZipToolHelper_ShouldOverwrite_Please_specify__overwrite__or__parallel__with_the___tool_zip_conflict_resolution_command_);
                
                string singularToolMessage = Resources.AddZipToolHelper_ShouldOverwrite_Conflicting_tool___0_;
                string singularReportMessage = Resources.AddZipToolHelper_ShouldOverwrite_Conflicting_report___0_;
                string plualReportMessage = Resources.AddZipToolHelper_ShouldOverwrite_Conflicting_reports___0_;
                if (reports.Count == 1)
                {
                    _out.WriteLine(singularReportMessage, reports[0].GetKey());
                }
                else if (reports.Count > 1)
                {
                    List<string> reportTitles = reports.Select(sp => sp.GetKey()).ToList();
                    string reportTitlesJoined = string.Join(", ", reportTitles); // Not L10N
                    _out.WriteLine(plualReportMessage, reportTitlesJoined);
                }
                if (toolCollectionName != null)
                { 
                    _out.WriteLine(singularToolMessage, toolCollectionName);
                }
                return null;
            }
        }

        public string InstallProgram(ProgramPathContainer missingProgramPathContainer, ICollection<ToolPackage> packages, string pathToInstallScript)
        {
            if (packages.Count > 0 && !packagesHandled)
            {
                _out.WriteLine(Resources.AddZipToolHelper_InstallProgram_Error__Package_installation_not_handled_in_SkylineRunner___If_you_have_already_handled_package_installation_use_the___tool_ignore_required_packages_flag);
                return null;
            }
            string path;
            return Settings.Default.ToolFilePaths.TryGetValue(missingProgramPathContainer, out path) ? path : FindProgramPath(missingProgramPathContainer);
        }

        public bool? ShouldOverwriteAnnotations(List<AnnotationDef> annotations)
        {
            if (overwriteAnnotations == null)
            {
                _out.WriteLine(Resources.AddZipToolHelper_ShouldOverwriteAnnotations_There_are_annotations_with_conflicting_names__Please_use_the___tool_zip_overwrite_annotations_command_);
            }
            if (overwriteAnnotations == true)
            {
                _out.WriteLine(Resources.AddZipToolHelper_ShouldOverwriteAnnotations_There_are_conflicting_annotations__Overwriting_);
                foreach (var annotationDef in annotations)
                {
                    _out.WriteLine(Resources.AddZipToolHelper_ShouldOverwriteAnnotations_Warning__the_annotation__0__is_being_overwritten, annotationDef.GetKey());
                }
            }
            if (overwriteAnnotations == false)
            {
                _out.WriteLine(Resources.AddZipToolHelper_ShouldOverwriteAnnotations_There_are_conflicting_annotations__Keeping_existing_);
                foreach (var annotationDef in annotations)
                {
                    _out.WriteLine(Resources.AddZipToolHelper_ShouldOverwriteAnnotations_Warning__the_annotation__0__may_not_be_what_your_tool_requires_, annotationDef.GetKey());
                }
            }
            return overwriteAnnotations;
        }

        public string FindProgramPath(ProgramPathContainer missingProgramPathContainer)
        {
            if ((Equals(programPathContainer, missingProgramPathContainer)) && programPath != null)
            {
                //add to settings list
                Settings.Default.ToolFilePaths.Add(programPathContainer,programPath);
                return programPath;
            }
            _out.WriteLine(Resources.AddZipToolHelper_FindProgramPath_A_tool_requires_Program__0__Version__1__and_it_is_not_specified_with_the___tool_program_macro_and___tool_program_path_commands__Tool_Installation_Canceled_, missingProgramPathContainer.ProgramName, missingProgramPathContainer.ProgramVersion);
            return null;
        }
    }

    internal class CommandProgressMonitor : IProgressMonitor
    {
        private ProgressStatus _currentProgress;
        private readonly DateTime _waitStart;
        private DateTime _lastOutput;
        private string _lastMessage;

        private readonly TextWriter _out;
        private Thread _waitingThread;
        private volatile bool _waiting;

        public CommandProgressMonitor(TextWriter outWriter, ProgressStatus status)
        {
            _out = outWriter;
            _waitStart = _lastOutput = DateTime.Now;

            UpdateProgress(status);
        }

        bool IProgressMonitor.IsCanceled
        {
            get { return false; }
        }

        UpdateProgressResponse IProgressMonitor.UpdateProgress(ProgressStatus status)
        {
            UpdateProgress(status);

            return UpdateProgressResponse.normal;
        }

        public bool HasUI { get { return false; } }

        public Exception ErrorException { get { return _currentProgress != null ? _currentProgress.ErrorException : null; } }

        private void UpdateProgress(ProgressStatus status)
        {
            if (status.PercentComplete != -1)
            {
                // Stop the waiting thread if it is running.
                _waiting = false;
                if(_waitingThread != null && _waitingThread.IsAlive)
                {
                    _waitingThread.Join();
                }  
            }

            var currentTime = DateTime.Now;
            // Show progress at least every 2 seconds and at 100%, if any other percentage
            // output has been shown.
            if ((currentTime - _lastOutput).Seconds < 2 && !status.IsError && (status.PercentComplete != 100 || _lastOutput == _waitStart))
            {
                return;
            }

            bool writeMessage = !string.IsNullOrEmpty(status.Message) &&
                                (_lastMessage == null || !ReferenceEquals(status.Message, _lastMessage));

            if (status.IsError)
            {
                _out.WriteLine(Resources.CommandLine_GeneralException_Error___0_, status.ErrorException.Message);
                writeMessage = false;
            }
            else if (status.PercentComplete == -1)
            {
                _out.Write(Resources.CommandWaitBroker_UpdateProgress_Waiting___);
                _waiting = true;
                // Start a thread that will indicate "waiting" progress to the console every few seconds.
                _waitingThread = new Thread(Wait){IsBackground = true};
                _waitingThread.Start();
            }
            else if (_currentProgress != null && status.PercentComplete != _currentProgress.PercentComplete)
            {
                // If writing a message at the same time as updating percent complete,
                // put them both on the same line
                if (writeMessage)
                    _out.WriteLine("{0}% - {1}", status.PercentComplete, status.Message); // Not L10N
                else
                    _out.WriteLine("{0}%", status.PercentComplete); // Not L10N
            }
            else if (writeMessage)
            {
                _out.WriteLine(status.Message);
            }

            if (writeMessage)
                _lastMessage = status.Message;
            _lastOutput = currentTime;
            _currentProgress = status;
        }

        private void Wait()
        {
            while (_waiting)
            {
                _out.Write("."); // Not L10N
                Thread.Sleep(1000);
            }
            _out.WriteLine(Resources.CommandWaitBroker_Wait_Done);
        }
    }
}