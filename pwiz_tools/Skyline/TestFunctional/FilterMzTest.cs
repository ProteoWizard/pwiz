/*
 * Original author: Brian Pratt <bspratt@proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class FilterMzTest : AbstractFunctionalTestEx
    {

        /// <summary>
        /// Verify that mz range setting overrides the AutoManageChildren property and will remove nodes
        /// with mz outside the range.
        /// </summary>
        /// 
        [TestMethod]
        public void FilterMzRangeTest()
        {
            TestFilesZip = @"TestFunctional\FilterMzTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Load a doc with two peptides and all possible transitions switched on, and thus in AutoManageChildren=false mode
            var docName = TestFilesDir.GetTestPath("FilterMzTest.sky");
            RunUI(() => SkylineWindow.OpenFile(docName));
            var doc = WaitForDocumentLoaded();
            AssertEx.IsDocumentState(doc, null, 1, 2, 24, 702);

            // Change instrument setting to min Mz 1040, should see the first peptide "PEPTIDER" (mz=956 at z=1) empty out altogether,
            // and the second "IIDDEERR" (mz=1045 at z=1) reduced to just precursor transitions
            var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Instrument;
                transitionSettingsUI.MinMz = 1040; 
            });
            OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
            doc = WaitForDocumentChange(doc);

            // Formerly this would result in 8 transitions, not 6, because the iTraq transtions (mz=114.1107) were not filtered out
            AssertEx.IsDocumentState(doc, null, 1, 2, 2, 6);

            RunUI(() => { SkylineWindow.Undo(); });
            doc = WaitForDocumentChange(doc);

            // Change instrument setting to min Mz 400, should see many transitions removed
            // Formerly these were remaining in place since AutoManageChildren wasn't enabled
            var transitionSettingsUI2 = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                transitionSettingsUI2.SelectedTab = TransitionSettingsUI.TABS.Instrument;
                transitionSettingsUI2.MinMz = 400;
            });
            OkDialog(transitionSettingsUI2, transitionSettingsUI2.OkDialog);
            doc = WaitForDocumentChange(doc);

            AssertEx.IsDocumentState(doc, null, 1, 2, 8, 104);
        }
    }
}
