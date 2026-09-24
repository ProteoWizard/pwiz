/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on compomics-utilities 5.0.39P2 (https://github.com/compomics/compomics-utilities)
 *   AminoAcid, StandardMasses, ElementaryIon and Peptide.estimateTheoreticMass, Apache-2.0,
 *   and on java.util.stream.DoubleStream.sum
 *
 * Copyright 2026 University of Washington - Seattle, WA
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

using System.Collections.Generic;

namespace pwiz.CarafeSharp.Proteome
{
    /// <summary>
    /// The full-precision masses compomics computes a Carafe peptidoform's mass from, and that
    /// computation itself: water plus the residues, then plus the modifications, each group
    /// added with Java's compensated <c>DoubleStream.sum</c>, so the result matches to the bit.
    /// Values are those of the Carafe-vendored jar.
    /// </summary>
    public static class CompomicsMasses
    {
        /// <summary><c>ElementaryIon.proton.getTheoreticMass()</c>.</summary>
        public const double PROTON = 1.007276466812;

        /// <summary><c>StandardMasses.h2o.mass</c>.</summary>
        public const double H2O = 18.0105646837;

        private static readonly double[] RESIDUE_MASSES = BuildResidueMasses();

        /// <summary>The monoisotopic mass of one of the 20 standard residues, NaN otherwise.</summary>
        public static double GetResidueMass(char aa)
        {
            return aa >= 'A' && aa <= 'Z' ? RESIDUE_MASSES[aa - 'A'] : double.NaN;
        }

        /// <summary>
        /// <c>Peptide.getMass()</c> for a standard-residue sequence carrying modifications of the
        /// given masses, in the order they were added to the peptide.
        /// </summary>
        public static double PeptideMass(string sequence, IEnumerable<double> modificationMasses)
        {
            var residues = new JavaDoubleSum();
            foreach (char aa in sequence)
                residues.Add(GetResidueMass(aa));
            double mass = H2O + residues.Result;
            var modifications = new JavaDoubleSum();
            foreach (double modMass in modificationMasses)
                modifications.Add(modMass);
            mass += modifications.Result;
            // Plus the (always empty) search-parameter fixed modifications, an empty sum.
            return mass + new JavaDoubleSum().Result;
        }

        private static double[] BuildResidueMasses()
        {
            var masses = new double[26];
            for (int i = 0; i < masses.Length; i++)
                masses[i] = double.NaN;
            masses['A' - 'A'] = 71.03711378471;
            masses['C' - 'A'] = 103.00918478471;
            masses['D' - 'A'] = 115.02694302383;
            masses['E' - 'A'] = 129.04259308797;
            masses['F' - 'A'] = 147.06841391299;
            masses['G' - 'A'] = 57.02146372057;
            masses['H' - 'A'] = 137.05891185845;
            masses['I' - 'A'] = 113.08406397713;
            masses['K' - 'A'] = 128.094963014;
            masses['L' - 'A'] = 113.08406397713;
            masses['M' - 'A'] = 131.04048491299;
            masses['N' - 'A'] = 114.04292744114;
            masses['P' - 'A'] = 97.05276384884999;
            masses['Q' - 'A'] = 128.05857750528;
            masses['R' - 'A'] = 156.1011110236;
            masses['S' - 'A'] = 87.03202840427;
            masses['T' - 'A'] = 101.04767846841;
            masses['V' - 'A'] = 99.06841391299;
            masses['W' - 'A'] = 186.07931294986;
            masses['Y' - 'A'] = 163.06332853255;
            return masses;
        }

        /// <summary>
        /// <c>DoubleStream.sum()</c> on a sequential stream: Kahan summation as
        /// <c>Collectors.sumWithCompensation</c> and <c>computeFinalSum</c> do it, with the plain
        /// sum kept only to return an infinity the compensated sum turns into NaN.
        /// </summary>
        private struct JavaDoubleSum
        {
            private double _sum;
            private double _compensation;
            private double _simpleSum;

            public void Add(double value)
            {
                double tmp = value - _compensation;
                double velvel = _sum + tmp;
                _compensation = velvel - _sum - tmp;
                _sum = velvel;
                _simpleSum += value;
            }

            public double Result
            {
                get
                {
                    double tmp = _sum - _compensation;
                    return double.IsNaN(tmp) && double.IsInfinity(_simpleSum) ? _simpleSum : tmp;
                }
            }
        }
    }
}
