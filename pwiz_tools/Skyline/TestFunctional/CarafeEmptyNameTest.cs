using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
                Assert.AreEqual(String.Format(Resources.MessageBoxHelper_ValidateNameTextBox__0__cannot_be_empty, "Name"),
                    dlg.Message);
                dlg.OkDialog();
            });

            OkDialog(buildLibraryDlg, buildLibraryDlg.CancelDialog);
            OkDialog(peptideSettings, peptideSettings.OkDialog);
        }

    }
}
