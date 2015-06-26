using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace BumberDash
{
    class MSGFParams
    {
        public int Instrument { get; set; }
        //public int FragmentationMethod { get; set; }
        public int Protocol { get; set; }
        public double PrecursorTolerance { get; set; }
        public string PrecursorToleranceUnits { get; set; }
        public int IsotopeErrorMin { get; set; }
        public int IsotopeErrorMax { get; set; }
        public int Specificity { get; set; }
        public string OutputSuffix { get; set; }
        public int CleavageAgent { get; set; }

        public MSGFParams()
        {
            Instrument = InstrumentOptions.LowResLTQ;
            //FragmentationMethod = FragmentationMethodOptions.Auto;
            Protocol = ProtocolOptions.NoProtocol;
            PrecursorTolerance = 20.0;
            PrecursorToleranceUnits = PrecursorToleranceUnitOptions.PPM;
            IsotopeErrorMin = 0;
            IsotopeErrorMax = 2;
            Specificity = SpecificityOptions.SemiTryptic;
            OutputSuffix = "_MSGF";
            CleavageAgent = CleavageAgentOptions["Trypsin"];
        }

        public string GetPrecursorToleranceString()
        {
            return PrecursorTolerance + PrecursorToleranceUnits;
        }

        public string GetIsotopeErrorString()
        {
            return IsotopeErrorMin + "," + IsotopeErrorMax;
        }

        #region Support Classes
        public static class InstrumentOptions
        {
            public static int LowResLTQ = 0;
            public static int HighResLTQ = 1;
            public static int TOF = 2;
            public static int QExactive = 3;
        }

        public static class FragmentationMethodOptions
        {
            public static int Auto = 0;
            public static int CID = 1;
            public static int ETD = 2;
            public static int HCD = 3;
            public static int MergeFromSamePrecursor = 4;
        }

        public static class ProtocolOptions
        {
            public static int NoProtocol = 0;
            public static int Phosphorylation = 1;
            public static int iTRAQ = 2;
            public static int iTRAQPhospho = 3;
        }

        public static class PrecursorToleranceUnitOptions
        {
            public static string Daltons = "Da";
            public static string PPM = "ppm";
        }

        public static class SpecificityOptions
        {
            public static int NonTryptic = 0;
            public static int SemiTryptic = 1;
            public static int Tryptic = 2;
        }

        public static Dictionary<string, int> CleavageAgentOptions
        {
            get
            {
                return new Dictionary<string, int>
                    {
                        {"No_enzyme", 0},
                        {"Trypsin", 1},
                        {"Chymotrypsin", 2},
                        {"Lys-C", 3},
                        {"Lys-N", 4},
                        {"Arg-C", 6},
                        {"Asp-N", 7}
                        
                    };
            }
        }
        #endregion
    }

    class MSGFHandler
    {
        public static string MSGFParamsToOverload(MSGFParams config)
        {
            return "-inst " + config.Instrument
                   + " -o \"[FileNameOnly]" + config.OutputSuffix + ".mzid\""
                   + " -tda 1"
                   + " -m 0"// + config.FragmentationMethod
                   + " -protocol " + config.Protocol
                   + " -e " + config.CleavageAgent
                   + " -t " + config.PrecursorTolerance.ToString(CultureInfo.InvariantCulture) + config.PrecursorToleranceUnits
                   + " -ti " + config.IsotopeErrorMin + "," + config.IsotopeErrorMax
                   + " -ntt " + config.Specificity
                   + " -s \"[FullFileName]\"";
        }

        public static MSGFParams OverloadToMSGFParams(string overloadString)
        {
            var config = new MSGFParams();
            var splitOverload = overloadString.Split();
            for (var x = 0; x < splitOverload.Length; x++)
            {
                var nextNumber=-999;
                var validNumberNext = false;
                if (x + 1 < splitOverload.Length)
                    validNumberNext = int.TryParse(splitOverload[x + 1], out nextNumber);
                switch (splitOverload[x])
                {
                    case "-inst":
                        if (validNumberNext)
                            config.Instrument = nextNumber;
                        break;
                    case "-o":
                        var trimmedString = splitOverload[x + 1].Replace("\"[FileNameOnly]", string.Empty)
                                                                .Replace(".mzid\"", string.Empty);
                        config.OutputSuffix = trimmedString;
                        break;
                    //case "-m":
                    //    if (validNumberNext)
                    //        config.FragmentationMethod = nextNumber;
                        break;
                    case "-e":
                        if (validNumberNext)
                            config.CleavageAgent = nextNumber;
                        break;
                    case "-p":
                        if (validNumberNext)
                            config.Protocol = nextNumber;
                        break;
                    case "-t":
                        if (x + 1 < splitOverload.Length)
                        {
                            double tolerance;
                            var rx = new Regex(@"([\.\d]+)([A-Za-z]+)");
                            if (!rx.IsMatch(splitOverload[x + 1]))
                                break;
                            var groups = rx.Match(splitOverload[x + 1]).Groups;
                            if (groups.Count != 3 || !double.TryParse(groups[1].Value, out tolerance))
                                break;
                            config.PrecursorTolerance = tolerance;
                            config.PrecursorToleranceUnits = groups[2].Value;
                        }
                        break;
                    case "-ti":
                        if (x + 1 < splitOverload.Length)
                        {
                            int min;
                            int max;
                            var splitNext = splitOverload[x + 1].Split(',');
                            if (splitNext.Length != 2)
                                break;
                            if (!int.TryParse(splitNext[0], out min) || !int.TryParse(splitNext[1], out max))
                                break;
                            config.IsotopeErrorMin = min;
                            config.IsotopeErrorMax = max;
                        }
                        break;
                    case "-ntt":
                        if (validNumberNext)
                            config.Specificity = nextNumber;
                        break;
                }
            }
            return config;
        }

        public static string ModListToModString(List<Util.Modification> modList, int maxNumMods)
        {
            var sb = new StringBuilder("NumMods=" + maxNumMods + Environment.NewLine);
            foreach (var mod in modList)
            {
                var residue = mod.Residue.Trim("[]".ToCharArray()).ToUpperInvariant();
                var closestMod = Util.UnimodLookup.GetClosestMod(mod.Mass);
                var position = "any";

                if (residue.StartsWith("("))
                {
                    position = "N-term";
                    if (residue == "(")
                        residue = "*";
                }
                else if (residue.EndsWith(")"))
                {
                    position = "C-term";
                    if (residue == ")")
                        residue = "*";
                }

                if (Math.Abs(closestMod.MonoMass-mod.Mass) > 1)
                    sb.AppendLine(string.Format("{0},{1},{2},{3},{4}",
                                            mod.Mass, residue,
                                            mod.Type == "Static" ? "fix" : "opt",
                                            position, "Custom"));
                else
                    sb.AppendLine(string.Format("{0},{1},{2},{3},{4}",
                                            closestMod.Composition, residue,
                                            mod.Type == "Static" ? "fix" : "opt",
                                            position, closestMod.Name));
            }

            return sb.ToString();
        }

        public static List<Util.Modification> ModStringToModList(string modString)
        {
            var modList = new List<Util.Modification>();
            var modLines = modString.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in modLines)
            {
                if (line.StartsWith("#") || line.StartsWith("NumMods"))
                    continue;
                var splitLine = line.Split(",".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                var unimodEntry = Util.UnimodLookup.FullUnimodList.Find(x => x.Name == splitLine[4]) ??
                                  Util.UnimodLookup.FullUnimodList.Find(x => x.Composition == splitLine[0]);
                
                var residue = splitLine[1] == "*" ? string.Empty : splitLine[1];
                var type = splitLine[2] == "fix" ? "Static" : "Dynamic";
                if (splitLine[3] == "N-term")
                    residue = residue.Insert(0, "(");
                else if (splitLine[3] == "C-term")
                    residue += ")";

                if (unimodEntry != null && !string.IsNullOrEmpty(residue))
                    modList.Add(new Util.Modification {Residue = residue, Mass = unimodEntry.MonoMass, Type = type});

            }
            return modList;
        }
    }
}
