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
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate.Query;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline
{
    internal class CommandArgs
    {
        public string SkylineFile { get; private set; }
        public string ReplicateFile { get; private set; }
        public string ReplicateName { get; private set; }
        public bool ImportingResults
        {
            get { return !string.IsNullOrEmpty(ReplicateFile); }
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
            get { return !String.IsNullOrEmpty(TransListInstrumentType);  }
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
                if (ExportOptimize.OptimizeTypes.Any(opt => opt.Equals(value)))
                {
                    _optimizeType = value;
                }
                else
                {
                    throw new ArgumentException(string.Format("The instrument parameter {0} is not valid for optimization.", value));
                }
            }
        }

        public ExportMethodType ExportMethodType { get; private set; }

        public string TemplateFile { get; private set; }

        public ExportSchedulingAlgorithm ExportSchedulingAlgorithm { get; private set; }

        public bool SchedulingReplicateSet { get; private set; }
        public int SchedulingReplicateIndex { get; private set; }

        public bool IgnoreProteins { get; private set; }

        private int _dwellTime;
        public int DwellTime
        {
            get { return _dwellTime; }
            set
            {
                if (value < MassListExporter.DWELL_TIME_MIN || value > MassListExporter.DWELL_TIME_MAX)
                {
                    throw new ArgumentException(string.Format("The dwell time {0} must be between {1} and {2}.", value, MassListExporter.DWELL_TIME_MIN, MassListExporter.DWELL_TIME_MAX));
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
                if (value < MassListExporter.RUN_LENGTH_MIN || value > MassListExporter.RUN_LENGTH_MAX)
                {
                    throw new ArgumentException(string.Format("The run length {0} must be between {1} and {2}.", value, MassListExporter.RUN_LENGTH_MIN, MassListExporter.RUN_LENGTH_MAX));
                }
                _runLength = value;
            }
        }

        public string TransListFile { get; private set; }

        public CommandArgs()
        {
            SetDefaults();
        }

        public void SetDefaults()
        {
            ReportColumnSeparator = ',';
            MaxTransitionsPerInjection = MassListExporter.MAX_TRANS_PER_INJ_DEFAULT;
            OptimizeType = ExportOptimize.NONE;
            ExportStrategy = ExportStrategy.Single;
            ExportMethodType = ExportMethodType.Standard;
            //This is a flag
            SchedulingReplicateIndex = -1;
            ExportSchedulingAlgorithm = ExportSchedulingAlgorithm.Average;
            DwellTime = MassListExporter.DWELL_TIME_DEFAULT;
            RunLength = MassListExporter.RUN_LENGTH_DEFAULT;
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


        public bool ParseArgs(string[] args, TextWriter output)
        {
            foreach (string s in args)
            {
                var pair = new NameValuePair(s);

                if (pair.Name.Equals("in"))
                {
                    SkylineFile = pair.Value;
                }

                else if (pair.Name.Equals("import"))
                {
                    ReplicateFile = pair.Value;
                }

                else if (pair.Name.Equals("replicate"))
                {
                    ReplicateName = pair.Value;
                }

                else if (pair.Name.Equals("out"))
                {
                    SaveFile = pair.Value;
                }

                else if (pair.Name.Equals("save"))
                {
                    Saving = true;
                }

                else if (pair.Name.Equals("report"))
                {
                    ReportName = pair.Value;
                }

                else if (pair.Name.Equals("separator"))
                {
                    if (pair.Value.Equals("TAB"))
                        ReportColumnSeparator = '\t';
                    else
                        ReportColumnSeparator = pair.Value[0];
                }

                else if (pair.Name.Equals("reportfile"))
                {
                    ReportFile = pair.Value;
                }

                else if (pair.Name.Equals("exp-translist-instrument"))
                {
                    try
                    {
                        TransListInstrumentType = pair.Value;
                    } catch (ArgumentException)
                    {
                        output.WriteLine("Error: {0} is not a valid instrument type. Please choose from:", pair.Value);
                        foreach(string str in ExportInstrumentType.TRANSITION_LIST_TYPES)
                        {
                            output.WriteLine(str);
                        }
                        output.WriteLine("No transition list will be exported.");
                    }
                }
                else if (pair.Name.Equals("exp-method-instrument"))
                {
                    try
                    {
                        MethodInstrumentType = pair.Value;
                    }
                    catch (ArgumentException)
                    {
                        output.WriteLine("Error: {0} is not a valid instrument type. Please choose from:", pair.Value);
                        foreach (string str in ExportInstrumentType.METHOD_TYPES)
                        {
                            output.WriteLine(str);
                        }
                        output.WriteLine("No method will be exported.");
                    }
                }
                else if (pair.Name.Equals("exp-strategy"))
                {
                    ExportStrategySet = true;

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
                        output.WriteLine("Warning: export strategy must be one of \"single\", \"protein\" or \"buckets\".");
                        output.WriteLine("Defaulting to \"single\".");
                        //already set to Single
                    }
                }

                else if (pair.Name.Equals("exp-max-trans"))
                {
                    try
                    {
                        MaxTransitionsPerInjection = pair.ValueInt;
                    }
                    catch
                    {
                        output.WriteLine("Warning: invalid max transitions per injection parameter.");
                        output.WriteLine("Defaulting to " + MassListExporter.MAX_TRANS_PER_INJ_DEFAULT + ".");
                        MaxTransitionsPerInjection = MassListExporter.MAX_TRANS_PER_INJ_DEFAULT;
                    }
                }

                else if (pair.Name.Equals("exp-optimizing"))
                {
                    try
                    {
                        OptimizeType = pair.Value;
                    } catch (ArgumentException) {
                        output.WriteLine("Warning: invalid optimization parameter. Use \"ce\", \"dp\", or \"none\".");
                        output.WriteLine("Defaulting to none.");
                    }
                }
                else if (pair.Name.Equals("exp-method-type"))
                {
                    var type = pair.Value;
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
                        output.WriteLine("Warning: export method type must be \"standard\" or \"scheduled\".");
                        output.WriteLine("Defaulting to standard.");
                    }
                }
                else if (pair.Name.Equals("exp-scheduling-algorithm"))
                {
                    var sAlg = pair.Value;

                    if (sAlg.Equals("single", StringComparison.CurrentCultureIgnoreCase))
                    {
                        ExportSchedulingAlgorithm = ExportSchedulingAlgorithm.Single;
                    }
                    else if (sAlg.Equals("average", StringComparison.CurrentCultureIgnoreCase))
                    {
                        //defaults to average
                    }
                    else
                    {
                        output.WriteLine("Warning: export scheduling algorithm must be \"single\" or \"average\".");
                        output.WriteLine("Defaulting to average.");
                    }
                }
                else if (pair.Name.Equals("exp-scheduling-replicate-index"))
                {
                    SchedulingReplicateSet = true;

                    try
                    {
                        SchedulingReplicateIndex = pair.ValueInt;
                    }
                    catch
                    {
                        output.WriteLine("Warning: invalid scheduling replicate index parameter.");
                        output.WriteLine("Defaulting to the last replicate in the document.");
                    }
                }
                else if (pair.Name.Equals("exp-template"))
                {
                    TemplateFile = pair.Value;
                }
                else if (pair.Name.Equals("exp-ignore-proteins"))
                {
                    IgnoreProteins = true;
                }
                else if (pair.Name.Equals("exp-dwelltime"))
                {
                    try
                    {
                        DwellTime = pair.ValueInt;
                    }
                    catch
                    {
                        output.Write("Warning: dwell time must be between {0} and {1}. Defaulting to {2}",
                            MassListExporter.DWELL_TIME_MIN, MassListExporter.DWELL_TIME_MAX, MassListExporter.DWELL_TIME_DEFAULT);
                    }
                }
                else if (pair.Name.Equals("exp-addenergyramp"))
                {
                    AddEnergyRamp = true;
                }
                else if (pair.Name.Equals("exp-runlength"))
                {
                    try
                    {
                        RunLength = pair.ValueInt;
                    }
                    catch
                    {
                        output.Write("Warning: run length must be between {0} and {1}. Defaulting to {2}",
                            MassListExporter.RUN_LENGTH_MIN, MassListExporter.RUN_LENGTH_MAX, MassListExporter.RUN_LENGTH_DEFAULT);
                    }
                }
                else if (pair.Name.Equals("exp-translist-out"))
                {
                    TransListFile = pair.Value;
                }
            }

            if (String.IsNullOrEmpty(SkylineFile))
            {
                output.WriteLine("Error: Use --in to specify a Skyline document to open.");
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

    internal class CommandLine : IDisposable
    {
        private readonly TextWriter _out;

        SrmDocument _doc;

        public CommandLine(TextWriter output)
        {
            _out = output;
        }

        public void Run(string[] args)
        {

            var commandArgs = new CommandArgs();

            if(!commandArgs.ParseArgs(args, _out))
                return;

            if (!OpenSkyFile(commandArgs.SkylineFile))
                return;

            if (commandArgs.ImportingResults)
            {
                ImportResultsFile(commandArgs.ReplicateFile, commandArgs.ReplicateName, commandArgs.SkylineFile);
            }

            if (commandArgs.Saving)
            {
                SaveFile(commandArgs.SaveFile);
            }

            if (commandArgs.ExportingReport)
            {
                ExportReport(commandArgs.ReportName, commandArgs.ReportFile, commandArgs.ReportColumnSeparator);
            }

            if (commandArgs.ExportingTransitionList)
            {
                ExportTransList(commandArgs);
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

                    try
                    {
                        _doc = (SrmDocument)xmlSerializer.Deserialize(stream);
                    }
                    catch (Exception)
                    {
                        _out.WriteLine("Error opening file: {0}", filePath);
                        return false;
                    }

                    _out.WriteLine("File {0} opened.", Path.GetFileName(filePath));
                }
            }
            catch (FileNotFoundException)
            {
                _out.WriteLine("Error: The Skyline file {0} does not exist.", filePath);
                return false;
            }
            catch (InvalidDataException x)
            { 
                _out.WriteLine("Error: There was an error opening the file");
                _out.WriteLine("{0}", filePath);
                _out.WriteLine(XmlUtil.GetInvalidDataMessage(filePath, x));
                return false;
            } catch (Exception)
            {
                _out.WriteLine("Error: There was an unanticipated error opening the file");
                _out.WriteLine("{0}", filePath);
            }
            return true;
        }

        public void ImportResultsFile(string replicateFile, string replicateName, string skylineFile)
        {
            if (string.IsNullOrEmpty(replicateName))
                replicateName = Path.GetFileNameWithoutExtension(replicateFile);

            _out.WriteLine("Adding results...");

            SrmDocument newDoc = ImportResults(_doc, skylineFile, replicateName, replicateFile);

            if (ReferenceEquals(_doc, newDoc))
            {
                _out.WriteLine("There was an error importing the results file {0}.", replicateFile);
                return;
            }

            _doc = newDoc;

            _out.WriteLine("Results added from {0}.", Path.GetFileName(replicateFile));
            //the replicate was added successfully
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
                _out.WriteLine("--reportfile=path/to/file.csv parameter.");
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

            using (var saver = new FileSaver(reportFile))
            {
                if (!saver.CanSave(false))
                {
                    _out.WriteLine("Error: the file {0} could not be saved.", reportFile);
                    _out.WriteLine("Check to make sure it is not read-only.");
                }

                using (var writer = new StreamWriter(saver.SafeName))
                {
                    Report report = Report.Load(reportSpec);

                    Database database = new Database(_doc.Settings)
                    {
                        LongWaitBroker = new CommandWaitBroker(_out),
                        PercentOfWait = 100
                    };

                    database.AddSrmDocument(_doc);

                    ResultSet resultSet = report.Execute(database);
                    
                    ResultSet.WriteReportHelper(resultSet, reportColSeparator, writer,
                                                      CultureInfo.CurrentCulture);

                    writer.Flush();

                    writer.Close();
                }

                saver.Commit();
                _out.WriteLine("Report {0} exported successfully.", reportName);
            }
        }

        // This function needs so many variables, we might as well just pass the whole CommandArgs object
        public void ExportTransList(CommandArgs args)
        {

            if (String.IsNullOrEmpty(args.TransListFile))
            {
                _out.WriteLine("Error: You must specify a transition list file to write to with the");
                _out.WriteLine("--exp-translist-out=path/to/file.csv parameter. No transition list");
                _out.WriteLine("will be exported.");
                return;
            }

            if (!args.ExportStrategySet)
            {
                _out.WriteLine("Warning: No export strategy specified (from \"single\", \"protein\" or");
                _out.WriteLine("\"buckets\"). Defaulting to \"single\".");
                args.ExportStrategy = ExportStrategy.Single;
            }

            int maxInstrumentTrans = _doc.Settings.TransitionSettings.Instrument.MaxTransitions ??
                     TransitionInstrument.MAX_TRANSITION_MAX;

            if ((args.MaxTransitionsPerInjection < MassListExporter.MAX_TRANS_PER_INJ_MIN ||
                 args.MaxTransitionsPerInjection > maxInstrumentTrans) &&
                    (Equals(args.ExportStrategy, ExportStrategy.Buckets) ||
                     Equals(args.ExportStrategy, ExportStrategy.Protein)))
            {
                _out.WriteLine("Warning: Max transitions per injection must be set to some value between");
                _out.WriteLine(" {0} and {1} for export strategies \"protein\" and \"buckets\".", MassListExporter.MAX_TRANS_PER_INJ_MIN, maxInstrumentTrans);
                _out.WriteLine("Defaulting to {0}.", MassListExporter.MAX_TRANS_PER_INJ_DEFAULT);

                args.MaxTransitionsPerInjection = MassListExporter.MAX_TRANS_PER_INJ_DEFAULT;
            }

            MassListExporter exporter;

            switch (args.TransListInstrumentType)
            {
                case ExportInstrumentType.ABI:
                    if (args.DwellTime < MassListExporter.DWELL_TIME_MIN || args.DwellTime > MassListExporter.DWELL_TIME_MAX)
                    {
                        _out.WriteLine("Warning: Missing or invalid dwell time parameter. This parameter is needed");
                        _out.WriteLine("to export an AB Sciex transition list and must be between {0} and {1}.",
                            MassListExporter.DWELL_TIME_MIN, MassListExporter.DWELL_TIME_MAX);
                        _out.WriteLine("Defaulting to {0}", MassListExporter.DWELL_TIME_DEFAULT);
                        args.DwellTime = MassListExporter.DWELL_TIME_DEFAULT;
                    }

                    exporter = new AbiMassListExporter(_doc) { DwellTime = args.DwellTime };
                    break;
                case ExportInstrumentType.Agilent:
                    if (args.DwellTime < MassListExporter.DWELL_TIME_MIN || args.DwellTime > MassListExporter.DWELL_TIME_MAX)
                    {
                        _out.WriteLine("Warning: Missing or invalid dwell time parameter. This parameter is needed");
                        _out.WriteLine("to export an Agilent transition list and must be between {0} and {1}.",
                            MassListExporter.DWELL_TIME_MIN, MassListExporter.DWELL_TIME_MAX);
                        _out.WriteLine("Defaulting to {0}", MassListExporter.DWELL_TIME_DEFAULT);
                        args.DwellTime = MassListExporter.DWELL_TIME_DEFAULT;
                    }

                    exporter = new AgilentMassListExporter(_doc) { DwellTime = args.DwellTime };
                    break;
                case ExportInstrumentType.Thermo:
                    exporter = new ThermoMassListExporter(_doc) { AddEnergyRamp = args.AddEnergyRamp };
                    break;
                case ExportInstrumentType.Waters:
                    if (args.RunLength < MassListExporter.RUN_LENGTH_MIN || args.RunLength > MassListExporter.RUN_LENGTH_MAX)
                    {
                        _out.WriteLine("Warning: Missing or invalid run length parameter. This parameter is needed");
                        _out.WriteLine("to export a Waters transition list and must be between {0} and {1}.",
                                       MassListExporter.RUN_LENGTH_MIN, MassListExporter.RUN_LENGTH_MAX);
                        _out.WriteLine("Defaulting to {0}", MassListExporter.RUN_LENGTH_DEFAULT);
                        args.RunLength = MassListExporter.RUN_LENGTH_DEFAULT;
                    }

                    exporter = new WatersMassListExporter(_doc) { RunLength = args.RunLength };
                    break;
                default:
                    //this should never happen
                    _out.WriteLine("Error: Instrument vendor must be one of:");
                    foreach (string vendor in ExportInstrumentType.TRANSITION_LIST_TYPES)
                    {
                        _out.WriteLine(vendor);
                    }
                    _out.WriteLine("No transition list will be exported.");
                    return;
            }

            exporter.Strategy = args.ExportStrategy;
            exporter.IgnoreProteins = args.IgnoreProteins;
            exporter.MaxTransitions = args.MaxTransitionsPerInjection;
            //exporter.MethodType = args.ExportMethodType;

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

            exporter.OptimizeType = args.OptimizeType;
            exporter.OptimizeStepSize = optimizeStepSize;
            exporter.OptimizeStepCount = optimizeStepCount;
            exporter.SchedulingAlgorithm = args.ExportSchedulingAlgorithm;

            //the default is average
            if (Equals(args.ExportSchedulingAlgorithm, ExportSchedulingAlgorithm.Average))
            {
                exporter.SchedulingReplicateIndex = null;
            }
            else if (args.SchedulingReplicateSet)
            {
                if (args.SchedulingReplicateIndex < 1)
                {
                    //a warning has already been printed by CommandArgs.ParseArgs
                    //default to the last replicate
                    exporter.SchedulingReplicateIndex = _doc.Settings.MeasuredResults.Chromatograms.Count - 1;
                }
                else
                {
                    if (args.SchedulingReplicateIndex > _doc.Settings.MeasuredResults.Chromatograms.Count)
                    {
                        _out.WriteLine("Warning: The specified replicate index ({0}) is greater than the number of", args.SchedulingReplicateIndex);
                        _out.WriteLine("replicates in the document ({0}). Defaulting to the last replicate in the document.");
                        exporter.SchedulingReplicateIndex = _doc.Settings.MeasuredResults.Chromatograms.Count - 1;
                    }
                    else
                    {

                        exporter.SchedulingReplicateIndex = args.SchedulingReplicateIndex;
                    }
                }
            }
            else
            {
                _out.WriteLine("Warning: No scheduling replicate is set. Defaulting to the last replicate");
                _out.WriteLine("in the document.");
                exporter.SchedulingReplicateIndex = _doc.Settings.MeasuredResults.Chromatograms.Count - 1;
            }

            if (!CheckInstrument(args.TransListInstrumentType, _doc))
            {
                _out.WriteLine("Warning: The specified instrument vendor does not match the vendor");
                _out.WriteLine("in the settings of the document. Continuing exporting a transition");
                _out.WriteLine("list anyway...");
            }

            try
            {
                exporter.Export(args.TransListFile);
                _out.WriteLine("The transition list was exported successfully.");
            }
            catch (Exception)
            {
                _out.WriteLine("Error: The file could not be saved. Check that the specified file directory");
                _out.WriteLine("exists and is writeable.");
                return;
            }
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

        public static SrmDocument ImportResults(SrmDocument doc, string docPath, string replicate, string dataFile)
        {
            var docContainer = new ResultsMemoryDocumentContainer(doc, docPath);

            var listChromatograms = new List<ChromatogramSet>();

            if (doc.Settings.HasResults)
                listChromatograms.AddRange(doc.Settings.MeasuredResults.Chromatograms);

            listChromatograms.Add(new ChromatogramSet(replicate, new[] {dataFile}));

            var results = doc.Settings.HasResults
                              ? doc.Settings.MeasuredResults.ChangeChromatograms(listChromatograms)
                              : new MeasuredResults(listChromatograms);

            var docAdded = doc.ChangeMeasuredResults(results);

            docContainer.SetDocument(docAdded, doc, true);

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
            if (!Equals(instrument, ExportInstrumentType.Thermo_LTQ))
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


    internal class CommandWaitBroker : ILongWaitBroker
    {
        #region Implementation of ILongWaitBroker

        private int _currentProgress;
        private DateTime _waitStart;
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