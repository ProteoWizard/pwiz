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
            const double delta = .000001;
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
        }
    }
}
