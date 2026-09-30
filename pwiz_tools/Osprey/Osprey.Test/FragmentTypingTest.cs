/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Osprey's own fragment typing (<see cref="FragmentTyping"/>) and its comparison with the
    /// types a library states (<see cref="FragmentTypeCheck"/>).
    /// </summary>
    [TestClass]
    public class FragmentTypingTest
    {
        private const string SEQUENCE = @"PEPTIDEK";

        // The first twelve residues repeat the first six, so b6 and b12^2 share one m/z.
        private const string REPEAT = @"AAAAAAAAAAAAAAK";

        [TestMethod]
        public void TestFragmentTypingRules()
        {
            AssertNearestPrimaryIonWithinTolerance();
            AssertChargeLimitAndNoLosses();
            AssertTiesAndOnePeakPerIon();
            AssertNearerIonWinsOverLowerCharge();
            AssertStackedModificationsType();
            AssertNonFinitePeakIsUntyped();
        }

        /// <summary>
        /// A peak takes the nearest primary b or y ion within the search tolerance - ppm taken at
        /// the ion's m/z, or Th - and a peak just outside it stays Unknown.
        /// </summary>
        private static void AssertNearestPrimaryIonWithinTolerance()
        {
            double y3 = Mz(IonType.Y, 3, 1, SEQUENCE, null);
            double b4 = Mz(IonType.B, 4, 1, SEQUENCE, null);
            var ppm10 = FragmentToleranceConfig.Hram(10);
            double inside = y3 * 9.9e-6;
            double outside = y3 * 10.1e-6;
            var types = Type(SEQUENCE, 2, ppm10, y3, y3 + inside, y3 - inside, y3 + outside, b4);
            AssertIon(types[0], IonType.Y, 3, 1);
            AssertIon(types[1], IonType.Y, 3, 1);
            AssertIon(types[2], IonType.Y, 3, 1);
            Assert.AreEqual(IonType.Unknown, types[3].IonType, @"just outside 10 ppm");
            AssertIon(types[4], IonType.B, 4, 1);

            // Unit resolution: 0.5 Th. y4 has no other ion within reach; y3 does - b7^2 sits
            // 0.5 Th above it - so a peak between them takes the nearer.
            var th05 = FragmentToleranceConfig.UnitResolution(0.5);
            double y4 = Mz(IonType.Y, 4, 1, SEQUENCE, null);
            double b7Doubly = Mz(IonType.B, 7, 2, SEQUENCE, null);
            types = Type(SEQUENCE, 2, th05, y4 + 0.45, y4 - 0.55, y3 + 0.2, b7Doubly - 0.2);
            AssertIon(types[0], IonType.Y, 4, 1);
            Assert.AreEqual(IonType.Unknown, types[1].IonType, @"just outside 0.5 Th");
            AssertIon(types[2], IonType.Y, 3, 1);
            AssertIon(types[3], IonType.B, 7, 2);
        }

        /// <summary>
        /// Fragment charge runs to min(precursor charge, 2): a doubly charged ion is typed for a
        /// 2+ precursor and not for a 1+ one, and no triply charged ion is typed for a 3+ one. A
        /// neutral-loss peak is not typed as the ion it lost from.
        /// </summary>
        private static void AssertChargeLimitAndNoLosses()
        {
            var ppm10 = FragmentToleranceConfig.Hram(10);
            double y6Doubly = Mz(IonType.Y, 6, 2, SEQUENCE, null);
            double y7Triply = Mz(IonType.Y, 7, 3, SEQUENCE, null);
            double y5Water = Mz(IonType.Y, 5, 1, SEQUENCE, NeutralLoss.H2OMass);
            AssertIon(Type(SEQUENCE, 2, ppm10, y6Doubly)[0], IonType.Y, 6, 2);
            Assert.AreEqual(IonType.Unknown, Type(SEQUENCE, 1, ppm10, y6Doubly)[0].IonType, @"no 2+ ion of a 1+ precursor");
            Assert.AreEqual(IonType.Unknown, Type(SEQUENCE, 3, ppm10, y7Triply)[0].IonType, @"no 3+ ions");
            Assert.AreEqual(IonType.Unknown, Type(SEQUENCE, 2, ppm10, y5Water)[0].IonType, @"no neutral losses");

            Assert.IsTrue(FragmentTyping.IsPrimary(new FragmentAnnotation { IonType = IonType.Y, Ordinal = 3, Charge = 2 }));
            Assert.IsFalse(FragmentTyping.IsPrimary(new FragmentAnnotation { IonType = IonType.Y, Ordinal = 3, Charge = 3 }));
            Assert.IsFalse(FragmentTyping.IsPrimary(new FragmentAnnotation { IonType = IonType.A, Ordinal = 3, Charge = 1 }));
            Assert.IsFalse(FragmentTyping.IsPrimary(new FragmentAnnotation
                { IonType = IonType.Y, Ordinal = 3, Charge = 1, NeutralLoss = NeutralLossCode.H2O }));
        }

        /// <summary>
        /// Two ions at one m/z - b6 and b12^2 of a peptide whose first twelve residues repeat its
        /// first six - go to the lower charge: the peak is typed b6, never left untyped. Each ion
        /// types one peak, the most intense first, so a second peak at that m/z takes b12^2, and a
        /// third, with both ions claimed, stays Unknown.
        /// </summary>
        private static void AssertTiesAndOnePeakPerIon()
        {
            var ppm10 = FragmentToleranceConfig.Hram(10);
            double b6 = Mz(IonType.B, 6, 1, REPEAT, null);
            Assert.AreEqual(b6, Mz(IonType.B, 12, 2, REPEAT, null), 1e-9);
            AssertIon(Type(REPEAT, 2, ppm10, b6)[0], IonType.B, 6, 1);

            var stats = new FragmentTypingStats();
            var fragments = Fragments((b6, 0.1f), (b6, 1.0f), (b6, 0.05f));
            FragmentTyping.TypeFragments(REPEAT, null, 2, fragments, ppm10, stats);
            AssertIon(fragments[1].Annotation, IonType.B, 6, 1);
            AssertIon(fragments[0].Annotation, IonType.B, 12, 2);
            Assert.AreEqual(IonType.Unknown, fragments[2].Annotation.IonType, @"both ions already claimed");
            Assert.AreEqual(2, stats.Typed);
            Assert.AreEqual(1, stats.Untyped);
        }

        /// <summary>
        /// Only ions equally near within floating-point rounding tie: b5^2 and y2 of GIRSHNETPEK
        /// are distinct compositions 0.7 mTh apart, so a peak at either takes that ion, and the
        /// charge preference does not turn a peak at b5^2 into the singly charged y2.
        /// </summary>
        private static void AssertNearerIonWinsOverLowerCharge()
        {
            const string sequence = @"GIRSHNETPEK";
            var ppm10 = FragmentToleranceConfig.Hram(10);
            double b5Doubly = Mz(IonType.B, 5, 2, sequence, null);
            double y2 = Mz(IonType.Y, 2, 1, sequence, null);
            Assert.IsTrue(ppm10.WithinTolerance(y2, b5Doubly), @"both ions reach both peaks");
            var types = Type(sequence, 2, ppm10, b5Doubly, y2);
            AssertIon(types[0], IonType.B, 5, 2);
            AssertIon(types[1], IonType.Y, 2, 1);
        }

        /// <summary>
        /// An N-terminal acetyl and an oxidized first methionine both sit at position 0, and every
        /// ion spanning it carries both: b3 is typed at their summed mass, and not at either alone.
        /// </summary>
        private static void AssertStackedModificationsType()
        {
            const string sequence = @"MPEPTIDEK";
            var mods = new[]
            {
                new Modification { Position = 0, MassDelta = 42.010565 },
                new Modification { Position = 0, MassDelta = 15.994915 },
            };
            var modMasses = PeptideFragmentMass.ModMassesByPosition(mods);
            double b3 = PeptideFragmentMass.CalculateFragmentMz(IonType.B, 3, 1, sequence, modMasses, null).Value;
            var fragments = Fragments((b3, 1f), (b3 - 42.010565, 0.5f));
            FragmentTyping.TypeFragments(sequence, mods, 2, fragments, FragmentToleranceConfig.Hram(10), null);
            AssertIon(fragments[0].Annotation, IonType.B, 3, 1);
            Assert.AreEqual(IonType.Unknown, fragments[1].Annotation.IonType, @"one modification light is no ion");
        }

        private static void AssertNonFinitePeakIsUntyped()
        {
            foreach (double mz in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                Assert.AreEqual(IonType.Unknown, Type(SEQUENCE, 2, FragmentToleranceConfig.UnitResolution(0.5), mz)[0].IonType);
        }

        /// <summary>
        /// A DIA-NN TSV keeps its columns as the typing the search uses, and what they state is
        /// compared with Osprey's typing: a y ion stated at a b ion's m/z differs, a stated
        /// neutral loss is outside the model. The check reports one line - a warning when a peak
        /// differs, else information - with the examples under <c>--verbose</c>; a reader given no
        /// tolerance checks nothing.
        /// </summary>
        [TestMethod]
        public void TestStatedTypesAreComparedWithTyping()
        {
            double b3 = Mz(IonType.B, 3, 1, SEQUENCE, null);
            double y3 = Mz(IonType.Y, 3, 1, SEQUENCE, null);
            double y4 = Mz(IonType.Y, 4, 1, SEQUENCE, null);
            double y5Water = Mz(IonType.Y, 5, 1, SEQUENCE, NeutralLoss.H2OMass);
            string tsv = @"ModifiedPeptide	StrippedPeptide	PrecursorMz	PrecursorCharge	Tr_recalibrated	ProteinID	FragmentMz	RelativeIntensity	FragmentType	FragmentNumber	FragmentCharge	FragmentLossType" + "\n" +
                         Line(y3, @"y", 3, @"noloss") +
                         Line(y4, @"y", 4, @"noloss") +
                         Line(b3, @"y", 6, @"noloss") +        // a y6 at b3's m/z: differs
                         Line(y5Water, @"y", 5, @"H2O");       // a loss: outside the model

            var loader = new DiannTsvLoader(2, FragmentToleranceConfig.Hram(10));
            List<LibraryEntry> entries;
            using (var reader = new StringReader(tsv))
            {
                entries = loader.ParseReader(reader);
            }
            // The columns are the typing: the stated y6 stays a y6.
            BlibLibraryInputTest.AssertAnnotation(entries.Single().Fragments[2].Annotation, IonType.Y, 6, 1, NeutralLossCode.None);
            var check = loader.TypeCheck;
            Assert.AreEqual(2, check.Agree);
            Assert.AreEqual(1, check.Differ);
            Assert.AreEqual(1, check.Outside);
            string example = string.Format(
                OspreyIOResources.FragmentTypeCheck_AddPeak__0___charge__1___peak_m_z__2_F4___the_library_says__3___Osprey_computes__4_,
                SEQUENCE, 2, b3, @"y6", @"b3");
            CollectionAssert.AreEqual(new[] { example }, check.Examples.ToArray());

            var info = new List<string>();
            var warnings = new List<string>();
            check.Report(@"library.tsv", false, info.Add, warnings.Add);
            CollectionAssert.AreEqual(new[] { check.Summary(@"library.tsv") }, warnings);
            Assert.AreEqual(0, info.Count, @"no examples without --verbose");
            warnings.Clear();
            check.Report(@"library.tsv", true, info.Add, warnings.Add);
            CollectionAssert.AreEqual(new[] { example }, info);

            // A library whose statements all agree reports a line of information, not a warning.
            var agreeing = new DiannTsvLoader(2, FragmentToleranceConfig.Hram(10));
            using (var reader = new StringReader(tsv.Substring(0, tsv.IndexOf(Line(b3, @"y", 6, @"noloss"), System.StringComparison.Ordinal))))
            {
                agreeing.ParseReader(reader);
            }
            info.Clear();
            warnings.Clear();
            agreeing.TypeCheck.Report(@"library.tsv", true, info.Add, warnings.Add);
            Assert.AreEqual(0, warnings.Count);
            CollectionAssert.AreEqual(new[] { agreeing.TypeCheck.Summary(@"library.tsv") }, info);

            // A stated ion Osprey agrees is possible but would not choose itself - b12^2 of a
            // repeat, where Osprey prefers the b6 at the same m/z - is the library's choice, not
            // a disagreement: counted apart, and no warning.
            double b6 = Mz(IonType.B, 6, 1, REPEAT, null);
            string isobaricTsv = tsv.Substring(0, tsv.IndexOf('\n') + 1) +
                                 Line(Mz(IonType.Y, 3, 1, REPEAT, null), @"y", 3, @"noloss").Replace(@"PEPTIDEK", REPEAT) +
                                 Line(b6, @"b", 12, @"noloss", 2).Replace(@"PEPTIDEK", REPEAT);
            var isobaric = new DiannTsvLoader(2, FragmentToleranceConfig.Hram(10));
            using (var reader = new StringReader(isobaricTsv))
            {
                isobaric.ParseReader(reader);
            }
            Assert.AreEqual(1, isobaric.TypeCheck.Agree);
            Assert.AreEqual(1, isobaric.TypeCheck.LibraryChoice);
            Assert.AreEqual(0, isobaric.TypeCheck.Differ);
            warnings.Clear();
            isobaric.TypeCheck.Report(@"library.tsv", true, info.Add, warnings.Add);
            Assert.AreEqual(0, warnings.Count);

            // Without a tolerance nothing is compared.
            var noTolerance = new DiannTsvLoader(2);
            using (var reader = new StringReader(tsv))
            {
                noTolerance.ParseReader(reader);
            }
            Assert.IsFalse(noTolerance.TypeCheck.AnyStated);
        }

        private static string Line(double mz, string type, int number, string loss, int fragmentCharge = 1)
        {
            var invariant = System.Globalization.CultureInfo.InvariantCulture;
            return string.Join("\t", @"_PEPTIDEK_", SEQUENCE, @"466.7", @"2", @"10.5", @"P1", mz.ToString(invariant),
                @"1.0", type, number.ToString(invariant), fragmentCharge.ToString(invariant), loss) + "\n";
        }

        /// <summary>The type each of <paramref name="peaks"/> takes as the only peak of its spectrum.</summary>
        private static FragmentAnnotation[] Type(string sequence, int precursorCharge, FragmentToleranceConfig tolerance,
            params double[] peaks)
        {
            return peaks.Select(mz =>
            {
                var fragments = Fragments((mz, 1f));
                FragmentTyping.TypeFragments(sequence, null, precursorCharge, fragments, tolerance, null);
                return fragments[0].Annotation;
            }).ToArray();
        }

        private static LibraryFragment[] Fragments(params (double Mz, float Intensity)[] peaks)
        {
            return peaks.Select(peak => new LibraryFragment
            {
                Mz = peak.Mz,
                RelativeIntensity = peak.Intensity,
                Annotation = new FragmentAnnotation { IonType = IonType.Unknown, Charge = 1 }
            }).ToArray();
        }

        private static double Mz(IonType ionType, int ordinal, byte charge, string sequence, double? loss)
        {
            double? mz = PeptideFragmentMass.CalculateFragmentMz(ionType, ordinal, charge, sequence,
                new Dictionary<int, double>(), loss);
            Assert.IsNotNull(mz);
            return mz.Value;
        }

        private static void AssertIon(FragmentAnnotation annotation, IonType ionType, int ordinal, int charge)
        {
            BlibLibraryInputTest.AssertAnnotation(annotation, ionType, (byte)ordinal, (byte)charge, NeutralLossCode.None);
        }
    }
}
