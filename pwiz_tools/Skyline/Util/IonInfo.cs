/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using System.Linq;
using System.Text.RegularExpressions;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Util
{

    /// <summary>
    /// Describes a molecule as its neutral formula and an adduct.  Adducts have the form [M+H], [M-3K], [2M+Isoprop+H] etc.
    ///  </summary>
    public class IonInfo : Immutable
    {

        // Common terms for small molecule adducts per http://fiehnlab.ucdavis.edu/staff/kind/Metabolomics/MS-Adduct-Calculator/ESI-MS-adducts.xls
        // See also (An interesting list of pseudoelements is at http://winter.group.shef.ac.uk/chemputer/pseudo-elements.html for a longer list we may wish to implement later
        private static readonly IDictionary<string, string> DICT_ADDUCT_NICKNAMES =
            new Dictionary<string, string>
            {
                // ReSharper disable NonLocalizedString
                {"ACN", "C2H3N"}, // Acetonitrile
                {"DMSO", "C2H6OS"}, // Dimethylsulfoxide
                {"FA", "CH2O2"}, // Formic acid
                {"Hac", "CH3COOH"}, // Acetic acid
                {"TFA", "C2HF3O2"}, // Trifluoroacetic acid
                {"IsoProp", "C3H8O"}, // Isopropanol
                {"MeOH", "CH3OH"}, // CH3OH. methanol
                {"Cl37", BioMassCalc.Cl37},
                {"Br81", BioMassCalc.Br81},
                {"H2", BioMassCalc.H2},
                {"C13", BioMassCalc.C13},
                {"N15", BioMassCalc.N15},
                {"O17", BioMassCalc.O17},
                {"O18", BioMassCalc.O18}
                // ReSharper restore NonLocalizedString
            };

        // Ion charges seen in XCMS public and ESI-MS-adducts.xls
        private static readonly IDictionary<string, int> DICT_ADDUCT_ION_CHARGES =
            new Dictionary<string, int>
            {
               {BioMassCalc.H, 1},
               {BioMassCalc.K, 1},
               {BioMassCalc.Na, 1},
               {BioMassCalc.Li, 1},
               {BioMassCalc.Br,-1},
               {BioMassCalc.Cl,-1},
               {BioMassCalc.F, -1},
               {"CH3COO", -1}, // Deprotonated Hac // Not L10N
               {"HCOO", -1}, // Formate (deprotonated FA)   // Not L10N
               {"NH4",1} // Not L10N
            };

        private string _formula;  // Chemical formula, possibly followed by adduct description - something like "C12H3[M+H]" or "[2M+K]" or "M+H" or "[M+H]+" or "[M+Br]-" 
        private string _unlabledFormula;   // Chemical formula after adduct application and stripping of labels


        /// <summary>
        /// Constructs an IonFormula, which holds a neutral formula and adduct, or possibly just a chemical formula if no adduct is included in the description
        /// </summary>
        /// <param name="formulaWithOptionalAdduct">Chemical formula, possibly followed by adduct description - something like "C12H3[M+H]" or "[2M+K]" or "M+H" or "[M+H]+" or "[M+Br]-"</param>
        public IonInfo(string formulaWithOptionalAdduct)
        {
            Formula = formulaWithOptionalAdduct;
        }

        /// <summary>
        /// For serialization
        /// </summary>
        protected IonInfo()
        {
        }

        /// <summary>
        /// Formula description as provided to constructor, possibly with adduct removed if it was a charge description only.
        /// </summary>
        public string Formula
        {
            get { return _formula; }
            protected set
            {
                _formula = value;
                // Ignore adduct if it's just a charge description, like "[M+]"
                if (!string.IsNullOrEmpty(Adduct) && AdductIsChargeOnly(_formula))
                {
                    // ReSharper disable once PossibleNullReferenceException // Can't be null if adduct is non-null
                    _formula = _formula.Split('[')[0];  // Drop the [M+]
                }
                _unlabledFormula = BioMassCalc.MONOISOTOPIC.StripLabelsFromFormula(FormulaWithAdductApplied);
                Helpers.AssignIfEquals(ref _unlabledFormula, _formula); // Save some string space if actually unlableled
            }
        }

        /// <summary>
        /// Internal formula description with adduct description stripped off, or null if there is no adduct description
        /// </summary>
        public string NeutralFormula
        {
            get
            {
                int adductStart = AdductStartIndex;
                return adductStart < 0 ? _formula : _formula.Substring(0, adductStart);
            }
        }

        /// <summary>
        /// Adduct part of internal formula description, or null if there is none
        /// </summary>
        public string Adduct
        {
            get
            {
                int adductStart = AdductStartIndex;
                return adductStart < 0 ? null : _formula.Substring(adductStart);
            }
        }

        private int AdductStartIndex
        {
            get
            {
                return _formula != null ? _formula.IndexOf('[') : -1;
            }
        }

        /// <summary>
        /// Returns chemical formula with adduct applied then labels stripped
        /// </summary>
        public string UnlabeledFormula
        {
            get { return _unlabledFormula; }
        }

        /// <summary>
        /// Chemical formula after adduct description, if any, is applied
        /// </summary>
        public string FormulaWithAdductApplied
        {
            get
            {
                if (string.IsNullOrEmpty(Adduct))
                {
                    return _formula;
                }
                int charge;
                var mol = ApplyAdductInFormula(_formula, out charge);
                return mol.ToString();
            }
        }

        /// <summary>
        /// Check to see if an adduct is only a charge declaration, as in "[M+]".
        /// </summary>
        /// <param name="formula">A string like "C12H3[M+H]"</param>
        /// <returns>True if the adduct description contributes nothing to the ion formula other than charge information, such as "[M+]"</returns>
        public static bool AdductIsChargeOnly(string formula)
        {
            int charge;
            return Equals(ApplyAdductInFormula(formula, out charge), ApplyAdductInFormula(formula.Split('[')[0], out charge));
        }

        /// <summary>
        /// Take a molecular formula with adduct in it and return a Molecule.
        /// </summary>
        /// <param name="formula">A string like "C12H3[M+H]"</param>
        /// <param name="charge">Charge derived from adduct description by counting H, K etc as found in DICT_ADDUCT_ION_CHARGES</param>
        /// <returns></returns>
        public static Molecule ApplyAdductInFormula(string formula, out int charge)
        {
            var withoutAdduct = (formula ?? string.Empty).Split('[')[0];
            var adduct = (formula ?? string.Empty).Substring(withoutAdduct.Length);
            return ApplyAdductToFormula(withoutAdduct, adduct, out charge);
        }

        /// <summary>
        /// Generate a tooltip string that look something like this:
        ///
        ///   Formula may contain an adduct description (e.g. "C47H51NO14[M+IsoProp+H]").
        ///
        ///   Multipliers (e.g. "[2M+K]") and labels (e.g. "[M2Cl37+H]") are supported.
        ///   
        ///   Recognized adduct components include normal chemical symbols and:
        ///   ACN (C2H3N)
        ///   DMSO (C2H6OS)
        ///   FA (CH2O2)
        ///   Hac (CH3COOH)
        ///   TFA (C2HF3O2)
        ///   IsoProp (C3H8O)
        ///   MeOH (CH3OH)
        ///   Cl37 (Cl')
        ///   Br81 (Br')
        ///   C13 (C')
        ///   N15 (N')
        ///   O17 (O")
        ///   O18 (O').
        ///   
        ///   Charge states are inferred from the presence of these adduct components:
        ///   H (+1)
        ///   K (+1)
        ///   Na (+1)
        ///   Li (+1)
        ///   Br (-1)
        ///   Cl (-1)
        ///   F (-1)
        ///   CH3COO (-1)
        ///   NH4 (+1)
        /// 
        /// </summary>
        public static string AdductTips
        {
            get
            {
                var components = DICT_ADDUCT_NICKNAMES.Aggregate<KeyValuePair<string, string>, string>(null, (current, c) => current + (string.IsNullOrEmpty(current)?"\r\n":", ") + string.Format("{0} ({1})", c.Key, c.Value)); // Not L10N
                var chargers = DICT_ADDUCT_ION_CHARGES.Aggregate<KeyValuePair<string, int>, string>(null, (current, c) => current + (string.IsNullOrEmpty(current) ? "\r\n" : ", ") + string.Format("{0} ({1:+#;-#;+0})", c.Key, c.Value)); // Not L10N
                return string.Format(Resources.IonInfo_AdductTips_, components, chargers);
            }
        }

        /// <summary>
        /// Take a molecular formula and apply the described adduct to it.
        /// </summary>
        /// <param name="formula">A string like "C12H3"</param>
        /// <param name="adduct">A string like "[M+H]" or "[2M+K]" or "M+H" or "[M+H]+" or "[M+Br]- or "M2C13+Na" </param>
        /// <param name="charge">Charge derived from adduct description by counting H, K etc as ound in DICT_ADDUCT_ION_CHARGES</param>
        /// <returns></returns>
        public static Molecule ApplyAdductToFormula(string formula, string adduct, out int charge)
        {
            var regexOptions = RegexOptions.ExplicitCapture | RegexOptions.Singleline | RegexOptions.CultureInvariant;
            var outerRegex = new Regex(@"\[?(?<multM>\d*)M(?<labelCount>\d*)(?<label>[^\+\-]*)?(?<adduct>[\+\-][^\]]*)(\](?<declaredChargeCount>\d*)(?<declaredChargeSign>[+-]*)?)?", regexOptions); // Not L10N
            var innerRegex = new Regex(@"(?<oper>\+|\-)(?<multM>\d+)?(?<ion>[^-+]*)", regexOptions); // Not L10N
            int? declaredCharge = null;
            int? calculatedCharge = null;
            var molecule = Molecule.Parse(formula.Trim());
            adduct = (adduct??string.Empty).Trim();
            if (string.IsNullOrEmpty(adduct))
            {
                charge = 0;
                return molecule; // Nothing to do
            }
            var match = outerRegex.Match(adduct);
            var adductOperations = match.Groups["adduct"].Value; // Not L10N
            var success = match.Success && (match.Groups.Count == 7) && !string.IsNullOrEmpty(adductOperations);
            var resultDict = new Dictionary<string, int>();
            if (success)
            {
                success = HandleMultiplierAndLabel(adduct, match, molecule, resultDict);
                var declaredChargeCountStr = match.Groups["declaredChargeCount"].Value;  // Not L10N
                if (!string.IsNullOrEmpty(declaredChargeCountStr)) // Read the "2" in "[M+H+Na]2+" if any such thing is there
                {
                    if (!string.IsNullOrEmpty(declaredChargeCountStr))
                    {
                        int z;
                        success = int.TryParse(declaredChargeCountStr, out z);
                        declaredCharge = z;
                    }
                }
                var declaredChargeSignStr = match.Groups["declaredChargeSign"].Value;  // Not L10N
                if (!string.IsNullOrEmpty(declaredChargeSignStr)) // Read the "++" in "[M+2H]++" or "+" in "]2+" if any such thing is there
                {
                    declaredCharge = (declaredCharge ?? 1)*(declaredChargeSignStr.Count(c => c == '+') - declaredChargeSignStr.Count(c => c == '-'));
                }
                if (adductOperations.Equals("+")) // "[M+]" is legit // Not L10N
                {
                    calculatedCharge = 1;
                }
                else if (adductOperations.Equals("-"))  // "[M-]" is presumably also legit // Not L10N
                {
                    calculatedCharge = -1;
                }
                else
                {
                    // Now parse each part of the "+Na-2H" in "[M+Na-2H]" if any such thing is there
                    var matches = innerRegex.Matches(adductOperations);
                    int remaining = matches.Count;
                    foreach (Match m in matches)
                    {
                        remaining--;
                        if (m.Groups.Count < 4)
                        {
                            success = false;
                            break;
                        }
                        var multiplierM = 1;
                        var multMstr = m.Groups["multM"].Value; // Read the "2" in "+2H" if any such thing is there // Not L10N
                        if (!string.IsNullOrEmpty(multMstr)) 
                        {
                            success = int.TryParse(multMstr, out multiplierM);
                        }
                        if (m.Groups["oper"].Value.Contains("-")) // Not L10N
                        {
                            multiplierM *= -1;
                        }
                        var ion = m.Groups["ion"].Value; // Not L10N
                        int ionCharge;
                        if (DICT_ADDUCT_ION_CHARGES.TryGetValue(ion, out ionCharge))
                        {
                            calculatedCharge = (calculatedCharge ?? 0) + ionCharge * multiplierM;
                        }
                        string realname;
                        if (DICT_ADDUCT_NICKNAMES.TryGetValue(ion, out realname)) // Swap common nicknames like "DMSO" for "C2H6OS"
                        {
                            ion = realname;
                        }
                        var ionMolecule = Molecule.Parse(ion);
                        if (ionMolecule.Count == 0)
                        {
                            success = multiplierM == 1 && remaining != 0;  // Allow pointless + in "M+-H2O+H" but not trailing +in "M-H2O+H+"
                        }
                        foreach (var pair in ionMolecule)
                        {
                            int count;
                            if (resultDict.TryGetValue(pair.Key, out count))
                            {
                                resultDict[pair.Key] = count + pair.Value*multiplierM;
                            }
                            else
                            {
                                resultDict.Add(pair.Key, pair.Value * multiplierM);
                            }
                        }
                    }
                }
            }
            charge = calculatedCharge ?? declaredCharge ?? 0;
            var resultMol = Molecule.FromDict(new ImmutableSortedList<string, int>(resultDict));
            if (!resultMol.Keys.All(k => BioMassCalc.MONOISOTOPIC.IsKnownSymbol(k)))
            {
                throw new InvalidOperationException(string.Format(Resources.BioMassCalc_ApplyAdductToFormula_Unknown_symbol___0___in_adduct_description___1__, resultMol.Keys.First(k => !BioMassCalc.MONOISOTOPIC.IsKnownSymbol(k)), adduct));
            }
            if (!success)
            {
                throw new InvalidOperationException(string.Format(Resources.BioMassCalc_ApplyAdductToFormula_Failed_parsing_adduct_description___0__, adduct));
            }
            if (declaredCharge.HasValue && calculatedCharge.HasValue && declaredCharge != calculatedCharge)
            {
                throw new InvalidOperationException(string.Format(Resources.BioMassCalc_ApplyAdductToFormula_Failed_parsing_adduct_description___0____declared_charge__1__does_not_agree_with_calculated_charge__2_, adduct, declaredCharge.Value, calculatedCharge));
            }
            return resultMol;
        }

        /// <summary>
        /// Handle the "2" and "4Cl37" in "[2M4Cl37+H]"
        /// </summary>
        private static bool HandleMultiplierAndLabel(string adduct, Match match, Molecule molecule, IDictionary<string, int> resultDict)
        {
            bool success = true;
            var multiplierM = 1;
            var multMstr = match.Groups["multM"].Value; // Not L10N
            if (!string.IsNullOrEmpty(multMstr)) // Read the "2" in "[2M+..." if any such thing is there 
            {
                success = int.TryParse(multMstr, out multiplierM);
            }
            var labelCountStr = match.Groups["labelCount"].Value; // Read the "4" in "[2M4Cl37+..." if any such thing is there // Not L10N
            var labelCount = 1;
            if (!string.IsNullOrEmpty(labelCountStr)) // Read the "2" in "[2M+..." if any such thing is there 
            {
                success = int.TryParse(labelCountStr, out labelCount);
            }
            var label = match.Groups["label"].Value; // Read the "Cl37" in "[2M4Cl37+..." if any such thing is there // Not L10N
            var unlabel = string.Empty;
            if (!string.IsNullOrEmpty(label))
            {
                string syn;
                if (DICT_ADDUCT_NICKNAMES.TryGetValue(label, out syn))
                {
                    label = syn; // Cl37 -> Cl'
                }
                unlabel = label.Replace("'", ""); // Not L10N
            }
            foreach (var pair in molecule)
            {
                if (pair.Key.Equals(unlabel))
                {
                    // If label is "2Cl37" and molecule is CH4Cl5 then result is CH4Cl3Cl'2
                    var unlabelCount = pair.Value - labelCount;
                    if (unlabelCount > 0)
                    {
                        resultDict.Add(pair.Key, multiplierM*unlabelCount);
                    }
                    else if (unlabelCount < 0)
                    {
                        throw new InvalidOperationException(
                            string.Format(Resources.BioMassCalc_ApplyAdductToFormula_Adduct_description___0___calls_for_more_labeled__1__than_are_found_in_the_molecule,
                                adduct, unlabel));
                    }
                    resultDict.Add(label, multiplierM*labelCount);
                }
                else
                {
                    resultDict.Add(pair.Key, multiplierM*pair.Value);
                }
            }
            return success;
        }

        public static bool IsFormulaWithAdduct(string formula, out Molecule mol, out int charge, out string neutralFormula)
        {
            mol = null;
            charge = 0;
            neutralFormula = null;
            if (string.IsNullOrEmpty(formula))
            {
                return false;
            }
            // Does formula contain an adduct description?  If so, pull charge from that.
            if (formula.Contains('[') && formula.Contains(']'))
            {
                var parts = formula.Split('[');
                neutralFormula = parts[0];
                int z;
                var adduct = formula.Substring(neutralFormula.Length);
                mol = ApplyAdductToFormula(neutralFormula, adduct, out z);
                charge = z;
                return true;
            }
            return false;
        }

        public override string ToString()
        {
            return _formula ?? string.Empty;
        }
    }
}