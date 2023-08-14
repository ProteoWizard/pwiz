/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that every transition in a particular Skyline document is properly 
    /// matched up to a chromatogram in the SRM result files.
    /// </summary>
    [TestClass]
    public class SrmSmMolChromTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSrmSmallMoleculeChromatograms()
        {
            TestFilesZip = @"TestFunctional\SrmSmMolChromTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var testPath = TestFilesDir.GetTestPath("SrmSmMolChromTest.sky");
            RunUI(() => SkylineWindow.OpenFile(testPath));
            ImportResultsFiles(new[]
            {
                new MsDataFilePath(TestFilesDir.GetTestPath("ID36310_01_WAA263_3771_030118" + ExtensionTestContext.ExtMz5)),
                new MsDataFilePath(TestFilesDir.GetTestPath("ID36311_01_WAA263_3771_030118" + ExtensionTestContext.ExtMz5))
            });
            VerifyAllTransitionsHaveChromatograms();

            // Now tinker with molecule details that don't change the molecule mass - should still be able to associate chromatograms
            RunUI(() => { SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].FirstNode; });
            var doc = SkylineWindow.Document;
            var editMoleculeDlg = ShowDialog<EditCustomMoleculeDlg>(SkylineWindow.ModifyPeptide);
            RunUI(() => { editMoleculeDlg.NameText = editMoleculeDlg.NameText + "_renamed"; }); // Change the name
            OkDialog(editMoleculeDlg, editMoleculeDlg.OkDialog);
            WaitForDocumentChangeLoaded(doc);
            VerifyAllTransitionsHaveChromatograms();

            // That info should survive serialization round trip
            RunUI(() => SkylineWindow.SaveDocument(testPath));
            RunUI(() => SkylineWindow.NewDocument());
            RunUI(() => SkylineWindow.OpenFile(testPath));
            VerifyAllTransitionsHaveChromatograms();
        }

    }
}
