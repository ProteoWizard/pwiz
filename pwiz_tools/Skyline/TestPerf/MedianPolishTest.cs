using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Databinding;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    [TestClass]
    public class MedianPolishTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestMedianPolish()
        {
            TestFilesZip = @"TestPerf\MedianPolishTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Plasma-MagNet-Elute-Small.sky")));
            WaitForDocumentLoaded();
            RunDlg<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog, exportLiveReportDlg =>
            {
                exportLiveReportDlg.ReportName = "Protein Abundances";
                exportLiveReportDlg.SetUseInvariantLanguage(true);
                exportLiveReportDlg.OkDialog(TestFilesDir.GetTestPath("ProteinAbundances-Plasma-MagNet-Elute-Small.parquet"));
            });
            RunDlg<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog, exportLiveReportDlg =>
            {
                exportLiveReportDlg.ReportName = "PRISM";
                exportLiveReportDlg.SetUseInvariantLanguage(true);
                exportLiveReportDlg.OkDialog(TestFilesDir.GetTestPath("PRISM-Plasma-MagNet-Elute-Small.parquet"));
            });
        }
    }
}
