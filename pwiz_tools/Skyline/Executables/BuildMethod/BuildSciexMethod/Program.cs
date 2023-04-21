/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using SCIEX.Apis.Control.v1;
using SCIEX.Apis.Control.v1.DeviceMethods;
using SCIEX.Apis.Control.v1.DeviceMethods.Properties;
using SCIEX.Apis.Control.v1.DeviceMethods.Requests;
using SCIEX.Apis.Control.v1.DeviceMethods.Responses;
using SCIEX.Apis.Control.v1.Security.Requests;
using SCIEX.Apis.Control.v1.Security.Responses;

namespace BuildSciexMethod
{
    class Program
    {
        public class UsageException : Exception
        {
            public UsageException() { }
            public UsageException(string message) : base(message) { }
        }

        private static void Main(string[] args)
        {
            try
            {
                Environment.ExitCode = -1;  // Failure until success
                using (var builder = new Builder())
                {
                    builder.ParseCommandArgs(args);
                    builder.BuildMethod();
                }
                Environment.ExitCode = 0;
            }
            catch (UsageException x)
            {
                if (!string.IsNullOrEmpty(x.Message))
                {
                    Console.Error.WriteLine(x.Message);
                    Console.Error.WriteLine();
                }
                Usage();
            }
            catch (Exception x)
            {
                Console.Error.WriteLine("ERROR: {0}", x.Message);
            }
        }

        private static void Usage()
        {
            const string usage =
                "Usage: BuildSciexMethod [options] <template method> [list file]*\n" +
                "   Takes template method file and a Skyline generated \n" +
                "   transition list as inputs, to generate a new method file\n" +
                "   as output.\n" +
                "   -d               Standard (unscheduled) method\n" +
                "   -t               SCIEX ZenoTOF 7600\n" +
                "   -o <output file> New method is written to the specified output file\n" +
                "   -s               Transition list is read from stdin.\n" +
                "                    e.g. cat TranList.csv | BuildSciexMethod -s -o new.ext temp.ext\n" +
                "\n" +
                "   -q               Export Sciex OS Quant method\n" +
                "   -m               Multiple lists concatenated in the format:\n" +
                "                    file1.ext\n" +
                "                    <transition list>\n" +
                "\n" +
                "                    file2.ext\n" +
                "                    <transition list>\n" +
                "                    ...";
            Console.Error.Write(usage);
        }
    }

    public class Builder : IDisposable
    {
        private const string ServiceUri = "net.tcp://localhost:63333/SciexControlApiService";
        private readonly ISciexControlApi _api = SciexControlApiFactory.Create();
        private readonly List<MethodTransitions> _methodTrans = new List<MethodTransitions>();

        private enum InstrumentType { QQQ, TOF }

        private InstrumentType Instrument { get; set; }

        private string TemplateMethod { get; set; }
        private bool StandardMethod { get; set; }
        private bool ScheduledMethod => !StandardMethod;
        private bool IsConnected { get; set; }
        private bool IsLoggedIn { get; set; }
        private bool IsSciexOsQuantMethod { get; set; }

        public Builder()
        {
            Instrument = InstrumentType.QQQ;
        }

        public void ParseCommandArgs(string[] args)
        {
            // Default to stdin for transition list input
            string outputMethod = null;
            var readStdin = false;
            var multiFile = false;

            var i = 0;
            while (i < args.Length && args[i][0] == '-')
            {
                switch (args[i++][1])
                {
                    case 'd':
                        StandardMethod = true;
                        break;
                    case 't':
                        Instrument = InstrumentType.TOF;
                        break;
                    case 'o':
                        if (i >= args.Length)
                            throw new Program.UsageException();
                        outputMethod = Path.GetFullPath(args[i++]);
                        break;
                    case 's':
                        readStdin = true;
                        break;
                    case 'm':
                        multiFile = true;
                        break;
                    case 'q':
                        IsSciexOsQuantMethod = true;
                        break;
                    default:
                        throw new Program.UsageException();
                }
            }

            if (multiFile && !string.IsNullOrEmpty(outputMethod))
                throw new Program.UsageException("Multi-file and specific output are not compatible.");

            var argcLeft = args.Length - i;
            if (argcLeft < 1 || (!readStdin && argcLeft < 2))
                throw new Program.UsageException();

            TemplateMethod = Path.GetFullPath(args[i++]);

            // Read input into a list of lists of fields
            if (readStdin)
            {
                if (!multiFile && string.IsNullOrEmpty(outputMethod))
                    throw new Program.UsageException("Reading from standard in without multi-file format must specify an output file.");

                ReadTransitions(Console.In, outputMethod);
            }
            else
            {
                for (; i < args.Length; i++)
                {
                    var inputFile = Path.GetFullPath(args[i]);
                    string filter = null;
                    if (inputFile.Contains("*"))
                        filter = Path.GetFileName(inputFile);
                    else if (Directory.Exists(inputFile))
                        filter = "*.csv";

                    if (string.IsNullOrEmpty(filter))
                        ReadFile(inputFile, outputMethod, multiFile);
                    else
                    {
                        var dirName = Path.GetDirectoryName(filter) ?? ".";
                        foreach (var fileName in Directory.GetFiles(dirName, filter))
                            ReadFile(Path.Combine(dirName, fileName), null, multiFile);
                    }
                }
            }
        }

        private void ReadFile(string inputFile, string outputMethod, bool multiFile)
        {
            if (!multiFile && string.IsNullOrEmpty(outputMethod))
            {
                var methodFileName = Path.GetFileNameWithoutExtension(inputFile) + ".msm";
                var dirName = Path.GetDirectoryName(inputFile);
                outputMethod = (dirName != null ? Path.Combine(dirName, methodFileName) : inputFile);
            }

            using (var infile = new StreamReader(inputFile))
            {
                ReadTransitions(infile, outputMethod);
            }
        }

        private void ReadTransitions(TextReader instream, string outputMethod)
        {
            var outputMethodCurrent = outputMethod;
            var finalMethod = outputMethod;
            var sb = new StringBuilder();

            string line;
            while ((line = instream.ReadLine()) != null)
            {
                line = line.Trim();

                if (string.IsNullOrEmpty(outputMethodCurrent))
                {
                    if (!string.IsNullOrEmpty(outputMethod))
                    {
                        // Only one file, if outputMethod specified
                        throw new IOException($"Failure creating method file {outputMethod}. Transition lists may not contain blank lines.");
                    }

                    // Read output file path from a line in the file
                    outputMethodCurrent = line;
                    finalMethod = instream.ReadLine();
                    if (finalMethod == null)
                        throw new IOException("Empty transition list found.");

                    sb = new StringBuilder();
                }
                else if (string.IsNullOrEmpty(line))
                {
                    _methodTrans.Add(new MethodTransitions(outputMethodCurrent, finalMethod, sb.ToString()));
                    outputMethodCurrent = null;
                }
                else
                {
                    sb.AppendLine(line);
                }
            }

            // Add the last method, if there is one
            if (!string.IsNullOrEmpty(outputMethodCurrent))
            {
                _methodTrans.Add(new MethodTransitions(outputMethodCurrent, finalMethod, sb.ToString()));
            }
        }

        private TResponse ExecuteAndCheck<TResponse>(IControlRequest<TResponse> request) where TResponse : class, IControlResponse, new()
        {
            var response = _api.Execute(request);
            Check(response);
            return response;
        }

        private static void Check(IControlResponse response)
        {
            if (response.IsSuccessful)
                return;

            // This error message actually means that the server is older version (< SCIEX OS 3.0) and will not support newer functionality.
            // It actually doesn’t impact any further functionality that was already supported previously, including 7500 method editing.
            if (response is ConnectionResponse && Equals(response.ErrorCode, "System-InternalServerError"))
                return;

            var msgs = new List<string> { $"{response.GetType().Name} failure." };

            if (!string.IsNullOrEmpty(response.ErrorCode))
                msgs.Add($"Error code: {response.ErrorCode}.");
            if (!string.IsNullOrEmpty(response.ErrorMessage))
                msgs.Add($"Error message: {response.ErrorMessage}.");
            if (response is MsMethodValidationResponse validationResponse && validationResponse.ValidationErrors.Length > 0)
                msgs.AddRange(validationResponse.ValidationErrors.Select(error => $"Validation error: {error}"));

            throw new Exception(string.Join(Environment.NewLine, msgs));
        }

        public void BuildMethod()
        {
            if (Instrument == InstrumentType.QQQ &&
                _methodTrans.Any(t => t.Transitions.Any(tran => !tran.ProductMz.HasValue)))
            {
                throw new Exception("All transitions must have product m/z.");
            }

            // Connect and login
            Check(_api.Connect(new ConnectByUriRequest(ServiceUri)));
            IsConnected = true;
            Check(_api.Login(new LoginCurrentUserRequest()));
            IsLoggedIn = true;

            foreach (var methodTranList in _methodTrans)
            {
                Console.Error.WriteLine($"MESSAGE: Exporting method {Path.GetFileName(methodTranList.FinalMethod)}");

                if (string.IsNullOrEmpty(methodTranList.TransitionList))
                    throw new IOException($"Failure creating method file {methodTranList.FinalMethod}. The transition list is empty.");

                try
                {
                    WriteToTemplate(methodTranList);
                }
                catch (Exception x)
                {
                    throw new IOException($"Failure creating method file {methodTranList.FinalMethod}. {x.Message}");
                }

                if (!File.Exists(methodTranList.OutputMethod))
                {
                    throw new IOException($"Failure creating method file {methodTranList.FinalMethod}.");
                }

                // Skyline uses a segmented progress status, which expects 100% for each
                // segment, with one segment per file.
                Console.Error.WriteLine("100%");
            }
        }

        private void WriteToTemplate(MethodTransitions transitions)
        {
            // Load template
            var loadResponse = ExecuteAndCheck(new MsMethodLoadRequest(TemplateMethod));
            var method = loadResponse.MsMethod;

            // Edit method
            var massTable = InitMethod(method, transitions.Transitions.Length);
            var props = PropertyData.GetAll(Instrument, StandardMethod, massTable).ToArray();
            for (var i = 0; i < transitions.Transitions.Length; i++)
            {
                foreach (var prop in props)
                    prop.UpdateRow(massTable.Rows[i], transitions.Transitions[i]);
            }

            // Validate and save method
            ExecuteAndCheck(new MsMethodValidationRequest(method));
            ExecuteAndCheck(new MsMethodSaveRequest(method, transitions.OutputMethod));
        }

        private PropertiesTable InitMethod(IMsMethod method, int numTransitions)
        {
            // Check that there is at least one experiment
            var experiment = method.Experiments.FirstOrDefault();
            if (experiment == null)
                throw new Exception("Method does not contain any experiments.");

            var props = experiment.Properties;
            var massTable = experiment.MassTable;
            
            if (Instrument == InstrumentType.TOF)
            {
                if (experiment.ExperimentParts.Count != 2 ||
                    experiment.ExperimentParts[0].ExperimentPartName != ExperimentPartName.TOFMS ||
                    experiment.ExperimentParts[1].ExperimentPartName != ExperimentPartName.TOFMSMS)
                    throw new Exception("Expected two experiment parts, TOF MS and TOF MSMS");
                
                var part = experiment.ExperimentParts[1];
                props = part.Properties;
                massTable = part.PropertiesTable;
            }

            // Set the method's IsScanScheduleAppliedProperty.
            var scheduledProp = props.TryGet<IsScanScheduleAppliedProperty>();
            if (scheduledProp == null)
                throw new Exception("Experiment does not have IsScanScheduleAppliedProperty.");
            scheduledProp.Value = ScheduledMethod;

            // Check that there is at least one row in the mass table.
            if (massTable.Rows.Count == 0)
                throw new Exception("Mass table does not contain any rows.");

            // Adjust number of rows in the mass table to match number of transitions
            if (massTable.Rows.Count < numTransitions)
                massTable.CloneAndAddRow(0, numTransitions - massTable.Rows.Count);
            else if (massTable.Rows.Count > numTransitions)
                massTable.RemoveRows(massTable.Rows.Skip(numTransitions).ToArray());

            return massTable;
        }

        public void Dispose()
        {
            if (IsLoggedIn)
                _api.Logout(new LogoutRequest());

            if (IsConnected)
                _api.Disconnect(new DisconnectRequest());
        }

        private class PropertyData
        {
            private static string RowGetterMethodName => "TryGet";
            private static MethodInfo RowGetter => typeof(PropertiesRow).GetMethod(RowGetterMethodName);

            private Type Property { get; }
            private Func<MethodTransition, object> GetValueForTransition { get; }
            private Func<PropertiesRow, object> GetPropertiesRowObj { get; }
            private PropertyInfo ValueProperty { get; }

            private PropertyData(Type property, Func<MethodTransition, object> getValueForTransition = null)
            {
                Property = property;
                GetValueForTransition = getValueForTransition;

                if (RowGetter == null)
                    throw new Exception($"PropertiesRow does not have method {RowGetterMethodName}.");
                GetPropertiesRowObj = (Func<PropertiesRow, object>)Delegate.CreateDelegate(typeof(Func<PropertiesRow, object>), RowGetter.MakeGenericMethod(property));

                const string valueProperty = "Value";
                ValueProperty = property.GetProperty(valueProperty);
                if (ValueProperty == null)
                    throw new Exception($"Property '{property.Name}' does not have a property named '{valueProperty}'.");
            }

            public void UpdateRow(PropertiesRow row, MethodTransition transition)
            {
                if (GetValueForTransition == null)
                    return;

                var rowPropObj = GetPropertiesRowObj(row);
                var newValue = GetValueForTransition(transition);
                ValueProperty.SetValue(rowPropObj, newValue);
            }

            public static IEnumerable<PropertyData> GetAll(InstrumentType instrument, bool standardMethod, PropertiesTable table)
            {
                var timeProp = typeof(RetentionTimeProperty);
                if (standardMethod)
                {
                    timeProp = Equals(instrument, InstrumentType.QQQ)
                        ? typeof(DwellTimeProperty)
                        : typeof(AccumulationTimeProperty);
                }

                return new[]
                {
                    new PropertyData(typeof(GroupIdProperty), t => t.Group),
                    new PropertyData(typeof(CompoundIdProperty), t => t.Label),
                    new PropertyData(typeof(Q1MassProperty), t => t.PrecursorMz),
                    new PropertyData(typeof(Q3MassProperty), t => t.ProductMz),
                    new PropertyData(typeof(PrecursorIonProperty), t => t.PrecursorMz),
                    new PropertyData(typeof(FragmentIonProperty), t => t.ProductMz),
                    new PropertyData(timeProp, t => t.DwellOrRt),
                    new PropertyData(typeof(RetentionTimeToleranceProperty), !standardMethod ? t => t.RTWindow : (Func<MethodTransition, object>)null),
                    new PropertyData(typeof(DeclusteringPotentialProperty), t => t.DP),
                    new PropertyData(typeof(EntrancePotentialProperty)),
                    new PropertyData(typeof(CollisionEnergyProperty), t => t.CE),
                    new PropertyData(typeof(CollisionCellExitPotentialProperty)),
                    new PropertyData(typeof(ElectronKeProperty)),
                }.Where(prop => prop.GetValueForTransition != null && prop.GetPropertiesRowObj(table.Rows[0]) != null);
            }

            public override string ToString() => Property.Name;
        }
    }

    public sealed class MethodTransitions
    {
        public MethodTransitions(string outputMethod, string finalMethod, string transitionList)
        {
            OutputMethod = outputMethod;
            FinalMethod = finalMethod;
            TransitionList = transitionList;

            var tmp = new List<MethodTransition>();
            var reader = new StringReader(TransitionList);
            string line;
            while ((line = reader.ReadLine()) != null)
                tmp.Add(new MethodTransition(line));
            Transitions = tmp.ToArray();

            // Sciex requires that compound IDs are unique, so fix if necessary
            var idSet = new HashSet<string>();
            var needRename = new Dictionary<string, int>();
            foreach (var transition in Transitions)
            {
                if (!needRename.ContainsKey(transition.Label) && !idSet.Add(transition.Label))
                    needRename.Add(transition.Label, 0);
            }
            foreach (var transition in Transitions)
            {
                if (!needRename.TryGetValue(transition.Label, out var i))
                    continue;
                needRename[transition.Label]++;
                transition.Label += "_" + i;
            }
        }

        public string OutputMethod { get; }
        public string FinalMethod { get; }
        public string TransitionList { get; }
        public MethodTransition[] Transitions { get; }
    }

    public sealed class MethodTransition
    {
        private static readonly Dictionary<int, Action<MethodTransition, string>> Columns = new Dictionary<int, Action<MethodTransition, string>>
        {
            [0] = (t, s) => { t.PrecursorMz = double.Parse(s, CultureInfo.InvariantCulture); },
            [1] = (t, s) => { t.ProductMz = string.IsNullOrEmpty(s) ? (double?)null : double.Parse(s, CultureInfo.InvariantCulture); },
            [2] = (t, s) => { t.DwellOrRt = double.Parse(s, CultureInfo.InvariantCulture); },
            [3] = (t, s) => { t.Label = s; },
            [4] = (t, s) => { t.DP = double.Parse(s, CultureInfo.InvariantCulture); },
            [5] = (t, s) => { t.CE = double.Parse(s, CultureInfo.InvariantCulture); },
            [6] = (t, s) => { t.PrecursorWindow = string.IsNullOrEmpty(s) ? (double?)null : double.Parse(s, CultureInfo.InvariantCulture); },
            [7] = (t, s) => { t.ProductWindow = string.IsNullOrEmpty(s) ? (double?)null : double.Parse(s, CultureInfo.InvariantCulture); },
            [8] = (t, s) => { t.Group = s; },
            [9] = (t, s) => { t.AveragePeakArea = string.IsNullOrEmpty(s) ? (float?)null : float.Parse(s, CultureInfo.InvariantCulture); },
            [10] = (t, s) => { t.RTWindow = string.IsNullOrEmpty(s) ? (double?)null : 60 * double.Parse(s, CultureInfo.InvariantCulture); },
            [11] = (t, s) => { t.Threshold = string.IsNullOrEmpty(s) ? (double?)null : double.Parse(s, CultureInfo.InvariantCulture); },
            [12] = (t, s) => { t.Primary = string.IsNullOrEmpty(s) ? (int?)null : int.Parse(s, CultureInfo.InvariantCulture); },
            [13] = (t, s) => { t.CoV = string.IsNullOrEmpty(s) ? (double?)null : double.Parse(s, CultureInfo.InvariantCulture); },
        };

        public MethodTransition(string transitionListLine)
        {
            var values = transitionListLine.Split(',');
            if (values.Length < 6)
                throw new IOException("Invalid transition list format. Each line must at least have 6 values.");
            for (var i = 0; i < values.Length; i++)
            {
                try
                {
                    if (Columns.TryGetValue(i, out var func))
                        func(this, values[i]);
                }
                catch (FormatException)
                {
                    throw new IOException($"Invalid transition list format. Error parsing value '{values[i]}' in column {i}.");
                }
            }
        }

        public double PrecursorMz { get; private set; }
        public double? ProductMz { get; private set; }
        public double DwellOrRt { get; private set; }
        public string Label { get; set; }
        public double CE { get; private set; }
        public double DP { get; private set; }
        public double? PrecursorWindow { get; private set; }
        public double? ProductWindow { get; private set; }
        public double? Threshold { get; private set; }
        public int? Primary { get; private set; }
        public string Group { get; private set; }
        public float? AveragePeakArea { get; private set; }
        public double? RTWindow { get; private set; }
        public double? CoV { get; private set; }

        public override string ToString()
        {
            // For debugging
            return $"{Label}; Q1={PrecursorMz}; Q3={ProductMz}; DwellOrRT={DwellOrRt}";
        }
    }
}
