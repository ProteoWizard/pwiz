using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    [TestClass]
    public class TricPeakPickingTest : AbstractFunctionalTest
    {

        [TestMethod]
        public void TestTricPeakPicking()
        {
            IsPauseForScreenShots = true;
            RunPerfTests = true;
            TestFilesPersistent = new[] { "." };  // All persistent. No saving
            TestFilesZipPaths = new[] { @"http://proteome.gs.washington.edu/software/test/skyline-perf/TricPeakPickingTest.zip" };
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            string outDir = Path.Combine(TestContext.TestDir, "PerfPeakTest");
            Directory.CreateDirectory(outDir); // In case it doesn't already exists


            var fileUrl = TestFilesZipPaths[0];
            var directoryName = Path.GetFileNameWithoutExtension(fileUrl);
            var testFilesDir = new TestFilesDir(TestContext, fileUrl, null, new[] { directoryName });
            var directoryPath = testFilesDir.GetTestPath(directoryName);
            var skylineName = "HannesTRIC-Retry_12" + SrmDocument.EXT;
            var skylineDoc = Path.Combine(directoryPath, skylineName);

            
            RunUI(() => SkylineWindow.OpenFile(skylineDoc));
            var docLoaded = WaitForDocumentLoaded();
            
            //Create mprophet model without retention time squared score
            var reintegrateDlg = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
            RunUI(() =>
            {
                reintegrateDlg.UseTric = true;
                reintegrateDlg.OverwriteManual = true;
                reintegrateDlg.ReintegrateAll = true;
            });
            MProphetPeakScoringModel model;
            RunDlg<EditPeakScoringModelDlg>(reintegrateDlg.AddPeakScoringModel, dlg =>
            {
                dlg.UsesDecoys = true;
                dlg.UsesSecondBest = false;
                dlg.PeakScoringModelName = "TestScoringModel";
                //Disable squared retention time.
                dlg.PeakCalculatorsGrid.Items.First(p => p.Name.Contains("Retention time difference squared")).IsEnabled
                    = false;
                dlg.TrainModelClick();
                model = dlg.PeakScoringModel as MProphetPeakScoringModel;
                dlg.OkDialog();
            });

            

            //Run TRIC
            OkDialog(reintegrateDlg,reintegrateDlg.OkDialog);
            
            
            //Export report
            var tricBoundariesPath = Path.Combine(directoryPath, "tricBoundaries.csv");
            
            ExportPeakBoundaries(tricBoundariesPath);
            
            //Run reintegrate without alignment
            reintegrateDlg = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
            RunUI(() =>
            {
                reintegrateDlg.UseTric = false;
                reintegrateDlg.OverwriteManual = true;
                reintegrateDlg.ReintegrateAll = true;
                reintegrateDlg.ComboPeakScoringModelSelected = "TestScoringModel";
            });

            OkDialog(reintegrateDlg, reintegrateDlg.OkDialog);

            //Export report
            
            var noAlignBoundariesPath = Path.Combine(directoryPath, "noalignBoundaries.csv");
            
            ExportPeakBoundaries(noAlignBoundariesPath);
            
            //Open gold document
            RunUI(()=>SkylineWindow.OpenFile(Path.Combine(directoryPath, 
                "SkylineResult500Peptides-corrected-12.sky")));
            var goldDoc = WaitForDocumentLoaded();


            //Compare peaks picked 

            //Add open swath results as well
            var openSwathTricPath = Path.Combine(directoryPath,
                "result_all_target1pcnt_localMST_10pcnt_lld-Mods-Files-Apex.csv");
            var openSwathNoAlignPath = Path.Combine(directoryPath, "noalign_all_1pcnt-Mods-Files-Apex.csv");

            var comparePeaksDlg = ShowDialog<ComparePeakPickingDlg>(SkylineWindow.ShowCompareModelsDlg);
            AddPeakBoundariesFile(comparePeaksDlg, tricBoundariesPath,"tric",2);
            AddPeakBoundariesFile(comparePeaksDlg, noAlignBoundariesPath, "noalign",2);
            AddPeakBoundariesFile(comparePeaksDlg,openSwathTricPath,"open swath tric",3);
            AddPeakBoundariesFile(comparePeaksDlg,openSwathNoAlignPath,"open swath no align",3);

            var noAlignCompare =
                comparePeaksDlg.ComparePeakBoundariesList.
                    First(comp => comp.FilePath.Equals(noAlignBoundariesPath));
            var tricCompare =
                comparePeaksDlg.ComparePeakBoundariesList.
                    First(comp => comp.FilePath.Equals(tricBoundariesPath));
            var openSwathTricCompare =
                comparePeaksDlg.ComparePeakBoundariesList.
                    First(comp => comp.FilePath.Equals(openSwathTricPath));
            var openSwathNoAlignCompare =
                comparePeaksDlg.ComparePeakBoundariesList.
                    First(comp => comp.FilePath.Equals(openSwathNoAlignPath));

            //Look at total correct ids at 1% observed FDR
            Assert.AreEqual(comparePeaksDlg.GetYAtCutoffRoc(tricCompare),3932.0);
            Assert.AreEqual(comparePeaksDlg.GetYAtCutoffRoc(noAlignCompare),3735);
            Assert.AreEqual(comparePeaksDlg.GetYAtCutoffRoc(openSwathTricCompare), 3983);
            Assert.AreEqual(comparePeaksDlg.GetYAtCutoffRoc(openSwathNoAlignCompare), 3719);

            //Look at observerd fdr at expected 1% FDR
            Assert.AreEqual(comparePeaksDlg.GetYAtCutoffQQ(tricCompare),0.002,0.0001);
            Assert.AreEqual(comparePeaksDlg.GetYAtCutoffQQ(noAlignCompare),0.0049,0.0001);
            Assert.AreEqual(comparePeaksDlg.GetYAtCutoffQQ(openSwathTricCompare),0.0057,0.0001);
            Assert.AreEqual(comparePeaksDlg.GetYAtCutoffQQ(openSwathNoAlignCompare),0.0155,0.0001);
            
            
            OkDialog(comparePeaksDlg,comparePeaksDlg.OkDialog);
        }

        private static void AddPeakBoundariesFile(ComparePeakPickingDlg comparePeaksDlg, string path, string name, int warnings)
        {
            var addPeakCompareDlg = ShowDialog<AddPeakCompareDlg>(comparePeaksDlg.Add);
            RunUI(() =>
            {
                addPeakCompareDlg.IsModel = false;
                addPeakCompareDlg.FileName = name;
                addPeakCompareDlg.FilePath = path;
            });
            //Skip through warnings
            ShowDialog<AlertDlg>(
                addPeakCompareDlg.OkDialog);
            for (int i = 0; i < warnings; i++)
            {
                var warningDialogue = WaitForOpenForm<AlertDlg>();
                OkDialog(warningDialogue, warningDialogue.OkDialog);
            }
            WaitForClosedForm(addPeakCompareDlg);
        }

        private static void ExportPeakBoundaries(string path)
        {
            //Remove if exists
            File.Delete(path);



            var exportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);


            //Make sure has report template
            var editDlg = ShowDialog<ManageViewsForm>(exportDlg.EditList);
            RunUI(() =>
            {
                editDlg.ImportViews(Path.Combine(Path.GetDirectoryName(path), "PrecursorBoundaries.skyr"));
            });
            OkDialog(editDlg, editDlg.OkDialog);

            RunUI(() =>
            {
                exportDlg.ReportName = "Precursor Boundaries";
            });
            OkDialog(exportDlg, () => exportDlg.OkDialog(path, ','));
        }
    }
}
