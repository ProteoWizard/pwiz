/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using pwiz.Skyline.FileUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ManageResultsUndoTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestManageResultsUndo()
        {
            TestFilesZip = @"TestFunctional\ManageResultsUndoTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ManageResultsUndoTest.sky")));
            WaitForDocumentLoaded();
            Assert.AreEqual(3, SkylineWindow.Document.MeasuredResults.Chromatograms.Count);
            var manageResultsDlg = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
            RunUI(() =>
            {
                manageResultsDlg.SelectedChromatograms = new[]
                    {SkylineWindow.Document.MeasuredResults.Chromatograms.Last()};
                manageResultsDlg.RemoveReplicates();
            });
            OkDialog(manageResultsDlg, manageResultsDlg.OkDialog);
            RunUI(() => SkylineWindow.SaveDocument());
            Assert.AreEqual(2, SkylineWindow.Document.MeasuredResults.Chromatograms.Count);
            RunUI(()=>SkylineWindow.Undo());
            Assert.AreEqual(3, SkylineWindow.Document.MeasuredResults.Chromatograms.Count);
            WaitForDocumentLoaded();
        }
    }
}
