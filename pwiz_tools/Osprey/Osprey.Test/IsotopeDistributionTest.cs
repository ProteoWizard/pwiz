/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Tests for <see cref="IsotopeDistribution"/> (averagine-free isotope
    /// distribution from exact elemental composition), ported from
    /// osprey-core/src/isotope.rs. These exercise the HRAM-only MS1 isotope
    /// path that the unit-resolution regression never reaches.
    /// </summary>
    [TestClass]
    public class IsotopeDistributionTest
    {
        private const double TOLERANCE = 1e-6;

        #region PeptideComposition Tests

        [TestMethod]
        public void TestPeptideCompositionPeptide()
        {
            // PEPTIDE = sum of residue compositions + terminal H2O.
            //   P:C5H7NO  E:C5H7NO3  P:C5H7NO  T:C4H7NO2
            //   I:C6H11NO D:C4H5NO3  E:C5H7NO3
            //   C=34, H=51(+2)=53, N=7, O=14(+1)=15, S=0
            int c, h, n, o, s;
            Assert.IsTrue(IsotopeDistribution.PeptideComposition("PEPTIDE",
                out c, out h, out n, out o, out s));
            Assert.AreEqual(34, c);
            Assert.AreEqual(53, h);
            Assert.AreEqual(7, n);
            Assert.AreEqual(15, o);
            Assert.AreEqual(0, s);
        }

        [TestMethod]
        public void TestPeptideCompositionSulfur()
        {
            // MCK exercises the sulfur path: M and C each carry one S.
            //   M:C5H9NOS  C:C3H5NOS  K:C6H12N2O
            //   C=14, H=26(+2)=28, N=4, O=3(+1)=4, S=2
            int c, h, n, o, s;
            Assert.IsTrue(IsotopeDistribution.PeptideComposition("MCK",
                out c, out h, out n, out o, out s));
            Assert.AreEqual(14, c);
            Assert.AreEqual(28, h);
            Assert.AreEqual(4, n);
            Assert.AreEqual(4, o);
            Assert.AreEqual(2, s);
        }

        [TestMethod]
        public void TestPeptideCompositionSkipsModifications()
        {
            // Bracketed modification text + numeric mass delta is skipped, so a
            // modified sequence yields the same composition as the bare peptide.
            int c1, h1, n1, o1, s1;
            int c2, h2, n2, o2, s2;
            Assert.IsTrue(IsotopeDistribution.PeptideComposition("PEP[+79.966]TIDE",
                out c1, out h1, out n1, out o1, out s1));
            Assert.IsTrue(IsotopeDistribution.PeptideComposition("PEPTIDE",
                out c2, out h2, out n2, out o2, out s2));
            Assert.AreEqual(c2, c1);
            Assert.AreEqual(h2, h1);
            Assert.AreEqual(n2, n1);
            Assert.AreEqual(o2, o1);
            Assert.AreEqual(s2, s1);
        }

        [TestMethod]
        public void TestPeptideCompositionInvalidResidue()
        {
            // 'Z' is a letter but not a known amino acid -> composition fails.
            Assert.IsFalse(IsotopeDistribution.PeptideComposition("PEPTIDEZ",
                out _, out _, out _, out _, out _));
        }

        #endregion

        #region CalculateDistribution Tests

        [TestMethod]
        public void TestCalculateDistributionNormalizedAndMonotonic()
        {
            int c, h, n, o, s;
            Assert.IsTrue(IsotopeDistribution.PeptideComposition("PEPTIDE",
                out c, out h, out n, out o, out s));
            double[] dist = IsotopeDistribution.CalculateDistribution(c, h, n, o, s);

            Assert.AreEqual(5, dist.Length);

            // Normalized to sum 1.
            double sum = 0;
            foreach (double p in dist) sum += p;
            Assert.AreEqual(1.0, sum, TOLERANCE);

            // For a sub-1 kDa peptide the monoisotopic (M+0) peak dominates and
            // the envelope decreases monotonically.
            Assert.IsTrue(dist[0] > 0.5, "M+0 should dominate, got " + dist[0]);
            for (int i = 0; i < 4; i++)
                Assert.IsTrue(dist[i] > dist[i + 1],
                    string.Format("Envelope should decrease: dist[{0}]={1} !> dist[{2}]={3}",
                        i, dist[i], i + 1, dist[i + 1]));
        }

        [TestMethod]
        public void TestCalculateDistributionPureCarbon()
        {
            // Pure carbon C34: only the 13C binomial contributes. M+0 ~ 0.690,
            // M+1 ~ 0.257 (34 * 0.01084 weighting). Hand-checked against the
            // binomial expansion truncated at M+4 then normalized.
            double[] dist = IsotopeDistribution.CalculateDistribution(34, 0, 0, 0, 0);

            double sum = 0;
            foreach (double p in dist) sum += p;
            Assert.AreEqual(1.0, sum, TOLERANCE);

            Assert.AreEqual(0.690, dist[0], 0.005);
            Assert.AreEqual(0.257, dist[1], 0.005);
            Assert.IsTrue(dist[2] > 0 && dist[2] < 0.06);
        }

        #endregion

        #region IsotopeCosineScore Tests

        [TestMethod]
        public void TestIsotopeCosinePerfectMatch()
        {
            // theoretical = [M+0..M+4]; the observed envelope is aligned as
            // [M-1=0, M+0, M+1, M+2, M+3]. Building observed from theoretical
            // gives a perfect cosine of 1.0.
            double[] theo = IsotopeDistribution.CalculateDistribution(34, 53, 7, 15, 0);
            double[] observed = { 0.0, theo[0], theo[1], theo[2], theo[3] };

            double cosine = IsotopeDistribution.IsotopeCosineScore(observed, theo);
            Assert.AreEqual(1.0, cosine, TOLERANCE);
        }

        [TestMethod]
        public void TestIsotopeCosineInvalidInputs()
        {
            double[] theo = { 0.6, 0.25, 0.1, 0.04, 0.01 };

            // Null / too-short arrays return the -1 sentinel.
            Assert.AreEqual(-1.0, IsotopeDistribution.IsotopeCosineScore(null, theo), TOLERANCE);
            Assert.AreEqual(-1.0,
                IsotopeDistribution.IsotopeCosineScore(new[] { 1.0, 2.0 }, theo), TOLERANCE);
            // All-zero observed -> zero norm -> -1 sentinel.
            Assert.AreEqual(-1.0,
                IsotopeDistribution.IsotopeCosineScore(new double[5], theo), TOLERANCE);
        }

        [TestMethod]
        public void TestIsotopeCosineOrthogonalClampsToZero()
        {
            // Observed mass only at M-1, where theoretical is 0 -> dot product 0
            // -> cosine clamped to 0.0 (never negative).
            double[] theo = { 0.6, 0.25, 0.1, 0.04, 0.01 };
            double[] observed = { 10.0, 0.0, 0.0, 0.0, 0.0 };

            double cosine = IsotopeDistribution.IsotopeCosineScore(observed, theo);
            Assert.AreEqual(0.0, cosine, TOLERANCE);
        }

        #endregion

        #region PeptideIsotopeCosine Tests

        [TestMethod]
        public void TestPeptideIsotopeCosineRoundTrip()
        {
            int c, h, n, o, s;
            Assert.IsTrue(IsotopeDistribution.PeptideComposition("PEPTIDE",
                out c, out h, out n, out o, out s));
            double[] theo = IsotopeDistribution.CalculateDistribution(c, h, n, o, s);
            double[] observed = { 0.0, theo[0], theo[1], theo[2], theo[3] };

            double cosine = IsotopeDistribution.PeptideIsotopeCosine("PEPTIDE", observed);
            Assert.AreEqual(1.0, cosine, TOLERANCE);
        }

        [TestMethod]
        public void TestPeptideIsotopeCosineInvalidSequence()
        {
            // Composition fails on a non-amino-acid letter -> -1 sentinel.
            double[] observed = { 0.0, 0.6, 0.25, 0.1, 0.04 };
            Assert.AreEqual(-1.0,
                IsotopeDistribution.PeptideIsotopeCosine("ZZZ", observed), TOLERANCE);
        }

        #endregion
    }
}
