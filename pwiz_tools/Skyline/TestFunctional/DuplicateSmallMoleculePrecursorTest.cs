/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests a few things that might go wrong in a document where a molecule has multiple identical precursors
    /// </summary>
    [TestClass]
    public class DuplicateSmallMoleculePrecursorTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestDuplicateSmallMoleculePrecursors()
        {
            TestFilesZip = @"TestFunctional\DuplicateSmallMoleculePrecursorTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Skyline_test.sky"));
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0);
            });

            RunDlg<PopupPickList>(SkylineWindow.ShowPickChildrenInTest, popupPickList =>
            {
                Assert.AreEqual(4, popupPickList.ItemNames.Count());
                popupPickList.OnOk();
            });

            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUi =>
            {
                transitionSettingsUi.SelectedTab = TransitionSettingsUI.TABS.Filter;
                transitionSettingsUi.SmallMoleculePrecursorAdducts = "[M-H]";
                transitionSettingsUi.OkDialog();
            });
            Assert.AreEqual(4, SkylineWindow.Document.Molecules.First().TransitionGroupCount);

            // Now test fix for "an item with the same key has already been added" as in https://skyline.ms/announcements/home/support/thread.view?rowId=51494
            LoadNewDocument(true);
            RunUI(() => { SkylineWindow.OpenFile(TestFilesDir.GetTestPath("402.sky")); });
            var mzML = TestFilesDir.GetTestPath("402.mzML");
            ImportResultsFile(mzML);
            WaitForDocumentLoaded(240000);
            // If we get here without an exception, problem is solved
        }
    }
}
