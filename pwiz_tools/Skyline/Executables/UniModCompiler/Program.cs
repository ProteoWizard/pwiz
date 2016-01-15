/*
 * Original author: Alana Killeen <killea .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.Linq;
using System.IO;
using System.Xml.Serialization;

namespace UniModCompiler
{
    /// <summary>
    /// Takes an XML file of modifications,
    /// We used: http://www.unimod.org/xml/unimod.xml.
    /// Takes an XML file with short-names,
    /// We used: ProteinPilot.DataDictionary.xml supplied by AB SCIEX
    /// Takes a set of text files that contain line seperated lists of modifications.
    /// We used: http://www.matrixscience.com/cgi/get_params.pl
    /// 
    /// UniModCompiler matches the modification names in the text files to the modification definitions in the XML.
    /// Modifications are then compiled into lists of static modifications for Skyline, which are output to UniMod.cs
    /// </summary>
    class Program
    {
        private static Dictionary<string, mod_t> _dictStructuralMods;
        private static Dictionary<string, mod_t> _dictIsotopeMods;
        private static List<Mod> _listedMods;
        private static List<Mod> _listedHiddenMods;
        private static List<string> _impossibleMods;
        private static Dictionary<string, ThreeLetterCodeUsed> _dictModNameToThreeLetterCode;

        private class ThreeLetterCodeUsed
        {
            public ThreeLetterCodeUsed(string threeLetterCode)
            {
                ThreeLetterCode = threeLetterCode;
            }

            public string ThreeLetterCode { get; private set; }
            public bool Used { get; set; }
        }

        private const string PROJECT_PATH = @"..\..";
        private static readonly string INPUT_FILES_PATH = Path.Combine(PROJECT_PATH, "InputFiles");

        static void Main()
        {
            try
            {
                SequenceMassCalc();

                _dictStructuralMods = new Dictionary<string, mod_t>();
                _dictIsotopeMods = new Dictionary<string, mod_t>();
                _dictModNameToThreeLetterCode = LoadShortNames(Path.Combine(INPUT_FILES_PATH, "ProteinPilot.DataDictionary.xml"));                

                // Read in XML, creating a dictionary of mod titles to mods.
                StreamReader reader = new StreamReader(Path.Combine(INPUT_FILES_PATH, "unimod.xml"));
                XmlSerializer serializer = new XmlSerializer(typeof (Modification));
                var modifications = ((Modification) serializer.Deserialize(reader)).modifications;
                reader.Close();
                foreach (mod_t mod in modifications)
                {
                    // Only add to the dictionary of isotope mods it is truly an isotope mod.
                    if (Equals(mod.specificity[0].classification, classification_t.Isotopiclabel)
                        && CheckTrueIsotopeMod(mod.delta.element))
                        _dictIsotopeMods.Add(mod.title, mod);
                    else
                    {
                        // If the title contains a colon, see if it references a root structural modification
                        int iColon = mod.title.IndexOf(':');
                        if (iColon != -1)
                        {
                            string rootTitle = mod.title.Remove(iColon);
                            mod_t modParent;
                            if (_dictStructuralMods.TryGetValue(rootTitle, out modParent))
                            {
                                var elNew = DiffMod(mod.delta.element, modParent.delta.element);
                                if (IsIsotopicDiff(elNew))
                                {
                                    mod.delta.element = elNew;
                                    // Isotope modifications don't have neutral losses.  They get them
                                    // from their structural parent.
                                    foreach (var specificity in mod.specificity)
                                        specificity.NeutralLoss = null;
                                    _dictIsotopeMods.Add(mod.title, mod);
                                    continue;
                                }
                            }
                        }
                        _dictStructuralMods.Add(mod.title, mod);
                    }
                }

                _listedMods = new List<Mod>();
                _listedHiddenMods = new List<Mod>();

                foreach(var textFilePath in Directory.GetFiles(INPUT_FILES_PATH, "*.txt"))
                {
                    // Read in line seperated list, creating lists of the modifications we are looking for.
                    reader = new StreamReader(textFilePath);
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Contains("[MODS]"))
                            _listedMods.AddRange(ReadListedMods(reader));
                        if (line.Contains("[HIDDEN_MODS]"))
                            _listedHiddenMods.AddRange(ReadListedMods(reader));
                    }
                }

                _impossibleMods = new List<string>();

                // Writing the output file.
                StreamWriter writer = new StreamWriter(Path.Combine(PROJECT_PATH, @"..\..\Model\DocSettings\UniModData.cs"));
                var templateStream =
                    typeof (Program).Assembly.GetManifestResourceStream("UniModCompiler.UniModTemplate.cs");
                if (templateStream == null)
                    throw new IOException("Failed to open template");
                StreamReader templateReader = new StreamReader(templateStream);
                string templateLine;
                while ((templateLine = templateReader.ReadLine()) != null)
                {
                    if (!templateLine.Contains(@"// ADD MODS"))
                        writer.WriteLine(templateLine);
                    else
                    {
                        WriteMods(writer, false, false);
                        WriteMods(writer, false, true);
                        WriteMods(writer, true, false);
                        WriteMods(writer, true, true);
                    }
                }

                foreach (string impossibleMod in _impossibleMods)
                {
                    writer.WriteLine("//Impossible Modification: " + impossibleMod);
                }
                foreach (Mod listedMod in _listedMods)
                {
                    writer.WriteLine("//Unable to match: " + listedMod.Name);
                }
                foreach (Mod listedMod in _listedHiddenMods)
                {
                    writer.WriteLine("//Unable to match: " + listedMod.Name);
                }
                foreach (var unusedSciex in _dictModNameToThreeLetterCode.Where(p => !p.Value.Used).OrderBy(p => p.Key))
                {
                    writer.WriteLine("//Unused code: {0} = {1}", unusedSciex.Key, unusedSciex.Value.ThreeLetterCode);
                }

                writer.Close();
            }
            catch(Exception x)
            {
                Console.Error.WriteLine("ERROR: " + x.Message);
            }

            Console.Error.WriteLine("Press enter to continue...");
            Console.ReadLine();
        }

        private static elem_ref_t[] DiffMod(elem_ref_t[] element, elem_ref_t[] elementParent)
        {
            var listDiffRef = new List<elem_ref_t>();
            Array.Sort(element, (el1, el2) => string.CompareOrdinal(el1.symbol, el2.symbol));
            Array.Sort(elementParent, (el1, el2) => string.CompareOrdinal(el1.symbol, el2.symbol));
            int i = 0, j = 0;
            while (i < element.Length && j < elementParent.Length)
            {
                var el = element[i];
                int numEl = int.Parse(el.number, CultureInfo.InvariantCulture);
                var elParent = elementParent[j];
                int numParent = int.Parse(elParent.number);
                int orderEl = string.CompareOrdinal(el.symbol, elParent.symbol);
                string sym, num;
                if (orderEl == 0)
                {
                    i++;
                    j++;
                    int numTotal = numEl - numParent;
                    if (numTotal == 0)
                        continue;
                    sym = el.symbol;
                    num = numTotal.ToString(CultureInfo.InvariantCulture);
                }
                else if (orderEl < 0)
                {
                    i++;
                    sym = el.symbol;
                    num = el.number;
                }
                else
                {
                    j++;
                    sym = elParent.symbol;
                    num = (-numParent).ToString(CultureInfo.InvariantCulture);
                }
                listDiffRef.Add(new elem_ref_t { symbol = sym, number = num });
            }
            return listDiffRef.ToArray();
        }

        /// <summary>
        /// Returns true if the given element is a balanced isotopic label, where the only
        /// thing changing is the isotopes of the atoms in question.
        /// </summary>
        private static bool IsIsotopicDiff(IEnumerable<elem_ref_t> element)
        {
            var dictSymToCount = element.ToDictionary(el => el.symbol, el => int.Parse(el.number));
            foreach (var pair in dictSymToCount.ToArray())
            {
                string labelSym = pair.Key;
                int labelCount = pair.Value;
                char monoChar;
                if (DICT_HEAVY_LABELS.TryGetValue(labelSym, out monoChar))
                {
                    // Must be adding labeled atoms and removing unlabeled atoms
                    if (labelCount < 0)
                        return false;

                    string monoSym = monoChar.ToString(CultureInfo.InvariantCulture);
                    int monoCount;
                    if (!dictSymToCount.TryGetValue(monoSym, out monoCount))
                        return false;
                    if (labelCount + monoCount != 0)
                        return false;
                    dictSymToCount.Remove(monoSym);
                    dictSymToCount.Remove(labelSym);
                }
            }
            return dictSymToCount.Count == 0;
        }


        private static Dictionary<string, ThreeLetterCodeUsed> LoadShortNames(string path)
        {
            var dictModNameToThreeLetterCode = new Dictionary<string, ThreeLetterCodeUsed>();
            // Throws an exception, if it cannot read the file
            using (var reader = new StreamReader(path))
            {
                var serializer = new XmlSerializer(typeof(DataDictionary));
                var proteinPilotMods = ((DataDictionary)serializer.Deserialize(reader)).Mod;
                
                foreach (var mod in proteinPilotMods)
                {
                    var nameIndex =
                        mod.ItemsElementName.Select((t, i) => new {type = t, index = i}).First(
                            t => t.type.ToString() == ItemsChoiceType.Nme.ToString()).index;
                    var name = (string)mod.Items[nameIndex];
                    if ((name.Contains("Protein") && name.Contains("Terminal")) || name.Contains("Old3LetterCode"))
                        continue;
                    if (name.StartsWith("Terminal "))
                        name = name.Substring(9);

                    var threeLetterCodeIndex =
                        mod.ItemsElementName.Select((t, i) => new {type = t, index = i}).First(
                            t => t.type.ToString() == ItemsChoiceType.TLC.ToString()).index;
                    dictModNameToThreeLetterCode[name.ToLower()] = new ThreeLetterCodeUsed((string) mod.Items[threeLetterCodeIndex]);

                    var displayNameItem =
                        mod.ItemsElementName.Select((t, i) => new {type = t, index = i}).FirstOrDefault(
                            t => t.type.ToString() == ItemsChoiceType.DisplayName.ToString());
                    if (displayNameItem != null)
                    {
                        nameIndex = displayNameItem.index;
                        var displayNamename = (string) mod.Items[nameIndex];
                        dictModNameToThreeLetterCode[displayNamename.ToLower()] = new ThreeLetterCodeUsed((string) mod.Items[threeLetterCodeIndex]);
                    }
                }
            }

            AddNameAliases(dictModNameToThreeLetterCode,
                new Dictionary<string, string>
                {
                    {"GlyGlyGln", "GGQ"},
                    {"GlnThrGlyGly", "QTGG"},
                    {"GlnGlnGlnThrGlyGly", "QQQTGG"},
                    {"Chloro", "Chlorination" },
                    {"Dichloro", "dichlorination"},
                    {"Acetyl-PEO-Biotin", "PEO-Iodoacetyl-LC-Biotin"}
                });

            return dictModNameToThreeLetterCode;
        }

        private static void AddNameAliases(Dictionary<string, ThreeLetterCodeUsed> dictModNameToThreeLetterCode, Dictionary<string, string> dictAliases)
        {
            foreach (var nameValue in dictAliases)
            {
                dictModNameToThreeLetterCode.Add(nameValue.Value.ToLower(), dictModNameToThreeLetterCode[nameValue.Key.ToLower()]);
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
                if (string.IsNullOrEmpty(line))
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
                listedMods.Add(new Mod { Name = line, Title = title, AAs = aas ?? new char[0], Terminus = terminal });
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

                // For each AA in the listed modification, make sure the dictionary mod contains it.
                bool foundAllAAs = true;
                List<String> losses = null;
                List<String> aaLosses = null;
                foreach (char aa in mod.AAs)
                {
                    foundAllAAs = foundAllAAs && ContainsSite(dictMod, aa.ToString(CultureInfo.InvariantCulture), out aaLosses);
                    if (losses == null)
                        losses = aaLosses;
                    else if (!ListEquals(losses, aaLosses))
                        foundAllAAs = false;
                }              
                // Also check to make sure the dictionary mod contains the terminal, if any.
                foundAllAAs = foundAllAAs &&
                    (mod.Terminus == null || !mod.Terminus.Equals(Terminal.C) || ContainsSite(dictMod, "C-term", out aaLosses));
                foundAllAAs = foundAllAAs && 
                    (mod.Terminus == null || !mod.Terminus.Equals(Terminal.N) || ContainsSite(dictMod, "N-term", out aaLosses));
                if (losses == null)
                    losses = aaLosses;
                else if (!ListEquals(losses, aaLosses))
                    foundAllAAs = false;               

                // If the dictionary mod does not contain all desired AAs, continue.
                if (!foundAllAAs)
                    continue;

                // Build string for fragment loss.
                string lossesStr = "null";
                if(losses != null && losses.Count > 0)
                {
                    lossesStr = @"new [] { ";                    
                    foreach (string loss in losses)
                    {
                        lossesStr += String.Format(@"new FragmentLoss(""{0}""), ", loss);
                    }
                    lossesStr += "}";
                }

                string skylineFormula = BuildFormula(dictMod.delta.element);
                if(skylineFormula.Length == 0)
                    continue;
                bool hasLabelAtoms = isotopic && mod.AAs.Length > 0;
                string labelAtoms = "LabelAtoms.None";
                if (hasLabelAtoms && !CheckLabelAtoms(dictMod, mod.AAs, out labelAtoms))
                {
                    _impossibleMods.Add(mod.Name);
                    if (hidden)
                        _listedHiddenMods.Remove(mod);
                    else
                        _listedMods.Remove(mod);
                    continue;
                }

                ThreeLetterCodeUsed threeLetterCode;
                var nameKey = mod.Name.Substring(0, mod.Name.LastIndexOf("(", StringComparison.Ordinal)).Trim().ToLower();
                _dictModNameToThreeLetterCode.TryGetValue(nameKey, out threeLetterCode);

                writer.WriteLine("            new UniModModificationData");
                writer.WriteLine("            {");
                writer.WriteLine(@"                 Name = ""{0}"", ", mod.Name);
                writer.Write("                 ");
                if (mod.AAs.Length > 0)
                    writer.Write(@"AAs = ""{0}"", ", BuildAAString(mod.AAs));
                if (mod.Terminus != null)
                    writer.Write(@"Terminus = {0}, ", "ModTerminus." + mod.Terminus);
                writer.Write("LabelAtoms = {0}, ", labelAtoms);
                if (Equals(labelAtoms, "LabelAtoms.None"))
                    writer.Write(@"Formula = ""{0}"", ", BuildFormula(dictMod.delta.element));
                if(!Equals(lossesStr, "null"))
                    writer.Write("Losses = {0}, ", lossesStr);
                writer.WriteLine("ID = {0}, ", dictMod.record_id);
                writer.Write("                 Structural = {0}, ", (!isotopic).ToString(CultureInfo.InvariantCulture).ToLower());
                if (threeLetterCode != null)
                {
                    writer.Write(@"ShortName = ""{0}"", ", threeLetterCode.ThreeLetterCode);
                    threeLetterCode.Used = true;
                    if (mod.Terminus.HasValue)
                    {
                        nameKey = "Terminal " + nameKey;
                        ThreeLetterCodeUsed threeLetterCodeTerm;
                        if (_dictModNameToThreeLetterCode.TryGetValue(nameKey, out threeLetterCodeTerm))
                        {
                            if (!Equals(threeLetterCode.ThreeLetterCode, threeLetterCodeTerm.ThreeLetterCode))
                            {
                                Console.Error.WriteLine("Mismatched three letter codes {0} and terminal {1}",
                                    threeLetterCode.ThreeLetterCode, threeLetterCodeTerm.ThreeLetterCode);
                            }
                            else
                            {
                                threeLetterCodeTerm.Used = true;
                            }
                        }
                    }
                }
                writer.WriteLine("Hidden = {0}, ", hidden.ToString().ToLower());
                writer.WriteLine("            },");

                
                if (hidden)
                    _listedHiddenMods.Remove(mod);
                else
                    _listedMods.Remove(mod);
            }
        }

        private static bool ListEquals(List<string> list1, List<string> list2)
        {
            for (int i = 0; i < list1.Count; i++)
            {
                if (!Equals(list1[i], list2[i]))
                    return false;
            }
            return true;
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
        private static bool ContainsSite(mod_t modification, string aa, out List<string> losses)
        {
            losses = new List<string>();
            foreach (specificity_t specificty in modification.specificity)
            {
                if (Equals(specificty.site, aa) || 
                    (aa.Length > 1 && specificty.position.ToString().ToLower().Contains(aa.ToLower().Replace("-", ""))))
                {
                    if (specificty.NeutralLoss != null && specificty.NeutralLoss.Length > 0)
                    {   
                        foreach(NeutralLoss_t loss in specificty.NeutralLoss)
                        {
// ReSharper disable CompareOfFloatsByEqualityOperator
                            if(loss.avge_mass == 0)
// ReSharper restore CompareOfFloatsByEqualityOperator
                                continue;
                            losses.Add(BuildFormula(loss.element));
                        }
                    }
                    losses.Sort();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Check for label atoms in the given sequence matching the given aa, and create a corresponding string if found.
        /// </summary>
        private static bool CheckLabelAtoms(mod_t mod, char[] aas, out string labelAtomsFormula)
        {
            labelAtomsFormula = "";
            bool hasLabelAtoms = true;
            if (mod.title.StartsWith("Label"))
            {
                foreach (var element in mod.delta.element)
                {
                    char elementMatch;
                    if (hasLabelAtoms && DICT_HEAVY_LABELS.TryGetValue(element.symbol, out elementMatch))
                    {
                        foreach (char aa in aas)
                        {
                            int numInFormula = ParseSeqMassCalcFormula(aa, elementMatch);
                            if (!Equals(element.number, numInFormula.ToString(CultureInfo.InvariantCulture)))
                            {
                                hasLabelAtoms = false;
                                break;
                            }
                        }
                        if (!string.IsNullOrEmpty(labelAtomsFormula))
                            labelAtomsFormula += '|';

                        var symbol = element.symbol;
                        var elementIndex = symbol.IndexOfAny(new[] { 'N', 'C', 'O', 'H' });
                        labelAtomsFormula += "LabelAtoms." + symbol[elementIndex] + symbol.Remove(elementIndex);
                    }
                    else if (element.symbol.Length == 1 && DICT_HEAVY_LABELS.ContainsValue(element.symbol[0]))
                    {
                        foreach (char aa in aas)
                        {
                            int numInFormula = ParseSeqMassCalcFormula(aa, element.symbol[0]);
                            if ((Int32.Parse(element.number) * -1) > numInFormula)
                                return false;
                        }
                    }
                }
            }
            if (!hasLabelAtoms || string.IsNullOrEmpty(labelAtomsFormula))
                labelAtomsFormula = "LabelAtoms.None";
            return true;
        }

        private static int ParseSeqMassCalcFormula(char aa, char element)
        {
            var formula = AMINO_FORMULAS[aa];
            int index = formula.IndexOf(element);
            int numInFormula;
            if (index > formula.Length - 2 || char.IsLetter(formula[index + 1]))
                numInFormula = 1;
            else
            {
                int numLength = (index + 2 < formula.Length && char.IsNumber(formula[index + 2])) ? 2 : 1;
                numInFormula = Int32.Parse(formula.Substring(index + 1, numLength));
            }
            return numInFormula;
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
                symbol = DICT_HEAVY_LABELS.TryGetValue(symbol, out aa) ? aa.ToString(CultureInfo.InvariantCulture) + '\'' : symbol;
                if (num < 0)
                    negative += symbol + (num == -1 ? "" : (num * -1).ToString(CultureInfo.InvariantCulture));
                else
                    positive += symbol + (num == 1 ? "" : num.ToString(CultureInfo.InvariantCulture));
            }
            string formula = positive + (negative.Length > 3 ? negative : "");
            return positive.Length > 0 ? formula : formula.Replace(" ", "");
        }

        private struct Mod
        {
            public string Name { get; set; }
            public string Title { get; set; }
            public char[] AAs { get; set; }           
            public Terminal? Terminus { get; set; }
        }

        private enum Terminal
        {
// ReSharper disable InconsistentNaming
            C, N
// ReSharper restore InconsistentNaming
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
