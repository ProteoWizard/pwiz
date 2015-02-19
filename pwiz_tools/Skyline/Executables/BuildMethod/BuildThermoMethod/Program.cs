using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using Thermo.TNG.MethodXMLFactory;
using Thermo.TNG.MethodXMLInterface;
using XmlFusion;
using XmlTsq;
using Family = XmlFusion.Family;
using Version = XmlTsq.Version;

namespace BuildThermoMethod
{
    internal class UsageException : Exception { }

    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Environment.ExitCode = -1;  // Failure until success

                var builder = new BuildThermoMethod();
                builder.ParseCommandArgs(args);
                builder.Build();

                Environment.ExitCode = 0;
            }
            catch (UsageException)
            {
                Usage();
            }
            catch (IOException x)
            {
                Console.Error.WriteLine("ERROR: {0}", x.Message);
            }
            catch (Exception x)
            {
                Console.Error.WriteLine("ERROR: {0}", x.Message);
            }
        }

        static void Usage()
        {
            const string usage =
                    "Usage: BuildThermoMethod [options] <template method> [list file]*\n" +
                    "   Takes template Thermo method file and a Skyline generated Thermo\n" +
                    "   scheduled transition list as inputs, to generate a new Fusion or\n" +
                    "   Endura/Quantiva method file as output.\n" +
                    "   -f               Fusion method [default]\n" +
                    "   -e               Endura method\n" +
                    "   -q               Quantiva method\n" +
                    "   -o <output file> New method is written to the specified output file\n" +
                    "   -s               Transition list is read from stdin.\n" +
                    "                    e.g. cat TranList.csv | BuildThermoMethod -s -o new.ext temp.ext\n" +
                    "\n" +
                    "   -m               Multiple lists concatenated in the format:\n" +
                    "                    file1.ext\n" +
                    "                    <transition list>\n" +
                    "\n" +
                    "                    file2.ext\n" +
                    "                    <transition list>\n" +
                    "                    ...\n";
            Console.Error.Write(usage);
        }
    }

    internal sealed class MethodTransitions
    {
        public MethodTransitions(string outputMethod, string finalMethod, string transitionList)
        {
            OutputMethod = outputMethod;
            FinalMethod = finalMethod;
            TransitionList = transitionList;
        }

        public string OutputMethod { get; private set; }
        public string FinalMethod { get; private set; }
        public string TransitionList { get; private set; }
    }

    internal sealed class ListItem
    {
        public string Compound { get; set; }
        public string Polarity { get; set; }
        public double? RetentionStart { get; set; }
        public double? RetentionEnd { get; set; }
        public double? PrecursorMz { get; set; }
        public int? Charge { get; set; }
        public double? ProductMz { get; set; }
        public double? CollisionEnergy { get; set; }

        public ListItem()
        {
            Compound = null;
            Polarity = null;
            RetentionStart = null;
            RetentionEnd = null;
            PrecursorMz = 0.0;
            ProductMz = null;
            CollisionEnergy = 0.0;
        }

        public static ListItem FromLine(int lineNum, string[] fields, Dictionary<int, string> columnMap)
        {
            int numFields = fields.Count();

            if (numFields != columnMap.Count)
                throw new InvalidDataException("CSV data contains different number of values than headers");

            // Quantiva
            // Compound,Retention Time (min),RT Window (min),Polarity,Precursor (m/z),Product (m/z),Collision Energy (V)
            // Compound,Start Time (min), End Time (min),Polarity,Precursor (m/z),Product (m/z),Collision Energy (V)

            // Fusion
            // m/z,z,t start (min),t end (min),CID Collision Energy (%)

            var item = new ListItem();

            double? time = null, window = null;

            for (int i = 0; i < numFields; ++i)
            {
                bool parseFail = false;
                string curHeader = columnMap[i];
                string curValue = fields[i];

                if (string.IsNullOrWhiteSpace(curValue))
                    continue;

                if (curHeader.Equals("compound", StringComparison.InvariantCultureIgnoreCase))
                {
                    item.Compound = curValue;
                }
                else if (curHeader.Equals("retention time (min)", StringComparison.InvariantCultureIgnoreCase))
                {
                    double timeParse;
                    parseFail = !double.TryParse(curValue, NumberStyles.Float, CultureInfo.InvariantCulture, out timeParse);
                    if (!parseFail)
                        time = timeParse;
                }
                else if (curHeader.Equals("rt window (min)", StringComparison.InvariantCultureIgnoreCase))
                {
                    double windowParse;
                    parseFail = !double.TryParse(curValue, NumberStyles.Float, CultureInfo.InvariantCulture, out windowParse);
                    if (!parseFail)
                        window = windowParse;
                }
                else if (curHeader.Equals("start time (min)", StringComparison.InvariantCultureIgnoreCase) ||
                         curHeader.Equals("t start (min)", StringComparison.InvariantCultureIgnoreCase))
                {
                    double timeParse;
                    parseFail = !double.TryParse(curValue, NumberStyles.Float, CultureInfo.InvariantCulture, out timeParse);
                    if (!parseFail)
                        item.RetentionStart = timeParse;
                }
                else if (curHeader.Equals("end time (min)", StringComparison.InvariantCultureIgnoreCase) ||
                         curHeader.Equals("t end (min)", StringComparison.InvariantCultureIgnoreCase))
                {
                    double timeParse;
                    parseFail = !double.TryParse(curValue, NumberStyles.Float, CultureInfo.InvariantCulture, out timeParse);
                    if (!parseFail)
                        item.RetentionEnd = timeParse;
                }
                else if (curHeader.Equals("polarity", StringComparison.InvariantCultureIgnoreCase))
                {
                    item.Polarity = curValue;
                }
                else if (curHeader.Equals("precursor (m/z)", StringComparison.InvariantCultureIgnoreCase) ||
                         curHeader.Equals("m/z", StringComparison.InvariantCultureIgnoreCase))
                {
                    double mzParse;
                    parseFail = !double.TryParse(curValue, NumberStyles.Float, CultureInfo.InvariantCulture, out mzParse);
                    if (!parseFail)
                        item.PrecursorMz = mzParse;
                }
                else if (curHeader.Equals("z", StringComparison.InvariantCultureIgnoreCase))
                {
                    int chargeParse;
                    parseFail = !int.TryParse(curValue, NumberStyles.Float, CultureInfo.InvariantCulture, out chargeParse);
                    if (!parseFail)
                        item.Charge = chargeParse;
                }
                else if (curHeader.Equals("product (m/z)", StringComparison.InvariantCultureIgnoreCase))
                {
                    double mzParse;
                    parseFail = !double.TryParse(curValue, NumberStyles.Float, CultureInfo.InvariantCulture, out mzParse);
                    if (!parseFail)
                        item.ProductMz = mzParse;
                }
                else if (curHeader.Equals("collision energy (v)", StringComparison.InvariantCultureIgnoreCase) ||
                         curHeader.Equals("cid collision energy (%)", StringComparison.InvariantCultureIgnoreCase))
                {
                    double ceParse;
                    parseFail = !double.TryParse(curValue, NumberStyles.Float, CultureInfo.InvariantCulture, out ceParse);
                    if (!parseFail)
                        item.CollisionEnergy = ceParse;
                }

                if (parseFail)
                {
                    throw new SyntaxErrorException(string.Format("Error parsing value '{0}' for '{1}' on line {2}",
                        curValue, curHeader, lineNum));
                }
            }

            if ((!item.RetentionStart.HasValue || !item.RetentionEnd.HasValue) && time.HasValue && window.HasValue)
            {
                double halfWindow = window.Value/2;
                item.RetentionStart = time.Value - halfWindow;
                item.RetentionEnd = time.Value + halfWindow;
            }

            return item;
        }
    }

    internal sealed class BuildThermoMethod
    {
        private const string ThermoMethodExt = ".meth";
        private const string InstrumentFusion = "OrbitrapFusion";
        private const string InstrumentEndura = "TSQEndura";
        private const string InstrumentQuantiva = "TSQQuantiva";

        private string InstrumentType { get; set; }
        private string InstrumentVersion { get; set; }
        private string TemplateMethod { get; set; }
        private List<MethodTransitions> MethodTrans { get; set; }

        public BuildThermoMethod()
        {
            InstrumentType = InstrumentQuantiva;
            InstrumentVersion = null;
            TemplateMethod = null;
            MethodTrans = new List<MethodTransitions>();
        }

        public void ParseCommandArgs(string[] args)
        {
            string outputMethod = null;
            bool readStdin = false;
            bool multiFile = false;

            int i = 0;
            while (i < args.Length && args[i][0] == '-')
            {
                switch (args[i++][1])
                {
                    case 'f':
                        InstrumentType = InstrumentFusion;
                        break;
                    case 'e':
                        InstrumentType = InstrumentEndura;
                        break;
                    case 'q':
                        InstrumentType = InstrumentQuantiva;
                        break;
                    case 'o':
                        if (i >= args.Length)
                            throw new UsageException();
                        outputMethod = Path.GetFullPath(args[i++]);
                        break;
                    case 's':
                        readStdin = true;
                        break;
                    case 'm':
                        multiFile = true;
                        break;
                    default:
                        throw new UsageException();
                }
            }

            InstrumentVersion = MethodXMLFactory.GetLatestInstalledVersion(InstrumentType);

            if (multiFile && !string.IsNullOrEmpty(outputMethod))
                Usage("Multi-file and specific output are not compatibile.");

            int argcLeft = args.Length - i;
            if (argcLeft < 1 || (!readStdin && argcLeft < 2))
                Usage();

            TemplateMethod = Path.GetFullPath(args[i++]);

            // Read input into a list of lists of fields
            if (readStdin)
            {
                if (!multiFile && string.IsNullOrEmpty(outputMethod))
                    Usage("Reading from standard in without multi-file format must specify an output file.");

                ReadTransitions(Console.In, outputMethod);
            }
            else
            {
                for (; i < args.Length; i++)
                {
                    string inputFile = Path.GetFullPath(args[i]);
                    string filter = null;
                    if (inputFile.Contains('*'))
                        filter = Path.GetFileName(inputFile);
                    else if (Directory.Exists(inputFile))
                        filter = "*.csv";

                    if (string.IsNullOrEmpty(filter))
                        ReadFile(inputFile, outputMethod, multiFile);
                    else
                    {
                        string dirName = Path.GetDirectoryName(filter) ?? ".";
                        foreach (var fileName in Directory.GetFiles(dirName, filter))
                        {
                            ReadFile(Path.Combine(dirName, fileName), null, multiFile);
                        }
                    }
                }
            }
        }

        private static void Usage(string message)
        {
            Console.Error.WriteLine(message);
            Console.Error.WriteLine();
            Usage();
        }

        private static void Usage()
        {
            throw new UsageException();
        }

        private void ReadFile(string inputFile, string outputMethod, bool multiFile)
        {
            if (!multiFile && string.IsNullOrEmpty(outputMethod))
            {
                string methodFileName = Path.GetFileNameWithoutExtension(inputFile) + ThermoMethodExt;
                string dirName = Path.GetDirectoryName(inputFile);
                outputMethod = (dirName != null ? Path.Combine(dirName, methodFileName) : inputFile);
            }

            using (var infile = new StreamReader(inputFile))
            {
                ReadTransitions(infile, outputMethod);
            }
        }

        private void ReadTransitions(TextReader instream, string outputMethod)
        {
            string outputMethodCurrent = outputMethod;
            string finalMethod = outputMethod;
            var sb = new StringBuilder();

            string line;
            while ((line = instream.ReadLine()) != null)
            {
                line = line.Trim();
                //                if (line.StartsWith("protein.name,"))
                //                    continue;

                if (string.IsNullOrEmpty(outputMethodCurrent))
                {
                    if (!string.IsNullOrEmpty(outputMethod))
                    {
                        // Only one file, if outputMethod specified
                        throw new IOException(string.Format("Failure creating method file {0}. Mass lists may not contain blank lines.", outputMethod));
                    }

                    // Read output file path from a line in the file
                    outputMethodCurrent = line;
                    finalMethod = instream.ReadLine();
                    if (finalMethod == null)
                        throw new IOException(string.Format("Empty mass list found."));

                    sb = new StringBuilder();
                }
                else if (string.IsNullOrEmpty(line))
                {
                    MethodTrans.Add(new MethodTransitions(outputMethodCurrent, finalMethod, sb.ToString()));
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
                MethodTrans.Add(new MethodTransitions(outputMethodCurrent, finalMethod, sb.ToString()));
            }

            // Read remaining contents of stream, in case it is stdin
            while (instream.ReadLine() != null)
            {
            }
        }

        public void Build()
        {
            foreach (var methodTranList in MethodTrans)
            {
                Console.Error.WriteLine("MESSAGE: Exporting method {0}", Path.GetFileName(methodTranList.FinalMethod));
                if (string.IsNullOrEmpty(methodTranList.TransitionList))
                    throw new IOException(string.Format("Failure creating method file {0}. The mass list is empty.", methodTranList.FinalMethod));

                using (IMethodXMLContext mxc = MethodXMLFactory.CreateContext(InstrumentType, InstrumentVersion))
                using (IMethodXML mx = mxc.Create())
                {
                    mx.Open(TemplateMethod);
                    mx.EnableValidation(true);
                    
                    ListItem[] listItems = ParseList(methodTranList.TransitionList).ToArray();
                    if (!listItems.Any())
                        throw new IOException("Empty mass list found.");

                    if (InstrumentType.Equals(InstrumentFusion))
                        mx.ApplyMethodModificationsFromXML(GetFusionModificationXml(listItems));
                    else
                        mx.ImportMassListFromXML(GetTsqMassListXml(listItems));
                    mx.SaveAs(methodTranList.OutputMethod);
                }

                if (!File.Exists(methodTranList.OutputMethod))
                    throw new IOException(string.Format("Failure creating method file {0}.", methodTranList.FinalMethod));
            }
        }

        private static IEnumerable<ListItem> ParseList(string list)
        {
            string[] lines = list.Split('\r', '\n');
            if (!lines.Any())
                throw new InvalidDataException("CSV data does not contain any lines");

            var columnMap = new Dictionary<int, string>();

            for (int i = 0; i < lines.Count(); ++i)
            {
                string[] fields = (lines[i].Contains('\t')) ? lines[i].Split('\t') : lines[i].Split(',');
                int numFields = fields.Count();
                if (i == 0)
                {
                    // Get headers
                    for (int j = 0; j < numFields; ++j)
                        columnMap.Add(j, fields[j]);
                }
                else
                {
                    // Skip empty lines
                    if (string.IsNullOrEmpty(lines[i]))
                        continue;

                    yield return ListItem.FromLine(i + 1, fields, columnMap);
                }
            }
        }

        // Get XML for Endura/Quantiva methods
        private static string GetTsqMassListXml(IEnumerable<ListItem> items)
        {
            var method = new Method
            {
                Version = Version.Item1,
                Family = XmlTsq.Family.Hyperion,
                Item = new SRMExp
                {
                    MassList = items.Select(item => new SRMMassListListItem
                    {
                        Name = item.Compound,
                        PrecursorMass = item.PrecursorMz.GetValueOrDefault(),
                        ProductMass = item.ProductMz.GetValueOrDefault(),
                        StartTime = item.RetentionStart.GetValueOrDefault(),
                        StopTime = item.RetentionEnd.GetValueOrDefault(),
                        Polarity = (item.Polarity.Equals("positive", StringComparison.InvariantCultureIgnoreCase)
                            ? Polarity.positive
                            : Polarity.negative),
                        CollisionEnergy = item.CollisionEnergy.GetValueOrDefault(),
                        SLensSpecified = false,
                        DwellTimeSpecified = false,
                        Q1PeakWidthSpecified = false,
                        Q3PeakWidthSpecified = false,
                        SourceCIDSpecified = false
                    }).ToArray()
                }
            };

            var serializer = new XmlSerializer(method.GetType());
            var writer = new StringWriter();
            serializer.Serialize(writer, method);
            return writer.ToString();
        }

        // Get XML for Fusion methods
        private static string GetFusionModificationXml(IEnumerable<ListItem> items)
        {
            var itemsEnumerated = items.ToArray();
            var methodModifications = new MethodModifications
            {
                Version = XmlFusion.Version.Item1,
                Model = InstrumentFusion,
                Family = Family.Calcium,
                Type = XmlFusion.Type.SL,
                Modification = new[]
                {
                    new Modification
                    {
                        Order = 1,
                        Experiment = new[]
                        {
                            new Experiment
                            {
                                ExperimentIndex = 1, // Assume that we should modify the 2nd experiment
                                TMSnScan = new TMSnScan
                                {
                                    MassList = new MassList
                                    {
                                        StartEndTime = itemsEnumerated.Any(item => item.RetentionStart.HasValue),
                                        CollisionEnergy = itemsEnumerated.Any(item => item.CollisionEnergy.HasValue),
                                        MassListRecord = itemsEnumerated.Select(item => new MassListRecord
                                        {
                                            MOverZ = item.PrecursorMz.GetValueOrDefault(),
                                            MOverZSpecified = item.PrecursorMz.HasValue,
                                            Z = item.Charge.GetValueOrDefault(),
                                            ZSpecified = item.Charge.HasValue,
                                            StartTime = item.RetentionStart.GetValueOrDefault(),
                                            StartTimeSpecified = item.RetentionStart.HasValue,
                                            EndTime = item.RetentionEnd.GetValueOrDefault(),
                                            EndTimeSpecified = item.RetentionEnd.HasValue,
                                            CollisionEnergy = item.CollisionEnergy.GetValueOrDefault(),
                                            CollisionEnergySpecified = item.CollisionEnergy.HasValue,
                                            CompoundName = item.Compound,
                                        }).ToArray()
                                    }
                                }
                            }
                        }
                    }
                }
            };

            var serializer = new XmlSerializer(methodModifications.GetType());
            var writer = new StringWriter();
            serializer.Serialize(writer, methodModifications);
            return writer.ToString();
        }
    }
}
