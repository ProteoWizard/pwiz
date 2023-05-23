/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class NewMascotDatFileTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestNewMascotDatFile()
        {
            TestFilesZip = @"TestFunctional\NewMascotDatFileTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            const string libraryName = "MascotLibrary";
            var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Library;
            });
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettingsUi.ShowBuildLibraryDlg);
            RunUI(()=>
            {
                buildLibraryDlg.LibraryName = libraryName;
                buildLibraryDlg.LibraryPath = TestFilesDir.GetTestPath("MascotLibrary.blib");
                buildLibraryDlg.OkWizardPage();
                buildLibraryDlg.InputFileNames = new[] {TestFilesDir.GetTestPath("SmallMascotDataFile.dat") };
            });
            WaitForConditionUI(() => buildLibraryDlg.Grid.ScoreTypesLoaded);
            RunUI(()=>buildLibraryDlg.Grid.SetScoreThreshold(1.0));
            var warning = ShowDialog<MultiButtonMsgDlg>(buildLibraryDlg.OkWizardPage);
            OkDialog(warning, warning.OkDialog);
            Assert.IsTrue(WaitForCondition(() => peptideSettingsUi.AvailableLibraries.Contains(libraryName)));
            RunUI(() => peptideSettingsUi.PickedLibraries = new[] { libraryName });
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
            WaitForDocumentLoaded();
            Assert.AreEqual(1, SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.Count);
            var library = SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.First();
            Assert.AreEqual(1, library.Keys.Count());
        }
    }
}
