/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.ProteowizardWrapper;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData
{
    [TestClass]
    internal class DataFileMemoryLeakTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestDataFileSpectrumMemoryLeak()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"TestData\DataFileMemoryLeakTest.zip");
            using var msDataFile = new MsDataFileImpl(TestFilesDir.GetTestPath("DissociationMethodTest.mzML"));
            for (int repeat = 0; repeat < 100; repeat++)
            {
                int spectrumCount = msDataFile.SpectrumCount;
                for (int i = 0; i < spectrumCount; i++)
                {
                    var spectrum = msDataFile.GetSpectrum(i);
                    Assert.IsNotNull(spectrum);
                }
            }

            // An MGF file is parsed into a list that hands back the same Spectrum objects on every
            // read, so a read must leave their peaks in place for the next one
            var mgfPath = TestFilesDir.GetTestPath(@"Reread.mgf");
            File.WriteAllText(mgfPath, string.Join(Environment.NewLine,
                @"BEGIN IONS", @"TITLE=first", @"PEPMASS=500.25", @"CHARGE=2+", @"RTINSECONDS=10",
                @"100.5 10", @"200.5 20", @"300.5 30", @"END IONS",
                @"BEGIN IONS", @"TITLE=second", @"PEPMASS=600.25", @"CHARGE=2+", @"RTINSECONDS=20",
                @"150.5 15", @"250.5 25", @"END IONS", string.Empty));
            using var mgfFile = new MsDataFileImpl(mgfPath);
            Assert.AreEqual(2, mgfFile.SpectrumCount);
            var expectedMzs = new[] { new[] { 100.5, 200.5, 300.5 }, new[] { 150.5, 250.5 } };
            var expectedIntensities = new[] { new[] { 10.0, 20, 30 }, new[] { 15.0, 25 } };
            for (int repeat = 0; repeat < 3; repeat++)
            {
                for (int i = 0; i < expectedMzs.Length; i++)
                {
                    var spectrum = mgfFile.GetSpectrum(i);
                    CollectionAssert.AreEqual(expectedMzs[i], spectrum.Mzs);
                    CollectionAssert.AreEqual(expectedIntensities[i], spectrum.Intensities);
                }
            }
        }

        [TestMethod]
        public void TestDataFileConfigInfoMemoryLeak()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"TestData\DataFileMemoryLeakTest.zip");
            using var msDataFile = new MsDataFileImpl(TestFilesDir.GetTestPath("DissociationMethodTest.mzML"));
            for (int repeat = 0; repeat < 10000; repeat++)
            {
                var configInfo = msDataFile.ConfigInfo;
                Assert.IsNotNull(configInfo);
            }
        }
    }
}
