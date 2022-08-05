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

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Model.Lib;
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
            var dbMolecule = new DbMolecule(new Target("JKLMN")) { Id = 123456 };
            var dbPrecursorIon = new DbPrecursorIon(dbMolecule, Adduct.SINGLY_PROTONATED) { Id = 1234567 };
            var dbIonMobilityValue = new DbPrecursorAndIonMobility(dbPrecursorIon, 
                1.2, 2.3, eIonMobilityUnits.drift_time_msec, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC) { Id = 12345 };
            DbPrecursorAndIonMobility dbPrecursorAndIonMobilityValue2 = new DbPrecursorAndIonMobility(dbIonMobilityValue);
            for (var loop = 0; loop < 2; loop++)
            {
                Assert.AreEqual(dbIonMobilityValue.GetHashCode(), dbPrecursorAndIonMobilityValue2.GetHashCode());
                Assert.IsFalse(dbIonMobilityValue.Equals(null));
                Assert.IsTrue(dbIonMobilityValue.Equals(dbPrecursorAndIonMobilityValue2 as object));
                Assert.IsTrue(dbIonMobilityValue.Equals(dbIonMobilityValue));
                Assert.IsTrue(dbIonMobilityValue.Equals(dbIonMobilityValue as object));
                Assert.IsTrue(dbPrecursorAndIonMobilityValue2.Equals(dbIonMobilityValue));
                dbIonMobilityValue.CollisionalCrossSectionSqA = 1.3;
                Assert.AreNotEqual(dbIonMobilityValue.CollisionalCrossSectionSqA, dbPrecursorAndIonMobilityValue2.CollisionalCrossSectionSqA);
                if (loop==1)
                {
                    dbIonMobilityValue.DbPrecursorIon = new DbPrecursorIon(new Target("foo"), Adduct.SINGLY_PROTONATED) { Id = 1234567 };
                    Assert.AreNotEqual(dbIonMobilityValue.DbPrecursorIon.GetTarget(), dbMolecule);
                }
                else
                {
                    Assert.AreEqual(dbIonMobilityValue.DbPrecursorIon.DbMolecule, dbMolecule);
                }
                dbIonMobilityValue = new DbPrecursorAndIonMobility(
                    new DbPrecursorIon( 
                    SmallMoleculeLibraryAttributes.Create("caffeine", caffeineFormula, caffeineInChiKey, caffeineHMDB),
                    Adduct.FromStringAssumeProtonated("M+Na")) { Id = 23456 },
                    1.2, 2.3, eIonMobilityUnits.drift_time_msec, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC ) { Id = 12345 };
                dbPrecursorAndIonMobilityValue2 = new DbPrecursorAndIonMobility(dbIonMobilityValue);
            }

            var dictCCS1 = new Dictionary<LibKey, PrecursorFilter[]>();
            var im = IonMobilityValue.GetIonMobilityValue(12, eIonMobilityUnits.drift_time_msec);
            var ccs1 = new List<PrecursorFilter> 
            { 
                PrecursorFilter.EMPTY.ChangeIonMobilityAndCCS(IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.EMPTY, 1, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC)),
                PrecursorFilter.EMPTY.ChangeIonMobilityAndCCS(IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.EMPTY, 2, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC)) 
            }; // Collisional cross sections
            var ccs2 = new List<PrecursorFilter>
            {
                PrecursorFilter.EMPTY.ChangeIonMobilityAndCCS(IonMobilityAndCCS.GetIonMobilityAndCCS(im, 3, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC)),
                PrecursorFilter.EMPTY.ChangeIonMobilityAndCCS(IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.EMPTY, 4, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC))
            }; // Collisional cross sections
            const string seq1 = "JKLM";
            const string seq2 = "KLMN";
            dictCCS1.Add(new LibKey(seq1,1),ccs1.ToArray());
            dictCCS1.Add(new LibKey(seq2,1),ccs2.ToArray());
            var lib =new LibraryPrecursorFiltersInfo(dictCCS1);
            var status = new ProgressStatus(string.Empty);

            CollisionalCrossSectionGridViewDriver.CollectIonMobilitiesAndCollisionalCrossSections(null, lib, status, out var peptideTimes);
            var validatingIonMobilityPeptides = peptideTimes.ToArray();
            Assert.AreEqual(4, validatingIonMobilityPeptides.Length);
            Assert.AreEqual(1, validatingIonMobilityPeptides[0].CollisionalCrossSectionSqA);
            Assert.AreEqual(2, validatingIonMobilityPeptides[1].CollisionalCrossSectionSqA);
            Assert.AreEqual(3, validatingIonMobilityPeptides[2].CollisionalCrossSectionSqA);
            Assert.AreEqual(4, validatingIonMobilityPeptides[3].CollisionalCrossSectionSqA);
            Assert.AreEqual(HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC, validatingIonMobilityPeptides[1].HighEnergyIonMobilityOffset);


            // Test serialization of molecule with '$' in it, which we use as a tab replacement against XML parser variability
            var molser = CustomMolecule.FromSmallMoleculeLibraryAttributes(SmallMoleculeLibraryAttributes.Create("caffeine$", caffeineFormula, caffeineInChiKey, caffeineHMDB));
            var text = molser.ToSerializableString();
            Assert.AreEqual(molser, CustomMolecule.FromSerializableString(text));

            // Test handling of SmallMoleculeLibraryAttributes for mass-only descriptions
            var molserB = CustomMolecule.FromSmallMoleculeLibraryAttributes(SmallMoleculeLibraryAttributes.Create("caffeine$", null, new TypedMass(123.4, MassType.Monoisotopic), new TypedMass(123.45, MassType.Average), caffeineInChiKey, caffeineHMDB));
            var textB = molserB.ToSerializableString();
            Assert.AreEqual(molserB, CustomMolecule.FromSerializableString(textB));

            var dictCCS2 = new Dictionary<LibKey, PrecursorFilter[]>();
            var ccs3 = new List<IonMobilityAndCCS>
            {
                IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.GetIonMobilityValue(4, eIonMobilityUnits.drift_time_msec), 1.75, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC), 
                IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobilityValue.GetIonMobilityValue(5, eIonMobilityUnits.drift_time_msec), null, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC)
            }; // Drift times
            const string seq3 = "KLMNJ";
            dictCCS2.Add(new LibKey(seq3, Adduct.SINGLY_PROTONATED), ccs3.Select(c => PrecursorFilter.EMPTY.ChangeIonMobilityAndCCS(c)).ToArray());

            lib = new LibraryPrecursorFiltersInfo(dictCCS2);
            CollisionalCrossSectionGridViewDriver.CollectIonMobilitiesAndCollisionalCrossSections(null, lib, status, out peptideTimes);
            validatingIonMobilityPeptides = peptideTimes.ToArray();
            Assert.AreEqual(2, validatingIonMobilityPeptides.Length);
            Assert.AreEqual(1.75, validatingIonMobilityPeptides[0].CollisionalCrossSectionNullable);
            Assert.AreEqual(null, validatingIonMobilityPeptides[1].CollisionalCrossSectionNullable);
        }

    }
}