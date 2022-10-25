/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.DocSettings;
using pwiz.SkylineTestUtil;
using System.Linq;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Verify use of high energy ion mobility offset values for PRM data - formerly we used it only for All Ions.
    /// </summary>
    [TestClass]
    public class HighEnergyIonMobilityOffsetForPRMTest : AbstractFunctionalTestEx
    {

        [TestMethod] 
        public void TestHighEnergyIonMobilityOffsetForPRM()
        {

            TestFilesZip = GetPerfTestDataURL(@"PerfTestPrmDtOffset.zip");

            RunFunctionalTest();
            
        }

        protected override void DoTest()
        {
            string skyfile = TestFilesDir.GetTestPath("prm_dt_offset.sky");

            RunUI(() => SkylineWindow.OpenFile(skyfile));

            var doc = WaitForDocumentLoaded();
            AssertEx.IsDocumentState(doc, null, 1, 1, 1, 2);
            ImportResults("data.mzML");
            doc = SkylineWindow.Document;

            // Verify that this is PRM data
            AssertEx.AreEqual(FullScanAcquisitionMethod.PRM, doc.Settings.TransitionSettings.FullScan.AcquisitionMethod);

            // Verify that a high energy ion mobility offset was applied for chromatogram extraction
            var peptide = doc.Molecules.First();
            var precursor = peptide.TransitionGroups.First();
            var transitionGroupChromInfo = precursor.Results[0].First();
            // Without the fix, MS1 and MSMS drift times would be identical
            AssertEx.AreEqual(22.1, transitionGroupChromInfo.IonMobilityInfo.DriftTimeMS1, .0001);
            AssertEx.AreEqual(transitionGroupChromInfo.IonMobilityInfo.DriftTimeMS1 - .2, transitionGroupChromInfo.IonMobilityInfo.DriftTimeFragment, .0001);

        }
    }
}
