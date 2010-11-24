using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace UniModCompiler
{
    class Program
    {
        private static Dictionary<string, mod_t> _dictStructuralMods;
        private static Dictionary<string, mod_t> _dictIsotopeMods;
        private static List<Mod> _listedMods;
        private static List<Mod> _listedHiddenMods;

        static void Main(string[] args)
        {
            SequenceMassCalc();

            _dictStructuralMods = new Dictionary<string, mod_t>();
            _dictIsotopeMods = new Dictionary<string, mod_t>();
             
            // Read in XML, creating a dictionary of mod titles to mods.
            StreamReader reader = new StreamReader(args[0]);
            XmlSerializer serializer = new XmlSerializer(typeof(Modification));
            var modifications = ((Modification) serializer.Deserialize(reader)).modifications;               
            reader.Close();
            foreach(mod_t mod in modifications)
            {
                // Only add to the dictionary of isotope mods it is truly an isotope mod.
                if (Equals(mod.specificity[0].classification, classification_t.Isotopiclabel)
                    && CheckTrueIsotopeMod(mod.delta.element)) 
                    _dictIsotopeMods.Add(mod.title, mod);
                else
                    _dictStructuralMods.Add(mod.title, mod);
            }
          
            _listedMods = new List<Mod>();
            _listedHiddenMods = new List<Mod>();

            for (int i = 1; i < args.Length; i++)
            {
                // Read in line seperated list, creating lists of the modifications we are looking for.
                reader = new StreamReader(args[i]);
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Contains("[MODS]"))
                        _listedMods.AddRange(ReadListedMods(reader));
                    if (line.Contains("[HIDDEN_MODS]"))
                        _listedHiddenMods.AddRange(ReadListedMods(reader));
                }
            }

            // Writing the output file.
            StreamWriter writer = new StreamWriter(@"..\..\..\..\Model\DocSettings\UniMod.cs");
            var templateStream = typeof (Program).Assembly.GetManifestResourceStream("UniModCompiler.UniModTemplate.cs");
            if (templateStream == null)
                throw new IOException("Failed to open template");
            StreamReader templateReader = new StreamReader(templateStream);
            string templateLine;
            while((templateLine = templateReader.ReadLine()) != null)
            {
                if(templateLine.Contains(@"// INSERT StructuralMods"))
                    WriteMods(writer, false, false);
                else if (templateLine.Contains(@"// INSERT IsotopeMods"))
                    WriteMods(writer, false, true);
                else if (templateLine.Contains(@"// INSERT HiddenStructuralMods"))
                    WriteMods(writer, true, false);
                else if (templateLine.Contains(@"// INSERT HiddenIsotopeMods"))
                    WriteMods(writer, true, true);
                else
                {
                    writer.WriteLine(templateLine);
                }
            }
            writer.Close();

            foreach (Mod listedMod in _listedMods)
            {
                Console.WriteLine("Unable to match: " + listedMod.Name);
            }
            foreach (Mod listedMod in _listedHiddenMods)
            {
                Console.WriteLine("Unable to match: " + listedMod.Name);
            }
        }

        /// <summary>
        /// Check if the given sequence is an isotope modification.
        /// </summary>
        private static bool CheckTrueIsotopeMod(IEnumerable<elem_ref_t> element)
        {
            int label15N = 0;
            int label13C = 0;
            int label18O = 0;
            int label2H = 0;
            bool has15N = false;
            bool has13C = false;
            bool has18O = false;
            bool has2H = false;

            // If we appear to have heavy labeling, check that the formula balances.
            foreach (elem_ref_t n in element)
            {
                has15N = has15N || Equals(n.symbol, "15N");
                has13C = has13C || Equals(n.symbol, "13C");
                has18O = has18O || Equals(n.symbol, "18O");
                has2H = has2H || Equals(n.symbol, "2H");
                label15N += Equals(n.symbol, "15N") || Equals(n.symbol, "N") ? 0 : Int32.Parse(n.number);
                label13C += Equals(n.symbol, "13C") || Equals(n.symbol, "C") ? 0 : Int32.Parse(n.number);
                label18O += Equals(n.symbol, "18O") || Equals(n.symbol, "O") ? 0 : Int32.Parse(n.number);
                label2H += Equals(n.symbol, "2H") || Equals(n.symbol, "H") ? 0 : Int32.Parse(n.number);
            }

            return (has15N || has13C || has18O || has2H) &&
                (!has15N || label15N == 0) && (!has13C || label13C == 0) 
                && (!has18O || label18O == 0) && (!has2H || label2H == 0);
        }

        /// <summary>
        /// Read a file containing line seperated lists of modifications.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        private static IEnumerable<Mod> ReadListedMods(StreamReader reader)
        {
            List<Mod> listedMods = new List<Mod>();
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (Equals("", line) || line.Contains("["))
                    return listedMods;
                var splitLine = line.Split(new[] { ' ' }, 2);
                var title = splitLine[0];
                splitLine[1] = splitLine[1].Remove(0, 1);
                splitLine[1] = splitLine[1].Remove(splitLine[1].Length - 1);
                var aasAndTerms = splitLine[1].Split(' ');
                Terminal? terminal = null;
                char[] aas = null;
                foreach (string s in aasAndTerms)
                {
                    if (s.Contains("C-term"))
                        terminal = Terminal.C;
                    else if (s.Contains("N-term"))
                        terminal = Terminal.N;
                    else
                    {
                        aas = s.ToCharArray();
                    }
                }
                listedMods.Add(new Mod { Name = line, Title = title, AAs = aas ?? new char[0], Terminal = terminal });
            }
            return listedMods;
        }
              
        /// <summary>
        /// Search for the listed modifications within the XML modifications, and write matches to the C# file.
        /// </summary>
        private static void WriteMods(StreamWriter writer, bool hidden, bool isotopic)
        {
            var dict = isotopic ? _dictIsotopeMods : _dictStructuralMods;

            IList<Mod> listedModsCopy = hidden ? new List<Mod>(_listedHiddenMods) : new List<Mod>(_listedMods);
            // Look for the listed mods in the given dictionary.
            foreach (Mod mod in listedModsCopy)
            {
                mod_t dictMod;
                if (!dict.TryGetValue(mod.Title, out dictMod))
                    continue;

                // For each AA in the listed modifcation, make sure the dictionary mod contains it.
                bool foundAllAAs = true;
                foreach (char aa in mod.AAs)
                {
                    foundAllAAs = foundAllAAs && ContainsSite(dictMod, aa.ToString());
                }              
                // Also check to make sure the dictionary mod contains the terminal, if any.
                foundAllAAs = foundAllAAs && 
                    (mod.Terminal == null || !mod.Terminal.Equals(Terminal.C) || ContainsSite(dictMod, "C-term"));
                foundAllAAs = foundAllAAs && 
                    (mod.Terminal == null || !mod.Terminal.Equals(Terminal.N) || ContainsSite(dictMod, "N-term"));

                // If the dictionary mod does not contain all desired AAs, continue.
                if (!foundAllAAs)
                    continue;

                string skylineFormula = BuildFormula(dictMod.delta.element);
                if(skylineFormula.Length == 0)
                    continue;
                 
                writer.WriteLine(
                    String.Format(@"                new StaticMod(""{0}"", {1}, {2}, {3}),",
                                  mod.Name, 
                                  mod.AAs.Length > 0 ? '"' + BuildAAString(mod.AAs) + '"' : "null",
                                  mod.Terminal != null ? "ModTerminus." + mod.Terminal : "null",
                                  isotopic && mod.AAs.Length > 0 ? BuildLabelAtomsString(dictMod.delta.element, mod.AAs) 
                                    : '"' + BuildFormula(dictMod.delta.element) + '"'));
                
                if (hidden)
                    _listedHiddenMods.Remove(mod);
                else
                    _listedMods.Remove(mod);
            }
        }

        private static string BuildAAString(char[] aas)
        {
            string result = "" + aas[0];
            for (int i = 1; i < aas.Length; i++ )
            {
                result += ", " + aas[i];
            }
            return result;
        }

        /// <summary>
        /// Check that an AA from the list of modifications is found in the XML modification.
        /// </summary>
        private static bool ContainsSite(mod_t modification, string aa)
        {
            foreach (specificity_t specificty in modification.specificity)
            {
                if (Equals(specificty.site, aa))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Check for label atoms in the given sequence matching the given aa, and create a corresponding string if found.
        /// </summary>
        private static string BuildLabelAtomsString(IEnumerable<elem_ref_t> elements, IEnumerable<char> aas)
        {
            string labelAtoms = "";
            foreach (var element in elements)
            {
                char elementMatch;
                if (DICT_HEAVY_LABELS.TryGetValue(element.symbol, out elementMatch))
                {
                    foreach (char aa in aas)
                    {
                        var formula = AMINO_FORMULAS[aa];
                        int index = formula.IndexOf(elementMatch);
                        int numInFormula = (index > formula.Length - 2 || char.IsLetter(formula[index + 1]))
                                        ? 1
                                        : Int32.Parse(formula[index + 1].ToString());
                        if (element.number != numInFormula.ToString())
                            return "LabelAtoms.None";
                    }
                    var symbol = element.symbol;
                    var elementIndex = symbol.IndexOfAny(new[] { 'N', 'C', 'O', 'H' });
                    labelAtoms += "|LabelAtoms." +
                                  symbol[elementIndex] + symbol.Remove(elementIndex);
                }
            }
            return labelAtoms.Length > 0 ? labelAtoms.Substring(1) : "LabelAtoms.None";
        }

        /// <summary>
        /// Create a Skyline chemical formula given a modification from the XML.
        /// </summary>
        private static string BuildFormula(IEnumerable<elem_ref_t> elements)
        {
            string positive = "";
            string negative = " - ";
            foreach (elem_ref_t element in elements)
            {
                int num = Int32.Parse(element.number);
                string symbol = element.symbol;
                char aa;
                symbol = DICT_HEAVY_LABELS.TryGetValue(symbol, out aa) ? aa.ToString() + '\'' : symbol;
                if (num < 0)
                    negative += symbol + (num == -1 ? "" : (num * -1).ToString());
                else
                    positive += symbol + (num == 1 ? "" : num.ToString());
            }
            string formula = positive + (negative.Length > 3 ? negative : "");
            return positive.Length > 0 ? formula : formula.Replace(" ", "");
        }

        private struct Mod
        {
            public string Name { get; set; }
            public string Title { get; set; }
            public char[] AAs { get; set; }           
            public Terminal? Terminal { get; set; }
        }

        private enum Terminal
        {
            C, N
        }

        private static readonly Dictionary<string, char> DICT_HEAVY_LABELS 
            = new Dictionary<string, char> {{"15N", 'N'}, {"13C", 'C'}, {"18O", 'O'}, {"2H", 'H'}};

        private static readonly string[] AMINO_FORMULAS = new string[128];

        private static void SequenceMassCalc()
        {
            // ReSharper disable CharImplicitlyConvertedToNumeric
            AMINO_FORMULAS['a'] = AMINO_FORMULAS['A'] = "C3H5ON";
            AMINO_FORMULAS['c'] = AMINO_FORMULAS['C'] = "C3H5ONS";
            AMINO_FORMULAS['d'] = AMINO_FORMULAS['D'] = "C4H5O3N";
            AMINO_FORMULAS['e'] = AMINO_FORMULAS['E'] = "C5H7O3N";
            AMINO_FORMULAS['f'] = AMINO_FORMULAS['F'] = "C9H9ON";
            AMINO_FORMULAS['g'] = AMINO_FORMULAS['G'] = "C2H3ON";
            AMINO_FORMULAS['h'] = AMINO_FORMULAS['H'] = "C6H7ON3";
            AMINO_FORMULAS['i'] = AMINO_FORMULAS['I'] = "C6H11ON";
            AMINO_FORMULAS['k'] = AMINO_FORMULAS['K'] = "C6H12ON2";
            AMINO_FORMULAS['l'] = AMINO_FORMULAS['L'] = "C6H11ON";
            AMINO_FORMULAS['m'] = AMINO_FORMULAS['M'] = "C5H9ONS";
            AMINO_FORMULAS['n'] = AMINO_FORMULAS['N'] = "C4H6O2N2";
            AMINO_FORMULAS['o'] = AMINO_FORMULAS['O'] = "C12H19N3O2";
            AMINO_FORMULAS['p'] = AMINO_FORMULAS['P'] = "C5H7ON";
            AMINO_FORMULAS['q'] = AMINO_FORMULAS['Q'] = "C5H8O2N2";
            AMINO_FORMULAS['r'] = AMINO_FORMULAS['R'] = "C6H12ON4";
            AMINO_FORMULAS['s'] = AMINO_FORMULAS['S'] = "C3H5O2N";
            AMINO_FORMULAS['t'] = AMINO_FORMULAS['T'] = "C4H7O2N";
            AMINO_FORMULAS['u'] = AMINO_FORMULAS['U'] = "C3H5NOSe";
            AMINO_FORMULAS['v'] = AMINO_FORMULAS['V'] = "C5H9ON";
            AMINO_FORMULAS['w'] = AMINO_FORMULAS['W'] = "C11H10ON2";
            AMINO_FORMULAS['y'] = AMINO_FORMULAS['Y'] = "C9H9O2N";
            // ReSharper restore CharImplicitlyConvertedToNumeric
        }
    }

}
