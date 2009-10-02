/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Text;
using mercury;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Enrichment
{
    public class ResidueComposition
    {
        public const double PROTON_MASS = 1.00727649;
        public static readonly Dictionary<String, char> LongNames = new Dictionary<string, char> {
            {"Ala",'A'},
            {"Arg",'R'},
            {"Asn",'N'},
            {"Asp",'D'},
            {"Cys",'C'},
            {"Glu",'E'},
            {"Gln",'Q'},
            {"Gly",'G'},
            {"His",'H'},
            {"Ile",'I'},
            {"Leu",'L'},
            {"Lys",'K'},
            {"Met",'M'},
            {"Phe",'F'},
            {"Pro",'P'},
            {"Ser",'S'},
            {"Thr",'R'},
            {"Trp",'W'},
            {"Tyr",'Y'},
            {"Val",'V'}
        };
        private readonly Dictionary<String, String> residueFormulas = new Dictionary<string, string>();
        private readonly Dictionary<String, double> massDeltas = new Dictionary<string, double>();
        public ResidueComposition()
        {
            residueFormulas.Add("A", "C3H5ON");
            residueFormulas.Add("C", "C3H5ONS");
            residueFormulas.Add("D", "C4H5O3N");
            residueFormulas.Add("E", "C5H7O3N");
            residueFormulas.Add("F", "C9H9ON");
            residueFormulas.Add("G", "C2H3ON");
            residueFormulas.Add("H", "C6H7ON3");
            residueFormulas.Add("I", "C6H11ON");
            residueFormulas.Add("K", "C6H12ON2");
            residueFormulas.Add("L", "C6H11ON");
            residueFormulas.Add("M", "C5H9ONS");
            residueFormulas.Add("N", "C4H6O2N2");
            residueFormulas.Add("O", "C12H19N3O2");
            residueFormulas.Add("P", "C5H7ON");
            residueFormulas.Add("Q", "C5H8O2N2");
            residueFormulas.Add("R", "C6H12ON4");
            residueFormulas.Add("S", "C3H5O2N");
            residueFormulas.Add("T", "C4H7O2N");
            residueFormulas.Add("U", "C3H5NOSe");
            residueFormulas.Add("V", "C5H9ON");
            residueFormulas.Add("W", "C11H10ON2");
            residueFormulas.Add("Y", "C9H9O2N");
            IsotopeAbundances = new IsotopeAbundances();
        }
        public void SetMassDelta(String aminoAcid, double massDelta)
        {
            if (massDelta == 0)
            {
                massDeltas.Remove(aminoAcid);
            }
            else
            {
                massDeltas[aminoAcid] = massDelta;
            }
        }
        public String MolecularFormula(char aminoAcid)
        {
            return residueFormulas["" + aminoAcid];
        }
        public String MolecularFormula(String sequence)
        {
            StringBuilder result = new StringBuilder();
            for (int i = 0; i < sequence.Length; i++)
            {
                String residue = sequence.Substring(i, 1);
                if (residueFormulas.ContainsKey(residue))
                {
                    result.Append(residueFormulas[sequence.Substring(i, 1)]);
                }
                else
                {
                    // TODO
                }
            }
            result.Append("H2O");
            return result.ToString();
        }
        public Dictionary<String, int> FormulaToDictionary(String formula)
        {
            Dictionary<String, int> result = new Dictionary<string, int>();
            String currentElement = null;
            int currentQuantity = 0;
            for (int ich = 0; ich < formula.Length; ich++)
            {
                char ch = formula[ich];
                if (Char.IsDigit(ch))
                {
                    currentQuantity = currentQuantity * 10 + (ch - '0');
                }
                else if (Char.IsUpper(ch))
                {
                    if (currentElement != null)
                    {
                        if (currentQuantity == 0)
                        {
                            currentQuantity = 1;
                        }
                        if (result.ContainsKey(currentElement))
                        {
                            result[currentElement] = result[currentElement] + currentQuantity;
                        }
                        else
                        {
                            result[currentElement] = currentQuantity;
                        }
                    }
                    currentQuantity = 0;
                    currentElement = "" + ch;
                }
                else if (Char.IsLower(ch))
                {
                    currentElement = currentElement + ch;
                }
            }
            if (currentElement != null)
            {
                if (currentQuantity == 0)
                {
                    currentQuantity = 1;
                }
                if (result.ContainsKey(currentElement))
                {
                    result[currentElement] = result[currentElement] + currentQuantity;
                }
                else
                {
                    result[currentElement] = currentQuantity;
                }
            }
            return result;
        }
        public String DictionaryToFormula(Dictionary<String, int> dictionary)
        {
            StringBuilder result = new StringBuilder();
            foreach (var pair in dictionary)
            {
                if (pair.Value == 0)
                {
                    continue;
                }
                result.Append(pair.Key);
                if (pair.Value != 1)
                {
                    result.Append(pair.Value);
                }
            }
            return result.ToString();
        }
        public double GetMonoisotopicMz(ChargedPeptide chargedPeptide)
        {
            return GetMonoisotopicMz(MolecularFormula(chargedPeptide.Sequence), chargedPeptide.Charge) + GetMzDelta(chargedPeptide);
        }
        public double GetAverageMz(ChargedPeptide chargedPeptide)
        {
            double total = 0;
            double count = 0;
            foreach (var entry in GetIsotopeMasses(chargedPeptide))
            {
                total += entry.Key*entry.Value;
                count += entry.Value;
            }
            return total/count;
        }
        public double GetMzDelta(ChargedPeptide chargedPeptide)
        {
            double total = 0;
            for (int i = 0; i < chargedPeptide.Sequence.Length; i++)
            {
                double delta;
                massDeltas.TryGetValue(chargedPeptide.Sequence.Substring(i, 1), out delta);
                total += delta;
            }
            return total/chargedPeptide.Charge;
        }
        public double GetMonoisotopicMz(String formula, int charge)
        {
            Dictionary<double, double> dict = GetIsotopeMasses(formula, charge);
            return dict.Keys.Min();
        }

        public Dictionary<double, double> GetIsotopeMasses(ChargedPeptide chargedPeptide)
        {
            Dictionary<double, double> rawMassAbundances = GetIsotopeMasses(MolecularFormula(chargedPeptide.Sequence),
                                                                            chargedPeptide.Charge);
            double massDelta = GetMzDelta(chargedPeptide);
            Dictionary<double, double> result = new Dictionary<double, double>();
            foreach (var entry in rawMassAbundances)
            {
                result.Add(entry.Key + massDelta, entry.Value);
            }
            return result;
        }

        public Dictionary<double, double> GetIsotopeMasses(String molecularFormula, int charge)
        {
            using (ManagedCMercury8 mercury = new ManagedCMercury8())
            {
                mercury.SetIsotopeAbundances(IsotopeAbundances.getAbundances());
                return Dictionaries.Normalize(mercury.Calculate(molecularFormula, charge), 1.0);
            }
        }

        public IsotopeAbundances IsotopeAbundances
        {
            get;
            set;
        }
        public Dictionary<String, String> ResidueFormulas
        {
            get
            {
                return residueFormulas;
            }
        }
    }
}
