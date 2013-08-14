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
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Hibernate.Query;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results;
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
        public string ImportSourceDirectory { get; private set; }
        public Regex ImportNamingPattern { get; private set; }
        public DateTime RemoveBeforeDate { get; private set; }
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

        public string ReportName { get; private set; }
        public char ReportColumnSeparator { get; private set; }
        public string ReportFile { get; private set; }
        public bool ExportingReport
        {
            get { return !string.IsNullOrEmpty(ReportName); }
        }

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
                    throw new ArgumentException(string.Format("The instrument type {0} is not valid for transition list export", value));
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
                    throw new ArgumentException(string.Format("The instrument type {0} is not valid for method export", value));
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

        private string _optimizeType;
        public string OptimizeType
        {
            get { return _optimizeType; }
            set
            {
                if(value == null)
                {
                    _optimizeType = null;
                    return;
                }

                var valueUpper = value.ToUpper();
                switch(valueUpper)
                {
                    case "NONE":
                        _optimizeType = ExportOptimize.NONE;
                        break;
                    case "CE":
                        _optimizeType = ExportOptimize.CE;
                        break;
                    case "DP":
                        _optimizeType = ExportOptimize.DP;
                        break;
                    default:
                        throw new ArgumentException(string.Format("The instrument parameter {0} is not valid for optimization.", value));
                }
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
                    throw new ArgumentException(string.Format("The primary transition count {0} must be between {1} and {2}.", value, AbstractMassListExporter.PRIMARY_COUNT_MIN, AbstractMassListExporter.PRIMARY_COUNT_MAX));
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
                    throw new ArgumentException(string.Format("The dwell time {0} must be between {1} and {2}.", value, AbstractMassListExporter.DWELL_TIME_MIN, AbstractMassListExporter.DWELL_TIME_MAX));
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
                    throw new ArgumentException(string.Format("The run length {0} must be between {1} and {2}.", value, AbstractMassListExporter.RUN_LENGTH_MIN, AbstractMassListExporter.RUN_LENGTH_MAX));
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
                    OptimizeType = OptimizeType,
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
            OptimizeType = ExportOptimize.NONE;
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
                if (arg.StartsWith("--"))
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
                _out.WriteLine("Error: {0}", x.Message);
                return false;
            }
            catch (Exception x)
            {
                // Unexpected behavior, but better to output the error then appear to crash, and
                // have Windows write it to the application event log.
                _out.WriteLine("Error: {0}", x.Message);
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

                if (IsNameValue(pair, "in"))
                {
                    SkylineFile = GetFullPath(pair.Value);
                    // Set requiresInCommand to be true so if SkylineFile is null or empty it still complains.
                    RequiresSkylineDocument = true;
                }
                else if (IsNameValue(pair, "dir"))
                {
                    Directory.SetCurrentDirectory(pair.Value);
                }
                else if (IsNameOnly(pair, "timestamp"))
                {
                    _out.IsTimeStamped = true;
                }

                // A command that exports all the tools to a text file in a SkylineRunner form for --batch-commands
                // Not advertised.
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

                 // Import a skyr file.
                else if (IsNameValue(pair, "report-add"))
                {
                    ImportingSkyr = true;
                    SkyrPath = pair.Value;
                }

                else if (IsNameValue(pair, "report-conflict-resolution"))
                {
                    string input = pair.Value.ToLower();
                    if (input == "overwrite")
                    {
                        ResolveSkyrConflictsBySkipping = false;
                    }
                    if (input == "skip")
                    {
                        ResolveSkyrConflictsBySkipping = true;
                    }
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
                    string input = pair.Value.ToLower();
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

                else if (IsNameValue(pair, "import-naming-pattern"))
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
                            _out.WriteLine("Error: Regular expression {0} cannot be parsed.", importNamingPatternVal);
                            _out.WriteLine(e.Message);
                            return false;
                        }

                        Match match = Regex.Match(importNamingPatternVal, @".*\(.+\).*");
                        if (!match.Success)
                        {
                            _out.WriteLine("Error: Regular expression '{0}' does not have any groups.",
                                            importNamingPatternVal);
                            _out.WriteLine(
                                "       One group is required. The part of the file or sub-directory name");
                            _out.WriteLine(
                                "       that matches the first group in the regular expression is used as");
                            _out.WriteLine("       the replicate name.");
                            return false;
                        }
                    }
                }

                else if (IsNameValue(pair, "import-before"))
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
                            _out.WriteLine("Error: Date {0} cannot be parsed.", importBeforeDate);
                            _out.WriteLine(e.Message);
                            return false;
                        }
                    }
                }

                else if (IsNameValue(pair, "import-on-or-after"))
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
                            _out.WriteLine("Error: Date {0} cannot be parsed.", importAfterDate);
                            _out.WriteLine(e.Message);
                            return false;
                        }
                    }
                }

                else if (IsNameValue(pair, "remove-before"))
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
                            _out.WriteLine("Error: Date {0} cannot be parsed.", removeBeforeDate);
                            _out.WriteLine(e.Message);
                            return false;
                        }
                    }
                }

                else if (IsNameValue(pair, "report-name"))
                {
                    ReportName = pair.Value;
                    RequiresSkylineDocument = true;
                }

                else if (IsNameValue(pair, "report-file"))
                {
                    ReportFile = GetFullPath(pair.Value);
                    RequiresSkylineDocument = true;
                }

                else if (IsNameValue(pair, "report-format"))
                {
                    if (pair.Value.Equals("TSV", StringComparison.CurrentCultureIgnoreCase))
                        ReportColumnSeparator = TextUtil.SEPARATOR_TSV;
                    else if (pair.Value.Equals("CSV", StringComparison.CurrentCultureIgnoreCase))
                        ReportColumnSeparator = TextUtil.CsvSeparator;
                    else
                    {
                        _out.WriteLine(
                            "Warning: The report format {0} is invalid. It must be either \"CSV\" or \"TSV\".",
                            pair.Value);
                        _out.WriteLine("Defaulting to CSV.");
                        ReportColumnSeparator = TextUtil.CsvSeparator;
                    }
                }

                else if (IsNameValue(pair, "exp-translist-instrument"))
                {
                    try
                    {
                        TransListInstrumentType = pair.Value;
                        RequiresSkylineDocument = true;
                    }
                    catch (ArgumentException)
                    {
                        _out.WriteLine("Warning: The instrument type {0} is not valid. Please choose from:",
                                        pair.Value);
                        foreach (string str in ExportInstrumentType.TRANSITION_LIST_TYPES)
                        {
                            _out.WriteLine(str);
                        }
                        _out.WriteLine("No transition list will be exported.");
                    }
                }
                else if (IsNameValue(pair, "exp-method-instrument"))
                {
                    try
                    {
                        MethodInstrumentType = pair.Value;
                        RequiresSkylineDocument = true;
                    }
                    catch (ArgumentException)
                    {
                        _out.WriteLine("Warning: The instrument type {0} is not valid. Please choose from:",
                                        pair.Value);
                        foreach (string str in ExportInstrumentType.METHOD_TYPES)
                        {
                            _out.WriteLine(str);
                        }
                        _out.WriteLine("No method will be exported.");
                    }
                }
                else if (IsNameValue(pair, "exp-file"))
                {
                    ExportPath = GetFullPath(pair.Value);
                    RequiresSkylineDocument = true;
                }
                else if (IsNameValue(pair, "exp-strategy"))
                {
                    ExportStrategySet = true;
                    RequiresSkylineDocument = true;

                    string strategy = pair.Value;

                    if (strategy.Equals("single", StringComparison.CurrentCultureIgnoreCase))
                    {
                        //default
                    }
                    else if (strategy.Equals("protein", StringComparison.CurrentCultureIgnoreCase))
                        ExportStrategy = ExportStrategy.Protein;
                    else if (strategy.Equals("buckets", StringComparison.CurrentCultureIgnoreCase))
                        ExportStrategy = ExportStrategy.Buckets;
                    else
                    {
                        _out.WriteLine("Warning: The export strategy {0} is not valid. It must be one of",
                                        pair.Value);
                        _out.WriteLine("\"single\", \"protein\" or \"buckets\". Defaulting to single.");
                        //already set to Single
                    }
                }

                else if (IsNameValue(pair, "exp-method-type"))
                {
                    var type = pair.Value;
                    RequiresSkylineDocument = true;
                    if (type.Equals("scheduled", StringComparison.CurrentCultureIgnoreCase))
                    {
                        ExportMethodType = ExportMethodType.Scheduled;
                    }
                    else if (type.Equals("triggered", StringComparison.CurrentCultureIgnoreCase))
                    {
                        ExportMethodType = ExportMethodType.Triggered;
                    }
                    else if (type.Equals("standard", StringComparison.CurrentCultureIgnoreCase))
                    {
                        //default
                    }
                    else
                    {
                        _out.WriteLine(
                            "Warning: The method type {0} is invalid. It must be \"standard\", \"scheduled\" or \"triggered\".",
                            pair.Value);
                        _out.WriteLine("Defaulting to standard.");
                    }
                }

                else if (IsNameValue(pair, "exp-max-trans"))
                {
                    //This one can't be kept within bounds because the bounds depend on the instrument
                    //and the document. 
                    try
                    {
                        MaxTransitionsPerInjection = pair.ValueInt;
                    }
                    catch
                    {
                        _out.WriteLine("Warning: Invalid max transitions per injection parameter ({0}).", pair.Value);
                        _out.WriteLine("It must be a number. Defaulting to " +
                                        AbstractMassListExporter.MAX_TRANS_PER_INJ_DEFAULT + ".");
                        MaxTransitionsPerInjection = AbstractMassListExporter.MAX_TRANS_PER_INJ_DEFAULT;
                    }
                    RequiresSkylineDocument = true;
                }

                else if (IsNameValue(pair, "exp-optimizing"))
                {
                    try
                    {
                        OptimizeType = pair.Value;
                    }
                    catch (ArgumentException)
                    {
                        _out.WriteLine(
                            "Warning: Invalid optimization parameter ({0}). Use \"ce\", \"dp\", or \"none\".",
                            pair.Value);
                        _out.WriteLine("Defaulting to none.");
                    }
                    RequiresSkylineDocument = true;
                }
                else if (IsNameValue(pair, "exp-scheduling-replicate"))
                {
                    SchedulingReplicate = pair.Value;
                    RequiresSkylineDocument = true;
                }
                else if (IsNameValue(pair, "exp-template"))
                {
                    TemplateFile = GetFullPath(pair.Value);
                    RequiresSkylineDocument = true;
                }
                else if (IsNameOnly(pair, "exp-ignore-proteins"))
                {
                    IgnoreProteins = true;
                    RequiresSkylineDocument = true;
                }
                else if (IsNameValue(pair, "exp-primary-count"))
                {
                    try
                    {
                        PrimaryTransitionCount = pair.ValueInt;
                    }
                    catch
                    {
                        _out.WriteLine(
                            "Warning: The primary transition count {0} is invalid. it must be a number between {1} and {2}.",
                            pair.Value,
                            AbstractMassListExporter.PRIMARY_COUNT_MIN, AbstractMassListExporter.PRIMARY_COUNT_MAX);
                        _out.WriteLine("Defaulting to {0}.", AbstractMassListExporter.PRIMARY_COUNT_DEFAULT);
                    }
                    RequiresSkylineDocument = true;
                }
                else if (IsNameValue(pair, "exp-dwell-time"))
                {
                    try
                    {
                        DwellTime = pair.ValueInt;
                    }
                    catch
                    {
                        _out.WriteLine(
                            "Warning: The dwell time {0} is invalid. it must be a number between {1} and {2}.",
                            pair.Value,
                            AbstractMassListExporter.DWELL_TIME_MIN, AbstractMassListExporter.DWELL_TIME_MAX);
                        _out.WriteLine("Defaulting to {0}.", AbstractMassListExporter.DWELL_TIME_DEFAULT);
                    }
                    RequiresSkylineDocument = true;
                }
                else if (IsNameOnly(pair, "exp-add-energy-ramp"))
                {
                    AddEnergyRamp = true;
                    RequiresSkylineDocument = true;
                }
                else if (IsNameValue(pair, "exp-run-length"))
                {
                    try
                    {
                        RunLength = pair.ValueInt;
                    }
                    catch
                    {
                        _out.WriteLine(
                            "Warning: The run length {0} is invalid. It must be a number between {1} and {2}.",
                            pair.Value,
                            AbstractMassListExporter.RUN_LENGTH_MIN, AbstractMassListExporter.RUN_LENGTH_MAX);
                        _out.WriteLine("Defaulting to {0}.", AbstractMassListExporter.RUN_LENGTH_DEFAULT);
                    }
                    RequiresSkylineDocument = true;
                }
            }
           
            // If skylineFile isn't set and one of the commands that requires --in is called, complain.
            if (String.IsNullOrEmpty(SkylineFile) && RequiresSkylineDocument && !_isDocumentLoaded)
            {
                _out.WriteLine("Error: Use --in to specify a Skyline document to open.");
                return false;
            }

            if(ImportingReplicateFile && ImportingSourceDirectory)
            {
                _out.WriteLine("Error: --import-file and --import-all options cannot be used simultaneously.");
                return false;
            }
            if(ImportingReplicateFile && ImportNamingPattern != null)
            {       
                _out.WriteLine("Error: --import-naming-pattern cannot be used with the --import-file option.");
                return false;
            }
            if(ImportingSourceDirectory && !string.IsNullOrEmpty(ReplicateName))
            {
                _out.WriteLine("Error: --import-replicate-name cannot be used with the --import-all option.");
                return false;
            }
            
            // Use the original file as the output file, if not told otherwise.
            if (Saving && String.IsNullOrEmpty(SaveFile))
            {
                SaveFile = SkylineFile;
            }
            return true;
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

        private class ValueMissingException : UsageException
        {
            public ValueMissingException(string name)
                : base(string.Format("The argument --{0} requires a value and must be specified in the format --name=value",  name))
            {
            }
        }

        private class ValueUnexpectedException : UsageException
        {
            public ValueUnexpectedException(string name)
                : base(string.Format("The argument --{0} should not have a value specified", name))
            {
            }
        }

        private class UsageException : ArgumentException
        {
            protected UsageException(string message) : base(message)
            {
            }
        }
    }

    public class CommandLine : IDisposable
    {
        private readonly CommandStatusWriter _out;

        private SrmDocument _doc;
        private string _skylineFile;

        private ExportCommandProperties _exportProperties;

        public CommandLine(CommandStatusWriter output)
        {
            _out = output;
        }

        public void Run(string[] args)
        {

            var commandArgs = new CommandArgs(_out, _doc != null);

            if(!commandArgs.ParseArgs(args))
            {
                _out.WriteLine("Exiting...");
                return;
            }

            // First come the commands that do not depend on an --in command to run.
            // These commands modify Settings.Default instead of working with an open skyline document.
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
                _out.WriteLine("Exiting...");
                return;
            }

            if (commandArgs.ImportingResults)
            {   
                if (commandArgs.ImportingReplicateFile)
                {
                    // If expected results are not imported successfully, terminate
                    if (!ImportResultsFile(commandArgs.ReplicateFile,
                                           commandArgs.ReplicateName,
                                           commandArgs.ImportAppend))
                        return;
                }
                else if(commandArgs.ImportingSourceDirectory)
                {
                    // If expected results are not imported successfully, terminate
                    if(!ImportResultsInDir(commandArgs.ImportSourceDirectory, commandArgs.ImportNamingPattern,
                                           commandArgs.ImportBeforeDate, commandArgs.ImportOnOrAfterDate))
                        return;
                }
            }

            if (_doc != null && !_doc.Settings.IsLoaded)
            {
                IProgressMonitor progressMonitor = new CommandWaitBroker(_out, new ProgressStatus(string.Empty));
                var docContainer = new ResultsMemoryDocumentContainer(null, _skylineFile) { ProgressMonitor = progressMonitor };
                docContainer.SetDocument(_doc, null, true);
                _doc = docContainer.Document;
            }

            if (commandArgs.RemovingResults)
            {
                RemoveResults(commandArgs.RemoveBeforeDate);
            }

            if (commandArgs.ImportingFasta)
            {
                try
                {
                    ImportFasta(commandArgs.FastaPath, commandArgs.KeepEmptyProteins);
                }
                catch (Exception x)
                {
                    _out.WriteLine("Error: Failed importing the file {0}. {1}", commandArgs.FastaPath, x.Message);
                }
            }

            if (commandArgs.SettingLibraryPath)
            {
                if (!SetLibrary(commandArgs.LibraryName, commandArgs.LibraryPath))
                    _out.WriteLine("Not setting library.");
            }

            if (commandArgs.Saving)
            {
                SaveFile(commandArgs.SaveFile ?? _skylineFile);
            }

            if (commandArgs.ExportingReport)
            {
                ExportReport(commandArgs.ReportName, commandArgs.ReportFile, commandArgs.ReportColumnSeparator);
            }

            if (!string.IsNullOrEmpty(commandArgs.TransListInstrumentType) &&
                !string.IsNullOrEmpty(commandArgs.MethodInstrumentType))
            {
                _out.WriteLine("Error: You cannot simultaneously export a transition list and a method.");
                _out.WriteLine("Neither will be exported. Please change the command line parameters.");
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
        }

        public bool OpenSkyFile(string skylineFile)
        {
            try
            {
                using (var stream = new FileStream(skylineFile, FileMode.Open))
                {
                    XmlSerializer xmlSerializer = new XmlSerializer(typeof(SrmDocument));
                    _out.WriteLine("Opening file...");

                    _doc = ConnectDocument((SrmDocument)xmlSerializer.Deserialize(stream), skylineFile);
                    if (_doc == null)
                        return false;

                    _out.WriteLine("File {0} opened.", Path.GetFileName(skylineFile));
                }
            }
            catch (FileNotFoundException)
            {
                _out.WriteLine("Error: The Skyline file {0} does not exist.", skylineFile);
                return false;
            }
            catch (Exception x)
            {
                _out.WriteLine("Error: There was an error opening the file");
                _out.WriteLine("{0}", skylineFile);
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
                    _out.WriteLine("Error: Could not find the spectral library {0} for this document.", docLibFile);
                    return null;
                }
            }

            var settings = document.Settings.ConnectLibrarySpecs(library =>
            {
                LibrarySpec spec;
                if (Settings.Default.SpectralLibraryList.TryGetValue(library.Name, out spec))
                    return spec;

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
                _out.WriteLine("Warning: Could not find the spectral library {0}", library.Name);
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
                return result as RCalcIrt;

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

            _out.WriteLine("Error: Could not find the iRT database {0}", Path.GetFileName(irtCalc.DatabasePath));
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
                return result;

            string fileName = Path.GetFileName(backgroundProteomeSpec.DatabasePath);
            // First look for the file name in the document directory
            string pathBackgroundProteome = Path.Combine(Path.GetDirectoryName(documentPath) ?? string.Empty, fileName ?? string.Empty);
            if (File.Exists(pathBackgroundProteome))
                return new BackgroundProteomeSpec(backgroundProteomeSpec.Name, pathBackgroundProteome);
            // In the user's default library directory
            pathBackgroundProteome = Path.Combine(Settings.Default.ProteomeDbDirectory, fileName ?? string.Empty);
            if (File.Exists(pathBackgroundProteome))
                return new BackgroundProteomeSpec(backgroundProteomeSpec.Name, pathBackgroundProteome);
            _out.WriteLine("Warning: Could not find the background proteome file {0}", Path.GetFileName(fileName));
            return BackgroundProteomeList.GetDefault();
        }

        public bool ImportResultsInDir(string sourceDir, Regex namingPattern, DateTime? importBefore, DateTime? importOnOrAfter)
        {
            var listNamedPaths = GetDataSources(sourceDir, namingPattern);
            if (listNamedPaths == null)
            {
                return false;
            }

            foreach (KeyValuePair<string, string[]> namedPaths in listNamedPaths)
            {
                string replicateName = namedPaths.Key;
                string[] files = namedPaths.Value;
                foreach (var file in files)
                {
                    // Skip if file write time is after importBefore or before importAfter
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if ((importBefore != null && importBefore < fileInfo.LastWriteTime) ||
                            (importOnOrAfter != null && importOnOrAfter >= fileInfo.LastWriteTime))
                            continue;
                    }
                    catch (Exception e)
                    {
                        _out.WriteLine("Error: Could not get last write time for file {0}", file);
                        _out.WriteLine(e);
                        return false;
                    }

                    if (!ImportResultsFile(file, replicateName))
                        return false;
                }
            }
            return true;
        }

        private IEnumerable<KeyValuePair<string, string[]>> GetDataSources(string sourceDir, Regex namingPattern)
        {   
            // get all the valid data sources (files and sub directories) in this directory.
            IList<KeyValuePair<string, string[]>> listNamedPaths;
            try
            {
                listNamedPaths = DataSourceUtil.GetDataSources(sourceDir).ToArray();
            }
            catch(IOException e)
            {
                _out.WriteLine("Error: Failure reading file information from directory {0}.", sourceDir);
                _out.WriteLine(e.Message);
                return null;
            }
            if (!listNamedPaths.Any())
            {
                _out.WriteLine("Error: No data sources found in directory {0}.", sourceDir);
                return null;
            }

            // If we were given a regular expression apply it to the replicate names
            if(namingPattern != null)
            {
                List<KeyValuePair<string, string[]>> listRenamedPaths;
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
            List<KeyValuePair<string, string[]>> listNewPaths;
            if(!RemoveImportedFiles(listNamedPaths, out listNewPaths))
            {
                return null;
            }
            return listNewPaths;
        }

        private bool ApplyNamingPattern(IEnumerable<KeyValuePair<string, string[]>> listNamedPaths, Regex namingPattern, 
                                        out List<KeyValuePair<string, string[]>> listRenamedPaths)
        {
            listRenamedPaths = new List<KeyValuePair<string, string[]>>();

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
                        _out.WriteLine("Error: Match to regular expression is empty for {0}.", replName);
                        return false;
                    }                    
                    if (uniqNames.Contains(replNameNew))
                    {
                        _out.WriteLine("Error: Duplicate replicate name '{0}'", replNameNew);
                        _out.WriteLine("       after applying regular expression.");
                        return false;
                    }
                    uniqNames.Add(replNameNew);
                    listRenamedPaths.Add(new KeyValuePair<string, string[]>(replNameNew, namedPaths.Value));
                }
                else
                {
                    _out.WriteLine("Error: {0} does not match the regular expression.", replName);
                    return false;
                }
            }

            return true;
        }

        private bool CheckReplicateFiles(IEnumerable<KeyValuePair<string, string[]>> listNamedPaths)
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
                    var filePaths = new HashSet<string>(namedPaths.Value.Select(path => path.ToLower()));
                    foreach (var dataFilePath in chromatogram.MSDataFilePaths)
                    {
                        if (!filePaths.Contains(dataFilePath.ToLower()))
                        {
                            _out.WriteLine(
                                "Error: Replicate {0} in the document has an unexpected file {1}.",
                                replicateName,
                                dataFilePath);
                            return false;
                        }
                    }
                }
            }
            return true;
        }
        
        private bool RemoveImportedFiles(IEnumerable<KeyValuePair<string, string[]>> listNamedPaths,
                                         out List<KeyValuePair<string, string[]>> listNewNamedPaths)
        {
            listNewNamedPaths = new List<KeyValuePair<string, string[]>>();

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
                    var chromatFilePaths = new HashSet<string>(chromatogram.MSDataFilePaths.Select(path => path.ToLower()));

                    var filePaths = namedPaths.Value;
                    var filePathsNotInRepl = new List<string>(filePaths.Length);
                    foreach (var fpath in filePaths)
                    {
                        if (chromatFilePaths.Contains(fpath.ToLower()))
                        {
                            _out.WriteLine("{0} -> {1}", replicateName, fpath);
                            _out.WriteLine("  Note: The file has already been imported. Ignoring...");
                        }
                        else
                        {
                            filePathsNotInRepl.Add(fpath);
                        }
                    }

                    if (filePathsNotInRepl.Count > 0)
                    {
                        listNewNamedPaths.Add(new KeyValuePair<string, string[]>(replicateName,
                                                                                 filePathsNotInRepl.ToArray()));
                    }
                }
            }
            return true;
        }

        public bool ImportResultsFile(string replicateFile, string replicateName, bool append)
        {
            if (string.IsNullOrEmpty(replicateName))
                replicateName = Path.GetFileNameWithoutExtension(replicateFile);

            if(_doc.Settings.HasResults && _doc.Settings.MeasuredResults.ContainsChromatogram(replicateName))
            {
                if (!append)
                {
                    // CONSIDER: Error? Check if the replicate contains the file?
                    //           It does not seem right to just continue on to export a report
                    //           or new method without the results added.
                    _out.WriteLine("Warning: The replicate {0} already exists", replicateName);
                    _out.WriteLine("in the given document and the --import-append option is not specified.");
                    _out.WriteLine("The replicate will not be added to the document.");
                    return true;
                }
                
                // If we are appending to an existing replicate in the document
                // make sure this file is not already in the replicate.
                ChromatogramSet chromatogram;
                int index;
                _doc.Settings.MeasuredResults.TryGetChromatogramSet(replicateName, out chromatogram, out index);

                if (chromatogram.MSDataFilePaths.Contains(replicateFile, StringComparer.OrdinalIgnoreCase))
                {
                    _out.WriteLine("{0} -> {1}", replicateName, replicateFile);
                    _out.WriteLine("    Note: The file has already been imported. Ignoring...");

                    return true;
                }
            }

            return ImportResultsFile(replicateFile, replicateName);
        }

        public bool ImportResultsFile(string replicateFile, string replicateName)
        {
            _out.WriteLine("Adding results...");

            // Hack for un-readable RAW files from Thermo instruments.
            if(!CanReadFile(replicateFile))
            {
                _out.WriteLine("Warning: Cannot read file {0}.", replicateFile);
                _out.WriteLine("         Ignoring...");
                return true;
            }

            //This function will also detect whether the replicate exists in the document
            ProgressStatus status;
            SrmDocument newDoc;
            IProgressMonitor progressMonitor = new CommandWaitBroker(_out, new ProgressStatus(string.Empty));

            try
            {
                newDoc = ImportResults(_doc, _skylineFile, replicateName, replicateFile, progressMonitor, out status);
            }
            catch (Exception x)
            {
                _out.WriteLine("Error: Failed importing the results file {0}.", replicateFile);
                _out.WriteLine(x.Message);
                return false;
            }

            status = status ?? new ProgressStatus("").Complete();
            if (status.IsError && status.ErrorException != null)
            {
                if (status.ErrorException is MissingDataException)
                {
                    _out.WriteLine("Warning: Failed importing the results file {0}.", replicateFile);
                    _out.WriteLine(status.ErrorException.Message);
                    _out.WriteLine("         Ignoring...");
                    return true;
                }
                _out.WriteLine("Error: Failed importing the results file {0}.", replicateFile);
                _out.WriteLine(status.ErrorException.Message);
                return false;
            }
            if (!status.IsComplete || ReferenceEquals(_doc, newDoc))
            {
                _out.WriteLine("Error: Failed importing the results file {0}.", replicateFile);
                return false;
            }

            _doc = newDoc;

            _out.WriteLine("Results added from {0} to replicate {1}.", Path.GetFileName(replicateFile), replicateName);
            //the file was imported successfully
            return true;
        }

        public void RemoveResults(DateTime removeBefore)
        {
            _out.WriteLine("Removing results before " + removeBefore.ToShortDateString() + "...");
            var filteredChroms = new List<ChromatogramSet>();
            foreach (var chromSet in _doc.Settings.MeasuredResults.Chromatograms)
            {
                var listFileInfos = chromSet.MSDataFileInfos.Where(fileInfo =>
                    fileInfo.RunStartTime == null || fileInfo.RunStartTime >= removeBefore).ToArray();
                if (ArrayUtil.ReferencesEqual(listFileInfos, chromSet.MSDataFileInfos))
                    filteredChroms.Add(chromSet);
                else
                {
                    foreach (var fileInfo in chromSet.MSDataFileInfos.Except(listFileInfos))
                        _out.WriteLine("Removed {0}.", fileInfo.FilePath);
                    if (listFileInfos.Any())
                        filteredChroms.Add(chromSet.ChangeMSDataFileInfos(listFileInfos));
                }
            }
            if (!ArrayUtil.ReferencesEqual(filteredChroms, _doc.Settings.MeasuredResults.Chromatograms))
            {
                MeasuredResults newMeasuredResults = filteredChroms.Any() ?
                    _doc.Settings.MeasuredResults.ChangeChromatograms(filteredChroms) : null;

                _doc = _doc.ChangeMeasuredResults(newMeasuredResults);
            }
        }

        public void ImportFasta(string path, bool keepEmptyProteins)
        {
            _out.WriteLine("Importing FASTA file {0}...", Path.GetFileName(path));
            using (var readerFasta = new StreamReader(path))
            {
                IProgressMonitor progressMonitor = new NoMessageCommandWaitBroker(_out, new ProgressStatus(string.Empty));
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
                _out.WriteLine("Error: Cannot set library name without path.");
                return false;
            }
            else if (!File.Exists(path))
            {
                _out.WriteLine("Error: The file {0} does not exist.", path);
                return false;
            }
            else if (path.EndsWith(BiblioSpecLiteSpec.EXT_REDUNDANT))
            {
                _out.WriteLine("Error: The file {0} appears to be a redundant library.");
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
                _out.WriteLine("Error: The file {0} is not a supported spectral library file format.", path);
                return false;
            }

            // Check for conflicting names
            foreach (var docLibrarySpec in _doc.Settings.PeptideSettings.Libraries.LibrarySpecs)
            {
                if (docLibrarySpec.Name == librarySpec.Name || docLibrarySpec.FilePath == librarySpec.FilePath)
                {
                    _out.WriteLine("Error: The library you are trying to add conflicts with a library already in the file.");
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
        private bool CanReadFile(string replicatePath)
        {
            if (!File.Exists(replicatePath) && !Directory.Exists(replicatePath))
            {
                _out.WriteLine("Error: File does not exist: {0}.",replicatePath);
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
// ReSharper disable ObjectCreationAsStatement
                new MsDataFileImpl(replicatePath);  
// ReSharper restore ObjectCreationAsStatement
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
            _out.WriteLine(("Saving file..."));
            try
            {
                SaveDocument(_doc, saveFile);
            }
            catch
            {
                _out.WriteLine("Error: The file could not be saved to {0}.", saveFile);
                _out.WriteLine("Check that the directory exists and is not read-only.");
                return;
            }
            _out.WriteLine("File " + Path.GetFileName(saveFile) + " saved.");
        }

        public void ExportReport(string reportName, string reportFile, char reportColSeparator)
        {

            if (String.IsNullOrEmpty(reportFile))
            {
                _out.WriteLine("Error: If you specify a report, you must specify the");
                _out.WriteLine("--report-file=path/to/file.csv parameter.");
                return;
            }

            //Check that the report exists
            ReportSpec reportSpec = Settings.Default.GetReportSpecByName(reportName);
            if (reportSpec == null)
            {
                _out.WriteLine("Error: The report {0} does not exist. If it has spaces in its name,", reportName);
                _out.WriteLine("use \"double quotes\" around the entire list of command parameters.");
                return;
            }

            _out.WriteLine("Exporting report {0}...", reportName);

            try
            {
                using (var saver = new FileSaver(reportFile))
                {
                    if (!saver.CanSave())
                    {
                        _out.WriteLine("Error: The report {0} could not be saved to {1}.", reportName, reportFile);
                        _out.WriteLine("Check to make sure it is not read-only.");
                    }

                    var status = new ProgressStatus(string.Empty);
                    IProgressMonitor broker = new CommandWaitBroker(_out, status);

                    using (var writer = new StreamWriter(saver.SafeName))
                    {
                        Report report = Report.Load(reportSpec);

                        using (Database database = new Database(_doc.Settings)
                        {
                            ProgressMonitor = broker,
                            Status = status,
                            PercentOfWait = 80
                        })
                        {
                            database.AddSrmDocument(_doc);
                            status = database.Status;

                            ResultSet resultSet = report.Execute(database);

                            broker.UpdateProgress(status = status.ChangePercentComplete(95));
                            ResultSet.WriteReportHelper(resultSet, reportColSeparator, writer,
                                                        CultureInfo.CurrentCulture);
                        }

                        writer.Flush();
                        writer.Close();
                    }

                    broker.UpdateProgress(status.Complete());
                    saver.Commit();
                    _out.WriteLine("Report {0} exported successfully.", reportName);
                }
            }
            catch (Exception x)
            {
                _out.WriteLine("Error: Failure attempting to save {0} report to {1}.", reportName, reportFile);
                _out.WriteLine(x.Message);
            }
        }

        // A function for adding tools to the Tools Menu.
        public void ImportTool (string title, string command, string arguments, string initialDirectory, string reportTitle, bool outputToImmediateWindow, bool? resolveToolConflictsBySkipping)
        {
            if (title == null | command == null)
            {
                _out.WriteLine("Error: to import a tool it must have a name and a command");
                _out.WriteLine("       Use --tool-add to specify a name");
                _out.WriteLine("       Use --tool-command to specify a command");
                _out.WriteLine("       The tool was not imported...");
                return;
            }
            // Check if the command is of a supported type and not a URL
            else if (!ConfigureToolsDlg.CheckExtension(command) && !ToolDescription.IsWebPageCommand(command))
            {
                string supportedTypes = String.Join("; ", ConfigureToolsDlg.EXTENSIONS);
                supportedTypes = supportedTypes.Replace(".", "*.");
                _out.WriteLine("Error: the provided command for the tool {0} is not of a supported type", title);
                _out.WriteLine("       Supported Types are: {0}", supportedTypes);
                _out.WriteLine("       The tool was not imported...");
                return;
            }
            if (arguments != null && arguments.Contains(ToolMacros.INPUT_REPORT_TEMP_PATH))
            {
                if (string.IsNullOrEmpty(reportTitle))
                {
                    _out.WriteLine("Error: If {0} is and argument the tool must have a Report Title", ToolMacros.INPUT_REPORT_TEMP_PATH);
                    _out.WriteLine("        use the --tool-report parameter to specify a report");
                    _out.WriteLine("        The tool was not imported...");
                    return;
                }

                if (!Settings.Default.ReportSpecList.ContainsKey(reportTitle))
                {
                    _out.WriteLine("Error: Please import the report format for {0}.", reportTitle);
                    _out.WriteLine("        Use the --report-add parameter to add the missing custom report.");
                    _out.WriteLine("        The tool was not imported...");
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
                        _out.WriteLine("Error: A tool titled {0} already exists.", tool.Title);
                        _out.WriteLine(              "       Please use --tool-conflict-resolution=< overwrite | skip >");
                        _out.WriteLine("       tool titled {0} was not added.", tool.Title);
                        return; // Dont add.
                    }
                    // Skip conflicts
                    if (resolveToolConflictsBySkipping == true)
                    {
                        _out.WriteLine("Warning: skipping tool {0} due to a name conflict.", tool.Title);
//                        _out.WriteLine("         tool {0} was not modified.", tool.Title);
                        return;
                    }
                    // Ovewrite conflicts
                    if (resolveToolConflictsBySkipping == false)
                    {
                        _out.WriteLine("Warning: the tool {0} was overwritten", tool.Title);
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
                _out.WriteLine("{0} was added to the Tools Menu", title);
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
                _out.WriteLine("Error: {0} does not exist. --batch-commands failed.", path);
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
                            string[] args = ParseInput(input);
                            Run(args);
                        }
                    }
                }
                catch (Exception)
                {
                    _out.WriteLine("Error: failed to open file {0} --batch-commands command failed.", path);
                }
            }            
        }
        
        /// <summary>
        ///  A method for parsing command line inputs to accept quotes arround strings and double quotes within those strings.
        ///  See CommandLineTest.cs ConsoleParserTest() for specific examples of its behavior. 
        /// </summary>
        /// <param name="inputs"> string on inputs </param>
        /// <returns> string[] of parsed commands </returns>
        public static string[] ParseInput (string inputs)
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
        /// A method for parsing an array of individual arguments to be passed to the command line to generate
        /// the string that will ultimately be passed. If a argument has white space, it is surrounded by quotes.
        /// If an empty (size 0) array is given, it returns string.Empty 
        /// </summary>
        /// <param name="arguments">The arguments to parse</param>
        /// <returns>The appropriately formatted command line argument string</returns>
        public static string ParseCommandLineArray(string[] arguments)
        {
            if (!arguments.Any())
            {
                return string.Empty;
            }
            
            StringBuilder commandLineArguments = new StringBuilder();
            foreach (string argument in arguments)
            {
                if (argument.Contains(" ") || argument.Contains("\t") || argument.Equals(string.Empty)) //Consider: Should this handle null reference?
                {
                    commandLineArguments.Append(" \"" + argument + "\"");
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
                _out.WriteLine("Error: {0} does not exist. --report-add command failed.", path);
            }
            else
            {           
                ImportSkyrHelper helper = new ImportSkyrHelper(_out, resolveSkyrConflictsBySkipping);
                bool imported;
                try
                {
                    imported = Settings.Default.ReportSpecList.ImportFile(path, helper.ResolveImportConflicts);                 
                }
                catch (Exception e)
                {
                    _out.WriteLine("Failure loading {0}. \n {1}", path, e);
                    return;
                }
                if (imported)
                {
                    Settings.Default.Save();
                    _out.WriteLine("Success! Imported Reports from {0}", Path.GetFileNameWithoutExtension(path));
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
                                           ? "The name '{0}' already exists."
                                           : "The following names already exist:\n\n{0}\n\n";
                _outWriter.WriteLine(messageFormat, string.Join("\n", existing.ToArray()));
                if (resolveSkyrConflictsBySkipping == null)
                {
                    _outWriter.WriteLine("Error: Please specify a way to resolve conflicts.");
                    _outWriter.WriteLine("       Use command --report-conflict-resolution=< overwrite | skip >");
                    return null;
                }
                if (resolveSkyrConflictsBySkipping == true)
                {
                    _outWriter.WriteLine("Resolving conflicts by skipping.");
                    // The objects are skipped below for being in the list called existing
                }
                if (resolveSkyrConflictsBySkipping == false)
                {
                    _outWriter.WriteLine("Resolving conflicts by overwriting.");
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
                _out.WriteLine("Error: You must specify an output file to write to with the");
                _out.WriteLine("--exp-file=path/to/file parameter. No transition list");
                _out.WriteLine("will be exported.");
                return;
            }

            if (Equals(type, ExportFileType.Method))
            {
                if (string.IsNullOrEmpty(args.TemplateFile))
                {
                    _out.WriteLine("Error: A template file is required to export a method.");
                    return;
                }
                if (Equals(args.MethodInstrumentType, ExportInstrumentType.AGILENT6400)
                        ? !Directory.Exists(args.TemplateFile)
                        : !File.Exists(args.TemplateFile))
                {
                    _out.WriteLine("Error: The template file {0} does not exist.", args.TemplateFile);
                    return;
                }
                if (Equals(args.MethodInstrumentType, ExportInstrumentType.AGILENT6400) &&
                    !AgilentMethodExporter.IsAgilentMethodPath(args.TemplateFile))
                {
                    _out.WriteLine("Error: The folder {0} does not appear to contain an Agilent QQQ", args.TemplateFile);
                    _out.WriteLine("method template.  The folder is expected to have a .m extension, and contain the");
                    _out.WriteLine("file qqqacqmethod.xsd.");
                    return;
                }
            }

            if (!args.ExportStrategySet)
            {
                _out.WriteLine("Warning: No export strategy specified (from \"single\", \"protein\" or");
                _out.WriteLine("\"buckets\"). Defaulting to \"single\".");
                args.ExportStrategy = ExportStrategy.Single;
            }

            if (args.AddEnergyRamp && !Equals(args.TransListInstrumentType, ExportInstrumentType.THERMO))
            {
                _out.WriteLine("Warning: The add-energy-ramp parameter is only applicable for Thermo");
                _out.WriteLine("transition lists. This parameter will be ignored.");
            }

            string instrument = Equals(type, ExportFileType.List)
                                    ? args.TransListInstrumentType
                                    : args.MethodInstrumentType;
            if (!CheckInstrument(instrument, _doc))
            {
                _out.WriteLine("Warning: The vendor {0} does not match the vendor", instrument);
                _out.WriteLine("in either the CE or DP prediction setting.");
                _out.WriteLine("Continuing exporting a transition list anyway...");
            }


            int maxInstrumentTrans = _doc.Settings.TransitionSettings.Instrument.MaxTransitions ??
                                     TransitionInstrument.MAX_TRANSITION_MAX;

            if ((args.MaxTransitionsPerInjection < AbstractMassListExporter.MAX_TRANS_PER_INJ_MIN ||
                 args.MaxTransitionsPerInjection > maxInstrumentTrans) &&
                (Equals(args.ExportStrategy, ExportStrategy.Buckets) ||
                 Equals(args.ExportStrategy, ExportStrategy.Protein)))
            {
                _out.WriteLine("Warning: Max transitions per injection must be set to some value between");
                _out.WriteLine("{0} and {1} for export strategies \"protein\" and \"buckets\" and for", AbstractMassListExporter.MAX_TRANS_PER_INJ_MIN, maxInstrumentTrans);
                _out.WriteLine("scheduled methods. You specified {1}. Defaulting to {0}.", AbstractMassListExporter.MAX_TRANS_PER_INJ_DEFAULT, args.MaxTransitionsPerInjection);

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
                    _out.WriteLine("Error: The template extension {0} does not match the expected extension for",extension);
                    _out.WriteLine("the instrument {0}. No method will be exported.", args.MethodInstrumentType);
                    return;
                }
            }

            var prediction = _doc.Settings.TransitionSettings.Prediction;
            double optimizeStepSize = 0;
            int optimizeStepCount = 0;

            if (Equals(args.OptimizeType, ExportOptimize.CE))
            {
                var regression = prediction.CollisionEnergy;
                optimizeStepSize = regression.StepSize;
                optimizeStepCount = regression.StepCount;
            }
            else if (Equals(args.OptimizeType, ExportOptimize.DP))
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
                            _out.WriteLine("Error: the {0} instrument lacks support for direct method export for", instrument);
                            _out.WriteLine("       triggered acquisition.");
                            _out.WriteLine("       You must export a {0} transition list and manually import it into", ExportInstrumentType.THERMO);
                            _out.WriteLine("       a method file using vendor software.");
                        }
                        else
                        {
                            _out.WriteLine("Error: the instrument type {0} does not support triggered acquisition.", instrument);
                        }
                    }
                    else if (!_doc.Settings.HasResults && !_doc.Settings.HasLibraries)
                    {
                        canTrigger = false;
                        _out.WriteLine("Error: triggered acquistion requires a spectral library or imported results");
                        _out.WriteLine("       in order to rank transitions.");
                    }
                    else if (!ExportInstrumentType.CanTrigger(instrument, _doc))
                    {
                        canTrigger = false;
                        _out.WriteLine("Error: The current document contains peptides without enough information");
                        _out.WriteLine("       to rank transitions for triggered acquisition.");
                    }
                    if (!canTrigger)
                    {
                        _out.WriteLine(Equals(type, ExportFileType.List)
                                               ? "No list will be exported."
                                               : "No method will be exported.");
                        return;
                    }
                    _exportProperties.PrimaryTransitionCount = args.PrimaryTransitionCount;
                }

                if (!ExportInstrumentType.CanSchedule(instrument, _doc))
                {
                    var predictionPep = _doc.Settings.PeptideSettings.Prediction;
                    if (!ExportInstrumentType.CanScheduleInstrumentType(instrument, _doc))
                    {
                        _out.WriteLine("Error: the specified instrument {0} is not compatible with scheduled methods.",
                                       instrument);
                    }
                    else if (predictionPep.RetentionTime == null)
                    {
                        if (predictionPep.UseMeasuredRTs)
                        {
                            _out.WriteLine("Error: to export a scheduled method, you must first choose a retention time");
                            _out.WriteLine("       predictor in Peptide Settings / Prediction, or import results for all");
                            _out.WriteLine("       peptides in the document.");
                        }
                        else
                        {
                            _out.WriteLine("Error: to export a scheduled method, you must first choose a retention time");
                            _out.WriteLine("       predictor in Peptide Settings / Prediction.");
                        }
                    }
                    else if (!predictionPep.RetentionTime.Calculator.IsUsable)
                    {
                        _out.WriteLine("Error: the retention time prediction calculator is unable to score.");
                        _out.WriteLine("       Check the calculator settings.");
                    }
                    else if (!predictionPep.RetentionTime.IsUsable)
                    {
                        _out.WriteLine("Error: the retention time predictor is unable to auto-calculate a regression.");
                        _out.WriteLine("       Check to make sure the document contains times for all of the required");
                        _out.WriteLine("       standard peptides.");
                    }
                    else
                    {
                        _out.WriteLine("Error: To export a scheduled method, you must first import results for all");
                        _out.WriteLine("       peptides in the document.");
                    }
                    _out.WriteLine(Equals(type, ExportFileType.List)
                                           ? "No list will be exported."
                                           : "No method will be exported.");
                    return;
                }

                if (Equals(args.ExportSchedulingAlgorithm, ExportSchedulingAlgorithm.Average))
                {
                    _exportProperties.SchedulingReplicateNum = 0;
                }
                else
                {
                    if(args.SchedulingReplicate.Equals("LAST"))
                    {
                        _exportProperties.SchedulingReplicateNum = _doc.Settings.MeasuredResults.Chromatograms.Count - 1;
                    }
                    else
                    {
                        //check whether the given replicate exists
                        if (!_doc.Settings.MeasuredResults.ContainsChromatogram(args.SchedulingReplicate))
                        {
                            _out.WriteLine("Error: the specified replicate {0} does not exist in the document.",
                                           args.SchedulingReplicate);
                            _out.WriteLine(Equals(type, ExportFileType.List)
                                                   ? "No list will be exported."
                                                   : "No method will be exported.");
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
                _out.WriteLine("Error: The file {0} could not be saved.", args.ExportPath);
                _out.WriteLine("       Check that the specified file directory exists and is writeable.");
                _out.WriteLine(x.Message);
                return;
            }

            _out.WriteLine(Equals(type, ExportFileType.List)
                               ? "List {0} exported successfully."
                               : "Method {0} exported successfully.",
                           Path.GetFileName(args.ExportPath));
        }

        public static void SaveDocument(SrmDocument doc, string outFile)
        {
            using (var writer = new XmlTextWriter(outFile, Encoding.UTF8) {Formatting = Formatting.Indented})
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
        public static SrmDocument ImportResults(SrmDocument doc, string docPath, string replicate, string dataFile,
                                                IProgressMonitor progressMonitor, out ProgressStatus status)
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
                    string dataFileNormalized = Path.GetFullPath(dataFile);
                    listChromatograms.Add(new ChromatogramSet(replicate, new[] { dataFileNormalized }));
                }

                var results = doc.Settings.HasResults
                                  ? doc.Settings.MeasuredResults.ChangeChromatograms(listChromatograms)
                                  : new MeasuredResults(listChromatograms);

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

        public void Dispose()
        {
            _out.Close();
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
                value = DateTime.Now.ToString("[yyyy/MM/dd HH:mm:ss]\t") + value;
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
            var waitBroker = new CommandWaitBroker(_out, new ProgressStatus(string.Empty));
            performExport(waitBroker);
        }
    }

    internal class NoMessageCommandWaitBroker : CommandWaitBroker
    {
        public NoMessageCommandWaitBroker(TextWriter outWriter, ProgressStatus status) : base(outWriter, status)
        {
        }

        protected override void WriteStatusMessage(string message)
        {
            // Do nothing
        }
    }

    internal class CommandWaitBroker : IProgressMonitor
    {
        private ProgressStatus _currentProgress;
        private readonly DateTime _waitStart;
        private DateTime _lastOutput;

        private readonly TextWriter _out;

        public CommandWaitBroker(TextWriter outWriter, ProgressStatus status)
        {
            _out = outWriter;
            _waitStart = _lastOutput = DateTime.Now;

            UpdateProgress(status);
        }

        bool IProgressMonitor.IsCanceled
        {
            get { return false; }
        }

        void IProgressMonitor.UpdateProgress(ProgressStatus status)
        {
            UpdateProgress(status);
        }

        public bool HasUI { get { return false; } }

        protected virtual void WriteStatusMessage(string message)
        {
            _out.WriteLine(message);
        }

        private void UpdateProgress(ProgressStatus status)
        {
            if (!string.IsNullOrEmpty(status.Message) &&
                (_currentProgress == null || !ReferenceEquals(status.Message, _currentProgress.Message)))
            {
                WriteStatusMessage(status.Message);
            }
            if (_currentProgress != null && status.PercentComplete != _currentProgress.PercentComplete)
            {
                // Show progress at least every 2 seconds and at 100%, if any other percentage
                // output has been shown.
                var currentTime = DateTime.Now;
                if ((currentTime - _lastOutput).Seconds > 2 || (status.PercentComplete == 100 && _lastOutput != _waitStart))
                {
                    _out.WriteLine("{0}%", status.PercentComplete);
                    _lastOutput = currentTime;
                }
            }
            _currentProgress = status;
        }
    }
}