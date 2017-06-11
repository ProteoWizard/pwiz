/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Test speed of updates for Library Explorer in a document with many variable
    /// modifications and peptide matches with many variable modifications.
    /// </summary>
    [TestClass]
    public class LibraryExplorerSpeedTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestLibraryExplorerSpeed()
        {
            TestFilesZip = @"TestFunctional\LibraryExplorerSpeedTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("160706_SWATH_Histones_30W.sky")));
            WaitForDocumentLoaded();
            TestThreeLibraryPages(0, 0, 0);
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, dlg =>
            {
                dlg.MaxVariableMods = 3;
                dlg.OkDialog();
            });
            TestThreeLibraryPages(55, 9, 61);
        }

        private static void TestThreeLibraryPages(int unmatched1, int unmatched2, int unmatched3)
        {
            var libraryDlg = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            PrepareLibraryDlg(libraryDlg);
            RunUI(() => Assert.AreEqual(unmatched1, libraryDlg.UnmatchedPeptidesCount));
            PrepareLibraryDlg(libraryDlg, libraryDlg.NextPage);
            RunUI(() => Assert.AreEqual(unmatched2, libraryDlg.UnmatchedPeptidesCount));
            PrepareLibraryDlg(libraryDlg, libraryDlg.NextPage);
            RunUI(() => Assert.AreEqual(unmatched3, libraryDlg.UnmatchedPeptidesCount));
            OkDialog(libraryDlg, libraryDlg.Close);
        }

        private static void PrepareLibraryDlg(ViewLibraryDlg libraryDlg, Action prepareAction = null)
        {
            if (prepareAction != null)
                SkylineWindow.BeginInvoke(prepareAction);
            if (!TryWaitForCondition(2000, () => libraryDlg.IsUpdateComplete))
            {
                libraryDlg.IsUpdateCanceled = true;
                Assert.Fail("Unexpected long wait filling peptide list form");
            }
        }
    }
}