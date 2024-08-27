/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Util;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Verify reading of electron ionization data as high energy only all ions data.
    /// </summary>
    [TestClass]
    public class ElectronIonizationAllIonsTest : AbstractFunctionalTestEx
    {

        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)] 
        public void ElectronIonizationAllIonsPerfTest()
        {
            TestFilesZip = GetPerfTestDataURL(@"PerfElectronIonizationAllIonsTest.zip");
            TestFilesPersistent = new[] { "GC_00343_500ng_62std2nd.raw" }; // List of files that we'd like to unzip alongside parent zipFile, and (re)use in place

            RunFunctionalTest();
            
        }

        protected override void DoTest()
        {
            var skyfile = TestFilesDir.GetTestPath("test.sky"); // Has Full Scan settings good to go
            OpenDocument(skyfile);
            var doc0 = PasteSmallMoleculeListNoAutoManage(TestFilesDir.GetTestPath("Testing_list_for_skyline.csv"));  // Paste into targets window, say no to the offer to set new nodes to automanage

            // Enable automanage children so settings change will act on doc structure
            RunDlg<RefineDlg>(SkylineWindow.ShowRefineDlg, refineDlg =>
            {
                refineDlg.AutoPrecursors = true;
                refineDlg.AutoTransitions = true;
                refineDlg.OkDialog();
            });
            WaitForDocumentChange(doc0);

            // Enable fragments only
            RunUI(() => SkylineWindow.ModifyDocument("fragments only", doc => doc.ChangeSettings(doc.Settings.ChangeTransitionFilter(f =>
                f.ChangeSmallMoleculeIonTypes(new[] { IonType.custom })))));
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 2, 9, 9, 30);
            
            // Enable precursors too
            RunUI(() => SkylineWindow.ModifyDocument("fragments and precursors", doc => doc.ChangeSettings(doc.Settings.ChangeTransitionFilter(f =>
                f.ChangeSmallMoleculeIonTypes(new[] { IonType.custom, IonType.precursor })))));
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 2, 9, 9, 39);

            ImportResults(TestFilesDir.GetTestPath("GC_00343_500ng_62std2nd.raw"));
            // If data has not been properly understood as high energy all-ions (despite being MS1), some peaks won't be found
            foreach (var peak in from tg in SkylineWindow.Document.MoleculeTransitions
                from r in tg.Results
                from p in r
                select p)
            {
                Assume.IsTrue((peak.PointsAcrossPeak ?? 0) > 20); // Without proper logic, points across peak is about half this since we skip every other spectrum as being low energy
                Assume.IsTrue(peak.Rank > 0);
                Assume.IsTrue(peak.Area > 0);
            }
        }

    }
}
