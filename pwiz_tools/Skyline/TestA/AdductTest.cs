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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    /// <summary>
    /// Summary description for RefineTest
    /// </summary>
    [TestClass]
    public class AdductTest : AbstractUnitTest
    {
        private string PENTANE = "C5H12";

        private void TestPentaneAdduct(string adduct, string expectedFormula, int expectedCharge)
        {
            int z;
            var actual = IonInfo.ApplyAdductToFormula(PENTANE, adduct, out z).ToString();
            Assert.AreEqual(expectedFormula, actual, "unexpected formula for adduct "+adduct);
            Assert.AreEqual(expectedCharge, z, "unexpected charge for adduct " + adduct);
        }

        private void TestTaxolAdduct(string adduct, double expectedMz, int expectedCharge)
        {
            // See http://fiehnlab.ucdavis.edu/staff/kind/Metabolomics/MS-Adduct-Calculator/
            var Taxol = "C47H51NO14"; // M=853.33089 (agrees with chemspider)
            Assert.AreEqual(853.3309, BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(Taxol), .0001); 
            int charge;
            var molecule = IonInfo.ApplyAdductToFormula(Taxol, adduct, out charge);
            Assert.AreEqual(expectedCharge, charge);
            var mass = BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(molecule.ToString());
            var mz = BioMassCalc.CalculateIonMz(mass, charge);
            Assert.AreEqual(expectedMz, mz, .001);
        }

        private void TestException(string formula, string adduct)
        {
            AssertEx.ThrowsException<InvalidOperationException>(() =>
            {
                int zz;
                IonInfo.ApplyAdductToFormula(formula, adduct, out zz);
            });
        }


        [TestMethod]
        public void AdductParserTest()
        {
            TestPentaneAdduct("[M+2H]", "C5H14", 2);
            TestPentaneAdduct("[M2C13+2H]", "C3C'2H14", 2); // Labeled
            TestPentaneAdduct("[2M2C13+2H]", "C6C'4H26", 2); // Labeled, multiplied
            TestPentaneAdduct("M+H", "C5H13", 1);
            TestPentaneAdduct("M+", PENTANE, 1);
            TestPentaneAdduct("M+CH3COO", "C7H15O2", -1); // From XCMS
            TestPentaneAdduct("[M+H]1+", "C5H13", 1);
            TestPentaneAdduct("[M-H]1-", "C5H11", -1);
            TestPentaneAdduct("[M-2H]", "C5H10", -2);
            TestPentaneAdduct("[M-2H]2-", "C5H10", -2);
            TestPentaneAdduct("[M+2H]++", "C5H14", 2);
            TestPentaneAdduct("[M+DMSO+2H]++", "C7H20OS", 2);
            TestPentaneAdduct("[M+DMSO+2H]2+", "C7H20OS", 2);
            TestPentaneAdduct("[M+HCOO]", "C6H13O2", -1);
            TestPentaneAdduct("[M+NOS]5+", "C5H12NOS", 5); // Not a real adduct, but be ready for adducts we just don't know about
            TestPentaneAdduct("[M+NOS]5", "C5H12NOS", 5); // Not a real adduct, but be ready for adducts we just don't know about
            TestPentaneAdduct("[M+NOS]5-", "C5H12NOS", -5); // Not a real adduct, but be ready for adducts we just don't know about
            
            // See http://fiehnlab.ucdavis.edu/staff/kind/Metabolomics/MS-Adduct-Calculator/
            // There you will find an excel spreadsheet from which I pulled these numbers, which as it turns out has several errors in it.
            // There is also a table in the web page itself that contains the same values and some unmarked corrections.
            // Sadly that faulty spreadsheet is copied all over the internet.  I've let the author know what we found. - bspratt
            TestTaxolAdduct("M+3H", 285.450906, 3);
            TestTaxolAdduct("M+2H+Na", 292.778220, 3);
            TestTaxolAdduct("M+H+2Na", 300.105557, 3); // Spreadsheet and table both say 300.209820, but also says adduct "mass" = 15.766190, I get 15.6618987 using their H and Na masses (and this is clearly m/z, not mass)
            TestTaxolAdduct("M+3Na", 307.432848, 3);
            TestTaxolAdduct("M+2H", 427.672721, 2);
            TestTaxolAdduct("M+H+NH4", 436.185995, 2);
            TestTaxolAdduct("M+H+Na", 438.663692, 2);
            TestTaxolAdduct("M+H+K", 446.650662, 2);
            TestTaxolAdduct("M+ACN+2H", 448.185995, 2);
            TestTaxolAdduct("M+2Na", 449.654663, 2);
            TestTaxolAdduct("M+2ACN+2H", 468.699268, 2);
            TestTaxolAdduct("M+3ACN+2H", 489.212542, 2);
            TestTaxolAdduct("M+H", 854.338166, 1);
            TestTaxolAdduct("M+NH4", 871.364713, 1);
            TestTaxolAdduct("M+Na", 876.320108, 1);
            TestTaxolAdduct("M+CH3OH+H", 886.364379, 1);
            TestTaxolAdduct("M+K", 892.294048, 1);
            TestTaxolAdduct("M+ACN+H", 895.364713, 1);
            TestTaxolAdduct("M+2Na-H", 898.302050, 1);
            TestTaxolAdduct("M+IsoProp+H", 914.396230, 1);
            TestTaxolAdduct("M+ACN+Na", 917.346655, 1);
            TestTaxolAdduct("M+2K-H", 930.249930, 1);  // Spreadsheet and table disagree - spreadsheet says "M+2K+H" but that's 3+, not 1+, and this fits the mz value
            TestTaxolAdduct("M+DMSO+H", 932.352110, 1);
            TestTaxolAdduct("M+2ACN+H", 936.391260, 1);
            TestTaxolAdduct("M+IsoProp+Na+H", 468.692724, 2); // Spreadsheet and table both say mz=937.386000 z=1 (does Isoprop interact somehow to eliminate half the ionization?)
            TestTaxolAdduct("2M+H", 1707.669056, 1);
            TestTaxolAdduct("2M+NH4", 1724.695603, 1);
            TestTaxolAdduct("2M+Na", 1729.650998, 1);
            TestTaxolAdduct("2M+3H2O+2H", 881.354, 2); // Does not appear in table.  Charge agrees but spreadsheet says mz= 1734.684900
            TestTaxolAdduct("2M+K", 1745.624938, 1);
            TestTaxolAdduct("2M+ACN+H", 1748.695603, 1);
            TestTaxolAdduct("2M+ACN+Na", 1770.677545, 1);
            TestTaxolAdduct("M-3H", 283.436354, -3);
            TestTaxolAdduct("M-2H", 425.658169, -2);
            TestTaxolAdduct("M-H2O-H", 834.312500, -1);
            TestTaxolAdduct("M+-H2O-H", 834.312500, -1); // Tolerate empty atom description ("+-")
            TestTaxolAdduct("M-H", 852.323614, -1);
            TestTaxolAdduct("M+Na-2H", 874.305556, -1);
            TestTaxolAdduct("M+Cl", 888.300292, -1);
            TestTaxolAdduct("M+K-2H", 890.279496, -1);
            TestTaxolAdduct("M+FA-H", 898.329091, -1);
            TestTaxolAdduct("M+Hac-H", 912.344741, -1);
            TestTaxolAdduct("M+Br", 932.249775, -1);
            TestTaxolAdduct("M+TFA-H", 966.316476, -1);
            TestTaxolAdduct("2M-H", 1705.654504, -1);
            TestTaxolAdduct("2M+FA-H", 1751.659981, -1);
            TestTaxolAdduct("2M+Hac-H", 1765.675631, -1);
            TestTaxolAdduct("3M-H", 2558.985394, -1); // Spreadsheet and table give mz as 2560.999946 -but also gives adduct "mass" as 1.007276, should be -1.007276

            // Using example adducts from
            // https://gnps.ucsd.edu/ProteoSAFe/gnpslibrary.jsp?library=GNPS-LIBRARY#%7B%22Library_Class_input%22%3A%221%7C%7C2%7C%7C3%7C%7CEXACT%22%7D
            int z;
            var Hectochlorin = "C27H34Cl2N2O9S2";
            var massHectochlorin = 664.108276; // http://www.chemspider.com/Chemical-Structure.552449.html?rid=3a7c08af-0886-4e82-9e4f-5211b8efb373
            var mol = IonInfo.ApplyAdductToFormula(Hectochlorin, "M+H", out z).ToString();
            var mass = BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(mol);
            Assert.AreEqual(massHectochlorin + BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula("H"), mass, 0.00001);
            var mz = BioMassCalc.CalculateIonMz(mass, z);
            Assert.AreEqual(665.11555415, mz, .000001);  // GNPS says 665.0 for Hectochlorin M+H
            mol = IonInfo.ApplyAdductToFormula(Hectochlorin, "MCl37+H", out z).ToString();
            Assert.AreEqual("C27ClCl'H35N2O9S2", mol);
            mass = BioMassCalc.MONOISOTOPIC.CalculateMassFromFormula(mol);
            Assert.AreEqual(667.11315, mass, .00001);
            mol = IonInfo.ApplyAdductToFormula(Hectochlorin, "M2Cl37+H", out z).ToString();
            Assert.AreEqual("C27Cl'2H35N2O9S2", mol);

           TestException(Hectochlorin, "M3Cl37+H"); // Trying to label more chlorines than exist in the molecule
           TestException(PENTANE, "M+foo+H"); // Unknown adduct
           TestException(PENTANE, "M+H+"); // Trailing sign 
           TestException(PENTANE,"[M-2H]3-"); // Declared charge doesn't match described charge

           // Test label stripping
           Assert.AreEqual("C5H9NO2S", (new IonInfo("C5H9H'3NO2S[M-3H]")).UnlabeledFormula);

            // Peptide representations
            Assert.AreEqual("C40H65N11O16[M+2H]", (new SequenceMassCalc(MassType.Average)).GetIonFormula("PEPTIDER", 2, null));
        }
    }
}