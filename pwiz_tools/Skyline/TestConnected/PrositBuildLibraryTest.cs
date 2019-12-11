/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Prosit.Models;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestConnected
{
    [TestClass]
    public class PrositBuildLibraryTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPrositBuildLibrary()
        {
            if (!PrositConfigTest.HasPrositServer())
            {
                return;
            }
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var toolsOptionsUi = ShowDialog<ToolOptionsUI>(SkylineWindow.ShowToolOptionsUI);
            RunUI(()=>
            {
                toolsOptionsUi.NavigateToTab(ToolOptionsUI.TABS.Prosit);
                toolsOptionsUi.PrositIntensityModelCombo = PrositIntensityModel.Models.First();
                toolsOptionsUi.PrositRetentionTimeModelCombo = PrositRetentionTimeModel.Models.First();
            });
            OkDialog(toolsOptionsUi, toolsOptionsUi.OkDialog);
            RunDlg<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg, pasteDlg =>
            {
                SetClipboardText("ELVIS\r\nLIVES");
                pasteDlg.PastePeptides();
                pasteDlg.OkDialog();
            });
            var peptideSettings = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() =>
            {
                peptideSettings.SelectedTab = PeptideSettingsUI.TABS.Library;
            });

            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);
            const string libraryWithoutIrt = "LibraryWithoutIrt";
            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = libraryWithoutIrt;
                buildLibraryDlg.LibraryPath = Path.Combine(TestContext.TestDir, "LibraryWithoutIrt.blib");
                buildLibraryDlg.Prosit = true;
            });
            OkDialog(buildLibraryDlg, buildLibraryDlg.OkWizardPage);

            buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);
            const string libraryWithIrt = "LibraryWithIrt";
            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = libraryWithIrt;
                buildLibraryDlg.LibraryPath = Path.Combine(TestContext.TestDir, "LibraryWithIrt.blib");
                buildLibraryDlg.Prosit = true;
                buildLibraryDlg.IrtStandard = IrtStandard.PIERCE;
            });
            OkDialog(buildLibraryDlg, buildLibraryDlg.OkWizardPage);
            var addIrtPeptidesDlg = WaitForOpenForm<AddIrtPeptidesDlg>();
            OkDialog(addIrtPeptidesDlg, addIrtPeptidesDlg.OkDialog);
            var addRetentionTimePredictorDlg = WaitForOpenForm<AddRetentionTimePredictorDlg>();
            OkDialog(addRetentionTimePredictorDlg, addRetentionTimePredictorDlg.NoDialog);
            OkDialog(peptideSettings, peptideSettings.OkDialog);

            var spectralLibraryViewer = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            RunUI(() =>
            {
                spectralLibraryViewer.ChangeSelectedLibrary(libraryWithoutIrt);
                spectralLibraryViewer.ChangeSelectedLibrary(libraryWithIrt);
            });
            OkDialog(spectralLibraryViewer, spectralLibraryViewer.Close);
        }
    }
}
