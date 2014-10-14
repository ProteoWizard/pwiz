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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI.IonMobility;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
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
            const double HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC = -.01;
            var dbIon1 = new DbIonMobilityPeptide("JKLMN", 1.2, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC) { Id = 12345 };
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
            dbIon1.PeptideModSeq = "foo";
            Assert.AreNotEqual(dbIon1.CollisionalCrossSection, dbIon2.CollisionalCrossSection);
            Assert.AreNotEqual(dbIon1.Sequence, dbIon2.Sequence);
            Assert.AreNotEqual(dbIon1.PeptideModSeq, dbIon2.PeptideModSeq);

            var dictCCS1 = new Dictionary<LibKey, IonMobilityInfo[]>();
            var ccs1 = new List<IonMobilityInfo> { new IonMobilityInfo(1, true, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC), new IonMobilityInfo(2, true, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC) }; // Collisional cross sections
            var ccs2 = new List<IonMobilityInfo> { new IonMobilityInfo(3, true, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC), new IonMobilityInfo(4, true, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC) }; // Collisional cross sections
            const string seq1 = "JKLM";
            const string seq2 = "KLMN";
            dictCCS1.Add(new LibKey(seq1,1),ccs1.ToArray());
            dictCCS1.Add(new LibKey(seq2,1),ccs2.ToArray());
            var lib = new List<LibraryIonMobilityInfo> { new LibraryIonMobilityInfo("test", dictCCS1) };

            var peptideTimes = CollisionalCrossSectionGridViewDriver.ConvertDriftTimesToCollisionalCrossSections(null,
                lib, 1, null);
            var validatingIonMobilityPeptides = peptideTimes as ValidatingIonMobilityPeptide[] ?? peptideTimes.ToArray();
            Assert.AreEqual(2, validatingIonMobilityPeptides.Count());
            Assert.AreEqual(1.5, validatingIonMobilityPeptides[0].CollisionalCrossSection);
            Assert.AreEqual(3.5, validatingIonMobilityPeptides[1].CollisionalCrossSection);
            Assert.AreEqual(HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC, validatingIonMobilityPeptides[1].HighEnergyDriftTimeOffsetMsec);


            var dictCCS2 = new Dictionary<LibKey, IonMobilityInfo[]>();
            var ccs3 = new List<IonMobilityInfo> { new IonMobilityInfo(4, false, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC), new IonMobilityInfo(5, false, HIGH_ENERGY_DRIFT_TIME_OFFSET_MSEC) }; // Drift times
            const string seq3 = "KLMNJ";
            dictCCS2.Add(new LibKey(seq3, 1), ccs3.ToArray());
            lib.Add(new LibraryIonMobilityInfo("test2", dictCCS2));
            List<LibraryIonMobilityInfo> lib1 = lib;
            AssertEx.ThrowsException<Exception>(() => CollisionalCrossSectionGridViewDriver.ConvertDriftTimesToCollisionalCrossSections(null,
                lib1, 2, null),
                String.Format(
                        Resources.CollisionalCrossSectionGridViewDriver_ProcessIonMobilityValues_Cannot_import_measured_drift_time_for_sequence__0___no_collisional_cross_section_conversion_parameters_were_provided_for_charge_state__1__,
                        seq3, 1));

            var regressions = new Dictionary<int, RegressionLine> {{1, new RegressionLine(2, 1)}};
            lib = new List<LibraryIonMobilityInfo> { new LibraryIonMobilityInfo("test", dictCCS2) };
            peptideTimes = CollisionalCrossSectionGridViewDriver.ConvertDriftTimesToCollisionalCrossSections(null,
                            lib, 1, regressions);
            validatingIonMobilityPeptides = peptideTimes as ValidatingIonMobilityPeptide[] ?? peptideTimes.ToArray();
            Assert.AreEqual(1, validatingIonMobilityPeptides.Count());
            Assert.AreEqual(1.75, validatingIonMobilityPeptides[0].CollisionalCrossSection);
        }

    }
}