/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
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

using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

// Tests the handling of EI data, in which all data is actually MS1,
// but it's all fragmented, so we treat it as MS2

namespace pwiz.SkylineTestData
{
    [TestClass]
    public class CommandLineEiTest : AbstractUnitTestEx
    {
        private const string ZIP_FILE = @"TestData\CommandLineEiTest.zip";


        [TestMethod]
        public void ConsoleImportEiTest()
        {
            TestFilesDir = new TestFilesDir(TestContext, ZIP_FILE);
            var blankDocPath = TestFilesDir.GetTestPath("blank.sky");
            var transitionsListPath = TestFilesDir.GetTestPath("TransitionList.csv");
            var eiSettingsDocPath = TestFilesDir.GetTestPath("import-ei-settings.sky");
            var eiDocPath = TestFilesDir.GetTestPath("import-ei.sky");
            var eiLoadedDocPath = TestFilesDir.GetTestPath("import-ei-loaded.sky");

            var output = RunCommand("--new=" + eiDocPath,
                "--full-scan-acquisition-method=EI",  // Treat MS1 data as all-ions-fragmentation MS2 results
                "--full-scan-product-analyzer=tof",
                "--full-scan-product-res=30000",
                "--full-scan-rt-filter=scheduling_windows",
                "--full-scan-rt-filter-tolerance=5",
                "--tran-product-ion-charges=1",
                "--instrument-min-mz=42",
                "--instrument-max-mz=2000",
                "--instrument-dynamic-min-mz",
                "--instrument-method-mz-tolerance=" + 0.42,
                "--integrate-all",
                "--out=" + eiDocPath,
                "--import-transition-list=" + transitionsListPath);
            AssertEx.Contains(output, string.Format(Resources.CommandLine_ImportTransitionList_Importing_transiton_list__0____,
                Path.GetFileName(transitionsListPath))); 
            var doc = ResultsUtil.DeserializeDocument(eiDocPath);
            Assert.AreEqual(1, doc.MoleculeGroupCount);
            Assert.AreEqual(4, doc.MoleculeCount);

            var msg = RunCommand("--in=" + eiDocPath,
                "--import-all-files=" + TestFilesDir.FullPath,
                "--out=" + eiLoadedDocPath);
            doc = ResultsUtil.DeserializeDocument(eiLoadedDocPath);
            Assert.AreEqual(16, doc.MoleculeTransitions.Count());
            Assert.AreEqual(14, doc.MoleculeTransitions.Count(t => t.Results[0][0].Area > 0));
            AssertEx.Serializable(doc); // Verifies consistency with current Skyline schema
        }

    }
}
