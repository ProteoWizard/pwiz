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
using System.Collections.Generic;
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
    public class PwizFileInfoTest
    {
        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void TestInstrumentInfo()
        {
            const string testZipPath = @"TestA\PwizFileInfoTest.zip";

            var testFilesDir = new TestFilesDir(TestContext, testZipPath);

            // Waters file (.raw directory)
            string path = testFilesDir.GetTestPath("160109_Mix1_calcurve_075.raw");
            MsDataFileImpl msDataFile;
            List<MsInstrumentConfigInfo> instrumentInfoList;
            MsInstrumentConfigInfo instrument;
            if (ExtensionTestContext.CanImportWatersRaw)
            {
                msDataFile = new MsDataFileImpl(path);
                instrumentInfoList = msDataFile.GetInstrumentConfigInfoList().ToList();
                Assert.AreEqual(1, instrumentInfoList.Count);
                instrument = instrumentInfoList[0];
                Assert.IsFalse(instrument.IsEmpty);
                Assert.AreEqual("Waters instrument model", instrument.Model);
                Assert.AreEqual("", instrument.Ionization);
                Assert.AreEqual("", instrument.Analyzer);
                Assert.AreEqual("", instrument.Detector);
                msDataFile.Dispose();
            }

            // ABI .wiff file
            path = testFilesDir.GetTestPath("051309_digestion.wiff");
            msDataFile = new MsDataFileImpl(path);
            instrumentInfoList = msDataFile.GetInstrumentConfigInfoList().ToList();
            Assert.AreEqual(1, instrumentInfoList.Count);
            instrument = instrumentInfoList[0];
            Assert.IsFalse(instrument.IsEmpty);
            Assert.AreEqual("Applied Biosystems instrument model", instrument.Model);          
            Assert.AreEqual("", instrument.Ionization);
            Assert.AreEqual("", instrument.Analyzer);
            Assert.AreEqual("", instrument.Detector);
            msDataFile.Dispose();

            // MzWiff generated mzXML files
            path = testFilesDir.GetTestPath("051309_digestion-s3.mzXML");
            msDataFile = new MsDataFileImpl(path);
            instrumentInfoList = msDataFile.GetInstrumentConfigInfoList().ToList();
            Assert.AreEqual(1, instrumentInfoList.Count);
            instrument = instrumentInfoList[0];
            Assert.IsFalse(instrument.IsEmpty);
            Assert.AreEqual("4000 Q Trap", instrument.Model);
            Assert.AreEqual("electrospray ionization", instrument.Ionization);
            Assert.AreEqual("TOFMS", instrument.Analyzer);
            Assert.AreEqual("", instrument.Detector);
            msDataFile.Dispose();

            // Agilent file (.d directory)
            path = testFilesDir.GetTestPath("081809_100fmol-MichromMix-05.d");
            msDataFile = new MsDataFileImpl(path);
            instrumentInfoList = msDataFile.GetInstrumentConfigInfoList().ToList();
            Assert.AreEqual(1, instrumentInfoList.Count);
            instrument = instrumentInfoList[0];
            Assert.IsFalse(instrument.IsEmpty);
            Assert.AreEqual("Agilent instrument model", instrument.Model);
            Assert.AreEqual("nanoelectrospray", instrument.Ionization);
            Assert.AreEqual("quadrupole/quadrupole/quadrupole", instrument.Analyzer);
            Assert.AreEqual("electron multiplier", instrument.Detector);
            msDataFile.Dispose();

            // Thermo .raw|mzML file
            path = testFilesDir.GetTestPath("CE_Vantage_15mTorr_0001_REP1_01" + ExtensionTestContext.ExtThermoRaw);
            msDataFile = new MsDataFileImpl(path);
            instrumentInfoList = msDataFile.GetInstrumentConfigInfoList().ToList();
            Assert.AreEqual(1, instrumentInfoList.Count);
            instrument = instrumentInfoList[0];
            Assert.IsFalse(instrument.IsEmpty);
            Assert.AreEqual("TSQ Vantage", instrument.Model);
            Assert.AreEqual("nanoelectrospray", instrument.Ionization);
            Assert.AreEqual("quadrupole/quadrupole/quadrupole", instrument.Analyzer);
            Assert.AreEqual("electron multiplier", instrument.Detector);
            msDataFile.Dispose();
        }
    }
}
