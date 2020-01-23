using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class RescoreImportDocumentTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestRescoreImportDocument()
        {
            TestFilesZip = @"TestFunctional\RescoreImportDocumentTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("ImportTo.sky")));
            WaitForDocumentLoaded();
            var manageResultsDlg = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
            RunDlg<RescoreResultsDlg>(manageResultsDlg.Rescore, dlg => dlg.Rescore(false));
            WaitForDocumentLoaded();
            var importDocResultsDlg = ShowDialog<ImportDocResultsDlg>(() =>
                SkylineWindow.ImportFiles(TestFilesDir.GetTestPath("ImportFrom.sky")));
            RunUI(() =>
            {
                importDocResultsDlg.Action = MeasuredResults.MergeAction.add;
                importDocResultsDlg.IsMergePeptides = true;
            });
            OkDialog(importDocResultsDlg, importDocResultsDlg.OkDialog);
            WaitForDocumentLoaded();
        }
    }
}
