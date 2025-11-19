/*
 * Original author: Kaipo Tamura <kaipot .at. uw dot edu>,
 *                  Rita Chupalov <ritach .at. uw dot edu>,
 *                  Brendan MacLean <brendanx .at. uw dot edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using Thermo.TNG.MethodXMLFactory;
using Thermo.TNG.MethodXMLInterface;

// NOTE: Avoid adding using clauses for the classes generated from XSDs. Class names within these namespaces are highly redundant.

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

    /// <summary>
    /// Code for a BuildThermoMethod.exe which gets run by Skyline to merge
    /// a transition or isolation list into a template Thermo method for any
    /// of the modern Thermo mass spectrometers that support the XML method
    /// modification interface defined in the GitHub project:
    ///
    /// https://github.com/thermofisherlsms/meth-modifications
    ///
    /// You can build this project and run XmlMethodChanger.exe against XML
    /// from BuildThermoMethod.exe -x.
    ///
    /// This program uses classes generated from the .xsd files in the project
    /// under:
    ///
    /// https://github.com/thermofisherlsms/meth-modifications/tree/master/xsds
    ///
    /// Using a VS Command Prompt and a command like:
    ///
    /// >xsd xsds\Calcium\4.3\MethodModifications.xsd /classes /namespace:XmlCalcium_4_2
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Environment.ExitCode = -1;  // Failure until success
            var builder = new BuildThermoMethod();

            try
            {
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
                builder.WriteError(x);
            }
            catch (Exception x)
            {
                builder.WriteError(x);
            }
        }

        static void Usage()
        {
            const string usage =
                    "Usage: BuildThermoMethod [options] <template method> [list file]*\n" +
                    "   Takes template Thermo method file and a Skyline generated Thermo\n" +
                    "   scheduled transition list as inputs, to generate a method file\n" +
                    "   as output.\n" +
                    "   -t <inst type>    as in registry [default TSQAltis]\n" +
                    "   -v <inst version> as in registry (e.g. 4.2)\n" +
                    "   -o <output file>  New method is written to the specified output file\n" +
                    "   -x                Export method XML to <basename>.xml file\n" +
                    "   -s                Transition list is read from stdin.\n" +
                    "                     e.g. cat TranList.csv | BuildThermoMethod -s -o new.ext temp.ext\n" +
                    "\n" +
                    "   -m                Multiple lists concatenated in the format:\n" +
                    "                     file1.ext\n" +
                    "                     <transition list>\n" +
                    "\n" +
                    "                     file2.ext\n" +
                    "                     <transition list>\n" +
                    "                     ...\n";
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
        public string Compound;
        public string Polarity;
        public double? RetentionStart;
        public double? RetentionEnd;
        public double? PrecursorMz;
        public int? Charge;
        public double? ProductMz;
        public double? CollisionEnergy;
        public SureQuantInfo SureQuantInfo;
        public double? IntensityThreshold;
        public double? FaimsCv;

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

            // Stellar
            // m/z,z,t start (min),t stop (min),HCD Collision Energy/Energies (%), FAIMS CV (V)

            var item = new ListItem();

            double? time = null, window = null;

            for (int i = 0; i < numFields; ++i)
            {
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
                    ParseValue(curHeader, lineNum, curValue, out time);
                }
                else if (curHeader.Equals("rt window (min)", StringComparison.InvariantCultureIgnoreCase))
                {
                    ParseValue(curHeader, lineNum, curValue, out window);
                }
                else if (curHeader.Equals("start time (min)", StringComparison.InvariantCultureIgnoreCase) ||
                         curHeader.Equals("t start (min)", StringComparison.InvariantCultureIgnoreCase))
                {
                    ParseValue(curHeader, lineNum, curValue, out item.RetentionStart);
                }
                else if (curHeader.Equals("end time (min)", StringComparison.InvariantCultureIgnoreCase) ||
                         curHeader.Equals("t end (min)", StringComparison.InvariantCultureIgnoreCase) ||
                         curHeader.Equals("t stop (min)", StringComparison.InvariantCultureIgnoreCase))
                {
                    ParseValue(curHeader, lineNum, curValue, out item.RetentionEnd);
                }
                else if (curHeader.Equals("polarity", StringComparison.InvariantCultureIgnoreCase))
                {
                    item.Polarity = curValue;
                }
                else if (curHeader.Equals("precursor (m/z)", StringComparison.InvariantCultureIgnoreCase) ||
                         curHeader.Equals("m/z", StringComparison.InvariantCultureIgnoreCase))
                {
                    ParseValue(curHeader, lineNum, curValue, out item.PrecursorMz);
                }
                else if (curHeader.Equals("z", StringComparison.InvariantCultureIgnoreCase))
                {
                    ParseValue(curHeader, lineNum, curValue, out item.Charge);
                }
                else if (curHeader.Equals("product (m/z)", StringComparison.InvariantCultureIgnoreCase))
                {
                    ParseValue(curHeader, lineNum, curValue, out item.ProductMz);
                }
                else if (curHeader.Equals("collision energy (v)", StringComparison.InvariantCultureIgnoreCase) ||
                         curHeader.Equals("cid collision energy (%)", StringComparison.InvariantCultureIgnoreCase) ||
                         curHeader.Equals("hcd collision energy/energies (%)", StringComparison.InvariantCultureIgnoreCase))
                {
                    ParseValue(curHeader, lineNum, curValue, out item.CollisionEnergy);
                }
                else if (curHeader.Equals("surequant info", StringComparison.InvariantCultureIgnoreCase))
                {
                    ParseValue(curHeader, lineNum, curValue, out item.SureQuantInfo);
                }
                else if (curHeader.Equals("intensity threshold", StringComparison.InvariantCultureIgnoreCase))
                {
                    ParseValue(curHeader, lineNum, curValue, out item.IntensityThreshold);
                }
                else if (curHeader.Equals("faims cv (v)", StringComparison.InvariantCultureIgnoreCase))
                {
                    ParseValue(curHeader, lineNum, curValue, out item.FaimsCv);
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

        private static void ParseValue(string curHeader, int lineNum, string curValue, out double? value)
        {
            if (!double.TryParse(curValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var valueParse))
                ThrowParseError(curValue, curHeader, lineNum);

            value = valueParse;
        }

        private static void ParseValue(string curHeader, int lineNum, string curValue, out int? value)
        {
            if (!int.TryParse(curValue, out var valueParse))
                ThrowParseError(curValue, curHeader, lineNum);

            value = valueParse;
        }
        private static void ParseValue(string curHeader, int lineNum, string curValue, out SureQuantInfo value)
        {
            if (!SureQuantInfo.TryParse(curValue, out var valueParse))
                ThrowParseError(curValue, curHeader, lineNum);

            value = valueParse;
        }
        
        private static void ThrowParseError(string curValue, string curHeader, int lineNum)
        {
            throw new SyntaxErrorException(string.Format("Error parsing value '{0}' for '{1}' on line {2}", curValue, curHeader, lineNum));
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
        private const string InstrumentFusionLumos = "OrbitrapFusionLumos";
        private const string InstrumentEclipse = "OrbitrapEclipse";
        private const string InstrumentAscend = "OrbitrapAscend";
        private const string InstrumentAstral = "OrbitrapAstral";
        private const string InstrumentAstralZoom = "OrbitrapAstralZoom";
        private const string InstrumentExploris = "OrbitrapExploris480";
        private const string InstrumentEndura = "TSQEndura";
        private const string InstrumentQuantiva = "TSQQuantiva";
        private const string InstrumentAltis = "TSQAltis";
        private const string InstrumentStellar = "Stellar";

        private static readonly string[] KnownInstruments =
        {
            InstrumentFusion, InstrumentFusionLumos, InstrumentEclipse, InstrumentAscend,   // tribrids
            InstrumentAstral, InstrumentAstralZoom, // tofs
            InstrumentExploris, // exactives
            InstrumentEndura, InstrumentQuantiva, InstrumentAltis,  // tsqs
            InstrumentStellar, // qits
        };

        private string InstrumentType { get; set; }
        private float? InstrumentSoftwareVersion { get; set; }
        private string InstrumentVersion { get; set; }
        private string TemplateMethod { get; set; }
        private List<MethodTransitions> MethodTrans { get; set; }
        private bool ExportXml { get; set; }

        public BuildThermoMethod()
        {
            InstrumentType = InstrumentAltis;
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
                    case 't':
                        if (i >= args.Length)
                            throw new UsageException();
                        InstrumentType = args[i++];
                        if (!KnownInstruments.Contains(InstrumentType))
                            throw new UsageException(string.Format("Unknown instrument type {0}", InstrumentType));
                        break;
                    case 'v':
                        string verText = args[i++];
                        if (!float.TryParse(verText, NumberStyles.Float, CultureInfo.InvariantCulture, out var ver))
                            throw new UsageException(string.Format("Unrecognized instrument software version {0}", verText));
                        InstrumentSoftwareVersion = ver;
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
                Usage("Multi-file and specific output are not compatible.");

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

                        ListItem[] listItems;
                        try
                        {
                            listItems = ParseList(methodTranList.TransitionList).ToArray();
                            if (!listItems.Any())
                                throw new IOException("Empty mass list found.");
                        }
                        catch (Exception)
                        {
                            File.WriteAllText(Path.ChangeExtension(methodTranList.OutputMethod, ".csv"), methodTranList.TransitionList);
                            throw;
                        }

                        switch (InstrumentType)
                        {
                            case InstrumentFusion:
                                mx.ApplyMethodModificationsFromXML(GetFusionModificationXml(InstrumentType, listItems, outMeth));
                                break;
                            case InstrumentAstral:
                            case InstrumentAstralZoom:
                            case InstrumentExploris:
                                ApplyExplorisModificationsXml(mx, InstrumentType, listItems, outMeth);
                                break;
                            case InstrumentAscend:
                            case InstrumentEclipse:
                            case InstrumentFusionLumos:
                                ApplyCalciumModificationsXml(mx, InstrumentType, listItems, outMeth);
                                break;
                            case InstrumentStellar:
                                mx.ApplyMethodModificationsFromXML(GetStellarXml(listItems, outMeth));
                                break;
                            case InstrumentAltis:
                            case InstrumentEndura:
                            case InstrumentQuantiva:
                                mx.ImportMassListFromXML(GetHyperionXml(listItems, outMeth));
                                break;
                            default:
                                // In theory, this should have been caught already, but in case
                                // a new instrument was added without being assigned above, this
                                // will give a clear error.
                                throw new UsageException(string.Format("Unknown instrument type {0}", InstrumentType));
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

            for (int i = 0; i < lines.Length; ++i)
            {
                var line = lines[i];
                string[] fields = line.ParseDsvFields(line.Contains('\t') ? '\t' : ',');
                int numFields = fields.Length;
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
            var method = new XmlTsq.Method
            {
                Version = XmlTsq.Version.Item1,
                Family = XmlTsq.Family.Hyperion,
                Item = new XmlTsq.SRMExp
                {
                    MassList = items.Select(item => new XmlTsq.SRMMassListListItem
                    {
                        Name = item.Compound,
                        PrecursorMass = item.PrecursorMz.GetValueOrDefault(),
                        ProductMass = item.ProductMz.GetValueOrDefault(),
                        StartTime = item.RetentionStart.GetValueOrDefault(),
                        StopTime = item.RetentionEnd.GetValueOrDefault(),
                        Polarity = (item.Polarity.Equals("positive", StringComparison.InvariantCultureIgnoreCase)
                            ? XmlTsq.Polarity.positive
                            : XmlTsq.Polarity.negative),
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

        private string GetStellarXml(IList<ListItem> items, string outMethod)
        {
            var records = new List<XmlStellar.MassListRecord>();
            foreach (var item in items)
            {
                records.Add(new XmlStellar.MassListRecord
                {
                    MOverZ = item.PrecursorMz.GetValueOrDefault().ToString(CultureInfo.InvariantCulture),
                    Z = item.Charge.GetValueOrDefault(),
                    StartTime = item.RetentionStart.GetValueOrDefault(),
                    EndTime = item.RetentionEnd.GetValueOrDefault(),
                    CompoundName = item.Compound,
                    CIDCollisionEnergy = item.CollisionEnergy.GetValueOrDefault(),
                    FAIMSCV = item.FaimsCv.GetValueOrDefault(),
                    ZSpecified = true,
                    StartTimeSpecified = true,
                    EndTimeSpecified = true,
                    CIDCollisionEnergySpecified = true,
                    FAIMSCVSpecified = true,
                });
            }

            var xml = new XmlStellar.MethodModifications
            {
                Modification = new[] {new XmlStellar.Modification
                    {
                        Order = 1,
                        Experiment = new []{new XmlStellar.Experiment
                            {
                                ExperimentIndex = 1,
                                ExperimentIndexSpecified = true,
                                TMSnScan = new XmlStellar.TMSnScan
                                {
                                    MassList = new XmlStellar.MassList
                                    {
                                        MassListRecord = records.ToArray(),
                                    }
                                }
                            }
                        }}
                }
            };

            return Serialize(xml, outMethod);
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
                                ExperimentIndex = 1, // Assume that we should modify the 2nd experiment (MS1, MSn)
                                ExperimentIndexSpecified = true,
                                TMSnScan = new XmlFusion.TMSnScan
                                {
                                    MassList = new XmlFusion.MassList
                                    {
                                        StartEndTime = itemsEnumerated.Any(item => item.RetentionStart.HasValue),
                                        CollisionEnergyHCD = itemsEnumerated.Any(item => item.CollisionEnergy.HasValue), // TODO
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
                                            CollisionEnergyHCD = item.CollisionEnergy.GetValueOrDefault(), // TODO
                                            CollisionEnergyHCDSpecified = item.CollisionEnergy.HasValue, // TODO
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

        // Apply XML for Exploris methods
        private void ApplyExplorisModificationsXml(IMethodXML mx, string instrument, ListItem[] items, string outMethod)
        {
            bool isSchema44 = IsExplorisSchema4_4(instrument);
            try
            {
                // First try to use a MassListFilter which will throw if one is not present in the first experiment
                if (!isSchema44) 
                    mx.ApplyMethodModificationsFromXML(GetExplorisFilterXml(instrument, items, outMethod));
                else
                    mx.ApplyMethodModificationsFromXML(GetExplorisFilterXml_4_4(instrument, items, outMethod));
            }
            catch (Exception e)
            {
                // If it is SureQuant or an unexpected error, just throw the exception
                if (IsSureQuant(items) /*|| !e.Message.Contains("TargetedMassInclusion")*/)
                    throw;
                // If the installed software is older, changing a MassList table is not possible
                if (!isSchema44)
                    throw;

                Console.Out.WriteLine("Failed writing to TargetedMassInclusion as filter in MS1 scan.");
                Console.Out.WriteLine(e.ToString());
                Console.Out.WriteLine();
                Console.Out.WriteLine("Trying MassList table in MSn scan.");

                // If it is not SureQuant, then try the Mass List Table found within a tMS2 OT CID experiment
                mx.ApplyMethodModificationsFromXML(GetExplorisListXml_4_4(instrument, items, outMethod));
            }
        }

        private bool IsExplorisSchema4_4(string instrument)
        {
            if (InstrumentSoftwareVersion.HasValue)
            {
                switch (instrument)
                {
                    case InstrumentExploris:
                        return InstrumentSoftwareVersion >= 4.3;
                    case InstrumentAstral:
                    case InstrumentAstralZoom:
                        Console.WriteLine("Instrument {0} version {1}", instrument, InstrumentSoftwareVersion);
                        return InstrumentSoftwareVersion >= 2;
                }
            }

            return false;
        }

        private string GetExplorisListXml_4_4(string instrument, IList<ListItem> items, string outMethod)
        {
            // Uses XmlExploris_4_4 in order to get access to TMSnScan
            var itemsEnumerated = items.ToArray();
            var methodModifications = new XmlExploris_4_4.MethodModifications
            {
                Version = XmlExploris_4_4.Version.Item1,
                Model = instrument,
                Family = XmlExploris_4_4.Family.Merkur,
                Type = XmlExploris_4_4.Type.SL,
                Modification = new[]
                {
                    new XmlExploris_4_4.Modification
                    {
                        Order = 1,
                        Experiment = new[]
                        {
                            new XmlExploris_4_4.Experiment
                            {
                                ExperimentIndex = 1, // Assume that we should modify the 2nd experiment (MS1, MSn)
                                ExperimentIndexSpecified = true,
                                TMSnScan = new XmlExploris_4_4.TMSnScan
                                {
                                    MassList = new XmlExploris_4_4.MassList
                                    {
                                        StartEndTime = itemsEnumerated.Any(item => item.RetentionStart.HasValue),
                                        CollisionEnergyTypeSpecified = itemsEnumerated.Any(item => item.CollisionEnergy.HasValue),
                                        CollisionEnergyType = XmlExploris_4_4.CollisionEnergyType.Normalized,   // redundant because this is zero
                                        MassListRecord = itemsEnumerated.Select(item => new XmlExploris_4_4.MassListRecord
                                        {
                                            MOverZ = item.PrecursorMz.GetValueOrDefault(),
                                            MOverZSpecified = item.PrecursorMz.HasValue,
                                            Z = item.Charge.GetValueOrDefault(),
                                            ZSpecified = item.Charge.HasValue,
                                            StartTime = item.RetentionStart.GetValueOrDefault(),
                                            StartTimeSpecified = item.RetentionStart.HasValue,
                                            EndTime = item.RetentionEnd.GetValueOrDefault(),
                                            EndTimeSpecified = item.RetentionEnd.HasValue,
                                            CollisionEnergies = item.CollisionEnergy?.ToString(CultureInfo.InvariantCulture),
                                            CompoundName = item.Compound,
                                            IntensityThreshold = item.IntensityThreshold.GetValueOrDefault(),
                                            IntensityThresholdSpecified = item.IntensityThreshold.HasValue
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

        private bool IsSureQuant(IList<ListItem> items)
        {
            return items.Count > 0 && items[0].SureQuantInfo != null;
        }

        private string GetExplorisFilterXml(string instrument, IList<ListItem> items, string outMethod)
        {
            var sureQuant = IsSureQuant(items);

            var records = new Dictionary<Tuple<int, char>, List<ListItem>>();
            foreach (var item in items)
            {
                var key = sureQuant ? item.SureQuantInfo.Key : Tuple.Create(0, '\0');
                if (!records.TryGetValue(key, out var list))
                {
                    records[key] = new List<ListItem>();
                    list = records[key];
                }
                list.Add(item);
            }

            const int experimentIndex = 0; // Assume that we should modify the 1st experiment (MS1 -> Filter -> MS2)

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
                    if (sureQuant)
                    {
                        if (!Equals(groupMz, item.PrecursorMz))
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

                if (sureQuant)
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

        private string GetExplorisFilterXml_4_4(string instrument, IList<ListItem> items, string outMethod)
        {
            var sureQuant = IsSureQuant(items);

            var records = new Dictionary<Tuple<int, char>, List<ListItem>>();
            foreach (var item in items)
            {
                var key = sureQuant ? item.SureQuantInfo.Key : Tuple.Create(0, '\0');
                if (!records.TryGetValue(key, out var list))
                {
                    records[key] = new List<ListItem>();
                    list = records[key];
                }
                list.Add(item);
            }

            const int experimentIndex = 0; // Assume that we should modify the 1st experiment (MS1 -> Filter -> MS2)

            var modifications = new List<XmlExploris_4_4.Modification>();
            var recordIndex = 0;
            var modIndex = 1;
            foreach (var record in records)
            {
                var mass = new List<XmlExploris_4_4.MassListRecord>();
                var massTrigger = new List<XmlExploris_4_4.MassListRecord>();

                double? groupMz = null;

                foreach (var item in record.Value)
                {
                    if (sureQuant)
                    {
                        if (!Equals(groupMz, item.PrecursorMz))
                        {
                            groupMz = item.PrecursorMz;
                            mass.Add(new XmlExploris_4_4.MassListRecord
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
                            massTrigger.Add(new XmlExploris_4_4.MassListRecord
                            {
                                CollisionEnergies = item.CollisionEnergy?.ToString(CultureInfo.InvariantCulture),
                                MOverZ = item.ProductMz.GetValueOrDefault(),
                                MOverZSpecified = item.ProductMz.HasValue,
                                CompoundName = item.SureQuantInfo.FragmentName,
                                GroupID = item.PrecursorMz.GetValueOrDefault(),
                                GroupIDSpecified = true,
                            });
                        }
                    }
                    else
                    {
                        mass.Add(new XmlExploris_4_4.MassListRecord
                        {
                            MOverZ = item.PrecursorMz.GetValueOrDefault(),
                            MOverZSpecified = item.PrecursorMz.HasValue,
                            Z = item.Charge.GetValueOrDefault(),
                            ZSpecified = item.Charge.HasValue,
                            StartTime = item.RetentionStart.GetValueOrDefault(),
                            StartTimeSpecified = item.RetentionStart.HasValue,
                            EndTime = item.RetentionEnd.GetValueOrDefault(),
                            EndTimeSpecified = item.RetentionEnd.HasValue,
                            CollisionEnergies = item.CollisionEnergy?.ToString(CultureInfo.InvariantCulture),
                            CompoundName = item.Compound,
                            IntensityThreshold = item.IntensityThreshold.GetValueOrDefault(),
                            IntensityThresholdSpecified = item.IntensityThreshold.HasValue
                        });
                    }
                }

                modifications.Add(new XmlExploris_4_4.Modification
                {
                    Order = modIndex++,
                    Experiment = new[]
                    {
                        new XmlExploris_4_4.Experiment
                        {
                            ExperimentIndex = experimentIndex,
                            ExperimentIndexSpecified = true,
                            MassListFilter = new XmlExploris_4_4.MassListFilter
                            {
                                MassListType = XmlExploris_4_4.MassListType.TargetedMassInclusion,
                                Above = true,
                                SourceNodePosition = new [] {recordIndex},
                                MassList = new XmlExploris_4_4.MassList {IntensityThreshold = true, MassListRecord = mass.ToArray()}
                            }
                        }
                    }
                });

                if (sureQuant)
                {
                    if (!massTrigger.Any())
                        throw new Exception("Targeted mass trigger list empty (only precursors?)");

                    modifications.Add(new XmlExploris_4_4.Modification
                    {
                        Order = modIndex++,
                        Experiment = new[]
                        {
                            new XmlExploris_4_4.Experiment
                            {
                                ExperimentIndex = experimentIndex,
                                ExperimentIndexSpecified = true,
                                MassListFilter = new XmlExploris_4_4.MassListFilter
                                {
                                    MassListType = XmlExploris_4_4.MassListType.TargetedMassTrigger,
                                    Above = false,
                                    SourceNodePosition = new[] {recordIndex},
                                    MassList = new XmlExploris_4_4.MassList {MassListRecord = massTrigger.ToArray()}
                                }
                            }
                        }
                    });
                }

                recordIndex++;
            }

            var methodModifications = new XmlExploris_4_4.MethodModifications
            {
                Version = XmlExploris_4_4.Version.Item1,
                Model = instrument,
                Family = XmlExploris_4_4.Family.Merkur,
                Type = XmlExploris_4_4.Type.SL,
                Modification = modifications.ToArray()
            };

            return Serialize(methodModifications, outMethod);
        }

        private void ApplyCalciumModificationsXml(IMethodXML mx, string instrument, ListItem[] items, string outMethod)
        {
            try
            {
                // First try to use a MassListFilter if one is present
                if (!InstrumentSoftwareVersion.HasValue || InstrumentSoftwareVersion.Value < 4.2)
                    mx.ApplyMethodModificationsFromXML(GetCalciumFilterXml(instrument, items, outMethod));
                else
                    mx.ApplyMethodModificationsFromXML(GetCalciumFilterXml_4_2(instrument, items, outMethod));
            }
            catch (Exception e)
            {
                // If it is SureQuant or an unexpected error, just throw the exception
                if (IsSureQuant(items) || !e.Message.Contains("TargetedMassInclusion"))
                    throw;

                Console.Out.WriteLine("Failed writing to TargetedMassInclusion as filter in MS1 scan.");
                Console.Out.WriteLine(e.ToString());
                Console.Out.WriteLine();
                Console.Out.WriteLine("Trying MassList table in MSn scan.");

                if (!InstrumentSoftwareVersion.HasValue || InstrumentSoftwareVersion.Value < 4.2)
                    mx.ApplyMethodModificationsFromXML(GetCalciumListXml(instrument, items, outMethod));
                else
                    mx.ApplyMethodModificationsFromXML(GetCalciumListXml_4_2(instrument, items, outMethod));
            }
        }

        // Get XML for Calcium methods (Fusion, Fusion Lumos, Eclipse)
        private string GetCalciumListXml(string instrument, IList<ListItem> items, string outMethod)
        {
            var itemsEnumerated = items.ToArray();
            var methodModifications = new XmlCalcium.MethodModifications
            {
                Version = XmlCalcium.Version.Item1,
                Model = instrument,
                Family = XmlCalcium.Family.Calcium,
                Type = XmlCalcium.Type.SL,
                Modification = new[]
                {
                    new XmlCalcium.Modification
                    {
                        Order = 1,
                        Experiment = new[]
                        {
                            new XmlCalcium.Experiment
                            {
                                ExperimentIndex = 1, // Assume that we should modify the 2nd experiment (MS1, MSn)
                                ExperimentIndexSpecified = true,
                                TMSnScan = new XmlCalcium.TMSnScan
                                {
                                    MassList = new XmlCalcium.MassList
                                    {
                                        StartEndTime = itemsEnumerated.Any(item => item.RetentionStart.HasValue),
                                        CollisionEnergyHCD = itemsEnumerated.Any(item => item.CollisionEnergy.HasValue),
                                        MassListRecord = itemsEnumerated.Select(item => new XmlCalcium.MassListRecord
                                        {
                                            MOverZ = item.PrecursorMz.GetValueOrDefault(),
                                            MOverZSpecified = item.PrecursorMz.HasValue,
                                            Z = item.Charge.GetValueOrDefault(),
                                            ZSpecified = item.Charge.HasValue,
                                            StartTime = item.RetentionStart.GetValueOrDefault(),
                                            StartTimeSpecified = item.RetentionStart.HasValue,
                                            EndTime = item.RetentionEnd.GetValueOrDefault(),
                                            EndTimeSpecified = item.RetentionEnd.HasValue,
                                            CollisionEnergyHCD = item.CollisionEnergy.GetValueOrDefault(),
                                            CollisionEnergyHCDSpecified = item.CollisionEnergy.HasValue,
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

        // Get XML for Calcium methods (Fusion, Fusion Lumos, Eclipse)
        private string GetCalciumListXml_4_2(string instrument, IList<ListItem> items, string outMethod)
        {
            var itemsEnumerated = items.ToArray();
            var methodModifications = new XmlCalcium_4_2.MethodModifications
            {
                Version = XmlCalcium_4_2.Version.Item1,
                Model = instrument,
                Family = XmlCalcium_4_2.Family.Calcium,
                Type = XmlCalcium_4_2.Type.SL,
                Modification = new[]
                {
                    new XmlCalcium_4_2.Modification
                    {
                        Order = 1,
                        Experiment = new[]
                        {
                            new XmlCalcium_4_2.Experiment
                            {
                                ExperimentIndex = 1, // Assume that we should modify the 2nd experiment (MS1, MSn)
                                ExperimentIndexSpecified = true,
                                TMSnScan = new XmlCalcium_4_2.TMSnScan
                                {
                                    MassList = new XmlCalcium_4_2.MassList
                                    {
                                        StartEndTime = itemsEnumerated.Any(item => item.RetentionStart.HasValue),
                                        CollisionEnergyHCD = itemsEnumerated.Any(item => item.CollisionEnergy.HasValue),
                                        MassListRecord = itemsEnumerated.Select(item => new XmlCalcium_4_2.MassListRecord
                                        {
                                            MOverZ = item.PrecursorMz.GetValueOrDefault(),
                                            MOverZSpecified = item.PrecursorMz.HasValue,
                                            Z = item.Charge.GetValueOrDefault(),
                                            ZSpecified = item.Charge.HasValue,
                                            StartTime = item.RetentionStart.GetValueOrDefault(),
                                            StartTimeSpecified = item.RetentionStart.HasValue,
                                            EndTime = item.RetentionEnd.GetValueOrDefault(),
                                            EndTimeSpecified = item.RetentionEnd.HasValue,
                                            CollisionEnergyHCD = item.CollisionEnergy.GetValueOrDefault(),
                                            CollisionEnergyHCDSpecified = item.CollisionEnergy.HasValue,
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

        private string GetCalciumFilterXml(string instrument, IList<ListItem> items, string outMethod)
        {
            var sureQuant = IsSureQuant(items);

            var records = new Dictionary<Tuple<int, char>, List<ListItem>>();
            foreach (var item in items)
            {
                var key = sureQuant ? item.SureQuantInfo.Key : Tuple.Create(0, '\0');
                if (!records.TryGetValue(key, out var list))
                {
                    records[key] = new List<ListItem>();
                    list = records[key];
                }
                list.Add(item);
            }

            const int experimentIndex = 0; // Assume that we should modify the 1st experiment (MS1 -> Filter -> MS2)

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
                    if (sureQuant)
                    {
                        if (Equals(groupMz, item.PrecursorMz))
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
                            CollisionEnergyHCD = item.CollisionEnergy.GetValueOrDefault(),
                            CollisionEnergyHCDSpecified = item.CollisionEnergy.HasValue,
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
                                MassList = sureQuant 
                                    ? new XmlCalcium.MassList {IntensityThreshold = true, MassListRecord = mass.ToArray()}
                                    : new XmlCalcium.MassList {IntensityThreshold = true, MassListRecord = mass.ToArray(),
                                        CollisionEnergyHCD = records.Values.SelectMany(list => list).Any(item => item.CollisionEnergy.HasValue)}
                            }
                        }
                    }
                });

                if (sureQuant)
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

        private string GetCalciumFilterXml_4_2(string instrument, IList<ListItem> items, string outMethod)
        {
            var sureQuant = IsSureQuant(items);

            var records = new Dictionary<Tuple<int, char>, List<ListItem>>();
            foreach (var item in items)
            {
                var key = sureQuant ? item.SureQuantInfo.Key : Tuple.Create(0, '\0');
                if (!records.TryGetValue(key, out var list))
                {
                    records[key] = new List<ListItem>();
                    list = records[key];
                }
                list.Add(item);
            }

            const int experimentIndex = 0; // Assume that we should modify the 1st experiment (MS1 -> Filter -> MS2)

            var modifications = new List<XmlCalcium_4_2.Modification>();
            var recordIndex = 0;
            var modIndex = 1;
            foreach (var record in records)
            {
                var mass = new List<XmlCalcium_4_2.MassListRecord>();
                var massTrigger = new List<XmlCalcium_4_2.MassListRecord>();

                double? groupMz = null;

                foreach (var item in record.Value)
                {
                    if (sureQuant)
                    {
                        if (Equals(groupMz, item.PrecursorMz))
                        {
                            groupMz = item.PrecursorMz;
                            mass.Add(new XmlCalcium_4_2.MassListRecord
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
                            massTrigger.Add(new XmlCalcium_4_2.MassListRecord
                            {
                                MOverZ = item.ProductMz.GetValueOrDefault(),
                                MOverZSpecified = item.ProductMz.HasValue,
                                CompoundName = item.SureQuantInfo.FragmentName,
                                GroupID = item.PrecursorMz.GetValueOrDefault(),
                                GroupIDSpecified = true,
                            });
                        }
                    }
                    else
                    {
                        mass.Add(new XmlCalcium_4_2.MassListRecord
                        {
                            MOverZ = item.PrecursorMz.GetValueOrDefault(),
                            MOverZSpecified = item.PrecursorMz.HasValue,
                            Z = item.Charge.GetValueOrDefault(),
                            ZSpecified = item.Charge.HasValue,
                            StartTime = item.RetentionStart.GetValueOrDefault(),
                            StartTimeSpecified = item.RetentionStart.HasValue,
                            EndTime = item.RetentionEnd.GetValueOrDefault(),
                            EndTimeSpecified = item.RetentionEnd.HasValue,
                            CollisionEnergyHCD = item.CollisionEnergy.GetValueOrDefault(),
                            CollisionEnergyHCDSpecified = item.CollisionEnergy.HasValue,
                            CompoundName = item.Compound,
                            IntensityThreshold = item.IntensityThreshold.GetValueOrDefault(),
                            IntensityThresholdSpecified = item.IntensityThreshold.HasValue
                        });
                    }
                }

                modifications.Add(new XmlCalcium_4_2.Modification
                {
                    Order = modIndex++,
                    Experiment = new[]
                    {
                        new XmlCalcium_4_2.Experiment
                        {
                            ExperimentIndex = experimentIndex,
                            ExperimentIndexSpecified = true,
                            TargetedInclusionMassListFilter = new XmlCalcium_4_2.TargetedInclusionMassListFilter
                            {
                                MassList = sureQuant
                                    ? new XmlCalcium_4_2.MassList {IntensityThreshold = true, MassListRecord = mass.ToArray()}
                                    : new XmlCalcium_4_2.MassList {IntensityThreshold = true, MassListRecord = mass.ToArray(),
                                        CollisionEnergyHCD = records.Values.SelectMany(list => list).Any(item => item.CollisionEnergy.HasValue)}
                            }
                        }
                    }
                });

                if (sureQuant)
                {
                    if (!massTrigger.Any())
                        throw new Exception("Targeted mass trigger list empty (only precursors?)");

                    modifications.Add(new XmlCalcium_4_2.Modification
                    {
                        Order = modIndex++,
                        Experiment = new[]
                        {
                            new XmlCalcium_4_2.Experiment
                            {
                                ExperimentIndex = experimentIndex,
                                ExperimentIndexSpecified = true,
                                MassListFilter = new XmlCalcium_4_2.MassListFilter
                                {
                                    MassListType = XmlCalcium_4_2.MassListType.TargetedMassTrigger,
                                    Above = false,
                                    SourceNodePosition = new[] {recordIndex},
                                    MassList = new XmlCalcium_4_2.MassList {MassListRecord = massTrigger.ToArray()}
                                }
                            }
                        }
                    });
                }

                recordIndex++;
            }

            var methodModifications = new XmlCalcium_4_2.MethodModifications
            {
                Version = XmlCalcium_4_2.Version.Item1,
                Model = instrument,
                Family = XmlCalcium_4_2.Family.Calcium,
                Type = XmlCalcium_4_2.Type.SL,
                Modification = modifications.ToArray()
            };

            return Serialize(methodModifications, outMethod);
        }

        private class XmlFile
        {
            public XmlFile(string path, string unwrittenText)
            {
                Path = path;
                UnwrittenText = unwrittenText;
            }

            public string Path { get; }
            public string UnwrittenText { get; }
        }

        private readonly List<XmlFile> _xmlFiles = new List<XmlFile>();

        private string Serialize(object method, string outMethod)
        {
            var serializer = new XmlSerializer(method.GetType());
            var writer = new StringWriter();
            serializer.Serialize(writer, method);
            string xmlText = writer.ToString();
            string extPrefix = _xmlFiles.Count > 0 ? _xmlFiles.Count.ToString() : string.Empty;
            string xmlPath = Path.ChangeExtension(outMethod, extPrefix + ".xml");
            if (xmlPath != null)
            {
                if (!ExportXml)
                    _xmlFiles.Add(new XmlFile(xmlPath, xmlText));
                else
                {
                    _xmlFiles.Add(new XmlFile(xmlPath, string.Empty));
                    File.WriteAllText(xmlPath, xmlText);
                }

            }
            return xmlText;
        }

        private void WriteUnsavedXml()
        {
            foreach (var xmlFile in _xmlFiles.Where(xmlFile => !string.IsNullOrEmpty(xmlFile.UnwrittenText)))
            {
                File.WriteAllText(xmlFile.Path, xmlFile.UnwrittenText);
            }
        }

        public void WriteError(Exception x)
        {
            Console.Error.Write(GetErrorText(x));
            WriteUnsavedXml();
        }

        private static string GetErrorText(Exception x)
        {
            // Thermo libraries can sometimes throw exceptions with multi-line errors
            // Any lines that do not get ERROR: prepended will not get reported
            var sb = new StringBuilder();
            var reader = new StringReader(x.ToString());
            string line;
            while ((line = reader.ReadLine()) != null)
                sb.AppendLine("ERROR: " + line);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Copied from Skyline.Util.Extensions.TextUtil
    /// </summary>
    internal static class TextUtil
    {
        /// <summary>
        /// Splits a line of text in delimiter-separated value format into an array of fields.
        /// The function correctly handles quotation marks.
        /// (N.B. our quotation mark handling now differs from the (March 2018) behavior of Excel and Google Spreadsheets
        /// when dealing with somewhat absurd uses of quotes as found in our tests, but that seems to be OK for  general use.
        /// </summary>
        /// <param name="line">The line to be split into fields</param>
        /// <param name="separator">The separator being used</param>
        /// <returns>An array of field strings</returns>
        public static string[] ParseDsvFields(this string line, char separator)
        {
            var listFields = new List<string>();
            var sbField = new StringBuilder();
            bool inQuotes = false;
            for (var chIndex = 0; chIndex < line.Length; chIndex++)
            {
                var ch = line[chIndex];
                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        // Is this the closing quote, or is this an escaped quote?
                        if (chIndex + 1 < line.Length && line[chIndex + 1] == '"')
                        {
                            sbField.Append(ch); // Treat "" as an escaped quote
                            chIndex++; // Consume both quotes
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        sbField.Append(ch);
                    }
                }
                else if (ch == '"')
                {
                    if (sbField.Length == 0) // Quote at start of field is special case
                    {
                        inQuotes = true;
                    }
                    else
                    {
                        if (chIndex + 1 < line.Length && line[chIndex + 1] == '"')
                        {
                            sbField.Append(ch); // Treat "" as an escaped quote
                            chIndex++; // Consume both quotes
                        }
                        else
                        {
                            // N.B. we effectively ignore a bare quote in an unquoted string. 
                            // This is technically an undefined behavior, so that's probably OK.
                            // Excel and Google sheets treat it as a literal quote, but that 
                            // would be a change in our established behavior
                            inQuotes = true;
                        }
                    }
                }
                else if (ch == separator)
                {
                    listFields.Add(sbField.ToString());
                    sbField.Remove(0, sbField.Length);
                }
                else
                {
                    sbField.Append(ch);
                }
            }
            listFields.Add(sbField.ToString());
            return listFields.ToArray();
        }
    }
}
