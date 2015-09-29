/*
 * Original author: Vagisha Sharma <vsharma .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.ProteowizardWrapper;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    /// <summary>
    /// Summary description for PwizFileInfoTest
    /// </summary>
    [TestClass]
    public class PwizFileInfoTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestInstrumentInfo()
        {
            const string testZipPath = @"TestA\PwizFileInfoTest.zip";

            var testFilesDir = new TestFilesDir(TestContext, testZipPath);

            // Waters file (.raw directory) and mz5 equivalent
            foreach (
                var ext in
                    new[]
                    {ExtensionTestContext.ExtWatersRaw, ExtensionTestContext.ExtMz5})
            {
                VerifyInstrumentInfo(testFilesDir.GetTestPath("160109_Mix1_calcurve_075" + ext),
                    "Waters instrument model", "", "", "");
            }

            // ABI .wiff file
            if (ExtensionTestContext.CanImportAbWiff)
            {
                VerifyInstrumentInfo(testFilesDir.GetTestPath("051309_digestion.wiff"),
                    "4000 QTRAP", "electrospray ionization", "quadrupole/quadrupole/axial ejection linear ion trap", "electron multiplier");
            }

            // MzWiff generated mzXML files
            VerifyInstrumentInfo(testFilesDir.GetTestPath("051309_digestion-s3.mzXML"),
                "4000 Q Trap", "electrospray ionization", "TOFMS", "");

            // Agilent file (.d directory)
            VerifyInstrumentInfo(testFilesDir.GetTestPath("081809_100fmol-MichromMix-05" + ExtensionTestContext.ExtAgilentRaw),
                "Agilent instrument model", "nanoelectrospray", "quadrupole/quadrupole/quadrupole", "electron multiplier");

            // Thermo .raw|mzML file
            foreach (
                var ext in
                    new[]
                    {ExtensionTestContext.ExtThermoRaw, ExtensionTestContext.ExtMzml})
            {
                VerifyInstrumentInfo(testFilesDir.GetTestPath("CE_Vantage_15mTorr_0001_REP1_01" + ext),
                    "TSQ Vantage", "nanoelectrospray", "quadrupole/quadrupole/quadrupole", "electron multiplier");
            }
        }

        private static void VerifyInstrumentInfo(string path, string model, string ionization, string analyzer, string detector)
        {
            using (var msDataFile = new MsDataFileImpl(path))
            {
                var instrumentInfoList = msDataFile.GetInstrumentConfigInfoList().ToList();
                Assert.AreEqual(1, instrumentInfoList.Count);
                var instrument = instrumentInfoList[0];
                Assert.IsFalse(instrument.IsEmpty);
                Assert.AreEqual(model, instrument.Model);
                Assert.AreEqual(ionization, instrument.Ionization);
                Assert.AreEqual(analyzer, instrument.Analyzer);
                Assert.AreEqual(detector, instrument.Detector);
            }
        }
    }
}
