/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI.IonMobility;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class IonMobilityUnitTest : AbstractUnitTest
    {
        /// <summary>
        /// Test the inner workings of ion mobility libraries
        /// </summary>
        [TestMethod]
        public void TestLibIonMobilityInfo()
        {
            const string caffeineFormula = "C8H10N4O2";
            const string caffeineInChiKey = "RYYVLZVUVIJVGH-UHFFFAOYSA-N";
            const string caffeineHMDB = "HMDB01847";
            const double HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC = -.01;
            var dbIon1 = new DbIonMobilityPeptide(new Target("JKLMN"), Adduct.SINGLY_PROTONATED, 1.2, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC) { Id = 12345 };
            for (var loop = 0; loop < 2; loop++)
            {
                var dbIon2 = new DbIonMobilityPeptide(dbIon1);
                DbIonMobilityPeptide dbIon3 = null;
                Assert.AreEqual(dbIon1.GetHashCode(), dbIon2.GetHashCode());
                Assert.IsFalse(dbIon1.Equals(null));
                Assert.IsTrue(dbIon1.Equals(dbIon2 as object));
                // ReSharper disable once ExpressionIsAlwaysNull
                Assert.IsFalse(dbIon1.Equals(dbIon3 as object));
                Assert.IsTrue(dbIon1.Equals(dbIon1));
                Assert.IsTrue(dbIon1.Equals(dbIon1 as object));
                Assert.IsTrue(dbIon1.Equals(dbIon2));
                dbIon1.CollisionalCrossSection = 1.3;
                Assert.AreNotEqual(dbIon1.CollisionalCrossSection, dbIon2.CollisionalCrossSection);
                if (loop==1)
                {
                    dbIon1.ModifiedTarget = new Target("foo");
                    Assert.AreNotEqual(dbIon1.Target, dbIon2.Target);
                    Assert.AreNotEqual(dbIon1.ModifiedTarget, dbIon2.ModifiedTarget);
                }
                else
                {
                    Assert.AreEqual(dbIon1.ModifiedTarget, dbIon2.ModifiedTarget);
                    Assert.AreEqual(dbIon1.ModifiedTarget.Molecule, dbIon2.ModifiedTarget.Molecule);
                }
                dbIon1 = new DbIonMobilityPeptide( 
                    SmallMoleculeLibraryAttributes.Create("caffeine", caffeineFormula, caffeineInChiKey, caffeineHMDB),
                    Adduct.FromStringAssumeProtonated("M+Na"), 
                    1.2, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC) { Id = 12345 };
            }

            var dictCCS1 = new Dictionary<LibKey, IonMobilityAndCCS[]>();
            var ccs1 = new List<IonMobilityAndCCS> {  IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.EMPTY, 1, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),  IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.EMPTY, 2, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC) }; // Collisional cross sections
            var ccs2 = new List<IonMobilityAndCCS> {  IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.EMPTY, 3, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC),  IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.EMPTY, 4, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC) }; // Collisional cross sections
            const string seq1 = "JKLM";
            const string seq2 = "KLMN";
            dictCCS1.Add(new LibKey(seq1,1),ccs1.ToArray());
            dictCCS1.Add(new LibKey(seq2,1),ccs2.ToArray());
            var lib = new List<LibraryIonMobilityInfo> { new LibraryIonMobilityInfo("test", dictCCS1) };

            var peptideTimes = CollisionalCrossSectionGridViewDriver.ConvertDriftTimesToCollisionalCrossSections(null,
                lib, 1, null);
            var validatingIonMobilityPeptides = peptideTimes as ValidatingIonMobilityPeptide[] ?? peptideTimes.ToArray();
            Assert.AreEqual(2, validatingIonMobilityPeptides.Length);
            Assert.AreEqual(1.5, validatingIonMobilityPeptides[0].CollisionalCrossSection);
            Assert.AreEqual(3.5, validatingIonMobilityPeptides[1].CollisionalCrossSection);
            Assert.AreEqual(HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC, validatingIonMobilityPeptides[1].HighEnergyDriftTimeOffsetMsec);

            // Test serialization of molecule with '$' in it, which we use as a tab replacement against XML parser variability
            var molser = CustomMolecule.FromSmallMoleculeLibraryAttributes(SmallMoleculeLibraryAttributes.Create("caffeine$", caffeineFormula, caffeineInChiKey, caffeineHMDB));
            var text = molser.ToSerializableString();
            Assert.AreEqual(molser, CustomMolecule.FromSerializableString(text));

            // Test handling of SmallMoleculeLibraryAttributes for mass-only descriptions
            var molserB = CustomMolecule.FromSmallMoleculeLibraryAttributes(SmallMoleculeLibraryAttributes.Create("caffeine$", null, new TypedMass(123.4, MassType.Monoisotopic), new TypedMass(123.45, MassType.Average), caffeineInChiKey, caffeineHMDB));
            var textB = molserB.ToSerializableString();
            Assert.AreEqual(molserB, CustomMolecule.FromSerializableString(textB));

            var dictCCS2 = new Dictionary<LibKey, IonMobilityAndCCS[]>();
            var ccs3 = new List<IonMobilityAndCCS> { IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.GetIonMobilityValue(4, eIonMobilityUnits.drift_time_msec), null, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC), IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.GetIonMobilityValue(5, eIonMobilityUnits.drift_time_msec), null, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC) }; // Drift times
            const string seq3 = "KLMNJ";
            dictCCS2.Add(new LibKey(seq3, Adduct.SINGLY_PROTONATED), ccs3.ToArray());
            lib.Add(new LibraryIonMobilityInfo("test2", dictCCS2));
            List<LibraryIonMobilityInfo> lib1 = lib;
            AssertEx.ThrowsException<Exception>(() => CollisionalCrossSectionGridViewDriver.ConvertDriftTimesToCollisionalCrossSections(null,
                lib1, 2, null),
                String.Format(
                        Resources.CollisionalCrossSectionGridViewDriver_ProcessIonMobilityValues_Cannot_import_measured_ion_mobility_for_sequence__0___no_collisional_cross_section_conversion_parameters_were_provided_for_charge_state__1__,
                        seq3, 1));

            var regressions = new Dictionary<int, RegressionLine> {{1, new RegressionLine(2, 1)}};
            lib = new List<LibraryIonMobilityInfo> { new LibraryIonMobilityInfo("test", dictCCS2) };
            peptideTimes = CollisionalCrossSectionGridViewDriver.ConvertDriftTimesToCollisionalCrossSections(null,
                            lib, 1, regressions);
            validatingIonMobilityPeptides = peptideTimes as ValidatingIonMobilityPeptide[] ?? peptideTimes.ToArray();
            Assert.AreEqual(1, validatingIonMobilityPeptides.Length);
            Assert.AreEqual(1.75, validatingIonMobilityPeptides[0].CollisionalCrossSection);
        }

    }
}