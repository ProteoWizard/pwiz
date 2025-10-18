/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using pwiz.Skyline.Alerts;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;
using pwiz.Skyline.Properties;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class CarafeEmptyNameTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestCarafeEmptyName()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestEmptyNameMessage();
            TestEmptyPathMessage();
        }

        private void TestEmptyPathMessage()
        {
            var peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);

            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = "No peptides prediction";
                buildLibraryDlg.Carafe = true;
            });

            RunDlg<MessageDlg>(buildLibraryDlg.OkWizardPage, dlg =>
            {
                Assert.AreEqual(SettingsUIResources.BuildLibraryDlg_ValidateBuilder_You_must_specify_an_output_file_path,
                    dlg.Message);
                dlg.OkDialog();
            });

            OkDialog(buildLibraryDlg, buildLibraryDlg.CancelDialog);
            OkDialog(peptideSettings, peptideSettings.OkDialog);
        }
        private void TestEmptyNameMessage()
        {
            var peptideSettings = ShowPeptideSettings(PeptideSettingsUI.TABS.Library);
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);

            RunUI(() =>
            {
                buildLibraryDlg.Carafe = true;
            });

            RunDlg<AlertDlg>(buildLibraryDlg.OkWizardPage, dlg =>
            {
                Assert.AreEqual(string.Format(Resources.MessageBoxHelper_ValidateNameTextBox__0__cannot_be_empty, "Name"),
                    dlg.Message);
                dlg.OkDialog();
            });

            OkDialog(buildLibraryDlg, buildLibraryDlg.CancelDialog);
            OkDialog(peptideSettings, peptideSettings.OkDialog);
        }
    }
}
