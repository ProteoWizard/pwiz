/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using pwiz.Common.Chemistry;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Verify correct handling of negative ions in DIA.
    /// </summary>
    [TestClass]
    public class PerfImportNegativeIonDIATest : AbstractFunctionalTest
    {

        [TestMethod]
        public void NegativeIonDIATest()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestFilesZip = GetPerfTestDataURL(@"PerfNegativeIonDIA.zip");
            TestFilesPersistent = new[] { "neg.mzML", "pos.mzML" }; // list of files that we'd like to unzip alongside parent zipFile, and (re)use in place
            var testFilesDir = new TestFilesDir(TestContext, TestFilesZip, null, TestFilesPersistent);

            RunUI(() => SkylineWindow.OpenFile(testFilesDir.GetTestPath("Test positive-negative.sky")));
            WaitForDocumentLoaded();

            ImportResultsFiles(TestFilesPersistent.Select(file => MsDataFileUri.Parse(testFilesDir.GetTestPath(file))));
            
            // Verify that pos ion has a chromatogram for pos replicate, and neg ion has one for neg replicate
            var doc = WaitForDocumentLoaded(400000);
            var results = doc.Settings.MeasuredResults;
            var negIndex = results.Chromatograms[0].Name.Equals("neg") ? 0 : 1;
            var posIndex = (negIndex ==1) ? 0 : 1;
            var tolerance = new MzTolerance(.005f);
            foreach (var pair in doc.MoleculePrecursorPairs)
            {
                var index = pair.NodeGroup.PrecursorCharge < 0 ? negIndex : posIndex;
                var message = "expected a chromatogram for precursor and for product in replicate \"" + results.Chromatograms[index].Name +"\"";
                ChromatogramGroupInfo[] chromGroupInfo;
                Assert.IsTrue(results.TryLoadChromatogram(index, pair.NodePep, pair.NodeGroup, tolerance, out chromGroupInfo), message);
                foreach (var chromGroup in chromGroupInfo)
                {
                    Assert.AreEqual(2, chromGroup.TransitionPointSets.Count(), message);
                }
            }
        }
    }
}