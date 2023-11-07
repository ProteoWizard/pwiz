/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class MeasuredIonTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestMeasuredIonRoundTrip()
        {
            var measuredIonList = new MeasuredIonList();
            measuredIonList.AddDefaults();
            Assert.AreNotEqual(0, measuredIonList.Count);
            foreach (var measuredIon in measuredIonList)
            {
                var roundTrip = AssertEx.RoundTrip(measuredIon);
                Assert.AreEqual(measuredIon.Adduct, roundTrip.Adduct);
            }

            // Verify that the masses of the TMT ions are the same as published in:
            // https://assets.thermofisher.com/TFS-Assets/LSG/manuals/MAN0015866_2162600_TMT_MassTagging_UG.pdf
            double delta = .000001;
            Assert.AreEqual(126.127726, MeasuredIonList.TMT_126.SettingsCustomIon.MonoisotopicMassMz, delta);
            Assert.AreEqual(127.124761, MeasuredIonList.TMT_127_L.SettingsCustomIon.MonoisotopicMassMz, delta);
            Assert.AreEqual(127.131081, MeasuredIonList.TMT_127_H.SettingsCustomIon.MonoisotopicMassMz, delta);
            Assert.AreEqual(128.128116, MeasuredIonList.TMT_128_L.SettingsCustomIon.MonoisotopicMassMz, delta);
            Assert.AreEqual(128.134436, MeasuredIonList.TMT_128_H.SettingsCustomIon.MonoisotopicMassMz, delta);
            Assert.AreEqual(129.131471, MeasuredIonList.TMT_129_L.SettingsCustomIon.MonoisotopicMassMz, delta);
            Assert.AreEqual(129.137790, MeasuredIonList.TMT_129_H.SettingsCustomIon.MonoisotopicMassMz, delta);
            Assert.AreEqual(130.134825, MeasuredIonList.TMT_130_L.SettingsCustomIon.MonoisotopicMassMz, delta);
            Assert.AreEqual(130.141145, MeasuredIonList.TMT_130_H.SettingsCustomIon.MonoisotopicMassMz, delta);
            Assert.AreEqual(131.138180, MeasuredIonList.TMT_131.SettingsCustomIon.MonoisotopicMassMz, delta);
            // Verify TMT pro masses the same as publications
            // https://pubs.acs.org/doi/full/10.1021/acs.analchem.9b04474
            // https://www.nature.com/articles/s41592-020-0781-4
            Assert.AreEqual(131.138180, MeasuredIonList.TMT_131_L.SettingsCustomIon.MonoisotopicMassMz, delta);
            delta *= 100; // Published m/z's don't agree with Skyline until 4th decimal place rounding
            Assert.AreEqual(131.14450, MeasuredIonList.TMT_131_H.SettingsCustomIon.MonoisotopicMassMz, delta);
            Assert.AreEqual(132.14150, MeasuredIonList.TMT_132_L.SettingsCustomIon.MonoisotopicMassMz, delta);
            Assert.AreEqual(132.14785, MeasuredIonList.TMT_132_H.SettingsCustomIon.MonoisotopicMassMz, delta);
            Assert.AreEqual(133.14488, MeasuredIonList.TMT_133_L.SettingsCustomIon.MonoisotopicMassMz, delta);
            Assert.AreEqual(133.15120, MeasuredIonList.TMT_133_H.SettingsCustomIon.MonoisotopicMassMz, delta);
            Assert.AreEqual(134.14824, MeasuredIonList.TMT_134_L.SettingsCustomIon.MonoisotopicMassMz, delta);
            // https://www.ncbi.nlm.nih.gov/pmc/articles/PMC8210943/
            delta /= 10; // Agree at 5th decimal place for 18-plex ions
            Assert.AreEqual(134.154557, MeasuredIonList.TMT_134_H.SettingsCustomIon.MonoisotopicMassMz, delta);
            Assert.AreEqual(135.151592, MeasuredIonList.TMT_135.SettingsCustomIon.MonoisotopicMassMz, delta);
        }
    }
}
