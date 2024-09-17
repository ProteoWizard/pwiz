/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Summary description for RefineTest
    /// </summary>
    [TestClass]
    public class AdductTest : AbstractUnitTest
    {
        private string PENTANE = "C5H12";
        private string PENTANE_MASS_OFFSET = @"[111.11/111.111]"; // Not a meaningful value, just for testing
        private string TWO_PENTANE_MASS_OFFSET = @"[222.22/222.222]"; // Not a meaningful value, just for testing
        private readonly double massTaxol = 853.3309;

        private void TestPentaneAdduct(string adductText, string expectedFormula, int expectedCharge, HashSet<string> coverage)
        {
            var adduct = Adduct.FromStringAssumeProtonated(adductText);
            var actualFormula = IonInfo.ApplyAdductToFormula(PENTANE, adduct).ToString();
            if (!Equals(expectedFormula, actualFormula))
            {
                // ApplyAdductToFormula doesn't necessarily preserve element order, so check again as dictionary
                var dictExpected = IonInfo.ApplyAdductToFormula(expectedFormula, Adduct.EMPTY);
                var dictActual = IonInfo.ApplyAdductToFormula(PENTANE, adduct);
                if (dictExpected.Molecule.Count != dictActual.Molecule.Count || 
                    !dictExpected.Molecule.All(kvp => dictActual.Molecule.TryGetValue(kvp.Key, out var v) && v == kvp.Value))
                {
                    Assert.AreEqual(expectedFormula, actualFormula, "unexpected formula for adduct " + adduct);
                }
            }
            Assert.AreEqual(expectedCharge, adduct.AdductCharge, "unexpected charge for adduct " + adduct);
            coverage.Add(adduct.AsFormula());
        }

        private void TestMassOffsetAdduct(string adductText, string expectedFormula, int expectedCharge, HashSet<string> coverage)
        {
            var adduct = Adduct.FromStringAssumeProtonated(adductText);
            var formulaWithOffset = PENTANE + PENTANE_MASS_OFFSET;
            var actualFormula = IonInfo.ApplyAdductToFormula(formulaWithOffset, adduct).ToString();
            if (!Equals(expectedFormula, actualFormula))
            {
                // ApplyAdductToFormula doesn't necessarily preserve element order, so check again as dictionary
                var dictExpected = IonInfo.ApplyAdductToFormula(expectedFormula, Adduct.EMPTY);
                var dictActual = IonInfo.ApplyAdductToFormula(formulaWithOffset, adduct);
                if (dictExpected.Molecule.Count != dictActual.Molecule.Count ||
                    !dictExpected.Molecule.All(kvp => dictActual.Molecule.TryGetValue(kvp.Key, out var v) && v == kvp.Value) ||
                    dictExpected.AverageMassOffset != dictActual.AverageMassOffset || dictExpected.MonoMassOffset != dictActual.MonoMassOffset)
                {
                    Assert.AreEqual(expectedFormula, actualFormula, "unexpected formula for adduct " + adduct);
                }
            }
            Assert.AreEqual(expectedCharge, adduct.AdductCharge, "unexpected charge for adduct " + adduct);
            coverage.Add(adduct.AsFormula());
        }

        private void TestTaxolAdduct(string adductText, double expectedMz, int expectedCharge, HashSet<string> coverage)
        {
            // See http://fiehnlab.ucdavis.edu/staff/kind/Metabolomics/MS-Adduct-Calculator/
            var Taxol = "C47H51NO14"; // M=853.33089 (agrees with chemspider)
            var calcMass = BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(Taxol);
            Assert.AreEqual(massTaxol, calcMass, .0001);
            var adduct = Adduct.FromStringAssumeProtonated(adductText);
            Assert.AreEqual(expectedCharge, adduct.AdductCharge);
            var mz = BioMassCalc.CalculateIonMz(calcMass, adduct);
            Assert.AreEqual(expectedMz, mz, .001);
            var massWithIsotopes = adduct.MassFromMz(mz, MassType.Monoisotopic);
            var incrementalMassFromIsotopes = adduct.ApplyIsotopeLabelsToMass(TypedMass.ZERO_MONO_MASSNEUTRAL);
            Assert.AreNotEqual(massWithIsotopes, incrementalMassFromIsotopes);
            Assert.AreEqual(massTaxol, massWithIsotopes - incrementalMassFromIsotopes / adduct.GetMassMultiplier(), .0001);
            coverage.Add(adduct.AsFormula());
        }

        private void TestException(string formula, string adductText)
        {
            AssertEx.ThrowsException<InvalidDataException>(() =>
            {
                var adduct = Adduct.FromStringAssumeProtonated(adductText);
                IonInfo.ApplyAdductToFormula(formula, adduct);
            });
        }

        private string RoundtripFormulaString(string f)
        {
            ParsedMolecule.TryParseFormula(f, out var mol, out _);
            return mol.ToString();
        }

        private void TestAdductOperators()
        {
            // Test some underlying formula handling for fanciful user-supplied values
            AssertEx.AreEqual("COOOHN", RoundtripFormulaString("COOOHNS0"));
            AssertEx.AreEqual("XeC12N1", RoundtripFormulaString("XeC12N1H0"));
            AssertEx.AreEqual("XeC12N1", RoundtripFormulaString("XeC12N01H0"));
            AssertEx.AreEqual("C'3C2H9NO2O\"2S1", RoundtripFormulaString("C'3C2H9H'0NO2O\"2S001"));
            var labels = new Dictionary<string, int>(){{"C'",3},{"O\"",2}}; // Find the C'3O"2 in  C'3C2H9H'0NO2O"2S (yes, H'0 - seen in the wild - but we drop zero counts)
            AssertEx.AreEqual(labels, BioMassCalc.FindIsotopeLabelsInFormula("C'3C2H9H'0NO2O\"2S"));
            AssertEx.AreEqual(0, Adduct.SINGLY_PROTONATED.CompareTo(Adduct.FromStringAssumeProtonated("(M+H)+")));
            AssertEx.AreEqual(Adduct.SINGLY_PROTONATED, Adduct.FromStringAssumeProtonated("(M+H)+") );
            AssertEx.IsTrue(ReferenceEquals(Adduct.SINGLY_PROTONATED, Adduct.FromStringAssumeProtonated("(M+H)+"))); // Check our cacheing
            AssertEx.IsTrue(Adduct.FromStringAssumeProtonatedNonProteomic("[M-H2O+H]+").SameEffect(Adduct.FromStringAssumeProtonatedNonProteomic("(M+H)+[-H2O]")));
            Assert.IsTrue(Molecule.AreEquivalentFormulas("C10H30Si5O5H-CH4", "C9H27O5Si5"));
            AssertEx.AreEqual("C7H27O2Si4", BioMassCalc.FindFormulaIntersection(new[] { 
                Molecule.Parse("C8H305O2Si5H-CH4"),
                Molecule.Parse("C9H27O5Si4"),
                Molecule.Parse("C9H27O5Si5Na")}).ToString());
            AssertEx.AreEqual("C7H27O5Si4", BioMassCalc.FindFormulaIntersectionUnlabeled(new[] {
                Molecule.Parse("C7C'H30Si5O5H-CH4"),
                Molecule.Parse("C9H27O5Si4"),
                Molecule.Parse("C9H25H'2O5Si5Na")}).ToString());

            // There is a difference between a proteomic adduct and non proteomic, primarily in how they display
            Assert.AreEqual(Adduct.FromStringAssumeChargeOnly("M+H"), Adduct.M_PLUS_H);
            Assert.AreEqual(Adduct.FromStringAssumeProtonatedNonProteomic("1"), Adduct.M_PLUS_H);
            Assert.AreEqual(Adduct.FromStringAssumeChargeOnly("1"), Adduct.M_PLUS);
            Assert.AreEqual(Adduct.FromStringAssumeProtonatedNonProteomic("M+H"), Adduct.M_PLUS_H);
            Assert.AreEqual(Adduct.FromStringAssumeProtonated("1"), Adduct.SINGLY_PROTONATED);
            Assert.AreEqual(Adduct.FromStringAssumeProtonated("M+H"), Adduct.SINGLY_PROTONATED);
            Assert.AreEqual(Adduct.FromStringAssumeChargeOnly("M+H").AsFormula(), Adduct.SINGLY_PROTONATED.AsFormula()); // But the underlying chemistry is the same

            var mPlusSPlus = Adduct.FromStringAssumeProtonated("[M+S]+");
            AssertEx.AreEqual(1, mPlusSPlus.AdductCharge);
            var mPlusS = Adduct.FromStringAssumeProtonated("M+S");
            var mPlusSchangeCharge = mPlusS.ChangeCharge(1);
            AssertEx.IsTrue(mPlusSPlus.SameEffect(mPlusSchangeCharge));
            Assert.AreEqual(mPlusSPlus, mPlusSchangeCharge);
            Assert.AreEqual(Adduct.FromStringAssumeProtonated("M(-1.234)+2Na"), Adduct.FromStringAssumeProtonated("M(-1.234)+3Na").ChangeCharge(2));
            Assert.AreEqual(Adduct.FromStringAssumeProtonated("M1.234+2Na"), Adduct.FromStringAssumeProtonated("M1.234+3Na").ChangeCharge(2));
            Assert.AreEqual(Adduct.FromStringAssumeProtonated("M2Cl37-2Na"), Adduct.FromStringAssumeProtonated("M2Cl37+3Na").ChangeCharge(-2));
            Assert.AreEqual(Adduct.FromStringAssumeProtonated("M1.234-2Na"), Adduct.FromStringAssumeProtonated("M1.234+3Na").ChangeCharge(-2));
            Assert.AreEqual(Adduct.FromStringAssumeProtonated("M(-1.234)-2Na"), Adduct.FromStringAssumeProtonated("M(-1.234)+3Na").ChangeCharge(-2));

            Assert.IsFalse(Adduct.M_PLUS_H.IsProteomic);
            Assert.IsTrue(Adduct.M_PLUS_H.IsProtonated);
            Assert.IsTrue(Adduct.SINGLY_PROTONATED.IsProteomic);
            Assert.IsTrue(Adduct.SINGLY_PROTONATED.IsProtonated);
            Assert.IsFalse(Adduct.SINGLY_PROTONATED.IsEmpty);
            Assert.IsFalse(Adduct.EMPTY.IsProteomic);
            Assert.IsTrue(Adduct.EMPTY.IsEmpty);

            // Honor explicit charges 
            var mCh3Cl = "[M+2CH3+Cl]";
            var adductCH3 = Adduct.FromString(mCh3Cl, Adduct.ADDUCT_TYPE.non_proteomic, null);
            Assert.AreEqual(1, adductCH3.AdductCharge); // CH3 is +1, Cl is -1
            Assert.AreEqual(mCh3Cl, adductCH3.ToString());

            adductCH3 = Adduct.FromString(mCh3Cl+"+", Adduct.ADDUCT_TYPE.non_proteomic, null); // Declare correct charge
            Assert.AreEqual(1, adductCH3.AdductCharge); // CH3 is +1, Cl is -1
            Assert.AreEqual(mCh3Cl, adductCH3.ToString()); // We dropped the charge declaration since it's redundant

            mCh3Cl += "++";  // Wrong charge, but since this is a non-canonical adduct, we will honor it
            adductCH3 = Adduct.FromString(mCh3Cl, Adduct.ADDUCT_TYPE.non_proteomic, null);
            Assert.AreEqual(2, adductCH3.AdductCharge);
            Assert.AreEqual(mCh3Cl, adductCH3.ToString()); // We kept the charge declaration since it's weird

            AssertEx.ThrowsException<InvalidDataException>(() => Adduct.FromStringAssumeProtonated("[M+2H]-")); // Try to declare wrong charge on common adduct

            // Exercise the ability to work with masses and isotope labels
            Assert.IsTrue(ReferenceEquals(Adduct.SINGLY_PROTONATED, Adduct.SINGLY_PROTONATED.Unlabeled));
            var nolabel = Adduct.FromStringAssumeProtonated("M-2Na");
            var label = Adduct.FromStringAssumeProtonated("M2Cl37-2Na");
            Assert.AreEqual(nolabel, label.Unlabeled);
            Assert.IsTrue(ReferenceEquals(nolabel, nolabel.Unlabeled));
            Assert.IsFalse(nolabel.MassFromMz(300.0, MassType.Monoisotopic).IsHeavy());
            Assert.IsFalse(label.MassFromMz(300.0, MassType.Monoisotopic).IsHeavy());
            Assert.IsTrue(label.MassFromMz(300.0, MassType.MonoisotopicHeavy).IsHeavy());
            Assert.IsTrue(label.MassFromMz(300.0, MassType.Monoisotopic).IsMonoIsotopic());
            Assert.IsFalse(nolabel.MassFromMz(300.0, MassType.Average).IsHeavy());
            Assert.IsFalse(label.MassFromMz(300.0, MassType.Average).IsHeavy());
            Assert.IsTrue(label.MassFromMz(300.0, MassType.AverageHeavy).IsHeavy());
            Assert.IsTrue(label.MassFromMz(300.0, MassType.Average).IsAverage());
            var massHeavy = label.ApplyToMass(new TypedMass(300, MassType.MonoisotopicHeavy)); // Will not have isotope effect added in mz calc, as it's already heavy
            var massLight = label.ApplyToMass(new TypedMass(300, MassType.Monoisotopic)); // Will have isotope effect added in mz calc
            Assert.AreNotEqual(massHeavy, massLight);
            Assert.AreNotEqual(label.MzFromNeutralMass(massHeavy), label.MzFromNeutralMass(massLight));

            Assert.IsTrue(Adduct.PossibleAdductDescriptionStart("["));
            Assert.IsTrue(Adduct.PossibleAdductDescriptionStart("M"));
            Assert.IsTrue(Adduct.PossibleAdductDescriptionStart("[2M+CH3COO]"));
            Assert.AreEqual(Adduct.FromStringAssumeProtonated("M+2CH3COO"), Adduct.FromStringAssumeProtonated("M+CH3COO").ChangeCharge(-2));
            Assert.AreEqual(Adduct.FromStringAssumeProtonated("M-2CH3COO"), Adduct.FromStringAssumeProtonated("M+CH3COO").ChangeCharge(2));
            Assert.AreEqual(Adduct.FromStringAssumeProtonated("M-2Na"), Adduct.FromStringAssumeProtonated("M+Na").ChangeCharge(-2));
            Assert.AreEqual(Adduct.FromStringAssumeProtonated("M+Na"), Adduct.FromStringAssumeProtonated("M+Na").ChangeCharge(1));
            Assert.AreEqual(Adduct.FromStringAssumeProtonated("M+2Na"), Adduct.FromStringAssumeProtonated("M+Na").ChangeCharge(2));
            Assert.AreEqual(Adduct.FromStringAssumeProtonated("M+2Na"), Adduct.FromStringAssumeProtonated("M+3Na").ChangeCharge(2));
            Assert.AreEqual(Adduct.FromStringAssumeProtonated("M2Cl37+2Na"), Adduct.FromStringAssumeProtonated("M2Cl37+3Na").ChangeCharge(2));
            AssertEx.ThrowsException<InvalidDataException>(()=>Adduct.FromStringAssumeProtonated("M+2Na-H").ChangeCharge(2)); // Too complex to adjust formula

            AssertEx.ThrowsException<InvalidDataException>(() => Adduct.FromStringAssumeProtonatedNonProteomic("[M2")); // Seen in the wild, wasn't handled well

            Assert.AreEqual(Adduct.FromStringAssumeProtonated("M++++").AdductCharge,Adduct.FromChargeNoMass(4).AdductCharge);
            Assert.AreEqual(Adduct.FromStringAssumeProtonated("M+4"), Adduct.FromChargeNoMass(4));
            Assert.AreEqual(Adduct.FromStringAssumeProtonated("M--").AdductCharge, Adduct.FromChargeNoMass(-2).AdductCharge);
            Assert.AreEqual(Adduct.FromStringAssumeProtonated("M-2"), Adduct.FromChargeNoMass(-2));
            Assert.IsTrue(ReferenceEquals(Adduct.FromStringAssumeChargeOnly("M-"), Adduct.FromChargeNoMass(-1))); // Both should return Adduct.M_MINUS
            Assert.IsTrue(ReferenceEquals(Adduct.FromStringAssumeChargeOnly("M+"), Adduct.FromChargeNoMass(1))); // Both should return Adduct.M_PLUS
            Assert.IsTrue(ReferenceEquals(Adduct.FromStringAssumeChargeOnly("[M+]"), Adduct.FromChargeNoMass(1))); // Both should return Adduct.M_PLUS
            Assert.IsTrue(ReferenceEquals(Adduct.FromStringAssumeChargeOnly("M-H"), Adduct.M_MINUS_H));
            Assert.IsTrue(ReferenceEquals(Adduct.FromStringAssumeChargeOnly("M+H"), Adduct.M_PLUS_H)); 

            var a = Adduct.FromChargeProtonated(-1);
            var aa = Adduct.FromStringAssumeProtonated("M+CH3COO");
            var b = Adduct.FromChargeProtonated(2);
            var bb = Adduct.FromChargeProtonated(2);
            var bbb = Adduct.FromChargeProtonated(2);
            var bbbb = Adduct.FromChargeProtonated(2);
            var c = Adduct.FromChargeProtonated(3);
            var cc = Adduct.FromStringAssumeChargeOnly("M+3H");
            var ccc = Adduct.FromStringAssumeChargeOnly("[M+3H]");

            Assert.AreEqual(a.AdductCharge,aa.AdductCharge);
            Assert.IsTrue(b == bb);
            Assert.IsTrue(b == bbb);
            Assert.IsTrue(ReferenceEquals(bbb, bbbb));
            Assert.IsTrue(c.AdductCharge == cc.AdductCharge);
            Assert.IsFalse(c == cc);
            Assert.IsTrue(c != cc);
            Assert.IsTrue(cc == ccc);
            Assert.IsTrue(a < aa);
            Assert.IsTrue(a < b);
            Assert.IsTrue(b > a);
            Assert.IsTrue(b != a);

            var sorted = new List<Adduct> { a, aa, b, bb, bbb, bbbb, c, cc, ccc };
            var unsorted = new List<Adduct> { bb, aa, ccc, b, c, bbb, a, bbbb, cc };
            Assert.IsFalse(sorted.SequenceEqual(unsorted));
            unsorted.Sort();
            Assert.IsTrue(sorted.SequenceEqual(unsorted));

            var adduct0 = Adduct.FromStringAssumeProtonatedNonProteomic("[M-H]");
            var adduct1 = Adduct.FromStringAssumeProtonatedNonProteomic("[MC13-H]");
            var adduct2 = Adduct.FromStringAssumeProtonatedNonProteomic("[M2C13-H]");
            var adduct3 = Adduct.FromStringAssumeProtonatedNonProteomic("[M3C13-H]");
            var adduct4 = Adduct.FromStringAssumeProtonatedNonProteomic("[M+Na]");
            var adduct5 = Adduct.FromStringAssumeProtonatedNonProteomic("[M-2H]");
            sorted = new List<Adduct> { adduct0, adduct1, adduct2, adduct3, adduct5, adduct4 };
            unsorted = new List<Adduct> { adduct3, adduct5, adduct4, adduct2, adduct1, adduct0 };
            unsorted.Sort();
            Assert.IsTrue(sorted.SequenceEqual(unsorted));

            var ints = new AdductMap<int>();
            Assert.AreEqual(0, ints[a]);
            ints[a] = 7;
            Assert.AreEqual(7, ints[a]);

            var adducts = new AdductMap<Adduct>();
            Assert.AreEqual(null, adducts[a]);
            adducts[a] = b;
            Assert.AreEqual(b, adducts[a]);
            adducts[a] = c;
            Assert.AreEqual(c, adducts[a]);

            var d = Adduct.FromStringAssumeProtonated("[2M+3H]");
            var dd = Adduct.FromStringAssumeProtonated("[M+3H]");
            var ddd = Adduct.FromStringAssumeProtonated("[M-Na]");
            Assert.IsTrue(d.ChangeMassMultiplier(1).SameEffect(dd));
            Assert.IsTrue(dd.ChangeMassMultiplier(2).SameEffect(d));
            Assert.AreEqual(dd.ChangeIonFormula("-Na"), ddd);
            Assert.AreEqual(d.ChangeMassMultiplier(1).ChangeIonFormula("-Na"), ddd);

            CheckLabel(BioMassCalc.Cl37);
            CheckLabel(BioMassCalc.Br81);
            CheckLabel(BioMassCalc.S33);
            CheckLabel(BioMassCalc.S34);
            CheckLabel(BioMassCalc.P32);
            CheckLabel(BioMassCalc.C14);
            CheckLabel(BioMassCalc.O17);
            CheckLabel(BioMassCalc.O18);

            var tips = Adduct.Tips;
            foreach (var nickname in Adduct.DICT_ADDUCT_NICKNAMES)
            {
                Assert.IsTrue(tips.Contains(nickname.Key));
            }
            foreach (var nickname in Adduct.DICT_ADDUCT_ISOTOPE_NICKNAMES)
            {
                Assert.IsTrue(tips.Contains(nickname.Key));
            }
        
        }

        private static void CheckLabel(string label)
        {
            string unlabel = label.Substring(0, label.Length - 1);
            var adductLabel = Adduct.DICT_ADDUCT_ISOTOPE_NICKNAMES.FirstOrDefault(x => x.Value == label).Key;
            var labeled = Adduct.FromStringAssumeProtonated("[2M3C13-Na]");
            var unlabeled = Adduct.FromStringAssumeProtonated("[2M-Na]");
            var relabeled = Adduct.FromStringAssumeProtonated("[2M3Cl374H2-Na]".Replace("Cl37", adductLabel));
            var massNeutral = 5.6;
            Assert.AreEqual(
                2 * (massNeutral + 3 * (BioMassCalc.MONOISOTOPIC.GetMass("C'") - BioMassCalc.MONOISOTOPIC.GetMass("C"))) -
                BioMassCalc.MONOISOTOPIC.GetMass("Na"),
                labeled.ApplyToMass(new TypedMass(massNeutral, MassType.Monoisotopic)));
            Assert.AreEqual(unlabeled, labeled.ChangeIsotopeLabels(null));
            var labels = new Dictionary<string, int> {{label, 3}, {"H2", 4}};
            Assert.AreEqual(relabeled, labeled.ChangeIsotopeLabels(labels));
            var labelsToo = new Dictionary<string, int> {{label, 3}, {"H'", 4}};
            Assert.AreEqual(relabeled, unlabeled.ChangeIsotopeLabels(labelsToo));
            var labelsAlso = new Dictionary<string, int> {{adductLabel, 3}, {"H'", 4}}; // Mixed
            Assert.AreEqual(relabeled, unlabeled.ChangeIsotopeLabels(labelsAlso));
            Assert.AreNotEqual(labeled, unlabeled.ChangeIsotopeLabels(labelsAlso));

            // Check Deuterium and Tritium handling
            Assert.AreEqual(BioMassCalc.MONOISOTOPIC.GetMass("Cl2Cl'3H5H'4N12"), BioMassCalc.MONOISOTOPIC.GetMass("Cl2Cl'3H5D4N12"));
            Assert.AreEqual(BioMassCalc.MONOISOTOPIC.GetMass("Cl2Cl'3H5H\"4N12"), BioMassCalc.MONOISOTOPIC.GetMass("Cl2Cl'3H5T4N12"));
            Assert.AreEqual(ParsedMolecule.Create("Cl2Cl'3H5H'4N12".Replace("Cl'", label).Replace("Cl", unlabel)),
                relabeled.ApplyIsotopeLabelsToFormula("Cl5H9N12".Replace("Cl", unlabel))); // Replaces three of five Cl and four of nine H
            var m100 = new TypedMass(100, MassType.Monoisotopic);
            var mdiff = 2 * (3 * (BioMassCalc.MONOISOTOPIC.GetMass(label) -
                                  BioMassCalc.MONOISOTOPIC.GetMass(unlabel)) +
                             4 * (BioMassCalc.MONOISOTOPIC.GetMass(BioMassCalc.H2) -
                                  BioMassCalc.MONOISOTOPIC.GetMass(BioMassCalc.H)));
            Assert.AreEqual(m100 + mdiff, relabeled.ApplyIsotopeLabelsToMass(m100),
                .005); // Expect increase of 2*(3(Cl37-Cl)+4(H2-H))
            var isotopeAsMass = Adduct.FromStringAssumeProtonated("[2M1.23-Na]");
            Assert.AreEqual(102.46, isotopeAsMass.ApplyIsotopeLabelsToMass(m100), .005); // Expect increase of 2*1.23
        }

        private void TestMassOnly(IEnumerable<string>[] adductLists)
        {
            var formula = @"[456.78]"; // Mass-only molecule description
            var molecule = ParsedMolecule.Create(formula);
            var massMolecule = molecule.MonoMassOffset;
            AssertEx.AreEqual(456.78,massMolecule);
            foreach (var adductList in adductLists)
            {
                foreach (var adductStr in adductList)
                {
                    var adduct = Adduct.FromString(adductStr, Adduct.ADDUCT_TYPE.proteomic, null);
                    var ion = IonInfo.ApplyAdductToFormula(formula, adduct);
                    var massAdduct = adduct.MonoMassAdduct + adduct.IsotopesIncrementalMonoMass + massMolecule * (adduct.GetMassMultiplier()-1);
                    var massIon = BioMassCalc.MONOISOTOPIC.CalculateMass(ion);
                    if (!adduct.IsChargeOnly)
                    {
                        AssertEx.AreNotEqual(massIon, massMolecule, "adduct has no effect?");
                    }
                    AssertEx.AreEqual(massIon, massMolecule + massAdduct, .0001, $"ion {ion} mass {massIon} vs mol {molecule} mass + adduct {adduct} mass ({massMolecule}+{massAdduct}");
                }
            }
        }

        [TestMethod]
        public void AdductParserTest()
        {
            var AllSupportedAdducts = new[] {
                Adduct.DEFACTO_STANDARD_ADDUCTS,
                Adduct.COMMON_CHARGEONLY_ADDUCTS,
                Adduct.COMMON_SMALL_MOL_ADDUCTS.Select(a => a.AdductFormula),
                Adduct.COMMON_PROTONATED_ADDUCTS.Select(a => a.AdductFormula),
            };

            TestMassOnly(AllSupportedAdducts);

            TestAdductOperators();

            var coverage = new HashSet<string>();
            TestPentaneAdducts(coverage);

            TestMassOffsetAdducts(coverage);

            TestTaxolAdducts(coverage);

            // Using example adducts from
            // https://gnps.ucsd.edu/ProteoSAFe/gnpslibrary.jsp?library=GNPS-LIBRARY#%7B%22Library_Class_input%22%3A%221%7C%7C2%7C%7C3%7C%7CEXACT%22%7D
            var Hectochlorin = "C27H34Cl2N2O9S2";
            var massHectochlorin = 664.108276; // http://www.chemspider.com/Chemical-Structure.552449.html?rid=3a7c08af-0886-4e82-9e4f-5211b8efb373
            var adduct = Adduct.FromStringAssumeProtonated("M+H");
            var mol = IonInfo.ApplyAdductToFormula(Hectochlorin, adduct);
            var mass = BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(mol.ToString(), out _);
            Assert.AreEqual(massHectochlorin + BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula("H"), mass, 0.00001);
            var mz = BioMassCalc.CalculateIonMz(BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(Hectochlorin, out _), adduct);
            Assert.AreEqual(665.11555415, mz, .000001);  // GNPS says 665.0 for Hectochlorin M+H
            mol = IonInfo.ApplyAdductToFormula(Hectochlorin, Adduct.FromStringAssumeProtonated("MCl37+H"));
            Assert.AreEqual("C27H35ClCl'N2O9S2", mol.ToString());
            mass = BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(mol.ToString(), out _);
            Assert.AreEqual(667.11315, mass, .00001);
            mol = IonInfo.ApplyAdductToFormula(Hectochlorin, Adduct.FromStringAssumeProtonated("M2Cl37+H"));
            Assert.AreEqual("C27H35Cl'2N2O9S2", mol.ToString());

            // Test ability to describe isotope label by mass only
            var heavy = Adduct.FromStringAssumeProtonated("2M1.2345+H");
            mz = BioMassCalc.CalculateIonMz(new TypedMass(massHectochlorin, MassType.Monoisotopic), heavy);
            heavy = Adduct.FromStringAssumeProtonated("2M1.2345");
            mz = BioMassCalc.CalculateIonMass(new TypedMass(massHectochlorin, MassType.Monoisotopic), heavy);
            Assert.AreEqual(2 * (massHectochlorin + 1.23456), mz, .001);
            heavy = Adduct.FromStringAssumeProtonated("M1.2345");
            mz = BioMassCalc.CalculateIonMass(new TypedMass(massHectochlorin, MassType.Monoisotopic), heavy);
            Assert.AreEqual(massHectochlorin + 1.23456, mz, .001);
            heavy = Adduct.FromStringAssumeProtonated("2M(-1.2345)+H");
            mz = BioMassCalc.CalculateIonMz(new TypedMass(massHectochlorin, MassType.Monoisotopic), heavy);
            Assert.AreEqual((2 * (massHectochlorin - 1.23456) + BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula("H")), mz, .001);
            heavy = Adduct.FromStringAssumeProtonated("2M(-1.2345)");
            mz = BioMassCalc.CalculateIonMass(new TypedMass(massHectochlorin, MassType.Monoisotopic), heavy);
            Assert.AreEqual(2 * (massHectochlorin - 1.23456), mz, .001);
            heavy = Adduct.FromStringAssumeProtonated("2M(1.2345)+H");
            mz = BioMassCalc.CalculateIonMz(new TypedMass(massHectochlorin, MassType.Monoisotopic), heavy);
            Assert.AreEqual((2 * (massHectochlorin + 1.23456) + BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula("H")), mz, .001);
            heavy = Adduct.FromStringAssumeProtonated("2M(1.2345)");
            mz = BioMassCalc.CalculateIonMass(new TypedMass(massHectochlorin, MassType.Monoisotopic), heavy);
            Assert.AreEqual(2 * (massHectochlorin + 1.23456), mz, .001);

            TestException(PENTANE, "zM+2H"); // That "z" doesn't make any sense as a mass multiplier (must be a positive integer)
            TestException(PENTANE, "-2M+2H"); // That "-2" doesn't make any sense as a mass multiplier (must be a positive integer)
            TestException("", "+M"); // Meaningless, used to cause an exception in our parser
            TestException(Hectochlorin, "M3Cl37+H"); // Trying to label more chlorines than exist in the molecule
            TestException(Hectochlorin, "M-3Cl+H"); // Trying to remove more chlorines than exist in the molecule
            TestException(PENTANE, "M+foo+H"); // Unknown adduct
            TestException(PENTANE, "M2Cl37H+H"); // nonsense label ("2Cl37H2" would make sense, but regular H doesn't belong)
            TestException(PENTANE, "M+2H+"); // Trailing sign - we now understand this as a charge state declaration, but this one doesn't match described charge
            TestException(PENTANE, "[M-2H]3-"); // Declared charge doesn't match described charge
            TestException(PENTANE, "[M-]3-"); // Declared charge doesn't match described charge
            TestException(PENTANE, "[M+]-"); // Declared charge doesn't match described charge
            TestException(PENTANE, "[M+2]-"); // Declared charge doesn't match described charge
            TestException(PENTANE, "[M+2]+3"); // Declared charge doesn't match described charge

            // Test label stripping
            Assert.AreEqual("C5H9NO2S", (new IonInfo("C5H9H'3NO2S[M-3H]")).UnlabeledFormula.ToString());

            // Peptide representations
            Assert.AreEqual("C40H65N11O16", (new SequenceMassCalc(MassType.Average)).GetNeutralFormula("PEPTIDER", null).ToString());

            // Figuring out adducts from old style skyline doc ion molecules and ion precursors
            var adductDiff = Adduct.FromFormulaDiff("C6H27NO2Si2C'5", "C'5H11NO2", 3);
            Assert.AreEqual("[M+C6H16Si2]3+", adductDiff.AdductFormula);
            Assert.AreEqual(3, adductDiff.AdductCharge);
            Assert.AreEqual(Adduct.FromString("[M+C6H16Si2]3+", Adduct.ADDUCT_TYPE.non_proteomic, null), adductDiff);
            adductDiff = Adduct.FromFormulaDiff("C6H27NO2", "C6H27NO2", 3);
            Assert.AreEqual("[M+3]", adductDiff.AdductFormula);
            Assert.AreEqual(3, adductDiff.AdductCharge);
            adductDiff = Adduct.ProtonatedFromFormulaDiff("C6H27NO2Si2C'5", "C'5H11NO2", 3);
            var expectedFromProtonatedDiff = "[M+C6H13Si2+3H]";
            Assert.AreEqual(expectedFromProtonatedDiff, adductDiff.AdductFormula);
            Assert.AreEqual(3, adductDiff.AdductCharge);
            Assert.AreEqual(Adduct.FromString(expectedFromProtonatedDiff, Adduct.ADDUCT_TYPE.non_proteomic, null), adductDiff);
            adductDiff = Adduct.ProtonatedFromFormulaDiff("C6H27NO2", "C6H27NO2", 3);
            Assert.AreEqual("[M+3H]", adductDiff.AdductFormula);
            Assert.AreEqual(3, adductDiff.AdductCharge);

            // Implied positive mode
            TestPentaneAdduct("MH", "C5H13", 1, coverage); // implied pos mode seems to be fairly common in the wild
            TestPentaneAdduct("MH+", "C5H13", 1, coverage); // implied pos mode seems to be fairly common in the wild
            TestPentaneAdduct("MNH4", "C5H16N", 1, coverage); // implied pos mode seems to be fairly common in the wild
            TestPentaneAdduct("MNH4+", "C5H16N", 1, coverage); // implied pos mode seems to be fairly common in the wild
            TestPentaneAdduct("2MNH4+", "C10H28N", 1, coverage); // implied pos mode seems to be fairly common in the wild

            // Methyl
            TestPentaneAdduct("[M+2CH3]", "C7H18", 2, coverage); // Methyl is 2+

            // Explict charge states within the adduct
            TestPentaneAdduct("[M+S+]", "C5H12S", 1, coverage); // We're trusting the user to declare charge
            TestPentaneAdduct("[3M+S+]", "C15H36S", 1, coverage); // We're trusting the user to declare charge
            TestPentaneAdduct("[M+S++]", "C5H12S", 2, coverage); // We're trusting the user to declare charge
            TestPentaneAdduct("[MS+]", "C5H12S", 1, coverage); // We're trusting the user to declare charge
            TestPentaneAdduct("[MS++]", "C5H12S", 2, coverage); // We're trusting the user to declare charge
            TestPentaneAdduct("[M+S+2]", "C5H12S", 2, coverage); // We're trusting the user to declare charge
            TestPentaneAdduct("[M+S]2+", "C5H12S", 2, coverage); // We're trusting the user to declare charge
            TestPentaneAdduct("[M+S-]", "C5H12S", -1, coverage); // We're trusting the user to declare charge
            TestPentaneAdduct("[M+S--]", "C5H12S", -2, coverage); // We're trusting the user to declare charge
            TestPentaneAdduct("[M-3H-3]", "C5H9", -3, coverage); // We're trusting the user to declare charge
            TestPentaneAdduct("[M+S-2]", "C5H12S", -2, coverage); // We're trusting the user to declare charge
            TestPentaneAdduct("[M+S]2-", "C5H12S", -2, coverage); // We're trusting the user to declare charge

            // Did we test all the adducts we claim to support?
            foreach (var adducts in AllSupportedAdducts)
            {
                foreach (var adductText in adducts)
                {
                    if (!coverage.Contains(adductText))
                    {
                        Assert.Fail("Need to add a test for adduct {0}", adductText);
                    }
                }
            }
        }

        private void TestTaxolAdducts(HashSet<string> coverage)
        {
            // See http://fiehnlab.ucdavis.edu/staff/kind/Metabolomics/MS-Adduct-Calculator/
            // There you will find an excel spreadsheet from which I pulled these numbers, which as it turns out has several errors in it.
            // There is also a table in the web page itself that contains the same values and some unmarked corrections.
            // Sadly that faulty spreadsheet is copied all over the internet.  I've let the author know what we found. - bspratt
            TestTaxolAdduct("M+3H", 285.450928, 3, coverage);
            TestTaxolAdduct("M+2H+Na", 292.778220, 3, coverage);
            TestTaxolAdduct("M+H+2Na", 300.105557, 3, coverage); // Spreadsheet and table both say 300.209820, but also says adduct "mass" = 15.766190, I get 15.6618987 using their H and Na masses (and this is clearly m/z, not mass)
            TestTaxolAdduct("M+3Na", 307.432848, 3, coverage);
            TestTaxolAdduct("M+2H", 427.672721, 2, coverage);
            TestTaxolAdduct("M+H+NH4", 436.185995, 2, coverage);
            TestTaxolAdduct("M+H+Na", 438.663692, 2, coverage);
            TestTaxolAdduct("M+H+K", 446.650662, 2, coverage);
            TestTaxolAdduct("M+ACN+2H", 448.185995, 2, coverage);
            TestTaxolAdduct("M+2Na", 449.654663, 2, coverage);
            TestTaxolAdduct("M+2ACN+2H", 468.699268, 2, coverage);
            TestTaxolAdduct("M+3ACN+2H", 489.212542, 2, coverage);
            TestTaxolAdduct("M+H", 854.338166, 1, coverage);
            TestTaxolAdduct("M+NH4", 871.364713, 1, coverage);
            TestTaxolAdduct("M+Na", 876.320108, 1, coverage);
            TestTaxolAdduct("M+CH3OH+H", 886.364379, 1, coverage);
            TestTaxolAdduct("M+K", 892.294048, 1, coverage);
            TestTaxolAdduct("M+ACN+H", 895.364713, 1, coverage);
            TestTaxolAdduct("M+2Na-H", 898.302050, 1, coverage);
            TestTaxolAdduct("M+IsoProp+H", 914.396230, 1, coverage);
            TestTaxolAdduct("M+ACN+Na", 917.346655, 1, coverage);
            TestTaxolAdduct("M+2K-H", 930.249930, 1, coverage);  // Spreadsheet and table disagree - spreadsheet says "M+2K+H" but that's 3+, not 1+, and this fits the mz value
            TestTaxolAdduct("M+DMSO+H", 932.352110, 1, coverage);
            TestTaxolAdduct("M+2ACN+H", 936.391260, 1, coverage);
            TestTaxolAdduct("M+IsoProp+Na+H", 468.692724, 2, coverage); // Spreadsheet and table both say mz=937.386000 z=1 (does Isoprop interact somehow to eliminate half the ionization?)
            TestTaxolAdduct("2M+H", 1707.669056, 1, coverage);
            TestTaxolAdduct("2M+NH4", 1724.695603, 1, coverage);
            TestTaxolAdduct("2M+Na", 1729.650998, 1, coverage);
            TestTaxolAdduct("2M+3H2O+2H", 881.354, 2, coverage); // Does not appear in table.  Charge agrees but spreadsheet says mz= 1734.684900
            TestTaxolAdduct("2M+K", 1745.624938, 1, coverage);
            TestTaxolAdduct("2M+ACN+H", 1748.695603, 1, coverage);
            TestTaxolAdduct("2M+ACN+Na", 1770.677545, 1, coverage);
            TestTaxolAdduct("M-3H", 283.436354, -3, coverage);
            TestTaxolAdduct("M-2H", 425.658169, -2, coverage);
            TestTaxolAdduct("M-H2O-H", 834.312500, -1, coverage);
            TestTaxolAdduct("M+-H2O-H", 834.312500, -1, coverage); // Tolerate empty atom description ("+-")
            TestTaxolAdduct("M-H", 852.323614, -1, coverage);
            TestTaxolAdduct("M+Na-2H", 874.305556, -1, coverage);
            TestTaxolAdduct("M+Cl", 888.300292, -1, coverage);
            TestTaxolAdduct("M+K-2H", 890.279496, -1, coverage);
            TestTaxolAdduct("M+FA-H", 898.329091, -1, coverage);
            TestTaxolAdduct("M+Hac-H", 912.344741, -1, coverage);
            TestTaxolAdduct("M+Br", 932.249775, -1, coverage);
            TestTaxolAdduct("MT+TFA-H", 968.324767, -1, coverage); // Tritium label + TFA
            TestTaxolAdduct("M+TFA-H", 966.316476, -1, coverage);
            TestTaxolAdduct("2M-H", 1705.654504, -1, coverage);
            TestTaxolAdduct("2M+FA-H", 1751.659981, -1, coverage);
            TestTaxolAdduct("2M+Hac-H", 1765.675631, -1, coverage);
            TestTaxolAdduct("3M-H", 2558.985394, -1, coverage); // Spreadsheet and table give mz as 2560.999946 -but also gives adduct "mass" as 1.007276, should be -1.007276

            // A couple more simple ones we support with statics
            TestTaxolAdduct("M+4H", 214.3400149, 4, coverage);
            TestTaxolAdduct("M+5H", 171.6734671, 5, coverage);

            // And a few of our own to exercise the interaction of multiplier and isotope
            var dC13 = BioMassCalc.MONOISOTOPIC.GetMass(BioMassCalc.C13) - BioMassCalc.MONOISOTOPIC.GetMass(BioMassCalc.C);
            TestTaxolAdduct("M2C13+3H", 285.450928 + (2 * dC13) / 3.0, 3, coverage);
            TestTaxolAdduct("M2C13+2H+Na", 292.778220 + (2 * dC13) / 3.0, 3, coverage);
            TestTaxolAdduct("2M2C13+3H", 285.450906 + (massTaxol + 4 * dC13) / 3.0, 3, coverage);
            TestTaxolAdduct("2M2C13+2H+Na", 292.778220 + (massTaxol + 4 * dC13) / 3.0, 3, coverage);
            var dC14 = BioMassCalc.MONOISOTOPIC.GetMass(BioMassCalc.C14) - BioMassCalc.MONOISOTOPIC.GetMass(BioMassCalc.C);
            TestTaxolAdduct("M2C14+3H", 285.450928 + (2 * dC14) / 3.0, 3, coverage);
            TestTaxolAdduct("M2C14+2H+Na", 292.778220 + (2 * dC14) / 3.0, 3, coverage);
            TestTaxolAdduct("2M2C14+3H", 285.450906 + (massTaxol + 4 * dC14) / 3.0, 3, coverage);
            TestTaxolAdduct("2M2C14+2H+Na", 292.778220 + (massTaxol + 4 * dC14) / 3.0, 3, coverage);
        }

        private void TestPentaneAdducts(HashSet<string> coverage)
        {
            TestPentaneAdduct("[M+2NH4]", "C5H20N2", 2, coverage); // multiple of a group
            TestPentaneAdduct("[M+2(NH4)]", "C5H20N2", 2, coverage); // multiple of a group in parenthesis
            TestPentaneAdduct("[M+2H]", "C5H14", 2, coverage);
            TestPentaneAdduct("[M+2Cu65+2H]", "C5Cu'2H14", 2, coverage); // With heavy copper as in MaConDa Contaminants DB https://www.maconda.bham.ac.uk/downloads/MaConDa__v1_0__csv.zip 
            TestPentaneAdduct("[M2C13+2H]", "C3C'2H14", 2, coverage); // Labeled
            TestPentaneAdduct("[2M2C13+2H]", "C6C'4H26", 2, coverage); // Labeled dimer
            TestPentaneAdduct("[2M2C14+2H]", "C6C\"4H26", 2, coverage); // Labeled dimer
            TestPentaneAdduct("[M2C13]", "C3C'2H12", 0, coverage); // Labeled no charge
            TestPentaneAdduct("[2M2C13]", "C6C'4H24", 0, coverage); // Labeled, dimer, no charge
            TestPentaneAdduct("[2M]", "C10H24", 0, coverage); // dimer no charge
            TestPentaneAdduct("[2M2C13+3]", "C6C'4H24", 3, coverage); // Labeled, dimer, charge only
            TestPentaneAdduct("[2M2C13]+3", "C6C'4H24", 3, coverage); // Labeled, dimer, charge only
            TestPentaneAdduct("[2M2C13+++]", "C6C'4H24", 3, coverage); // Labeled, dimer, charge only
            TestPentaneAdduct("[2M2C13]+++", "C6C'4H24", 3, coverage); // Labeled, dimer, charge only
            TestPentaneAdduct("[2M+3]", "C10H24", 3, coverage); // dimer charge only
            TestPentaneAdduct("[2M]+3", "C10H24", 3, coverage); // dimer charge only
            TestPentaneAdduct("[2M+++]", "C10H24", 3, coverage); // dimer charge only
            TestPentaneAdduct("[2M]+++", "C10H24", 3, coverage); // dimer charge only
            TestPentaneAdduct("[2M2C13-3]", "C6C'4H24", -3, coverage); // Labeled, dimer, charge only
            TestPentaneAdduct("[2M2C13---]", "C6C'4H24", -3, coverage); // Labeled, dimer, charge only
            TestPentaneAdduct("[2M-3]", "C10H24", -3, coverage); // dimer charge only
            TestPentaneAdduct("[2M---]", "C10H24", -3, coverage); // dimer charge only
            TestPentaneAdduct("[2M2C133H2+2H]", "C6C'4H20H'6", 2, coverage); // Labeled with some complexity, multiplied
            TestPentaneAdduct("M+H", "C5H13", 1, coverage);
            TestPentaneAdduct("M+", PENTANE, 1, coverage);
            TestPentaneAdduct("M+2", PENTANE, 2, coverage);
            TestPentaneAdduct("M+3", PENTANE, 3, coverage);
            TestPentaneAdduct("M-", PENTANE, -1, coverage);
            TestPentaneAdduct("M-2", PENTANE, -2, coverage);
            TestPentaneAdduct("M-3", PENTANE, -3, coverage);
            TestPentaneAdduct("M++", PENTANE, 2, coverage);
            TestPentaneAdduct("M--", PENTANE, -2, coverage);
            TestPentaneAdduct("M", PENTANE, 0, coverage); // Trivial non-adduct
            TestPentaneAdduct("M+CH3COO", "C7H15O2", -1, coverage); // From XCMS
            TestPentaneAdduct("[M+H]1+", "C5H13", 1, coverage);
            TestPentaneAdduct("[M-H]1-", "C5H11", -1, coverage);
            TestPentaneAdduct("[M-2H]", "C5H10", -2, coverage);
            TestPentaneAdduct("[M-2H]2-", "C5H10", -2, coverage);
            TestPentaneAdduct("[M+2H]++", "C5H14", 2, coverage);
            TestPentaneAdduct("[MH2+2H]++", "C5H13H'", 2, coverage); // Isotope
            TestPentaneAdduct("[MH3+2H]++", "C5H13H\"", 2, coverage);  // Isotope
            TestPentaneAdduct("[MD+2H]++", "C5H13H'", 2, coverage);  // Isotope
            TestPentaneAdduct("[MD+DMSO+2H]++", "C7H19H'OS", 2, coverage); // Check handling of Deuterium and DMSO together
            TestPentaneAdduct("[MT+DMSO+2H]++", "C7H19H\"OS", 2, coverage); // Check handling of Tritium
            TestPentaneAdduct("[M+DMSO+2H]++", "C7H20OS", 2, coverage);
            TestPentaneAdduct("[M+DMSO+2H]2+", "C7H20OS", 2, coverage);
            TestPentaneAdduct("[M+MeOH-H]", "C6H15O", -1, coverage); // Methanol "CH3OH"
            TestPentaneAdduct("[M+MeOX-H]", "C6H14N", -1, coverage); // Methoxamine "CH3N"
            TestPentaneAdduct("[M+TMS-H]", "C8H19Si", -1, coverage);  // MSTFA(N-methyl-N-trimethylsilytrifluoroacetamide) "C3H8Si"
            TestPentaneAdduct("[M+TMS+MeOX]-", "C9H23NSi", -1, coverage);
            TestPentaneAdduct("[M+HCOO]", "C6H13O2", -1, coverage);
            TestPentaneAdduct("[M+NOS]5+", "C5H12NOS", 5, coverage); // Not a real adduct, but be ready for adducts we just don't know about
            TestPentaneAdduct("[M+NOS]5", "C5H12NOS", 5, coverage); // Not a real adduct, but be ready for adducts we just don't know about
            TestPentaneAdduct("[M+NOS]5-", "C5H12NOS", -5, coverage); // Not a real adduct, but be ready for adducts we just don't know about
        }
        private void TestMassOffsetAdducts(HashSet<string> coverage)
        {
            TestMassOffsetAdduct("[M+2NH4]", "C5H20N2" + PENTANE_MASS_OFFSET, 2, coverage); // multiple of a group
            TestMassOffsetAdduct("[M+2(NH4)]", "C5H20N2" + PENTANE_MASS_OFFSET, 2, coverage); // multiple of a group in parenthesis
            TestMassOffsetAdduct("[M+2H]", "C5H14" + PENTANE_MASS_OFFSET, 2, coverage);
            TestMassOffsetAdduct("[M+2Cu65+2H]", "C5Cu'2H14" + PENTANE_MASS_OFFSET, 2, coverage); // With heavy copper as in MaConDa Contaminants DB https://www.maconda.bham.ac.uk/downloads/MaConDa__v1_0__csv.zip 
            TestMassOffsetAdduct("[M2C13+2H]", "C3C'2H14" + PENTANE_MASS_OFFSET, 2, coverage); // Labeled
            TestMassOffsetAdduct("[2M2C13+2H]", "C6C'4H26" + TWO_PENTANE_MASS_OFFSET, 2, coverage); // Labeled dimer
            TestMassOffsetAdduct("[2M2C14+2H]", "C6C\"4H26" + TWO_PENTANE_MASS_OFFSET, 2, coverage); // Labeled dimer
            TestMassOffsetAdduct("[M2C13]", "C3C'2H12" + PENTANE_MASS_OFFSET, 0, coverage); // Labeled no charge
            TestMassOffsetAdduct("[2M2C13]", "C6C'4H24" + TWO_PENTANE_MASS_OFFSET, 0, coverage); // Labeled, dimer, no charge
            TestMassOffsetAdduct("[2M]", "C10H24" + TWO_PENTANE_MASS_OFFSET, 0, coverage); // dimer no charge
            TestMassOffsetAdduct("[2M2C13+3]", "C6C'4H24" + TWO_PENTANE_MASS_OFFSET, 3, coverage); // Labeled, dimer, charge only
            TestMassOffsetAdduct("[2M2C13]+3", "C6C'4H24" + TWO_PENTANE_MASS_OFFSET, 3, coverage); // Labeled, dimer, charge only
            TestMassOffsetAdduct("[2M2C13+++]", "C6C'4H24" + TWO_PENTANE_MASS_OFFSET, 3, coverage); // Labeled, dimer, charge only
            TestMassOffsetAdduct("[2M2C13]+++", "C6C'4H24" + TWO_PENTANE_MASS_OFFSET, 3, coverage); // Labeled, dimer, charge only
            TestMassOffsetAdduct("[2M+3]", "C10H24" + TWO_PENTANE_MASS_OFFSET, 3, coverage); // dimer charge only
            TestMassOffsetAdduct("[2M]+3", "C10H24" + TWO_PENTANE_MASS_OFFSET, 3, coverage); // dimer charge only
            TestMassOffsetAdduct("[2M+++]", "C10H24" + TWO_PENTANE_MASS_OFFSET, 3, coverage); // dimer charge only
            TestMassOffsetAdduct("[2M]+++", "C10H24" + TWO_PENTANE_MASS_OFFSET, 3, coverage); // dimer charge only
            TestMassOffsetAdduct("[2M2C13-3]", "C6C'4H24" + TWO_PENTANE_MASS_OFFSET, -3, coverage); // Labeled, dimer, charge only
            TestMassOffsetAdduct("[2M2C13---]", "C6C'4H24" + TWO_PENTANE_MASS_OFFSET, -3, coverage); // Labeled, dimer, charge only
            TestMassOffsetAdduct("[2M-3]", "C10H24" + TWO_PENTANE_MASS_OFFSET,-3, coverage); // dimer charge only
            TestMassOffsetAdduct("[2M---]", "C10H24" + TWO_PENTANE_MASS_OFFSET,-3, coverage); // dimer charge only
            TestMassOffsetAdduct("[2M2C133H2+2H]", "C6C'4H20H'6" + TWO_PENTANE_MASS_OFFSET, 2, coverage); // Labeled with some complexity, multiplied
            TestMassOffsetAdduct("M+H", "C5H13" + PENTANE_MASS_OFFSET, 1, coverage);
            TestMassOffsetAdduct("M+", PENTANE + PENTANE_MASS_OFFSET, 1, coverage);
            TestMassOffsetAdduct("M+2", PENTANE + PENTANE_MASS_OFFSET, 2, coverage);
            TestMassOffsetAdduct("M+3", PENTANE + PENTANE_MASS_OFFSET, 3, coverage);
            TestMassOffsetAdduct("M-", PENTANE + PENTANE_MASS_OFFSET, -1, coverage);
            TestMassOffsetAdduct("M-2", PENTANE + PENTANE_MASS_OFFSET, -2, coverage);
            TestMassOffsetAdduct("M-3", PENTANE + PENTANE_MASS_OFFSET, -3, coverage);
            TestMassOffsetAdduct("M++", PENTANE + PENTANE_MASS_OFFSET, 2, coverage);
            TestMassOffsetAdduct("M--", PENTANE + PENTANE_MASS_OFFSET, -2, coverage);
            TestMassOffsetAdduct("M", PENTANE + PENTANE_MASS_OFFSET, 0, coverage); // Trivial non-adduct
            TestMassOffsetAdduct("M+CH3COO", "C7H15O2" + PENTANE_MASS_OFFSET, -1, coverage); // From XCMS
            TestMassOffsetAdduct("[M+H]1+", "C5H13" + PENTANE_MASS_OFFSET, 1, coverage);
            TestMassOffsetAdduct("[M-H]1-", "C5H11" + PENTANE_MASS_OFFSET, -1, coverage);
            TestMassOffsetAdduct("[M-2H]", "C5H10" + PENTANE_MASS_OFFSET, -2, coverage);
            TestMassOffsetAdduct("[M-2H]2-", "C5H10" + PENTANE_MASS_OFFSET, -2, coverage);
            TestMassOffsetAdduct("[M+2H]++", "C5H14" + PENTANE_MASS_OFFSET, 2, coverage);
            TestMassOffsetAdduct("[MH2+2H]++", "C5H13H'" + PENTANE_MASS_OFFSET, 2, coverage); // Isotope
            TestMassOffsetAdduct("[MH3+2H]++", "C5H13H\"" + PENTANE_MASS_OFFSET, 2, coverage);  // Isotope
            TestMassOffsetAdduct("[MD+2H]++", "C5H13H'" + PENTANE_MASS_OFFSET, 2, coverage);  // Isotope
            TestMassOffsetAdduct("[MD+DMSO+2H]++", "C7H19H'OS" + PENTANE_MASS_OFFSET, 2, coverage); // Check handling of Deuterium and DMSO together
            TestMassOffsetAdduct("[MT+DMSO+2H]++", "C7H19H\"OS" + PENTANE_MASS_OFFSET, 2, coverage); // Check handling of Tritium
            TestMassOffsetAdduct("[M+DMSO+2H]++", "C7H20OS" + PENTANE_MASS_OFFSET, 2, coverage);
            TestMassOffsetAdduct("[M+DMSO+2H]2+", "C7H20OS" + PENTANE_MASS_OFFSET, 2, coverage);
            TestMassOffsetAdduct("[M+MeOH-H]", "C6H15O" + PENTANE_MASS_OFFSET, -1, coverage); // Methanol "CH3OH"
            TestMassOffsetAdduct("[M+MeOX-H]", "C6H14N" + PENTANE_MASS_OFFSET, -1, coverage); // Methoxamine "CH3N"
            TestMassOffsetAdduct("[M+TMS-H]", "C8H19Si" + PENTANE_MASS_OFFSET, -1, coverage);  // MSTFA(N-methyl-N-trimethylsilytrifluoroacetamide) "C3H8Si"
            TestMassOffsetAdduct("[M+TMS+MeOX]-", "C9H23NSi" + PENTANE_MASS_OFFSET, -1, coverage);
            TestMassOffsetAdduct("[M+HCOO]", "C6H13O2" + PENTANE_MASS_OFFSET, -1, coverage);
            TestMassOffsetAdduct("[M+NOS]5+", "C5H12NOS" + PENTANE_MASS_OFFSET, 5, coverage); // Not a real adduct, but be ready for adducts we just don't know about
            TestMassOffsetAdduct("[M+NOS]5", "C5H12NOS" + PENTANE_MASS_OFFSET, 5, coverage); // Not a real adduct, but be ready for adducts we just don't know about
            TestMassOffsetAdduct("[M+NOS]5-", "C5H12NOS" + PENTANE_MASS_OFFSET, -5, coverage); // Not a real adduct, but be ready for adducts we just don't know about
        }

        [TestMethod]
        public void ChargeStateTextTest()
        {
            int min = Transition.MIN_PRODUCT_CHARGE, max = Transition.MAX_PRODUCT_CHARGE;
            for (int i = min; i < max; i++)
            {
                ValidateChargeText(i, Transition.GetChargeIndicator(Adduct.FromChargeProtonated(i)));
                ValidateChargeText(-i, Transition.GetChargeIndicator(Adduct.FromChargeProtonated(-i)));
                ValidateChargeText(i, Transition.GetChargeIndicator(Adduct.FromChargeProtonated(i), CultureInfo.InvariantCulture));
                ValidateChargeText(-i, Transition.GetChargeIndicator(Adduct.FromChargeProtonated(-i), CultureInfo.InvariantCulture));
                ValidateChargeText(i, GetLongFormChargeIndicator(i));
                ValidateChargeText(-i, GetLongFormChargeIndicator(-i));
            }
        }

        private static void ValidateChargeText(int charge, string chargeText)
        {
            const string pepText = "PEPTIDER";
            int min = Transition.MIN_PRODUCT_CHARGE, max = Transition.MAX_PRODUCT_CHARGE;
            Assert.AreEqual(pepText, Transition.StripChargeIndicators(pepText + chargeText, min, max),
                "Unable to round trip charge text " + chargeText);
            Assert.AreEqual(Adduct.FromChargeProtonated(charge), Transition.GetChargeFromIndicator(chargeText, min, max));

            // If the charge indicator contains a space, make sure it is not necessary to be interpreted correctly
            if (chargeText.Contains(' '))
                ValidateChargeText(charge, chargeText.Replace(" ", string.Empty));
        }

        private string GetLongFormChargeIndicator(int i)
        {
            char c = i > 0 ? '+' : '-';
            var sb = new StringBuilder();
            sb.Append(c, Math.Abs(i));
            return sb.ToString();
        }
    }
}