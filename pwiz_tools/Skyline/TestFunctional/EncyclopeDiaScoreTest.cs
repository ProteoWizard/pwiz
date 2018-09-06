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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class EncyclopeDiaScoreTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestEncyclopeDiaScore()
        {
            TestFilesZip = @"TestFunctional\EncyclopeDiaScoreTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            const string libName = "elibtest";
            var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Library);
            var libListDlg =
                ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsUi.EditLibraryList);
            var addLibDlg = ShowDialog<EditLibraryDlg>(libListDlg.AddItem);
            RunUI(() =>
            {
                addLibDlg.LibraryName = libName;
                addLibDlg.LibraryPath = TestFilesDir.GetTestPath("elibtest.elib");
            });
            OkDialog(addLibDlg, addLibDlg.OkDialog);
            OkDialog(libListDlg, libListDlg.OkDialog);
            RunUI(() => peptideSettingsUi.PickedLibraries = new[] { libName });
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
            WaitForDocumentLoaded();
            var libraryViewer = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            var filterMatchedPeptidesDlg = ShowDialog<FilterMatchedPeptidesDlg>(libraryViewer.AddAllPeptides);
            OkDialog(filterMatchedPeptidesDlg, filterMatchedPeptidesDlg.OkDialog);
            var confirmationMessage = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(confirmationMessage, confirmationMessage.OkDialog);
            Assert.AreEqual(23, SkylineWindow.Document.PeptideCount);
            OkDialog(libraryViewer, libraryViewer.Close);

            var transitionSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                transitionSettings.FragmentTypes = "p";
                transitionSettings.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Count;
            });
            OkDialog(transitionSettings, transitionSettings.OkDialog);

            RunUI(()=>SkylineWindow.SaveDocument(TestFilesDir.GetTestPath("elibscoretest.sky")));

            ImportResults(TestFilesDir.GetTestPath("30May2018-Lumos-DIA-ind-12mz-400to1000-HumanAD-COP-01" + ExtensionTestContext.ExtMz5));
            WaitForDocumentLoaded();
            var scores = SkylineWindow.Document.MoleculeTransitionGroups
                .Where(tg => null != tg.Results)
                .SelectMany(tg => tg.Results.SelectMany(r => r.Select(chromInfo => chromInfo.QValue)))
                .Where(score => null != score)
                .ToArray();
            Assert.AreNotEqual(0, scores.Length);
        }
    }
}
