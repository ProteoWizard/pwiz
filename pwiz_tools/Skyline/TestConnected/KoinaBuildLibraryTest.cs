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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Koina.Models;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestConnected
{
    [TestClass]
    public class KoinaBuildLibraryTest : AbstractFunctionalTest
    {
        [TestMethod, NoParallelTesting(TestExclusionReason.DOCKER_ROOT_CERTS)]
        public void TestKoinaBuildLibrary()
        {
            if (!HasKoinaServer())
            {
                return;
            }
            RunFunctionalTest();
        }

        private string LibraryPathWithoutIrt => TestContext.GetTestPath("TestKoinaBuildLibrary\\LibraryWithoutIrt.blib");
        private string LibraryPathWithIrt => TestContext.GetTestPath("TestKoinaBuildLibrary\\LibraryWithIrt.blib");

        protected override void DoTest()
        {
            var toolsOptionsUi = ShowDialog<ToolOptionsUI>(SkylineWindow.ShowToolOptionsUI);
            RunUI(()=>
            {
                toolsOptionsUi.NavigateToTab(ToolOptionsUI.TABS.Koina);
                toolsOptionsUi.KoinaIntensityModelCombo = KoinaIntensityModel.Models.First();
                toolsOptionsUi.KoinaRetentionTimeModelCombo = KoinaRetentionTimeModel.Models.First();
            });
            OkDialog(toolsOptionsUi, toolsOptionsUi.OkDialog);
            var peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Modifications);
            const string OXIDATION_M = "Oxidation (M)";
            AddStaticMod(OXIDATION_M, peptideSettings);
            RunUI(() =>
            {
                peptideSettings.PickedStaticMods = peptideSettings.PickedStaticMods.Union(new[] { OXIDATION_M }).ToArray();
                peptideSettings.OkDialog();
            });
            RunDlg<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg, pasteDlg =>
            {
                SetClipboardText("ELVIS\r\nLIVES\r\nFISAM[Oxidation (M)]LPC[+57.02146]NKFC[+57.02146]K");
                pasteDlg.PastePeptides();
                pasteDlg.OkDialog();
            });
            peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);
            const string libraryWithoutIrt = "LibraryWithoutIrt";
            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = libraryWithoutIrt;
                buildLibraryDlg.LibraryPath = LibraryPathWithoutIrt;
                buildLibraryDlg.Koina = true;
            });
            OkDialog(buildLibraryDlg, buildLibraryDlg.OkWizardPage);

            buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);
            const string libraryWithIrt = "LibraryWithIrt";
            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = libraryWithIrt;
                buildLibraryDlg.LibraryPath = LibraryPathWithIrt;
                buildLibraryDlg.Koina = true;
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

        protected override void Cleanup()
        {
            DirectoryEx.SafeDelete("TestKoinaBuildLibrary");
        }
    }
}
