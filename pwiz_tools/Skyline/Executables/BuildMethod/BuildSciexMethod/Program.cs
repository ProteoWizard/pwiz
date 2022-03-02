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
using System.Text;
using SCIEX.Apis.Control.v1;
using SCIEX.Apis.Control.v1.DeviceMethods;
using SCIEX.Apis.Control.v1.DeviceMethods.Properties;
using SCIEX.Apis.Control.v1.DeviceMethods.Requests;
using SCIEX.Apis.Control.v1.Security.Requests;

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
                "   -o <output file> New method is written to the specified output file\n" +
                "   -s               Transition list is read from stdin.\n" +
                "                    e.g. cat TranList.csv | BuildSciexMethod -s -o new.ext temp.ext\n" +
                "\n" +
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

        private string TemplateMethod { get; set; }

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
                    default:
                        throw new Program.UsageException();
                }
            }

            if (multiFile && !string.IsNullOrEmpty(outputMethod))
                throw new Program.UsageException("Multi-file and specific output are not compatibile.");

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
                        throw new IOException(string.Format("Failure creating method file {0}. Transition lists may not contain blank lines.", outputMethod));
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

            var msg = new StringBuilder();
            msg.AppendFormat("{0} failure.", response.GetType().Name);
            if (!string.IsNullOrEmpty(response.ErrorCode))
                msg.AppendFormat(" Error code: {0}.", response.ErrorCode);
            if (!string.IsNullOrEmpty(response.ErrorMessage))
                msg.AppendFormat(" Error message: {0}.", response.ErrorMessage);
            throw new Exception(msg.ToString());
        }

        public void BuildMethod()
        {
            // Connect and login
            Check(_api.Connect(new ConnectByUriRequest(ServiceUri)));
            Check(_api.Login(new LoginCurrentUserRequest()));

            foreach (var methodTranList in _methodTrans)
            {
                Console.Error.WriteLine($"MESSAGE: Exporting method {Path.GetFileName(methodTranList.FinalMethod)}");

                if (string.IsNullOrEmpty(methodTranList.TransitionList))
                    throw new IOException(string.Format("Failure creating method file {0}. The transition list is empty.", methodTranList.FinalMethod));

                try
                {
                    WriteToTemplate(methodTranList);
                }
                catch (Exception x)
                {
                    throw new IOException(string.Format("Failure creating method file {0}.  {1}", methodTranList.FinalMethod, x.Message));
                }

                if (!File.Exists(methodTranList.OutputMethod))
                {
                    throw new IOException(string.Format("Failure creating method file {0}.", methodTranList.FinalMethod));
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
            if (method.Experiments.Count == 0)
                throw new Exception("Method does not contain any experiments.");
            var massTable = method.Experiments[0].MassTable;
            switch (massTable.Rows.Count)
            {
                case 0:
                    throw new Exception("Mass table does not contain any rows.");
                case 1:
                    break;
                default:
                    massTable.RemoveRows(massTable.Rows.Skip(1).ToArray());
                    break;
            }
            massTable.CloneAndAddRow(0, transitions.Transitions.Length - 1);

            const string rowGetterMethodName = "TryGet";
            var rowGetter = typeof(PropertiesRow).GetMethod(rowGetterMethodName);
            if (rowGetter == null)
                throw new Exception(string.Format("PropertiesRow does not have method {0}.", rowGetterMethodName));
            var allProps = new[]
                {
                    typeof(GroupIdProperty), typeof(CompoundIdProperty),
                    typeof(Q1MassProperty), typeof(Q3MassProperty),
                    typeof(DwellTimeProperty),
                    typeof(DeclusteringPotentialProperty), typeof(EntrancePotentialProperty),
                    typeof(CollisonEnergyProperty), typeof(CollisionCellExitPotentialProperty)
                }
                .Where(prop => rowGetter.MakeGenericMethod(prop).Invoke(massTable.Rows[0], null) != null)
                .ToList();

            for (var i = 0; i < transitions.Transitions.Length; i++)
            {
                var row = massTable.Rows[i];
                var transition = transitions.Transitions[i];
                foreach (var prop in allProps)
                {
                    if (prop == typeof(GroupIdProperty))
                        row.TryGet<GroupIdProperty>().Value = transition.Group;
                    else if (prop == typeof(CompoundIdProperty))
                        row.TryGet<CompoundIdProperty>().Value = transition.Label;
                    else if (prop == typeof(Q1MassProperty))
                        row.TryGet<Q1MassProperty>().Value = transition.PrecursorMz;
                    else if (prop == typeof(Q3MassProperty))
                        row.TryGet<Q3MassProperty>().Value = transition.ProductMz;
                    else if (prop == typeof(DwellTimeProperty))
                        row.TryGet<DwellTimeProperty>().Value = transition.Dwell;
                    else if (prop == typeof(DeclusteringPotentialProperty))
                        row.TryGet<DeclusteringPotentialProperty>().Value = transition.DP;
                    else if (prop == typeof(EntrancePotentialProperty))
                    {
                    }
                    else if (prop == typeof(CollisonEnergyProperty))
                        row.TryGet<CollisonEnergyProperty>().Value = transition.CE;
                    else if (prop == typeof(CollisionCellExitPotentialProperty))
                    {
                    }
                    else
                        throw new Exception(string.Format("Unhandled property '{0}'.", prop.Name));
                }
            }

            // Save method
            ExecuteAndCheck(new MsMethodSaveRequest(method, transitions.OutputMethod));
        }

        public void Dispose()
        {
            _api.Logout(new LogoutRequest());
            _api.Disconnect(new DisconnectRequest());
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
        }

        public string OutputMethod { get; }
        public string FinalMethod { get; }
        public string TransitionList { get; }
        public MethodTransition[] Transitions { get; }
    }

    public sealed class MethodTransition
    {
        public MethodTransition(string transitionListLine)
        {
            var values = transitionListLine.Split(',');
            if (values.Length < 6)
                throw new IOException("Invalid transition list format. Each line must at least have 6 values.");
            try
            {
                var i = 0;
                PrecursorMz = double.Parse(values[i++], CultureInfo.InvariantCulture);
                ProductMz = double.Parse(values[i++], CultureInfo.InvariantCulture);
                Dwell = double.Parse(values[i++], CultureInfo.InvariantCulture);
                Label = values[i++];
                DP = double.Parse(values[i++], CultureInfo.InvariantCulture);
                CE = double.Parse(values[i++], CultureInfo.InvariantCulture);
                if (i < values.Length)
                    PrecursorWindow = string.IsNullOrEmpty(values[i]) ? (double?)null : double.Parse(values[i], CultureInfo.InvariantCulture);
                i++;

                if (i < values.Length)
                    ProductWindow = string.IsNullOrEmpty(values[i]) ? (double?)null : double.Parse(values[i], CultureInfo.InvariantCulture);
                i++;

                if (i < values.Length)
                    Group = values[i++];

                if (i < values.Length)
                    AveragePeakArea = string.IsNullOrEmpty(values[i]) ? (float?)null : float.Parse(values[i], CultureInfo.InvariantCulture);
                i++;

                if (i < values.Length)
                    VariableRtWindow = string.IsNullOrEmpty(values[i]) ? (double?)null : double.Parse(values[i], CultureInfo.InvariantCulture);
                i++;

                if (i < values.Length)
                    Threshold = string.IsNullOrEmpty(values[i]) ? (double?)null : double.Parse(values[i], CultureInfo.InvariantCulture);
                i++;

                if (i < values.Length)
                    Primary = string.IsNullOrEmpty(values[i]) ? 1 : int.Parse(values[i], CultureInfo.InvariantCulture);
                i++;

                if (i < values.Length)
                    CoV = string.IsNullOrEmpty(values[i]) ? 1 : double.Parse(values[i], CultureInfo.InvariantCulture);
            }

            catch (FormatException)
            {
                throw new IOException("Invalid transition list format. Failure parsing numeric value.");
            }
        }

        public double PrecursorMz { get; }
        public double ProductMz { get; }
        public double Dwell { get; }
        public string Label { get; }
        public double CE { get; }
        public double DP { get; }
        public double? PrecursorWindow { get; }
        public double? ProductWindow { get; }
        public double? Threshold { get; }
        public int? Primary { get; }
        public string Group { get; }
        public float? AveragePeakArea { get; }
        public double? VariableRtWindow { get; }
        public double? CoV { get; }

        public int ExperimentIndex { get; set; }
    }
}
