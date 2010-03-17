/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System.Globalization;
using System.Text;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// Helpers for identifying amin acid characters.
    /// </summary>
    public static class AminoAcid
    {
        public static bool IsAA(char c)
        {
            switch (c)
            {
                case 'A':
                case 'C':
                case 'D':
                case 'E':
                case 'F':
                case 'G':
                case 'H':
                case 'I':
                case 'K':
                case 'L':
                case 'M':
                case 'N':
                case 'O':   // Pyrrolysine
                case 'P':
                case 'Q':
                case 'R':
                case 'S':
                case 'T':
                case 'U':   // Selenocysteine
                case 'V':
                case 'W':
                case 'Y':
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsExAA(char c)
        {
            if (IsAA(c))
                return true;

            // Indeterminate symbols
            switch (c)
            {
                case 'B':   // Aspartic acid or Asparagine
                // TODO: Should J be allowed?
                case 'J':
                case 'X':   // Any
                case 'Z':   // Glutamic acid or Glutamine
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Mass calculator for amino acid sequences.
    /// </summary>
    public class SequenceMassCalc : IPrecursorMassCalc, IFragmentMassCalc
    {
        public static int MassPrecision { get { return 6; } }

        /// <summary>
        /// Average mass of an amino acid from 
        /// http://www.sciencedirect.com/science?_ob=ArticleURL&_udi=B6TH2-3VXYTSN-G&_user=582538&_rdoc=1&_fmt=&_orig=search&_sort=d&view=c&_acct=C000029718&_version=1&_urlVersion=0&_userid=582538&md5=ee0d1eba6e6c7e34d031d85ce9613eec
        /// </summary>
        public static double MassAveragine { get { return 111.1254; } }

        /// <summary>
        /// Returns a mass + H value that has been correctly rounded,
        /// to allow it to be persisted to XML that can be reloaded,
        /// and saved again without change.
        /// </summary>
        /// <param name="mh">Initial high-precision mass + h value</param>
        /// <returns>Rounded mass + h value</returns>
        public static double PersistentMH(double mh)
        {
            return PersistentNeutral(mh) + BioMassCalc.MassProton;
        }

        /// <summary>
        /// Returns a neutral mass rounded for output to XML.  The
        /// initial mass + h value should have come from a call to
        /// PersistentMH in order for this value to be reloaded and
        /// saved again without change.
        /// </summary>
        /// <param name="mh">Initial mass + h</param>
        /// <returns>Rounded neutral mass value</returns>
        public static double PersistentNeutral(double mh)
        {
            return Math.Round(mh - BioMassCalc.MassProton, MassPrecision);            
        }

        /// <summary>
        /// Returns a m/z value rounded for output to XML.
        /// </summary>
        /// <param name="mz">Initial m/z value</param>
        /// <returns>Rounded m/z value</returns>
        public static double PersistentMZ(double mz)
        {
            return Math.Round(mz, MassPrecision);
        }

        public static double GetMZ(double massH, int charge)
        {
            return (massH + (charge - 1)*BioMassCalc.MassProton)/charge;
        }

        public static double GetMH(double mz, int charge)
        {
            return mz*charge - (charge - 1)*BioMassCalc.MassProton;
        }

        public static double ParseModMass(BioMassCalc calc, string desc)
        {
            string parse = desc;
            double totalMass = calc.ParseMass(ref parse);
            if (parse.Length > 0 && parse[0] == '-')
            {
                parse = parse.Substring(1);
                totalMass -= calc.ParseMass(ref parse);
            }

            if (totalMass == 0.0 || parse.Length > 0)
                throw new ArgumentException(string.Format("The expression '{0}' is not a valid chemical formula.", desc));

            return Math.Round(totalMass, MassPrecision);
        }

        public static string GetModDiffDescription(double massDiff)
        {
            return GetModDiffDescription(massDiff, false);
        }

        public static string GetModDiffDescription(double massDiff, bool formatNarrow)
        {
            if (formatNarrow)
                // Narrow format allows for removal of .0 when decimal is not present
                // One of the more important cases is 15N labeling which produces a lot of
                // [+1] and [+2] values.  Also assumed to be for UI, so use local format.
                return string.Format("[{0}{1}]", (massDiff > 0 ? "+" : ""), Math.Round(massDiff, 1));
            else
                // Non-narrow format is used for library look-up and must be consistent with LibKey format
                return string.Format(CultureInfo.InvariantCulture, "[{0}{1:F01}]", (massDiff > 0 ? "+" : ""), massDiff);
        }

        private readonly BioMassCalc _massCalc;
        private readonly double[] _aminoMasses = new double[128];
        private readonly double[] _aminoModMasses = new double[128];
        private readonly double[] _aminoNTermModMasses = new double[128];
        private readonly double[] _aminoCTermModMasses = new double[128];
        // private readonly double _massWater;
        // private readonly double _massAmmonia;
        private readonly double _massDiffA;
        private readonly double _massDiffB;
        private readonly double _massDiffC;
        private readonly double _massDiffX;
        private readonly double _massDiffY;
        private readonly double _massDiffZ;
        private readonly double _massCleaveC;
        private double _massModCleaveC;
        private readonly double _massCleaveN;
        private double _massModCleaveN;

        public SequenceMassCalc(MassType type)
        {
            _massCalc = new BioMassCalc(type);

            // Mass of a proton, i.e. +1 positive charge, hydrogen atom without its electron.
            // See http://antoine.frostburg.edu/chem/senese/101/atoms/index.shtml
            // _massWater = _massCalc.CalculateMass("H2O");
            // _massAmmonia = _massCalc.CalculateMass("NH3");
            _massDiffA = -_massCalc.CalculateMass("CO");
            _massDiffB = 0.0;
            _massDiffC = _massCalc.CalculateMass("NH3");
            _massDiffY = _massCalc.CalculateMass("H2O");
            _massDiffX = _massCalc.CalculateMass("CO2");
            _massDiffZ = _massDiffY - _massCalc.CalculateMass("NH2");

            _massCleaveN = _massCalc.CalculateMass("H");
            _massCleaveC = _massCalc.CalculateMass("OH");

            InitAminoAcidMasses();
        }

        public double ParseModMass(string formula)
        {
            return ParseModMass(_massCalc, formula);
        }

        public double GetModMass(char aa, StaticMod mod)
        {
            if (!string.IsNullOrEmpty(mod.Formula))
                return ParseModMass(mod.Formula);
            else if (mod.LabelAtoms != LabelAtoms.None)
                return ParseModMass(GetHeavyFormula(aa, mod.LabelAtoms));
            else if (_massCalc.MassType == MassType.Monoisotopic)
                return mod.MonoisotopicMass ?? 0;
            else
                return mod.AverageMass ?? 0;            
        }

        public void AddStaticModifications(IEnumerable<StaticMod> mods)
        {
            foreach (StaticMod mod in mods)
            {

                if (!mod.AA.HasValue)
                {
                    if (mod.Terminus != null)
                    {
                        double mass = GetModMass('\0', mod);
                        if (mod.Terminus == ModTerminus.C)
                            _massModCleaveC += mass;
                        else
                            _massModCleaveN += mass;
                    }
                    else
                    {
                        // Label all amino acids with this label
                        for (char aa = 'A'; aa <= 'Z'; aa++)
                        {
                            if (AMINO_FORMULAS[aa] != null)
                                _aminoModMasses[aa] = _aminoModMasses[char.ToLower(aa)] += GetModMass(aa, mod);
                        }
                    }
                }
                else
                {
                    char aa = mod.AA.Value;
                    double mass = GetModMass(aa, mod);
                    switch (mod.Terminus)
                    {
                        default:
                            _aminoModMasses[aa] = _aminoModMasses[char.ToLower(aa)] += mass;
                            break;
                        case ModTerminus.N:
                            _aminoNTermModMasses[aa] = _aminoNTermModMasses[char.ToLower(aa)] += mass;
                            break;
                        case ModTerminus.C:
                            _aminoCTermModMasses[aa] = _aminoCTermModMasses[char.ToLower(aa)] += mass;
                            break;
                    }
                }
            }
        }

        public bool IsModified(string seq)
        {
            if (string.IsNullOrEmpty(seq))
                return false;
            if (_massModCleaveC + _massModCleaveN != 0)
                return true;
            int len = seq.Length;
            if (_aminoNTermModMasses[seq[0]] + _aminoCTermModMasses[seq[len - 1]] != 0)
                return true;
            foreach (char c in seq)
            {
                if (_aminoModMasses[c] != 0)
                    return true;
            }
            return false;
        }

        public string GetModifiedSequence(string seq, bool formatNarrow)
        {
            return GetModifiedSequence(seq, null, formatNarrow);
        }

        public string GetModifiedSequence(string seq, IList<double> mods, bool formatNarrow)
        {
            // If no modifications, do nothing
            if (!IsModified(seq) && mods == null)
                return seq;

            // Otherwise, build a modified sequence string like AMC[+57.0]LP[-37.1]K
            StringBuilder sb = new StringBuilder();
            for (int i = 0, len = seq.Length; i < len; i++)
            {
                char c = seq[i];
                double mod = _aminoModMasses[c];
                // Explicit modifications
                if (mods != null && i < mods.Count)
                    mod += mods[i];
                // Terminal modifications
                if (i == 0)
                    mod += _massModCleaveN + _aminoNTermModMasses[c];
                else if (i == len - 1)
                    mod += _massModCleaveC + _aminoCTermModMasses[c];

                sb.Append(c);
                if (mod != 0)
                    sb.Append(GetModDiffDescription(mod, formatNarrow));
            }
            return sb.ToString();
        }

        public MassType MassType
        {
            get { return _massCalc.MassType; }
        }

        public double GetPrecursorMass(string seq)
        {
            return GetPrecursorMass(seq, null);
        }

        public double GetPrecursorMass(string seq, IList<double> mods)
        {
            double mass = _massCleaveN + _massModCleaveN +
                _massCleaveC + _massModCleaveC + BioMassCalc.MassProton;

            // Add any amino acid terminal specific modifications
            int len = seq.Length;
            if (len > 0)
                mass += _aminoNTermModMasses[seq[0]];
            if (len > 1)
                mass += _aminoCTermModMasses[seq[len - 1]];

            // Add masses of amino acids
            for (int i = 0; i < len; i++)
            {
                char c = seq[i];
                mass += _aminoMasses[c] + _aminoModMasses[c];
                if (mods != null && i < mods.Count)
                    mass += mods[i];
            }
            return mass;                
        }

        public double[,] GetFragmentIonMasses(string seq)
        {
            return GetFragmentIonMasses(seq, null);
        }

        public double[,] GetFragmentIonMasses(string seq, IList<double> mods)
        {
            int len = seq.Length - 1;
            const int a = (int) IonType.a;
            const int b = (int) IonType.b;
            const int c = (int) IonType.c;
            const int x = (int) IonType.x;
            const int y = (int) IonType.y;
            const int z = (int) IonType.z;

            double nTermMassB = _massDiffB + _massModCleaveN + BioMassCalc.MassProton;
            double deltaA = _massDiffA - _massDiffB;
            double deltaC = _massDiffC - _massDiffB;
            double cTermMassY = _massDiffY + _massModCleaveC + BioMassCalc.MassProton;
            double deltaX = _massDiffX - _massDiffY;
            double deltaZ = _massDiffZ - _massDiffY;

            double[,] masses = new double[z + 1, len];

            int iN = 0, iC = len;
            nTermMassB += _aminoNTermModMasses[seq[iN]];
            cTermMassY += _aminoCTermModMasses[seq[iC]];
            while (iC > 0)
            {
                char aa = seq[iN];
                nTermMassB += _aminoMasses[aa] + _aminoModMasses[aa];
                if (mods != null && iN < mods.Count)
                    nTermMassB += mods[iN];
                masses[a, iN] = nTermMassB + deltaA;
                masses[b, iN] = nTermMassB;
                masses[c, iN] = nTermMassB + deltaC;
                iN++;

                aa = seq[iC];
                cTermMassY += _aminoMasses[aa] + _aminoModMasses[aa];
                if (mods != null && iC < mods.Count)
                    cTermMassY += mods[iC];
                iC--;
                masses[x, iC] = cTermMassY + deltaX;
                masses[y, iC] = cTermMassY;
                masses[z, iC] = cTermMassY + deltaZ;
            }

            return masses;
        }

        public static IEnumerable<double> GetFragmentMasses(IonType type, double[,] masses)
        {
            int col = (int) type;
            int len = masses.GetLength(1);
            if (Transition.IsNTerminal(type))
            {
                for (int i = 0; i < len; i++)
                    yield return masses[col, i];
            }
            else
            {
                for (int i = len - 1; i >= 0; i--)
                    yield return masses[col, i];
            }
        }

        public double GetFragmentMass(Transition transition)
        {
            return GetFragmentMass(transition, null);
        }

        public double GetFragmentMass(Transition transition, IList<double> mods)
        {
            return GetFragmentMass(transition.Group.Peptide.Sequence,
                transition.IonType, transition.Ordinal, mods);
        }

        public double GetPrecursorFragmentMass(string seq)
        {
            return GetFragmentMass(seq, IonType.precursor, seq.Length);
        }

        public double GetFragmentMass(string seq, IonType type, int ordinal)
        {
            return GetFragmentMass(seq, type, ordinal, null);
        }

        public double GetFragmentMass(string seq, IonType type, int ordinal, IList<double> mods)
        {
            if (Transition.IsPrecursor(type))
                return GetPrecursorMass(seq, mods);

            int len = seq.Length - 1;

            double mass = GetTermMass(type) + BioMassCalc.MassProton;
            bool nterm = Transition.IsNTerminal(type);
            int iA = (nterm ? 0 : len);
            int inc = (nterm ? 1 : -1);

            mass += (nterm ? _aminoNTermModMasses[seq[iA]] : _aminoCTermModMasses[seq[iA]]);
            for (int i = 0; i < ordinal; i++)
            {
                char aa = seq[iA];
                mass += _aminoMasses[aa] + _aminoModMasses[aa];
                if (mods != null && iA < mods.Count)
                    mass += mods[iA];
                iA += inc;
            }

            return mass;
        }

        private double GetTermMass(IonType type)
        {
            switch (type)
            {
                case IonType.a: return _massDiffA + _massModCleaveN;
                case IonType.b: return _massDiffB + _massModCleaveN;
                case IonType.c: return _massDiffC + _massModCleaveN;
                case IonType.x: return _massDiffX + _massModCleaveC;
                case IonType.y: return _massDiffY + _massModCleaveC;
                case IonType.z: return _massDiffZ + _massModCleaveC;
                default:
                    throw new ArgumentException("Invalid ion type");
            }
        }

        /// <summary>
        /// Initializes the masses for amino acid characters in the mass look-up.
        /// <para>
        /// See Wikipedia FASTA Format page for details:
        /// http://en.wikipedia.org/wiki/FASTA_format#Sequence_identifiers
        /// </para>
        /// </summary>
        private void InitAminoAcidMasses()
        {
            for (int i = 0; i < AMINO_FORMULAS.Length; i++)
            {
                string formula = AMINO_FORMULAS[i];
                if (formula != null)
                    _aminoMasses[i] = _massCalc.CalculateMass(formula);
            }

            // ReSharper disable CharImplicitlyConvertedToNumeric
            // Handle values for non-amino acids
            // Wikipedia says Aspartic acid or Asparagine
            _aminoMasses['b'] = _aminoMasses['B'] =
                (_massCalc.CalculateMass("C4H5NO3") + _massCalc.CalculateMass("C4H6N2O2")) / 2;
            _aminoMasses['j'] = _aminoMasses['J'] = 0.0;
            _aminoMasses['x'] = _aminoMasses['X'] = 111.060000;	// Why?
            // Wikipedia says Glutamic acid or Glutamine
            _aminoMasses['z'] = _aminoMasses['Z'] =
                (_massCalc.CalculateMass("C5H6ON2") + _massCalc.CalculateMass("C5H8N2O2")) / 2;
            // ReSharper restore CharImplicitlyConvertedToNumeric
        }

        private static readonly string[] AMINO_FORMULAS = new string[128];

        static SequenceMassCalc()
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

        public static string GetAminoAcidFormula(char aa)
        {
            return AMINO_FORMULAS[aa];
        }

        public static string GetHeavyFormula(char aa, LabelAtoms labelAtoms)
        {
            string formulaAA = AMINO_FORMULAS[aa];
            string formulaHeavy = formulaAA;
            if ((labelAtoms & LabelAtoms.C13) != 0)
                formulaHeavy = formulaHeavy.Replace(BioMassCalc.C, BioMassCalc.C13);
            if ((labelAtoms & LabelAtoms.N15) != 0)
                formulaHeavy = formulaHeavy.Replace(BioMassCalc.N, BioMassCalc.N15);
            if ((labelAtoms & LabelAtoms.O18) != 0)
                formulaHeavy = formulaHeavy.Replace(BioMassCalc.O, BioMassCalc.O18);
            if ((labelAtoms & LabelAtoms.H2) != 0)
                formulaHeavy = formulaHeavy.Replace(BioMassCalc.H, BioMassCalc.H2);
            return formulaHeavy + " - " + formulaAA;
        }
    }

    public class ExplicitSequenceMassCalc : IPrecursorMassCalc, IFragmentMassCalc
    {
        private readonly SequenceMassCalc _massCalcBase;
        private readonly IList<double> _mods;

        public ExplicitSequenceMassCalc(SequenceMassCalc massCalcBase, IList<double> mods)
        {
            _massCalcBase = massCalcBase;
            _mods = mods;
        }

        public double GetPrecursorMass(string seq)
        {
            return _massCalcBase.GetPrecursorMass(seq, _mods);
        }

        public bool IsModified(string seq)
        {
            return _massCalcBase.IsModified(seq) ||
                _mods.IndexOf(m => m != 0) != -1; // If any non-zero modification values
        }

        public string GetModifiedSequence(string seq, bool formatNarrow)
        {
            return _massCalcBase.GetModifiedSequence(seq, _mods, formatNarrow);
        }

        public double[,] GetFragmentIonMasses(string seq)
        {
            return _massCalcBase.GetFragmentIonMasses(seq, _mods);
        }

        public double GetFragmentMass(Transition transition)
        {
            return _massCalcBase.GetFragmentMass(transition, _mods);
        }

        public double GetFragmentMass(string seq, IonType type, int ordinal)
        {
            return _massCalcBase.GetFragmentMass(seq, type, ordinal, _mods);
        }

        public double GetPrecursorFragmentMass(string seq)
        {
            return _massCalcBase.GetPrecursorFragmentMass(seq);
        }
    }
}