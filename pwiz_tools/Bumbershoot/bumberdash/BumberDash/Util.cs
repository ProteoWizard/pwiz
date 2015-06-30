//
// $Id: Util.cs 107 2012-11-10 01:07:43Z chambm $
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the Bumberdash project.
//
// The Initial Developer of the Original Code is Jay Holman.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari, Matt Chambers
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace BumberDash
{
    public static class Util
    {
        #region Lists of valid paramaters

        public static Dictionary<string, string> parameterTypes = new Dictionary<string, string>
                                                                      {
                                                                          {"UseChargeStateFromMS", "bool"},
                                                                          {"AdjustPrecursorMass", "bool"},
                                                                          {"DuplicateSpectra", "bool"},
                                                                          {"UseSmartPlusThreeModel", "bool"},
                                                                          {"MassReconMode", "bool"},
                                                                          {"UseNETAdjustment", "bool"},
                                                                          {"ComputeXCorr", "bool"},
                                                                          {"UseAvgMassOfSequences", "bool"},
                                                                          {"CleanLibSpectra", "bool"},
                                                                          {"FASTARefreshResults", "bool"},
                                                                          {"RecalculateLibPepMasses", "bool"},
                                                                          {"DeisotopingMode", "int"},
                                                                          {"MinTerminiCleavages", "int"},
                                                                          {"CPUs", "int"},
                                                                          {"MaxMissedCleavages", "int"},
                                                                          {"ProteinSamplingTime", "int"},
                                                                          {"MaxDynamicMods", "int"},
                                                                          {"MaxNumPreferredDeltaMasses", "int"},
                                                                          {"NumChargeStates", "int"},
                                                                          {"NumIntensityClasses", "int"},
                                                                          {"MaxResultRank", "int"},
                                                                          {"MaxPeakCount", "int"},
                                                                          {"TagLength", "int"},
                                                                          {"MinPeptideLength", "int"},
                                                                          {"MaxTagCount", "int"},
                                                                          {"MaxPeptideLength","int"},
                                                                          {"MaxAmbResultsForBlindMods","int"},
                                                                          {"LibMaxPeakCount", "int"},
                                                                          {"MinPeptideMass", "double"},
                                                                          {"IsotopeMzTolerance", "double"},
                                                                          {"ComplementMzTolerance", "double"},
                                                                          {"TicCutoffPercentage", "double"},
                                                                          {"PrecursorMzTolerance", "double"},
                                                                          {"MaxPeptideMass", "double"},
                                                                          {"ClassSizeMultiplier", "double"},
                                                                          {"MaxPrecursorAdjustment", "double"},
                                                                          {"MinPrecursorAdjustment", "double"},
                                                                          {"BlosumThreshold", "double"},
                                                                          {"MaxModificationMassPlus", "double"},
                                                                          {"MaxModificationMassMinus", "double"},
                                                                          {"IntensityScoreWeight", "double"},
                                                                          {"MzFidelityScoreWeight", "double"},
                                                                          {"ComplementScoreWeight", "double"},
                                                                          {"MaxTagScore", "double"},
                                                                          {"PrecursorAdjustmentStep", "double"},
                                                                          {"LibTicCutoffPercentage", "double"},
                                                                          {"Name", "string"},
                                                                          {"CleavageRules", "string"},
                                                                          {"FragmentationRule", "string"},
                                                                          {"PrecursorMzToleranceUnits", "string"},
                                                                          {"FragmentMzToleranceUnits", "string"},
                                                                          {"Blosum", "string"},
                                                                          {"UnimodXML", "string"},
                                                                          {"ExplainUnknownMassShiftsAs", "string"},
                                                                          {"OutputSuffix", "string"},
                                                                          {"DecoyPrefix", "string"},
                                                                          {"AvgPrecursorMzTolerance","string"},
                                                                          {"MonoPrecursorMzTolerance","string"},
                                                                          {"NTerminusMzTolerance", "string"},
                                                                          {"CTerminusMzTolerance", "string"},
                                                                          {"FragmentMzTolerance", "string"},
                                                                          {"MonoisotopeAdjustmentSet","string"},
                                                                          {"OutputFormat","string"},
                                                                          {"PrecursorMzToleranceRule","string"}
                                                                      };

        #endregion

        #region Version Info

        public static string Version { get { return GetAssemblyVersion(Assembly.GetExecutingAssembly().GetName()); } }
        public static DateTime LastModified { get { return GetAssemblyLastModified(Assembly.GetExecutingAssembly().GetName()); } }

        public static AssemblyName GetAssemblyByName(string assemblyName)
        {
            if (Assembly.GetCallingAssembly().GetName().FullName.Contains(assemblyName))
                return Assembly.GetCallingAssembly().GetName();

            foreach (AssemblyName a in Assembly.GetCallingAssembly().GetReferencedAssemblies())
            {
                if (a.FullName.Contains(assemblyName + ','))
                    return a;
            }
            return null;
        }

        public static string GetAssemblyVersion(AssemblyName assembly)
        {
            Match versionMatch = Regex.Match(assembly.ToString(), @"Version=([\d.]+)");
            return versionMatch.Groups[1].Success ? versionMatch.Groups[1].Value : "unknown";
        }

        public static DateTime GetAssemblyLastModified(AssemblyName assembly)
        {
            return File.GetLastWriteTime(Assembly.ReflectionOnlyLoad(assembly.FullName).Location);
        }

        #endregion

        #region Unimod info

        public class UnimodEntry
        {
            public string Name { get; set; }
            public double MonoMass { get; set; }
            public double AvgMass { get; set; }
            public string Composition { get; set; }

            public UnimodEntry(string name, double mono, double avg, string composition)
            {
                Name = name;
                MonoMass = mono;
                AvgMass = avg;
                Composition = composition;
            }
        }

        public static class UnimodLookup
        {
            public static List<UnimodEntry> FullUnimodList = new List<UnimodEntry>
                {
                    new UnimodEntry("2HPG",282.052824,282.2476,"C16H10O5"),
                    new UnimodEntry("a-type-ion",-46.005479,-46.0254,"C-1H-2O-2"),
                    new UnimodEntry("AccQTag",170.048013,170.1674,"C10H6N2O1"),
                    new UnimodEntry("Acetyl",42.010565,42.0367,"C2H2O1"),
                    new UnimodEntry("ADP-Ribosyl",541.06111,541.3005,"C15H21N5O13P2"),
                    new UnimodEntry("AEBS",183.035399,183.2276,"C8H9N1O2S1"),
                    new UnimodEntry("AEC-MAEC",59.019355,59.1334,"C2H5N1O-1S1"),
                    new UnimodEntry("Amidated",-0.984016,-0.9848,"H1N1O-1"),
                    new UnimodEntry("Amidine",41.026549,41.0519,"C2H3N1"),
                    new UnimodEntry("Amidino",42.021798,42.04,"C1H2N2"),
                    new UnimodEntry("Amino",15.010899,15.0146,"H1N1"),
                    new UnimodEntry("Ammonia-loss",-17.026549,-17.0305,"H-3N-1"),
                    new UnimodEntry("Archaeol",634.662782,635.1417,"C43H86O2"),
                    new UnimodEntry("Arg->GluSA",-43.053433,-43.0711,"C-1H-5N-3O1"),
                    new UnimodEntry("Arg->Orn",-42.021798,-42.04,"C-1H-2N-2"),
                    new UnimodEntry("BADGE",340.167459,340.4129,"C21H24O4"),
                    new UnimodEntry("Benzoyl",104.026215,104.1061,"C7H4O1"),
                    new UnimodEntry("BHT",218.167065,218.3346,"C15H22O1"),
                    new UnimodEntry("BHTOH",234.16198,234.334,"C15H22O2"),
                    new UnimodEntry("Biotin",226.077598,226.2954,"C10H14N2O2S1"),
                    new UnimodEntry("Biotin-HPDP",428.191582,428.6124,"C19H32N4O3S2"),
                    new UnimodEntry("Biotin-PEO-Amine",356.188212,356.4835,"C16H28N4O3S1"),
                    new UnimodEntry("BisANS",594.091928,594.6569,"C32H22N2O6S2"),
                    new UnimodEntry("Bromo",77.910511,78.8961,"H-1Br1"),
                    new UnimodEntry("Bromobimane",190.074228,190.1986,"C10H10N2O2"),
                    new UnimodEntry("C8-QAT",227.224915,227.3862,"C14H29N1O1"),
                    new UnimodEntry("CAF",135.983029,136.1265,"C3H4O4S1"),
                    new UnimodEntry("CAMthiopropanoyl",145.019749,145.1796,"C5H7N1O2S1"),
                    new UnimodEntry("Can-FP-biotin",447.195679,447.5291,"C19H34N3O5S1P1"),
                    new UnimodEntry("Carbamidomethyl",57.021464,57.0513,"C2H3N1O1"),
                    new UnimodEntry("Carbamyl",43.005814,43.0247,"C1H1N1O1"),
                    new UnimodEntry("Carboxy",43.989829,44.0095,"C1O2"),
                    new UnimodEntry("Carboxy->Thiocarboxy",15.977156,16.0656,"O-1S1"),
                    new UnimodEntry("Carboxyethyl",72.021129,72.0627,"C3H4O2"),
                    new UnimodEntry("Carboxymethyl",58.005479,58.0361,"C2H2O2"),
                    new UnimodEntry("CHDH",294.183109,294.3859,"C17H26O4"),
                    new UnimodEntry("Cholesterol",368.344302,368.6383,"C27H44"),
                    new UnimodEntry("CoenzymeA",765.09956,765.5182,"C21H34N7O16S1P3"),
                    new UnimodEntry("Crotonaldehyde",70.041865,70.0898,"C4H6O1"),
                    new UnimodEntry("Cyano",24.995249,25.0095,"C1H-1N1"),
                    new UnimodEntry("CyDye-Cy3",672.298156,672.8335,"C37H44N4O6S1"),
                    new UnimodEntry("CyDye-Cy5",684.298156,684.8442,"C38H44N4O6S1"),
                    new UnimodEntry("Cys->Dha",-33.987721,-34.0809,"H-2S-1"),
                    new UnimodEntry("Cys->Oxoalanine",-17.992806,-18.0815,"H-2O1S-1"),
                    new UnimodEntry("Cys->PyruvicAcid",-33.003705,-33.0961,"H-3N-1O1S-1"),
                    new UnimodEntry("Cysteinyl",119.004099,119.1423,"C3H5N1O2S1"),
                    new UnimodEntry("Cytopiloyne",362.136553,362.3738,"C19H22O7"),
                    new UnimodEntry("Cytopiloyne+water",380.147118,380.3891,"C19H24O8"),
                    new UnimodEntry("DAET",87.050655,87.1866,"C4H9N1O-1S1"),
                    new UnimodEntry("Dansyl",233.051049,233.2862,"C12H11N1O2S1"),
                    new UnimodEntry("Deamidated",0.984016,0.9848,"H-1N-1O1"),
                    new UnimodEntry("Decanoyl",154.135765,154.2493,"C10H18O1"),
                    new UnimodEntry("Dehydrated",-18.010565,-18.0153,"H-2O-1"),
                    new UnimodEntry("Dehydro",-1.007825,-1.0079,"H-1"),
                    new UnimodEntry("Delta:H(2)C(2)",26.01565,26.0373,"C2H2"),
                    new UnimodEntry("Delta:H(2)C(3)",38.01565,38.048,"C3H2"),
                    new UnimodEntry("Delta:H(2)C(3)O(1)",54.010565,54.0474,"C3H2O1"),
                    new UnimodEntry("Delta:H(2)C(5)",62.01565,62.0694,"C5H2"),
                    new UnimodEntry("Delta:H(4)C(2)",28.0313,28.0532,"C2H4"),
                    new UnimodEntry("Delta:H(4)C(2)O(-1)S(1)",44.008456,44.1188,"C2H4O-1S1"),
                    new UnimodEntry("Delta:H(4)C(3)",40.0313,40.0639,"C3H4"),
                    new UnimodEntry("Delta:H(4)C(3)O(1)",56.026215,56.0633,"C3H4O1"),
                    new UnimodEntry("Delta:H(4)C(6)",76.0313,76.096,"C6H4"),
                    new UnimodEntry("Delta:H(5)C(2)",29.039125,29.0611,"C2H5"),
                    new UnimodEntry("Delta:H(6)C(6)O(1)",94.041865,94.1112,"C6H6O1"),
                    new UnimodEntry("Delta:H(8)C(6)O(2)",112.05243,112.1265,"C6H8O2"),
                    new UnimodEntry("Deoxy",-15.994915,-15.9994,"O-1"),
                    new UnimodEntry("DeStreak",75.998285,76.1176,"C2H4O1S1"),
                    new UnimodEntry("Dethiomethyl",-48.003371,-48.1075,"C-1H-4S-1"),
                    new UnimodEntry("DHP",118.065674,118.1558,"C8H8N1"),
                    new UnimodEntry("Diacylglycerol",576.511761,576.9334,"C37H68O4"),
                    new UnimodEntry("Dibromo",155.821022,157.7921,"H-2Br2"),
                    new UnimodEntry("Didehydro",-2.01565,-2.0159,"H-2"),
                    new UnimodEntry("Didehydroretinylidene",264.187801,264.4046,"C20H24"),
                    new UnimodEntry("Diethyl",56.0626,56.1063,"C4H8"),
                    new UnimodEntry("Diironsubcluster",342.786916,342.876,"C5H-1N2O5S2Fe2"),
                    new UnimodEntry("Diisopropylphosphate",164.060231,164.1394,"C6H13O3P1"),
                    new UnimodEntry("Dimethyl",28.0313,28.0532,"C2H4"),
                    new UnimodEntry("DimethylpyrroleAdduct",78.04695,78.1118,"C6H6"),
                    new UnimodEntry("Dioxidation",31.989829,31.9988,"O2"),
                    new UnimodEntry("Diphthamide",143.118438,143.2068,"C7H15N2O1"),
                    new UnimodEntry("Dipyrrolylmethanemethyl",418.137616,418.3973,"C20H22N2O8"),
                    new UnimodEntry("DTBP",87.01427,87.1435,"C3H5N1S1"),
                    new UnimodEntry("EDT-iodoacetyl-PEO-biotin",490.174218,490.7034,"C20H34N4O4S3"),
                    new UnimodEntry("EDT-maleimide-PEO-biotin",601.206246,601.8021,"C25H39N5O6S3"),
                    new UnimodEntry("EQAT",184.157563,184.2786,"C10H20N2O1"),
                    new UnimodEntry("ESP",338.177647,338.4682,"C16H26N4O2S1"),
                    new UnimodEntry("Ethanedithiol",75.980527,76.1838,"C2H4O-1S2"),
                    new UnimodEntry("Ethanolyl",44.026215,44.0526,"C2H4O1"),
                    new UnimodEntry("Ethyl",28.0313,28.0532,"C2H4"),
                    new UnimodEntry("FAD",783.141486,783.5339,"C27H31N9O15P2"),
                    new UnimodEntry("Farnesyl",204.187801,204.3511,"C15H24"),
                    new UnimodEntry("Fluorescein",388.082112,388.3497,"C22H14N1O6"),
                    new UnimodEntry("FMN",438.094051,438.3285,"C17H19N4O8P1"),
                    new UnimodEntry("FMNC",456.104615,456.3438,"C17H21N4O9P1"),
                    new UnimodEntry("FMNH",454.088965,454.3279,"C17H19N4O9P1"),
                    new UnimodEntry("FNEM",427.069202,427.3625,"C24H13N1O7"),
                    new UnimodEntry("Formyl",27.994915,28.0101,"C1O1"),
                    new UnimodEntry("FormylMet",159.035399,159.2062,"C6H9N1O2S1"),
                    new UnimodEntry("FP-Biotin",572.316129,572.7405,"C27H49N4O5S1P1"),
                    new UnimodEntry("FTC",421.073241,421.4259,"C21H15N3O5S1"),
                    new UnimodEntry("GeranylGeranyl",272.250401,272.4681,"C20H32"),
                    new UnimodEntry("GIST-Quat",127.099714,127.1842,"C7H13N1O1"),
                    new UnimodEntry("Gln->pyro-Glu",-17.026549,-17.0305,"H-3N-1"),
                    new UnimodEntry("Glu",129.042593,129.114,"C5H7N1O3"),
                    new UnimodEntry("Glu->pyro-Glu",-18.010565,-18.0153,"H-2O-1"),
                    new UnimodEntry("Glucuronyl",176.032088,176.1241,"C6H8O6"),
                    new UnimodEntry("GluGlu",258.085186,258.228,"C10H14N2O6"),
                    new UnimodEntry("GluGluGlu",387.127779,387.3419,"C15H21N3O9"),
                    new UnimodEntry("GluGluGluGlu",516.170373,516.4559,"C20H28N4O12"),
                    new UnimodEntry("Glutathione",305.068156,305.3076,"C10H15N3O6S1"),
                    new UnimodEntry("Glycerophospho",154.00311,154.0584,"C3H7O5P1"),
                    new UnimodEntry("GlycerylPE",197.04531,197.1262,"C5H12N1O5P1"),
                    new UnimodEntry("Glycosyl",148.037173,148.114,"C5H8O5"),
                    new UnimodEntry("GlyGly",114.042927,114.1026,"C4H6N2O2"),
                    new UnimodEntry("GPIanchor",123.00853,123.0477,"C2H6N1O3P1"),
                    new UnimodEntry("Guanidinyl",42.021798,42.04,"C1H2N2"),
                    new UnimodEntry("Heme",616.177295,616.4873,"C34H32N4O4Fe1"),
                    new UnimodEntry("HexN",161.068808,161.1558,"C6H11N1O4"),
                    new UnimodEntry("His->Asn",-23.015984,-23.0366,"C-2H-1N-1O1"),
                    new UnimodEntry("His->Asp",-22.031969,-22.0519,"C-2H-2N-2O2"),
                    new UnimodEntry("HMVK",86.036779,86.0892,"C4H6O2"),
                    new UnimodEntry("HNE",156.11503,156.2221,"C9H16O2"),
                    new UnimodEntry("HNE+Delta:H(2)",158.13068,158.238,"C9H18O2"),
                    new UnimodEntry("HPG",132.021129,132.1162,"C8H4O2"),
                    new UnimodEntry("Hydroxycinnamyl",146.036779,146.1427,"C9H6O2"),
                    new UnimodEntry("Hydroxyfarnesyl",220.182715,220.3505,"C15H24O1"),
                    new UnimodEntry("Hydroxyheme",614.161645,614.4714,"C34H30N4O4Fe1"),
                    new UnimodEntry("Hydroxymethyl",30.010565,30.026,"C1H2O1"),
                    new UnimodEntry("Hydroxytrimethyl",59.04969,59.0871,"C3H7O1"),
                    new UnimodEntry("Hypusine",87.068414,87.1204,"C4H9N1O1"),
                    new UnimodEntry("IBTP",316.138088,316.3759,"C22H21P1"),
                    new UnimodEntry("ICAT-C",227.126991,227.2603,"C10H17N3O3"),
                    new UnimodEntry("ICAT-D",442.224991,442.5728,"C20H34N4O5S1"),
                    new UnimodEntry("ICAT-G",486.251206,486.6253,"C22H38N4O6S1"),
                    new UnimodEntry("ICAT-H",345.097915,345.7754,"C15H20N1O6Cl1"),
                    new UnimodEntry("ICPL",105.021464,105.0941,"C6H3N1O1"),
                    new UnimodEntry("IED-Biotin",326.141261,326.4145,"C14H22N4O3S1"),
                    new UnimodEntry("IGBP",296.016039,297.1478,"C12H13N2O2Br1"),
                    new UnimodEntry("IMID",68.037448,68.0773,"C3H4N2"),
                    new UnimodEntry("Iminobiotin",225.093583,225.3106,"C10H15N3O1S1"),
                    new UnimodEntry("IodoU-AMP",322.020217,322.1654,"C9H11N2O9P1"),
                    new UnimodEntry("ITRAQ114",144.105918,144.1680,"144.105918"),
                    new UnimodEntry("ITRAQ115",144.099599,144.1688,"144.099599"),
                    new UnimodEntry("Isopropylphospho",122.013281,122.0596,"C3H7O3P1"),
                    new UnimodEntry("LeuArgGlyGly",383.228103,383.446,"C16H29N7O4"),
                    new UnimodEntry("Lipoyl",188.032956,188.3103,"C8H12O1S2"),
                    new UnimodEntry("Lys->Allysine",-1.031634,-1.0311,"H-3N-1O1"),
                    new UnimodEntry("Lys->AminoadipicAcid",14.96328,14.9683,"H-3N-1O2"),
                    new UnimodEntry("Lys-loss",-128.094963,-128.1723,"C-6H-12N-2O-1"),
                    new UnimodEntry("Maleimide-PEO2-Biotin",525.225719,525.6183,"C23H35N5O7S1"),
                    new UnimodEntry("Menadione",170.036779,170.1641,"C11H6O2"),
                    new UnimodEntry("Met->Hse",-29.992806,-30.0922,"C-1H-2O1S-1"),
                    new UnimodEntry("Met->Hsl",-48.003371,-48.1075,"C-1H-4S-1"),
                    new UnimodEntry("Methyl",14.01565,14.0266,"C1H2"),
                    new UnimodEntry("Methyl+Deamidated",14.999666,15.0113,"C1H1N-1O1"),
                    new UnimodEntry("Methylamine",13.031634,13.0418,"C1H3N1O-1"),
                    new UnimodEntry("Methylpyrroline",109.052764,109.1259,"C6H7N1O1"),
                    new UnimodEntry("Methylthio",45.987721,46.0916,"C1H2S1"),
                    new UnimodEntry("Microcin",831.197041,831.6871,"C36H37N3O20"),
                    new UnimodEntry("MicrocinC7",386.110369,386.3003,"C13H19N6O6P1"),
                    new UnimodEntry("Myristoleyl",208.182715,208.3398,"C14H24O1"),
                    new UnimodEntry("Myristoyl",210.198366,210.3556,"C14H26O1"),
                    new UnimodEntry("Myristoyl+Delta:H(-4)",206.167065,206.3239,"C14H22O1"),
                    new UnimodEntry("NBS",152.988449,153.1585,"C6H3N1O2S1"),
                    new UnimodEntry("NDA",175.042199,175.1855,"C13H5N1"),
                    new UnimodEntry("NEIAA",85.052764,85.1045,"C4H7N1O1"),
                    new UnimodEntry("Nethylmaleimide",125.047679,125.1253,"C6H7N1O2"),
                    new UnimodEntry("Nethylmaleimide+water",143.058243,143.1406,"C6H9N1O3"),
                    new UnimodEntry("NHS-LC-Biotin",339.161662,339.453,"C16H25N3O3S1"),
                    new UnimodEntry("NIPCAM",99.068414,99.1311,"C5H9N1O1"),
                    new UnimodEntry("Nitro",44.985078,44.9976,"H-1N1O2"),
                    new UnimodEntry("Nitrosyl",28.990164,28.9982,"H-1N1O1"),
                    new UnimodEntry("Nmethylmaleimide",111.032028,111.0987,"C5H5N1O2"),
                    new UnimodEntry("Nmethylmaleimide+water",129.042593,129.114,"C5H7N1O3"),
                    new UnimodEntry("Octanoyl",126.104465,126.1962,"C8H14O1"),
                    new UnimodEntry("Oxidation",15.994915,15.9994,"O1"),
                    new UnimodEntry("Palmitoleyl",236.214016,236.3929,"C16H28O1"),
                    new UnimodEntry("Palmitoyl",238.229666,238.4088,"C16H30O1"),
                    new UnimodEntry("PEO-Iodoacetyl-LC-Biotin",414.193691,414.5196,"C18H30N4O5S1"),
                    new UnimodEntry("PET",121.035005,121.2028,"C7H7N1O-1S1"),
                    new UnimodEntry("Phenylisocyanate",119.037114,119.1207,"C7H5N1O1"),
                    new UnimodEntry("Phospho",79.966331,79.9799,"H1O3P1"),
                    new UnimodEntry("Phosphoadenosine",329.05252,329.2059,"C10H12N5O6P1"),
                    new UnimodEntry("Phosphoguanosine",345.047435,345.2053,"C10H12N5O7P1"),
                    new UnimodEntry("Phosphopantetheine",340.085794,340.333,"C11H21N2O6S1P1"),
                    new UnimodEntry("PhosphoribosyldephosphoCoA",881.146904,881.6335,"C26H42N7O19S1P3"),
                    new UnimodEntry("PhosphoUridine",306.025302,306.166,"C9H11N2O8P1"),
                    new UnimodEntry("Phycocyanobilin",586.279135,586.678,"C33H38N4O6"),
                    new UnimodEntry("Phycoerythrobilin",588.294785,588.6939,"C33H40N4O6"),
                    new UnimodEntry("Phytochromobilin",584.263485,584.6621,"C33H36N4O6"),
                    new UnimodEntry("Piperidine",68.0626,68.117,"C5H8"),
                    new UnimodEntry("Pro->pyro-Glu",13.979265,13.9835,"H-2O1"),
                    new UnimodEntry("Pro->Pyrrolidinone",-30.010565,-30.026,"C-1H-2O-1"),
                    new UnimodEntry("Pro->Pyrrolidone",-27.994915,-28.0101,"C-1O-1"),
                    new UnimodEntry("Propionamide",71.037114,71.0779,"C3H5N1O1"),
                    new UnimodEntry("Propionyl",56.026215,56.0633,"C3H4O1"),
                    new UnimodEntry("PropylNAGthiazoline",232.064354,232.2768,"C9H14N1O4S1"),
                    new UnimodEntry("PyMIC",134.048013,134.1353,"C7H6N2O1"),
                    new UnimodEntry("PyridoxalPhosphate",229.014009,229.1266,"C8H8N1O5P1"),
                    new UnimodEntry("Pyridylacetyl",119.037114,119.1207,"C7H5N1O1"),
                    new UnimodEntry("Pyridylethyl",105.057849,105.1372,"C7H7N1"),
                    new UnimodEntry("Pyro-carbamidomethyl",39.994915,40.0208,"C2O1"),
                    new UnimodEntry("PyruvicAcidIminyl",70.005479,70.0468,"C3H2O2"),
                    new UnimodEntry("QAT",171.149738,171.26,"C9H19N2O1"),
                    new UnimodEntry("Quinone",29.974179,29.9829,"H-2O2"),
                    new UnimodEntry("Retinylidene",266.203451,266.4204,"C20H26"),
                    new UnimodEntry("Ser->LacticAcid",-15.010899,-15.0146,"H-1N-1"),
                    new UnimodEntry("SMA",127.063329,127.1412,"C6H9N1O2"),
                    new UnimodEntry("SPITC",214.971084,215.2495,"C7H5N1O3S2"),
                    new UnimodEntry("Succinyl",100.016044,100.0728,"C4H4O3"),
                    new UnimodEntry("SulfanilicAcid",155.004099,155.1744,"C6H5N1O2S1"),
                    new UnimodEntry("Sulfide",31.972071,32.065,"S1"),
                    new UnimodEntry("Sulfo",79.956815,80.0632,"O3S1"),
                    new UnimodEntry("Sulfo-NHS-LC-LC-Biotin",452.245726,452.6106,"C22H36N4O4S1"),
                    new UnimodEntry("Thioacyl",87.998285,88.1283,"C3H4O1S1"),
                    new UnimodEntry("Thiophos-S-S-biotin",525.142894,525.6658,"C19H34N4O5S3P1"),
                    new UnimodEntry("Thiophospho",95.943487,96.0455,"H1O2S1P1"),
                    new UnimodEntry("TMAB",128.107539,128.1922,"C7H14N1O1"),
                    new UnimodEntry("Trimethyl",42.04695,42.0797,"C3H6"),
                    new UnimodEntry("Trioxidation",47.984744,47.9982,"O3"),
                    new UnimodEntry("Tripalmitate",788.725777,789.3049,"C51H96O5"),
                    new UnimodEntry("Trp->Hydroxykynurenin",19.989829,19.9881,"C-1O2"),
                    new UnimodEntry("Trp->Kynurenin",3.994915,3.9887,"C-1O1"),
                    new UnimodEntry("Trp->Oxolactone",13.979265,13.9835,"H-2O1"),
                    new UnimodEntry("Tyr->Dha",-94.041865,-94.1112,"C-6H-6O-1"),
                    new UnimodEntry("Xlink:DMP",122.084398,122.1677,"C7H10N2"),
                    new UnimodEntry("Xlink:DMP-s",154.110613,154.2096,"C8H14N2O1"),
                    new UnimodEntry("Xlink:SSD",253.095023,253.2512,"C12H15N1O5")
                };

            public static UnimodEntry GetClosestMod(double monoMass)
            {
                double closestAmount = double.MaxValue;
                UnimodEntry closestMod = null;
                foreach (var entry in FullUnimodList)
                {
                    var difference = Math.Abs(entry.MonoMass - monoMass);
                    if (difference < closestAmount)
                    {
                        closestAmount = difference;
                        closestMod = entry;
                    }
                }
                return closestMod;
            }
        }

        #endregion

        public struct Modification
        {
            public string Residue { get; set; }
            public double Mass { get; set; }
            public string Type { get; set; }

        }
    }
}
