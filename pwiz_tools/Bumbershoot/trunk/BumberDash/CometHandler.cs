using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace BumberDash
{
    class CometParams
    {
        public double PrecursorTolerance { get; set; }
        public int PrecursorUnit { get; set; }
        public int Specificity { get; set; }
        public double StaticCysteineMod { get; set; }
        public List<Modification> DynamicModifications { get; set; } //n-term &c-term need special handling
        public int MaxMods { get; set; }
        public double FragmentBinTolerance { get; set; } //unique
        public double FragmentBinOffset { get; set; } //unique
        public string OutputSuffix { get; set; }
        public int CleavageAgent { get; set; }
        private int _maxMissedCleavages;
        public int MaxMissedCleavages
        {
            get { return _maxMissedCleavages; }
            set
            {
                if (value <= 5) _maxMissedCleavages = value;
                else throw new Exception("Max allowed missed cleavages is 5");
            }
        }


        private CometParams(int configuration)
        {
            if (configuration == Preconfigurations.IonTrap)
            {
                FragmentBinTolerance = 1.0005;
                FragmentBinOffset = 0.4;
            }
            else if (configuration == Preconfigurations.Tof)
            {
                FragmentBinTolerance = 0.1;
                FragmentBinOffset = 0.0;
            }
            else if (configuration == Preconfigurations.HighResolution)
            {
                FragmentBinTolerance = 0.02;
                FragmentBinOffset = 0.0;
            }
            else throw new Exception("Invalid Comet Preconfiguration");

            PrecursorTolerance = 10;
            PrecursorUnit = PrecursorUnitOptions.PPM;
            Specificity = SpecificityOptions.Tryptic;
            MaxMissedCleavages = 2;
            StaticCysteineMod = 0;
            DynamicModifications = new List<Modification>();
            MaxMods = 3;
            OutputSuffix = "_CM";
        }

        public static CometParams GetIonTrapParams()
        {
            return new CometParams(Preconfigurations.IonTrap);
        }

        public static CometParams GetTofParams()
        {
            return new CometParams(Preconfigurations.Tof);
        }

        public static CometParams GetHighResParams()
        {
            return new CometParams(Preconfigurations.HighResolution);
        }

        #region Support classes
        public static class PrecursorUnitOptions
        {
            public static int Daltons = 0;
            public static int PPM = 2;
        }

        public static class SpecificityOptions
        {
            public static int SemiTryptic = 1;
            public static int Tryptic = 2;
        }

        public static Dictionary<string,int> CleavageAgentOptions
        {
            get
            {
                return new Dictionary<string, int>
                    {
                        {"No_enzyme", 0},
                        {"Trypsin", 1},
                        {"Trypsin/P", 2},
                        {"Lys-C", 3},
                        {"Lys-N", 4},
                        {"Arg-C", 5},
                        {"Asp-N", 6},
                        {"CNBr", 7},
                        {"Glu-C", 8},
                        {"PepsinA", 9},
                        {"Chymotrypsin", 10}
                    };
            }
        }

        private static class Preconfigurations
        {
            public static int IonTrap = 1;
            public static int Tof = 2;
            public static int HighResolution = 3;
        }

        public class Modification
        {
            public string Residue { get; set; }
            public double MassChange { get; set; }

            public Modification(string residue, double massCharge)
            {
                Residue = residue;
                MassChange = massCharge;
            }

            public bool isNterminal()
            {
                return Residue == "(";
            }

            public bool isCterminal()
            {
                return Residue == ")";
            }
        }
        #endregion
    }

    class CometHandler
    {
        public static string CometParamsToFileContents(CometParams options)
        {
            var configFile = new StringWriter();
            configFile.WriteLine("# comet_version 2013.02 rev. 1");
            configFile.WriteLine("# Comet MS/MS search engine parameters file.");
            configFile.WriteLine("# Everything following the '#' symbol is treated as a comment.");
            configFile.WriteLine("");
            configFile.WriteLine("decoy_search = 1                       # 0=no (default), 1=concatenated search, 2=separate search");
            configFile.WriteLine("");
            configFile.WriteLine("num_threads = 0                        # 0=poll CPU to set num threads; else specify num threads directly (max 64)");
            configFile.WriteLine("");
            configFile.WriteLine("#");
            configFile.WriteLine("# masses");
            configFile.WriteLine("#");
            configFile.WriteLine("peptide_mass_tolerance = {0}",options.PrecursorTolerance.ToString(CultureInfo.InvariantCulture));
            configFile.WriteLine("peptide_mass_units = {0}                 # 0=amu, 1=mmu, 2=ppm",options.PrecursorUnit);
            configFile.WriteLine("mass_type_parent = 1                   # 0=average masses, 1=monoisotopic masses");
            configFile.WriteLine("mass_type_fragment = 1                 # 0=average masses, 1=monoisotopic masses");
            configFile.WriteLine("precursor_tolerance_type = 1           # 0=MH+ (default), 1=precursor m/z");
            configFile.WriteLine("isotope_error = 1                      # 0=off, 1=on -1/0/1/2/3 (standard C13 error), 2= -8/-4/0/4/8 (for +4/+8 labeling)");
            configFile.WriteLine("");
            configFile.WriteLine("#");
            configFile.WriteLine("# search enzyme");
            configFile.WriteLine("#");
            configFile.WriteLine("search_enzyme_number = {0}               # choose from list at end of this params file", options.CleavageAgent);
            configFile.WriteLine("num_enzyme_termini = {0}                 # valid values are 1 (semi-digested), 2 (fully digested, default), 8 N-term, 9 C-term",options.Specificity);
            configFile.WriteLine("allowed_missed_cleavage = {0}            # maximum value is 5; for enzyme search",options.MaxMissedCleavages);
            configFile.WriteLine("");
            configFile.WriteLine("#");
            configFile.WriteLine("# Up to 6 variable modifications are supported");
            configFile.WriteLine("# format:  <mass> <residues> <0=variable/1=binary> <max mods per a peptide>");
            configFile.WriteLine("#     e.g. 79.966331 STY 0 3");
            configFile.WriteLine("#");
            for (var x = 1; x <= 6; x++)
                if (options.DynamicModifications.Count >= x)
                    configFile.WriteLine("variable_mod{0} = {1}   {2} 0 3", x, options.DynamicModifications[x - 1].MassChange.ToString(CultureInfo.InvariantCulture),
                                      options.DynamicModifications[x-1].Residue);
                else
                    configFile.WriteLine("variable_mod{0} = 0.0 X 0 3", x);
            configFile.WriteLine("max_variable_mods_in_peptide = {0}",options.MaxMods);
            configFile.WriteLine("");
            configFile.WriteLine("#");
            configFile.WriteLine("# fragment ions");
            configFile.WriteLine("#");
            configFile.WriteLine("# ion trap ms/ms:  1.0005 tolerance, 0.4 offset (mono masses), theoretical_fragment_ions = 1");
            configFile.WriteLine("# high res ms/ms:    0.02 tolerance, 0.0 offset (mono masses), theoretical_fragment_ions = 0");
            configFile.WriteLine("#");
            configFile.WriteLine("fragment_bin_tol = {0}              # binning to use on fragment ions", options.FragmentBinTolerance.ToString(CultureInfo.InvariantCulture));
            configFile.WriteLine("fragment_bin_offset = {0}              # offset position to start the binning (0.0 to 1.0)", options.FragmentBinOffset.ToString(CultureInfo.InvariantCulture));
            configFile.WriteLine("theoretical_fragment_ions = 0          # 0=default peak shape, 1=M peak only");
            configFile.WriteLine("use_A_ions = 0");
            configFile.WriteLine("use_B_ions = 1");
            configFile.WriteLine("use_C_ions = 0");
            configFile.WriteLine("use_X_ions = 0");
            configFile.WriteLine("use_Y_ions = 1");
            configFile.WriteLine("use_Z_ions = 0");
            configFile.WriteLine("use_NL_ions = 1                        # 0=no, 1=yes to consider NH3/H2O neutral loss peaks");
            configFile.WriteLine("use_sparse_matrix = 1");
            configFile.WriteLine("");
            configFile.WriteLine("#");
            configFile.WriteLine("# output");
            configFile.WriteLine("#");
            configFile.WriteLine("output_sqtstream = 0                   # 0=no, 1=yes  write sqt to standard output");
            configFile.WriteLine("output_sqtfile = 0                     # 0=no, 1=yes  write sqt file");
            configFile.WriteLine("output_txtfile = 0                     # 0=no, 1=yes  write tab-delimited txt file");
            configFile.WriteLine("output_pepxmlfile = 1                  # 0=no, 1=yes  write pep.xml file");
            configFile.WriteLine("output_pinxmlfile = 0                  # 0=no, 1=yes  write pin.xml file");
            configFile.WriteLine("output_outfiles = 0                    # 0=no, 1=yes  write .out files");
            configFile.WriteLine("print_expect_score = 1                 # 0=no, 1=yes to replace Sp with expect in out & sqt");
            configFile.WriteLine("num_output_lines = 5                   # num peptide results to show");
            configFile.WriteLine("show_fragment_ions = 0                 # 0=no, 1=yes for out files only");
            configFile.WriteLine("output_suffix = {0}", options.OutputSuffix);
            configFile.WriteLine("");
            configFile.WriteLine("sample_enzyme_number = 1               # Sample enzyme which is possibly different than the one applied to the search.");
            configFile.WriteLine("                                       # Used to calculate NTT & NMC in pepXML output (default=1 for trypsin).");
            configFile.WriteLine("");
            configFile.WriteLine("#");
            configFile.WriteLine("# mzXML parameters");
            configFile.WriteLine("#");
            configFile.WriteLine("scan_range = 0 0                       # start and scan scan range to search; 0 as 1st entry ignores parameter");
            configFile.WriteLine("precursor_charge = 0 0                 # precursor charge range to analyze; does not override mzXML charge; 0 as 1st entry ignores parameter");
            configFile.WriteLine("ms_level = 2                           # MS level to analyze, valid are levels 2 (default) or 3");
            configFile.WriteLine("activation_method = ALL                # activation method; used if activation method set; allowed ALL, CID, ECD, ETD, PQD, HCD, IRMPD");
            configFile.WriteLine("");
            configFile.WriteLine("#");
            configFile.WriteLine("# misc parameters");
            configFile.WriteLine("#");
            configFile.WriteLine("digest_mass_range = 600.0 5000.0       # MH+ peptide mass range to analyze");
            configFile.WriteLine("num_results = 5                        # number of search hits to store internally");
            configFile.WriteLine("skip_researching = 1                   # for '.out' file output only, 0=search everything again (default), 1=don't search if .out exists");
            configFile.WriteLine("max_fragment_charge = 3                # set maximum fragment charge state to analyze (allowed max 5)");
            configFile.WriteLine("max_precursor_charge = 6               # set maximum precursor charge state to analyze (allowed max 9)");
            configFile.WriteLine("nucleotide_reading_frame = 0           # 0=proteinDB, 1-6, 7=forward three, 8=reverse three, 9=all six");
            configFile.WriteLine("clip_nterm_methionine = 0              # 0=leave sequences as-is; 1=also consider sequence w/o N-term methionine");
            configFile.WriteLine("spectrum_batch_size = 8000           # max. # of spectra to search at a time; 0 to search the entire scan range in one loop");
            configFile.WriteLine("decoy_prefix = XXX_                  # decoy entries are denoted by this string which is pre-pended to each protein accession");
            configFile.WriteLine("");
            configFile.WriteLine("#");
            configFile.WriteLine("# spectral processing");
            configFile.WriteLine("#");
            configFile.WriteLine("minimum_peaks = 10                     # required minimum number of peaks in spectrum to search");
            configFile.WriteLine("minimum_intensity = 0                  # minimum intensity value to read in");
            configFile.WriteLine("remove_precursor_peak = 0              # 0=no, 1=yes, 2=all charge reduced precursor peaks (for ETD)");
            configFile.WriteLine("remove_precursor_tolerance = 1.5       # +- Da tolerance for precursor removal");
            configFile.WriteLine("clear_mz_range = 0.0 0.0               # for iTRAQ/TMT type data; will clear out all peaks in the specified m/z range");
            configFile.WriteLine("");
            configFile.WriteLine("#");
            configFile.WriteLine("# additional modifications");
            configFile.WriteLine("#");
            configFile.WriteLine("");
            configFile.WriteLine("variable_C_terminus = 0.0");
            configFile.WriteLine("variable_N_terminus = 0.0");
            configFile.WriteLine("variable_C_terminus_distance = -1      # -1=all peptides, 0=protein terminus, 1-N = maximum offset from C-terminus");
            configFile.WriteLine("variable_N_terminus_distance = -1      # -1=all peptides, 0=protein terminus, 1-N = maximum offset from N-terminus");
            configFile.WriteLine("");
            configFile.WriteLine("add_Cterm_peptide = 0.0");
            configFile.WriteLine("add_Nterm_peptide = 0.0");
            configFile.WriteLine("add_Cterm_protein = 0.0");
            configFile.WriteLine("add_Nterm_protein = 0.0");
            configFile.WriteLine("");
            configFile.WriteLine("add_G_glycine = 0.0000                 # added to G - avg.  57.0513, mono.  57.02146");
            configFile.WriteLine("add_A_alanine = 0.0000                 # added to A - avg.  71.0779, mono.  71.03711");
            configFile.WriteLine("add_S_serine = 0.0000                  # added to S - avg.  87.0773, mono.  87.03203");
            configFile.WriteLine("add_P_proline = 0.0000                 # added to P - avg.  97.1152, mono.  97.05276");
            configFile.WriteLine("add_V_valine = 0.0000                  # added to V - avg.  99.1311, mono.  99.06841");
            configFile.WriteLine("add_T_threonine = 0.0000               # added to T - avg. 101.1038, mono. 101.04768");
            configFile.WriteLine("add_C_cysteine = {0}             # added to C - avg. 103.1429, mono. 103.00918", options.StaticCysteineMod.ToString(CultureInfo.InvariantCulture));
            configFile.WriteLine("add_L_leucine = 0.0000                 # added to L - avg. 113.1576, mono. 113.08406");
            configFile.WriteLine("add_I_isoleucine = 0.0000              # added to I - avg. 113.1576, mono. 113.08406");
            configFile.WriteLine("add_N_asparagine = 0.0000              # added to N - avg. 114.1026, mono. 114.04293");
            configFile.WriteLine("add_D_aspartic_acid = 0.0000           # added to D - avg. 115.0874, mono. 115.02694");
            configFile.WriteLine("add_Q_glutamine = 0.0000               # added to Q - avg. 128.1292, mono. 128.05858");
            configFile.WriteLine("add_K_lysine = 0.0000                  # added to K - avg. 128.1723, mono. 128.09496");
            configFile.WriteLine("add_E_glutamic_acid = 0.0000           # added to E - avg. 129.1140, mono. 129.04259");
            configFile.WriteLine("add_M_methionine = 0.0000              # added to M - avg. 131.1961, mono. 131.04048");
            configFile.WriteLine("add_O_ornithine = 0.0000               # added to O - avg. 132.1610, mono  132.08988");
            configFile.WriteLine("add_H_histidine = 0.0000               # added to H - avg. 137.1393, mono. 137.05891");
            configFile.WriteLine("add_F_phenylalanine = 0.0000           # added to F - avg. 147.1739, mono. 147.06841");
            configFile.WriteLine("add_R_arginine = 0.0000                # added to R - avg. 156.1857, mono. 156.10111");
            configFile.WriteLine("add_Y_tyrosine = 0.0000                # added to Y - avg. 163.0633, mono. 163.06333");
            configFile.WriteLine("add_W_tryptophan = 0.0000              # added to W - avg. 186.0793, mono. 186.07931");
            configFile.WriteLine("add_B_user_amino_acid = 0.0000         # added to B - avg.   0.0000, mono.   0.00000");
            configFile.WriteLine("add_J_user_amino_acid = 0.0000         # added to J - avg.   0.0000, mono.   0.00000");
            configFile.WriteLine("add_U_user_amino_acid = 0.0000         # added to U - avg.   0.0000, mono.   0.00000");
            configFile.WriteLine("add_X_user_amino_acid = 0.0000         # added to X - avg.   0.0000, mono.   0.00000");
            configFile.WriteLine("add_Z_user_amino_acid = 0.0000         # added to Z - avg.   0.0000, mono.   0.00000");
            configFile.WriteLine("");
            configFile.WriteLine("#");
            configFile.WriteLine("# COMET_ENZYME_INFO _must_ be at the end of this parameters file");
            configFile.WriteLine("#");
            configFile.WriteLine("[COMET_ENZYME_INFO]");
            configFile.WriteLine("0.  No_enzyme              0      -           -");
            configFile.WriteLine("1.  Trypsin                1      KR          P");
            configFile.WriteLine("2.  Trypsin/P              1      KR          -");
            configFile.WriteLine("3.  Lys_C                  1      K           P");
            configFile.WriteLine("4.  Lys_N                  0      K           -");
            configFile.WriteLine("5.  Arg_C                  1      R           P");
            configFile.WriteLine("6.  Asp_N                  0      D           -");
            configFile.WriteLine("7.  CNBr                   1      M           -");
            configFile.WriteLine("8.  Glu_C                  1      DE          P");
            configFile.WriteLine("9.  PepsinA                1      FL          P");
            configFile.WriteLine("10. Chymotrypsin           1      FWYL        P");
            configFile.WriteLine("");
            return configFile.ToString();
        }

        public static CometParams FileContentsToCometParams(string fileContents)
        {
            var config = CometParams.GetIonTrapParams();
            var fileLines = fileContents.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            var rx = new Regex(@"^([\w]+)\s*=\s*([\w\s\.]+)");

            foreach (var nextLine in fileLines)
            {
                if (!rx.IsMatch(nextLine))
                    continue;
                var match = rx.Match(nextLine);
                if (match.Groups.Count < 3)
                    continue;
                double nextNumber;
                var validNumber = double.TryParse(match.Groups[2].Value, out nextNumber);
                switch (match.Groups[1].Value)
                {
                    case "peptide_mass_tolerance":
                        if (!validNumber)
                            break;
                        config.PrecursorTolerance = nextNumber;
                        break;
                    case "peptide_mass_units":
                        if (!validNumber)
                            break;
                        config.PrecursorUnit = (int)nextNumber;
                        break;
                    case "search_enzyme_number":
                        if (!validNumber)
                            break;
                        config.CleavageAgent = (int)nextNumber;
                        break;
                    case "num_enzyme_termini":
                        if (!validNumber)
                            break;
                        config.Specificity = (int)nextNumber;
                        break;
                    case "allowed_missed_cleavage":
                        if (!validNumber)
                            break;
                        config.MaxMissedCleavages = (int)nextNumber;
                        break;
                    case "max_variable_mods_in_peptide":
                        if (!validNumber)
                            break;
                        config.MaxMods = (int)nextNumber;
                        break;
                    case "fragment_bin_tol":
                        if (!validNumber)
                            break;
                        config.FragmentBinTolerance = nextNumber;
                        break;
                    case "fragment_bin_offset":
                        if (!validNumber)
                            break;
                        config.FragmentBinOffset = nextNumber;
                        break;
                    case "add_C_cysteine":
                        if (!validNumber)
                            break;
                        config.StaticCysteineMod = nextNumber;
                        break;
                }
                if (match.Groups[1].Value.StartsWith("variable_mod"))
                {
                    var splitValue = match.Groups[2].Value.Split(" ".ToCharArray(),
                                                                 StringSplitOptions.RemoveEmptyEntries);
                    if (splitValue.Length < 4)
                        continue;
                    double massShift;
                    if (!double.TryParse(splitValue[0], out massShift))
                        continue;
                    config.DynamicModifications.Add(new CometParams.Modification(splitValue[1], massShift));
                }
            }

            return config;
        }
    }
}
