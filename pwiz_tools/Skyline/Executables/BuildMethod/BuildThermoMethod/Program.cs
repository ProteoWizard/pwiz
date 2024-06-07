﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using Thermo.TNG.MethodXMLFactory;
using Thermo.TNG.MethodXMLInterface;
using XmlCalcium;
using XmlExploris;
using XmlFusion;
using XmlTsq;
using XmlStellar;

namespace BuildThermoMethod
{
    internal class UsageException : Exception
    {
        public UsageException()
        {
        }

        public UsageException(string message) : base(message)
        {
        }
    }

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
            catch (UsageException x)
            {
                if (!string.IsNullOrEmpty(x.Message))
                    Console.Error.WriteLine("ERROR: {0}", x.Message);
                Usage();
            }
            catch (IOException x)
            {
                Console.Error.Write(GetErrorText(x));
            }
            catch (Exception x)
            {
                Console.Error.Write(GetErrorText(x));
            }
        }

        private static string GetErrorText(Exception x)
        {
            // Thermo libraries can sometimes throw exceptions with multi-line errors
            // Any lines that do not get ERROR: prepended will not get reported
            var sb = new StringBuilder();
            var reader = new StringReader(x.Message);
            string line;
            while ((line = reader.ReadLine()) != null)
                sb.AppendLine("ERROR: " + line);
            return sb.ToString();
        }

        static void Usage()
        {
            const string usage =
                    "Usage: BuildThermoMethod [options] <template method> [list file]*\n" +
                    "   Takes template Thermo method file and a Skyline generated Thermo\n" +
                    "   scheduled transition list as inputs, to generate a method file\n" +
                    "   as output.\n" +
                    "   -f               Fusion method [default]\n" +
                    "   -e               Endura method\n" +
                    "   -q               Quantiva method\n" +
                    "   -a               Altis method\n" +
                    "   -p               Exploris method\n" +
                    "   -l               Fusion Lumos method\n" +
                    "   -c               Eclipse method\n" +
                    "   -t               Stellar method\n" +
                    "   -o <output file> New method is written to the specified output file\n" +
                    "   -x               Export method XML to <basename>.xml file\n" +
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
        public SureQuantInfo SureQuantInfo { get; set; }
        public double? IntensityThreshold { get; set; }

        public ListItem()
        {
            Compound = null;
            Polarity = null;
            RetentionStart = null;
            RetentionEnd = null;
            PrecursorMz = 0.0;
            Charge = null;
            ProductMz = null;
            CollisionEnergy = 0.0;
            SureQuantInfo = null;
            IntensityThreshold = null;
        }

        public static ListItem FromLine(int lineNum, string[] fields, Dictionary<int, string> columnMap)
        {
            int numFields = fields.Length;

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
                    var match = Regex.Match(curValue, @".+\(([\+|-])\)?(\d+)\).*");
                    if (match.Success)
                    {
                        if(int.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var res))
                            item.Charge = (match.Groups[1].Value == "-" ? -1 : 1) * res;
                    }
                }
                else if (curHeader.Equals("retention time (min)", StringComparison.InvariantCultureIgnoreCase))
                {
                    parseFail = !double.TryParse(curValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var timeParse);
                    if (!parseFail)
                        time = timeParse;
                }
                else if (curHeader.Equals("rt window (min)", StringComparison.InvariantCultureIgnoreCase))
                {
                    parseFail = !double.TryParse(curValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var windowParse);
                    if (!parseFail)
                        window = windowParse;
                }
                else if (curHeader.Equals("start time (min)", StringComparison.InvariantCultureIgnoreCase) ||
                         curHeader.Equals("t start (min)", StringComparison.InvariantCultureIgnoreCase))
                {
                    parseFail = !double.TryParse(curValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var timeParse);
                    if (!parseFail)
                        item.RetentionStart = timeParse;
                }
                else if (curHeader.Equals("end time (min)", StringComparison.InvariantCultureIgnoreCase) ||
                         curHeader.Equals("t end (min)", StringComparison.InvariantCultureIgnoreCase) ||
                         curHeader.Equals("t stop (min)", StringComparison.InvariantCultureIgnoreCase))
                {
                    parseFail = !double.TryParse(curValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var timeParse);
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
                    parseFail = !double.TryParse(curValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var mzParse);
                    if (!parseFail)
                        item.PrecursorMz = mzParse;
                }
                else if (curHeader.Equals("z", StringComparison.InvariantCultureIgnoreCase))
                {
                    parseFail = !int.TryParse(curValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var chargeParse);
                    if (!parseFail)
                        item.Charge = chargeParse;
                }
                else if (curHeader.Equals("product (m/z)", StringComparison.InvariantCultureIgnoreCase))
                {
                    parseFail = !double.TryParse(curValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var mzParse);
                    if (!parseFail)
                        item.ProductMz = mzParse;
                }
                else if (curHeader.Equals("collision energy (v)", StringComparison.InvariantCultureIgnoreCase) ||
                         curHeader.Equals("cid collision energy (%)", StringComparison.InvariantCultureIgnoreCase))
                {
                    parseFail = !double.TryParse(curValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var ceParse);
                    if (!parseFail)
                        item.CollisionEnergy = ceParse;
                }
                else if (curHeader.Equals("surequant info", StringComparison.InvariantCultureIgnoreCase))
                {
                    parseFail = !SureQuantInfo.TryParse(curValue, out var info);
                    if (!parseFail)
                        item.SureQuantInfo = info;
                }
                else if (curHeader.Equals("intensity threshold", StringComparison.InvariantCultureIgnoreCase))
                {
                    parseFail = !double.TryParse(curValue, out var thresholdParse);
                    if (!parseFail)
                        item.IntensityThreshold = thresholdParse;
                }

                if (parseFail)
                {
                    throw new SyntaxErrorException(string.Format("Error parsing value '{0}' for '{1}' on line {2}", curValue, curHeader, lineNum));
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

    internal sealed class SureQuantInfo
    {
        public int Charge { get; private set; }
        public bool Heavy { get; private set; }
        public string Sequence { get; private set; }
        public bool IsPrecursor { get; private set; }
        public string FragmentName { get; private set; }

        private char LastChar => Sequence.Length > 0 ? Sequence.Last() : 'X';

        public Tuple<int, char> Key
        {
            get
            {
                var c = LastChar;
                switch (c)
                {
                    case 'K':
                        if (Heavy)
                            c = 'k';
                        break;
                    case 'R':
                        if (Heavy)
                            c = 'r';
                        break;
                    default:
                        c = 'X';
                        break;
                }
                return Tuple.Create(Charge, c);
            }
        }

        private SureQuantInfo(int charge, bool heavy, string seq, bool isPrecursor, string fragmentName)
        {
            Charge = charge;
            Heavy = heavy;
            Sequence = seq;
            IsPrecursor = isPrecursor;
            FragmentName = fragmentName;
        }

        public static bool TryParse(string info, out SureQuantInfo obj)
        {
            obj = null;
            var labelIdx = info.IndexOfAny(new[] {'H', 'L'});
            if (labelIdx == -1)
                return false;
            if (!int.TryParse(info.Substring(0, labelIdx), out var charge))
                return false;
            var heavy = info[labelIdx] == 'H';
            info = info.Substring(labelIdx + 1);
            var splitIdx = info.IndexOf(';');
            if (splitIdx == -1)
                return false;
            var fragmentName = info.Substring(splitIdx + 1);
            var isPrecursor = false;
            if (fragmentName.StartsWith("*"))
            {
                isPrecursor = true;
                fragmentName = fragmentName.Substring(1);
            }
            obj = new SureQuantInfo(charge, heavy, info.Substring(0, splitIdx), isPrecursor, fragmentName);
            return true;
        }
    }

    internal sealed class BuildThermoMethod
    {
        private const string ThermoMethodExt = ".meth";
        private const string InstrumentFusion = "OrbitrapFusion";
        private const string InstrumentExploris = "OrbitrapExploris480";
        private const string InstrumentFusionLumos = "OrbitrapFusionLumos";
        private const string InstrumentEclipse = "OrbitrapEclipse";
        private const string InstrumentEndura = "TSQEndura";
        private const string InstrumentQuantiva = "TSQQuantiva";
        private const string InstrumentAltis = "TSQAltis";
        private const string InstrumentStellar = "Stellar";

        private string InstrumentType { get; set; }
        private string InstrumentVersion { get; set; }
        private string TemplateMethod { get; set; }
        private List<MethodTransitions> MethodTrans { get; set; }
        private bool ExportXml { get; set; }

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
                string arg = args[i++];
                switch (arg[1])
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
                    case 'a':
                        InstrumentType = InstrumentAltis;
                        break;
                    case 'p':
                        InstrumentType = InstrumentExploris;
                        break;
                    case 'l':
                        InstrumentType = InstrumentFusionLumos;
                        break;
                    case 'c':
                        InstrumentType = InstrumentEclipse;
                        break;
                    case 't':
                        InstrumentType = InstrumentStellar;
                        break;
                    case 'o':
                        if (i >= args.Length)
                            throw new UsageException();
                        outputMethod = Path.GetFullPath(args[i++]);
                        break;
                    case 'x':
                        ExportXml = true;
                        break;
                    case 's':
                        readStdin = true;
                        break;
                    case 'm':
                        multiFile = true;
                        break;
                    default:
                        throw new UsageException(string.Format("Unknown argument {0}", arg));
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
                        throw new IOException("Empty mass list found.");

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

                string outMeth = EnsureExtension(methodTranList.OutputMethod, ".meth");  // Thermo needs the extension to be .meth
                if (!Equals(outMeth, methodTranList.OutputMethod) && File.Exists(methodTranList.OutputMethod))
                    File.Move(methodTranList.OutputMethod, outMeth);

                try
                {
                    using (IMethodXMLContext mxc = MethodXMLFactory.CreateContext(InstrumentType, InstrumentVersion))
                    using (IMethodXML mx = mxc.Create())
                    {
                        mx.Open(TemplateMethod);
                        mx.EnableValidation(true);

                        var listItems = ParseList(methodTranList.TransitionList).ToArray();
                        if (!listItems.Any())
                            throw new IOException("Empty mass list found.");

                        switch (InstrumentType)
                        {
                            case InstrumentFusion:
                                mx.ApplyMethodModificationsFromXML(GetFusionModificationXml(InstrumentType, listItems, outMeth));
                                break;
                            case InstrumentExploris:
                                mx.ApplyMethodModificationsFromXML(GetExplorisXml(InstrumentType, listItems, outMeth));
                                break;
                            case InstrumentEclipse:
                            // case InstrumentFusion:
                            case InstrumentFusionLumos:
                                mx.ApplyMethodModificationsFromXML(GetCalciumXml(InstrumentType, listItems, outMeth));
                                break;
                            case InstrumentStellar:
                                mx.ApplyMethodModificationsFromXML(GetStellarXml(listItems, outMeth));
                                break;
                            default:
                                mx.ImportMassListFromXML(GetHyperionXml(listItems, outMeth));
                                break;
                        }
                        mx.SaveAs(outMeth);
                    }

                    if (!File.Exists(outMeth))
                        throw new IOException(string.Format("Failure creating method file {0}.", methodTranList.FinalMethod));
                }
                finally 
                {
                    if (!Equals(outMeth, methodTranList.OutputMethod))
                        File.Move(outMeth, methodTranList.OutputMethod);
                }
            }
        }

        private string EnsureExtension(string path, string ext)
        {
            if (path.EndsWith(ext))
                return path;
            return Path.ChangeExtension(path, ext);
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
        private string GetHyperionXml(IEnumerable<ListItem> items, string outMethod)
        {
            var method = new Method
            {
                Version = XmlTsq.Version.Item1,
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

            return Serialize(method, outMethod);
        }

        // Get XML for Fusion methods
        private string GetFusionModificationXml(string instrument, IEnumerable<ListItem> items, string outMethod)
        {
            var itemsEnumerated = items.ToArray();
            var methodModifications = new XmlFusion.MethodModifications
            {
                Version = XmlFusion.Version.Item1,
                Model = instrument,
                Family = XmlFusion.Family.Calcium,
                Type = XmlFusion.Type.SL,
                Modification = new[]
                {
                    new XmlFusion.Modification
                    {
                        Order = 1,
                        Experiment = new[]
                        {
                            new XmlFusion.Experiment
                            {
                                ExperimentIndex = 1, // Assume that we should modify the 2nd experiment
                                ExperimentIndexSpecified = true,
                                TMSnScan = new XmlFusion.TMSnScan
                                {
                                    MassList = new XmlFusion.MassList
                                    {
                                        StartEndTime = itemsEnumerated.Any(item => item.RetentionStart.HasValue),
                                        CollisionEnergyCID = itemsEnumerated.Any(item => item.CollisionEnergy.HasValue), // TODO
                                        MassListRecord = itemsEnumerated.Select(item => new XmlFusion.MassListRecord
                                        {
                                            MOverZ = item.PrecursorMz.GetValueOrDefault(),
                                            MOverZSpecified = item.PrecursorMz.HasValue,
                                            Z = item.Charge.GetValueOrDefault(),
                                            ZSpecified = item.Charge.HasValue,
                                            StartTime = item.RetentionStart.GetValueOrDefault(),
                                            StartTimeSpecified = item.RetentionStart.HasValue,
                                            EndTime = item.RetentionEnd.GetValueOrDefault(),
                                            EndTimeSpecified = item.RetentionEnd.HasValue,
                                            CollisionEnergyCID = item.CollisionEnergy.GetValueOrDefault(), // TODO
                                            CollisionEnergyCIDSpecified = item.CollisionEnergy.HasValue, // TODO
                                            CompoundName = item.Compound,
                                        }).ToArray()
                                    }
                                }
                            }
                        }
                    }
                }
            };

            return Serialize(methodModifications, outMethod);
        }

        // Get XML for Exploris methods
        private string GetExplorisXml(string instrument, IList<ListItem> items, string outMethod)
        {
            var surequant = items.Count > 0 && items[0].SureQuantInfo != null;

            var records = new Dictionary<Tuple<int, char>, List<ListItem>>();
            foreach (var item in items)
            {
                var key = surequant ? item.SureQuantInfo.Key : Tuple.Create(0, '\0');
                if (!records.TryGetValue(key, out var list))
                {
                    records[key] = new List<ListItem>();
                    list = records[key];
                }
                list.Add(item);
            }

            const int experimentIndex = 0; // Assume that we should modify the 1st experiment

            var modifications = new List<XmlExploris.Modification>();
            var recordIndex = 0;
            var modIndex = 1;
            foreach (var record in records)
            {
                var mass = new List<XmlExploris.MassListRecord>();
                var massTrigger = new List<XmlExploris.MassListRecord>();

                double? groupMz = null;

                foreach (var item in record.Value)
                {
                    if (surequant)
                    {
                        if (groupMz != item.PrecursorMz)
                        {
                            groupMz = item.PrecursorMz;
                            mass.Add(new XmlExploris.MassListRecord
                            {
                                MOverZ = item.PrecursorMz.GetValueOrDefault(),
                                MOverZSpecified = item.PrecursorMz.HasValue,
                                Z = item.SureQuantInfo.Charge,
                                ZSpecified = true,
                                StartTime = item.RetentionStart.GetValueOrDefault(),
                                StartTimeSpecified = item.RetentionStart.HasValue,
                                EndTime = item.RetentionEnd.GetValueOrDefault(),
                                EndTimeSpecified = item.RetentionEnd.HasValue,
                                CompoundName = item.Compound,
                                IntensityThreshold = item.IntensityThreshold.GetValueOrDefault(),
                                IntensityThresholdSpecified = item.IntensityThreshold.HasValue
                            });
                        }
                        if (!item.SureQuantInfo.IsPrecursor)
                        {
                            massTrigger.Add(new XmlExploris.MassListRecord
                            {
                                CollisionEnergy = item.CollisionEnergy.GetValueOrDefault(),
                                CollisionEnergySpecified = item.CollisionEnergy.HasValue,
                                MOverZ = item.ProductMz.GetValueOrDefault(),
                                MOverZSpecified = item.ProductMz.HasValue,
                                CompoundName = item.SureQuantInfo.FragmentName,
                                GroupID = item.PrecursorMz.GetValueOrDefault().ToString(CultureInfo.InvariantCulture),
                                GroupIDSpecified = true,
                            });
                        }
                    }
                    else
                    {
                        mass.Add(new XmlExploris.MassListRecord
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
                            IntensityThreshold = item.IntensityThreshold.GetValueOrDefault(),
                            IntensityThresholdSpecified = item.IntensityThreshold.HasValue
                        });
                    }
                }

                modifications.Add(new XmlExploris.Modification
                {
                    Order = modIndex++,
                    Experiment = new[]
                    {
                        new XmlExploris.Experiment
                        {
                            ExperimentIndex = experimentIndex,
                            ExperimentIndexSpecified = true,
                            MassListFilter = new XmlExploris.MassListFilter
                            {
                                MassListType = XmlExploris.MassListType.TargetedMassInclusion,
                                Above = true,
                                SourceNodePosition = new [] {recordIndex},
                                MassList = new XmlExploris.MassList {IntensityThreshold = true, MassListRecord = mass.ToArray()}
                            }
                        }
                    }
                });

                if (surequant)
                {
                    if (!massTrigger.Any())
                        throw new Exception("Targeted mass trigger list empty (only precursors?)");

                    modifications.Add(new XmlExploris.Modification
                    {
                        Order = modIndex++,
                        Experiment = new[]
                        {
                            new XmlExploris.Experiment
                            {
                                ExperimentIndex = experimentIndex,
                                ExperimentIndexSpecified = true,
                                MassListFilter = new XmlExploris.MassListFilter
                                {
                                    MassListType = XmlExploris.MassListType.TargetedMassTrigger,
                                    Above = false,
                                    SourceNodePosition = new[] {recordIndex},
                                    MassList = new XmlExploris.MassList {MassListRecord = massTrigger.ToArray()}
                                }
                            }
                        }
                    });
                }

                recordIndex++;
            }

            var methodModifications = new XmlExploris.MethodModifications
            {
                Version = XmlExploris.Version.Item1,
                Model = instrument,
                Family = XmlExploris.Family.Merkur,
                Type = XmlExploris.Type.SL,
                Modification = modifications.ToArray()
            };

            return Serialize(methodModifications, outMethod);
        }

        private string GetStellarXml(IList<ListItem> items, string outMethod)
        {
            var records = new List<XmlStellar.MassListRecord>();
            foreach (var item in items)
            {
                records.Add(new XmlStellar.MassListRecord()
                {
                    MOverZ = item.PrecursorMz.GetValueOrDefault().ToString(CultureInfo.InvariantCulture),
                    Z = item.Charge.GetValueOrDefault(),
                    StartTime = item.RetentionStart.GetValueOrDefault(),
                    EndTime = item.RetentionEnd.GetValueOrDefault(),
                    CompoundName = item.Compound,
                    CIDCollisionEnergy = item.CollisionEnergy.GetValueOrDefault(),
                    ZSpecified = true,
                    StartTimeSpecified = true,
                    EndTimeSpecified = true,
                    CIDCollisionEnergySpecified = true
                });
            }

            var xml = new XmlStellar.MethodModifications()
            {
                Modification = new[] {new XmlStellar.Modification()
                {
                    Order = 1,
                    Experiment = new []{new XmlStellar.Experiment()
                        {
                            ExperimentIndex = 1,
                            ExperimentIndexSpecified = true,
                            TMSnScan = new XmlStellar.TMSnScan()
                            {
                                MassList = new XmlStellar.MassList()
                                {
                                    MassListRecord = records.ToArray(),
                                }
                            }
                        }
                }}
            }};

            return Serialize(xml, outMethod);
        }

        // Get XML for Calcium methods (Fusion, Fusion Lumos, Eclipse)
        private string GetCalciumXml(string instrument, IList<ListItem> items, string outMethod)
        {
            var surequant = items.Count > 0 && items[0].SureQuantInfo != null;

            var records = new Dictionary<Tuple<int, char>, List<ListItem>>();
            foreach (var item in items)
            {
                var key = surequant ? item.SureQuantInfo.Key : Tuple.Create(0, '\0');
                if (!records.TryGetValue(key, out var list))
                {
                    records[key] = new List<ListItem>();
                    list = records[key];
                }
                list.Add(item);
            }

            const int experimentIndex = 0; // Assume that we should modify the 1st experiment

            var modifications = new List<XmlCalcium.Modification>();
            var recordIndex = 0;
            var modIndex = 1;
            foreach (var record in records)
            {
                var mass = new List<XmlCalcium.MassListRecord>();
                var massTrigger = new List<XmlCalcium.MassListRecord>();

                double? groupMz = null;

                foreach (var item in record.Value)
                {
                    if (surequant)
                    {
                        if (groupMz != item.PrecursorMz)
                        {
                            groupMz = item.PrecursorMz;
                            mass.Add(new XmlCalcium.MassListRecord
                            {
                                MOverZ = item.PrecursorMz.GetValueOrDefault(),
                                MOverZSpecified = item.PrecursorMz.HasValue,
                                Z = item.SureQuantInfo.Charge,
                                ZSpecified = true,
                                StartTime = item.RetentionStart.GetValueOrDefault(),
                                StartTimeSpecified = item.RetentionStart.HasValue,
                                EndTime = item.RetentionEnd.GetValueOrDefault(),
                                EndTimeSpecified = item.RetentionEnd.HasValue,
                                CompoundName = item.Compound,
                                IntensityThreshold = item.IntensityThreshold.GetValueOrDefault(),
                                IntensityThresholdSpecified = item.IntensityThreshold.HasValue
                            });
                        }
                        if (!item.SureQuantInfo.IsPrecursor)
                        {
                            massTrigger.Add(new XmlCalcium.MassListRecord
                            {
                                MOverZ = item.ProductMz.GetValueOrDefault(),
                                MOverZSpecified = item.ProductMz.HasValue,
                                CompoundName = item.SureQuantInfo.FragmentName,
                                GroupID = item.PrecursorMz.GetValueOrDefault().ToString(CultureInfo.InvariantCulture),
                                GroupIDSpecified = true,
                            });
                        }
                    }
                    else
                    {
                        mass.Add(new XmlCalcium.MassListRecord
                        {
                            MOverZ = item.PrecursorMz.GetValueOrDefault(),
                            MOverZSpecified = item.PrecursorMz.HasValue,
                            Z = item.Charge.GetValueOrDefault(),
                            ZSpecified = item.Charge.HasValue,
                            StartTime = item.RetentionStart.GetValueOrDefault(),
                            StartTimeSpecified = item.RetentionStart.HasValue,
                            EndTime = item.RetentionEnd.GetValueOrDefault(),
                            EndTimeSpecified = item.RetentionEnd.HasValue,
                            CollisionEnergyCID = item.CollisionEnergy.GetValueOrDefault(),
                            CollisionEnergyCIDSpecified = item.CollisionEnergy.HasValue,
                            CompoundName = item.Compound,
                            IntensityThreshold = item.IntensityThreshold.GetValueOrDefault(),
                            IntensityThresholdSpecified = item.IntensityThreshold.HasValue
                        });
                    }
                }

                modifications.Add(new XmlCalcium.Modification
                {
                    Order = modIndex++,
                    Experiment = new[]
                    {
                        new XmlCalcium.Experiment
                        {
                            ExperimentIndex = experimentIndex,
                            ExperimentIndexSpecified = true,
                            TargetedInclusionMassListFilter = new XmlCalcium.TargetedInclusionMassListFilter
                            {
                                MassList = new XmlCalcium.MassList {IntensityThreshold = true, MassListRecord = mass.ToArray()}
                            }
                        }
                    }
                });

                if (surequant)
                {
                    if (!massTrigger.Any())
                        throw new Exception("Targeted mass trigger list empty (only precursors?)");

                    modifications.Add(new XmlCalcium.Modification
                    {
                        Order = modIndex++,
                        Experiment = new[]
                        {
                            new XmlCalcium.Experiment
                            {
                                ExperimentIndex = experimentIndex,
                                ExperimentIndexSpecified = true,
                                MassListFilter = new XmlCalcium.MassListFilter
                                {
                                    MassListType = XmlCalcium.MassListType.TargetedMassTrigger,
                                    Above = false,
                                    SourceNodePosition = new[] {recordIndex},
                                    MassList = new XmlCalcium.MassList {MassListRecord = massTrigger.ToArray()}
                                }
                            }
                        }
                    });
                }

                recordIndex++;
            }

            var methodModifications = new XmlCalcium.MethodModifications
            {
                Version = XmlCalcium.Version.Item1,
                Model = instrument,
                Family = XmlCalcium.Family.Calcium,
                Type = XmlCalcium.Type.SL,
                Modification = modifications.ToArray()
            };

            return Serialize(methodModifications, outMethod);
        }

        private string Serialize(object method, string outMethod)
        {
            var serializer = new XmlSerializer(method.GetType());
            var writer = new StringWriter();
            serializer.Serialize(writer, method);
            string xmlText = writer.ToString();
            if (ExportXml)
            {
                string xmlPath = Path.ChangeExtension(outMethod, ".xml");
                if (xmlPath != null)
                    File.WriteAllText(xmlPath, xmlText);
            }
            return xmlText;
        }
    }
}
