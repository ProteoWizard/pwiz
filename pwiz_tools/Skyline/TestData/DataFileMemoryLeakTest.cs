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
