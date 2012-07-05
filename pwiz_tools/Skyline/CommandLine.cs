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
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate.Query;
using pwiz.Skyline.Model.Results;
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
        private bool _importingTool;
        public bool ImportingTool
        {
            get { return !string.IsNullOrEmpty(ToolName) || _importingTool; }
            set { _importingTool = value; }
        }
        public bool? ResolveToolConflictsBySkipping { get; set; }

        // For keeping track of when an in command is required.
        public bool _requiresInCommand;

        // For --run-commands parameter
        public string CommandListPath { get; private set; }
        private bool _runningCommandList;
        public bool RunningCommandList
        {
            get { return !string.IsNullOrEmpty(CommandListPath) || _runningCommandList; }
            set { _runningCommandList = value; }
        }

        // For adding a skyr file to user.config
        public string SkyrPath { get; private set; }
        private bool _importingSkyr;
        public bool ImportingSkyr
        {
            get { return !string.IsNullOrEmpty(SkyrPath) || _importingSkyr; }
            set { _importingSkyr = value; }
        }
        public bool? ResolveSkyrConflictsBySkipping { get; set; }

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

        private readonly TextWriter _out;

        public CommandArgs(TextWriter output)
        {
            ResolveToolConflictsBySkipping = null;
            ResolveSkyrConflictsBySkipping = null;
            _out = output;

            ReportColumnSeparator = ',';
            MaxTransitionsPerInjection = AbstractMassListExporter.MAX_TRANS_PER_INJ_DEFAULT;
            OptimizeType = ExportOptimize.NONE;
            ExportStrategy = ExportStrategy.Single;
            ExportMethodType = ExportMethodType.Standard;
            DwellTime = AbstractMassListExporter.DWELL_TIME_DEFAULT;
            RunLength = AbstractMassListExporter.RUN_LENGTH_DEFAULT;
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

            foreach (string s in args)
            {
                var pair = new NameValuePair(s);
                    if (pair.Name.Equals("in"))
                    {
                        SkylineFile = Path.GetFullPath(pair.Value);
                        // Set requiresInCommand to be true so if SkylineFile is null or empty it still complains.
                        _requiresInCommand = true;
                    }

//                    // A command that exports all the tools to a text file in a SkylineRunner form for --run-commands
//                    else if (pair.Name.Equals("tool-list-export"))
//                    {
//                        string pathToOutputFile = pair.Value;
//                        using (StreamWriter sw = new StreamWriter(pathToOutputFile))
//                        {
//                            foreach (var tool in Settings.Default.ToolList)
//                            {
//                                sw.WriteLine("--tool-add=" + tool.Title +
//                                             " --tool-command=" + tool.Command +
//                                             " --tool-arguments=" + tool.Arguments +
//                                             " --tool-initial-dir=" + tool.InitialDirectory);
//                            }
//                        }
//                    }

                    // Import a skyr file.
                    else if (pair.Name.Equals("skyr-add"))
                    {
                        ImportingSkyr = true;
                        SkyrPath = pair.Value;
                    }

                    else if (pair.Name.Equals("resolve-skyr-conflicts"))
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

                    else if (pair.Name.Equals("tool-add"))
                    {
                        ImportingTool = true;
                        ToolName = pair.Value;
                    }

                    else if (pair.Name.Equals("tool-command"))
                    {
                        ImportingTool = true;
                        ToolCommand = pair.Value;
                    }

                    else if (pair.Name.Equals("tool-arguments"))
                    {
                        ImportingTool = true;
                        ToolArguments = pair.Value;
                    }

                    else if (pair.Name.Equals("tool-initial-dir"))
                    {
                        ImportingTool = true;
                        ToolInitialDirectory = pair.Value;
                    }

                    else if (pair.Name.Equals("resolve-tool-conflicts"))
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
                    else if (pair.Name.Equals("run-commands"))
                    {
                        CommandListPath = pair.Value;
                        RunningCommandList = true;
                    }

                    else if (pair.Name.Equals("save"))
                    {
                        Saving = true;
                        _requiresInCommand = true;
                    }

                    else if (pair.Name.Equals("out"))
                    {
                        SaveFile = Path.GetFullPath(pair.Value);
                        _requiresInCommand = true;
                    }

                    else if (pair.Name.Equals("import-file"))
                    {
                        ReplicateFile = Path.GetFullPath(pair.Value);
                        _requiresInCommand = true;
                    }

                    else if (pair.Name.Equals("import-replicate-name"))
                    {
                        ReplicateName = pair.Value;
                        _requiresInCommand = true;
                    }

                    else if (pair.Name.Equals("import-append"))
                    {
                        ImportAppend = true;
                        _requiresInCommand = true;
                    }

                    else if (pair.Name.Equals("import-all"))
                    {
                        ImportSourceDirectory = Path.GetFullPath(pair.Value);
                        _requiresInCommand = true;
                    }

                    else if (pair.Name.Equals("import-naming-pattern"))
                    {
                        var importNamingPatternVal = pair.Value;
                        _requiresInCommand = true;
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

                    else if (pair.Name.Equals("report-name"))
                    {
                        ReportName = pair.Value;
                        _requiresInCommand = true;
                    }

                    else if (pair.Name.Equals("report-file"))
                    {
                        ReportFile = Path.GetFullPath(pair.Value);
                        _requiresInCommand = true;
                    }

                    else if (pair.Name.Equals("report-format"))
                    {
                        if (pair.Value.Equals("TSV", StringComparison.CurrentCultureIgnoreCase))
                            ReportColumnSeparator = '\t';
                        else if (pair.Value.Equals("CSV", StringComparison.CurrentCultureIgnoreCase))
                            ReportColumnSeparator = TextUtil.GetCsvSeparator(CultureInfo.CurrentCulture);
                        else
                        {
                            _out.WriteLine(
                                "Warning: The report format {0} is invalid. It must be either \"CSV\" or \"TSV\".",
                                pair.Value);
                            _out.WriteLine("Defaulting to CSV.");
                            ReportColumnSeparator = TextUtil.GetCsvSeparator(CultureInfo.CurrentCulture);
                        }
                    }

                    else if (pair.Name.Equals("exp-translist-instrument"))
                    {
                        try
                        {
                            TransListInstrumentType = pair.Value;
                            _requiresInCommand = true;
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
                    else if (pair.Name.Equals("exp-method-instrument"))
                    {
                        try
                        {
                            MethodInstrumentType = pair.Value;
                            _requiresInCommand = true;
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
                    else if (pair.Name.Equals("exp-file"))
                    {
                        ExportPath = Path.GetFullPath(pair.Value);
                        _requiresInCommand = true;
                    }
                    else if (pair.Name.Equals("exp-strategy"))
                    {
                        ExportStrategySet = true;
                        _requiresInCommand = true;

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

                    else if (pair.Name.Equals("exp-method-type"))
                    {
                        var type = pair.Value;
                        _requiresInCommand = true;
                        if (type.Equals("scheduled", StringComparison.CurrentCultureIgnoreCase))
                        {
                            ExportMethodType = ExportMethodType.Scheduled;
                        }
                        else if (type.Equals("standard", StringComparison.CurrentCultureIgnoreCase))
                        {
                            //default
                        }
                        else
                        {
                            _out.WriteLine(
                                "Warning: The method type {0} is invalid. It must be \"standard\" or \"scheduled\".",
                                pair.Value);
                            _out.WriteLine("Defaulting to standard.");
                        }
                    }

                    else if (pair.Name.Equals("exp-max-trans"))
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
                        _requiresInCommand = true;
                    }

                    else if (pair.Name.Equals("exp-optimizing"))
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
                        _requiresInCommand = true;
                    }
                    else if (pair.Name.Equals("exp-scheduling-replicate"))
                    {
                        SchedulingReplicate = pair.Value;
                        _requiresInCommand = true;
                    }
                    else if (pair.Name.Equals("exp-template"))
                    {
                        TemplateFile = Path.GetFullPath(pair.Value);
                        _requiresInCommand = true;
                    }
                    else if (pair.Name.Equals("exp-ignore-proteins"))
                    {
                        IgnoreProteins = true;
                        _requiresInCommand = true;
                    }
                    else if (pair.Name.Equals("exp-dwell-time"))
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
                        _requiresInCommand = true;
                    }
                    else if (pair.Name.Equals("exp-add-energy-ramp"))
                    {
                        AddEnergyRamp = true;
                        _requiresInCommand = true;
                    }
                    else if (pair.Name.Equals("exp-run-length"))
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
                        _requiresInCommand = true;
                    }                
            }

            // First come the commands that do not depend on an --in command to run.
            // These commands modify Settings.Default instead of working with an open skyline document.

            if (ImportingTool)
            {                
                CommandLine.ImportTool(ToolName,ToolCommand,ToolArguments,ToolInitialDirectory, _out, ResolveToolConflictsBySkipping);                             
            }            
            if (RunningCommandList)
            {
                CommandLine.RunCommandList(CommandListPath, _out);
            }
            if (ImportingSkyr)
            {
                CommandLine.ImportSkyr(SkyrPath, _out, ResolveSkyrConflictsBySkipping);
            }
            
            // All commands that do not depend on an --in command have now run 

            // If skylineFile isn't set and one of the commands that requires --in is called, complain.
            if (String.IsNullOrEmpty(SkylineFile) && _requiresInCommand)
            {
                _out.WriteLine("Error: Use --in to specify a Skyline document to open.");
                return false;
            }
            // If _requiresInCommand was not set to true and all commands
            // that dont depend on --in have already been run, we are done.
            if (!_requiresInCommand)
            {
                // Returning false has the desired effect of exiting.
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

    }

    public class CommandLine : IDisposable
    {
        private readonly TextWriter _out;

        SrmDocument _doc;

        private ExportCommandProperties _exportProperties;

        public CommandLine(TextWriter output)
        {
            _out = output;
        }

        public void Run(string[] args)
        {

            var commandArgs = new CommandArgs(_out);

            if(!commandArgs.ParseArgs(args))
            {
                _out.WriteLine("Exiting...");
                return;
            }

            if (!OpenSkyFile(commandArgs.SkylineFile))
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
                                           commandArgs.SkylineFile,
                                           commandArgs.ImportAppend))
                        return;
                }
                else if(commandArgs.ImportingSourceDirectory)
                {
                    // If expected results are not imported successfully, terminate
                    if(!ImportResultsInDir(commandArgs.ImportSourceDirectory, commandArgs.ImportNamingPattern,
                                       commandArgs.SkylineFile))
                    	return;
                }
            }

            if (commandArgs.Saving)
            {
                SaveFile(commandArgs.SaveFile);
            }

            if (commandArgs.ExportingReport)
            {
                ExportReport(commandArgs.ReportName, commandArgs.ReportFile, commandArgs.ReportColumnSeparator);
            }

            if (!string.IsNullOrEmpty(commandArgs.TransListInstrumentType) &&
                !string.IsNullOrEmpty(commandArgs.MethodInstrumentType))
            {
                _out.WriteLine("Error: You cannot simultaneously export a transition list and a method.");
                _out.WriteLine("Neither will be exported. Please change your command line parameters.");
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

        public bool OpenSkyFile(string filePath)
        {
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open))
                {
                    XmlSerializer xmlSerializer = new XmlSerializer(typeof(SrmDocument));
                    _out.WriteLine("Opening file...");

                    _doc = (SrmDocument)xmlSerializer.Deserialize(stream);

                    _out.WriteLine("File {0} opened.", Path.GetFileName(filePath));
                }
            }
            catch (FileNotFoundException)
            {
                _out.WriteLine("Error: The Skyline file {0} does not exist.", filePath);
                return false;
            }
            catch (Exception x)
            {
                _out.WriteLine("Error: There was an error opening the file");
                _out.WriteLine("{0}", filePath);
                _out.WriteLine(XmlUtil.GetInvalidDataMessage(filePath, x));
                return false;
            }
            return true;
        }

        public bool ImportResultsInDir(string sourceDir, Regex namingPattern, string skylineFile)
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
                    if (!ImportResultsFile(file, replicateName, skylineFile))
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

        public bool ImportResultsFile(string replicateFile, string replicateName, string skylineFile, bool append)
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

            return ImportResultsFile(replicateFile, replicateName, skylineFile);
        }

        public bool ImportResultsFile(string replicateFile, string replicateName, string skylineFile)
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
            try
            {
                newDoc = ImportResults(_doc, skylineFile, replicateName, replicateFile, out status);
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
                if (status.ErrorException is InvalidDataException)
                {
                    if (status.ErrorException.Message.Contains("No SRM/MRM data found"))
                    {
                        _out.WriteLine("Warning: Failed importing the results file {0}.", replicateFile);
                        _out.WriteLine(status.ErrorException.Message);
                        _out.WriteLine("         Ignoring...");
                        return true;
                    }
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
                new MsDataFileImpl(replicatePath);  
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
                _out.WriteLine("use \"double quotes\" around your entire list of command parameters.");
                return;
            }

            _out.WriteLine("Exporting report {0}...", reportName);

            try
            {
                using (var saver = new FileSaver(reportFile))
                {
                    if (!saver.CanSave(false))
                    {
                        _out.WriteLine("Error: The report {0} could not be saved to {1}.", reportName, reportFile);
                        _out.WriteLine("Check to make sure it is not read-only.");
                    }

                    using (var writer = new StreamWriter(saver.SafeName))
                    {
                        Report report = Report.Load(reportSpec);

                        using (Database database = new Database(_doc.Settings)
                        {
                            LongWaitBroker = new CommandWaitBroker(_out),
                            PercentOfWait = 100
                        })
                        {
                            database.AddSrmDocument(_doc);

                            ResultSet resultSet = report.Execute(database);

                            ResultSet.WriteReportHelper(resultSet, reportColSeparator, writer,
                                                        CultureInfo.CurrentCulture);
                        }

                        writer.Flush();

                        writer.Close();
                    }

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
        public static void ImportTool (string title, string command, string arguments, string initialDirectory, TextWriter _out, bool? resolveToolConflictsBySkipping)
        {
            if (title == null | command == null)
            {
                _out.WriteLine("Error: to import a tool it must have a name and a command");
                _out.WriteLine("       Use --tool-add to specify a name");
                _out.WriteLine("       Use --tool-command to specify a command");
                _out.WriteLine("       The tool was not imported...");
                return;
            }
            // Check if the command is of a supported type.
            else if (!ConfigureToolsDlg.checkExtension(command))
            {
                string supportedTypes = String.Join("; ", ConfigureToolsDlg.EXTENSIONS);
                supportedTypes = supportedTypes.Replace(".", "*.");
                _out.WriteLine(string.Format("Error: the provided command for the tool {0} is not of a supported type", title));
                _out.WriteLine(string.Format("       Supported Types are: {0}", supportedTypes));
                _out.WriteLine("       The tool was not imported...");
                return;
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
                        _out.WriteLine(string.Format("Error: A tool titled {0} already exists.", tool.Title));
                        _out.WriteLine(              "       Please use --resolve-tool-conflicts=< overwrite | skip >");
                        _out.WriteLine(string.Format("       tool titled {0} was not added.", tool.Title));
                        return; // Dont add.
                    }
                    // Skip conflicts
                    if (resolveToolConflictsBySkipping == true)
                    {
                        _out.WriteLine(string.Format("Warning: skipping tool {0} due to a name conflict.", tool.Title));
//                        _out.WriteLine(string.Format("         tool {0} was not modified.", tool.Title));
                        return;
                    }
                    // Ovewrite conflicts
                    if (resolveToolConflictsBySkipping == false)
                    {
                        _out.WriteLine(string.Format("Warning: overwriting tool {0}", tool.Title));
//                      _out.WriteLine(string.Format("         tool {0} was modified.", tool.Title));
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
                _out.WriteLine(string.Format("{0} was added to the Tools Menu", title));
            }
            // Conflicts have been dealt with now add the tool.                       
            // Adding the tool. ToolArguments and ToolInitialDirectory are optional. 
            // If arguments or initialDirectory is null set it to be an empty string.
            arguments = arguments ?? ""; 
            initialDirectory = initialDirectory ?? ""; 
            Settings.Default.ToolList.Add(new ToolDescription(title, command, arguments, initialDirectory));
            Settings.Default.Save();        
        }

        // A function for running each line of a text file like a SkylineRunner command
        public static void RunCommandList(string path, TextWriter _out)
        {
            if (!File.Exists(path))
            {
                _out.WriteLine(string.Format("Error: {0} does not exist. --run-commands command failed.", path));
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
                            CommandArgs comandArgs = new CommandArgs(_out);
                            comandArgs.ParseArgs(args);
                        }
                    }
                }
                catch (Exception)
                {
                    _out.WriteLine(string.Format("Error: failed to open file {0} --run-commands command failed.", path));
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
                    else if (foundSingle)
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

        public static void ImportSkyr (string path, TextWriter _out, bool? resolveSkyrConflictsBySkipping)
        {          
            if (!File.Exists(path))
            {
                _out.WriteLine(string.Format("Error: {0} does not exist. --skyr-add command failed.", path));
            }
            else
            {           
                if (ShareListDlg<ReportSpecList, ReportSpec>.ImportFile(_out, Settings.Default.ReportSpecList, path, resolveSkyrConflictsBySkipping))
                {
                    Settings.Default.Save();
                    _out.WriteLine(string.Format("Success! Imported Reports from {0}", Path.GetFileNameWithoutExtension(path)));
                }           
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
                _out.WriteLine("in the document settings. Continuing exporting a transition");
                _out.WriteLine("list anyway...");
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

            if(Equals(args.ExportMethodType, ExportMethodType.Scheduled))
            {

                if (Equals(type, ExportFileType.Method) && !ExportInstrumentType.CanSchedule(args.MethodInstrumentType, _doc))
                {
                    _out.WriteLine("Error: your specified instrument {0} is not compatible with scheduled methods.",
                                   args.TransListInstrumentType);
                    _out.WriteLine("No method will be exported.");
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
                            _out.WriteLine("Error: the specified replicate {0} does not exist in your document.",
                                           args.SchedulingReplicate);
                            _out.WriteLine("No {0} will be exported.", Equals(type, ExportFileType.Method) ? "method" : "transition list");
                            return;
                        }

                        _exportProperties.SchedulingReplicateNum =
                            _doc.Settings.MeasuredResults.Chromatograms.IndexOf(
                                rep => rep.Name.Equals(args.SchedulingReplicate));
                    }
                }
            }

            var instrumentType = Equals(type, ExportFileType.List)
                                     ? args.TransListInstrumentType
                                     : args.MethodInstrumentType;

            try
            {
                _exportProperties.ExportFile(instrumentType, type, args.ExportPath, _doc, args.TemplateFile);
            }
            catch (IOException x)
            {
                _out.WriteLine("Error: The file could not be saved. Check that the specified file directory");
                _out.WriteLine("exists and is writeable.");
                _out.WriteLine(x.Message);
                return;
            }

            _out.WriteLine("The file was exported successfully.");
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
                    if (File.Exists(cachePath))
                        File.Delete(cachePath);
                }
            }
        }


        /// <summary>
        /// This function will add the given replicate, from dataFile, to the given document. If the replicate
        /// does not exist, it will be added. If it does exist, it will be appended to.
        /// </summary>
        public static SrmDocument ImportResults(SrmDocument doc, string docPath, string replicate, string dataFile, out ProgressStatus status)
        {
            var docContainer = new ResultsMemoryDocumentContainer(doc, docPath);

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

            var docAdded = doc.ChangeMeasuredResults(results);

            if (!docContainer.SetDocument(docAdded, doc, true))
            {
                throw new ApplicationException("Threading error while setting document after importing");
            }

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

    public class ExportCommandProperties : ExportProperties
    {
        private readonly TextWriter _out;

        public ExportCommandProperties(TextWriter output)
        {
            _out = output;
        }

        public override void PerformLongExport(Action<IProgressMonitor> performExport)
        {
            var waitBroker = new CommandWaitBroker(_out);
            new ProgressWaitBroker(performExport).PerformWork(waitBroker);
        }
    }


    internal class CommandWaitBroker : ILongWaitBroker
    {
        #region Implementation of ILongWaitBroker

        private int _currentProgress;
        private readonly DateTime _waitStart;
        private DateTime _lastOutput;

        private readonly TextWriter _out;

        public CommandWaitBroker(TextWriter outWriter)
        {
            _out = outWriter;
            _waitStart = _lastOutput = DateTime.Now;
        }

        bool ILongWaitBroker.IsCanceled
        {
            get { return false; }
        }

        int ILongWaitBroker.ProgressValue
        {
            get { return _currentProgress; }
            set
            {
                _currentProgress = value;
                var currentTime = DateTime.Now;
                // Show progress at least every 2 seconds and at 100%, if any other percentage
                // output has been shown.
                if ((currentTime - _lastOutput).Seconds > 2 || (_currentProgress == 100 && _lastOutput != _waitStart))
                {
                    _out.WriteLine("{0}%", _currentProgress);
                    _lastOutput = currentTime;
                }
            }
        }

        string ILongWaitBroker.Message
        {
            set { _out.WriteLine(value); }
        }

        bool ILongWaitBroker.IsDocumentChanged(SrmDocument docOrig)
        {
            return false;
        }

        DialogResult ILongWaitBroker.ShowDialog(Func<IWin32Window, DialogResult> show)
        {
            throw new InvalidOperationException("Attempt to show a window in command-line mode.");
        }

        #endregion
    }
}