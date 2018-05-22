using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.Skyline.Model.Results.RemoteApi;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class UnifiFunctionalTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestUnifi()
        {
            if (!UnifiTestUtil.EnableUnifiTests)
            {
                return;
            }
            TestFilesZip = @"TestFunctional\UnifiFunctionalTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("test.sky")));
            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            var openDataSourceDialog = ShowDialog<OpenDataSourceDialog>(importResultsDlg.OkDialog);
            var editAccountDlg = ShowDialog<EditRemoteAccountDlg>(() => openDataSourceDialog.CurrentDirectory = RemoteUrl.EMPTY);
            RunUI(()=>editAccountDlg.SetRemoteAccount(UnifiTestUtil.GetTestAccount()));
            OkDialog(editAccountDlg, editAccountDlg.OkDialog);
            OpenFile(openDataSourceDialog, "Company");
            OpenFile(openDataSourceDialog, "Test Data");
            OpenFile(openDataSourceDialog, "HDMSe");
            OpenFile(openDataSourceDialog, "250 fmol Hi3 E coli peptides 3-6 min");
            var lockMassDlg = WaitForOpenForm<ImportResultsLockMassDlg>();
            OkDialog(lockMassDlg, lockMassDlg.OkDialog);
            // It takes a really long time to extract chromatograms, so we let it run for 5 seconds and then open a file where it's already imported
            WaitForDocumentLoaded(5000);
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("test_imported.sky")));
            WaitForDocumentLoaded();
            RunUI(() => SkylineWindow.SelectElement(ElementRefs.FromObjectReference(ElementLocator.Parse("Molecule:/sp|P0A6A8|ACP_ECOLI/ITTVQAAIDYINGHQA"))));
            ClickChromatogram(4.0, 100);
            GraphFullScan graphFullScan = FindOpenForm<GraphFullScan>();
            Assert.IsNotNull(graphFullScan);
        }

        private void OpenFile(OpenDataSourceDialog openDataSourceDialog, string name)
        {
            WaitForConditionUI(() => openDataSourceDialog.ListItemNames.Contains(name));
            RunUI(()=>
            {
                openDataSourceDialog.SelectFile(name);
                openDataSourceDialog.Open();
            });
            
        }
    }
}
