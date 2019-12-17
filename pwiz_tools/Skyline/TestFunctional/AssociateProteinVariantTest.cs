using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
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
    public class AssociateProteinVariantTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestAssociateProteinVariant()
        {
            TestFilesZip = @"TestFunctional\AssociateProteinVariantTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Filter;
                peptideSettingsUi.TextExcludeAAs = 0;
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Library;
            });
            var libListDlg =
                ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsUi.EditLibraryList);
            var addLibDlg = ShowDialog<EditLibraryDlg>(libListDlg.AddItem);
            RunUI(() =>
            {
                addLibDlg.LibraryName = "MyLibrary";
                addLibDlg.LibraryPath = TestFilesDir.GetTestPath("peptides.blib");
            });
            OkDialog(addLibDlg, addLibDlg.OkDialog);
            OkDialog(libListDlg, libListDlg.OkDialog);
            RunUI(()=>
            {
                peptideSettingsUi.SetLibraryChecked(0, true);
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Digest;
            });
            var buildBackgroundProteomeDlg = ShowDialog<BuildBackgroundProteomeDlg>(
                peptideSettingsUi.ShowBuildBackgroundProteomeDlg);
            RunUI(() =>
            {
                buildBackgroundProteomeDlg.BackgroundProteomeName = "MyBackgroundProteome";
                buildBackgroundProteomeDlg.BackgroundProteomePath = TestFilesDir.GetTestPath("MyBackgroundProteome.protdb");
                buildBackgroundProteomeDlg.AddFastaFile(TestFilesDir.GetTestPath("PEPTIDEC.fasta"));
            });
            OkDialog(buildBackgroundProteomeDlg, buildBackgroundProteomeDlg.OkDialog);
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
            WaitForDocumentLoaded();
            SkylineWindow.ModifyDocumentNoUndo(doc =>
                doc.ChangeSettings(doc.Settings.ChangePeptideSettings(
                    doc.Settings.PeptideSettings.ChangeLibraries(
                        doc.Settings.PeptideSettings.Libraries.ChangePick(PeptidePick.either)))));
            SetClipboardTextUI(">MyProtein\r\nKAAAPEPTIDEAAAKKK");
            RunUI(()=>SkylineWindow.Paste());
            var libraryViewer = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            RunUI(() =>
            {
                libraryViewer.AssociateMatchingProteins = true;
            });
            var alertDlg = ShowDialog<AlertDlg>(libraryViewer.AddAllPeptides);
            OkDialog(alertDlg, ()=>alertDlg.DialogResult = DialogResult.Yes);
            PauseTest();
        }
    }
}
